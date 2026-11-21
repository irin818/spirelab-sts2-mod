#!/usr/bin/env bash

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$(pwd -P)"

fail() {
    printf '%s\n' "$*" >&2
    exit 1
}

[[ "$(uname -s)" == Darwin && "$(uname -m)" == arm64 ]] ||
    fail "This setup script is for Apple Silicon macOS. See README for other platforms."

tool_dir="$root/.local"
downloads="$tool_dir/downloads"
project=SpireLab/SpireLab.csproj
sdk_version="$(plutil -extract sdk.version raw -o - global.json)"
megadot_version=4.5.1-m.14
template_version=2.5.3
baselib_version=3.4.8
godot="$tool_dir/megadot/MegaDot.app/Contents/MacOS/Godot"

export DOTNET_ROOT="$tool_dir/dotnet-10"
export DOTNET_CLI_HOME="$tool_dir"
export NUGET_PACKAGES="$tool_dir/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$DOTNET_ROOT:$PATH"

checksum_matches() {
    local expected="$1" archive="$2" algorithm="$3"
    [[ -f "$archive" ]] && printf '%s  %s\n' "$expected" "$archive" | shasum -a "$algorithm" -c -s
}

download() {
    local url="$1" checksum="$2" algorithm="$3" archive="$downloads/${1##*/}"
    if ! checksum_matches "$checksum" "$archive" "$algorithm"; then
        curl -fSL --retry 2 --silent --show-error "$url" -o "$archive.part"
        checksum_matches "$checksum" "$archive.part" "$algorithm" || fail "Checksum mismatch for $url"
        mv "$archive.part" "$archive"
    fi
    printf '%s\n' "$archive"
}

setup() {
    local archive
    mkdir -p "$downloads"
    if [[ ! -x "$DOTNET_ROOT/dotnet" || ! -d "$DOTNET_ROOT/sdk/$sdk_version" ]]; then
        printf 'Installing .NET SDK %s\n' "$sdk_version"
        archive="$(download "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/dotnet-sdk-$sdk_version-osx-arm64.tar.gz" \
            1c9a13f625d0c08f08ef5a53597bababe3d2473527e98db2199210940c69dd5fc599a39a4d7c30ebf297ca2334a81b2f64b9bf05ff99585de9af4a5d85215e96 512)"
        mkdir -p "$DOTNET_ROOT"
        tar -xzf "$archive" -C "$DOTNET_ROOT"
    fi
    if [[ ! -x "$godot" ]]; then
        printf 'Installing MegaDot %s\n' "$megadot_version"
        archive="$(download "https://megadot.megacrit.com/$megadot_version/megadot-$megadot_version-macos-universal-editor-csharp.zip" \
            3e6838b676648c75a82dfc499631c1bcdbe466b219e8ad09a5f118998ab8d9c4 256)"
        unzip -q -o "$archive" -d "$tool_dir/megadot"
    fi
    if ! check_baselib; then
        printf 'Installing BaseLib %s and its compatibility helper\n' "$baselib_version"
        archive="$(download "https://github.com/Alchyr/BaseLib-StS2/releases/download/v$baselib_version/BaseLib.v$baselib_version.zip" \
            ad30d8aa4dee8e5d31150ab75eae6e6b927fa83ce30af93f4b2a144c3b09f82f 256)"
        unzip -q -o "$archive" -d "$tool_dir/baselib"
    fi
    # The .NET CLI returns 106 when this exact template is already installed.
    dotnet new install "Alchyr.Sts2.Templates@$template_version" || [[ $? -eq 106 ]]
    dotnet tool restore
}

check_baselib() {
    local file
    for file in BaseLib.dll BaseLib.json BaseLib.pck BaseLibCompatibilityHelper.dll BaseLibCompatibilityHelper.json; do
        [[ -s "$tool_dir/baselib/$file" ]] || return 1
    done
    [[ "$(plutil -extract version raw -o - "$tool_dir/baselib/BaseLib.json")" == "v$baselib_version" ]]
}

check_tools() {
    local game_data editor_version
    printf '%s %s\n' "$(sw_vers -productName)" "$(sw_vers -productVersion)"
    git --version
    printf '.NET SDK %s\n' "$(dotnet --version)"
    [[ -x "$godot" ]] || fail "MegaDot is missing. Run $0 setup."
    editor_version="$("$godot" --version)"
    [[ "$editor_version" == "${megadot_version/-/.}.mono."* ]] ||
        fail "Found MegaDot $editor_version. Expected $megadot_version Mono."
    printf 'MegaDot %s\n' "$editor_version"
    game_data="$(dotnet msbuild "$project" -getProperty:Sts2DataDir)"
    [[ -f "$game_data/sts2.dll" ]] || fail "No sts2.dll at $game_data. Set Sts2Path to your Steam game directory."
    printf 'Game %s\n' "$(plutil -extract version raw -o - "$game_data/../release_info.json")"
    printf 'Game reference %s\n' "$game_data/sts2.dll"
    check_baselib || fail "BaseLib or its compatibility helper is missing. Run $0 setup."
    printf 'BaseLib %s\n' "$baselib_version"
    dotnet new list alchyrsts2charmod
    dotnet tool run ilspycmd --version
}

pack() {
    [[ -x "$godot" ]] || fail "MegaDot is missing. Run $0 setup."
    if [[ "${SPIRELAB_FORCE_ART_IMPORT:-0}" == 1 || ! -f SpireLab/.godot/editor/project_metadata.cfg ]]; then
        "$godot" --headless --editor --path "$root/SpireLab" --import
    fi
    dotnet publish "$project" -c ExportRelease -p:GodotPath="$godot"
    python3 "$root/scripts/verify-witch-release.py" pack
}

case "${1:-}" in
    setup) setup ;;
    witch-test)
        shift
        exec "$root/scripts/witch-test-macos.sh" "$@"
        ;;
    art-sync)
        shift
        python3 "$root/scripts/sync-art.py" "$@"
        ;;
    check|build|pack|ilspy|code|editor)
        [[ -x "$DOTNET_ROOT/dotnet" ]] || fail "The project SDK is missing. Run $0 setup."
        case "$1" in
            check) check_tools ;;
            build) dotnet build SpireLab/SpireLab.sln ;;
            pack) pack ;;
            ilspy)
                shift
                dotnet tool run ilspycmd "$@"
                ;;
            editor)
                [[ -x "$godot" ]] || fail "MegaDot is missing. Run $0 setup."
                "$godot" --editor --path "$root/SpireLab"
                ;;
            code)
                vscode="/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code"
                if [[ ! -x "$vscode" ]]; then
                    vscode="$(command -v code)" || fail "VS Code CLI was not found."
                fi
                "$vscode" --install-extension ms-dotnettools.csharp
                "$vscode" --new-window "$root/SpireLab"
                ;;
        esac
        ;;
    *) fail "Usage: $0 {setup|check|build|pack|art-sync|witch-test|ilspy|code|editor}" ;;
esac
