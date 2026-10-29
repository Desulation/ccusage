# ccusage C# AOT MCP Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace this fork's npm distribution layer with a .NET 10 Native AOT MCP server (`ccusage-mcp`) that wraps the unchanged Rust engine, shipped as a Claude Code plugin with a single `.mcpb` bundle, with CI/release pipelines and docs to match.

**Architecture:** A single C# project derives its tool/prompt surface from the engine's own `cli-commands.json` / `cli-help.json` (embedded by link), validates every request against that catalog, builds an argv, and runs the engine as a child process, passing its `--json` stdout through untouched. Tools and prompts are registered through the MCP SDK's list/call handler hooks with source-generated JSON (no reflection). Packaging produces `ccusage.mcpb` (all six RIDs) plus per-platform archives; GitHub Actions build, test (unit + FakeEngine + real-engine e2e), bundle, smoke-test, and release.

**Tech Stack:** .NET SDK 10.0.402 (`global.json` rollForward `latestFeature`), C# / Native AOT, `ModelContextProtocol` 2.3.0, `Microsoft.Extensions.Hosting` 10.0.x, `System.Text.Json` source generation, MSTest 4.x on Microsoft.Testing.Platform, Rust engine (unchanged), Nix (CI only), GitHub Actions, Claude Code CLI (plugin validation), bash + PowerShell 7 packaging scripts.

**Spec:** `docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md` (read it fully before any task; section numbers below refer to it).

## Global Constraints

- **Phase 0 is a hard gate.** Task 0 is a throwaway spike. **Execution STOPS after Task 0** until the human has reviewed the findings addendum. Any fallback that changes user-visible behavior (spec §11 "If the answer is no" column) needs the human's written approval before Tasks 1+ proceed. Tasks that depend on a Phase 0 answer say so in a **Phase 0 dependency** line.
- **Third-party dependencies:** only those in spec §12: NuGet `ModelContextProtocol` **2.3.0** exactly; GitHub Actions `Songmu/tagpr`, `amannn/action-semantic-pull-request`, `nixbuild/nix-quick-install-action`, `nix-community/cache-nix-action` at their existing SHAs. First-party is fine: Microsoft/.NET NuGet packages (`Microsoft.Extensions.*`, `MSTest`, `Microsoft.Testing.*`, `System.*`), GitHub `actions/*` and `github/*`, `gh` CLI, Claude Code CLI. **If a task would need anything else, STOP and flag it for human approval; do not add it.** (This plan removes `Kesin11/actions-timeline`, `useblacksmith/*`, `hustcer/setup-nu`, `lycheeverse/lychee-action`, `autofix-ci/action`, `pullfrog/pullfrog`, `oven-sh/setup-bun`, `pnpm/action-setup` because they are not on the approved list.)
- **Never modify `rust/`.** Edits to upstream-owned files are limited to spec §3.1/§3.2. New work lives in `dotnet/`, `plugin/`, `.claude-plugin/`, new workflow/action files, and `docs/guide/mcp.md`.
- **Models:** every task names `Implementer: Opus` and `Reviewer: Opus` or `Reviewer: Fable` (spec §13).
- **TDD** for all C# logic: write the failing test, run it to see it fail, implement, run to see it pass, commit. MSTest on Microsoft.Testing.Platform (`global.json` `"test": {"runner": "Microsoft.Testing.Platform"}`).
- **Tooling for implementers:** prefer the dotnet MCP tools (`mcp__dotnet__dotnet_sdk` `ConfigureGlobalJson`, `mcp__dotnet__dotnet_solution` `Create`/`Add`, `mcp__dotnet__dotnet_project` `New`/`Build`/`Test`/`Publish`/`SetProperty`, `mcp__dotnet__dotnet_package` `Add`/`Search`) for creating, building, running, testing and package changes; shell `dotnet` only when no MCP action covers it (e.g. `dotnet test` with MTP-specific arguments, `dotnet publish` with `-p:` overrides). Prefer `roslyn_*` MCP tools for C# analysis when available. Load deferred tools with `ToolSearch` (`select:mcp__dotnet__dotnet_project,...`) before first use. Verification commands below are written as shell commands so the expected output is unambiguous; run them through the equivalent MCP action when one exists.
- **Environment:** Windows 11 dev machine, .NET SDK 10.0.402 (and 9.0.319), `cargo` 1.97.1 via rustup, Claude Code CLI 2.1.303, `gh` 2.102, Python 3.12, Git Bash, PowerShell 7. **No Nix, no Node, no pnpm locally.** Each verification step is tagged **[local]** or **[CI only]**. Nix steps (`nix build`, `nix flake check`) and Linux/macOS behavior are **[CI only]**; Windows AOT publish needs the Visual Studio "Desktop development with C++" workload (checked in Task 0 step 1; if absent, AOT publish is also [CI only] and local verification stops at the AOT/trim *analyzers*, which run on `dotnet build`).
- **Commits:** small Conventional Commits, one or more per task, each ending with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. **Never push.** The human decides when to push or open a PR. Commit scopes used here: `mcp` (C# server), `plugin`, `bundle`, `ci`, `release`, `deps`, `docs`, `chore`.
- **Version** everywhere is the root `package.json` `version` (currently `20.0.45`); every new file that carries a version uses that exact string.
- **Naming:** all MCP parameter names are kebab-case and equal the long flag name without `--`. Tools: `daily`, `weekly`, `monthly`, `session`, `blocks`, `statusline`, `list-sources`, `report`. Prompt: `ccusage`. Executable: `ccusage-mcp`. Env vars: `CCUSAGE_ENGINE`, `CCUSAGE_MCP_TIMEOUT_SECONDS` (default `120`).
- **Order:** tasks run in the order listed; each leaves the repo building and all tests passing. `apps/ccusage` is deleted (Task 14) only after fixtures were copied to `dotnet/tests/fixtures/` (Task 8) and CI no longer references it (Tasks 11–12).

## Review Focus

Inputs the spec implies but does not spell out, most likely to bite first; each has its test pinned to the owning task:

1. **Engine stdout that is empty or not JSON with exit code 0** (e.g. a report with no data, or a future engine printing a notice) must still be passed through verbatim as content block 1, never re-parsed or wrapped — Task 7b `CallAsync_PassesNonJsonStdoutUnchanged`.
2. **Multi-megabyte engine output** must not deadlock the runner (stdout and stderr are drained concurrently) — Task 6 `RunAsync_LargeStdout_DoesNotDeadlock` (FakeEngine emits 8 MB).
3. **Free-form values with spaces, quotes and non-ASCII** (`project`, `project-aliases`, `timezone`, `id`, `config`, `pi-path`) must reach the engine as single argv elements on Windows too — Task 6 `RunAsync_PassesSpacesQuotesAndUnicodeAsSingleArgv`.
4. **`hook-input` that is not a JSON object** (string, array, null) must be a validation error naming `hook-input`, and the engine must not start — Task 7b `CallAsync_Statusline_RejectsNonObjectHookInput`.
5. **An invalid `CCUSAGE_MCP_TIMEOUT_SECONDS`** (`abc`, `0`, `-5`) must fall back to 120 s with one stderr warning rather than crash or wait forever — Task 6 `TimeoutSeconds_InvalidEnv_FallsBackTo120`.

Also pinned: unknown parameters on any typed tool are rejected (strict schemas, Task 3 `Validate_UnknownParameter_Rejected`); a `CCUSAGE_ENGINE` path that does not exist is skipped, not fatal, and listed in the error (Task 5).

---

## File map

New (owned by this fork):

```
dotnet/global.json
dotnet/Directory.Build.props                 net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, <Version>20.0.45</Version>
dotnet/Directory.Packages.props              central package versions
dotnet/Ccusage.Mcp.slnx
dotnet/AGENTS.md
dotnet/packaging/manifest.template.json
dotnet/packaging/launch.sh
dotnet/packaging/assemble-bundle.sh          artifacts dir -> ccusage.mcpb + archives + SHA256SUMS
dotnet/packaging/verify-bundle.sh            layout + manifest checks
dotnet/packaging/bundle-smoke.ps1            start bundle via manifest command, JSON-RPC smoke
dotnet/src/Ccusage.Mcp/Ccusage.Mcp.csproj   AssemblyName ccusage-mcp, PublishAot, embeds the two JSON files by link
dotnet/src/Ccusage.Mcp/Program.cs
dotnet/src/Ccusage.Mcp/Commands/{CommandCatalog,OptionKind,OptionTypeTable,CommandRequest,CommandValidator,ValidationException,CliArgsParser}.cs
dotnet/src/Ccusage.Mcp/Engine/{EngineLocator,EngineRunner,EngineResult}.cs
dotnet/src/Ccusage.Mcp/Tools/{ToolDefinitions,ToolHandlers}.cs
dotnet/src/Ccusage.Mcp/Prompts/PromptHandlers.cs
dotnet/src/Ccusage.Mcp/Json/McpJsonContext.cs
dotnet/tests/Ccusage.Mcp.Tests/Ccusage.Mcp.Tests.csproj
dotnet/tests/Ccusage.Mcp.Tests/{Commands,Engine,Tools,Prompts,E2E,Snapshots}/...
dotnet/tests/FakeEngine/FakeEngine.csproj    AssemblyName ccusage (so the built apphost is ccusage[.exe])
dotnet/tests/FakeEngine/Program.cs
dotnet/tests/fixtures/                       copied from apps/ccusage/test/
.claude-plugin/marketplace.json
plugin/.claude-plugin/plugin.json
plugin/skills/ccusage/SKILL.md
.agents/skills/dotnet/SKILL.md
.github/actions/publish-mcp-server/action.yaml
.github/workflows/build-artifacts.yaml       reusable (workflow_call): build matrix + bundle
.github/workflows/sync-upstream.yaml
docs/guide/mcp.md
```

Modified upstream files: exactly spec §3.2 plus the `.agents/skills/*` files whose text references deleted paths (listed in Task 15). Deleted: spec §3.1 (Task 14).

---

### Task 0: Phase 0 verification spike (HARD GATE)

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Answer the eight questions in spec §11 with a throwaway hand-built bundle and record findings as a spec addendum. Nothing from this task ships; the only committed artifact is the addendum.

**Files:**
- Create (throwaway, outside the repo): `C:\Users\gman2\source\AiTesting\ccusage-csharp\phase0\` (spike server, bundle, local marketplace). Nothing under the repo root.
- Modify: `docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md` — append section `## 15. Phase 0 findings (2026-10-29)`.

**Interfaces:** Produces the decisions consumed by Task 5 (Q5), Task 7a/7b (Q6), Task 9 (Q7, Q3), Task 10 (Q1, Q2, Q4, Q8).

- [ ] **Step 1: Check AOT prerequisites** [local]

Run `dotnet new console -n Hello -o phase0/hello --aot` (via `mcp__dotnet__dotnet_project` `New`, template `console`, additionalOptions `--aot`) then `dotnet publish phase0/hello -r win-x64 -c Release`.
Expected: `phase0/hello/bin/Release/net10.0/win-x64/publish/Hello.exe` exists. If the publish fails with a missing linker/`link.exe` error, install the "Desktop development with C++" workload or record "AOT publish is CI-only on this machine" in the addendum and continue (the remaining questions do not need a local AOT build except Q6, which then runs in a `workflow_dispatch` scratch run only if the human agrees).

- [ ] **Step 2: Build the minimal spike server** [local]

In `phase0/spike/`: console project `ccusage-mcp` referencing `ModelContextProtocol` 2.3.0 and `Microsoft.Extensions.Hosting` 10.0.14, `PublishAot=true`, `InvariantGlobalization=true`, `IsAotCompatible=true`, `TrimmerSingleWarn=false`, `IlcTreatWarningsAsErrors=true`. Register **no attributed tools**; instead configure `McpServerOptions.Handlers` (or the `IMcpServerBuilder.WithListToolsHandler` / `WithCallToolHandler` / `WithListPromptsHandler` / `WithGetPromptHandler` extension methods — record which exists in 2.3.0) with one tool `ping` whose `InputSchema` is a `JsonElement` parsed from the literal `{"type":"object","properties":{},"additionalProperties":false}`, returning `CallToolResult` with one `TextContentBlock` `"pong"`, and one prompt `ping` returning one user message. Serialize any custom type through a `[JsonSerializable]` `JsonSerializerContext`. Add `--version` handling that prints `ccusage-mcp spike` and exits before host startup.

Publish for `win-x64`: `dotnet publish -r win-x64 -c Release`. **Q6 answer = publish succeeds with zero `IL2xxx`/`IL3xxx` warnings** (the build log must contain no `warning IL`). Record every API name used (handler property/extension names, delegate signatures, `RequestContext<T>` shape, how `CallToolRequestParams.Arguments` is typed), since Task 7 copies them verbatim.

- [ ] **Step 3: Hand-build the spike bundle** [local]

Lay out `phase0/bundle/` exactly as spec §7.3 (`manifest.json` from §7.3 with `"version": "0.0.0-spike"`, `launch.sh` as §7.3 describes, `win/ccusage-mcp.exe` = the spike publish output, `win/x64/ccusage.exe` and `win/arm64/ccusage.exe` = copies of the Rust engine built with `cargo build --manifest-path rust/Cargo.toml --release --bin ccusage --features fetch-litellm-pricing`; the `linux/` and `osx/` folders may hold the same spike `.exe` renamed, as placeholders). Zip it as `ccusage.mcpb` (`Compress-Archive` then rename, or `python -m zipfile -c`). Record the compressed size of a bundle where all eight binaries are real-sized (estimate: Windows server + 2 engines measured, times the §7.3 file count) as **Q8**.

- [ ] **Step 4: Install as a local plugin and probe Claude Code** [local, Windows only]

Create `phase0/marketplace/.claude-plugin/marketplace.json` per spec §7.1 with `source: "./plugin"` and the `mcpServers` field, and `phase0/marketplace/plugin/.claude-plugin/plugin.json`. Try `mcpServers` pointing at (a) a `file://` path to `ccusage.mcpb`, (b) a `https://` URL (ask the human to upload the spike `.mcpb` to a draft pre-release on `Desulation/ccusage` if (a) is unsupported). Run `claude plugin validate --strict phase0/marketplace` and `claude plugin validate --strict phase0/marketplace/plugin`; try `mcpServers` on the marketplace entry and, if rejected, inside `plugin.json` — **Q7 = the placement that passes**. Then in a scratch Claude Code session: `/plugin marketplace add C:\...\phase0\marketplace`, `/plugin install ccusage@ccusage`, `/mcp`.
  - **Q1:** does the server start on Windows via `platform_overrides.win32`? (If Claude Code ran `/bin/sh launch.sh`, the answer is no.)
  - **Q2:** does `${__dirname}` resolve (server starts from the extracted folder; check the process command line in Task Manager / `Get-CimInstance Win32_Process`)?
  - **Q3:** was the first connection attempt before extraction finished (look for an initial failure in `claude --debug` output / `/mcp` status, then reconnect)?
  - **Q4:** cannot be observed on Windows. Check Claude Code's extraction behavior on a Linux/macOS machine if the human has one; otherwise record "unverified; `launch.sh` restores `+x` regardless" (the fallback is already in the design, so this is not user-visible).
  - **Q5:** no Windows ARM64 hardware available. Record the implementation of `RuntimeInformation.OSArchitecture` in the .NET 10 runtime source (`src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/RuntimeInformation.Windows.cs`, which calls `IsWow64Process2`/`GetNativeSystemInfo`) with a link, and whether it reports the OS architecture under emulation. If the source is inconclusive, Task 5 implements the `IsWow64Process2` `[LibraryImport]` fallback.

- [ ] **Step 5: Write the addendum**

Append to the spec:

```markdown
## 15. Phase 0 findings (2026-10-29)

| # | Question | Answer | Evidence | Consequence for the plan |
|---|---|---|---|---|
| 1 | platform_overrides honored | yes / no / unverified | ... | none / fallback (needs approval) |
| 2 | ${__dirname} substituted | ... | ... | ... |
| 3 | extracted before first start | ... | ... | skill error path text |
| 4 | exec bits preserved | ... | ... | launch.sh chmod stays |
| 5 | OSArchitecture under emulation | ... | ... | Task 5 uses OSArchitecture / IsWow64Process2 |
| 6 | SDK 2.3.0 handlers AOT-clean | ... | API names: ... | Task 7 API list |
| 7 | mcpServers placement | entry / manifest | validate output | §7.1 adjusted |
| 8 | bundle size | NN MB | file list + sizes | none |

### Environment notes
- AOT publish locally: works / CI-only (reason).
```

Mark every fallback that changes user-visible behavior with **"REQUIRES HUMAN APPROVAL"**.

- [ ] **Step 6: Commit and STOP**

```bash
git add docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md
git commit -m "docs(spec): add Phase 0 verification findings" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

**STOP HERE.** Report the findings to the human. Do not start Task 1 until the human has reviewed section 15 and explicitly approved any fallback it lists.

---

### Task 1: `dotnet/` skeleton (solution, props, server stub, test project, FakeEngine)

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** A building, testable `dotnet/` tree with one passing test, the server printing `--version`, and the AOT/trim analyzers enabled, so every later task starts from green.

**Files:**
- Create: `dotnet/global.json`, `dotnet/Directory.Build.props`, `dotnet/Directory.Packages.props`, `dotnet/Ccusage.Mcp.slnx`, `dotnet/src/Ccusage.Mcp/Ccusage.Mcp.csproj`, `dotnet/src/Ccusage.Mcp/Program.cs`, `dotnet/src/Ccusage.Mcp/ServerVersion.cs`, `dotnet/tests/Ccusage.Mcp.Tests/Ccusage.Mcp.Tests.csproj`, `dotnet/tests/Ccusage.Mcp.Tests/TestPaths.cs`, `dotnet/tests/Ccusage.Mcp.Tests/VersionTests.cs`, `dotnet/tests/FakeEngine/FakeEngine.csproj`, `dotnet/tests/FakeEngine/Program.cs`, `dotnet/AGENTS.md`
- Modify: `.gitignore` (append `dotnet/**/bin/`, `dotnet/**/obj/`, `dotnet/dist/`, `*.mcpb`)

**Interfaces:**
- Produces: `Ccusage.Mcp.ServerVersion.Value` (`static string`, from `AssemblyInformationalVersionAttribute`, build metadata after `+` stripped); FakeEngine protocol (see step 5); test helper `TestPaths.FakeEnginePath` (`static string`, from assembly metadata `FakeEnginePath`).

- [ ] **Step 1: Create global.json, props and solution**

`dotnet/global.json`: `{"sdk": {"version": "10.0.402", "rollForward": "latestFeature"}, "test": {"runner": "Microsoft.Testing.Platform"}}` (use `mcp__dotnet__dotnet_sdk` `ConfigureGlobalJson` with `sdkVersion`, `rollForward`, `testRunner`, `globalJsonPath: dotnet/global.json`, then confirm the file content).

`dotnet/Directory.Build.props`: `TargetFramework net10.0`, `Nullable enable`, `ImplicitUsings enable`, `LangVersion latest`, `TreatWarningsAsErrors true`, `EnforceCodeStyleInBuild true`, `AnalysisLevel latest-recommended`, `Version 20.0.45`, `InvariantGlobalization true`, `ManagePackageVersionsCentrally true`.

`dotnet/Directory.Packages.props`: `PackageVersion` items: `ModelContextProtocol` **2.3.0**; `Microsoft.Extensions.Hosting` 10.0.14; `MSTest` 4.5.2 (confirm with `mcp__dotnet__dotnet_package` `Search` `exactMatch` that these are the versions available; if `MSTest`/`Microsoft.Extensions.Hosting` have a newer patch, use it and record the number here; `ModelContextProtocol` stays 2.3.0).

Solution: `mcp__dotnet__dotnet_solution` `Create` name `Ccusage.Mcp`, format `slnx`, output `dotnet/`.

- [ ] **Step 2: Create the server project**

`mcp__dotnet__dotnet_project` `New` template `console`, name `Ccusage.Mcp`, output `dotnet/src/Ccusage.Mcp`, additionalOptions `--use-program-main`. Then set in the csproj: `AssemblyName ccusage-mcp`, `RootNamespace Ccusage.Mcp`, `OutputType Exe`, `PublishAot true`, `IsAotCompatible true`, `TrimmerSingleWarn false`, `IlcTreatWarningsAsErrors true`, `StripSymbols true`, `SatelliteResourceLanguages en`, `UseSystemResourceKeys true`. Package references (no versions; CPM): `ModelContextProtocol`, `Microsoft.Extensions.Hosting`. Add to the solution.

`Program.cs`: `static int Main(string[] args)`: if `args` contains `--version` → write `ccusage-mcp {ServerVersion.Value}` to stdout, return 0 (engine line added in Task 5). Otherwise write `ccusage-mcp: MCP server not wired yet` to stderr and return 2 (replaced in Task 7b).

`ServerVersion.cs`: `public static class ServerVersion { public static string Value { get; } }` reading `Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion` and cutting at the first `+`.

- [ ] **Step 3: Create the test project and FakeEngine**

`mcp__dotnet__dotnet_project` `New` template `mstest`, name `Ccusage.Mcp.Tests`, output `dotnet/tests/Ccusage.Mcp.Tests`. Ensure the csproj has `EnableMSTestRunner true`, `TestingPlatformDotnetTestSupport true`, `OutputType Exe`, `IsPackable false`, a `PackageReference` to `MSTest`, a `ProjectReference` to `../../src/Ccusage.Mcp/Ccusage.Mcp.csproj`, a `ProjectReference` to `../FakeEngine/FakeEngine.csproj` with `ReferenceOutputAssembly="false"`, and:

```xml
<PropertyGroup>
  <FakeEngineExe Condition="$([MSBuild]::IsOSPlatform('Windows'))">.exe</FakeEngineExe>
</PropertyGroup>
<ItemGroup>
  <AssemblyMetadata Include="FakeEnginePath" Value="$(MSBuildThisFileDirectory)../FakeEngine/bin/$(Configuration)/net10.0/ccusage$(FakeEngineExe)" />
</ItemGroup>
```

Delete the template's `Test1.cs`. Add `TestPaths.cs`: `internal static class TestPaths { public static string FakeEnginePath { get; } }` reading the `AssemblyMetadataAttribute` with key `FakeEnginePath` and normalizing with `Path.GetFullPath`.

FakeEngine: `New` template `console`, name `FakeEngine`, output `dotnet/tests/FakeEngine`, `--use-program-main`; csproj `AssemblyName ccusage`, `UseAppHost true`. Add both to the solution.

- [ ] **Step 4: Write the failing tests**

`VersionTests.cs`:
- `ServerVersion_MatchesRootPackageJson`: read `../../../package.json` relative to the repo (locate the repo root by walking up from `AppContext.BaseDirectory` until a directory containing `package.json` and `rust/` is found), parse `version` with `JsonDocument`, assert equals `ServerVersion.Value`.
- `FakeEngine_EchoesArgv`: start `TestPaths.FakeEnginePath` with args `["daily","--json"]`, read stdout, parse JSON, assert `argv` array equals the args and `stdin` is `""`.

Run: `dotnet test dotnet/Ccusage.Mcp.slnx` (or `mcp__dotnet__dotnet_project` `Test`, project `dotnet/Ccusage.Mcp.slnx`). Expected: both FAIL (`ServerVersion` missing / FakeEngine prints the template hello).

- [ ] **Step 5: Implement FakeEngine and ServerVersion**

FakeEngine `Program.Main(string[] args)` → `int`: reads all stdin only if `Console.IsInputRedirected` and an arg equals `statusline` (otherwise stdin is left alone so tests without stdin do not block); control args, each also echoed:
  - `--fake-exit=<n>` → return `n`;
  - `--fake-stderr=<text>` → write `<text>` to stderr;
  - `--fake-stdout=<text>` → write `<text>` to stdout **instead of** the echo JSON;
  - `--fake-stdout-bytes=<n>` → write `n` bytes of `x` to stdout instead of the echo;
  - `--fake-hang` → `Thread.Sleep(Timeout.Infinite)` after writing stdout;
  - `--version` → print `ccusage 0.0.0-fake` and return 0.
  Default stdout: `{"argv":[...],"stdin":"<stdin or empty>"}` via `JsonSerializer` with a source-generated context (`[JsonSerializable(typeof(FakeEcho))]`; `FakeEcho(string[] Argv, string Stdin)`), then return 0.

Run tests. Expected: PASS.

- [ ] **Step 6: Verify the analyzers are on and `--version` works** [local]

`dotnet build dotnet/Ccusage.Mcp.slnx -c Release` → 0 warnings, 0 errors. `dotnet run --project dotnet/src/Ccusage.Mcp -- --version` → `ccusage-mcp 20.0.45`.

- [ ] **Step 7: Write `dotnet/AGENTS.md`**

Content: layout of `dotnet/` (one paragraph per directory from the file map), the three commands (`dotnet build dotnet/Ccusage.Mcp.slnx`, `dotnet test dotnet/Ccusage.Mcp.slnx`, `dotnet publish dotnet/src/Ccusage.Mcp -c Release -r <rid>`), the rule "catalog data comes from `rust/crates/ccusage-cli-parser/src/*.json`; never copy it", the FakeEngine control-arg protocol from step 5, the e2e env vars (`CCUSAGE_MCP_E2E_SERVER`, `CCUSAGE_ENGINE`; Task 8), and "snapshot update: `UPDATE_SNAPSHOTS=1`" (Task 7a).

- [ ] **Step 8: Commit**

```bash
git add dotnet .gitignore
git commit -m "feat(mcp): scaffold dotnet solution, server stub, tests and FakeEngine" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `CommandCatalog` from the embedded upstream JSON

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Derive sources, pages, option applicability and option metadata from `cli-commands.json` + `cli-help.json` (embedded by link), with the hand-written type table and the drift tests of spec §4.1/§10.1.

**Files:**
- Modify: `dotnet/src/Ccusage.Mcp/Ccusage.Mcp.csproj` (embedded resources)
- Create: `dotnet/src/Ccusage.Mcp/Commands/OptionKind.cs`, `OptionTypeTable.cs`, `CommandCatalog.cs`, `dotnet/src/Ccusage.Mcp/Json/McpJsonContext.cs` (catalog DTOs only for now)
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Commands/CommandCatalogTests.cs`, `OptionTypeTableDriftTests.cs`

**Interfaces (produces):**

```csharp
public enum OptionKind { Bool, NegatableBool, String, Date, Int, Number, IntOrMax, Enum, CsvEnum, Path, PathList }

public sealed record OptionSpec(
    string Name,                 // long flag without "--", e.g. "start-of-week"
    string? ShortAlias,          // "w" for "-w", null if none
    bool TakesValue,             // "<x>" or "[x]" in flags
    bool ValueOptional,          // "[x]"
    string Description,
    string? Default,
    OptionKind Kind,
    IReadOnlyList<string> EnumValues);   // empty unless Kind is Enum/CsvEnum

public sealed record PageSpec(string Source, string Report, string Description, IReadOnlyList<OptionSpec> Options);
public sealed record SourceSpec(string Id, string DisplayName, IReadOnlyList<string> Reports);

public sealed class CommandCatalog
{
    public static CommandCatalog Load();                       // from embedded resources (cached singleton: CommandCatalog.Instance)
    public IReadOnlyList<SourceSpec> Sources { get; }          // "all" first, then the 18 agents in cli-commands.json root order
    public IReadOnlyList<string> Reports { get; }              // daily, weekly, monthly, session, blocks, statusline
    public bool TryGetPage(string source, string report, out PageSpec page);
    public IReadOnlyList<string> SourcesWithReport(string report);
    public IReadOnlyList<string> SourcesAccepting(string report, string optionName);
    public IReadOnlyList<OptionSpec> UnionOptions(string report);   // over all sources that have the report, catalog order, deduped by Name
    public static IReadOnlySet<string> NeverExposed { get; }        // json, compact, color, no-color, help, version, all
}

public static class OptionTypeTable
{
    public static IReadOnlyDictionary<string, (OptionKind Kind, string[] EnumValues)> Entries { get; }
    public static IReadOnlyDictionary<string, string> DisplayNames { get; }   // source id -> display name
}
```

Derivation rules (the tests below pin them):
- Pages: every `pages[]` entry with an `options` key. `path` of length 1 whose name is a report (`daily|weekly|monthly|session|blocks|statusline`) is source `all`, except `blocks` and `statusline`, which are source `claude`. `path` of length 2 is `(source, report)`. The root `claude/blocks` and `claude/statusline` pages duplicate the root ones; keep the first seen.
- Sources: `pages[]` entries that have a `commands` key, in file order; `all` is synthesized first with reports `daily, weekly, monthly, session`.
- Page options: `options` is either a key in `combinedOptions` (a list of group names) or a group name in `cli-help.json`; concatenate the groups' entries in order; drop entries whose long name is in `NeverExposed` and drop `--no-<x>` entries (negation is represented by `Kind == NegatableBool` on `<x>`); dedupe by long name keeping the first.
- Flags parsing: `"-s, --since <since>"` → ShortAlias `s`, Name `since`, TakesValue true, ValueOptional false; `"--speed [speed]"` → TakesValue true, ValueOptional true; `"--by-agent"` → TakesValue false.
- Kind and enum values: Kind from `OptionTypeTable.Entries[name]`; `EnumValues` from the JSON `choices` when present, else from the table entry.
- `OptionTypeTable.Entries` (exactly): `since Date`, `until Date`, `timezone String`, `offline NegatableBool`, `cache NegatableBool`, `config Path`, `no-cost Bool`, `sections CsvEnum [daily,weekly,monthly,session]`, `by-agent Bool`, `last Int`, `id String`, `start-of-week Enum`, `mode Enum`, `order Enum`, `breakdown Bool`, `debug Bool`, `debug-samples Int`, `single-thread Bool`, `instances Bool`, `project String`, `project-aliases String`, `speed Enum`, `pi-path PathList`, `open-claw-path PathList`, `active Bool`, `recent Bool`, `token-limit IntOrMax`, `session-length Number`, `visual-burn-rate Enum`, `cost-source Enum`, `refresh-interval Int`, `context-low-threshold Int`, `context-medium-threshold Int`. (`session-length` is `f64` in the engine — `rust/crates/ccusage-cli/src/types.rs:152` — hence `Number`, a kind the spec's list omits; see Open Questions.)
- `DisplayNames`: `claude` Claude Code, `codex` Codex, `opencode` OpenCode, `amp` Amp, `droid` Droid, `codebuff` Codebuff, `hermes` Hermes, `pi` pi-agent, `goose` Goose, `kilo` Kilo, `copilot` GitHub Copilot CLI, `gemini` Gemini CLI, `antigravity` Antigravity, `kimi` Kimi, `qwen` Qwen, `openclaw` OpenClaw, `grok` Grok Build CLI, `zcode` ZCode, `all` All sources. Fallback for an unknown id is the id itself.

- [ ] **Step 1: Embed the JSON by link**

In `Ccusage.Mcp.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="../../../rust/crates/ccusage-cli-parser/src/cli-commands.json" LogicalName="cli-commands.json" />
  <EmbeddedResource Include="../../../rust/crates/ccusage-cli-parser/src/cli-help.json" LogicalName="cli-help.json" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing catalog tests**

`CommandCatalogTests.cs` (all use `CommandCatalog.Instance`):
- `Sources_AreAllPlusEighteenAgentsInRootOrder`: ids equal `["all","claude","codex","opencode","amp","droid","codebuff","hermes","pi","goose","kilo","copilot","gemini","antigravity","kimi","qwen","openclaw","grok","zcode"]`.
- `Reports_ForAll`: `all` reports = `daily, monthly, weekly, session` (set equality).
- `Reports_ForCodex`: `daily, monthly, session`; `Reports_ForOpencode` includes `weekly`; `Reports_ForClaude` = `daily, monthly, weekly, session, blocks, statusline`.
- `RootBlocksAndStatusline_BelongToClaude`: `TryGetPage("claude","blocks")` true, `TryGetPage("all","blocks")` false.
- `SourcesWithReport_Weekly`: `["all","claude","opencode"]`.
- `Page_ClaudeDaily_Options`: names in order `since, until, mode, debug, debug-samples, order, breakdown, offline, single-thread, timezone, config, instances, project, project-aliases, last`.
- `Page_AllDaily_Options`: `since, until, timezone, sections, by-agent, offline, no-cost, config, last`.
- `Page_Statusline_Options`: `offline, visual-burn-rate, cost-source, cache, refresh-interval, context-low-threshold, context-medium-threshold, timezone, config, debug`.
- `Options_NeverContainNeverExposed`: no page option named `json|compact|color|no-color|help|version|all`, and none starting with `no-`.
- `Flags_Parsed`: `claude daily` `since` has ShortAlias `s`, TakesValue true, ValueOptional false; `codex daily` `speed` ValueOptional true, EnumValues `auto,standard,flex,fast`; `all weekly` `start-of-week` Default `monday`; `claude weekly` `start-of-week` Default `sunday`.
- `SourcesAccepting_Speed_Daily`: `["codex"]`; `SourcesAccepting_Id_Session`: `["all","claude","codex"]`.
- `UnionOptions_Daily_IsDedupedCatalogOrder`: contains each of `since, until, timezone, sections, by-agent, offline, no-cost, config, last, mode, debug, debug-samples, order, breakdown, single-thread, instances, project, project-aliases, speed, pi-path, open-claw-path` exactly once.
- `Sections_EnumValues_FromTable`: `UnionOptions("daily")` entry `sections` Kind CsvEnum, EnumValues `daily,weekly,monthly,session`.

`OptionTypeTableDriftTests.cs`:
- `EveryHelpFlag_IsTypedOrNeverExposed`: for every entry in every group of `cli-help.json`, the long name (minus `no-` prefix for negations) is in `OptionTypeTable.Entries` or `NeverExposed`.
- `EveryTypedEntry_ExistsInHelp`: every `Entries` key appears in `cli-help.json` (catches stale table rows).
- `EnumKinds_HaveValues`: every option with Kind Enum/CsvEnum has non-empty EnumValues; when the JSON has `choices`, they equal EnumValues.
- `NegatableBools_HaveNoFlagInHelp`: for every `NegatableBool` entry, `--no-<name>` exists somewhere in `cli-help.json`.
- `EverySource_HasDisplayName`: every `Sources[].Id` is a key in `DisplayNames` (and `DisplayNames` has no key that is not a source).

Run: `dotnet test dotnet/Ccusage.Mcp.slnx`. Expected: FAIL (types missing).

- [ ] **Step 3: Implement `OptionKind`, `OptionTypeTable`, `CommandCatalog`**

Parse the resources with `JsonDocument` (no DTO reflection) or with `[JsonSerializable]` DTO records in `McpJsonContext` (`CliCommandsFile`, `CliPage`, `CliHelpEntry`); either is AOT-safe — choose the DTO route and keep `McpJsonContext` as the single context (`[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]`). Resource names are the `LogicalName`s above.

- [ ] **Step 4: Run tests** — Expected: PASS. Run `dotnet build -c Release` — 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): derive command catalog from embedded cli-commands/cli-help json" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `CommandRequest`, `CommandValidator`, argv building

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Validate a normalized request against the catalog and produce the exact engine argv; failures are `ValidationException`s with the user-facing messages the MCP layer returns verbatim.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Commands/CommandRequest.cs`, `CommandValidator.cs`, `ValidationException.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Commands/CommandValidatorTests.cs`, `ArgvBuilderTests.cs`

**Interfaces:**
- Consumes: `CommandCatalog`, `OptionSpec`, `OptionKind` (Task 2).
- Produces:

```csharp
public sealed record OptionValue(string Name, string Raw);          // Raw: bools "true"/"false", numbers invariant text, others verbatim
public sealed record CommandRequest(string Source, string Report, IReadOnlyList<OptionValue> Options, string? Stdin = null);
public sealed class ValidationException(string message) : Exception(message);
public static class CommandValidator
{
    public static IReadOnlyList<string> ValidateAndBuildArgv(CommandCatalog catalog, CommandRequest request); // throws ValidationException
}
```

Argv rules: `[source]` omitted when `all`; `claude blocks`/`claude statusline` emit `blocks`/`statusline` **without** `claude` (the root pages); then `report`; then `--json` for every report except `statusline`; then options **in the page's catalog order** (not request order). Bool `true` → `--name`; Bool `false` → nothing; NegatableBool `true` → `--name`, `false` → `--no-name`; everything else → `--name`, `value` as two elements, value verbatim (no normalization, so parity with a direct CLI call holds). Duplicate option names in a request → error.

Messages (exact, tests assert them):
- `Unknown source '{s}'. Valid sources: all, claude, codex, ...` (full list, comma+space).
- `Unknown report '{r}'. Valid reports: daily, weekly, monthly, session, blocks, statusline.`
- `Report '{r}' is not available for source '{s}'. Sources with '{r}': a, b, c.`
- `Parameter '{p}' does not apply to '{s} {r}'. Sources that accept it for '{r}': a, b.` (if the report has no source accepting it at all: `Parameter '{p}' is not a parameter of '{r}'.`)
- `Parameter '{p}' was given more than once.`
- `Parameter '{p}' must be one of: a, b, c (got '{v}').` — Enum; CsvEnum checks each comma-separated item, trimmed, non-empty: `Parameter 'sections' contains '{item}'; each item must be one of: daily, weekly, monthly, session.`
- `Parameter '{p}' must be a date in YYYY-MM-DD or YYYYMMDD form (got '{v}').` — regex `^\d{4}-\d{2}-\d{2}$|^\d{8}$` and a real calendar date (`DateOnly.TryParseExact` with both patterns).
- `Parameter '{p}' must be an integer >= {min} (got '{v}').` — Int; min 1 for `last`, 0 otherwise.
- `Parameter '{p}' must be a number >= 0 (got '{v}').` — Number (`double.TryParse` invariant).
- `Parameter '{p}' must be a positive integer or "max" (got '{v}').` — IntOrMax.
- `Parameter '{p}' must be true or false (got '{v}').` — Bool/NegatableBool.
- `Parameter '{p}' must not be empty.` — String/Path/PathList/Date with empty Raw.
- `Parameter 'timezone' must be an IANA time zone id (got '{v}').` — only when `TimeZoneInfo.TryFindSystemTimeZoneById` fails **and** the value contains whitespace or is empty; otherwise pass through (the engine owns the authoritative check, and Windows tz databases vary).

- [ ] **Step 1: Write the failing validator tests**

`CommandValidatorTests.cs` (table-driven with `[DataRow]`):
- Valid pairs: every `(source, report)` in the catalog validates with no options → argv starts as expected (`("all","daily") → ["daily","--json"]`, `("claude","blocks") → ["blocks","--json"]`, `("claude","statusline") → ["statusline"]`, `("codex","session") → ["codex","session","--json"]`).
- Invalid pairs: `("codex","weekly")`, `("codex","blocks")`, `("all","blocks")`, `("opencode","statusline")` → message `Report 'weekly' is not available for source 'codex'. Sources with 'weekly': all, claude, opencode.` etc.
- `Validate_UnknownSource`, `Validate_UnknownReport`.
- `Validate_UnknownParameter_Rejected`: `("all","daily",[("bogus","1")])` → `Parameter 'bogus' is not a parameter of 'daily'.`
- `Validate_InapplicableParameter_NamesAcceptingSources`: `("claude","daily",[("speed","fast")])` → `... Sources that accept it for 'daily': codex.`
- Enum: `mode=fast` rejected; `mode=display` ok; `start-of-week=Monday` rejected (case-sensitive).
- CsvEnum: `sections=daily,session` ok; `sections=daily,hourly` rejected with the item message; `sections=` rejected (`must not be empty`).
- Date: `20260101`, `2026-01-01` ok; `2026/01/01`, `20261301`, `2026-02-30` rejected.
- Int: `last=1` ok, `last=0` and `last=-1` and `last=1.5` rejected; `debug-samples=0` ok.
- IntOrMax: `token-limit=500000`, `token-limit=max` ok; `token-limit=0`, `token-limit=MAX`, `token-limit=lots` rejected.
- Number: `session-length=5`, `session-length=2.5` ok; `session-length=abc` rejected.
- Bool: `offline=true/false` ok; `offline=yes` rejected; `by-agent=1` rejected.
- Duplicate: two `since` → `Parameter 'since' was given more than once.`

`ArgvBuilderTests.cs`:
- `("all","daily",[last=2, by-agent=true, offline=true])` → `["daily","--json","--by-agent","--offline","--last","2"]` (catalog order: `sections, by-agent, offline, no-cost, config, last` → by-agent, offline, last).
- `("claude","daily",[project="My Project", instances=true, offline=false])` → `["claude","daily","--json","--no-offline","--instances","--project","My Project"]` (page order: mode, debug, debug-samples, order, breakdown, offline, single-thread, timezone, config, instances, project, project-aliases, last; NegatableBool false → `--no-offline`).
- `("claude","blocks",[token-limit=max, session-length=2.5, active=true])` → `["blocks","--json","--active","--token-limit","max","--session-length","2.5"]`.
- `("claude","statusline",[cache=false, visual-burn-rate=emoji])` → `["statusline","--visual-burn-rate","emoji","--no-cache"]` (no `--json`; order from the statusline page).
- `("codex","daily",[speed=flex])` → `["codex","daily","--json","--speed","flex"]`.
- `("pi","daily",[pi-path="/a,/b c"])` → `["pi","daily","--json","--pi-path","/a,/b c"]` (one element).
- `("all","weekly",[start-of-week=monday])` → `["weekly","--json","--start-of-week","monday"]`.
- Bool false on plain Bool emits nothing: `("all","daily",[no-cost=false])` → `["daily","--json"]`.

Run tests. Expected: FAIL.

- [ ] **Step 2: Implement `CommandRequest`, `ValidationException`, `CommandValidator`** with the signatures and messages above.

- [ ] **Step 3: Run tests** — Expected: PASS. `dotnet build -c Release` — 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): validate requests against the catalog and build engine argv" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `CliArgsParser`

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Turn CLI-style text (`report` tool `args`, prompt `args`, `/ccusage $ARGUMENTS`) into a `CommandRequest` using the grammar of spec §5.5.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Commands/CliArgsParser.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Commands/CliArgsParserTests.cs`

**Interfaces:**
- Consumes: `CommandCatalog` (`Sources`, `Reports`, `TryGetPage`, `NeverExposed`), `CommandRequest`, `ValidationException`.
- Produces: `public static class CliArgsParser { public static IReadOnlyList<string> Tokenize(string args); public static CommandRequest Parse(CommandCatalog catalog, string args); }`

Grammar: tokenize on whitespace; a double-quoted span (`"..."`) is one token with the quotes removed and may contain spaces; `\"` inside quotes is a literal quote; an unterminated quote → `Unterminated quoted value in: {args}`. Then: if the first token is a source id → source, consume; if the next token is a report name → report, consume; else report `daily`; `source` defaults to `all`. Then flags only; the page is `(source, report)` (validated by `TryGetPage`; failure → the Task 3 pair message). `--name=value` and `--name value`; `-x` short aliases resolved against **that page's** options (`-i` is `instances` on `claude daily` and `id` on `claude session`); `--name` for an option with `TakesValue && !ValueOptional` needs a value (`Flag '--since' needs a value.`); for `ValueOptional`, the next token is consumed only if it exists and does not start with `-`, otherwise Raw is the option's `Default` (and if `Default` is null, `"true"`); for `TakesValue == false`, Raw `"true"`; `--no-<name>` for a `NegatableBool` → Raw `"false"`; `--no-<name>` otherwise → `Unknown argument '--no-<name>'.`; a bare non-flag token after flags began → `Unknown argument '{tok}'.`; a flag in `NeverExposed` (`--json`, `-j`, `--compact`, `--color`, `--no-color`, `--help`, `-h`, `--version`, `-v`, `--all`) → `Flag '{flag}' is not available through the MCP server.`; report `statusline` (with or without `claude`) → `'statusline' needs a hook payload; use the statusline tool.`; unknown long flag → `Unknown argument '--foo'.`. Empty or whitespace-only `args` → `("all","daily",[])`. Parsing does not validate values (Task 3 does).

- [ ] **Step 1: Write the failing tests**

- `Tokenize_Quotes`: `claude daily --project "My Project" --since 20260101` → 6 tokens with `My Project` intact; `--project="A B"` → one token `--project=A B`.
- `Tokenize_UnterminatedQuote_Throws`.
- `Parse_Empty_IsAllDaily`: `""` and `"   "` → Source `all`, Report `daily`, no options.
- `Parse_ReportOnly`: `weekly --last 2 --by-agent` → all/weekly, options `[("last","2"),("by-agent","true")]` in input order.
- `Parse_SourceAndReport`: `codex daily --since 2026-10-01` → codex/daily.
- `Parse_SourceOnly_DefaultsDaily`: `codex` → codex/daily.
- `Parse_EqualsForm`: `--since=20260101`.
- `Parse_ShortAlias_PerPage`: `claude daily -i` → `instances=true`; `claude session -i abc` → `id=abc`; `claude daily -s 20260101 -O` → since, offline=true.
- `Parse_OptionalValue`: `codex daily --speed` → `speed=auto` (Default); `codex daily --speed fast` → `fast`; `codex daily --speed --offline` → speed=auto, offline=true; `claude blocks -n` → `session-length=5`.
- `Parse_NegatedFlag`: `claude daily --no-offline` → `offline=false`; `claude daily --no-breakdown` → `Unknown argument '--no-breakdown'.`
- `Parse_NeverExposed`: each of `--json`, `-j`, `--compact`, `--color`, `--no-color`, `--help`, `-h`, `--version`, `-v`, `--all` → `Flag '...' is not available through the MCP server.`
- `Parse_Statusline_Rejected`: `statusline` and `claude statusline --offline` → the statusline message.
- `Parse_UnknownToken`: `daily foo` → `Unknown argument 'foo'.`; `daily --bogus` → `Unknown argument '--bogus'.`
- `Parse_InvalidPair`: `codex weekly` → the Task 3 pair message.
- `Parse_MissingValue`: `daily --since` → `Flag '--since' needs a value.`

Run. Expected: FAIL.

- [ ] **Step 2: Implement `CliArgsParser`.**

- [ ] **Step 3: Run tests** — PASS; Release build 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): parse CLI-style arguments into command requests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `EngineLocator`

**Implementer:** Opus  **Reviewer:** Opus

**Phase 0 dependency:** Q5. If the addendum says `RuntimeInformation.OSArchitecture` reports the OS architecture under emulation (or the runtime source confirms it), use it; otherwise add the `IsWow64Process2` `[LibraryImport]` fallback described in step 3.

**Goal:** Resolve the engine path per spec §6.1 and report every checked location on failure; also add the engine line to `--version`.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Engine/EngineLocator.cs`, `EngineNotFoundException.cs`
- Modify: `dotnet/src/Ccusage.Mcp/Program.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Engine/EngineLocatorTests.cs`

**Interfaces (produces):**

```csharp
public sealed class EngineNotFoundException(IReadOnlyList<string> checkedPaths) : Exception
{   // Message: "ccusage engine not found. Checked:\n  <path1>\n  <path2>..." ; set CCUSAGE_ENGINE or install ccusage next to ccusage-mcp
    public IReadOnlyList<string> CheckedPaths { get; } }

public sealed class EngineLocator(
    Func<string, string?> getEnv,          // Environment.GetEnvironmentVariable in production
    string serverDirectory,                // AppContext.BaseDirectory in production
    Architecture osArchitecture,           // RuntimeInformation.OSArchitecture (or the Q5 fallback) in production
    bool isWindows)                        // OperatingSystem.IsWindows()
{
    public static EngineLocator Default { get; }
    public string Resolve();                                   // throws EngineNotFoundException
    public IReadOnlyList<string> CandidatePaths();             // in resolution order, for messages and list-sources
}
```

Candidates in order: (1) `CCUSAGE_ENGINE` if set and non-empty (as given); (2) `<serverDirectory>/ccusage[.exe]`; (3) `<serverDirectory>/<arch>/ccusage[.exe]` where `<arch>` is `x64` for `Architecture.X64`, `arm64` for `Architecture.Arm64` (other architectures skip rule 3); (4) each directory of `PATH` (split on `Path.PathSeparator`, skip empties) joined with `ccusage[.exe]`. `Resolve` returns the first candidate for which `File.Exists` is true. `.exe` suffix only when `isWindows`.

- [ ] **Step 1: Write the failing tests** (each builds a temp directory tree with `Directory.CreateTempSubdirectory`, writes empty files, and uses a dictionary-backed `getEnv`):
- `Resolve_PrefersEnvVar` (env file exists; also a sibling `ccusage` exists → env wins).
- `Resolve_EnvVarMissingFile_IsSkipped_AndListed` (env points at a missing file, sibling exists → sibling returned; `CandidatePaths()[0]` is the env path).
- `Resolve_SiblingBeforeArchFolder`.
- `Resolve_ArchFolder_UsesOsArchitecture` (`Architecture.Arm64`, file in `arm64/`, none in `x64/` → arm64 path; with `Architecture.X64` → throws).
- `Resolve_Path_LastResort` (PATH with two dirs, file in the second).
- `Resolve_Windows_UsesExe` (`isWindows: true`, file `ccusage.exe`), `Resolve_NonWindows_NoExe`.
- `Resolve_NotFound_ListsEveryCandidate` (message contains env path, sibling path, arch path, both PATH entries, in order).

Run. Expected: FAIL.

- [ ] **Step 2: Implement `EngineLocator` and `EngineNotFoundException`.**

- [ ] **Step 3: Q5 fallback (only if Phase 0 requires it)**

Add `private static partial class NativeMethods` with `[LibraryImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWow64Process2(nint hProcess, out ushort processMachine, out ushort nativeMachine);` and `public static Architecture DetectOsArchitecture()` mapping `nativeMachine` `0xAA64` → Arm64, `0x8664` → X64, else `RuntimeInformation.OSArchitecture`; use it for `Default` on Windows. No test (needs hardware); document in `dotnet/AGENTS.md`.

- [ ] **Step 4: `--version` engine line**

In `Program.cs`, after the server line: try `EngineLocator.Default.Resolve()`, run it with `--version` (plain `Process.Start`, 10 s wait; the full runner arrives in Task 6) and print its first stdout line (`ccusage 20.0.45`); on `EngineNotFoundException` print `ccusage: not found` followed by the `Checked:` list, exit code stays 0. [local] run `dotnet run --project dotnet/src/Ccusage.Mcp -- --version` with `CCUSAGE_ENGINE` pointing at a cargo-built `rust/target/release/ccusage.exe` → two lines `ccusage-mcp 20.0.45` / `ccusage 20.0.45`.

- [ ] **Step 5: Run tests** — PASS. Commit:

```bash
git add dotnet
git commit -m "feat(mcp): locate the ccusage engine by env, server dir, arch folder and PATH" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `EngineRunner` with FakeEngine tests

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Run the engine without a shell, capture stdout/stderr concurrently, deliver stdin for `statusline`, enforce the timeout and cancellation by killing the process tree (spec §6.2, §10.2).

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Engine/EngineRunner.cs`, `EngineResult.cs`, `EngineInvocation.cs`
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Engine/EngineRunnerTests.cs`

**Interfaces (produces):**

```csharp
public sealed record EngineInvocation(string EnginePath, IReadOnlyList<string> Argv, string? Stdin);
public sealed record EngineResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);
public sealed class EngineRunner(TimeSpan timeout, Action<string> logWarning)   // logWarning writes to stderr in production
{
    public static TimeSpan TimeoutFromEnvironment(Func<string, string?> getEnv, Action<string> logWarning); // CCUSAGE_MCP_TIMEOUT_SECONDS, default 120 s; invalid or <= 0 -> warn once, 120 s
    public Task<EngineResult> RunAsync(EngineInvocation invocation, CancellationToken cancellationToken);
}
```

Behavior: `ProcessStartInfo` with `FileName = EnginePath`, `ArgumentList` filled from `Argv`, `UseShellExecute=false`, `CreateNoWindow=true`, `RedirectStandardOutput/Error=true`, `RedirectStandardInput = Stdin is not null`, `StandardOutputEncoding/StandardErrorEncoding = UTF8`, environment inherited plus `NO_COLOR=1`. Start two `ReadToEndAsync` tasks before writing stdin; write stdin then close it. Wait with a linked `CancellationTokenSource` (caller token + `CancelAfter(timeout)`); on expiry or cancellation call `process.Kill(entireProcessTree: true)`, await exit, return `TimedOut = true` for timeout; for caller cancellation rethrow `OperationCanceledException` after the kill. Warning text for invalid env: `CCUSAGE_MCP_TIMEOUT_SECONDS='{v}' is not a positive number; using 120.`

- [ ] **Step 1: Write the failing tests** (all use `TestPaths.FakeEnginePath`; runner timeout 10 s unless stated):
- `RunAsync_EchoesArgvAndExitZero`: argv `["daily","--json"]` → ExitCode 0, stdout JSON `argv` equals, Stderr `""`, TimedOut false.
- `RunAsync_PassesSpacesQuotesAndUnicodeAsSingleArgv`: argv `["claude","daily","--project","My \"Quoted\" Project — 日本語"]` → echoed `argv[3]` equals exactly.
- `RunAsync_DeliversStdin`: `Stdin = "{\"session_id\":\"x\"}"`, argv `["statusline"]` → echo `stdin` equals.
- `RunAsync_CapturesStderr`: `--fake-stderr=warn: pricing` → Stderr contains `warn: pricing`, ExitCode 0.
- `RunAsync_NonZeroExit`: `--fake-exit=3` → ExitCode 3.
- `RunAsync_LargeStdout_DoesNotDeadlock`: `--fake-stdout-bytes=8388608` → Stdout.Length == 8388608 within 10 s.
- `RunAsync_Timeout_KillsAndReportsTimedOut`: runner timeout 1 s, `--fake-hang` → the task completes within 5 s, `TimedOut` true, `Stdout` holds the echo written before the hang.
- `RunAsync_Cancellation_KillsProcess`: cancel after 300 ms with `--fake-hang` → throws `OperationCanceledException` within 5 s.
- `RunAsync_SetsNoColor`: FakeEngine `--fake-stdout` is not needed; instead extend FakeEngine echo with `"noColor": Environment.GetEnvironmentVariable("NO_COLOR")` and assert `"1"` (update FakeEngine's `FakeEcho` record and `dotnet/AGENTS.md`).
- `TimeoutSeconds_InvalidEnv_FallsBackTo120`: for `abc`, `0`, `-5`, `""`, null → 120 s; warning invoked exactly once for the first three, never for empty/null; `45` → 45 s.

Run. Expected: FAIL.

- [ ] **Step 2: Implement `EngineRunner`, `EngineResult`, `EngineInvocation`.**

- [ ] **Step 3: Run tests** — PASS (run twice to check for flakiness in the timeout/cancel tests). Release build 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): run the engine with concurrent capture, stdin, timeout and cancellation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7a: `ToolDefinitions`, JSON schemas, prompt definition, schema snapshot

**Implementer:** Opus  **Reviewer:** Fable

**Phase 0 dependency:** Q6 — use the exact SDK type names recorded in the addendum (`Tool`, `ToolAnnotations`, `Prompt`, `PromptArgument`, `ListToolsResult`, `ListPromptsResult`, `McpJsonUtilities.DefaultOptions`).

**Goal:** Generate the MCP tool list (eight tools with JSON input schemas from the catalog) and the `ccusage` prompt definition, pinned by checked-in snapshots.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Tools/ToolDefinitions.cs`, `dotnet/src/Ccusage.Mcp/Prompts/PromptDefinitions.cs`
- Modify: `dotnet/src/Ccusage.Mcp/Json/McpJsonContext.cs` (add schema-building DTOs if used), `dotnet/tests/Ccusage.Mcp.Tests/Ccusage.Mcp.Tests.csproj` (`Snapshots/**` as `Content` copied to output; `AssemblyMetadata` `SnapshotsSourceDir` = `$(MSBuildThisFileDirectory)Snapshots`)
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Tools/ToolDefinitionsTests.cs`, `SnapshotTests.cs`, `dotnet/tests/Ccusage.Mcp.Tests/Snapshots/tools-list.json`, `prompts-list.json`

**Interfaces (produces):**

```csharp
public static class ToolDefinitions
{
    public static IReadOnlyList<Tool> Build(CommandCatalog catalog);            // order: daily, weekly, monthly, session, blocks, statusline, list-sources, report
    public static JsonElement BuildReportSchema(CommandCatalog catalog, string report);  // for the four source-parameterized tools
}
public static class PromptDefinitions { public static Prompt Ccusage { get; } }
```

Schema rules (every schema: `"type":"object"`, `"additionalProperties": false`, `properties` in catalog order, `required` only where stated):
- `daily|weekly|monthly|session`: property `source` first: `{"type":"string","enum":["all", ...sources with that report...],"default":"all","description":"Data source. Default: all."}`; then `UnionOptions(report)` mapped by kind: Bool/NegatableBool → `{"type":"boolean"}`; Enum → `{"type":"string","enum":[...]}`; CsvEnum → `{"type":"string"}` with description suffix ` Comma-separated; each item one of: a, b, c.`; Date → `{"type":"string","pattern":"^(\\d{4}-\\d{2}-\\d{2}|\\d{8})$"}`; Int → `{"type":"integer","minimum":1}` for `last`, `minimum 0` otherwise; Number → `{"type":"number","minimum":0}`; IntOrMax → `{"type":"string","pattern":"^([1-9]\\d*|max)$"}` (a JSON integer is also accepted at call time, Task 7b); String/Path/PathList → `{"type":"string"}`. Each description = help description + (if `Default` non-null) ` Default: {Default}.` + ` Applies to: a, b, c.` listing `SourcesAccepting(report, name)` (omit the "Applies to" suffix when it equals every source that has the report).
- `blocks`: no `source`; options of page `(claude, blocks)` with the same mapping.
- `statusline`: `hook-input` `{"type":"object","description":"Claude Code status-line hook payload (session_id, transcript_path, cwd, model, workspace, version, cost, context_window). Written to the engine's stdin unchanged."}` first and `required: ["hook-input"]`; then the `(claude, statusline)` options.
- `list-sources`: `{"type":"object","properties":{},"additionalProperties":false}`.
- `report`: `args` `{"type":"string","description":"CLI-style arguments as typed after `ccusage`, e.g. \"codex daily --since 2026-10-01\", \"weekly --last 2 --by-agent\", \"\" (= daily, all sources)."}`, `required: ["args"]`.
- Tool descriptions: `daily`: `Token usage and cost grouped by day. Default source: all detected sources. Long ranges produce large JSON; prefer since/until or last.`; `weekly`/`monthly`/`session` analogous (`grouped by week` / `grouped by month` / `grouped by session`); `blocks`: `Claude Code usage grouped by 5-hour billing blocks (Claude only).`; `statusline`: `Claude Code status line text for a hook payload (Claude only; returns plain text).`; `list-sources`: `List supported sources, their reports, and the server/engine versions and engine path.`; `report`: `Run any ccusage report from CLI-style arguments; same validation as the typed tools.`
- Annotations on every tool: `ReadOnlyHint = true`, `DestructiveHint = false`, `IdempotentHint = true`, `OpenWorldHint` unset (null).
- Prompt `ccusage`: description `Run a ccusage report and present it as a readable table.`; one argument `args` (`required: false`), description identical to the `report` tool's `args` description.

- [ ] **Step 1: Write the failing tests**

`ToolDefinitionsTests.cs`:
- `Build_ReturnsEightToolsInOrder` (names).
- `Daily_SourceEnum_IsAllPlusEverySourceWithDaily` (19 values, `all` first).
- `Weekly_SourceEnum_IsAllClaudeOpencode`.
- `Daily_Schema_HasUnionProperties_AdditionalPropertiesFalse` (contains `speed`, `instances`, `sections`; `additionalProperties` false; no `required`).
- `Daily_Speed_Description_AppliesToCodex` (ends with `Applies to: codex.`).
- `Daily_Since_HasNoAppliesTo` (every daily source accepts `since`).
- `Blocks_HasNoSource_AndTokenLimitPattern`.
- `Statusline_RequiresHookInput_ObjectType`.
- `Report_RequiresArgs`; `ListSources_EmptyProperties`.
- `Annotations_ReadOnlyIdempotentNotDestructive_OnEveryTool` (and `OpenWorldHint` is null).
- `Prompt_HasArgsArgument`.

`SnapshotTests.cs`:
- `ToolsList_MatchesSnapshot`: serialize `new ListToolsResult { Tools = ToolDefinitions.Build(catalog) }` with `new JsonSerializerOptions(McpJsonUtilities.DefaultOptions) { WriteIndented = true }`; compare to `Snapshots/tools-list.json` (normalize `\r\n` → `\n`); when env `UPDATE_SNAPSHOTS` is `1`, overwrite the **source** snapshot file (path from assembly metadata `SnapshotsSourceDir` = `$(MSBuildThisFileDirectory)Snapshots`) and fail with `Snapshot updated; re-run.` so updates are never silent.
- `PromptsList_MatchesSnapshot` likewise with `ListPromptsResult`.

Create the two snapshot files empty; run tests → FAIL.

- [ ] **Step 2: Implement `ToolDefinitions` and `PromptDefinitions`**

Build schemas with `Utf8JsonWriter` into a buffer and `JsonDocument.Parse(...).RootElement.Clone()` (no reflection). Then run with `UPDATE_SNAPSHOTS=1` once, inspect the generated `tools-list.json` by eye against spec §5 (every §5.1 option appears on the right tools; `statusline` has no `--json`-ish entries), re-run without the env var.

- [ ] **Step 3: Run tests** — PASS; Release build 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): generate tool and prompt definitions with schema snapshots" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7b: `ToolHandlers`, `PromptHandlers`, `Program` wiring, AOT publish

**Implementer:** Opus  **Reviewer:** Fable

**Phase 0 dependency:** Q6 — handler registration API and delegate signatures exactly as recorded in the addendum.

**Goal:** `tools/list`, `tools/call`, `prompts/list`, `prompts/get` over stdio, the server publishing as Native AOT with zero trim/AOT warnings.

**Files:**
- Create: `dotnet/src/Ccusage.Mcp/Tools/ToolHandlers.cs`, `dotnet/src/Ccusage.Mcp/Prompts/PromptHandlers.cs`, `dotnet/src/Ccusage.Mcp/Tools/ToolArguments.cs`
- Modify: `dotnet/src/Ccusage.Mcp/Program.cs`, `dotnet/src/Ccusage.Mcp/Json/McpJsonContext.cs` (add `ListSourcesResult`, `SourceInfo`)
- Test: `dotnet/tests/Ccusage.Mcp.Tests/Tools/ToolHandlersTests.cs`, `dotnet/tests/Ccusage.Mcp.Tests/Prompts/PromptHandlersTests.cs`

**Interfaces:**
- Consumes: Tasks 2–7a (`CommandCatalog`, `CommandValidator.ValidateAndBuildArgv`, `CliArgsParser.Parse`, `EngineLocator`, `EngineRunner`, `ToolDefinitions`, `PromptDefinitions`).
- Produces:

```csharp
public static class ToolArguments
{   // JSON -> CommandRequest; throws ValidationException
    public static CommandRequest FromTyped(CommandCatalog catalog, string tool, IReadOnlyDictionary<string, JsonElement>? args);
}
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
public sealed record SourceInfo([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("display-name")] string DisplayName, [property: JsonPropertyName("reports")] IReadOnlyList<string> Reports);
public sealed record ListSourcesResult(/* server-version, engine-version (nullable), engine-path (nullable), engine-error (nullable, omitted when null), sources */);
```

`ToolArguments.FromTyped` rules: unknown tool → `ValidationException("Unknown tool '{name}'.")`; `source` (daily/weekly/monthly/session) read as string, default `all`; `blocks`/`statusline` → source `claude`; every other property: JSON `true/false` → `"true"/"false"`; JSON number → `GetRawText()`; JSON string → the string; `null` → ignored; any other kind → `Parameter '{p}' has an unsupported JSON type ({kind}).`; `hook-input` must be `JsonValueKind.Object` → `Stdin = GetRawText()`, else `Parameter 'hook-input' must be a JSON object.`; missing `hook-input` → `Parameter 'hook-input' is required.`; properties not in the page are left for `CommandValidator` to reject (unknown-parameter message).

`CallAsync` results: success → `Content[0]` = `TextContentBlock(Stdout)`; if `Stderr` non-empty → `Content[1]` = `TextContentBlock("stderr:\n" + Stderr)`; `ValidationException` → `IsError = true`, `Content[0]` text = message (engine not started); `EngineNotFoundException` → `IsError`, its message; non-zero exit → `IsError`, text `ccusage exited with code {n}.\n{Stderr}`; `TimedOut` → `IsError`, `ccusage timed out after {seconds} s.\n{Stderr}`; `list-sources` → one text block with the `ListSourcesResult` JSON (indented), `engine-version` from running `--version` and stripping the `ccusage ` prefix; `report` → `CliArgsParser.Parse` then the same path; `statusline` success → the engine's plain text (block 1).

`GetAsync("ccusage", args)`: `args.args` string (missing → `""`); on success one `PromptMessage` role `user` whose text is exactly:

```
ccusage {args}

```json
{stdout}
```

Present this report as a readable table with totals, and call out the main cost drivers (the models, days, or sessions with the highest cost).
```

(`{args}` trimmed; for empty args the first line is `ccusage daily` — the resolved request rendered as `source report` with `all` omitted.) On any error: text `ccusage {args} failed:\n\n{error text}` where error text is the same string the tool would return. `GetAsync` for any other name → throw `McpException` with `Unknown prompt '{name}'.`

- [ ] **Step 1: Write the failing handler tests** (locator with `getEnv` returning `TestPaths.FakeEnginePath` for `CCUSAGE_ENGINE`, runner timeout 10 s):
- `ListAsync_ReturnsDefinitions` (8 tools, same as `ToolDefinitions.Build`).
- `CallAsync_Daily_BuildsArgvAndPassesStdout`: args `{"source":"codex","since":"20260101","offline":true}` → block 0 JSON `argv` == `["codex","daily","--json","--since","20260101","--offline"]`, `IsError` false/null, one block.
- `CallAsync_Blocks_NoSourceParameter`: `{"token-limit":500000}` (JSON number) → argv `["blocks","--json","--token-limit","500000"]`.
- `CallAsync_Statusline_WritesStdin`: `{"hook-input":{"session_id":"s"},"offline":true}` → argv `["statusline","--offline"]` and echo `stdin` == `{"session_id":"s"}`.
- `CallAsync_Statusline_RejectsNonObjectHookInput`: `"x"`, `[1]`, missing → `IsError`, message as above; FakeEngine not run (echo absent).
- `CallAsync_Statusline_HookInput_Missing`.
- `CallAsync_ValidationError_IsError`: `{"source":"codex","speed":"turbo"}` on `daily` → `IsError`, message starts `Parameter 'speed' must be one of`.
- `CallAsync_UnknownTool_IsError`.
- `CallAsync_StderrBecomesSecondBlock`: FakeEngine rule: when argv contains `--debug` it also writes `debug: fake` to stderr (added in step 2). Tool `daily` with `{"source":"claude","debug":true}` → 2 blocks, block 1 text starts with `stderr:` and contains `debug: fake`, `IsError` not true.
- `CallAsync_NonZeroExit_IsError`: FakeEngine returns exit 4 when argv contains `--project` `"fail"` (add rule: `--project fail` → exit 4 with stderr `boom`); assert text `ccusage exited with code 4.\nboom`.
- `CallAsync_Timeout_IsError`: runner timeout 1 s; FakeEngine hangs when `--id hang` (add rule); assert text starts `ccusage timed out after 1 s.`
- `CallAsync_EngineMissing_IsError_ListsPaths`: locator with env pointing to a missing path and empty PATH → `IsError`, text contains `Checked:`.
- `CallAsync_PassesNonJsonStdoutUnchanged`: FakeEngine rule `--project plain` → stdout `not json\n` exit 0 → block 0 text == `not json\n`.
- `CallAsync_ListSources_Shape`: parse block 0; `server-version` == `ServerVersion.Value`; `engine-version` == `0.0.0-fake`; `engine-path` == FakeEnginePath; `sources[0].id` == `all`, `sources[1]` == `{claude, "Claude Code", [daily, monthly, weekly, session, blocks, statusline]}` (order from the catalog); `engine-error` absent.
- `CallAsync_ListSources_EngineMissing`: `engine-version` null, `engine-error` contains `Checked:`, still not `IsError`.
- `CallAsync_Report_UsesParser`: `{"args":"weekly --last 2"}` → argv `["weekly","--json","--last","2"]`; `{"args":"statusline"}` → `IsError` with the statusline message.

`PromptHandlersTests.cs`:
- `ListAsync_ReturnsCcusagePrompt`.
- `GetAsync_Success_FormatsMessage`: args `"codex daily"` → one user message; text starts `ccusage codex daily\n\n```json\n`, contains the echo JSON, ends with the instruction sentence.
- `GetAsync_EmptyArgs_RendersDaily`: first line `ccusage daily`.
- `GetAsync_Error_FormatsFailure`: `"codex weekly"` → text starts `ccusage codex weekly failed:\n\nReport 'weekly' is not available`.
- `GetAsync_UnknownPrompt_Throws`.

Run. Expected: FAIL.

- [ ] **Step 2: Implement `ToolArguments`, `ToolHandlers`, `PromptHandlers`, extend FakeEngine** (rules: `--debug` → stderr `debug: fake`; `--project fail` → exit 4 + stderr `boom`; `--project plain` → stdout `not json\n`; `--id hang` → hang after stdout). Update `dotnet/AGENTS.md`.

- [ ] **Step 3: Wire `Program.cs`**

`Host.CreateApplicationBuilder(args)`; `builder.Logging.ClearProviders(); builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);` (stdout stays clean); register `CommandCatalog.Instance`, `EngineLocator.Default`, `new EngineRunner(EngineRunner.TimeoutFromEnvironment(Environment.GetEnvironmentVariable, msg => Console.Error.WriteLine(msg)), msg => Console.Error.WriteLine(msg))`, `ToolHandlers`, `PromptHandlers` as singletons; `builder.Services.AddMcpServer(o => { o.ServerInfo = new() { Name = "ccusage", Version = ServerVersion.Value }; }).WithStdioServerTransport()` plus the four handler registrations using the Phase 0 API names, delegating to `ToolHandlers`/`PromptHandlers` (`request.Params.Name`, `request.Params.Arguments`). Keep the `--version` branch before the host. `await builder.Build().RunAsync()`.

- [ ] **Step 4: Run tests** — PASS. [local] `dotnet build -c Release` 0 warnings.

- [ ] **Step 5: AOT publish** [local if the Phase 0 addendum says AOT works locally; otherwise CI only in Task 11]

`dotnet publish dotnet/src/Ccusage.Mcp -c Release -r win-x64` → `dotnet/src/Ccusage.Mcp/bin/Release/net10.0/win-x64/publish/ccusage-mcp.exe`; the build output contains no `warning IL`. Smoke [local]: `echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"0"}}}' | ./ccusage-mcp.exe` prints one JSON-RPC result with `serverInfo.name` `ccusage` on stdout and nothing else on stdout.

- [ ] **Step 6: Commit**

```bash
git add dotnet
git commit -m "feat(mcp): wire MCP tool and prompt handlers over stdio with AOT publish" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Fixtures copy and end-to-end parity tests with the real engine

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Copy the upstream fixtures into `dotnet/tests/fixtures/` and drive the published server over stdio with the MCP SDK client, asserting pass-through parity with the engine (spec §10.3).

**Files:**
- Create: `dotnet/tests/fixtures/claude/**`, `dotnet/tests/fixtures/codex/**`, `dotnet/tests/fixtures/statusline/*.json` (copies — the originals in `apps/ccusage/test/` are deleted in Task 14), `dotnet/tests/Ccusage.Mcp.Tests/E2E/E2eEnvironment.cs`, `E2E/EndToEndTests.cs`
- Modify: `dotnet/tests/Ccusage.Mcp.Tests/Ccusage.Mcp.Tests.csproj` (assembly metadata `FixturesDir` = `$(MSBuildThisFileDirectory)../fixtures`), `dotnet/AGENTS.md`

**Interfaces:**
- Consumes: published `ccusage-mcp` (env `CCUSAGE_MCP_E2E_SERVER` = path to the executable) and the engine (env `CCUSAGE_ENGINE`); `McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions { Command, EnvironmentVariables }))` from the SDK (names per the Phase 0 API list).
- Produces: `E2eEnvironment.RequireOrInconclusive()` → `(string ServerPath, string EnginePath, string FixturesDir, IDictionary<string,string> Env)`; calls `Assert.Inconclusive("Set CCUSAGE_MCP_E2E_SERVER and CCUSAGE_ENGINE to run e2e tests.")` when either variable is unset.

- [ ] **Step 1: Copy fixtures**

`cp -r apps/ccusage/test/fixtures/claude dotnet/tests/fixtures/claude`; `cp -r apps/ccusage/test/fixtures/codex dotnet/tests/fixtures/codex`; `mkdir dotnet/tests/fixtures/statusline && cp apps/ccusage/test/statusline-test*.json dotnet/tests/fixtures/statusline/` (and `test-transcript.jsonl` alongside). Do not modify them. Commit this copy on its own:

```bash
git add dotnet/tests/fixtures
git commit -m "test(mcp): copy engine fixtures for end-to-end tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 2: Write the failing e2e tests**

Env for both the server process and the direct engine run: `CLAUDE_CONFIG_DIR = <fixtures>/claude`, `CODEX_HOME = <fixtures>/codex`, `HOME` and `USERPROFILE` = a fresh temp directory (so the 16 other sources find nothing), `NO_COLOR=1`, `LOG_LEVEL=0`, `CCUSAGE_ENGINE = EnginePath`. `[TestClass] EndToEndTests`, each `[TestMethod]` first calls `RequireOrInconclusive()`:
- `Initialize_ReportsServerInfo`: `client.ServerInfo.Name == "ccusage"`, `Version == ServerVersion.Value`.
- `ToolsList_MatchesSnapshot`: `client.ListToolsAsync()` names equal the snapshot's.
- Parity (`[DataRow]`): `("daily", {"source":"claude","offline":true})`, `("daily", {"source":"codex","offline":true})`, `("monthly", {"source":"claude","offline":true,"breakdown":true})`, `("session", {"source":"claude","offline":true})`, `("daily", {"offline":true})` (all sources), `("weekly", {"source":"claude","offline":true,"start-of-week":"monday"})`, `("blocks", {"offline":true})`, `("report", {"args":"codex daily --offline"})`: call the tool; compute argv with `CommandValidator.ValidateAndBuildArgv` (or `CliArgsParser` for `report`); run the engine directly with `EngineRunner`; assert block 0 text **equals** the engine stdout byte-for-byte and `IsError` is not true.
- `Statusline_ReturnsEngineText`: `hook-input` = parsed `fixtures/statusline/statusline-test.json`, `offline: true` → block 0 text equals the engine's stdout when fed the same payload on stdin with `["statusline","--offline"]`.
- `Prompt_Get_ContainsEngineJson`: `client.GetPromptAsync("ccusage", {"args":"claude daily --offline"})` → one message whose text contains the direct engine stdout.
- `ValidationError_DoesNotStartEngine`: `("daily", {"source":"codex","speed":"turbo"})` → `IsError`.
- `OutputSize_IsLogged`: for the all-sources `daily` result, write `Console.WriteLine($"daily(all) bytes: {len}")` (spec §14 asks to observe sizes).

Run `dotnet test` without the env vars → the e2e tests are **Inconclusive** (not failed), everything else passes. Then [local] publish (Task 7b step 5 output) and build the engine (`cargo build --manifest-path rust/Cargo.toml --release --bin ccusage --features fetch-litellm-pricing`), set `CCUSAGE_MCP_E2E_SERVER` and `CCUSAGE_ENGINE`, run `dotnet test` → e2e FAIL (not implemented) — then implement `E2eEnvironment` and fix until PASS.

- [ ] **Step 3: Run with the env vars** [local] — all PASS. Record the printed `daily(all) bytes` in the commit message body.

- [ ] **Step 4: Commit**

```bash
git add dotnet
git commit -m "test(mcp): add end-to-end parity tests against the real engine" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Plugin, skill and marketplace

**Implementer:** Opus  **Reviewer:** Opus

**Phase 0 dependency:** Q7 decides where `mcpServers` lives (marketplace entry vs `plugin.json`); Q3 decides whether the skill's error path mentions a one-time `/mcp` reconnect.

**Goal:** `/plugin marketplace add Desulation/ccusage` + `/plugin install ccusage@ccusage` metadata and the `/ccusage` skill, validated locally with `claude plugin validate --strict`.

**Files:**
- Create: `.claude-plugin/marketplace.json`, `plugin/.claude-plugin/plugin.json`, `plugin/skills/ccusage/SKILL.md`

- [ ] **Step 1: Write the files**

`.claude-plugin/marketplace.json`: spec §7.1 verbatim with `<version>` → `20.0.45` (URL `https://github.com/Desulation/ccusage/releases/download/v20.0.45/ccusage.mcpb`), `mcpServers` placed per Q7.

`plugin/.claude-plugin/plugin.json`: `{"name":"ccusage","version":"20.0.45","description":"Coding-agent CLI token usage and cost reports over MCP","author":{"name":"Desulation"},"repository":"https://github.com/Desulation/ccusage","license":"MIT"}` (+ `mcpServers` if Q7 says so).

`plugin/skills/ccusage/SKILL.md` frontmatter: `name: ccusage`; `description: Show coding-agent token usage and cost reports (Claude Code, Codex, OpenCode, and 15 more) by day, week, month, session or billing block through the ccusage MCP server.`; `argument-hint: "[source] [report] [--flags]"`; `allowed-tools: mcp__plugin_ccusage_ccusage__report`. Body (exact intent, your words): call the `report` tool with `args` set to exactly `$ARGUMENTS` (an empty string means daily across all sources); present the returned JSON as a readable table with totals and the top cost drivers; on an error result, show the error text and the valid choices it lists; examples `/ccusage claude daily`, `/ccusage codex monthly`, `/ccusage weekly --last 2`; if Q3 was "no": one line "If the tool is unavailable right after installation, run `/mcp` once to reconnect."

- [ ] **Step 2: Validate** [local]

`claude plugin validate --strict plugin` and `claude plugin validate --strict .` (or the marketplace path form Phase 0 found) → both report success, no warnings. Fix any reported field.

- [ ] **Step 3: Commit**

```bash
git add .claude-plugin plugin
git commit -m "feat(plugin): add Claude Code plugin, /ccusage skill and marketplace" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Bundle packaging (manifest template, `launch.sh`, assemble/verify/smoke scripts)

**Implementer:** Opus  **Reviewer:** Fable

**Phase 0 dependency:** Q1/Q2 (manifest `platform_overrides`/`${__dirname}` form — if the fallback was approved, the manifest changes accordingly), Q4 (`launch.sh` keeps the `chmod +x` restore either way).

**Goal:** Scripts that turn the six RID artifacts into `ccusage.mcpb`, six archives and `SHA256SUMS`, verify the layout, and smoke-test the bundle through its manifest command on each OS.

**Files:**
- Create: `dotnet/packaging/manifest.template.json`, `dotnet/packaging/launch.sh`, `dotnet/packaging/assemble-bundle.sh`, `dotnet/packaging/verify-bundle.sh`, `dotnet/packaging/bundle-smoke.ps1`

**Interfaces (produces):**
- `assemble-bundle.sh --version <v> --artifacts <dir> --out <dir>`: expects `<dir>/ccusage-mcp-<rid>/ccusage-mcp[.exe]` for `linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64` and `<dir>/ccusage-<rid>/ccusage[.exe]` for all six RIDs (the artifact layout Task 11 uploads); writes `<out>/ccusage.mcpb`, `<out>/ccusage-mcp-<rid>.tar.gz` (linux/osx; contains `ccusage-mcp`, `ccusage`, `LICENSE`) and `<out>/ccusage-mcp-win-x64.zip`, `<out>/ccusage-mcp-win-arm64.zip` (contain `ccusage-mcp.exe` (the win-x64 server), `ccusage.exe`, `LICENSE`), `<out>/SHA256SUMS` (`sha256sum` format, file names only). Bundle layout exactly spec §7.3; `manifest.json` = template with `__VERSION__` replaced by `<v>`. Sets `chmod +x` on every non-Windows binary before zipping (`zip -X -r`, so unix modes are stored).
- `verify-bundle.sh <ccusage.mcpb> --version <v>`: unzips to a temp dir and asserts the exact file list of §7.3 (no extra files), `manifest.json` parses (`jq`) with `.manifest_version == "0.3"`, `.name == "ccusage"`, `.version == <v>`, `.server.type == "binary"`, `.server.mcp_config.command == "/bin/sh"`, `.server.mcp_config.platform_overrides.win32.command == "${__dirname}/win/ccusage-mcp.exe"`, `.compatibility.platforms == ["darwin","linux","win32"]`; each `linux/*` and `osx/*` file has the executable bit in the zip (`unzip -Z -l`/`zipinfo` mode contains `x`); exits non-zero with the first failing check named.
- `bundle-smoke.ps1 -Bundle <ccusage.mcpb> -FixturesDir <dir>`: extracts to a temp dir, reads `manifest.json`, selects `mcp_config` (`platform_overrides.win32` when `$IsWindows`, else the default), substitutes `${__dirname}`, starts the process with `CLAUDE_CONFIG_DIR=<fixtures>/claude` and `HOME`/`USERPROFILE` = temp, sends newline-delimited JSON-RPC: `initialize`, `notifications/initialized`, `tools/list`, `tools/call list-sources`, `tools/call daily {"source":"claude","offline":true}`; asserts: initialize result `serverInfo.name == "ccusage"`; tools/list has 8 tools; list-sources text parses and `engine-version` is non-null; daily result `isError` not true and block 0 text parses as JSON; prints `bundle-smoke: ok` and exits 0, otherwise prints the failing step and exits 1.
- `launch.sh`: `#!/bin/sh`, `set -eu`; `os` from `uname -s` (`Linux`→`linux`, `Darwin`→`osx`, else `echo "ccusage: unsupported OS: $(uname -s)" >&2; exit 1`); `arch` from `uname -m` (`x86_64|amd64`→`x64`, `aarch64|arm64`→`arm64`, else error exit 1); `dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)`; `target="$dir/$os/$arch/ccusage-mcp"`; missing → error listing `$target`, exit 1; `[ -x "$target" ] || chmod +x "$target" "$dir/$os/$arch/ccusage"`; `exec "$target" "$@"`.
- `manifest.template.json`: spec §7.3 JSON with `"version": "__VERSION__"`.

- [ ] **Step 1: Write the scripts** as specified.

- [ ] **Step 2: Verify locally with fake binaries** [local, Git Bash + pwsh]

Build a fake artifacts dir: for each of the six RIDs copy the FakeEngine apphost (`dotnet/tests/FakeEngine/bin/Debug/net10.0/ccusage.exe`) — it only needs to exist for layout checks — and for the five server RIDs copy the win-x64 published `ccusage-mcp.exe` (or any file). Run `assemble-bundle.sh --version 20.0.45 --artifacts <dir> --out dist-test` → the 8 files exist; `verify-bundle.sh dist-test/ccusage.mcpb --version 20.0.45` → exit 0. Negative check: delete `manifest.json` from a copy → verify exits 1 naming `manifest.json`. `sh -n dotnet/packaging/launch.sh` parses. If `shellcheck` is available locally run it; otherwise it runs under treefmt in CI (`nix flake check`).

Smoke [local, Windows]: build a real win bundle: artifacts dir with the real published `ccusage-mcp.exe` for all five server RIDs and the cargo-built engine for the six engine RIDs; `assemble-bundle.sh` then `pwsh dotnet/packaging/bundle-smoke.ps1 -Bundle dist-test/ccusage.mcpb -FixturesDir dotnet/tests/fixtures` → `bundle-smoke: ok`. Record the `.mcpb` size (should be in the §7.3 30–50 MB range given the engine size; the Windows-only copies inflate it here, so note the real figure comes from CI). Linux/macOS legs: **[CI only]** (Task 11 `bundle-smoke`). Delete `dist-test`.

- [ ] **Step 3: Commit**

```bash
git add dotnet/packaging
git commit -m "feat(bundle): add .mcpb manifest template, launcher and packaging scripts" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11a: Composite actions: engine build without npm staging, server publish, Blacksmith removal

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** Rework `.github/actions/build-*-native-package` to build and upload the engine binary as `ccusage-<rid>`, add `publish-mcp-server`, and drop Blacksmith from `setup-nix-cache`.

**Files:**
- Modify: `.github/actions/build-linux-native-package/action.yaml`, `.github/actions/build-macos-nix-native-package/action.yaml`, `.github/actions/build-windows-native-package/action.yaml`, `.github/actions/setup-nix-cache/action.yaml`
- Create: `.github/actions/publish-mcp-server/action.yaml`

- [ ] **Step 1: `setup-nix-cache`**: delete the `Mount Nix store sticky disk` step (the `useblacksmith/stickydisk` use), the `Trim Nix store sticky disk` step, and the `sticky` output logic (`backend` step keeps only `commit`); every `if: steps.backend.outputs.sticky != 'true'` condition becomes unconditional; update the `description` text to drop the Blacksmith sentences. Keep `nix-quick-install-action` and `cache-nix-action` at their current SHAs.

- [ ] **Step 2: Engine build actions**: add input `rid` (required) to all three. Replace the `Stage native package` and `Archive native package binary` steps with one `Stage engine binary` step: `mkdir -p "$RUNNER_TEMP/engine" && cp "$BINARY" "$RUNNER_TEMP/engine/ccusage"` (Windows: `ccusage.exe`); change the `upload-artifact` step to `name: ccusage-${{ inputs.rid }}`, `path: ${{ runner.temp }}/engine/ccusage*`. Remove the `Root current Crane artifacts for cache save` step's `/nix/var/nix/gcroots` sticky-disk comments only if they are wrong without Blacksmith (cache-nix-action still benefits from roots — keep the steps). macOS: the `x64` branch (`ccusage-darwin-x64` cross build) stays available but the §9.1 matrix no longer uses it (osx-x64 builds with cargo on `macos-15-intel`); leave the branch in place (minimal diff). Windows: unchanged apart from staging/upload.

- [ ] **Step 3: `publish-mcp-server/action.yaml`** (composite): inputs `rid` (required). Steps: `actions/setup-dotnet@<SHA of the current v5 release, looked up with gh api repos/actions/setup-dotnet/git/ref/tags/v5.x.y>` with `global-json-file: dotnet/global.json`; `dotnet publish dotnet/src/Ccusage.Mcp -c Release -r ${{ inputs.rid }} -o "$RUNNER_TEMP/server" 2>&1 | tee "$RUNNER_TEMP/publish.log"` then `! grep -E 'warning IL[0-9]+' "$RUNNER_TEMP/publish.log"` (fail on any trim/AOT warning); `actions/upload-artifact` (same SHA as ci.yaml uses) `name: ccusage-mcp-${{ inputs.rid }}`, `path: ${{ runner.temp }}/server/ccusage-mcp*` excluding `*.pdb`/`*.dbg`/`*.dSYM`.

- [ ] **Step 4: Lint** [local]: `python -c "import yaml,glob;[yaml.safe_load(open(f)) for f in glob.glob('.github/actions/*/action.yaml')]"` parses (install nothing; if PyYAML is absent, skip and rely on CI). `actionlint`/`zizmor` run in CI preflight **[CI only]**.

- [ ] **Step 5: Commit**

```bash
git add .github/actions
git commit -m "ci: build engine artifacts per RID and publish the AOT server without npm staging" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11b: `build-artifacts.yaml` (reusable) and `ci.yaml` rewrite

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** CI per spec §9.2 on GitHub-hosted runners: kept jobs, `dotnet-test`, build matrix, `e2e`, `bundle`, `bundle-smoke`, `plugin-validate`, `version-consistency`, `docs-build`; npm/JS/perf jobs removed.

**Files:**
- Create: `.github/workflows/build-artifacts.yaml`
- Modify: `.github/workflows/ci.yaml`

- [ ] **Step 1: `build-artifacts.yaml`**

`on: workflow_call: inputs: ref (string, required)`. Jobs:
- `build` — `strategy.fail-fast: false`, matrix `include` (spec §9.1): `{rid: linux-x64, os: ubuntu-latest, engine: nix-static}`, `{rid: linux-arm64, os: ubuntu-24.04-arm, engine: nix-static}`, `{rid: osx-arm64, os: macos-latest, engine: nix}`, `{rid: osx-x64, os: macos-15-intel, engine: cargo}`, `{rid: win-x64, os: windows-latest, engine: cargo}`, `{rid: win-arm64, os: windows-11-arm, engine: cargo, server: false}`. Steps: checkout `ref: ${{ inputs.ref }}` `persist-credentials: false`; engine via the Task 11a actions (`build-linux-native-package` for `nix-static` with `arch` derived from rid, `build-macos-nix-native-package` for `nix` with `arch: arm64`, `build-windows-native-package` for `cargo` on Windows; for `osx-x64` (`engine: cargo` on macOS) three inline workflow steps: `rustup toolchain install "$(sed -n 's/^channel *= *"\([^"]*\)".*/\1/p' rust-toolchain.toml)" --profile minimal --no-self-update`, `cargo build --manifest-path rust/Cargo.toml --release --bin ccusage --features fetch-litellm-pricing` with `RUSTUP_TOOLCHAIN` set to that channel, and `upload-artifact` `name: ccusage-osx-x64`, `path: rust/target/release/ccusage`); then `uses: ./.github/actions/publish-mcp-server` with `rid` when `matrix.server != false`. Permissions `contents: read`, `actions: write`.
- `bundle` — `needs: build`, `ubuntu-latest`: checkout; `download-artifact` `pattern: ccusage-*`, `path: ${{ runner.temp }}/artifacts` (**not** `merge-multiple`); `version=$(jq -r .version package.json)`; `dotnet/packaging/assemble-bundle.sh --version "$version" --artifacts "$RUNNER_TEMP/artifacts" --out dist`; `dotnet/packaging/verify-bundle.sh dist/ccusage.mcpb --version "$version"`; `du -h dist/ccusage.mcpb | tee -a "$GITHUB_STEP_SUMMARY"`; upload `ccusage-release-assets` = `dist/*`.

- [ ] **Step 2: Rewrite `ci.yaml`**

Keep (with `runs-on` changed from `blacksmith-*` to `ubuntu-latest`, and `ubuntu-slim` kept where present): `changes`, `tirith-scan`, `tirith-sarif-upload`, `preflight`, `release-dispatch-status-pending`, `release-dispatch-status`. `test` keeps checkout + setup-nix-cache + the `parallel:` block with only `Run nix flake check` and `Run Rust tests`; delete the `JS test`, `Nushell contribution-gate tests`, `Babashka performance harness test` steps and the `Root test dependency layers for the sticky disk` step. Delete jobs `build-native-packages`, `npm-publish-dry-run-and-upload-pkg-pr-now`, `ccusage-preview-e2e`, `ccusage-perf-comment`, `action-timeline` (its action is not approved). Add:
- `dotnet-test`: `needs: [changes, tirith-scan]`, `if: code-changed`, matrix `os: [ubuntu-latest, windows-latest, macos-latest]`; checkout; `actions/setup-dotnet` (SHA from Task 11a) `global-json-file: dotnet/global.json`; `dotnet build dotnet/Ccusage.Mcp.slnx -c Release`; `dotnet test dotnet/Ccusage.Mcp.slnx -c Release --no-build` (e2e tests are Inconclusive here).
- `build-artifacts`: `needs: [changes, tirith-scan]`, `if: code-changed`, `uses: ./.github/workflows/build-artifacts.yaml`, `with: ref: ${{ github.event.pull_request.head.sha || github.sha }}`, `permissions: {contents: read, actions: write}`.
- `e2e`: `needs: build-artifacts`, matrix `include`: `{os: ubuntu-latest, rid: linux-x64}`, `{os: windows-latest, rid: win-x64}`, `{os: macos-latest, rid: osx-arm64}`; checkout; setup-dotnet; download `ccusage-mcp-${{ matrix.rid }}` and `ccusage-${{ matrix.rid }}` into `$RUNNER_TEMP/bin`; `chmod +x` (non-Windows); export `CCUSAGE_MCP_E2E_SERVER` and `CCUSAGE_ENGINE` (bash step writing to `$GITHUB_ENV`, with `.exe` on Windows); `dotnet test dotnet/Ccusage.Mcp.slnx -c Release`.
- `bundle-smoke`: `needs: build-artifacts`, same 3-OS matrix; checkout (for fixtures and the script); download `ccusage-release-assets`; `pwsh dotnet/packaging/bundle-smoke.ps1 -Bundle "$RUNNER_TEMP/assets/ccusage.mcpb" -FixturesDir dotnet/tests/fixtures` (shell `pwsh` on all three).
- `plugin-validate`: `ubuntu-latest`; checkout; `curl -fsSL https://claude.ai/install.sh | bash -s 2.1.303` (pin = the local version; bump deliberately); `"$HOME/.local/bin/claude" plugin validate --strict plugin` and the marketplace form from Task 9.
- `version-consistency`: `ubuntu-latest`; bash: `v=$(jq -r .version package.json)`; compare with `jq -r .version docs/package.json`, `jq -r .version plugin/.claude-plugin/plugin.json`, `jq -r '.plugins[0].mcpServers' .claude-plugin/marketplace.json | grep -o 'v[0-9.]*' | tr -d v` (adjust the jq path to the Q7 placement), `sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' dotnet/Directory.Build.props`; fail listing any mismatch.
- `docs-build`: `needs: changes` (runs always, so docs-only PRs are checked), `ubuntu-latest`; checkout; setup-nix-cache; `nix profile install --inputs-from . .#pnpm nixpkgs#nodejs nixpkgs#just`; `pnpm install --frozen-lockfile`; `just docs::build`.
- `ci-gate`: `needs` = `changes, tirith-scan, tirith-sarif-upload, preflight, test, dotnet-test, build-artifacts, e2e, bundle-smoke, plugin-validate, version-consistency, docs-build`.
Triggers unchanged (`push main`, `pull_request`, `workflow_dispatch`).

Note: until Task 14 removes `apps/ccusage`, `docs-build` still works (the old `cp ../apps/ccusage/config-schema.json` path exists), and the `version-consistency` job passes because Task 9 wrote `20.0.45` everywhere.

- [ ] **Step 3: Lint** [local]: YAML parses (as in 11a). **[CI only]**: actionlint/zizmor via preflight, and the whole workflow on the first push.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/build-artifacts.yaml .github/workflows/ci.yaml
git commit -m "ci: replace npm jobs with dotnet tests, build matrix, bundle, smoke and plugin validation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: `release.yaml`

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** On a tagpr tag: build matrix → bundle → GitHub Release with `ccusage.mcpb`, six archives and `SHA256SUMS` via `gh release create` (spec §9.3).

**Files:**
- Modify: `.github/workflows/release.yaml`

- [ ] **Step 1: Edit**

`tagpr` job: `runs-on: ubuntu-latest`; keep `Songmu/tagpr` at its SHA. `ci` dispatch job: unchanged. Replace `build-native-packages` with `build-artifacts: needs: tagpr; if: needs.tagpr.outputs.tag != ''; uses: ./.github/workflows/build-artifacts.yaml; with: ref: ${{ needs.tagpr.outputs.tag }}; permissions: {contents: read, actions: write}`. Delete the `npm` job. `release` job: `needs: [tagpr, build-artifacts]`, `runs-on: ubuntu-latest`, `permissions: contents: write`; checkout `ref: tag`, `persist-credentials: false`; `download-artifact` `name: ccusage-release-assets` `path: dist`; `gh release create "$TAG" dist/* --title "$TAG" --generate-notes --verify-tag` with `GH_TOKEN: ${{ github.token }}` and `TAG: ${{ needs.tagpr.outputs.tag }}`. Remove the `setup-nix-cache` and `nix run .#changelogithub` steps (release notes now come from `--generate-notes`).

- [ ] **Step 2: Lint** [local] YAML parses; **[CI only]** first real release.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release.yaml
git commit -m "ci(release): publish the mcpb bundle and archives with gh release create" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: `sync-upstream.yaml`

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** Weekly PR that brings `ccusage/ccusage` `main` into this fork (spec §9.4).

**Files:**
- Create: `.github/workflows/sync-upstream.yaml`

- [ ] **Step 1: Write**

`on: schedule: [{cron: '0 6 * * 1'}]` + `workflow_dispatch`. `permissions: {contents: write, pull-requests: write}`. `concurrency: sync-upstream`. One job `sync` on `ubuntu-latest`: checkout `fetch-depth: 0` (credentials persisted — needed for the push); `git remote add upstream https://github.com/ccusage/ccusage.git && git fetch upstream main`; step `check`: `if git merge-base --is-ancestor upstream/main origin/main; then echo "up_to_date=true" >> "$GITHUB_OUTPUT"; fi`; remaining steps `if: steps.check.outputs.up_to_date != 'true'`: `git push --force origin upstream/main:refs/heads/sync/upstream`; body = `printf 'Incoming commits from ccusage/ccusage main:\n\n'; git log --oneline --no-decorate origin/main..upstream/main` into `$RUNNER_TEMP/body.md`; `if gh pr list --head sync/upstream --base main --state open --json number -q '.[0].number' | grep -q .; then gh pr edit <n> --body-file ...; else gh pr create --base main --head sync/upstream --title 'chore(deps): sync upstream ccusage/ccusage' --body-file ...; fi` with `GH_TOKEN: ${{ github.token }}`. Comment at the top: CI does not run automatically on this PR (GITHUB_TOKEN); run `gh workflow run ci.yaml --ref sync/upstream`.

- [ ] **Step 2: Lint** [local] YAML parses. **[CI only]** `workflow_dispatch` after push.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/sync-upstream.yaml
git commit -m "ci: add weekly upstream sync pull request workflow" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14a: Remove the npm layer; move `config-schema.json`; slim the JS workspace; versioning

**Implementer:** Opus  **Reviewer:** Opus

**Prerequisite:** Task 8 committed the fixture copies; Tasks 11–12 removed every workflow reference to `apps/ccusage` and `packages/`.

**Goal:** Spec §3.1 deletions and §3.2 edits for the npm packages, workspace, Nix references, justfile, schema location and `.tagpr`.

**Files:**
- Delete: `apps/ccusage/` (after the moves below), `packages/` (6 dirs), `pnpm-lock.yaml` (regenerated), `.oxlintrc.json`, `.oxfmtrc.json`, `nix/tools/publint/`
- Move: `apps/ccusage/config-schema.json` → `config-schema.json`; `apps/ccusage/LICENSE` → `LICENSE` (replacing the symlink); `apps/ccusage/README.md` → `README.md` (replacing the symlink; rewritten in Task 16)
- Modify: `package.json`, `pnpm-workspace.yaml`, `package.nix`, `nix/checks.nix`, `nix/treefmt.nix`, `nix/git-hooks.nix`, `nix/packages.nix`, `nix/dev-shell.nix`, `justfile`, `ccusage.example.json`, `docs/package.json`, `docs/README.md`, `.tagpr`, `.gitignore`

- [ ] **Step 1: Moves and deletions**

```bash
git rm README.md LICENSE                      # the symlinks
git mv apps/ccusage/README.md README.md
git mv apps/ccusage/LICENSE LICENSE
git mv apps/ccusage/config-schema.json config-schema.json
git rm -r apps/ccusage packages .oxlintrc.json .oxfmtrc.json nix/tools/publint pnpm-lock.yaml
```

Confirm `apps/` is now empty and remove the directory.

- [ ] **Step 2: Edit upstream files (minimal)**

- `package.json` → `{"name":"ccusage","version":"20.0.45","description":"Analyze coding (agent) CLI token usage and costs from local data","homepage":"https://github.com/Desulation/ccusage#readme","license":"MIT","private":true,"workspaces":["docs"]}` (no `scripts`, no `packageManager`, no `type`, no `dependencies`). `name` changes from `ccusage-monorepo` to `ccusage` because `package.nix` derives `pname` from it (see Open Questions).
- `pnpm-workspace.yaml`: `packages: [docs]`; delete the `types` catalog (`@types/node`, `@types/react`) — then also drop those two `devDependencies` from `docs/package.json` (they only resolved via `catalog:types`); keep everything else.
- `package.nix`: replace `cliPackageJson = lib.importJSON (root + /apps/ccusage/package.json);` with `rootPackageJson = lib.importJSON (root + /package.json);`, `pname = rootPackageJson.name`, `meta = { inherit (rootPackageJson) description homepage; license = lib.getLicenseFromSpdxId rootPackageJson.license; mainProgram = "ccusage"; }`; fix the two comments.
- `nix/checks.nix`: `apps/ccusage/config-schema.json` → `config-schema.json` (3 places incl. messages), `just ccusage::generate-schema` → `just schema`; delete the `publint` check attribute (whole block); keep `oxlint`, `gitleaks`, `config-example` (path updated), `bun-nix`.
- `nix/treefmt.nix`: `install_if_changed "$tmp" config-schema.json`, `install_if_changed config-schema.json docs/public/config-schema.json`, `schema-gen.includes` first entry `config-schema.json`, comment lines updated.
- `nix/git-hooks.nix`: `node-test.entry` → `... --test nix/tools/models-dev-gen/compact.test.ts`.
- `nix/packages.nix`: delete `publint = pkgs.callPackage ../nix/tools/publint { inherit bunCli; };` and `publint` from `packages`. `nix/dev-shell.nix`: delete `config.packages.publint`.
- `justfile`: delete `mod ccusage 'apps/ccusage'`; `build: docs::build`; `test-node` → `TZ=UTC node --test nix/tools/models-dev-gen/compact.test.ts`; delete `generate-large-fixture`; `schema` comment → `Regenerate config-schema.json from the Rust source`; update the header comment's `pnpm run` sentence to drop the prepack mention.
- `ccusage.example.json`: `"$schema": "./config-schema.json"`.
- `docs/package.json`: both scripts `cp ../config-schema.json public/config-schema.json && ...`; remove `@types/node`, `@types/react`. `docs/README.md`: `config-schema.json` path sentence.
- `.tagpr`: `versionFile = package.json,docs/package.json,plugin/.claude-plugin/plugin.json,.claude-plugin/marketplace.json,dotnet/Directory.Build.props`.
- `.gitignore`: delete `packages/ccusage-*/bin/`.

- [ ] **Step 3: Regenerate `pnpm-lock.yaml`**

Requires pnpm 12.15 (`packageManager` pin removed; Nix pins `pnpm_12`). **Not available locally.** Options, in order: (a) the human runs `pnpm install --lockfile-only` in the Nix dev shell / any machine with pnpm 12 and commits; (b) temporarily `winget install OpenJS.NodeJS.LTS` + `corepack enable && corepack prepare pnpm@12.15.0 --activate`, run `pnpm install --lockfile-only` at the repo root, commit the lockfile only. If neither is possible in this session, leave the lockfile deleted, note it in the commit body, and flag it — `docs-build` in CI will fail until it is regenerated.

- [ ] **Step 4: Verify** [local]: `grep -rn "apps/ccusage\|packages/ccusage\|publint" --exclude-dir=.git --exclude-dir=superpowers --exclude-dir=rust .` returns only `.agents/skills/**` and `docs/**`/`README.md` hits (handled in Tasks 15–16) — no hits in `nix/`, `justfile`, `.github/`, `package*.json`, `.tagpr`. `dotnet test dotnet/Ccusage.Mcp.slnx` still passes (fixtures live in `dotnet/tests/fixtures`). `python -c "import json;json.load(open('package.json'));json.load(open('docs/package.json'));json.load(open('ccusage.example.json'))"`. **[CI only]**: `nix flake check` (schema drift check now reads `config-schema.json`), `docs-build`.

- [ ] **Step 5: Commit** (two commits: moves/deletions, then edits)

```bash
git commit -m "chore: remove npm packages and launcher; move config-schema.json and LICENSE to the root" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git commit -m "chore(deps): keep pnpm workspace for docs only and point Nix, just and tagpr at the new layout" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14b: Delete upstream-only workflows and scripts; trim `check-pr-title.yaml`

**Implementer:** Opus  **Reviewer:** Opus

**Files:**
- Delete: `.github/workflows/{pr-gate.yaml,issue-gate.yaml,gate-close-pr.yaml,approve-contributor.yaml,pullfrog.yml,autofix.yaml,update-pricing.yaml,check-links.yaml}`, `.github/scripts/contribution-gate/` (dir), `.github/scripts/contribution-gate.nu`, `.github/scripts/contribution-gate.test.nu`, `.github/APPROVED_CONTRIBUTORS`, `.github/scripts/npm-registry-gate.nu`, `.github/scripts/generate-e2e-fixture.nu`, `.github/scripts/upsert-pr-comment.nu` (the last two were referenced only by the removed CI jobs)
- Modify: `.github/workflows/check-pr-title.yaml` (delete the `scope` job entirely, including its comment block, leaving only `main`)
- Keep: `.github/scripts/pricing-lock.nu`, `update-litellm-lock.nu`, `update-models-dev-lock.nu` (used by `justfile` recipes), `.github/actions/commit-and-push-main` (unused now; see Open Questions), `.github/tagpr-template.md`, `renovate.json`, `release.yml`, `FUNDING.yaml`, `ISSUE_TEMPLATE/`, `actionlint.yaml`.

- [ ] **Step 1: `git rm` the files above; edit `check-pr-title.yaml`.**
- [ ] **Step 2: Verify** [local]: `grep -rn "hustcer/setup-nu\|useblacksmith\|lycheeverse\|autofix-ci\|pullfrog\|Kesin11\|blacksmith-" .github` → no output. `ls .github/workflows` → `build-artifacts.yaml check-pr-title.yaml ci.yaml release.yaml sync-upstream.yaml`.
- [ ] **Step 3: Commit**

```bash
git add -A .github
git commit -m "ci: drop upstream contribution-gate, pricing, link-check and autofix automation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: `AGENTS.md` and `.agents/skills` updates

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** Agent guidance describes the C# layer instead of the npm layer; no skill points at a deleted path.

**Files:**
- Create: `.agents/skills/dotnet/SKILL.md`
- Delete: `.agents/skills/typescript/` (whole dir), `.agents/skills/testing/references/node-test.md`
- Modify: `AGENTS.md`, `.agents/skills/development/SKILL.md`, `.agents/skills/development/references/commands.md`, `.agents/skills/testing/SKILL.md`, `.agents/skills/docs/SKILL.md`, and the dangling references in `.agents/skills/{fix-ci,nushell,profile,rust-binary-size,tdd}/SKILL.md` and `.agents/skills/profile/references/rust.md` (lines found by `grep -rn "apps/ccusage" .agents`)

- [ ] **Step 1: `AGENTS.md`**: routing table — replace the `typescript` row with `dotnet` ("`dotnet/` C# MCP server: catalog, validator, engine runner, MCP handlers, packaging, MSTest"); `testing` row → "cargo and MSTest tests…"; Repository Shape — replace the `apps/ccusage` sentence with "`dotnet/` is the MCP distribution layer: it wraps the `ccusage` binary and adds no adapter logic"; nearest-AGENTS list: replace `apps/ccusage/AGENTS.md` with `dotnet/AGENTS.md`; Policy: drop the `devDependencies` bullet; add "C# only in `dotnet/`; third-party NuGet packages need explicit approval (see `docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md` §12)".
- [ ] **Step 2: `dotnet/SKILL.md`**: frontmatter `name: dotnet`, description "Guides work in dotnet/: the C# Native AOT MCP server, its MSTest suite, FakeEngine, fixtures and .mcpb packaging. Use before editing .cs, .csproj, .props, .slnx or dotnet/packaging files."; body: point to `dotnet/AGENTS.md`; TDD rule; "catalog from upstream JSON — never hand-copy"; AOT rules (no reflection serialization, `[JsonSerializable]` only, zero IL warnings); where tests live; snapshot update; e2e env vars; prefer the dotnet MCP tools.
- [ ] **Step 3: `development/SKILL.md`**: replace "The npm Packaging Seam" with "The MCP Packaging Seam" (the `dotnet/` server, `dotnet/packaging/*`, `plugin/`, `.claude-plugin/`, version files per `.tagpr`); `references/commands.md`: JS/TS tooling sentence → docs only; Releases → "tagpr tags; `release.yaml` builds the matrix, bundles `ccusage.mcpb`, and creates the GitHub Release"; mention `sync-upstream.yaml`.
- [ ] **Step 4: `testing/SKILL.md`**: "Node" section → ".NET: MSTest in `dotnet/tests/Ccusage.Mcp.Tests`, FakeEngine, fixtures under `dotnet/tests/fixtures`, e2e via env vars; the remaining Node test is `nix/tools/models-dev-gen/compact.test.ts` (`just test-node`)". `docs/SKILL.md`: surfaces list → root `README.md`, `docs/guide/` incl. `docs/guide/mcp.md`, VitePress nav, `plugin/skills/ccusage/SKILL.md`; drop the `apps/ccusage` bullets. `fix-ci`: table rows → `dotnet-test` → `dotnet test dotnet/Ccusage.Mcp.slnx`; `e2e` → set env vars then the same; `bundle-smoke` → `pwsh dotnet/packaging/bundle-smoke.ps1`; remove the JS/Babashka rows. `nushell`, `profile`, `rust-binary-size`, `tdd`: delete or reword the sentences naming `apps/ccusage/...` (profile: the JS-launcher profiling paragraph goes; rust-binary-size `paths`/`globs` drop the `apps/ccusage/scripts/**` entries).
- [ ] **Step 5: Verify** [local]: `grep -rn "apps/ccusage\|packages/ccusage\|cli.js\|publint" AGENTS.md .agents` → no output.
- [ ] **Step 6: Commit**

```bash
git add AGENTS.md .agents
git commit -m "docs(agents): describe the dotnet MCP layer and retire the npm skills" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: README and docs site rewrite

**Implementer:** Opus  **Reviewer:** Opus

**Goal:** Spec §3.4 exactly.

**Files:**
- Modify: `README.md`, `docs/index.md`, `docs/.vitepress/config.ts`, `docs/guide/installation.md`, `docs/guide/getting-started.md`, `docs/guide/index.md`, every `docs/guide/**` file with `npx ccusage`/`bunx ccusage` (list: `amp/index.md`, `codebuff/index.md`, `copilot/index.md`, `droid/index.md`, `hermes/index.md`, `kilo/index.md`, `openclaw/index.md`, `opencode/index.md`, `pi/index.md`, `qwen/index.md`, plus `getting-started.md`, `installation.md`, `index.md`)
- Create: `docs/guide/mcp.md`
- Delete: `docs/guide/sponsors.md`

- [ ] **Step 1: `README.md`** — rewrite per §3.4. Remove: the whole badge block (Socket, npm version, NPM downloads, DeepWiki, Awesome, the 技術者倫理 badge, Trendshift), "Major Sponsors", GitHub Sponsors, Star History sections; every `npx`/`bunx`/`pnpm dlx`/`pnpx`/pkg.pr.new line and the bunx caching note; `nix run github:ccusage/ccusage`; the `pnpm install` dev note and the update-pricing paragraph. Rewrite: intro + credit line `Fork of [ccusage/ccusage](https://github.com/ccusage/ccusage) by @ryoppippi`; Quick Start (`/plugin marketplace add Desulation/ccusage`, `/plugin install ccusage@ccusage`, `/ccusage claude daily`); Installation (1 plugin, 2 Claude Desktop `.mcpb`, 3 other clients: archive + `.mcp.json` snippet `{"mcpServers":{"ccusage":{"command":"/path/to/ccusage-mcp"}}}`, 4 standalone CLI from the archive with `xattr -d com.apple.quarantine ccusage ccusage-mcp` note); Usage (slash commands, example questions such as "How much did I spend on Codex this month?", the existing CLI block with `bunx ccusage` → `ccusage`); MCP reference table (8 tools + prompt + skill, link `docs/guide/mcp.md`); Development (Nix/direnv/just kept; `dotnet/` section: .NET 10 SDK, `dotnet build/test/publish`, e2e env vars; `sync-upstream.yaml` sentence). Keep: Supported Sources table, Features (+ "MCP server for Claude Code and other MCP clients"), License (MIT © @ryoppippi); Documentation link → `docs/guide/`.
- [ ] **Step 2: `docs/guide/**`** — `sed`-style replace `npx ccusage@latest ` → `ccusage `, `bunx ccusage ` → `ccusage ` in the listed files, then read each diff hunk to fix prose around it ("install globally first" sentences). Rewrite `installation.md` and `getting-started.md` to mirror the README's Installation and Quick Start (drop the runtime comparison, Node/Bun requirements, npm update/uninstall sections; Development Installation keeps Nix/direnv/just and adds the `dotnet/` commands; Verification uses `ccusage --version` and `ccusage-mcp --version`). `docs/guide/index.md`: fix its one `npx`/`bunx` line.
- [ ] **Step 3: `docs/guide/mcp.md`** — sections: Overview; Install (three ways); Tools (one subsection per tool with its parameters table generated by reading `dotnet/tests/Ccusage.Mcp.Tests/Snapshots/tools-list.json` — names, types, applies-to); Prompt `ccusage` and the `/mcp__plugin_ccusage_ccusage__ccusage` / `/ccusage:ccusage` forms (§5.6); Skill `/ccusage` (§7.2); Results and errors (§5.7); Engine location order and `CCUSAGE_ENGINE` (§6.1); Environment (`CCUSAGE_MCP_TIMEOUT_SECONDS`, inherited `CLAUDE_CONFIG_DIR`/`CODEX_HOME`…); Troubleshooting (engine not found → the `Checked:` list; timeouts → `since`/`last` or raise the env var; large outputs → `since`/`last`/`no-cost`; macOS quarantine; first-start reconnect if Q3 said so).
- [ ] **Step 4: Sponsor/promo removal** — delete `docs/guide/sponsors.md`; remove the whole `<div ...>Support ccusage ... </div>` block from `docs/index.md` (keep the project-history line); `docs/.vitepress/config.ts`: remove the `Sponsor` nav item, the `Sponsors` sidebar item (and the `Community` group if it is then only `Community Projects` — keep the group with that one item), the `/sponsor` redirect; add `{ text: 'MCP Server', link: '/guide/mcp' }` under `Integration`; also remove the `npm`, `Package Stats`, `DeepWiki` nav items, the `npm` social link and the `/npm`, `/deepwiki` redirects, and point the remaining GitHub links (`nav`, `socialLinks`, `editLink.pattern`, `actions` link in `docs/index.md`) at `https://github.com/Desulation/ccusage` (see Open Questions). Leave `docs/wrangler.jsonc`.
- [ ] **Step 5: Verify** [local]: `grep -rn "npx ccusage\|bunx\|pnpm dlx\|pnpx\|pkg.pr.new\|sponsors.ryoppippi\|github.com/sponsors" README.md docs/guide docs/index.md docs/.vitepress` → no output. `grep -c "Desulation/ccusage" README.md` ≥ 3. **[CI only]**: `docs-build`.
- [ ] **Step 6: Commit**

```bash
git add README.md docs
git commit -m "docs: rewrite README and guides for the MCP plugin distribution" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Final consistency pass

**Implementer:** Opus  **Reviewer:** Fable

**Goal:** Confirm the success criteria that can be checked locally and leave a clean branch for the human to push.

- [ ] **Step 1** [local]: `dotnet build dotnet/Ccusage.Mcp.slnx -c Release` (0 warnings), `dotnet test dotnet/Ccusage.Mcp.slnx -c Release` (all pass; e2e Inconclusive without env vars), then with `CCUSAGE_MCP_E2E_SERVER`/`CCUSAGE_ENGINE` set (all pass).
- [ ] **Step 2** [local]: `claude plugin validate --strict plugin`; version check script from Task 11b run locally with bash + `jq` (or Python equivalent) → all five agree.
- [ ] **Step 3** [local]: `git grep -n "apps/ccusage\|packages/ccusage\|npm publish\|pkg.pr.new\|blacksmith" -- . ':!rust' ':!docs/superpowers' ':!pnpm-lock.yaml'` → no output. `git status` clean.
- [ ] **Step 4** [local]: `git log --oneline main..HEAD` — every commit message is a Conventional Commit ending with the Co-Authored-By line (`git log --format=%B main..HEAD | grep -c "Co-Authored-By: Claude Opus 5.5"` equals the commit count).
- [ ] **Step 5**: Write the handoff note for the human: what is **[CI only]** and still unverified (Nix builds, Linux/macOS bundle and `launch.sh`, the release flow, `plugin-validate` in CI), and the pnpm-lock status. Do not push.

---

## Open questions and spec ambiguities (for the human; not resolved silently)

1. **Root `package.json` `name`.** Spec §3.2 says "keep `name`" but `package.nix` must derive `pname`/`meta` from it; the plan sets `name` to `"ccusage"` (from `"ccusage-monorepo"`) and adds `homepage`. Confirm, or keep `ccusage-monorepo` and hardcode `pname = "ccusage"` in `package.nix`.
2. **`session-length` kind.** The engine parses it as `f64`; the spec's kind list has `int` only. The plan adds a `number` kind for it. Confirm.
3. **`Kesin11/actions-timeline`** (existing `action-timeline` CI job) is not in §12's approved list; the plan deletes the job. Confirm (or approve the action).
4. **`nix/tools/changelogithub`** becomes unused once `release.yaml` uses `gh release create --generate-notes`. The plan leaves it in place (§2 minimal divergence). Delete instead?
5. **`.github/actions/commit-and-push-main`** is used only by the deleted `update-pricing.yaml`. Plan keeps it; delete instead?
6. **`.github/scripts/generate-e2e-fixture.nu` and `upsert-pr-comment.nu`** are used only by deleted CI jobs; the plan deletes them though §3.1 does not list them. Confirm.
7. **`CONTRIBUTING.md`** describes the contribution gate and `lgtm` approvals that no longer exist here. Not in §3.2; the plan leaves it. Trim?
8. **Docs nav/social links** (`npm`, `Package Stats`, `DeepWiki`, upstream GitHub URLs, `editLink`) are not explicitly covered by §3.4's "sponsor and promotional content"; the plan removes the npm/DeepWiki/stats items and repoints GitHub links at `Desulation/ccusage`. Confirm scope.
9. **`docs-build` CI job.** The spec says the docs build "is checked in CI", but upstream's `ci.yaml` has no such job. The plan adds one (`docs-build`, pnpm via Nix). Confirm.
10. **pnpm lockfile regeneration** needs pnpm, which is not on the dev machine (Task 14a step 3). Decide who regenerates it and how.
11. **Blacksmith arm64 macOS x64 build.** §9.1 builds osx-x64 with `cargo` on `macos-15-intel`; the existing `nix build .#ccusage-darwin-x64` cross build becomes unused (kept in the macOS action and `nix/darwin-x64-package.nix`). OK to leave?
12. **Argv form for Claude-only tools.** `blocks`/`statusline` are emitted as root commands (`ccusage blocks`), not `ccusage claude blocks`; both pages exist with identical options. Parity tests compare against the same argv, so either is consistent; confirm the root form.
13. **Unknown-parameter strictness.** Typed tools reject any property not in the schema (`additionalProperties: false` plus server-side check). Some clients send extra keys; confirm strict rejection is wanted.
14. **Claude Code pin for `plugin-validate`:** `2.1.303` (the local version). Confirm or choose another.
15. **Q4/Q5 cannot be verified on this machine** (no Linux/macOS, no Windows ARM64). The addendum will say "unverified" unless the human can test elsewhere.
