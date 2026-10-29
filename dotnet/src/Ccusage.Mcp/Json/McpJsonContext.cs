using System.Text.Json.Serialization;

namespace Ccusage.Mcp.Json;

/// <summary>Source-generated (AOT-safe) JSON metadata for every type the server serializes.</summary>
/// <remarks><c>WriteIndented</c> only affects output (<see cref="ListSourcesResult"/>); the catalog files are only read.</remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(CliCommandsFile))]
[JsonSerializable(typeof(Dictionary<string, List<CliHelpEntry>>))]
[JsonSerializable(typeof(ListSourcesResult))]
internal sealed partial class McpJsonContext : JsonSerializerContext;

/// <summary>A source in the <c>list-sources</c> result.</summary>
public sealed record SourceInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("display-name")] string DisplayName,
    [property: JsonPropertyName("reports")] IReadOnlyList<string> Reports);

/// <summary>The <c>list-sources</c> result (spec §5.4); <c>engine-error</c> is omitted when the engine was located and queried.</summary>
public sealed record ListSourcesResult(
    [property: JsonPropertyName("server-version")] string ServerVersion,
    [property: JsonPropertyName("engine-version")] string? EngineVersion,
    [property: JsonPropertyName("engine-path")] string? EnginePath,
    [property: JsonPropertyName("engine-error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EngineError,
    [property: JsonPropertyName("sources")] IReadOnlyList<SourceInfo> Sources);

/// <summary>rust/crates/ccusage-cli-parser/src/cli-commands.json.</summary>
internal sealed record CliCommandsFile(Dictionary<string, List<string>> CombinedOptions, CliRoot Root, List<CliPage> Pages);

internal sealed record CliRoot(List<string> Usage, List<CliCommand> Commands);

internal sealed record CliCommand(string Name, string Description);

/// <summary>A help page: a leaf command carries <c>Options</c> (a combined-options key or a cli-help.json group), a parent carries <c>Commands</c>.</summary>
internal sealed record CliPage(List<string> Path, string Description, string? Usage, string? Options, List<CliCommand>? Commands);

/// <summary>An entry of a cli-help.json option group.</summary>
internal sealed record CliHelpEntry(string Flags, string Description, string? Default, List<string>? Choices);
