#!/bin/bash
set -u

cd "$(dirname "${BASH_SOURCE[0]}")" || exit 1

if ./scripts/dev-macos.sh witch-test "$@"; then
    exit 0
else
    status=$?
    printf '\n测试实验室启动或运行失败（退出码 %s）。详情见上方输出。\n' "$status" >&2
    if [[ -t 0 ]]; then
        read -r -p '按回车关闭。'
    fi
    exit "$status"
fi
