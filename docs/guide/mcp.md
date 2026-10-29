# MCP Server

ccusage ships as an MCP server, `ccusage-mcp`, so Claude Code and other MCP clients can answer questions such as "How much did I spend on Codex this month?" from the same local data the CLI reads.

## Overview

`ccusage-mcp` is a .NET 10 Native AOT server that speaks MCP over stdio (there is no HTTP transport). It does not parse usage logs itself: every request is validated, turned into a `ccusage` command line and run by the ccusage engine, the Rust CLI documented in the rest of this guide. The engine's JSON output is returned unchanged.

The server offers:

- eight tools: [`daily`](#daily), [`weekly`](#weekly), [`monthly`](#monthly), [`session`](#session), [`blocks`](#blocks), [`statusline`](#statusline), [`list-sources`](#list-sources) and [`report`](#report);
- one prompt, [`ccusage`](#prompt-ccusage);
- with the Claude Code plugin, the [`/ccusage` skill](#skill-ccusage).

Every tool is marked read-only, non-destructive and idempotent. The server writes no files and opens no network port; the engine reads local usage files and, unless `offline` is set, fetches LiteLLM pricing data over the network.

## Install

### Claude Code plugin (recommended)

```text
/plugin marketplace add Desulation/ccusage
/plugin install ccusage@ccusage
```

The plugin downloads `ccusage.mcpb` from the matching [GitHub Release](https://github.com/Desulation/ccusage/releases). The bundle holds the server and the engine for Windows, Linux and macOS on x64 and arm64, and the right pair is started for your machine. Claude Code names the server `plugin:ccusage:ccusage`, so its tools appear as `mcp__plugin_ccusage_ccusage__daily`, `mcp__plugin_ccusage_ccusage__report` and so on. Try it with:

```text
/ccusage claude daily
```

### Claude Desktop

Download `ccusage.mcpb` from [Releases](https://github.com/Desulation/ccusage/releases) and open it with Claude Desktop to install it as a desktop extension. It is the same bundle the plugin uses.

### Other MCP clients

Download the archive for your platform from [Releases](https://github.com/Desulation/ccusage/releases):

| Platform            | Archive                          |
| ------------------- | -------------------------------- |
| Linux x64           | `ccusage-mcp-linux-x64.tar.gz`   |
| Linux arm64         | `ccusage-mcp-linux-arm64.tar.gz` |
| macOS Apple silicon | `ccusage-mcp-osx-arm64.tar.gz`   |
| macOS Intel         | `ccusage-mcp-osx-x64.tar.gz`     |
| Windows x64         | `ccusage-mcp-win-x64.zip`        |
| Windows arm64       | `ccusage-mcp-win-arm64.zip`      |

Each archive contains the server (`ccusage-mcp`) and the engine (`ccusage`) side by side, plus `LICENSE`; on Windows they are `ccusage-mcp.exe` and `ccusage.exe`. The Windows arm64 archive pairs the x64 server, which runs under emulation, with the native arm64 engine. `SHA256SUMS` on the release lists the checksum of every asset.

Extract the archive, keep both executables in the same directory, and register the server with your client, for example in a `.mcp.json` file:

```json
{
	"mcpServers": {
		"ccusage": {
			"command": "/path/to/ccusage-mcp"
		}
	}
}
```

On Windows, point `command` at `ccusage-mcp.exe` and write each backslash in the path as `\\`. Registered under the name `ccusage`, the server's prompt is `/ccusage:ccusage` in Claude Code.

On macOS, a browser download is quarantined. Clear the flag before the first start:

```bash
xattr -d com.apple.quarantine ccusage ccusage-mcp
```

The same archive gives you the standalone `ccusage` CLI; see [Installation](/guide/installation).

## Tools

All tools share these rules:

- Parameter names are kebab-case and equal the long CLI flag without `--`: `--since` is `since`, `--by-agent` is `by-agent`. Everything is optional except `hook-input` on `statusline` and `args` on `report`.
- `daily`, `weekly`, `monthly` and `session` take `source`, which defaults to `all` (every detected source in one report). The source ids are `claude`, `codex`, `opencode`, `amp`, `droid`, `codebuff`, `hermes`, `pi`, `goose`, `kilo`, `copilot`, `gemini`, `antigravity`, `kimi`, `qwen`, `openclaw`, `grok` and `zcode`. `weekly` exists only for `all`, `claude` and `opencode`. `blocks` and `statusline` are Claude Code only and have no `source`.
- Each tool's schema is the union of the options its report accepts across all sources. The **Applies to** column lists the sources that accept a parameter ("every source" means every source the tool accepts). A parameter given for a source that does not accept it is rejected with a message naming the sources that do.
- Unknown parameters are rejected with the list of valid parameters for that source and report. A JSON `null` counts as omitted.
- `date` values are `YYYY-MM-DD` or `YYYYMMDD`. Defaults are the engine's own defaults when the parameter is omitted.
- `offline` and `cache` are negatable: `false` sends `--no-offline` / `--no-cache`. For the other booleans `false` is the same as omitting them.
- The server always adds `--json` (except for `statusline`, which has no JSON mode). `--json`, `--compact`, `--color`, `--no-color`, `--help`, `--version` and `--all` are never exposed.

Long date ranges produce large JSON. Narrow them with `since`/`until` or `last`.

## daily

Token usage and cost grouped by day. Default source: all detected sources. Long ranges produce large JSON; prefer since/until or last.

| Parameter         | Type                                      | Description                                                                                                                                                | Applies to                                                                                                                           |
| ----------------- | ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| `source`          | string                                    | `all` or any [source id](#tools). Default: `all`.                                                                                                          | —                                                                                                                                    |
| `since`           | date                                      | Filter from date (YYYY-MM-DD or YYYYMMDD).                                                                                                                 | every source                                                                                                                         |
| `until`           | date                                      | Filter until date (inclusive).                                                                                                                             | every source                                                                                                                         |
| `timezone`        | string                                    | Timezone for date grouping (IANA).                                                                                                                         | every source                                                                                                                         |
| `sections`        | string                                    | Emit multiple unified report sections from one load (daily, weekly, monthly, session). Comma-separated; each item one of: daily, weekly, monthly, session. | all                                                                                                                                  |
| `by-agent`        | boolean                                   | Include per-agent JSON breakdowns in unified report rows. Default: `false`.                                                                                | all                                                                                                                                  |
| `offline`         | boolean                                   | Use cached pricing data where supported. Default: `false`.                                                                                                 | every source                                                                                                                         |
| `no-cost`         | boolean                                   | Hide cost information in table and JSON output. Default: `false`.                                                                                          | all, codex, opencode, amp, droid, codebuff, hermes, pi, goose, kilo, copilot, gemini, antigravity, kimi, qwen, openclaw, grok, zcode |
| `config`          | string                                    | Path to configuration file. Default: `auto-discovery`.                                                                                                     | every source                                                                                                                         |
| `last`            | integer ≥ 1                               | Show only the most recent N periods of the report (1 is today, this week, or this month).                                                                  | every source                                                                                                                         |
| `mode`            | one of `auto`, `calculate`, `display`     | Cost calculation mode. Default: `auto`.                                                                                                                    | claude                                                                                                                               |
| `debug`           | boolean                                   | Show pricing mismatch information for debugging. Default: `false`.                                                                                         | claude                                                                                                                               |
| `debug-samples`   | integer ≥ 0                               | Number of sample discrepancies to show in debug output. Default: `5`.                                                                                      | claude                                                                                                                               |
| `order`           | one of `desc`, `asc`                      | Sort order. Default: `asc`.                                                                                                                                | claude                                                                                                                               |
| `breakdown`       | boolean                                   | Show per-model cost breakdown. Default: `false`.                                                                                                           | claude                                                                                                                               |
| `single-thread`   | boolean                                   | Disable parallel JSONL file loading. Default: `false`.                                                                                                     | claude                                                                                                                               |
| `instances`       | boolean                                   | Show usage breakdown by project/instance. Default: `false`.                                                                                                | claude                                                                                                                               |
| `project`         | string                                    | Filter to specific project name.                                                                                                                           | claude                                                                                                                               |
| `project-aliases` | string                                    | Comma-separated project aliases (e.g., 'ccusage=Usage Tracker,myproject=My Project').                                                                      | claude                                                                                                                               |
| `speed`           | one of `auto`, `standard`, `flex`, `fast` | Cost speed tier: auto uses recorded settings, then Codex config.toml; use standard, flex, or fast to override. Default: `auto`.                            | codex                                                                                                                                |
| `pi-path`         | string                                    | Path or comma-separated paths to pi-agent sessions directories.                                                                                            | pi                                                                                                                                   |
| `open-claw-path`  | string                                    | Path or comma-separated paths to OpenClaw data directories (default: ~/.openclaw, ~/.clawdbot, ~/.moltbot, ~/.moldbot).                                    | openclaw                                                                                                                             |

## weekly

Token usage and cost grouped by week. Default source: all detected sources. Long ranges produce large JSON; prefer since/until or last.

| Parameter       | Type                                                                                | Description                                                                                                                                                | Applies to    |
| --------------- | ----------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------- |
| `source`        | one of `all`, `claude`, `opencode`                                                  | Data source. Default: `all`.                                                                                                                               | —             |
| `since`         | date                                                                                | Filter from date (YYYY-MM-DD or YYYYMMDD).                                                                                                                 | every source  |
| `until`         | date                                                                                | Filter until date (inclusive).                                                                                                                             | every source  |
| `timezone`      | string                                                                              | Timezone for date grouping (IANA).                                                                                                                         | every source  |
| `sections`      | string                                                                              | Emit multiple unified report sections from one load (daily, weekly, monthly, session). Comma-separated; each item one of: daily, weekly, monthly, session. | all           |
| `by-agent`      | boolean                                                                             | Include per-agent JSON breakdowns in unified report rows. Default: `false`.                                                                                | all           |
| `offline`       | boolean                                                                             | Use cached pricing data where supported. Default: `false`.                                                                                                 | every source  |
| `no-cost`       | boolean                                                                             | Hide cost information in table and JSON output. Default: `false`.                                                                                          | all, opencode |
| `config`        | string                                                                              | Path to configuration file. Default: `auto-discovery`.                                                                                                     | every source  |
| `start-of-week` | one of `sunday`, `monday`, `tuesday`, `wednesday`, `thursday`, `friday`, `saturday` | Start day of the week. Default: `monday`.                                                                                                                  | all, claude   |
| `last`          | integer ≥ 1                                                                         | Show only the most recent N periods of the report (1 is today, this week, or this month).                                                                  | every source  |
| `mode`          | one of `auto`, `calculate`, `display`                                               | Cost calculation mode. Default: `auto`.                                                                                                                    | claude        |
| `debug`         | boolean                                                                             | Show pricing mismatch information for debugging. Default: `false`.                                                                                         | claude        |
| `debug-samples` | integer ≥ 0                                                                         | Number of sample discrepancies to show in debug output. Default: `5`.                                                                                      | claude        |
| `order`         | one of `desc`, `asc`                                                                | Sort order. Default: `asc`.                                                                                                                                | claude        |
| `breakdown`     | boolean                                                                             | Show per-model cost breakdown. Default: `false`.                                                                                                           | claude        |
| `single-thread` | boolean                                                                             | Disable parallel JSONL file loading. Default: `false`.                                                                                                     | claude        |

## monthly

Token usage and cost grouped by month. Default source: all detected sources. Long ranges produce large JSON; prefer since/until or last.

| Parameter        | Type                                      | Description                                                                                                                                                | Applies to                                                                                                                           |
| ---------------- | ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| `source`         | string                                    | `all` or any [source id](#tools). Default: `all`.                                                                                                          | —                                                                                                                                    |
| `since`          | date                                      | Filter from date (YYYY-MM-DD or YYYYMMDD).                                                                                                                 | every source                                                                                                                         |
| `until`          | date                                      | Filter until date (inclusive).                                                                                                                             | every source                                                                                                                         |
| `timezone`       | string                                    | Timezone for date grouping (IANA).                                                                                                                         | every source                                                                                                                         |
| `sections`       | string                                    | Emit multiple unified report sections from one load (daily, weekly, monthly, session). Comma-separated; each item one of: daily, weekly, monthly, session. | all                                                                                                                                  |
| `by-agent`       | boolean                                   | Include per-agent JSON breakdowns in unified report rows. Default: `false`.                                                                                | all                                                                                                                                  |
| `offline`        | boolean                                   | Use cached pricing data where supported. Default: `false`.                                                                                                 | every source                                                                                                                         |
| `no-cost`        | boolean                                   | Hide cost information in table and JSON output. Default: `false`.                                                                                          | all, codex, opencode, amp, droid, codebuff, hermes, pi, goose, kilo, copilot, gemini, antigravity, kimi, qwen, openclaw, grok, zcode |
| `config`         | string                                    | Path to configuration file. Default: `auto-discovery`.                                                                                                     | every source                                                                                                                         |
| `last`           | integer ≥ 1                               | Show only the most recent N periods of the report (1 is today, this week, or this month).                                                                  | every source                                                                                                                         |
| `mode`           | one of `auto`, `calculate`, `display`     | Cost calculation mode. Default: `auto`.                                                                                                                    | claude                                                                                                                               |
| `debug`          | boolean                                   | Show pricing mismatch information for debugging. Default: `false`.                                                                                         | claude                                                                                                                               |
| `debug-samples`  | integer ≥ 0                               | Number of sample discrepancies to show in debug output. Default: `5`.                                                                                      | claude                                                                                                                               |
| `order`          | one of `desc`, `asc`                      | Sort order. Default: `asc`.                                                                                                                                | claude                                                                                                                               |
| `breakdown`      | boolean                                   | Show per-model cost breakdown. Default: `false`.                                                                                                           | claude                                                                                                                               |
| `single-thread`  | boolean                                   | Disable parallel JSONL file loading. Default: `false`.                                                                                                     | claude                                                                                                                               |
| `speed`          | one of `auto`, `standard`, `flex`, `fast` | Cost speed tier: auto uses recorded settings, then Codex config.toml; use standard, flex, or fast to override. Default: `auto`.                            | codex                                                                                                                                |
| `pi-path`        | string                                    | Path or comma-separated paths to pi-agent sessions directories.                                                                                            | pi                                                                                                                                   |
| `open-claw-path` | string                                    | Path or comma-separated paths to OpenClaw data directories (default: ~/.openclaw, ~/.clawdbot, ~/.moltbot, ~/.moldbot).                                    | openclaw                                                                                                                             |

## session

Token usage and cost grouped by session. Default source: all detected sources. Long ranges produce large JSON; prefer since/until or last.

| Parameter        | Type                                      | Description                                                                                                                                                | Applies to                                                                                                                           |
| ---------------- | ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| `source`         | string                                    | `all` or any [source id](#tools). Default: `all`.                                                                                                          | —                                                                                                                                    |
| `since`          | date                                      | Filter from date (YYYY-MM-DD or YYYYMMDD).                                                                                                                 | every source                                                                                                                         |
| `until`          | date                                      | Filter until date (inclusive).                                                                                                                             | every source                                                                                                                         |
| `timezone`       | string                                    | Timezone for date grouping (IANA).                                                                                                                         | every source                                                                                                                         |
| `sections`       | string                                    | Emit multiple unified report sections from one load (daily, weekly, monthly, session). Comma-separated; each item one of: daily, weekly, monthly, session. | all                                                                                                                                  |
| `by-agent`       | boolean                                   | Include per-agent JSON breakdowns in unified report rows. Default: `false`.                                                                                | all                                                                                                                                  |
| `offline`        | boolean                                   | Use cached pricing data where supported. Default: `false`.                                                                                                 | every source                                                                                                                         |
| `no-cost`        | boolean                                   | Hide cost information in table and JSON output. Default: `false`.                                                                                          | all, codex, opencode, amp, droid, codebuff, hermes, pi, goose, kilo, copilot, gemini, antigravity, kimi, qwen, openclaw, grok, zcode |
| `config`         | string                                    | Path to configuration file. Default: `auto-discovery`.                                                                                                     | every source                                                                                                                         |
| `id`             | string                                    | Filter to specific session ID.                                                                                                                             | all, claude, codex                                                                                                                   |
| `mode`           | one of `auto`, `calculate`, `display`     | Cost calculation mode. Default: `auto`.                                                                                                                    | claude                                                                                                                               |
| `debug`          | boolean                                   | Show pricing mismatch information for debugging. Default: `false`.                                                                                         | claude                                                                                                                               |
| `debug-samples`  | integer ≥ 0                               | Number of sample discrepancies to show in debug output. Default: `5`.                                                                                      | claude                                                                                                                               |
| `order`          | one of `desc`, `asc`                      | Sort order. Default: `asc`.                                                                                                                                | claude                                                                                                                               |
| `breakdown`      | boolean                                   | Show per-model cost breakdown. Default: `false`.                                                                                                           | claude                                                                                                                               |
| `single-thread`  | boolean                                   | Disable parallel JSONL file loading. Default: `false`.                                                                                                     | claude                                                                                                                               |
| `speed`          | one of `auto`, `standard`, `flex`, `fast` | Cost speed tier: auto uses recorded settings, then Codex config.toml; use standard, flex, or fast to override. Default: `auto`.                            | codex                                                                                                                                |
| `pi-path`        | string                                    | Path or comma-separated paths to pi-agent sessions directories.                                                                                            | pi                                                                                                                                   |
| `open-claw-path` | string                                    | Path or comma-separated paths to OpenClaw data directories (default: ~/.openclaw, ~/.clawdbot, ~/.moltbot, ~/.moldbot).                                    | openclaw                                                                                                                             |

## blocks

Claude Code usage grouped by 5-hour billing blocks (Claude only).

| Parameter        | Type                                  | Description                                                                                       |
| ---------------- | ------------------------------------- | ------------------------------------------------------------------------------------------------- |
| `since`          | date                                  | Filter from date (YYYYMMDD format).                                                               |
| `until`          | date                                  | Filter until date (YYYYMMDD format).                                                              |
| `mode`           | one of `auto`, `calculate`, `display` | Cost calculation mode. Default: `auto`.                                                           |
| `debug`          | boolean                               | Show pricing mismatch information for debugging. Default: `false`.                                |
| `debug-samples`  | integer ≥ 0                           | Number of sample discrepancies to show in debug output. Default: `5`.                             |
| `order`          | one of `desc`, `asc`                  | Sort order. Default: `asc`.                                                                       |
| `breakdown`      | boolean                               | Show per-model cost breakdown. Default: `false`.                                                  |
| `offline`        | boolean                               | Use cached pricing data for Claude models instead of fetching from API. Default: `false`.         |
| `single-thread`  | boolean                               | Disable parallel JSONL file loading. Default: `false`.                                            |
| `timezone`       | string                                | Timezone for date grouping (e.g., UTC, America/New_York, Asia/Tokyo). Default: `system timezone`. |
| `config`         | string                                | Path to configuration file. Default: `auto-discovery`.                                            |
| `active`         | boolean                               | Show only active block with projections. Default: `false`.                                        |
| `recent`         | boolean                               | Show blocks from last 3 days (including active). Default: `false`.                                |
| `token-limit`    | positive integer or `max`             | Token limit for quota warnings (e.g., 500000 or "max").                                           |
| `session-length` | number ≥ 0                            | Session block duration in hours. Default: `5`.                                                    |

`token-limit` also accepts a JSON integer. See [Blocks Reports](/guide/blocks-reports) for what the fields mean.

## statusline

Claude Code status line text for a hook payload (Claude only; returns plain text).

| Parameter                  | Type                                        | Description                                                                                                                                                        |
| -------------------------- | ------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `hook-input`               | object (required)                           | Claude Code status-line hook payload (session_id, transcript_path, cwd, model, workspace, version, cost, context_window). Written to the engine's stdin unchanged. |
| `offline`                  | boolean                                     | Use cached pricing data for Claude models instead of fetching from API. Default: `true`.                                                                           |
| `visual-burn-rate`         | one of `off`, `emoji`, `text`, `emoji-text` | Controls the visualization of the burn rate status. Default: `off`.                                                                                                |
| `cost-source`              | one of `auto`, `ccusage`, `cc`, `both`      | Session cost source. Default: `auto`.                                                                                                                              |
| `cache`                    | boolean                                     | Enable cache for status line output. Default: `true`.                                                                                                              |
| `refresh-interval`         | integer ≥ 0                                 | Refresh interval in seconds for cache expiry. Default: `1`.                                                                                                        |
| `context-low-threshold`    | integer ≥ 0                                 | Context usage percentage below which status is shown in green (0-100). Default: `50`.                                                                              |
| `context-medium-threshold` | integer ≥ 0                                 | Context usage percentage below which status is shown in yellow (0-100). Default: `80`.                                                                             |
| `timezone`                 | string                                      | Timezone for date grouping (IANA).                                                                                                                                 |
| `config`                   | string                                      | Path to configuration file. Default: `auto-discovery`.                                                                                                             |
| `debug`                    | boolean                                     | Show pricing mismatch information for debugging. Default: `false`.                                                                                                 |

`hook-input` must be a JSON object; it is serialized and written to the engine's stdin as Claude Code's status-line hook would. The result is the engine's plain-text status line, not JSON. `statusline` is not available through `report`, the prompt or the skill, because they have no way to pass the payload. See [Statusline Integration](/guide/statusline).

## list-sources

List supported sources, their reports, and the server/engine versions and engine path (engine-error when the engine cannot be located or queried).

No parameters. The result fields are described under [Results and errors](#results-and-errors). Use it to check which engine the server found and which reports each source supports.

## report

Run any ccusage report from CLI-style arguments; same validation as the typed tools.

| Parameter | Type              | Description                                                                                                                                   |
| --------- | ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `args`    | string (required) | CLI-style arguments as typed after `ccusage`, e.g. "codex daily --since 2026-10-01", "weekly --last 2 --by-agent", "" (= daily, all sources). |

Grammar: an optional source, an optional report (default `daily`), then flags.

- Long flags and their short aliases from `ccusage --help` are accepted, as `--flag value` or `--flag=value`. A value in double quotes may contain spaces.
- A flag whose value is optional takes the next word only when it does not start with `-`.
- `--no-offline` sets `offline` to `false`.
- `blocks` is a Claude Code report, so write `claude blocks`; `blocks` alone is rejected because the default source is `all`.
- Unknown words, the never-exposed flags (`--json`, `--compact`, `--color`, `--no-color`, `--help`, `--version`, `--all` and the short forms `-j`, `-h`, `-v`) and `statusline` are rejected.

Validation and the engine command line are the same as for the typed tools. This is the tool the `/ccusage` skill calls.

## Prompt `ccusage`

The `ccusage` prompt takes one argument, `args`, with the same grammar as the [`report`](#report) tool (empty means a daily report across all sources). It runs the report and returns one user message holding the command that ran, the engine JSON in a fenced `json` block, and an instruction to present it as a readable table with totals and the main cost drivers. On a validation or engine error the message carries the error text instead.

Claude Code lists MCP prompts as `/<server>:<prompt> (MCP)`:

- installed through the plugin, the server is `plugin:ccusage:ccusage`, and the prompt runs as `/mcp__plugin_ccusage_ccusage__ccusage`;
- registered directly as server `ccusage` (for example in `.mcp.json`), it runs as `/ccusage:ccusage`.

For Claude Code plugin users, the `/ccusage` skill below is the short form.

## Skill `/ccusage`

The plugin adds a `/ccusage` skill. It calls the `report` tool with `args` set to exactly the text you typed after `/ccusage`, then presents the JSON as a table with totals and the top cost drivers. On an error it shows the error and the valid choices the error lists.

```text
/ccusage claude daily
/ccusage codex monthly
/ccusage weekly --last 2
```

The bare `/ccusage` works unless another command already uses that name; `/ccusage:ccusage` always works. You can also just ask a question ("Which Claude Code sessions cost the most this week?") and let Claude pick a tool.

## Results and errors

- **Success:** the first content block is the engine's stdout as text, unmodified: JSON for `daily`, `weekly`, `monthly`, `session`, `blocks` and `report`, plain text for `statusline`. When the engine wrote to stderr (for example `debug` output or a pricing-fetch fallback warning), a second text block starts with `stderr:`.
- **Validation error:** a tool error (`isError: true`) naming the parameter, the problem and the valid choices. The engine is not started.
- **Engine error:** a tool error with the exit code and the engine's stderr (`ccusage exited with code …`), the timeout for a run that took too long (`ccusage timed out after … s.`), or, when no engine was found, every location checked (see [Engine location](#engine-location)).
- **Cancellation:** cancelling the MCP request stops the engine and its child processes.

`list-sources` always succeeds. Its result is JSON:

```json
{
	"server-version": "20.0.45",
	"engine-version": "20.0.45",
	"engine-path": "/path/to/ccusage",
	"sources": [
		{
			"id": "all",
			"display-name": "All sources",
			"reports": ["daily", "weekly", "monthly", "session"]
		},
		{
			"id": "codex",
			"display-name": "Codex",
			"reports": ["daily", "monthly", "session"]
		}
	]
}
```

The real list has every source. When the engine cannot be located or queried, `engine-error` holds the reason and `engine-version` (and, if the engine was not found, `engine-path`) is `null`; `engine-error` is left out otherwise.

## Engine location

The server looks for the engine in this order; the first existing file wins:

1. `CCUSAGE_ENGINE`, an absolute path to the engine executable.
2. `ccusage` (`ccusage.exe` on Windows) in the server's own directory. This is the layout of the release archives and of the Linux/macOS part of the bundle.
3. `<arch>/ccusage` in the server's directory, where `<arch>` is the operating system's architecture, `x64` or `arm64`. This is the Windows bundle layout: the x64 server running under emulation on Windows arm64 still picks the native arm64 engine.
4. `ccusage` on `PATH`.

The server's directory is where `ccusage-mcp` itself lives, never the client's working directory. If `CCUSAGE_ENGINE` points at a file that does not exist, the search continues with rule 2. `ccusage-mcp --version` prints the server version and then the version of the engine it found (or why none was usable).

## Environment

| Variable                      | Effect                                                                                                                                 |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `CCUSAGE_ENGINE`              | Absolute path to the engine; checked first (see [Engine location](#engine-location)).                                                  |
| `CCUSAGE_MCP_TIMEOUT_SECONDS` | Time limit for one engine run, in whole seconds. Default `120`. Any other value than a positive integer logs a warning and uses `120`. |

The engine inherits the server's environment, with `NO_COLOR=1` added. Data-location variables such as `CLAUDE_CONFIG_DIR`, `CODEX_HOME` or `OPENCODE_DATA_DIR` therefore work as they do for the CLI (see [Environment Variables](/guide/environment-variables)), as long as they are set in the environment the MCP client starts the server from. Most clients also accept an `env` object next to `command` in their server configuration.

## Troubleshooting

### The tool is unavailable after installation

If the tool is unavailable right after installation, run `/mcp` once to reconnect.

### Engine not found

The error starts with `ccusage engine not found. Set CCUSAGE_ENGINE or place ccusage next to ccusage-mcp.` and is followed by a `Checked:` list of every path tried, in the order above. Put `ccusage` next to `ccusage-mcp` (both come in the same archive), or set `CCUSAGE_ENGINE` to the engine's absolute path. `list-sources` reports the same problem in `engine-error`.

### Timeouts

A run that exceeds the limit is stopped and reported as `ccusage timed out after 120 s.` (or your configured value). Narrow the request with `since`/`until` or `last`, or raise `CCUSAGE_MCP_TIMEOUT_SECONDS`.

### Large outputs

Claude Code truncates very large MCP results. Ask for a shorter period with `since`/`until` or `last`, or drop the cost fields with `no-cost` (not available for `claude` reports).

### macOS: quarantined binaries

If macOS refuses to run `ccusage-mcp` or `ccusage`, the files were quarantined by a browser download. Run `xattr -d com.apple.quarantine ccusage ccusage-mcp` in the extracted directory. Bundles that Claude Code downloads itself are not affected.

### No data

The engine reads the same local files as the CLI. See [Getting Started](/guide/getting-started#troubleshooting) for the default data locations of each source.
