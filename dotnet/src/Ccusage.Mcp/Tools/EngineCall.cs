using System.ComponentModel;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Engine;

namespace Ccusage.Mcp.Tools;

/// <summary>The outcome of one engine call: <see cref="Error"/> non-null means failure, its text returned to the client verbatim.</summary>
internal readonly record struct EngineCallResult(string? Stdout, string? Stderr, string? Error)
{
    public static EngineCallResult Failure(string error) => new(null, null, error);
}

/// <summary>
/// The one validate → locate → run → map-errors path, shared by <see cref="ToolHandlers"/> and
/// <see cref="Prompts.PromptHandlers"/>. Every report, typed or CLI-style, reaches the engine only through here,
/// so <see cref="CommandValidator"/> always runs first.
/// </summary>
internal static class EngineCall
{
    /// <summary>
    /// Builds the request, validates it into argv, locates the engine and runs it. Validation and location failures
    /// return before the engine starts.
    /// </summary>
    /// <param name="request">Builds the request; may throw <see cref="ValidationException"/> (typed arguments, CLI text).</param>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public static async Task<EngineCallResult> ExecuteAsync(CommandCatalog catalog, EngineLocator locator, EngineRunner runner,
        Func<CommandRequest> request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(request);

        EngineInvocation invocation;
        try
        {
            var built = request();
            var argv = CommandValidator.ValidateAndBuildArgv(catalog, built);
            invocation = new EngineInvocation(locator.Resolve(), argv, built.Stdin);
        }
        catch (ValidationException e)
        {
            return EngineCallResult.Failure(e.Message);
        }
        catch (EngineNotFoundException e)
        {
            return EngineCallResult.Failure(e.Message);
        }

        return await RunAsync(runner, invocation, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs an already-built invocation and maps the outcome: a start failure, a timeout (with the configured
    /// <see cref="EngineRunner.Timeout"/>) and a non-zero exit become <see cref="EngineCallResult.Error"/>, each with stderr.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public static async Task<EngineCallResult> RunAsync(EngineRunner runner, EngineInvocation invocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(invocation);

        EngineResult result;
        try
        {
            result = await runner.RunAsync(invocation, ct).ConfigureAwait(false);
        }
        catch (Win32Exception e) // the located file could not be started (not executable, wrong architecture, ...)
        {
            return EngineCallResult.Failure($"ccusage could not be started ({invocation.EnginePath}): {e.Message}");
        }

        if (result.TimedOut)
        {
            return EngineCallResult.Failure($"ccusage timed out after {(int)runner.Timeout.TotalSeconds} s.\n{result.Stderr}");
        }

        if (result.ExitCode != 0)
        {
            return EngineCallResult.Failure($"ccusage exited with code {result.ExitCode}.\n{result.Stderr}");
        }

        return new EngineCallResult(result.Stdout, result.Stderr, null);
    }
}
