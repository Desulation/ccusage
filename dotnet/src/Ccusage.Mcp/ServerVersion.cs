using System.Reflection;

namespace Ccusage.Mcp;

/// <summary>The server version: the entry assembly's informational version without build metadata.</summary>
public static class ServerVersion
{
    public static string Value { get; } = Read();

    private static string Read()
    {
        var informational = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? throw new InvalidOperationException("Entry assembly has no AssemblyInformationalVersionAttribute.");
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}
