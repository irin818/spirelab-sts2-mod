#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$(pwd -P)"
scenario=mirror-basic
check=false
verify=false
while [[ $# -gt 0 ]]; do
    case "$1" in
        --check) check=true; shift ;;
        --verify) verify=true; shift ;;
        --scenario) scenario="${2:?scenario required}"; shift 2 ;;
        *) printf 'Unknown Witch Test Lab option: %s\n' "$1" >&2; exit 2 ;;
    esac
done
case "$scenario" in
    mirror-basic|mirror-reflect|mirror-stacking|mirror-multihit|mirror-hp|desire-greed|desire-sloth|desire-gamble|arcane-insight|early-game) ;;
    *) printf 'Unknown Witch Test Lab scenario: %s\n' "$scenario" >&2; exit 2 ;;
esac
./scripts/dev-macos.sh check
export DOTNET_ROOT="$root/.local/dotnet-10"
export DOTNET_CLI_HOME="$root/.local"
export NUGET_PACKAGES="$root/.local/nuget"
export PATH="$DOTNET_ROOT:$PATH"
game="${Sts2Path:-${SteamLibraryPath:-$HOME/Library/Application Support/Steam/steamapps}/common/Slay the Spire 2}"
godot="$root/.local/megadot/MegaDot.app/Contents/MacOS/Godot"
[[ -x "$game/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2" ]]
mkdir -p .local/witch-test-lab
lab="$(mktemp -d "$root/.local/witch-test-lab/session.XXXXXX")"
profile="SpireLabTestLab-${lab##*.}"
cache="$lab/project"
mods="$lab/SlayTheSpire2.app/Contents/MacOS/mods"
mkdir -p "$cache" "$lab/output"
rsync -a --exclude=.godot --exclude=bin --exclude=obj SpireLab/ "$cache/"
mod_version="$(plutil -extract version raw -o - "$cache/SpireLab.json")"
printf '正在从当前工程构建魔女 %s Debug 版并准备独立游戏副本。\n' "$mod_version"
cp -cR "$game/SlayTheSpire2.app" "$lab/"
bin="$lab/SlayTheSpire2.app/Contents/MacOS"
# Only move paths in the fresh clone. Do not leave symlinked loaders in the sandbox.
for name in mods mods_STEAMTEST; do
    if [[ -e "$bin/$name" || -L "$bin/$name" ]]; then mv "$bin/$name" "$lab/$name-from-source"; fi
done
mkdir -p "$mods"/{BaseLib,SpireLab,SpireLab.TestLab}
cp .local/baselib/BaseLib{.dll,.pck,.json,CompatibilityHelper.dll,CompatibilityHelper.json} "$mods/BaseLib/"
{
    dotnet build "$cache/SpireLab.csproj" -c Debug -p:WitchTestLab=true -p:ModOutputDir="$lab/output"
    "$godot" --headless --editor --path "$cache" --import
    "$godot" --headless --path "$cache" --export-pack BasicExport "$lab/output/SpireLab.pck"
    dotnet build tests/SpireLab.TestLab/SpireLab.TestLab.csproj -c Debug -o "$lab/launcher" -p:SpireLabTestAssembly="$lab/output/SpireLab.dll"
} > "$lab/build.log" 2>&1 || { tail -45 "$lab/build.log" >&2; exit 1; }
cp "$lab/output"/SpireLab{.dll,.pck,.json} "$mods/SpireLab/"
cp "$lab/launcher/SpireLab.TestLab.dll" tests/SpireLab.TestLab/SpireLab.TestLab.json "$mods/SpireLab.TestLab/"
# Never write through a possible copied override.cfg symlink.
if [[ -e "$bin/override.cfg" || -L "$bin/override.cfg" ]]; then mv "$bin/override.cfg" "$lab/override-from-source.cfg"; fi
cat > "$bin/override.cfg" <<PROFILE
[application]
config/use_custom_user_dir=true
config/custom_user_dir_name="$profile"
PROFILE
settings="$HOME/Library/Application Support/$profile/default/1"
[[ ! -e "$settings" ]]
mkdir -p "$settings"
cat > "$settings/settings.save" <<'SETTINGS'
{"language":"zhs","seen_ea_disclaimer":true,"mod_settings":{"mods_enabled":true}}
SETTINGS
[[ "$(find "$mods" -maxdepth 1 -type d -name 'SpireLab' | wc -l | tr -d ' ')" == 1 ]]
printf 'Witch Test Lab files: %s\nWitch Test Lab profile: %s\nSteam and Steam Cloud: disabled in the cloned game\n' "$lab" "$settings"
if "$check"; then exit 0; fi
export SPIRELAB_TEST_LAB_PROFILE="$profile"
export SPIRELAB_TEST_LAB_ROOT="$lab"
export SPIRELAB_TEST_LAB_SCENARIO="$scenario"
export SPIRELAB_TEST_LAB_VERIFY="$verify"
"$bin/Slay the Spire 2" --force-steam=off --witch-test-lab --windowed --resolution 1280x800 --log-file "$lab/game.log"
if "$verify"; then
    awk '
        /^\[WitchTestLab\] ISOLATED / { isolated = 1 }
        $0 == "[WitchTestLab] PASS" { passed = 1 }
        /^\[ERROR\]/ || /^ERROR:/ || /^SCRIPT ERROR:/ {
            if (passed && /were leaked at exit\.$|resources still in use at exit|shaders of type .* were never freed$/) next
            print
            failed = 1
        }
        END { exit !(isolated && passed && !failed) }
    ' "$lab/game.log"
fi
