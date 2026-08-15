#include "CLRBinding/CLRBinding.hpp"
#include "AppFrame.h"
#include "GameObject/GameObjectPool.h"
#include "LuaBinding/modern/GameObject.hpp"

using namespace luastg;

namespace
{
	using std::string_view_literals::operator ""sv;

	constexpr auto reason_out_of_world_bound{ "luastg:leave_world_border"sv };

	/// @brief 获取对象池，引擎关闭后为 nullptr
	[[nodiscard]] GameObjectPool* getPool() noexcept
	{
		return LAPP.GetGameObjectPoolIfExists();
	}

	/// @brief C# 侧创建的游戏对象的回调转发
	struct CLRGameObjectCallbacks : IGameObjectCallbacks
	{
		[[nodiscard]] std::string_view getCallbacksName(GameObject*) const noexcept override {
			return "csharp"sv;
		}
		void onQueueToDestroy(GameObject* self, std::string_view const reason) override {
			if (auto const api = GetCLRManagedAPI()) {
				auto const r = reason == reason_out_of_world_bound
					? CLRGameObjectDestroyReason::Bound
					: CLRGameObjectDestroyReason::Other;
				api->CallOnDestroy(static_cast<uint32_t>(self->id), static_cast<uint8_t>(r));
			}
		}
		void onUpdate(GameObject* self) override {
			if (auto const api = GetCLRManagedAPI()) {
				api->CallOnFrame(static_cast<uint32_t>(self->id));
			}
		}
		void onLateUpdate(GameObject*) override {}
		void onRender(GameObject* self) override {
			if (auto const api = GetCLRManagedAPI()) {
				api->CallOnRender(static_cast<uint32_t>(self->id));
			}
		}
		void onTrigger(GameObject* self, GameObject* other) override {
			if (auto const api = GetCLRManagedAPI()) {
				api->CallOnColli(static_cast<uint32_t>(self->id), static_cast<uint32_t>(other->id));
			}
		}
		static CLRGameObjectCallbacks& getInstance() {
			static CLRGameObjectCallbacks instance;
			return instance;
		}
	};

	/// @brief 对象池事件转发（对象被真正回收时通知托管侧解除包装）
	struct CLRGameObjectManagerCallbacks : IGameObjectManagerCallbacks
	{
		[[nodiscard]] std::string_view getCallbacksName() const noexcept override {
			return "csharp"sv;
		}
		void onCreate(GameObject*) override {}
		void onDestroy(GameObject* const object) override {
			if (auto const api = GetCLRManagedAPI()) {
				api->DetachGameObject(static_cast<uint32_t>(object->id));
			}
		}
		void onBeforeBatchDestroy() override {}
		void onAfterBatchDestroy() override {}
		void onBeforeBatchUpdate() override {}
		void onAfterBatchUpdate() override {}
		void onBeforeBatchRender() override {}
		void onAfterBatchRender() override {}
		void onBeforeBatchOutOfWorldBoundCheck() override {}
		void onAfterBatchOutOfWorldBoundCheck() override {}
		void onBeforeBatchIntersectDetect() override {}
		void onAfterBatchIntersectDetect() override {}
		static CLRGameObjectManagerCallbacks& getInstance() {
			static CLRGameObjectManagerCallbacks instance;
			return instance;
		}
	};
}

namespace luastg
{
	/// @brief 将托管侧回调注册到对象池（在 CoreCLR 初始化成功后调用）
	void RegisterCLRGameObjectCallbacks()
	{
		LPOOL.addCallbacks(&CLRGameObjectManagerCallbacks::getInstance());
	}
}

// 引擎 API 实现
uintptr_t luastg::CLRBinding::gameObject_new(uint32_t const callback_mask)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return 0;
	}
	auto const object = pool->allocateWithCallbacks(&CLRGameObjectCallbacks::getInstance());
	if (object == nullptr) {
		return 0;
	}
	GameObjectFeatures features{};
	features.is_class = true;
	features.is_render_class = (callback_mask & (1u << 5)) != 0;
	features.has_callback_create = (callback_mask & (1u << 6)) != 0;
	features.has_callback_destroy = (callback_mask & (1u << 3)) != 0;
	features.has_callback_update = (callback_mask & (1u << 0)) != 0;
	features.has_callback_render = (callback_mask & (1u << 1)) != 0;
	features.has_callback_trigger = (callback_mask & (1u << 2)) != 0;
	features.has_callback_legacy_kill = (callback_mask & (1u << 4)) != 0;
	object->features = features;
	// 在 Lua 对象表中注册包装表，使 Lua 侧也能访问该对象的引擎数据
	if (auto* const vm = LAPP.GetLuaEngine(); vm != nullptr) {
		luastg::binding::GameObject::createClrObjectWrapper(vm, static_cast<uint32_t>(object->id), object);
	}
	return reinterpret_cast<uintptr_t>(object);
}

uint8_t luastg::CLRBinding::gameObject_queueToFree(uintptr_t const object, uint8_t const kill_mode)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return 0;
	}
	return pool->queueToFree(reinterpret_cast<GameObject*>(object), kill_mode != 0) ? 1 : 0;
}

void luastg::CLRBinding::gameObject_defaultRender(uintptr_t const object)
{
	reinterpret_cast<GameObject*>(object)->Render();
}

void luastg::CLRBinding::gameObject_releaseResource(uintptr_t const object)
{
	reinterpret_cast<GameObject*>(object)->ReleaseResource();
}

uint8_t luastg::CLRBinding::gameObject_changeResource(uintptr_t const object, const char* const res_name)
{
	return reinterpret_cast<GameObject*>(object)->ChangeResource(std::string_view(res_name)) ? 1 : 0;
}

void luastg::CLRBinding::gameObject_dirtReset(uintptr_t const object)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return;
	}
	pool->DirtResetObject(reinterpret_cast<GameObject*>(object));
}

uintptr_t luastg::CLRBinding::gameObject_getById(int32_t const id)
{
	auto* const pool = getPool();
	if (pool == nullptr || id < 0 || static_cast<uint32_t>(id) >= LOBJPOOL_SIZE) {
		return 0;
	}
	return reinterpret_cast<uintptr_t>(pool->GetPooledObject(static_cast<size_t>(id)));
}

uint8_t luastg::CLRBinding::gameObject_setGroup(uintptr_t const object, int64_t const group)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return 0;
	}
	auto const p = reinterpret_cast<GameObject*>(object);
	if (pool->isLockedByDetectIntersection(p)) {
		return 0; // 碰撞检测中不允许修改
	}
	if (group < 0 || group >= LOBJPOOL_GROUPN) {
		return 0;
	}
	if (p->group != group) {
		p->setGroup(group);
	}
	return 1;
}

uint8_t luastg::CLRBinding::gameObject_setLayer(uintptr_t const object, double const layer)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return 0;
	}
	auto const p = reinterpret_cast<GameObject*>(object);
	if (pool->isRendering()) {
		return 0; // 渲染中不允许修改
	}
	if (p->layer != layer) {
		p->setLayer(layer);
	}
	return 1;
}

void luastg::CLRBinding::gameObject_setResourceRenderState(uintptr_t const object, uint8_t const blend, uint32_t const argb)
{
	reinterpret_cast<GameObject*>(object)->setResourceRenderState(
		static_cast<BlendMode>(blend), core::Color4B(argb)
	);
}

void luastg::CLRBinding::gameObject_setParticleRenderState(uintptr_t const object, uint8_t const blend, uint32_t const argb)
{
	reinterpret_cast<GameObject*>(object)->setParticleRenderState(
		static_cast<BlendMode>(blend), core::Color4B(argb)
	);
}

void luastg::CLRBinding::gameObject_stopParticle(uintptr_t const object)
{
	reinterpret_cast<GameObject*>(object)->stopParticle();
}

void luastg::CLRBinding::gameObject_fireParticle(uintptr_t const object)
{
	reinterpret_cast<GameObject*>(object)->startParticle();
}

uint32_t luastg::CLRBinding::gameObject_getParticleCount(uintptr_t const object)
{
	return static_cast<uint32_t>(reinterpret_cast<GameObject*>(object)->getParticleCount());
}

int32_t luastg::CLRBinding::gameObject_getParticleEmission(uintptr_t const object)
{
	return reinterpret_cast<GameObject*>(object)->getParticleEmission();
}

void luastg::CLRBinding::gameObject_setParticleEmission(uintptr_t const object, int32_t const value)
{
	reinterpret_cast<GameObject*>(object)->setParticleEmission(value);
}

uint8_t luastg::CLRBinding::gameObject_isIntersect(uintptr_t const object1, uintptr_t const object2)
{
	return reinterpret_cast<GameObject*>(object1)->isIntersect(reinterpret_cast<GameObject*>(object2)) ? 1 : 0;
}

uint8_t luastg::CLRBinding::gameObject_isInRect(uintptr_t const object, double const l, double const r, double const b, double const t)
{
	return reinterpret_cast<GameObject*>(object)->isInRect(l, r, b, t) ? 1 : 0;
}

const char* luastg::CLRBinding::gameObject_getResourceName(uintptr_t const object)
{
	auto const name = reinterpret_cast<GameObject*>(object)->getRenderResourceName();
	return name.data();
}

void luastg::CLRBinding::pool_updateMovementsLegacy()
{
	if (auto* const pool = getPool()) pool->updateMovementsLegacy();
}
void luastg::CLRBinding::pool_updateMovements()
{
	if (auto* const pool = getPool()) pool->updateMovements();
}
void luastg::CLRBinding::pool_updateNextLegacy()
{
	if (auto* const pool = getPool()) pool->updateNextLegacy();
}
void luastg::CLRBinding::pool_updateNext()
{
	if (auto* const pool = getPool()) pool->updateNext();
}
void luastg::CLRBinding::pool_render()
{
	if (auto* const pool = getPool()) pool->render();
}
void luastg::CLRBinding::pool_detectOutOfWorldBoundLegacy()
{
	if (auto* const pool = getPool()) pool->detectOutOfWorldBoundLegacy();
}
void luastg::CLRBinding::pool_detectOutOfWorldBound()
{
	if (auto* const pool = getPool()) pool->detectOutOfWorldBound();
}

void luastg::CLRBinding::pool_detectIntersectionLegacy(uint32_t const group1, uint32_t const group2)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return;
	}
	if (group1 < LOBJPOOL_GROUPN && group2 < LOBJPOOL_GROUPN) {
		pool->detectIntersectionLegacy(group1, group2);
	}
}

void luastg::CLRBinding::pool_detectIntersection(const uint32_t* const group_pairs, uint32_t const count)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return;
	}
	std::pmr::vector<GameObjectPool::IntersectionDetectionGroupPair> pairs;
	pairs.reserve(count);
	for (uint32_t i = 0; i < count; i += 1) {
		auto const group1 = group_pairs[i * 2];
		auto const group2 = group_pairs[i * 2 + 1];
		if (group1 < LOBJPOOL_GROUPN && group2 < LOBJPOOL_GROUPN) {
			pairs.emplace_back(group1, group2);
		}
	}
	pool->detectIntersection(pairs);
}

void luastg::CLRBinding::pool_updateXY()
{
	if (auto* const pool = getPool()) pool->UpdateXY();
}
void luastg::CLRBinding::pool_resetPool()
{
	if (auto* const pool = getPool()) pool->ResetPool();
}

void luastg::CLRBinding::pool_setBound(double const l, double const r, double const b, double const t)
{
	if (auto* const pool = getPool()) pool->SetBound(l, r, b, t);
}

uint8_t luastg::CLRBinding::pool_isPointInBound(double const x, double const y)
{
	auto* const pool = getPool();
	return pool != nullptr && pool->isPointInBound(x, y) ? 1 : 0;
}

uint32_t luastg::CLRBinding::pool_getCapacity()
{
	return LOBJPOOL_SIZE;
}

uint32_t luastg::CLRBinding::pool_getObjectCount()
{
	auto* const pool = getPool();
	return pool != nullptr ? static_cast<uint32_t>(pool->GetObjectCount()) : 0u;
}

int32_t luastg::CLRBinding::pool_updateListFirst()
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return -1;
	}
	auto const object = pool->getUpdateListFirst();
	return object ? static_cast<int32_t>(object->id) : -1;
}

int32_t luastg::CLRBinding::pool_updateListNext(int32_t const id)
{
	auto* const pool = getPool();
	if (pool == nullptr) {
		return -1;
	}
	auto const object = pool->getUpdateListNext(static_cast<size_t>(id));
	return object ? static_cast<int32_t>(object->id) : -1;
}

int32_t luastg::CLRBinding::pool_detectListFirst(int32_t const group)
{
	auto* const pool = getPool();
	if (pool == nullptr || group < 0 || group >= LOBJPOOL_GROUPN) {
		return -1;
	}
	auto const object = pool->getDetectListFirst(static_cast<size_t>(group));
	return object ? static_cast<int32_t>(object->id) : -1;
}

int32_t luastg::CLRBinding::pool_detectListNext(int32_t const group, int32_t const id)
{
	auto* const pool = getPool();
	if (pool == nullptr || group < 0 || group >= LOBJPOOL_GROUPN) {
		return -1;
	}
	auto const object = pool->getDetectListNext(static_cast<size_t>(group), static_cast<size_t>(id));
	return object ? static_cast<int32_t>(object->id) : -1;
}

int64_t luastg::CLRBinding::pool_getSuperPauseTime()
{
	auto* const pool = getPool();
	return pool != nullptr ? pool->GetSuperPauseTime() : 0;
}
int64_t luastg::CLRBinding::pool_getNextFrameSuperPauseTime()
{
	auto* const pool = getPool();
	return pool != nullptr ? pool->GetNextFrameSuperPauseTime() : 0;
}
void luastg::CLRBinding::pool_setNextFrameSuperPauseTime(int64_t const time)
{
	if (auto* const pool = getPool()) pool->SetNextFrameSuperPauseTime(time);
}
int64_t luastg::CLRBinding::pool_updateSuperPause()
{
	auto* const pool = getPool();
	return pool != nullptr ? pool->UpdateSuperPause() : 0;
}
