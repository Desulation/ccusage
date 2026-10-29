using System.Diagnostics;
using System.Text.Json;

namespace Ccusage.Mcp.Tests;

[TestClass]
public sealed class VersionTests
{
    [TestMethod]
    public void ServerVersion_MatchesRootPackageJson()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "package.json")));
        Assert.AreEqual(doc.RootElement.GetProperty("version").GetString(), ServerVersion.Value);
    }

    [TestMethod]
    public async Task FakeEngine_EchoesArgv()
    {
        var (exit, stdout, _) = await RunProcess(TestPaths.FakeEnginePath, ["daily", "--json"]);
        using var doc = JsonDocument.Parse(stdout);
        Assert.AreEqual(0, exit);
        CollectionAssert.AreEqual(new[] { "daily", "--json" }, doc.RootElement.GetProperty("argv").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.AreEqual("", doc.RootElement.GetProperty("stdin").GetString());
    }

    // Walks up from the test output directory to the first directory holding both package.json and rust/.
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "package.json")) && Directory.Exists(Path.Combine(dir.FullName, "rust")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"No directory containing package.json and rust/ above {AppContext.BaseDirectory}.");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcess(string path, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(path)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {path}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
