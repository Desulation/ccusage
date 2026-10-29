---
name: dotnet
description: Guides work in dotnet/: the C# Native AOT MCP server, its MSTest suite, FakeEngine, fixtures and .mcpb packaging. Use before editing .cs, .csproj, .props, .slnx or dotnet/packaging files.
paths:
  - 'dotnet/**'
globs: 'dotnet/**'
---

# ccusage dotnet

`dotnet/` holds `ccusage-mcp`, a Native AOT MCP server (C#, .NET 10) that runs the `ccusage`
engine and returns its output as MCP tool results. It is a distribution layer: adapter logic stays
in Rust, and `rust/` is never modified from here.

Read `dotnet/AGENTS.md` first. It owns the layout, the commands, the FakeEngine protocol, the e2e
setup and the AOT rules; this skill only routes and states the rules that bind every change.
Design authority: `docs/superpowers/specs/2026-10-29-csharp-aot-mcp-server-design.md`.

## Tooling

- Prefer the dotnet MCP tools (`mcp__dotnet__dotnet_project` `Build` / `Test` / `Publish`,
  `mcp__dotnet__dotnet_package` for package changes) over shell `dotnet`.
- Run `dotnet test` with `dotnet/` as the working directory: `dotnet/global.json` selects the
  Microsoft.Testing.Platform runner, and from the repository root the run fails.
- A local AOT publish (`-r <rid>`) goes only through the dotnet MCP `Publish` action; from the
  shell the native link step fails because `vswhere.exe` is not on `PATH`.

## Rules

- TDD for every behavior change (`tdd` skill): a failing MSTest first, then the code.
- The tool catalog is derived from `rust/crates/ccusage-cli-parser/src/cli-commands.json` and
  `cli-help.json`, embedded into `Ccusage.Mcp` by link. Never copy, hand-write or edit that data.
- AOT: zero trim/AOT warnings, source-generated `JsonSerializerContext` for all JSON, no
  reflection-based serialization, `Type.GetType`, `Activator.CreateInstance` or dynamic code.
- The engine is located through `CCUSAGE_ENGINE`, then the server's directory, then its
  `<arch>` subfolder (`x64` or `arm64`, the OS architecture), then `PATH`
  (`Engine/EngineLocator.cs`).
- The only approved third-party NuGet package is `ModelContextProtocol` 2.3.0. First-party
  Microsoft/.NET packages are fine; anything else needs explicit approval. Versions live in
  `Directory.Packages.props`.

## Tests

- `tests/Ccusage.Mcp.Tests/` - MSTest unit, integration and e2e tests, with method-level
  parallelism: never set process-wide environment variables in a test; pass them through the
  child process environment or an injected `getEnv`.
- `tests/FakeEngine/` - the test-only `ccusage` stand-in; its control flags are in
  `dotnet/AGENTS.md`.
- `tests/fixtures/` - Claude, Codex and statusline fixture data for the e2e and bundle-smoke tests.
- Snapshots live in `tests/Ccusage.Mcp.Tests/Snapshots/`; run with `UPDATE_SNAPSHOTS=1` to
  rewrite them, then re-run without it.
- E2E tests run only when `CCUSAGE_MCP_E2E_SERVER` (a published `ccusage-mcp`) and
  `CCUSAGE_ENGINE` (a real `ccusage` engine) are both set; otherwise they report inconclusive.

## Packaging

`dotnet/packaging/` holds the `.mcpb` manifest template, the Linux/macOS arch launcher and the
bundle scripts; `plugin/` and `.claude-plugin/` hold the Claude Code plugin and marketplace. The
version in `dotnet/Directory.Build.props` is bumped by tagpr together with the other `.tagpr`
version files.
