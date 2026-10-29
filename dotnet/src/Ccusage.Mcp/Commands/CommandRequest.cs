namespace Ccusage.Mcp.Commands;

/// <summary>One requested option, normalized to text.</summary>
/// <param name="Name">Long name without <c>--</c>.</param>
/// <param name="Raw">The value: bools as <c>true</c>/<c>false</c>, numbers as invariant text, others verbatim.</param>
public sealed record OptionValue(string Name, string Raw);

/// <summary>A normalized report request, validated and turned into engine argv by <see cref="CommandValidator"/>.</summary>
/// <param name="Source">Source id (<c>all</c> or an agent).</param>
/// <param name="Report">Report name.</param>
/// <param name="Options">Requested options, in any order.</param>
/// <param name="Stdin">Text piped to the engine's stdin (statusline), if any.</param>
public sealed record CommandRequest(string Source, string Report, IReadOnlyList<OptionValue> Options, string? Stdin = null);
