using System.Text.Json.Serialization;

namespace Ccusage.Mcp.Json;

/// <summary>Source-generated (AOT-safe) JSON metadata for every type the server serializes.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CliCommandsFile))]
[JsonSerializable(typeof(Dictionary<string, List<CliHelpEntry>>))]
internal sealed partial class McpJsonContext : JsonSerializerContext;

/// <summary>rust/crates/ccusage-cli-parser/src/cli-commands.json.</summary>
internal sealed record CliCommandsFile(Dictionary<string, List<string>> CombinedOptions, CliRoot Root, List<CliPage> Pages);

internal sealed record CliRoot(List<string> Usage, List<CliCommand> Commands);

internal sealed record CliCommand(string Name, string Description);

/// <summary>A help page: a leaf command carries <c>Options</c> (a combined-options key or a cli-help.json group), a parent carries <c>Commands</c>.</summary>
internal sealed record CliPage(List<string> Path, string Description, string? Usage, string? Options, List<CliCommand>? Commands);

/// <summary>An entry of a cli-help.json option group.</summary>
internal sealed record CliHelpEntry(string Flags, string Description, string? Default, List<string>? Choices);
