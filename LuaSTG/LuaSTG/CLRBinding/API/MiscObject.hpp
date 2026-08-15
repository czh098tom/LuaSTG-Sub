// LuaSTG CoreCLR 绑定：杂项对象 API 列表
// 对应 Lua 侧：
//   LuaBinding/LW_Color.cpp          lstg.Color / lstg.HSVColor
//   LuaBinding/LW_StopWatch.cpp      lstg.StopWatch
//   LuaBinding/LW_BentLaser.cpp      lstg.CurveLaser（BentLaserData）
//   LuaBinding/LW_ParticleSystem.cpp lstg.ParticleSystemData
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
//
// 说明：
// - Color 在 C# 侧为纯值类型（uint ARGB 存储），仅 HSV 换算走引擎（与 Lua 侧共用 DirectXMath 实现）
// - StopWatch / BentLaserData / ParticleSystemData 为引擎对象包装类，
//   handle 为 C++ 侧堆分配句柄（uintptr_t），销毁后访问由 C# 侧抛 ObjectDisposedException

// ========== Color（HSV 换算，输入输出均为 0~100 刻度，与 LW_Color.cpp 一致） ==========

// HSV -> ARGB（对应 LW_Color.cpp 的 HSV2RGB，由 lstg.HSVColor 与 Color:AHSV 使用）
DECLARE_CLR_API(uint32_t, color_hsvToARGB, (double a, double h, double s, double v))
// ARGB -> HSV（对应 LW_Color.cpp 的 RGB2HSV，a/h/s/v 输出 0~100 刻度）
DECLARE_CLR_API(void, color_argbToHSV, (uint32_t argb, double* a, double* h, double* s, double* v))

// ========== StopWatch（对应 LW_StopWatch.cpp 的 fcyStopWatch） ==========

// 创建停表（内部 QueryPerformanceCounter），返回句柄，0 表示失败
DECLARE_CLR_API(uintptr_t, stopWatch_create, ())
// 销毁停表
DECLARE_CLR_API(void, stopWatch_destroy, (uintptr_t handle))
// 归零
DECLARE_CLR_API(void, stopWatch_reset, (uintptr_t handle))
// 暂停
DECLARE_CLR_API(void, stopWatch_pause, (uintptr_t handle))
// 继续
DECLARE_CLR_API(void, stopWatch_resume, (uintptr_t handle))
// 获得流逝时间（秒）
DECLARE_CLR_API(double, stopWatch_getElapsed, (uintptr_t handle))

// ========== BentLaserData（对应 LW_BentLaser.cpp / GameObjectBentLaser） ==========

// 创建曲线激光数据（GameObjectBentLaser::AllocInstance），返回句柄，0 表示失败
DECLARE_CLR_API(uintptr_t, bentLaser_create, ())
// 销毁曲线激光数据（GameObjectBentLaser::FreeInstance）
DECLARE_CLR_API(void, bentLaser_destroy, (uintptr_t handle))
// 根据坐标更新节点（Update 的坐标形式），返回 1 成功
DECLARE_CLR_API(uint8_t, bentLaser_update, (uintptr_t handle, double x, double y, double rot, int32_t length, double width, uint8_t active))
// 根据游戏对象更新节点（Update 的对象形式，object 为引擎 GameObject 指针），返回 1 成功
DECLARE_CLR_API(uint8_t, bentLaser_updateByObject, (uintptr_t handle, uintptr_t object, int32_t length, double width, uint8_t active))
// 直接修改单个节点（UpdateNode 的数值形式，索引从 0 开始），返回 1 成功
DECLARE_CLR_API(uint8_t, bentLaser_updateSingleNode, (uintptr_t handle, int32_t index, double x, double y, double width))
// 对某个节点开启或关闭（UpdateNode 的对象形式，node 为负数时从尾部数起），返回 1 成功
DECLARE_CLR_API(uint8_t, bentLaser_updateNodeByObject, (uintptr_t handle, uintptr_t object, int32_t node, int32_t length, double width, uint8_t active))
// 按坐标列表更新节点（UpdatePositionByList，positions 为 length 组 (x,y)），返回 1 成功
DECLARE_CLR_API(uint8_t, bentLaser_updatePositionByList, (uintptr_t handle, const double* positions, int32_t length, double width, int32_t index, uint8_t revert))
// 按列表更新全部节点（UpdateAllNode，xs/ys 为 node_count 个元素；widths 为空时使用固定 width），返回 1 成功
DECLARE_CLR_API(uint8_t, bentLaser_updateAllNode, (uintptr_t handle, int32_t node_count, const float* xs, const float* ys, const float* widths, double width))
// 按长度采样（SampleByLength），采样点写入 out_x/out_y/out_rot（最多 capacity 个），返回总采样数
DECLARE_CLR_API(int32_t, bentLaser_sampleByLength, (uintptr_t handle, double length, float* out_x, float* out_y, float* out_rot, int32_t capacity))
// 按时间采样（SampleByTime，内部除以 60），采样点写入 out_x/out_y/out_rot（最多 capacity 个），返回总采样数
DECLARE_CLR_API(int32_t, bentLaser_sampleByTime, (uintptr_t handle, double time, float* out_x, float* out_y, float* out_rot, int32_t capacity))
// 渲染曲线激光（GLOBAL_SCALE_COLLI_SHAPE 启用时 scale 会乘全局图像缩放系数），返回 1 成功
DECLARE_CLR_API(uint8_t, bentLaser_render, (uintptr_t handle, const char* tex_name, uint8_t blend, uint32_t argb, double tex_left, double tex_top, double tex_width, double tex_height, double scale))
// 渲染碰撞体
DECLARE_CLR_API(void, bentLaser_renderCollider, (uintptr_t handle, uint32_t argb))
// 碰撞检测（对应 CollisionCheck），返回 1 命中
DECLARE_CLR_API(uint8_t, bentLaser_collisionCheck, (uintptr_t handle, double x, double y, double rot, double a, double b, uint8_t rect))
// 碰撞检测（对应 CollisionCheckWidth / CollisionCheckWithWidth 的数值形式），返回 1 命中
DECLARE_CLR_API(uint8_t, bentLaser_collisionCheckWidth, (uintptr_t handle, double width, double x, double y, double rot, double a, double b, uint8_t rect))
// 碰撞检测（对应 CollisionCheckWithWidth 的对象形式，读取对象的原始 x/y/rot/a/b/rect），返回 1 命中
DECLARE_CLR_API(uint8_t, bentLaser_collisionCheckWithObject, (uintptr_t handle, uintptr_t object, double width))
// 检查是否有节点在边界内（对应 BoundCheck），返回 1 在边界内
DECLARE_CLR_API(uint8_t, bentLaser_boundCheck, (uintptr_t handle))
// 更改所有节点的碰撞和渲染宽度（对应 SetAllWidth）
DECLARE_CLR_API(void, bentLaser_setAllWidth, (uintptr_t handle, double width))
// 设置碰撞包络（对应 SetEnvelope）
DECLARE_CLR_API(void, bentLaser_setEnvelope, (uintptr_t handle, double height, double base, double rate, double power))
// 读取碰撞包络（对应 GetEnvelope）
DECLARE_CLR_API(void, bentLaser_getEnvelope, (uintptr_t handle, double* height, double* base, double* rate, double* power))
// 获取节点数量（对应 __len）
DECLARE_CLR_API(int32_t, bentLaser_getNodeCount, (uintptr_t handle))

// ========== ParticleSystemData（对应 LW_ParticleSystem.cpp） ==========

// 创建粒子系统实例（绑定 LoadPS 加载的资源），返回句柄，0 表示资源不存在或创建失败
DECLARE_CLR_API(uintptr_t, particleSystem_create, (const char* ps_name))
// 销毁粒子系统实例（销毁 pool 并释放资源引用）
DECLARE_CLR_API(void, particleSystem_destroy, (uintptr_t handle))
// 经典 API
DECLARE_CLR_API(void, particleSystem_setActive, (uintptr_t handle, uint8_t active))
DECLARE_CLR_API(size_t, particleSystem_getAliveCount, (uintptr_t handle))
DECLARE_CLR_API(void, particleSystem_setEmission, (uintptr_t handle, int32_t emission))
DECLARE_CLR_API(int32_t, particleSystem_getEmission, (uintptr_t handle))
// 更新粒子系统；mode: 0=仅 delta 1=delta+中心坐标 2=delta+中心坐标+发射朝向（角度制）
DECLARE_CLR_API(void, particleSystem_update, (uintptr_t handle, double delta, double x, double y, double rot_degree, uint8_t mode))
// 渲染粒子系统（对应 Render，等比缩放）
DECLARE_CLR_API(void, particleSystem_render, (uintptr_t handle, double scale))
DECLARE_CLR_API(void, particleSystem_setOldBehavior, (uintptr_t handle, uint8_t value))
// LuaSTG-x 风格 API
DECLARE_CLR_API(uint8_t, particleSystem_isActive, (uintptr_t handle))
DECLARE_CLR_API(int32_t, particleSystem_getEmissionFreq, (uintptr_t handle))
DECLARE_CLR_API(void, particleSystem_setEmissionFreq, (uintptr_t handle, int32_t value))
DECLARE_CLR_API(uint8_t, particleSystem_isRelative, (uintptr_t handle))
DECLARE_CLR_API(void, particleSystem_setRelative, (uintptr_t handle, uint8_t value))
// 中心坐标（对应 getCenter/setCenter）
DECLARE_CLR_API(void, particleSystem_getCenter, (uintptr_t handle, float* x, float* y))
DECLARE_CLR_API(void, particleSystem_setCenter, (uintptr_t handle, double x, double y))
// 发射朝向（弧度，对应 getRotation/setRotation）
DECLARE_CLR_API(float, particleSystem_getRotation, (uintptr_t handle))
DECLARE_CLR_API(void, particleSystem_setRotation, (uintptr_t handle, float rot))
// 随机数种子（对应 getSeed/setSeed）
DECLARE_CLR_API(uint32_t, particleSystem_getSeed, (uintptr_t handle))
DECLARE_CLR_API(void, particleSystem_setSeed, (uintptr_t handle, uint32_t seed))
// 渲染混合模式（对应 getRenderMode/setRenderMode）
DECLARE_CLR_API(uint8_t, particleSystem_getBlendMode, (uintptr_t handle))
DECLARE_CLR_API(void, particleSystem_setBlendMode, (uintptr_t handle, uint8_t blend))
// 资源名称（对应 getResource），返回引擎拥有的字符串
DECLARE_CLR_API(const char*, particleSystem_getResourceName, (uintptr_t handle))
// 粒子信息数值字段读写（field_id 见 CLRMiscObject.cpp 的 PSField 与 C# ParticleSystemInfoField）
DECLARE_CLR_API(float, particleSystem_getInfoField, (uintptr_t handle, uint8_t field_id))
DECLARE_CLR_API(void, particleSystem_setInfoField, (uintptr_t handle, uint8_t field_id, float value))
// 粒子信息颜色字段读写（color_id: 0=起始颜色 1=结束颜色；argb 为 0xAARRGGBB；F 版本为 0~1 浮点分量）
DECLARE_CLR_API(uint32_t, particleSystem_getColorField, (uintptr_t handle, uint8_t color_id))
DECLARE_CLR_API(void, particleSystem_getColorFieldF, (uintptr_t handle, uint8_t color_id, float* r, float* g, float* b, float* a))
DECLARE_CLR_API(void, particleSystem_setColorField, (uintptr_t handle, uint8_t color_id, uint32_t argb))
DECLARE_CLR_API(void, particleSystem_setColorFieldF, (uintptr_t handle, uint8_t color_id, float r, float g, float b, float a))
