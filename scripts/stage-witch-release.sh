#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

if [[ "$#" != 2 && "$#" != 3 ]]; then
    printf 'Usage: %s <tested-game-mods-directory> <tested-screenshot-directory> [verified-existing-workshop-directory]\n' "$0" >&2
    exit 2
fi
tested="$(cd "$1" && pwd -P)"
captures="$(cd "$2" && pwd -P)"
python3 scripts/verify-witch-release.py tested-mods "$tested"
version="$(plutil -extract version raw -o - "$tested/SpireLab/SpireLab.json")"
mkdir -p .local/release
stage="$(mktemp -d ".local/release/Witch-$version.XXXXXX")"
install="$stage/install"
workshop="$stage/SteamWorkshop"
mkdir -p "$install"/{SpireLab,BaseLib} "$workshop"/{content,previews}
for name in SpireLab.dll SpireLab.pck SpireLab.json; do
    cp "$tested/SpireLab/$name" "$install/SpireLab/"
    cp "$tested/SpireLab/$name" "$workshop/content/"
done
python3 scripts/verify-witch-release.py workshop "$workshop"
for name in BaseLib.dll BaseLib.pck BaseLib.json BaseLibCompatibilityHelper.dll BaseLibCompatibilityHelper.json; do
    cp "$tested/BaseLib/$name" "$install/BaseLib/"
done
cp docs/workshop/licenses/BaseLib-MIT.txt "$install/BaseLib/LICENSE.txt"
cp docs/workshop/INSTALL.txt "$install/"
cp docs/workshop/README.md "$workshop/README.md"

if [[ "$#" == 3 ]]; then
    existing="$(cd "$3" && pwd -P)"
    [[ "$(cat "$existing/mod_id.txt")" =~ ^[0-9]+$ ]]
    [[ "$(plutil -extract visibility raw -o - "$existing/workshop.json")" == private ]]
    [[ "$(sips -g format "$existing/image.png" | awk '$1 == "format:" { print $2 }')" == png ]]
    cp "$existing"/{mod_id.txt,workshop.json,image.png} "$workshop/"
else
    cp docs/workshop/workshop.json "$workshop/"
    swift docs/art/witch-v2/export-cover.swift docs/art/witch-v2/source/character-select-reframed.png "$workshop/image.png"
fi
for name in character-select-macos card-hand-macos mirror-form-macos; do
    sips -s format jpeg -s formatOptions 85 "$captures/$name.png" --out "$workshop/previews/$name.jpg" >/dev/null
done
for image in "$workshop/image.png" "$workshop"/previews/*; do
    [[ "$(stat -f %z "$image")" -lt 1000000 ]] || { printf 'Workshop image exceeds the 1 MB limit: %s\n' "$image" >&2; exit 1; }
done
cp "$workshop/image.png" "$install/workshop-cover.png"

archive="SpireLab-The-Witch-$version.zip"
workshop_archive="SpireLab-The-Witch-$version-SteamWorkshop.zip"
(cd "$install" && zip -q -X -r "../$archive" SpireLab BaseLib INSTALL.txt workshop-cover.png)
(cd "$stage" && zip -q -X -r "$workshop_archive" SteamWorkshop)
for file in "$archive" "$workshop_archive"; do
    unzip -tq "$stage/$file"
done
for entry in SpireLab/SpireLab{.dll,.pck,.json} BaseLib/BaseLib{.dll,.pck,.json,CompatibilityHelper.dll,CompatibilityHelper.json}; do
    unzip -p "$stage/$archive" "$entry" | cmp -s - "$tested/$entry"
done
for file in SpireLab{.dll,.pck,.json}; do
    unzip -p "$stage/$workshop_archive" "SteamWorkshop/content/$file" | cmp -s - "$tested/SpireLab/$file"
done
(cd "$stage" && shasum -a 256 "$archive" "$workshop_archive" > SHA256SUMS && shasum -a 256 -c SHA256SUMS)
printf 'Prepared local install and Steam Workshop files: %s\n' "$stage"
