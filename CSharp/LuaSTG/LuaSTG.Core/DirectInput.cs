// DirectInput 游戏控制器输入（对应 Lua 侧 lstg.DirectInput / dinput 库，
// LuaBinding/LW_DInput.cpp，引擎侧为 AppFrame::GetDInput() 返回的
// Platform::DirectInput）。

using System.Runtime.InteropServices;

namespace LuaSTG.Core
{
    /// <summary>设备各轴的取值范围（对应 Platform::DirectInput::AxisRange，平铺为基本类型）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectInputAxisRange
    {
        public int XMin;
        public int YMin;
        public int ZMin;
        public int XMax;
        public int YMax;
        public int ZMax;
        public int RxMin;
        public int RyMin;
        public int RzMin;
        public int RxMax;
        public int RyMax;
        public int RzMax;
        public int Slider0Min;
        public int Slider1Min;
        public int Slider0Max;
        public int Slider1Max;
    }

    /// <summary>设备原始状态（对应 Platform::DirectInput::RawState，平铺为基本类型）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DirectInputRawState
    {
        public int LX;
        public int LY;
        public int LZ;
        public int LRx;
        public int LRy;
        public int LRz;
        public int Slider0;
        public int Slider1;
        /// <summary>POV（帽开关）方向，原始值（Lua 侧表为 &amp;0xFFFF，-1 表示未按下）。</summary>
        public uint POV0;
        public uint POV1;
        public uint POV2;
        public uint POV3;
        /// <summary>32 个按钮的原始字节（非 0 表示按下）。</summary>
        public fixed byte Buttons[32];

        /// <summary>查询按钮状态（索引 0..31）。</summary>
        public bool GetButton(int index)
        {
            fixed (byte* p = Buttons)
            {
                return p[index] != 0;
            }
        }
    }

    /// <summary>设备手柄化状态（对应 Platform::DirectInput::State，平铺为基本类型）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectInputGamepadState
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    /// <summary>
    /// DirectInput 游戏控制器输入（对应 Lua 侧 lstg.DirectInput / dinput 库）。
    /// 设备索引为 0 基；Lua 侧为 1 基，脚本移植时需注意。
    /// 需引擎启用 DirectInput（GetDInput() 非空）时各查询才有效，否则 Count 为 0、查询返回失败。
    /// </summary>
    public static unsafe class DirectInput
    {
        /// <summary>当前设备数量（对应 dinput.count；引擎未启用 DirectInput 时为 0）。</summary>
        public static int Count()
            => (int)LuaSTGAPI.api.dinput_count();

        /// <summary>
        /// 重新枚举设备（对应 dinput.refresh）。
        /// </summary>
        /// <returns>设备数量</returns>
        public static int Refresh()
            => (int)LuaSTGAPI.api.dinput_refresh();

        /// <summary>更新所有设备状态（对应 dinput.update，引擎每帧调用）。</summary>
        public static void Update()
            => LuaSTGAPI.api.dinput_update();

        /// <summary>重置所有设备状态（对应 dinput.reset）。</summary>
        public static void Reset()
            => LuaSTGAPI.api.dinput_reset();

        /// <summary>
        /// 获取设备各轴的取值范围（对应 dinput.getAxisRange）。
        /// </summary>
        /// <param name="index">设备索引（0 基）</param>
        /// <param name="range">输出的轴范围</param>
        /// <returns>设备是否存在</returns>
        public static bool GetAxisRange(int index, out DirectInputAxisRange range)
        {
            fixed (DirectInputAxisRange* p = &range)
            {
                return LuaSTGAPI.api.dinput_getAxisRange((uint)index, (int*)p) != 0;
            }
        }

        /// <summary>
        /// 获取设备原始状态（对应 dinput.getRawState）。
        /// </summary>
        /// <param name="index">设备索引（0 基）</param>
        /// <param name="state">输出的原始状态</param>
        /// <returns>设备是否存在</returns>
        public static bool GetRawState(int index, out DirectInputRawState state)
        {
            fixed (DirectInputRawState* p = &state)
            {
                var axes = (int*)p;                       // LX..Slider1 共 8 个 int32
                var pov = (uint*)(axes + 8);              // POV0..POV3 共 4 个 uint32
                var buttons = (byte*)(pov + 4);           // Buttons[32]
                return LuaSTGAPI.api.dinput_getRawState((uint)index, axes, pov, buttons) != 0;
            }
        }

        /// <summary>
        /// 获取设备手柄化状态（对应 dinput.getState）。
        /// </summary>
        /// <param name="index">设备索引（0 基）</param>
        /// <param name="state">输出的手柄状态</param>
        /// <returns>设备是否存在</returns>
        public static bool GetState(int index, out DirectInputGamepadState state)
        {
            int* buffer = stackalloc int[7];
            bool ok = LuaSTGAPI.api.dinput_getState((uint)index, buffer) != 0;
            state = new DirectInputGamepadState
            {
                Buttons = (ushort)buffer[0],
                LeftTrigger = (byte)buffer[1],
                RightTrigger = (byte)buffer[2],
                ThumbLX = (short)buffer[3],
                ThumbLY = (short)buffer[4],
                ThumbRX = (short)buffer[5],
                ThumbRY = (short)buffer[6],
            };
            return ok;
        }

        /// <summary>
        /// 获取设备名（对应 dinput.getDeviceName，引擎以 UTF-8 返回）。
        /// 设备不存在时返回空字符串。
        /// </summary>
        public static string GetDeviceName(int index)
            => StringMarshal.FromUtf8(LuaSTGAPI.api.dinput_getDeviceName((uint)index));

        /// <summary>
        /// 获取产品名（对应 dinput.getProductName）。
        /// 设备不存在时返回空字符串。
        /// </summary>
        public static string GetProductName(int index)
            => StringMarshal.FromUtf8(LuaSTGAPI.api.dinput_getProductName((uint)index));

        /// <summary>查询设备是否为 XInput 兼容设备（对应 dinput.isXInputDevice）。</summary>
        public static bool IsXInputDevice(int index)
            => LuaSTGAPI.api.dinput_isXInputDevice((uint)index) != 0;
    }
}
