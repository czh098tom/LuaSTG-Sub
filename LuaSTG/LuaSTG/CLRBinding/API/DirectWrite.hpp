// LuaSTG CoreCLR 绑定：DirectWrite 文字排版（对应 LuaBinding/external/lua_dwrite.cpp）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 约定：
// - 句柄（uintptr_t）是 C++ 侧堆分配的 DirectWrite 对象包装结构的指针（CLRDirectWrite.cpp 中定义），
//   由 dwrite_*Destroy 销毁；0 表示创建失败或已销毁
// - 颜色统一 uint32_t ARGB（内存布局 0xAARRGGBB，core::Color4B(uint32_t) 构造）
// - 枚举参数为 int32_t，取 DirectWrite 原生枚举值（C# 侧 DirectWrite.FontStretch 等）
// - DirectWrite 使用的字符串（字体族名 / 区域名 / 文本 / 输出文件路径）为 UTF-16 const char16_t*；
//   引擎资源名与文件系统路径为 UTF-8 const char*
// - 返回 uint8_t 的函数为错误码：0 = 成功；各函数的错误码含义见注释
//   （Lua 侧 luaL_error 的场景改为错误码返回，由 C# 侧抛出异常）
// - Lua 侧 DirectWrite.CreateTextMetrics / CreateOverhangMetrics 创建的是纯字段 userdata，
//   C# 侧对应托管 struct（LuaSTG.Core.TextMetrics / OverhangMetrics），无需引擎 API
// - 底层工厂（WIC/D2D1/DWrite/自定义字体文件加载器）在 CLRDirectWrite.cpp 内部惰性初始化，
//   不单独暴露（对应 Lua 侧 luaopen_dwrite 时创建的 DirectWrite.Factory）

// ============ 模块函数（DirectWrite.* → C# LuaSTG.Core.DirectWrite） ============

// DirectWrite.CreateFontCollection：从字体文件列表创建自定义字体集（列表内路径为 UTF-8，可来自打包文件系统）
// 返回句柄，0 = 失败（DirectWrite 组件未初始化或字体集创建失败）
DECLARE_CLR_API(uintptr_t, dwrite_createFontCollection, (const char** paths, uint32_t count))
// DirectWrite.CreateTextFormat：创建文本格式；font_collection 传 0 表示使用系统字体集
// 返回句柄，0 = 失败（IDWriteFactory::CreateTextFormat 失败）
DECLARE_CLR_API(uintptr_t, dwrite_createTextFormat, (const char16_t* family_name, uintptr_t font_collection, int32_t font_weight, int32_t font_style, int32_t font_stretch, float font_size, const char16_t* locale_name))
// DirectWrite.CreateTextLayout：从文本与格式创建文本布局（text 为 UTF-16，length 为字符数）
// 返回句柄，0 = 失败（IDWriteFactory::CreateTextLayout 失败）
DECLARE_CLR_API(uintptr_t, dwrite_createTextLayout, (const char16_t* text, uint32_t length, uintptr_t text_format, float max_width, float max_height))
// DirectWrite.CreateTextRenderer：创建文本渲染器（仅状态字段，无引擎对象）
// 返回句柄，0 = 失败（内存分配失败）
DECLARE_CLR_API(uintptr_t, dwrite_createTextRenderer, ())
// DirectWrite.CreateTextureFromTextLayout：将文本布局栅格化为纹理并放入资源池
// pool_type 取 luastg::ResourcePoolType（1 = Global，2 = Stage）；
// outline_width > 0.0001 时启用描边（与 Lua 侧条件一致），否则不描边
// 颜色为 ARGB，缺省值（Lua 侧未传时）由 C# 重载补：font_color = 白色，outline_color = 黑色
// 错误码：0 = 成功；1 = 无效资源池类型；2 = 纹理已存在；3 = 创建画布图像失败；
//         4 = 创建栅格化器失败；5 = 栅格化失败；6 = 创建纹理失败；7 = 上传纹理数据失败；8 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_createTextureFromTextLayout, (uintptr_t text_layout, int32_t pool_type, const char* texture_name, float outline_width, uint32_t font_color_argb, uint32_t outline_color_argb))
// DirectWrite.SaveTextLayoutToFile：将文本布局栅格化并保存为 PNG 文件（file_path 为 UTF-16 绝对/相对路径）
// use_outline 对应 Lua 侧是否传入第 3 个参数（gettop >= 3），传 1 时走描边分支
// 错误码：0 = 成功；1 = 创建位图失败；2 = 创建栅格化器失败；3 = 栅格化失败；4 = 保存失败；5 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_saveTextLayoutToFile, (uintptr_t text_layout, const char16_t* file_path, float outline_width, uint8_t use_outline))

// ============ FontCollection 对象（DirectWrite.FontCollection） ============

// 销毁 FontCollection（对应 Lua userdata 的 __gc，注销字体集加载器）
DECLARE_CLR_API(void, dwrite_fontCollectionDestroy, (uintptr_t font_collection))
// FontCollection:GetDebugInformation：输出字体集详细信息（UTF-8，线程局部缓冲，C# 立即拷贝）
// 失败（句柄无效）返回空串
DECLARE_CLR_API(const char*, dwrite_fontCollectionGetDebugInformation, (uintptr_t font_collection))

// ============ TextFormat 对象（DirectWrite.TextFormat） ============

// 销毁 TextFormat（对应 Lua userdata 的 __gc）
DECLARE_CLR_API(void, dwrite_textFormatDestroy, (uintptr_t text_format))

// ============ TextLayout 对象（DirectWrite.TextLayout） ============
// 带 position/length 参数的函数对应 DWRITE_TEXT_RANGE{position, length}

// 销毁 TextLayout（对应 Lua userdata 的 __gc）
DECLARE_CLR_API(void, dwrite_textLayoutDestroy, (uintptr_t text_layout))
// TextLayout:SetFontCollection
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 任一句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetFontCollection, (uintptr_t text_layout, uintptr_t font_collection, uint32_t position, uint32_t length))
// TextLayout:SetFontFamilyName（name 为 UTF-16）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetFontFamilyName, (uintptr_t text_layout, const char16_t* name, uint32_t position, uint32_t length))
// TextLayout:SetLocaleName（name 为 UTF-16）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetLocaleName, (uintptr_t text_layout, const char16_t* name, uint32_t position, uint32_t length))
// TextLayout:SetFontSize
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetFontSize, (uintptr_t text_layout, float font_size, uint32_t position, uint32_t length))
// TextLayout:SetFontStyle（DWRITE_FONT_STYLE 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetFontStyle, (uintptr_t text_layout, int32_t font_style, uint32_t position, uint32_t length))
// TextLayout:SetFontWeight（DWRITE_FONT_WEIGHT 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetFontWeight, (uintptr_t text_layout, int32_t font_weight, uint32_t position, uint32_t length))
// TextLayout:SetFontStretch（DWRITE_FONT_STRETCH 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetFontStretch, (uintptr_t text_layout, int32_t font_stretch, uint32_t position, uint32_t length))
// TextLayout:SetStrikethrough
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetStrikethrough, (uintptr_t text_layout, uint8_t enable, uint32_t position, uint32_t length))
// TextLayout:SetUnderline
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetUnderline, (uintptr_t text_layout, uint8_t enable, uint32_t position, uint32_t length))
// TextLayout:SetIncrementalTabStop
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetIncrementalTabStop, (uintptr_t text_layout, float tab_size))
// TextLayout:SetLineSpacing（method 为 DWRITE_LINE_SPACING_METHOD 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetLineSpacing, (uintptr_t text_layout, int32_t method, float line_spacing, float baseline))
// TextLayout:SetTextAlignment（DWRITE_TEXT_ALIGNMENT 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetTextAlignment, (uintptr_t text_layout, int32_t align))
// TextLayout:SetParagraphAlignment（DWRITE_PARAGRAPH_ALIGNMENT 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetParagraphAlignment, (uintptr_t text_layout, int32_t align))
// TextLayout:SetFlowDirection（DWRITE_FLOW_DIRECTION 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetFlowDirection, (uintptr_t text_layout, int32_t direction))
// TextLayout:SetReadingDirection（DWRITE_READING_DIRECTION 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetReadingDirection, (uintptr_t text_layout, int32_t direction))
// TextLayout:SetWordWrapping（DWRITE_WORD_WRAPPING 枚举值）
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetWordWrapping, (uintptr_t text_layout, int32_t wrapping))
// TextLayout:SetMaxWidth
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetMaxWidth, (uintptr_t text_layout, float max_width))
// TextLayout:SetMaxHeight
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutSetMaxHeight, (uintptr_t text_layout, float max_height))
// TextLayout:DetermineMinWidth
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutDetermineMinWidth, (uintptr_t text_layout, float* out_min_width))
// TextLayout:GetMetrics：填充 9 个 double（C# 侧 TextMetrics 字段顺序）
// 顺序：left, top, width, widthIncludingTrailingWhitespace, height,
//       layoutWidth, layoutHeight, maxBidiReorderingDepth, lineCount
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutGetMetrics, (uintptr_t text_layout, double* out_metrics))
// TextLayout:GetOverhangMetrics：填充 4 个 double（C# 侧 OverhangMetrics 字段顺序）
// 顺序：left, top, right, bottom
// 错误码：0 = 成功；1 = 引擎调用失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textLayoutGetOverhangMetrics, (uintptr_t text_layout, double* out_metrics))
// TextLayout:GetMaxHeight（句柄无效时返回 0）
DECLARE_CLR_API(float, dwrite_textLayoutGetMaxHeight, (uintptr_t text_layout))
// TextLayout:GetMaxWidth（句柄无效时返回 0）
DECLARE_CLR_API(float, dwrite_textLayoutGetMaxWidth, (uintptr_t text_layout))

// ============ TextRenderer 对象（DirectWrite.TextRenderer） ============

// 销毁 TextRenderer（对应 Lua userdata 的 __gc）
DECLARE_CLR_API(void, dwrite_textRendererDestroy, (uintptr_t text_renderer))
// TextRenderer:SetTextColor（ARGB）
DECLARE_CLR_API(void, dwrite_textRendererSetTextColor, (uintptr_t text_renderer, uint32_t argb))
// TextRenderer:SetTextOutlineColor（ARGB）
DECLARE_CLR_API(void, dwrite_textRendererSetTextOutlineColor, (uintptr_t text_renderer, uint32_t argb))
// TextRenderer:SetTextOutlineWidth
DECLARE_CLR_API(void, dwrite_textRendererSetTextOutlineWidth, (uintptr_t text_renderer, float width))
// TextRenderer:SetShadowColor（ARGB）
DECLARE_CLR_API(void, dwrite_textRendererSetShadowColor, (uintptr_t text_renderer, uint32_t argb))
// TextRenderer:SetShadowRadius
DECLARE_CLR_API(void, dwrite_textRendererSetShadowRadius, (uintptr_t text_renderer, float radius))
// TextRenderer:SetShadowExtend
DECLARE_CLR_API(void, dwrite_textRendererSetShadowExtend, (uintptr_t text_renderer, float extend))
// TextRenderer:Render：将文本布局渲染到渲染目标纹理（texture_name 为 UTF-8 资源名）
// 错误码：0 = 成功；1 = 纹理不存在；2 = 纹理不是渲染目标；3 = 渲染失败；4 = 句柄无效
DECLARE_CLR_API(uint8_t, dwrite_textRendererRender, (uintptr_t text_renderer, const char* texture_name, uintptr_t text_layout, float offset_x, float offset_y))
