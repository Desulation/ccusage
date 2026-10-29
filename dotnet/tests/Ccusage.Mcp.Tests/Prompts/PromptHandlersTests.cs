using System.Text.Json;
using Ccusage.Mcp.Commands;
using Ccusage.Mcp.Prompts;
using Ccusage.Mcp.Tests.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Ccusage.Mcp.Tests.Prompts;

[TestClass]
public sealed class PromptHandlersTests
{
    private static PromptHandlers Prompts() =>
        new(CommandCatalog.Instance, HandlerFixture.Locator(TestPaths.FakeEnginePath, ""), HandlerFixture.Runner(10));

    private static Dictionary<string, JsonElement> Args(string json) => HandlerFixture.Args(json);

    private static string Text(GetPromptResult result) => ((TextContentBlock)result.Messages.Single().Content).Text;

    [TestMethod]
    public async Task ListAsync_ReturnsCcusagePrompt() => Assert.AreEqual("ccusage", (await Prompts().ListAsync(default)).Prompts.Single().Name);

    [TestMethod]
    public async Task GetAsync_Success_FormatsMessage()
    {
        var r = await Prompts().GetAsync("ccusage", Args("""{"args":"codex daily"}"""), default);
        Assert.AreEqual(Role.User, r.Messages.Single().Role);
        Assert.StartsWith("ccusage codex daily\n\n```json\n{\"argv\":", Text(r));
        Assert.EndsWith(
            "\n```\n\nPresent this report as a readable table with totals, and call out the main cost drivers (the models, days, or sessions with the highest cost).",
            Text(r));
    }

    [TestMethod]
    public async Task GetAsync_EmptyArgs_RendersDaily() =>
        Assert.StartsWith("ccusage daily\n\n", Text(await Prompts().GetAsync("ccusage", null, default)));

    [TestMethod]
    public async Task GetAsync_Error_FormatsFailure() =>
        Assert.StartsWith(
            "ccusage codex weekly failed:\n\nReport 'weekly' is not available for source 'codex'.",
            Text(await Prompts().GetAsync("ccusage", Args("""{"args":"codex weekly"}"""), default)));

    // §5.6: a validation error is the prompt's message, not a protocol error.
    [TestMethod]
    [DataRow("""{"args":5}""")]
    [DataRow("""{"args":["daily"]}""")]
    public async Task GetAsync_NonStringArgs_FormatsFailure(string json) =>
        Assert.AreEqual(
            "ccusage failed:\n\nParameter 'args' must be a string.",
            Text(await Prompts().GetAsync("ccusage", Args(json), default)));

    [TestMethod]
    public async Task GetAsync_UnknownPrompt_Throws() =>
        Assert.AreEqual(
            "Unknown prompt 'x'.",
            (await Assert.ThrowsExactlyAsync<McpException>(() => Prompts().GetAsync("x", null, default).AsTask())).Message);
}
