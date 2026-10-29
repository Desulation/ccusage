using System.Text.Json;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Prompts;
using Ccusage.Mcp.Tools;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Tests.Tools;

[TestClass]
public sealed class ToolDefinitionsTests
{
    private static readonly IReadOnlyList<Tool> Tools = ToolDefinitions.Build(CommandCatalog.Instance);

    private static Tool T(string n) => Tools.Single(t => t.Name == n);

    private static JsonElement Props(string n) => T(n).InputSchema.GetProperty("properties");

    private static string?[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()).ToArray();

    [TestMethod]
    public void Build_ReturnsEightToolsInOrder() =>
        CollectionAssert.AreEqual(
            new[] { "daily", "weekly", "monthly", "session", "blocks", "statusline", "list-sources", "report" },
            Tools.Select(t => t.Name).ToArray());

    [TestMethod]
    public void Daily_SourceEnum_IsAllPlusEverySourceWithDaily()
    {
        var e = Strings(Props("daily").GetProperty("source").GetProperty("enum"));
        Assert.HasCount(19, e);
        Assert.AreEqual("all", e[0]);
    }

    [TestMethod]
    public void Weekly_SourceEnum() =>
        CollectionAssert.AreEqual(new[] { "all", "claude", "opencode" }, Strings(Props("weekly").GetProperty("source").GetProperty("enum")));

    [TestMethod]
    public void Daily_Schema_IsStrictUnion()
    {
        var s = T("daily").InputSchema;
        Assert.IsFalse(s.GetProperty("additionalProperties").GetBoolean());
        Assert.IsFalse(s.TryGetProperty("required", out _));
        foreach (var p in new[] { "speed", "instances", "sections", "pi-path", "last" })
        {
            Assert.IsTrue(Props("daily").TryGetProperty(p, out _), p);
        }
    }

    [TestMethod]
    public void Daily_Speed_AppliesToCodex() =>
        Assert.EndsWith(" Default: auto. Applies to: codex.", Props("daily").GetProperty("speed").GetProperty("description").GetString()!);

    [TestMethod]
    public void Daily_Since_HasNoAppliesTo() =>
        Assert.DoesNotContain("Applies to", Props("daily").GetProperty("since").GetProperty("description").GetString()!);

    [TestMethod]
    public void Daily_Last_MinimumOne() => Assert.AreEqual(1, Props("daily").GetProperty("last").GetProperty("minimum").GetInt32());

    [TestMethod]
    public void Daily_Since_Pattern() =>
        Assert.AreEqual(@"^(\d{4}-\d{2}-\d{2}|\d{8})$", Props("daily").GetProperty("since").GetProperty("pattern").GetString());

    [TestMethod]
    public void Blocks_HasNoSource_AndTokenLimitPattern()
    {
        Assert.IsFalse(Props("blocks").TryGetProperty("source", out _));
        Assert.AreEqual(@"^([1-9]\d*|max)$", Props("blocks").GetProperty("token-limit").GetProperty("pattern").GetString());
        Assert.AreEqual("number", Props("blocks").GetProperty("session-length").GetProperty("type").GetString());
    }

    [TestMethod]
    public void Statusline_RequiresHookInputObject()
    {
        Assert.AreEqual("object", Props("statusline").GetProperty("hook-input").GetProperty("type").GetString());
        CollectionAssert.AreEqual(new[] { "hook-input" }, Strings(T("statusline").InputSchema.GetProperty("required")));
    }

    [TestMethod]
    public void Report_RequiresArgs() =>
        CollectionAssert.AreEqual(new[] { "args" }, Strings(T("report").InputSchema.GetProperty("required")));

    [TestMethod]
    public void ListSources_EmptyProperties() => Assert.AreEqual(0, Props("list-sources").EnumerateObject().Count());

    [TestMethod]
    public void Annotations_OnEveryTool()
    {
        foreach (var t in Tools)
        {
            Assert.IsNotNull(t.Annotations, t.Name);
            Assert.IsTrue(t.Annotations.ReadOnlyHint, t.Name);
            Assert.IsFalse(t.Annotations.DestructiveHint, t.Name);
            Assert.IsTrue(t.Annotations.IdempotentHint, t.Name);
            Assert.IsNull(t.Annotations.OpenWorldHint, t.Name);
        }
    }

    [TestMethod]
    public void Prompt_HasOptionalArgsArgument()
    {
        var p = PromptDefinitions.Ccusage;
        Assert.AreEqual("ccusage", p.Name);
        Assert.IsNotNull(p.Arguments);
        var a = p.Arguments.Single();
        Assert.AreEqual("args", a.Name);
        Assert.AreNotEqual(true, a.Required);
    }

    // Schemas and CommandValidator must agree: every typed tool's properties (minus source/hook-input) are exactly
    // the union of its report's page options, so a schema-valid argument is never an unknown parameter.
    [TestMethod]
    [DataRow("daily")]
    [DataRow("weekly")]
    [DataRow("monthly")]
    [DataRow("session")]
    [DataRow("blocks")]
    [DataRow("statusline")]
    public void ReportSchema_PropertiesAreUnionOptionsInCatalogOrder(string report)
    {
        var names = Props(report).EnumerateObject().Select(p => p.Name).Where(n => n is not ("source" or "hook-input")).ToArray();
        CollectionAssert.AreEqual(CommandCatalog.Instance.UnionOptions(report).Select(o => o.Name).ToArray(), names);
    }

    [TestMethod]
    public void Statusline_HasTenOptionsAfterHookInput()
    {
        var names = Props("statusline").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.AreEqual("hook-input", names[0]);
        Assert.HasCount(11, names);
    }

    [TestMethod]
    public void Daily_Sections_IsCsvStringWithItemList()
    {
        var sections = Props("daily").GetProperty("sections");
        Assert.AreEqual("string", sections.GetProperty("type").GetString());
        Assert.Contains(" Comma-separated; each item one of: daily, weekly, monthly, session.", sections.GetProperty("description").GetString()!);
    }

    [TestMethod]
    public void Daily_Offline_IsBoolean() =>
        Assert.AreEqual("boolean", Props("daily").GetProperty("offline").GetProperty("type").GetString());

    [TestMethod]
    public void Daily_DebugSamples_MinimumZero() =>
        Assert.AreEqual(0, Props("daily").GetProperty("debug-samples").GetProperty("minimum").GetInt32());

    [TestMethod]
    public void Daily_Mode_IsEnum()
    {
        var mode = Props("daily").GetProperty("mode");
        Assert.AreEqual("string", mode.GetProperty("type").GetString());
        CollectionAssert.AreEqual(CommandCatalog.Instance.UnionOptions("daily").Single(o => o.Name == "mode").EnumValues.ToArray(), Strings(mode.GetProperty("enum")));
    }

    [TestMethod]
    public void Daily_Source_DefaultAndDescription()
    {
        var source = Props("daily").GetProperty("source");
        Assert.AreEqual("all", source.GetProperty("default").GetString());
        Assert.AreEqual("Data source. Default: all.", source.GetProperty("description").GetString());
    }

    [TestMethod]
    public void BuildReportSchema_MatchesToolInputSchema() =>
        Assert.AreEqual(T("weekly").InputSchema.GetRawText(), ToolDefinitions.BuildReportSchema(CommandCatalog.Instance, "weekly").GetRawText());

    [TestMethod]
    public void Prompt_ArgsDescription_EqualsReportArgsDescription() =>
        Assert.AreEqual(
            Props("report").GetProperty("args").GetProperty("description").GetString(),
            PromptDefinitions.Ccusage.Arguments!.Single().Description);
}
