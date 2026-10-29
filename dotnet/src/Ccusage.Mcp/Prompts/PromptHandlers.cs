using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Engine;
using Ccusage.Mcp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Prompts;

/// <summary>
/// <c>prompts/list</c> and <c>prompts/get</c> (spec §5.6). The <c>ccusage</c> prompt runs its report through
/// <see cref="EngineCall.ExecuteAsync"/> and returns one user message with the result or the error text.
/// </summary>
public sealed class PromptHandlers(CommandCatalog catalog, EngineLocator locator, EngineRunner runner)
{
    private const string Instruction =
        "Present this report as a readable table with totals, and call out the main cost drivers (the models, days, or sessions with the highest cost).";

    private readonly CommandCatalog catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly EngineLocator locator = locator ?? throw new ArgumentNullException(nameof(locator));
    private readonly EngineRunner runner = runner ?? throw new ArgumentNullException(nameof(runner));

    /// <summary>The one prompt, <c>ccusage</c>.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance member of the handler contract, resolved from DI like GetAsync.")]
    public ValueTask<ListPromptsResult> ListAsync(CancellationToken ct) =>
        ValueTask.FromResult(new ListPromptsResult { Prompts = [PromptDefinitions.Ccusage] });

    /// <summary>Runs the <c>ccusage</c> prompt's report (<c>args</c> missing means <c>""</c>, i.e. daily for all sources).</summary>
    /// <exception cref="McpException">The prompt is unknown, or <c>args</c> is not a string.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public async ValueTask<GetPromptResult> GetAsync(string name, IDictionary<string, JsonElement>? args, CancellationToken ct)
    {
        if (name != PromptDefinitions.Ccusage.Name)
        {
            throw new McpException($"Unknown prompt '{name}'.");
        }

        string text;
        try
        {
            text = ToolArguments.CliText(args, required: false);
        }
        catch (ValidationException e)
        {
            throw new McpException(e.Message);
        }

        var result = await EngineCall.ExecuteAsync(catalog, locator, runner, () => CliArgsParser.Parse(catalog, text), ct).ConfigureAwait(false);

        var rendered = text.Trim() is { Length: > 0 } trimmed ? trimmed : "daily";
        var message = result.Error is null
            ? $"ccusage {rendered}\n\n```json\n{result.Stdout}\n```\n\n{Instruction}"
            : $"ccusage {rendered} failed:\n\n{result.Error}";
        return new GetPromptResult
        {
            Messages = [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = message } }],
        };
    }
}
