# LuaSTG CoreCLR API 覆盖说明

C# 侧 API（`LuaSTG.Core`）按 Lua 绑定逐模块移植，引擎 API 总数 **554 个**（见 `Generated/UnmanagedAPI.g.cs`）。

## 已移植模块（对应 Lua API）

| C# 类型 | Lua 侧 | 说明 |
|---|---|---|
| `LuaSTGAPI` | lstg 核心函数 | 版本/窗口/FPS/日志/LoadTextFile 等 |
| `GameObjectBase`/`GameObjectManager` | lstg 对象系统 | 内存覆写直读引擎数据（与 Lua 天然同步）；全部属性/批量操作/链表迭代/超级暂停 |
| `Renderer`/`Render`/`PostEffectShader` | lstg.Renderer / lstg.* 渲染 | 现代 + 兼容两套；后效三种形式 |
| `ResourceManager`/`ResourceWrappers` | lstg.ResourceManager / Resource.cpp | 全部 Load*/Set*State/EnumRes + 现代类型 API |
| `Audio` | lstg.Audio | 设备/SE/BGM 全量 |
| `Input`/`XInput`/`DirectInput` | lstg.Input / xinput / dinput | KeyCode 171 项 |
| `Platform`/`FileManager`/`Archive`/`FontRenderer` | lstg.Platform/FileManager/Archive/FontRenderer | 含搜索路径/压缩包/字体渲染 |
| `Color`/`StopWatch`/`BentLaserData`/`ParticleSystemData` | lstg.Color/StopWatch/BentLaserData/ParticleSystemData | Color 为 struct；其余为引擎对象包装 |
| `RenderTarget`/`Mesh`/`MeshRenderer`/`Texture2D`/`VideoDecoder`/`Sprite`/`SpriteRenderer`×3/`Vector2/3/4` | modern/* | Graphics 目录 |
| `Window`(含扩展)/`SwapChain`/`Display`/`Clipboard`/`FileSystemWatcher`/`ShellIntegration` | modern/* | 含显示模式/GPU 兼容 API |
| `DirectWrite.*` | DirectWrite | COM 底层完整移植 |
| `Rng.*`（26 种） | lstg.Rand / random | 纯 C# 逐位对齐引擎（含 MSVC STL 分布适配器） |
| `HttpRequest`/`HttpResponseMessage` | http.* | 引擎 WinHTTP 对象包装 |

## 有意不移植的 API（与引擎配置/语言生态对应）

| Lua API | 原因 |
|---|---|
| `lstg.Platform.Execute` | 引擎默认未启用 `LUASTG_ENABLE_EXECUTE_API`（Config.h） |
| `lstg.ExtractRes` | 引擎默认未启用 `USING_ENCRYPTION` |
| `steam.*` | 引擎默认未启用 `LUASTG_STEAM_API_ENABLE`（启用后为第三方 Steamworks 绑定） |
| `cjson`/`lfs`/`socket` | Lua 生态库，C# 使用 .NET BCL（System.Text.Json / System.IO / Sockets） |
| `lstg.DoFile` | 执行 Lua 脚本属语言侧功能；C# 侧由程序集自身承载 |
| `os.execute`/`io.popen` | 同 Execute，引擎已默认禁用 |
| Lua 侧 `removed.lua` 中的空操作 API | Lua 侧本身为 no-op |

## 对象生命周期约定

- **游戏对象（GameObject）完全跟随引擎生命周期**（与 Lua 侧行为一致）：
  `Delete()`/`Kill()` 仅把对象标记为待回收（status = Dead/Killed）并立即触发
  `OnDestroy` 回调（回调内引擎数据仍可读写）；同一帧内引擎数据照常访问，
  引擎回调（OnFrame/OnRender/OnColli）照常分发；真正回收发生在帧末
  `GameObjectManager.AfterFrame()`，引擎归还对象池并解除 C# 包装；
  **回收之后再访问引擎数据抛 `ObjectDisposedException`**（`IsValid` 为 false）。
- **引擎对象**（BentLaserData/StopWatch/PostEffectShader/图形对象/Archive/Http 等）：
  C# 包装持有原生句柄；能用无参构造的用无参构造（StopWatch/BentLaserData），
  需要参数的用工厂（`RenderTarget.Create` 等）；`Dispose()`/`Destroy()` 立即销毁引擎对象；
  **销毁后访问一律抛 `ObjectDisposedException`**。
- **值类型**（Color/Vector/TextMetrics/RNG）：无生命周期。
- **池内资源**（纹理/精灵等）：以名字标识、引擎资源池持有；包装类 `Destroy()` 即从池移除。
- **语言侧自身数据不互通**：C# 包装类的托管字段与 Lua 对象表的自定义字段互不可见；
  引擎数据（对象池属性、资源状态等）两侧实时同步。

## 性能设计

- 引擎数据以内存覆写（struct overlay）直读，GameObject 属性零函数调用；
  布局由引擎在启动时校验（`CLRGameObjectLayoutInfo`），配置变更即报错。
- 所有引擎调用经由 `delegate* unmanaged[Cdecl]` 函数指针表（`LuaSTGAPI.api`），
  无 P/Invoke 封送层；字符串 UTF-8 显式封送；批量数据以指针 + 计数单次传递。
