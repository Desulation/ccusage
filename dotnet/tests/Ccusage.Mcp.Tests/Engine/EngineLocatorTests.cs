using System.Runtime.InteropServices;
using Ccusage.Mcp.Engine;

namespace Ccusage.Mcp.Tests.Engine;

[TestClass]
public sealed class EngineLocatorTests
{
    private static string Tmp() => Directory.CreateTempSubdirectory().FullName;

    private static string Touch(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, []);
        return path;
    }

    // Environment is injected (never process-wide): only CCUSAGE_ENGINE and PATH are answered.
    private static EngineLocator Loc(string? env, string dir, Architecture arch = Architecture.X64, bool win = false, string path = "") =>
        new(k => k == "CCUSAGE_ENGINE" ? env : k == "PATH" ? path : null, dir, arch, win);

    [TestMethod]
    public void Resolve_PrefersEnvVar()
    {
        var d = Tmp();
        var env = Touch(Tmp(), "ccusage");
        Touch(d, "ccusage");
        Assert.AreEqual(env, Loc(env, d).Resolve());
    }

    [TestMethod]
    public void Resolve_EnvVarMissingFile_IsSkipped_AndListed()
    {
        var d = Tmp();
        var missing = Path.Combine(Tmp(), "nope");
        var sib = Touch(d, "ccusage");
        var l = Loc(missing, d);
        Assert.AreEqual(sib, l.Resolve());
        Assert.AreEqual(missing, l.CandidatePaths()[0]);
    }

    [TestMethod]
    public void Resolve_SiblingBeforeArchFolder()
    {
        var d = Tmp();
        var sib = Touch(d, "ccusage");
        Touch(Directory.CreateDirectory(Path.Combine(d, "x64")).FullName, "ccusage");
        Assert.AreEqual(sib, Loc(null, d).Resolve());
    }

    [TestMethod]
    public void Resolve_ArchFolder_UsesOsArchitecture()
    {
        var d = Tmp();
        var arm = Touch(Directory.CreateDirectory(Path.Combine(d, "arm64")).FullName, "ccusage");
        Assert.AreEqual(arm, Loc(null, d, Architecture.Arm64).Resolve());
        Assert.ThrowsExactly<EngineNotFoundException>(() => Loc(null, d, Architecture.X64).Resolve());
    }

    [TestMethod]
    public void Resolve_Path_LastResort()
    {
        var d = Tmp();
        var p1 = Tmp();
        var p2 = Tmp();
        var hit = Touch(p2, "ccusage");
        Assert.AreEqual(hit, Loc(null, d, path: string.Join(Path.PathSeparator, p1, p2)).Resolve());
    }

    [TestMethod]
    public void Resolve_Windows_UsesExe()
    {
        var d = Tmp();
        var exe = Touch(d, "ccusage.exe");
        Touch(d, "ccusage");
        Assert.AreEqual(exe, Loc(null, d, win: true).Resolve());
    }

    [TestMethod]
    public void Resolve_NonWindows_NoExe()
    {
        var d = Tmp();
        Touch(d, "ccusage.exe");
        Assert.ThrowsExactly<EngineNotFoundException>(() => Loc(null, d, win: false).Resolve());
    }

    [TestMethod]
    public void Resolve_NotFound_ListsEveryCandidateInOrder()
    {
        var d = Tmp();
        var p1 = Tmp();
        var p2 = Tmp();
        var env = Path.Combine(Tmp(), "e");
        var ex = Assert.ThrowsExactly<EngineNotFoundException>(
            () => Loc(env, d, Architecture.Arm64, path: string.Join(Path.PathSeparator, p1, p2)).Resolve());
        CollectionAssert.AreEqual(
            new[] { env, Path.Combine(d, "ccusage"), Path.Combine(d, "arm64", "ccusage"), Path.Combine(p1, "ccusage"), Path.Combine(p2, "ccusage") },
            ex.CheckedPaths.ToArray());
        StringAssert.StartsWith(ex.Message, "ccusage engine not found. Set CCUSAGE_ENGINE or place ccusage next to ccusage-mcp. Checked:\n  " + env);
    }
}
