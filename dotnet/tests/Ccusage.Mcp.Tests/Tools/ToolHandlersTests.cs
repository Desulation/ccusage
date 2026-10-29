using System.Runtime.InteropServices;
using System.Text.Json;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Engine;
using Ccusage.Mcp.Tools;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Tests.Tools;

/// <summary>Builds handler dependencies against FakeEngine; the environment is injected, never process-wide.</summary>
internal static class HandlerFixture
{
    // An empty server directory, so only CCUSAGE_ENGINE (and the injected PATH) can resolve an engine.
    private static readonly string EmptyServerDirectory = Directory.CreateTempSubdirectory().FullName;

    public static EngineLocator Locator(string enginePath, string path) =>
        new(k => k == "CCUSAGE_ENGINE" ? enginePath : k == "PATH" ? path : null, EmptyServerDirectory, Architecture.X64, OperatingSystem.IsWindows());

    public static EngineRunner Runner(int timeoutSeconds) => new(TimeSpan.FromSeconds(timeoutSeconds), _ => { });

    public static Dictionary<string, JsonElement> Args(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
}

[TestClass]
public sealed class ToolHandlersTests
{
    private static ToolHandlers Handlers(string? enginePath = null, int timeoutSeconds = 10, string path = "") =>
        new(CommandCatalog.Instance, HandlerFixture.Locator(enginePath ?? TestPaths.FakeEnginePath, path), HandlerFixture.Runner(timeoutSeconds));

    private static Dictionary<string, JsonElement> Args(string json) => HandlerFixture.Args(json);

    private static string Text(CallToolResult result, int i) => ((TextContentBlock)result.Content[i]).Text;

    private static string?[] Argv(CallToolResult result) =>
        JsonDocument.Parse(Text(result, 0)).RootElement.GetProperty("argv").EnumerateArray().Select(x => x.GetString()).ToArray();

    private static string MissingEngine => Path.Combine(Path.GetTempPath(), "missing-engine");

    [TestMethod]
    public async Task ListAsync_ReturnsDefinitions() => Assert.HasCount(8, (await Handlers().ListAsync(default)).Tools);

    [TestMethod]
    public async Task CallAsync_Daily_BuildsArgvAndPassesStdout()
    {
        var r = await Handlers().CallAsync("daily", Args("""{"source":"codex","since":"20260101","offline":true}"""), default);
        Assert.AreNotEqual(true, r.IsError);
        Assert.HasCount(1, r.Content);
        CollectionAssert.AreEqual(new[] { "codex", "daily", "--json", "--since", "20260101", "--offline" }, Argv(r));
    }

    [TestMethod]
    public async Task CallAsync_Blocks_NumberArgument() =>
        CollectionAssert.AreEqual(
            new[] { "blocks", "--json", "--token-limit", "500000" },
            Argv(await Handlers().CallAsync("blocks", Args("""{"token-limit":500000}"""), default)));

    [TestMethod]
    public async Task CallAsync_Statusline_WritesStdin()
    {
        var r = await Handlers().CallAsync("statusline", Args("""{"hook-input":{"session_id":"s"},"offline":true}"""), default);
        CollectionAssert.AreEqual(new[] { "statusline", "--offline" }, Argv(r));
        Assert.AreEqual("""{"session_id":"s"}""", JsonDocument.Parse(Text(r, 0)).RootElement.GetProperty("stdin").GetString());
    }

    [TestMethod]
    [DataRow("""{"hook-input":"x"}""", "Parameter 'hook-input' must be a JSON object.")]
    [DataRow("""{"hook-input":[1]}""", "Parameter 'hook-input' must be a JSON object.")]
    [DataRow("""{"offline":true}""", "Parameter 'hook-input' is required.")]
    [DataRow("""{"hook-input":null}""", "Parameter 'hook-input' is required.")] // a JSON null counts as omitted
    public async Task CallAsync_Statusline_RejectsNonObjectHookInput(string json, string msg)
    {
        var r = await Handlers().CallAsync("statusline", Args(json), default);
        Assert.AreEqual(true, r.IsError);
        Assert.AreEqual(msg, Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_ValidationError_IsError()
    {
        var r = await Handlers().CallAsync("daily", Args("""{"source":"codex","speed":"turbo"}"""), default);
        Assert.AreEqual(true, r.IsError);
        Assert.StartsWith("Parameter 'speed' must be one of: auto, standard, flex, fast", Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_UnknownParameter_IsError()
    {
        var r = await Handlers().CallAsync("blocks", Args("""{"source":"all"}"""), default);
        Assert.AreEqual(true, r.IsError);
        Assert.StartsWith("Parameter 'source' is not a parameter of 'claude blocks'. Valid parameters: ", Text(r, 0));
    }

    // A non-string source reaches CommandValidator as its JSON text, so the error lists the valid sources (§5.7).
    [TestMethod]
    [DataRow("""{"source":5}""", "5")]
    [DataRow("""{"source":true}""", "true")]
    [DataRow("""{"source":{"a":1}}""", """{"a":1}""")]
    [DataRow("""{"source":[1]}""", "[1]")]
    public async Task CallAsync_NonStringSource_ListsValidSources(string json, string shown)
    {
        var r = await Handlers().CallAsync("daily", Args(json), default);
        Assert.AreEqual(true, r.IsError);
        Assert.StartsWith($"Unknown source '{shown}'. Valid sources: all, claude, ", Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_NullSource_MeansAll() =>
        CollectionAssert.AreEqual(new[] { "daily", "--json" }, Argv(await Handlers().CallAsync("daily", Args("""{"source":null}"""), default)));

    [TestMethod]
    public async Task CallAsync_UnknownTool_IsError()
    {
        var r = await Handlers().CallAsync("nope", null, default);
        Assert.AreEqual(true, r.IsError);
        Assert.AreEqual("Unknown tool 'nope'.", Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_StderrBecomesSecondBlock()
    {
        var r = await Handlers().CallAsync("daily", Args("""{"source":"claude","debug":true}"""), default);
        Assert.AreNotEqual(true, r.IsError);
        Assert.HasCount(2, r.Content);
        Assert.StartsWith("stderr:\n", Text(r, 1));
        Assert.Contains("debug: fake", Text(r, 1));
    }

    [TestMethod]
    public async Task CallAsync_NonZeroExit_IsError()
    {
        var r = await Handlers().CallAsync("daily", Args("""{"source":"claude","project":"fail"}"""), default);
        Assert.AreEqual(true, r.IsError);
        Assert.StartsWith("ccusage exited with code 4.\nboom", Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_Timeout_IsError()
    {
        var r = await Handlers(timeoutSeconds: 1).CallAsync("session", Args("""{"source":"claude","id":"hang"}"""), default);
        Assert.AreEqual(true, r.IsError);
        Assert.StartsWith("ccusage timed out after 1 s.", Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_EngineMissing_IsError_ListsPaths()
    {
        var r = await Handlers(enginePath: MissingEngine).CallAsync("daily", null, default);
        Assert.AreEqual(true, r.IsError);
        Assert.Contains("Checked:", Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_PassesNonJsonStdoutUnchanged()
    {
        var r = await Handlers().CallAsync("daily", Args("""{"source":"claude","project":"plain"}"""), default);
        Assert.AreNotEqual(true, r.IsError);
        Assert.AreEqual("not json\n", Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_ListSources_Shape()
    {
        var r = await Handlers().CallAsync("list-sources", null, default);
        var j = JsonDocument.Parse(Text(r, 0)).RootElement;
        Assert.AreEqual(ServerVersion.Value, j.GetProperty("server-version").GetString());
        Assert.AreEqual("0.0.0-fake", j.GetProperty("engine-version").GetString());
        Assert.AreEqual(TestPaths.FakeEnginePath, j.GetProperty("engine-path").GetString());
        Assert.IsFalse(j.TryGetProperty("engine-error", out _));
        var s = j.GetProperty("sources");
        Assert.AreEqual("all", s[0].GetProperty("id").GetString());
        Assert.AreEqual("Claude Code", s[1].GetProperty("display-name").GetString());
        CollectionAssert.AreEqual(
            new[] { "daily", "monthly", "weekly", "session", "blocks", "statusline" },
            s[1].GetProperty("reports").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [TestMethod]
    public async Task CallAsync_ListSources_EngineMissing()
    {
        var r = await Handlers(enginePath: MissingEngine).CallAsync("list-sources", null, default);
        var j = JsonDocument.Parse(Text(r, 0)).RootElement;
        Assert.AreNotEqual(true, r.IsError);
        Assert.AreEqual(JsonValueKind.Null, j.GetProperty("engine-version").ValueKind);
        Assert.Contains("Checked:", j.GetProperty("engine-error").GetString()!);
    }

    [TestMethod]
    public async Task CallAsync_Report_UsesParser()
    {
        CollectionAssert.AreEqual(
            new[] { "weekly", "--json", "--last", "2" },
            Argv(await Handlers().CallAsync("report", Args("""{"args":"weekly --last 2"}"""), default)));
        var bad = await Handlers().CallAsync("report", Args("""{"args":"statusline"}"""), default);
        Assert.AreEqual(true, bad.IsError);
        Assert.AreEqual("'statusline' needs a hook payload; use the statusline tool.", Text(bad, 0));
    }

    // The report and list-sources schemas declare additionalProperties:false; a JSON null still counts as omitted.
    [TestMethod]
    [DataRow("report", """{"args":"daily","source":"codex"}""", "Parameter 'source' is not a parameter of 'report'. Valid parameters: args.")]
    [DataRow("report", """{"since":"20260101"}""", "Parameter 'since' is not a parameter of 'report'. Valid parameters: args.")]
    [DataRow("report", """{}""", "Parameter 'args' is required.")]
    [DataRow("report", """{"args":null}""", "Parameter 'args' is required.")]
    [DataRow("report", """{"args":5}""", "Parameter 'args' must be a string.")]
    [DataRow("list-sources", """{"source":"codex"}""", "Parameter 'source' is not a parameter of 'list-sources', which takes no parameters.")]
    public async Task CallAsync_UntypedTools_RejectBadArguments(string tool, string json, string msg)
    {
        var r = await Handlers().CallAsync(tool, Args(json), default);
        Assert.AreEqual(true, r.IsError);
        Assert.HasCount(1, r.Content);
        Assert.AreEqual(msg, Text(r, 0));
    }

    [TestMethod]
    public async Task CallAsync_Report_IgnoresNullExtraKey() =>
        CollectionAssert.AreEqual(
            new[] { "daily", "--json" },
            Argv(await Handlers().CallAsync("report", Args("""{"args":"daily","source":null}"""), default)));

    [TestMethod]
    public async Task CallAsync_ListSources_IgnoresNullKey()
    {
        var r = await Handlers().CallAsync("list-sources", Args("""{"source":null}"""), default);
        Assert.AreNotEqual(true, r.IsError);
        Assert.AreEqual("0.0.0-fake", JsonDocument.Parse(Text(r, 0)).RootElement.GetProperty("engine-version").GetString());
    }
}
