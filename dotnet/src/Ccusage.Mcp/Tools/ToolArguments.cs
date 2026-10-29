using System.Globalization;
using System.Text.Json;
using Ccusage.Mcp.Commands;

namespace Ccusage.Mcp.Tools;

/// <summary>
/// Turns MCP tool-call arguments into a <see cref="CommandRequest"/>. The SDK does not check arguments against a
/// hand-built tool's input schema, so every value is converted by its actual JSON kind and
/// <see cref="CommandValidator"/> has the final word (unknown parameters included).
/// </summary>
public static class ToolArguments
{
    private const string SourceParameter = "source";
    private const string HookInputParameter = "hook-input";
    private const string ArgsParameter = "args";
    private const string StatuslineTool = "statusline";

    // The typed tools with a source parameter; blocks and statusline are Claude-only.
    private static readonly string[] SourceTools = ["daily", "weekly", "monthly", "session"];

    /// <summary>
    /// The request for a typed report tool: <c>source</c> (default <c>all</c>) for daily/weekly/monthly/session,
    /// source <c>claude</c> for blocks/statusline, <c>hook-input</c> as statusline's stdin, and every other
    /// argument as an option (booleans as <c>true</c>/<c>false</c>, numbers as text, strings verbatim, nulls ignored).
    /// </summary>
    /// <exception cref="ValidationException">The tool is unknown or an argument has an unusable JSON type.</exception>
    public static CommandRequest FromTyped(CommandCatalog catalog, string tool, IDictionary<string, JsonElement>? args)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var hasSource = SourceTools.Contains(tool);
        if (!hasSource && tool is not ("blocks" or StatuslineTool))
        {
            throw new ValidationException($"Unknown tool '{tool}'.");
        }

        args ??= new Dictionary<string, JsonElement>();
        var source = hasSource ? "all" : "claude";
        var stdin = tool == StatuslineTool ? HookInput(args) : null;
        var options = new List<OptionValue>();
        foreach (var (name, value) in args)
        {
            if (hasSource && name == SourceParameter)
            {
                source = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString()!,
                    JsonValueKind.Null => source,
                    _ => throw Unsupported(name, value),
                };
            }
            else if (tool == StatuslineTool && name == HookInputParameter)
            {
                // Already taken as stdin.
            }
            else if (ToRaw(catalog, tool, name, value) is { } raw)
            {
                options.Add(new OptionValue(name, raw));
            }
        }

        return new CommandRequest(source, tool, options.AsReadOnly(), stdin);
    }

    /// <summary>The CLI-style text of the <c>report</c> tool's or the <c>ccusage</c> prompt's <c>args</c>.</summary>
    /// <param name="required">Whether a missing (or null) <c>args</c> is an error; otherwise it is <c>""</c>.</param>
    /// <exception cref="ValidationException"><c>args</c> is missing while required, or is not a string.</exception>
    public static string CliText(IDictionary<string, JsonElement>? args, bool required)
    {
        if (args is null || !args.TryGetValue(ArgsParameter, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return required ? throw new ValidationException($"Parameter '{ArgsParameter}' is required.") : "";
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new ValidationException($"Parameter '{ArgsParameter}' must be a string.");
    }

    private static string HookInput(IDictionary<string, JsonElement> args)
    {
        if (!args.TryGetValue(HookInputParameter, out var value))
        {
            throw new ValidationException($"Parameter '{HookInputParameter}' is required.");
        }

        return value.ValueKind == JsonValueKind.Object
            ? value.GetRawText()
            : throw new ValidationException($"Parameter '{HookInputParameter}' must be a JSON object.");
    }

    // An option's text value, or null for a JSON null (treated as not given).
    private static string? ToRaw(CommandCatalog catalog, string report, string name, JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => KindOf(catalog, report, name) is OptionKind.Int or OptionKind.IntOrMax && value.TryGetInt64(out var integer)
            ? integer.ToString(CultureInfo.InvariantCulture)
            : value.GetRawText(), // a non-integer for an integer option is left for CommandValidator to reject
        JsonValueKind.Null => null,
        _ => throw Unsupported(name, value),
    };

    private static OptionKind? KindOf(CommandCatalog catalog, string report, string name) =>
        catalog.UnionOptions(report).FirstOrDefault(o => o.Name == name)?.Kind;

    private static ValidationException Unsupported(string name, JsonElement value) =>
        new($"Parameter '{name}' has an unsupported JSON type ({value.ValueKind}).");
}
