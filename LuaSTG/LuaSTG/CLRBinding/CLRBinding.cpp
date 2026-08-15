#include "CLRBinding/CLRBinding.hpp"

#include <windows.h>
#include <spdlog/spdlog.h>
#include <cstddef>
#include <string>
#include <string_view>

#include "AppFrame.h"
#include "LConfig.h"
#include "GameObject/GameObjectPool.h"
#include "utf8.hpp"

using namespace luastg;

namespace
{
	ManagedAPI g_clr_managed{};

	CLRGameObjectLayoutInfo const g_gameobject_layout = {
		.api_version = 1,
		.struct_size = static_cast<uint32_t>(sizeof(GameObject)),
		.pool_size = LOBJPOOL_SIZE,
		.group_count = LOBJPOOL_GROUPN,
		.feature_flags = 0
#ifdef USING_MULTI_GAME_WORLD
			| (1u << 0)
#endif
#ifdef USER_SYSTEM_OPERATION
			| (1u << 1)
#endif
#ifdef GLOBAL_SCALE_COLLI_SHAPE
			| (1u << 2)
#endif
#ifdef LUASTG_GAME_OBJECT_PARTICLE_SYSTEM_OBJECT
			| (1u << 3)
#endif
#ifdef LUASTG_ENABLE_GAME_OBJECT_PROPERTY_PAUSE
			| (1u << 4)
#endif
		,
		.offset_id = static_cast<uint32_t>(offsetof(GameObject, last_x) - sizeof(double)), // id 位域与 last_x 相邻
		.offset_last_x = static_cast<uint32_t>(offsetof(GameObject, last_x)),
		.offset_last_y = static_cast<uint32_t>(offsetof(GameObject, last_y)),
		.offset_x = static_cast<uint32_t>(offsetof(GameObject, x)),
		.offset_y = static_cast<uint32_t>(offsetof(GameObject, y)),
		.offset_dx = static_cast<uint32_t>(offsetof(GameObject, dx)),
		.offset_dy = static_cast<uint32_t>(offsetof(GameObject, dy)),
		.offset_vx = static_cast<uint32_t>(offsetof(GameObject, vx)),
		.offset_vy = static_cast<uint32_t>(offsetof(GameObject, vy)),
		.offset_ax = static_cast<uint32_t>(offsetof(GameObject, ax)),
		.offset_ay = static_cast<uint32_t>(offsetof(GameObject, ay)),
#ifdef USER_SYSTEM_OPERATION
		.offset_max_vx = static_cast<uint32_t>(offsetof(GameObject, max_vx)),
		.offset_max_vy = static_cast<uint32_t>(offsetof(GameObject, max_vy)),
		.offset_max_v = static_cast<uint32_t>(offsetof(GameObject, max_v)),
		.offset_ag = static_cast<uint32_t>(offsetof(GameObject, ag)),
#else
		.offset_max_vx = 0,
		.offset_max_vy = 0,
		.offset_max_v = 0,
		.offset_ag = 0,
#endif
		.offset_group = static_cast<uint32_t>(offsetof(GameObject, group)),
		.offset_a = static_cast<uint32_t>(offsetof(GameObject, a)),
		.offset_b = static_cast<uint32_t>(offsetof(GameObject, b)),
		.offset_col_r = static_cast<uint32_t>(offsetof(GameObject, col_r)),
		.offset_layer = static_cast<uint32_t>(offsetof(GameObject, layer)),
		.offset_hscale = static_cast<uint32_t>(offsetof(GameObject, hscale)),
		.offset_vscale = static_cast<uint32_t>(offsetof(GameObject, vscale)),
		.offset_rot = static_cast<uint32_t>(offsetof(GameObject, rot)),
		.offset_omega = static_cast<uint32_t>(offsetof(GameObject, omega)),
		.offset_ani_timer = static_cast<uint32_t>(offsetof(GameObject, ani_timer)),
		.offset_res = static_cast<uint32_t>(offsetof(GameObject, res)),
		.offset_ps = static_cast<uint32_t>(offsetof(GameObject, ps)),
		.offset_timer = static_cast<uint32_t>(offsetof(GameObject, timer)),
		.offset_vertex_color = static_cast<uint32_t>(offsetof(GameObject, vertex_color)),
		.offset_blend_mode = static_cast<uint32_t>(offsetof(GameObject, blend_mode)),
		.offset_features = static_cast<uint32_t>(offsetof(GameObject, features)),
		.offset_status = static_cast<uint32_t>(offsetof(GameObject, status)),
		.offset_flags = 279, // 位标志字节（bound/colli/rect/hide/navi/ignore_super_pause/last_xy_touched）
	};

	[[nodiscard]] std::wstring getExecutableDirectory()
	{
		wchar_t buffer[MAX_PATH];
		DWORD const length = ::GetModuleFileNameW(nullptr, buffer, MAX_PATH);
		if (length == 0 || length >= MAX_PATH)
		{
			return {};
		}
		std::wstring path(buffer, length);
		if (auto const pos = path.find_last_of(L'\\'); pos != std::wstring::npos)
		{
			path.resize(pos + 1);
		}
		return path;
	}
}

namespace luastg
{
	ManagedAPI const* GetCLRManagedAPI() noexcept
	{
		if (g_clr_managed.managed_api_count == 0)
		{
			return nullptr;
		}
		return &g_clr_managed;
	}

	void DeactivateCLRBinding() noexcept
	{
		g_clr_managed = {};
	}

	void CLRBinding::log(int32_t const level, const char* const text)
	{
		spdlog::log(static_cast<spdlog::level::level_enum>(level), "[CSharp] {}", std::string_view(text));
	}

	const void* CLRBinding::getGameObjectLayout()
	{
		return static_cast<const void*>(&g_gameobject_layout);
	}

	uint32_t CLRBinding::getVersionMajor()
	{
		return LUASTG_VERSION_MAJOR;
	}
	uint32_t CLRBinding::getVersionMinor()
	{
		return LUASTG_VERSION_MINOR;
	}
	uint32_t CLRBinding::getVersionPatch()
	{
		return LUASTG_VERSION_PATCH;
	}
	const char* CLRBinding::getVersionName()
	{
		return LUASTG_INFO;
	}
	const char* CLRBinding::getBranchName()
	{
		return LUASTG_BRANCH;
	}

	void CLRBinding::setWindowed(uint8_t const value)
	{
		LAPP.SetWindowed(value != 0);
	}
	void CLRBinding::setVsync(uint8_t const value)
	{
		LAPP.SetVsync(value != 0);
	}
	void CLRBinding::setResolution(uint32_t const width, uint32_t const height)
	{
		LAPP.SetResolution(width, height);
	}
	void CLRBinding::setTargetFPS(uint32_t const fps)
	{
		LAPP.SetFPS(fps);
	}
	double CLRBinding::getFPS()
	{
		return LAPP.GetFPS();
	}
	void CLRBinding::setWindowTitle(const char* const title)
	{
		LAPP.SetTitle(title);
	}
	void CLRBinding::setSplash(uint8_t const value)
	{
		LAPP.SetSplash(value != 0);
	}

	uint8_t CLRBinding::beginScene()
	{
		return LAPP.getRenderer2D()->beginBatch() ? 1 : 0;
	}
	uint8_t CLRBinding::endScene()
	{
		return LAPP.getRenderer2D()->endBatch() ? 1 : 0;
	}
	void CLRBinding::renderClear(uint8_t const a, uint8_t const r, uint8_t const g, uint8_t const b)
	{
		LAPP.getRenderer2D()->clearRenderTarget(core::Color4B(r, g, b, a));
	}

	bool InitCLRBinding(const char_t* const managed_dir, ManagedAPI* const out_managed)
	{
		auto const exe_dir = getExecutableDirectory();
		if (exe_dir.empty())
		{
			spdlog::error("[clr] 无法获取可执行文件路径");
			return false;
		}
		std::wstring const directory = exe_dir + managed_dir;

		CLRHost host;
		auto const runtime_config = directory + L"LuaSTG.runtimeconfig.json";
		if (!host.init(runtime_config.c_str()))
		{
			return false;
		}

		auto const core_assembly = directory + L"LuaSTG.Core.dll";
		void* fn = nullptr;
		int const rc = host.loadAssemblyAndGetFunctionPointer(
			core_assembly.c_str(),
			L"LuaSTG.Core.LuaSTGAPI",
			L"StartUp",
			UNMANAGEDCALLERSONLY_METHOD,
			&fn
		);
		if (rc != 0 || fn == nullptr)
		{
			spdlog::error("[clr] 加载托管程序集入口失败（0x{:08x}）：{}", rc, utf8::to_string(core_assembly));
			return false;
		}

		using entry_point_fn = int (__stdcall*)(UnmanagedAPI*, ManagedAPI*);

		UnmanagedAPI unmanaged{};
		ManagedAPI managed{};
		if (reinterpret_cast<entry_point_fn>(fn)(&unmanaged, &managed) != 0)
		{
			spdlog::error("[clr] 托管侧启动失败");
			return false;
		}

		constexpr uint32_t expected_managed_api_count = 12;
		if (managed.managed_api_count != expected_managed_api_count)
		{
			spdlog::error("[clr] 托管侧回调数量不匹配（{} != {}）", managed.managed_api_count, expected_managed_api_count);
			return false;
		}

		*out_managed = managed;
		g_clr_managed = managed;
		spdlog::info("[clr] CoreCLR 绑定初始化成功，共 {} 个引擎 API", unmanaged.unmanaged_api_count);
		return true;
	}
}
