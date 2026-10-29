using Ccusage.Mcp.Commands;

namespace Ccusage.Mcp.Tests.Commands;

[TestClass]
public sealed class CommandValidatorTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;

    private static CommandRequest Req(string s, string r, params (string n, string v)[] o) =>
        new(s, r, o.Select(x => new OptionValue(x.n, x.v)).ToList());

    private static string Error(CommandRequest req) =>
        Assert.ThrowsExactly<ValidationException>(() => CommandValidator.ValidateAndBuildArgv(C, req)).Message;

    // First argv token per the argv rules.
    private static string Head(string s, string r) => s == "all" || r is "blocks" or "statusline" ? r : s;

    [TestMethod]
    public void EveryCatalogPair_Validates()
    {
        foreach (var s in C.Sources)
        {
            foreach (var r in s.Reports)
            {
                var argv = CommandValidator.ValidateAndBuildArgv(C, Req(s.Id, r));
                Assert.AreEqual(Head(s.Id, r), argv[0], $"{s.Id} {r}");
            }
        }
    }

    [TestMethod]
    [DataRow("codex", "weekly", "Report 'weekly' is not available for source 'codex'. Sources with 'weekly': all, claude, opencode.")]
    [DataRow("all", "blocks", "Report 'blocks' is not available for source 'all'. Sources with 'blocks': claude.")]
    [DataRow("opencode", "statusline", "Report 'statusline' is not available for source 'opencode'. Sources with 'statusline': claude.")]
    public void InvalidPair_Message(string s, string r, string expected) => Assert.AreEqual(expected, Error(Req(s, r)));

    [TestMethod]
    public void Validate_UnknownSource() =>
        StringAssert.StartsWith(Error(Req("foo", "daily")), "Unknown source 'foo'. Valid sources: all, claude, codex, ");

    [TestMethod]
    public void Validate_UnknownReport() =>
        Assert.AreEqual("Unknown report 'hourly'. Valid reports: daily, weekly, monthly, session, blocks, statusline.", Error(Req("all", "hourly")));

    [TestMethod]
    public void Validate_UnknownParameter_ListsValidOnes() =>
        Assert.AreEqual(
            "Parameter 'bogus' is not a parameter of 'all daily'. Valid parameters: since, until, timezone, sections, by-agent, offline, no-cost, config, last.",
            Error(Req("all", "daily", ("bogus", "1"))));

    [TestMethod]
    public void Validate_InapplicableParameter_NamesAcceptingSources() =>
        Assert.AreEqual(
            "Parameter 'speed' does not apply to 'claude daily'. Sources that accept it for 'daily': codex.",
            Error(Req("claude", "daily", ("speed", "fast"))));

    [TestMethod]
    public void Validate_Duplicate() =>
        Assert.AreEqual("Parameter 'since' was given more than once.", Error(Req("all", "daily", ("since", "20260101"), ("since", "20260102"))));

    [TestMethod]
    [DataRow("claude", "daily", "mode", "fast", "Parameter 'mode' must be one of: auto, calculate, display (got 'fast').")]
    [DataRow("all", "weekly", "start-of-week", "Monday", "Parameter 'start-of-week' must be one of: sunday, monday, tuesday, wednesday, thursday, friday, saturday (got 'Monday').")]
    [DataRow("all", "daily", "sections", "daily,hourly", "Parameter 'sections' contains 'hourly'; each item must be one of: daily, weekly, monthly, session.")]
    [DataRow("all", "daily", "sections", "", "Parameter 'sections' must not be empty.")]
    [DataRow("all", "daily", "since", "2026/01/01", "Parameter 'since' must be a date in YYYY-MM-DD or YYYYMMDD form (got '2026/01/01').")]
    [DataRow("all", "daily", "since", "20261301", "Parameter 'since' must be a date in YYYY-MM-DD or YYYYMMDD form (got '20261301').")]
    [DataRow("all", "daily", "until", "2026-02-30", "Parameter 'until' must be a date in YYYY-MM-DD or YYYYMMDD form (got '2026-02-30').")]
    [DataRow("all", "daily", "last", "0", "Parameter 'last' must be an integer >= 1 (got '0').")]
    [DataRow("all", "daily", "last", "1.5", "Parameter 'last' must be an integer >= 1 (got '1.5').")]
    [DataRow("claude", "daily", "debug-samples", "-1", "Parameter 'debug-samples' must be an integer >= 0 (got '-1').")]
    [DataRow("claude", "blocks", "token-limit", "0", "Parameter 'token-limit' must be a positive integer or \"max\" (got '0').")]
    [DataRow("claude", "blocks", "token-limit", "MAX", "Parameter 'token-limit' must be a positive integer or \"max\" (got 'MAX').")]
    [DataRow("claude", "blocks", "session-length", "abc", "Parameter 'session-length' must be a number >= 0 (got 'abc').")]
    [DataRow("all", "daily", "offline", "yes", "Parameter 'offline' must be true or false (got 'yes').")]
    [DataRow("all", "daily", "by-agent", "1", "Parameter 'by-agent' must be true or false (got '1').")]
    [DataRow("claude", "daily", "project", "", "Parameter 'project' must not be empty.")]
    [DataRow("all", "daily", "timezone", "Asia Tokyo", "Parameter 'timezone' must be an IANA time zone id (got 'Asia Tokyo').")]
    public void InvalidValue_Message(string s, string r, string n, string v, string expected) => Assert.AreEqual(expected, Error(Req(s, r, (n, v))));

    [TestMethod]
    [DataRow("claude", "daily", "mode", "display")]
    [DataRow("all", "daily", "sections", "daily, session")]
    [DataRow("all", "daily", "since", "20260101")]
    [DataRow("all", "daily", "since", "2026-01-01")]
    [DataRow("all", "daily", "last", "1")]
    [DataRow("claude", "daily", "debug-samples", "0")]
    [DataRow("claude", "blocks", "token-limit", "500000")]
    [DataRow("claude", "blocks", "token-limit", "max")]
    [DataRow("claude", "blocks", "session-length", "2.5")]
    [DataRow("all", "daily", "offline", "false")]
    [DataRow("all", "daily", "timezone", "America/New_York")]
    public void ValidValue_Accepted(string s, string r, string n, string v) =>
        Assert.AreEqual(Head(s, r), CommandValidator.ValidateAndBuildArgv(C, Req(s, r, (n, v)))[0]);
}
