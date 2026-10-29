# AGENTS.md - dotnet

`ccusage-mcp`: a Native AOT MCP server (C#, .NET 10) that runs the `ccusage` engine and returns its
output as MCP tool results. Design: `docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md`.

## Layout

- `global.json` - SDK 10.0.x, `rollForward: latestFeature`, Microsoft.Testing.Platform test runner.
- `Directory.Build.props` - net10.0, nullable, warnings-as-errors, analyzers, `<Version>` (= root `package.json` version).
- `Directory.Packages.props` - central package versions; `PackageReference`s carry no `Version`.
- `nuget.config` - nuget.org as the only package source.
- `packaging/` - `.mcpb` manifest template and the Linux/macOS arch launcher.
- `src/Ccusage.Mcp/` - the server; assembly and executable name `ccusage-mcp`.
- `tests/Ccusage.Mcp.Tests/` - MSTest unit, integration and e2e tests.
- `tests/FakeEngine/` - test-only console app (`ccusage`) standing in for the engine.
- `tests/fixtures/` - e2e fixture projects copied from `apps/ccusage/test/`.

## Commands

```sh
dotnet build dotnet/Ccusage.Mcp.slnx
(cd dotnet && dotnet test)        # i.e. dotnet test dotnet/Ccusage.Mcp.slnx, run inside dotnet/
dotnet publish dotnet/src/Ccusage.Mcp -c Release -r <rid>
```

`dotnet test` must run with `dotnet/` as the working directory: the SDK reads `global.json` from
the current directory upward, and only `dotnet/global.json` opts into the Microsoft.Testing.Platform
runner. From the repository root it falls back to VSTest mode, which MTP rejects on .NET 10.

Agents prefer the dotnet MCP tools (`mcp__dotnet__dotnet_project` `Build` / `Test` / `Publish`,
with `workingDirectory` set to `dotnet/`). A local AOT publish (`-r <rid>`) must go through the
dotnet MCP `Publish` action: from the agent's Git Bash and PowerShell shells the native link step
fails because `vswhere.exe` is not on `PATH` (spec §15).

## Catalog data

The tool catalog comes from `rust/crates/ccusage-cli-parser/src/*.json`, embedded into
`Ccusage.Mcp` by link. Never copy it into `dotnet/`, and never modify `rust/`.

## FakeEngine protocol

`tests/FakeEngine` builds `ccusage(.exe)`; tests find it through `TestPaths.FakeEnginePath`.
By default it writes `{"argv":[...],"stdin":"...","noColor":"<NO_COLOR or null>"}` to stdout and
exits 0. Control args (each is also echoed in `argv`):

- `--fake-exit=<n>` - exit code `n`.
- `--fake-stderr=<text>` - write `text` to stderr.
- `--fake-stdout=<text>` - write `text` instead of the echo.
- `--fake-stdout-bytes=<n>` - write `n` bytes of `x` instead of the echo.
- `--fake-hang` - sleep forever after writing stdout.
- `--fake-child` - start a silent, hanging copy of itself that inherits the standard handles (it
  holds the stdout pipe open, so only a process-tree kill ends the run).
- `--fake-pid-file=<path>` - before writing stdout, atomically write `<pid>` (plus `\n<child pid>`
  with `--fake-child`) to `path`, so tests can assert the process tree was killed.
- `--version` - print `ccusage 0.0.0-fake`.

Engine-flag rules, so handler tests can drive failures through arguments that pass validation:

- `--debug` - also write `debug: fake` to stderr.
- `--project fail` - exit 4 with stderr `boom`.
- `--project plain` - write `not json\n` instead of the echo.
- `--id hang` - sleep forever after writing stdout.

Stdin is read to end (as UTF-8, like the real engine) only when it is redirected and an argument
equals `statusline`; otherwise `stdin` is `""`.

## Test environment variables

- `CCUSAGE_MCP_E2E_SERVER` - path to a published `ccusage-mcp` for the e2e tests.
- `CCUSAGE_ENGINE` - path to the real `ccusage` engine for the e2e tests.
- `UPDATE_SNAPSHOTS=1` - rewrite snapshot files instead of comparing against them.

## End-to-end tests

`tests/Ccusage.Mcp.Tests/E2E/EndToEndTests.cs` starts the published server over stdio with the MCP
SDK client and asserts each result's text equals running the engine directly with the same argv
(spec §10.3). Without both `CCUSAGE_MCP_E2E_SERVER` and `CCUSAGE_ENGINE` every e2e test reports
inconclusive (`Set CCUSAGE_MCP_E2E_SERVER and CCUSAGE_ENGINE to run e2e tests.`), so the default
suite stays green without the engine. Both the server and the direct engine runs get
`CLAUDE_CONFIG_DIR=tests/fixtures/claude`, `CODEX_HOME=tests/fixtures/codex`, a fresh temp
`HOME`/`USERPROFILE`, `NO_COLOR=1`, `LOG_LEVEL=0` and `CCUSAGE_ENGINE` through the child process
environment; the test process environment is never changed.

Setup (Windows, from the repository root):

```sh
cargo build --manifest-path rust/Cargo.toml --release --bin ccusage --features fetch-litellm-pricing
# Publish through the dotnet MCP tool: mcp__dotnet__dotnet_project Publish,
# project dotnet/src/Ccusage.Mcp, runtime win-x64, configuration Release (never shell dotnet publish).
cd dotnet
CCUSAGE_MCP_E2E_SERVER=$(pwd)/src/Ccusage.Mcp/bin/Release/net10.0/win-x64/publish/ccusage-mcp.exe \
CCUSAGE_ENGINE=$(pwd)/../rust/target/release/ccusage.exe \
dotnet test --project Ccusage.Mcp.slnx
```

The MCP `Test` action cannot set environment variables, so this run uses the shell. Observed on
the fixtures: `daily(all) bytes: 3770` (the length of `ccusage daily --json --offline`).

## AOT rules

- `PublishAot`, `IsAotCompatible` and `IlcTreatWarningsAsErrors` are on: zero IL trim/AOT warnings.
- No reflection-based serialization. All JSON goes through `[JsonSerializable]` source-generated
  `JsonSerializerContext`s.
- No `Type.GetType`, `Activator.CreateInstance`, or dynamic code generation.
