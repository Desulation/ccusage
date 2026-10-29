using Ccusage.Mcp.Tools;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Prompts;

/// <summary>The MCP prompt definitions (spec §5.6).</summary>
public static class PromptDefinitions
{
    /// <summary>The <c>ccusage</c> prompt: one optional <c>args</c> argument with the <c>report</c> tool's grammar.</summary>
    public static Prompt Ccusage { get; } = new()
    {
        Name = "ccusage",
        Description = "Run a ccusage report and present it as a readable table.",
        Arguments =
        [
            new PromptArgument
            {
                Name = "args",
                Description = ToolDefinitions.ReportArgsDescription,
                Required = false,
            },
        ],
    };
}
