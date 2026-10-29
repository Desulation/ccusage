# ccusage C# AOT MCP Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace this fork's npm distribution layer with a .NET 10 Native AOT MCP server (`ccusage-mcp`) that wraps the unchanged Rust engine, shipped as a Claude Code plugin with a single `.mcpb` bundle, with CI/release pipelines and docs to match.

**Architecture:** One C# project derives its tool/prompt surface from the engine's own `cli-commands.json` / `cli-help.json` (embedded by link), validates every request against that catalog, builds an argv, runs the engine as a child process and passes its `--json` stdout through untouched. Tools and prompts are registered through the MCP SDK's list/call handler hooks with source-generated JSON (no reflection). Packaging produces `ccusage.mcpb` (six RIDs) plus per-platform archives; GitHub Actions build, test (unit + FakeEngine + real-engine e2e), bundle, smoke-test and release.

**Tech Stack:** .NET SDK 10.0.402 (`global.json` rollForward `latestFeature`), C# Native AOT, `ModelContextProtocol` 2.3.0, `Microsoft.Extensions.Hosting` 10.0.14, `System.Text.Json` source generation, MSTest 4.5.2 on Microsoft.Testing.Platform, Rust engine (unchanged), Nix (CI only), GitHub Actions, Claude Code CLI 2.1.303, bash + PowerShell 7 packaging scripts.

**Spec:** `docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md` — read it fully before any task; `§n` below refers to it. §15 (Phase 0 findings) is written by Task 0; §16 (planning decisions) already records the decisions this plan applies.

## Progress

- [ ] Task 0: Phase 0 verification spike (HARD GATE)
- [ ] Task 1: `dotnet/` skeleton
- [ ] Task 2: `CommandCatalog`
- [ ] Task 3: `CommandRequest` / `CommandValidator` / argv
- [ ] Task 4: `CliArgsParser`
- [ ] Task 5: `EngineLocator`
- [ ] Task 6: `EngineRunner` + FakeEngine
- [ ] Task 7a: `ToolDefinitions` / `PromptDefinitions` / snapshots
- [ ] Task 7b: handlers, `Program` wiring, AOT publish
- [ ] Task 8: fixtures + e2e parity tests
- [ ] Task 9: plugin, skill, marketplace
- [ ] Task 10: bundle packaging scripts
- [ ] Task 11a: composite actions
- [ ] Task 11b: `build-artifacts.yaml` + `ci.yaml`
- [ ] Task 12: `release.yaml`
- [ ] Task 13: `sync-upstream.yaml`
- [ ] Task 14a: npm layer removal, schema move, workspace slimming
- [ ] Task 14b: workflow/script deletions, `check-pr-title`, `CONTRIBUTING.md`
- [ ] Task 15: `AGENTS.md` + skills
- [ ] Task 16: README + docs
- [ ] Task 17: final consistency pass + PR decision

## Global Constraints

- **Phase 0 is a hard gate.** Task 0 is a throwaway spike. Execution STOPS at Task 0 step 15 until the human has reviewed §15. Each Phase 0 fallback that changes user-visible behavior has its own decision step (Task 0 steps 16–18) and needs the human's explicit approval. Tasks depending on a Phase 0 answer carry a **Phase 0 dependency** line.
- **Decision steps.** A step marked **DECISION** states the question, the options, the recommendation and the consequence of each answer, and ends with "STOP until the human answers." Implementers never push, open a PR, add a dependency, or change user-visible behavior beyond the spec without the matching decision step being answered.
- **Third-party dependencies:** only §12's: NuGet `ModelContextProtocol` **2.3.0** exactly; GitHub Actions `Songmu/tagpr`, `amannn/action-semantic-pull-request`, `nixbuild/nix-quick-install-action`, `nix-community/cache-nix-action` at their existing SHAs. First-party is fine: Microsoft/.NET NuGet packages, GitHub `actions/*` and `github/*`, `gh`, Claude Code CLI. If a task needs anything else: STOP, raise it as a DECISION step (template in Task 0 step 4), wait. This plan removes `Kesin11/actions-timeline`, `useblacksmith/*`, `hustcer/setup-nu`, `lycheeverse/lychee-action`, `autofix-ci/action`, `pullfrog/pullfrog`, `oven-sh/setup-bun`, `pnpm/action-setup` (not approved).
- **Never modify `rust/`.** Upstream-owned file edits are limited to §3.1/§3.2 plus §16 items 7–8. New work lives in `dotnet/`, `plugin/`, `.claude-plugin/`, new workflow/action files, `docs/guide/mcp.md`.
- **Models:** every task states `Implementer: Opus` and `Reviewer: Opus|Fable` (§13).
- **TDD** for all C# logic; MSTest on Microsoft.Testing.Platform (`global.json` `"test": {"runner": "Microsoft.Testing.Platform"}`). Test code blocks show `[TestMethod]` names and assertions; setup helpers are named once per task and written by the implementer.
- **Tooling:** prefer the dotnet MCP tools — `mcp__dotnet__dotnet_sdk` (`ConfigureGlobalJson`), `mcp__dotnet__dotnet_solution` (`Create`, `Add`), `mcp__dotnet__dotnet_project` (`New`, `Build`, `Test`, `Publish`, `SetProperty`), `mcp__dotnet__dotnet_package` (`Add`, `Search`) — for creating, building, running, testing and package changes; shell `dotnet` only when no action covers it. Prefer `roslyn_*` tools for C# analysis when available. Load deferred tools with `ToolSearch` (`select:mcp__dotnet__dotnet_project,...`). Verification commands are written as shell so the expected output is unambiguous; run them through the equivalent MCP action when one exists.
- **Environment:** Windows 11, .NET SDK 10.0.402 (+9.0.319), `cargo` 1.97.1, Claude Code 2.1.303, `gh` 2.102, Python 3.12, Git Bash, PowerShell 7. **No Nix, Node or pnpm locally.** Every verification step is tagged **[local]** or **[CI only]**. Nix and Linux/macOS behavior are [CI only]. Windows AOT publish needs the VS "Desktop development with C++" workload (Task 0 step 1); without it AOT publish is [CI only] and local checks stop at the AOT/trim analyzers on `dotnet build`.
- **Commits:** small Conventional Commits, each ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Scopes: `mcp`, `plugin`, `bundle`, `ci`, `release`, `deps`, `docs`, `chore`, `spec`. **Never push** outside an answered DECISION step (Task 11b step 9, Task 14a step 12, Task 17 step 6).
- **Version** = root `package.json` `version` (`20.0.45`); every new file carrying a version uses that string.
- **Naming:** kebab-case parameters equal to the long flag without `--`. Tools `daily, weekly, monthly, session, blocks, statusline, list-sources, report`; prompt `ccusage`; executable `ccusage-mcp`; env `CCUSAGE_ENGINE`, `CCUSAGE_MCP_TIMEOUT_SECONDS` (default `120`).
- **Order:** tasks in the listed order; each leaves the repo building and tests green. `apps/ccusage` is deleted (Task 14a) only after fixtures were copied (Task 8) and CI stopped referencing it (Tasks 11–12).

## Review Focus

Inputs the spec implies but does not spell out; each has its test pinned to the owning task:

1. **Engine stdout that is empty or not JSON with exit 0** must pass through verbatim as content block 1 — Task 7b `CallAsync_PassesNonJsonStdoutUnchanged`.
2. **Multi-megabyte engine output** must not deadlock the runner — Task 6 `RunAsync_LargeStdout_DoesNotDeadlock` (8 MB).
3. **Values with spaces, quotes and non-ASCII** (`project`, `timezone`, `id`, paths) must reach the engine as single argv elements on Windows too — Task 6 `RunAsync_PassesSpacesQuotesAndUnicodeAsSingleArgv`.
4. **`hook-input` that is not a JSON object** must be a validation error naming `hook-input`, engine not started — Task 7b `CallAsync_Statusline_RejectsNonObjectHookInput`.
5. **Invalid `CCUSAGE_MCP_TIMEOUT_SECONDS`** (`abc`, `0`, `-5`) falls back to 120 s with one stderr warning — Task 6 `TimeoutSeconds_InvalidEnv_FallsBackTo120`.

Also pinned: unknown parameters on typed tools are rejected with the valid list (Task 3 `Validate_UnknownParameter_ListsValidOnes`); a `CCUSAGE_ENGINE` path that does not exist is skipped, not fatal, and listed (Task 5 `Resolve_EnvVarMissingFile_IsSkipped_AndListed`).

---

## File map

```
dotnet/global.json                                SDK 10.0.402, rollForward latestFeature, MTP runner
dotnet/Directory.Build.props                      net10.0, Nullable, TreatWarningsAsErrors, <Version>20.0.45</Version>
dotnet/Directory.Packages.props                   ModelContextProtocol 2.3.0, Microsoft.Extensions.Hosting 10.0.14, MSTest 4.5.2
dotnet/Ccusage.Mcp.slnx
dotnet/AGENTS.md
dotnet/packaging/{manifest.template.json,launch.sh,assemble-bundle.sh,verify-bundle.sh,bundle-smoke.ps1}
dotnet/src/Ccusage.Mcp/Ccusage.Mcp.csproj         AssemblyName ccusage-mcp, PublishAot, embeds the two JSON files by link
dotnet/src/Ccusage.Mcp/{Program.cs,ServerVersion.cs}
dotnet/src/Ccusage.Mcp/Commands/{OptionKind,OptionTypeTable,CommandCatalog,CommandRequest,ValidationException,CommandValidator,CliArgsParser}.cs
dotnet/src/Ccusage.Mcp/Engine/{EngineLocator,EngineNotFoundException,EngineInvocation,EngineResult,EngineRunner}.cs
dotnet/src/Ccusage.Mcp/Tools/{ToolDefinitions,ToolArguments,ToolHandlers}.cs
dotnet/src/Ccusage.Mcp/Prompts/{PromptDefinitions,PromptHandlers}.cs
dotnet/src/Ccusage.Mcp/Json/McpJsonContext.cs
dotnet/tests/Ccusage.Mcp.Tests/{Ccusage.Mcp.Tests.csproj,TestPaths.cs,VersionTests.cs}
dotnet/tests/Ccusage.Mcp.Tests/{Commands,Engine,Tools,Prompts,E2E,Snapshots}/...
dotnet/tests/FakeEngine/{FakeEngine.csproj,Program.cs}   AssemblyName ccusage -> apphost ccusage[.exe]
dotnet/tests/fixtures/{claude,codex,statusline}/   copied from apps/ccusage/test/
.claude-plugin/marketplace.json
plugin/.claude-plugin/plugin.json
plugin/skills/ccusage/SKILL.md
.agents/skills/dotnet/SKILL.md
.github/actions/publish-mcp-server/action.yaml
.github/workflows/build-artifacts.yaml             reusable: build matrix + bundle
.github/workflows/sync-upstream.yaml
docs/guide/mcp.md
```

Modified upstream files: §3.2 plus `CONTRIBUTING.md` (§16.7) and the `.agents/skills/*` files that name deleted paths (Task 15). Deleted: §3.1 plus §16.5–6 (Task 14).

---

### Task 0: Phase 0 verification spike (HARD GATE)

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Answer §11's eight questions with a throwaway bundle; record findings as spec §15; obtain the human's go-ahead.

**Files:**
- Create (throwaway, outside the repo): `C:\Users\gman2\source\AiTesting\ccusage-csharp\phase0\` — nothing under the repo root.
- Modify: `docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md` (append `## 15. Phase 0 findings (2026-10-29)`).

**Interfaces:** Produces the §15 table consumed by Task 5 (Q5), Task 7a/7b (Q6 API names), Task 9 (Q7, Q3), Task 10/11b (Q1, Q2, Q4, Q8).

- [ ] **Step 1: Check AOT prerequisites** [local]

`mcp__dotnet__dotnet_project` `New`, template `console`, name `Hello`, output `phase0/hello`, additionalOptions `--aot`; then `dotnet publish phase0/hello -r win-x64 -c Release`.
Expected: `phase0/hello/bin/Release/net10.0/win-x64/publish/Hello.exe` exists. A `link.exe`/platform-linker error means the C++ workload is missing: note "AOT publish is CI-only on this machine" for §15 and continue.

- [ ] **Step 2: Create the spike server project** [local]

`phase0/spike/Spike.csproj`: console, `AssemblyName ccusage-mcp`, `PublishAot true`, `InvariantGlobalization true`, `IsAotCompatible true`, `TrimmerSingleWarn false`, `IlcTreatWarningsAsErrors true`; packages `ModelContextProtocol` 2.3.0, `Microsoft.Extensions.Hosting` 10.0.14. `Program.cs`: `--version` prints `ccusage-mcp spike` and exits; otherwise `Host.CreateApplicationBuilder`, `AddMcpServer(...)`, `WithStdioServerTransport()`, and **handler hooks only** (no attributed tools): list-tools returning one `Tool { Name = "ping", InputSchema = JsonDocument.Parse("""{"type":"object","properties":{},"additionalProperties":false}""").RootElement }`, call-tool returning `CallToolResult` with one `TextContentBlock("pong")`, list-prompts/get-prompt for one prompt `ping` with one user message. Any custom type goes through a `[JsonSerializable]` `JsonSerializerContext`.
Expected: `dotnet build phase0/spike -c Release` → 0 warnings. Write down, for §15 Q6: the registration API used (`McpServerOptions.Handlers.*` or `IMcpServerBuilder.WithListToolsHandler/...`), the delegate signatures, how `RequestContext<CallToolRequestParams>` exposes `Params.Name` and `Params.Arguments` (`IReadOnlyDictionary<string, JsonElement>?` or other), and the `McpJsonUtilities.DefaultOptions` name.

- [ ] **Step 3: Publish the spike as AOT (Q6)** [local, or CI-only per step 1]

`dotnet publish phase0/spike -r win-x64 -c Release 2>&1 | tee phase0/publish.log`.
Expected: exit 0 and `grep -c "warning IL" phase0/publish.log` prints `0`. Q6 = yes. Then `echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"0"}}}' | phase0/spike/bin/Release/net10.0/win-x64/publish/ccusage-mcp.exe` prints one JSON-RPC result on stdout.

- [ ] **Step 4: DECISION (only if step 3 reported IL warnings or failed)**

Question: the MCP C# SDK 2.3.0 handler path is not AOT-clean with zero warnings. Options: (A) investigate and work around inside first-party code (e.g. supply our own `JsonSerializerOptions` with the SDK's context chained, suppress nothing); (B) accept specific `IL` warnings as suppressions with justification; (C) a different MCP library — **not allowed without approval**. Recommendation: A, then B only for warnings traceable to SDK code we cannot avoid. Each answer leads to: A → repeat step 3; B → list the suppressed IDs in §15 and Task 7b; C → spec §12 addendum first. **STOP until the human answers.** (This step is also the template for any later third-party dependency: question, options, recommendation, consequences, STOP.)

- [ ] **Step 5: Build the Rust engine for the bundle** [local]

`cargo build --manifest-path rust/Cargo.toml --release --bin ccusage --features fetch-litellm-pricing`.
Expected: `rust/target/release/ccusage.exe` exists; `rust/target/release/ccusage.exe --version` prints `ccusage 20.0.45`.

- [ ] **Step 6: Lay out the spike bundle directory** [local]

`phase0/bundle/` exactly as §7.3: `manifest.json` = §7.3 JSON with `"version": "0.0.0-spike"`; `launch.sh` (content as Task 10 step 2); `win/ccusage-mcp.exe` = step 3 output; `win/x64/ccusage.exe` and `win/arm64/ccusage.exe` = copies of step 5's binary; `linux/{x64,arm64}/{ccusage-mcp,ccusage}` and `osx/{x64,arm64}/{ccusage-mcp,ccusage}` = copies of the same two files renamed (placeholders).
Expected: `find phase0/bundle -type f | wc -l` prints `13`.

- [ ] **Step 7: Zip and measure (Q8)** [local]

`cd phase0/bundle && python -m zipfile -c ../ccusage.mcpb manifest.json launch.sh win linux osx` (or `Compress-Archive` + rename).
Expected: `phase0/ccusage.mcpb` exists; record its size and the per-file sizes (`unzip -l`) in §15 as Q8 — note that the real figure (11 distinct binaries: 5 servers + 6 engines) comes from the CI `bundle` job's step summary (Task 11b).

- [ ] **Step 8: Create the local marketplace** [local]

`phase0/marketplace/.claude-plugin/marketplace.json` = §7.1 JSON with `mcpServers` pointing at `file:///C:/Users/gman2/source/AiTesting/ccusage-csharp/phase0/ccusage.mcpb` (variant A) and `phase0/marketplace/plugin/.claude-plugin/plugin.json` = `{"name":"ccusage","version":"0.0.0-spike","description":"spike","author":{"name":"Desulation"},"license":"MIT"}`.
Expected: both files parse with `python -m json.tool`.

- [ ] **Step 9: Run `claude plugin validate` variants (Q7)** [local]

Run `claude plugin validate --strict phase0/marketplace` and `claude plugin validate --strict phase0/marketplace/plugin`. If the marketplace-entry `mcpServers` is rejected, move it into `plugin.json` and re-run.
Expected: one placement passes both commands with no warnings; record the passing placement, the exact commands, and the file-path form (`file://` accepted or not) in §15 Q7. If a `file://` source is unsupported, record it and ask the human (in step 15) to upload `phase0/ccusage.mcpb` to a draft pre-release before step 10 can run with an `https://` URL.

- [ ] **Step 10: Install and probe in a scratch Claude Code session (Q1, Q2, Q3)** [local, Windows]

**Never modify the human's Claude Code configuration:** do not run `/plugin marketplace add`, `/plugin install`, `claude plugin marketplace add` or `claude plugin install` against the real config. Load the spike for one session only with `--plugin-dir`, which installs nothing.

Copy `phase0/ccusage.mcpb` into a session-only plugin folder `phase0/plugin-local/` whose `.claude-plugin/plugin.json` is the step 8 manifest plus `"mcpServers": "./ccusage.mcpb"`. From an empty scratch directory run (headless, one session, debug log to a file):

```powershell
claude -p --debug --plugin-dir C:\Users\gman2\source\AiTesting\ccusage-csharp\phase0\plugin-local --allowedTools "mcp__plugin_ccusage_ccusage__ping" "Call the ping tool from the ccusage MCP server once and print its exact result." 2> C:\Users\gman2\source\AiTesting\ccusage-csharp\phase0\claude-debug.log
```

While it runs, capture the server process: `Get-CimInstance Win32_Process -Filter "Name='ccusage-mcp.exe'" | Select CommandLine`.
Expected observations recorded in §15: **Q1** = `ccusage-mcp.exe` ran (so `platform_overrides.win32` was honored rather than `/bin/sh launch.sh`) and the ping result came back; **Q2** = its command line shows the extracted bundle path in place of `${__dirname}`; quote the relevant `claude-debug.log` lines. **Q3** (download timing for an `https://` bundle URL) cannot be observed with a local bundle: record it as "unverified locally — checked on the first release", with the step 18 fallback (document the one-time `/mcp` reconnect) applied preemptively in the skill's error path.

- [ ] **Step 11: Record Q4 as pending CI**

Write in §15: "Q4: not observable on Windows. The zip stores unix modes (`zip -X`) and `launch.sh` restores `+x`; Task 11b's `bundle-smoke` jobs on ubuntu-latest and macos-latest assert the exec bits after extraction (**pending CI**). Claude Code's own extractor remains unverified until the plugin is installed on Linux/macOS."
Expected: the sentence is present in the §15 table row 4.

- [ ] **Step 12: Record Q5 (runtime source + pending CI)**

Read `RuntimeInformation.Windows.cs` in dotnet/runtime for the .NET 10 branch (`src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/RuntimeInformation.Windows.cs`; it uses `Interop.Kernel32.GetNativeSystemInfo`/`IsWow64Process2`) and record the link and whether `OSArchitecture` reports the host OS architecture for an x64 process on Windows ARM64. Add: "Task 11b's `bundle-smoke` leg on `windows-11-arm` asserts the win-x64 server selects `win\arm64\ccusage.exe` (**pending CI**)."
Expected: row 5 names the source file, the conclusion, and the pending CI check; Task 5 step 7 is marked needed or not needed.

- [ ] **Step 13: Write the §15 addendum**

Append to the spec:

```markdown
## 15. Phase 0 findings (2026-10-29)

| # | Question | Answer | Evidence | Consequence |
|---|---|---|---|---|
| 1 | platform_overrides honored | yes / no | ... | none / fallback — REQUIRES HUMAN APPROVAL |
| 2 | ${__dirname} substituted | yes / no | ... | none / fallback — REQUIRES HUMAN APPROVAL |
| 3 | extracted before first start | yes / no | ... | none / skill + docs mention `/mcp` reconnect — REQUIRES HUMAN APPROVAL |
| 4 | exec bits preserved | pending CI | ... | launch.sh chmod stays |
| 5 | OSArchitecture under emulation | yes / no / pending CI | runtime source link | Task 5 step 7 needed: yes/no |
| 6 | SDK 2.3.0 handlers AOT-clean | yes / no | API: ... | Task 7a/7b API names |
| 7 | mcpServers placement | entry / manifest | validate output | §7.1 form; Task 9 |
| 8 | bundle size | NN MB (spike) | file list | real size from CI bundle job |

### Environment notes
- AOT publish locally: works / CI-only (reason).
- Claude Code version used: 2.1.303.
```

Expected: every row filled; every fallback marked **REQUIRES HUMAN APPROVAL**.

- [ ] **Step 14: Commit**

```bash
git add docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md
git commit -m "docs(spec): add Phase 0 verification findings" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 15: DECISION — Phase 0 gate**

Question: "§15 is complete; may implementation proceed to Task 1?" Options: (A) proceed as planned; (B) proceed after the fallbacks in steps 16–18 are decided; (C) revise the spec first. Recommendation: A if rows 1–3 and 6–7 are "yes"/resolved, else B. A → Task 1; B → answer steps 16–18 then Task 1; C → the human edits the spec and re-plans. **STOP until the human answers.**

- [ ] **Step 16: DECISION — Q1 fallback (only if row 1 is "no")**

Question: Claude Code ignored `platform_overrides`; adopt §11's fallback of four per-platform marketplace entries (windows, macos universal, linux-x64, linux-arm64)? Options: (A) yes — Task 9 writes four plugin entries and Task 10 builds four bundles; (B) keep one bundle and make `launch.sh` unnecessary by shipping `win/ccusage-mcp.exe` as the manifest's primary `command` with a POSIX fallback investigated separately; (C) stop. Recommendation: A (documented in §11). A → §7.1/§7.3 addendum rows + Tasks 9/10/11b adjusted; B → spike again; C → re-plan. **STOP until the human answers.**

- [ ] **Step 17: DECISION — Q2 fallback (only if row 2 is "no")**

Question: `${__dirname}` was not substituted; which bundle-relative mechanism did the spike find (record it), or fall back to step 16's option A? Options: (A) use the mechanism found; (B) per-platform entries. Recommendation: A if found. A → Task 10 manifest template uses it; B → as step 16 A. **STOP until the human answers.**

- [ ] **Step 18: DECISION — Q3 (only if row 3 is "no")**

Question: the server's first connection attempt happened before extraction finished; document a one-time `/mcp` reconnect in the skill's error path and `docs/guide/mcp.md` troubleshooting? Options: (A) yes; (B) no, leave undocumented. Recommendation: A. A → Task 9 step 3 and Task 16 step 7 include the sentence; B → nothing. **STOP until the human answers.**

---

### Task 1: `dotnet/` skeleton (solution, props, server stub, test project, FakeEngine)

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** A building `dotnet/` tree with two passing tests, `ccusage-mcp --version`, and the AOT/trim analyzers on.

**Files:**
- Create: `dotnet/global.json`, `dotnet/Directory.Build.props`, `dotnet/Directory.Packages.props`, `dotnet/Ccusage.Mcp.slnx`, `dotnet/src/Ccusage.Mcp/Ccusage.Mcp.csproj`, `dotnet/src/Ccusage.Mcp/Program.cs`, `dotnet/src/Ccusage.Mcp/ServerVersion.cs`, `dotnet/tests/Ccusage.Mcp.Tests/Ccusage.Mcp.Tests.csproj`, `dotnet/tests/Ccusage.Mcp.Tests/TestPaths.cs`, `dotnet/tests/Ccusage.Mcp.Tests/VersionTests.cs`, `dotnet/tests/FakeEngine/FakeEngine.csproj`, `dotnet/tests/FakeEngine/Program.cs`, `dotnet/AGENTS.md`
- Modify: `.gitignore`

**Interfaces (produces):**
- `public static class ServerVersion { public static string Value { get; } }` in `dotnet/src/Ccusage.Mcp/ServerVersion.cs` — `AssemblyInformationalVersionAttribute` of the entry assembly, cut at the first `+`.
- `internal static class TestPaths { public static string FakeEnginePath { get; } }` in `dotnet/tests/Ccusage.Mcp.Tests/TestPaths.cs` — from `AssemblyMetadataAttribute` key `FakeEnginePath`, `Path.GetFullPath`.
- FakeEngine protocol: stdout `{"argv":[...],"stdin":"...","noColor":"<NO_COLOR or null>"}`, exit 0. Control args (each also echoed): `--fake-exit=<n>` exit code; `--fake-stderr=<text>` written to stderr; `--fake-stdout=<text>` replaces the echo; `--fake-stdout-bytes=<n>` n bytes of `x` replace the echo; `--fake-hang` sleep forever after writing stdout; `--version` prints `ccusage 0.0.0-fake`. Stdin is read to end only when `Console.IsInputRedirected` and an arg equals `statusline`.

- [ ] **Step 1: Write `dotnet/global.json`**

`mcp__dotnet__dotnet_sdk` `ConfigureGlobalJson` with `globalJsonPath: dotnet/global.json`, `sdkVersion: 10.0.402`, `rollForward: latestFeature`, `testRunner: Microsoft.Testing.Platform`.
Expected content: `{"sdk":{"version":"10.0.402","rollForward":"latestFeature"},"test":{"runner":"Microsoft.Testing.Platform"}}` (whitespace may differ).

- [ ] **Step 2: Write `dotnet/Directory.Build.props`**

Properties: `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `LangVersion latest`, `TreatWarningsAsErrors true`, `EnforceCodeStyleInBuild true`, `AnalysisLevel latest-recommended`, `Version 20.0.45`, `InvariantGlobalization true`, `ManagePackageVersionsCentrally true`.
Expected: `grep -c "<Version>20.0.45</Version>" dotnet/Directory.Build.props` prints `1`.

- [ ] **Step 3: Write `dotnet/Directory.Packages.props`**

`PackageVersion` items: `ModelContextProtocol` `2.3.0`, `Microsoft.Extensions.Hosting` `10.0.14`, `MSTest` `4.5.2`. Confirm the last two with `mcp__dotnet__dotnet_package` `Search` (`exactMatch: true`); a newer patch may be used and noted in the commit body. `ModelContextProtocol` stays `2.3.0`.
Expected: three `PackageVersion` elements.

- [ ] **Step 4: Create the solution**

`mcp__dotnet__dotnet_solution` `Create`, name `Ccusage.Mcp`, format `slnx`, output `dotnet`.
Expected: `dotnet/Ccusage.Mcp.slnx` exists.

- [ ] **Step 5: Create the server project**

`mcp__dotnet__dotnet_project` `New`, template `console`, name `Ccusage.Mcp`, output `dotnet/src/Ccusage.Mcp`, additionalOptions `--use-program-main`. Csproj properties: `AssemblyName ccusage-mcp`, `RootNamespace Ccusage.Mcp`, `PublishAot true`, `IsAotCompatible true`, `TrimmerSingleWarn false`, `IlcTreatWarningsAsErrors true`, `StripSymbols true`, `UseSystemResourceKeys true`, `SatelliteResourceLanguages en`. `PackageReference`s `ModelContextProtocol`, `Microsoft.Extensions.Hosting` (no `Version` attribute). `mcp__dotnet__dotnet_solution` `Add`.
Expected: `dotnet build dotnet/Ccusage.Mcp.slnx` succeeds.

- [ ] **Step 6: Create the test project**

`mcp__dotnet__dotnet_project` `New`, template `mstest`, name `Ccusage.Mcp.Tests`, output `dotnet/tests/Ccusage.Mcp.Tests`. Csproj: `EnableMSTestRunner true`, `TestingPlatformDotnetTestSupport true`, `OutputType Exe`, `IsPackable false`, `PackageReference MSTest`, `ProjectReference ../../src/Ccusage.Mcp/Ccusage.Mcp.csproj`, `ProjectReference ../FakeEngine/FakeEngine.csproj` with `ReferenceOutputAssembly="false"`, and:

```xml
<PropertyGroup>
  <FakeEngineExe Condition="$([MSBuild]::IsOSPlatform('Windows'))">.exe</FakeEngineExe>
</PropertyGroup>
<ItemGroup>
  <AssemblyMetadata Include="FakeEnginePath" Value="$(MSBuildThisFileDirectory)../FakeEngine/bin/$(Configuration)/net10.0/ccusage$(FakeEngineExe)" />
</ItemGroup>
```

Delete the template's `Test1.cs`. Add the project to the solution.
Expected: `mcp__dotnet__dotnet_solution` `List` shows both projects.

- [ ] **Step 7: Create the FakeEngine project**

`New` template `console`, name `FakeEngine`, output `dotnet/tests/FakeEngine`, `--use-program-main`; csproj `AssemblyName ccusage`, `UseAppHost true`. Add to the solution.
Expected: `dotnet build dotnet/Ccusage.Mcp.slnx` produces `dotnet/tests/FakeEngine/bin/Debug/net10.0/ccusage.exe`.

- [ ] **Step 8: Write the failing tests** in `dotnet/tests/Ccusage.Mcp.Tests/VersionTests.cs`

```csharp
[TestClass]
public sealed class VersionTests
{
    // RepoRoot(): walk up from AppContext.BaseDirectory to the first directory containing package.json and rust/
    // RunProcess(path, args): start the process, return (exitCode, stdout, stderr)
    [TestMethod]
    public void ServerVersion_MatchesRootPackageJson()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "package.json")));
        Assert.AreEqual(doc.RootElement.GetProperty("version").GetString(), ServerVersion.Value);
    }

    [TestMethod]
    public async Task FakeEngine_EchoesArgv()
    {
        var (exit, stdout, _) = await RunProcess(TestPaths.FakeEnginePath, ["daily", "--json"]);
        using var doc = JsonDocument.Parse(stdout);
        Assert.AreEqual(0, exit);
        CollectionAssert.AreEqual(new[] { "daily", "--json" }, doc.RootElement.GetProperty("argv").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.AreEqual("", doc.RootElement.GetProperty("stdin").GetString());
    }
}
```

- [ ] **Step 9: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`.
Expected: build error CS0103 `The name 'ServerVersion' does not exist`; after stubbing `ServerVersion`, `FakeEngine_EchoesArgv` fails with a `JsonException` on the template's `Hello, World!`.

- [ ] **Step 10: Implement `ServerVersion`** in `dotnet/src/Ccusage.Mcp/ServerVersion.cs` — `public static class ServerVersion { public static string Value { get; } }` per Interfaces.

- [ ] **Step 11: Implement `Program.Main`** in `dotnet/src/Ccusage.Mcp/Program.cs`

`static int Main(string[] args)`: `--version` present → `Console.Out.WriteLine($"ccusage-mcp {ServerVersion.Value}")`, return 0; else `Console.Error.WriteLine("ccusage-mcp: MCP server not wired yet")`, return 2 (replaced in Task 7b).

- [ ] **Step 12: Implement FakeEngine** in `dotnet/tests/FakeEngine/Program.cs`

`static int Main(string[] args)` per the protocol; echo via `[JsonSerializable(typeof(FakeEcho))] partial class FakeEngineJsonContext : JsonSerializerContext`, `record FakeEcho(string[] Argv, string Stdin, string? NoColor)` with `[JsonPropertyName("argv")]`, `("stdin")`, `("noColor")`.

- [ ] **Step 13: Run the tests to verify they pass**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0, Passed: 2`.

- [ ] **Step 14: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 15: Verify `--version`** [local]

Run: `dotnet run --project dotnet/src/Ccusage.Mcp -- --version`. Expected: `ccusage-mcp 20.0.45`.

- [ ] **Step 16: Write `dotnet/AGENTS.md`**

Six sections: layout (one line per file-map directory); commands (`dotnet build dotnet/Ccusage.Mcp.slnx`, `dotnet test dotnet/Ccusage.Mcp.slnx`, `dotnet publish dotnet/src/Ccusage.Mcp -c Release -r <rid>`); "catalog data comes from `rust/crates/ccusage-cli-parser/src/*.json` by link; never copy it"; the FakeEngine protocol; e2e env vars `CCUSAGE_MCP_E2E_SERVER` / `CCUSAGE_ENGINE` (Task 8); snapshot update `UPDATE_SNAPSHOTS=1` (Task 7a); AOT rules (no reflection serialization; `[JsonSerializable]` only; zero IL warnings).
Expected: the file has those sections.

- [ ] **Step 17: Append to `.gitignore`**

Lines `dotnet/**/bin/`, `dotnet/**/obj/`, `dotnet/dist/`, `*.mcpb`.
Expected: `git status --short` lists no `bin/` or `obj/` path.

- [ ] **Step 18: Commit**

```bash
git add dotnet .gitignore
git commit -m "feat(mcp): scaffold dotnet solution, server stub, tests and FakeEngine" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `CommandCatalog` from the embedded upstream JSON

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Derive sources, pages, option applicability and option metadata from `cli-commands.json` + `cli-help.json` with the hand-written type table and drift tests (§4.1, §10.1).

**Files:**
- Modify: `dotnet/src/Ccusage.Mcp/Ccusage.Mcp.csproj`
- Create: `dotnet/src/Ccusage.Mcp/Commands/OptionKind.cs`, `Commands/OptionTypeTable.cs`, `Commands/CommandCatalog.cs`, `dotnet/src/Ccusage.Mcp/Json/McpJsonContext.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Commands/CommandCatalogTests.cs`, `Commands/OptionTypeTableDriftTests.cs`

**Interfaces (produces):**

```csharp
public enum OptionKind { Bool, NegatableBool, String, Date, Int, Number, IntOrMax, Enum, CsvEnum, Path, PathList }
public sealed record OptionSpec(string Name, string? ShortAlias, bool TakesValue, bool ValueOptional,
    string Description, string? Default, OptionKind Kind, IReadOnlyList<string> EnumValues);
public sealed record PageSpec(string Source, string Report, string Description, IReadOnlyList<OptionSpec> Options);
public sealed record SourceSpec(string Id, string DisplayName, IReadOnlyList<string> Reports);
public sealed class CommandCatalog
{
    public static CommandCatalog Instance { get; }            // Load() cached
    public static CommandCatalog Load();                      // embedded cli-commands.json + cli-help.json
    public IReadOnlyList<SourceSpec> Sources { get; }         // "all" first, then root-command order
    public IReadOnlyList<string> Reports { get; }             // daily, weekly, monthly, session, blocks, statusline
    public bool TryGetPage(string source, string report, [NotNullWhen(true)] out PageSpec? page);
    public IReadOnlyList<string> SourcesWithReport(string report);
    public IReadOnlyList<string> SourcesAccepting(string report, string optionName);
    public IReadOnlyList<OptionSpec> UnionOptions(string report);   // over all sources having the report; catalog order; dedupe by Name
    public static IReadOnlySet<string> NeverExposed { get; }        // json, compact, color, no-color, help, version, all
}
public static class OptionTypeTable
{
    public static IReadOnlyDictionary<string, (OptionKind Kind, string[] EnumValues)> Entries { get; }
    public static IReadOnlyDictionary<string, string> DisplayNames { get; }
}
```

Derivation rules: a page is every `pages[]` entry with an `options` key; `path` of length 1 → source `all`, except `blocks`/`statusline` → source `claude`; length 2 → `(source, report)`; duplicates (`claude/blocks` vs root `blocks`) keep the first seen. Sources = `pages[]` entries with a `commands` key in file order, `all` synthesized first (reports `daily, weekly, monthly, session`). Page options: `options` is either a `combinedOptions` key (list of group names) or a group name in `cli-help.json`; concatenate the groups' entries; drop `NeverExposed` names and `--no-<x>` entries; dedupe by name keeping the first. Flags: `"-s, --since <since>"` → alias `s`, name `since`, TakesValue, not optional; `"--speed [speed]"` → optional; `"--by-agent"` → no value. Kind from `Entries[name]`; EnumValues from JSON `choices` when present, else from the table.

`Entries` (exact): `since Date; until Date; timezone String; offline NegatableBool; cache NegatableBool; config Path; no-cost Bool; sections CsvEnum [daily,weekly,monthly,session]; by-agent Bool; last Int; id String; start-of-week Enum; mode Enum; order Enum; breakdown Bool; debug Bool; debug-samples Int; single-thread Bool; instances Bool; project String; project-aliases String; speed Enum; pi-path PathList; open-claw-path PathList; active Bool; recent Bool; token-limit IntOrMax; session-length Number; visual-burn-rate Enum; cost-source Enum; refresh-interval Int; context-low-threshold Int; context-medium-threshold Int` (`Number` for `session-length` per §16.2).

`DisplayNames`: `all` All sources; `claude` Claude Code; `codex` Codex; `opencode` OpenCode; `amp` Amp; `droid` Droid; `codebuff` Codebuff; `hermes` Hermes; `pi` pi-agent; `goose` Goose; `kilo` Kilo; `copilot` GitHub Copilot CLI; `gemini` Gemini CLI; `antigravity` Antigravity; `kimi` Kimi; `qwen` Qwen; `openclaw` OpenClaw; `grok` Grok Build CLI; `zcode` ZCode. Unknown id → the id itself.

- [ ] **Step 1: Embed the JSON by link**

Add to `Ccusage.Mcp.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="../../../rust/crates/ccusage-cli-parser/src/cli-commands.json" LogicalName="cli-commands.json" />
  <EmbeddedResource Include="../../../rust/crates/ccusage-cli-parser/src/cli-help.json" LogicalName="cli-help.json" />
</ItemGroup>
```

Expected: `dotnet build` succeeds; no copy of the JSON exists under `dotnet/`.

- [ ] **Step 2: Write the failing catalog tests** in `Commands/CommandCatalogTests.cs`

```csharp
[TestClass]
public sealed class CommandCatalogTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;
    private static string[] Names(string s, string r) { Assert.IsTrue(C.TryGetPage(s, r, out var p)); return p!.Options.Select(o => o.Name).ToArray(); }

    [TestMethod] public void Sources_AreAllPlusEighteenAgentsInRootOrder() =>
        CollectionAssert.AreEqual(new[] { "all","claude","codex","opencode","amp","droid","codebuff","hermes","pi","goose","kilo","copilot","gemini","antigravity","kimi","qwen","openclaw","grok","zcode" }, C.Sources.Select(s => s.Id).ToArray());

    [DataTestMethod]
    [DataRow("all", "daily,monthly,weekly,session")]
    [DataRow("claude", "daily,monthly,weekly,session,blocks,statusline")]
    [DataRow("codex", "daily,monthly,session")]
    [DataRow("opencode", "daily,weekly,monthly,session")]
    public void Reports_PerSource(string id, string csv) =>
        CollectionAssert.AreEquivalent(csv.Split(','), C.Sources.Single(s => s.Id == id).Reports.ToArray());

    [TestMethod] public void RootBlocksAndStatusline_BelongToClaude()
    { Assert.IsTrue(C.TryGetPage("claude", "blocks", out _)); Assert.IsFalse(C.TryGetPage("all", "blocks", out _)); Assert.IsTrue(C.TryGetPage("claude", "statusline", out _)); }

    [TestMethod] public void SourcesWithReport_Weekly() =>
        CollectionAssert.AreEqual(new[] { "all", "claude", "opencode" }, C.SourcesWithReport("weekly").ToArray());

    [DataTestMethod]
    [DataRow("claude", "daily", "since,until,mode,debug,debug-samples,order,breakdown,offline,single-thread,timezone,config,instances,project,project-aliases,last")]
    [DataRow("all", "daily", "since,until,timezone,sections,by-agent,offline,no-cost,config,last")]
    [DataRow("claude", "statusline", "offline,visual-burn-rate,cost-source,cache,refresh-interval,context-low-threshold,context-medium-threshold,timezone,config,debug")]
    [DataRow("claude", "blocks", "since,until,mode,debug,debug-samples,order,breakdown,offline,single-thread,timezone,config,active,recent,token-limit,session-length")]
    public void Page_Options_InCatalogOrder(string s, string r, string csv) => CollectionAssert.AreEqual(csv.Split(','), Names(s, r));

    [TestMethod] public void Options_NeverContainNeverExposedOrNegations()
    { foreach (var src in C.Sources) foreach (var rep in src.Reports) foreach (var n in Names(src.Id, rep))
      { Assert.IsFalse(CommandCatalog.NeverExposed.Contains(n), n); Assert.IsFalse(n.StartsWith("no-") && n != "no-cost", n); } }

    [TestMethod] public void Flags_Parsed()
    {
        C.TryGetPage("claude", "daily", out var cd); var since = cd!.Options.Single(o => o.Name == "since");
        Assert.AreEqual("s", since.ShortAlias); Assert.IsTrue(since.TakesValue); Assert.IsFalse(since.ValueOptional); Assert.AreEqual(OptionKind.Date, since.Kind);
        Assert.IsFalse(cd.Options.Single(o => o.Name == "instances").TakesValue);
        C.TryGetPage("codex", "daily", out var cx); var speed = cx!.Options.Single(o => o.Name == "speed");
        Assert.IsTrue(speed.ValueOptional); Assert.AreEqual("auto", speed.Default);
        CollectionAssert.AreEqual(new[] { "auto", "standard", "flex", "fast" }, speed.EnumValues.ToArray());
        C.TryGetPage("all", "weekly", out var aw); Assert.AreEqual("monday", aw!.Options.Single(o => o.Name == "start-of-week").Default);
        C.TryGetPage("claude", "weekly", out var cw); Assert.AreEqual("sunday", cw!.Options.Single(o => o.Name == "start-of-week").Default);
    }

    [DataTestMethod]
    [DataRow("daily", "speed", "codex")]
    [DataRow("session", "id", "all,claude,codex")]
    [DataRow("daily", "since", "all,claude,codex,opencode,amp,droid,codebuff,hermes,pi,goose,kilo,copilot,gemini,antigravity,kimi,qwen,openclaw,grok,zcode")]
    public void SourcesAccepting(string report, string option, string csv) =>
        CollectionAssert.AreEqual(csv.Split(','), C.SourcesAccepting(report, option).ToArray());

    [TestMethod] public void UnionOptions_Daily_IsDedupedCatalogOrder()
    {
        var union = C.UnionOptions("daily");
        CollectionAssert.AreEqual(new[] { "since","until","timezone","sections","by-agent","offline","no-cost","config","last","mode","debug","debug-samples","order","breakdown","single-thread","instances","project","project-aliases","speed","pi-path","open-claw-path" }, union.Select(o => o.Name).ToArray());
        var sections = union.Single(o => o.Name == "sections");
        Assert.AreEqual(OptionKind.CsvEnum, sections.Kind);
        CollectionAssert.AreEqual(new[] { "daily", "weekly", "monthly", "session" }, sections.EnumValues.ToArray());
    }
}
```

- [ ] **Step 3: Write the failing drift tests** in `Commands/OptionTypeTableDriftTests.cs`

```csharp
[TestClass]
public sealed class OptionTypeTableDriftTests
{
    // HelpEntries(): (flags, choices?) for every entry of every group in the embedded cli-help.json (JsonDocument)
    // LongName(flags): the "--name" token without "--"
    // ReportFor(name): the first report whose UnionOptions contains name

    [TestMethod] public void EveryHelpFlag_IsTypedOrNeverExposed()
    { foreach (var (flags, _) in HelpEntries()) { var n = LongName(flags); var b = n.StartsWith("no-") && n != "no-cost" ? n[3..] : n;
        Assert.IsTrue(OptionTypeTable.Entries.ContainsKey(b) || CommandCatalog.NeverExposed.Contains(b), $"untyped flag --{n}"); } }

    [TestMethod] public void EveryTypedEntry_ExistsInHelp()
    { var help = HelpEntries().Select(e => LongName(e.flags)).ToHashSet();
      foreach (var k in OptionTypeTable.Entries.Keys) Assert.IsTrue(help.Contains(k), $"stale table row {k}"); }

    [TestMethod] public void EnumKinds_HaveValues_AndMatchHelpChoices()
    { foreach (var (flags, choices) in HelpEntries()) { var n = LongName(flags);
        if (!OptionTypeTable.Entries.TryGetValue(n, out var e) || e.Kind is not (OptionKind.Enum or OptionKind.CsvEnum)) continue;
        var spec = CommandCatalog.Instance.UnionOptions(ReportFor(n)).Single(o => o.Name == n);
        Assert.IsTrue(spec.EnumValues.Count > 0, n);
        if (choices is not null) CollectionAssert.AreEqual(choices, spec.EnumValues.ToArray(), n); } }

    [TestMethod] public void NegatableBools_HaveNoFlagInHelp()
    { var help = HelpEntries().Select(e => LongName(e.flags)).ToHashSet();
      foreach (var (k, v) in OptionTypeTable.Entries) if (v.Kind == OptionKind.NegatableBool) Assert.IsTrue(help.Contains("no-" + k), k); }

    [TestMethod] public void EverySource_HasDisplayName()
    { var ids = CommandCatalog.Instance.Sources.Select(s => s.Id).ToHashSet();
      foreach (var id in ids) Assert.IsTrue(OptionTypeTable.DisplayNames.ContainsKey(id), id);
      foreach (var k in OptionTypeTable.DisplayNames.Keys) Assert.IsTrue(ids.Contains(k), $"orphan display name {k}"); }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: CS0246 `The type or namespace name 'CommandCatalog' could not be found` (also `OptionKind`, `OptionTypeTable`).

- [ ] **Step 5: Implement `OptionKind`** in `Commands/OptionKind.cs` — the enum from Interfaces.

- [ ] **Step 6: Implement `OptionTypeTable`** in `Commands/OptionTypeTable.cs` — `Entries` and `DisplayNames` exactly as listed (`FrozenDictionary`).

- [ ] **Step 7: Implement the JSON DTOs and `McpJsonContext`** in `Json/McpJsonContext.cs`

`[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)] [JsonSerializable(typeof(CliCommandsFile))] [JsonSerializable(typeof(Dictionary<string, List<CliHelpEntry>>))] internal sealed partial class McpJsonContext : JsonSerializerContext`; records `CliCommandsFile(Dictionary<string, List<string>> CombinedOptions, CliRoot Root, List<CliPage> Pages)`, `CliRoot(List<string> Usage, List<CliCommand> Commands)`, `CliCommand(string Name, string Description)`, `CliPage(List<string> Path, string Description, string? Usage, string? Options, List<CliCommand>? Commands)`, `CliHelpEntry(string Flags, string Description, string? Default, List<string>? Choices)`.

- [ ] **Step 8: Implement `CommandCatalog`** in `Commands/CommandCatalog.cs` — the class plus `OptionSpec`, `PageSpec`, `SourceSpec` from Interfaces; resources via `typeof(CommandCatalog).Assembly.GetManifestResourceStream("cli-commands.json")` / `"cli-help.json"`, deserialized with `McpJsonContext.Default`.

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`; all `CommandCatalogTests` and `OptionTypeTableDriftTests` passed.

- [ ] **Step 10: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`.

- [ ] **Step 11: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): derive command catalog from embedded cli-commands/cli-help json" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `CommandRequest`, `CommandValidator`, argv building

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Validate a normalized request against the catalog and produce the exact engine argv; failures are `ValidationException`s whose messages the MCP layer returns verbatim.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Commands/CommandRequest.cs`, `Commands/ValidationException.cs`, `Commands/CommandValidator.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Commands/CommandValidatorTests.cs`, `Commands/ArgvBuilderTests.cs`

**Interfaces:**
- Consumes: `CommandCatalog`, `OptionSpec`, `OptionKind` (Task 2).
- Produces:

```csharp
public sealed record OptionValue(string Name, string Raw);   // Raw: bools "true"/"false", numbers invariant text, others verbatim
public sealed record CommandRequest(string Source, string Report, IReadOnlyList<OptionValue> Options, string? Stdin = null);
public sealed class ValidationException(string message) : Exception(message);
public static class CommandValidator
{
    public static IReadOnlyList<string> ValidateAndBuildArgv(CommandCatalog catalog, CommandRequest request); // throws ValidationException
}
```

Argv rules: `[source]` omitted when `all`; `claude blocks` / `claude statusline` emit the root form `blocks` / `statusline` without `claude` (§16.12); then the report; then `--json` for every report except `statusline`; then options **in the page's catalog order**. `Bool` true → `--name`, false → nothing; `NegatableBool` true → `--name`, false → `--no-name`; all other kinds → `--name`, `value` as two elements, value verbatim.

Messages (exact):
- `Unknown source '{s}'. Valid sources: all, claude, codex, opencode, amp, droid, codebuff, hermes, pi, goose, kilo, copilot, gemini, antigravity, kimi, qwen, openclaw, grok, zcode.`
- `Unknown report '{r}'. Valid reports: daily, weekly, monthly, session, blocks, statusline.`
- `Report '{r}' is not available for source '{s}'. Sources with '{r}': a, b, c.`
- `Parameter '{p}' is not a parameter of '{s} {r}'. Valid parameters: a, b, c.` (unknown anywhere for that report; §16.13)
- `Parameter '{p}' does not apply to '{s} {r}'. Sources that accept it for '{r}': a, b.` (known for the report, not for this source)
- `Parameter '{p}' was given more than once.`
- `Parameter '{p}' must be one of: a, b, c (got '{v}').` (Enum)
- `Parameter '{p}' contains '{item}'; each item must be one of: a, b, c.` (CsvEnum, items trimmed; empty item or empty value → the "must not be empty" message)
- `Parameter '{p}' must be a date in YYYY-MM-DD or YYYYMMDD form (got '{v}').` (`DateOnly.TryParseExact` with `yyyy-MM-dd` and `yyyyMMdd`)
- `Parameter '{p}' must be an integer >= {min} (got '{v}').` (Int; min 1 for `last`, else 0)
- `Parameter '{p}' must be a number >= 0 (got '{v}').` (Number; `double.TryParse` invariant)
- `Parameter '{p}' must be a positive integer or "max" (got '{v}').` (IntOrMax)
- `Parameter '{p}' must be true or false (got '{v}').` (Bool/NegatableBool)
- `Parameter '{p}' must not be empty.` (String/Path/PathList/Date/CsvEnum with empty or whitespace Raw)
- `timezone` is passed through unless it contains whitespace (then `must not be empty`-style: `Parameter 'timezone' must be an IANA time zone id (got '{v}').`); the engine owns the authoritative check.

- [ ] **Step 1: Write the failing validator tests** in `Commands/CommandValidatorTests.cs`

```csharp
[TestClass]
public sealed class CommandValidatorTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;
    private static CommandRequest Req(string s, string r, params (string n, string v)[] o) => new(s, r, o.Select(x => new OptionValue(x.n, x.v)).ToList());
    private static string Error(CommandRequest req) => Assert.ThrowsExactly<ValidationException>(() => CommandValidator.ValidateAndBuildArgv(C, req)).Message;

    [TestMethod] public void EveryCatalogPair_Validates()
    { foreach (var s in C.Sources) foreach (var r in s.Reports) CommandValidator.ValidateAndBuildArgv(C, Req(s.Id, r)); }

    [DataTestMethod]
    [DataRow("codex", "weekly", "Report 'weekly' is not available for source 'codex'. Sources with 'weekly': all, claude, opencode.")]
    [DataRow("all", "blocks", "Report 'blocks' is not available for source 'all'. Sources with 'blocks': claude.")]
    [DataRow("opencode", "statusline", "Report 'statusline' is not available for source 'opencode'. Sources with 'statusline': claude.")]
    public void InvalidPair_Message(string s, string r, string expected) => Assert.AreEqual(expected, Error(Req(s, r)));

    [TestMethod] public void Validate_UnknownSource() => StringAssert.StartsWith(Error(Req("foo", "daily")), "Unknown source 'foo'. Valid sources: all, claude, codex, ");
    [TestMethod] public void Validate_UnknownReport() => Assert.AreEqual("Unknown report 'hourly'. Valid reports: daily, weekly, monthly, session, blocks, statusline.", Error(Req("all", "hourly")));

    [TestMethod] public void Validate_UnknownParameter_ListsValidOnes() =>
        Assert.AreEqual("Parameter 'bogus' is not a parameter of 'all daily'. Valid parameters: since, until, timezone, sections, by-agent, offline, no-cost, config, last.", Error(Req("all", "daily", ("bogus", "1"))));

    [TestMethod] public void Validate_InapplicableParameter_NamesAcceptingSources() =>
        Assert.AreEqual("Parameter 'speed' does not apply to 'claude daily'. Sources that accept it for 'daily': codex.", Error(Req("claude", "daily", ("speed", "fast"))));

    [TestMethod] public void Validate_Duplicate() => Assert.AreEqual("Parameter 'since' was given more than once.", Error(Req("all", "daily", ("since", "20260101"), ("since", "20260102"))));

    [DataTestMethod]
    [DataRow("claude", "daily", "mode", "fast", "Parameter 'mode' must be one of: auto, calculate, display (got 'fast').")]
    [DataRow("all", "weekly", "start-of-week", "Monday", "Parameter 'start-of-week' must be one of: sunday, monday, tuesday, wednesday, thursday, friday, saturday (got 'Monday').")]
    [DataRow("all", "daily", "sections", "daily,hourly", "Parameter 'sections' contains 'hourly'; each item must be one of: daily, weekly, monthly, session.")]
    [DataRow("all", "daily", "sections", "", "Parameter 'sections' must not be empty.")]
    [DataRow("all", "daily", "since", "2026/01/01", "Parameter 'since' must be a date in YYYY-MM-DD or YYYYMMDD form (got '2026/01/01').")]
    [DataRow("all", "daily", "since", "20261301", "Parameter 'since' must be a date in YYYY-MM-DD or YYYYMMDD form (got '20261301').")]
    [DataRow("all", "daily", "until", "2026-02-30", "Parameter 'until' must be a date in YYYY-MM-DD or YYYYMMDD form (got '2026-02-30').")]
    [DataRow("all", "daily", "last", "0", "Parameter 'last' must be an integer >= 1 (got '0').")]
    [DataRow("all", "daily", "last", "1.5", "Parameter 'last' must be an integer >= 1 (got '1.5').")]
    [DataRow("claude", "daily", "debug-samples", "-1", "Parameter 'debug-samples' must be an integer >= 0 (got '-1').")]
    [DataRow("claude", "blocks", "token-limit", "0", "Parameter 'token-limit' must be a positive integer or \"max\" (got '0').")]
    [DataRow("claude", "blocks", "token-limit", "MAX", "Parameter 'token-limit' must be a positive integer or \"max\" (got 'MAX').")]
    [DataRow("claude", "blocks", "session-length", "abc", "Parameter 'session-length' must be a number >= 0 (got 'abc').")]
    [DataRow("all", "daily", "offline", "yes", "Parameter 'offline' must be true or false (got 'yes').")]
    [DataRow("all", "daily", "by-agent", "1", "Parameter 'by-agent' must be true or false (got '1').")]
    [DataRow("claude", "daily", "project", "", "Parameter 'project' must not be empty.")]
    [DataRow("all", "daily", "timezone", "Asia Tokyo", "Parameter 'timezone' must be an IANA time zone id (got 'Asia Tokyo').")]
    public void InvalidValue_Message(string s, string r, string n, string v, string expected) => Assert.AreEqual(expected, Error(Req(s, r, (n, v))));

    [DataTestMethod]
    [DataRow("claude", "daily", "mode", "display")]
    [DataRow("all", "daily", "sections", "daily, session")]
    [DataRow("all", "daily", "since", "20260101")]
    [DataRow("all", "daily", "since", "2026-01-01")]
    [DataRow("all", "daily", "last", "1")]
    [DataRow("claude", "daily", "debug-samples", "0")]
    [DataRow("claude", "blocks", "token-limit", "500000")]
    [DataRow("claude", "blocks", "token-limit", "max")]
    [DataRow("claude", "blocks", "session-length", "2.5")]
    [DataRow("all", "daily", "offline", "false")]
    [DataRow("all", "daily", "timezone", "America/New_York")]
    public void ValidValue_Accepted(string s, string r, string n, string v) => CommandValidator.ValidateAndBuildArgv(C, Req(s, r, (n, v)));
}
```

- [ ] **Step 2: Write the failing argv tests** in `Commands/ArgvBuilderTests.cs`

```csharp
[TestClass]
public sealed class ArgvBuilderTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;
    private static string[] Argv(string s, string r, params (string n, string v)[] o) =>
        CommandValidator.ValidateAndBuildArgv(C, new CommandRequest(s, r, o.Select(x => new OptionValue(x.n, x.v)).ToList())).ToArray();

    [TestMethod] public void AllDaily_NoOptions() => CollectionAssert.AreEqual(new[] { "daily", "--json" }, Argv("all", "daily"));
    [TestMethod] public void CodexSession() => CollectionAssert.AreEqual(new[] { "codex", "session", "--json" }, Argv("codex", "session"));
    [TestMethod] public void ClaudeBlocks_RootForm() => CollectionAssert.AreEqual(new[] { "blocks", "--json" }, Argv("claude", "blocks"));
    [TestMethod] public void ClaudeStatusline_NoJson() => CollectionAssert.AreEqual(new[] { "statusline" }, Argv("claude", "statusline"));

    [TestMethod] public void Options_InCatalogOrder_NotRequestOrder() =>
        CollectionAssert.AreEqual(new[] { "daily", "--json", "--by-agent", "--offline", "--last", "2" }, Argv("all", "daily", ("last", "2"), ("by-agent", "true"), ("offline", "true")));

    [TestMethod] public void NegatableFalse_EmitsNoFlag_PlainFalse_EmitsNothing() =>
        CollectionAssert.AreEqual(new[] { "claude", "daily", "--json", "--no-offline", "--instances", "--project", "My Project" },
            Argv("claude", "daily", ("project", "My Project"), ("instances", "true"), ("offline", "false"), ("breakdown", "false")));

    [TestMethod] public void Blocks_Values() =>
        CollectionAssert.AreEqual(new[] { "blocks", "--json", "--active", "--token-limit", "max", "--session-length", "2.5" }, Argv("claude", "blocks", ("token-limit", "max"), ("session-length", "2.5"), ("active", "true")));

    [TestMethod] public void Statusline_Options() =>
        CollectionAssert.AreEqual(new[] { "statusline", "--visual-burn-rate", "emoji", "--no-cache" }, Argv("claude", "statusline", ("cache", "false"), ("visual-burn-rate", "emoji")));

    [TestMethod] public void Codex_Speed() => CollectionAssert.AreEqual(new[] { "codex", "daily", "--json", "--speed", "flex" }, Argv("codex", "daily", ("speed", "flex")));
    [TestMethod] public void PathList_IsOneElement() => CollectionAssert.AreEqual(new[] { "pi", "daily", "--json", "--pi-path", "/a,/b c" }, Argv("pi", "daily", ("pi-path", "/a,/b c")));
    [TestMethod] public void AllWeekly_StartOfWeek() => CollectionAssert.AreEqual(new[] { "weekly", "--json", "--start-of-week", "monday" }, Argv("all", "weekly", ("start-of-week", "monday")));
    [TestMethod] public void PlainBoolFalse_EmitsNothing() => CollectionAssert.AreEqual(new[] { "daily", "--json" }, Argv("all", "daily", ("no-cost", "false")));
    [TestMethod] public void DateValue_PassedVerbatim() => CollectionAssert.AreEqual(new[] { "daily", "--json", "--since", "2026-01-01" }, Argv("all", "daily", ("since", "2026-01-01")));
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: CS0246 `'CommandRequest'`, `'OptionValue'`, `'ValidationException'`, `'CommandValidator'` not found.

- [ ] **Step 4: Implement `CommandRequest` and `OptionValue`** in `Commands/CommandRequest.cs` — the two records from Interfaces.

- [ ] **Step 5: Implement `ValidationException`** in `Commands/ValidationException.cs`.

- [ ] **Step 6: Implement `CommandValidator.ValidateAndBuildArgv`** in `Commands/CommandValidator.cs` — the rules and messages above; one private `ValidateValue(OptionSpec spec, string raw)` per kind.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`; all `CommandValidatorTests` and `ArgvBuilderTests` passed.

- [ ] **Step 8: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`.

- [ ] **Step 9: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): validate requests against the catalog and build engine argv" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `CliArgsParser`

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Turn CLI-style text (`report` tool `args`, prompt `args`, `/ccusage $ARGUMENTS`) into a `CommandRequest` with the grammar of §5.5.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Commands/CliArgsParser.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Commands/CliArgsParserTests.cs`

**Interfaces:**
- Consumes: `CommandCatalog` (`Sources`, `Reports`, `TryGetPage`, `NeverExposed`), `CommandRequest`, `OptionValue`, `ValidationException`.
- Produces: `public static class CliArgsParser { public static IReadOnlyList<string> Tokenize(string args); public static CommandRequest Parse(CommandCatalog catalog, string args); }`

Grammar: whitespace-separated tokens; a double-quoted span is one token with the quotes removed and may contain spaces; `\"` inside quotes is a literal quote; unterminated quote → `Unterminated quoted value in: {args}`. First token a source id → source (default `all`); next token a report → report (default `daily`); then flags. The page `(source, report)` must exist (else the Task 3 pair message). `--name=value` and `--name value`; `-x` short aliases resolved against **that page's** options (`-i` is `instances` on `claude daily`, `id` on `claude session`); `TakesValue && !ValueOptional` without a value → `Flag '--since' needs a value.`; `ValueOptional`: next token consumed only if it exists and does not start with `-`, else Raw = `Default` (null → `"true"`); no-value option → Raw `"true"`; `--no-<name>` for a `NegatableBool` → Raw `"false"`, for anything else → `Unknown argument '--no-<name>'.`; bare token after flags began → `Unknown argument '{tok}'.`; `NeverExposed` flags (`--json`, `-j`, `--compact`, `--color`, `--no-color`, `--help`, `-h`, `--version`, `-v`, `--all`) → `Flag '{flag}' is not available through the MCP server.`; report `statusline` (with or without `claude`) → `'statusline' needs a hook payload; use the statusline tool.`; unknown long flag → `Unknown argument '--foo'.`; empty/whitespace input → `("all","daily",[])`. Values are not validated here (Task 3 does).

- [ ] **Step 1: Write the failing tests** in `Commands/CliArgsParserTests.cs`

```csharp
[TestClass]
public sealed class CliArgsParserTests
{
    private static readonly CommandCatalog C = CommandCatalog.Instance;
    private static CommandRequest P(string a) => CliArgsParser.Parse(C, a);
    private static string Error(string a) => Assert.ThrowsExactly<ValidationException>(() => P(a)).Message;
    private static (string, string)[] Opts(CommandRequest r) => r.Options.Select(o => (o.Name, o.Raw)).ToArray();

    [TestMethod] public void Tokenize_Quotes()
    { CollectionAssert.AreEqual(new[] { "claude", "daily", "--project", "My Project", "--since", "20260101" }, CliArgsParser.Tokenize("claude daily --project \"My Project\" --since 20260101").ToArray());
      CollectionAssert.AreEqual(new[] { "--project=A B" }, CliArgsParser.Tokenize("--project=\"A B\"").ToArray());
      CollectionAssert.AreEqual(new[] { "say \"hi\"" }, CliArgsParser.Tokenize("\"say \\\"hi\\\"\"").ToArray()); }
    [TestMethod] public void Tokenize_UnterminatedQuote_Throws() => Assert.AreEqual("Unterminated quoted value in: daily --project \"x", Error("daily --project \"x"));

    [DataTestMethod] [DataRow("")] [DataRow("   ")]
    public void Parse_Empty_IsAllDaily(string a) { var r = P(a); Assert.AreEqual(("all", "daily", 0), (r.Source, r.Report, r.Options.Count)); }

    [TestMethod] public void Parse_ReportOnly() { var r = P("weekly --last 2 --by-agent"); Assert.AreEqual(("all", "weekly"), (r.Source, r.Report)); CollectionAssert.AreEqual(new[] { ("last", "2"), ("by-agent", "true") }, Opts(r)); }
    [TestMethod] public void Parse_SourceAndReport() { var r = P("codex daily --since 2026-10-01"); Assert.AreEqual(("codex", "daily"), (r.Source, r.Report)); CollectionAssert.AreEqual(new[] { ("since", "2026-10-01") }, Opts(r)); }
    [TestMethod] public void Parse_SourceOnly_DefaultsDaily() => Assert.AreEqual(("codex", "daily"), (P("codex").Source, P("codex").Report));
    [TestMethod] public void Parse_EqualsForm() => CollectionAssert.AreEqual(new[] { ("since", "20260101") }, Opts(P("daily --since=20260101")));

    [DataTestMethod]
    [DataRow("claude daily -i", "instances", "true")]
    [DataRow("claude session -i abc", "id", "abc")]
    [DataRow("claude daily -s 20260101", "since", "20260101")]
    [DataRow("claude daily -O", "offline", "true")]
    public void Parse_ShortAlias_PerPage(string a, string n, string v) => CollectionAssert.AreEqual(new[] { (n, v) }, Opts(P(a)));

    [DataTestMethod]
    [DataRow("codex daily --speed", "speed", "auto")]
    [DataRow("codex daily --speed fast", "speed", "fast")]
    [DataRow("claude blocks -n", "session-length", "5")]
    public void Parse_OptionalValue(string a, string n, string v) => CollectionAssert.AreEqual(new[] { (n, v) }, Opts(P(a)));
    [TestMethod] public void Parse_OptionalValue_StopsAtFlag() => CollectionAssert.AreEqual(new[] { ("speed", "auto"), ("offline", "true") }, Opts(P("codex daily --speed --offline")));

    [TestMethod] public void Parse_NegatedNegatable() => CollectionAssert.AreEqual(new[] { ("offline", "false") }, Opts(P("claude daily --no-offline")));
    [TestMethod] public void Parse_NegatedPlainBool_Unknown() => Assert.AreEqual("Unknown argument '--no-breakdown'.", Error("claude daily --no-breakdown"));

    [DataTestMethod]
    [DataRow("--json")] [DataRow("-j")] [DataRow("--compact")] [DataRow("--color")] [DataRow("--no-color")] [DataRow("--help")] [DataRow("-h")] [DataRow("--version")] [DataRow("-v")] [DataRow("--all")]
    public void Parse_NeverExposed(string flag) => Assert.AreEqual($"Flag '{flag}' is not available through the MCP server.", Error("daily " + flag));

    [DataTestMethod] [DataRow("statusline")] [DataRow("claude statusline --offline")]
    public void Parse_Statusline_Rejected(string a) => Assert.AreEqual("'statusline' needs a hook payload; use the statusline tool.", Error(a));

    [DataTestMethod]
    [DataRow("daily foo", "Unknown argument 'foo'.")]
    [DataRow("daily --bogus", "Unknown argument '--bogus'.")]
    [DataRow("daily --since", "Flag '--since' needs a value.")]
    [DataRow("codex weekly", "Report 'weekly' is not available for source 'codex'. Sources with 'weekly': all, claude, opencode.")]
    public void Parse_Errors(string a, string expected) => Assert.AreEqual(expected, Error(a));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: CS0103 `The name 'CliArgsParser' does not exist`.

- [ ] **Step 3: Implement `CliArgsParser.Tokenize`** in `Commands/CliArgsParser.cs` — signature from Interfaces; a single pass over the characters with an in-quotes flag.

- [ ] **Step 4: Implement `CliArgsParser.Parse`** in the same file — the grammar above; alias lookup is `page.Options.FirstOrDefault(o => o.ShortAlias == alias)`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`; all `CliArgsParserTests` passed.

- [ ] **Step 6: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`.

- [ ] **Step 7: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): parse CLI-style arguments into command requests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `EngineLocator`

**Implementer:** Opus  **Reviewer:** Opus

**Phase 0 dependency:** §15 row 5. If it says `RuntimeInformation.OSArchitecture` reports the host architecture under emulation (or the runtime source confirms it), skip step 7; otherwise do step 7.

**Goal:** Resolve the engine path per §6.1, list every checked location on failure, and add the engine line to `--version`.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Engine/EngineLocator.cs`, `Engine/EngineNotFoundException.cs`
- Modify: `dotnet/src/Ccusage.Mcp/Program.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Engine/EngineLocatorTests.cs`

**Interfaces (produces):**

```csharp
public sealed class EngineNotFoundException(IReadOnlyList<string> checkedPaths) : Exception
{   // Message: "ccusage engine not found. Set CCUSAGE_ENGINE or place ccusage next to ccusage-mcp. Checked:\n  <p1>\n  <p2>..."
    public IReadOnlyList<string> CheckedPaths { get; } }

public sealed class EngineLocator(Func<string, string?> getEnv, string serverDirectory, Architecture osArchitecture, bool isWindows)
{
    public static EngineLocator Default { get; }   // Environment.GetEnvironmentVariable, AppContext.BaseDirectory, OSArchitecture (or step 7), OperatingSystem.IsWindows()
    public IReadOnlyList<string> CandidatePaths();  // resolution order
    public string Resolve();                        // first existing candidate; throws EngineNotFoundException
}
```

Candidates: (1) `CCUSAGE_ENGINE` if non-empty, as given; (2) `<serverDirectory>/ccusage[.exe]`; (3) `<serverDirectory>/<arch>/ccusage[.exe]` with `x64` for `Architecture.X64`, `arm64` for `Architecture.Arm64` (other values skip rule 3); (4) each non-empty entry of `PATH` split on `Path.PathSeparator`, joined with `ccusage[.exe]`. `.exe` only when `isWindows`.

- [ ] **Step 1: Write the failing tests** in `Engine/EngineLocatorTests.cs`

```csharp
[TestClass]
public sealed class EngineLocatorTests
{
    // Tmp(): Directory.CreateTempSubdirectory().FullName; Touch(dir, name): creates an empty file and returns its path
    // Loc(env, dir, arch = Architecture.X64, win = false, path = ""): new EngineLocator(k => k == "CCUSAGE_ENGINE" ? env : k == "PATH" ? path : null, dir, arch, win)

    [TestMethod] public void Resolve_PrefersEnvVar()
    { var d = Tmp(); var env = Touch(Tmp(), "ccusage"); Touch(d, "ccusage"); Assert.AreEqual(env, Loc(env, d).Resolve()); }

    [TestMethod] public void Resolve_EnvVarMissingFile_IsSkipped_AndListed()
    { var d = Tmp(); var missing = Path.Combine(Tmp(), "nope"); var sib = Touch(d, "ccusage"); var l = Loc(missing, d);
      Assert.AreEqual(sib, l.Resolve()); Assert.AreEqual(missing, l.CandidatePaths()[0]); }

    [TestMethod] public void Resolve_SiblingBeforeArchFolder()
    { var d = Tmp(); var sib = Touch(d, "ccusage"); Touch(Directory.CreateDirectory(Path.Combine(d, "x64")).FullName, "ccusage"); Assert.AreEqual(sib, Loc(null, d).Resolve()); }

    [TestMethod] public void Resolve_ArchFolder_UsesOsArchitecture()
    { var d = Tmp(); var arm = Touch(Directory.CreateDirectory(Path.Combine(d, "arm64")).FullName, "ccusage");
      Assert.AreEqual(arm, Loc(null, d, Architecture.Arm64).Resolve());
      Assert.ThrowsExactly<EngineNotFoundException>(() => Loc(null, d, Architecture.X64).Resolve()); }

    [TestMethod] public void Resolve_Path_LastResort()
    { var d = Tmp(); var p1 = Tmp(); var p2 = Tmp(); var hit = Touch(p2, "ccusage");
      Assert.AreEqual(hit, Loc(null, d, path: string.Join(Path.PathSeparator, p1, p2)).Resolve()); }

    [TestMethod] public void Resolve_Windows_UsesExe()
    { var d = Tmp(); var exe = Touch(d, "ccusage.exe"); Touch(d, "ccusage"); Assert.AreEqual(exe, Loc(null, d, win: true).Resolve()); }

    [TestMethod] public void Resolve_NonWindows_NoExe()
    { var d = Tmp(); Touch(d, "ccusage.exe"); Assert.ThrowsExactly<EngineNotFoundException>(() => Loc(null, d, win: false).Resolve()); }

    [TestMethod] public void Resolve_NotFound_ListsEveryCandidateInOrder()
    { var d = Tmp(); var p1 = Tmp(); var p2 = Tmp(); var env = Path.Combine(Tmp(), "e");
      var ex = Assert.ThrowsExactly<EngineNotFoundException>(() => Loc(env, d, Architecture.Arm64, path: string.Join(Path.PathSeparator, p1, p2)).Resolve());
      CollectionAssert.AreEqual(new[] { env, Path.Combine(d, "ccusage"), Path.Combine(d, "arm64", "ccusage"), Path.Combine(p1, "ccusage"), Path.Combine(p2, "ccusage") }, ex.CheckedPaths.ToArray());
      StringAssert.StartsWith(ex.Message, "ccusage engine not found. Set CCUSAGE_ENGINE or place ccusage next to ccusage-mcp. Checked:\n  " + env); }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: CS0246 `'EngineLocator'` / `'EngineNotFoundException'` not found.

- [ ] **Step 3: Implement `EngineNotFoundException`** in `Engine/EngineNotFoundException.cs`.

- [ ] **Step 4: Implement `EngineLocator`** in `Engine/EngineLocator.cs` — Interfaces signatures; `Resolve` is `CandidatePaths().FirstOrDefault(File.Exists) ?? throw new EngineNotFoundException(CandidatePaths())`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`; all `EngineLocatorTests` passed.

- [ ] **Step 6: Add the engine line to `--version`** in `Program.cs`

After the server line: `try { var p = EngineLocator.Default.Resolve(); Process.Start(new ProcessStartInfo(p, ["--version"]) { RedirectStandardOutput = true, UseShellExecute = false })` → print the first stdout line (10 s wait) } `catch (EngineNotFoundException e) { Console.Out.WriteLine("ccusage: not found"); Console.Out.WriteLine(e.Message); }`; exit code stays 0.
Verify [local]: `CCUSAGE_ENGINE=$(pwd)/rust/target/release/ccusage.exe dotnet run --project dotnet/src/Ccusage.Mcp -- --version` prints `ccusage-mcp 20.0.45` then `ccusage 20.0.45`; unset → second line `ccusage: not found` followed by the `Checked:` list.

- [ ] **Step 7: Q5 fallback (only if §15 row 5 says `OSArchitecture` is unreliable)**

In `EngineLocator.cs`: `private static partial class NativeMethods { [LibraryImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static partial bool IsWow64Process2(nint hProcess, out ushort processMachine, out ushort nativeMachine); }` and `internal static Architecture DetectOsArchitecture()` → `nativeMachine` `0xAA64` → `Arm64`, `0x8664` → `X64`, else `RuntimeInformation.OSArchitecture`; `Default` uses it on Windows. No unit test (hardware); the CI check is Task 11b's `windows-11-arm` bundle-smoke leg. Note in `dotnet/AGENTS.md`.
Expected: `dotnet build -c Release` 0 warnings (no `SYSLIB1054`).

- [ ] **Step 8: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`.

- [ ] **Step 9: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): locate the ccusage engine by env, server dir, arch folder and PATH" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `EngineRunner` with FakeEngine tests

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Run the engine without a shell, capture stdout/stderr concurrently, deliver stdin for `statusline`, enforce timeout and cancellation by killing the process tree (§6.2, §10.2).

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Engine/EngineInvocation.cs`, `Engine/EngineResult.cs`, `Engine/EngineRunner.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Engine/EngineRunnerTests.cs`

**Interfaces (produces):**

```csharp
public sealed record EngineInvocation(string EnginePath, IReadOnlyList<string> Argv, string? Stdin);
public sealed record EngineResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);
public sealed class EngineRunner(TimeSpan timeout, Action<string> logWarning)
{
    public static TimeSpan TimeoutFromEnvironment(Func<string, string?> getEnv, Action<string> logWarning); // CCUSAGE_MCP_TIMEOUT_SECONDS; default 120; invalid or <= 0 -> warn once, 120
    public Task<EngineResult> RunAsync(EngineInvocation invocation, CancellationToken cancellationToken);
}
```

Behavior: `ProcessStartInfo { FileName = EnginePath, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = Stdin is not null, StandardOutputEncoding = UTF8, StandardErrorEncoding = UTF8 }`, `ArgumentList` from `Argv`, environment inherited plus `NO_COLOR=1`. Start both `ReadToEndAsync` before writing stdin; write then close stdin. Linked CTS = caller token + `CancelAfter(timeout)`; on expiry → `Kill(entireProcessTree: true)`, await exit, `TimedOut = true`; on caller cancellation → kill, then rethrow `OperationCanceledException`. Warning text: `CCUSAGE_MCP_TIMEOUT_SECONDS='{v}' is not a positive number; using 120.`

- [ ] **Step 1: Write the failing tests** in `Engine/EngineRunnerTests.cs`

```csharp
[TestClass]
public sealed class EngineRunnerTests
{
    // Run(argv, stdin = null, timeoutSeconds = 10, ct = default): new EngineRunner(TimeSpan.FromSeconds(timeoutSeconds), _ => { }).RunAsync(new(TestPaths.FakeEnginePath, argv, stdin), ct)
    // Echo(result): JsonDocument.Parse(result.Stdout).RootElement

    [TestMethod] public async Task RunAsync_EchoesArgvAndExitZero()
    { var r = await Run(["daily", "--json"]); Assert.AreEqual((0, "", false), (r.ExitCode, r.Stderr, r.TimedOut));
      CollectionAssert.AreEqual(new[] { "daily", "--json" }, Echo(r).GetProperty("argv").EnumerateArray().Select(e => e.GetString()).ToArray()); }

    [TestMethod] public async Task RunAsync_PassesSpacesQuotesAndUnicodeAsSingleArgv()
    { const string v = "My \"Quoted\" Project — 日本語"; var r = await Run(["claude", "daily", "--project", v]);
      Assert.AreEqual(v, Echo(r).GetProperty("argv")[3].GetString()); }

    [TestMethod] public async Task RunAsync_DeliversStdin()
    { var r = await Run(["statusline"], "{\"session_id\":\"x\"}"); Assert.AreEqual("{\"session_id\":\"x\"}", Echo(r).GetProperty("stdin").GetString()); }

    [TestMethod] public async Task RunAsync_SetsNoColor() => Assert.AreEqual("1", Echo(await Run(["daily"])).GetProperty("noColor").GetString());

    [TestMethod] public async Task RunAsync_CapturesStderr()
    { var r = await Run(["daily", "--fake-stderr=warn: pricing"]); Assert.AreEqual(0, r.ExitCode); StringAssert.Contains(r.Stderr, "warn: pricing"); }

    [TestMethod] public async Task RunAsync_NonZeroExit() => Assert.AreEqual(3, (await Run(["daily", "--fake-exit=3"])).ExitCode);

    [TestMethod] public async Task RunAsync_LargeStdout_DoesNotDeadlock()
    { var r = await Run(["daily", "--fake-stdout-bytes=8388608"]); Assert.AreEqual(8388608, r.Stdout.Length); }

    [TestMethod] public async Task RunAsync_Timeout_KillsAndReportsTimedOut()
    { var sw = Stopwatch.StartNew(); var r = await Run(["daily", "--fake-hang"], timeoutSeconds: 1);
      Assert.IsTrue(r.TimedOut); Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString()); StringAssert.Contains(r.Stdout, "\"argv\""); }

    [TestMethod] public async Task RunAsync_Cancellation_KillsProcess()
    { using var cts = new CancellationTokenSource(300); var sw = Stopwatch.StartNew();
      await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Run(["daily", "--fake-hang"], ct: cts.Token));
      Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString()); }

    [DataTestMethod]
    [DataRow("abc", 120, 1)] [DataRow("0", 120, 1)] [DataRow("-5", 120, 1)] [DataRow("", 120, 0)] [DataRow(null, 120, 0)] [DataRow("45", 45, 0)]
    public void TimeoutSeconds_InvalidEnv_FallsBackTo120(string? value, int expectedSeconds, int expectedWarnings)
    { var warnings = new List<string>(); var t = EngineRunner.TimeoutFromEnvironment(k => k == "CCUSAGE_MCP_TIMEOUT_SECONDS" ? value : null, warnings.Add);
      Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), t); Assert.AreEqual(expectedWarnings, warnings.Count);
      if (expectedWarnings == 1) Assert.AreEqual($"CCUSAGE_MCP_TIMEOUT_SECONDS='{value}' is not a positive number; using 120.", warnings[0]); }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: CS0246 `'EngineRunner'` / `'EngineInvocation'` / `'EngineResult'` not found.

- [ ] **Step 3: Implement `EngineInvocation` and `EngineResult`** in their files — the records from Interfaces.

- [ ] **Step 4: Implement `EngineRunner.TimeoutFromEnvironment`** in `Engine/EngineRunner.cs` — `int.TryParse` invariant; `> 0` required.

- [ ] **Step 5: Implement `EngineRunner.RunAsync`** in the same file — the behavior above.

- [ ] **Step 6: Run the tests to verify they pass (twice)**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx` two times. Expected: both runs `Failed: 0`, including the timeout and cancellation tests.

- [ ] **Step 7: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`.

- [ ] **Step 8: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): run the engine with concurrent capture, stdin, timeout and cancellation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7a: `ToolDefinitions`, `PromptDefinitions`, schema snapshots

**Implementer:** Opus  **Reviewer:** Fable

**Phase 0 dependency:** §15 row 6 — use the SDK type names recorded there (`Tool`, `ToolAnnotations`, `Prompt`, `PromptArgument`, `ListToolsResult`, `ListPromptsResult`, `McpJsonUtilities.DefaultOptions`).

**Goal:** The eight tool definitions with JSON input schemas derived from the catalog and the `ccusage` prompt definition, pinned by checked-in snapshots.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Tools/ToolDefinitions.cs`, `dotnet/src/Ccusage.Mcp/Prompts/PromptDefinitions.cs`, `dotnet/tests/Ccusage.Mcp.Tests/Snapshots/tools-list.json`, `Snapshots/prompts-list.json`
- Modify: `dotnet/tests/Ccusage.Mcp.Tests/Ccusage.Mcp.Tests.csproj` (`<Content Include="Snapshots/**" CopyToOutputDirectory="PreserveNewest" />`; `AssemblyMetadata` `SnapshotsSourceDir` = `$(MSBuildThisFileDirectory)Snapshots`)
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Tools/ToolDefinitionsTests.cs`, `Tools/SnapshotTests.cs`

**Interfaces (produces):**

```csharp
public static class ToolDefinitions
{
    public static IReadOnlyList<Tool> Build(CommandCatalog catalog);            // daily, weekly, monthly, session, blocks, statusline, list-sources, report
    public static JsonElement BuildReportSchema(CommandCatalog catalog, string report);
}
public static class PromptDefinitions { public static Prompt Ccusage { get; } }
```

Schema rules (every schema `"type":"object"`, `"additionalProperties":false`, `properties` in catalog order):
- `daily|weekly|monthly|session`: `source` first — `{"type":"string","enum":["all",<sources with that report>],"default":"all","description":"Data source. Default: all."}`; then `UnionOptions(report)` mapped: Bool/NegatableBool → `{"type":"boolean"}`; Enum → `{"type":"string","enum":[...]}`; CsvEnum → `{"type":"string"}` + description suffix ` Comma-separated; each item one of: a, b, c.`; Date → `{"type":"string","pattern":"^(\\d{4}-\\d{2}-\\d{2}|\\d{8})$"}`; Int → `{"type":"integer","minimum":1}` for `last`, `"minimum":0` otherwise; Number → `{"type":"number","minimum":0}`; IntOrMax → `{"type":"string","pattern":"^([1-9]\\d*|max)$"}`; String/Path/PathList → `{"type":"string"}`. Description = help description + ` Default: {Default}.` when non-null + ` Applies to: a, b.` (`SourcesAccepting`) unless every source with the report accepts it.
- `blocks`: options of `(claude, blocks)`, no `source`. `statusline`: `hook-input` first — `{"type":"object","description":"Claude Code status-line hook payload (session_id, transcript_path, cwd, model, workspace, version, cost, context_window). Written to the engine's stdin unchanged."}`, `required:["hook-input"]`, then `(claude, statusline)` options. `list-sources`: `{"type":"object","properties":{},"additionalProperties":false}`. `report`: `args` `{"type":"string","description":"CLI-style arguments as typed after `ccusage`, e.g. \"codex daily --since 2026-10-01\", \"weekly --last 2 --by-agent\", \"\" (= daily, all sources)."}`, `required:["args"]`.
- Descriptions: `daily` `Token usage and cost grouped by day. Default source: all detected sources. Long ranges produce large JSON; prefer since/until or last.`; `weekly`/`monthly`/`session` same with `by week` / `by month` / `by session`; `blocks` `Claude Code usage grouped by 5-hour billing blocks (Claude only).`; `statusline` `Claude Code status line text for a hook payload (Claude only; returns plain text).`; `list-sources` `List supported sources, their reports, and the server/engine versions and engine path.`; `report` `Run any ccusage report from CLI-style arguments; same validation as the typed tools.`
- Annotations: `ReadOnlyHint = true`, `DestructiveHint = false`, `IdempotentHint = true`, `OpenWorldHint = null`.
- Prompt `ccusage`: description `Run a ccusage report and present it as a readable table.`; one argument `args`, `Required = false`, description = the `report` tool's `args` description.

- [ ] **Step 1: Write the failing definition tests** in `Tools/ToolDefinitionsTests.cs`

```csharp
[TestClass]
public sealed class ToolDefinitionsTests
{
    private static readonly IReadOnlyList<Tool> Tools = ToolDefinitions.Build(CommandCatalog.Instance);
    private static Tool T(string n) => Tools.Single(t => t.Name == n);
    private static JsonElement Props(string n) => T(n).InputSchema.GetProperty("properties");

    [TestMethod] public void Build_ReturnsEightToolsInOrder() =>
        CollectionAssert.AreEqual(new[] { "daily", "weekly", "monthly", "session", "blocks", "statusline", "list-sources", "report" }, Tools.Select(t => t.Name).ToArray());

    [TestMethod] public void Daily_SourceEnum_IsAllPlusEverySourceWithDaily()
    { var e = Props("daily").GetProperty("source").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray(); Assert.AreEqual(19, e.Length); Assert.AreEqual("all", e[0]); }

    [TestMethod] public void Weekly_SourceEnum() =>
        CollectionAssert.AreEqual(new[] { "all", "claude", "opencode" }, Props("weekly").GetProperty("source").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray());

    [TestMethod] public void Daily_Schema_IsStrictUnion()
    { var s = T("daily").InputSchema; Assert.IsFalse(s.GetProperty("additionalProperties").GetBoolean()); Assert.IsFalse(s.TryGetProperty("required", out _));
      foreach (var p in new[] { "speed", "instances", "sections", "pi-path", "last" }) Assert.IsTrue(Props("daily").TryGetProperty(p, out _), p); }

    [TestMethod] public void Daily_Speed_AppliesToCodex() => StringAssert.EndsWith(Props("daily").GetProperty("speed").GetProperty("description").GetString(), " Default: auto. Applies to: codex.");
    [TestMethod] public void Daily_Since_HasNoAppliesTo() => Assert.IsFalse(Props("daily").GetProperty("since").GetProperty("description").GetString()!.Contains("Applies to"));
    [TestMethod] public void Daily_Last_MinimumOne() => Assert.AreEqual(1, Props("daily").GetProperty("last").GetProperty("minimum").GetInt32());
    [TestMethod] public void Daily_Since_Pattern() => Assert.AreEqual("^(\\d{4}-\\d{2}-\\d{2}|\\d{8})$", Props("daily").GetProperty("since").GetProperty("pattern").GetString());

    [TestMethod] public void Blocks_HasNoSource_AndTokenLimitPattern()
    { Assert.IsFalse(Props("blocks").TryGetProperty("source", out _)); Assert.AreEqual("^([1-9]\\d*|max)$", Props("blocks").GetProperty("token-limit").GetProperty("pattern").GetString());
      Assert.AreEqual("number", Props("blocks").GetProperty("session-length").GetProperty("type").GetString()); }

    [TestMethod] public void Statusline_RequiresHookInputObject()
    { Assert.AreEqual("object", Props("statusline").GetProperty("hook-input").GetProperty("type").GetString());
      CollectionAssert.AreEqual(new[] { "hook-input" }, T("statusline").InputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray()); }

    [TestMethod] public void Report_RequiresArgs() => CollectionAssert.AreEqual(new[] { "args" }, T("report").InputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray());
    [TestMethod] public void ListSources_EmptyProperties() => Assert.AreEqual(0, Props("list-sources").EnumerateObject().Count());

    [TestMethod] public void Annotations_OnEveryTool()
    { foreach (var t in Tools) { Assert.AreEqual(true, t.Annotations!.ReadOnlyHint, t.Name); Assert.AreEqual(false, t.Annotations.DestructiveHint, t.Name); Assert.AreEqual(true, t.Annotations.IdempotentHint, t.Name); Assert.IsNull(t.Annotations.OpenWorldHint, t.Name); } }

    [TestMethod] public void Prompt_HasOptionalArgsArgument()
    { var p = PromptDefinitions.Ccusage; Assert.AreEqual("ccusage", p.Name); var a = p.Arguments!.Single(); Assert.AreEqual("args", a.Name); Assert.AreNotEqual(true, a.Required); }
}
```

- [ ] **Step 2: Write the failing snapshot tests** in `Tools/SnapshotTests.cs`

```csharp
[TestClass]
public sealed class SnapshotTests
{
    private static readonly JsonSerializerOptions Pretty = new(McpJsonUtilities.DefaultOptions) { WriteIndented = true };
    // AssertSnapshot(name, actualJson): compares to Snapshots/<name> in the output dir (normalize \r\n -> \n);
    // when Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1": overwrite <SnapshotsSourceDir>/<name> (assembly metadata) and Assert.Fail("Snapshot updated; re-run.")

    [TestMethod] public void ToolsList_MatchesSnapshot() =>
        AssertSnapshot("tools-list.json", JsonSerializer.Serialize(new ListToolsResult { Tools = ToolDefinitions.Build(CommandCatalog.Instance).ToList() }, Pretty));

    [TestMethod] public void PromptsList_MatchesSnapshot() =>
        AssertSnapshot("prompts-list.json", JsonSerializer.Serialize(new ListPromptsResult { Prompts = [PromptDefinitions.Ccusage] }, Pretty));
}
```

Create both snapshot files empty.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: CS0103 `'ToolDefinitions'` / `'PromptDefinitions'` does not exist.

- [ ] **Step 4: Implement `ToolDefinitions.BuildReportSchema`** in `Tools/ToolDefinitions.cs` — write with `Utf8JsonWriter` into an `ArrayBufferWriter<byte>`, return `JsonDocument.Parse(...).RootElement.Clone()`; a private `WriteOption(Utf8JsonWriter w, OptionSpec o, IReadOnlyList<string> appliesTo, bool everyone)` per kind.

- [ ] **Step 5: Implement `ToolDefinitions.Build`** in the same file — the eight `Tool`s with the descriptions and annotations above.

- [ ] **Step 6: Implement `PromptDefinitions.Ccusage`** in `Prompts/PromptDefinitions.cs`.

- [ ] **Step 7: Generate the snapshots**

Run: `UPDATE_SNAPSHOTS=1 dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: both snapshot tests fail with `Snapshot updated; re-run.`; `Snapshots/tools-list.json` now lists 8 tools. Read it once against §5.1–§5.5 (every option appears on the right tools; `statusline` has `hook-input` and 10 options).

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`; `ToolDefinitionsTests` and `SnapshotTests` passed.

- [ ] **Step 9: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`.

- [ ] **Step 10: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): generate tool and prompt definitions with schema snapshots" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7b: `ToolHandlers`, `PromptHandlers`, `Program` wiring, AOT publish

**Implementer:** Opus  **Reviewer:** Fable

**Phase 0 dependency:** §15 row 6 — handler registration API and delegate signatures exactly as recorded; if Task 0 step 4 approved specific IL warning suppressions, only those IDs may be suppressed, each with a justification comment.

**Goal:** `tools/list`, `tools/call`, `prompts/list`, `prompts/get` over stdio; Native AOT publish with zero trim/AOT warnings.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Tools/ToolArguments.cs`, `Tools/ToolHandlers.cs`, `dotnet/src/Ccusage.Mcp/Prompts/PromptHandlers.cs`
- Modify: `dotnet/src/Ccusage.Mcp/Program.cs`, `Json/McpJsonContext.cs` (add `ListSourcesResult`, `SourceInfo`), `dotnet/tests/FakeEngine/Program.cs`, `dotnet/AGENTS.md`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Tools/ToolHandlersTests.cs`, `Prompts/PromptHandlersTests.cs`

**Interfaces:**
- Consumes: Tasks 2–7a.
- Produces:

```csharp
public static class ToolArguments
{   public static CommandRequest FromTyped(CommandCatalog catalog, string tool, IReadOnlyDictionary<string, JsonElement>? args); } // throws ValidationException
public sealed class ToolHandlers(CommandCatalog catalog, EngineLocator locator, EngineRunner runner)
{
    public ValueTask<ListToolsResult> ListAsync(CancellationToken ct);
    public ValueTask<CallToolResult> CallAsync(string name, IReadOnlyDictionary<string, JsonElement>? args, CancellationToken ct);
}
public sealed class PromptHandlers(CommandCatalog catalog, EngineLocator locator, EngineRunner runner)
{
    public ValueTask<ListPromptsResult> ListAsync(CancellationToken ct);
    public ValueTask<GetPromptResult> GetAsync(string name, IReadOnlyDictionary<string, JsonElement>? args, CancellationToken ct);
}
public sealed record SourceInfo(string Id, string DisplayName, IReadOnlyList<string> Reports);                    // JSON names id, display-name, reports
public sealed record ListSourcesResult(string ServerVersion, string? EngineVersion, string? EnginePath, string? EngineError, IReadOnlyList<SourceInfo> Sources); // server-version, engine-version, engine-path, engine-error (omitted when null), sources
```

`FromTyped`: unknown tool → `Unknown tool '{name}'.`; `source` string (default `all`) for the four report tools; `blocks`/`statusline` → source `claude`; property mapping: JSON `true/false` → `"true"/"false"`, number → `GetRawText()`, string → itself, `null` → ignored, other → `Parameter '{p}' has an unsupported JSON type ({kind}).`; `hook-input`: missing → `Parameter 'hook-input' is required.`, non-object → `Parameter 'hook-input' must be a JSON object.`, object → `Stdin = GetRawText()`; unknown properties are passed to `CommandValidator` (which rejects them with the valid list).

`CallAsync`: success → `Content[0] = TextContentBlock(Stdout)`; `Stderr` non-empty → `Content[1] = TextContentBlock("stderr:\n" + Stderr)`; `ValidationException` → `IsError = true`, text = message (engine not started); `EngineNotFoundException` → `IsError`, its message; non-zero exit → `IsError`, `ccusage exited with code {n}.\n{Stderr}`; timeout → `IsError`, `ccusage timed out after {seconds} s.\n{Stderr}`; `list-sources` → indented `ListSourcesResult` JSON (`engine-version` = `--version` stdout minus `ccusage ` prefix; engine missing → `engine-version`/`engine-path` null, `engine-error` = exception message, not `IsError`); `report` → `CliArgsParser.Parse` then the same path.

`GetAsync("ccusage")`: `args.args` (missing → `""`); success message role `user`, text exactly `ccusage {rendered}\n\n```json\n{stdout}\n```\n\nPresent this report as a readable table with totals, and call out the main cost drivers (the models, days, or sessions with the highest cost).` where `{rendered}` is the trimmed input, or `daily` when empty; error → `ccusage {rendered} failed:\n\n{error text}`; other prompt names → `McpException("Unknown prompt '{name}'.")`.

FakeEngine additions (document in `dotnet/AGENTS.md`): argv contains `--debug` → also writes `debug: fake` to stderr; `--project` followed by `fail` → exit 4, stderr `boom`; `--project` followed by `plain` → stdout `not json\n`; `--id` followed by `hang` → hang after stdout.

- [ ] **Step 1: Write the failing handler tests** in `Tools/ToolHandlersTests.cs`

```csharp
[TestClass]
public sealed class ToolHandlersTests
{
    // Handlers(enginePath = TestPaths.FakeEnginePath, timeoutSeconds = 10, path = ""): new ToolHandlers(CommandCatalog.Instance,
    //     new EngineLocator(k => k == "CCUSAGE_ENGINE" ? enginePath : k == "PATH" ? path : null, Directory.CreateTempSubdirectory().FullName, Architecture.X64, OperatingSystem.IsWindows()),
    //     new EngineRunner(TimeSpan.FromSeconds(timeoutSeconds), _ => { }))
    // Args(json): JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value)
    // Text(result, i): ((TextContentBlock)result.Content[i]).Text ; Argv(result): JSON "argv" of Text(result, 0) as string[]

    [TestMethod] public async Task ListAsync_ReturnsDefinitions() => Assert.AreEqual(8, (await Handlers().ListAsync(default)).Tools.Count);

    [TestMethod] public async Task CallAsync_Daily_BuildsArgvAndPassesStdout()
    { var r = await Handlers().CallAsync("daily", Args("""{"source":"codex","since":"20260101","offline":true}"""), default);
      Assert.AreNotEqual(true, r.IsError); Assert.AreEqual(1, r.Content.Count); CollectionAssert.AreEqual(new[] { "codex", "daily", "--json", "--since", "20260101", "--offline" }, Argv(r)); }

    [TestMethod] public async Task CallAsync_Blocks_NumberArgument() =>
        CollectionAssert.AreEqual(new[] { "blocks", "--json", "--token-limit", "500000" }, Argv(await Handlers().CallAsync("blocks", Args("""{"token-limit":500000}"""), default)));

    [TestMethod] public async Task CallAsync_Statusline_WritesStdin()
    { var r = await Handlers().CallAsync("statusline", Args("""{"hook-input":{"session_id":"s"},"offline":true}"""), default);
      CollectionAssert.AreEqual(new[] { "statusline", "--offline" }, Argv(r)); Assert.AreEqual("""{"session_id":"s"}""", JsonDocument.Parse(Text(r, 0)).RootElement.GetProperty("stdin").GetString()); }

    [DataTestMethod]
    [DataRow("""{"hook-input":"x"}""", "Parameter 'hook-input' must be a JSON object.")]
    [DataRow("""{"hook-input":[1]}""", "Parameter 'hook-input' must be a JSON object.")]
    [DataRow("""{"offline":true}""", "Parameter 'hook-input' is required.")]
    public async Task CallAsync_Statusline_RejectsNonObjectHookInput(string json, string msg)
    { var r = await Handlers().CallAsync("statusline", Args(json), default); Assert.AreEqual(true, r.IsError); Assert.AreEqual(msg, Text(r, 0)); }

    [TestMethod] public async Task CallAsync_ValidationError_IsError()
    { var r = await Handlers().CallAsync("daily", Args("""{"source":"codex","speed":"turbo"}"""), default); Assert.AreEqual(true, r.IsError); StringAssert.StartsWith(Text(r, 0), "Parameter 'speed' must be one of: auto, standard, flex, fast"); }

    [TestMethod] public async Task CallAsync_UnknownParameter_IsError()
    { var r = await Handlers().CallAsync("blocks", Args("""{"source":"all"}"""), default); Assert.AreEqual(true, r.IsError); StringAssert.StartsWith(Text(r, 0), "Parameter 'source' is not a parameter of 'claude blocks'. Valid parameters: "); }

    [TestMethod] public async Task CallAsync_UnknownTool_IsError()
    { var r = await Handlers().CallAsync("nope", null, default); Assert.AreEqual(true, r.IsError); Assert.AreEqual("Unknown tool 'nope'.", Text(r, 0)); }

    [TestMethod] public async Task CallAsync_StderrBecomesSecondBlock()
    { var r = await Handlers().CallAsync("daily", Args("""{"source":"claude","debug":true}"""), default); Assert.AreNotEqual(true, r.IsError); Assert.AreEqual(2, r.Content.Count); StringAssert.StartsWith(Text(r, 1), "stderr:\n"); StringAssert.Contains(Text(r, 1), "debug: fake"); }

    [TestMethod] public async Task CallAsync_NonZeroExit_IsError()
    { var r = await Handlers().CallAsync("daily", Args("""{"source":"claude","project":"fail"}"""), default); Assert.AreEqual(true, r.IsError); StringAssert.StartsWith(Text(r, 0), "ccusage exited with code 4.\nboom"); }

    [TestMethod] public async Task CallAsync_Timeout_IsError()
    { var r = await Handlers(timeoutSeconds: 1).CallAsync("session", Args("""{"source":"claude","id":"hang"}"""), default); Assert.AreEqual(true, r.IsError); StringAssert.StartsWith(Text(r, 0), "ccusage timed out after 1 s."); }

    [TestMethod] public async Task CallAsync_EngineMissing_IsError_ListsPaths()
    { var r = await Handlers(enginePath: Path.Combine(Path.GetTempPath(), "missing-engine")).CallAsync("daily", null, default); Assert.AreEqual(true, r.IsError); StringAssert.Contains(Text(r, 0), "Checked:"); }

    [TestMethod] public async Task CallAsync_PassesNonJsonStdoutUnchanged()
    { var r = await Handlers().CallAsync("daily", Args("""{"source":"claude","project":"plain"}"""), default); Assert.AreNotEqual(true, r.IsError); Assert.AreEqual("not json\n", Text(r, 0)); }

    [TestMethod] public async Task CallAsync_ListSources_Shape()
    { var r = await Handlers().CallAsync("list-sources", null, default); var j = JsonDocument.Parse(Text(r, 0)).RootElement;
      Assert.AreEqual(ServerVersion.Value, j.GetProperty("server-version").GetString()); Assert.AreEqual("0.0.0-fake", j.GetProperty("engine-version").GetString());
      Assert.AreEqual(TestPaths.FakeEnginePath, j.GetProperty("engine-path").GetString()); Assert.IsFalse(j.TryGetProperty("engine-error", out _));
      var s = j.GetProperty("sources"); Assert.AreEqual("all", s[0].GetProperty("id").GetString()); Assert.AreEqual("Claude Code", s[1].GetProperty("display-name").GetString());
      CollectionAssert.AreEqual(new[] { "daily", "monthly", "weekly", "session", "blocks", "statusline" }, s[1].GetProperty("reports").EnumerateArray().Select(x => x.GetString()).ToArray()); }

    [TestMethod] public async Task CallAsync_ListSources_EngineMissing()
    { var r = await Handlers(enginePath: Path.Combine(Path.GetTempPath(), "missing-engine")).CallAsync("list-sources", null, default); var j = JsonDocument.Parse(Text(r, 0)).RootElement;
      Assert.AreNotEqual(true, r.IsError); Assert.AreEqual(JsonValueKind.Null, j.GetProperty("engine-version").ValueKind); StringAssert.Contains(j.GetProperty("engine-error").GetString(), "Checked:"); }

    [TestMethod] public async Task CallAsync_Report_UsesParser()
    { CollectionAssert.AreEqual(new[] { "weekly", "--json", "--last", "2" }, Argv(await Handlers().CallAsync("report", Args("""{"args":"weekly --last 2"}"""), default)));
      var bad = await Handlers().CallAsync("report", Args("""{"args":"statusline"}"""), default); Assert.AreEqual(true, bad.IsError); Assert.AreEqual("'statusline' needs a hook payload; use the statusline tool.", Text(bad, 0)); }
}
```

- [ ] **Step 2: Write the failing prompt tests** in `Prompts/PromptHandlersTests.cs`

```csharp
[TestClass]
public sealed class PromptHandlersTests
{
    // Prompts(): PromptHandlers built like Handlers() in ToolHandlersTests; Text(result): ((TextContentBlock)result.Messages.Single().Content).Text

    [TestMethod] public async Task ListAsync_ReturnsCcusagePrompt() => Assert.AreEqual("ccusage", (await Prompts().ListAsync(default)).Prompts.Single().Name);

    [TestMethod] public async Task GetAsync_Success_FormatsMessage()
    { var r = await Prompts().GetAsync("ccusage", Args("""{"args":"codex daily"}"""), default); Assert.AreEqual(Role.User, r.Messages.Single().Role);
      StringAssert.StartsWith(Text(r), "ccusage codex daily\n\n```json\n{\"argv\":"); StringAssert.EndsWith(Text(r), "\n```\n\nPresent this report as a readable table with totals, and call out the main cost drivers (the models, days, or sessions with the highest cost)."); }

    [TestMethod] public async Task GetAsync_EmptyArgs_RendersDaily() => StringAssert.StartsWith(Text(await Prompts().GetAsync("ccusage", null, default)), "ccusage daily\n\n");

    [TestMethod] public async Task GetAsync_Error_FormatsFailure() =>
        StringAssert.StartsWith(Text(await Prompts().GetAsync("ccusage", Args("""{"args":"codex weekly"}"""), default)), "ccusage codex weekly failed:\n\nReport 'weekly' is not available for source 'codex'.");

    [TestMethod] public async Task GetAsync_UnknownPrompt_Throws() =>
        Assert.AreEqual("Unknown prompt 'x'.", (await Assert.ThrowsExactlyAsync<McpException>(() => Prompts().GetAsync("x", null, default).AsTask())).Message);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: CS0246 `'ToolHandlers'` / `'PromptHandlers'` / `'ToolArguments'` not found.

- [ ] **Step 4: Extend FakeEngine** in `dotnet/tests/FakeEngine/Program.cs` with the four rules above; add them to `dotnet/AGENTS.md`.

- [ ] **Step 5: Add `SourceInfo` and `ListSourcesResult`** to `Json/McpJsonContext.cs` with `[JsonPropertyName]`s and `[JsonSerializable(typeof(ListSourcesResult))]`; `JsonIgnoreCondition.WhenWritingNull` on `EngineError`.

- [ ] **Step 6: Implement `ToolArguments.FromTyped`** in `Tools/ToolArguments.cs`.

- [ ] **Step 7: Implement `ToolHandlers`** in `Tools/ToolHandlers.cs` — `ListAsync` returns `ToolDefinitions.Build(catalog)`; `CallAsync` per the rules; a private `RunAsync(CommandRequest, ct)` shared with `PromptHandlers` via an `internal static` helper `EngineCall.ExecuteAsync(catalog, locator, runner, request, ct) → (string? text, string? error)` in the same file.

- [ ] **Step 8: Implement `PromptHandlers`** in `Prompts/PromptHandlers.cs`.

- [ ] **Step 9: Wire `Program.cs`**

Keep the `--version` branch. Then `var builder = Host.CreateApplicationBuilder(args); builder.Logging.ClearProviders(); builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);` register singletons `CommandCatalog.Instance`, `EngineLocator.Default`, `new EngineRunner(EngineRunner.TimeoutFromEnvironment(Environment.GetEnvironmentVariable, Console.Error.WriteLine), Console.Error.WriteLine)`, `ToolHandlers`, `PromptHandlers`; `builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "ccusage", Version = ServerVersion.Value }).WithStdioServerTransport()` plus the four handler registrations from §15 row 6 delegating to `ToolHandlers.ListAsync/CallAsync` and `PromptHandlers.ListAsync/GetAsync` using `request.Params?.Name` and `request.Params?.Arguments`; `await builder.Build().RunAsync(); return 0;`.

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`; all `ToolHandlersTests` and `PromptHandlersTests` passed.

- [ ] **Step 11: Build Release with 0 warnings** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`. Expected: `0 Warning(s)`.

- [ ] **Step 12: AOT publish** [local if §15 says AOT works locally; otherwise CI only in Task 11b]

Run: `dotnet publish dotnet/src/Ccusage.Mcp -c Release -r win-x64 2>&1 | tee publish.log; grep -c "warning IL" publish.log`. Expected: `0`; `dotnet/src/Ccusage.Mcp/bin/Release/net10.0/win-x64/publish/ccusage-mcp.exe` exists. Delete `publish.log`.

- [ ] **Step 13: Stdio smoke** [local, same condition]

Run: `printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"0"}}}' | dotnet/src/Ccusage.Mcp/bin/Release/net10.0/win-x64/publish/ccusage-mcp.exe`. Expected: exactly one line on stdout, a JSON-RPC result with `"serverInfo":{"name":"ccusage","version":"20.0.45"}`.

- [ ] **Step 14: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): wire MCP tool and prompt handlers over stdio with AOT publish" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Fixtures copy and end-to-end parity tests with the real engine

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Copy the upstream fixtures to `dotnet/tests/fixtures/` and drive the published server over stdio with the MCP SDK client, asserting pass-through parity with the engine (§10.3).

**Files:**
- Create: `dotnet/tests/fixtures/claude/**`, `dotnet/tests/fixtures/codex/**`, `dotnet/tests/fixtures/statusline/*.json`, `dotnet/tests/fixtures/statusline/test-transcript.jsonl` (copies), `dotnet/tests/Ccusage.Mcp.Tests/E2E/E2eEnvironment.cs`, `E2E/EndToEndTests.cs`
- Modify: `dotnet/tests/Ccusage.Mcp.Tests/Ccusage.Mcp.Tests.csproj` (`AssemblyMetadata` `FixturesDir` = `$(MSBuildThisFileDirectory)../fixtures`), `dotnet/AGENTS.md`

**Interfaces:**
- Consumes: the published server (env `CCUSAGE_MCP_E2E_SERVER`), the engine (env `CCUSAGE_ENGINE`), the SDK client (`McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions { Command, EnvironmentVariables }))`; names per §15 row 6), `CommandValidator`, `CliArgsParser`, `EngineRunner`.
- Produces: `internal static class E2eEnvironment { public static E2eContext RequireOrInconclusive(); }` with `record E2eContext(string ServerPath, string EnginePath, string FixturesDir, Dictionary<string, string?> Env)` — `Assert.Inconclusive("Set CCUSAGE_MCP_E2E_SERVER and CCUSAGE_ENGINE to run e2e tests.")` when either variable is unset. `Env` = `CLAUDE_CONFIG_DIR=<fixtures>/claude`, `CODEX_HOME=<fixtures>/codex`, `HOME` and `USERPROFILE` = a fresh temp dir, `NO_COLOR=1`, `LOG_LEVEL=0`, `CCUSAGE_ENGINE=<EnginePath>` — applied to both the server process and direct engine runs.

- [ ] **Step 1: Copy the fixtures**

```bash
mkdir -p dotnet/tests/fixtures/statusline
cp -r apps/ccusage/test/fixtures/claude dotnet/tests/fixtures/claude
cp -r apps/ccusage/test/fixtures/codex dotnet/tests/fixtures/codex
cp apps/ccusage/test/statusline-test*.json apps/ccusage/test/test-transcript.jsonl dotnet/tests/fixtures/statusline/
```

Expected: `find dotnet/tests/fixtures -type f | wc -l` prints `9`; `diff -r apps/ccusage/test/fixtures/claude dotnet/tests/fixtures/claude` is silent.

- [ ] **Step 2: Commit the copy**

```bash
git add dotnet/tests/fixtures
git commit -m "test(mcp): copy engine fixtures for end-to-end tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: Write the failing e2e tests** in `E2E/EndToEndTests.cs`

```csharp
[TestClass]
public sealed class EndToEndTests
{
    // Ctx: E2eEnvironment.RequireOrInconclusive() in [TestInitialize]
    // Client(): McpClient over StdioClientTransport { Command = Ctx.ServerPath, EnvironmentVariables = Ctx.Env }
    // Direct(argv, stdin = null): EngineRunner(120 s).RunAsync(new(Ctx.EnginePath, argv, stdin)) with Ctx.Env applied to the process
    // Text(result): ((TextContentBlock)result.Content[0]).Text

    [TestMethod] public async Task Initialize_ReportsServerInfo()
    { await using var c = await Client(); Assert.AreEqual("ccusage", c.ServerInfo.Name); Assert.AreEqual(ServerVersion.Value, c.ServerInfo.Version); }

    [TestMethod] public async Task ToolsList_MatchesDefinitions()
    { await using var c = await Client(); CollectionAssert.AreEqual(ToolDefinitions.Build(CommandCatalog.Instance).Select(t => t.Name).ToArray(), (await c.ListToolsAsync()).Select(t => t.Name).ToArray()); }

    [DataTestMethod]
    [DataRow("daily", """{"source":"claude","offline":true}""")]
    [DataRow("daily", """{"source":"codex","offline":true}""")]
    [DataRow("monthly", """{"source":"claude","offline":true,"breakdown":true}""")]
    [DataRow("session", """{"source":"claude","offline":true}""")]
    [DataRow("weekly", """{"source":"claude","offline":true,"start-of-week":"monday"}""")]
    [DataRow("daily", """{"offline":true}""")]
    [DataRow("blocks", """{"offline":true}""")]
    public async Task TypedTool_EqualsDirectEngineOutput(string tool, string json)
    { await using var c = await Client(); var args = Args(json); var r = await c.CallToolAsync(tool, args.ToDictionary(k => k.Key, k => (object?)k.Value));
      var argv = CommandValidator.ValidateAndBuildArgv(CommandCatalog.Instance, ToolArguments.FromTyped(CommandCatalog.Instance, tool, args));
      var direct = await Direct(argv); Assert.AreEqual(0, direct.ExitCode, direct.Stderr); Assert.AreNotEqual(true, r.IsError); Assert.AreEqual(direct.Stdout, Text(r));
      if (tool == "daily" && json == """{"offline":true}""") Console.WriteLine($"daily(all) bytes: {direct.Stdout.Length}"); }

    [TestMethod] public async Task ReportTool_EqualsDirectEngineOutput()
    { await using var c = await Client(); var r = await c.CallToolAsync("report", new Dictionary<string, object?> { ["args"] = "codex daily --offline" });
      var direct = await Direct(CommandValidator.ValidateAndBuildArgv(CommandCatalog.Instance, CliArgsParser.Parse(CommandCatalog.Instance, "codex daily --offline"))); Assert.AreEqual(direct.Stdout, Text(r)); }

    [TestMethod] public async Task Statusline_ReturnsEngineText()
    { var payload = File.ReadAllText(Path.Combine(Ctx.FixturesDir, "statusline", "statusline-test.json")); await using var c = await Client();
      var r = await c.CallToolAsync("statusline", new Dictionary<string, object?> { ["hook-input"] = JsonDocument.Parse(payload).RootElement, ["offline"] = true });
      var direct = await Direct(["statusline", "--offline"], payload); Assert.AreEqual(direct.Stdout, Text(r)); }

    [TestMethod] public async Task Prompt_Get_ContainsEngineJson()
    { await using var c = await Client(); var p = await c.GetPromptAsync("ccusage", new Dictionary<string, object?> { ["args"] = "claude daily --offline" });
      var direct = await Direct(["claude", "daily", "--json", "--offline"]); StringAssert.Contains(((TextContentBlock)p.Messages.Single().Content).Text, direct.Stdout); }

    [TestMethod] public async Task ValidationError_IsError()
    { await using var c = await Client(); var r = await c.CallToolAsync("daily", new Dictionary<string, object?> { ["source"] = "codex", ["speed"] = "turbo" }); Assert.AreEqual(true, r.IsError); }
}
```

- [ ] **Step 4: Run the tests without the env vars**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: build error CS0103 `'E2eEnvironment'` does not exist (the remaining tests are untouched).

- [ ] **Step 5: Implement `E2eEnvironment`** in `E2E/E2eEnvironment.cs` per Interfaces.

- [ ] **Step 6: Run the tests without the env vars**

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`; every `EndToEndTests` method reported as skipped/inconclusive with the message `Set CCUSAGE_MCP_E2E_SERVER and CCUSAGE_ENGINE to run e2e tests.`.

- [ ] **Step 7: Build the engine and publish the server** [local]

Run: `cargo build --manifest-path rust/Cargo.toml --release --bin ccusage --features fetch-litellm-pricing` and `dotnet publish dotnet/src/Ccusage.Mcp -c Release -r win-x64`. Expected: both binaries exist. (If AOT is CI-only here, use `dotnet publish dotnet/src/Ccusage.Mcp -c Release -r win-x64 -p:PublishAot=false --self-contained` for the local run — the e2e behavior is identical; the AOT build is exercised in CI.)

- [ ] **Step 8: Run the e2e tests with the env vars** [local]

Run: `CCUSAGE_MCP_E2E_SERVER=$(pwd)/dotnet/src/Ccusage.Mcp/bin/Release/net10.0/win-x64/publish/ccusage-mcp.exe CCUSAGE_ENGINE=$(pwd)/rust/target/release/ccusage.exe dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: `Failed: 0`, no inconclusive results, and a `daily(all) bytes: N` line in the output. Fix `E2eEnvironment` / handlers until this holds.

- [ ] **Step 9: Record the e2e setup in `dotnet/AGENTS.md`** — the two env vars, the step 7 commands, and the observed `daily(all) bytes` value.

- [ ] **Step 10: Commit**

```bash
git add dotnet
git commit -m "test(mcp): add end-to-end parity tests against the real engine" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Plugin, skill and marketplace

**Implementer:** Opus  **Reviewer:** Opus

**Phase 0 dependency:** §15 row 7 (placement of `mcpServers`); Task 0 step 18's answer (Q3 reconnect sentence); Task 0 step 16's answer (one or four plugin entries).

**Goal:** Metadata for `/plugin marketplace add Desulation/ccusage` + `/plugin install ccusage@ccusage`, and the `/ccusage` skill, validated with `claude plugin validate --strict`.

**Files:**
- Create: `.claude-plugin/marketplace.json`, `plugin/.claude-plugin/plugin.json`, `plugin/skills/ccusage/SKILL.md`

- [ ] **Step 1: Write `.claude-plugin/marketplace.json`**

§7.1 JSON with `<version>` → `20.0.45` (`https://github.com/Desulation/ccusage/releases/download/v20.0.45/ccusage.mcpb`), `mcpServers` placed per §15 row 7.
Expected: `python -m json.tool .claude-plugin/marketplace.json` succeeds; `grep -c "v20.0.45/ccusage.mcpb" .claude-plugin/marketplace.json` prints `1`.

- [ ] **Step 2: Write `plugin/.claude-plugin/plugin.json`**

`{"name":"ccusage","version":"20.0.45","description":"Coding-agent CLI token usage and cost reports over MCP","author":{"name":"Desulation"},"repository":"https://github.com/Desulation/ccusage","license":"MIT"}` plus `mcpServers` if §15 row 7 puts it here.
Expected: valid JSON with `version` `20.0.45`.

- [ ] **Step 3: Write `plugin/skills/ccusage/SKILL.md`**

Frontmatter: `name: ccusage`; `description: Show coding-agent token usage and cost reports (Claude Code, Codex, OpenCode and 15 more) by day, week, month, session or billing block through the ccusage MCP server.`; `argument-hint: "[source] [report] [--flags]"`; `allowed-tools: mcp__plugin_ccusage_ccusage__report`. Body: (1) call the `report` tool with `args` set to exactly `$ARGUMENTS` — an empty string means daily across all sources; (2) present the returned JSON as a readable table with totals and the top cost drivers; (3) on an error result, show the error text and the valid choices it lists; (4) examples `/ccusage claude daily`, `/ccusage codex monthly`, `/ccusage weekly --last 2`; (5) only if Task 0 step 18 answered A: "If the tool is unavailable right after installation, run `/mcp` once to reconnect."
Expected: file starts with `---`, has the four frontmatter keys, and contains `$ARGUMENTS`.

- [ ] **Step 4: Validate the plugin** [local]

Run: `claude plugin validate --strict plugin`. Expected: success, no warnings.

- [ ] **Step 5: Validate the marketplace** [local]

Run the marketplace form recorded in §15 row 7 (e.g. `claude plugin validate --strict .`). Expected: success, no warnings.

- [ ] **Step 6: Commit**

```bash
git add .claude-plugin plugin
git commit -m "feat(plugin): add Claude Code plugin, /ccusage skill and marketplace" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Bundle packaging (manifest template, `launch.sh`, assemble/verify/smoke scripts)

**Implementer:** Opus  **Reviewer:** Fable

**Phase 0 dependency:** §15 rows 1–2 and Task 0 steps 16–17 (manifest form); row 4 (`launch.sh` keeps the `chmod +x` restore either way).

**Goal:** Scripts that turn the six RID artifacts into `ccusage.mcpb`, six archives and `SHA256SUMS`, verify the layout, and smoke-test a bundle through its manifest command on each OS, including the Q4/Q5 CI checks (§16.15).

**Files:**
- Create: `dotnet/packaging/manifest.template.json`, `dotnet/packaging/launch.sh`, `dotnet/packaging/assemble-bundle.sh`, `dotnet/packaging/verify-bundle.sh`, `dotnet/packaging/bundle-smoke.ps1`

**Interfaces (produces):**
- `assemble-bundle.sh --version <v> --artifacts <dir> --out <dir>`: input layout `<dir>/ccusage-mcp-<rid>/ccusage-mcp[.exe]` for `linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64` and `<dir>/ccusage-<rid>/ccusage[.exe]` for all six RIDs (Task 11a artifact names). Output: `<out>/ccusage.mcpb` (layout §7.3; `manifest.json` = template with `__VERSION__` → `<v>`; `chmod +x` on every non-Windows binary; `zip -X -r` so unix modes are stored), `<out>/ccusage-mcp-<rid>.tar.gz` for linux/osx (`ccusage-mcp`, `ccusage`, `LICENSE`), `<out>/ccusage-mcp-win-x64.zip` and `<out>/ccusage-mcp-win-arm64.zip` (`ccusage-mcp.exe` = the win-x64 server, `ccusage.exe`, `LICENSE`), `<out>/SHA256SUMS` (`sha256sum` format, basenames). Exit 1 naming the first missing input.
- `verify-bundle.sh <ccusage.mcpb> --version <v>`: unzip to a temp dir; assert the exact §7.3 file list (13 files, nothing else); `jq -e` checks `.manifest_version == "0.3"`, `.name == "ccusage"`, `.version == "<v>"`, `.server.type == "binary"`, `.server.entry_point == "launch.sh"`, `.server.mcp_config.command == "/bin/sh"`, `.server.mcp_config.args == ["${__dirname}/launch.sh"]`, `.server.mcp_config.platform_overrides.win32.command == "${__dirname}/win/ccusage-mcp.exe"`, `.compatibility.platforms == ["darwin","linux","win32"]`; every `linux/*/*` and `osx/*/*` entry has `x` in `zipinfo -l` mode; `sh -n launch.sh` parses. Exit 1 naming the first failing check.
- `bundle-smoke.ps1 -Bundle <ccusage.mcpb> -FixturesDir <dir> -ExpectEnginePath <relative path>`: extract (`unzip` on Linux/macOS so modes survive; `Expand-Archive` on Windows); on Linux/macOS assert `(Get-Item <os>/<arch>/ccusage-mcp).UnixMode` and the engine's contain `x` (Q4 check); read `manifest.json`; pick `mcp_config.platform_overrides.win32` when `$IsWindows`, else the default `command`/`args`; replace `${__dirname}` with the extraction dir; start the process with env `CLAUDE_CONFIG_DIR=<fixtures>/claude`, `HOME`/`USERPROFILE` = temp; send newline-delimited JSON-RPC `initialize`, `notifications/initialized`, `tools/list`, `tools/call list-sources`, `tools/call daily {"source":"claude","offline":true}`; assert `serverInfo.name == "ccusage"`, 8 tools, `list-sources` text parses with non-null `engine-version` and `engine-path` ending in `-ExpectEnginePath` (Q5 check: `win\arm64\ccusage.exe` on Windows ARM64), `daily` result not `isError` and block 0 parses as JSON; print `bundle-smoke: ok`, exit 0; otherwise print the failing step, exit 1.
- `launch.sh`: `#!/bin/sh`, `set -eu`; `uname -s` `Linux`→`linux`, `Darwin`→`osx`, else `echo "ccusage: unsupported OS: $(uname -s)" >&2; exit 1`; `uname -m` `x86_64|amd64`→`x64`, `aarch64|arm64`→`arm64`, else error exit 1; `dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)`; `target="$dir/$os/$arch/ccusage-mcp"`; missing → `echo "ccusage: missing $target" >&2; exit 1`; `[ -x "$target" ] || chmod +x "$target" "$dir/$os/$arch/ccusage"`; `exec "$target" "$@"`.
- `manifest.template.json`: §7.3 JSON with `"version": "__VERSION__"`.

- [ ] **Step 1: Write `manifest.template.json`.** Expected: `python -m json.tool` succeeds; contains `__VERSION__` once.

- [ ] **Step 2: Write `launch.sh`.** Expected: `sh -n dotnet/packaging/launch.sh` exits 0.

- [ ] **Step 3: Write `assemble-bundle.sh`.** Expected: `bash -n` exits 0.

- [ ] **Step 4: Write `verify-bundle.sh`.** Expected: `bash -n` exits 0.

- [ ] **Step 5: Write `bundle-smoke.ps1`.** Expected: `pwsh -NoProfile -Command "[scriptblock]::Create((Get-Content -Raw dotnet/packaging/bundle-smoke.ps1)) | Out-Null"` exits 0 (parses).

- [ ] **Step 6: Build a fake artifacts directory** [local]

Script: for each of the six RIDs copy `dotnet/tests/FakeEngine/bin/Debug/net10.0/ccusage.exe` to `/tmp/arts/ccusage-<rid>/ccusage` (`.exe` for win RIDs); for the five server RIDs copy the published `ccusage-mcp.exe` to `/tmp/arts/ccusage-mcp-<rid>/ccusage-mcp` (`.exe` for win-x64).
Expected: `find /tmp/arts -type f | wc -l` prints `11`.

- [ ] **Step 7: Run assemble on the fake artifacts** [local]

Run: `dotnet/packaging/assemble-bundle.sh --version 20.0.45 --artifacts /tmp/arts --out /tmp/dist`. Expected: `ls /tmp/dist` lists `SHA256SUMS ccusage-mcp-linux-arm64.tar.gz ccusage-mcp-linux-x64.tar.gz ccusage-mcp-osx-arm64.tar.gz ccusage-mcp-osx-x64.tar.gz ccusage-mcp-win-arm64.zip ccusage-mcp-win-x64.zip ccusage.mcpb`; `sha256sum -c /tmp/dist/SHA256SUMS` (run inside `/tmp/dist`) prints `OK` for 7 files.

- [ ] **Step 8: Run verify (positive)** [local]

Run: `dotnet/packaging/verify-bundle.sh /tmp/dist/ccusage.mcpb --version 20.0.45`. Expected: exit 0, last line `verify-bundle: ok`.

- [ ] **Step 9: Run verify (negative)** [local]

Run: `cp /tmp/dist/ccusage.mcpb /tmp/broken.mcpb && zip -d /tmp/broken.mcpb manifest.json && dotnet/packaging/verify-bundle.sh /tmp/broken.mcpb --version 20.0.45; echo $?`. Expected: `1`, with output naming `manifest.json`.

- [ ] **Step 10: Build a real Windows bundle and smoke it** [local, Windows]

Replace the server copies in `/tmp/arts/ccusage-mcp-*/` with the real published `ccusage-mcp.exe` and the engine copies with `rust/target/release/ccusage.exe`; re-run step 7; then `pwsh dotnet/packaging/bundle-smoke.ps1 -Bundle /tmp/dist/ccusage.mcpb -FixturesDir dotnet/tests/fixtures -ExpectEnginePath 'win\x64\ccusage.exe'`.
Expected: last line `bundle-smoke: ok`. Linux/macOS legs and the ARM64 leg: **[CI only]** (Task 11b). Delete `/tmp/arts`, `/tmp/dist`, `/tmp/broken.mcpb`.

- [ ] **Step 11: Commit**

```bash
git add dotnet/packaging
git commit -m "feat(bundle): add .mcpb manifest template, launcher and packaging scripts" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11a: Composite actions (engine per RID, server publish, Blacksmith removal)

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** `.github/actions/build-*-native-package` upload the engine binary as `ccusage-<rid>` without npm staging; a new `publish-mcp-server` action publishes the AOT server as `ccusage-mcp-<rid>` and fails on any IL warning; `setup-nix-cache` no longer uses Blacksmith.

**Files:**
- Modify: `.github/actions/setup-nix-cache/action.yaml`, `.github/actions/build-linux-native-package/action.yaml`, `.github/actions/build-macos-nix-native-package/action.yaml`, `.github/actions/build-windows-native-package/action.yaml`
- Create: `.github/actions/publish-mcp-server/action.yaml`

- [ ] **Step 1: Remove Blacksmith from `setup-nix-cache`**

Delete the `Mount Nix store sticky disk` step (`useblacksmith/stickydisk`), the `Trim Nix store sticky disk` step, and the `sticky` output in the `backend` step (keep `commit`); make every `if: steps.backend.outputs.sticky != 'true'` step unconditional; drop the Blacksmith sentences from `description`. Keep `nix-quick-install-action` and `cache-nix-action` SHAs.
Expected: `grep -c "sticky\|blacksmith" .github/actions/setup-nix-cache/action.yaml` prints `0`.

- [ ] **Step 2: Rework the Linux engine action**

Add input `rid` (required). Replace the `Stage native package` and `Archive native package binary` steps with `Stage engine binary`: `mkdir -p "$RUNNER_TEMP/engine" && cp "$BINARY" "$RUNNER_TEMP/engine/ccusage"`. Upload step: `name: ccusage-${{ inputs.rid }}`, `path: ${{ runner.temp }}/engine/ccusage`. Leave the Nix cache rooting steps.
Expected: no `apps/ccusage` or `packages/` string remains in the file.

- [ ] **Step 3: Rework the macOS engine action** — same edits as step 2 (keep the `x64` cross-build branch; §16.11).
Expected: no `apps/ccusage` or `packages/` string remains.

- [ ] **Step 4: Rework the Windows engine action** — add `rid`; staging copies to `$RUNNER_TEMP/engine/ccusage.exe`; upload `ccusage-${{ inputs.rid }}` with `path: ${{ runner.temp }}/engine/ccusage.exe`.
Expected: no `packages/` string remains.

- [ ] **Step 5: Look up the `actions/setup-dotnet` SHA**

Run: `gh api repos/actions/setup-dotnet/releases/latest --jq .tag_name` then `gh api repos/actions/setup-dotnet/git/ref/tags/<tag> --jq .object.sha` (dereference an annotated tag with `gh api repos/actions/setup-dotnet/git/tags/<sha> --jq .object.sha` if `.object.type` is `tag`).
Expected: a 40-hex SHA and its tag, recorded for steps 6 and Task 11b.

- [ ] **Step 6: Write `publish-mcp-server/action.yaml`**

Composite; input `rid` (required). Steps: `actions/setup-dotnet@<sha> # <tag>` with `global-json-file: dotnet/global.json`; `shell: bash` run `dotnet publish dotnet/src/Ccusage.Mcp -c Release -r "$RID" -o "$RUNNER_TEMP/server" 2>&1 | tee "$RUNNER_TEMP/publish.log"` followed by `if grep -E 'warning IL[0-9]+' "$RUNNER_TEMP/publish.log"; then echo "::error::trim/AOT warnings"; exit 1; fi`; `rm -f "$RUNNER_TEMP"/server/*.pdb "$RUNNER_TEMP"/server/*.dbg; rm -rf "$RUNNER_TEMP"/server/*.dSYM`; `actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2` with `name: ccusage-mcp-${{ inputs.rid }}`, `path: ${{ runner.temp }}/server/ccusage-mcp*`, `if-no-files-found: error`.
Expected: file parses as YAML.

- [ ] **Step 7: Parse all action files** [local]

Run: `python -c "import yaml,glob;[yaml.safe_load(open(f)) for f in glob.glob('.github/actions/*/action.yaml')];print('ok')"` (skip if PyYAML is absent; actionlint/zizmor run in CI preflight **[CI only]**). Expected: `ok`.

- [ ] **Step 8: Commit**

```bash
git add .github/actions
git commit -m "ci: build engine artifacts per RID and publish the AOT server without npm staging" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11b: `build-artifacts.yaml` (reusable) and `ci.yaml` rewrite

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** CI per §9.2 on GitHub-hosted runners, plus §16.9 (`docs-build` with lockfile regeneration) and §16.15 (Q4/Q5 checks in `bundle-smoke`).

**Files:**
- Create: `.github/workflows/build-artifacts.yaml`
- Modify: `.github/workflows/ci.yaml`

- [ ] **Step 1: Write `build-artifacts.yaml` — `build` job**

`on: workflow_call: inputs: ref: {type: string, required: true}`. Job `build`: `permissions: {contents: read, actions: write}`, `strategy: {fail-fast: false, matrix: {include: [...]}}` with `{rid: linux-x64, os: ubuntu-latest, engine: nix-static, arch: x64}`, `{rid: linux-arm64, os: ubuntu-24.04-arm, engine: nix-static, arch: arm64}`, `{rid: osx-arm64, os: macos-latest, engine: nix, arch: arm64}`, `{rid: osx-x64, os: macos-15-intel, engine: cargo-mac}`, `{rid: win-x64, os: windows-latest, engine: cargo-win, arch: x64}`, `{rid: win-arm64, os: windows-11-arm, engine: cargo-win, arch: arm64, server: false}`. Steps: checkout `ref: ${{ inputs.ref }}`, `persist-credentials: false`; `./.github/actions/build-linux-native-package` (`if: matrix.engine == 'nix-static'`, inputs `rid`, `arch`, `binary: result/bin/ccusage`, `cache-scope: ${{ matrix.rid }}`, `nix-github-token: ${{ github.event_name != 'pull_request' }}`); `./.github/actions/build-macos-nix-native-package` (`if: matrix.engine == 'nix'`, `arch: arm64`, `binary: result/bin/ccusage`, `cache-scope`, `rid`); `./.github/actions/build-windows-native-package` (`if: matrix.engine == 'cargo-win'`, `arch`, `binary: rust/target/release/ccusage.exe`, `rid`, `save-cargo-cache: ${{ matrix.arch == 'arm64' }}`); for `cargo-mac` three inline steps — `rustup toolchain install "$(sed -n 's/^channel *= *"\([^"]*\)".*/\1/p' rust-toolchain.toml)" --profile minimal --no-self-update` (echo the channel to `$GITHUB_OUTPUT`), `cargo build --manifest-path rust/Cargo.toml --release --bin ccusage --features fetch-litellm-pricing` with `RUSTUP_TOOLCHAIN`, `upload-artifact` `name: ccusage-osx-x64`, `path: rust/target/release/ccusage`; finally `./.github/actions/publish-mcp-server` with `rid` (`if: matrix.server != false`).
Expected: YAML parses; the matrix has six entries.

- [ ] **Step 2: Add the `bundle` job to `build-artifacts.yaml`**

`needs: build`, `runs-on: ubuntu-latest`, `permissions: contents: read`: checkout (`ref`, no credentials); `actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2` with `pattern: ccusage-*`, `path: ${{ runner.temp }}/artifacts` (no `merge-multiple`); `version="$(jq -r .version package.json)"`; `dotnet/packaging/assemble-bundle.sh --version "$version" --artifacts "$RUNNER_TEMP/artifacts" --out dist`; `dotnet/packaging/verify-bundle.sh dist/ccusage.mcpb --version "$version"`; `du -h dist/ccusage.mcpb | tee -a "$GITHUB_STEP_SUMMARY"`; upload `ccusage-release-assets` = `dist/*`.
Expected: YAML parses.

- [ ] **Step 3: Trim `ci.yaml` — kept jobs**

Change every `runs-on: blacksmith-*` to `ubuntu-latest` (keep `ubuntu-slim`). In `test`: keep checkout + `setup-nix-cache` + the `parallel:` block with only `Run nix flake check` and `Run Rust tests`; delete `JS test`, `Nushell contribution-gate tests`, `Babashka performance harness test`, and `Root test dependency layers for the sticky disk`. Delete jobs `build-native-packages`, `npm-publish-dry-run-and-upload-pkg-pr-now`, `ccusage-preview-e2e`, `ccusage-perf-comment`, `action-timeline` (§16.3). Keep `changes`, `tirith-scan`, `tirith-sarif-upload`, `preflight`, `release-dispatch-status-pending`, `release-dispatch-status`.
Expected: `grep -c "blacksmith\|Kesin11\|pkg.pr.new\|apps/ccusage" .github/workflows/ci.yaml` prints `0`.

- [ ] **Step 4: Add `dotnet-test`**

`needs: [changes, tirith-scan]`, `if: needs.changes.outputs.code-changed == 'true'`, matrix `os: [ubuntu-latest, windows-latest, macos-latest]`, `permissions: contents: read`; checkout; `actions/setup-dotnet@<sha from Task 11a step 5>` `global-json-file: dotnet/global.json`; `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`; `dotnet test dotnet/Ccusage.Mcp.slnx -c Release --no-build`.
Expected: YAML parses.

- [ ] **Step 5: Add `build-artifacts`, `e2e`, `bundle-smoke`**

`build-artifacts`: `needs: [changes, tirith-scan]`, `if: code-changed`, `uses: ./.github/workflows/build-artifacts.yaml`, `with: {ref: ${{ github.event.pull_request.head.sha || github.sha }}}`, `permissions: {contents: read, actions: write}`.
`e2e`: `needs: build-artifacts`, matrix `include` `{os: ubuntu-latest, rid: linux-x64, exe: ''}`, `{os: windows-latest, rid: win-x64, exe: .exe}`, `{os: macos-latest, rid: osx-arm64, exe: ''}`; checkout; setup-dotnet; download `ccusage-mcp-${{ matrix.rid }}` and `ccusage-${{ matrix.rid }}` to `${{ runner.temp }}/bin`; bash step `chmod +x "$RUNNER_TEMP"/bin/* || true; echo "CCUSAGE_MCP_E2E_SERVER=$RUNNER_TEMP/bin/ccusage-mcp${EXE}" >> "$GITHUB_ENV"; echo "CCUSAGE_ENGINE=$RUNNER_TEMP/bin/ccusage${EXE}" >> "$GITHUB_ENV"`; `dotnet test dotnet/Ccusage.Mcp.slnx -c Release`.
`bundle-smoke`: `needs: build-artifacts`, matrix `include` `{os: ubuntu-latest, expect: linux/x64/ccusage}`, `{os: ubuntu-24.04-arm, expect: linux/arm64/ccusage}`, `{os: macos-latest, expect: osx/arm64/ccusage}`, `{os: macos-15-intel, expect: osx/x64/ccusage}`, `{os: windows-latest, expect: 'win\x64\ccusage.exe'}`, `{os: windows-11-arm, expect: 'win\arm64\ccusage.exe'}` (the last leg is the Q5 check; the unix legs are the Q4 check); checkout; download `ccusage-release-assets` to `${{ runner.temp }}/assets`; `shell: pwsh` run `dotnet/packaging/bundle-smoke.ps1 -Bundle "$env:RUNNER_TEMP/assets/ccusage.mcpb" -FixturesDir dotnet/tests/fixtures -ExpectEnginePath '${{ matrix.expect }}'`.
Expected: YAML parses.

- [ ] **Step 6: Add `plugin-validate`, `version-consistency`, `docs-build`**

`plugin-validate` (`ubuntu-latest`): checkout; `curl -fsSL https://claude.ai/install.sh | bash -s 2.1.303` (§16.14); `"$HOME/.local/bin/claude" plugin validate --strict plugin`; the marketplace command from §15 row 7.
`version-consistency` (`ubuntu-latest`): bash with `jq`: `v=$(jq -r .version package.json)`; `d=$(jq -r .version docs/package.json)`; `p=$(jq -r .version plugin/.claude-plugin/plugin.json)`; `m=$(grep -o 'download/v[0-9][0-9.]*/ccusage.mcpb' .claude-plugin/marketplace.json | sed 's#download/v##; s#/ccusage.mcpb##')`; `b=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' dotnet/Directory.Build.props)`; fail with `::error::version mismatch: package.json=$v docs=$d plugin=$p marketplace=$m props=$b` unless all five equal.
`docs-build` (`needs: changes`, always runs, `ubuntu-latest`, `permissions: contents: read`): checkout; `setup-nix-cache`; `nix profile install --inputs-from . .#pnpm nixpkgs#nodejs nixpkgs#just`; `pnpm install --lockfile-only`; step `lockfile` — `if ! git diff --quiet -- pnpm-lock.yaml; then echo changed=true >> "$GITHUB_OUTPUT"; fi`; `upload-artifact` `name: pnpm-lock`, `path: pnpm-lock.yaml`, `if: steps.lockfile.outputs.changed == 'true'`; `pnpm install --frozen-lockfile`; `just docs::build`; final step `if: steps.lockfile.outputs.changed == 'true'` → `echo "::error::pnpm-lock.yaml is stale; download the pnpm-lock artifact and commit it"; exit 1` (§16.10).
Expected: YAML parses.

- [ ] **Step 7: Update `ci-gate`**

`needs`: `changes, tirith-scan, tirith-sarif-upload, preflight, test, dotnet-test, build-artifacts, e2e, bundle-smoke, plugin-validate, version-consistency, docs-build`. Triggers unchanged (`push main`, `pull_request`, `workflow_dispatch`).
Expected: `python -c "import yaml;yaml.safe_load(open('.github/workflows/ci.yaml'));yaml.safe_load(open('.github/workflows/build-artifacts.yaml'));print('ok')"` prints `ok`.

- [ ] **Step 8: Commit**

```bash
git add .github/workflows/build-artifacts.yaml .github/workflows/ci.yaml
git commit -m "ci: replace npm jobs with dotnet tests, build matrix, bundle, smoke and plugin validation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 9: DECISION — push the branch for CI-only verification**

Question: "Push `feat/csharp-mcp-server` to `origin` now so `ci.yaml` runs (Nix builds, six-RID matrix, e2e on three OSes, bundle-smoke incl. the Q4/Q5 legs, plugin-validate, docs-build)?" Options: (A) push now (`git push -u origin feat/csharp-mcp-server`; CI runs on `push` only for `main`, so also run `gh workflow run ci.yaml --ref feat/csharp-mcp-server`); (B) wait until Task 14a (one push covers CI verification and the lockfile artifact); (C) do not push. Recommendation: B — fewer CI runs on a tree still referencing `apps/ccusage`. A → push, then record the run URL and fix failures before Task 12; B → continue, push at Task 14a step 12; C → everything [CI only] stays unverified and is listed in Task 17 step 5. **STOP until the human answers.**

---

### Task 12: `release.yaml`

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** On a tagpr tag: build matrix → bundle → GitHub Release with `ccusage.mcpb`, six archives and `SHA256SUMS` via `gh release create` (§9.3; `nix/tools/changelogithub` stays in the tree unused, §16.4).

**Files:**
- Modify: `.github/workflows/release.yaml`

- [ ] **Step 1: Edit the `tagpr` and `ci` jobs** — `tagpr` `runs-on: ubuntu-latest`; keep `Songmu/tagpr@e1250d6c84e4425db82b5e90549d62dfe23e9ebe`; `ci` unchanged.
Expected: `grep -c blacksmith .github/workflows/release.yaml` prints `0`.

- [ ] **Step 2: Replace `build-native-packages` with `build-artifacts`** — `needs: tagpr`, `if: needs.tagpr.outputs.tag != ''`, `uses: ./.github/workflows/build-artifacts.yaml`, `with: {ref: ${{ needs.tagpr.outputs.tag }}}`, `permissions: {contents: read, actions: write}`.
Expected: no `build-*-native-package` `uses:` remains in the file.

- [ ] **Step 3: Delete the `npm` job.** Expected: `grep -c "pnpm\|npm-registry-gate\|publint" .github/workflows/release.yaml` prints `0`.

- [ ] **Step 4: Rewrite the `release` job**

`needs: [tagpr, build-artifacts]`, `if: needs.tagpr.outputs.tag != ''`, `runs-on: ubuntu-latest`, `permissions: contents: write`; checkout `ref: ${{ needs.tagpr.outputs.tag }}`, `persist-credentials: false`; `actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2` `name: ccusage-release-assets`, `path: dist`; `gh release create "$TAG" dist/* --title "$TAG" --generate-notes --verify-tag` with `env: {GH_TOKEN: ${{ github.token }}, TAG: ${{ needs.tagpr.outputs.tag }}}`. Remove `setup-nix-cache` and `nix run .#changelogithub`.
Expected: `python -c "import yaml;yaml.safe_load(open('.github/workflows/release.yaml'));print('ok')"` prints `ok`. Real release: **[CI only]**.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/release.yaml
git commit -m "ci(release): publish the mcpb bundle and archives with gh release create" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: `sync-upstream.yaml`

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** Weekly PR bringing `ccusage/ccusage` `main` into this fork (§9.4).

**Files:**
- Create: `.github/workflows/sync-upstream.yaml`

- [ ] **Step 1: Write the workflow**

`on: {schedule: [{cron: '0 6 * * 1'}], workflow_dispatch: {}}`; `permissions: {contents: write, pull-requests: write}`; `concurrency: sync-upstream`; job `sync` on `ubuntu-latest`: checkout `fetch-depth: 0` (credentials persisted, needed for the push); `git remote add upstream https://github.com/ccusage/ccusage.git && git fetch upstream main`; step `check`: `if git merge-base --is-ancestor upstream/main origin/main; then echo up_to_date=true >> "$GITHUB_OUTPUT"; fi`; the rest `if: steps.check.outputs.up_to_date != 'true'`: `git push --force origin upstream/main:refs/heads/sync/upstream`; `{ printf 'Incoming commits from ccusage/ccusage main:\n\n'; git log --oneline --no-decorate origin/main..upstream/main; } > "$RUNNER_TEMP/body.md"`; `n=$(gh pr list --head sync/upstream --base main --state open --json number -q '.[0].number')`; `if [ -n "$n" ]; then gh pr edit "$n" --body-file "$RUNNER_TEMP/body.md"; else gh pr create --base main --head sync/upstream --title 'chore(deps): sync upstream ccusage/ccusage' --body-file "$RUNNER_TEMP/body.md"; fi` with `GH_TOKEN: ${{ github.token }}`. Header comment: CI does not run automatically on this PR (GITHUB_TOKEN); run `gh workflow run ci.yaml --ref sync/upstream`.
Expected: YAML parses; `grep -c "push.*main\b" .github/workflows/sync-upstream.yaml` shows no push to `main` (only `refs/heads/sync/upstream`).

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/sync-upstream.yaml
git commit -m "ci: add weekly upstream sync pull request workflow" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14a: Remove the npm layer; move `config-schema.json`; slim the JS workspace; versioning

**Implementer:** Opus  **Reviewer:** Opus

**Prerequisites:** Task 8 committed the fixture copies; Tasks 11–12 removed every workflow reference to `apps/ccusage` and `packages/`.

**Goal:** §3.1 deletions and §3.2 edits for the npm packages, workspace, Nix references, justfile, schema location, `.tagpr`; §16.1 and §16.10.

**Files:**
- Delete: `apps/ccusage/` (after the moves), `packages/` (6 dirs), `.oxlintrc.json`, `.oxfmtrc.json`, `nix/tools/publint/`
- Move: `apps/ccusage/config-schema.json` → `config-schema.json`; `apps/ccusage/LICENSE` → `LICENSE`; `apps/ccusage/README.md` → `README.md` (both replacing symlinks; README rewritten in Task 16)
- Modify: `package.json`, `pnpm-workspace.yaml`, `package.nix`, `nix/checks.nix`, `nix/treefmt.nix`, `nix/git-hooks.nix`, `nix/packages.nix`, `nix/dev-shell.nix`, `justfile`, `ccusage.example.json`, `docs/package.json`, `docs/README.md`, `.tagpr`, `.gitignore`; `pnpm-lock.yaml` (kept, regenerated by CI)

- [ ] **Step 1: Replace the symlinks and move the schema**

```bash
git rm README.md LICENSE
git mv apps/ccusage/README.md README.md
git mv apps/ccusage/LICENSE LICENSE
git mv apps/ccusage/config-schema.json config-schema.json
```

Expected: `test -L README.md; echo $?` prints `1`; `head -1 LICENSE` is `MIT License`.

- [ ] **Step 2: Delete the npm layer**

```bash
git rm -r -q apps/ccusage packages .oxlintrc.json .oxfmtrc.json nix/tools/publint
rmdir apps
```

Expected: `ls apps packages 2>&1` reports both missing.

- [ ] **Step 3: Rewrite root `package.json`** (§16.1)

Content: `{"name":"ccusage","version":"20.0.45","description":"Analyze coding (agent) CLI token usage and costs from local data","homepage":"https://github.com/Desulation/ccusage#readme","license":"MIT","private":true,"workspaces":["docs"]}` — no `scripts`, `packageManager`, `type`, `dependencies`.
Expected: `python -m json.tool package.json` succeeds; `jq -r .name package.json` → `ccusage` (use Python if `jq` is absent).

- [ ] **Step 4: Edit `pnpm-workspace.yaml`** — `packages: [docs]`; delete the `types` catalog (`@types/node`, `@types/react`); keep everything else.
Expected: `grep -c "apps/\*\|packages/\*\|types:" pnpm-workspace.yaml` prints `0`.

- [ ] **Step 5: Edit `docs/package.json`** — scripts `"build": "cp ../config-schema.json public/config-schema.json && vitepress build"`, `"dev": "cp ../config-schema.json public/config-schema.json && vitepress dev"`; remove `@types/node` and `@types/react` devDependencies.
Expected: `grep -c "apps/ccusage\|catalog:types" docs/package.json` prints `0`.

- [ ] **Step 6: Edit `package.nix`** — replace `cliPackageJson = lib.importJSON (root + /apps/ccusage/package.json);` with `rootPackageJson = lib.importJSON (root + /package.json);`; `pname = rootPackageJson.name`; `meta = { inherit (rootPackageJson) description homepage; license = lib.getLicenseFromSpdxId rootPackageJson.license; mainProgram = "ccusage"; }`; fix the two comments above them.
Expected: `grep -c "cliPackageJson\|apps/ccusage" package.nix` prints `0`.

- [ ] **Step 7: Edit `nix/checks.nix`** — `apps/ccusage/config-schema.json` → `config-schema.json` (diff, two messages, `check-jsonschema` line); `just ccusage::generate-schema` → `just schema`; delete the whole `publint = mkRepoCheck ...;` attribute; fix the comment at the old line 104.
Expected: `grep -c "apps/ccusage\|publint" nix/checks.nix` prints `0`.

- [ ] **Step 8: Edit `nix/treefmt.nix`, `nix/git-hooks.nix`, `nix/packages.nix`, `nix/dev-shell.nix`**

`treefmt.nix`: `install_if_changed "$tmp" config-schema.json`; `install_if_changed config-schema.json docs/public/config-schema.json`; `schema-gen.includes` first entry `"config-schema.json"`; comments updated. `git-hooks.nix`: `node-test.entry` → `"${lib.getExe pkgs.nodejs} --test nix/tools/models-dev-gen/compact.test.ts"`. `packages.nix`: delete the `publint = ...;` binding and `publint` from `packages`. `dev-shell.nix`: delete `config.packages.publint`.
Expected: `grep -rc "apps/ccusage\|publint" nix/ | grep -v ":0"` prints nothing.

- [ ] **Step 9: Edit `justfile`, `ccusage.example.json`, `docs/README.md`, `.tagpr`, `.gitignore`**

`justfile`: delete `mod ccusage 'apps/ccusage'`; `build: docs::build`; `test-node` → `TZ=UTC node --test nix/tools/models-dev-gen/compact.test.ts`; delete `generate-large-fixture`; `schema` comment → `# Regenerate config-schema.json from the Rust source`; drop the prepack sentence in the header comment. `ccusage.example.json`: `"$schema": "./config-schema.json"`. `docs/README.md`: the schema sentence names `config-schema.json`. `.tagpr`: `versionFile = package.json,docs/package.json,plugin/.claude-plugin/plugin.json,.claude-plugin/marketplace.json,dotnet/Directory.Build.props`. `.gitignore`: delete `packages/ccusage-*/bin/`.
Expected: `grep -rn "apps/ccusage\|packages/ccusage" justfile ccusage.example.json docs/README.md .tagpr .gitignore` prints nothing.

- [ ] **Step 10: Verify the tree** [local]

Run: `grep -rln "apps/ccusage\|packages/ccusage\|publint" --exclude-dir=.git --exclude-dir=superpowers --exclude-dir=rust --exclude=pnpm-lock.yaml .`. Expected: only files under `.agents/`, `AGENTS.md`, `README.md`, `docs/guide/`, `docs/index.md`, `docs/.vitepress/` (handled by Tasks 15–16). Then `dotnet test dotnet/Ccusage.Mcp.slnx` → `Failed: 0`. **[CI only]**: `nix flake check`, `docs-build`.

- [ ] **Step 11: Commit (two commits)**

```bash
git add -A apps packages README.md LICENSE config-schema.json .oxlintrc.json .oxfmtrc.json nix/tools/publint
git commit -m "chore: remove npm packages and launcher; move config-schema.json and LICENSE to the root" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git add package.json pnpm-workspace.yaml docs/package.json docs/README.md package.nix nix justfile ccusage.example.json .tagpr .gitignore
git commit -m "chore(deps): keep pnpm workspace for docs only and point Nix, just and tagpr at the new layout" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 12: DECISION — push the branch (lockfile regeneration + CI-only verification)**

Question: "Push `feat/csharp-mcp-server` to `origin` and dispatch CI so `docs-build` regenerates `pnpm-lock.yaml` (uploaded as the `pnpm-lock` artifact; §16.10) and the Nix / multi-OS / bundle-smoke jobs run?" Options: (A) push now (`git push -u origin feat/csharp-mcp-server && gh workflow run ci.yaml --ref feat/csharp-mcp-server`); (B) the human regenerates the lockfile elsewhere (`pnpm install --lockfile-only` with pnpm 12) and pushes later; (C) do not push. Recommendation: A. A → step 13; B → skip step 13, commit the human-provided lockfile as `chore(deps): regenerate pnpm lockfile for the docs-only workspace`; C → lockfile stays stale, `docs-build` is listed as unverified in Task 17 step 5. **STOP until the human answers.**

- [ ] **Step 13: Download and commit the regenerated lockfile** (only after step 12 = A)

Run: `run=$(gh run list --workflow ci.yaml --branch feat/csharp-mcp-server --limit 1 --json databaseId -q '.[0].databaseId')`; wait for `docs-build` to finish (`gh run watch "$run"`); `gh run download "$run" --name pnpm-lock --dir .`; `git diff --stat pnpm-lock.yaml`.
Expected: `pnpm-lock.yaml` changed (importers reduced to `.` and `docs`). Commit:

```bash
git add pnpm-lock.yaml
git commit -m "chore(deps): regenerate pnpm lockfile for the docs-only workspace" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Fix any other CI failure from that run before Task 14b (each fix its own commit); pushing the fixes is covered by the step 12 answer.

---

### Task 14b: Delete upstream-only workflows and scripts; trim `check-pr-title.yaml` and `CONTRIBUTING.md`

**Implementer:** Opus  **Reviewer:** Opus

**Files:**
- Delete: `.github/workflows/{pr-gate.yaml,issue-gate.yaml,gate-close-pr.yaml,approve-contributor.yaml,pullfrog.yml,autofix.yaml,update-pricing.yaml,check-links.yaml}`, `.github/scripts/contribution-gate/`, `.github/scripts/contribution-gate.nu`, `.github/scripts/contribution-gate.test.nu`, `.github/APPROVED_CONTRIBUTORS`, `.github/scripts/npm-registry-gate.nu`, `.github/scripts/generate-e2e-fixture.nu`, `.github/scripts/upsert-pr-comment.nu` (§16.6), `.github/actions/commit-and-push-main/` (§16.5)
- Modify: `.github/workflows/check-pr-title.yaml`, `CONTRIBUTING.md` (§16.7)
- Keep: `.github/scripts/{pricing-lock,update-litellm-lock,update-models-dev-lock}.nu` (justfile recipes), `.github/tagpr-template.md`, `renovate.json`, `release.yml`, `FUNDING.yaml`, `ISSUE_TEMPLATE/`, `actionlint.yaml`

- [ ] **Step 1: Delete the files**

```bash
git rm -r -q .github/workflows/pr-gate.yaml .github/workflows/issue-gate.yaml .github/workflows/gate-close-pr.yaml .github/workflows/approve-contributor.yaml .github/workflows/pullfrog.yml .github/workflows/autofix.yaml .github/workflows/update-pricing.yaml .github/workflows/check-links.yaml .github/scripts/contribution-gate .github/scripts/contribution-gate.nu .github/scripts/contribution-gate.test.nu .github/APPROVED_CONTRIBUTORS .github/scripts/npm-registry-gate.nu .github/scripts/generate-e2e-fixture.nu .github/scripts/upsert-pr-comment.nu .github/actions/commit-and-push-main
```

Expected: `ls .github/workflows` prints `build-artifacts.yaml check-pr-title.yaml ci.yaml release.yaml sync-upstream.yaml`.

- [ ] **Step 2: Trim `check-pr-title.yaml`** — delete the `scope` job and its comment block; keep `main`.
Expected: `grep -c "hustcer/setup-nu\|scope:" .github/workflows/check-pr-title.yaml` prints `0`.

- [ ] **Step 3: Trim `CONTRIBUTING.md`** — delete the `## Contribution Gate` section (through the `lgtm` bullets and the sentence after them); in `## Quality Bar For Issues` delete `Maintainers may reopen clear, useful issues and approve the author for future issues or PRs.`; leave every other section.
Expected: `grep -c "lgtm\|contribution gate" -i CONTRIBUTING.md` prints `0`.

- [ ] **Step 4: Verify** [local]

Run: `grep -rn "hustcer/setup-nu\|useblacksmith\|lycheeverse\|autofix-ci\|pullfrog\|Kesin11\|blacksmith-\|commit-and-push-main\|upsert-pr-comment\|generate-e2e-fixture" .github`. Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add -A .github CONTRIBUTING.md
git commit -m "ci: drop upstream contribution-gate, pricing, link-check and autofix automation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: `AGENTS.md` and `.agents/skills` updates

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** Agent guidance describes the C# layer; no skill names a deleted path.

**Files:**
- Create: `.agents/skills/dotnet/SKILL.md`
- Delete: `.agents/skills/typescript/`, `.agents/skills/testing/references/node-test.md`
- Modify: `AGENTS.md`, `.agents/skills/development/SKILL.md`, `.agents/skills/development/references/commands.md`, `.agents/skills/testing/SKILL.md`, `.agents/skills/docs/SKILL.md`, `.agents/skills/fix-ci/SKILL.md`, `.agents/skills/nushell/SKILL.md`, `.agents/skills/profile/SKILL.md`, `.agents/skills/profile/references/rust.md`, `.agents/skills/rust-binary-size/SKILL.md`, `.agents/skills/tdd/SKILL.md`

- [ ] **Step 1: Edit `AGENTS.md`**

Routing: replace the `typescript` row with `` `dotnet` - `dotnet/` C# MCP server: catalog, validator, engine runner, MCP handlers, packaging, MSTest.``; `testing` row → "cargo and MSTest tests, CLI snapshots, fixtures, pricing and model tests". Repository Shape: replace the `apps/ccusage` sentence with "`dotnet/` is the MCP distribution layer: it wraps the `ccusage` binary and adds no adapter logic."; nearest-AGENTS list: `apps/ccusage/AGENTS.md` → `dotnet/AGENTS.md - C# MCP server`. Policy: delete the `devDependencies` bullet; add "C# only in `dotnet/`; any third-party NuGet package needs explicit approval (spec §12)."
Expected: `grep -c "apps/ccusage\|typescript" AGENTS.md` prints `0`.

- [ ] **Step 2: Write `.agents/skills/dotnet/SKILL.md`**

Frontmatter `name: dotnet`, `description: Guides work in dotnet/: the C# Native AOT MCP server, its MSTest suite, FakeEngine, fixtures and .mcpb packaging. Use before editing .cs, .csproj, .props, .slnx or dotnet/packaging files.`; body: pointer to `dotnet/AGENTS.md`; TDD rule; catalog-from-upstream-JSON rule; AOT rules; test layout; `UPDATE_SNAPSHOTS=1`; e2e env vars; "prefer the dotnet MCP tools".
Expected: file exists; frontmatter has `name` and `description`.

- [ ] **Step 3: Delete the TypeScript skill and the Node test reference**

```bash
git rm -r -q .agents/skills/typescript .agents/skills/testing/references/node-test.md
```

Expected: `ls .agents/skills | grep -c typescript` prints `0`.

- [ ] **Step 4: Edit `development/SKILL.md` and `references/commands.md`** — "The npm Packaging Seam" → "The MCP Packaging Seam" (`dotnet/`, `dotnet/packaging/*`, `plugin/`, `.claude-plugin/`, version files per `.tagpr`); description frontmatter drops "the npm launcher or native packaging"; `commands.md`: JS/TS tooling sentence → "docs-site tooling in `docs/package.json`"; Releases → "tagpr tags; `release.yaml` builds the matrix, bundles `ccusage.mcpb`, and creates the GitHub Release; `sync-upstream.yaml` opens the weekly upstream PR".
Expected: `grep -c "apps/ccusage\|npm" .agents/skills/development/SKILL.md .agents/skills/development/references/commands.md` prints `0` for both.

- [ ] **Step 5: Edit `testing/SKILL.md` and `docs/SKILL.md`** — testing: "Node" section → ".NET: MSTest in `dotnet/tests/Ccusage.Mcp.Tests`, FakeEngine, fixtures under `dotnet/tests/fixtures`, e2e via `CCUSAGE_MCP_E2E_SERVER`/`CCUSAGE_ENGINE`; the one remaining Node test is `nix/tools/models-dev-gen/compact.test.ts` (`just test-node`)"; description mentions MSTest. docs: surfaces → root `README.md`, `docs/guide/` incl. `docs/guide/mcp.md`, VitePress nav, `plugin/skills/ccusage/SKILL.md`; delete the two `apps/ccusage` bullets.
Expected: `grep -c "apps/ccusage\|node-test" .agents/skills/testing/SKILL.md .agents/skills/docs/SKILL.md` prints `0` for both.

- [ ] **Step 6: Fix the remaining dangling references**

`fix-ci`: table rows → `dotnet-test` → `dotnet test dotnet/Ccusage.Mcp.slnx`; `e2e` → "set the two env vars, then the same"; `bundle-smoke` → `pwsh dotnet/packaging/bundle-smoke.ps1`; delete the `JS test` and `Babashka` rows. `nushell`: description and body drop `apps/ccusage/scripts`, `upsert-pr-comment.nu`, `stage-native-package.nu`, `native-binary.nu`. `profile`: delete the JS-launcher profiling paragraph and the `node --cpu-prof ... cli.js` example; `references/rust.md`: delete the `compare-pr-performance.bb` sentence. `rust-binary-size`: drop the `apps/ccusage/scripts/**` entries from `paths`/`globs` and the sentence naming them. `tdd`: delete the `node --test ... cli.test.ts` line.
Expected: `grep -rn "apps/ccusage\|packages/ccusage\|cli.js\|publint\|compare-pr-performance" AGENTS.md .agents` prints nothing.

- [ ] **Step 7: Commit**

```bash
git add -A AGENTS.md .agents
git commit -m "docs(agents): describe the dotnet MCP layer and retire the npm skills" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: README and docs site rewrite

**Implementer:** Opus  **Reviewer:** Opus

**Phase 0 dependency:** Task 0 step 18 (reconnect sentence in troubleshooting).

**Goal:** §3.4 exactly, plus §16.8.

**Files:**
- Modify: `README.md`, `docs/index.md`, `docs/.vitepress/config.ts`, `docs/guide/installation.md`, `docs/guide/getting-started.md`, `docs/guide/index.md`, `docs/guide/{amp,codebuff,copilot,droid,hermes,kilo,openclaw,opencode,pi,qwen}/index.md`
- Create: `docs/guide/mcp.md`
- Delete: `docs/guide/sponsors.md`

- [ ] **Step 1: Rewrite `README.md`**

Remove: the whole badge block (Socket, npm version, NPM downloads, DeepWiki, Awesome, 技術者倫理, Trendshift); `## Major Sponsors`, GitHub Sponsors, Star History; every `npx`/`bunx`/`pnpm dlx`/`pnpx`/pkg.pr.new line and the bunx caching note; `nix run github:ccusage/ccusage`; the `pnpm install` dev note; the update-pricing paragraph. Rewrite: intro + `Fork of [ccusage/ccusage](https://github.com/ccusage/ccusage) by @ryoppippi`; `## Quick Start` (`/plugin marketplace add Desulation/ccusage`, `/plugin install ccusage@ccusage`, `/ccusage claude daily`); `## Installation` with four numbered options (plugin; Claude Desktop `ccusage.mcpb` from Releases; other MCP clients: archive + `{"mcpServers":{"ccusage":{"command":"/path/to/ccusage-mcp"}}}`; standalone `ccusage` CLI from the archive + `xattr -d com.apple.quarantine ccusage ccusage-mcp` note); `## Usage` (slash commands; three example questions such as "How much did I spend on Codex this month?"; the existing CLI block with `bunx ccusage` → `ccusage`); `## MCP reference` table (8 tools, the prompt, the skill; link `docs/guide/mcp.md`); `## Development` (Nix/direnv/`just` kept; `dotnet/` subsection: .NET 10 SDK, `dotnet build/test/publish`, e2e env vars; one sentence on `sync-upstream.yaml`). Keep: Supported Sources table, Features (+ `MCP server for Claude Code and other MCP clients`), License (MIT © @ryoppippi); Documentation link → `docs/guide/`.
Expected: `grep -c "npx\|bunx\|pnpm dlx\|pnpx\|pkg.pr.new\|sponsors\|star-history\|shields.io" README.md` prints `0`; `grep -c "Desulation/ccusage" README.md` ≥ 3.

- [ ] **Step 2: Replace package-runner commands in the guides**

For each listed guide: `npx ccusage@latest ` → `ccusage `, `bunx ccusage ` → `ccusage `; then read each changed hunk and fix prose that assumed a package runner.
Expected: `grep -rn "npx ccusage\|bunx ccusage" docs/guide` prints nothing.

- [ ] **Step 3: Rewrite `docs/guide/installation.md`** — mirror README `## Installation` (four options), a `## Development` section (Nix/direnv/just + `dotnet/` commands), `## Verification` (`ccusage --version`, `ccusage-mcp --version`), `## Updating` (`/plugin update ccusage@ccusage`; re-download the archive), `## Troubleshooting` (macOS quarantine; engine not found → `CCUSAGE_ENGINE`), `## Next Steps` (links kept). Drop runtime comparison, Node/Bun requirements, npm sections.
Expected: `grep -c "npm\|bunx\|node" -i docs/guide/installation.md` prints `0` (allow `Node` only if it appears in an unrelated kept sentence — none expected).

- [ ] **Step 4: Rewrite `docs/guide/getting-started.md`** — Prerequisites (Claude Code or another MCP client, or the standalone CLI); Quick Start = README's; keep "Your First Report", "Understanding the Output", "Next Steps", "Common Use Cases" (commands as `ccusage …`), "Troubleshooting", "Custom Data Directory", "Getting Help" (GitHub link → `Desulation/ccusage`; add "ask Claude: `/ccusage --help`-style questions go through the `list-sources` tool"). Delete the Nix/npx/pnpm/pkg.pr.new code group and the bunx tip.
Expected: `grep -c "bunx\|npx\|pkg.pr.new\|nix run" docs/guide/getting-started.md` prints `0`.

- [ ] **Step 5: Fix `docs/guide/index.md`** — its one `npx`/`bunx` line → `ccusage`. Expected: no `npx`/`bunx` in the file.

- [ ] **Step 6: Write `docs/guide/mcp.md`**

Sections: `# MCP Server`; Overview; Install (three ways, same as README); Tools — one `##` per tool with a parameters table (name, type, description incl. "Applies to") generated by reading `dotnet/tests/Ccusage.Mcp.Tests/Snapshots/tools-list.json`; Prompt `ccusage` with the `/mcp__plugin_ccusage_ccusage__ccusage` and `/ccusage:ccusage` forms (§5.6); Skill `/ccusage` (§7.2); Results and errors (§5.7); Engine location (the four rules, `CCUSAGE_ENGINE`); Environment (`CCUSAGE_MCP_TIMEOUT_SECONDS`, inherited `CLAUDE_CONFIG_DIR`/`CODEX_HOME`/…); Troubleshooting (engine not found → the `Checked:` list; timeouts → `since`/`last` or raise the env var; large outputs → `since`/`last`/`no-cost`; macOS quarantine; the `/mcp` reconnect sentence only if Task 0 step 18 = A).
Expected: `grep -c "^## " docs/guide/mcp.md` ≥ 9; every tool name appears as a heading.

- [ ] **Step 7: Remove sponsor/promotional content** (§3.4, §16.8)

`git rm docs/guide/sponsors.md`; delete the `<div ...>Support ccusage … </div>` block from `docs/index.md` (keep the project-history line; `actions` GitHub link → `https://github.com/Desulation/ccusage`); in `docs/.vitepress/config.ts`: delete nav items `npm`, `DeepWiki`, `Package Stats`, `Sponsor`; delete the `Sponsors` sidebar item (keep `Community` with `Community Projects`); add `{ text: 'MCP Server', link: '/guide/mcp' }` to the `Integration` group; delete the `npm` social link; delete the `/npm`, `/deepwiki`, `/sponsor` redirects; point `nav` GitHub, `Changelog`, `socialLinks` github, and `editLink.pattern` at `https://github.com/Desulation/ccusage`. Leave `docs/wrangler.jsonc`.
Expected: `grep -rn "sponsor\|npmjs\|deepwiki\|tanstack\|ryoppippi/ccusage" -i docs/index.md docs/.vitepress/config.ts` prints nothing.

- [ ] **Step 8: Verify** [local]

Run: `grep -rn "npx ccusage\|bunx\|pnpm dlx\|pnpx\|pkg.pr.new\|sponsors.ryoppippi\|github.com/sponsors" README.md docs/guide docs/index.md docs/.vitepress`. Expected: no output. **[CI only]**: `docs-build`.

- [ ] **Step 9: Commit**

```bash
git add -A README.md docs
git commit -m "docs: rewrite README and guides for the MCP plugin distribution" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Final consistency pass

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Confirm the success criteria that can be checked locally; hand a clean branch to the human.

- [ ] **Step 1: Build and unit-test** [local]

Run: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release && dotnet test dotnet/Ccusage.Mcp.slnx -c Release --no-build`. Expected: `0 Warning(s)`; `Failed: 0`; e2e tests inconclusive.

- [ ] **Step 2: E2E locally** [local]

Run: Task 8 step 8's command. Expected: `Failed: 0`, no inconclusive results.

- [ ] **Step 3: Plugin and versions** [local]

Run: `claude plugin validate --strict plugin` and the Task 11b version-consistency script (bash + `jq`, or Python). Expected: success; one version printed five times.

- [ ] **Step 4: Dangling references** [local]

Run: `git grep -n "apps/ccusage\|packages/ccusage\|npm publish\|pkg.pr.new\|blacksmith" -- . ':!rust' ':!docs/superpowers' ':!pnpm-lock.yaml'`. Expected: no output. `git status --short` → empty.

- [ ] **Step 5: Audit the commits and write the handoff note** [local]

Run: `git log --format=%B main..HEAD | grep -c "Co-Authored-By: Claude Opus 5.5"` equals `git rev-list --count main..HEAD`; every subject matches `^(feat|fix|chore|docs|test|ci|refactor)(\([a-z-]+\))?: `. Write the handoff note (in the reply, not a file): what remains **[CI only]**/unverified (Nix builds, Linux/macOS bundle and `launch.sh`, the Windows ARM64 leg, the release flow, `plugin-validate`), whether the lockfile was regenerated, and the §15 rows still "pending CI".
Expected: counts match; note written.

- [ ] **Step 6: DECISION — open the pull request**

Question: "Open a PR `feat/csharp-mcp-server` → `main` on `Desulation/ccusage` now?" Options: (A) yes — `gh pr create --base main --head feat/csharp-mcp-server --title "feat(mcp): replace npm distribution with a C# Native AOT MCP server plugin" --body-file <note>` ending with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`; (B) push only (if not pushed yet) and let the human open it; (C) neither. Recommendation: A if CI on the branch is green, else B. A → create the PR and report its URL; B → push and report; C → stop. **STOP until the human answers.**

---

## Resolved decisions (approved by the human on 2026-10-29; recorded in spec §16)

1. Root `package.json` `name` becomes `"ccusage"` (plus `homepage`); `package.nix` derives `pname`/`meta` from it — Task 14a steps 3, 6.
2. A `number` option kind for `session-length` — Task 2 (`OptionKind.Number`), Task 3, Task 7a.
3. The `action-timeline` job is deleted; `Kesin11/actions-timeline` stays unapproved — Task 11b step 3.
4. `nix/tools/changelogithub` stays in the tree (minimal divergence) — Task 12.
5. `.github/actions/commit-and-push-main` is deleted — Task 14b step 1.
6. `.github/scripts/generate-e2e-fixture.nu` and `upsert-pr-comment.nu` are deleted — Task 14b step 1.
7. `CONTRIBUTING.md` loses the contribution-gate and `lgtm` sections — Task 14b step 3.
8. Docs nav drops `npm`, `Package Stats`, `DeepWiki`; GitHub links and `editLink` point at `Desulation/ccusage` — Task 16 step 7.
9. A `docs-build` CI job (pnpm via Nix) — Task 11b step 6.
10. `docs-build` regenerates `pnpm-lock.yaml` with `--lockfile-only`, uploads it as the `pnpm-lock` artifact when it differs and fails; the implementer downloads it with `gh run download` and commits it, after the push decision — Task 11b step 6, Task 14a steps 12–13.
11. `nix/darwin-x64-package.nix` and the macOS action's cross-build branch stay — Task 11a step 3.
12. Claude-only reports use the root argv form (`ccusage blocks`, `ccusage statusline`) — Task 3.
13. Strict unknown-parameter rejection on typed tools (`additionalProperties: false` + server-side check) with an error listing the valid parameters — Task 3, Task 7a, Task 7b.
14. Claude Code `2.1.303` is pinned for `plugin-validate` — Task 11b step 6.
15. Q4/Q5 are verified in CI: the `windows-11-arm` `bundle-smoke` leg asserts the win-x64 server selects `win\arm64\ccusage.exe`; the Linux/macOS `bundle-smoke` legs check exec bits after extraction; Phase 0 records both as "pending CI" — Task 0 steps 11–12, Task 10, Task 11b step 5.
