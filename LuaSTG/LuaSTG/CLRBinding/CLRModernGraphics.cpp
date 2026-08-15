// LuaSTG CoreCLR 绑定：现代图形类实现
// 对应 Lua 绑定：LuaBinding/modern/RenderTarget.cpp、DepthStencilBuffer.cpp、Texture2D.cpp、
//               Mesh.cpp、MeshRenderer.cpp、VideoDecoder.cpp（+ LuaBinding/VideoBindingHelpers.cpp）、
//               Sprite.cpp、SpriteRenderer.cpp
// 移植时保留引擎调用逻辑，Lua 栈操作（luaL_check/luaL_error）改为错误码与 0 句柄返回
//
// 句柄管理模式：每个 uintptr_t 句柄指向 C++ 侧堆分配的包装结构（对应 Lua 侧 userdata），
// 包装结构内以 core::SmartReference 持有引擎对象引用；MeshRenderer/SpriteRenderer 的包装结构
// 额外持有变换状态（与 Lua userdata 字段一致）。*Release 负责 delete 包装结构并释放引用。

#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"
#include "GameResource/LegacyBlendStateHelper.hpp"
#include "GameResource/Implement/ResourceTextureImpl.hpp"
#include "core/Graphics/Mesh.hpp"
#include "core/Matrix4x4.hpp"
#include "core/VideoDecoder.hpp"

#include <DirectXMath.h>
#include <new>
#include <vector>

using namespace luastg;

namespace
{
	core::Graphics::IRenderer* LR2D() noexcept {
		return LAPP.getRenderer2D();
	}

	// ============ 句柄包装结构（对应 Lua 侧 userdata 结构） ============

	template<typename T>
	struct RefHandle {
		core::SmartReference<T> data;
	};

	struct RenderTargetHandle_t {
		core::SmartReference<core::IRenderTarget> data;
	};
	struct DepthStencilBufferHandle_t {
		core::SmartReference<core::IDepthStencilBuffer> data;
	};
	struct Texture2DHandle_t {
		core::SmartReference<core::ITexture2D> data;
	};
	struct MeshHandle_t {
		core::SmartReference<core::Graphics::IMesh> data;
	};
	struct MeshRendererHandle_t {
		core::SmartReference<core::Graphics::IMeshRenderer> data;
		core::Vector3F position{};
		core::Vector3F scale{};
		core::Vector3F rotation_yaw_pitch_roll{};
	};
	struct VideoDecoderHandle_t {
		core::SmartReference<core::IVideoDecoder> data;
	};
	struct SpriteHandle_t {
		core::SmartReference<core::Graphics::ISprite> data;
	};
	struct SpriteRendererHandle_t {
		core::SmartReference<core::Graphics::ISpriteRenderer> data;
		core::Vector2F position{};
		core::Vector2F scale{};
		float rotation{};
		bool is_dirty{};
	};

	template<typename T>
	[[nodiscard]] T* asHandle(uintptr_t const handle) noexcept {
		return reinterpret_cast<T*>(handle);
	}

	/// @brief 包装一个引擎纹理指针为持有的 Texture2D 句柄（retain，由 mg_textureRelease 释放）
	[[nodiscard]] uintptr_t wrapTexture(core::ITexture2D* const texture) noexcept {
		if (!texture)
			return 0;
		auto* const handle = new (std::nothrow) Texture2DHandle_t();
		if (!handle)
			return 0;
		handle->data = texture; // SmartReference::operator=(T*) 会 retain
		return reinterpret_cast<uintptr_t>(handle);
	}

	/// @brief MeshRenderer 变换重算（对应 Lua 侧 MeshRendererBinding::applyTransform）
	void applyMeshRendererTransform(MeshRendererHandle_t* const self) {
		auto const scale = DirectX::XMMatrixScaling(self->scale.x, self->scale.y, self->scale.z);
		auto const rotation = DirectX::XMMatrixRotationRollPitchYaw(self->rotation_yaw_pitch_roll.y, self->rotation_yaw_pitch_roll.x, self->rotation_yaw_pitch_roll.z);
		auto const position = DirectX::XMMatrixTranslation(self->position.x, self->position.y, self->position.z);
		auto const transform = DirectX::XMMatrixMultiply(DirectX::XMMatrixMultiply(scale, rotation), position);
		DirectX::XMFLOAT4X4A matrix;
		DirectX::XMStoreFloat4x4(&matrix, transform);
		self->data->setTransform(*reinterpret_cast<core::Matrix4F*>(&matrix));
	}

	/// @brief SpriteRenderer 变换应用（对应 Lua 侧 SpriteRendererBinding 的 setTransform/draw 内逻辑）
	void applySpriteRendererTransform(SpriteRendererHandle_t* const self) {
		self->data->setTransform(self->position, self->scale, self->rotation); // TODO: TBD（与 Lua 侧一致）
		self->data->setZ(0.5f); // TODO: allow custom（与 Lua 侧一致）
	}
}

namespace luastg
{
	// ============ RenderTarget ============

	uintptr_t CLRBinding::mg_renderTargetCreate(uint32_t const width, uint32_t const height)
	{
		core::SmartReference<core::IRenderTarget> render_target;
		if (!LAPP.getGraphicsDevice()->createRenderTarget(core::Vector2U(width, height), render_target.put())) {
			spdlog::error("[clr] create RenderTarget ({}x{}) failed", width, height);
			return 0;
		}
		auto* const handle = new (std::nothrow) RenderTargetHandle_t();
		if (!handle) {
			return 0;
		}
		handle->data.attach(render_target.detach());
		return reinterpret_cast<uintptr_t>(handle);
	}
	uint32_t CLRBinding::mg_renderTargetGetWidth(uintptr_t const handle)
	{
		auto* const h = asHandle<RenderTargetHandle_t>(handle);
		return (h && h->data) ? h->data->getTexture()->getSize().x : 0u;
	}
	uint32_t CLRBinding::mg_renderTargetGetHeight(uintptr_t const handle)
	{
		auto* const h = asHandle<RenderTargetHandle_t>(handle);
		return (h && h->data) ? h->data->getTexture()->getSize().y : 0u;
	}
	uintptr_t CLRBinding::mg_renderTargetGetTexture(uintptr_t const handle)
	{
		auto* const h = asHandle<RenderTargetHandle_t>(handle);
		return (h && h->data) ? wrapTexture(h->data->getTexture()) : 0u;
	}
	void CLRBinding::mg_renderTargetRelease(uintptr_t const handle)
	{
		delete asHandle<RenderTargetHandle_t>(handle);
	}

	// ============ DepthStencilBuffer ============

	uintptr_t CLRBinding::mg_depthStencilCreate(uint32_t const width, uint32_t const height)
	{
		core::SmartReference<core::IDepthStencilBuffer> buffer;
		if (!LAPP.getGraphicsDevice()->createDepthStencilBuffer(core::Vector2U(width, height), buffer.put())) {
			spdlog::error("[clr] create DepthStencilBuffer ({}x{}) failed", width, height);
			return 0;
		}
		auto* const handle = new (std::nothrow) DepthStencilBufferHandle_t();
		if (!handle) {
			return 0;
		}
		handle->data.attach(buffer.detach());
		return reinterpret_cast<uintptr_t>(handle);
	}
	uint32_t CLRBinding::mg_depthStencilGetWidth(uintptr_t const handle)
	{
		auto* const h = asHandle<DepthStencilBufferHandle_t>(handle);
		return (h && h->data) ? h->data->getSize().x : 0u;
	}
	uint32_t CLRBinding::mg_depthStencilGetHeight(uintptr_t const handle)
	{
		auto* const h = asHandle<DepthStencilBufferHandle_t>(handle);
		return (h && h->data) ? h->data->getSize().y : 0u;
	}
	void CLRBinding::mg_depthStencilRelease(uintptr_t const handle)
	{
		delete asHandle<DepthStencilBufferHandle_t>(handle);
	}

	// ============ Texture2D ============

	uintptr_t CLRBinding::mg_textureCreateFromFile(const char* const path, uint32_t const mipmap_levels)
	{
		if (!path) {
			return 0;
		}
		core::SmartReference<core::ITexture2D> texture;
		if (!LAPP.getGraphicsDevice()->createTextureFromFile(path, mipmap_levels != 1u, texture.put())) {
			spdlog::error("[clr] create Texture2D from file '{}' failed", path);
			return 0;
		}
		auto* const handle = new (std::nothrow) Texture2DHandle_t();
		if (!handle) {
			return 0;
		}
		handle->data.attach(texture.detach());
		return reinterpret_cast<uintptr_t>(handle);
	}
	uint32_t CLRBinding::mg_textureGetWidth(uintptr_t const handle)
	{
		auto* const h = asHandle<Texture2DHandle_t>(handle);
		return (h && h->data) ? h->data->getSize().x : 0u;
	}
	uint32_t CLRBinding::mg_textureGetHeight(uintptr_t const handle)
	{
		auto* const h = asHandle<Texture2DHandle_t>(handle);
		return (h && h->data) ? h->data->getSize().y : 0u;
	}
	uint8_t CLRBinding::mg_textureSetDefaultSampler(uintptr_t const handle, uint8_t const sampler)
	{
		auto* const h = asHandle<Texture2DHandle_t>(handle);
		if (!h || !h->data) {
			return 2;
		}
		if (sampler > static_cast<uint8_t>(core::Graphics::IRenderer::SamplerState::LinearBorderWhite)) {
			return 1; // unknown sampler
		}
		auto const state = static_cast<core::Graphics::IRenderer::SamplerState>(sampler);
		h->data->setSamplerState(LR2D()->getKnownSamplerState(state));
		return 0;
	}
	void CLRBinding::mg_textureRelease(uintptr_t const handle)
	{
		delete asHandle<Texture2DHandle_t>(handle);
	}

	// ============ Mesh ============

	uintptr_t CLRBinding::mg_meshCreate(
		uint32_t const vertex_count, uint32_t const index_count,
		uint8_t const vertex_position_no_z, uint8_t const vertex_index_compression,
		uint8_t const vertex_color_compression, uint8_t const primitive_topology)
	{
		core::Graphics::MeshOptions options;
		options.vertex_count = vertex_count;
		options.index_count = index_count;
		options.vertex_position_no_z = vertex_position_no_z != 0;
		options.vertex_index_compression = vertex_index_compression != 0;
		options.vertex_color_compression = vertex_color_compression != 0;
		options.primitive_topology = static_cast<core::Graphics::PrimitiveTopology>(primitive_topology);

		core::Graphics::IMesh* mesh = nullptr;
		if (!core::Graphics::IMesh::create(LAPP.getGraphicsDevice(), options, &mesh)) {
			spdlog::error("[clr] create Mesh failed.");
			return 0;
		}
		auto* const handle = new (std::nothrow) MeshHandle_t();
		if (!handle) {
			mesh->release();
			return 0;
		}
		handle->data.attach(mesh);
		return reinterpret_cast<uintptr_t>(handle);
	}
	uint32_t CLRBinding::mg_meshGetVertexCount(uintptr_t const handle)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		return (h && h->data) ? h->data->getVertexCount() : 0u;
	}
	uint32_t CLRBinding::mg_meshGetIndexCount(uintptr_t const handle)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		return (h && h->data) ? h->data->getIndexCount() : 0u;
	}
	uint8_t CLRBinding::mg_meshGetPrimitiveTopology(uintptr_t const handle)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		return (h && h->data) ? static_cast<uint8_t>(h->data->getPrimitiveTopology()) : 0u;
	}
	uint8_t CLRBinding::mg_meshIsReadOnly(uintptr_t const handle)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		return (h && h->data && h->data->isReadOnly()) ? 1u : 0u;
	}
	void CLRBinding::mg_meshSetVertex3(uintptr_t const handle, uint32_t const vertex_index, float const x, float const y, float const z, float const u, float const v, uint32_t const argb)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setVertex(vertex_index, core::Vector3F(x, y, z), core::Vector2F(u, v), core::Color4B(argb));
		}
	}
	void CLRBinding::mg_meshSetVertex3F(uintptr_t const handle, uint32_t const vertex_index, float const x, float const y, float const z, float const u, float const v, float const r, float const g, float const b, float const a)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setVertex(vertex_index, core::Vector3F(x, y, z), core::Vector2F(u, v), core::Vector4F(r, g, b, a));
		}
	}
	void CLRBinding::mg_meshSetVertex2(uintptr_t const handle, uint32_t const vertex_index, float const x, float const y, float const u, float const v, uint32_t const argb)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setVertex(vertex_index, core::Vector2F(x, y), core::Vector2F(u, v), core::Color4B(argb));
		}
	}
	void CLRBinding::mg_meshSetVertex2F(uintptr_t const handle, uint32_t const vertex_index, float const x, float const y, float const u, float const v, float const r, float const g, float const b, float const a)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setVertex(vertex_index, core::Vector2F(x, y), core::Vector2F(u, v), core::Vector4F(r, g, b, a));
		}
	}
	void CLRBinding::mg_meshSetPosition3(uintptr_t const handle, uint32_t const vertex_index, float const x, float const y, float const z)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setPosition(vertex_index, core::Vector3F(x, y, z));
		}
	}
	void CLRBinding::mg_meshSetPosition2(uintptr_t const handle, uint32_t const vertex_index, float const x, float const y)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setPosition(vertex_index, core::Vector2F(x, y));
		}
	}
	void CLRBinding::mg_meshSetUv(uintptr_t const handle, uint32_t const vertex_index, float const u, float const v)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setUv(vertex_index, core::Vector2F(u, v));
		}
	}
	void CLRBinding::mg_meshSetColor(uintptr_t const handle, uint32_t const vertex_index, uint32_t const argb)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setColor(vertex_index, core::Color4B(argb));
		}
	}
	void CLRBinding::mg_meshSetColorF(uintptr_t const handle, uint32_t const vertex_index, float const r, float const g, float const b, float const a)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setColor(vertex_index, core::Vector4F(r, g, b, a));
		}
	}
	void CLRBinding::mg_meshSetIndex(uintptr_t const handle, uint32_t const index_index, uint32_t const vertex_index)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setIndex(index_index, vertex_index);
		}
	}
	uint8_t CLRBinding::mg_meshCommit(uintptr_t const handle)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		return (h && h->data && h->data->commit()) ? 1u : 0u;
	}
	void CLRBinding::mg_meshSetReadOnly(uintptr_t const handle)
	{
		auto* const h = asHandle<MeshHandle_t>(handle);
		if (h && h->data) {
			h->data->setReadOnly();
		}
	}
	void CLRBinding::mg_meshRelease(uintptr_t const handle)
	{
		delete asHandle<MeshHandle_t>(handle);
	}

	// ============ MeshRenderer ============

	uintptr_t CLRBinding::mg_meshRendererCreate()
	{
		core::Graphics::IMeshRenderer* renderer = nullptr;
		if (!core::Graphics::IMeshRenderer::create(LAPP.getGraphicsDevice(), &renderer)) {
			spdlog::error("[clr] create MeshRenderer failed.");
			return 0;
		}
		auto* const handle = new (std::nothrow) MeshRendererHandle_t();
		if (!handle) {
			renderer->release();
			return 0;
		}
		handle->data.attach(renderer);
		return reinterpret_cast<uintptr_t>(handle);
	}
	void CLRBinding::mg_meshRendererSetPosition(uintptr_t const handle, float const x, float const y, float const z)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (!h || !h->data) {
			return;
		}
		h->position = core::Vector3F(x, y, z);
		applyMeshRendererTransform(h);
	}
	void CLRBinding::mg_meshRendererSetScale(uintptr_t const handle, float const x, float const y, float const z)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (!h || !h->data) {
			return;
		}
		h->scale = core::Vector3F(x, y, z);
		applyMeshRendererTransform(h);
	}
	void CLRBinding::mg_meshRendererSetRotationYawPitchRoll(uintptr_t const handle, float const yaw, float const pitch, float const roll)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (!h || !h->data) {
			return;
		}
		h->rotation_yaw_pitch_roll = core::Vector3F(yaw, pitch, roll);
		applyMeshRendererTransform(h);
	}
	uint8_t CLRBinding::mg_meshRendererSetMesh(uintptr_t const handle, uintptr_t const mesh)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (!h || !h->data) {
			return 2;
		}
		if (mesh == 0) {
			h->data->setMesh(nullptr);
			return 0;
		}
		auto* const m = asHandle<MeshHandle_t>(mesh);
		if (!m || !m->data) {
			return 2;
		}
		h->data->setMesh(m->data.get());
		return 0;
	}
	uint8_t CLRBinding::mg_meshRendererSetTexture(uintptr_t const handle, uintptr_t const texture)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (!h || !h->data) {
			return 2;
		}
		if (texture == 0) {
			h->data->setTexture(nullptr);
			return 0;
		}
		auto* const t = asHandle<Texture2DHandle_t>(texture);
		if (!t || !t->data) {
			return 2;
		}
		h->data->setTexture(t->data.get());
		return 0;
	}
	uint8_t CLRBinding::mg_meshRendererSetTextureByName(uintptr_t const handle, const char* const name)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (!h || !h->data || !name) {
			return 2;
		}
		// 按资源名从资源池查找（对应 Lua 侧 setTexture 的字符串参数形式）
		core::SmartReference<IResourceTexture> texture_resource = LAPP.GetResourceMgr().FindTexture(name);
		if (!texture_resource) {
			spdlog::error("[clr] can't find texture '{}'", name);
			return 1;
		}
		h->data->setTexture(texture_resource->GetTexture());
		return 0;
	}
	void CLRBinding::mg_meshRendererSetLegacyBlendState(uintptr_t const handle, uint8_t const blend)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (!h || !h->data) {
			return;
		}
		auto const [v, b] = translateLegacyBlendState((BlendMode)blend);
		h->data->setLegacyBlendState(v, b);
	}
	void CLRBinding::mg_meshRendererDraw(uintptr_t const handle)
	{
		auto* const h = asHandle<MeshRendererHandle_t>(handle);
		if (h && h->data) {
			h->data->draw(LR2D());
		}
	}
	void CLRBinding::mg_meshRendererRelease(uintptr_t const handle)
	{
		delete asHandle<MeshRendererHandle_t>(handle);
	}

	// ============ VideoDecoder ============

	uintptr_t CLRBinding::mg_videoCreate(
		const char* const path,
		uint32_t const video_stream, uint32_t const width, uint32_t const height,
		uint8_t const premultiplied_alpha, uint8_t const looping,
		double const loop_end, double const loop_duration)
	{
		if (!path) {
			return 0;
		}
		core::VideoOpenOptions opt;
		opt.video_stream_index = video_stream;
		opt.output_width = width;
		opt.output_height = height;
		opt.premultiplied_alpha = premultiplied_alpha != 0;
		opt.looping = looping != 0;
		opt.loop_end = loop_end;
		opt.loop_duration = loop_duration;

		core::SmartReference<core::IVideoDecoder> decoder;
		if (!LAPP.getGraphicsDevice()->createVideoDecoder(decoder.put())) {
			spdlog::error("[clr] create VideoDecoder from file '{}' failed", path);
			return 0;
		}
		if (!decoder->open(path, opt)) {
			spdlog::error("[clr] create VideoDecoder from file '{}' failed", path);
			return 0;
		}
		auto* const handle = new (std::nothrow) VideoDecoderHandle_t();
		if (!handle) {
			return 0;
		}
		handle->data.attach(decoder.detach());
		return reinterpret_cast<uintptr_t>(handle);
	}
	uint32_t CLRBinding::mg_videoGetWidth(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data) ? h->data->getVideoSize().x : 0u;
	}
	uint32_t CLRBinding::mg_videoGetHeight(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data) ? h->data->getVideoSize().y : 0u;
	}
	double CLRBinding::mg_videoGetDuration(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data) ? h->data->getDuration() : 0.0;
	}
	double CLRBinding::mg_videoGetCurrentTime(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data) ? h->data->getCurrentTime() : 0.0;
	}
	double CLRBinding::mg_videoGetFPS(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			return 0.0;
		}
		auto const interval = h->data->getFrameInterval();
		return interval > 0.0 ? 1.0 / interval : 0.0;
	}
	uint8_t CLRBinding::mg_videoIsLooping(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data && h->data->isLooping()) ? 1u : 0u;
	}
	uintptr_t CLRBinding::mg_videoGetTexture(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data) ? wrapTexture(h->data->getTexture()) : 0u;
	}
	uint8_t CLRBinding::mg_videoSeek(uintptr_t const handle, double const time)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data && h->data->seek(time)) ? 1u : 0u;
	}
	uint8_t CLRBinding::mg_videoUpdate(uintptr_t const handle, double const time)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data && h->data->updateToTime(time)) ? 1u : 0u;
	}
	void CLRBinding::mg_videoSetLooping(uintptr_t const handle, uint8_t const loop)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (h && h->data) {
			h->data->setLooping(loop != 0);
		}
	}
	void CLRBinding::mg_videoSetLoopRange(uintptr_t const handle, double const loop_end, double const loop_duration)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (h && h->data) {
			h->data->setLoopRange(loop_end, loop_duration);
		}
	}
	void CLRBinding::mg_videoGetLoopRange(uintptr_t const handle, double* const loop_end, double* const loop_duration)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			if (loop_end) *loop_end = 0.0;
			if (loop_duration) *loop_duration = 0.0;
			return;
		}
		double end = 0.0, duration = 0.0;
		h->data->getLoopRange(&end, &duration);
		if (loop_end) *loop_end = end;
		if (loop_duration) *loop_duration = duration;
	}
	uint32_t CLRBinding::mg_videoGetVideoStreamCount(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			return 0u;
		}
		std::vector<core::VideoStreamInfo> list;
		h->data->getVideoStreams([](core::VideoStreamInfo const& info, void* userdata) {
			static_cast<std::vector<core::VideoStreamInfo>*>(userdata)->push_back(info);
		}, &list);
		return static_cast<uint32_t>(list.size());
	}
	uint8_t CLRBinding::mg_videoGetVideoStream(uintptr_t const handle, uint32_t const index, uint32_t* const stream_index, uint32_t* const width, uint32_t* const height, double* const fps, double* const duration)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			return 1;
		}
		std::vector<core::VideoStreamInfo> list;
		h->data->getVideoStreams([](core::VideoStreamInfo const& info, void* userdata) {
			static_cast<std::vector<core::VideoStreamInfo>*>(userdata)->push_back(info);
		}, &list);
		if (index >= list.size()) {
			return 1;
		}
		auto const& info = list[index];
		if (stream_index) *stream_index = info.index;
		if (width) *width = info.width;
		if (height) *height = info.height;
		if (fps) *fps = info.fps;
		if (duration) *duration = info.duration_seconds;
		return 0;
	}
	uint32_t CLRBinding::mg_videoGetAudioStreamCount(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			return 0u;
		}
		std::vector<core::AudioStreamInfo> list;
		h->data->getAudioStreams([](core::AudioStreamInfo const& info, void* userdata) {
			static_cast<std::vector<core::AudioStreamInfo>*>(userdata)->push_back(info);
		}, &list);
		return static_cast<uint32_t>(list.size());
	}
	uint8_t CLRBinding::mg_videoGetAudioStream(uintptr_t const handle, uint32_t const index, uint32_t* const stream_index, uint32_t* const channels, uint32_t* const sample_rate, double* const duration)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			return 1;
		}
		std::vector<core::AudioStreamInfo> list;
		h->data->getAudioStreams([](core::AudioStreamInfo const& info, void* userdata) {
			static_cast<std::vector<core::AudioStreamInfo>*>(userdata)->push_back(info);
		}, &list);
		if (index >= list.size()) {
			return 1;
		}
		auto const& info = list[index];
		if (stream_index) *stream_index = info.index;
		if (channels) *channels = info.channels;
		if (sample_rate) *sample_rate = info.sample_rate;
		if (duration) *duration = info.duration_seconds;
		return 0;
	}
	uint32_t CLRBinding::mg_videoGetVideoStreamIndex(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		return (h && h->data) ? h->data->getVideoStreamIndex() : 0u;
	}
	uint8_t CLRBinding::mg_videoReopen(uintptr_t const handle, uint32_t const video_stream, uint32_t const width, uint32_t const height, uint8_t const premultiplied_alpha, uint8_t const looping, double const loop_end, double const loop_duration)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			return 0u;
		}
		core::VideoOpenOptions opt = h->data->getLastOpenOptions();
		opt.video_stream_index = video_stream;
		opt.output_width = width;
		opt.output_height = height;
		opt.premultiplied_alpha = premultiplied_alpha != 0;
		opt.looping = looping != 0;
		opt.loop_end = loop_end;
		opt.loop_duration = loop_duration;
		return h->data->reopen(opt) ? 1u : 0u;
	}
	uint8_t CLRBinding::mg_videoReopenLast(uintptr_t const handle)
	{
		auto* const h = asHandle<VideoDecoderHandle_t>(handle);
		if (!h || !h->data) {
			return 0u;
		}
		return h->data->reopen(h->data->getLastOpenOptions()) ? 1u : 0u;
	}
	void CLRBinding::mg_videoRelease(uintptr_t const handle)
	{
		delete asHandle<VideoDecoderHandle_t>(handle);
	}

	// ============ Sprite ============

	uintptr_t CLRBinding::mg_spriteCreate(uintptr_t const texture, float const x, float const y, float const width, float const height, uint8_t const has_center, float const center_x, float const center_y, float const unit_per_pixel)
	{
		auto* const tex = asHandle<Texture2DHandle_t>(texture);
		if (!tex || !tex->data) {
			spdlog::error("[clr] create Sprite failed: invalid texture handle");
			return 0;
		}
		auto* const handle = new (std::nothrow) SpriteHandle_t();
		if (!handle) {
			return 0;
		}
		core::Graphics::ISprite* sprite = nullptr;
		if (!core::Graphics::ISprite::create(tex->data.get(), &sprite)) {
			delete handle;
			spdlog::error("[clr] create Sprite failed");
			return 0;
		}
		handle->data.attach(sprite);
		handle->data->setTexture(tex->data.get());
		handle->data->setTextureRect(core::RectF(x, y, x + width, y + height));
		if (has_center != 0) {
			handle->data->setTextureCenter(core::Vector2F(center_x, center_y));
		}
		handle->data->setUnitsPerPixel(unit_per_pixel);
		return reinterpret_cast<uintptr_t>(handle);
	}
	void CLRBinding::mg_spriteSetTexture(uintptr_t const handle, uintptr_t const texture)
	{
		auto* const h = asHandle<SpriteHandle_t>(handle);
		auto* const t = asHandle<Texture2DHandle_t>(texture);
		if (h && h->data && t && t->data) {
			h->data->setTexture(t->data.get());
		}
	}
	uintptr_t CLRBinding::mg_spriteGetTexture(uintptr_t const handle)
	{
		auto* const h = asHandle<SpriteHandle_t>(handle);
		return (h && h->data) ? wrapTexture(h->data->getTexture()) : 0u;
	}
	void CLRBinding::mg_spriteSetTextureRect(uintptr_t const handle, float const x, float const y, float const width, float const height)
	{
		// 注：Lua 侧此处为 core::RectF(x, y, x + width, x + height)，属笔误，移植时修正为 y + height
		auto* const h = asHandle<SpriteHandle_t>(handle);
		if (h && h->data) {
			h->data->setTextureRect(core::RectF(x, y, x + width, y + height));
		}
	}
	void CLRBinding::mg_spriteGetTextureRect(uintptr_t const handle, float* const x, float* const y, float* const width, float* const height)
	{
		auto* const h = asHandle<SpriteHandle_t>(handle);
		if (!h || !h->data) {
			if (x) *x = 0.0f;
			if (y) *y = 0.0f;
			if (width) *width = 0.0f;
			if (height) *height = 0.0f;
			return;
		}
		auto const rect = h->data->getTextureRect();
		if (x) *x = rect.a.x;
		if (y) *y = rect.a.y;
		if (width) *width = rect.b.x - rect.a.x;
		if (height) *height = rect.b.y - rect.a.y;
	}
	void CLRBinding::mg_spriteSetCenter(uintptr_t const handle, float const x, float const y)
	{
		auto* const h = asHandle<SpriteHandle_t>(handle);
		if (h && h->data) {
			h->data->setTextureCenter(core::Vector2F(x, y));
		}
	}
	void CLRBinding::mg_spriteGetCenter(uintptr_t const handle, float* const x, float* const y)
	{
		auto* const h = asHandle<SpriteHandle_t>(handle);
		if (!h || !h->data) {
			if (x) *x = 0.0f;
			if (y) *y = 0.0f;
			return;
		}
		auto const center = h->data->getTextureCenter();
		if (x) *x = center.x;
		if (y) *y = center.y;
	}
	void CLRBinding::mg_spriteSetUnitPerPixel(uintptr_t const handle, float const value)
	{
		auto* const h = asHandle<SpriteHandle_t>(handle);
		if (h && h->data) {
			h->data->setUnitsPerPixel(value);
		}
	}
	float CLRBinding::mg_spriteGetUnitPerPixel(uintptr_t const handle)
	{
		auto* const h = asHandle<SpriteHandle_t>(handle);
		return (h && h->data) ? h->data->getUnitsPerPixel() : 0.0f;
	}
	void CLRBinding::mg_spriteRelease(uintptr_t const handle)
	{
		delete asHandle<SpriteHandle_t>(handle);
	}

	// ============ SpriteRenderer ============

	uintptr_t CLRBinding::mg_spriteRendererCreate()
	{
		core::Graphics::ISpriteRenderer* renderer = nullptr;
		if (!core::Graphics::ISpriteRenderer::create(&renderer)) {
			spdlog::error("[clr] create SpriteRenderer failed.");
			return 0;
		}
		auto* const handle = new (std::nothrow) SpriteRendererHandle_t();
		if (!handle) {
			renderer->release();
			return 0;
		}
		handle->data.attach(renderer);
		return reinterpret_cast<uintptr_t>(handle);
	}
	void CLRBinding::mg_spriteRendererSetTransform(uintptr_t const handle, float const x, float const y, float const rotation, float const scale_x, float const scale_y)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (!h || !h->data) {
			return;
		}
		h->position = core::Vector2F(x, y);
		h->rotation = rotation;
		h->scale = core::Vector2F(scale_x, scale_y);
		applySpriteRendererTransform(h);
		h->is_dirty = false;
	}
	void CLRBinding::mg_spriteRendererSetPosition(uintptr_t const handle, float const x, float const y)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (h && h->data) {
			h->position = core::Vector2F(x, y);
			h->is_dirty = true;
		}
	}
	void CLRBinding::mg_spriteRendererSetScale(uintptr_t const handle, float const x, float const y)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (h && h->data) {
			h->scale = core::Vector2F(x, y);
			h->is_dirty = true;
		}
	}
	void CLRBinding::mg_spriteRendererSetRotation(uintptr_t const handle, float const rotation)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (h && h->data) {
			h->rotation = rotation;
			h->is_dirty = true;
		}
	}
	void CLRBinding::mg_spriteRendererSetSprite(uintptr_t const handle, uintptr_t const sprite)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		auto* const s = asHandle<SpriteHandle_t>(sprite);
		if (h && h->data && s && s->data) {
			h->data->setSprite(s->data.get());
		}
	}
	void CLRBinding::mg_spriteRendererSetColor(uintptr_t const handle, uint32_t const argb)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (h && h->data) {
			h->data->setColor(core::Color4B(argb));
		}
	}
	void CLRBinding::mg_spriteRendererSetColor4(uintptr_t const handle, uint32_t const c1, uint32_t const c2, uint32_t const c3, uint32_t const c4)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (h && h->data) {
			h->data->setColor(core::Color4B(c1), core::Color4B(c2), core::Color4B(c3), core::Color4B(c4));
		}
	}
	void CLRBinding::mg_spriteRendererSetLegacyBlendState(uintptr_t const handle, uint8_t const blend)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (!h || !h->data) {
			return;
		}
		auto const [v, b] = translateLegacyBlendState((BlendMode)blend);
		h->data->setLegacyBlendState(v, b);
	}
	void CLRBinding::mg_spriteRendererDraw(uintptr_t const handle)
	{
		auto* const h = asHandle<SpriteRendererHandle_t>(handle);
		if (!h || !h->data) {
			return;
		}
		if (h->is_dirty) {
			applySpriteRendererTransform(h);
		}
		h->data->draw(LR2D());
	}
	void CLRBinding::mg_spriteRendererRelease(uintptr_t const handle)
	{
		delete asHandle<SpriteRendererHandle_t>(handle);
	}

	// ============ SpriteRectRenderer ============

	uintptr_t CLRBinding::mg_spriteRectRendererCreate()
	{
		core::Graphics::ISpriteRenderer* renderer = nullptr;
		if (!core::Graphics::ISpriteRenderer::create(&renderer)) {
			spdlog::error("[clr] create SpriteRectRenderer failed.");
			return 0;
		}
		auto* const handle = new (std::nothrow) RefHandle<core::Graphics::ISpriteRenderer>();
		if (!handle) {
			renderer->release();
			return 0;
		}
		handle->data.attach(renderer);
		return reinterpret_cast<uintptr_t>(handle);
	}
	void CLRBinding::mg_spriteRectRendererSetRect(uintptr_t const handle, float const left, float const right, float const bottom, float const top)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->setTransform(core::RectF(left, top, right, bottom));
		}
	}
	void CLRBinding::mg_spriteRectRendererSetSprite(uintptr_t const handle, uintptr_t const sprite)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		auto* const s = asHandle<SpriteHandle_t>(sprite);
		if (h && h->data && s && s->data) {
			h->data->setSprite(s->data.get());
		}
	}
	void CLRBinding::mg_spriteRectRendererSetColor(uintptr_t const handle, uint32_t const argb)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->setColor(core::Color4B(argb));
		}
	}
	void CLRBinding::mg_spriteRectRendererSetColor4(uintptr_t const handle, uint32_t const c1, uint32_t const c2, uint32_t const c3, uint32_t const c4)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->setColor(core::Color4B(c1), core::Color4B(c2), core::Color4B(c3), core::Color4B(c4));
		}
	}
	void CLRBinding::mg_spriteRectRendererSetLegacyBlendState(uintptr_t const handle, uint8_t const blend)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (!h || !h->data) {
			return;
		}
		auto const [v, b] = translateLegacyBlendState((BlendMode)blend);
		h->data->setLegacyBlendState(v, b);
	}
	void CLRBinding::mg_spriteRectRendererDraw(uintptr_t const handle)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->draw(LR2D());
		}
	}
	void CLRBinding::mg_spriteRectRendererRelease(uintptr_t const handle)
	{
		delete asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
	}

	// ============ SpriteQuadRenderer ============

	uintptr_t CLRBinding::mg_spriteQuadRendererCreate()
	{
		core::Graphics::ISpriteRenderer* renderer = nullptr;
		if (!core::Graphics::ISpriteRenderer::create(&renderer)) {
			spdlog::error("[clr] create SpriteQuadRenderer failed.");
			return 0;
		}
		auto* const handle = new (std::nothrow) RefHandle<core::Graphics::ISpriteRenderer>();
		if (!handle) {
			renderer->release();
			return 0;
		}
		handle->data.attach(renderer);
		return reinterpret_cast<uintptr_t>(handle);
	}
	void CLRBinding::mg_spriteQuadRendererSetQuad2(uintptr_t const handle, float const x1, float const y1, float const x2, float const y2, float const x3, float const y3, float const x4, float const y4)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->setTransform(
				core::Vector2F(x1, y1),
				core::Vector2F(x2, y2),
				core::Vector2F(x3, y3),
				core::Vector2F(x4, y4)
			);
			h->data->setZ(0.5f); // TODO: allow custom（与 Lua 侧一致）
		}
	}
	void CLRBinding::mg_spriteQuadRendererSetQuad3(uintptr_t const handle, float const x1, float const y1, float const z1, float const x2, float const y2, float const z2, float const x3, float const y3, float const z3, float const x4, float const y4, float const z4)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->setTransform(
				core::Vector3F(x1, y1, z1),
				core::Vector3F(x2, y2, z2),
				core::Vector3F(x3, y3, z3),
				core::Vector3F(x4, y4, z4)
			);
			h->data->setZ(0.5f); // TODO: allow custom（与 Lua 侧一致）
		}
	}
	void CLRBinding::mg_spriteQuadRendererSetSprite(uintptr_t const handle, uintptr_t const sprite)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		auto* const s = asHandle<SpriteHandle_t>(sprite);
		if (h && h->data && s && s->data) {
			h->data->setSprite(s->data.get());
		}
	}
	void CLRBinding::mg_spriteQuadRendererSetColor(uintptr_t const handle, uint32_t const argb)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->setColor(core::Color4B(argb));
		}
	}
	void CLRBinding::mg_spriteQuadRendererSetColor4(uintptr_t const handle, uint32_t const c1, uint32_t const c2, uint32_t const c3, uint32_t const c4)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->setColor(core::Color4B(c1), core::Color4B(c2), core::Color4B(c3), core::Color4B(c4));
		}
	}
	void CLRBinding::mg_spriteQuadRendererSetLegacyBlendState(uintptr_t const handle, uint8_t const blend)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (!h || !h->data) {
			return;
		}
		auto const [v, b] = translateLegacyBlendState((BlendMode)blend);
		h->data->setLegacyBlendState(v, b);
	}
	void CLRBinding::mg_spriteQuadRendererDraw(uintptr_t const handle)
	{
		auto* const h = asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
		if (h && h->data) {
			h->data->draw(LR2D());
		}
	}
	void CLRBinding::mg_spriteQuadRendererRelease(uintptr_t const handle)
	{
		delete asHandle<RefHandle<core::Graphics::ISpriteRenderer>>(handle);
	}
	uint8_t CLRBinding::mg_renderTargetPushToStack(uintptr_t const rt_handle, uintptr_t const ds_handle)
	{
		auto* ctx = LR2D();
		if (!ctx->isBatchScope())
			return 1;
		auto* const rt = asHandle<RenderTargetHandle_t>(rt_handle);
		if (rt_handle == 0 || rt == nullptr || !rt->data)
			return 2;
		DepthStencilBufferHandle_t* ds = nullptr;
		if (ds_handle != 0) {
			ds = asHandle<DepthStencilBufferHandle_t>(ds_handle);
			if (ds == nullptr || !ds->data)
				return 2;
			if (rt->data->getTexture()->getSize() != ds->data->getSize())
				return 3;
		}
		ctx->flush();
		core::SmartReference<IResourceTexture> texture;
		texture.attach(new luastg::RenderTargetStackResourceTextureImpl(rt->data.get(), ds ? ds->data.get() : nullptr));
		if (!LAPP.GetRenderTargetManager()->PushRenderTarget(texture.get())) {
			return 4;
		}
		ctx->setViewportAndScissorRect();
		return 0;
	}
}
