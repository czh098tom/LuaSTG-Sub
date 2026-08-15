using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LuaSTG.Core
{
    /// <summary>
    /// 游戏对象管理器（对应 Lua 侧 lstg.GameObjectManager 与对象批量操作 API）。
    /// </summary>
    public static unsafe class GameObjectManager
    {
        // ========== 对象迭代 ==========

        /// <summary>
        /// 获取或创建指定引擎对象的 C# 包装。
        /// C# 创建的对象已有包装；Lua 创建的对象按需创建只读包装。
        /// </summary>
        internal static GameObjectBase GetOrCreateWrapper(uint id)
        {
            var existing = GameObjectBase.IdToObject[id];
            if (existing != null && existing._native != null)
            {
                return existing;
            }
            var ptr = LuaSTGAPI.api.gameObject_getById((int)id);
            if (ptr == 0)
            {
                throw new InvalidOperationException($"对象 {id} 不存在");
            }
            return new LuaGameObject((NativeGameObject*)ptr);
        }

        /// <summary>
        /// 遍历更新链表中的所有对象（对应 lstg.ObjList()）。
        /// Lua 侧创建的对象也会被遍历到。
        /// </summary>
        public static IEnumerable<GameObjectBase> ObjList()
        {
            for (var id = TryGetUpdateListFirst(); id >= 0; )
            {
                if (TryGetWrapper(id, out var obj))
                {
                    yield return obj;
                }
                id = TryGetUpdateListNext(id);
            }
        }

        /// <summary>
        /// 遍历指定碰撞组中的所有对象（对应 lstg.ObjList(group)）。
        /// </summary>
        public static IEnumerable<GameObjectBase> ObjList(int group)
        {
            if (group < 0 || group >= GameObjectBase.GroupCount)
            {
                throw new ArgumentOutOfRangeException(nameof(group), $"碰撞组取值范围为 0~{GameObjectBase.GroupCount - 1}");
            }
            for (var id = TryGetDetectListFirst(group); id >= 0; )
            {
                if (TryGetWrapper(id, out var obj))
                {
                    yield return obj;
                }
                id = TryGetDetectListNext(group, id);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int TryGetUpdateListFirst() => LuaSTGAPI.api.pool_updateListFirst();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int TryGetUpdateListNext(int id) => LuaSTGAPI.api.pool_updateListNext(id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int TryGetDetectListFirst(int group) => LuaSTGAPI.api.pool_detectListFirst(group);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int TryGetDetectListNext(int group, int id) => LuaSTGAPI.api.pool_detectListNext(group, id);

        /// <summary>按 id 获取包装对象，对象空闲时返回 false</summary>
        private static unsafe bool TryGetWrapper(int id, out GameObjectBase obj)
        {
            if (GameObjectBase.IdToObject[id] is { } existing && existing._native != null)
            {
                obj = existing;
                return true;
            }
            var ptr = LuaSTGAPI.api.gameObject_getById(id);
            if (ptr == 0)
            {
                obj = null!;
                return false;
            }
            obj = new LuaGameObject((NativeGameObject*)ptr);
            return true;
        }

        /// <summary>当前已分配的对象数量（对应 lstg.GetnObj）</summary>
        public static uint GetObjectCount() => LuaSTGAPI.api.pool_getObjectCount();

        /// <summary>对象池容量</summary>
        public static uint GetPoolCapacity() => LuaSTGAPI.api.pool_getCapacity();

        // ========== 批量操作（游戏主循环调用，语义与 Lua 版完全一致） ==========

        /// <summary>更新所有对象的运动（传统模式，对应 lstg.ObjFrame()）</summary>
        public static void ObjFrame() => LuaSTGAPI.api.pool_updateMovementsLegacy();

        /// <summary>更新所有对象的运动（批量模式，对应 lstg.ObjFrame(2)）</summary>
        public static void ObjFrame2() => LuaSTGAPI.api.pool_updateMovements();

        /// <summary>帧末回收与计时器推进（对应 lstg.AfterFrame()）</summary>
        public static void AfterFrame() => LuaSTGAPI.api.pool_updateNextLegacy();

        /// <summary>帧末回收与计时器推进（批量模式，对应 lstg.AfterFrame(2)）</summary>
        public static void AfterFrame2() => LuaSTGAPI.api.pool_updateNext();

        /// <summary>渲染所有对象（对应 lstg.ObjRender()）</summary>
        public static void ObjRender() => LuaSTGAPI.api.pool_render();

        /// <summary>出界检查（对应 lstg.BoundCheck()）</summary>
        public static void BoundCheck() => LuaSTGAPI.api.pool_detectOutOfWorldBoundLegacy();

        /// <summary>出界检查（延迟模式，对应 lstg.BoundCheck(2)）</summary>
        public static void BoundCheck2() => LuaSTGAPI.api.pool_detectOutOfWorldBound();

        /// <summary>两组之间的碰撞检测（对应 lstg.CollisionCheck(g1, g2)）</summary>
        public static void CollisionCheck(uint group1, uint group2)
        {
            if (group1 >= GameObjectBase.GroupCount || group2 >= GameObjectBase.GroupCount)
            {
                throw new ArgumentOutOfRangeException($"碰撞组取值范围为 0~{GameObjectBase.GroupCount - 1}");
            }
            LuaSTGAPI.api.pool_detectIntersectionLegacy(group1, group2);
        }

        /// <summary>多组两两之间的碰撞检测（对应 lstg.CollisionCheck{{g1,g2},...}）</summary>
        public static void CollisionCheck(ReadOnlySpan<(uint Group1, uint Group2)> groupPairs)
        {
            if (groupPairs.IsEmpty)
            {
                return;
            }
            // 转换为引擎需要的紧凑数组
            Span<uint> buffer = stackalloc uint[64];
            uint[]? overflow = null;
            Span<uint> flat = buffer;
            if (groupPairs.Length > 32)
            {
                overflow = new uint[groupPairs.Length * 2];
                flat = overflow;
            }
            for (var i = 0; i < groupPairs.Length; i++)
            {
                flat[i * 2] = groupPairs[i].Group1;
                flat[i * 2 + 1] = groupPairs[i].Group2;
            }
            fixed (uint* p = flat)
            {
                LuaSTGAPI.api.pool_detectIntersection(p, (uint)groupPairs.Length);
            }
        }

        /// <summary>更新对象的坐标增量（对应 lstg.UpdateXY）</summary>
        public static void UpdateXY() => LuaSTGAPI.api.pool_updateXY();

        /// <summary>清空对象池（对应 lstg.ResetPool）</summary>
        public static void ResetPool() => LuaSTGAPI.api.pool_resetPool();

        // ========== 场景边界 ==========

        /// <summary>设置场景边界（对应 lstg.SetBound）</summary>
        public static void SetBound(double l, double r, double b, double t) => LuaSTGAPI.api.pool_setBound(l, r, b, t);

        /// <summary>坐标是否在场景边界内</summary>
        public static bool IsPointInBound(double x, double y) => LuaSTGAPI.api.pool_isPointInBound(x, y) != 0;

        // ========== 超级暂停（对应 lstg.GetSuperPause 等） ==========

        /// <summary>获取当前可信的超级暂停时间</summary>
        public static long GetSuperPauseTime() => LuaSTGAPI.api.pool_getSuperPauseTime();

        /// <summary>获取下一帧超级暂停时间</summary>
        public static long GetNextFrameSuperPauseTime() => LuaSTGAPI.api.pool_getNextFrameSuperPauseTime();

        /// <summary>设置下一帧超级暂停时间</summary>
        public static void SetNextFrameSuperPauseTime(long time) => LuaSTGAPI.api.pool_setNextFrameSuperPauseTime(time);

        /// <summary>推进超级暂停（每帧调用一次，返回当前可信值）</summary>
        public static long UpdateSuperPause() => LuaSTGAPI.api.pool_updateSuperPause();

        // ========== 全局图像缩放（引擎 GLOBAL_SCALE_COLLI_SHAPE 相关） ==========

        /// <summary>全局图像缩放系数（每次访问从引擎读取，对应 lstg.SetImageScale 设置的值）</summary>
        internal static double GlobalImageScale => ResourceManager.ImageScale;

        // ========== 数学辅助（对应 Lua 侧 embedded GameObject.lua 的辅助函数） ==========

        /// <summary>按角度制方向设置速度（对应 lstg.SetV）</summary>
        public static void SetV(GameObjectBase obj, double speed, double angleDeg, bool updateRot = false)
        {
            var rad = angleDeg * (Math.PI / 180.0);
            obj.Vx = speed * Math.Cos(rad);
            obj.Vy = speed * Math.Sin(rad);
            if (updateRot)
            {
                obj.Rot = angleDeg;
            }
        }

        /// <summary>获取速度大小与方向（角度制，对应 lstg.GetV）</summary>
        public static (double Speed, double AngleDeg) GetV(GameObjectBase obj)
        {
            var vx = obj.Vx;
            var vy = obj.Vy;
            return (Math.Sqrt(vx * vx + vy * vy), Math.Atan2(vy, vx) * (180.0 / Math.PI));
        }

        /// <summary>两点距离（对应 lstg.Dist）</summary>
        public static double Dist(double x1, double y1, double x2, double y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>两点连线的角度（角度制，对应 lstg.Angle）</summary>
        public static double Angle(double x1, double y1, double x2, double y2)
        {
            return Math.Atan2(y2 - y1, x2 - x1) * (180.0 / Math.PI);
        }

        /// <summary>角度制三角函数（对应 lstg.sin/cos/tan/asin/acos/atan/atan2）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sin(double degree) => Math.Sin(degree * (Math.PI / 180.0));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cos(double degree) => Math.Cos(degree * (Math.PI / 180.0));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Tan(double degree) => Math.Tan(degree * (Math.PI / 180.0));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Asin(double value) => Math.Asin(value) * (180.0 / Math.PI);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Acos(double value) => Math.Acos(value) * (180.0 / Math.PI);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan2(double y, double x) => Math.Atan2(y, x) * (180.0 / Math.PI);
    }
}
