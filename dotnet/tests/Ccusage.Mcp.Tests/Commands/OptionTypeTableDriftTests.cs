using System.Text.Json;
using Ccusage.Mcp.Commands;

namespace Ccusage.Mcp.Tests.Commands;

/// <summary>
/// Guards the hand-written <see cref="OptionTypeTable"/> against drift from the embedded upstream cli-help.json.
/// </summary>
[TestClass]
public sealed class OptionTypeTableDriftTests
{
    [TestMethod]
    public void EveryHelpFlag_IsTypedOrNeverExposed()
    {
        foreach (var (flags, _) in HelpEntries())
        {
            var n = LongName(flags);
            var b = n.StartsWith("no-", StringComparison.Ordinal) && n != "no-cost" ? n[3..] : n;
            Assert.IsTrue(OptionTypeTable.Entries.ContainsKey(b) || CommandCatalog.NeverExposed.Contains(b), $"untyped flag --{n}");
        }
    }

    [TestMethod]
    public void EveryTypedEntry_ExistsInHelp()
    {
        var help = HelpEntries().Select(e => LongName(e.Flags)).ToHashSet();
        foreach (var k in OptionTypeTable.Entries.Keys)
        {
            Assert.IsTrue(help.Contains(k), $"stale table row {k}");
        }
    }

    [TestMethod]
    public void EnumKinds_HaveValues_AndMatchHelpChoices()
    {
        foreach (var (flags, choices) in HelpEntries())
        {
            var n = LongName(flags);
            if (!OptionTypeTable.Entries.TryGetValue(n, out var e) || e.Kind is not (OptionKind.Enum or OptionKind.CsvEnum))
            {
                continue;
            }

            var spec = CommandCatalog.Instance.UnionOptions(ReportFor(n)).Single(o => o.Name == n);
            Assert.IsTrue(spec.EnumValues.Count > 0, n);
            if (choices is not null)
            {
                CollectionAssert.AreEqual(choices, spec.EnumValues.ToArray(), n);
            }
        }
    }

    [TestMethod]
    public void NegatableBools_HaveNoFlagInHelp()
    {
        var help = HelpEntries().Select(e => LongName(e.Flags)).ToHashSet();
        foreach (var (k, v) in OptionTypeTable.Entries)
        {
            if (v.Kind == OptionKind.NegatableBool)
            {
                Assert.IsTrue(help.Contains("no-" + k), k);
            }
        }
    }

    [TestMethod]
    public void EverySource_HasDisplayName()
    {
        var ids = CommandCatalog.Instance.Sources.Select(s => s.Id).ToHashSet();
        foreach (var id in ids)
        {
            Assert.IsTrue(OptionTypeTable.DisplayNames.ContainsKey(id), id);
        }

        foreach (var k in OptionTypeTable.DisplayNames.Keys)
        {
            Assert.IsTrue(ids.Contains(k), $"orphan display name {k}");
        }
    }

    // (flags, choices?) for every entry of every group in the embedded cli-help.json.
    private static List<(string Flags, string[]? Choices)> HelpEntries()
    {
        using var stream = typeof(CommandCatalog).Assembly.GetManifestResourceStream("cli-help.json")
            ?? throw new InvalidOperationException("cli-help.json is not embedded in Ccusage.Mcp.");
        using var doc = JsonDocument.Parse(stream);
        var entries = new List<(string, string[]?)>();
        foreach (var group in doc.RootElement.EnumerateObject())
        {
            foreach (var entry in group.Value.EnumerateArray())
            {
                var flags = entry.GetProperty("flags").GetString()!;
                var choices = entry.TryGetProperty("choices", out var c) ? c.EnumerateArray().Select(x => x.GetString()!).ToArray() : null;
                entries.Add((flags, choices));
            }
        }

        Assert.IsTrue(entries.Count > 0, "cli-help.json has no entries");
        return entries;
    }

    // The "--name" token of a flags string, without "--".
    private static string LongName(string flags) =>
        flags.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.TrimEnd(','))
            .Single(t => t.StartsWith("--", StringComparison.Ordinal))[2..];

    // The first report (catalog order) whose union options contain the option.
    private static string ReportFor(string name) =>
        CommandCatalog.Instance.Reports.First(r => CommandCatalog.Instance.UnionOptions(r).Any(o => o.Name == name));
}
