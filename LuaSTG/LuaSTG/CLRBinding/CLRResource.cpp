#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"
#include "GameResource/ResourceManager.h"
#include "LMathConstant.hpp"
#include "lua.hpp"

#include <algorithm>
#include <cstring>
#include <string>
#include <string_view>
#include <vector>

// 对应 Lua 侧绑定：
// - LuaSTG/LuaSTG/LuaBinding/LW_ResourceMgr.cpp（lstg.ResourceManager 兼容 API）
// - LuaSTG/LuaSTG/LuaBinding/Resource.cpp（luaopen_LuaSTG_Sub 现代类型 API：
//   ResourceCollection / ResourceTexture / ResourceSprite / ResourceSpriteSequence）
// 错误处理：Lua 侧 luaL_error 的场景改为错误码返回（约定见 API/Resource.hpp 顶部注释），
// 由 C# 侧包装（LuaSTG.Core/ResourceManager.cs / ResourceWrappers.cs）抛出异常。
//
// 句柄约定：uintptr_t 句柄是引擎资源对象（IResourceXxx*）的借用指针，对象归资源池所有。
// 从池中移除资源后句柄失效，C# 包装类通过 Destroy() 主动置空来避免悬垂访问。

using namespace luastg;

namespace
{
	// 返回给 C# 的字符串使用线程局部缓冲（C# 侧在调用返回后立即拷贝）
	thread_local std::string g_enum_res_name_buffer;

	// 粒子定义数组的固定长度（见 API/Resource.hpp 中 res_loadParticleFromInfo 的布局说明）
	constexpr uint32_t kParticleInfoValueCount = 30;

	/// 数值校验 + 转换：0 = None，1 = Global，2 = Stage
	[[nodiscard]] bool tryGetPoolType(int32_t const value, ResourcePoolType* const out)
	{
		switch (value)
		{
		case 0:
			if (out) *out = ResourcePoolType::None;
			return true;
		case 1:
			if (out) *out = ResourcePoolType::Global;
			return true;
		case 2:
			if (out) *out = ResourcePoolType::Stage;
			return true;
		default:
			return false;
		}
	}

	/// 数值校验 + 转换：与 ResourceType 的枚举值一致（Texture=1 ... Model=10）
	[[nodiscard]] bool tryGetResourceType(int32_t const value, ResourceType* const out)
	{
		if (value < 1 || value > 10)
			return false;
		if (out)
			*out = static_cast<ResourceType>(value);
		return true;
	}

	/// 取指定类型的资源池；None / 非法类型返回 nullptr
	[[nodiscard]] ResourcePool* getPool(int32_t const pool_type)
	{
		ResourcePoolType t{};
		if (!tryGetPoolType(pool_type, &t))
			return nullptr;
		return LRES.GetResourcePool(t); // None -> nullptr
	}

	/// SmartReference -> 借用句柄（不增加引用计数，对象归资源池所有）
	template<typename T>
	[[nodiscard]] uintptr_t toHandle(core::SmartReference<T> const& ref)
	{
		return reinterpret_cast<uintptr_t>(ref.get());
	}

	/// 借助临时 Lua 状态枚举资源名（引擎的导出接口只接受 lua_State*），
	/// 语义与 Lua 侧 EnumRes 一致：一次调用得到一份名字数组
	[[nodiscard]] uint32_t enumResourceCount(ResourcePool* const pool, int32_t const res_type)
	{
		if (pool == nullptr)
			return 0;
		ResourceType t{};
		if (!tryGetResourceType(res_type, &t))
			return 0;
		lua_State* const L = luaL_newstate();
		if (L == nullptr)
			return 0;
		uint32_t count = 0;
		pool->ExportResourceList(L, t); // 在栈上留下一个名字数组
		count = static_cast<uint32_t>(lua_objlen(L, 1));
		lua_close(L);
		return count;
	}

	/// 按索引（0 起始）取资源池中的资源名
	[[nodiscard]] bool enumResourceNameByIndex(ResourcePool* const pool, int32_t const res_type, uint32_t const index, std::string& out)
	{
		out.clear();
		if (pool == nullptr)
			return false;
		ResourceType t{};
		if (!tryGetResourceType(res_type, &t))
			return false;
		lua_State* const L = luaL_newstate();
		if (L == nullptr)
			return false;
		bool ok = false;
		pool->ExportResourceList(L, t); // 在栈上留下一个名字数组
		if (index < static_cast<uint32_t>(lua_objlen(L, 1)))
		{
			lua_rawgeti(L, 1, static_cast<int>(index) + 1);
			if (char const* const s = lua_tostring(L, -1))
			{
				out = s;
				ok = true;
			}
			lua_pop(L, 1);
		}
		lua_close(L);
		return ok;
	}
}

namespace luastg
{
	// ===== 资源池状态与全局状态 =====

	int32_t CLRBinding::res_getPoolStatus()
	{
		return static_cast<int32_t>(LRES.GetActivedPoolType());
	}

	uint8_t CLRBinding::res_setPoolStatus(int32_t const pool_type)
	{
		ResourcePoolType t{};
		if (!tryGetPoolType(pool_type, &t))
			return 3;
		LRES.SetActivedPoolType(t);
		return 0;
	}

	double CLRBinding::res_getGlobalImageScale()
	{
		return static_cast<double>(LRES.GetGlobalImageScaleFactor());
	}

	uint8_t CLRBinding::res_setGlobalImageScale(double const scale)
	{
		if (scale == 0.0)
			return 1; // 与 Lua 侧 SetImageScale 的 luaL_error 场景对应
		LRES.SetGlobalImageScaleFactor(static_cast<float>(scale));
		return 0;
	}

	void CLRBinding::res_setResourceLoadingLog(uint8_t const enable)
	{
		ResourceMgr::SetResourceLoadingLog(enable != 0);
	}

	uint8_t CLRBinding::res_getResourceLoadingLog()
	{
		return ResourceMgr::GetResourceLoadingLog() ? 1 : 0;
	}

	// ===== 资源查找（关卡池优先，再到全局池，与 LRES.Find* 一致）=====

	uintptr_t CLRBinding::res_findTexture(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindTexture(name)) : 0;
	}

	uintptr_t CLRBinding::res_findSprite(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindSprite(name)) : 0;
	}

	uintptr_t CLRBinding::res_findAnimation(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindAnimation(name)) : 0;
	}

	uintptr_t CLRBinding::res_findMusic(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindMusic(name)) : 0;
	}

	uintptr_t CLRBinding::res_findSoundEffect(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindSound(name)) : 0;
	}

	uintptr_t CLRBinding::res_findParticle(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindParticle(name)) : 0;
	}

	uintptr_t CLRBinding::res_findSpriteFont(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindSpriteFont(name)) : 0;
	}

	uintptr_t CLRBinding::res_findTrueTypeFont(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindTTFFont(name)) : 0;
	}

	uintptr_t CLRBinding::res_findFX(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindFX(name)) : 0;
	}

	uintptr_t CLRBinding::res_findModel(const char* const name)
	{
		return name != nullptr ? toHandle(LRES.FindModel(name)) : 0;
	}

	// ===== 按句柄取资源信息 =====

	int32_t CLRBinding::res_getResourceType(uintptr_t const handle)
	{
		if (handle == 0)
			return 0;
		return static_cast<int32_t>(reinterpret_cast<IResourceBase*>(handle)->GetType());
	}

	const char* CLRBinding::res_getResourceName(uintptr_t const handle)
	{
		g_enum_res_name_buffer.clear();
		if (handle == 0)
			return "";
		g_enum_res_name_buffer = reinterpret_cast<IResourceBase*>(handle)->GetResName();
		return g_enum_res_name_buffer.c_str();
	}

	// ===== 指定资源池的查找/存在性判断 =====

	uintptr_t CLRBinding::res_poolGetTexture(int32_t const pool_type, const char* const name)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr)
			return 0;
		return toHandle(pool->GetTexture(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_poolGetSprite(int32_t const pool_type, const char* const name)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr)
			return 0;
		return toHandle(pool->GetSprite(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_poolGetAnimation(int32_t const pool_type, const char* const name)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr)
			return 0;
		return toHandle(pool->GetAnimation(std::string_view(name)));
	}

	uint8_t CLRBinding::res_poolCheckResourceExists(int32_t const pool_type, int32_t const res_type, const char* const name)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr)
			return 0;
		ResourceType t{};
		if (!tryGetResourceType(res_type, &t))
			return 0;
		return pool->CheckResourceExists(t, std::string_view(name)) ? 1 : 0;
	}

	int32_t CLRBinding::res_checkRes(int32_t const res_type, const char* const name)
	{
		if (name == nullptr)
			return 0;
		ResourceType t{};
		if (!tryGetResourceType(res_type, &t))
			return 0;
		// 与 Lua 侧一致：先在全局池中寻找再到关卡池中找
		if (LRES.GetResourcePool(ResourcePoolType::Global)->CheckResourceExists(t, std::string_view(name)))
			return static_cast<int32_t>(ResourcePoolType::Global);
		if (LRES.GetResourcePool(ResourcePoolType::Stage)->CheckResourceExists(t, std::string_view(name)))
			return static_cast<int32_t>(ResourcePoolType::Stage);
		return static_cast<int32_t>(ResourcePoolType::None);
	}

	// ===== 资源移除 =====

	uint8_t CLRBinding::res_removeResource(int32_t const pool_type, int32_t const res_type, const char* const name)
	{
		ResourcePoolType pt{};
		if (!tryGetPoolType(pool_type, &pt))
			return 3;
		if (pt == ResourcePoolType::None)
			return 0; // 引擎行为：None 为空操作
		ResourceType rt{};
		if (!tryGetResourceType(res_type, &rt) || name == nullptr)
			return 3;
		LRES.GetResourcePool(pt)->RemoveResource(rt, name);
		return 0;
	}

	uint8_t CLRBinding::res_clearResourcePool(int32_t const pool_type)
	{
		ResourcePoolType pt{};
		if (!tryGetPoolType(pool_type, &pt))
			return 3;
		if (pt == ResourcePoolType::None)
			return 0; // 引擎行为：None 为空操作
		LRES.GetResourcePool(pt)->Clear();
		return 0;
	}

	// ===== 资源枚举 =====

	uint32_t CLRBinding::res_enumResCount(int32_t const pool_type, int32_t const res_type)
	{
		return enumResourceCount(getPool(pool_type), res_type);
	}

	const char* CLRBinding::res_enumResNameByIndex(int32_t const pool_type, int32_t const res_type, uint32_t const index)
	{
		if (!enumResourceNameByIndex(getPool(pool_type), res_type, index, g_enum_res_name_buffer))
			return "";
		return g_enum_res_name_buffer.c_str();
	}

	// ===== 加载/创建 =====

	uintptr_t CLRBinding::res_loadTexture(int32_t const pool_type, const char* const name, const char* const path, uint8_t const mipmaps)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr)
			return 0;
		if (!pool->LoadTexture(name, path, mipmaps != 0))
			return 0;
		return toHandle(pool->GetTexture(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_createSprite(int32_t const pool_type, const char* const name, const char* const texname,
		double const x, double const y, double const w, double const h, double const a, double const b, uint8_t const rect)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || texname == nullptr)
			return 0;
		if (!pool->CreateSprite(name, texname, x, y, w, h, a, b, rect != 0))
			return 0;
		return toHandle(pool->GetSprite(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_copySprite(int32_t const pool_type, const char* const name, const char* const src_name)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || src_name == nullptr)
			return 0;
		if (!pool->CopySprite(name, src_name))
			return 0;
		return toHandle(pool->GetSprite(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_createAnimationFromTexture(int32_t const pool_type, const char* const name, const char* const texname,
		double const x, double const y, double const w, double const h, int32_t const n, int32_t const m, int32_t const intv,
		double const a, double const b, uint8_t const rect)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || texname == nullptr)
			return 0;
		if (!pool->CreateAnimation(name, texname, x, y, w, h, n, m, intv, a, b, rect != 0))
			return 0;
		return toHandle(pool->GetAnimation(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_createAnimationFromSprites(int32_t const pool_type, const char* const name,
		const char** const sprite_names, uint32_t const count, int32_t const intv, double const a, double const b, uint8_t const rect)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || sprite_names == nullptr)
			return 0;
		std::vector<core::SmartReference<IResourceSprite>> sprites;
		sprites.reserve(count);
		for (uint32_t i = 0; i < count; i += 1)
		{
			char const* const sprite_name = sprite_names[i];
			if (sprite_name == nullptr)
				return 0;
			// 与 Lua 侧一致：按名字到资源池中解析精灵
			auto sprite = LRES.FindSprite(sprite_name);
			if (!sprite)
				return 0;
			sprites.push_back(std::move(sprite));
		}
		if (!pool->CreateAnimation(name, sprites, intv, a, b, rect != 0))
			return 0;
		return toHandle(pool->GetAnimation(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_createRenderTarget(int32_t const pool_type, const char* const name,
		int32_t const width, int32_t const height, uint8_t const depth_buffer)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr)
			return 0;
		// 与 Lua 侧一致：显式指定尺寸时必须为正
		if ((width != 0 || height != 0) && (width < 1 || height < 1))
			return 0;
		if (!pool->CreateRenderTarget(name, width, height, depth_buffer != 0))
			return 0;
		return toHandle(pool->GetTexture(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadMusic(int32_t const pool_type, const char* const name, const char* const path,
		double const loop_end, double const loop_duration, uint8_t const once_decode)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr)
			return 0;
		// 与 Lua 侧一致：loop_start = max(0, loop_end - loop_duration)
		double const loop_start = (std::max)(0.0, loop_end - loop_duration);
		if (!pool->LoadMusic(name, path, loop_start, loop_end, once_decode != 0))
			return 0;
		return toHandle(pool->GetMusic(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadSoundEffect(int32_t const pool_type, const char* const name, const char* const path)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr)
			return 0;
		if (!pool->LoadSoundEffect(name, path))
			return 0;
		return toHandle(pool->GetSound(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadParticle(int32_t const pool_type, const char* const name, const char* const path,
		const char* const img_name, double const a, double const b, uint8_t const rect)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr || img_name == nullptr)
			return 0;
		if (!pool->LoadParticle(name, path, img_name, a, b, rect != 0))
			return 0;
		return toHandle(pool->GetParticle(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadParticleFromInfo(int32_t const pool_type, const char* const name,
		const double* const values, uint32_t const value_count, const char* const img_name,
		double const a, double const b, uint8_t const blend_alpha, uint8_t const rect)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || img_name == nullptr)
			return 0;
		if (values == nullptr || value_count != kParticleInfoValueCount)
			return 0;
		// 布局见 API/Resource.hpp 中 res_loadParticleFromInfo 的注释（与 Lua 侧 table 形式一一对应）
		hgeParticleSystemInfo info{};
		info.nEmission = static_cast<int>(values[0]);
		info.fLifetime = static_cast<float>(values[1]);
		info.fDirection = static_cast<float>(values[2]);
		info.fSpread = static_cast<float>(values[3]);
		info.fParticleLifeMin = static_cast<float>(values[4]);
		info.fParticleLifeMax = static_cast<float>(values[5]);
		info.fSpeedMin = static_cast<float>(values[6]);
		info.fSpeedMax = static_cast<float>(values[7]);
		info.fGravityMin = static_cast<float>(values[8]);
		info.fGravityMax = static_cast<float>(values[9]);
		info.fRadialAccelMin = static_cast<float>(values[10]);
		info.fRadialAccelMax = static_cast<float>(values[11]);
		info.fTangentialAccelMin = static_cast<float>(values[12]);
		info.fTangentialAccelMax = static_cast<float>(values[13]);
		info.fSizeStart = static_cast<float>(values[14]);
		info.fSizeEnd = static_cast<float>(values[15]);
		info.fSizeVar = static_cast<float>(values[16]);
		info.fSpinStart = static_cast<float>(values[17]);
		info.fSpinEnd = static_cast<float>(values[18]);
		info.fSpinVar = static_cast<float>(values[19]);
		info.fColorVar = static_cast<float>(values[20]);
		info.fAlphaVar = static_cast<float>(values[21]);
		for (int i = 0; i < 4; i += 1)
		{
			info.colColorStart[i] = static_cast<float>(values[22 + i]);
			info.colColorEnd[i] = static_cast<float>(values[26 + i]);
		}
		// 与 Lua 侧一致：角度 -> 弧度（angle_var 不做转换，保持引擎遗留行为）
		info.fDirection *= static_cast<float>(L_DEG_TO_RAD);
		info.fSpread *= static_cast<float>(L_DEG_TO_RAD);
		info.fSpinStart *= static_cast<float>(L_DEG_TO_RAD);
		info.fSpinEnd *= static_cast<float>(L_DEG_TO_RAD);
		// 与 Lua 侧一致："alpha" -> 6<<16，其余（含 "add" 与缺省）-> 4<<16
		info.iBlendInfo = (blend_alpha != 0) ? (6u << 16) : (4u << 16);
		if (!pool->LoadParticle(name, info, img_name, a, b, rect != 0))
			return 0;
		return toHandle(pool->GetParticle(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadSpriteFont(int32_t const pool_type, const char* const name, const char* const path, uint8_t const mipmaps)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr)
			return 0;
		if (!pool->LoadSpriteFont(name, path, mipmaps != 0))
			return 0;
		return toHandle(pool->GetSpriteFont(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadSpriteFontWithTexture(int32_t const pool_type, const char* const name,
		const char* const path, const char* const tex_path, uint8_t const mipmaps)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr || tex_path == nullptr)
			return 0;
		if (!pool->LoadSpriteFont(name, path, tex_path, mipmaps != 0))
			return 0;
		return toHandle(pool->GetSpriteFont(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadTTFFont(int32_t const pool_type, const char* const name, const char* const path,
		float const width, float const height)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr)
			return 0;
		if (!pool->LoadTTFFont(name, path, width, height))
			return 0;
		return toHandle(pool->GetTTFFont(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadTrueTypeFont(int32_t const pool_type, const char* const name,
		const char** const sources, const uint32_t* const font_faces, const float* const sizes, uint32_t const count)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || count == 0)
			return 0;
		if (sources == nullptr || font_faces == nullptr || sizes == nullptr)
			return 0;
		std::vector<core::Graphics::TrueTypeFontInfo> fonts(count);
		for (uint32_t i = 0; i < count; i += 1)
		{
			auto& font = fonts[i];
			font.source = sources[i] != nullptr ? core::StringView(sources[i]) : core::StringView();
			font.font_face = font_faces[i];
			font.font_size = core::Vector2F(sizes[i * 2], sizes[i * 2 + 1]);
			font.is_force_to_file = false;
			font.is_buffer = false;
		}
		if (!pool->LoadTrueTypeFont(name, fonts.data(), fonts.size()))
			return 0;
		return toHandle(pool->GetTTFFont(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadFX(int32_t const pool_type, const char* const name, const char* const path)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr)
			return 0;
		if (!pool->LoadFX(name, path))
			return 0;
		return toHandle(pool->GetFX(std::string_view(name)));
	}

	uintptr_t CLRBinding::res_loadModel(int32_t const pool_type, const char* const name, const char* const path)
	{
		auto* const pool = getPool(pool_type);
		if (pool == nullptr || name == nullptr || path == nullptr)
			return 0;
		if (!pool->LoadModel(name, path))
			return 0;
		return toHandle(pool->GetModel(std::string_view(name)));
	}

	// ===== 纹理对象访问 =====

	uint8_t CLRBinding::res_tex_getSize(uintptr_t const handle, uint32_t* const out_width, uint32_t* const out_height)
	{
		if (out_width != nullptr)
			*out_width = 0;
		if (out_height != nullptr)
			*out_height = 0;
		if (handle == 0)
			return 1;
		auto const size = reinterpret_cast<IResourceTexture*>(handle)->GetTexture()->getSize();
		if (out_width != nullptr)
			*out_width = size.x;
		if (out_height != nullptr)
			*out_height = size.y;
		return 0;
	}

	uint8_t CLRBinding::res_tex_isRenderTarget(uintptr_t const handle)
	{
		if (handle == 0)
			return 0;
		return reinterpret_cast<IResourceTexture*>(handle)->IsRenderTarget() ? 1 : 0;
	}

	uint8_t CLRBinding::res_tex_setPreMulAlpha(uintptr_t const handle, uint8_t const enable)
	{
		if (handle == 0)
			return 1;
		reinterpret_cast<IResourceTexture*>(handle)->GetTexture()->setPremultipliedAlpha(enable != 0);
		return 0;
	}

	uint8_t CLRBinding::res_tex_setSamplerState(uintptr_t const handle, uint8_t const sampler)
	{
		if (handle == 0)
			return 1;
		if (sampler > static_cast<uint8_t>(core::Graphics::IRenderer::SamplerState::MAX_INDEX))
			return 2;
		auto* const p_sampler = LAPP.getRenderer2D()->getKnownSamplerState(
			static_cast<core::Graphics::IRenderer::SamplerState>(sampler));
		if (p_sampler == nullptr)
			return 2;
		reinterpret_cast<IResourceTexture*>(handle)->GetTexture()->setSamplerState(p_sampler);
		return 0;
	}

	uint8_t CLRBinding::res_getTextureSize(const char* const name, uint32_t* const out_width, uint32_t* const out_height)
	{
		if (out_width != nullptr)
			*out_width = 0;
		if (out_height != nullptr)
			*out_height = 0;
		if (name == nullptr)
			return 1;
		core::Vector2U size{};
		if (!LRES.GetTextureSize(name, size))
			return 1;
		if (out_width != nullptr)
			*out_width = size.x;
		if (out_height != nullptr)
			*out_height = size.y;
		return 0;
	}

	// ===== 精灵对象访问 =====

	void CLRBinding::res_sprite_setCenter(uintptr_t const handle, double const x, double const y)
	{
		if (handle == 0)
			return;
		reinterpret_cast<IResourceSprite*>(handle)->GetSprite()->setTextureCenter(
			core::Vector2F(static_cast<float>(x), static_cast<float>(y)));
	}

	void CLRBinding::res_sprite_setUnitsPerPixel(uintptr_t const handle, double const value)
	{
		if (handle == 0)
			return;
		reinterpret_cast<IResourceSprite*>(handle)->GetSprite()->setUnitsPerPixel(static_cast<float>(value));
	}

	double CLRBinding::res_sprite_getUnitsPerPixel(uintptr_t const handle)
	{
		if (handle == 0)
			return 0.0;
		return static_cast<double>(reinterpret_cast<IResourceSprite*>(handle)->GetSprite()->getUnitsPerPixel());
	}

	uint8_t CLRBinding::res_sprite_getSize(uintptr_t const handle, double* const out_width, double* const out_height)
	{
		if (out_width != nullptr)
			*out_width = 0.0;
		if (out_height != nullptr)
			*out_height = 0.0;
		if (handle == 0)
			return 1;
		auto const rect = reinterpret_cast<IResourceSprite*>(handle)->GetSprite()->getTextureRect();
		if (out_width != nullptr)
			*out_width = static_cast<double>(rect.b.x - rect.a.x);
		if (out_height != nullptr)
			*out_height = static_cast<double>(rect.b.y - rect.a.y);
		return 0;
	}

	void CLRBinding::res_sprite_setBlendMode(uintptr_t const handle, uint8_t const blend)
	{
		if (handle == 0)
			return;
		reinterpret_cast<IResourceSprite*>(handle)->SetBlendMode(static_cast<BlendMode>(blend));
	}

	void CLRBinding::res_sprite_setColor(uintptr_t const handle, uint32_t const c1, uint32_t const c2, uint32_t const c3, uint32_t const c4)
	{
		if (handle == 0)
			return;
		reinterpret_cast<IResourceSprite*>(handle)->SetColor(core::Color4B(c1), core::Color4B(c2), core::Color4B(c3), core::Color4B(c4));
	}

	// ===== 动画对象访问 =====

	uint32_t CLRBinding::res_anim_getCount(uintptr_t const handle)
	{
		if (handle == 0)
			return 0;
		return static_cast<uint32_t>(reinterpret_cast<IResourceAnimation*>(handle)->GetCount());
	}

	uintptr_t CLRBinding::res_anim_getSprite(uintptr_t const handle, uint32_t const index)
	{
		if (handle == 0)
			return 0;
		auto* const p = reinterpret_cast<IResourceAnimation*>(handle);
		if (index >= static_cast<uint32_t>(p->GetCount()))
			return 0;
		return reinterpret_cast<uintptr_t>(p->GetSprite(index));
	}

	uint8_t CLRBinding::res_anim_isSpriteCloned(uintptr_t const handle)
	{
		if (handle == 0)
			return 0;
		return reinterpret_cast<IResourceAnimation*>(handle)->IsSpriteCloned() ? 1 : 0;
	}

	int32_t CLRBinding::res_anim_setScale(uintptr_t const handle, double const value)
	{
		if (handle == 0)
			return 1;
		auto* const p = reinterpret_cast<IResourceAnimation*>(handle);
		if (!p->IsSpriteCloned())
			return 2; // 与 Lua 侧 luaL_error 场景对应
		for (size_t i = 0; i < p->GetCount(); ++i)
			p->GetSprite(static_cast<uint32_t>(i))->GetSprite()->setUnitsPerPixel(static_cast<float>(value));
		return 0;
	}

	int32_t CLRBinding::res_anim_getScale(uintptr_t const handle, double* const out_value)
	{
		if (out_value != nullptr)
			*out_value = 0.0;
		if (handle == 0)
			return 1;
		auto* const p = reinterpret_cast<IResourceAnimation*>(handle);
		if (!p->IsSpriteCloned())
			return 2; // 与 Lua 侧 luaL_error 场景对应
		if (out_value != nullptr)
			*out_value = static_cast<double>(p->GetSprite(0)->GetSprite()->getUnitsPerPixel());
		return 0;
	}

	void CLRBinding::res_anim_setBlendMode(uintptr_t const handle, uint8_t const blend)
	{
		if (handle == 0)
			return;
		reinterpret_cast<IResourceAnimation*>(handle)->SetBlendMode(static_cast<BlendMode>(blend));
	}

	void CLRBinding::res_anim_setVertexColor(uintptr_t const handle, uint32_t const c1, uint32_t const c2, uint32_t const c3, uint32_t const c4)
	{
		if (handle == 0)
			return;
		core::Color4B colors[4] = { core::Color4B(c1), core::Color4B(c2), core::Color4B(c3), core::Color4B(c4) };
		reinterpret_cast<IResourceAnimation*>(handle)->SetVertexColor(colors);
	}

	int32_t CLRBinding::res_anim_setCenter(uintptr_t const handle, double const x, double const y)
	{
		if (handle == 0)
			return 1;
		auto* const p = reinterpret_cast<IResourceAnimation*>(handle);
		if (!p->IsSpriteCloned())
			return 2; // 与 Lua 侧 luaL_error 场景对应
		for (size_t i = 0; i < p->GetCount(); ++i)
		{
			p->GetSprite(static_cast<uint32_t>(i))->GetSprite()->setTextureCenter(core::Vector2F(
				static_cast<float>(x), static_cast<float>(y)));
		}
		return 0;
	}

	// ===== 字体对象访问 =====

	void CLRBinding::res_font_setBlendMode(uintptr_t const handle, uint8_t const blend)
	{
		if (handle == 0)
			return;
		reinterpret_cast<IResourceFont*>(handle)->SetBlendMode(static_cast<BlendMode>(blend));
	}

	void CLRBinding::res_font_setBlendColor(uintptr_t const handle, uint32_t const argb)
	{
		if (handle == 0)
			return;
		reinterpret_cast<IResourceFont*>(handle)->SetBlendColor(core::Color4B(argb));
	}

	void CLRBinding::res_cacheTTFString(const char* const name, const char* const text)
	{
		if (name == nullptr || text == nullptr)
			return;
		LRES.CacheTTFFontString(name, text, std::strlen(text));
	}
}
