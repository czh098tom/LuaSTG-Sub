#include "CLRBinding/CLRBinding.hpp"
#include "AppFrame.h"
#include "windows/XInput.hpp"
#include "utf8.hpp"

// 外部模块 API 实现（XInput / DirectInput）
// - XInput 对应 LuaBinding/external/lua_xinput.cpp，引擎封装位于
//   engine/win32/windows/XInput.cpp（动态加载 XInput DLL，维护 valid/state 缓存）；
//   设备索引为 0 基（Lua 侧为 1 基，Lua 绑定层已减一，此处直接使用引擎索引）。
// - DirectInput 对应 LuaBinding/LW_DInput.cpp，通过 LAPP.GetDInput() 访问
//   Platform::DirectInput；结构体结果平铺到调用方缓冲区。
// - random 库与 lstg.Rand（well512）为纯算法，直接在 C# 侧
//   （CSharp/LuaSTG/LuaSTG.Core/Rng/）实现，不经本文件。

namespace luastg
{
	namespace
	{
		namespace xinput = Platform::XInput;
	}

	// =========================================================================
	// XInput
	// =========================================================================

	uint8_t CLRBinding::xinput_isConnected(int32_t const index)
	{
		return xinput::isConnected(index) ? 1u : 0u;
	}

	int32_t CLRBinding::xinput_refresh()
	{
		return xinput::refresh();
	}

	void CLRBinding::xinput_update()
	{
		xinput::update();
	}

	uint8_t CLRBinding::xinput_getKeyState(int32_t const index, int32_t const key)
	{
		return xinput::getKeyState(index, key) ? 1u : 0u;
	}

	uint8_t CLRBinding::xinput_getKeyStateAny(int32_t const key)
	{
		return xinput::getKeyState(key) ? 1u : 0u;
	}

	float CLRBinding::xinput_getLeftTrigger(int32_t const index)
	{
		return xinput::getLeftTrigger(index);
	}

	float CLRBinding::xinput_getLeftTriggerAny()
	{
		return xinput::getLeftTrigger();
	}

	float CLRBinding::xinput_getRightTrigger(int32_t const index)
	{
		return xinput::getRightTrigger(index);
	}

	float CLRBinding::xinput_getRightTriggerAny()
	{
		return xinput::getRightTrigger();
	}

	float CLRBinding::xinput_getLeftThumbX(int32_t const index)
	{
		return xinput::getLeftThumbX(index);
	}

	float CLRBinding::xinput_getLeftThumbXAny()
	{
		return xinput::getLeftThumbX();
	}

	float CLRBinding::xinput_getLeftThumbY(int32_t const index)
	{
		return xinput::getLeftThumbY(index);
	}

	float CLRBinding::xinput_getLeftThumbYAny()
	{
		return xinput::getLeftThumbY();
	}

	float CLRBinding::xinput_getRightThumbX(int32_t const index)
	{
		return xinput::getRightThumbX(index);
	}

	float CLRBinding::xinput_getRightThumbXAny()
	{
		return xinput::getRightThumbX();
	}

	float CLRBinding::xinput_getRightThumbY(int32_t const index)
	{
		return xinput::getRightThumbY(index);
	}

	float CLRBinding::xinput_getRightThumbYAny()
	{
		return xinput::getRightThumbY();
	}

	// =========================================================================
	// DirectInput
	// =========================================================================

	uint32_t CLRBinding::dinput_count()
	{
		auto const self = LAPP.GetDInput();
		return self ? self->count() : 0u;
	}

	uint32_t CLRBinding::dinput_refresh()
	{
		auto const self = LAPP.GetDInput();
		return self ? self->refresh() : 0u;
	}

	void CLRBinding::dinput_update()
	{
		auto const self = LAPP.GetDInput();
		if (self)
		{
			self->update();
		}
	}

	void CLRBinding::dinput_reset()
	{
		auto const self = LAPP.GetDInput();
		if (self)
		{
			self->reset();
		}
	}

	uint8_t CLRBinding::dinput_getAxisRange(uint32_t const index, int32_t* const out_range)
	{
		auto const self = LAPP.GetDInput();
		if (!self || !out_range)
		{
			return 0u;
		}
		Platform::DirectInput::AxisRange range;
		if (!self->getAxisRange(index, &range))
		{
			return 0u;
		}
		out_range[0] = range.XMin;
		out_range[1] = range.YMin;
		out_range[2] = range.ZMin;
		out_range[3] = range.XMax;
		out_range[4] = range.YMax;
		out_range[5] = range.ZMax;
		out_range[6] = range.RxMin;
		out_range[7] = range.RyMin;
		out_range[8] = range.RzMin;
		out_range[9] = range.RxMax;
		out_range[10] = range.RyMax;
		out_range[11] = range.RzMax;
		out_range[12] = range.Slider0Min;
		out_range[13] = range.Slider1Min;
		out_range[14] = range.Slider0Max;
		out_range[15] = range.Slider1Max;
		return 1u;
	}

	uint8_t CLRBinding::dinput_getRawState(uint32_t const index, int32_t* const out_axes, uint32_t* const out_pov, uint8_t* const out_buttons)
	{
		auto const self = LAPP.GetDInput();
		if (!self || !out_axes || !out_pov || !out_buttons)
		{
			return 0u;
		}
		Platform::DirectInput::RawState state;
		if (!self->getRawState(index, &state))
		{
			return 0u;
		}
		out_axes[0] = state.lX;
		out_axes[1] = state.lY;
		out_axes[2] = state.lZ;
		out_axes[3] = state.lRx;
		out_axes[4] = state.lRy;
		out_axes[5] = state.lRz;
		out_axes[6] = state.rglSlider[0];
		out_axes[7] = state.rglSlider[1];
		for (int i = 0; i < 4; i += 1)
		{
			out_pov[i] = state.rgdwPOV[i];
		}
		for (int i = 0; i < 32; i += 1)
		{
			out_buttons[i] = state.rgbButtons[i];
		}
		return 1u;
	}

	uint8_t CLRBinding::dinput_getState(uint32_t const index, int32_t* const out_state)
	{
		auto const self = LAPP.GetDInput();
		if (!self || !out_state)
		{
			return 0u;
		}
		Platform::DirectInput::State state;
		if (!self->getState(index, &state))
		{
			return 0u;
		}
		out_state[0] = static_cast<int32_t>(state.wButtons);
		out_state[1] = static_cast<int32_t>(state.bLeftTrigger);
		out_state[2] = static_cast<int32_t>(state.bRightTrigger);
		out_state[3] = static_cast<int32_t>(state.sThumbLX);
		out_state[4] = static_cast<int32_t>(state.sThumbLY);
		out_state[5] = static_cast<int32_t>(state.sThumbRX);
		out_state[6] = static_cast<int32_t>(state.sThumbRY);
		return 1u;
	}

	namespace
	{
		// 设备名/产品名的返回缓冲（C# 侧立即拷贝；两个缓冲相互独立，
		// 与 Lua 侧每调用生成临时 std::string 的生存期策略等价）
		std::string g_dinput_device_name_buffer;
		std::string g_dinput_product_name_buffer;
	}

	const char* CLRBinding::dinput_getDeviceName(uint32_t const index)
	{
		auto const self = LAPP.GetDInput();
		if (!self)
		{
			return nullptr;
		}
		wchar_t const* const name = self->getDeviceName(index);
		if (!name)
		{
			return nullptr;
		}
		try
		{
			g_dinput_device_name_buffer = utf8::to_string(name);
			return g_dinput_device_name_buffer.c_str();
		}
		catch (...)
		{
			return nullptr;
		}
	}

	const char* CLRBinding::dinput_getProductName(uint32_t const index)
	{
		auto const self = LAPP.GetDInput();
		if (!self)
		{
			return nullptr;
		}
		wchar_t const* const name = self->getProductName(index);
		if (!name)
		{
			return nullptr;
		}
		try
		{
			g_dinput_product_name_buffer = utf8::to_string(name);
			return g_dinput_product_name_buffer.c_str();
		}
		catch (...)
		{
			return nullptr;
		}
	}

	uint8_t CLRBinding::dinput_isXInputDevice(uint32_t const index)
	{
		auto const self = LAPP.GetDInput();
		if (!self)
		{
			return 0u;
		}
		return self->isXInputDevice(index) ? 1u : 0u;
	}
}
