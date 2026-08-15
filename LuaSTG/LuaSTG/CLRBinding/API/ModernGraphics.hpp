// LuaSTG CoreCLR 绑定：现代图形类（对应 LuaBinding/modern/ 下的对象绑定）
// 覆盖：RenderTarget / DepthStencilBuffer / Texture2D / Mesh / MeshRenderer /
//       VideoDecoder / Sprite / SpriteRenderer / SpriteRectRenderer / SpriteQuadRenderer
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 约定：
// - 对象句柄 uintptr_t：C++ 侧堆分配的包装结构指针（内含 core::SmartReference 持有引擎对象引用），
//   0 表示失败/空；各 *Release 负责 delete 包装结构并释放引擎引用
// - 对象创建失败返回 0 并记录引擎日志，C# 侧抛异常
// - 颜色统一 uint32_t ARGB（内存布局 0xAARRGGBB，core::Color4B(uint32_t) 构造）
// - 混合模式参数 uint8_t 取 luastg::BlendMode 枚举值（C# 侧 BlendMode）
// - 采样器 / 图元拓扑等枚举以 uint8_t 传递，值与引擎枚举一致
// - 返回 uint8_t 的函数为错误码：0 = 成功；各函数的错误码含义见注释
// - Vector2/3/4 为纯数学类型，在 C# 侧直接实现，不经过本 API 列表

// ============ RenderTarget（lstg.RenderTarget → C# LuaSTG.Core.RenderTarget） ============

// lstg.RenderTarget.create(width, height)，错误码：0 = 成功；1 = 创建失败（句柄返回 0）
DECLARE_CLR_API(uintptr_t, mg_renderTargetCreate, (uint32_t width, uint32_t height))
// lstg.RenderTarget:getWidth()（经由渲染目标纹理尺寸）
DECLARE_CLR_API(uint32_t, mg_renderTargetGetWidth, (uintptr_t handle))
// lstg.RenderTarget:getHeight()
DECLARE_CLR_API(uint32_t, mg_renderTargetGetHeight, (uintptr_t handle))
// lstg.RenderTarget:getTexture()，返回新持有的 Texture2D 句柄（已 retain，需 mg_textureRelease 释放），0 = 无纹理
DECLARE_CLR_API(uintptr_t, mg_renderTargetGetTexture, (uintptr_t handle))
// 释放句柄（对应 Lua __gc）
DECLARE_CLR_API(void, mg_renderTargetRelease, (uintptr_t handle))

// ============ DepthStencilBuffer（lstg.DepthStencilBuffer） ============

// lstg.DepthStencilBuffer.create(width, height)，创建失败返回 0
DECLARE_CLR_API(uintptr_t, mg_depthStencilCreate, (uint32_t width, uint32_t height))
// lstg.DepthStencilBuffer:getWidth()
DECLARE_CLR_API(uint32_t, mg_depthStencilGetWidth, (uintptr_t handle))
// lstg.DepthStencilBuffer:getHeight()
DECLARE_CLR_API(uint32_t, mg_depthStencilGetHeight, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_depthStencilRelease, (uintptr_t handle))

// ============ Texture2D（lstg.Texture2D） ============

// lstg.Texture2D.createFromFile(path, mipmap_levels)，mipmap_levels != 1 时生成 mipmap；失败返回 0
DECLARE_CLR_API(uintptr_t, mg_textureCreateFromFile, (const char* path, uint32_t mipmap_levels))
// lstg.Texture2D:getWidth()
DECLARE_CLR_API(uint32_t, mg_textureGetWidth, (uintptr_t handle))
// lstg.Texture2D:getHeight()
DECLARE_CLR_API(uint32_t, mg_textureGetHeight, (uintptr_t handle))
// lstg.Texture2D:setDefaultSampler(sampler)，sampler 为 IRenderer::SamplerState 枚举值
// 错误码：0 = 成功；1 = 采样器值非法
DECLARE_CLR_API(uint8_t, mg_textureSetDefaultSampler, (uintptr_t handle, uint8_t sampler))
// 释放句柄（同时用于释放 mg_renderTargetGetTexture / mg_videoGetTexture / mg_spriteGetTexture 返回的句柄）
DECLARE_CLR_API(void, mg_textureRelease, (uintptr_t handle))

// ============ Mesh（lstg.Mesh） ============

// lstg.Mesh.create{...}，MeshOptions 逐项平铺；primitive_topology 为 PrimitiveTopology 枚举值；失败返回 0
DECLARE_CLR_API(uintptr_t, mg_meshCreate, (uint32_t vertex_count, uint32_t index_count, uint8_t vertex_position_no_z, uint8_t vertex_index_compression, uint8_t vertex_color_compression, uint8_t primitive_topology))
// lstg.Mesh:getVertexCount()
DECLARE_CLR_API(uint32_t, mg_meshGetVertexCount, (uintptr_t handle))
// lstg.Mesh:getIndexCount()
DECLARE_CLR_API(uint32_t, mg_meshGetIndexCount, (uintptr_t handle))
// lstg.Mesh:getPrimitiveTopology()，返回 PrimitiveTopology 枚举值
DECLARE_CLR_API(uint8_t, mg_meshGetPrimitiveTopology, (uintptr_t handle))
// lstg.Mesh:isReadOnly()
DECLARE_CLR_API(uint8_t, mg_meshIsReadOnly, (uintptr_t handle))
// lstg.Mesh:setVertex(index, x, y, z, u, v, color)，3D 位置 + ARGB 颜色
DECLARE_CLR_API(void, mg_meshSetVertex3, (uintptr_t handle, uint32_t vertex_index, float x, float y, float z, float u, float v, uint32_t argb))
// lstg.Mesh:setVertex(index, x, y, z, u, v, r, g, b, a)，3D 位置 + RGBA 浮点颜色
DECLARE_CLR_API(void, mg_meshSetVertex3F, (uintptr_t handle, uint32_t vertex_index, float x, float y, float z, float u, float v, float r, float g, float b, float a))
// lstg.Mesh:setVertex(index, x, y, u, v, color)，2D 位置 + ARGB 颜色
DECLARE_CLR_API(void, mg_meshSetVertex2, (uintptr_t handle, uint32_t vertex_index, float x, float y, float u, float v, uint32_t argb))
// lstg.Mesh:setVertex(index, x, y, u, v, r, g, b, a)，2D 位置 + RGBA 浮点颜色
DECLARE_CLR_API(void, mg_meshSetVertex2F, (uintptr_t handle, uint32_t vertex_index, float x, float y, float u, float v, float r, float g, float b, float a))
// lstg.Mesh:setPosition(index, x, y, z)，3D 位置
DECLARE_CLR_API(void, mg_meshSetPosition3, (uintptr_t handle, uint32_t vertex_index, float x, float y, float z))
// lstg.Mesh:setPosition(index, x, y)，2D 位置
DECLARE_CLR_API(void, mg_meshSetPosition2, (uintptr_t handle, uint32_t vertex_index, float x, float y))
// lstg.Mesh:setUv(index, u, v)
DECLARE_CLR_API(void, mg_meshSetUv, (uintptr_t handle, uint32_t vertex_index, float u, float v))
// lstg.Mesh:setColor(index, color)，ARGB 颜色
DECLARE_CLR_API(void, mg_meshSetColor, (uintptr_t handle, uint32_t vertex_index, uint32_t argb))
// lstg.Mesh:setColor(index, r, g, b, a)，RGBA 浮点颜色
DECLARE_CLR_API(void, mg_meshSetColorF, (uintptr_t handle, uint32_t vertex_index, float r, float g, float b, float a))
// lstg.Mesh:setIndex(index_index, vertex_index)
DECLARE_CLR_API(void, mg_meshSetIndex, (uintptr_t handle, uint32_t index_index, uint32_t vertex_index))
// lstg.Mesh:commit()，返回是否成功
DECLARE_CLR_API(uint8_t, mg_meshCommit, (uintptr_t handle))
// lstg.Mesh:setReadOnly()
DECLARE_CLR_API(void, mg_meshSetReadOnly, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_meshRelease, (uintptr_t handle))

// ============ MeshRenderer（lstg.MeshRenderer） ============
// 句柄包装结构额外持有 position/scale/rotation_yaw_pitch_roll 状态（与 Lua userdata 一致），
// set* 时用 DirectXMath 重算 SRT 矩阵并应用到引擎

// lstg.MeshRenderer.create()（带 mesh/texture 参数的形式由 C# 侧组合 SetMesh/SetTexture 完成），失败返回 0
DECLARE_CLR_API(uintptr_t, mg_meshRendererCreate, ())
// lstg.MeshRenderer:setPosition(x, y[, z])，3 参数形式由 C# 重载补 z=0
DECLARE_CLR_API(void, mg_meshRendererSetPosition, (uintptr_t handle, float x, float y, float z))
// lstg.MeshRenderer:setScale(x, y[, z])，2 参数形式由 C# 重载补 z=1
DECLARE_CLR_API(void, mg_meshRendererSetScale, (uintptr_t handle, float x, float y, float z))
// lstg.MeshRenderer:setRotationYawPitchRoll(yaw, pitch, roll)，弧度制
DECLARE_CLR_API(void, mg_meshRendererSetRotationYawPitchRoll, (uintptr_t handle, float yaw, float pitch, float roll))
// lstg.MeshRenderer:setMesh(mesh)，mesh 句柄 0 表示清除
// 错误码：0 = 成功；2 = mesh 句柄无效
DECLARE_CLR_API(uint8_t, mg_meshRendererSetMesh, (uintptr_t handle, uintptr_t mesh))
// lstg.MeshRenderer:setTexture(texture)，texture 句柄 0 表示清除
// 错误码：0 = 成功；2 = texture 句柄无效
DECLARE_CLR_API(uint8_t, mg_meshRendererSetTexture, (uintptr_t handle, uintptr_t texture))
// lstg.MeshRenderer:setTexture(name)，按资源名从资源池查找纹理（Lua 侧字符串参数形式）
// 错误码：0 = 成功；1 = 纹理资源不存在
DECLARE_CLR_API(uint8_t, mg_meshRendererSetTextureByName, (uintptr_t handle, const char* name))
// lstg.MeshRenderer:setLegacyBlendState(blend)，blend 为 luastg::BlendMode 枚举值
DECLARE_CLR_API(void, mg_meshRendererSetLegacyBlendState, (uintptr_t handle, uint8_t blend))
// lstg.MeshRenderer:draw()，使用引擎 2D 渲染器
DECLARE_CLR_API(void, mg_meshRendererDraw, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_meshRendererRelease, (uintptr_t handle))

// ============ VideoDecoder（lstg.VideoDecoder） ============
// VideoOpenOptions 逐项平铺：video_stream（uint32_t，C# 侧默认 uint.MaxValue 表示自动）、
// width/height（0 = 原始尺寸）、premultiplied_alpha、looping、loop_end、loop_duration

// lstg.VideoDecoder.create(path[, options])，失败返回 0
DECLARE_CLR_API(uintptr_t, mg_videoCreate, (const char* path, uint32_t video_stream, uint32_t width, uint32_t height, uint8_t premultiplied_alpha, uint8_t looping, double loop_end, double loop_duration))
// lstg.VideoDecoder:getWidth()
DECLARE_CLR_API(uint32_t, mg_videoGetWidth, (uintptr_t handle))
// lstg.VideoDecoder:getHeight()
DECLARE_CLR_API(uint32_t, mg_videoGetHeight, (uintptr_t handle))
// lstg.VideoDecoder:getDuration()，秒
DECLARE_CLR_API(double, mg_videoGetDuration, (uintptr_t handle))
// lstg.VideoDecoder:getCurrentTime()，秒
DECLARE_CLR_API(double, mg_videoGetCurrentTime, (uintptr_t handle))
// lstg.VideoDecoder:getFPS()（由帧间隔换算）
DECLARE_CLR_API(double, mg_videoGetFPS, (uintptr_t handle))
// lstg.VideoDecoder:isLooping()
DECLARE_CLR_API(uint8_t, mg_videoIsLooping, (uintptr_t handle))
// lstg.VideoDecoder:getTexture()，返回新持有的 Texture2D 句柄，0 = 当前无纹理
DECLARE_CLR_API(uintptr_t, mg_videoGetTexture, (uintptr_t handle))
// lstg.VideoDecoder:seek(time)，返回是否成功
DECLARE_CLR_API(uint8_t, mg_videoSeek, (uintptr_t handle, double time))
// lstg.VideoDecoder:update(time)（updateToTime），返回是否成功
DECLARE_CLR_API(uint8_t, mg_videoUpdate, (uintptr_t handle, double time))
// lstg.VideoDecoder:setLooping(loop)
DECLARE_CLR_API(void, mg_videoSetLooping, (uintptr_t handle, uint8_t loop))
// lstg.VideoDecoder:setLoopRange(loop_end, loop_duration)
DECLARE_CLR_API(void, mg_videoSetLoopRange, (uintptr_t handle, double loop_end, double loop_duration))
// lstg.VideoDecoder:getLoopRange()，双返回值拆为出参
DECLARE_CLR_API(void, mg_videoGetLoopRange, (uintptr_t handle, double* loop_end, double* loop_duration))
// lstg.VideoDecoder:getVideoStreams() 的计数查询
DECLARE_CLR_API(uint32_t, mg_videoGetVideoStreamCount, (uintptr_t handle))
// lstg.VideoDecoder:getVideoStreams() 的单项查询（Lua 表键 index/width/height/fps/duration 平铺为出参）
// 错误码：0 = 成功；1 = 下标越界
DECLARE_CLR_API(uint8_t, mg_videoGetVideoStream, (uintptr_t handle, uint32_t index, uint32_t* stream_index, uint32_t* width, uint32_t* height, double* fps, double* duration))
// lstg.VideoDecoder:getAudioStreams() 的计数查询
DECLARE_CLR_API(uint32_t, mg_videoGetAudioStreamCount, (uintptr_t handle))
// lstg.VideoDecoder:getAudioStreams() 的单项查询（Lua 表键 index/channels/sample_rate/duration 平铺为出参）
// 错误码：0 = 成功；1 = 下标越界
DECLARE_CLR_API(uint8_t, mg_videoGetAudioStream, (uintptr_t handle, uint32_t index, uint32_t* stream_index, uint32_t* channels, uint32_t* sample_rate, double* duration))
// lstg.VideoDecoder:getVideoStreamIndex()
DECLARE_CLR_API(uint32_t, mg_videoGetVideoStreamIndex, (uintptr_t handle))
// lstg.VideoDecoder:reopen(options)，返回是否成功
DECLARE_CLR_API(uint8_t, mg_videoReopen, (uintptr_t handle, uint32_t video_stream, uint32_t width, uint32_t height, uint8_t premultiplied_alpha, uint8_t looping, double loop_end, double loop_duration))
// lstg.VideoDecoder:reopen()（无参数表，沿用上次打开参数），返回是否成功
DECLARE_CLR_API(uint8_t, mg_videoReopenLast, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_videoRelease, (uintptr_t handle))

// ============ Sprite（lstg.Sprite） ============

// lstg.Sprite.create(texture, x, y, w, h[, cx, cy[, unit_per_pixel]])；
// has_center 为 0 时不设置中心（与 Lua 不传 cx/cy 一致），unit_per_pixel 默认 1.0（C# 重载补默认值）
DECLARE_CLR_API(uintptr_t, mg_spriteCreate, (uintptr_t texture, float x, float y, float width, float height, uint8_t has_center, float center_x, float center_y, float unit_per_pixel))
// lstg.Sprite:setTexture(texture)
DECLARE_CLR_API(void, mg_spriteSetTexture, (uintptr_t handle, uintptr_t texture))
// lstg.Sprite:getTexture()，返回新持有的 Texture2D 句柄，0 = 无纹理
DECLARE_CLR_API(uintptr_t, mg_spriteGetTexture, (uintptr_t handle))
// lstg.Sprite:setTextureRect(x, y, width, height)
DECLARE_CLR_API(void, mg_spriteSetTextureRect, (uintptr_t handle, float x, float y, float width, float height))
// lstg.Sprite:getTextureRect()，4 返回值拆为出参
DECLARE_CLR_API(void, mg_spriteGetTextureRect, (uintptr_t handle, float* x, float* y, float* width, float* height))
// lstg.Sprite:setCenter(x, y)
DECLARE_CLR_API(void, mg_spriteSetCenter, (uintptr_t handle, float x, float y))
// lstg.Sprite:getCenter()，2 返回值拆为出参
DECLARE_CLR_API(void, mg_spriteGetCenter, (uintptr_t handle, float* x, float* y))
// lstg.Sprite:setUnitPerPixel(value)
DECLARE_CLR_API(void, mg_spriteSetUnitPerPixel, (uintptr_t handle, float value))
// lstg.Sprite:getUnitPerPixel()
DECLARE_CLR_API(float, mg_spriteGetUnitPerPixel, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_spriteRelease, (uintptr_t handle))

// ============ SpriteRenderer（lstg.SpriteRenderer） ============
// 句柄包装结构额外持有 position/scale/rotation/is_dirty 状态（与 Lua userdata 一致），
// draw 时若 is_dirty 则重新计算变换（与 Lua 侧逻辑相同）

// lstg.SpriteRenderer.create()（带 sprite 参数的形式由 C# 侧组合 SetSprite 完成），失败返回 0
DECLARE_CLR_API(uintptr_t, mg_spriteRendererCreate, ())
// lstg.SpriteRenderer:setTransform(x, y[, rot[, sx[, sy]]])，默认 rot=0、sx=1、sy=sx（C# 重载补默认值）
DECLARE_CLR_API(void, mg_spriteRendererSetTransform, (uintptr_t handle, float x, float y, float rotation, float scale_x, float scale_y))
// lstg.MeshRenderer:setPosition(x, y)，标记脏，draw 时应用
DECLARE_CLR_API(void, mg_spriteRendererSetPosition, (uintptr_t handle, float x, float y))
// lstg.SpriteRenderer:setScale(x, y)，标记脏，draw 时应用
DECLARE_CLR_API(void, mg_spriteRendererSetScale, (uintptr_t handle, float x, float y))
// lstg.SpriteRenderer:setRotation(rot)，标记脏，draw 时应用
DECLARE_CLR_API(void, mg_spriteRendererSetRotation, (uintptr_t handle, float rotation))
// lstg.SpriteRenderer:setSprite(sprite)
DECLARE_CLR_API(void, mg_spriteRendererSetSprite, (uintptr_t handle, uintptr_t sprite))
// lstg.SpriteRenderer:setColor(color)，ARGB
DECLARE_CLR_API(void, mg_spriteRendererSetColor, (uintptr_t handle, uint32_t argb))
// lstg.SpriteRenderer:setColor(c1, c2, c3, c4)，4 顶点 ARGB 颜色
DECLARE_CLR_API(void, mg_spriteRendererSetColor4, (uintptr_t handle, uint32_t c1, uint32_t c2, uint32_t c3, uint32_t c4))
// lstg.SpriteRenderer:setLegacyBlendState(blend)
DECLARE_CLR_API(void, mg_spriteRendererSetLegacyBlendState, (uintptr_t handle, uint8_t blend))
// lstg.SpriteRenderer:draw()
DECLARE_CLR_API(void, mg_spriteRendererDraw, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_spriteRendererRelease, (uintptr_t handle))

// ============ SpriteRectRenderer（lstg.SpriteRectRenderer） ============

// lstg.SpriteRectRenderer.create()，失败返回 0
DECLARE_CLR_API(uintptr_t, mg_spriteRectRendererCreate, ())
// lstg.SpriteRectRenderer:setRect(left, right, bottom, top)（Lua 参数顺序）
DECLARE_CLR_API(void, mg_spriteRectRendererSetRect, (uintptr_t handle, float left, float right, float bottom, float top))
// lstg.SpriteRectRenderer:setSprite(sprite)
DECLARE_CLR_API(void, mg_spriteRectRendererSetSprite, (uintptr_t handle, uintptr_t sprite))
// lstg.SpriteRectRenderer:setColor(color)，ARGB
DECLARE_CLR_API(void, mg_spriteRectRendererSetColor, (uintptr_t handle, uint32_t argb))
// lstg.SpriteRectRenderer:setColor(c1, c2, c3, c4)
DECLARE_CLR_API(void, mg_spriteRectRendererSetColor4, (uintptr_t handle, uint32_t c1, uint32_t c2, uint32_t c3, uint32_t c4))
// lstg.SpriteRectRenderer:setLegacyBlendState(blend)
DECLARE_CLR_API(void, mg_spriteRectRendererSetLegacyBlendState, (uintptr_t handle, uint8_t blend))
// lstg.SpriteRectRenderer:draw()
DECLARE_CLR_API(void, mg_spriteRectRendererDraw, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_spriteRectRendererRelease, (uintptr_t handle))

// ============ SpriteQuadRenderer（lstg.SpriteQuadRenderer） ============

// lstg.SpriteQuadRenderer.create()，失败返回 0
DECLARE_CLR_API(uintptr_t, mg_spriteQuadRendererCreate, ())
// lstg.SpriteQuadRenderer:setQuad(x1, y1, x2, y2, x3, y3, x4, y4)，2D 四顶点
DECLARE_CLR_API(void, mg_spriteQuadRendererSetQuad2, (uintptr_t handle, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4))
// lstg.SpriteQuadRenderer:setQuad(x1, y1, z1, ..., x4, y4, z4)，3D 四顶点
DECLARE_CLR_API(void, mg_spriteQuadRendererSetQuad3, (uintptr_t handle, float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3, float x4, float y4, float z4))
// lstg.SpriteQuadRenderer:setSprite(sprite)
DECLARE_CLR_API(void, mg_spriteQuadRendererSetSprite, (uintptr_t handle, uintptr_t sprite))
// lstg.SpriteQuadRenderer:setColor(color)，ARGB
DECLARE_CLR_API(void, mg_spriteQuadRendererSetColor, (uintptr_t handle, uint32_t argb))
// lstg.SpriteQuadRenderer:setColor(c1, c2, c3, c4)
DECLARE_CLR_API(void, mg_spriteQuadRendererSetColor4, (uintptr_t handle, uint32_t c1, uint32_t c2, uint32_t c3, uint32_t c4))
// lstg.SpriteQuadRenderer:setLegacyBlendState(blend)
DECLARE_CLR_API(void, mg_spriteQuadRendererSetLegacyBlendState, (uintptr_t handle, uint8_t blend))
// lstg.SpriteQuadRenderer:draw()
DECLARE_CLR_API(void, mg_spriteQuadRendererDraw, (uintptr_t handle))
// 释放句柄
DECLARE_CLR_API(void, mg_spriteQuadRendererRelease, (uintptr_t handle))
