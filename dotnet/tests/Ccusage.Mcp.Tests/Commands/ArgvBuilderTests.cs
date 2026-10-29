using Ccusage.Mcp.Commands;

namespace Ccusage.Mcp.Tests.Commands;

[TestClass]
public sealed class ArgvBuilderTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;

    private static string[] Argv(string s, string r, params (string n, string v)[] o) =>
        CommandValidator.ValidateAndBuildArgv(C, new CommandRequest(s, r, o.Select(x => new OptionValue(x.n, x.v)).ToList())).ToArray();

    [TestMethod]
    public void AllDaily_NoOptions() => CollectionAssert.AreEqual(new[] { "daily", "--json" }, Argv("all", "daily"));

    [TestMethod]
    public void CodexSession() => CollectionAssert.AreEqual(new[] { "codex", "session", "--json" }, Argv("codex", "session"));

    [TestMethod]
    public void ClaudeBlocks_RootForm() => CollectionAssert.AreEqual(new[] { "blocks", "--json" }, Argv("claude", "blocks"));

    [TestMethod]
    public void ClaudeStatusline_NoJson() => CollectionAssert.AreEqual(new[] { "statusline" }, Argv("claude", "statusline"));

    [TestMethod]
    public void Options_InCatalogOrder_NotRequestOrder() =>
        CollectionAssert.AreEqual(
            new[] { "daily", "--json", "--by-agent", "--offline", "--last", "2" },
            Argv("all", "daily", ("last", "2"), ("by-agent", "true"), ("offline", "true")));

    [TestMethod]
    public void NegatableFalse_EmitsNoFlag_PlainFalse_EmitsNothing() =>
        CollectionAssert.AreEqual(
            new[] { "claude", "daily", "--json", "--no-offline", "--instances", "--project", "My Project" },
            Argv("claude", "daily", ("project", "My Project"), ("instances", "true"), ("offline", "false"), ("breakdown", "false")));

    [TestMethod]
    public void Blocks_Values() =>
        CollectionAssert.AreEqual(
            new[] { "blocks", "--json", "--active", "--token-limit", "max", "--session-length", "2.5" },
            Argv("claude", "blocks", ("token-limit", "max"), ("session-length", "2.5"), ("active", "true")));

    [TestMethod]
    public void Statusline_Options() =>
        CollectionAssert.AreEqual(
            new[] { "statusline", "--visual-burn-rate", "emoji", "--no-cache" },
            Argv("claude", "statusline", ("cache", "false"), ("visual-burn-rate", "emoji")));

    [TestMethod]
    public void Codex_Speed() => CollectionAssert.AreEqual(new[] { "codex", "daily", "--json", "--speed", "flex" }, Argv("codex", "daily", ("speed", "flex")));

    [TestMethod]
    public void PathList_IsOneElement() => CollectionAssert.AreEqual(new[] { "pi", "daily", "--json", "--pi-path", "/a,/b c" }, Argv("pi", "daily", ("pi-path", "/a,/b c")));

    [TestMethod]
    public void AllWeekly_StartOfWeek() =>
        CollectionAssert.AreEqual(new[] { "weekly", "--json", "--start-of-week", "monday" }, Argv("all", "weekly", ("start-of-week", "monday")));

    [TestMethod]
    public void PlainBoolFalse_EmitsNothing() => CollectionAssert.AreEqual(new[] { "daily", "--json" }, Argv("all", "daily", ("no-cost", "false")));

    [TestMethod]
    public void DateValue_PassedVerbatim() =>
        CollectionAssert.AreEqual(new[] { "daily", "--json", "--since", "2026-01-01" }, Argv("all", "daily", ("since", "2026-01-01")));
}
