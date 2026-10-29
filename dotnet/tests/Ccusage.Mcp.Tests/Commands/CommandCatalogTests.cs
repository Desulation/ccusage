using Ccusage.Mcp.Commands;

namespace Ccusage.Mcp.Tests.Commands;

[TestClass]
public sealed class CommandCatalogTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;

    private static string[] Names(string s, string r)
    {
        Assert.IsTrue(C.TryGetPage(s, r, out var p), $"{s}/{r}");
        return p!.Options.Select(o => o.Name).ToArray();
    }

    [TestMethod]
    public void Sources_AreAllPlusEighteenAgentsInRootOrder() =>
        CollectionAssert.AreEqual(
            new[] { "all", "claude", "codex", "opencode", "amp", "droid", "codebuff", "hermes", "pi", "goose", "kilo", "copilot", "gemini", "antigravity", "kimi", "qwen", "openclaw", "grok", "zcode" },
            C.Sources.Select(s => s.Id).ToArray());

    [TestMethod]
    public void Reports_AreDefinedOrder() =>
        CollectionAssert.AreEqual(new[] { "daily", "weekly", "monthly", "session", "blocks", "statusline" }, C.Reports.ToArray());

    [TestMethod]
    [DataRow("all", "daily,monthly,weekly,session")]
    [DataRow("claude", "daily,monthly,weekly,session,blocks,statusline")]
    [DataRow("codex", "daily,monthly,session")]
    [DataRow("opencode", "daily,weekly,monthly,session")]
    public void Reports_PerSource(string id, string csv) =>
        CollectionAssert.AreEquivalent(csv.Split(','), C.Sources.Single(s => s.Id == id).Reports.ToArray());

    [TestMethod]
    public void Sources_HaveDisplayNames()
    {
        Assert.AreEqual("All sources", C.Sources.Single(s => s.Id == "all").DisplayName);
        Assert.AreEqual("GitHub Copilot CLI", C.Sources.Single(s => s.Id == "copilot").DisplayName);
    }

    [TestMethod]
    public void RootBlocksAndStatusline_BelongToClaude()
    {
        Assert.IsTrue(C.TryGetPage("claude", "blocks", out _));
        Assert.IsFalse(C.TryGetPage("all", "blocks", out _));
        Assert.IsTrue(C.TryGetPage("claude", "statusline", out _));
    }

    [TestMethod]
    public void TryGetPage_UnknownPair_ReturnsFalse()
    {
        Assert.IsFalse(C.TryGetPage("codex", "weekly", out var page));
        Assert.IsNull(page);
        Assert.IsFalse(C.TryGetPage("nope", "daily", out _));
    }

    [TestMethod]
    public void Page_CarriesSourceReportAndDescription()
    {
        Assert.IsTrue(C.TryGetPage("codex", "daily", out var page));
        Assert.AreEqual("codex", page!.Source);
        Assert.AreEqual("daily", page.Report);
        Assert.AreEqual("Show Codex token usage grouped by day", page.Description);
    }

    [TestMethod]
    public void SourcesWithReport_Weekly() =>
        CollectionAssert.AreEqual(new[] { "all", "claude", "opencode" }, C.SourcesWithReport("weekly").ToArray());

    [TestMethod]
    [DataRow("claude", "daily", "since,until,mode,debug,debug-samples,order,breakdown,offline,single-thread,timezone,config,instances,project,project-aliases,last")]
    [DataRow("all", "daily", "since,until,timezone,sections,by-agent,offline,no-cost,config,last")]
    [DataRow("claude", "statusline", "offline,visual-burn-rate,cost-source,cache,refresh-interval,context-low-threshold,context-medium-threshold,timezone,config,debug")]
    [DataRow("claude", "blocks", "since,until,mode,debug,debug-samples,order,breakdown,offline,single-thread,timezone,config,active,recent,token-limit,session-length")]
    public void Page_Options_InCatalogOrder(string s, string r, string csv) => CollectionAssert.AreEqual(csv.Split(','), Names(s, r));

    [TestMethod]
    public void Options_NeverContainNeverExposedOrNegations()
    {
        foreach (var src in C.Sources)
        {
            foreach (var rep in src.Reports)
            {
                foreach (var n in Names(src.Id, rep))
                {
                    Assert.IsFalse(CommandCatalog.NeverExposed.Contains(n), n);
                    Assert.IsFalse(n.StartsWith("no-", StringComparison.Ordinal) && n != "no-cost", n);
                }
            }
        }
    }

    [TestMethod]
    public void Flags_Parsed()
    {
        C.TryGetPage("claude", "daily", out var cd);
        var since = cd!.Options.Single(o => o.Name == "since");
        Assert.AreEqual("s", since.ShortAlias);
        Assert.IsTrue(since.TakesValue);
        Assert.IsFalse(since.ValueOptional);
        Assert.AreEqual(OptionKind.Date, since.Kind);
        Assert.IsFalse(cd.Options.Single(o => o.Name == "instances").TakesValue);
        Assert.IsNull(cd.Options.Single(o => o.Name == "project-aliases").ShortAlias);

        C.TryGetPage("codex", "daily", out var cx);
        var speed = cx!.Options.Single(o => o.Name == "speed");
        Assert.IsTrue(speed.TakesValue);
        Assert.IsTrue(speed.ValueOptional);
        Assert.AreEqual("auto", speed.Default);
        CollectionAssert.AreEqual(new[] { "auto", "standard", "flex", "fast" }, speed.EnumValues.ToArray());

        C.TryGetPage("all", "weekly", out var aw);
        Assert.AreEqual("monday", aw!.Options.Single(o => o.Name == "start-of-week").Default);
        C.TryGetPage("claude", "weekly", out var cw);
        Assert.AreEqual("sunday", cw!.Options.Single(o => o.Name == "start-of-week").Default);
    }

    [TestMethod]
    public void NoCost_IsPlainBool()
    {
        C.TryGetPage("all", "daily", out var ad);
        var noCost = ad!.Options.Single(o => o.Name == "no-cost");
        Assert.AreEqual(OptionKind.Bool, noCost.Kind);
        Assert.IsFalse(noCost.TakesValue);
        Assert.AreEqual(0, noCost.EnumValues.Count);
    }

    [TestMethod]
    [DataRow("daily", "speed", "codex")]
    [DataRow("session", "id", "all,claude,codex")]
    [DataRow("daily", "since", "all,claude,codex,opencode,amp,droid,codebuff,hermes,pi,goose,kilo,copilot,gemini,antigravity,kimi,qwen,openclaw,grok,zcode")]
    public void SourcesAccepting(string report, string option, string csv) =>
        CollectionAssert.AreEqual(csv.Split(','), C.SourcesAccepting(report, option).ToArray());

    [TestMethod]
    public void UnionOptions_Daily_IsDedupedCatalogOrder()
    {
        var union = C.UnionOptions("daily");
        CollectionAssert.AreEqual(
            new[] { "since", "until", "timezone", "sections", "by-agent", "offline", "no-cost", "config", "last", "mode", "debug", "debug-samples", "order", "breakdown", "single-thread", "instances", "project", "project-aliases", "speed", "pi-path", "open-claw-path" },
            union.Select(o => o.Name).ToArray());
        var sections = union.Single(o => o.Name == "sections");
        Assert.AreEqual(OptionKind.CsvEnum, sections.Kind);
        CollectionAssert.AreEqual(new[] { "daily", "weekly", "monthly", "session" }, sections.EnumValues.ToArray());
    }

    [TestMethod]
    public void UnionOptions_UnknownReport_IsEmpty() => Assert.AreEqual(0, C.UnionOptions("yearly").Count);
}
