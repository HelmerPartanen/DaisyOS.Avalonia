#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
binary="${DAISYOS_SHELL_BINARY:-$repo_root/publish/linux-x64/DaisyOS.Shell}"
output="${1:-$repo_root/artifacts/diagnostics/shell-performance.txt}"
duration="${DAISYOS_PERFORMANCE_SOAK_SECONDS:-1800}"

[[ -x "$binary" ]] || { echo "Missing production shell: $binary" >&2; exit 2; }
[[ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]] || { echo "A graphical session is required." >&2; exit 2; }
mkdir -p "$(dirname "$output")"
rm -f "$output"

DAISYOS_RESOURCE_REPORT_PATH="$output" \
DAISYOS_RESOURCE_REPORT_SCHEDULE_SECONDS="30,300,$duration" \
"$binary" --skip-onboarding --real-services &
shell_pid=$!
trap 'kill "$shell_pid" 2>/dev/null || true; wait "$shell_pid" 2>/dev/null || true' EXIT

deadline=$((SECONDS + duration + 45))
while [[ $SECONDS -lt $deadline ]]; do
  if [[ -s "$output" ]] && [[ "$(rg -c '^--- Shell resource sample' "$output" || true)" -ge 3 ]]; then break; fi
  sleep 1
done
[[ -s "$output" ]] || { echo "The shell did not write its benchmark report." >&2; exit 1; }
"$repo_root/scripts/check-shell-performance-report.sh" "$output"
