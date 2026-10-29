#!/usr/bin/env bash
# Verifies a ccusage.mcpb: exact spec §7.3 file list, manifest fields, unix exec bits on the
# Linux/macOS binaries (spec §15 row 4) and that launch.sh parses.
#
# Usage: verify-bundle.sh <ccusage.mcpb> --version <v>
# Exits 1 naming the first failing check; prints "verify-bundle: ok" on success.
set -euo pipefail

fail() {
  echo "verify-bundle: FAIL: $*" >&2
  exit 1
}

bundle=""
version=""
while [ $# -gt 0 ]; do
  case "$1" in
    --version) [ $# -ge 2 ] || fail "--version needs a value"; version=$2; shift 2 ;;
    -*) fail "unknown argument: $1 (usage: verify-bundle.sh <ccusage.mcpb> --version <v>)" ;;
    *) [ -z "$bundle" ] || fail "unexpected argument: $1"; bundle=$1; shift ;;
  esac
done
[ -n "$bundle" ] || fail "missing <ccusage.mcpb> argument"
[ -n "$version" ] || fail "missing --version"
[ -f "$bundle" ] || fail "bundle not found: $bundle"

for tool in unzip zipinfo jq; do
  command -v "$tool" > /dev/null 2>&1 || fail "$tool is required"
done

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
unzip -q "$bundle" -d "$work/x" || fail "cannot unzip $bundle"

# --- file list: exactly the 13 files of spec §7.3 -----------------------------------------
expected="manifest.json
launch.sh
win/ccusage-mcp.exe
win/x64/ccusage.exe
win/arm64/ccusage.exe
linux/x64/ccusage-mcp
linux/x64/ccusage
linux/arm64/ccusage-mcp
linux/arm64/ccusage
osx/x64/ccusage-mcp
osx/x64/ccusage
osx/arm64/ccusage-mcp
osx/arm64/ccusage"

entries=$(zipinfo -1 "$bundle") || fail "cannot list $bundle"
while IFS= read -r path; do
  printf '%s\n' "$entries" | grep -qxF -- "$path" || fail "missing file: $path"
done <<< "$expected"
while IFS= read -r entry; do
  printf '%s\n' "$expected" | grep -qxF -- "$entry" || fail "unexpected entry: $entry"
done <<< "$entries"
count=$(printf '%s\n' "$entries" | wc -l | tr -d ' ')
[ "$count" = "13" ] || fail "expected 13 entries, found $count"

# --- manifest.json --------------------------------------------------------------------------
manifest="$work/x/manifest.json"
jq -e . "$manifest" > /dev/null 2>&1 || fail "manifest.json is not valid JSON"

check() {
  # $1 = jq filter (single-quoted at the call site when it contains ${__dirname}).
  jq -e --arg v "$version" "$1" "$manifest" > /dev/null 2>&1 || fail "manifest.json: $1"
}
check '.manifest_version == "0.3"'
check '.name == "ccusage"'
check '.version == $v'
check '.server.type == "binary"'
check '.server.entry_point == "launch.sh"'
check '.server.mcp_config.command == "/bin/sh"'
check '.server.mcp_config.args == ["${__dirname}/launch.sh"]'
check '.server.mcp_config.platform_overrides.win32.command == "${__dirname}/win/ccusage-mcp.exe"'
check '.compatibility.platforms == ["darwin","linux","win32"]'

# --- exec bits on linux/*/* and osx/*/* ----------------------------------------------------
modes=$(zipinfo -l "$bundle" | awk '{ print $1, $NF }') || fail "cannot read modes of $bundle"
checked=0
while read -r mode name; do
  case "$name" in
    linux/*/* | osx/*/*)
      checked=$((checked + 1))
      case "$mode" in
        ???x*) ;;
        *) fail "not executable in zip: $name (mode $mode)" ;;
      esac
      ;;
  esac
done <<< "$modes"
[ "$checked" = "8" ] || fail "expected 8 linux/osx binaries in zipinfo -l, found $checked"

# --- launch.sh ------------------------------------------------------------------------------
sh -n "$work/x/launch.sh" || fail "launch.sh does not parse (sh -n)"

echo "verify-bundle: ok"
