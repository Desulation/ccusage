#!/bin/sh
# ccusage .mcpb launcher for Linux and macOS (spec §7.3): picks <os>/<arch>/ccusage-mcp next to
# this script and execs it. Windows uses the manifest's win32 platform override instead.
set -eu

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  *)
    echo "ccusage: unsupported OS: $(uname -s)" >&2
    exit 1
    ;;
esac

case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  aarch64 | arm64) arch=arm64 ;;
  *)
    echo "ccusage: unsupported architecture: $(uname -m)" >&2
    exit 1
    ;;
esac

dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
target="$dir/$os/$arch/ccusage-mcp"

if [ ! -f "$target" ]; then
  echo "ccusage: missing $target" >&2
  exit 1
fi

# A zip extractor that drops unix modes leaves the binaries non-executable; restore them.
[ -x "$target" ] || chmod +x "$target" "$dir/$os/$arch/ccusage"

exec "$target" "$@"
