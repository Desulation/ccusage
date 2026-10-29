# Installation

ccusage is distributed as an MCP server, `ccusage-mcp`, that runs the ccusage engine (the `ccusage` CLI). Releases are published on [GitHub Releases](https://github.com/Desulation/ccusage/releases): a single `ccusage.mcpb` bundle for every platform, per-platform archives, and `SHA256SUMS`.

## Install Options

### 1. Claude Code Plugin (Recommended)

```text
/plugin marketplace add Desulation/ccusage
/plugin install ccusage@ccusage
```

The plugin downloads `ccusage.mcpb` from the matching release. The bundle holds the server and the engine for Windows, Linux, and macOS on x64 and arm64. Try it with `/ccusage claude daily`, or ask Claude a question such as "How much did I spend on Codex this month?". See [MCP Server](/guide/mcp) for the tools, prompt, and skill.

### 2. Claude Desktop

Download `ccusage.mcpb` from [Releases](https://github.com/Desulation/ccusage/releases) and open it with Claude Desktop to install it as a desktop extension.

### 3. Other MCP Clients

Download the archive for your platform from [Releases](https://github.com/Desulation/ccusage/releases):

| Platform            | Archive                          |
| ------------------- | -------------------------------- |
| Linux x64           | `ccusage-mcp-linux-x64.tar.gz`   |
| Linux arm64         | `ccusage-mcp-linux-arm64.tar.gz` |
| macOS Apple silicon | `ccusage-mcp-osx-arm64.tar.gz`   |
| macOS Intel         | `ccusage-mcp-osx-x64.tar.gz`     |
| Windows x64         | `ccusage-mcp-win-x64.zip`        |
| Windows arm64       | `ccusage-mcp-win-arm64.zip`      |

Each archive contains the server (`ccusage-mcp`) and the engine (`ccusage`) side by side; on Windows they are `ccusage-mcp.exe` and `ccusage.exe`. Keep them in the same directory and register the server with your client, for example in `.mcp.json`:

```json
{
	"mcpServers": {
		"ccusage": {
			"command": "/path/to/ccusage-mcp"
		}
	}
}
```

### 4. Standalone CLI

The same archive contains the standalone `ccusage` CLI. Put `ccusage` (`ccusage.exe` on Windows) in a directory on your `PATH`:

```bash
ccusage daily
ccusage monthly --breakdown
ccusage blocks --active
```

::: tip macOS downloads
Files downloaded with a browser are quarantined on macOS. Clear the flag in the extracted directory before the first run:

```bash
xattr -d com.apple.quarantine ccusage ccusage-mcp
```

:::

## Development

For development or contributing:

```bash
# Clone the repository
git clone https://github.com/Desulation/ccusage.git
cd ccusage

# Allow direnv to load the Nix dev shell
direnv allow
```

The Nix dev shell provides the pinned Rust toolchain, `pnpm` (used only to build this docs site), GitHub CLI, git hooks, package tooling, and project utilities. Run project tasks with `just`:

```bash
# Format the tree
just fmt

# Run tests
just test

# Run static checks
just check

# Build the docs site
just build
```

The MCP server lives in `dotnet/` and needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), which the Nix dev shell does not provide:

```bash
dotnet build dotnet/Ccusage.Mcp.slnx
(cd dotnet && dotnet test)    # run inside dotnet/, where global.json selects Microsoft.Testing.Platform
dotnet publish dotnet/src/Ccusage.Mcp -c Release -r <rid>    # Native AOT, e.g. -r linux-x64
```

The end-to-end tests run only when `CCUSAGE_MCP_E2E_SERVER` (a published `ccusage-mcp`) and `CCUSAGE_ENGINE` (a `ccusage` engine build) are both set; otherwise they report inconclusive. Fork maintenance (enabling Actions, the weekly upstream sync) is described in the [README](https://github.com/Desulation/ccusage#development).

## Verification

Check the CLI and the server:

```bash
ccusage --version
ccusage-mcp --version
```

`ccusage --version` prints `ccusage <version>`. `ccusage-mcp --version` prints `ccusage-mcp <version>` and then the version line of the engine it found, or `ccusage engine: unavailable (…)` with the reason. In an MCP client, the `list-sources` tool reports the same versions and the engine path.

## Updating

- **Claude Code plugin:** `/plugin update ccusage@ccusage`
- **Claude Desktop:** download the new `ccusage.mcpb` from [Releases](https://github.com/Desulation/ccusage/releases) and open it again.
- **Archives and standalone CLI:** download the new archive and replace both `ccusage-mcp` and `ccusage`, so the server and the engine stay on the same release.

## Troubleshooting

### macOS: Quarantined Binaries

If macOS refuses to open `ccusage` or `ccusage-mcp`, the files were quarantined by a browser download:

```bash
xattr -d com.apple.quarantine ccusage ccusage-mcp
```

### Engine Not Found

`ccusage-mcp` looks for the engine through `CCUSAGE_ENGINE`, next to itself, in an `x64`/`arm64` subdirectory next to itself, and on `PATH`. If none of these exists, every tool returns an error that starts with `ccusage engine not found` and lists each path it checked. Keep `ccusage` next to `ccusage-mcp`, or set `CCUSAGE_ENGINE` to the engine's absolute path. See [Engine location](/guide/mcp#engine-location).

For timeouts, large outputs, and a tool that is not available right after installation, see [MCP troubleshooting](/guide/mcp#troubleshooting).

## Next Steps

After installation, check out:

- [Getting Started Guide](/guide/getting-started) - Your first usage report
- [MCP Server](/guide/mcp) - Tools, prompt, and skill reference
- [Configuration](/guide/configuration) - Customize ccusage behavior
- [Daily Usage](/guide/daily-reports) - Understand daily usage patterns
