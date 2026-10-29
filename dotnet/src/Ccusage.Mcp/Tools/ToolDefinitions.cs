using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ccusage.Mcp.Commands;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Tools;

/// <summary>
/// The MCP tool definitions (spec §5): the typed report tools with input schemas generated from the
/// <see cref="CommandCatalog"/>, plus <c>list-sources</c> and <c>report</c>. Every schema is strict
/// (<c>additionalProperties: false</c>) and lists its properties in catalog order, matching what
/// <see cref="CommandValidator"/> accepts.
/// </summary>
public static class ToolDefinitions
{
    /// <summary>The <c>report</c> tool's <c>args</c> description, shared with the <c>ccusage</c> prompt.</summary>
    public const string ReportArgsDescription =
        """CLI-style arguments as typed after `ccusage`, e.g. "codex daily --since 2026-10-01", "weekly --last 2 --by-agent", "" (= daily, all sources).""";

    private const string DatePattern = @"^(\d{4}-\d{2}-\d{2}|\d{8})$";
    private const string IntOrMaxPattern = @"^([1-9]\d*|max)$";

    private const string HookInputDescription =
        "Claude Code status-line hook payload (session_id, transcript_path, cwd, model, workspace, version, cost, context_window). Written to the engine's stdin unchanged.";

    private static readonly string[] SourceReports = ["daily", "weekly", "monthly", "session"];

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The eight tools: daily, weekly, monthly, session, blocks, statusline, list-sources, report.</summary>
    public static IReadOnlyList<Tool> Build(CommandCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return new[]
        {
            NewTool("daily", ReportDescription("day"), BuildReportSchema(catalog, "daily")),
            NewTool("weekly", ReportDescription("week"), BuildReportSchema(catalog, "weekly")),
            NewTool("monthly", ReportDescription("month"), BuildReportSchema(catalog, "monthly")),
            NewTool("session", ReportDescription("session"), BuildReportSchema(catalog, "session")),
            NewTool("blocks", "Claude Code usage grouped by 5-hour billing blocks (Claude only).", BuildReportSchema(catalog, "blocks")),
            NewTool("statusline", "Claude Code status line text for a hook payload (Claude only; returns plain text).", BuildReportSchema(catalog, "statusline")),
            NewTool("list-sources",
                "List supported sources, their reports, and the server/engine versions and engine path (engine-error when the engine cannot be located or queried).",
                BuildSchema(_ => { })),
            NewTool("report", "Run any ccusage report from CLI-style arguments; same validation as the typed tools.",
                BuildSchema(w =>
                {
                    w.WriteStartObject("args");
                    w.WriteString("type", "string");
                    w.WriteString("description", ReportArgsDescription);
                    w.WriteEndObject();
                }, "args")),
        }.AsReadOnly();
    }

    /// <summary>
    /// The input schema of a catalog report: <c>source</c> first for daily/weekly/monthly/session, <c>hook-input</c>
    /// (required) first for statusline, then the union of the report's options across sources in catalog order.
    /// </summary>
    public static JsonElement BuildReportSchema(CommandCatalog catalog, string report)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!catalog.Reports.Contains(report))
        {
            throw new ArgumentException($"Unknown report '{report}'.", nameof(report));
        }

        var withReport = catalog.SourcesWithReport(report);
        var isStatusline = report == "statusline";
        return BuildSchema(w =>
        {
            if (SourceReports.Contains(report))
            {
                w.WriteStartObject("source");
                w.WriteString("type", "string");
                WriteStringArray(w, "enum", withReport);
                w.WriteString("default", "all");
                w.WriteString("description", "Data source. Default: all.");
                w.WriteEndObject();
            }

            if (isStatusline)
            {
                w.WriteStartObject("hook-input");
                w.WriteString("type", "object");
                w.WriteString("description", HookInputDescription);
                w.WriteEndObject();
            }

            foreach (var option in catalog.UnionOptions(report))
            {
                var appliesTo = catalog.SourcesAccepting(report, option.Name);
                WriteOption(w, option, appliesTo, appliesTo.Count == withReport.Count);
            }
        }, isStatusline ? "hook-input" : null);
    }

    private static string ReportDescription(string unit) =>
        $"Token usage and cost grouped by {unit}. Default source: all detected sources. Long ranges produce large JSON; prefer since/until or last.";

    private static Tool NewTool(string name, string description, JsonElement inputSchema) => new()
    {
        Name = name,
        Description = description,
        InputSchema = inputSchema,
        Annotations = new ToolAnnotations
        {
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = null,
        },
    };

    // {"type":"object","properties":{...},"additionalProperties":false[,"required":[required]]}
    private static JsonElement BuildSchema(Action<Utf8JsonWriter> writeProperties, string? required = null)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            writeProperties(w);
            w.WriteEndObject();
            w.WriteBoolean("additionalProperties", false);
            if (required is not null)
            {
                WriteStringArray(w, "required", [required]);
            }

            w.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteOption(Utf8JsonWriter w, OptionSpec o, IReadOnlyList<string> appliesTo, bool everyone)
    {
        var description = o.Description;
        w.WriteStartObject(o.Name);
        switch (o.Kind)
        {
            case OptionKind.Bool or OptionKind.NegatableBool:
                w.WriteString("type", "boolean");
                break;
            case OptionKind.Enum:
                w.WriteString("type", "string");
                WriteStringArray(w, "enum", o.EnumValues);
                break;
            case OptionKind.CsvEnum:
                w.WriteString("type", "string");
                description += $" Comma-separated; each item one of: {string.Join(", ", o.EnumValues)}.";
                break;
            case OptionKind.Date:
                w.WriteString("type", "string");
                w.WriteString("pattern", DatePattern);
                break;
            case OptionKind.Int:
                w.WriteString("type", "integer");
                w.WriteNumber("minimum", o.Name == "last" ? 1 : 0);
                break;
            case OptionKind.Number:
                w.WriteString("type", "number");
                w.WriteNumber("minimum", 0);
                break;
            case OptionKind.IntOrMax:
                w.WriteString("type", "string");
                w.WriteString("pattern", IntOrMaxPattern);
                break;
            case OptionKind.String or OptionKind.Path or OptionKind.PathList:
                w.WriteString("type", "string");
                break;
            default:
                throw new InvalidOperationException($"Option --{o.Name} has unsupported kind {o.Kind}.");
        }

        if (o.Default is not null)
        {
            description += $" Default: {o.Default}.";
        }

        if (!everyone)
        {
            description += $" Applies to: {string.Join(", ", appliesTo)}.";
        }

        w.WriteString("description", description);
        w.WriteEndObject();
    }

    private static void WriteStringArray(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    }
}
