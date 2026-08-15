namespace LuaSTG.Core
{
    /// <summary>
    /// 键码（对应 Lua 侧 lstg.Input.Keyboard 常量表，共 171 项）。
    /// 枚举值与 Lua 常量一致，即 Windows 虚拟键码（VK）。
    /// 注意：部分名称为同一 VK 的别名（ImeHangul/ImeKana、ImeKanji/ImeHanja）。
    /// </summary>
    public enum KeyCode : int
    {
        None = 0x00,

        // 控制键
        Back = 0x08,
        Tab = 0x09,
        /// <summary>VK_CLEAR，仅为 Lua 兼容保留，引擎键盘状态不跟踪</summary>
        Clear = 0x0C,
        Enter = 0x0D,
        /// <summary>VK_SHIFT，左右 Shift 的通用码（区分左右用 LeftShift/RightShift）</summary>
        Shift = 0x10,
        /// <summary>VK_CONTROL，左右 Control 的通用码</summary>
        Control = 0x11,
        /// <summary>VK_MENU，左右 Alt 的通用码</summary>
        Alt = 0x12,
        Pause = 0x13,
        CapsLock = 0x14,

        // 输入法相关
        ImeHangul = 0x15,
        ImeKana = 0x15,
        ImeOn = 0x16,
        ImeJunja = 0x17,
        ImeFinal = 0x18,
        ImeKanji = 0x19,
        ImeHanja = 0x19,
        ImeOff = 0x1A,

        Escape = 0x1B,

        ImeConvert = 0x1C,
        ImeNoConvert = 0x1D,
        ImeAccept = 0x1E,
        ImeModeChangeRequest = 0x1F,

        // 导航与编辑
        Space = 0x20,
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Select = 0x29,
        Print = 0x2A,
        Execute = 0x2B,
        PrintScreen = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,
        Help = 0x2F,

        // 主键盘数字排
        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,

        // 字母
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,

        LeftWindows = 0x5B,
        RightWindows = 0x5C,
        Apps = 0x5D,

        Sleep = 0x5F,

        // 小键盘
        NumPad0 = 0x60,
        NumPad1 = 0x61,
        NumPad2 = 0x62,
        NumPad3 = 0x63,
        NumPad4 = 0x64,
        NumPad5 = 0x65,
        NumPad6 = 0x66,
        NumPad7 = 0x67,
        NumPad8 = 0x68,
        NumPad9 = 0x69,
        Multiply = 0x6A,
        Add = 0x6B,
        Separator = 0x6C,
        Subtract = 0x6D,
        Decimal = 0x6E,
        Divide = 0x6F,

        // 功能键
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
        F13 = 0x7C,
        F14 = 0x7D,
        F15 = 0x7E,
        F16 = 0x7F,
        F17 = 0x80,
        F18 = 0x81,
        F19 = 0x82,
        F20 = 0x83,
        F21 = 0x84,
        F22 = 0x85,
        F23 = 0x86,
        F24 = 0x87,

        NumLock = 0x90,
        Scroll = 0x91,

        // 左右区分的修饰键
        LeftShift = 0xA0,
        RightShift = 0xA1,
        LeftControl = 0xA2,
        RightControl = 0xA3,
        LeftAlt = 0xA4,
        RightAlt = 0xA5,

        // 浏览器/媒体
        BrowserBack = 0xA6,
        BrowserForward = 0xA7,
        BrowserRefresh = 0xA8,
        BrowserStop = 0xA9,
        BrowserSearch = 0xAA,
        BrowserFavorites = 0xAB,
        BrowserHome = 0xAC,
        VolumeMute = 0xAD,
        VolumeDown = 0xAE,
        VolumeUp = 0xAF,
        MediaNextTrack = 0xB0,
        MediaPreviousTrack = 0xB1,
        MediaStop = 0xB2,
        MediaPlayPause = 0xB3,
        LaunchMail = 0xB4,
        SelectMedia = 0xB5,
        LaunchApplication1 = 0xB6,
        LaunchApplication2 = 0xB7,

        // OEM 键（沿用 Lua 常量名）
        Semicolon = 0xBA,
        Plus = 0xBB,
        Comma = 0xBC,
        Minus = 0xBD,
        Period = 0xBE,
        Question = 0xBF,
        Tilde = 0xC0,

        OpenBrackets = 0xDB,
        Pipe = 0xDC,
        CloseBrackets = 0xDD,
        Quotes = 0xDE,
        Oem8 = 0xDF,

        /// <summary>102 键盘的 OEM 反斜杠/竖线键（VK_OEM_102）</summary>
        Oem102 = 0xE2,

        ProcessKey = 0xE5,

        /// <summary>小键盘回车，引擎侧占用未分配的 VK 0xE8</summary>
        NumPadEnter = 0xE8,

        OemCopy = 0xF2,
        OemAuto = 0xF3,
        OemEnlW = 0xF4,

        Attn = 0xF6,
        Crsel = 0xF7,
        Exsel = 0xF8,
        EraseEof = 0xF9,

        Play = 0xFA,
        Zoom = 0xFB,

        Pa1 = 0xFD,
        OemClear = 0xFE,
    }

    /// <summary>
    /// 鼠标键（对应 Lua 侧 lstg.Input.Mouse 常量表）。
    /// 枚举值与 Lua 常量一致，即 Windows 虚拟键码（VK）。
    /// 注意：部分名称为同一 VK 的别名（Primary/Left、Secondary/Right、X1/XButton1、X2/XButton2）。
    /// </summary>
    public enum MouseButton : int
    {
        None = 0x00,

        Primary = 0x01,
        Left = 0x01,
        Middle = 0x04,
        Secondary = 0x02,
        Right = 0x02,

        X1 = 0x05,
        XButton1 = 0x05,
        X2 = 0x06,
        XButton2 = 0x06,
    }

    /// <summary>
    /// 输入（对应 Lua 侧 lstg.Input 命名空间与 lstg 顶层输入兼容 API）。
    /// 键盘查询直接位于本类（语义对应 lstg.Input.Keyboard），
    /// 鼠标查询位于 <see cref="Mouse"/>（语义对应 lstg.Input.Mouse）。
    /// </summary>
    public static unsafe class Input
    {
        // ------------------------------------------------------------------
        // 键盘（lstg.Input.Keyboard.GetKeyState / 兼容 API lstg.GetKeyState）
        // ------------------------------------------------------------------

        /// <summary>
        /// 查询键盘按键状态（对应 lstg.Input.Keyboard.GetKeyState）。
        /// </summary>
        /// <param name="key">键码</param>
        /// <returns>本帧该键是否处于按下状态</returns>
        public static bool GetKeyState(KeyCode key)
            => LuaSTGAPI.api.input_getKeyState((int)key) != 0;

        /// <summary>
        /// 按虚拟键码查询键盘按键状态（对应兼容 API lstg.GetKeyState）。
        /// </summary>
        /// <param name="vkCode">Windows 虚拟键码</param>
        /// <returns>本帧该键是否处于按下状态</returns>
        public static bool GetKeyState(int vkCode)
            => LuaSTGAPI.api.input_getKeyState(vkCode) != 0;

        /// <summary>
        /// 获取最后按下的键（对应兼容 API lstg.GetLastKey，Lua 侧已废弃）。
        /// </summary>
        /// <returns>最后按下的虚拟键码</returns>
        [Obsolete("lstg.GetLastKey 在 Lua 侧已废弃，仅为兼容保留")]
        public static KeyCode GetLastKey()
            => (KeyCode)LuaSTGAPI.api.input_getLastKey();

        // ------------------------------------------------------------------
        // 鼠标（lstg.Input.Mouse）
        // ------------------------------------------------------------------

        /// <summary>
        /// 鼠标（对应 Lua 侧 lstg.Input.Mouse）。
        /// </summary>
        public static class Mouse
        {
            /// <summary>
            /// 查询鼠标按键状态（对应 lstg.Input.Mouse.GetKeyState）。
            /// </summary>
            /// <param name="button">鼠标键</param>
            /// <returns>本帧该键是否处于按下状态</returns>
            public static bool GetKeyState(MouseButton button)
                => LuaSTGAPI.api.input_getMouseState((int)button) != 0;

            /// <summary>
            /// 按虚拟键码查询鼠标按键状态。
            /// </summary>
            /// <param name="vkButton">鼠标键虚拟键码（VK_LBUTTON/VK_RBUTTON/VK_MBUTTON/VK_XBUTTON1/VK_XBUTTON2）</param>
            /// <returns>本帧该键是否处于按下状态</returns>
            public static bool GetKeyState(int vkButton)
                => LuaSTGAPI.api.input_getMouseState(vkButton) != 0;

            /// <summary>
            /// 获取鼠标在画布坐标系中的位置（对应 lstg.Input.Mouse.GetPosition）。
            /// </summary>
            /// <param name="noFlip">为 true 时不做 y 翻转（屏幕坐标，原点左上角）；
            /// 为 false 时返回 LuaSTG 世界坐标（原点左下角）</param>
            /// <param name="x">横坐标</param>
            /// <param name="y">纵坐标</param>
            public static void GetPosition(bool noFlip, out double x, out double y)
            {
                double px = 0.0, py = 0.0;
                LuaSTGAPI.api.input_getMousePosition(noFlip ? (byte)1 : (byte)0, &px, &py);
                x = px;
                y = py;
            }

            /// <summary>
            /// 获取鼠标在 LuaSTG 世界坐标系（原点左下角）中的位置。
            /// </summary>
            /// <param name="x">横坐标</param>
            /// <param name="y">纵坐标</param>
            public static void GetPosition(out double x, out double y)
                => GetPosition(false, out x, out y);

            /// <summary>
            /// 获取本帧鼠标滚轮增量（对应 lstg.Input.Mouse.GetWheelDelta）。
            /// </summary>
            /// <returns>滚过的“格”数，向前为正、向后为负</returns>
            public static double GetWheelDelta()
                => LuaSTGAPI.api.input_getMouseWheelDelta() / 120.0;
        }

        // ------------------------------------------------------------------
        // lstg 顶层兼容 API（Lua 侧已建议废弃，保留以便移植旧脚本）
        // ------------------------------------------------------------------

        /// <summary>
        /// 按旧版索引查询鼠标按键状态（对应兼容 API lstg.GetMouseState）。
        /// </summary>
        /// <param name="button">0=左键 1=中键 2=右键 3=X1 4=X2</param>
        /// <returns>本帧该键是否处于按下状态</returns>
        [Obsolete("请使用 Input.Mouse.GetKeyState(MouseButton)，此 API 仅为兼容 lstg.GetMouseState 保留")]
        public static bool GetMouseState(int button)
            => LuaSTGAPI.api.input_getMouseStateLegacy(button) != 0;

        /// <summary>
        /// 获取鼠标位置（对应兼容 API lstg.GetMousePosition，等价于 Input.Mouse.GetPosition）。
        /// </summary>
        /// <param name="noFlip">为 true 时不做 y 翻转</param>
        /// <param name="x">横坐标</param>
        /// <param name="y">纵坐标</param>
        [Obsolete("请使用 Input.Mouse.GetPosition，此 API 仅为兼容 lstg.GetMousePosition 保留")]
        public static void GetMousePosition(bool noFlip, out double x, out double y)
            => Mouse.GetPosition(noFlip, out x, out y);

        /// <summary>
        /// 获取本帧鼠标滚轮原始增量（对应兼容 API lstg.GetMouseWheelDelta）。
        /// </summary>
        /// <returns>原始滚轮增量（单位 1/120 格）</returns>
        [Obsolete("请使用 Input.Mouse.GetWheelDelta，此 API 仅为兼容 lstg.GetMouseWheelDelta 保留")]
        public static int GetMouseWheelDelta()
            => LuaSTGAPI.api.input_getMouseWheelDelta();
    }
}
