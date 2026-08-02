#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
source "$repo_root/scripts/release-common.sh"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is missing. Install .NET SDK 10 before publishing."
  exit 1
fi

output_dir="publish/linux-x64"
output_mode_file="${output_dir}.publish-mode"
version="${DAISYOS_VERSION:-$(daisyos_version_from_props "$repo_root/Directory.Build.props")}"
channel="${DAISYOS_RELEASE_CHANNEL:-nightly}"
build_date="${DAISYOS_BUILD_DATE:-$(date -u +%Y%m%d)}"
revision="$(daisyos_short_revision)"
fast=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --fast)
      fast=true
      shift
      ;;
    -h|--help)
      echo "Usage: scripts/publish-shell.sh [--fast]"
      echo ""
      echo "  --fast  Skip production ReadyToRun precompilation for local testing."
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      echo "Usage: scripts/publish-shell.sh [--fast]" >&2
      exit 1
      ;;
  esac
done

daisyos_validate_version "$version"
daisyos_validate_channel "$channel"

publish_properties=(
  -p:VersionPrefix="$version"
  -p:DaisyOSVersionChannel="$channel"
  -p:DaisyOSBuildDate="$build_date"
  -p:DaisyOSSourceRevision="$revision"
)

if [[ "$fast" == true ]]; then
  publish_properties+=(
    -p:PublishReadyToRun=false
    -p:PublishReadyToRunComposite=false
  )
  echo "Fast local publish: skipping production ReadyToRun precompilation."
else
  publish_properties+=(
    -p:PublishReadyToRun=true
    -p:PublishReadyToRunComposite=true
  )
fi

rm -rf "$output_dir"
rm -f "$output_mode_file"
dotnet restore src/DaisyOS.Shell/DaisyOS.Shell.csproj \
  --runtime linux-x64 \
  "${publish_properties[@]}"
dotnet publish src/DaisyOS.Shell/DaisyOS.Shell.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --no-restore \
  --output "$output_dir" \
  "${publish_properties[@]}"

if [[ "$fast" == true ]]; then
  printf 'fast\n' > "$output_mode_file"
else
  printf 'production\n' > "$output_mode_file"
fi

echo ""
echo "Publish complete: $output_dir"
if [[ "$fast" == true ]]; then
  echo "Mode: fast local build (not suitable for ISO or release images)"
else
  echo "Mode: production ReadyToRun build"
fi
echo ""

shell_binary="$output_dir/DaisyOS.Shell"
if [ -f "$shell_binary" ]; then
  chmod +x "$shell_binary"
  echo "Binary: $shell_binary"
  echo "Size: $(du -sh "$output_dir" | cut -f1)"
  echo ""
  echo "To test the published binary (headless, outside dotnet run):"
  echo "  DAISYOS_SHELL_SERVICES=mock $shell_binary"
  echo ""
  echo "To copy to ISO profile:"
  echo "  scripts/copy-shell-to-iso.sh"
else
  echo "Warning: Binary not found at expected path."
  echo "Check $output_dir for the published files."
fi
