#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
published_shell="$repo_root/publish/linux-x64/DaisyOS.Shell"

export DAISYOS_SHELL_SERVICES="${DAISYOS_SHELL_SERVICES:-real}"
export XDG_CURRENT_DESKTOP=DaisyOS
export XDG_SESSION_DESKTOP=DaisyOS
export XDG_SESSION_TYPE=wayland

if [[ -x "$published_shell" && "${DAISYOS_USE_PUBLISHED:-0}" == "1" ]]; then
  exec env DAISYOS_SHELL_SESSION=1 "$published_shell" --real-services --shell-session
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "The published shell and dotnet are both unavailable. Run scripts/publish-shell.sh first."
  exit 1
fi

exec env DAISYOS_SHELL_SESSION=1 dotnet run --project "$repo_root/src/DaisyOS.Shell" -- --real-services --shell-session
