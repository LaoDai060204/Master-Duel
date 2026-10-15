#!/usr/bin/env bash
set -euo pipefail

editor_root=${UNITY_INSTALL_ROOT:-/workspace/.tools/unity/6000.6.3f1}
archive=${UNITY_ARCHIVE_PATH:-/workspace/.tools/Unity-6000.6.3f1.tar.xz}
url=https://download.unity3d.com/download_unity/45d8eee7de74/LinuxEditorInstaller/Unity-6000.6.3f1.tar.xz
# Publisher integrity value from Unity's HTTPS release metadata API.
expected_md5=6b3fb34f445221e3e1b5e439d7e1d87e
marker="$editor_root/.installed-45d8eee7de74"

if [[ -f "$marker" && -x "$editor_root/Editor/Unity" ]]; then
    "$editor_root/Editor/Unity" -version
    exit 0
fi

mkdir -p -- "$(dirname -- "$archive")" "$editor_root"
if [[ ! -f "$archive" ]] || ! printf '%s  %s\n' "$expected_md5" "$archive" | md5sum --check --status; then
    curl -fLsS --retry 2 "$url" -o "$archive.part"
    printf '%s  %s\n' "$expected_md5" "$archive.part" | md5sum --check --status
    mv -- "$archive.part" "$archive"
fi

tar --no-same-owner -xJf "$archive" -C "$editor_root"
"$editor_root/Editor/Unity" -version
touch -- "$marker"
