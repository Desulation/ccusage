using System.Reflection;

namespace Ccusage.Mcp.Tests;

internal static class TestPaths
{
    public static string FakeEnginePath { get; } = Path.GetFullPath(
        typeof(TestPaths).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "FakeEnginePath")
            .Value ?? throw new InvalidOperationException("FakeEnginePath assembly metadata has no value."));
}
