using System.Runtime.InteropServices;

namespace Ccusage.Mcp.Engine;

/// <summary>
/// Resolves the ccusage engine (spec §6.1): <c>CCUSAGE_ENGINE</c>, then <c>&lt;server dir&gt;/ccusage[.exe]</c>,
/// then <c>&lt;server dir&gt;/&lt;os-arch&gt;/ccusage[.exe]</c>, then each <c>PATH</c> entry. The first existing file wins.
/// </summary>
/// <remarks>
/// The server directory is <see cref="AppContext.BaseDirectory"/>, never the current directory: an MCP client
/// launches the server with the session's working directory (spec §15). <see cref="RuntimeInformation.OSArchitecture"/>
/// reports the native architecture under x64 emulation on Windows ARM64 (spec §15 row 5), so no
/// <c>IsWow64Process2</c> fallback is needed.
/// </remarks>
public sealed class EngineLocator(Func<string, string?> getEnv, string serverDirectory, Architecture osArchitecture, bool isWindows)
{
    public static EngineLocator Default { get; } = new(
        Environment.GetEnvironmentVariable,
        AppContext.BaseDirectory,
        RuntimeInformation.OSArchitecture,
        OperatingSystem.IsWindows());

    private string FileName => isWindows ? "ccusage.exe" : "ccusage";

    /// <summary>Every location checked, in resolution order.</summary>
    public IReadOnlyList<string> CandidatePaths()
    {
        var candidates = new List<string>();

        var env = getEnv("CCUSAGE_ENGINE");
        if (!string.IsNullOrEmpty(env))
        {
            candidates.Add(env);
        }

        candidates.Add(Path.Combine(serverDirectory, FileName));

        var archFolder = osArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };
        if (archFolder is not null)
        {
            candidates.Add(Path.Combine(serverDirectory, archFolder, FileName));
        }

        foreach (var entry in (getEnv("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (entry.Length > 0)
            {
                candidates.Add(Path.Combine(entry, FileName));
            }
        }

        return candidates;
    }

    /// <summary>The first existing candidate.</summary>
    /// <exception cref="EngineNotFoundException">No candidate exists.</exception>
    public string Resolve()
    {
        var candidates = CandidatePaths();
        return candidates.FirstOrDefault(File.Exists) ?? throw new EngineNotFoundException(candidates);
    }
}
