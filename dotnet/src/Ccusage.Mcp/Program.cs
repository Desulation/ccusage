namespace Ccusage.Mcp;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Contains("--version"))
        {
            Console.Out.WriteLine($"ccusage-mcp {ServerVersion.Value}");
            return 0;
        }

        // Replaced by the MCP stdio server in Task 7b.
        Console.Error.WriteLine("ccusage-mcp: MCP server not wired yet");
        return 2;
    }
}
