#include "CLRBinding/CLRBinding.hpp"
#include "AppFrame.h"

// 输入 API 实现（对应 LuaBinding/LW_Input.cpp）
// 引擎侧说明：
// - 键盘状态为按虚拟键码索引的位图，GetKeyState(int) 直接按 VK 查询；
// - 鼠标状态为 DirectXTK Mouse::State，GetMouseState(int) 按 VK 查询，
//   GetMouseState_legacy(int) 为旧版 0..4 索引；
// - GetMousePosition 内部做窗口->画布的 letterbox/stretch 变换。

namespace luastg
{
	uint8_t CLRBinding::input_getKeyState(int32_t const vk_code)
	{
		return LAPP.GetKeyState(vk_code) ? 1u : 0u;
	}

	int32_t CLRBinding::input_getLastKey()
	{
		return LAPP.GetLastKey();
	}

	uint8_t CLRBinding::input_getMouseState(int32_t const vk_button)
	{
		return LAPP.GetMouseState(vk_button) ? 1u : 0u;
	}

	uint8_t CLRBinding::input_getMouseStateLegacy(int32_t const button)
	{
		return LAPP.GetMouseState_legacy(button) ? 1u : 0u;
	}

	void CLRBinding::input_getMousePosition(uint8_t const no_flip, double* const x, double* const y)
	{
		core::Vector2F const tPos = LAPP.GetMousePosition(no_flip != 0);
		if (x)
			*x = static_cast<double>(tPos.x);
		if (y)
			*y = static_cast<double>(tPos.y);
	}

	int32_t CLRBinding::input_getMouseWheelDelta()
	{
		return LAPP.GetMouseWheelDelta();
	}
}
