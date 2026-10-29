namespace Ccusage.Mcp.Engine;

/// <summary>One engine run: the executable, its argv (passed verbatim, no shell), and stdin for <c>statusline</c>.</summary>
/// <param name="Environment">
/// Per-run overrides merged over the inherited environment after <c>NO_COLOR=1</c>; a <see langword="null"/> value
/// removes the variable.
/// </param>
public sealed record EngineInvocation(
    string EnginePath,
    IReadOnlyList<string> Argv,
    string? Stdin,
    IReadOnlyDictionary<string, string?>? Environment = null);
