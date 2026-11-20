#!/usr/bin/env bash

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$(pwd -P)"
case "${1:-surveyor}" in
    surveyor) character=surveyor; smoke_flag=--spirelab-smoke ;;
    witch) character=witch; smoke_flag=--witch-smoke ;;
    *) printf 'Usage: %s [surveyor|witch]\n' "$0" >&2; exit 2 ;;
esac
mkdir -p .local/logs
./scripts/dev-macos.sh build
./scripts/dev-macos.sh pack > .local/logs/"$character"-smoke-pack.log 2>&1

export DOTNET_ROOT="$root/.local/dotnet-10"
export DOTNET_CLI_HOME="$root/.local"
export NUGET_PACKAGES="$root/.local/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"

steamapps="${SteamLibraryPath:-$HOME/Library/Application Support/Steam/steamapps}"
game="${Sts2Path:-$steamapps/common/Slay the Spire 2}"
test_dir="$(mktemp -d "$root/.local/$character-smoke.XXXXXX")"
test_profile="SpireLabSmoke-${test_dir##*.}"
app="$test_dir/SlayTheSpire2.app"
cp -cR "$game/SlayTheSpire2.app" "$test_dir/"
bin="$app/Contents/MacOS"
mods="$bin/mods"

# The clone can contain existing local mods. Move them out of the loader's search path.
if [[ -d "$mods" ]]; then mv "$mods" "$test_dir/original-mods"; fi
if [[ -d "$bin/mods_STEAMTEST" ]]; then mv "$bin/mods_STEAMTEST" "$test_dir/original-mods-steamtest"; fi
mkdir -p "$mods/BaseLib" "$mods/SpireLab" "$mods/SpireLabSmoke"
cp .local/baselib/BaseLib{.dll,.pck,.json,CompatibilityHelper.dll,CompatibilityHelper.json} "$mods/BaseLib/"
cp SpireLab/bin/mods/ExportRelease/SpireLab/SpireLab{.dll,.pck,.json} "$mods/SpireLab/"
dotnet build tests/SpireLab.Smoke/SpireLab.Smoke.csproj -o "$test_dir/smoke-build"
cp "$test_dir/smoke-build/SpireLab.Smoke.dll" tests/SpireLab.Smoke/SpireLab.Smoke.json "$mods/SpireLabSmoke/"

cat > "$bin/override.cfg" <<EOF_CONFIG
[application]
config/use_custom_user_dir=true
config/custom_user_dir_name="$test_profile"
EOF_CONFIG
profile="$HOME/Library/Application Support/$test_profile/default/1"
mkdir -p "$profile"
cat > "$profile/settings.save" <<'EOF_SETTINGS'
{"language":"eng","mod_settings":{"mods_enabled":true}}
EOF_SETTINGS

log="$test_dir/game.log"
printf 'Smoke test log: %s\n' "$log"
if ! "$bin/Slay the Spire 2" --headless --force-steam=off "$smoke_flag" --quit-after 15000 \
    --max-fps 120 --log-file "$log" > "$test_dir/console.log" 2>&1; then
    tail -n 40 "$test_dir/console.log" >&2
    exit 1
fi
if ! awk '
    /^\[SpireLabSmoke\] / { print }
    /^\[ERROR\]/ || /^ERROR:/ || /^SCRIPT ERROR:/ {
        if (passed && /were leaked at exit\.$|resources still in use at exit/) next
        print
        failed = 1
    }
    $0 == "[SpireLabSmoke] PASS" { passed = 1 }
    END { exit (!passed || failed) }
' "$log"; then
    printf 'Game errors or incomplete smoke test in %s\n' "$log" >&2
    exit 1
fi
