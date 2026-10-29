using System.Diagnostics;
using Ccusage.Mcp.Engine;

namespace Ccusage.Mcp;

/// <summary><c>ccusage-mcp --version</c>: the server version line, then the engine's version line.</summary>
public static class VersionCommand
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Writes <c>ccusage-mcp {version}</c>, then the engine's first non-empty <c>--version</c> stdout line, or
    /// <c>ccusage engine: unavailable ({message})</c> for any engine failure. Always returns 0.
    /// </summary>
    public static int Run(TextWriter output, EngineLocator locator)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(locator);

        output.WriteLine($"ccusage-mcp {ServerVersion.Value}");
        try
        {
            output.WriteLine(ReadEngineVersion(locator.Resolve()));
        }
        catch (Exception e) // any engine failure (not found, cannot start, hang, no output) is reported, never thrown
        {
            output.WriteLine($"ccusage engine: unavailable ({e.Message})");
        }

        return 0;
    }

    private static string ReadEngineVersion(string enginePath)
    {
        var psi = new ProcessStartInfo(enginePath, ["--version"])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {enginePath}.");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync(); // drained so a chatty stderr cannot block the engine

        var elapsed = Stopwatch.StartNew();
        // One 10 s budget covers exit and stdout EOF (a grandchild can hold the pipe open after the engine exits).
        if (!process.WaitForExit(Timeout) || !stdout.Wait(Remaining(elapsed)))
        {
            KillTree(process);
            throw new TimeoutException("ccusage --version did not exit within 10 s");
        }

        return stdout.Result.ReplaceLineEndings("\n").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0)
            ?? throw new InvalidOperationException("ccusage --version printed no version");
    }

    private static TimeSpan Remaining(Stopwatch elapsed)
    {
        var remaining = Timeout - elapsed.Elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}
