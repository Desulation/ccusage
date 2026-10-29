---
name: fix-ci
description: Diagnoses and fixes failing GitHub Actions checks with gh. Use when CI fails on a pull request and needs logs, focused fixes, and validation.
---

# Fix CI

1. Read the check state with `gh pr checks`, then pull the failing job logs with
   `gh run view <run-id> --log-failed`. Logs fetched through `gh` are reproducible;
   browser-only inspection is not. Pending checks can usually wait unless the failure is
   already clear. The `(non-blocking)` macos-latest jobs sit outside `ci gate` and can queue
   for hours without a runner; do not wait on them, but treat a real failure there as an
   osx-arm64 regression.

2. Reproduce locally. CI runs almost everything through Nix, so the failing step maps onto a
   `just` recipe:

   | CI job / step                                  | Local                                                            |
   | ---------------------------------------------- | ---------------------------------------------------------------- |
   | `preflight`, or `Run nix flake check`          | `just check`                                                     |
   | `Run Rust tests` (`nix build .#ccusage-tests`) | `just rust::test`                                                |
   | `dotnet-test`                                  | `cd dotnet && dotnet test --project Ccusage.Mcp.slnx`            |
   | `e2e`                                          | set `CCUSAGE_MCP_E2E_SERVER` and `CCUSAGE_ENGINE`, then the same |
   | `bundle-smoke`                                 | `pwsh dotnet/packaging/bundle-smoke.ps1`                         |

   Start with the narrowest command that reproduces the failure — a single `cargo test`
   filter — before the recipe above.

3. Fix the smallest cause that explains the failed check, using the skill for the area being
   changed: `testing`, `development`, `docs`, or the nearest package `AGENTS.md`. When the
   failure was generated output or formatting, commit the regenerated result the check
   requires rather than hand-editing it.

4. Commit with the `commit` skill; a manifest change and its lockfile update belong in the
   same commit, and unrelated cleanups belong in separate ones. Let the git hooks run.

5. Push, then use `create-pr` to comment or request another review pass when appropriate.
