#pragma once
#include "GameObject/GameObject.hpp"
#include "lua.hpp"

namespace luastg::binding {

	struct GameObject {

		static std::string_view const class_name;

		static bool is(lua_State* vm, int index);

		static luastg::GameObject* as(lua_State* vm, int index);

		static int pushGameObjectTable(lua_State* vm);

		/// @brief 为 C# (CoreCLR) 侧创建的游戏对象在 Lua 对象表中注册包装表
		/// @note 使 Lua 侧（如 lstg.ObjList）也能访问该对象的引擎数据；
		///       包装表的类槽位指向内部空回调存根，引擎回调仍由 C# 侧处理
		static void createClrObjectWrapper(lua_State* vm, uint32_t id, luastg::GameObject* object);

		static void registerClass(lua_State* vm);

	};

}
