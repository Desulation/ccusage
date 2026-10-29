using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FakeEngine;

// Test double for the ccusage engine. Echoes {"argv":[...],"stdin":"...","noColor":...} and exits 0.
// Control args (each is also echoed in argv):
//   --fake-exit=<n>          exit code
//   --fake-stderr=<text>     written to stderr
//   --fake-stdout=<text>     replaces the echo
//   --fake-stdout-bytes=<n>  n bytes of 'x' replace the echo
//   --fake-hang              sleep forever after writing stdout
//   --version                prints "ccusage 0.0.0-fake"
// Stdin is read to end only when it is redirected and an arg equals "statusline".
static class Program
{
    static int Main(string[] args)
    {
        if (args.Contains("--version"))
        {
            Console.Out.WriteLine("ccusage 0.0.0-fake");
            return 0;
        }

        var exitCode = 0;
        string? stderr = null;
        string? stdoutOverride = null;
        var hang = false;

        foreach (var arg in args)
        {
            if (TryValue(arg, "--fake-exit=", out var exit))
            {
                exitCode = int.Parse(exit, CultureInfo.InvariantCulture);
            }
            else if (TryValue(arg, "--fake-stderr=", out var err))
            {
                stderr = err;
            }
            else if (TryValue(arg, "--fake-stdout=", out var output))
            {
                stdoutOverride = output;
            }
            else if (TryValue(arg, "--fake-stdout-bytes=", out var bytes))
            {
                stdoutOverride = new string('x', int.Parse(bytes, CultureInfo.InvariantCulture));
            }
            else if (arg == "--fake-hang")
            {
                hang = true;
            }
        }

        var stdin = Console.IsInputRedirected && args.Contains("statusline")
            ? Console.In.ReadToEnd()
            : "";

        if (stderr is not null)
        {
            Console.Error.Write(stderr);
            Console.Error.Flush();
        }

        var stdout = stdoutOverride
            ?? JsonSerializer.Serialize(
                new FakeEcho(args, stdin, Environment.GetEnvironmentVariable("NO_COLOR")),
                FakeEngineJsonContext.Default.FakeEcho);
        Console.Out.Write(stdout);
        Console.Out.Flush();

        if (hang)
        {
            Thread.Sleep(Timeout.Infinite);
        }

        return exitCode;
    }

    private static bool TryValue(string arg, string prefix, out string value)
    {
        if (arg.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = arg[prefix.Length..];
            return true;
        }

        value = "";
        return false;
    }
}

internal sealed record FakeEcho(
    [property: JsonPropertyName("argv")] string[] Argv,
    [property: JsonPropertyName("stdin")] string Stdin,
    [property: JsonPropertyName("noColor")] string? NoColor);

[JsonSerializable(typeof(FakeEcho))]
internal sealed partial class FakeEngineJsonContext : JsonSerializerContext;
