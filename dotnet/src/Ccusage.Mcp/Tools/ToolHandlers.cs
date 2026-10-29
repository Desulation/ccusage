using System.Text.Json;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Engine;
using Ccusage.Mcp.Json;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Tools;

/// <summary>
/// <c>tools/list</c> and <c>tools/call</c> (spec §5). Every report runs through <see cref="EngineCall.ExecuteAsync"/>;
/// failures are tool errors (<c>isError: true</c>) carrying the error text, never protocol errors.
/// </summary>
public sealed class ToolHandlers(CommandCatalog catalog, EngineLocator locator, EngineRunner runner)
{
    private const string VersionPrefix = "ccusage ";

    private readonly CommandCatalog catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly EngineLocator locator = locator ?? throw new ArgumentNullException(nameof(locator));
    private readonly EngineRunner runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly IReadOnlyList<Tool> tools = ToolDefinitions.Build(catalog);

    /// <summary>The eight tool definitions.</summary>
    public ValueTask<ListToolsResult> ListAsync(CancellationToken ct) =>
        ValueTask.FromResult(new ListToolsResult { Tools = [.. tools] });

    /// <summary>Runs a tool: a typed report, <c>report</c> (CLI-style text) or <c>list-sources</c>.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public async ValueTask<CallToolResult> CallAsync(string name, IDictionary<string, JsonElement>? args, CancellationToken ct)
    {
        if (name == "list-sources")
        {
            return new CallToolResult { Content = [Text(await ListSourcesAsync(ct).ConfigureAwait(false))] };
        }

        Func<CommandRequest> request = name == "report"
            ? () => CliArgsParser.Parse(catalog, ToolArguments.CliText(args, required: true))
            : () => ToolArguments.FromTyped(catalog, name, args);
        var result = await EngineCall.ExecuteAsync(catalog, locator, runner, request, ct).ConfigureAwait(false);

        if (result.Error is not null)
        {
            return new CallToolResult { IsError = true, Content = [Text(result.Error)] };
        }

        return string.IsNullOrEmpty(result.Stderr)
            ? new CallToolResult { Content = [Text(result.Stdout ?? "")] }
            : new CallToolResult { Content = [Text(result.Stdout ?? ""), Text("stderr:\n" + result.Stderr)] };
    }

    // Never fails: an engine that cannot be located or queried is reported in engine-error.
    private async Task<string> ListSourcesAsync(CancellationToken ct)
    {
        string? enginePath = null;
        string? engineVersion = null;
        string? engineError = null;
        try
        {
            enginePath = locator.Resolve();
        }
        catch (EngineNotFoundException e)
        {
            engineError = e.Message;
        }

        if (enginePath is not null)
        {
            var version = await EngineCall.RunAsync(runner, new EngineInvocation(enginePath, ["--version"], null), ct).ConfigureAwait(false);
            engineVersion = version.Error is null ? ParseVersion(version.Stdout) : null;
            engineError = version.Error ?? (engineVersion is null ? "ccusage --version printed no version." : null);
        }

        var sources = catalog.Sources.Select(s => new SourceInfo(s.Id, s.DisplayName, s.Reports)).ToArray();
        var result = new ListSourcesResult(ServerVersion.Value, engineVersion, enginePath, engineError, sources);
        return JsonSerializer.Serialize(result, McpJsonContext.Default.ListSourcesResult);
    }

    // "ccusage 20.0.45\n" -> "20.0.45"; null when stdout has no non-empty line.
    private static string? ParseVersion(string? stdout)
    {
        var line = (stdout ?? "").ReplaceLineEndings("\n").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return line is not null && line.StartsWith(VersionPrefix, StringComparison.Ordinal) ? line[VersionPrefix.Length..] : line;
    }

    private static TextContentBlock Text(string text) => new() { Text = text };
}
