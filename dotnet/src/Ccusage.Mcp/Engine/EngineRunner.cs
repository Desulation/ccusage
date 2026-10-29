using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Ccusage.Mcp.Engine;

/// <summary>
/// Runs the ccusage engine without a shell (spec §6.2): argv verbatim, the server's environment plus <c>NO_COLOR=1</c>,
/// stdout and stderr captured concurrently, stdin written only when the invocation carries it (<c>statusline</c>).
/// On timeout the process tree is killed and the result reports <see cref="EngineResult.TimedOut"/>; on caller
/// cancellation the tree is killed and <see cref="OperationCanceledException"/> is thrown.
/// </summary>
/// <remarks>
/// Never writes to the server's stdout (the MCP channel) or stderr (Claude Code records every stderr line as
/// <c>[ERROR]</c>), and never logs on the success path: <c>logWarning</c> is only for genuine problems.
/// </remarks>
public sealed class EngineRunner(TimeSpan timeout, Action<string> logWarning)
{
    private const string TimeoutVariable = "CCUSAGE_MCP_TIMEOUT_SECONDS";
    private const int DefaultTimeoutSeconds = 120;

    // CancelAfter rejects delays beyond the timer's range; a longer timeout is effectively "never" anyway.
    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(int.MaxValue);

    // After a tree kill the pipes close almost at once; this bounds the wait for a process that escaped the kill.
    private static readonly TimeSpan DrainGrace = TimeSpan.FromSeconds(5);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Action<string> logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));

    /// <summary>The configured timeout for one engine run.</summary>
    public TimeSpan Timeout { get; } = timeout > TimeSpan.Zero
        ? timeout
        : throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The engine timeout must be positive.");

    /// <summary>
    /// <c>CCUSAGE_MCP_TIMEOUT_SECONDS</c> as a positive whole number of seconds; unset or empty means 120 s.
    /// Anything else warns once and uses 120 s.
    /// </summary>
    public static TimeSpan TimeoutFromEnvironment(Func<string, string?> getEnv, Action<string> logWarning)
    {
        ArgumentNullException.ThrowIfNull(getEnv);
        ArgumentNullException.ThrowIfNull(logWarning);

        var value = getEnv(TimeoutVariable);
        if (string.IsNullOrEmpty(value))
        {
            return TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        logWarning($"{TimeoutVariable}='{value}' is not a positive number; using {DefaultTimeoutSeconds}.");
        return TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    }

    /// <summary>Runs the engine once and returns its exit code and complete output.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public async Task<EngineResult> RunAsync(EngineInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = CreateStartInfo(invocation) };
        process.Start();

        // Both readers start before stdin is written, so an engine filling either pipe can never block the run.
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        // Stdin is always redirected so the engine never inherits the MCP channel; without input it sees EOF at once.
        // The write runs off this thread and is awaited with the deadline: a write (or the flush in Close) to an
        // engine that does not read its stdin blocks until the pipe breaks, which only the kill guarantees.
        var stdin = Task.Run(() => WriteStdinAsync(process.StandardInput, invocation.Stdin), CancellationToken.None);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout < MaxTimerDelay ? Timeout : MaxTimerDelay);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            // A grandchild can hold a pipe open after the engine exits, so EOF is also bounded by the deadline.
            await Task.WhenAll(stdout, stderr, stdin).WaitAsync(deadline.Token).ConfigureAwait(false);
            return new EngineResult(process.ExitCode, stdout.Result, stderr.Result, TimedOut: false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            KillTree(process);
            await DrainAsync(process, stdout, stderr, stdin).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new EngineResult(process.HasExited ? process.ExitCode : -1, Completed(stdout), Completed(stderr), TimedOut: true);
        }
    }

    private static ProcessStartInfo CreateStartInfo(EngineInvocation invocation)
    {
        var psi = new ProcessStartInfo
        {
            FileName = invocation.EnginePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };

        foreach (var arg in invocation.Argv)
        {
            psi.ArgumentList.Add(arg);
        }

        // psi.Environment starts as a copy of the server's environment.
        psi.Environment["NO_COLOR"] = "1";
        if (invocation.Environment is { } overrides)
        {
            foreach (var (name, value) in overrides)
            {
                if (value is null)
                {
                    psi.Environment.Remove(name);
                }
                else
                {
                    psi.Environment[name] = value;
                }
            }
        }

        return psi;
    }

    private static async Task WriteStdinAsync(StreamWriter input, string? text)
    {
        try
        {
            if (text is not null)
            {
                await input.WriteAsync(text.AsMemory()).ConfigureAwait(false);
                await input.FlushAsync().ConfigureAwait(false);
            }

            input.Close();
        }
        catch (IOException)
        {
            // The engine exited (or was killed) without reading all of stdin: its exit code and output report it.
        }
    }

    private async Task DrainAsync(Process process, params Task[] pending)
    {
        var drained = Task.WhenAll([process.WaitForExitAsync(CancellationToken.None), .. pending]);
        await drained.WaitAsync(DrainGrace).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!drained.IsCompleted)
        {
            logWarning($"ccusage engine pipes did not close within {DrainGrace.TotalSeconds:0} s of killing its process tree; output may be incomplete.");
        }
    }

    private void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            logWarning($"Could not kill the ccusage engine process tree: {e.Message}");
        }
    }

    private static string Completed(Task<string> output) => output.IsCompletedSuccessfully ? output.Result : "";
}
