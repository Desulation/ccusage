using System.Runtime.InteropServices;
using Ccusage.Mcp.Engine;

namespace Ccusage.Mcp.Tests;

[TestClass]
public sealed class VersionCommandTests
{
    // Only CCUSAGE_ENGINE is answered (no PATH), and the server directory is an empty temp dir,
    // so the env path is the only candidate that can exist.
    private static EngineLocator Locator(string enginePath) =>
        new(k => k == "CCUSAGE_ENGINE" ? enginePath : null, Directory.CreateTempSubdirectory().FullName, Architecture.X64, OperatingSystem.IsWindows());

    private static (int Exit, string[] Lines) Run(EngineLocator locator)
    {
        using var w = new StringWriter();
        var exit = VersionCommand.Run(w, locator);
        return (exit, w.ToString().ReplaceLineEndings("\n").Split('\n'));
    }

    [TestMethod]
    public void Version_PrintsServerAndEngineLines()
    {
        var (exit, lines) = Run(Locator(TestPaths.FakeEnginePath));
        Assert.AreEqual(0, exit);
        Assert.AreEqual($"ccusage-mcp {ServerVersion.Value}", lines[0]);
        Assert.AreEqual("ccusage 0.0.0-fake", lines[1]);
    }

    [TestMethod]
    public void Version_EngineMissing_ReportsUnavailable_ExitZero()
    {
        var (exit, lines) = Run(Locator(Path.Combine(Path.GetTempPath(), "missing-engine")));
        Assert.AreEqual(0, exit);
        Assert.AreEqual($"ccusage-mcp {ServerVersion.Value}", lines[0]);
        StringAssert.StartsWith(lines[1], "ccusage engine: unavailable (ccusage engine not found.");
    }

    [TestMethod]
    public void Version_EngineCannotStart_ReportsUnavailable_ExitZero()
    {
        // Exists, but is an empty file: not a runnable executable on any OS.
        var bogus = Path.Combine(Directory.CreateTempSubdirectory().FullName, OperatingSystem.IsWindows() ? "ccusage.exe" : "ccusage");
        File.WriteAllText(bogus, "");
        var (exit, lines) = Run(Locator(bogus));
        Assert.AreEqual(0, exit);
        Assert.AreEqual($"ccusage-mcp {ServerVersion.Value}", lines[0]);
        StringAssert.StartsWith(lines[1], "ccusage engine: unavailable (");
    }
}
