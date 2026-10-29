---
name: ccusage
description: Show coding-agent token usage and cost reports (Claude Code, Codex, OpenCode and 15 more) by day, week, month, session or billing block through the ccusage MCP server.
argument-hint: '[source] [report] [--flags]'
allowed-tools: mcp__plugin_ccusage_ccusage__report
---

# ccusage

1. Call the `report` tool of the ccusage MCP server with `args` set to exactly `$ARGUMENTS`.
   Pass the text unchanged: do not add, remove or reorder words or flags. An empty string
   means a daily report across all sources.
2. Present the returned JSON as a readable table: one row per period (day, week, month,
   session or block) with its token counts and cost, followed by a totals row. After the
   table, name the top cost drivers (the largest models, sources or periods by cost).
3. If the tool returns an error result, show the error text as returned and list the valid
   choices it names (sources, reports or flags) so the user can retry.

Examples:

- `/ccusage claude daily`
- `/ccusage codex monthly`
- `/ccusage weekly --last 2`

If the tool is unavailable right after installation, run `/mcp` once to reconnect.
