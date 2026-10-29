using System.Reflection;

namespace Ccusage.Mcp.Tests.E2E;

/// <summary>What an end-to-end test runs against.</summary>
/// <param name="Env">
/// Applied to both the server process and direct engine runs, on top of the inherited environment; the test process
/// environment is never changed.
/// </param>
internal sealed record E2eContext(string ServerPath, string EnginePath, string FixturesDir, Dictionary<string, string?> Env);

/// <summary>Reads the e2e configuration (<c>CCUSAGE_MCP_E2E_SERVER</c>, <c>CCUSAGE_ENGINE</c>) and the fixture paths.</summary>
internal static class E2eEnvironment
{
    private const string ServerVariable = "CCUSAGE_MCP_E2E_SERVER";
    private const string EngineVariable = "CCUSAGE_ENGINE";

    private static readonly string FixturesDir = Path.GetFullPath(
        typeof(E2eEnvironment).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "FixturesDir")
            .Value ?? throw new InvalidOperationException("FixturesDir assembly metadata has no value."));

    /// <summary>
    /// The e2e context with a fresh, empty HOME/USERPROFILE directory (the caller deletes it), or
    /// <see cref="Assert.Inconclusive(string)"/> when either variable is unset.
    /// </summary>
    public static E2eContext RequireOrInconclusive()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        var engine = Environment.GetEnvironmentVariable(EngineVariable);
        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(engine))
        {
            Assert.Inconclusive($"Set {ServerVariable} and {EngineVariable} to run e2e tests.");
        }

        var serverPath = Path.GetFullPath(server);
        var enginePath = Path.GetFullPath(engine);
        // Fail rather than skip: the variables are set, so a wrong path is a broken setup, not an opt-out.
        Assert.IsTrue(File.Exists(serverPath), $"{ServerVariable} does not exist: {serverPath}");
        Assert.IsTrue(File.Exists(enginePath), $"{EngineVariable} does not exist: {enginePath}");

        var home = Directory.CreateTempSubdirectory("ccusage-mcp-e2e-").FullName;
        var env = new Dictionary<string, string?>
        {
            ["CLAUDE_CONFIG_DIR"] = Path.Combine(FixturesDir, "claude"),
            ["CODEX_HOME"] = Path.Combine(FixturesDir, "codex"),
            ["HOME"] = home,
            ["USERPROFILE"] = home,
            ["NO_COLOR"] = "1",
            ["LOG_LEVEL"] = "0",
            [EngineVariable] = enginePath,
        };
        return new E2eContext(serverPath, enginePath, FixturesDir, env);
    }
}
