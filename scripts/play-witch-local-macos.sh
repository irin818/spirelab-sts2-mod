#!/bin/bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
root="$(pwd -P)"
export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"

fail() { printf '魔女本地测试：%s\n' "$*" >&2; exit 1; }

[[ "$(uname -s)" == Darwin && "$(uname -m)" == arm64 ]] || fail "此启动器需要 Apple Silicon macOS。"
[[ -f SpireLab/SpireLab.csproj && -x scripts/dev-macos.sh ]] || fail "缺少 SpireLab 工程或构建脚本。"
[[ ! -L "$root/.local" ]] || fail ".local 是符号链接，无法确认测试目录独立。"
runtime="$root/.local/witch-local-play"
app="$runtime/SlayTheSpire2.app"
bin="$app/Contents/MacOS"
exe="$bin/Slay the Spire 2"
[[ ! -L "$runtime" ]] || fail "测试目录是符号链接。"
mkdir -p "$runtime"
if [[ ! -f "$runtime/.managed" ]]; then
    [[ -z "$(ls -A "$runtime")" ]] || fail "测试目录已有其他内容：$runtime"
    printf '%s\n' "$root" > "$runtime/.managed"
fi
[[ ! -L "$runtime/.managed" && "$(cat "$runtime/.managed")" == "$root" ]] ||
    fail "测试目录标识和当前工程不一致。"

lock="$runtime/launch.lock"
shlock -f "$lock" -p "$$" || fail "本地测试正在构建或运行。请先正常退出该游戏。"
if ps -axo command= | awk -v exe="$exe" '{ sub(/^[[:space:]]+/, ""); if (index($0, exe " ") == 1) seen=1 } END { exit !seen }'; then
    rm -f "$lock"
    fail "已有同一测试副本正在运行。"
fi
stage=""
cleanup() {
    # The lock makes this one fixed staging directory exclusive to this launcher.
    if [[ -n "$stage" ]]; then rm -rf "$stage"; fi
    rm -f "$lock"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

steamapps="${SteamLibraryPath:-$HOME/Library/Application Support/Steam/steamapps}"
game="${Sts2Path:-$steamapps/common/Slay the Spire 2}"
[[ -d "$game" ]] || fail "找不到游戏。请设置 Sts2Path 指向已安装的《杀戮尖塔 2》目录。"
export Sts2Path="$(cd "$game" && pwd -P)"
source_app="$Sts2Path/SlayTheSpire2.app"
case "$source_app" in "$runtime"/*) fail "Sts2Path 指向测试副本。请使用原始游戏路径。" ;; esac
release_info="$source_app/Contents/Resources/release_info.json"
game_pack="$source_app/Contents/Resources/Slay the Spire 2.pck"
source_exe="$source_app/Contents/MacOS/Slay the Spire 2"
[[ -x "$source_exe" && -s "$release_info" && -s "$game_pack" ]] ||
    fail "游戏 App、版本文件或数据包不完整：$source_app"

logs="$runtime/logs"
[[ ! -L "$logs" ]] || fail "日志目录是符号链接。"
mkdir -p "$logs"
for name in environment build pack game console errors; do
    [[ ! -L "$logs/$name.log" && ! -L "$logs/$name.previous.log" ]] || fail "日志文件是符号链接。"
    if [[ -f "$logs/$name.log" ]]; then mv -f "$logs/$name.log" "$logs/$name.previous.log"; fi
done
printf '工作目录：%s\n日志目录：%s\n' "$runtime" "$logs"
if ! ./scripts/dev-macos.sh check > "$logs/environment.log" 2>&1; then
    tail -n 20 "$logs/environment.log"
    fail "依赖检查失败。缺失的项目依赖可通过 ./scripts/dev-macos.sh setup 准备。"
fi
printf '正在编译本地最新魔女 Mod……\n'
if ! ./scripts/dev-macos.sh build > "$logs/build.log" 2>&1; then
    tail -n 25 "$logs/build.log"
    fail "编译失败，详见 $logs/build.log"
fi
printf '正在导入美术并打包……\n'
if ! SPIRELAB_FORCE_ART_IMPORT=1 ./scripts/dev-macos.sh pack > "$logs/pack.log" 2>&1; then
    tail -n 25 "$logs/pack.log"
    fail "打包失败，详见 $logs/pack.log"
fi

profile="SpireLabLocal-$(printf '%s' "$root" | shasum -a 256 | cut -c1-12)"
profile_base="$HOME/Library/Application Support/$profile"
for path in "$profile_base" "$profile_base/default" "$profile_base/default/1" "$profile_base/default/1/settings.save"; do
    [[ ! -L "$path" ]] || fail "测试存档路径包含符号链接：$path"
done
profile_dir="$profile_base/default/1"
mkdir -p "$profile_dir"
if [[ ! -f "$profile_dir/settings.save" ]]; then
    printf '%s\n' '{"language":"zhs","seen_ea_disclaimer":true,"mod_settings":{"mods_enabled":true}}' > "$profile_dir/settings.save"
fi

stamp="$runtime/game-fingerprint"
[[ ! -L "$app" && ! -L "$stamp" ]] || fail "测试副本或版本记录是符号链接。"
fingerprint="$(
    {
        printf '%s\n' "$source_app"
        shasum -a 256 "$release_info" "$source_exe"
        stat -f '%z %m %i' "$game_pack"
    } | shasum -a 256 | cut -d ' ' -f 1
)"
# A previous interrupted run may leave this private scratch directory.
stage="$runtime/staging"
[[ ! -L "$stage" ]] || { stage=""; fail "暂存目录是符号链接。"; }
if [[ -e "$stage" ]]; then
    # Restore a displaced runnable bundle if termination interrupted the swap.
    if [[ ! -e "$app" && -d "$stage/previous.app" ]]; then mv "$stage/previous.app" "$app"; fi
    rm -rf "$stage"
fi
mkdir -p "$stage/mods/BaseLib" "$stage/mods/SpireLab"
base="$root/.local/baselib"
cp "$base"/BaseLib{.dll,.pck,.json,CompatibilityHelper.dll,CompatibilityHelper.json} "$stage/mods/BaseLib/"
payload="$root/SpireLab/bin/mods/ExportRelease/SpireLab"
cp "$payload"/SpireLab{.dll,.pck,.json} "$stage/mods/SpireLab/"
python3 "$root/scripts/verify-witch-release.py" tested-mods "$stage/mods"

install_config() {
    local target="$1"
    [[ -d "$target" && ! -L "$target" ]] || fail "测试游戏的二进制目录异常：$target"
    [[ "$(cd "$target" && pwd -P)" == "$target" ]] || fail "测试二进制目录指向副本外部。"
    printf '[application]\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name="%s"\n' "$profile" > "$stage/override.cfg"
    [[ ! -d "$target/override.cfg" && ! -L "$target/override.cfg" ]] || fail "测试副本的 override.cfg 类型异常。"
    mv -f "$stage/override.cfg" "$target/override.cfg"
}
install_mods() {
    local target="$1"
    # Move any copied mods aside without walking into them. Only the allowlisted directories are installed.
    for old in mods mods_STEAMTEST; do
        if [[ -e "$target/$old" || -L "$target/$old" ]]; then mv "$target/$old" "$stage/old-$old"; fi
    done
    if ! mv "$stage/mods" "$target/mods"; then
        if [[ -d "$stage/old-mods" ]]; then mv "$stage/old-mods" "$target/mods"; fi
        fail "测试 Mod 安装失败。"
    fi
}

if [[ ! -x "$exe" || ! -f "$stamp" || "$(cat "$stamp")" != "$fingerprint" ]]; then
    printf '正在更新隔离游戏副本……\n'
    candidate="$stage/incoming.app"
    cp -cR "$source_app" "$candidate"
    candidate_bin="$candidate/Contents/MacOS"
    install_config "$candidate_bin"
    install_mods "$candidate_bin"
    if [[ -e "$app" ]]; then mv "$app" "$stage/previous.app"; fi
    if ! mv "$candidate" "$app"; then
        if [[ -d "$stage/previous.app" ]]; then mv "$stage/previous.app" "$app"; fi
        fail "无法替换隔离副本。"
    fi
    printf '%s\n' "$fingerprint" > "$stamp"
else
    install_config "$bin"
    install_mods "$bin"
fi
rm -rf "$stage"
stage=""
[[ -x "$exe" && ! -L "$exe" ]] || fail "隔离副本的游戏程序不可执行。"
printf '测试存档：%s\n正在打开独立游戏，请在主菜单正常选择魔女。\n' "$profile_base"

status=0
env -u SteamAppId -u SteamGameId "$exe" --force-steam=off --log-file "$logs/game.log" > "$logs/console.log" 2>&1 || status=$?
awk '/^ERROR:|^SCRIPT ERROR:|^\[ERROR\]/ { print FILENAME ": " $0 }' "$logs/game.log" "$logs/console.log" > "$logs/errors.log" 2>/dev/null || true
if [[ "$status" -ne 0 ]]; then
    tail -n 24 "$logs/console.log"
    fail "游戏退出状态为 $status。详见 $logs/console.log 和 $logs/errors.log"
fi
printf '测试游戏已退出。日志保存在 %s\n' "$logs"
