#!/usr/bin/env bash
# Builds the double-click packages into dist/. Run on macOS: packaging/build.sh
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
web="$root/src/PersonaChat.Web"
packaging="$root/packaging"
dist="$root/dist"

# Signing fails inside folders synced by iCloud, because sync keeps adding file attributes.
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

publish() {
  local runtime="$1" target="$2"
  dotnet publish "$web" -c Release -r "$runtime" --self-contained true -o "$target" -v quiet

  # The developer's own settings and history must never reach another person.
  cp "$web/appsettings.example.json" "$target/appsettings.json"
  cp "$packaging/desktop.json" "$target/desktop.json"
  rm -f "$target"/appsettings.Development.json "$target"/*.pdb
  if find "$target" -name '*.db*' | grep -q .; then
    echo "A database file was published, stopping." >&2
    exit 1
  fi
}

zip_folder() {
  local staging="$1" name="$2"
  (cd "$staging" && ditto -c -k --norsrc --noextattr --noqtn . "$dist/$name.zip")
  rm -rf "$staging"
}

build_mac() {
  local runtime="$1" name="$2"
  local staging="$work/$name"
  local bundle="$staging/PersonaChat.app/Contents"

  mkdir -p "$bundle/MacOS" "$bundle/Resources"
  publish "$runtime" "$bundle/Resources/app"
  cp "$packaging/macos/Info.plist" "$bundle/Info.plist"
  cp "$packaging/macos/launcher.sh" "$bundle/MacOS/PersonaChat"
  chmod +x "$bundle/MacOS/PersonaChat"
  cp "$packaging/README.txt" "$staging/"

  # Apple Silicon refuses to run code without at least an ad-hoc signature,
  # and codesign refuses files that carry Finder attributes.
  xattr -cr "$staging/PersonaChat.app"
  codesign --force --deep --sign - "$staging/PersonaChat.app"
  zip_folder "$staging" "$name"
}

build_windows() {
  local name="PersonaChat-Windows-x64"
  local staging="$work/$name"

  mkdir -p "$staging"
  publish win-x64 "$staging/PersonaChat"
  cp "$packaging/README.txt" "$staging/"
  zip_folder "$staging" "$name"
}

rm -rf "$dist"
mkdir -p "$dist"
build_mac osx-arm64 PersonaChat-macOS-AppleSilicon
build_mac osx-x64 PersonaChat-macOS-Intel
build_windows
ls -lh "$dist"
