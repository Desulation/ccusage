using System.Globalization;

namespace Ccusage.Mcp.Commands;

/// <summary>Validates a <see cref="CommandRequest"/> against the <see cref="CommandCatalog"/> and builds the engine argv.</summary>
public static class CommandValidator
{
    private const string AllSource = "all";
    private const string ClaudeSource = "claude";
    private const string StatuslineReport = "statusline";

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyyMMdd"];

    /// <summary>
    /// The engine argv for the request: the source (omitted for <c>all</c> and for the Claude root reports
    /// <c>blocks</c>/<c>statusline</c>), the report, <c>--json</c> (except for <c>statusline</c>), then the
    /// options in the page's catalog order.
    /// </summary>
    /// <exception cref="ValidationException">The request does not match the catalog; the message says why.</exception>
    public static IReadOnlyList<string> ValidateAndBuildArgv(CommandCatalog catalog, CommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        var (source, report) = (request.Source, request.Report);
        if (!catalog.Sources.Any(s => s.Id == source))
        {
            throw new ValidationException($"Unknown source '{source}'. Valid sources: {Join(catalog.Sources.Select(s => s.Id))}.");
        }

        if (!catalog.Reports.Contains(report))
        {
            throw new ValidationException($"Unknown report '{report}'. Valid reports: {Join(catalog.Reports)}.");
        }

        if (!catalog.TryGetPage(source, report, out var page))
        {
            throw PairUnavailable(catalog, source, report);
        }

        var given = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var option in request.Options)
        {
            var spec = page.Options.FirstOrDefault(o => o.Name == option.Name);
            if (spec is null)
            {
                throw new ValidationException(catalog.UnionOptions(report).Any(o => o.Name == option.Name)
                    ? $"Parameter '{option.Name}' does not apply to '{source} {report}'. Sources that accept it for '{report}': {Join(catalog.SourcesAccepting(report, option.Name))}."
                    : $"Parameter '{option.Name}' is not a parameter of '{source} {report}'. Valid parameters: {Join(page.Options.Select(o => o.Name))}.");
            }

            if (!given.TryAdd(option.Name, option.Raw))
            {
                throw new ValidationException($"Parameter '{option.Name}' was given more than once.");
            }

            ValidateValue(spec, option.Raw);
        }

        var argv = new List<string>();
        if (source != AllSource && !(source == ClaudeSource && report is "blocks" or StatuslineReport))
        {
            argv.Add(source);
        }

        argv.Add(report);
        if (report != StatuslineReport)
        {
            argv.Add("--json");
        }

        foreach (var spec in page.Options)
        {
            if (given.TryGetValue(spec.Name, out var raw))
            {
                AppendOption(argv, spec, raw);
            }
        }

        return argv.AsReadOnly();
    }

    /// <summary>The error for a (source, report) pair the catalog has no page for; shared with <see cref="CliArgsParser"/>.</summary>
    internal static ValidationException PairUnavailable(CommandCatalog catalog, string source, string report) =>
        new($"Report '{report}' is not available for source '{source}'. Sources with '{report}': {Join(catalog.SourcesWithReport(report))}.");

    private static void AppendOption(List<string> argv, OptionSpec spec, string raw)
    {
        switch (spec.Kind)
        {
            case OptionKind.Bool:
                if (raw == "true")
                {
                    argv.Add($"--{spec.Name}");
                }

                break;
            case OptionKind.NegatableBool:
                argv.Add(raw == "true" ? $"--{spec.Name}" : $"--no-{spec.Name}");
                break;
            default:
                argv.Add($"--{spec.Name}");
                argv.Add(raw);
                break;
        }
    }

    private static void ValidateValue(OptionSpec spec, string raw)
    {
        var p = spec.Name;
        switch (spec.Kind)
        {
            case OptionKind.Bool or OptionKind.NegatableBool:
                if (raw is not ("true" or "false"))
                {
                    throw new ValidationException($"Parameter '{p}' must be true or false (got '{raw}').");
                }

                break;

            case OptionKind.Enum:
                if (!spec.EnumValues.Contains(raw))
                {
                    throw new ValidationException($"Parameter '{p}' must be one of: {Join(spec.EnumValues)} (got '{raw}').");
                }

                break;

            case OptionKind.CsvEnum:
                RequireNonEmpty(p, raw);
                foreach (var item in raw.Split(',').Select(i => i.Trim()))
                {
                    RequireNonEmpty(p, item);
                    if (!spec.EnumValues.Contains(item))
                    {
                        throw new ValidationException($"Parameter '{p}' contains '{item}'; each item must be one of: {Join(spec.EnumValues)}.");
                    }
                }

                break;

            case OptionKind.Date:
                RequireNonEmpty(p, raw);
                if (!DateOnly.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    throw new ValidationException($"Parameter '{p}' must be a date in YYYY-MM-DD or YYYYMMDD form (got '{raw}').");
                }

                break;

            case OptionKind.Int:
                var min = p == "last" ? 1 : 0;
                if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var integer) || integer < min)
                {
                    throw new ValidationException($"Parameter '{p}' must be an integer >= {min} (got '{raw}').");
                }

                break;

            case OptionKind.Number:
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0)
                {
                    throw new ValidationException($"Parameter '{p}' must be a number >= 0 (got '{raw}').");
                }

                break;

            case OptionKind.IntOrMax:
                if (raw != "max" && !(long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) && limit >= 1))
                {
                    throw new ValidationException($"Parameter '{p}' must be a positive integer or \"max\" (got '{raw}').");
                }

                break;

            default: // String, Path, PathList
                RequireNonEmpty(p, raw);

                // The engine owns the authoritative time zone check; whitespace is never part of an IANA id.
                if (p == "timezone" && raw.Any(char.IsWhiteSpace))
                {
                    throw new ValidationException($"Parameter '{p}' must be an IANA time zone id (got '{raw}').");
                }

                break;
        }
    }

    private static void RequireNonEmpty(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"Parameter '{name}' must not be empty.");
        }
    }

    private static string Join(IEnumerable<string> values) => string.Join(", ", values);
}
