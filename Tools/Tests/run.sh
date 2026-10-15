#!/usr/bin/env bash
set -euo pipefail

test_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
dotnet_bin=${DOTNET:-dotnet}

if ! command -v "$dotnet_bin" >/dev/null 2>&1; then
    echo 'Cần .NET SDK 8; đặt DOTNET tới executable nếu SDK không nằm trong PATH.' >&2
    exit 2
fi
if ! command -v python3 >/dev/null 2>&1; then
    echo 'Cần Python 3 để đọc mã nguồn Boot theo encoding gốc.' >&2
    exit 2
fi

"$dotnet_bin" run --project "$test_root/ConfigRegression/ConfigRegression.csproj"
"$dotnet_bin" run --project "$test_root/ConfigRegression/ConfigRegression.csproj" --property:DefineConstants=UNITY_ANDROID
"$dotnet_bin" run --project "$test_root/StartupRegression/StartupRegression.csproj"
"$dotnet_bin" run --project "$test_root/SettingsRegression/SettingsRegression.csproj"
for backend in New Legacy Both None; do
    "$dotnet_bin" run --project "$test_root/ScrollRegression/ScrollRegression.csproj" --property:InputBackend="$backend"
done
