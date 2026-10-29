using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Ccusage.Mcp.Engine;

namespace Ccusage.Mcp.Tests.Engine;

[TestClass]
public sealed class EngineRunnerTests
{
    // Environment is injected through EngineInvocation.Environment (never process-wide: MSTest runs methods in parallel).
    // Every run has its own short engine timeout, so a broken runner fails a test instead of hanging the suite.
    private static Task<EngineResult> Run(
        string[] argv,
        string? stdin = null,
        int timeoutSeconds = 10,
        Dictionary<string, string?>? env = null,
        CancellationToken ct = default) =>
        new EngineRunner(TimeSpan.FromSeconds(timeoutSeconds), _ => { })
            .RunAsync(new(TestPaths.FakeEnginePath, argv, stdin, env), ct);

    private static JsonElement Echo(EngineResult result) => JsonDocument.Parse(result.Stdout).RootElement;

    // A FakeEngine run with --fake-child: the engine plus a hanging child that inherits its stdout pipe, so only a
    // whole-tree kill ends the run. FakeEngine writes "<engine pid>\n<child pid>" to the pid file before its stdout.
    // Dispose kills any survivor, so a failed assertion can never leak a process that holds the test host's pipes.
    private sealed class FakeTree : IDisposable
    {
        private readonly string pidFile = Path.Combine(Directory.CreateTempSubdirectory().FullName, "pids");

        public string[] Argv => ["daily", "--fake-child", $"--fake-pid-file={pidFile}", "--fake-hang"];

        public bool Started => File.Exists(pidFile);

        public async Task WaitForPidsAsync()
        {
            var sw = Stopwatch.StartNew();
            while (!File.Exists(pidFile))
            {
                if (sw.Elapsed > TimeSpan.FromSeconds(10))
                {
                    Assert.Fail("FakeEngine never wrote its pid file.");
                }

                await Task.Delay(20);
            }
        }

        public void AssertKilled()
        {
            var survivors = Survivors(waitForExit: TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, survivors.Count, $"survived the engine run: {string.Join(", ", survivors)}");
        }

        public void Dispose()
        {
            foreach (var pid in Survivors(waitForExit: TimeSpan.Zero))
            {
                using var process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
            }
        }

        private int[] Pids()
        {
            Assert.IsTrue(File.Exists(pidFile), "FakeEngine never wrote its pid file.");
            var pids = File.ReadAllText(pidFile).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            Assert.AreEqual(2, pids.Length, "expected the engine pid and its child pid");
            return pids;
        }

        // Only a live FakeEngine counts, so a reused pid can neither fail the assertion nor be killed.
        private List<int> Survivors(TimeSpan waitForExit)
        {
            var survivors = new List<int>();
            foreach (var pid in File.Exists(pidFile) ? Pids() : [])
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    if (string.Equals(process.MainModule?.FileName, TestPaths.FakeEnginePath, StringComparison.OrdinalIgnoreCase)
                        && !process.WaitForExit(waitForExit))
                    {
                        survivors.Add(pid);
                    }
                }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Gone (or exited while being inspected), or not ours to inspect.
                }
            }

            return survivors;
        }
    }

    [TestMethod]
    public async Task RunAsync_EchoesArgvAndExitZero()
    {
        var r = await Run(["daily", "--json"]);
        Assert.AreEqual((0, "", false), (r.ExitCode, r.Stderr, r.TimedOut));
        CollectionAssert.AreEqual(
            new[] { "daily", "--json" },
            Echo(r).GetProperty("argv").EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_PassesSpacesQuotesAndUnicodeAsSingleArgv()
    {
        const string v = "My \"Quoted\" Project — 日本語";
        var r = await Run(["claude", "daily", "--project", v]);
        Assert.AreEqual(v, Echo(r).GetProperty("argv")[3].GetString());
    }

    [TestMethod]
    public async Task RunAsync_DeliversStdin()
    {
        var r = await Run(["statusline"], "{\"session_id\":\"x\"}");
        Assert.AreEqual("{\"session_id\":\"x\"}", Echo(r).GetProperty("stdin").GetString());
    }

    [TestMethod]
    public async Task RunAsync_DeliversStdinAsUtf8WithoutBom()
    {
        const string s = "{\"cwd\":\"C:/Projekt — 日本語\"}";
        var r = await Run(["statusline"], s);
        Assert.AreEqual(s, Echo(r).GetProperty("stdin").GetString());
    }

    [TestMethod]
    public async Task RunAsync_SetsNoColor() => Assert.AreEqual("1", Echo(await Run(["daily"])).GetProperty("noColor").GetString());

    [TestMethod]
    public async Task RunAsync_AppliesEnvironmentOverrides()
    {
        Assert.AreEqual("x", Echo(await Run(["daily"], env: new() { ["NO_COLOR"] = "x" })).GetProperty("noColor").GetString());
        Assert.AreEqual(JsonValueKind.Null, Echo(await Run(["daily"], env: new() { ["NO_COLOR"] = null })).GetProperty("noColor").ValueKind);
    }

    [TestMethod]
    public void Timeout_IsExposed() => Assert.AreEqual(TimeSpan.FromSeconds(7), new EngineRunner(TimeSpan.FromSeconds(7), _ => { }).Timeout);

    [TestMethod]
    public void Constructor_RejectsNonPositiveTimeout() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new EngineRunner(TimeSpan.Zero, _ => { }));

    [TestMethod]
    public async Task RunAsync_CapturesStderr()
    {
        var r = await Run(["daily", "--fake-stderr=warn: pricing"]);
        Assert.AreEqual(0, r.ExitCode);
        Assert.AreEqual("warn: pricing", r.Stderr); // FakeEngine writes stderr without a trailing newline
    }

    [TestMethod]
    public async Task RunAsync_NonZeroExit() => Assert.AreEqual(3, (await Run(["daily", "--fake-exit=3"])).ExitCode);

    [TestMethod]
    public async Task RunAsync_LargeStdout_DoesNotDeadlock()
    {
        var r = await Run(["daily", "--fake-stdout-bytes=8388608"]);
        Assert.AreEqual(8388608, r.Stdout.Length);
    }

    [TestMethod]
    public async Task RunAsync_Timeout_KillsAndReportsTimedOut()
    {
        using var tree = new FakeTree();
        var sw = Stopwatch.StartNew();
        var r = await Run(tree.Argv, timeoutSeconds: 1);
        Assert.IsTrue(r.TimedOut);
        tree.AssertKilled();
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString());
        StringAssert.Contains(r.Stdout, "\"argv\"");
    }

    [TestMethod]
    public async Task RunAsync_Cancellation_KillsProcess()
    {
        // Canceled once the tree is up (not on a fixed timer), so the kill always has an engine and a child to reach.
        // The 10 s engine timeout still bounds the run if cancellation were ignored.
        using var tree = new FakeTree();
        using var cts = new CancellationTokenSource();
        var run = Run(tree.Argv, ct: cts.Token);
        await tree.WaitForPidsAsync();

        var sw = Stopwatch.StartNew();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => run);
        tree.AssertKilled();
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString());
    }

    [TestMethod]
    public async Task RunAsync_AlreadyCanceled_ThrowsWithoutStarting()
    {
        using var tree = new FakeTree();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Run(tree.Argv, ct: cts.Token));
        await Task.Delay(500); // long enough for a wrongly started FakeEngine to write its pid file
        Assert.IsFalse(tree.Started);
    }

    [TestMethod]
    [DataRow("abc", 120, 1)]
    [DataRow("0", 120, 1)]
    [DataRow("-5", 120, 1)]
    [DataRow("", 120, 0)]
    [DataRow(null, 120, 0)]
    [DataRow("45", 45, 0)]
    public void TimeoutSeconds_InvalidEnv_FallsBackTo120(string? value, int expectedSeconds, int expectedWarnings)
    {
        var warnings = new List<string>();
        var t = EngineRunner.TimeoutFromEnvironment(k => k == "CCUSAGE_MCP_TIMEOUT_SECONDS" ? value : null, warnings.Add);
        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), t);
        Assert.AreEqual(expectedWarnings, warnings.Count);
        if (expectedWarnings == 1)
        {
            Assert.AreEqual($"CCUSAGE_MCP_TIMEOUT_SECONDS='{value}' is not a positive number; using 120.", warnings[0]);
        }
    }
}
