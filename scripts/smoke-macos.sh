#!/usr/bin/env bash

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$(pwd -P)"
quit_after=15000
case "${1:-witch}" in
    witch) character=witch; smoke_flag=--witch-smoke ;;
    witch-save) character=witch-save; smoke_flag=--witch-save-probe ;;
    witch-legacy-save) character=witch-legacy-save; smoke_flag=--witch-resume-probe ;;
    witch-campaign) character=witch-campaign; smoke_flag=--witch-campaign; quit_after=200000 ;;
    witch-art) character=witch-art; smoke_flag=--witch-art ;;
    witch-finisher) character=witch-finisher; smoke_flag=--witch-finisher ;;
    *) printf 'Usage: %s [witch|witch-save|witch-legacy-save|witch-campaign|witch-art|witch-finisher]\n' "$0" >&2; exit 2 ;;
esac
if [[ "$character" == witch-legacy-save && ! -f "${SPIRELAB_WITCH_LEGACY_SAVE:-}" ]]; then
    printf 'Set SPIRELAB_WITCH_LEGACY_SAVE to a Witch v1.0 save probe fixture.\n' >&2
    exit 2
fi
mkdir -p .local/logs
if [[ -n "${SPIRELAB_SMOKE_MOD_SOURCE:-}" ]]; then
    payload="$(cd "$SPIRELAB_SMOKE_MOD_SOURCE" && pwd -P)"
    for suffix in dll pck json; do
        [[ -s "$payload/SpireLab.$suffix" ]] || { printf 'Missing SpireLab.%s in %s\n' "$suffix" "$payload" >&2; exit 1; }
    done
    printf 'Using existing runtime files from %s\n' "$payload"
else
    ./scripts/dev-macos.sh build
    ./scripts/dev-macos.sh pack > .local/logs/"$character"-smoke-pack.log 2>&1
    payload="$root/SpireLab/bin/mods/ExportRelease/SpireLab"
fi

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
baselib_payload="${SPIRELAB_SMOKE_BASELIB_SOURCE:-$root/.local/baselib}"
cp "$baselib_payload"/BaseLib{.dll,.pck,.json,CompatibilityHelper.dll,CompatibilityHelper.json} "$mods/BaseLib/"
cp "$payload"/SpireLab{.dll,.pck,.json} "$mods/SpireLab/"
dotnet build tests/SpireLab.Smoke/SpireLab.Smoke.csproj \
    --artifacts-path "$test_dir/smoke-artifacts" -o "$test_dir/smoke-build"
cp "$test_dir/smoke-build/SpireLab.Smoke.dll" tests/SpireLab.Smoke/SpireLab.Smoke.json "$mods/SpireLabSmoke/"

cat > "$bin/override.cfg" <<EOF_CONFIG
[application]
config/use_custom_user_dir=true
config/custom_user_dir_name="$test_profile"
EOF_CONFIG
profile="$HOME/Library/Application Support/$test_profile/default/1"
mkdir -p "$profile"
limit_background=true
if [[ "$character" == witch-finisher ]]; then limit_background=false; fi
cat > "$profile/settings.save" <<EOF_SETTINGS
{"language":"eng","seen_ea_disclaimer":true,"limit_fps_in_background":$limit_background,"mod_settings":{"mods_enabled":true}}
EOF_SETTINGS
if [[ "$character" == witch-legacy-save ]]; then
    mkdir -p "$profile/modded/profile1/saves"
    cp "$SPIRELAB_WITCH_LEGACY_SAVE" "$profile/modded/profile1/saves/current_run.save"
fi

render_args=(--headless)
if [[ "$character" == witch-art || "$character" == witch-finisher || "${SPIRELAB_SMOKE_WINDOWED:-0}" == 1 ]]; then
    render_args=(--windowed --resolution "${SPIRELAB_SMOKE_RESOLUTION:-1200x675}")
fi
if [[ "$character" == witch-art ]]; then
    export SPIRELAB_WITCH_ART_SCREENSHOTS="$test_dir/captures"
    mkdir -p "$SPIRELAB_WITCH_ART_SCREENSHOTS"
    printf 'Screenshots: %s\n' "$SPIRELAB_WITCH_ART_SCREENSHOTS"
fi
if [[ "$character" == witch-finisher ]]; then
    export SPIRELAB_WITCH_FINISHER_CAPTURES="$test_dir/captures"
    mkdir -p "$SPIRELAB_WITCH_FINISHER_CAPTURES"
    printf 'Finisher screenshots: %s\n' "$SPIRELAB_WITCH_FINISHER_CAPTURES"
fi
log="$test_dir/game.log"
printf 'Smoke test log: %s\n' "$log"
run_game() {
    local flag="$1" target_log="$2"
    if ! "$bin/Slay the Spire 2" "${render_args[@]}" --force-steam=off "$flag" --quit-after "$quit_after" \
        --max-fps 120 --log-file "$target_log" > "$target_log.console" 2>&1; then
        tail -n 40 "$target_log.console" >&2
        return 1
    fi
}
run_game "$smoke_flag" "$log"
if [[ "$character" == witch-campaign ]]; then
    awk '/Selecting character:|Entering Boss|Victory!|Run failed/ { print }' "$log"
    awk '/Selecting character: .*SPIRELAB-THE_WITCH/ { witch = 1 }
         /Victory! Run completed/ { win = 1 }
         !win && (/^\[ERROR\]/ || /^ERROR:/ || /^SCRIPT ERROR:/) { failed = 1 }
         END { exit !(witch && win && !failed) }' "$log"
    exit $?
fi

validate_log() {
    if ! awk '
    /^\[SpireLabSmoke\] / { print }
    /^\[ERROR\]/ || /^ERROR:/ || /^SCRIPT ERROR:/ {
        if (passed && /were leaked at exit\.$|resources still in use at exit|shaders of type .* were never freed$/) next
        print
        failed = 1
    }
    $0 == "[SpireLabSmoke] PASS" { passed = 1 }
    END { exit (!passed || failed) }
' "$1"; then
        printf 'Game errors or incomplete smoke test in %s\n' "$1" >&2
        return 1
    fi
}

validate_log "$log"
if [[ "$character" == witch-save ]]; then
    resume_log="$test_dir/resume.log"
    printf 'Cold resume log: %s\n' "$resume_log"
    run_game --witch-resume-probe "$resume_log"
    validate_log "$resume_log"
fi
