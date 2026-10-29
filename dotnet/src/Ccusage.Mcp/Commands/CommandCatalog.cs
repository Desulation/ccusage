using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ccusage.Mcp.Json;

namespace Ccusage.Mcp.Commands;

/// <summary>One engine option as it applies to a page.</summary>
/// <param name="Name">Long name without <c>--</c>.</param>
/// <param name="ShortAlias">Single-dash alias without <c>-</c>, if any.</param>
/// <param name="TakesValue">The flag takes a value (<c>&lt;x&gt;</c> or <c>[x]</c>).</param>
/// <param name="ValueOptional">The value may be omitted (<c>[x]</c>).</param>
/// <param name="Description">Help text from cli-help.json.</param>
/// <param name="Default">Default shown in cli-help.json, if any.</param>
/// <param name="Kind">Value type from <see cref="OptionTypeTable"/>.</param>
/// <param name="EnumValues">Allowed values: cli-help.json <c>choices</c>, else the type table's; empty for non-enum kinds.</param>
public sealed record OptionSpec(string Name, string? ShortAlias, bool TakesValue, bool ValueOptional,
    string Description, string? Default, OptionKind Kind, IReadOnlyList<string> EnumValues);

/// <summary>A runnable (source, report) pair and the options it accepts, in catalog order.</summary>
public sealed record PageSpec(string Source, string Report, string Description, IReadOnlyList<OptionSpec> Options);

/// <summary>A usage source (<c>all</c> or an agent) and the reports it supports.</summary>
public sealed record SourceSpec(string Id, string DisplayName, IReadOnlyList<string> Reports);

/// <summary>
/// The engine's command surface — sources, the (source, report) support matrix and per-page options —
/// derived from the upstream cli-commands.json and cli-help.json embedded in this assembly.
/// </summary>
public sealed class CommandCatalog
{
    private const string AllSource = "all";
    private const string ClaudeSource = "claude";

    private static readonly Lazy<CommandCatalog> Cached = new(Load);

    // Reports of the synthesized "all" source.
    private static readonly string[] AllSourceReports = ["daily", "weekly", "monthly", "session"];

    // Defined, not derived: the root command list orders daily, monthly, weekly.
    private static readonly string[] CatalogReports = ["daily", "weekly", "monthly", "session", "blocks", "statusline"];

    // Root pages that are Claude-only reports rather than "all" reports.
    private static readonly FrozenSet<string> ClaudeRootReports = FrozenSet.ToFrozenSet(["blocks", "statusline"], StringComparer.Ordinal);

    private readonly FrozenDictionary<(string Source, string Report), PageSpec> _pages;
    private readonly FrozenDictionary<string, IReadOnlyList<OptionSpec>> _unions;

    private CommandCatalog(IReadOnlyList<SourceSpec> sources, FrozenDictionary<(string Source, string Report), PageSpec> pages)
    {
        Sources = sources;
        _pages = pages;
        _unions = CatalogReports.ToFrozenDictionary(r => r, BuildUnion, StringComparer.Ordinal);
    }

    /// <summary>The catalog loaded once from the embedded JSON.</summary>
    public static CommandCatalog Instance => Cached.Value;

    /// <summary>Engine flags never offered to MCP clients (output format, help, compatibility no-ops).</summary>
    public static IReadOnlySet<string> NeverExposed { get; } =
        FrozenSet.ToFrozenSet(["json", "compact", "color", "no-color", "help", "version", "all"], StringComparer.Ordinal);

    /// <summary><c>all</c> first, then the agent sources in cli-commands.json order.</summary>
    public IReadOnlyList<SourceSpec> Sources { get; }

    /// <summary>Every report name, in the defined order daily, weekly, monthly, session, blocks, statusline.</summary>
    public IReadOnlyList<string> Reports { get; } = Array.AsReadOnly(CatalogReports);

    /// <summary>Loads a new catalog from the embedded cli-commands.json and cli-help.json.</summary>
    public static CommandCatalog Load()
    {
        var commands = ReadResource("cli-commands.json", McpJsonContext.Default.CliCommandsFile);
        var help = ReadResource("cli-help.json", McpJsonContext.Default.DictionaryStringListCliHelpEntry);
        var helpNames = help.Values.SelectMany(g => g).Select(e => ParseFlags(e.Flags).Name).ToFrozenSet(StringComparer.Ordinal);

        var sources = new List<SourceSpec> { new(AllSource, OptionTypeTable.DisplayNameFor(AllSource), Array.AsReadOnly(AllSourceReports)) };
        var pages = new Dictionary<(string Source, string Report), PageSpec>();
        foreach (var page in commands.Pages)
        {
            if (page.Commands is not null && page.Path.Count == 1)
            {
                var id = page.Path[0];
                sources.Add(new SourceSpec(id, OptionTypeTable.DisplayNameFor(id), page.Commands.Select(c => c.Name).ToArray().AsReadOnly()));
            }

            if (page.Options is null)
            {
                continue;
            }

            var key = page.Path switch
            {
                [var report] => (ClaudeRootReports.Contains(report) ? ClaudeSource : AllSource, report),
                [var source, var report] => (source, report),
                _ => throw new InvalidOperationException($"cli-commands.json page path [{string.Join(", ", page.Path)}] has an unexpected length."),
            };
            if (!pages.ContainsKey(key))
            {
                pages[key] = new PageSpec(key.Item1, key.Item2, page.Description, BuildOptions(page.Options, commands.CombinedOptions, help, helpNames));
            }
        }

        foreach (var source in sources)
        {
            foreach (var report in source.Reports)
            {
                if (!pages.ContainsKey((source.Id, report)))
                {
                    throw new InvalidOperationException($"cli-commands.json lists {source.Id} {report} but has no options page for it.");
                }
            }
        }

        return new CommandCatalog(sources.AsReadOnly(), pages.ToFrozenDictionary());
    }

    /// <summary>The page for a (source, report) pair, if the engine supports it.</summary>
    public bool TryGetPage(string source, string report, [NotNullWhen(true)] out PageSpec? page) =>
        _pages.TryGetValue((source, report), out page);

    /// <summary>Source ids supporting the report, in <see cref="Sources"/> order.</summary>
    public IReadOnlyList<string> SourcesWithReport(string report) =>
        Sources.Where(s => s.Reports.Contains(report)).Select(s => s.Id).ToArray();

    /// <summary>Source ids whose page for the report accepts the option, in <see cref="Sources"/> order.</summary>
    public IReadOnlyList<string> SourcesAccepting(string report, string optionName) =>
        Sources.Where(s => TryGetPage(s.Id, report, out var page) && page.Options.Any(o => o.Name == optionName))
            .Select(s => s.Id).ToArray();

    /// <summary>
    /// The options of every source's page for the report, in <see cref="Sources"/> then catalog order,
    /// deduplicated by name (first wins); empty for an unknown report.
    /// </summary>
    public IReadOnlyList<OptionSpec> UnionOptions(string report) =>
        _unions.TryGetValue(report, out var union) ? union : [];

    private IReadOnlyList<OptionSpec> BuildUnion(string report)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return Sources
            .Select(s => TryGetPage(s.Id, report, out var page) ? page.Options : [])
            .SelectMany(options => options)
            .Where(o => seen.Add(o.Name))
            .ToArray()
            .AsReadOnly();
    }

    private static ReadOnlyCollection<OptionSpec> BuildOptions(string optionsKey, Dictionary<string, List<string>> combined,
        Dictionary<string, List<CliHelpEntry>> help, FrozenSet<string> helpNames)
    {
        var groups = combined.TryGetValue(optionsKey, out var named) ? named : [optionsKey];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var options = new List<OptionSpec>();
        foreach (var group in groups)
        {
            if (!help.TryGetValue(group, out var entries))
            {
                throw new InvalidOperationException($"cli-help.json has no option group '{group}'.");
            }

            foreach (var entry in entries)
            {
                var (name, alias, takesValue, valueOptional) = ParseFlags(entry.Flags);

                // "--no-x" whose "--x" exists is a negation, expressed through x's NegatableBool; a "--no-x"
                // without a positive counterpart (no-cost) is a real option.
                var isNegation = name.StartsWith("no-", StringComparison.Ordinal) && helpNames.Contains(name[3..]);
                if (NeverExposed.Contains(name) || isNegation || !seen.Add(name))
                {
                    continue;
                }

                if (!OptionTypeTable.Entries.TryGetValue(name, out var type))
                {
                    throw new InvalidOperationException($"Option --{name} has no OptionTypeTable entry.");
                }

                var enumValues = entry.Choices is { } choices ? choices.ToArray() : type.EnumValues.ToArray();
                options.Add(new OptionSpec(name, alias, takesValue, valueOptional, entry.Description, entry.Default, type.Kind, enumValues.AsReadOnly()));
            }
        }

        return options.AsReadOnly();
    }

    // "-s, --since <since>" -> (since, s, true, false); "--speed [speed]" -> (speed, null, true, true); "--by-agent" -> (by-agent, null, false, false).
    private static (string Name, string? Alias, bool TakesValue, bool ValueOptional) ParseFlags(string flags)
    {
        string? name = null;
        string? alias = null;
        var takesValue = false;
        var valueOptional = false;
        foreach (var token in flags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.TrimEnd(',')))
        {
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                name = token[2..];
            }
            else if (token.StartsWith('-'))
            {
                alias = token[1..];
            }
            else if (token.StartsWith('<'))
            {
                takesValue = true;
            }
            else if (token.StartsWith('['))
            {
                takesValue = true;
                valueOptional = true;
            }
        }

        return (name ?? throw new InvalidOperationException($"cli-help.json flags '{flags}' have no long name."), alias, takesValue, valueOptional);
    }

    private static T ReadResource<T>(string name, JsonTypeInfo<T> typeInfo)
    {
        using var stream = typeof(CommandCatalog).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource {name} is missing.");
        return JsonSerializer.Deserialize(stream, typeInfo)
            ?? throw new InvalidOperationException($"Embedded resource {name} deserialized to null.");
    }
}
