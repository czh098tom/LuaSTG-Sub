# CoreCLR 绑定开发规范（代理必读）

本目录包含 LuaSTG CoreCLR 绑定的代码生成与验证工具。移植一个 Lua API 模块到 C# 需要修改以下四类文件，请严格遵循本规范。

## 文件所有权

每个模块对应以下文件（以模块 X 代表 Render/Audio/Input/...）：

| 文件 | 说明 | 所有者 |
|---|---|---|
| `LuaSTG/LuaSTG/CLRBinding/API/X.hpp` | X-macro API 列表（单一事实源） | 该模块代理独占 |
| `LuaSTG/LuaSTG/CLRBinding/CLRX.cpp` | C++ 侧 API 实现（薄封装，调用引擎） | 该模块代理独占 |
| `CSharp/LuaSTG/LuaSTG.Core/X.cs`（或多个文件） | C# 侧包装 API | 该模块代理独占 |
| `APIList.hpp` / `CMakeLists.txt` / `CLRBinding.hpp` / `LuaSTGAPI.cs` 等共享文件 | 已预先注册所有模块 | 禁止修改 |

**禁止修改任何不属于自己模块的文件。** 共享文件（AppFrame、GameObject 等）如确需修改，在最终报告中说明，由主代理统一处理。

## API 定义格式（API/X.hpp）

每行一条 API，参数与返回值只允许生成器支持的类型：

```cpp
DECLARE_CLR_API(返回类型, 函数名, (类型1 参数1, 类型2 参数2))
```

支持的类型（C++ → C#）：
- `void`、`bool`→byte、`int8_t/uint8_t/int16_t/uint16_t/int32_t/uint32_t/int64_t/uint64_t`
- `float`、`double`、`size_t`→nuint、`intptr_t`→nint、`uintptr_t`→nuint
- `const char*`→byte*（UTF-8，调用方保证生存期）、`const char16_t*`→char*
- 指针参数：`const uint32_t*`、`double*` 等 → 对应 C# 指针
- 其余类型在 `generate.py` 的 TYPE_MAP 中扩展（需要时先改生成器）

命名约定：C++/表内函数名用模块前缀 + 驼峰（如 `renderer_setOrtho`、`audio_playSound`）。返回字符串一律 `const char*`（指向引擎拥有的静态或长生存期内存，C# 立即拷贝）。布尔用 `uint8_t`（0/1）。

## C++ 实现约定（CLRX.cpp）

参考 `CLRBinding/CLRGameObject.cpp` 与 `CLRBinding/CLRBinding.cpp`：

```cpp
#include "CLRBinding/CLRBinding.hpp"
#include "AppFrame.h"            // 按需

// 引擎 API 实现
void luastg::CLRBinding::renderer_setOrtho(double l, double r, double b, double t)
{
    LAPP.getRenderer2D()->setOrtho(l, r, b, t);
}
```

- 函数定义必须与 API/X.hpp 中声明的签名完全一致。
- 先阅读对应 Lua 绑定文件（`LuaSTG/LuaSTG/LuaBinding/LW_X.cpp` 或 `modern/X.cpp`），
  把其中 lua_push/lua_to 之外的引擎调用逻辑原样移植。
- 错误处理：Lua 侧 luaL_error 的场景，C# 侧应可感知（返回 0/错误码或由 C# 侧先校验）。
- 字符串参数为 UTF-8 `const char*`，直接传给引擎接口（引擎内部使用 UTF-8）。
- 结构体小值参数（Vector2F/Color4B 等）拆成基本类型传递。

## C# 包装约定（LuaSTG.Core/X.cs）

参考 `GameObjectManager.cs` 与 `LuaSTGAPI.cs`：

```csharp
public static unsafe partial class Renderer  // 或 public class 包装引擎对象
{
    public static void SetOrtho(double l, double r, double b, double t)
        => LuaSTGAPI.api.renderer_setOrtho(l, r, b, t);
}
```

- 方法名用 PascalCase，语义与 Lua 侧函数一一对应，XML 注释标明对应的 Lua API 名。
- 字符串参数用 `using var s = new MarshaledString(name);` 封送；返回字符串用
  `StringMarshal.FromUtf8(ptr)`。
- 引擎对象包装类：持有 `nint _native`，无参构造函数分配引擎对象，`Dispose()`/`Destroy()`
  成员函数销毁，销毁后访问引擎数据抛 `ObjectDisposedException`（参考 GameObjectBase）。
- C# 侧自身维护的状态（如缓存）不得缓存引擎数据（保证与 Lua 侧同步）。

## 验证流程

1. 写完 API/X.hpp 后必须重新生成并构建：
   ```bash
   bash tool/clr-api-generator/regen_and_build.sh
   ```
   （内部有互斥锁，多代理并行安全；报错先看是否是自己模块的文件）
2. C++ 语法检查（不触发 CMake）：
   ```bash
   bash tool/clr-api-generator/syntax_check.sh LuaSTG/LuaSTG/CLRBinding/CLRX.cpp
   ```
3. 两个都通过才算完成。

## 生成器

`generate.py` 解析 `API/*.hpp` 生成 `CSharp/LuaSTG/LuaSTG.Core/Generated/UnmanagedAPI.g.cs`。
该文件禁止手改；C# 侧通过 `LuaSTGAPI.api.<函数名>` 调用引擎。
