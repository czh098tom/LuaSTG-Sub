using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 窗口边框样式（对应 lstg.Window.FrameStyle，与引擎 core::WindowFrameStyle 一致）。
    /// </summary>
    public enum FrameStyle : int
    {
        /// <summary>无边框（Lua: borderless，引擎: None）</summary>
        Borderless = 0,
        /// <summary>固定大小边框（Lua: fixed）</summary>
        Fixed = 1,
        /// <summary>普通可调节边框（Lua: normal）</summary>
        Normal = 2,
    }

    /// <summary>
    /// 交换链缩放模式（对应 lstg.SwapChain.ScalingMode，与引擎 core::SwapChainScalingMode 一致）。
    /// </summary>
    public enum ScalingMode : int
    {
        /// <summary>拉伸填充（Lua: stretch）</summary>
        Stretch = 0,
        /// <summary>保持纵横比（Lua: aspect_ratio）</summary>
        AspectRatio = 1,
    }

    /// <summary>
    /// 主窗口（对应 Lua 侧 lstg.Window，单例风格：全部成员操作引擎主窗口）。
    /// 窗口扩展以静态属性形式暴露：<see cref="InputMethod"/>、<see cref="TextInput"/>、<see cref="Windows11"/>，
    /// 扩展存在性可用对应 Is*Supported 属性查询（对应 Lua 侧 queryInterface），不可用时调用其成员抛
    /// <see cref="NotSupportedException"/>。
    /// 兼容 API lstg.SetTitle/SetSplash 已由 <see cref="LuaSTGAPI.SetSplash"/>/
    /// <see cref="LuaSTGAPI.SetWindowTitle"/>（引擎 Core 模块）提供，此处不重复定义。
    /// </summary>
    public static unsafe partial class Window
    {
        private static readonly InputMethodExtension _inputMethod = new();
        private static readonly TextInputExtension _textInput = new();
        private static readonly Windows11Extension _windows11 = new();
        private static bool _isWindows11ExtensionSupported;

        static Window()
        {
            // 对应 Lua 侧 Window:queryInterface("lstg.Window.Windows11Extension")：
            // Windows11Extension 仅在 Windows 11 上可用，其余扩展始终可用
            using var name = new MarshaledString("lstg.Window.Windows11Extension");
            _isWindows11ExtensionSupported = LuaSTGAPI.api.window_queryInterface(name) != 0;
        }

        /// <summary>主窗口的引擎句柄（对应 lstg.Window.getMain，借用句柄，不得释放）</summary>
        public static nint Handle => (nint)LuaSTGAPI.api.window_getMain();

        // ------------------------------------------------------------------
        // 实例方法（lstg.Window 成员）
        // ------------------------------------------------------------------

        /// <summary>设置窗口标题（对应 lstg.Window:setTitle）</summary>
        public static void SetTitle(string text)
        {
            using var s = new MarshaledString(text);
            LuaSTGAPI.api.window_setTitle(s);
        }

        /// <summary>获取窗口客户区大小（对应 lstg.Window:getClientAreaSize）</summary>
        /// <param name="width">客户区宽度（像素）</param>
        /// <param name="height">客户区高度（像素）</param>
        public static void GetClientAreaSize(out uint width, out uint height)
        {
            uint w = 0, h = 0;
            LuaSTGAPI.api.window_getClientAreaSize(&w, &h);
            width = w;
            height = h;
        }

        /// <summary>获取窗口边框样式（对应 lstg.Window:getStyle）</summary>
        public static FrameStyle GetStyle()
            => (FrameStyle)LuaSTGAPI.api.window_getStyle();

        /// <summary>获取显示器缩放系数（对应 lstg.Window:getDisplayScale）</summary>
        public static float GetDisplayScale()
            => LuaSTGAPI.api.window_getDisplayScale();

        /// <summary>
        /// 设置窗口化模式（对应 lstg.Window:setWindowed）。
        /// </summary>
        /// <param name="width">窗口宽度（像素）</param>
        /// <param name="height">窗口高度（像素）</param>
        /// <param name="style">边框样式</param>
        /// <param name="display">目标显示器，null 表示使用当前显示器</param>
        public static void SetWindowed(uint width, uint height, FrameStyle style, Display? display)
            => LuaSTGAPI.api.window_setWindowed(width, height, (int)style, display != null ? (nuint)display.Handle : 0);

        /// <summary>设置窗口化模式，使用当前显示器（对应 lstg.Window:setWindowed）</summary>
        public static void SetWindowed(uint width, uint height, FrameStyle style)
            => SetWindowed(width, height, style, null);

        /// <summary>
        /// 设置窗口化模式，沿用当前边框样式与显示器（对应 lstg.Window:setWindowed 缺省参数行为）。
        /// </summary>
        public static void SetWindowed(uint width, uint height)
            => SetWindowed(width, height, GetStyle(), null);

        /// <summary>
        /// 设置全屏模式（对应 lstg.Window:setFullscreen）。
        /// </summary>
        /// <param name="display">目标显示器，null 表示使用当前显示器</param>
        public static void SetFullscreen(Display? display)
            => LuaSTGAPI.api.window_setFullscreen(display != null ? (nuint)display.Handle : 0);

        /// <summary>在当前显示器上设置全屏模式（对应 lstg.Window:setFullscreen 缺省参数行为）</summary>
        public static void SetFullscreen()
            => SetFullscreen(null);

        /// <summary>获取光标可见性（对应 lstg.Window:getCursorVisibility）</summary>
        public static bool GetCursorVisibility()
            => LuaSTGAPI.api.window_getCursorVisibility() != 0;

        /// <summary>设置光标可见性（对应 lstg.Window:setCursorVisibility）</summary>
        public static void SetCursorVisibility(bool visible)
            => LuaSTGAPI.api.window_setCursorVisibility(visible ? (byte)1 : (byte)0);

        // ------------------------------------------------------------------
        // 窗口扩展（lstg.Window:queryInterface 获取，这里以静态属性形式暴露）
        // ------------------------------------------------------------------

        /// <summary>输入法扩展（对应 lstg.Window.InputMethodExtension），始终可用</summary>
        public static InputMethodExtension InputMethod => _inputMethod;

        /// <summary>文本输入扩展（对应 lstg.Window.TextInputExtension），始终可用</summary>
        public static TextInputExtension TextInput => _textInput;

        /// <summary>Windows 11 扩展（对应 lstg.Window.Windows11Extension），仅在 Windows 11 上可用</summary>
        public static Windows11Extension Windows11 => _windows11;

        /// <summary>输入法扩展是否可用（对应 queryInterface 查询结果），始终为 true</summary>
        public static bool IsInputMethodExtensionSupported
        {
            get
            {
                using var name = new MarshaledString("lstg.Window.InputMethodExtension");
                return LuaSTGAPI.api.window_queryInterface(name) != 0;
            }
        }

        /// <summary>文本输入扩展是否可用（对应 queryInterface 查询结果），始终为 true</summary>
        public static bool IsTextInputExtensionSupported
        {
            get
            {
                using var name = new MarshaledString("lstg.Window.TextInputExtension");
                return LuaSTGAPI.api.window_queryInterface(name) != 0;
            }
        }

        /// <summary>Windows 11 扩展是否可用（对应 queryInterface 查询结果），非 Windows 11 为 false</summary>
        public static bool IsWindows11ExtensionSupported => _isWindows11ExtensionSupported;
    }

    /// <summary>
    /// 窗口输入法扩展（对应 lstg.Window.InputMethodExtension，经 <see cref="Window.InputMethod"/> 访问）。
    /// </summary>
    public sealed unsafe class InputMethodExtension
    {
        internal InputMethodExtension()
        {
        }

        /// <summary>输入法是否启用（对应 lstg.Window.InputMethodExtension:isInputMethodEnabled）</summary>
        public bool IsInputMethodEnabled
            => LuaSTGAPI.api.window_ime_isEnabled() != 0;

        /// <summary>启用/禁用输入法（对应 lstg.Window.InputMethodExtension:setInputMethodEnabled）</summary>
        public void SetInputMethodEnabled(bool enabled)
            => LuaSTGAPI.api.window_ime_setEnabled(enabled ? (byte)1 : (byte)0);

        /// <summary>设置输入法组合窗口位置（对应 lstg.Window.InputMethodExtension:setInputMethodPosition）</summary>
        /// <param name="x">横坐标（屏幕像素）</param>
        /// <param name="y">纵坐标（屏幕像素）</param>
        public void SetInputMethodPosition(int x, int y)
            => LuaSTGAPI.api.window_ime_setPosition(x, y);
    }

    /// <summary>
    /// 窗口文本输入扩展（对应 lstg.Window.TextInputExtension，经 <see cref="Window.TextInput"/> 访问）。
    /// 文本缓冲按码点（code point）索引。
    /// </summary>
    public sealed unsafe class TextInputExtension
    {
        internal TextInputExtension()
        {
        }

        /// <summary>文本输入是否启用（对应 lstg.Window.TextInputExtension:isEnabled / setEnabled）</summary>
        public bool IsEnabled
        {
            get => LuaSTGAPI.api.window_textInput_isEnabled() != 0;
            set => LuaSTGAPI.api.window_textInput_setEnabled(value ? (byte)1 : (byte)0);
        }

        /// <summary>获取输入缓冲文本（对应 lstg.Window.TextInputExtension:toString）</summary>
        public string GetBuffer()
            => StringMarshal.FromUtf8(LuaSTGAPI.api.window_textInput_getBuffer());

        /// <inheritdoc cref="GetBuffer"/>
        public override string ToString()
            => GetBuffer();

        /// <summary>清空输入缓冲（对应 lstg.Window.TextInputExtension:clear）</summary>
        public void Clear()
            => LuaSTGAPI.api.window_textInput_clear();

        /// <summary>获取光标位置（对应 lstg.Window.TextInputExtension:getCursorPosition），单位为码点</summary>
        public uint GetCursorPosition()
            => LuaSTGAPI.api.window_textInput_getCursorPosition();

        /// <summary>设置光标位置（对应 lstg.Window.TextInputExtension:setCursorPosition），单位为码点</summary>
        public void SetCursorPosition(uint position)
            => LuaSTGAPI.api.window_textInput_setCursorPosition(position);

        /// <summary>相对移动光标（对应 lstg.Window.TextInputExtension:addCursorPosition），单位为码点</summary>
        public void AddCursorPosition(int offset)
            => LuaSTGAPI.api.window_textInput_addCursorPosition(offset);

        /// <summary>在光标处插入文本（对应 lstg.Window.TextInputExtension:insert 单参数形式）</summary>
        public void Insert(string text)
            => Insert(GetCursorPosition(), text);

        /// <summary>在指定码点下标处插入文本（对应 lstg.Window.TextInputExtension:insert 双参数形式）</summary>
        /// <param name="index">插入位置的码点下标</param>
        /// <param name="text">UTF-8 文本</param>
        public void Insert(uint index, string text)
        {
            using var s = new MarshaledString(text);
            LuaSTGAPI.api.window_textInput_insert(index, s);
        }

        /// <summary>从光标处起删除若干码点（对应 lstg.Window.TextInputExtension:remove，缺省 index 为光标位置）</summary>
        /// <param name="count">删除的码点数量</param>
        public void Remove(uint count)
            => LuaSTGAPI.api.window_textInput_remove(GetCursorPosition(), count);

        /// <summary>删除指定范围内的码点（对应 lstg.Window.TextInputExtension:remove）</summary>
        /// <param name="index">起始码点下标</param>
        /// <param name="count">删除的码点数量</param>
        public void Remove(uint index, uint count)
            => LuaSTGAPI.api.window_textInput_remove(index, count);

        /// <summary>退格删除若干码点（对应 lstg.Window.TextInputExtension:backspace，缺省 count 为 1）</summary>
        public void Backspace(uint count = 1)
            => LuaSTGAPI.api.window_textInput_backspace(count);
    }

    /// <summary>
    /// Windows 11 窗口扩展（对应 lstg.Window.Windows11Extension，经 <see cref="Window.Windows11"/> 访问）。
    /// 仅在 Windows 11 上可用；其他系统上调用成员抛 <see cref="NotSupportedException"/>
    /// （对应 Lua 侧 queryInterface 返回 nil 的情形）。
    /// </summary>
    public sealed unsafe class Windows11Extension
    {
        internal Windows11Extension()
        {
        }

        /// <summary>检查扩展可用性</summary>
        private static void ThrowIfNotSupported()
        {
            if (!Window.IsWindows11ExtensionSupported)
            {
                throw new NotSupportedException("Windows11Extension 仅在 Windows 11 上可用");
            }
        }

        /// <summary>设置窗口圆角偏好（对应 lstg.Window.Windows11Extension:setWindowCornerPreference）</summary>
        public void SetWindowCornerPreference(bool allow)
        {
            ThrowIfNotSupported();
            LuaSTGAPI.api.window_win11_setWindowCornerPreference(allow ? (byte)1 : (byte)0);
        }

        /// <summary>设置标题栏自动隐藏偏好（对应 lstg.Window.Windows11Extension:setTitleBarAutoHidePreference）</summary>
        public void SetTitleBarAutoHidePreference(bool allow)
        {
            ThrowIfNotSupported();
            LuaSTGAPI.api.window_win11_setTitleBarAutoHidePreference(allow ? (byte)1 : (byte)0);
        }
    }

    /// <summary>
    /// 主交换链（对应 Lua 侧 lstg.SwapChain，单例风格：全部成员操作引擎主交换链）。
    /// </summary>
    public static unsafe partial class SwapChain
    {
        /// <summary>主交换链的引擎句柄（对应 lstg.SwapChain.getMain，借用句柄，不得释放）</summary>
        public static nint Handle => (nint)LuaSTGAPI.api.swapchain_getMain();

        /// <summary>
        /// 将交换链恢复到窗口模式并设置窗口大小（对应 lstg.SwapChain:setWindowed）。
        /// </summary>
        /// <returns>引擎是否接受本次设置</returns>
        public static bool SetWindowed(uint width, uint height)
            => LuaSTGAPI.api.swapchain_setWindowed(width, height) != 0;

        /// <summary>获取画布大小（对应 lstg.SwapChain:getSize）</summary>
        /// <param name="width">画布宽度（像素）</param>
        /// <param name="height">画布高度（像素）</param>
        public static void GetSize(out uint width, out uint height)
        {
            uint w = 0, h = 0;
            LuaSTGAPI.api.swapchain_getSize(&w, &h);
            width = w;
            height = h;
        }

        /// <summary>设置画布大小（对应 lstg.SwapChain:setSize）</summary>
        /// <returns>引擎是否接受本次设置</returns>
        public static bool SetSize(uint width, uint height)
            => LuaSTGAPI.api.swapchain_setSize(width, height) != 0;

        /// <summary>获取垂直同步偏好（对应 lstg.SwapChain:getVSyncPreference）</summary>
        public static bool GetVSyncPreference()
            => LuaSTGAPI.api.swapchain_getVSyncPreference() != 0;

        /// <summary>设置垂直同步偏好（对应 lstg.SwapChain:setVSyncPreference）</summary>
        public static void SetVSyncPreference(bool enable)
            => LuaSTGAPI.api.swapchain_setVSyncPreference(enable ? (byte)1 : (byte)0);

        /// <summary>获取缩放模式（对应 lstg.SwapChain:getScalingMode）</summary>
        public static ScalingMode GetScalingMode()
            => (ScalingMode)LuaSTGAPI.api.swapchain_getScalingMode();

        /// <summary>设置缩放模式（对应 lstg.SwapChain:setScalingMode）</summary>
        public static void SetScalingMode(ScalingMode mode)
            => LuaSTGAPI.api.swapchain_setScalingMode((int)mode);
    }
}
