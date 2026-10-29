# ccusage C# AOT MCP Server — Design

- **Date:** 2026-10-29
- **Repository:** `Desulation/ccusage` (fork of `ccusage/ccusage`), branch `feat/csharp-mcp-server`
- **Status:** Draft for review

## 1. Intent

Replace the npm distribution layer of this fork with a C# .NET 10 Native AOT MCP server,
delivered as a Claude Code plugin. The Rust engine in `rust/` stays the source of truth for
parsing, pricing, and aggregation. The C# server exposes every engine report to MCP clients
as typed tools, a slash-command prompt, and a `/ccusage` skill.

### What the user asked for

- Port the features to C#/.NET 10/AOT as an MCP server, using Microsoft libraries where
  possible; third-party libraries need explicit approval before entering the spec.
- Scope: **wrap the Rust engine** — npm is removed and C# AOT takes its place. No C#
  reimplementation of engine logic.
- The fork (`Desulation/ccusage`) is the repository; no separate repo.
- stdio transport only.
- Typed tools (one per report, `source` as a parameter) **plus** slash commands.
- Ship as a **Claude Code plugin** with a **single `.mcpb` bundle** for all platforms.
- Keep the VitePress `docs/` site (pnpm stays as its build tool only).
- kebab-case parameter names, matching the CLI flags.
- Delete upstream's contribution-gate automation, `update-pricing.yaml`, `check-links.yaml`;
  trim `check-pr-title.yaml`; add `sync-upstream.yaml` with CI run by hand on sync PRs.
- Keep Nix (Rust release builds, flake check); remove only `publint`.
- Planning by Fable; implementation by Opus; review by Opus or Fable by task complexity.

### Success criteria

1. `/plugin marketplace add Desulation/ccusage` then `/plugin install ccusage@ccusage`
   gives a working `ccusage` MCP server on Windows, Linux, and macOS (x64 and arm64).
2. Every engine report and option (except terminal-presentation flags) is reachable through
   the typed tools, the `report` tool, the `ccusage` MCP prompt, and `/ccusage <args>`.
3. For any request, the tool result equals the engine's own `--json` output for the same
   arguments (pass-through parity), verified by end-to-end tests on fixtures.
4. `ccusage-mcp` publishes as Native AOT with zero trim/AOT warnings.
5. A tagged release produces `ccusage.mcpb`, per-platform archives, and `SHA256SUMS` on a
   GitHub Release with no manual steps.
6. No npm publishing, npm launcher, or platform npm packages remain in the repo.

### Non-goals

- Reimplementing any engine logic (adapters, pricing, aggregation) in C#.
- Terminal table rendering, colors, compact layout, `--help` text.
- HTTP / Streamable HTTP transport.
- A CLI mode in `ccusage-mcp` beyond `--version`.
- Automated evaluation of Claude's behavior when running the skill (`claude plugin eval`).

## 2. Guiding rule: minimal upstream divergence

The fork pulls upstream weekly (§9.4). Every edit to an upstream-owned file is a future merge
conflict. **Change upstream files only where removing npm distribution requires it.** New
work lives in new paths (`dotnet/`, `plugin/`, `.claude-plugin/`, new workflows).

## 3. Repository changes

### 3.1 Removed

| Path                                                                                             | Reason                                                                                         |
| ------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------- |
| `apps/ccusage/` (except fixtures, see §3.3)                                                      | npm package, `cli.js` launcher, TS tests, packaging and npm-launcher benchmark scripts         |
| `packages/ccusage-*` (6 dirs)                                                                    | platform binary npm packages                                                                   |
| `pnpm-lock.yaml` (regenerated, see §3.2), `.oxlintrc.json`, `.oxfmtrc.json`                      | JS workspace lint/format for removed packages                                                  |
| `.github/scripts/npm-registry-gate.nu`                                                           | npm publish gate                                                                               |
| `.github/workflows/{pr-gate,issue-gate,gate-close-pr,approve-contributor,pullfrog,autofix}.y*ml` | upstream contribution-gate automation (needs upstream apps/secrets)                            |
| `.github/scripts/contribution-gate*`, `.github/APPROVED_CONTRIBUTORS`                            | used only by the above                                                                         |
| `.github/workflows/update-pricing.yaml`                                                          | pricing arrives via upstream sync; running it here conflicts with upstream's identical commits |
| `.github/workflows/check-links.yaml`                                                             | low value; docs build is checked in CI                                                         |
| `nix/tools/publint/` and its references                                                          | lints npm packages before publish                                                              |
| `.github/actions/build-*-native-package` npm staging steps                                       | replaced by engine build steps (§9.1)                                                          |
| `.agents/skills/typescript`, `.agents/skills/testing/references/node-test.md`                    | describe removed TS launcher; replaced by a `dotnet` skill                                     |

### 3.2 Modified (upstream files — keep edits minimal)

| Path                                                                                                              | Change                                                                                                                                                                                                                      |
| ----------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `package.json` (root)                                                                                             | Keep as version source and pnpm workspace root for `docs/` only: keep `name`, `version`, `description`, `license`, `private`; `workspaces: ["docs"]`; drop `scripts` and `packageManager` bootstrap (pnpm is pinned by Nix) |
| `pnpm-workspace.yaml`                                                                                             | `packages: [docs]`; keep security settings and the `docs` catalog; drop the `types` catalog entries no longer used                                                                                                          |
| `pnpm-lock.yaml`                                                                                                  | regenerated for the docs-only workspace                                                                                                                                                                                     |
| `package.nix`                                                                                                     | read `name`/meta from root `package.json` instead of `apps/ccusage/package.json`                                                                                                                                            |
| `config-schema.json`                                                                                              | moved from `apps/ccusage/config-schema.json` to repo root                                                                                                                                                                   |
| `nix/checks.nix`, `nix/treefmt.nix`, `nix/git-hooks.nix`, `justfile`, `ccusage.example.json`, `docs/package.json` | point at root `config-schema.json`; drop `mod ccusage`, the launcher Node test (keep `nix/tools/models-dev-gen` test), large-fixture generator recipe                                                                       |
| `.github/workflows/ci.yaml`, `release.yaml`                                                                       | rewritten per §9                                                                                                                                                                                                            |
| `.github/workflows/check-pr-title.yaml`                                                                           | keep only the Conventional Commits check; drop the adapter-scope job                                                                                                                                                        |
| `.github/actions/setup-nix-cache`                                                                                 | drop Blacksmith sticky-disk usage                                                                                                                                                                                           |
| `.tagpr`                                                                                                          | `versionFile` per §8                                                                                                                                                                                                        |
| `AGENTS.md`, `apps/ccusage/AGENTS.md` references, `.agents/skills/{development,testing,docs}`                     | describe the C# layer instead of the npm layer                                                                                                                                                                              |
| `docs/guide/**` + new `docs/guide/mcp.md`                                                                         | per §3.4                                                                                                                                                                                                                    |
| `README.md` (symlink into `apps/ccusage/`)                                                                        | becomes a real root `README.md`, rewritten per §3.4                                                                                                                                                                         |
| `LICENSE` (symlink to `apps/ccusage/LICENSE`)                                                                     | becomes a real root `LICENSE` file, content unchanged (MIT)                                                                                                                                                                 |

`rust/` is **not** modified.

### 3.3 Added

```
dotnet/
├── global.json                      SDK 10.0.x, rollForward latestFeature, MTP test runner
├── Directory.Build.props            net10.0, nullable, warnings-as-errors, <Version>
├── Directory.Packages.props         central package versions
├── Ccusage.Mcp.slnx
├── AGENTS.md
├── packaging/
│   ├── manifest.template.json       .mcpb manifest template (§7.3)
│   └── launch.sh                    Linux/macOS arch launcher (§7.3)
├── src/Ccusage.Mcp/                 the server (§4)
└── tests/
    ├── Ccusage.Mcp.Tests/           MSTest unit + integration + e2e (§10)
    ├── FakeEngine/                  test-only console app standing in for the engine
    └── fixtures/                    copied from apps/ccusage/test/ before deletion
.claude-plugin/marketplace.json      marketplace with one plugin entry (§7.1)
plugin/
├── .claude-plugin/plugin.json
└── skills/ccusage/SKILL.md
.agents/skills/dotnet/SKILL.md       how to work in dotnet/
.github/workflows/sync-upstream.yaml (§9.4)
```

### 3.4 README and docs content

The root `README.md` replaces the `apps/ccusage/README.md` symlink. Its content:

**Remove**

- npm badges (Socket, npm version, npm downloads).
- Upstream's promotional content: Major Sponsors, GitHub Sponsors, Star History, and the
  DeepWiki, Trendshift, and Awesome badges (they would present upstream's sponsors and
  stats as this fork's).
- Every `npx` / `bunx` / `pnpm dlx` / `pnpx` example, pkg.pr.new preview builds, and the bunx
  caching note.
- `nix run github:ccusage/ccusage` (runs upstream, not this fork).
- Development notes that no longer apply: `pnpm install` for workspace `node_modules`, the
  scheduled "update pricing" workflow paragraph.

**Rewrite**

- **Intro:** what the fork is — ccusage reports for MCP clients through a .NET Native AOT
  server, with the Rust engine unchanged — and a credit line: "Fork of
  [ccusage/ccusage](https://github.com/ccusage/ccusage) by @ryoppippi".
- **Quick Start:** `/plugin marketplace add Desulation/ccusage` →
  `/plugin install ccusage@ccusage` → `/ccusage claude daily`.
- **Installation:** (1) Claude Code plugin (recommended); (2) Claude Desktop via
  `ccusage.mcpb` from Releases; (3) other MCP clients via a per-platform archive plus a
  `.mcp.json` snippet; (4) the standalone `ccusage` CLI from the same archive, with the macOS
  `xattr` note for browser downloads.
- **Usage:** slash commands (`/ccusage …`); example questions that trigger the tools; the
  existing CLI examples with `bunx ccusage` changed to `ccusage`.
- **MCP reference:** summary table of tools, the `ccusage` prompt, and the `/ccusage` skill,
  linking to `docs/guide/mcp.md`.
- **Development:** keep the Nix/direnv/`just` setup; add a `dotnet/` section (.NET 10 SDK,
  build, test, AOT publish); mention `sync-upstream.yaml`.

**Keep:** Supported Sources table, Features list (add an MCP line), License
(MIT © @ryoppippi). The Documentation link changes from upstream's `ccusage.com` to this
repo's `docs/guide/` (the fork's docs site is built in CI but not deployed; hosting, e.g.
GitHub Pages, is out of scope).

**Docs site (`docs/`):**

- Every `npx ccusage@latest …` / `bunx ccusage …` across `docs/guide/**` becomes
  `ccusage …`.
- `installation.md` and `getting-started.md` are rewritten to match the README's
  Installation and Quick Start.
- New `docs/guide/mcp.md`: full tool, prompt, and skill reference (§5, §7.2), engine
  location (§6.1), and troubleshooting (engine not found, timeouts, large outputs).
- Sponsor and promotional content — `docs/guide/sponsors.md`, the sponsor block on
  `docs/index.md`, and the Sponsor/Sponsors nav entries and `/sponsor` redirect in
  `docs/.vitepress/config.ts` — gets the same treatment as the README: removed, keeping the
  upstream credit.
- `docs/wrangler.jsonc` (upstream's Cloudflare deployment of ccusage.com) is left in place
  but unused, per §2.

## 4. C# server architecture

Single project `dotnet/src/Ccusage.Mcp`, assembly/executable name `ccusage-mcp`. Published
with `PublishAot=true`, `InvariantGlobalization=true`, `IsAotCompatible=true`, trimming
warnings as errors. Logging to **stderr only** (stdout carries MCP JSON-RPC).

| Unit                        | Responsibility                                                                                                                                                          | Depends on                                         |
| --------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| `Program.cs`                | Host setup (`Host.CreateApplicationBuilder`), `AddMcpServer().WithStdioServerTransport()` plus the custom handlers below; `--version` prints server and engine versions | Microsoft.Extensions.Hosting, ModelContextProtocol |
| `Engine/EngineLocator`      | Resolve the Rust engine (§6.1)                                                                                                                                          | System.Runtime.InteropServices                     |
| `Engine/EngineRunner`       | Run the engine without a shell (§6.2)                                                                                                                                   | System.Diagnostics.Process                         |
| `Commands/CommandCatalog`   | Source/report/option model built from the embedded upstream JSON files plus a C# type table (§4.1)                                                                      | embedded resources                                 |
| `Commands/CommandRequest`   | Normalized request: source, report, ordered option values                                                                                                               | —                                                  |
| `Commands/CommandValidator` | Validate a request against the catalog; build the engine argv                                                                                                           | CommandCatalog                                     |
| `Commands/CliArgsParser`    | Tokenize and parse CLI-style text into a `CommandRequest` (for `report` and the prompt)                                                                                 | CommandValidator                                   |
| `Tools/ToolDefinitions`     | Generate MCP tool definitions and JSON input schemas from the catalog                                                                                                   | CommandCatalog                                     |
| `Tools/ToolHandlers`        | `tools/list` and `tools/call` handlers; JSON arguments → `CommandRequest` → engine → result                                                                             | Commands, Engine                                   |
| `Prompts/PromptHandlers`    | `prompts/list` and `prompts/get` for the `ccusage` prompt                                                                                                               | Commands, Engine                                   |
| `Json/McpJsonContext`       | System.Text.Json source-generated context for every serialized type                                                                                                     | System.Text.Json                                   |

Tools and prompts are registered through the SDK's list/call handler hooks with schemas
generated from catalog data — no reflection-based attribute discovery. This keeps exact
kebab-case names, stays AOT-clean, and makes the drift tests (§10) cover the schemas.

**Data flow:** MCP request → `ToolHandlers`/`PromptHandlers` → `CommandRequest` (typed JSON
arguments or `CliArgsParser`) → `CommandValidator` (failures become MCP errors; the engine is
not run) → argv → `EngineLocator` + `EngineRunner` → engine stdout passed through.

### 4.1 Catalog: derived from upstream data

`dotnet/src/Ccusage.Mcp/Ccusage.Mcp.csproj` embeds, by link (not copy):

- `rust/crates/ccusage-cli-parser/src/cli-commands.json` — root commands, `pages` (every
  valid `[source] report` path), and `combinedOptions`.
- `rust/crates/ccusage-cli-parser/src/cli-help.json` — option groups with flags,
  descriptions, defaults.

From these the catalog derives, with no hand-maintained copy:

- **Sources:** the root commands that have child report pages (the 18 agents).
- **Support matrix:** a `(source, report)` pair is valid iff a page exists for it. The
  unified report is source `all` (pages `daily`, `weekly`, `monthly`, `session`); root
  `blocks` and `statusline` are treated as `claude blocks` / `claude statusline`.
- **Option applicability:** the flags listed for each page via its option groups.

A hand-written C# table supplies only what the JSON lacks, keyed by long flag:

- value kind: `bool`, `negatable-bool` (`--offline`/`--no-offline`, `--cache`/`--no-cache`),
  `string`, `date`, `int`, `int-or-max`, `enum`, `csv-enum`, `path`, `path-list`;
- enum values (from the Rust parsers): `mode` auto|calculate|display; `order` asc|desc;
  `speed` auto|standard|fast|flex; `start-of-week` sunday…saturday; `visual-burn-rate`
  off|emoji|text|emoji-text; `cost-source` auto|ccusage|cc|both; `sections`
  daily|weekly|monthly|session;
- source display names (Claude Code, Codex, OpenCode, Amp, Droid, Codebuff, Hermes,
  pi-agent, Goose, Kilo, GitHub Copilot CLI, Gemini CLI, Antigravity, Kimi, Qwen, OpenClaw,
  Grok Build CLI, ZCode) with fallback to the id.

**Never exposed:** `--json` (forced on every report except `statusline`, which has no JSON
mode), `--compact`, `--color`, `--no-color`, `--help`, `--version`, `--all` (no-op
compatibility flag). Negated flags (`--no-offline`, `--no-cache`) are expressed as `false`
on the positive parameter.

Drift handling: if upstream adds a flag the C# type table does not know, the drift test
fails (§10.1). If upstream adds a source or page, it is picked up automatically; the
display-name test flags a missing display name.

## 5. MCP contract

All parameter names are kebab-case and equal the long flag name without `--`. All
parameters are optional unless marked required.

### 5.1 Report tools: `daily`, `weekly`, `monthly`, `session`

- `source`: enum `all` + the 18 source ids; default `all`. A `(source, tool)` pair without a
  page is rejected (e.g. `weekly` is valid only for `all`, `claude`, `opencode`).
- The input schema is the **union** of options applicable to that report across all
  sources. Each parameter description is generated with an "Applies to:" list. A parameter
  supplied for a source whose page lacks that flag is rejected with a message naming the
  sources that accept it.

Options by page, as of this upstream snapshot (the catalog is authoritative):

| Applies to                             | Options                                                                           |
| -------------------------------------- | --------------------------------------------------------------------------------- |
| every report page                      | `since`, `until` (YYYY-MM-DD or YYYYMMDD), `timezone` (IANA), `offline`, `config` |
| `all` and all non-Claude sources       | `no-cost`                                                                         |
| `all`                                  | `sections` (csv: daily, weekly, monthly, session), `by-agent`                     |
| `daily`/`weekly`/`monthly` pages       | `last` (int ≥ 1)                                                                  |
| `session` for `all`, `claude`, `codex` | `id`                                                                              |
| `weekly` for `all`, `claude`           | `start-of-week`                                                                   |
| `claude` (all reports)                 | `mode`, `order`, `breakdown`, `debug`, `debug-samples`, `single-thread`           |
| `claude daily`                         | `instances`, `project`, `project-aliases`                                         |
| `codex`                                | `speed`                                                                           |
| `pi`                                   | `pi-path`                                                                         |
| `openclaw`                             | `open-claw-path`                                                                  |

### 5.2 `blocks`

Claude only (no `source` parameter). Options: `since`, `until`, `timezone`, `offline`,
`config`, `mode`, `order`, `breakdown`, `debug`, `debug-samples`, `single-thread`, `active`,
`recent`, `token-limit` (int or `"max"`), `session-length` (number of hours).

### 5.3 `statusline`

- `hook-input` (**required**, object): the Claude Code status-line hook payload (fields such
  as `session_id`, `transcript_path`, `cwd`, `model`, `workspace`, `version`, `cost`,
  `context_window`). Serialized and written to the engine's stdin unchanged.
- Options: `offline`, `config`, `timezone`, `debug`, `visual-burn-rate`, `cost-source`,
  `cache`, `refresh-interval`, `context-low-threshold`, `context-medium-threshold`.
- Result: the engine's plain-text status line.

### 5.4 `list-sources`

No parameters. Returns JSON: `{ "server-version", "engine-version", "engine-path",
"sources": [{ "id", "display-name", "reports": [...] }] }`.

### 5.5 `report`

- `args` (**required**, string): CLI-style arguments as typed after `ccusage`, e.g.
  `"codex daily --since 2026-10-01"`, `"weekly --last 2 --by-agent"`, `""` (= daily, all
  sources).
- Grammar: optional source, optional report (default `daily`), then flags. Long flags and
  the short aliases from `cli-help.json` are accepted; `--flag=value` and `--flag value`;
  double-quoted values may contain spaces. Optional-value flags (`[value]` in
  `cli-help.json`) take the next token only when it does not start with `-`.
- Rejected: unknown tokens, never-exposed flags (§4.1), and `statusline` (needs a hook
  payload; use the `statusline` tool).
- Same validation and argv building as the typed tools. This is what the `/ccusage` skill
  calls.

### 5.6 Prompt `ccusage`

- One argument, `args`, same grammar as the `report` tool. Claude Code lists MCP prompts as
  `/<server>:<prompt> (MCP)`; installed through the plugin the server is
  `plugin:ccusage:ccusage`, so the prompt also runs as `/mcp__plugin_ccusage_ccusage__ccusage`.
  Registered directly (e.g. in another client's config) as server `ccusage`, it is
  `/ccusage:ccusage`. The `/ccusage` skill (§7.2) is the short form for Claude Code users.
- Returns one user message: the arguments that ran, the engine JSON in a fenced `json`
  block, and an instruction to present it as a readable table and call out cost drivers. On
  a validation or engine error the message carries the error text instead.

### 5.7 Results and errors

- **Success:** content block 1 is the engine stdout as text, unmodified. If stderr is
  non-empty (e.g. `debug` output, pricing-fetch fallback warnings), content block 2 is a
  text block that starts with `stderr:`.
- **Validation error:** MCP tool error (`isError: true`) naming the parameter, the problem,
  and the valid choices. The engine is not started.
- **Engine error:** MCP tool error with exit code and stderr; for a timeout, the timeout
  value; for a missing engine, every location checked (§6.1).
- **Annotations:** every tool sets `readOnlyHint: true`, `destructiveHint: false`,
  `idempotentHint: true`. `openWorldHint` is omitted, because the engine fetches LiteLLM
  pricing over the network unless `offline` is set.

## 6. Engine location and execution

### 6.1 `EngineLocator`

Resolution order; the first existing file wins:

1. `CCUSAGE_ENGINE` environment variable (absolute path).
2. `<server dir>/ccusage[.exe]` — release archives and the Linux/macOS bundle layout.
3. `<server dir>/<arch>/ccusage[.exe]` where `<arch>` is the **OS** architecture (`x64` or
   `arm64`) — the Windows bundle layout, so an x64 server running under emulation on Windows
   ARM64 still runs the native arm64 engine.
4. `ccusage[.exe]` on `PATH`.

If none exists, the error lists every path checked. The OS architecture comes from
`RuntimeInformation.OSArchitecture`; if Phase 0 (§11) shows it reports `X64` under
emulation, use `IsWow64Process2` through a source-generated `[LibraryImport]` (AOT-safe).

### 6.2 `EngineRunner`

- `ProcessStartInfo` with `ArgumentList`, `UseShellExecute=false`; never a shell.
- Inherits the server's environment (so `CLAUDE_CONFIG_DIR`, `CODEX_HOME`, etc. work) and
  adds `NO_COLOR=1`.
- Captures stdout and stderr concurrently; writes stdin only for `statusline`.
- Timeout: 120 s by default, overridable with `CCUSAGE_MCP_TIMEOUT_SECONDS`. On timeout or
  MCP request cancellation, kills the process tree and reports it.
- Returns `{ ExitCode, Stdout, Stderr, TimedOut }`.

### 6.3 Security properties

- argv is built only from validated catalog values; free-form values (`project`, `id`,
  paths, `timezone`) are passed as single argv elements, never concatenated or
  shell-interpreted.
- The server never writes files; the engine is invoked only in read-only report modes.
- No network listener (stdio only).

## 7. Plugin, skill, and bundle

### 7.1 Marketplace and plugin

`.claude-plugin/marketplace.json` lists one plugin:

```json
{
	"name": "ccusage",
	"owner": { "name": "Desulation" },
	"plugins": [
		{
			"name": "ccusage",
			"source": "./plugin",
			"description": "Coding-agent CLI token usage and cost reports over MCP",
			"mcpServers": "https://github.com/Desulation/ccusage/releases/download/v<version>/ccusage.mcpb"
		}
	]
}
```

`plugin/.claude-plugin/plugin.json` carries `name: "ccusage"`, `version`, `description`,
`author`, `repository`, `license: "MIT"`. Exact marketplace field placement (entry vs
manifest for `mcpServers`) is settled in Phase 0 against `claude plugin validate`.

Install: `/plugin marketplace add Desulation/ccusage`, then `/plugin install ccusage@ccusage`.

### 7.2 Skill `/ccusage`

`plugin/skills/ccusage/SKILL.md`:

- frontmatter `name: ccusage`, a description ("Show coding-agent token usage and cost
  reports…"), `argument-hint: [source] [report] [--flags]`, and `allowed-tools` listing
  `mcp__plugin_ccusage_ccusage__report`.
- Body: call the `report` tool with `args` set to exactly `$ARGUMENTS` (empty means daily
  across all sources), then present the JSON as a readable table with totals and the top
  cost drivers; on error, show the error and the valid choices it lists.

Usage: `/ccusage claude daily`, `/ccusage codex monthly`, `/ccusage weekly --last 2`. The
bare name works unless another command already uses `ccusage`; `/ccusage:ccusage` always
works.

### 7.3 Single `.mcpb` bundle

```
ccusage.mcpb (zip)
├── manifest.json
├── launch.sh                     Linux/macOS: uname -s/-m → exec <os>/<arch>/ccusage-mcp
├── win/ccusage-mcp.exe           win-x64 build (runs natively, or emulated on ARM64)
├── win/x64/ccusage.exe
├── win/arm64/ccusage.exe
├── linux/x64/{ccusage-mcp,ccusage}
├── linux/arm64/{ccusage-mcp,ccusage}
├── osx/x64/{ccusage-mcp,ccusage}
└── osx/arm64/{ccusage-mcp,ccusage}
```

`manifest.json` (generated from `dotnet/packaging/manifest.template.json`):

```json
{
	"manifest_version": "0.3",
	"name": "ccusage",
	"version": "<version>",
	"description": "Coding-agent CLI token usage and cost reports",
	"author": { "name": "Desulation" },
	"license": "MIT",
	"server": {
		"type": "binary",
		"entry_point": "launch.sh",
		"mcp_config": {
			"command": "/bin/sh",
			"args": ["${__dirname}/launch.sh"],
			"platform_overrides": {
				"win32": { "command": "${__dirname}/win/ccusage-mcp.exe", "args": [] }
			}
		}
	},
	"compatibility": { "platforms": ["darwin", "linux", "win32"] }
}
```

`launch.sh` maps `uname -s` (Linux→linux, Darwin→osx) and `uname -m` (x86_64|amd64→x64,
aarch64|arm64→arm64), checks the target exists and is executable (restoring `+x` if the zip
extraction dropped it), and `exec`s it with `"$@"`; unknown platforms exit non-zero with a
message on stderr. Windows ships only the x64 server; the engine choice happens in
`EngineLocator` rule 3.

Expected size: ~30–50 MB compressed (measured in Phase 0).

## 8. Versioning

- Canonical version: root `package.json` `version` (unchanged upstream wiring: Nix
  `package.nix`, `rust/crates/ccusage/build.rs`, and the `main.rs` version test keep
  reading it).
- tagpr `versionFile`: `package.json`, `docs/package.json`, `plugin/.claude-plugin/plugin.json`,
  `.claude-plugin/marketplace.json` (the version inside the bundle URL),
  `dotnet/Directory.Build.props`.
- CI job `version-consistency` fails if those five disagree.
- The `.mcpb` manifest version is stamped at bundle time from `package.json`.

## 9. CI/CD

All jobs use GitHub-hosted runners (Blacksmith references removed). Third-party actions stay
pinned by commit SHA.

### 9.1 Build matrix (shared by CI and release)

| RID         | Runner             | Engine build                                                  | Server build                                                                                                     |
| ----------- | ------------------ | ------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| linux-x64   | `ubuntu-latest`    | `nix build .#ccusage-static`                                  | `dotnet publish -r linux-x64`                                                                                    |
| linux-arm64 | `ubuntu-24.04-arm` | `nix build .#ccusage-static`                                  | `dotnet publish -r linux-arm64`                                                                                  |
| osx-arm64   | `macos-15-intel`   | `cargo build --release --target aarch64-apple-darwin` (cross) | `dotnet publish -r osx-arm64` (Native AOT cross-arch)                                                            |
| osx-x64     | `macos-15-intel`   | `cargo build --release --target x86_64-apple-darwin`          | `dotnet publish -r osx-x64`                                                                                      |
| win-x64     | `windows-latest`   | `cargo build --release`                                       | `dotnet publish -r win-x64`                                                                                      |
| win-arm64   | `windows-11-arm`   | `cargo build --release`                                       | not built (bundle ships win-x64 server); the per-platform archive pairs the win-x64 server with the arm64 engine |

The Linux and Windows `build-*-native-package` composite actions are reworked to build and
upload the engine binary without npm staging; the macOS legs build inline with cargo, and the
macOS Nix action is removed (§16 item 8). Each matrix job uploads `ccusage-mcp-<rid>` and
`ccusage-<rid>` artifacts.

Both macOS legs run on the Intel `macos-15-intel` runner, because `macos-latest` (Apple
silicon) runners were too scarce to gate on (§16 item 8). osx-arm64 is cross-compiled: cargo
builds for `aarch64-apple-darwin` and the Native AOT publish targets `osx-arm64` from the x64
host. Since nothing in the gating path runs those binaries on arm64, the build job asserts with
`lipo -archs` that the engine and the published server are `arm64` Mach-O binaries (`x86_64`
on osx-x64) and fails otherwise, so `bundle` and a release never receive the wrong
architecture.

### 9.2 `ci.yaml`

- **Kept:** change detection, tirith scan + SARIF upload, preflight (secret scan, actionlint),
  `nix flake check`, Rust tests (`nix build .#ccusage-tests`).
- **Removed:** JS tests, npm dry-run / pkg.pr.new, npm-launcher e2e, PR performance comment,
  Babashka harness test.
- **Added:**
  - `dotnet-test` on ubuntu/windows (macos non-blocking, below): build, MSTest (unit, runner,
    drift, schema snapshot).
  - `build` matrix (§9.1).
  - `e2e` per OS: run the published server and real engine against fixtures through the MCP
    client (§10.3).
  - `bundle`: assemble `ccusage.mcpb` + archives as workflow artifacts; validate layout.
  - `bundle-smoke` per OS: extract the bundle, start it through the manifest's command for
    that OS, run `initialize`, `tools/list`, `list-sources`, `daily` (offline, fixtures).
  - `plugin-validate`: install Claude Code with its native installer (pinned version) and run
    `claude plugin validate --strict` on `plugin/` and the marketplace.
  - `version-consistency` (§8).
- **Non-blocking Apple-silicon legs (§16 item 8):** the `macos-latest` legs of `dotnet-test`,
  `e2e` (osx-arm64) and `bundle-smoke` run as separate jobs named `(non-blocking)` that reuse
  the gating jobs' steps and are not in `ci gate`'s needs: a job that never gets a runner ends
  abandoned, which `continue-on-error` does not cover. They are the only place the cross-built
  osx-arm64 binaries run on arm64. The `macos-15-intel` `bundle-smoke` leg stays gating and
  covers the macOS exec bits (§15 row 4); `dotnet-test` has no gating macOS leg, as the unit
  suite is platform-independent.
- Triggers: existing ones plus `workflow_dispatch` (used for sync PRs, §9.4).

### 9.3 `release.yaml`

tagpr keeps driving the release PR and tag. On the tag: build matrix → bundle → create the
GitHub Release with `gh release create` and attach `ccusage.mcpb`,
`ccusage-mcp-<rid>.{zip|tar.gz}` (server + engine side by side) for all six RIDs, and
`SHA256SUMS`. The npm job and the npm-registry gate are removed.

### 9.4 `sync-upstream.yaml`

- Triggers: weekly cron + `workflow_dispatch`.
- Fetch `ccusage/ccusage` `main`; exit if already contained in `origin/main`.
- Force-push upstream's tip to branch `sync/upstream`; open or update a PR into `main`
  (`chore(deps): sync upstream ccusage/ccusage`) listing incoming commits.
- Permissions: `contents: write`, `pull-requests: write`; never pushes to `main`.
- Uses `GITHUB_TOKEN`, so CI does not auto-run on the PR; run `ci.yaml` by hand with
  `workflow_dispatch` on the `sync/upstream` branch. Conflicts are resolved in the PR.

## 10. Testing

Framework: MSTest on Microsoft.Testing.Platform. Implementation follows TDD.

### 10.1 Unit

- `CommandCatalog`: sources, support matrix, and applicability derived correctly from the
  embedded JSON; **drift test** — every flag in `cli-help.json` is either typed in the C#
  table or listed as never-exposed; every source has a display name.
- `CommandValidator`: every valid and invalid `(source, report)` pair; per-option
  applicability; enum values; date formats; `token-limit` int/`max`; `last` ≥ 1.
- `CliArgsParser`: quoting, `--flag=value`, short aliases, optional-value flags, unknown
  tokens, never-exposed flags, `statusline` rejection, empty input → daily/all.
- argv building: table of typed requests → exact engine argv for every tool.
- `EngineLocator`: resolution order with temp directories; architecture selection; error
  lists checked paths.
- Schema snapshot: `tools/list` and `prompts/list` output vs a checked-in snapshot.

### 10.2 Runner (with `FakeEngine`)

`FakeEngine` echoes argv/stdin as JSON and can be told to exit with a code, write stderr,
or hang. Tests: success pass-through, stderr block, non-zero exit → MCP error, timeout,
cancellation kills the process, stdin delivery for `statusline`.

### 10.3 End-to-end (real engine)

- Fixtures in `dotnet/tests/fixtures/` (Claude and Codex fixture projects and statusline
  payloads copied from `apps/ccusage/test/`), pointed at with `CLAUDE_CONFIG_DIR`,
  `CODEX_HOME`, and `--offline`.
- Drive the published AOT server over stdio with the MCP SDK client.
- **Parity:** for each tool, the result text equals running `ccusage … --json --offline`
  directly with the same arguments.

### 10.4 Packaging

Bundle layout and `manifest.json` checks; `claude plugin validate --strict`; version
consistency; the `bundle-smoke` job (§9.2).

The Rust test suite runs unchanged.

## 11. Phase 0: verification spike (first plan task)

Undocumented behaviors the design depends on, each with a decision rule. The spike is
throwaway: a hand-built bundle containing a minimal AOT stdio server, installed as a local
plugin.

| #   | Question                                                                                                             | If the answer is "no"                                                                                                                         |
| --- | -------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Does Claude Code honor `platform_overrides` in a plugin-referenced `.mcpb`?                                          | Fall back to per-platform marketplace entries (four: windows, macos with universal binaries, linux-x64, linux-arm64). Requires user sign-off. |
| 2   | Does it substitute `${__dirname}` in `mcp_config`?                                                                   | Use whatever bundle-relative mechanism it supports; else fallback 1.                                                                          |
| 3   | Is the bundle fully downloaded and extracted before the server first starts?                                         | Document the one-time `/mcp` reconnect in the skill's error path.                                                                             |
| 4   | Are executable bits preserved on Linux/macOS extraction?                                                             | `launch.sh` restores `+x`; ensure `launch.sh` itself runs through `/bin/sh` (no `+x` needed).                                                 |
| 5   | Does `RuntimeInformation.OSArchitecture` report `Arm64` under x64 emulation on Windows ARM64?                        | Use `IsWow64Process2` via `[LibraryImport]` (§6.1).                                                                                           |
| 6   | Can the MCP C# SDK 2.3.0 register list/call handlers with source-generated JSON under Native AOT with zero warnings? | Investigate before continuing; no third-party alternative without approval.                                                                   |
| 7   | Which marketplace/manifest field placement for `mcpServers` passes `claude plugin validate --strict`?                | Adjust §7.1 to the passing form.                                                                                                              |
| 8   | Actual compressed bundle size.                                                                                       | Informational.                                                                                                                                |

Findings are written into this spec (as an addendum) before implementation proceeds past
Phase 0. Any fallback that changes user-visible behavior needs the user's approval.

## 12. Dependencies

Third-party (approved by the user on 2026-10-29):

- NuGet `ModelContextProtocol` 2.3.0 (brings `ModelContextProtocol.Core`), Apache-2.0.
- GitHub Actions (existing, SHA-pinned): `Songmu/tagpr`, `amannn/action-semantic-pull-request`,
  `nixbuild/nix-quick-install-action`, `nix-community/cache-nix-action`.
- Removed: `hustcer/setup-nu` (from `check-pr-title.yaml`), `useblacksmith/stickydisk`,
  `lycheeverse/lychee-action`.

First-party (no approval needed): .NET 10 SDK/runtime packs and ILCompiler,
`Microsoft.Extensions.Hosting`, `System.Text.Json`, MSTest + Microsoft.Testing.Platform,
GitHub `actions/*` (`checkout`, `setup-dotnet`, `upload-artifact`, `download-artifact`),
`gh` CLI, Claude Code CLI (native installer).

Any further third-party dependency requires the user's approval and a spec addendum first.

## 13. Execution model

- **Plan:** written by a Fable subagent using the superpowers `writing-plans` skill from this
  spec, then reviewed by the user.
- **Implementation:** superpowers `subagent-driven-development`, one Opus implementer
  subagent per task, TDD.
- **Review:** after each task — Opus for mechanical tasks (file moves, workflow edits,
  catalog tables, packaging), Fable for complex ones (catalog derivation, validator/parser,
  runner/cancellation, AOT/MCP handler wiring, release pipeline).
- Rough task order: Phase 0 spike → `dotnet/` skeleton + catalog → validator/parser →
  locator/runner → MCP handlers → e2e → plugin + bundle → CI/release → repo cleanup (npm
  removal, schema move, workflow deletions) → docs.

## 14. Risks

- **Large tool output.** Long ranges can produce JSON larger than client output limits
  (Claude Code truncates large MCP results). Tool descriptions recommend `since`/`last`;
  sizes are observed in e2e and revisited if needed.
- **Sync conflicts.** Upstream edits to deleted or rewritten files conflict on every sync;
  mitigated by §2 and resolved in the sync PR.
- **Unsigned macOS binaries.** Downloads made by Claude Code are not quarantined; direct
  archive downloads through a browser may be. The docs explain `xattr -d com.apple.quarantine`;
  signing and notarization are out of scope.
- **Undocumented `.mcpb` handling in Claude Code.** Covered by Phase 0 with fallbacks.

## 15. Phase 0 findings (2026-10-29)

Spike files live outside the repo in `phase0/` (sibling of the repo root): an AOT stdio
server (`phase0/spike`), a hand-built bundle laid out as in §7.3 (`phase0/bundle`,
`phase0/ccusage.mcpb`), a marketplace (`phase0/marketplace`), and a session-only plugin
(`phase0/plugin-local`) loaded with `claude -p --plugin-dir`. Nothing was installed into the
Claude Code configuration.

| #   | Question                       | Answer                            | Evidence                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       | Consequence                                                                                                                                 |
| --- | ------------------------------ | --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | platform_overrides honored     | yes                               | Headless session `claude -p "Call the ping tool from the ccusage MCP server once and print its exact result." --debug --debug-file phase0/claude-debug-file.log --plugin-dir phase0/plugin-local --allowedTools "mcp__plugin_ccusage_ccusage__ping"` (run from empty `phase0/scratch`; `plugin.json` has `"mcpServers": "./ccusage.mcpb"`). The process Claude Code started was `ccusage-mcp.exe`, not `/bin/sh` (`Win32_Process` capture: `name=ccusage-mcp.exe ExecutablePath=…\phase0\plugin-local\.mcpb-cache\6746a2438180a35d\win\ccusage-mcp.exe`). Debug log: `MCP server "plugin:ccusage:ccusage": Successfully connected (transport: stdio) in 173ms`, `Calling MCP tool: ping`, `Tool 'ping' completed successfully in 3ms`; stdout: `pong`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | none                                                                                                                                        |
| 2   | ${__dirname} substituted       | yes                               | Captured command line: `C:\Users\gman2\source\AiTesting\ccusage-csharp\phase0\plugin-local\.mcpb-cache\6746a2438180a35d/win/ccusage-mcp.exe`, i.e. `${__dirname}` = the extraction directory (separators mixed, `\` from the path and `/` from the manifest; Windows accepts it). Debug log: `Extracted 13 files to …\phase0\plugin-local\.mcpb-cache\6746a2438180a35d`. With `--plugin-dir` the bundle is extracted to `<plugin dir>/.mcpb-cache/<content hash>/`, and a later load logs `Using cached MCPB from …`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | none                                                                                                                                        |
| 3   | extracted before first start   | unverified (local bundle only)    | With a local `./ccusage.mcpb` the load is sequential: `MCPB [ccusage]: Extraction complete (13 files)` at 08:22:25.276Z, then `MCP server "plugin:ccusage:ccusage": Starting connection` at 08:22:25.531Z, and the server process appears at 08:22:25.645 (local time 04:22:25.645). The download timing of an `https://` bundle URL cannot be observed locally: unverified, checked on the first release.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | The skill's error path and `docs/guide/mcp.md` troubleshooting mention the one-time `/mcp` reconnect preemptively — REQUIRES HUMAN APPROVAL |
| 4   | exec bits preserved            | pending CI                        | Q4: not observable on Windows. The spike zip was built with Python `zipfile` on Windows and stored no exec bits: its `linux/**` and `osx/**` entries carry mode `0100666` (only the `.exe` entries are `0100777`). Task 10 must build the bundle with `zip -X -r` (Info-ZIP on Linux/macOS, after `chmod +x`), not Python `zipfile` or `Compress-Archive`. Info-ZIP records unix modes in the external attributes, and `-X` only drops the extra uid/gid/timestamp fields. `launch.sh` restores `+x`; Task 11b's `bundle-smoke` jobs on ubuntu-latest and macos-latest assert the exec bits after extraction (**pending CI**). Claude Code's own extractor remains unverified until the plugin is installed on Linux/macOS.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | launch.sh chmod stays                                                                                                                       |
| 5   | OSArchitecture under emulation | yes (runtime source) / pending CI | [`RuntimeInformation.Windows.cs` (release/10.0)](https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/RuntimeInformation.Windows.cs): `OSArchitecture` resolves `IsWow64Process2` from kernel32 and maps its _native machine_ output (`0xAA64` → `Arm64`), falling back to `GetNativeSystemInfo` only where `IsWow64Process2` is missing (pre-Windows 10). The native machine is the host's, so an x64 process on Windows ARM64 gets `Arm64`. Task 11b's `bundle-smoke` leg on `windows-11-arm` asserts the win-x64 server selects `win\arm64\ccusage.exe` (**pending CI**).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            | Task 5 step 7 needed: no (unless the CI leg fails)                                                                                          |
| 6   | SDK 2.3.0 handlers AOT-clean   | yes                               | `dotnet build phase0/spike -c Release`: `0 Warning(s)`; clean `dotnet publish phase0/spike -r win-x64 -c Release` (with `PublishAot`, `IsAotCompatible`, `TrimmerSingleWarn=false`, `IlcTreatWarningsAsErrors=true`): exit 0, `grep -c "warning IL" phase0/publish.log` → `0`; 11,576,832-byte `ccusage-mcp.exe`. Piped `initialize` returns `{"result":{"protocolVersion":"2025-06-18","capabilities":{"logging":{},"prompts":{},"tools":{}},"serverInfo":{"name":"ccusage","version":"0.0.0-spike"}},"id":1,"jsonrpc":"2.0"}`; `tools/list`, `tools/call` (`pong`, and `isError:true` for an unknown tool), `prompts/list` and `prompts/get` all answer from the AOT binary. API: `IMcpServerBuilder.WithListToolsHandler/WithCallToolHandler/WithListPromptsHandler/WithGetPromptHandler` (extension class `Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions`; the same delegates are the `McpServerOptions.Handlers.*Handler` properties of `McpServerHandlers`). Delegate: `McpRequestHandler<TParams,TResult>` = `ValueTask<TResult> (RequestContext<TParams> request, CancellationToken cancellationToken)`. `RequestContext<T>.Params` is `T?`; `CallToolRequestParams.Name` is `string` (non-null); `CallToolRequestParams.Arguments` is `IDictionary<string, JsonElement>?`. `Tool.InputSchema` is `JsonElement`. Options: `ModelContextProtocol.McpJsonUtilities.DefaultOptions` (`JsonSerializerOptions`). Custom types serialize through our own `[JsonSerializable]` context. | Task 7a/7b use these names                                                                                                                  |
| 7   | mcpServers placement           | entry (manifest also passes)      | `claude plugin validate --strict phase0/marketplace` (entry `"mcpServers": "file:///C:/Users/gman2/source/AiTesting/ccusage-csharp/phase0/ccusage.mcpb"`) first failed only on `description: No marketplace description provided` (strict treats the warning as an error); with a top-level `"description"` added: `✔ Validation passed`. `claude plugin validate --strict phase0/marketplace/plugin`: `✔ Validation passed`. `file://` is accepted; an `https://…/releases/download/v0.0.0/ccusage.mcpb` entry also passes; a non-string value fails (`plugins.0.mcpServers: Invalid input`), so the field is checked. `mcpServers` in `plugin.json` also passes (`file://`, or `./ccusage.mcpb` when the file exists; a missing relative file fails with `Path not found`). Only the `plugin.json` placement was exercised at runtime (`--plugin-dir`); the entry placement is validated, not loaded.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | §7.1 keeps the entry placement and adds a top-level marketplace `"description"`; Task 9                                                     |
| 8   | bundle size                    | 32.8 MiB (spike)                  | `phase0/ccusage.mcpb` = 34,373,029 bytes (Python `zipfile`, deflate). Per file (size / compressed): `win/ccusage-mcp.exe` 11,576,832 / 4,120,017; `win/x64/ccusage.exe` and `win/arm64/ccusage.exe` 4,464,640 / 2,295,008 each; `linux/{x64,arm64}` and `osx/{x64,arm64}` placeholders repeat those two sizes; `manifest.json` 557 / 299; `launch.sh` 557 / 293. 13 files in all (two Windows binaries, copied as placeholders). Count correction: §7.3 has 13 files (`manifest.json`, `launch.sh`, 11 binaries) and 11 distinct binaries (5 servers: win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64; 6 engines: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64). The plan's "15 files" and "eight distinct binaries" figures are wrong. Claude Code also reported `Extraction complete (13 files)`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | real size from CI bundle job                                                                                                                |

### Environment notes

- AOT publish locally: works. `dotnet publish` through the dotnet MCP tool links with VS 18
  Community `link.exe`. From the agent's Git Bash and PowerShell shells the link step failed
  because VS 18's `vcvarsall.bat` could not find `vswhere.exe` on `PATH` (ILC itself finished
  with no warnings); this is a shell-environment issue, not a project issue.
- Claude Code version used: 2.1.303.
- Claude Code negotiated MCP protocol `2026-07-28` with the SDK 2.3.0 server (it called
  `server/discover`) and logs everything the server writes to stderr as `[ERROR]`. The real
  server should log only warnings and above to stderr.
- Stdio smoke tests must hold stdin open. Piping a single request
  (`echo '{"jsonrpc":"2.0","id":1,"method":"initialize",...}' | ccusage-mcp.exe`) prints
  nothing on stdout: the `initialize` handler completes (stderr shows `method 'initialize'
request handler completed`), but stdin EOF shuts the stdio transport down before the reply
  is written. With stdin kept open (the request followed by about 2 s of open input) exactly
  one result line comes back. Smoke steps must keep stdin open until the expected replies are
  read, or use a real MCP client (`McpClient` + `StdioClientTransport`). Affected plan steps:
  Task 0 step 3, Task 7b step 13 (`printf … | ccusage-mcp.exe`, which expects exactly one
  stdout line), and Task 10's `bundle-smoke.ps1` contract, which Task 11b's `bundle-smoke`
  jobs run. That script must read each reply before it closes stdin.
- `--plugin-dir` writes `.mcpb-cache/` next to the plugin's `.mcpb` and creates an empty
  `~/.claude/plugins/data/<plugin>-inline` directory. Neither installs the plugin.

## 16. Planning decisions (2026-10-29)

Decisions taken while writing the implementation plan
(`docs/superpowers/plans/2026-10-29-csharp-aot-mcp-server.md`), approved by the user. They
refine the sections named; earlier sections are left as written.

1. **Option kind `number` (§4.1).** The engine parses `--session-length` as `f64`
   (`rust/crates/ccusage-cli/src/types.rs`), so the C# type table gains a `number` kind
   (JSON schema `{"type":"number","minimum":0}`) alongside `int`; `session-length` uses it.
2. **`CONTRIBUTING.md` (§3.2).** The contribution-gate and `lgtm`/`lgtmi` approval sections
   are removed with the gate automation; the rest of the file stays.
3. **Docs navigation (§3.4).** Besides sponsor content, `docs/.vitepress/config.ts` drops the
   `npm`, `Package Stats` and `DeepWiki` nav items, the `npm` social link and the `/npm`,
   `/deepwiki`, `/sponsor` redirects; the GitHub, Changelog and `editLink` targets point at
   `Desulation/ccusage`.
4. **`docs-build` CI job (§9.2).** `ci.yaml` gains a `docs-build` job (pnpm and Node from Nix)
   that runs `pnpm install --lockfile-only`, uploads a changed `pnpm-lock.yaml` as the
   `pnpm-lock` artifact and fails, then `pnpm install --frozen-lockfile` and `just docs::build`.
   The regenerated lockfile is committed from that artifact (the development machine has no
   pnpm).
5. **Strict parameters (§5.1, §5.7).** Typed tools reject any parameter outside their schema
   (`additionalProperties: false` plus a server-side check) with an error that lists the
   valid parameters for that `(source, report)` page.
6. **Phase 0 Q4/Q5 in CI (§11).** Where the development machine cannot observe them, Q4 and
   Q5 are verified by CI: the `bundle-smoke` job's `windows-11-arm` leg asserts that the
   win-x64 server selects `win\arm64\ccusage.exe` (`list-sources` → `engine-path`), and its
   Linux/macOS legs assert the executable bits after extracting `ccusage.mcpb`. §15 records
   these rows as "pending CI"; anything still untestable is recorded as "unverified".

Also confirmed without changing the spec text: root `package.json` `name` becomes `ccusage`
(with `homepage`) so `package.nix` can derive `pname`/`meta` from it; the `action-timeline`
CI job is deleted (`Kesin11/actions-timeline` is not approved); `nix/tools/changelogithub`,
`nix/darwin-x64-package.nix` and the macOS action's cross-build branch stay in place;
`.github/actions/commit-and-push-main`, `.github/scripts/generate-e2e-fixture.nu` and
`.github/scripts/upsert-pr-comment.nu` are deleted; Claude-only reports use the root argv
form (`ccusage blocks`, `ccusage statusline`); `plugin-validate` pins Claude Code 2.1.303.

7. **`list-sources` `engine-error` (§5.4).** `list-sources` adds an `engine-error` string field
   when the engine cannot be located or queried; it is omitted when null. This is additive to
   the §5.4 shape.
8. **osx-arm64 cross-built on Intel (§9.1, §9.2), 2026-10-29.** Across four CI runs the
   `build osx-arm64` job on `macos-latest` never acquired a runner, which blocked `bundle`,
   `e2e`, `bundle-smoke` and `ci gate`, and `release.yaml` reuses the same chain. The user
   chose to always cross-build osx-arm64 on `macos-15-intel` (cargo
   `--target aarch64-apple-darwin`, Native AOT `-r osx-arm64`) with a `lipo -archs` check of
   both binaries, and to keep the `macos-latest` legs only as non-blocking jobs outside
   `ci gate`. `.github/actions/build-macos-nix-native-package` is deleted with its matrix leg,
   superseding the note above that kept the macOS action's cross-build branch; `nix/` and the
   darwin flake outputs stay, and `nix flake check` still evaluates them. Consequences: the
   osx-arm64 engine is now a plain cargo build like osx-x64, so it embeds the LiteLLM pricing
   fetched at build time instead of the flake-pinned snapshot and lacks the Nix link flags (it
   may be slightly larger); and both macOS legs depend on `macos-15-intel`, the last Intel
   macOS image GitHub offers, so its retirement will force a revisit.
