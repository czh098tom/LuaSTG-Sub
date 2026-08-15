// LuaSTG CoreCLR 绑定：外部模块（random/Well512/DInput/XInput 等）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// random / Well512 为纯算法，直接在 C# 侧（LuaSTG.Core/Rng/）实现，不占用本 API 列表。

// ---------------------------------------------------------------------------
// XInput（对应 LuaBinding/external/lua_xinput.cpp，引擎 engine/win32/windows/XInput.cpp）
// 设备索引为 0 基（0..XUSER_MAX_COUNT-1）；Lua 侧为 1 基，由绑定层减一。
// ---------------------------------------------------------------------------

// 对应 xinput.isConnected：查询手柄是否连接
DECLARE_CLR_API(uint8_t, xinput_isConnected, (int32_t index))
// 对应 xinput.refresh：重新枚举所有手柄，返回连接数
DECLARE_CLR_API(int32_t, xinput_refresh, ())
// 对应 xinput.update：更新已连接手柄的状态（断开时清空状态）
DECLARE_CLR_API(void, xinput_update, ())
// 对应 xinput.getKeyState(index, key)：查询指定手柄按键（key 为按钮位掩码，可组合）
DECLARE_CLR_API(uint8_t, xinput_getKeyState, (int32_t index, int32_t key))
// 对应 xinput.getKeyState(key)：查询第一个可用手柄的按键
DECLARE_CLR_API(uint8_t, xinput_getKeyStateAny, (int32_t key))
// 对应 xinput.getLeftTrigger：左扳机 [0,1]（Any 变体取第一个可用手柄）
DECLARE_CLR_API(float, xinput_getLeftTrigger, (int32_t index))
DECLARE_CLR_API(float, xinput_getLeftTriggerAny, ())
// 对应 xinput.getRightTrigger：右扳机 [0,1]
DECLARE_CLR_API(float, xinput_getRightTrigger, (int32_t index))
DECLARE_CLR_API(float, xinput_getRightTriggerAny, ())
// 对应 xinput.getLeftThumbX / getLeftThumbY：左摇杆 [-1,1]
DECLARE_CLR_API(float, xinput_getLeftThumbX, (int32_t index))
DECLARE_CLR_API(float, xinput_getLeftThumbXAny, ())
DECLARE_CLR_API(float, xinput_getLeftThumbY, (int32_t index))
DECLARE_CLR_API(float, xinput_getLeftThumbYAny, ())
// 对应 xinput.getRightThumbX / getRightThumbY：右摇杆 [-1,1]
DECLARE_CLR_API(float, xinput_getRightThumbX, (int32_t index))
DECLARE_CLR_API(float, xinput_getRightThumbXAny, ())
DECLARE_CLR_API(float, xinput_getRightThumbY, (int32_t index))
DECLARE_CLR_API(float, xinput_getRightThumbYAny, ())

// ---------------------------------------------------------------------------
// DirectInput（对应 LuaBinding/LW_DInput.cpp，引擎 AppFrame::GetDInput()）
// 设备索引为 0 基；Lua 侧为 1 基，由绑定层减一。结构体结果平铺到调用方提供的缓冲区。
// ---------------------------------------------------------------------------

// 对应 dinput.count：设备数量（引擎未启用 DirectInput 时为 0）
DECLARE_CLR_API(uint32_t, dinput_count, ())
// 对应 dinput.refresh：重新枚举设备，返回设备数量
DECLARE_CLR_API(uint32_t, dinput_refresh, ())
// 对应 dinput.update：更新所有设备状态
DECLARE_CLR_API(void, dinput_update, ())
// 对应 dinput.reset：重置所有设备状态
DECLARE_CLR_API(void, dinput_reset, ())
// 对应 dinput.getAxisRange：写入 16 个 int32（XMin..RzMax 与 Slider0/1 Min/Max，顺序同
// Platform::DirectInput::AxisRange），返回设备是否存在
DECLARE_CLR_API(uint8_t, dinput_getAxisRange, (uint32_t index, int32_t* out_range))
// 对应 dinput.getRawState：axes 写入 8 个 int32（lX,lY,lZ,lRx,lRy,lRz,rglSlider[2]），
// pov 写入 4 个 uint32（原始 rgdwPOV，Lua 侧表会再 &0xFFFF），
// buttons 写入 32 个 uint8（rgbButtons），返回设备是否存在
DECLARE_CLR_API(uint8_t, dinput_getRawState, (uint32_t index, int32_t* out_axes, uint32_t* out_pov, uint8_t* out_buttons))
// 对应 dinput.getState：写入 7 个 int32（wButtons,bLeftTrigger,bRightTrigger,
// sThumbLX,sThumbLY,sThumbRX,sThumbRY，均按符号扩展后的值），返回设备是否存在
DECLARE_CLR_API(uint8_t, dinput_getState, (uint32_t index, int32_t* out_state))
// 对应 dinput.getDeviceName：设备名（UTF-8，指向内部静态缓冲区，C# 侧立即拷贝；失败返回空指针）
DECLARE_CLR_API(const char*, dinput_getDeviceName, (uint32_t index))
// 对应 dinput.getProductName：产品名（UTF-8，同上）
DECLARE_CLR_API(const char*, dinput_getProductName, (uint32_t index))
// 对应 dinput.isXInputDevice：设备是否为 XInput 兼容设备
DECLARE_CLR_API(uint8_t, dinput_isXInputDevice, (uint32_t index))
