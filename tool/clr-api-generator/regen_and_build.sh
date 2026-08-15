#!/usr/bin/env bash
# 重新生成 C# UnmanagedAPI 声明并构建 C# 解决方案（带互斥锁，供并行代理使用）
# 用法：bash tool/clr-api-generator/regen_and_build.sh
set -u
REPO=$(cd "$(dirname "$0")/../.." && pwd)
LOCK="/tmp/luastg_clr_build.lockdir"

# mkdir 在所有平台上都是原子操作，可作为互斥锁（无 flock 依赖）
acquire_lock() {
    local tries=0
    while ! mkdir "$LOCK" 2>/dev/null; do
        tries=$((tries + 1))
        # 锁超过 180 秒视为残留（持有者已死亡），强制接管
        if [ $tries -ge 360 ]; then
            echo "[regen_and_build] 锁超时，强制接管" >&2
            rm -rf "$LOCK"
            mkdir "$LOCK" 2>/dev/null && return 0
            return 1
        fi
        sleep 0.5
    done
    return 0
}

if ! acquire_lock; then
    echo "[regen_and_build] 无法获取构建锁" >&2
    exit 1
fi
trap 'rm -rf "$LOCK"' EXIT

echo "[regen_and_build] 生成 UnmanagedAPI.g.cs ..."
python "$REPO/tool/clr-api-generator/generate.py" || exit 1

echo "[regen_and_build] 构建 C# 解决方案 ..."
dotnet build "$REPO/CSharp/LuaSTG/LuaSTG.sln" -v q --nologo || exit 1

echo "[regen_and_build] 完成"
