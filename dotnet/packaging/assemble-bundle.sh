#!/usr/bin/env bash
# Assembles the release assets from the build-matrix artifacts (spec §7.3, §9.3):
#   <out>/ccusage.mcpb                     single bundle, layout per spec §7.3
#   <out>/ccusage-mcp-<rid>.tar.gz         linux/osx: ccusage-mcp, ccusage, LICENSE
#   <out>/ccusage-mcp-win-{x64,arm64}.zip  ccusage-mcp.exe (win-x64 server), ccusage.exe, LICENSE
#   <out>/SHA256SUMS                       sha256sum format, basenames
#
# Usage: assemble-bundle.sh --version <v> --artifacts <dir> --out <dir>
# Input layout: <artifacts>/ccusage-mcp-<rid>/ccusage-mcp[.exe] (five server RIDs) and
#               <artifacts>/ccusage-<rid>/ccusage[.exe] (all six RIDs).
#
# The zips are built with Info-ZIP `zip`, which stores unix modes; never Python zipfile or
# Compress-Archive, which store none (spec §15 row 4).
set -euo pipefail

die() {
  echo "assemble-bundle: $*" >&2
  exit 1
}

version=""
artifacts=""
out=""
while [ $# -gt 0 ]; do
  case "$1" in
    --version) [ $# -ge 2 ] || die "--version needs a value"; version=$2; shift 2 ;;
    --artifacts) [ $# -ge 2 ] || die "--artifacts needs a value"; artifacts=$2; shift 2 ;;
    --out) [ $# -ge 2 ] || die "--out needs a value"; out=$2; shift 2 ;;
    *) die "unknown argument: $1 (usage: assemble-bundle.sh --version <v> --artifacts <dir> --out <dir>)" ;;
  esac
done
[ -n "$version" ] || die "missing --version"
[ -n "$artifacts" ] || die "missing --artifacts"
[ -n "$out" ] || die "missing --out"

command -v zip > /dev/null 2>&1 || die "Info-ZIP zip is required"
if command -v sha256sum > /dev/null 2>&1; then
  sha256() { sha256sum "$@"; }
elif command -v shasum > /dev/null 2>&1; then
  sha256() { shasum -a 256 "$@"; }
else
  die "sha256sum or shasum is required"
fi

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
template="$script_dir/manifest.template.json"
launcher="$script_dir/launch.sh"
license="$script_dir/../../LICENSE"

server_rids="linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64"
engine_rids="linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64 win-arm64"

exe_suffix() {
  case "$1" in
    win-*) echo ".exe" ;;
    *) echo "" ;;
  esac
}

server_input() { echo "$artifacts/ccusage-mcp-$1/ccusage-mcp$(exe_suffix "$1")"; }
engine_input() { echo "$artifacts/ccusage-$1/ccusage$(exe_suffix "$1")"; }

# Fail on the first missing input, before anything is written.
for f in "$template" "$launcher" "$license"; do
  [ -f "$f" ] || die "missing input: $f"
done
for rid in $server_rids; do
  f=$(server_input "$rid")
  [ -f "$f" ] || die "missing input: $f"
done
for rid in $engine_rids; do
  f=$(engine_input "$rid")
  [ -f "$f" ] || die "missing input: $f"
done

mkdir -p "$out"
out=$(CDPATH= cd -- "$out" && pwd)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# --- ccusage.mcpb -------------------------------------------------------------------------
stage="$work/bundle"
mkdir -p "$stage/win/x64" "$stage/win/arm64"
sed "s|__VERSION__|$version|" "$template" > "$stage/manifest.json"
grep -q "__VERSION__" "$stage/manifest.json" && die "manifest.json still contains __VERSION__"
cp "$launcher" "$stage/launch.sh"

cp "$(server_input win-x64)" "$stage/win/ccusage-mcp.exe"
cp "$(engine_input win-x64)" "$stage/win/x64/ccusage.exe"
cp "$(engine_input win-arm64)" "$stage/win/arm64/ccusage.exe"
for rid in linux-x64 linux-arm64 osx-x64 osx-arm64; do
  dir="$stage/${rid%%-*}/${rid#*-}"
  mkdir -p "$dir"
  cp "$(server_input "$rid")" "$dir/ccusage-mcp"
  cp "$(engine_input "$rid")" "$dir/ccusage"
done

# Modes first, so Info-ZIP records them in the external attributes.
chmod +x "$stage/launch.sh" "$stage"/win/ccusage-mcp.exe "$stage"/win/*/ccusage.exe \
  "$stage"/linux/*/* "$stage"/osx/*/*

rm -f "$out/ccusage.mcpb"
# -X drops the extra uid/gid/timestamp fields (modes stay); -D omits directory entries, so the
# archive holds exactly the 13 files of spec §7.3.
(cd "$stage" && zip -q -X -D -r "$out/ccusage.mcpb" manifest.json launch.sh win linux osx)

# --- per-platform archives ----------------------------------------------------------------
for rid in linux-x64 linux-arm64 osx-x64 osx-arm64; do
  dir="$work/archive-$rid"
  mkdir -p "$dir"
  cp "$(server_input "$rid")" "$dir/ccusage-mcp"
  cp "$(engine_input "$rid")" "$dir/ccusage"
  cp "$license" "$dir/LICENSE"
  chmod +x "$dir/ccusage-mcp" "$dir/ccusage"
  rm -f "$out/ccusage-mcp-$rid.tar.gz"
  tar -czf "$out/ccusage-mcp-$rid.tar.gz" -C "$dir" ccusage-mcp ccusage LICENSE
done

for rid in win-x64 win-arm64; do
  dir="$work/archive-$rid"
  mkdir -p "$dir"
  # Windows ships only the x64 server; it runs emulated on ARM64 (spec §7.3).
  cp "$(server_input win-x64)" "$dir/ccusage-mcp.exe"
  cp "$(engine_input "$rid")" "$dir/ccusage.exe"
  cp "$license" "$dir/LICENSE"
  rm -f "$out/ccusage-mcp-$rid.zip"
  (cd "$dir" && zip -q -X "$out/ccusage-mcp-$rid.zip" ccusage-mcp.exe ccusage.exe LICENSE)
done

# --- checksums ----------------------------------------------------------------------------
(
  cd "$out"
  sha256 ccusage.mcpb \
    ccusage-mcp-linux-arm64.tar.gz ccusage-mcp-linux-x64.tar.gz \
    ccusage-mcp-osx-arm64.tar.gz ccusage-mcp-osx-x64.tar.gz \
    ccusage-mcp-win-arm64.zip ccusage-mcp-win-x64.zip > SHA256SUMS
)

echo "assemble-bundle: wrote $out (version $version)"
