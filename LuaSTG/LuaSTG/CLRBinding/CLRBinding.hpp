#pragma once
#include <cstdint>
#include "CLRBinding/CLRHost.hpp"

namespace luastg
{
	// 引擎侧 API 的调用约定（C# 侧对应 delegate* unmanaged[Cdecl]）
	#if !defined(LUASTG_CLR_API_CALLTYPE)
		#define LUASTG_CLR_API_CALLTYPE __cdecl
	#endif

	// 游戏对象销毁原因（传递给托管侧）
	enum class CLRGameObjectDestroyReason : uint8_t {
		Del = 0,    // 语言侧调用 Del
		Kill = 1,   // 语言侧调用 Kill
		Bound = 2,  // 出界回收
		Other = 3,  // 其他原因
	};

	/// @brief 托管侧（C#）提供给引擎的回调函数集
	/// @note 与 CSharp/LuaSTG/LuaSTG.Core/ManagedAPI.cs 保持同步
	struct ManagedAPI
	{
		uint32_t managed_api_count; // 用于一致性校验

		// 游戏循环
		void (CORECLR_DELEGATE_CALLTYPE* GameInit)();
		uint8_t (CORECLR_DELEGATE_CALLTYPE* FrameFunc)(); // 返回 1 请求退出
		void (CORECLR_DELEGATE_CALLTYPE* RenderFunc)();
		void (CORECLR_DELEGATE_CALLTYPE* GameExit)();
		void (CORECLR_DELEGATE_CALLTYPE* FocusLoseFunc)();
		void (CORECLR_DELEGATE_CALLTYPE* FocusGainFunc)();
		void (CORECLR_DELEGATE_CALLTYPE* EventFunc)(uint8_t event_type, uint8_t state);

		// 游戏对象
		void (CORECLR_DELEGATE_CALLTYPE* DetachGameObject)(uint32_t id);
		void (CORECLR_DELEGATE_CALLTYPE* CallOnFrame)(uint32_t id);
		void (CORECLR_DELEGATE_CALLTYPE* CallOnRender)(uint32_t id);
		void (CORECLR_DELEGATE_CALLTYPE* CallOnDestroy)(uint32_t id, uint8_t reason);
		void (CORECLR_DELEGATE_CALLTYPE* CallOnColli)(uint32_t id, uint32_t other_id);
	};

	/// @brief 引擎（C++）提供给托管侧的 API 实现声明与函数指针表
	/// @note API 列表来源于 CLRBinding/API/ 下的模块定义文件，
	///       C# 侧的 UnmanagedAPI 声明由 tool/clr-api-generator 生成
	struct CLRBinding
	{
#define DECLARE_CLR_API(ReturnType, Name, Params) static ReturnType Name Params;
#include "CLRBinding/API/APIList.hpp"
#undef DECLARE_CLR_API
	};

	/// @brief 引擎侧 API 函数指针表，在初始化时传递给托管侧
	struct UnmanagedAPI
	{
		uint32_t unmanaged_api_count; // 用于一致性校验

#define DECLARE_CLR_API(ReturnType, Name, Params) ReturnType(LUASTG_CLR_API_CALLTYPE* Name) Params;
#include "CLRBinding/API/APIList.hpp"
#undef DECLARE_CLR_API

		UnmanagedAPI() {
			unmanaged_api_count = 0
#define DECLARE_CLR_API(ReturnType, Name, Params) + 1
#include "CLRBinding/API/APIList.hpp"
#undef DECLARE_CLR_API
			;
#define DECLARE_CLR_API(ReturnType, Name, Params) Name = &CLRBinding::Name;
#include "CLRBinding/API/APIList.hpp"
#undef DECLARE_CLR_API
		}
	};

	/// @brief C# 侧游戏对象结构体覆写的布局校验信息
	/// @note 与 CSharp/LuaSTG/LuaSTG.Core/Interop/NativeGameObjectLayout.g.cs 保持同步，
	///       由 getGameObjectLayout() 返回其静态实例指针
	struct CLRGameObjectLayoutInfo
	{
		uint32_t api_version;      // 布局信息版本
		uint32_t struct_size;      // sizeof(GameObject)
		uint32_t pool_size;        // LOBJPOOL_SIZE
		uint32_t group_count;      // LOBJPOOL_GROUPN
		uint32_t feature_flags;    // 编译特性位（见下方定义）

		// 特性位定义（与 Custom/Config.h 对应）
		// bit0: USING_MULTI_GAME_WORLD
		// bit1: USER_SYSTEM_OPERATION
		// bit2: GLOBAL_SCALE_COLLI_SHAPE
		// bit3: LUASTG_GAME_OBJECT_PARTICLE_SYSTEM_OBJECT
		// bit4: LUASTG_ENABLE_GAME_OBJECT_PROPERTY_PAUSE

		uint32_t offset_id;             // id : 16 + unique_id : 48
		uint32_t offset_last_x;
		uint32_t offset_last_y;
		uint32_t offset_x;
		uint32_t offset_y;
		uint32_t offset_dx;
		uint32_t offset_dy;
		uint32_t offset_vx;
		uint32_t offset_vy;
		uint32_t offset_ax;
		uint32_t offset_ay;
		uint32_t offset_max_vx;
		uint32_t offset_max_vy;
		uint32_t offset_max_v;
		uint32_t offset_ag;
		uint32_t offset_group;
		uint32_t offset_a;
		uint32_t offset_b;
		uint32_t offset_col_r;
		uint32_t offset_layer;
		uint32_t offset_hscale;
		uint32_t offset_vscale;
		uint32_t offset_rot;
		uint32_t offset_omega;
		uint32_t offset_ani_timer;
		uint32_t offset_res;
		uint32_t offset_ps;
		uint32_t offset_timer;
		uint32_t offset_vertex_color;
		uint32_t offset_blend_mode;
		uint32_t offset_features;
		uint32_t offset_status;
		uint32_t offset_flags;          // 位标志字节（bound/colli/rect/hide/navi/...）
	};

	/// @brief 当前激活的托管侧回调，未初始化时为 nullptr
	[[nodiscard]] ManagedAPI const* GetCLRManagedAPI() noexcept;

	/// @brief 初始化 CoreCLR 绑定
	/// @param managed_dir 托管程序集目录（UTF-16，如 L".\\Managed"）
	/// @param out_managed 输出托管侧回调函数集
	/// @return 初始化是否成功
	bool InitCLRBinding(const char_t* managed_dir, ManagedAPI* out_managed);
}
