#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
package_root="$repo_root/packaging/kwin/daisyos-window-bridge"

if ! command -v kpackagetool6 >/dev/null 2>&1; then
  echo "kpackagetool6 is required to install the DaisyOS KWin bridge." >&2
  exit 1
fi

if [[ ! -f "$package_root/metadata.json" || ! -f "$package_root/contents/code/main.js" ]]; then
  echo "DaisyOS KWin bridge package is incomplete: $package_root" >&2
  exit 1
fi

kpackagetool6 --type KWin/Script --remove daisyos-window-bridge >/dev/null 2>&1 || true
kpackagetool6 --type KWin/Script --install "$package_root"

if command -v kwriteconfig6 >/dev/null 2>&1; then
  kwriteconfig6 --file kwinrc --group Plugins --key daisyos-window-bridgeEnabled true
fi
qdbus6 org.kde.KWin /KWin org.kde.KWin.reconfigure >/dev/null 2>&1 || true
qdbus6 org.kde.KWin /Scripting org.kde.kwin.Scripting.start >/dev/null 2>&1 || true

echo "Installed DaisyOS KWin window bridge for this user session."
