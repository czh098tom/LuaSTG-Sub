// XInput 手柄输入（对应 Lua 侧 xinput 库，LuaBinding/external/lua_xinput.cpp）。
// 引擎侧实现位于 engine/win32/windows/XInput.cpp（动态加载 XInput DLL）。

namespace LuaSTG.Core
{
    /// <summary>
    /// XInput 手柄按钮掩码（对应 Lua 侧 xinput 库常量表，值与 XINPUT_GAMEPAD_* 一致）。
    /// </summary>
    public enum XInputButton : int
    {
        /// <summary>无按键</summary>
        Null = 0x0000,
        Up = 0x0001,
        Down = 0x0002,
        Left = 0x0004,
        Right = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
    }

    /// <summary>
    /// XInput 手柄输入（对应 Lua 侧 xinput 库）。
    /// 设备索引为 0 基（0..3，XUSER_MAX_COUNT-1）；Lua 侧为 1 基，脚本移植时需注意。
    /// 使用前先调用 <see cref="Refresh"/> 枚举设备，之后每帧调用 <see cref="Update"/>。
    /// </summary>
    public static unsafe class XInput
    {
        /// <summary>最大用户数（XUSER_MAX_COUNT）。</summary>
        public const int MaxUserCount = 4;

        /// <summary>查询手柄是否连接（对应 xinput.isConnected）。</summary>
        /// <param name="index">设备索引（0 基）</param>
        public static bool IsConnected(int index)
            => LuaSTGAPI.api.xinput_isConnected(index) != 0;

        /// <summary>
        /// 重新枚举所有手柄（对应 xinput.refresh）。
        /// </summary>
        /// <returns>当前连接的手柄数量</returns>
        public static int Refresh()
            => LuaSTGAPI.api.xinput_refresh();

        /// <summary>
        /// 更新所有已连接手柄的状态（对应 xinput.update，引擎每帧调用）。
        /// 设备断开时清空其状态。
        /// </summary>
        public static void Update()
            => LuaSTGAPI.api.xinput_update();

        /// <summary>查询指定手柄的按键状态（对应 xinput.getKeyState）。</summary>
        /// <param name="index">设备索引（0 基）</param>
        /// <param name="key">按钮（可为组合掩码）</param>
        public static bool GetKeyState(int index, XInputButton key)
            => LuaSTGAPI.api.xinput_getKeyState(index, (int)key) != 0;

        /// <summary>查询指定手柄的按键状态（对应 xinput.getKeyState，参数为原始掩码）。</summary>
        public static bool GetKeyState(int index, int key)
            => LuaSTGAPI.api.xinput_getKeyState(index, key) != 0;

        /// <summary>
        /// 查询第一个可用手柄的按键状态（对应 xinput.getKeyState 单参数形式）。
        /// </summary>
        public static bool GetKeyStateAny(XInputButton key)
            => LuaSTGAPI.api.xinput_getKeyStateAny((int)key) != 0;

        /// <summary>查询第一个可用手柄的按键状态（参数为原始掩码）。</summary>
        public static bool GetKeyStateAny(int key)
            => LuaSTGAPI.api.xinput_getKeyStateAny(key) != 0;

        /// <summary>获取左扳机值 [0,1]（对应 xinput.getLeftTrigger）。</summary>
        public static float GetLeftTrigger(int index)
            => LuaSTGAPI.api.xinput_getLeftTrigger(index);

        /// <summary>获取第一个可用手柄的左扳机值 [0,1]。</summary>
        public static float GetLeftTriggerAny()
            => LuaSTGAPI.api.xinput_getLeftTriggerAny();

        /// <summary>获取右扳机值 [0,1]（对应 xinput.getRightTrigger）。</summary>
        public static float GetRightTrigger(int index)
            => LuaSTGAPI.api.xinput_getRightTrigger(index);

        /// <summary>获取第一个可用手柄的右扳机值 [0,1]。</summary>
        public static float GetRightTriggerAny()
            => LuaSTGAPI.api.xinput_getRightTriggerAny();

        /// <summary>获取左摇杆 X 轴 [-1,1]（对应 xinput.getLeftThumbX）。</summary>
        public static float GetLeftThumbX(int index)
            => LuaSTGAPI.api.xinput_getLeftThumbX(index);

        /// <summary>获取第一个可用手柄的左摇杆 X 轴 [-1,1]。</summary>
        public static float GetLeftThumbXAny()
            => LuaSTGAPI.api.xinput_getLeftThumbXAny();

        /// <summary>获取左摇杆 Y 轴 [-1,1]（对应 xinput.getLeftThumbY）。</summary>
        public static float GetLeftThumbY(int index)
            => LuaSTGAPI.api.xinput_getLeftThumbY(index);

        /// <summary>获取第一个可用手柄的左摇杆 Y 轴 [-1,1]。</summary>
        public static float GetLeftThumbYAny()
            => LuaSTGAPI.api.xinput_getLeftThumbYAny();

        /// <summary>获取右摇杆 X 轴 [-1,1]（对应 xinput.getRightThumbX）。</summary>
        public static float GetRightThumbX(int index)
            => LuaSTGAPI.api.xinput_getRightThumbX(index);

        /// <summary>获取第一个可用手柄的右摇杆 X 轴 [-1,1]。</summary>
        public static float GetRightThumbXAny()
            => LuaSTGAPI.api.xinput_getRightThumbXAny();

        /// <summary>获取右摇杆 Y 轴 [-1,1]（对应 xinput.getRightThumbY）。</summary>
        public static float GetRightThumbY(int index)
            => LuaSTGAPI.api.xinput_getRightThumbY(index);

        /// <summary>获取第一个可用手柄的右摇杆 Y 轴 [-1,1]。</summary>
        public static float GetRightThumbYAny()
            => LuaSTGAPI.api.xinput_getRightThumbYAny();
    }
}
