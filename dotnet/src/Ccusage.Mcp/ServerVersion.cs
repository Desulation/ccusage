using System.Reflection;

namespace Ccusage.Mcp;

/// <summary>The server version: this assembly's informational version without build metadata.</summary>
/// <remarks>Reads the server assembly itself, not the entry assembly, which under a test host is the test project.</remarks>
public static class ServerVersion
{
    public static string Value { get; } = Read();

    private static string Read()
    {
        var informational = typeof(ServerVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? throw new InvalidOperationException("Server assembly has no AssemblyInformationalVersionAttribute.");
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
