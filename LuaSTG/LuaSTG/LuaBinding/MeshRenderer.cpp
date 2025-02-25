#include "Mesh.hpp"
#include "Texture2D.hpp"
#include "MeshRenderer.hpp"
#include "lua/plus.hpp"
#include "LuaWrapper.hpp"
#include "AppFrame.h"
#include <DirectXMath.h>

using std::string_view_literals::operator ""sv;

namespace LuaSTG::Sub::LuaBinding {
	std::string_view MeshRenderer::class_name{ "lstg.MeshRenderer" };

	struct MeshRendererBinding : MeshRenderer {
		// meta methods

		// NOLINTBEGIN(*-reserved-identifier)

		static int __gc(lua_State* vm) {
			if (auto const self = as(vm, 1); self->data) {
				self->data->release();
				self->data = nullptr;
			}
			return 0;
		}
		static int __tostring(lua_State* vm) {
			lua::stack_t const ctx(vm);
			[[maybe_unused]] auto const self = as(vm, 1);
			ctx.push_value(class_name);
			return 1;
		}
		static int __eq(lua_State* vm) {
			lua::stack_t const ctx(vm);
			auto const self = as(vm, 1);
			if (is(vm, 2)) {
				auto const other = as(vm, 2);
				ctx.push_value(self->data == other->data);
			}
			else {
				ctx.push_value(false);
			}
			return 1;
		}

		// NOLINTEND(*-reserved-identifier)

		// method

		static void applyTransform(MeshRenderer const* const self) {
			auto const scale = DirectX::XMMatrixScaling(self->scale.x, self->scale.y, self->scale.z);
			auto const rotation = DirectX::XMMatrixRotationRollPitchYaw(self->rotation_yaw_pitch_roll.y, self->rotation_yaw_pitch_roll.x, self->rotation_yaw_pitch_roll.z);
			auto const position = DirectX::XMMatrixTranslation(self->position.x, self->position.y, self->position.z);
			auto const transform = DirectX::XMMatrixMultiply(DirectX::XMMatrixMultiply(scale, rotation), position);
			DirectX::XMFLOAT4X4A matrix;
			DirectX::XMStoreFloat4x4(&matrix, transform);
			self->data->setTransform(*reinterpret_cast<Core::Matrix4F*>(&matrix));
		}
		static int setPosition(lua_State* vm) {
			lua::stack_t const ctx(vm);
			auto const self = as(vm, 1);

			auto const x = ctx.get_value<float>(1 + 1);
			auto const y = ctx.get_value<float>(1 + 2);
			if (ctx.index_of_top() >= 1 + 3) {
				auto const z = ctx.get_value<float>(1 + 3);
				self->position = Core::Vector3F(x, y, z);
			}
			else {
				self->position = Core::Vector3F(x, y, 0.0f);
			}
			applyTransform(self);

			ctx.push_value(lua::stack_index_t(1)); // return self
			return 1;
		}
		static int setScale(lua_State* vm) {
			lua::stack_t const ctx(vm);
			auto const self = as(vm, 1);

			auto const x = ctx.get_value<float>(1 + 1);
			auto const y = ctx.get_value<float>(1 + 2);
			if (ctx.index_of_top() >= 1 + 3) {
				auto const z = ctx.get_value<float>(1 + 3);
				self->scale = Core::Vector3F(x, y, z);
			}
			else {
				self->scale = Core::Vector3F(x, y, 1.0f);
			}
			applyTransform(self);

			ctx.push_value(lua::stack_index_t(1)); // return self
			return 1;
		}
		static int setRotationYawPitchRoll(lua_State* vm) {
			lua::stack_t const ctx(vm);
			auto const self = as(vm, 1);

			auto const yaw = ctx.get_value<float>(1 + 1);
			auto const pitch = ctx.get_value<float>(1 + 2);
			auto const roll = ctx.get_value<float>(1 + 3);
			self->rotation_yaw_pitch_roll = Core::Vector3F(yaw, pitch, roll);
			applyTransform(self);

			ctx.push_value(lua::stack_index_t(1)); // return self
			return 1;
		}
		static int setMesh(lua_State* vm) {
			lua::stack_t const ctx(vm);
			auto const self = as(vm, 1);

			auto const mesh = Mesh::as(vm, 1 + 1);
			self->data->setMesh(mesh->data);
			
			ctx.push_value(lua::stack_index_t(1)); // return self
			return 1;
		}
		static int setTexture(lua_State* vm) {
			lua::stack_t const ctx(vm);
			auto const self = as(vm, 1);

			auto const texture = Texture2D::as(vm, 1 + 1);
			self->data->setTexture(texture->data);

			ctx.push_value(lua::stack_index_t(1)); // return self
			return 1;
		}
		static int draw(lua_State* vm) {
			lua::stack_t const ctx(vm);
			auto const self = as(vm, 1);

			auto const renderer = LAPP.GetAppModel()->getRenderer();
			self->data->draw(renderer);

			ctx.push_value(lua::stack_index_t(1)); // return self
			return 1;
		}

		// static method

		static int create(lua_State* vm) {
			auto const device = LAPP.GetAppModel()->getDevice();
			auto const self = MeshRenderer::create(vm);
			if (!Core::Graphics::IMeshRenderer::create(device, &self->data)) {
				return luaL_error(vm, "create MeshRenderer failed.");
			}
			return 1;
		}
	};

	bool MeshRenderer::is(lua_State* vm, int const index) {
		lua::stack_t const ctx(vm);
		return ctx.is_metatable(index, class_name);
	}
	MeshRenderer* MeshRenderer::as(lua_State* vm, int const index) {
		lua::stack_t const ctx(vm);
		return ctx.as_userdata<MeshRenderer>(index);
	}
	MeshRenderer* MeshRenderer::create(lua_State* vm) {
		lua::stack_t const ctx(vm);
		auto const self = ctx.create_userdata<MeshRenderer>();
		auto const self_index = ctx.index_of_top();
		ctx.set_metatable(self_index, class_name);
		self->data = nullptr;
		return self;
	}
	void MeshRenderer::registerClass(lua_State* vm) {
		[[maybe_unused]] lua::stack_balancer_t stack_balancer(vm);
		lua::stack_t const ctx(vm);

		// method

		auto const method_table = ctx.push_module(class_name);
		ctx.set_map_value(method_table, "setPosition", &MeshRendererBinding::setPosition);
		ctx.set_map_value(method_table, "setScale", &MeshRendererBinding::setScale);
		ctx.set_map_value(method_table, "setRotationYawPitchRoll", &MeshRendererBinding::setRotationYawPitchRoll);
		ctx.set_map_value(method_table, "setMesh", &MeshRendererBinding::setMesh);
		ctx.set_map_value(method_table, "setTexture", &MeshRendererBinding::setTexture);
		ctx.set_map_value(method_table, "draw", &MeshRendererBinding::draw);
		ctx.set_map_value(method_table, "create", &MeshRendererBinding::create);

		// metatable

		auto const metatable = ctx.create_metatable(class_name);
		ctx.set_map_value(metatable, "__gc", &MeshRendererBinding::__gc);
		ctx.set_map_value(metatable, "__tostring", &MeshRendererBinding::__tostring);
		ctx.set_map_value(metatable, "__eq", &MeshRendererBinding::__eq);
		ctx.set_map_value(metatable, "__index", method_table);
	}
}
