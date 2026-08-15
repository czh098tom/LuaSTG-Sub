// LuaSTG CoreCLR 绑定：资源管理（对应 LW_ResourceMgr.cpp 与 LuaBinding/Resource.cpp）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 约定：
// - 资源池类型 int32_t pool_type：0 = None，1 = Global，2 = Stage（与 ResourcePoolType 一致）
// - 资源类型 int32_t res_type：取值与 luastg::ResourceType 一致（Texture=1 ... Model=10）
// - 混合模式 uint8_t blend：取 luastg::BlendMode 枚举值（C# 侧 BlendMode）
// - 颜色统一 uint32_t ARGB（内存布局 0xAARRGGBB，core::Color4B(uint32_t) 构造）
// - 引擎对象句柄 uintptr_t：借用的引擎资源对象指针（归资源池所有，C# 侧不得释放），
//   0 表示失败/不存在；句柄仅在资源仍存在于池中时有效
// - 加载/创建类 API 均指定目标资源池（pool_type 为 None/非法时返回 0），
//   兼容 API 的“当前资源池”由 C# 侧先查询 res_getPoolStatus 得到
// - 返回的 const char* 指向引擎侧线程局部缓冲，C# 侧必须在下一次调用前拷贝
// - 错误码（int32_t/uint8_t 返回值）通用含义：0 = 成功；1 = 资源不存在/未找到；
//   2 = 操作非法（如对未克隆精灵的动画设置缩放）；3 = 参数非法
// - 采样器状态 uint8_t sampler：取 core::Graphics::IRenderer::SamplerState 枚举值
//   （PointWrap=0, PointClamp=1, LinearWrap=4, LinearClamp=5 ...），空串默认 LinearClamp

// ===== 资源池状态与全局状态 =====
// 当前激活的资源池类型（对应 lstg.GetResourceStatus / getCurrentResourceCollection）
DECLARE_CLR_API(int32_t, res_getPoolStatus, ())
// 设置激活的资源池（对应 lstg.SetResourceStatus / setCurrentResourceCollection）
// 错误码：0 = 成功；3 = 非法 pool_type
DECLARE_CLR_API(uint8_t, res_setPoolStatus, (int32_t pool_type))
// 全局图像缩放系数（对应 lstg.SetImageScale/GetImageScale 无名形式）
// 设置时 scale 为 0 返回 1（与 Lua 侧 luaL_error 场景对应）
DECLARE_CLR_API(double, res_getGlobalImageScale, ())
DECLARE_CLR_API(uint8_t, res_setGlobalImageScale, (double scale))
// 资源加载日志开关（对应 lstg.SetResLoadInfo）
DECLARE_CLR_API(void, res_setResourceLoadingLog, (uint8_t enable))
DECLARE_CLR_API(uint8_t, res_getResourceLoadingLog, ())

// ===== 资源查找（关卡池优先，再到全局池，与 LRES.Find* 一致）=====
// 返回借用句柄，0 = 不存在
DECLARE_CLR_API(uintptr_t, res_findTexture, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findSprite, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findAnimation, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findMusic, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findSoundEffect, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findParticle, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findSpriteFont, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findTrueTypeFont, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findFX, (const char* name))
DECLARE_CLR_API(uintptr_t, res_findModel, (const char* name))
// 按句柄取资源信息（对应现代类型 API 的 getResourceType/getResourceName）
// 资源名指向引擎侧线程局部缓冲，C# 侧必须在下一次调用前拷贝；句柄为 0 时返回空串/0
DECLARE_CLR_API(int32_t, res_getResourceType, (uintptr_t handle))
DECLARE_CLR_API(const char*, res_getResourceName, (uintptr_t handle))

// ===== 指定资源池的查找/存在性判断（对应 ResourceCollection.get*/is*Exist）=====
DECLARE_CLR_API(uintptr_t, res_poolGetTexture, (int32_t pool_type, const char* name))
DECLARE_CLR_API(uintptr_t, res_poolGetSprite, (int32_t pool_type, const char* name))
DECLARE_CLR_API(uintptr_t, res_poolGetAnimation, (int32_t pool_type, const char* name))
// 对应 ResourcePool::CheckResourceExists；pool_type 非法时返回 0
DECLARE_CLR_API(uint8_t, res_poolCheckResourceExists, (int32_t pool_type, int32_t res_type, const char* name))
// 对应 lstg.CheckRes：先查全局池再查关卡池，返回资源所在池类型（0 = 不存在）
DECLARE_CLR_API(int32_t, res_checkRes, (int32_t res_type, const char* name))

// ===== 资源移除 =====
// 对应 lstg.RemoveResource 三参数形式（从指定池移除单个资源）
// 错误码：0 = 成功（含资源不存在时引擎仅记日志的情况）；3 = 非法 pool_type
DECLARE_CLR_API(uint8_t, res_removeResource, (int32_t pool_type, int32_t res_type, const char* name))
// 对应 lstg.RemoveResource 单参数形式（清空指定池）
// 错误码：0 = 成功；3 = 非法 pool_type（None 按引擎行为为空操作，返回 0）
DECLARE_CLR_API(uint8_t, res_clearResourcePool, (int32_t pool_type))

// ===== 资源枚举（对应 lstg.EnumRes）=====
// 指定池中指定类型资源的数量；pool_type/res_type 非法时返回 0
DECLARE_CLR_API(uint32_t, res_enumResCount, (int32_t pool_type, int32_t res_type))
// 按索引取资源名（0 起始），越界或参数非法时返回空串
DECLARE_CLR_API(const char*, res_enumResNameByIndex, (int32_t pool_type, int32_t res_type, uint32_t index))

// ===== 加载/创建（返回借用句柄，0 = 失败；错误细节见引擎日志）=====
// 对应 lstg.LoadTexture / ResourceCollection.createTextureFromFile
DECLARE_CLR_API(uintptr_t, res_loadTexture, (int32_t pool_type, const char* name, const char* path, uint8_t mipmaps))
// 对应 lstg.LoadImage（坐标与尺寸为纹理像素坐标，left/top/width/height）
DECLARE_CLR_API(uintptr_t, res_createSprite, (int32_t pool_type, const char* name, const char* texname, double x, double y, double w, double h, double a, double b, uint8_t rect))
// 对应 lstg.CopyImage
DECLARE_CLR_API(uintptr_t, res_copySprite, (int32_t pool_type, const char* name, const char* src_name))
// 对应 lstg.LoadAnimation 的纹理形式（n/m 为列数/行数，intv 为帧间隔）
DECLARE_CLR_API(uintptr_t, res_createAnimationFromTexture, (int32_t pool_type, const char* name, const char* texname, double x, double y, double w, double h, int32_t n, int32_t m, int32_t intv, double a, double b, uint8_t rect))
// 对应 lstg.LoadAnimation 的精灵列表形式（sprite_names 为 count 个精灵资源名，按 LRES.FindSprite 解析）
DECLARE_CLR_API(uintptr_t, res_createAnimationFromSprites, (int32_t pool_type, const char* name, const char** sprite_names, uint32_t count, int32_t intv, double a, double b, uint8_t rect))
// 对应 lstg.CreateRenderTarget；width/height 传 0 表示使用当前屏幕尺寸
DECLARE_CLR_API(uintptr_t, res_createRenderTarget, (int32_t pool_type, const char* name, int32_t width, int32_t height, uint8_t depth_buffer))
// 对应 lstg.LoadMusic；loop_start = max(0, loop_end - loop_duration)（C++ 侧计算）
DECLARE_CLR_API(uintptr_t, res_loadMusic, (int32_t pool_type, const char* name, const char* path, double loop_end, double loop_duration, uint8_t once_decode))
// 对应 lstg.LoadSound
DECLARE_CLR_API(uintptr_t, res_loadSoundEffect, (int32_t pool_type, const char* name, const char* path))
// 对应 lstg.LoadPS 的文件形式（HGE psi 定义文件）
DECLARE_CLR_API(uintptr_t, res_loadParticle, (int32_t pool_type, const char* name, const char* path, const char* img_name, double a, double b, uint8_t rect))
// 对应 lstg.LoadPS 的 table 形式。values 为 value_count(=30) 个 double，顺序为：
//   [0]emission [1]lifetime [2]direction(角度制) [3]spread(角度制)
//   [4]lifetime_min [5]lifetime_max [6]speed_min [7]speed_max
//   [8]gravity_min [9]gravity_max [10]radial_min [11]radial_max
//   [12]tangential_min [13]tangential_max [14]size_begin [15]size_end [16]size_var
//   [17]angle_begin(角度制) [18]angle_end(角度制) [19]angle_var(与 Lua 侧一致不做角度转换)
//   [20]color_var [21]alpha_var
//   [22..25]color_begin(r,g,b,a) [26..29]color_end(r,g,b,a)
// 角度制分量在 C++ 侧转换为弧度（与 Lua 侧一致）；blend_alpha 非 0 时混合为 "alpha"(6<<16)，
// 否则为 "add"(4<<16)（与 Lua 侧默认一致）；value_count != 30 时返回 0
DECLARE_CLR_API(uintptr_t, res_loadParticleFromInfo, (int32_t pool_type, const char* name, const double* values, uint32_t value_count, const char* img_name, double a, double b, uint8_t blend_alpha, uint8_t rect))
// 对应 lstg.LoadFont 的 HGE 字体形式
DECLARE_CLR_API(uintptr_t, res_loadSpriteFont, (int32_t pool_type, const char* name, const char* path, uint8_t mipmaps))
// 对应 lstg.LoadFont 的 fancy2d 字体形式（字体定义 + 纹理文件）
DECLARE_CLR_API(uintptr_t, res_loadSpriteFontWithTexture, (int32_t pool_type, const char* name, const char* path, const char* tex_path, uint8_t mipmaps))
// 对应 lstg.LoadTTF
DECLARE_CLR_API(uintptr_t, res_loadTTFFont, (int32_t pool_type, const char* name, const char* path, float width, float height))
// 对应 lstg.LoadTrueTypeFont；sources/font_faces/sizes 均为 count 个元素，sizes 每项 2 个 float（宽、高）
DECLARE_CLR_API(uintptr_t, res_loadTrueTypeFont, (int32_t pool_type, const char* name, const char** sources, const uint32_t* font_faces, const float* sizes, uint32_t count))
// 对应 lstg.LoadFX
DECLARE_CLR_API(uintptr_t, res_loadFX, (int32_t pool_type, const char* name, const char* path))
// 对应 lstg.LoadModel
DECLARE_CLR_API(uintptr_t, res_loadModel, (int32_t pool_type, const char* name, const char* path))

// ===== 纹理对象访问（handle 为 res_findTexture/res_loadTexture 等返回的借用句柄）=====
// 错误码：0 = 成功；1 = 句柄为空
DECLARE_CLR_API(uint8_t, res_tex_getSize, (uintptr_t handle, uint32_t* out_width, uint32_t* out_height))
// 是否为渲染目标（对应 lstg.IsRenderTarget，按名查找的部分由 C# 侧组合实现）
DECLARE_CLR_API(uint8_t, res_tex_isRenderTarget, (uintptr_t handle))
// 对应 lstg.SetTexturePreMulAlphaState
DECLARE_CLR_API(uint8_t, res_tex_setPreMulAlpha, (uintptr_t handle, uint8_t enable))
// 对应 lstg.SetTextureSamplerState
// 错误码：0 = 成功；1 = 句柄为空；2 = 非法 sampler 值
DECLARE_CLR_API(uint8_t, res_tex_setSamplerState, (uintptr_t handle, uint8_t sampler))
// 对应 lstg.GetTextureSize（按资源名，关卡池优先查找）
// 错误码：0 = 成功；1 = 纹理不存在
DECLARE_CLR_API(uint8_t, res_getTextureSize, (const char* name, uint32_t* out_width, uint32_t* out_height))

// ===== 精灵对象访问 =====
// 对应 lstg.SetImageCenter / ResourceSprite.setCenter
DECLARE_CLR_API(void, res_sprite_setCenter, (uintptr_t handle, double x, double y))
// 对应 lstg.SetImageScale/GetImageScale 的带名形式 / ResourceSprite.setUnitsPerPixel
DECLARE_CLR_API(void, res_sprite_setUnitsPerPixel, (uintptr_t handle, double value))
DECLARE_CLR_API(double, res_sprite_getUnitsPerPixel, (uintptr_t handle))
// 对应 lstg.GetImageSize：返回纹理裁剪矩形的宽高
DECLARE_CLR_API(uint8_t, res_sprite_getSize, (uintptr_t handle, double* out_width, double* out_height))
// 对应 lstg.SetImageState：混合模式 + 四角顶点色（单色形式由 C# 侧复制 4 份）
DECLARE_CLR_API(void, res_sprite_setBlendMode, (uintptr_t handle, uint8_t blend))
DECLARE_CLR_API(void, res_sprite_setColor, (uintptr_t handle, uint32_t c1, uint32_t c2, uint32_t c3, uint32_t c4))

// ===== 动画对象访问 =====
// 动画包含的精灵数量
DECLARE_CLR_API(uint32_t, res_anim_getCount, (uintptr_t handle))
// 按索引取动画中的精灵（借用句柄，0 = 越界）
DECLARE_CLR_API(uintptr_t, res_anim_getSprite, (uintptr_t handle, uint32_t index))
// 动画是否克隆了精灵（决定批量设置接口是否可用）
DECLARE_CLR_API(uint8_t, res_anim_isSpriteCloned, (uintptr_t handle))
// 对应 lstg.SetAnimationScale/GetAnimationScale；未克隆精灵时返回 2（Lua 侧 luaL_error 场景）
DECLARE_CLR_API(int32_t, res_anim_setScale, (uintptr_t handle, double value))
DECLARE_CLR_API(int32_t, res_anim_getScale, (uintptr_t handle, double* out_value))
// 对应 lstg.SetAnimationState / SetAnimationCenter
DECLARE_CLR_API(void, res_anim_setBlendMode, (uintptr_t handle, uint8_t blend))
DECLARE_CLR_API(void, res_anim_setVertexColor, (uintptr_t handle, uint32_t c1, uint32_t c2, uint32_t c3, uint32_t c4))
DECLARE_CLR_API(int32_t, res_anim_setCenter, (uintptr_t handle, double x, double y))

// ===== 字体对象访问 =====
// 对应 lstg.SetFontState
DECLARE_CLR_API(void, res_font_setBlendMode, (uintptr_t handle, uint8_t blend))
DECLARE_CLR_API(void, res_font_setBlendColor, (uintptr_t handle, uint32_t argb))
// 对应 lstg.CacheTTFString（字体不存在时引擎记录日志，与 Lua 侧行为一致）
DECLARE_CLR_API(void, res_cacheTTFString, (const char* name, const char* text))
