#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
package_root="$repo_root/packaging/kwin/daisyos-window-bridge"
metadata="$package_root/metadata.json"
script="$package_root/contents/code/main.js"

[[ -f "$metadata" ]]
[[ -f "$script" ]]
grep -Fq '"KPackageStructure": "KWin/Script"' "$metadata"
grep -Fq '"Id": "daisyos-window-bridge"' "$metadata"
grep -Fq 'workspace.windowAdded.connect' "$script"
grep -Fq 'workspace.windowActivated.connect' "$script"
grep -Fq 'PublishSnapshot' "$script"
grep -Fq 'keepBelow = true' "$script"

echo "KWin window bridge package checks passed."
