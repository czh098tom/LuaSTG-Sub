#!/usr/bin/env bash
# 重新生成 C# UnmanagedAPI 声明并构建 C# 解决方案（带互斥锁，供并行代理使用）
# 用法：bash tool/clr-api-generator/regen_and_build.sh
set -u
REPO=$(cd "$(dirname "$0")/../.." && pwd)
LOCK="/tmp/luastg_clr_build.lock"

exec 9>"$LOCK"
flock 9

echo "[regen_and_build] 生成 UnmanagedAPI.g.cs ..."
python "$REPO/tool/clr-api-generator/generate.py" || exit 1

echo "[regen_and_build] 构建 C# 解决方案 ..."
dotnet build "$REPO/CSharp/LuaSTG/LuaSTG.sln" -v q --nologo || exit 1

echo "[regen_and_build] 完成"
