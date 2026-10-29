<div align="center">
    <img src="docs/public/logo.svg" alt="ccusage logo" width="256" height="256">
    <h1>ccusage</h1>
</div>

<div align="center">
    <img src="docs/public/screenshot.png" alt="ccusage terminal report screenshot">
</div>

> Analyze coding (agent) CLI token usage and costs from local data.

This fork brings ccusage reports to Claude Code and other MCP clients. A .NET 10 Native AOT MCP server, `ccusage-mcp`, runs the ccusage Rust engine (unchanged from upstream) and returns its reports as MCP tools, a prompt, and a `/ccusage` skill. It ships as a Claude Code plugin, a single `ccusage.mcpb` bundle, and per-platform archives on GitHub Releases.

Fork of [ccusage/ccusage](https://github.com/ccusage/ccusage) by @ryoppippi

## Quick Start

In Claude Code:

```text
/plugin marketplace add Desulation/ccusage
/plugin install ccusage@ccusage
/ccusage claude daily
```

## Supported Sources

ccusage reads local usage data from coding agent CLIs and turns it into daily, weekly, monthly, and session reports.

| Source             | Focused command example     |
| ------------------ | --------------------------- |
| Claude Code        | `ccusage claude daily`      |
| Codex              | `ccusage codex daily`       |
| OpenCode           | `ccusage opencode daily`    |
| Amp                | `ccusage amp daily`         |
| Droid              | `ccusage droid daily`       |
| Codebuff           | `ccusage codebuff daily`    |
| Hermes Agent       | `ccusage hermes daily`      |
| pi-agent           | `ccusage pi daily`          |
| Goose              | `ccusage goose daily`       |
| OpenClaw           | `ccusage openclaw daily`    |
| Kilo               | `ccusage kilo daily`        |
| Kimi               | `ccusage kimi daily`        |
| Qwen               | `ccusage qwen daily`        |
| GitHub Copilot CLI | `ccusage copilot daily`     |
| Gemini CLI         | `ccusage gemini daily`      |
| Antigravity        | `ccusage antigravity daily` |
| Grok Build CLI     | `ccusage grok daily`        |
| ZCode              | `ccusage zcode daily`       |

Use `ccusage daily`, `ccusage weekly`, `ccusage monthly`, or `ccusage session` to include every detected source in one report.

## Installation

### 1. Claude Code plugin (recommended)

Run `/plugin marketplace add Desulation/ccusage`, then `/plugin install ccusage@ccusage`. The plugin downloads `ccusage.mcpb` from the matching release; it holds the server and the engine for Windows, Linux, and macOS on x64 and arm64.

### 2. Claude Desktop

Download `ccusage.mcpb` from [Releases](https://github.com/Desulation/ccusage/releases) and open it with Claude Desktop to install it as a desktop extension.

### 3. Other MCP clients

Download the archive for your platform from [Releases](https://github.com/Desulation/ccusage/releases): `ccusage-mcp-<rid>.tar.gz` for `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, or `ccusage-mcp-<rid>.zip` for `win-x64`, `win-arm64`. It contains the server (`ccusage-mcp`) and the engine (`ccusage`) side by side; keep them in the same directory and register the server, for example in `.mcp.json`:

```json
{ "mcpServers": { "ccusage": { "command": "/path/to/ccusage-mcp" } } }
```

On Windows the executables are `ccusage-mcp.exe` and `ccusage.exe`.

### 4. Standalone `ccusage` CLI

The same archive contains the `ccusage` CLI. Put it on your `PATH` and run `ccusage daily`.

### macOS downloads

Files downloaded with a browser are quarantined. Clear the flag in the extracted directory before the first run:

```bash
xattr -d com.apple.quarantine ccusage ccusage-mcp
```

## Usage

### Slash commands

The `/ccusage` skill takes CLI-style arguments, `[source] [report] [--flags]` (no arguments means a daily report across all sources; Claude-only reports need the `claude` prefix, and output-format flags such as `--json` are not accepted):

```text
/ccusage
/ccusage claude daily
/ccusage codex monthly
/ccusage weekly --last 2
/ccusage claude blocks --active
```

### Ask Claude

With the plugin installed, ask in plain language and Claude picks the tool:

- "How much did I spend on Codex this month?"
- "Which Claude Code sessions cost the most this week?"
- "Show my daily token usage across all agents since Monday."

### CLI

```bash
# Basic usage
ccusage          # Show all detected sources by day (default)
ccusage daily    # All detected sources by day
ccusage weekly   # All detected sources by week
ccusage monthly  # All detected sources by month
ccusage session  # All detected sources by session
ccusage blocks   # Claude Code 5-hour billing windows
ccusage statusline  # Claude Code status line for hooks (Beta)

# Source-focused reports and options
ccusage claude daily --mode display
ccusage codex daily --speed fast
ccusage opencode weekly
ccusage amp session
ccusage droid daily
ccusage codebuff daily
ccusage hermes daily
ccusage goose daily
ccusage openclaw daily
ccusage kilo daily
ccusage kimi daily
ccusage qwen daily
ccusage copilot daily
ccusage gemini daily
ccusage antigravity daily
ccusage grok daily
ccusage zcode daily
ccusage pi daily --pi-path /path/to/sessions
ccusage pi daily --pi-path /path/to/sessions,/archive/pi/sessions

# Explicit unified report
ccusage daily --all
ccusage daily --sections daily,monthly,session --json
ccusage daily --by-agent --json

# Filters and options
ccusage daily --since 2026-04-25 --until 2026-05-16
ccusage daily --last 1  # Today
ccusage weekly --last 1  # This week
ccusage monthly --last 1  # This month
ccusage daily --json  # JSON output
ccusage daily --no-cost  # Hide cost columns and JSON cost fields
ccusage daily --timezone UTC  # Use UTC timezone

# Project analysis
ccusage claude daily --instances  # Group Claude Code by project/instance
ccusage claude daily --project myproject  # Filter to specific Claude project
ccusage claude daily --instances --project myproject --json  # Combined usage

# Compact mode for screenshots/sharing
ccusage --compact  # Force compact table mode
ccusage monthly --compact  # Compact monthly report
```

## MCP reference

| Name           | Kind   | What it does                                                                                                     |
| -------------- | ------ | ---------------------------------------------------------------------------------------------------------------- |
| `daily`        | tool   | Usage and cost by day; `source` defaults to `all`                                                                |
| `weekly`       | tool   | Usage and cost by week; `source` is `all`, `claude`, or `opencode`                                               |
| `monthly`      | tool   | Usage and cost by month; `source` defaults to `all`                                                              |
| `session`      | tool   | Usage and cost by session; `source` defaults to `all`                                                            |
| `blocks`       | tool   | Claude Code 5-hour billing blocks                                                                                |
| `statusline`   | tool   | Claude Code status line text for a `hook-input` payload (required)                                               |
| `list-sources` | tool   | Supported sources and reports, server and engine versions, engine path                                           |
| `report`       | tool   | Any report from CLI-style `args`, such as `"codex daily --since 2026-10-01"`                                     |
| `ccusage`      | prompt | Runs a report from `args` and asks for a readable table; `/mcp__plugin_ccusage_ccusage__ccusage` with the plugin |
| `/ccusage`     | skill  | Plugin slash command that calls `report` with your arguments; `/ccusage:ccusage` always works                    |

Parameter names are the CLI's long flags without `--` (`since`, `by-agent`, `token-limit`, ...). Results are the engine's own JSON output, unchanged. See [docs/guide/mcp.md](docs/guide/mcp.md) for every parameter, the result and error format, engine location, and troubleshooting.

## Features

- 📊 **Daily Report**: View token usage and costs aggregated by date
- 📅 **Monthly Report**: View token usage and costs aggregated by month
- 💬 **Session Report**: View usage grouped by conversation sessions
- 🤖 **Unified CLI Reports**: View Claude Code, Codex, OpenCode, Amp, Droid, Codebuff, Hermes Agent, pi-agent, Goose, OpenClaw, Kilo, Kimi, Qwen, GitHub Copilot CLI, Gemini CLI, Antigravity, Grok Build CLI, and ZCode usage from one CLI
- 🔌 **MCP Server**: MCP server for Claude Code and other MCP clients, with typed tools, a prompt, and a `/ccusage` skill
- ⏰ **5-Hour Blocks Report**: Track usage within Claude's billing windows with active block monitoring
- 🚀 **Statusline Integration**: Compact usage display for Claude Code status bar hooks (Beta)
- 🤖 **Model Tracking**: See which models are used across supported sources
- 📊 **Model Breakdown**: View per-model cost breakdown with `--breakdown` flag
- 📅 **Date Filtering**: Filter reports by date range using `--since` and `--until`
- ⏱️ **Recent Periods**: Jump to today, this week, or this month with `--last 1` on any daily, weekly, or monthly report
- 📁 **Custom Paths**: Support for custom local data directory locations
- 🎨 **Beautiful Output**: Colorful table-formatted display with automatic responsive layout
- 📱 **Smart Tables**: Automatic compact mode for narrow terminals (< 100 characters) with essential columns
- 📸 **Compact Mode**: Use `--compact` flag to force compact table layout, perfect for screenshots and sharing
- 📋 **Enhanced Model Display**: Model names shown as bulleted lists for better readability
- 📄 **JSON Output**: Export data in structured JSON format with `--json`
- 💰 **Cost Tracking**: Shows costs in USD for each day/month/session
- 🔒 **Cost Hiding**: Remove cost columns and JSON cost fields with `--no-cost`
- 🔄 **Cache Token Support**: Tracks and displays cache creation and cache read tokens separately
- 🌐 **Offline Mode**: Use pre-cached pricing data without network connectivity with `--offline`
- 🧩 **Custom Pricing Overrides**: Override token pricing per raw model name in `ccusage.json` without rebuilding
- 🏗️ **Claude Instance Support**: Group Claude Code usage by project with `--instances` and filter by specific projects
- 🌍 **Timezone Support**: Configure timezone for date grouping with `--timezone` option
- ⚙️ **Configuration Files**: Set defaults with JSON configuration files, complete with IDE autocomplete and validation

## Documentation

Full documentation lives in **[docs/guide/](docs/guide/)**.

Further reading (Japanese): [how ccusage began](https://ryoppippi.com/blog/2025-05-29-zenn-6c9a8fe6629cd6-ja/)

## Development

<details>
<summary>Contributor setup</summary>

Contributor setup uses the Nix flake development environment with [nix-direnv](https://github.com/nix-community/nix-direnv) for pinned tools, and `just` for everyday development tasks. Install [Nix](https://nixos.org/) with the `nix-command` and `flakes` experimental features enabled, then let nix-direnv load the dev shell automatically when you enter the directory:

```sh
# Clone the repository
git clone https://github.com/Desulation/ccusage.git
cd ccusage

# Allow direnv to load the Nix dev shell
direnv allow
```

The dev shell provides the pinned Rust toolchain, `pnpm` (used only to build the `docs/` site), GitHub CLI, git hooks, generated local agent skills, package tooling, and project utilities from `flake.nix`.

Run project tasks with `just` from inside the Nix environment (`just --list` shows every recipe):

```sh
just fmt
just test
just check
```

### Nix Package

The flake exposes the `ccusage` engine as the default package and app:

```sh
nix run . -- daily
nix run . -- codex daily --offline
nix build
```

Nix builds embed the LiteLLM pricing file from the locked `litellm` flake input (a single-file URL pinned to a LiteLLM commit), so sandboxed builds do not fetch pricing at build time. Non-Nix Cargo builds read the same locked LiteLLM revision URL from `flake.lock` and fetch the pricing file from that revision at build time. To update the locked pricing snapshot:

```bash
just update-litellm-pricing
```

### MCP server (`dotnet/`)

The MCP server lives in `dotnet/` and needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet/global.json` asks for SDK 10.0.402 or a later 10.0 SDK); the Nix dev shell does not provide it.

```sh
dotnet build dotnet/Ccusage.Mcp.slnx
(cd dotnet && dotnet test)    # run inside dotnet/, where global.json selects Microsoft.Testing.Platform
dotnet publish dotnet/src/Ccusage.Mcp -c Release -r <rid>    # Native AOT, e.g. -r linux-x64
```

The end-to-end tests run the published server against the real engine. They report inconclusive unless both `CCUSAGE_MCP_E2E_SERVER` (path to a published `ccusage-mcp`) and `CCUSAGE_ENGINE` (path to a `ccusage` engine build) are set. See [dotnet/AGENTS.md](dotnet/AGENTS.md) for the full setup.

### Fork maintenance

GitHub keeps workflows switched off on a fresh fork until the owner clicks "I understand my workflows, go ahead and enable them" on the repository's Actions tab.

`.github/workflows/sync-upstream.yaml` opens a weekly pull request from `sync/upstream` that brings in `ccusage/ccusage` `main`. It is opened with `GITHUB_TOKEN`, so CI does not start on its own; run it by hand with `gh workflow run ci.yaml --ref sync/upstream`. Merge sync pull requests with a merge commit, never squash or rebase, so the repository must allow merge commits (Settings → General → Pull Requests). `GITHUB_TOKEN` cannot push commits that change `.github/workflows/*`, so in weeks where upstream changes a workflow the sync run fails; then sync by hand from a clone with an `upstream` remote for `https://github.com/ccusage/ccusage.git`: `git fetch upstream`, `git merge upstream/main`, resolve conflicts, and push with your own credentials.

</details>

## License

[MIT](LICENSE) © [@ryoppippi](https://github.com/ryoppippi)
