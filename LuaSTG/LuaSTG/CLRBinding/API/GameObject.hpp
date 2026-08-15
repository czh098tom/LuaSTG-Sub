// LuaSTG CoreCLR 绑定：游戏对象 API 列表
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（见 tool/clr-api-generator/generate.py）
// 游戏对象的普通数据属性（坐标、速度等）由 C# 侧直接以结构体覆写方式访问，无需经过函数调用

// 对象生命周期
// callback_mask: bit0=frame bit1=render bit2=colli bit3=del bit4=kill bit5=render_class bit6=create bit7=is_class
DECLARE_CLR_API(uintptr_t, gameObject_new, (uint32_t callback_mask))
// 返回 1 表示存在需要语言侧调用的析构回调（与 Lua 行为一致）
DECLARE_CLR_API(uint8_t, gameObject_queueToFree, (uintptr_t object, uint8_t kill_mode))
DECLARE_CLR_API(void, gameObject_defaultRender, (uintptr_t object))
DECLARE_CLR_API(void, gameObject_releaseResource, (uintptr_t object))
DECLARE_CLR_API(uint8_t, gameObject_changeResource, (uintptr_t object, const char* res_name))
DECLARE_CLR_API(void, gameObject_dirtReset, (uintptr_t object))
DECLARE_CLR_API(uintptr_t, gameObject_getById, (int32_t id))

// 需要引擎配合的属性修改
DECLARE_CLR_API(uint8_t, gameObject_setGroup, (uintptr_t object, int64_t group))
DECLARE_CLR_API(uint8_t, gameObject_setLayer, (uintptr_t object, double layer))
DECLARE_CLR_API(void, gameObject_setResourceRenderState, (uintptr_t object, uint8_t blend, uint32_t argb))
DECLARE_CLR_API(void, gameObject_setParticleRenderState, (uintptr_t object, uint8_t blend, uint32_t argb))
DECLARE_CLR_API(void, gameObject_stopParticle, (uintptr_t object))
DECLARE_CLR_API(void, gameObject_fireParticle, (uintptr_t object))
DECLARE_CLR_API(uint32_t, gameObject_getParticleCount, (uintptr_t object))
DECLARE_CLR_API(int32_t, gameObject_getParticleEmission, (uintptr_t object))
DECLARE_CLR_API(void, gameObject_setParticleEmission, (uintptr_t object, int32_t value))
DECLARE_CLR_API(uint8_t, gameObject_isIntersect, (uintptr_t object1, uintptr_t object2))
DECLARE_CLR_API(uint8_t, gameObject_isInRect, (uintptr_t object, double l, double r, double b, double t))
DECLARE_CLR_API(const char*, gameObject_getResourceName, (uintptr_t object))

// 对象池批量操作（对应 lstg.ObjFrame / lstg.AfterFrame / lstg.ObjRender 等）
DECLARE_CLR_API(void, pool_updateMovementsLegacy, ())
DECLARE_CLR_API(void, pool_updateMovements, ())
DECLARE_CLR_API(void, pool_updateNextLegacy, ())
DECLARE_CLR_API(void, pool_updateNext, ())
DECLARE_CLR_API(void, pool_render, ())
DECLARE_CLR_API(void, pool_detectOutOfWorldBoundLegacy, ())
DECLARE_CLR_API(void, pool_detectOutOfWorldBound, ())
DECLARE_CLR_API(void, pool_detectIntersectionLegacy, (uint32_t group1, uint32_t group2))
// group_pairs 为 count 个 uint32 对（group1, group2）
DECLARE_CLR_API(void, pool_detectIntersection, (const uint32_t* group_pairs, uint32_t count))
DECLARE_CLR_API(void, pool_updateXY, ())
DECLARE_CLR_API(void, pool_resetPool, ())
DECLARE_CLR_API(void, pool_setBound, (double l, double r, double b, double t))
DECLARE_CLR_API(uint8_t, pool_isPointInBound, (double x, double y))
DECLARE_CLR_API(uint32_t, pool_getCapacity, ())
DECLARE_CLR_API(uint32_t, pool_getObjectCount, ())

// 链表迭代（返回对象池下标，-1 表示结束）
DECLARE_CLR_API(int32_t, pool_updateListFirst, ())
DECLARE_CLR_API(int32_t, pool_updateListNext, (int32_t id))
DECLARE_CLR_API(int32_t, pool_detectListFirst, (int32_t group))
DECLARE_CLR_API(int32_t, pool_detectListNext, (int32_t group, int32_t id))

// 超级暂停
DECLARE_CLR_API(int64_t, pool_getSuperPauseTime, ())
DECLARE_CLR_API(int64_t, pool_getNextFrameSuperPauseTime, ())
DECLARE_CLR_API(void, pool_setNextFrameSuperPauseTime, (int64_t time))
DECLARE_CLR_API(int64_t, pool_updateSuperPause, ())
