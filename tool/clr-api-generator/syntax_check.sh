#!/usr/bin/env bash
# 对 CLRBinding 新增的 C++ 文件做语法检查（不触发 CMake 重新生成）
# 用法：tool/clr-api-generator/syntax_check.sh [file...]
set -u

REPO=$(cd "$(dirname "$0")/../.." && pwd)
VCPROJ="$REPO/build/amd64/LuaSTG/LuaSTG.vcxproj"
VCVARS="C:\\Program Files\\Microsoft Visual Studio\\18\\Community\\VC\\Auxiliary\\Build\\vcvars64.bat"
NETHOST_INC="C:\\Program Files\\dotnet\\packs\\Microsoft.NETCore.App.Host.win-x64\\8.0.30\\runtimes\\win-x64\\native"
# luajit.h 由 luajit 构建时生成，语法检查使用存根
LUAJIT_STUB=$(mktemp -d)
cat > "$LUAJIT_STUB/luajit.h" <<'EOF'
#ifndef LUAJIT_H
#define LUAJIT_H
#define LUAJIT_VERSION "LuaJIT 2.1.0-beta3"
#define LUAJIT_VERSION_NUM 20100
#define LUAJIT_VERSION_SYM luaJIT_version_2_1_0_beta3
#define LUAJIT_HAS_JIT 1
#include "lua.h"
#endif
EOF

# 提取 include 目录和宏定义
INCLUDES=$(grep -o '<AdditionalIncludeDirectories>[^<]*' "$VCPROJ" | head -1 | sed 's/<AdditionalIncludeDirectories>//; s/%(AdditionalIncludeDirectories)//' | tr ';' '\n' | sed 's/[[:space:]]*$//' | grep -v '^$' | sed 's/^/\/I"/; s/$/"/' | tr '\n' ' ')
DEFINES=$(grep -o '<PreprocessorDefinitions>[^<]*' "$VCPROJ" | head -1 | sed 's/<PreprocessorDefinitions>//; s/%(PreprocessorDefinitions)//' | tr ';' '\n' | grep -v '^$' | sed 's/^/\/D"/; s/$/"/' | tr '\n' ' ')

FILES=("$@")
if [ ${#FILES[@]} -eq 0 ]; then
    FILES=(
        "$REPO/LuaSTG/LuaSTG/CLRBinding/CLRHost.cpp"
        "$REPO/LuaSTG/LuaSTG/CLRBinding/CLRBinding.cpp"
        "$REPO/LuaSTG/LuaSTG/CLRBinding/CLRGameObject.cpp"
        "$REPO/LuaSTG/LuaSTG/CLRBinding/CLRAppFrame.cpp"
    )
fi

cat > /tmp/clr_syntax_check.bat <<EOF
@echo off
call "$VCVARS" >nul 2>&1
EOF
for f in "${FILES[@]}"; do
    winpath=$(cygpath -w "$f")
    cat >> /tmp/clr_syntax_check.bat <<EOF
echo ===== checking: $(basename "$f")
cl /Zs /std:c++20 /utf-8 /EHsc /permissive- /W3 /DNOMINMAX /FI"$(cygpath -w "$REPO")/LuaSTG/LuaSTG/SharedHeaders.h" $INCLUDES /I"$NETHOST_INC" /I"$(cygpath -w "$LUAJIT_STUB")" $DEFINES "$winpath"
EOF
done

cmd //c "$(cygpath -w /tmp/clr_syntax_check.bat)"
