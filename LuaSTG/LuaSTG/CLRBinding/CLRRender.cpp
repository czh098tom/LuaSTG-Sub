// LuaSTG CoreCLR 绑定：渲染模块实现
// 对应 Lua 绑定：LuaBinding/LW_Renderer.cpp、LuaBinding/LW_Render.cpp、LuaBinding/PostEffectShader.cpp
// 移植时保留引擎调用逻辑，Lua 栈操作（luaL_check/luaL_error）改为参数校验与错误码返回

#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"
#include "GameObject/GameObjectPool.h"
#include "GameResource/LegacyBlendStateHelper.hpp"

using namespace luastg;

namespace
{
	core::Graphics::IRenderer* LR2D() noexcept {
		return LAPP.getRenderer2D();
	}
	ResourceMgr& LRESMGR() noexcept {
		return LAPP.GetResourceMgr();
	}

#ifndef NDEBUG
#	define check_rendertarget_usage(P_TEXTURE) assert(!LAPP.GetRenderTargetManager()->CheckRenderTargetInUse((P_TEXTURE).get()))
#else
#	define check_rendertarget_usage(P_TEXTURE) ((void)0)
#endif

	[[nodiscard]] core::Graphics::IPostEffectShader* asPFX(uintptr_t const shader) noexcept {
		return reinterpret_cast<core::Graphics::IPostEffectShader*>(shader);
	}

	/// @brief 旧版混合模式 -> 渲染器混合状态（对应 Lua 侧 translate_blend_3d）
	[[nodiscard]] core::Graphics::IRenderer::BlendState translate_blend_3d(BlendMode const blend) noexcept {
		return translateLegacyBlendState(blend).blend_state;
	}

	/// @brief 旧版传统雾参数翻译（对应 Lua 侧 api_setFogState）
	void api_setFogState(float const start, float const end, core::Color4B const color) noexcept {
		auto* ctx = LR2D();
		if (start != end) {
			if (start == -1.0f) {
				ctx->setFogState(core::Graphics::IRenderer::FogState::Exp, color, end, 0.0f);
			}
			else if (start == -2.0f) {
				ctx->setFogState(core::Graphics::IRenderer::FogState::Exp2, color, end, 0.0f);
			}
			else {
				ctx->setFogState(core::Graphics::IRenderer::FogState::Linear, color, start, end);
			}
		}
		else {
			ctx->setFogState(core::Graphics::IRenderer::FogState::Disable, core::Color4B(), 0.0f, 0.0f);
		}
	}

	/// @brief 渲染目标栈非空时按 alpha 预乘颜色（对应 Lua 侧 lib_clearRenderTarget）
	void premultiply_color_for_rt(core::Color4B& color) noexcept {
		uint16_t const r = color.a * color.r;
		color.r = static_cast<uint8_t>((r + ((r + 257) >> 8)) >> 8);
		uint16_t const g = color.a * color.g;
		color.g = static_cast<uint8_t>((g + ((g + 257) >> 8)) >> 8);
		uint16_t const b = color.a * color.b;
		color.b = static_cast<uint8_t>((b + ((b + 257) >> 8)) >> 8);
	}
}

namespace luastg
{
	// ============ 清屏与深度 ============

	void CLRBinding::render_clearRenderTarget(uint32_t const argb)
	{
		core::Color4B color(argb);
		if (!LAPP.GetRenderTargetManager()->IsRenderTargetStackEmpty()) {
			premultiply_color_for_rt(color);
		}
		LR2D()->clearRenderTarget(color);
	}
	void CLRBinding::render_clearDepthBuffer(float const z)
	{
		LR2D()->clearDepthBuffer(z);
	}

	// ============ 投影与视口 ============

	void CLRBinding::render_setOrtho(double const l, double const r, double const b, double const t, double const znear, double const zfar)
	{
		// 对应 lib_setOrtho：Lua 侧 4 参数形式 (l, r, b, t) 时 znear=0、zfar=1（由 C# 重载补默认值）
		LR2D()->setOrtho(core::BoxF(
			(float)l, (float)t, (float)znear,
			(float)r, (float)b, (float)zfar
		));
	}
	uint8_t CLRBinding::render_setPerspective(
		double const eye_x, double const eye_y, double const eye_z,
		double const lookat_x, double const lookat_y, double const lookat_z,
		double const headup_x, double const headup_y, double const headup_z,
		double const fov, double const aspect_ratio, double const znear, double const zfar)
	{
		float const fovf = (float)fov;
		float const znearf = (float)znear;
		float const zfarf = (float)zfar;
		if (fovf < 0.0f || fovf >= L_PI_F)
			return 1; // Lua: invalid parameters, require (0 < fov < pi)
		if (znearf <= 0.0f || zfarf <= znearf)
			return 2; // Lua: invalid parameters, require (0 < z_near < z_far)
		LR2D()->setPerspective(
			core::Vector3F((float)eye_x, (float)eye_y, (float)eye_z),
			core::Vector3F((float)lookat_x, (float)lookat_y, (float)lookat_z),
			core::Vector3F((float)headup_x, (float)headup_y, (float)headup_z),
			fovf, (float)aspect_ratio, znearf, zfarf
		);
		return 0;
	}
	void CLRBinding::render_setViewport(double const left, double const top, double const right, double const bottom, double const znear, double const zfar)
	{
		// 对应 lib_setViewport：参数为引擎原生顺序（a.x, a.y, b.x, b.y），Y 轴向下，不做翻转
		LR2D()->setViewport(core::BoxF(
			(float)left, (float)top, (float)znear,
			(float)right, (float)bottom, (float)zfar
		));
	}
	void CLRBinding::render_setScissorRect(double const left, double const top, double const right, double const bottom)
	{
		LR2D()->setScissorRect(core::RectF((float)left, (float)top, (float)right, (float)bottom));
	}

	// ============ 渲染状态 ============

	uint8_t CLRBinding::render_setVertexColorBlendState(uint8_t const state)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		LR2D()->setVertexColorBlendState((core::Graphics::IRenderer::VertexColorBlendState)state);
		return 0;
	}
	uint8_t CLRBinding::render_setFogState(uint8_t const state, uint32_t const argb, float const density_or_znear, float const zfar)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		LR2D()->setFogState(
			(core::Graphics::IRenderer::FogState)state,
			core::Color4B(argb),
			density_or_znear,
			zfar
		);
		return 0;
	}
	uint8_t CLRBinding::render_setDepthState(uint8_t const state)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		LR2D()->setDepthState((core::Graphics::IRenderer::DepthState)state);
		return 0;
	}
	uint8_t CLRBinding::render_setBlendState(uint8_t const state)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		LR2D()->setBlendState((core::Graphics::IRenderer::BlendState)state);
		return 0;
	}
	uint8_t CLRBinding::render_setTexture(const char* const name)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		core::SmartReference<IResourceTexture> p = LRESMGR().FindTexture(name);
		if (!p) {
			spdlog::error("[luastg] lstg.Renderer.setTexture failed: can't find texture '{}'", name);
			return 2;
		}
		check_rendertarget_usage(p);
		LR2D()->setTexture(p->GetTexture());
		return 0;
	}

	// ============ 低级图元 ============

	uint8_t CLRBinding::render_drawTriangle(
		float const x1, float const y1, float const z1, float const u1, float const v1, uint32_t const c1,
		float const x2, float const y2, float const z2, float const u2, float const v2, uint32_t const c2,
		float const x3, float const y3, float const z3, float const u3, float const v3, uint32_t const c3)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		core::Graphics::IRenderer::DrawVertex const vertex[3] = {
			core::Graphics::IRenderer::DrawVertex(x1, y1, z1, u1, v1, c1),
			core::Graphics::IRenderer::DrawVertex(x2, y2, z2, u2, v2, c2),
			core::Graphics::IRenderer::DrawVertex(x3, y3, z3, u3, v3, c3),
		};
		LR2D()->drawTriangle(vertex);
		return 0;
	}
	uint8_t CLRBinding::render_drawQuad(
		float const x1, float const y1, float const z1, float const u1, float const v1, uint32_t const c1,
		float const x2, float const y2, float const z2, float const u2, float const v2, uint32_t const c2,
		float const x3, float const y3, float const z3, float const u3, float const v3, uint32_t const c3,
		float const x4, float const y4, float const z4, float const u4, float const v4, uint32_t const c4)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		core::Graphics::IRenderer::DrawVertex const vertex[4] = {
			core::Graphics::IRenderer::DrawVertex(x1, y1, z1, u1, v1, c1),
			core::Graphics::IRenderer::DrawVertex(x2, y2, z2, u2, v2, c2),
			core::Graphics::IRenderer::DrawVertex(x3, y3, z3, u3, v3, c3),
			core::Graphics::IRenderer::DrawVertex(x4, y4, z4, u4, v4, c4),
		};
		LR2D()->drawQuad(vertex);
		return 0;
	}

	// ============ 精灵绘制 ============

	uint8_t CLRBinding::render_drawSprite(const char* const name, float const x, float const y, float const rot, float const hscale, float const vscale, float const z)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		core::SmartReference<IResourceSprite> pimg2dres = LRESMGR().FindSprite(name);
		if (!pimg2dres) {
			spdlog::error("[luastg] lstg.Renderer.drawSprite failed, can't find sprite '{}'", name);
			return 2;
		}
		// 全局图像缩放系数在引擎侧应用（与 Lua 侧 lib_drawSprite 一致），rot 为角度制
		float const gscale = LRESMGR().GetGlobalImageScaleFactor();
		pimg2dres->Render(x, y, (float)((double)rot * L_DEG_TO_RAD), hscale * gscale, vscale * gscale, z);
		return 0;
	}
	uint8_t CLRBinding::render_drawSpriteRect(const char* const name, float const l, float const r, float const b, float const t, float const z)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		core::SmartReference<IResourceSprite> pimg2dres = LRESMGR().FindSprite(name);
		if (!pimg2dres) {
			spdlog::error("[luastg] lstg.Renderer.drawSpriteRect failed, can't find sprite '{}'", name);
			return 2;
		}
		pimg2dres->RenderRect(l, r, b, t, z);
		return 0;
	}
	uint8_t CLRBinding::render_drawSprite4V(
		const char* const name,
		float const x1, float const y1, float const z1,
		float const x2, float const y2, float const z2,
		float const x3, float const y3, float const z3,
		float const x4, float const y4, float const z4)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		core::SmartReference<IResourceSprite> pimg2dres = LRESMGR().FindSprite(name);
		if (!pimg2dres) {
			spdlog::error("[luastg] lstg.Renderer.drawSprite4V failed, can't find sprite '{}'", name);
			return 2;
		}
		pimg2dres->Render4V(x1, y1, z1, x2, y2, z2, x3, y3, z3, x4, y4, z4);
		return 0;
	}
	uint8_t CLRBinding::render_drawSpriteSequence(const char* const name, int32_t const ani_timer, float const x, float const y, float const rot, float const hscale, float const vscale, float const z)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		core::SmartReference<IResourceAnimation> pani2dres = LRESMGR().FindAnimation(name);
		if (!pani2dres) {
			spdlog::error("[luastg] lstg.Renderer.drawSpriteSequence failed, can't find sprite sequence '{}'", name);
			return 2;
		}
		// 全局图像缩放系数在引擎侧应用（与 Lua 侧 lib_drawSpriteSequence 一致），rot 为角度制
		float const gscale = LRESMGR().GetGlobalImageScaleFactor();
		pani2dres->Render(ani_timer, x, y, (float)((double)rot * L_DEG_TO_RAD), hscale * gscale, vscale * gscale, z);
		return 0;
	}
	uint8_t CLRBinding::render_drawTexture(
		const char* const name, uint8_t const blend,
		float const x1, float const y1, float const z1, float const u1, float const v1, uint32_t const c1,
		float const x2, float const y2, float const z2, float const u2, float const v2, uint32_t const c2,
		float const x3, float const y3, float const z3, float const u3, float const v3, uint32_t const c3,
		float const x4, float const y4, float const z4, float const u4, float const v4, uint32_t const c4)
	{
		auto* ctx = LR2D();
		if (!ctx->isBatchScope())
			return 1;
		core::Graphics::IRenderer::DrawVertex vertex[4] = {
			core::Graphics::IRenderer::DrawVertex(x1, y1, z1, u1, v1, c1),
			core::Graphics::IRenderer::DrawVertex(x2, y2, z2, u2, v2, c2),
			core::Graphics::IRenderer::DrawVertex(x3, y3, z3, u3, v3, c3),
			core::Graphics::IRenderer::DrawVertex(x4, y4, z4, u4, v4, c4),
		};

		// 应用混合模式（对应 Lua 侧 translate_blend）
		LAPP.updateGraph2DBlendMode((BlendMode)blend);

		core::SmartReference<IResourceTexture> ptex2dres = LRESMGR().FindTexture(name);
		if (!ptex2dres) {
			spdlog::error("[luastg] lstg.Renderer.drawTexture failed: can't find texture '{}'", name);
			return 2;
		}
		check_rendertarget_usage(ptex2dres);
		core::ITexture2D* ptex2d = ptex2dres->GetTexture();
		float const uscale = 1.0f / (float)ptex2d->getSize().x;
		float const vscale = 1.0f / (float)ptex2d->getSize().y;
		for (int i = 0; i < 4; ++i) {
			vertex[i].u *= uscale;
			vertex[i].v *= vscale;
		}
		ctx->setTexture(ptex2d);

		ctx->drawQuad(vertex[0], vertex[1], vertex[2], vertex[3]);
		return 0;
	}
	uint8_t CLRBinding::render_drawModel(const char* const name, float const x, float const y, float const z, float const roll, float const pitch, float const yaw, float const sx, float const sy, float const sz)
	{
		core::SmartReference<IResourceModel> pmodres = LRESMGR().FindModel(name);
		if (!pmodres) {
			spdlog::error("[luastg] lstg.Renderer.drawModel failed: can't find model '{}'", name);
			return 1;
		}
		pmodres->GetModel()->setScaling(core::Vector3F(sx, sy, sz));
		pmodres->GetModel()->setRotationRollPitchYaw(
			(float)((double)roll * L_DEG_TO_RAD),
			(float)((double)pitch * L_DEG_TO_RAD),
			(float)((double)yaw * L_DEG_TO_RAD)
		);
		pmodres->GetModel()->setPosition(core::Vector3F(x, y, z));
		LR2D()->drawModel(pmodres->GetModel());
		return 0;
	}

	// ============ 兼容 API ============

	void CLRBinding::render_setViewportCompat(double const l, double const r, double const b, double const t, double const znear, double const zfar)
	{
		// 对应 compat_SetViewport：Y 轴按当前渲染目标高度翻转
		core::BoxF box(
			(float)l, (float)t, (float)znear,
			(float)r, (float)b, (float)zfar
		);
		core::Vector2U const backbuf_size = LAPP.GetRenderTargetManager()->GetTopRenderTargetSize();
		box.a.y = (float)backbuf_size.y - box.a.y;
		box.b.y = (float)backbuf_size.y - box.b.y;
		LR2D()->setViewport(box);
	}
	void CLRBinding::render_setScissorRectCompat(double const left, double const right, double const bottom, double const top)
	{
		// 对应 compat_SetScissorRect：Y 轴按当前渲染目标高度翻转
		core::RectF rect(
			(float)left, (float)top,
			(float)right, (float)bottom
		);
		core::Vector2U const backbuf_size = LAPP.GetRenderTargetManager()->GetTopRenderTargetSize();
		rect.a.y = (float)backbuf_size.y - rect.a.y;
		rect.b.y = (float)backbuf_size.y - rect.b.y;
		LR2D()->setScissorRect(rect);
	}
	void CLRBinding::render_setFogCompat(double const start, double const end, uint32_t const argb)
	{
		// 对应 compat_SetFog
		api_setFogState((float)start, (float)end, core::Color4B(argb));
	}
	uint8_t CLRBinding::render_pushRenderTarget(const char* const name)
	{
		auto* ctx = LR2D();
		if (!ctx->isBatchScope())
			return 1;
		ctx->flush();
		core::SmartReference<IResourceTexture> p = LRESMGR().FindTexture(name);
		if (!p) {
			spdlog::error("[luastg] rendertarget '{}' not found.", name);
			return 2;
		}
		if (!p->IsRenderTarget()) {
			spdlog::error("[luastg] '{}' is not a rendertarget.", name);
			return 3;
		}
		if (!LAPP.GetRenderTargetManager()->PushRenderTarget(p.get())) {
			spdlog::error("[luastg] push rendertarget '{}' failed.", name);
			return 4;
		}
		ctx->setViewportAndScissorRect();
		return 0;
	}
	uint8_t CLRBinding::render_popRenderTarget()
	{
		auto* ctx = LR2D();
		if (!ctx->isBatchScope())
			return 1;
		ctx->flush();
		if (!LAPP.GetRenderTargetManager()->PopRenderTarget()) {
			spdlog::error("[luastg] pop rendertarget failed.");
			return 2;
		}
		ctx->setViewportAndScissorRect();
		return 0;
	}

	// ============ 文字与截图 ============

	uint8_t CLRBinding::render_renderText(const char* const font_name, const char* const text, float const x, float const y, float const scale, uint8_t const halign, uint8_t const valign)
	{
		// 对应 Lua 侧 RenderText：scale 乘以全局图像缩放系数
		if (!LAPP.RenderText(
			font_name, text,
			x, y,
			scale * LRESMGR().GetGlobalImageScaleFactor(),
			(FontAlignHorizontal)halign,
			(FontAlignVertical)valign
		)) {
			spdlog::error("[luastg] can't draw text '{}'.", font_name);
			return 1;
		}
		return 0;
	}
	uint8_t CLRBinding::render_renderTTF(const char* const font_name, const char* const text, float const left, float const right, float const bottom, float const top, float const scale, int32_t const format, uint32_t const argb)
	{
		// 对应 Lua 侧 RenderTTF：scale 乘以全局图像缩放系数
		if (!LAPP.RenderTTF(
			font_name, text,
			left, right, bottom, top,
			scale * LRESMGR().GetGlobalImageScaleFactor(),
			format,
			core::Color4B(argb)
		)) {
			spdlog::error("[luastg] can't render font '{}'.", font_name);
			return 1;
		}
		return 0;
	}
	void CLRBinding::render_snapshot(const char* const path)
	{
		LAPP.SnapShot(path);
	}
	void CLRBinding::render_saveTexture(const char* const tex_name, const char* const path)
	{
		LAPP.SaveTexture(tex_name, path);
	}
	void CLRBinding::render_drawCollider()
	{
		LPOOL.DrawCollider();
	}
	void CLRBinding::render_renderGroupCollider(int32_t const group, uint32_t const argb)
	{
		LPOOL.DrawGroupCollider2(group, core::Color4B(argb));
	}

	// ============ 后期特效着色器 ============

	uintptr_t CLRBinding::render_createPostEffectShader(const char* const path)
	{
		core::SmartReference<core::Graphics::IPostEffectShader> shader;
		if (!LR2D()->createPostEffectShader(path, shader.put())) {
			spdlog::error("[clr] lstg.CreatePostEffectShader failed, see log file for more detail");
			return 0;
		}
		// 增加一次引用后交给 C# 侧持有，由 render_pfxRelease 释放
		auto* const raw = shader.get();
		raw->retain();
		return reinterpret_cast<uintptr_t>(raw);
	}
	uintptr_t CLRBinding::render_findFX(const char* const name)
	{
		// 借用资源管理器持有的着色器，不加引用；C# 侧不得释放
		core::SmartReference<IResourcePostEffectShader> pfx = LRESMGR().FindFX(name);
		if (!pfx) {
			spdlog::error("[clr] posteffect '{}' not found.", name);
			return 0;
		}
		return reinterpret_cast<uintptr_t>(pfx->GetPostEffectShader());
	}
	uint8_t CLRBinding::render_pfxSetFloat(uintptr_t const shader, const char* const name, float const value)
	{
		auto* const p = asPFX(shader);
		if (!p)
			return 2;
		return p->setFloat(name, value) ? 0 : 1;
	}
	uint8_t CLRBinding::render_pfxSetFloat2(uintptr_t const shader, const char* const name, float const x, float const y)
	{
		auto* const p = asPFX(shader);
		if (!p)
			return 2;
		return p->setFloat2(name, core::Vector2F(x, y)) ? 0 : 1;
	}
	uint8_t CLRBinding::render_pfxSetFloat3(uintptr_t const shader, const char* const name, float const x, float const y, float const z)
	{
		auto* const p = asPFX(shader);
		if (!p)
			return 2;
		return p->setFloat3(name, core::Vector3F(x, y, z)) ? 0 : 1;
	}
	uint8_t CLRBinding::render_pfxSetFloat4(uintptr_t const shader, const char* const name, float const x, float const y, float const z, float const w)
	{
		auto* const p = asPFX(shader);
		if (!p)
			return 2;
		return p->setFloat4(name, core::Vector4F(x, y, z, w)) ? 0 : 1;
	}
	uint8_t CLRBinding::render_pfxSetTexture(uintptr_t const shader, const char* const name, const char* const tex_name)
	{
		auto* const p = asPFX(shader);
		if (!p)
			return 2;
		core::SmartReference<IResourceTexture> ptex = LRESMGR().FindTexture(tex_name);
		if (!ptex) {
			spdlog::error("[clr] can't find texture '{}'", tex_name);
			return 1;
		}
		check_rendertarget_usage(ptex);
		return p->setTexture2D(name, ptex->GetTexture()) ? 0 : 2;
	}
	void CLRBinding::render_pfxRelease(uintptr_t const shader)
	{
		if (auto* const p = asPFX(shader)) {
			p->release();
		}
	}
	uint8_t CLRBinding::render_postEffectDraw(uintptr_t const shader, uint8_t const blend)
	{
		if (!LR2D()->isBatchScope())
			return 1;
		auto const b = translate_blend_3d((BlendMode)blend);
		LR2D()->drawPostEffect(asPFX(shader), b);
		return 0;
	}
	uint8_t CLRBinding::render_postEffectSetScreenParams(uintptr_t const shader, const char* const rt_name)
	{
		// 对应 Lua 侧传统风格 PostEffect 的标准参数设置
		core::SmartReference<IResourceTexture> prt = LRESMGR().FindTexture(rt_name);
		if (!prt) {
			spdlog::error("[clr] texture '{}' not found.", rt_name);
			return 1;
		}
		check_rendertarget_usage(prt);

		core::Graphics::IPostEffectShader* p_effect = asPFX(shader);

		p_effect->setTexture2D("screen_texture", prt->GetTexture());

		auto const rt_size = prt->GetTexture()->getSize();
		p_effect->setFloat4("screen_texture_size", core::Vector4F(float(rt_size.x), float(rt_size.y), 0.0f, 0.0f));

		auto const vp = LR2D()->getViewport();
		p_effect->setFloat4("viewport", core::Vector4F(vp.a.x, vp.a.y, vp.b.x, vp.b.y));
		return 0;
	}
	uint8_t CLRBinding::render_postEffectAdvanced(
		const char* const ps_name, const char* const rt_name, uint8_t const rt_sampler, uint8_t const blend,
		const double* const cv_data, uint32_t const cv_count,
		const char* const tex0, uint8_t const sv0,
		const char* const tex1, uint8_t const sv1,
		const char* const tex2, uint8_t const sv2,
		const char* const tex3, uint8_t const sv3,
		uint32_t const tex_count)
	{
		if (!LR2D()->isBatchScope())
			return 4;
		auto const b = translate_blend_3d((BlendMode)blend);
		auto const rtsv = (core::Graphics::IRenderer::SamplerState)rt_sampler;

		core::SmartReference<IResourcePostEffectShader> pfx = LRESMGR().FindFX(ps_name);
		if (!pfx) {
			spdlog::error("[clr] posteffect '{}' not found.", ps_name);
			return 1;
		}

		core::SmartReference<IResourceTexture> prt = LRESMGR().FindTexture(rt_name);
		if (!prt) {
			spdlog::error("[clr] texture '{}' not found.", rt_name);
			return 2;
		}
		check_rendertarget_usage(prt);

		core::Vector4F cbdata[8] = {};
		core::ITexture2D* tdata[4] = {};
		core::Graphics::IRenderer::SamplerState tsdata[4] = {};

		uint32_t const ncv = (cv_count <= 8) ? cv_count : 8;
		if (cv_data) {
			for (uint32_t i = 0; i < ncv; i += 1) {
				cbdata[i] = core::Vector4F(
					(float)cv_data[i * 4 + 0],
					(float)cv_data[i * 4 + 1],
					(float)cv_data[i * 4 + 2],
					(float)cv_data[i * 4 + 3]
				);
			}
		}

		uint32_t const ntex = (tex_count <= 4) ? tex_count : 4;
		char const* const names[4] = { tex0, tex1, tex2, tex3 };
		uint8_t const svs[4] = { sv0, sv1, sv2, sv3 };
		for (uint32_t i = 0; i < ntex; i += 1) {
			if (!names[i]) {
				return 3;
			}
			core::SmartReference<IResourceTexture> ptex = LRESMGR().FindTexture(names[i]);
			if (!ptex) {
				spdlog::error("[clr] texture '{}' not found.", names[i]);
				return 3;
			}
			check_rendertarget_usage(ptex);
			tdata[i] = ptex->GetTexture();
			tsdata[i] = (core::Graphics::IRenderer::SamplerState)svs[i];
		}

		LR2D()->drawPostEffect(pfx->GetPostEffectShader(), b, prt->GetTexture(), rtsv, cbdata, ncv, tdata, tsdata, ntex);
		return 0;
	}
}
