// LuaSTG CoreCLR 绑定：核心 API 列表
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（见 tool/clr-api-generator/generate.py）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）

// 日志与信息
DECLARE_CLR_API(void, log, (int32_t level, const char* text))
DECLARE_CLR_API(const void*, getGameObjectLayout, ())
DECLARE_CLR_API(uint32_t, getVersionMajor, ())
DECLARE_CLR_API(uint32_t, getVersionMinor, ())
DECLARE_CLR_API(uint32_t, getVersionPatch, ())
DECLARE_CLR_API(const char*, getVersionName, ())
DECLARE_CLR_API(const char*, getBranchName, ())

// 框架设置
DECLARE_CLR_API(void, setWindowed, (uint8_t value))
DECLARE_CLR_API(void, setVsync, (uint8_t value))
DECLARE_CLR_API(void, setResolution, (uint32_t width, uint32_t height))
DECLARE_CLR_API(void, setTargetFPS, (uint32_t fps))
DECLARE_CLR_API(double, getFPS, ())
DECLARE_CLR_API(void, setWindowTitle, (const char* title))
DECLARE_CLR_API(void, setSplash, (uint8_t value))
DECLARE_CLR_API(void, setPreferenceGPU, (const char* gpu_name))
// 通过引擎文件系统读取文本文件（对应 lstg.LoadTextFile），packname 可为空指针
// 返回 UTF-8 文本指针（引擎静态缓冲，下一次调用前有效），失败返回空指针
DECLARE_CLR_API(const char*, loadTextFile, (const char* path, const char* packname))

// 渲染基础
DECLARE_CLR_API(uint8_t, beginScene, ())
DECLARE_CLR_API(uint8_t, endScene, ())
DECLARE_CLR_API(void, renderClear, (uint8_t a, uint8_t r, uint8_t g, uint8_t b))
