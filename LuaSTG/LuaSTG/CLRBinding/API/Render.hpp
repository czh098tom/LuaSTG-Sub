// LuaSTG CoreCLR 绑定：渲染（对应 LW_Renderer/LW_Render/PostEffectShader）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 约定：
// - 颜色统一 uint32_t ARGB（内存布局 0xAARRGGBB，core::Color4B(uint32_t) 构造）
// - 混合模式参数为 uint8_t，取引擎侧 luastg::BlendMode 枚举值（C# 侧 BlendMode）
// - 返回 uint8_t 的函数为错误码：0 = 成功；各函数的错误码含义见注释
// - BeginScene/EndScene 已由 Core.hpp 提供（beginScene/endScene），此处不再重复定义

// ============ 现代 API（lstg.Renderer → C# LuaSTG.Core.Renderer） ============

// 清空渲染目标（lstg.Renderer.clearRenderTarget / lstg.RenderClear）
// 渲染目标栈非空时颜色按 alpha 预乘（与 Lua 侧 lib_clearRenderTarget 一致）
DECLARE_CLR_API(void, render_clearRenderTarget, (uint32_t argb))
// 清空深度缓冲（lstg.Renderer.clearDepthBuffer / lstg.ClearZBuffer）
DECLARE_CLR_API(void, render_clearDepthBuffer, (float z))

// 设置正交投影（lstg.Renderer.setOrtho / lstg.SetOrtho）
// Lua 侧 4 参数形式 (l, r, b, t) 时 znear=0、zfar=1，由 C# 重载补默认值
DECLARE_CLR_API(void, render_setOrtho, (double l, double r, double b, double t, double znear, double zfar))
// 设置透视投影（lstg.Renderer.setPerspective / lstg.SetPerspective）
// 错误码：0 = 成功；1 = 无效 fov（要求 0 < fov < pi）；2 = 无效 z 范围（要求 0 < znear < zfar）
DECLARE_CLR_API(uint8_t, render_setPerspective, (double eye_x, double eye_y, double eye_z, double lookat_x, double lookat_y, double lookat_z, double headup_x, double headup_y, double headup_z, double fov, double aspect_ratio, double znear, double zfar))
// 设置视口（lstg.Renderer.setViewport）
// 参数为引擎原生顺序（left/top/right/bottom，Y 轴向下，不做翻转）；4 参数形式默认 znear=0、zfar=1
DECLARE_CLR_API(void, render_setViewport, (double left, double top, double right, double bottom, double znear, double zfar))
// 设置裁剪矩形（lstg.Renderer.setScissorRect，参数为 left/top/right/bottom，Y 轴向下，不做翻转）
DECLARE_CLR_API(void, render_setScissorRect, (double left, double top, double right, double bottom))

// 设置顶点色混合状态（lstg.Renderer.setVertexColorBlendState）
// 错误码：0 = 成功；1 = 不在渲染批次内（Lua 侧 luaL_error "invalid render operation"）
DECLARE_CLR_API(uint8_t, render_setVertexColorBlendState, (uint8_t state))
// 设置雾状态（lstg.Renderer.setFogState）
// 错误码：0 = 成功；1 = 不在渲染批次内
DECLARE_CLR_API(uint8_t, render_setFogState, (uint8_t state, uint32_t argb, float density_or_znear, float zfar))
// 设置深度缓冲开关（lstg.Renderer.setDepthState / lstg.SetZBufferEnable）
// 错误码：0 = 成功；1 = 不在渲染批次内
DECLARE_CLR_API(uint8_t, render_setDepthState, (uint8_t state))
// 设置混合状态（lstg.Renderer.setBlendState，state 为 IRenderer::BlendState）
// 错误码：0 = 成功；1 = 不在渲染批次内
DECLARE_CLR_API(uint8_t, render_setBlendState, (uint8_t state))
// 绑定纹理（lstg.Renderer.setTexture）
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 纹理不存在
DECLARE_CLR_API(uint8_t, render_setTexture, (const char* name))

// 绘制三角形（lstg.Renderer.drawTriangle），每顶点 6 个分量：x, y, z, u, v, color(ARGB)
// 错误码：0 = 成功；1 = 不在渲染批次内
DECLARE_CLR_API(uint8_t, render_drawTriangle, (float x1, float y1, float z1, float u1, float v1, uint32_t c1, float x2, float y2, float z2, float u2, float v2, uint32_t c2, float x3, float y3, float z3, float u3, float v3, uint32_t c3))
// 绘制四边形（lstg.Renderer.drawQuad），每顶点 6 个分量：x, y, z, u, v, color(ARGB)
// 错误码：0 = 成功；1 = 不在渲染批次内
DECLARE_CLR_API(uint8_t, render_drawQuad, (float x1, float y1, float z1, float u1, float v1, uint32_t c1, float x2, float y2, float z2, float u2, float v2, uint32_t c2, float x3, float y3, float z3, float u3, float v3, uint32_t c3, float x4, float y4, float z4, float u4, float v4, uint32_t c4))

// 绘制精灵（lstg.Renderer.drawSprite / lstg.Render）
// rot 为角度制；hscale/vscale 会乘以全局图像缩放系数（由 C++ 侧处理）
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 精灵不存在
DECLARE_CLR_API(uint8_t, render_drawSprite, (const char* name, float x, float y, float rot, float hscale, float vscale, float z))
// 绘制矩形精灵（lstg.Renderer.drawSpriteRect / lstg.RenderRect）
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 精灵不存在
DECLARE_CLR_API(uint8_t, render_drawSpriteRect, (const char* name, float l, float r, float b, float t, float z))
// 以 4 顶点绘制精灵（lstg.Renderer.drawSprite4V / lstg.Render4V）
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 精灵不存在
DECLARE_CLR_API(uint8_t, render_drawSprite4V, (const char* name, float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3, float x4, float y4, float z4))
// 绘制动画序列（lstg.Renderer.drawSpriteSequence / lstg.RenderAnimation）
// rot 为角度制；hscale/vscale 会乘以全局图像缩放系数（由 C++ 侧处理）
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 动画不存在
DECLARE_CLR_API(uint8_t, render_drawSpriteSequence, (const char* name, int32_t ani_timer, float x, float y, float rot, float hscale, float vscale, float z))
// 绘制纹理（lstg.Renderer.drawTexture / lstg.RenderTexture）
// blend 为 luastg::BlendMode 枚举值；每顶点 6 个分量：x, y, z, u, v, color(ARGB)
// u/v 传入像素坐标，由 C++ 侧按纹理尺寸归一化
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 纹理不存在
DECLARE_CLR_API(uint8_t, render_drawTexture, (const char* name, uint8_t blend, float x1, float y1, float z1, float u1, float v1, uint32_t c1, float x2, float y2, float z2, float u2, float v2, uint32_t c2, float x3, float y3, float z3, float u3, float v3, uint32_t c3, float x4, float y4, float z4, float u4, float v4, uint32_t c4))
// 绘制模型（lstg.Renderer.drawModel / lstg.RenderModel）
// roll/pitch/yaw 为角度制
// 错误码：0 = 成功；1 = 模型不存在
DECLARE_CLR_API(uint8_t, render_drawModel, (const char* name, float x, float y, float z, float roll, float pitch, float yaw, float sx, float sy, float sz))

// ============ 兼容 API（lstg.* → C# LuaSTG.Core.Render） ============

// lstg.SetViewport：与 render_setViewport 相同，但按渲染目标高度翻转 Y 轴
// 4 参数形式默认 znear=0、zfar=1（由 C# 重载补默认值）
DECLARE_CLR_API(void, render_setViewportCompat, (double l, double r, double b, double t, double znear, double zfar))
// lstg.SetScissorRect：参数为 left/right/bottom/top，按渲染目标高度翻转 Y 轴
DECLARE_CLR_API(void, render_setScissorRectCompat, (double left, double right, double bottom, double top))
// lstg.SetFog：传统雾参数
// start == end 关闭雾；start == -1 为 Exp（end 为密度）；start == -2 为 Exp2；否则为 Linear(start, end)
DECLARE_CLR_API(void, render_setFogCompat, (double start, double end, uint32_t argb))
// lstg.PushRenderTarget：按资源名压入渲染目标
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 纹理不存在；3 = 纹理不是渲染目标；4 = 压栈失败
DECLARE_CLR_API(uint8_t, render_pushRenderTarget, (const char* name))
// lstg.PopRenderTarget：弹出渲染目标
// 错误码：0 = 成功；1 = 不在渲染批次内；2 = 弹栈失败
DECLARE_CLR_API(uint8_t, render_popRenderTarget, ())

// ============ 文字与截图（LW_Render → C# LuaSTG.Core.Render） ============

// lstg.RenderText：渲染纹理字体；halign/valign 取 FontAlignHorizontal/FontAlignVertical 枚举值
// scale 会乘以全局图像缩放系数（由 C++ 侧处理）
// 错误码：0 = 成功；1 = 渲染失败（字体不存在等，详见引擎日志）
DECLARE_CLR_API(uint8_t, render_renderText, (const char* font_name, const char* text, float x, float y, float scale, uint8_t halign, uint8_t valign))
// lstg.RenderTTF：渲染 TTF 字体；format 为对齐标志位组合（0x01 居中 / 0x02 右对齐 / 0x04 垂直居中 / 0x08 底对齐 / 0x10 自动换行）
// scale 会乘以全局图像缩放系数（由 C++ 侧处理）
// 错误码：0 = 成功；1 = 渲染失败（字体不存在等，详见引擎日志）
DECLARE_CLR_API(uint8_t, render_renderTTF, (const char* font_name, const char* text, float left, float right, float bottom, float top, float scale, int32_t format, uint32_t argb))
// lstg.Snapshot：保存截图（失败仅记录引擎日志）
DECLARE_CLR_API(void, render_snapshot, (const char* path))
// lstg.SaveTexture：保存纹理到文件（失败仅记录引擎日志）
DECLARE_CLR_API(void, render_saveTexture, (const char* tex_name, const char* path))
// lstg.DrawCollider：绘制所有对象的碰撞体
DECLARE_CLR_API(void, render_drawCollider, ())
// lstg.RenderGroupCollider：绘制指定碰撞组的碰撞体
DECLARE_CLR_API(void, render_renderGroupCollider, (int32_t group, uint32_t argb))

// ============ 后期特效着色器（lstg.PostEffectShader → C# LuaSTG.Core.PostEffectShader） ============

// lstg.CreatePostEffectShader：创建独立的后效着色器，返回引擎对象句柄（0 = 失败）
// 句柄持有引用，C# 侧通过 render_pfxRelease 释放
DECLARE_CLR_API(uintptr_t, render_createPostEffectShader, (const char* path))
// 从资源池查找已加载的后效（lstg.LoadFX 加载的资源），返回借用句柄（0 = 不存在）
// 借用句柄归资源管理器所有，C# 侧不得调用 render_pfxRelease 释放
DECLARE_CLR_API(uintptr_t, render_findFX, (const char* name))
// lstg.PostEffectShader.setFloat，错误码：0 = 成功；1 = 引擎调用返回失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, render_pfxSetFloat, (uintptr_t shader, const char* name, float value))
// lstg.PostEffectShader.setFloat2，错误码：0 = 成功；1 = 引擎调用返回失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, render_pfxSetFloat2, (uintptr_t shader, const char* name, float x, float y))
// lstg.PostEffectShader.setFloat3，错误码：0 = 成功；1 = 引擎调用返回失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, render_pfxSetFloat3, (uintptr_t shader, const char* name, float x, float y, float z))
// lstg.PostEffectShader.setFloat4，错误码：0 = 成功；1 = 引擎调用返回失败；2 = 句柄无效
DECLARE_CLR_API(uint8_t, render_pfxSetFloat4, (uintptr_t shader, const char* name, float x, float y, float z, float w))
// lstg.PostEffectShader.setTexture，按资源名绑定纹理
// 错误码：0 = 成功；1 = 纹理不存在；2 = 句柄无效或引擎调用返回失败
DECLARE_CLR_API(uint8_t, render_pfxSetTexture, (uintptr_t shader, const char* name, const char* tex_name))
// 释放 render_createPostEffectShader 创建的着色器句柄（借用句柄禁止调用）
DECLARE_CLR_API(void, render_pfxRelease, (uintptr_t shader))

// lstg.PostEffect(shader, blend) 对象形式 / 传统形式最终绘制
// 错误码：0 = 成功；1 = 不在渲染批次内
DECLARE_CLR_API(uint8_t, render_postEffectDraw, (uintptr_t shader, uint8_t blend))
// lstg.PostEffect(rt, ps, blend) 传统形式的准备步骤：
// 按名称查找后效并设置 screen_texture / screen_texture_size / viewport 标准参数
// 错误码：0 = 成功；1 = 渲染目标纹理不存在
DECLARE_CLR_API(uint8_t, render_postEffectSetScreenParams, (uintptr_t shader, const char* rt_name))
// lstg.PostEffect(ps, rt, sampler, blend, cbdata, tdata) 旧版形式（引擎源码中标注为待废弃）
// cv_data 为 cv_count 个 Vector4（每 4 个 double 一组，最多 8 组）；tex0~tex3 为最多 4 个纹理资源名（空指针跳过）
// 错误码：0 = 成功；1 = 后效不存在；2 = 渲染目标纹理不存在；3 = 纹理资源不存在；4 = 不在渲染批次内
DECLARE_CLR_API(uint8_t, render_postEffectAdvanced, (const char* ps_name, const char* rt_name, uint8_t rt_sampler, uint8_t blend, const double* cv_data, uint32_t cv_count, const char* tex0, uint8_t sv0, const char* tex1, uint8_t sv1, const char* tex2, uint8_t sv2, const char* tex3, uint8_t sv3, uint32_t tex_count))
