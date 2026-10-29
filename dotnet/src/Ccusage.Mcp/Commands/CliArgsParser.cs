using System.Collections.Frozen;
using System.Text;

namespace Ccusage.Mcp.Commands;

/// <summary>
/// Parses CLI-style text as typed after <c>ccusage</c> (the <c>report</c> tool, the <c>ccusage</c> prompt and the
/// <c>/ccusage</c> skill) into a <see cref="CommandRequest"/>. Values are not validated here;
/// <see cref="CommandValidator"/> does that, so raw text and typed tools share one validation path.
/// </summary>
public static class CliArgsParser
{
    private const string DefaultSource = "all";
    private const string DefaultReport = "daily";
    private const string StatuslineReport = "statusline";

    // Short aliases of NeverExposed options; no page lists them because those options are dropped.
    private static readonly FrozenSet<string> NeverExposedShort = FrozenSet.ToFrozenSet(["j", "h", "v"], StringComparer.Ordinal);

    /// <summary>
    /// Splits on whitespace. A double-quoted span joins the current token, quotes removed, and may contain
    /// whitespace; inside quotes <c>\"</c> is a literal quote and every other character (backslashes included) is literal.
    /// </summary>
    /// <exception cref="ValidationException">A quote is not closed.</exception>
    public static IReadOnlyList<string> Tokenize(string args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var tokens = new List<string>();
        var current = new StringBuilder();
        var inToken = false;
        var inQuotes = false;
        for (var i = 0; i < args.Length; i++)
        {
            var c = args[i];
            if (inQuotes)
            {
                if (c == '\\' && i + 1 < args.Length && args[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    inToken = false;
                }
            }
            else
            {
                inToken = true;
                if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        if (inQuotes)
        {
            throw new ValidationException($"Unterminated quoted value in: {args}");
        }

        if (inToken)
        {
            tokens.Add(current.ToString());
        }

        return tokens.AsReadOnly();
    }

    /// <summary>
    /// Parses <c>[source] [report] [flags...]</c>: source defaults to <c>all</c>, report to <c>daily</c>;
    /// flags are resolved against that (source, report) page.
    /// </summary>
    /// <exception cref="ValidationException">The text does not parse; the message says why.</exception>
    public static CommandRequest Parse(CommandCatalog catalog, string args)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var tokens = Tokenize(args);
        var i = 0;
        var source = DefaultSource;
        if (i < tokens.Count && catalog.Sources.Any(s => s.Id == tokens[i]))
        {
            source = tokens[i++];
        }

        var report = DefaultReport;
        if (i < tokens.Count && catalog.Reports.Contains(tokens[i]))
        {
            report = tokens[i++];
        }

        if (report == StatuslineReport)
        {
            throw new ValidationException("'statusline' needs a hook payload; use the statusline tool.");
        }

        if (!catalog.TryGetPage(source, report, out var page))
        {
            throw CommandValidator.PairUnavailable(catalog, source, report);
        }

        var options = new List<OptionValue>();
        while (i < tokens.Count)
        {
            var token = tokens[i++];
            OptionSpec? spec;
            string flag;
            string? inline = null;
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var body = token[2..];
                var eq = body.IndexOf('=', StringComparison.Ordinal);
                var name = eq < 0 ? body : body[..eq];
                inline = eq < 0 ? null : body[(eq + 1)..];
                flag = $"--{name}";
                if (CommandCatalog.NeverExposed.Contains(name))
                {
                    throw NotAvailable(flag);
                }

                spec = page.Options.FirstOrDefault(o => o.Name == name);
                if (spec is null && name.StartsWith("no-", StringComparison.Ordinal) && inline is null)
                {
                    var positive = page.Options.FirstOrDefault(o => o.Name == name[3..]);
                    if (positive is { Kind: OptionKind.NegatableBool })
                    {
                        options.Add(new OptionValue(positive.Name, "false"));
                        continue;
                    }
                }
            }
            else if (token.Length > 1 && token[0] == '-')
            {
                var alias = token[1..];
                flag = token;
                if (NeverExposedShort.Contains(alias))
                {
                    throw NotAvailable(flag);
                }

                spec = page.Options.FirstOrDefault(o => o.ShortAlias == alias);
            }
            else
            {
                throw Unknown(token);
            }

            if (spec is null)
            {
                throw Unknown(flag);
            }

            options.Add(new OptionValue(spec.Name, inline ?? ReadValue(spec, flag, tokens, ref i)));
        }

        return new CommandRequest(source, report, options.AsReadOnly());
    }

    // The value of a flag given without "=value": the next token where the option takes one, else "true".
    private static string ReadValue(OptionSpec spec, string flag, IReadOnlyList<string> tokens, ref int i)
    {
        if (!spec.TakesValue)
        {
            return "true";
        }

        if (i < tokens.Count && !tokens[i].StartsWith('-'))
        {
            return tokens[i++];
        }

        return spec.ValueOptional
            ? spec.Default ?? "true"
            : throw new ValidationException($"Flag '{flag}' needs a value.");
    }

    private static ValidationException NotAvailable(string flag) => new($"Flag '{flag}' is not available through the MCP server.");

    private static ValidationException Unknown(string token) => new($"Unknown argument '{token}'.");
}
