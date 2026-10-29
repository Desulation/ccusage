using System.Collections.Frozen;

namespace Ccusage.Mcp.Commands;

/// <summary>
/// Hand-written typing for the engine's options and display names for its sources. cli-help.json has no
/// value types, so they live here; OptionTypeTableDriftTests keeps this table in step with the help file.
/// </summary>
public static class OptionTypeTable
{
    /// <summary>Option long name (without <c>--</c>) to its kind and, where cli-help.json has no choices, its enum values.</summary>
    public static IReadOnlyDictionary<string, (OptionKind Kind, string[] EnumValues)> Entries { get; } =
        new Dictionary<string, (OptionKind Kind, string[] EnumValues)>(StringComparer.Ordinal)
        {
            ["since"] = (OptionKind.Date, []),
            ["until"] = (OptionKind.Date, []),
            ["timezone"] = (OptionKind.String, []),
            ["offline"] = (OptionKind.NegatableBool, []),
            ["cache"] = (OptionKind.NegatableBool, []),
            ["config"] = (OptionKind.Path, []),
            ["no-cost"] = (OptionKind.Bool, []),
            ["sections"] = (OptionKind.CsvEnum, ["daily", "weekly", "monthly", "session"]),
            ["by-agent"] = (OptionKind.Bool, []),
            ["last"] = (OptionKind.Int, []),
            ["id"] = (OptionKind.String, []),
            ["start-of-week"] = (OptionKind.Enum, []),
            ["mode"] = (OptionKind.Enum, []),
            ["order"] = (OptionKind.Enum, []),
            ["breakdown"] = (OptionKind.Bool, []),
            ["debug"] = (OptionKind.Bool, []),
            ["debug-samples"] = (OptionKind.Int, []),
            ["single-thread"] = (OptionKind.Bool, []),
            ["instances"] = (OptionKind.Bool, []),
            ["project"] = (OptionKind.String, []),
            ["project-aliases"] = (OptionKind.String, []),
            ["speed"] = (OptionKind.Enum, []),
            ["pi-path"] = (OptionKind.PathList, []),
            ["open-claw-path"] = (OptionKind.PathList, []),
            ["active"] = (OptionKind.Bool, []),
            ["recent"] = (OptionKind.Bool, []),
            ["token-limit"] = (OptionKind.IntOrMax, []),
            ["session-length"] = (OptionKind.Number, []),
            ["visual-burn-rate"] = (OptionKind.Enum, []),
            ["cost-source"] = (OptionKind.Enum, []),
            ["refresh-interval"] = (OptionKind.Int, []),
            ["context-low-threshold"] = (OptionKind.Int, []),
            ["context-medium-threshold"] = (OptionKind.Int, []),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Source id to its human-readable name.</summary>
    public static IReadOnlyDictionary<string, string> DisplayNames { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["all"] = "All sources",
            ["claude"] = "Claude Code",
            ["codex"] = "Codex",
            ["opencode"] = "OpenCode",
            ["amp"] = "Amp",
            ["droid"] = "Droid",
            ["codebuff"] = "Codebuff",
            ["hermes"] = "Hermes",
            ["pi"] = "pi-agent",
            ["goose"] = "Goose",
            ["kilo"] = "Kilo",
            ["copilot"] = "GitHub Copilot CLI",
            ["gemini"] = "Gemini CLI",
            ["antigravity"] = "Antigravity",
            ["kimi"] = "Kimi",
            ["qwen"] = "Qwen",
            ["openclaw"] = "OpenClaw",
            ["grok"] = "Grok Build CLI",
            ["zcode"] = "ZCode",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The display name for a source id; an unknown id is its own display name.</summary>
    public static string DisplayNameFor(string id) => DisplayNames.TryGetValue(id, out var name) ? name : id;
}
