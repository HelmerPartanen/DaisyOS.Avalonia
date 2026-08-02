#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
publish_dir="$repo_root/publish/linux-x64"
publish_mode_file="${publish_dir}.publish-mode"
iso_shell_dir="$repo_root/os/archiso/DaisyOS/airootfs/opt/DaisyOS/shell"

if [[ ! -d "$publish_dir" ]]; then
  echo "Missing publish output at $publish_dir. Run scripts/publish-shell.sh first."
  exit 1
fi

if [[ ! -f "$publish_mode_file" ]] || [[ "$(<"$publish_mode_file")" != "production" ]]; then
  echo "This shell build is for local testing and cannot be copied into an ISO."
  echo "Run scripts/publish-shell.sh without --fast, then try again."
  exit 1
fi

mkdir -p "$iso_shell_dir"
find "$iso_shell_dir" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
cp -a "$publish_dir"/. "$iso_shell_dir"/

for runtime_executable in DaisyOS.Shell createdump; do
  if [[ -f "$iso_shell_dir/$runtime_executable" ]]; then
    chmod 755 "$iso_shell_dir/$runtime_executable"
  fi
done

source_fingerprint="$("$repo_root/scripts/source-fingerprint.sh")"
source_revision="$(git -C "$repo_root" rev-parse --short=12 HEAD 2>/dev/null || printf 'unknown')"
shell_sha256="$(sha256sum "$iso_shell_dir/DaisyOS.Shell" | awk '{print $1}')"
payload_sha256="$("$repo_root/scripts/payload-fingerprint.sh" "$iso_shell_dir")"
cat > "$iso_shell_dir/.daisyos-build-provenance" <<EOF
source_fingerprint=$source_fingerprint
source_revision=$source_revision
shell_sha256=$shell_sha256
payload_sha256=$payload_sha256
publish_mode=production
EOF

echo "Copied shell to $iso_shell_dir"
