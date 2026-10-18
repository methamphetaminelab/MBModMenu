#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

field() { sed -n "s/.*\"$1\": *\"\([^\"]*\)\".*/\1/p" thunderstore/manifest.json; }
name=$(field name)
version=$(field version_number)

if ! grep -q "\"$name\", \"$version\")" Plugin.cs; then
    echo "manifest.json says $version, BepInPlugin in Plugin.cs does not" >&2
    exit 1
fi

dotnet build -c Release -nologo -v q
rm -rf dist
mkdir dist
zip -jq "dist/$name-$version.zip" thunderstore/manifest.json thunderstore/icon.png README.md "bin/Release/net6.0/$name.dll"
ls -l dist
