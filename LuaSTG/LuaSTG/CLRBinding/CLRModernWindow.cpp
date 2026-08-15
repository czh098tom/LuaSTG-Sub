// LuaSTG CoreCLR 绑定：现代窗口与系统集成模块实现
// 对应 Lua 绑定：LuaBinding/modern/Window.cpp、SwapChain.cpp、Display.cpp、Clipboard.cpp、
//   FileSystemWatcher.cpp、ShellIntegration.cpp、LuaBinding/LW_LuaSTG.cpp（兼容显示 API）
// 移植时保留引擎调用逻辑，Lua 栈操作（luaL_check/luaL_error）改为空指针/错误码返回，
// 由 C# 侧包装（LuaSTG.Core/Window.cs 等）抛出异常，保持与 Lua 侧报错行为一致。

#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"

#include "core/SmartReference.hpp"
#include "core/Clipboard.hpp"
#include "core/FileSystemWatcher.hpp"
#include "core/ShellIntegration.hpp"
#include "windows/WindowsVersion.hpp"

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

using namespace luastg;

namespace
{
	// 返回给 C# 的字符串使用线程局部缓冲（C# 侧在调用返回后立即拷贝）
	thread_local std::string g_window_text_buffer;
	thread_local std::string g_display_friendly_name;
	thread_local std::string g_clipboard_text;
	thread_local std::string g_fswatcher_file_name;
	thread_local std::string g_gpu_name_by_index;
	thread_local std::string g_current_gpu_name;

	// Display.getAll 的缓存（getAllCount 刷新，getAllByIndex 借用，getAllClear 释放）
	thread_local std::vector<core::IDisplay*> g_display_list;

	// FileSystemWatcher.read 的事件缓存（对应 Lua 侧 read/next 的逐事件读取，C# 侧按 index 读取）
	thread_local std::vector<core::FileNotifyInformation> g_fswatcher_events;
	thread_local uintptr_t g_fswatcher_events_owner = 0;

	[[nodiscard]] core::IWindow* asWindow() noexcept {
		return LAPP.getWindow();
	}
	[[nodiscard]] core::ISwapChain* asSwapChain() noexcept {
		return LAPP.getSwapChain();
	}
	[[nodiscard]] core::IDisplay* asDisplay(uintptr_t const handle) noexcept {
		return reinterpret_cast<core::IDisplay*>(static_cast<uintptr_t>(handle));
	}
	[[nodiscard]] core::IMessageQueueBasedFileSystemWatcher* asWatcher(uintptr_t const handle) noexcept {
		return reinterpret_cast<core::IMessageQueueBasedFileSystemWatcher*>(static_cast<uintptr_t>(handle));
	}
	[[nodiscard]] core::IGraphicsDevice* asGraphicsDevice() noexcept {
		return LAPP.getGraphicsDevice();
	}

	void releaseDisplayList() noexcept {
		for (auto* const display : g_display_list) {
			if (display != nullptr) {
				display->release();
			}
		}
		g_display_list.clear();
	}
}

namespace luastg
{
	// ============ Window ============

	uintptr_t CLRBinding::window_getMain()
	{
		// 借用句柄：主窗口由引擎持有，C# 侧不得释放
		return reinterpret_cast<uintptr_t>(asWindow());
	}
	void CLRBinding::window_setTitle(const char* const text)
	{
		auto* const window = asWindow();
		if (window == nullptr || text == nullptr)
			return;
		window->setTitleText(std::string_view(text));
	}
	void CLRBinding::window_getClientAreaSize(uint32_t* const width, uint32_t* const height)
	{
		auto* const window = asWindow();
		auto const size = window ? window->_getCurrentSize() : core::Vector2U();
		if (width != nullptr)
			*width = size.x;
		if (height != nullptr)
			*height = size.y;
	}
	int32_t CLRBinding::window_getStyle()
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return static_cast<int32_t>(core::WindowFrameStyle::Normal);
		return static_cast<int32_t>(window->getFrameStyle());
	}
	float CLRBinding::window_getDisplayScale()
	{
		auto* const window = asWindow();
		return window ? window->getDPIScaling() : 1.0f;
	}
	void CLRBinding::window_setWindowed(uint32_t const width, uint32_t const height, int32_t const style, uintptr_t const display)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->setWindowMode(
			core::Vector2U(width, height),
			static_cast<core::WindowFrameStyle>(style),
			display != 0 ? asDisplay(display) : nullptr
		);
	}
	void CLRBinding::window_setFullscreen(uintptr_t const display)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->setFullScreenMode(display != 0 ? asDisplay(display) : nullptr);
	}
	uint8_t CLRBinding::window_getCursorVisibility()
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return 0;
		return window->getCursor() != core::WindowCursor::None ? 1 : 0;
	}
	void CLRBinding::window_setCursorVisibility(uint8_t const visible)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->setCursor(visible ? core::WindowCursor::Arrow : core::WindowCursor::None);
	}
	uintptr_t CLRBinding::window_queryInterface(const char* const name)
	{
		// 对应 Lua 侧 Window:queryInterface：按扩展类名查询，Windows11Extension 仅在 Win11 上可用
		auto* const window = asWindow();
		if (window == nullptr || name == nullptr)
			return 0;
		std::string_view const n(name);
		if (n == "lstg.Window.InputMethodExtension")
			return reinterpret_cast<uintptr_t>(window);
		if (n == "lstg.Window.TextInputExtension")
			return reinterpret_cast<uintptr_t>(window);
		if (n == "lstg.Window.Windows11Extension") {
			if (Platform::WindowsVersion::Is11())
				return reinterpret_cast<uintptr_t>(window);
		}
		return 0;
	}

	// ============ Window.InputMethodExtension ============

	uint8_t CLRBinding::window_ime_isEnabled()
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return 0;
		return window->getIMEState() ? 1 : 0;
	}
	void CLRBinding::window_ime_setEnabled(uint8_t const enabled)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->setIMEState(enabled != 0);
	}
	void CLRBinding::window_ime_setPosition(int32_t const x, int32_t const y)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->setInputMethodPosition(core::Vector2I(x, y));
	}

	// ============ Window.TextInputExtension ============

	uint8_t CLRBinding::window_textInput_isEnabled()
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return 0;
		return window->textInput_isEnabled() ? 1 : 0;
	}
	void CLRBinding::window_textInput_setEnabled(uint8_t const enabled)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->textInput_setEnabled(enabled != 0);
	}
	const char* CLRBinding::window_textInput_getBuffer()
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return "";
		auto const buffer = window->textInput_getBuffer();
		g_window_text_buffer.assign(buffer.data(), buffer.size());
		return g_window_text_buffer.c_str();
	}
	void CLRBinding::window_textInput_clear()
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->textInput_clearBuffer();
	}
	uint32_t CLRBinding::window_textInput_getCursorPosition()
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return 0;
		return window->textInput_getCursorPosition();
	}
	void CLRBinding::window_textInput_setCursorPosition(uint32_t const position)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->textInput_setCursorPosition(position);
	}
	void CLRBinding::window_textInput_addCursorPosition(int32_t const offset)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->textInput_addCursorPosition(offset);
	}
	void CLRBinding::window_textInput_insert(uint32_t const index, const char* const text)
	{
		auto* const window = asWindow();
		if (window == nullptr || text == nullptr)
			return;
		window->textInput_insertBufferRange(index, std::string_view(text));
	}
	void CLRBinding::window_textInput_remove(uint32_t const index, uint32_t const count)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->textInput_removeBufferRange(index, count);
	}
	void CLRBinding::window_textInput_backspace(uint32_t const count)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->textInput_backspace(count);
	}

	// ============ Window.Windows11Extension ============

	void CLRBinding::window_win11_setWindowCornerPreference(uint8_t const allow)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->setWindowCornerPreference(allow != 0);
	}
	void CLRBinding::window_win11_setTitleBarAutoHidePreference(uint8_t const allow)
	{
		auto* const window = asWindow();
		if (window == nullptr)
			return;
		window->setTitleBarAutoHidePreference(allow != 0);
	}

	// ============ SwapChain ============

	uintptr_t CLRBinding::swapchain_getMain()
	{
		// 借用句柄：主交换链由引擎持有，C# 侧不得释放
		return reinterpret_cast<uintptr_t>(asSwapChain());
	}
	uint8_t CLRBinding::swapchain_setWindowed(uint32_t const width, uint32_t const height)
	{
		auto* const swapchain = asSwapChain();
		if (swapchain == nullptr)
			return 0;
		return swapchain->setWindowMode(core::Vector2U(width, height)) ? 1 : 0;
	}
	void CLRBinding::swapchain_getSize(uint32_t* const width, uint32_t* const height)
	{
		auto* const swapchain = asSwapChain();
		auto const size = swapchain ? swapchain->getCanvasSize() : core::Vector2U();
		if (width != nullptr)
			*width = size.x;
		if (height != nullptr)
			*height = size.y;
	}
	uint8_t CLRBinding::swapchain_setSize(uint32_t const width, uint32_t const height)
	{
		auto* const swapchain = asSwapChain();
		if (swapchain == nullptr)
			return 0;
		return swapchain->setCanvasSize(core::Vector2U(width, height)) ? 1 : 0;
	}
	uint8_t CLRBinding::swapchain_getVSyncPreference()
	{
		auto* const swapchain = asSwapChain();
		if (swapchain == nullptr)
			return 0;
		return swapchain->getVSync() ? 1 : 0;
	}
	void CLRBinding::swapchain_setVSyncPreference(uint8_t const enable)
	{
		auto* const swapchain = asSwapChain();
		if (swapchain == nullptr)
			return;
		swapchain->setVSync(enable != 0);
	}
	int32_t CLRBinding::swapchain_getScalingMode()
	{
		auto* const swapchain = asSwapChain();
		if (swapchain == nullptr)
			return static_cast<int32_t>(core::SwapChainScalingMode::stretch);
		return static_cast<int32_t>(swapchain->getScalingMode());
	}
	void CLRBinding::swapchain_setScalingMode(int32_t const mode)
	{
		auto* const swapchain = asSwapChain();
		if (swapchain == nullptr)
			return;
		swapchain->setScalingMode(static_cast<core::SwapChainScalingMode>(mode));
	}

	// ============ Display ============

	uintptr_t CLRBinding::display_getPrimary()
	{
		// 返回句柄归 C# 所有（引用计数 1），由 display_release 释放
		core::SmartReference<core::IDisplay> display;
		if (!core::IDisplay::getPrimary(display.put()))
			return 0;
		return reinterpret_cast<uintptr_t>(display.detach());
	}
	uintptr_t CLRBinding::display_getNearestFromWindow()
	{
		// Lua 侧此功能未实现（直接 luaL_error），这里直接使用引擎接口按主窗口查询
		core::SmartReference<core::IDisplay> display;
		if (!core::IDisplay::getNearestFromWindow(asWindow(), display.put()))
			return 0;
		return reinterpret_cast<uintptr_t>(display.detach());
	}
	uint32_t CLRBinding::display_getAllCount()
	{
		// 刷新缓存（对应 Lua 侧 getAll 的两段式调用：先取数量再取列表）
		releaseDisplayList();
		size_t count = 0;
		if (!core::IDisplay::getAll(&count, nullptr))
			return 0;
		g_display_list.resize(count, nullptr);
		if (count != 0) {
			if (!core::IDisplay::getAll(&count, g_display_list.data()))
				return 0;
		}
		return static_cast<uint32_t>(count);
	}
	uintptr_t CLRBinding::display_getAllByIndex(uint32_t const index)
	{
		if (index >= g_display_list.size())
			return 0;
		auto* const display = g_display_list[index];
		if (display == nullptr)
			return 0;
		// 额外增加一次引用后交给 C# 侧持有
		display->retain();
		return reinterpret_cast<uintptr_t>(display);
	}
	void CLRBinding::display_getAllClear()
	{
		releaseDisplayList();
	}
	void CLRBinding::display_release(uintptr_t const display)
	{
		if (auto* const p = asDisplay(display)) {
			p->release();
		}
	}
	const char* CLRBinding::display_getFriendlyName(uintptr_t const display)
	{
		auto* const p = asDisplay(display);
		if (p == nullptr)
			return "";
		core::SmartReference<core::IImmutableString> friendly_name;
		p->getFriendlyName(friendly_name.put());
		if (!friendly_name)
			return "";
		auto const view = friendly_name->view();
		g_display_friendly_name.assign(view.data(), view.size());
		return g_display_friendly_name.c_str();
	}
	void CLRBinding::display_getSize(uintptr_t const display, uint32_t* const width, uint32_t* const height)
	{
		auto* const p = asDisplay(display);
		auto const size = p ? p->getSize() : core::Vector2U();
		if (width != nullptr)
			*width = size.x;
		if (height != nullptr)
			*height = size.y;
	}
	void CLRBinding::display_getPosition(uintptr_t const display, int32_t* const x, int32_t* const y)
	{
		auto* const p = asDisplay(display);
		auto const position = p ? p->getPosition() : core::Vector2I();
		if (x != nullptr)
			*x = position.x;
		if (y != nullptr)
			*y = position.y;
	}
	void CLRBinding::display_getRect(uintptr_t const display, int32_t* const left, int32_t* const top, int32_t* const right, int32_t* const bottom)
	{
		auto* const p = asDisplay(display);
		core::RectI const rect = p ? p->getRect() : core::RectI();
		if (left != nullptr)
			*left = rect.a.x;
		if (top != nullptr)
			*top = rect.a.y;
		if (right != nullptr)
			*right = rect.b.x;
		if (bottom != nullptr)
			*bottom = rect.b.y;
	}
	void CLRBinding::display_getWorkAreaSize(uintptr_t const display, uint32_t* const width, uint32_t* const height)
	{
		auto* const p = asDisplay(display);
		auto const size = p ? p->getWorkAreaSize() : core::Vector2U();
		if (width != nullptr)
			*width = size.x;
		if (height != nullptr)
			*height = size.y;
	}
	void CLRBinding::display_getWorkAreaPosition(uintptr_t const display, int32_t* const x, int32_t* const y)
	{
		auto* const p = asDisplay(display);
		auto const position = p ? p->getWorkAreaPosition() : core::Vector2I();
		if (x != nullptr)
			*x = position.x;
		if (y != nullptr)
			*y = position.y;
	}
	void CLRBinding::display_getWorkAreaRect(uintptr_t const display, int32_t* const left, int32_t* const top, int32_t* const right, int32_t* const bottom)
	{
		auto* const p = asDisplay(display);
		core::RectI const rect = p ? p->getWorkAreaRect() : core::RectI();
		if (left != nullptr)
			*left = rect.a.x;
		if (top != nullptr)
			*top = rect.a.y;
		if (right != nullptr)
			*right = rect.b.x;
		if (bottom != nullptr)
			*bottom = rect.b.y;
	}
	uint8_t CLRBinding::display_isPrimary(uintptr_t const display)
	{
		auto* const p = asDisplay(display);
		if (p == nullptr)
			return 0;
		return p->isPrimary() ? 1 : 0;
	}
	float CLRBinding::display_getDisplayScale(uintptr_t const display)
	{
		auto* const p = asDisplay(display);
		return p ? p->getDisplayScale() : 1.0f;
	}

	// ============ Clipboard ============

	uint8_t CLRBinding::clipboard_hasText()
	{
		return core::Clipboard::hasText() ? 1 : 0;
	}
	const char* CLRBinding::clipboard_getText()
	{
		// Lua 侧失败返回 (nil, nil)，这里用空指针表达失败
		if (!core::Clipboard::getText(g_clipboard_text))
			return nullptr;
		return g_clipboard_text.c_str();
	}
	uint8_t CLRBinding::clipboard_setText(const char* const text)
	{
		if (text == nullptr)
			return 0;
		return core::Clipboard::setText(std::string_view(text)) ? 1 : 0;
	}

	// ============ FileSystemWatcher ============

	uintptr_t CLRBinding::fswatcher_create(const char* const path)
	{
		if (path == nullptr)
			return 0;
		// 返回句柄归 C# 所有（引用计数 1），由 fswatcher_close 释放
		core::SmartReference<core::IMessageQueueBasedFileSystemWatcher> object;
		if (!core::IMessageQueueBasedFileSystemWatcher::create(std::string_view(path), object.put()))
			return 0;
		return reinterpret_cast<uintptr_t>(object.detach());
	}
	void CLRBinding::fswatcher_close(uintptr_t const watcher)
	{
		if (g_fswatcher_events_owner == watcher) {
			g_fswatcher_events.clear();
			g_fswatcher_events_owner = 0;
		}
		if (auto* const p = asWatcher(watcher)) {
			p->release();
		}
	}
	uint32_t CLRBinding::fswatcher_read(uintptr_t const watcher)
	{
		// 排空消息队列（对应 Lua 侧 read/next 的逐事件读取形式），事件缓存到线程局部列表
		auto* const p = asWatcher(watcher);
		if (p == nullptr)
			return 0;
		g_fswatcher_events_owner = watcher;
		g_fswatcher_events.clear();
		core::FileNotifyInformation info;
		while (p->next(&info)) {
			g_fswatcher_events.push_back(std::move(info));
		}
		return static_cast<uint32_t>(g_fswatcher_events.size());
	}
	int32_t CLRBinding::fswatcher_getEventAction(uintptr_t const watcher, uint32_t const index)
	{
		if (g_fswatcher_events_owner != watcher || index >= g_fswatcher_events.size())
			return static_cast<int32_t>(core::FileAction::unknown);
		return static_cast<int32_t>(g_fswatcher_events[index].action);
	}
	const char* CLRBinding::fswatcher_getEventFileName(uintptr_t const watcher, uint32_t const index)
	{
		if (g_fswatcher_events_owner != watcher || index >= g_fswatcher_events.size())
			return "";
		auto const* const file_name = g_fswatcher_events[index].file_name;
		if (file_name == nullptr)
			return "";
		g_fswatcher_file_name.assign(file_name->view());
		return g_fswatcher_file_name.c_str();
	}

	// ============ ShellIntegration ============

	uint8_t CLRBinding::shell_openFile(const char* const path)
	{
		if (path == nullptr)
			return 0;
		return core::ShellIntegration::openFile(std::string_view(path)) ? 1 : 0;
	}
	uint8_t CLRBinding::shell_openDirectory(const char* const path)
	{
		if (path == nullptr)
			return 0;
		return core::ShellIntegration::openDirectory(std::string_view(path)) ? 1 : 0;
	}
	uint8_t CLRBinding::shell_openUrl(const char* const url)
	{
		if (url == nullptr)
			return 0;
		return core::ShellIntegration::openUrl(std::string_view(url)) ? 1 : 0;
	}

	// ============ 兼容显示 API ============

	uint8_t CLRBinding::video_changeVideoMode(uint32_t const width, uint32_t const height, uint8_t const windowed, uint8_t const vsync)
	{
		// 对应 Lua 侧 ChangeVideoMode
		core::Vector2U const size(width, height);
		if (windowed) {
			return LAPP.SetDisplayModeWindow(size, vsync != 0) ? 1 : 0;
		}
		return LAPP.SetDisplayModeExclusiveFullscreen(size, vsync != 0, core::Rational()) ? 1 : 0;
	}
	uint32_t CLRBinding::video_enumResolutionCount()
	{
		// 与 Lua 侧 EnumResolutions 一致：固定 5 个 4:3 分辨率
		return 5;
	}
	uint8_t CLRBinding::video_enumResolutionByIndex(
		uint32_t const index,
		uint32_t* const width, uint32_t* const height,
		uint32_t* const numerator, uint32_t* const denominator)
	{
		// 与 Lua 侧 EnumResolutions 一致：60/1 刷新率的 4:3 分辨率列表
		static core::DisplayMode const mode_list[5] = {
			{   640,  480, { 60, 1 } },
			{   800,  600, { 60, 1 } },
			{   960,  720, { 60, 1 } },
			{  1024,  768, { 60, 1 } },
			{  1280,  960, { 60, 1 } },
		};
		if (index >= 5)
			return 0;
		auto const& mode = mode_list[index];
		if (width != nullptr)
			*width = mode.width;
		if (height != nullptr)
			*height = mode.height;
		if (numerator != nullptr)
			*numerator = mode.refresh_rate.numerator;
		if (denominator != nullptr)
			*denominator = mode.refresh_rate.denominator;
		return 1;
	}
	uint32_t CLRBinding::video_getGpuCount()
	{
		auto* const device = asGraphicsDevice();
		if (device == nullptr)
			return 0;
		return device->getGpuCount();
	}
	const char* CLRBinding::video_getGpuNameByIndex(uint32_t const index)
	{
		auto* const device = asGraphicsDevice();
		if (device == nullptr || index >= device->getGpuCount())
			return "";
		auto const name = device->getGpuName(index);
		g_gpu_name_by_index.assign(name.data(), name.size());
		return g_gpu_name_by_index.c_str();
	}
	const char* CLRBinding::video_getCurrentGpuName()
	{
		auto const* const device = asGraphicsDevice();
		if (device == nullptr)
			return nullptr;
		auto const name = device->getCurrentGpuName();
		g_current_gpu_name.assign(name.data(), name.size());
		return g_current_gpu_name.c_str();
	}
	uint8_t CLRBinding::video_changeGPU(const char* const name)
	{
		// 0=成功 1=渲染设备不可用 2=重建失败（Lua 侧后两者为 luaL_error）
		auto* const device = asGraphicsDevice();
		if (device == nullptr || name == nullptr)
			return 1;
		device->setPreferenceGpu(std::string_view(name));
		if (!device->recreate())
			return 2;
		return 0;
	}
	void CLRBinding::video_setSwapChainScalingMode(int32_t const mode)
	{
		// 对应 Lua 侧 SetSwapChainScalingMode
		auto* const swapchain = asSwapChain();
		if (swapchain == nullptr)
			return;
		swapchain->setScalingMode(static_cast<core::SwapChainScalingMode>(mode));
	}
}
