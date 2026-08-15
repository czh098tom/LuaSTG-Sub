#!/usr/bin/env bash
# 构建 C# 并运行引擎自测（需要引擎已通过 CMake 构建）
# 用法：bash tool/clr-api-generator/run_selftest.sh [Release|Debug]
set -u
REPO=$(cd "$(dirname "$0")/../.." && pwd)
CONFIG="${1:-Release}"
BIN="$REPO/build/amd64/bin"
RESULT="clr_selftest_result.txt"

echo "[selftest] 构建 C# ..."
bash "$REPO/tool/clr-api-generator/regen_and_build.sh" || exit 1

if [ ! -f "$BIN/LuaSTGSub.exe" ]; then
    echo "[selftest] 未找到 $BIN/LuaSTGSub.exe，请先构建引擎" >&2
    exit 2
fi

echo "[selftest] 运行引擎自测 ..."
rm -f "$RESULT"
(cd "$BIN" && ./LuaSTGSub.exe --clr-selftest) &
PID=$!
# 等待最多 120 秒
for i in $(seq 1 240); do
    [ -f "$BIN/$RESULT" ] && break
    sleep 0.5
    kill -0 $PID 2>/dev/null || break
done
kill $PID 2>/dev/null
wait $PID 2>/dev/null

if [ ! -f "$BIN/$RESULT" ]; then
    echo "[selftest] 未产生结果文件（引擎可能未加载 CLR，检查日志）" >&2
    exit 3
fi
cat "$BIN/$RESULT"
if grep -q "^RESULT PASS$" "$BIN/$RESULT"; then
    echo "[selftest] 全部通过"
    exit 0
fi
echo "[selftest] 存在失败项" >&2
exit 1
