---
name: development
description: Guides ccusage monorepo development. Use when editing workspace packages, dependencies, or shared configuration, and when running the `just` build, typecheck, test, format, or check recipes.
---

# ccusage Development

Root `AGENTS.md` holds the repository shape and the standing policies. This
skill is about working in the tree: the MCP packaging seam, the recipes, and
validation.

## The MCP Packaging Seam

`dotnet/` is the C# MCP server that wraps the Rust binary; the `dotnet` skill
and `dotnet/AGENTS.md` cover it. `dotnet/packaging/*` holds the `.mcpb`
manifest template, the Linux/macOS arch launcher and the bundle scripts,
`plugin/` is the Claude Code plugin, and `.claude-plugin/` its marketplace
entry. The version lives in every file listed under `versionFile` in `.tagpr`;
a packaging change usually touches those together.

## Gotchas

- `.claude/skills` is generated from `.agents/skills` by `nix/agent-skills.nix`.
  Edit the source tree; leave the generated one uncommitted.
- `LOG_LEVEL` gates runtime noise (`rust/crates/ccusage-core/src/logger.rs`):
  `0` suppresses progress and box titles, `>= 4` logs pricing refresh detail.
  Use `LOG_LEVEL=0` whenever output is captured or compared.

`references/commands.md` covers `just`, where a new dependency or tool belongs,
validation, and releases.
