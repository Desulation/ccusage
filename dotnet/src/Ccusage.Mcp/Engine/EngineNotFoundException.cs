namespace Ccusage.Mcp.Engine;

/// <summary>No engine exists at any candidate location; the message lists every path checked, in order.</summary>
public sealed class EngineNotFoundException(IReadOnlyList<string> checkedPaths)
    : Exception(BuildMessage(checkedPaths))
{
    public IReadOnlyList<string> CheckedPaths { get; } = checkedPaths;

    private static string BuildMessage(IReadOnlyList<string> checkedPaths) =>
        "ccusage engine not found. Set CCUSAGE_ENGINE or place ccusage next to ccusage-mcp. Checked:"
        + string.Concat(checkedPaths.Select(p => "\n  " + p));
}
