using System.Reflection;
using System.Text.Json;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Prompts;
using Ccusage.Mcp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Tests.Tools;

[TestClass]
public sealed class SnapshotTests
{
    private static readonly JsonSerializerOptions Pretty = new(McpJsonUtilities.DefaultOptions) { WriteIndented = true };

    [TestMethod]
    public void ToolsList_MatchesSnapshot() =>
        SnapshotAssert.Default.Matches("tools-list.json",
            JsonSerializer.Serialize(new ListToolsResult { Tools = ToolDefinitions.Build(CommandCatalog.Instance).ToList() }, Pretty));

    [TestMethod]
    public void PromptsList_MatchesSnapshot() =>
        SnapshotAssert.Default.Matches("prompts-list.json",
            JsonSerializer.Serialize(new ListPromptsResult { Prompts = [PromptDefinitions.Ccusage] }, Pretty));

    // The helper's own behavior, with the update switch injected (never a process-wide env var: tests run in parallel).
    [TestMethod]
    public void SnapshotAssert_LineEndingsAreNormalized()
    {
        var (expectedDir, sourceDir) = TempDirs();
        File.WriteAllText(Path.Combine(expectedDir, "s.json"), "{\r\n  \"a\": 1\r\n}\r\n");
        new SnapshotAssert(_ => null, expectedDir, sourceDir).Matches("s.json", "{\r\n  \"a\": 1\r\n}");
    }

    [TestMethod]
    public void SnapshotAssert_Mismatch_Fails()
    {
        var (expectedDir, sourceDir) = TempDirs();
        File.WriteAllText(Path.Combine(expectedDir, "s.json"), "{}\n");
        Assert.ThrowsExactly<AssertFailedException>(() => new SnapshotAssert(_ => null, expectedDir, sourceDir).Matches("s.json", "[]"));
        Assert.IsFalse(File.Exists(Path.Combine(sourceDir, "s.json")));
    }

    [TestMethod]
    public void SnapshotAssert_UpdateSwitch_WritesSourceFileAndFails()
    {
        var (expectedDir, sourceDir) = TempDirs();
        var env = new Dictionary<string, string?> { ["UPDATE_SNAPSHOTS"] = "1" };
        var ex = Assert.ThrowsExactly<AssertFailedException>(
            () => new SnapshotAssert(n => env.GetValueOrDefault(n), expectedDir, sourceDir).Matches("s.json", "{\r\n}"));
        Assert.Contains("Snapshot updated; re-run.", ex.Message);
        Assert.AreEqual("{\n}\n", File.ReadAllText(Path.Combine(sourceDir, "s.json")));
    }

    private static (string ExpectedDir, string SourceDir) TempDirs() =>
        (Directory.CreateTempSubdirectory().FullName, Directory.CreateTempSubdirectory().FullName);
}

/// <summary>
/// Compares text to a checked-in snapshot copied to the test output directory (line endings normalized to <c>\n</c>,
/// stored with one trailing newline). With <c>UPDATE_SNAPSHOTS=1</c> it overwrites the source snapshot instead and fails.
/// </summary>
internal sealed class SnapshotAssert(Func<string, string?> getEnv, string expectedDirectory, string sourceDirectory)
{
    public static SnapshotAssert Default { get; } = new(
        Environment.GetEnvironmentVariable,
        Path.Combine(AppContext.BaseDirectory, "Snapshots"),
        typeof(SnapshotAssert).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "SnapshotsSourceDir")
            .Value ?? throw new InvalidOperationException("SnapshotsSourceDir assembly metadata has no value."));

    public void Matches(string name, string actual)
    {
        var normalized = Normalize(actual) + "\n";
        if (getEnv("UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(Path.Combine(sourceDirectory, name), normalized);
            Assert.Fail("Snapshot updated; re-run.");
        }

        var expected = Normalize(File.ReadAllText(Path.Combine(expectedDirectory, name))) + "\n";
        Assert.AreEqual(expected, normalized, $"Snapshot {name} differs; run the tests with UPDATE_SNAPSHOTS=1 to update it.");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
}
