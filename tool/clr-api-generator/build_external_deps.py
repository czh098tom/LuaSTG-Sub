#!/usr/bin/env python3
"""手动执行外部依赖项目的 configure/build/install 步骤。

绕过 MSBuild 自定义构建批处理在长命令下 "找不到批处理标签 - VCEnd" 的 cmd 缺陷
（标签越过 8KB 块边界）。按依赖顺序执行，等价于 *_build.vcxproj 中的规则。
"""
import re
import pathlib
import html
import shlex
import subprocess
import sys

REPO = pathlib.Path(r"E:\luastg\LuaSTG-Sub-Sharp")
BUILD = REPO / "build" / "amd64"

# 依赖顺序：zlib-ng 最先（libpng/freetype 需要 zlib.h）
ORDER = [
    "zlib_ng_build",
    "libpng_build",
    "freetype_build",
    "libjpeg_build",
    "libwebp_build",
    "libogg_build",
    "libvorbis_build",
    "libflac_build",
    "minizip_ng_build",
    "DirectXTex_build",
    "gtest_build",
]


def extract_command(project: str) -> list[str] | None:
    p = BUILD / f"{project}.vcxproj"
    text = p.read_text(encoding="utf-8")
    m = re.search(r'<Command Condition="[^"]*Release\|x64\'?">(.*?)</Command>', text, re.DOTALL)
    if not m:
        return None
    cmd = html.unescape(m.group(1))
    lines = []
    for line in cmd.splitlines():
        s = line.strip()
        if not s or s.startswith(("setlocal", "endlocal", "if %errorlevel%", "goto", ":c", "exit")):
            continue
        if s in ("E:", "cd E:\\luastg\\LuaSTG-Sub-Sharp\\build\\amd64"):
            continue
        lines.append(s)
    return lines


def main() -> int:
    only = sys.argv[1:] if len(sys.argv) > 1 else ORDER
    failures = []
    for project in only:
        lines = extract_command(project)
        if lines is None:
            print(f"[skip] {project}: 无 Release 命令")
            continue
        print(f"==== {project} ({len(lines)} 步) ====", flush=True)
        for line in lines:
            if line.startswith("echo "):
                continue
            m = re.match(r'"([^"]+)"\s+(.*)', line)
            if not m:
                print(f"  [warn] 无法解析: {line[:80]}")
                continue
            exe, args = m.group(1), m.group(2)
            # 用 shlex 解析以保留带引号的参数（如 -G "Visual Studio 18 2026"）
            argv = [exe] + shlex.split(args, posix=False)
            argv = [a.strip('"') if a.startswith('"') and a.endswith('"') else a for a in argv]
            print(f"  $ {pathlib.Path(exe).name} (共 {len(argv) - 1} 个参数)", flush=True)
            r = subprocess.run(argv, capture_output=True, cwd=BUILD)
            if r.returncode != 0:
                print(f"  FAILED ({r.returncode}):", flush=True)
                print("   ", r.stdout.decode("utf-8", errors="replace")[-600:], flush=True)
                print("   ", r.stderr.decode("utf-8", errors="replace")[-600:], flush=True)
                failures.append(project)
                break
    print("====")
    print("失败项目:", failures if failures else "无")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
