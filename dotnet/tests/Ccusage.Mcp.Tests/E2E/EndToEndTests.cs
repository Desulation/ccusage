using System.Text.Json;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Engine;
using Ccusage.Mcp.Tests.Tools;
using Ccusage.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Tests.E2E;

/// <summary>
/// Drives the published server over stdio against the real engine (spec §10.3): each result's text must equal the
/// engine's own output for the same argv. Inconclusive unless <c>CCUSAGE_MCP_E2E_SERVER</c> and <c>CCUSAGE_ENGINE</c>
/// are set. All environment reaches the child processes through <see cref="E2eContext.Env"/>, never process-wide.
/// </summary>
[TestClass]
public sealed class EndToEndTests : IDisposable
{
    // Bounds every client operation; disposing the client kills the server's process tree.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(3);

    private readonly CancellationTokenSource timeout = new(TestTimeout);

    private E2eContext Ctx { get; set; } = null!;

    private CancellationToken Ct => timeout.Token;

    [TestInitialize]
    public void Initialize() => Ctx = E2eEnvironment.RequireOrInconclusive();

    public void Dispose() => timeout.Dispose();

    [TestCleanup]
    public void Cleanup()
    {
        // The fresh HOME/USERPROFILE directory made for this test (absent when the test was inconclusive).
        if (Ctx?.Env["HOME"] is { } home && Directory.Exists(home))
        {
            Directory.Delete(home, recursive: true);
        }
    }

    private async Task<McpClient> Client() =>
        await McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "ccusage-mcp-e2e",
                Command = Ctx.ServerPath,
                EnvironmentVariables = Ctx.Env,
            }),
            cancellationToken: Ct);

    private Task<EngineResult> Direct(IReadOnlyList<string> argv, string? stdin = null) =>
        new EngineRunner(TimeSpan.FromSeconds(120), _ => { }).RunAsync(new EngineInvocation(Ctx.EnginePath, argv, stdin, Ctx.Env), Ct);

    private static Dictionary<string, JsonElement> Args(string json) => HandlerFixture.Args(json);

    private static string Text(CallToolResult result) => ((TextContentBlock)result.Content[0]).Text;

    [TestMethod]
    public async Task Initialize_ReportsServerInfo()
    {
        await using var c = await Client();
        var info = c.ServerInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual("ccusage", info.Name);
        Assert.AreEqual(ServerVersion.Value, info.Version);
    }

    [TestMethod]
    public async Task ToolsList_MatchesDefinitions()
    {
        await using var c = await Client();
        CollectionAssert.AreEqual(
            ToolDefinitions.Build(CommandCatalog.Instance).Select(t => t.Name).ToArray(),
            (await c.ListToolsAsync(cancellationToken: Ct)).Select(t => t.Name).ToArray());
    }

    [TestMethod]
    [DataRow("daily", """{"source":"claude","offline":true}""")]
    [DataRow("daily", """{"source":"codex","offline":true}""")]
    [DataRow("monthly", """{"source":"claude","offline":true,"breakdown":true}""")]
    [DataRow("session", """{"source":"claude","offline":true}""")]
    [DataRow("weekly", """{"source":"claude","offline":true,"start-of-week":"monday"}""")]
    [DataRow("daily", """{"offline":true}""")]
    [DataRow("blocks", """{"offline":true}""")]
    public async Task TypedTool_EqualsDirectEngineOutput(string tool, string json)
    {
        await using var c = await Client();
        var args = Args(json);
        var r = await c.CallToolAsync(tool, args.ToDictionary(k => k.Key, k => (object?)k.Value), cancellationToken: Ct);
        var argv = CommandValidator.ValidateAndBuildArgv(CommandCatalog.Instance, ToolArguments.FromTyped(CommandCatalog.Instance, tool, args));
        var direct = await Direct(argv);
        Assert.AreEqual(0, direct.ExitCode, direct.Stderr);
        Assert.AreNotEqual(true, r.IsError);
        Assert.AreEqual(direct.Stdout, Text(r));
        if (tool == "daily" && json == """{"offline":true}""")
        {
            Console.WriteLine($"daily(all) bytes: {direct.Stdout.Length}");
        }
    }

    [TestMethod]
    public async Task ReportTool_EqualsDirectEngineOutput()
    {
        await using var c = await Client();
        var r = await c.CallToolAsync("report", new Dictionary<string, object?> { ["args"] = "codex daily --offline" }, cancellationToken: Ct);
        var direct = await Direct(CommandValidator.ValidateAndBuildArgv(CommandCatalog.Instance, CliArgsParser.Parse(CommandCatalog.Instance, "codex daily --offline")));
        Assert.AreEqual(direct.Stdout, Text(r));
    }

    [TestMethod]
    public async Task Statusline_ReturnsEngineText()
    {
        var payload = File.ReadAllText(Path.Combine(Ctx.FixturesDir, "statusline", "statusline-test.json"));
        using var hookInput = JsonDocument.Parse(payload);
        await using var c = await Client();
        var r = await c.CallToolAsync("statusline", new Dictionary<string, object?> { ["hook-input"] = hookInput.RootElement, ["offline"] = true }, cancellationToken: Ct);
        var direct = await Direct(["statusline", "--offline"], payload);
        Assert.AreEqual(direct.Stdout, Text(r));
    }

    [TestMethod]
    public async Task Prompt_Get_ContainsEngineJson()
    {
        await using var c = await Client();
        var p = await c.GetPromptAsync("ccusage", new Dictionary<string, object?> { ["args"] = "claude daily --offline" }, cancellationToken: Ct);
        var direct = await Direct(["claude", "daily", "--json", "--offline"]);
        StringAssert.Contains(((TextContentBlock)p.Messages.Single().Content).Text, direct.Stdout);
    }

    [TestMethod]
    public async Task ValidationError_IsError()
    {
        await using var c = await Client();
        var r = await c.CallToolAsync("daily", new Dictionary<string, object?> { ["source"] = "codex", ["speed"] = "turbo" }, cancellationToken: Ct);
        Assert.AreEqual(true, r.IsError);
    }
}
