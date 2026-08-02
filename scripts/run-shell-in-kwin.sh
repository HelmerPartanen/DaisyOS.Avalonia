#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
published_shell="$repo_root/publish/linux-x64/DaisyOS.Shell"

export DAISYOS_SHELL_SERVICES="${DAISYOS_SHELL_SERVICES:-real}"
export XDG_CURRENT_DESKTOP=DaisyOS
export XDG_SESSION_DESKTOP=DaisyOS
export XDG_SESSION_TYPE=wayland

if [[ -x "$published_shell" ]]; then
  exec "$published_shell" --real-services
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "The published shell and dotnet are both unavailable. Run scripts/publish-shell.sh first."
  exit 1
fi

exec dotnet run --project "$repo_root/src/DaisyOS.Shell" -- --real-services
