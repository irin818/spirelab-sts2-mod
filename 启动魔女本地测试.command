#!/bin/bash
# Finder launches this file in Terminal.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)" || exit 1
"$root/scripts/play-witch-local-macos.sh"
status=$?
if [[ "$status" -ne 0 && -t 0 ]]; then
    printf '\n启动未成功。请查看上方原因和日志路径，按回车关闭。'
    read -r ignored
fi
exit "$status"
