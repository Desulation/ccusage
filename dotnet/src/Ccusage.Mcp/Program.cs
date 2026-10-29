using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Engine;
using Ccusage.Mcp.Prompts;
using Ccusage.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ccusage.Mcp;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Contains("--version"))
        {
            return VersionCommand.Run(Console.Out, EngineLocator.Default);
        }

        var builder = Host.CreateApplicationBuilder(args);

        // stdout is the JSON-RPC channel, so every log goes to stderr. Claude Code shows each stderr line as [ERROR],
        // so only warnings and above are written and the success path stays silent (spec §15).
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddSingleton(CommandCatalog.Instance);
        builder.Services.AddSingleton(EngineLocator.Default);
        builder.Services.AddSingleton(new EngineRunner(
            EngineRunner.TimeoutFromEnvironment(Environment.GetEnvironmentVariable, Console.Error.WriteLine),
            Console.Error.WriteLine));
        builder.Services.AddSingleton<ToolHandlers>();
        builder.Services.AddSingleton<PromptHandlers>();

        builder.Services
            .AddMcpServer(o => o.ServerInfo = new() { Name = "ccusage", Version = ServerVersion.Value })
            .WithStdioServerTransport()
            .WithListToolsHandler(static (request, ct) => Handlers<ToolHandlers>(request.Services).ListAsync(ct))
            .WithCallToolHandler(static (request, ct) =>
                Handlers<ToolHandlers>(request.Services).CallAsync(request.Params?.Name ?? "", request.Params?.Arguments, ct))
            .WithListPromptsHandler(static (request, ct) => Handlers<PromptHandlers>(request.Services).ListAsync(ct))
            .WithGetPromptHandler(static (request, ct) =>
                Handlers<PromptHandlers>(request.Services).GetAsync(request.Params?.Name ?? "", request.Params?.Arguments, ct));

        await builder.Build().RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static T Handlers<T>(IServiceProvider? services)
        where T : notnull =>
        (services ?? throw new InvalidOperationException("The MCP request carries no service provider.")).GetRequiredService<T>();
}
