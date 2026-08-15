// LuaSTG CoreCLR 绑定：输入（对应 LW_Input，含键盘/鼠标）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）

// 键盘（对应 lstg.Input.Keyboard.GetKeyState 与兼容 API lstg.GetKeyState）
// 参数为 Windows 虚拟键码（C# 侧 KeyCode 枚举的底层值）
DECLARE_CLR_API(uint8_t, input_getKeyState, (int32_t vk_code))
// 已废弃：对应兼容 API lstg.GetLastKey，返回最后按下的虚拟键码
DECLARE_CLR_API(int32_t, input_getLastKey, ())

// 鼠标（对应 lstg.Input.Mouse 与兼容 API lstg.GetMouseState）
// 参数为鼠标键虚拟键码（VK_LBUTTON/VK_RBUTTON/VK_MBUTTON/VK_XBUTTON1/VK_XBUTTON2）
DECLARE_CLR_API(uint8_t, input_getMouseState, (int32_t vk_button))
// 已废弃：对应兼容 API lstg.GetMouseState，参数为旧版索引（0=左 1=中 2=右 3=X1 4=X2）
DECLARE_CLR_API(uint8_t, input_getMouseStateLegacy, (int32_t button))
// 对应 lstg.Input.Mouse.GetPosition 与兼容 API lstg.GetMousePosition
// 坐标经过画布缩放变换，no_flip 为假时 y 翻转为 LuaSTG 世界坐标（原点左下角）
DECLARE_CLR_API(void, input_getMousePosition, (uint8_t no_flip, double* x, double* y))
// 对应兼容 API lstg.GetMouseWheelDelta，返回自上帧起的原始滚轮增量（单位 1/120 格）
// lstg.Input.Mouse.GetWheelDelta 在 C# 侧除以 120 得到“格”数
DECLARE_CLR_API(int32_t, input_getMouseWheelDelta, ())
