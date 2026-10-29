using Ccusage.Mcp.Commands;

namespace Ccusage.Mcp.Tests.Commands;

[TestClass]
public sealed class CliArgsParserTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;

    private static CommandRequest P(string a) => CliArgsParser.Parse(C, a);

    private static string Error(string a) => Assert.ThrowsExactly<ValidationException>(() => P(a)).Message;

    private static (string, string)[] Opts(CommandRequest r) => r.Options.Select(o => (o.Name, o.Raw)).ToArray();

    [TestMethod]
    public void Tokenize_Quotes()
    {
        CollectionAssert.AreEqual(new[] { "claude", "daily", "--project", "My Project", "--since", "20260101" },
            CliArgsParser.Tokenize("claude daily --project \"My Project\" --since 20260101").ToArray());
        CollectionAssert.AreEqual(new[] { "--project=A B" }, CliArgsParser.Tokenize("--project=\"A B\"").ToArray());
        CollectionAssert.AreEqual(new[] { "say \"hi\"" }, CliArgsParser.Tokenize("\"say \\\"hi\\\"\"").ToArray());
    }

    [TestMethod]
    public void Tokenize_EmptyQuotes_IsEmptyToken() =>
        CollectionAssert.AreEqual(new[] { "--project", "" }, CliArgsParser.Tokenize("--project \"\"").ToArray());

    [TestMethod]
    public void Tokenize_BackslashOutsideEscapedQuote_IsLiteral() =>
        CollectionAssert.AreEqual(new[] { @"C:\logs\a b", @"x\y" }, CliArgsParser.Tokenize(@"""C:\logs\a b"" x\y").ToArray());

    [TestMethod]
    public void Tokenize_TabsAndNewlines_Separate() =>
        CollectionAssert.AreEqual(new[] { "daily", "--offline" }, CliArgsParser.Tokenize("\tdaily\r\n --offline ").ToArray());

    [TestMethod]
    public void Tokenize_UnterminatedQuote_Throws() =>
        Assert.AreEqual("Unterminated quoted value in: daily --project \"x", Error("daily --project \"x"));

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Parse_Empty_IsAllDaily(string a)
    {
        var r = P(a);
        Assert.AreEqual(("all", "daily", 0), (r.Source, r.Report, r.Options.Count));
    }

    [TestMethod]
    public void Parse_ReportOnly()
    {
        var r = P("weekly --last 2 --by-agent");
        Assert.AreEqual(("all", "weekly"), (r.Source, r.Report));
        CollectionAssert.AreEqual(new[] { ("last", "2"), ("by-agent", "true") }, Opts(r));
    }

    [TestMethod]
    public void Parse_SourceAndReport()
    {
        var r = P("codex daily --since 2026-10-01");
        Assert.AreEqual(("codex", "daily"), (r.Source, r.Report));
        CollectionAssert.AreEqual(new[] { ("since", "2026-10-01") }, Opts(r));
    }

    [TestMethod]
    public void Parse_SourceOnly_DefaultsDaily() => Assert.AreEqual(("codex", "daily"), (P("codex").Source, P("codex").Report));

    [TestMethod]
    public void Parse_EqualsForm() => CollectionAssert.AreEqual(new[] { ("since", "20260101") }, Opts(P("daily --since=20260101")));

    [TestMethod]
    public void Parse_QuotedValue() =>
        CollectionAssert.AreEqual(new[] { ("project", "My Project") }, Opts(P("claude daily --project \"My Project\"")));

    // cli-help.json reuses -i: --instances on claude daily, --id on claude session.
    [TestMethod]
    [DataRow("claude daily -i", "instances", "true")]
    [DataRow("claude session -i abc", "id", "abc")]
    [DataRow("claude daily -s 20260101", "since", "20260101")]
    [DataRow("claude daily -O", "offline", "true")]
    public void Parse_ShortAlias_PerPage(string a, string n, string v) => CollectionAssert.AreEqual(new[] { (n, v) }, Opts(P(a)));

    [TestMethod]
    public void Parse_ShortAliasCollision_ValidatesToPageFlag()
    {
        CollectionAssert.AreEqual(new[] { "daily", "--json", "--instances" },
            CommandValidator.ValidateAndBuildArgv(C, P("claude daily -i")).Skip(1).ToArray());
        CollectionAssert.AreEqual(new[] { "session", "--json", "--id", "abc" },
            CommandValidator.ValidateAndBuildArgv(C, P("claude session -i abc")).Skip(1).ToArray());
    }

    [TestMethod]
    public void Parse_UnknownShortAlias() => Assert.AreEqual("Unknown argument '-Z'.", Error("daily -Z"));

    [TestMethod]
    [DataRow("codex daily --speed", "speed", "auto")]
    [DataRow("codex daily --speed fast", "speed", "fast")]
    [DataRow("claude blocks -n", "session-length", "5")]
    public void Parse_OptionalValue(string a, string n, string v) => CollectionAssert.AreEqual(new[] { (n, v) }, Opts(P(a)));

    [TestMethod]
    public void Parse_OptionalValue_StopsAtFlag() =>
        CollectionAssert.AreEqual(new[] { ("speed", "auto"), ("offline", "true") }, Opts(P("codex daily --speed --offline")));

    [TestMethod]
    public void Parse_RequiredValue_StopsAtFlag() => Assert.AreEqual("Flag '--since' needs a value.", Error("daily --since --offline"));

    [TestMethod]
    public void Parse_RequiredValue_ShortAliasMissing() => Assert.AreEqual("Flag '-s' needs a value.", Error("claude daily -s"));

    [TestMethod]
    public void Parse_NegatedNegatable() => CollectionAssert.AreEqual(new[] { ("offline", "false") }, Opts(P("claude daily --no-offline")));

    [TestMethod]
    public void Parse_NegatedPlainBool_Unknown() => Assert.AreEqual("Unknown argument '--no-breakdown'.", Error("claude daily --no-breakdown"));

    [TestMethod]
    public void Parse_NoCost_IsRealOption() => CollectionAssert.AreEqual(new[] { ("no-cost", "true") }, Opts(P("daily --no-cost")));

    [TestMethod]
    [DataRow("--json")]
    [DataRow("-j")]
    [DataRow("--compact")]
    [DataRow("--color")]
    [DataRow("--no-color")]
    [DataRow("--help")]
    [DataRow("-h")]
    [DataRow("--version")]
    [DataRow("-v")]
    [DataRow("--all")]
    public void Parse_NeverExposed(string flag) =>
        Assert.AreEqual($"Flag '{flag}' is not available through the MCP server.", Error("daily " + flag));

    [TestMethod]
    public void Parse_NeverExposed_EqualsForm() =>
        Assert.AreEqual("Flag '--color' is not available through the MCP server.", Error("daily --color=always"));

    [TestMethod]
    [DataRow("statusline")]
    [DataRow("claude statusline --offline")]
    public void Parse_Statusline_Rejected(string a) =>
        Assert.AreEqual("'statusline' needs a hook payload; use the statusline tool.", Error(a));

    [TestMethod]
    [DataRow("daily foo", "Unknown argument 'foo'.")]
    [DataRow("foo", "Unknown argument 'foo'.")]
    [DataRow("daily --offline weekly", "Unknown argument 'weekly'.")]
    [DataRow("daily --bogus", "Unknown argument '--bogus'.")]
    [DataRow("daily --bogus=1", "Unknown argument '--bogus'.")]
    [DataRow("daily --since", "Flag '--since' needs a value.")]
    [DataRow("codex weekly", "Report 'weekly' is not available for source 'codex'. Sources with 'weekly': all, claude, opencode.")]
    public void Parse_Errors(string a, string expected) => Assert.AreEqual(expected, Error(a));

    [TestMethod]
    public void Parse_ValuesNotValidated() =>
        CollectionAssert.AreEqual(new[] { ("since", "yesterday"), ("offline", "maybe") }, Opts(P("daily --since yesterday --offline=maybe")));
}
