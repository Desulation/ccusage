namespace Ccusage.Mcp.Commands;

/// <summary>A request the catalog rejects; the message is returned to MCP clients verbatim.</summary>
public sealed class ValidationException(string message) : Exception(message);
