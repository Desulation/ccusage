namespace Ccusage.Mcp.Engine;

/// <summary>
/// What an engine run produced. When <see cref="TimedOut"/> is true the process tree was killed and
/// <see cref="ExitCode"/> is the killed process's code; the output is whatever was written before the kill.
/// </summary>
public sealed record EngineResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);
