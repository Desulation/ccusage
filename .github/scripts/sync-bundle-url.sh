#!/bin/sh
# tagpr postVersionCommand (see .tagpr). tagpr rewrites only the first version
# string in each versionFile, which in plugin.json is "version", so this moves
# the release URL in "mcpServers" to the next tag as well. POSIX sh rather than
# Nushell because the tagpr job runs on a bare runner without the Nix shell.
set -eu

: "${TAGPR_NEXT_VERSION:?TAGPR_NEXT_VERSION is set by tagpr}"
file=plugin/.claude-plugin/plugin.json
url="https://github.com/Desulation/ccusage/releases/download/${TAGPR_NEXT_VERSION}/ccusage.mcpb"

sed -i "s#https://github.com/Desulation/ccusage/releases/download/v[0-9][0-9.]*/ccusage\.mcpb#${url}#" "$file"

if [ "$(jq -r .mcpServers "$file")" != "$url" ]; then
	echo "sync-bundle-url: mcpServers in $file is not $url" >&2
	exit 1
fi
