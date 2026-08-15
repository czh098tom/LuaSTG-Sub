// LuaSTG CoreCLR 绑定：现代窗口与系统集成（Window/SwapChain/Display/Clipboard/FileSystemWatcher/ShellIntegration）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 对应 Lua 绑定：
//   LuaBinding/modern/Window.cpp（lstg.Window 及三个窗口扩展、lstg.SetSplash/SetTitle）
//   LuaBinding/modern/SwapChain.cpp（lstg.SwapChain）
//   LuaBinding/modern/Display.cpp（lstg.Display）
//   LuaBinding/modern/Clipboard.cpp（lstg.Clipboard）
//   LuaBinding/modern/FileSystemWatcher.cpp（lstg.FileSystemWatcher）
//   LuaBinding/modern/ShellIntegration.cpp（lstg.ShellIntegration）
//   LuaBinding/LW_LuaSTG.cpp（lstg.ChangeVideoMode 等兼容显示 API）
//
// 约定：
// - 引擎对象句柄（IWindow*/ISwapChain*/IDisplay*/IMessageQueueBasedFileSystemWatcher*）用 uintptr_t 传递，0 表示空
// - 返回的句柄所有权：display_getPrimary/display_getNearestFromWindow/display_getAllByIndex 返回的句柄
//   归 C# 侧所有（已增加引用计数），需调用 display_release 释放；window_getMain/swapchain_getMain/
//   window_queryInterface 返回的是引擎持有的主对象借用句柄，C# 侧不得释放
// - 返回字符串使用线程局部缓冲（C# 侧立即拷贝），空指针/空串表示失败或空

// ===== Window（lstg.Window，主窗口单例）=====

// 获取主窗口句柄（借用，不增加引用计数）
DECLARE_CLR_API(uintptr_t, window_getMain, ())
DECLARE_CLR_API(void, window_setTitle, (const char* text))
DECLARE_CLR_API(void, window_getClientAreaSize, (uint32_t* width, uint32_t* height))
// 返回 FrameStyle 枚举值（0=borderless 1=fixed 2=normal，对应 core::WindowFrameStyle）
DECLARE_CLR_API(int32_t, window_getStyle, ())
DECLARE_CLR_API(float, window_getDisplayScale, ())
// style 为 FrameStyle 枚举值；display 为目标显示器句柄，0 表示使用当前显示器
DECLARE_CLR_API(void, window_setWindowed, (uint32_t width, uint32_t height, int32_t style, uintptr_t display))
// display 为目标显示器句柄，0 表示使用当前显示器
DECLARE_CLR_API(void, window_setFullscreen, (uintptr_t display))
DECLARE_CLR_API(uint8_t, window_getCursorVisibility, ())
DECLARE_CLR_API(void, window_setCursorVisibility, (uint8_t visible))
// queryInterface 风格扩展查询（对应 Lua 侧 Window:queryInterface）：
// name 为 Lua 侧扩展类名（"lstg.Window.InputMethodExtension"/"lstg.Window.TextInputExtension"/
// "lstg.Window.Windows11Extension"），扩展存在时返回主窗口句柄（借用），不存在时返回 0
DECLARE_CLR_API(uintptr_t, window_queryInterface, (const char* name))

// ===== Window.InputMethodExtension（lstg.Window.InputMethodExtension）=====

DECLARE_CLR_API(uint8_t, window_ime_isEnabled, ())
DECLARE_CLR_API(void, window_ime_setEnabled, (uint8_t enabled))
DECLARE_CLR_API(void, window_ime_setPosition, (int32_t x, int32_t y))

// ===== Window.TextInputExtension（lstg.Window.TextInputExtension）=====

DECLARE_CLR_API(uint8_t, window_textInput_isEnabled, ())
DECLARE_CLR_API(void, window_textInput_setEnabled, (uint8_t enabled))
DECLARE_CLR_API(const char*, window_textInput_getBuffer, ())
DECLARE_CLR_API(void, window_textInput_clear, ())
DECLARE_CLR_API(uint32_t, window_textInput_getCursorPosition, ())
DECLARE_CLR_API(void, window_textInput_setCursorPosition, (uint32_t position))
DECLARE_CLR_API(void, window_textInput_addCursorPosition, (int32_t offset))
// 在 code_point_index 处插入 UTF-8 文本
DECLARE_CLR_API(void, window_textInput_insert, (uint32_t index, const char* text))
DECLARE_CLR_API(void, window_textInput_remove, (uint32_t index, uint32_t count))
DECLARE_CLR_API(void, window_textInput_backspace, (uint32_t count))

// ===== Window.Windows11Extension（lstg.Window.Windows11Extension，仅 Windows 11 可用）=====

DECLARE_CLR_API(void, window_win11_setWindowCornerPreference, (uint8_t allow))
DECLARE_CLR_API(void, window_win11_setTitleBarAutoHidePreference, (uint8_t allow))

// ===== SwapChain（lstg.SwapChain，主交换链单例）=====

// 获取主交换链句柄（借用，不增加引用计数）
DECLARE_CLR_API(uintptr_t, swapchain_getMain, ())
DECLARE_CLR_API(uint8_t, swapchain_setWindowed, (uint32_t width, uint32_t height))
DECLARE_CLR_API(void, swapchain_getSize, (uint32_t* width, uint32_t* height))
DECLARE_CLR_API(uint8_t, swapchain_setSize, (uint32_t width, uint32_t height))
DECLARE_CLR_API(uint8_t, swapchain_getVSyncPreference, ())
DECLARE_CLR_API(void, swapchain_setVSyncPreference, (uint8_t enable))
// 返回 ScalingMode 枚举值（0=stretch 1=aspect_ratio，对应 core::SwapChainScalingMode）
DECLARE_CLR_API(int32_t, swapchain_getScalingMode, ())
DECLARE_CLR_API(void, swapchain_setScalingMode, (int32_t mode))

// ===== Display（lstg.Display，实例包装）=====
// 句柄所有权见文件头部约定：Get* 工厂返回的句柄归 C# 所有，用 display_release 释放

// 获取主显示器（失败返回 0）
DECLARE_CLR_API(uintptr_t, display_getPrimary, ())
// 获取距离主窗口最近的显示器（失败返回 0；Lua 侧此功能未实现，这里直接使用引擎接口）
DECLARE_CLR_API(uintptr_t, display_getNearestFromWindow, ())
// 枚举全部显示器：getAllCount 刷新内部缓存并返回数量，getAllByIndex 取缓存中第 index 个
// （返回归 C# 所有的句柄，index 越界返回 0），getAllClear 释放内部缓存
DECLARE_CLR_API(uint32_t, display_getAllCount, ())
DECLARE_CLR_API(uintptr_t, display_getAllByIndex, (uint32_t index))
DECLARE_CLR_API(void, display_getAllClear, ())
DECLARE_CLR_API(void, display_release, (uintptr_t display))
DECLARE_CLR_API(const char*, display_getFriendlyName, (uintptr_t display))
DECLARE_CLR_API(void, display_getSize, (uintptr_t display, uint32_t* width, uint32_t* height))
DECLARE_CLR_API(void, display_getPosition, (uintptr_t display, int32_t* x, int32_t* y))
DECLARE_CLR_API(void, display_getRect, (uintptr_t display, int32_t* left, int32_t* top, int32_t* right, int32_t* bottom))
DECLARE_CLR_API(void, display_getWorkAreaSize, (uintptr_t display, uint32_t* width, uint32_t* height))
DECLARE_CLR_API(void, display_getWorkAreaPosition, (uintptr_t display, int32_t* x, int32_t* y))
DECLARE_CLR_API(void, display_getWorkAreaRect, (uintptr_t display, int32_t* left, int32_t* top, int32_t* right, int32_t* bottom))
DECLARE_CLR_API(uint8_t, display_isPrimary, (uintptr_t display))
DECLARE_CLR_API(float, display_getDisplayScale, (uintptr_t display))

// ===== Clipboard（lstg.Clipboard）=====

DECLARE_CLR_API(uint8_t, clipboard_hasText, ())
// 剪贴板无文本（失败）时返回空指针
DECLARE_CLR_API(const char*, clipboard_getText, ())
DECLARE_CLR_API(uint8_t, clipboard_setText, (const char* text))

// ===== FileSystemWatcher（lstg.FileSystemWatcher，实例包装）=====

// 创建文件系统监视器（失败返回 0），返回句柄归 C# 所有，用 fswatcher_close 释放
DECLARE_CLR_API(uintptr_t, fswatcher_create, (const char* path))
DECLARE_CLR_API(void, fswatcher_close, (uintptr_t watcher))
// 排空并缓存当前积压的全部事件（对应 Lua 侧 read/next 的循环调用形式），返回事件数量；
// 随后用 getEventAction/getEventFileName 按 index 读取（count+byIndex 拆分），
// 缓存绑定到 (watcher, 调用线程)，下一次 read 或 close 时失效
DECLARE_CLR_API(uint32_t, fswatcher_read, (uintptr_t watcher))
DECLARE_CLR_API(int32_t, fswatcher_getEventAction, (uintptr_t watcher, uint32_t index))
DECLARE_CLR_API(const char*, fswatcher_getEventFileName, (uintptr_t watcher, uint32_t index))

// ===== ShellIntegration（lstg.ShellIntegration）=====

DECLARE_CLR_API(uint8_t, shell_openFile, (const char* path))
DECLARE_CLR_API(uint8_t, shell_openDirectory, (const char* path))
DECLARE_CLR_API(uint8_t, shell_openUrl, (const char* url))

// ===== 兼容显示 API（LW_LuaSTG.cpp：lstg.ChangeVideoMode 等）=====

// 对应 lstg.ChangeVideoMode：windowed=1 时 SetDisplayModeWindow，否则 SetDisplayModeExclusiveFullscreen
DECLARE_CLR_API(uint8_t, video_changeVideoMode, (uint32_t width, uint32_t height, uint8_t windowed, uint8_t vsync))
// 对应 lstg.EnumResolutions（引擎固定返回 5 个 4:3 分辨率，刷新率 60/1）：
// count+byIndex 拆分，index 越界时 byIndex 返回 0
DECLARE_CLR_API(uint32_t, video_enumResolutionCount, ())
DECLARE_CLR_API(uint8_t, video_enumResolutionByIndex, (uint32_t index, uint32_t* width, uint32_t* height, uint32_t* numerator, uint32_t* denominator))
// 对应 lstg.EnumGPUs（渲染设备不可用时返回 0）
DECLARE_CLR_API(uint32_t, video_getGpuCount, ())
// 对应 lstg.EnumGPUs 的逐项名称（渲染设备不可用或越界时返回空串）
DECLARE_CLR_API(const char*, video_getGpuNameByIndex, (uint32_t index))
// 对应 lstg.GetCurrentGpuName（渲染设备不可用时返回空指针）
DECLARE_CLR_API(const char*, video_getCurrentGpuName, ())
// 对应 lstg.ChangeGPU：0=成功 1=渲染设备不可用 2=重建失败
DECLARE_CLR_API(uint8_t, video_changeGPU, (const char* name))
// 对应 lstg.SetSwapChainScalingMode（mode 为 ScalingMode 枚举值）
DECLARE_CLR_API(void, video_setSwapChainScalingMode, (int32_t mode))
