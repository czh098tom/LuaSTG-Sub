using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuaSTG.Core
{
    /// <summary>
    /// 游戏对象状态（与引擎侧 GameObjectStatus 保持一致）。
    /// </summary>
    public enum GameObjectStatus : byte
    {
        /// <summary>空闲（对象池未分配）</summary>
        Free = 0,
        /// <summary>正常活跃</summary>
        Active = 1,
        /// <summary>已标记删除（lstg.Del），将在帧末回收</summary>
        Dead = 2,
        /// <summary>已标记杀死（lstg.Kill），将在帧末回收</summary>
        Killed = 4,
    }

    /// <summary>
    /// 游戏对象基类。
    /// 引擎数据（坐标、速度、图层等）通过内存覆写直接读写，与 Lua 侧始终同步；
    /// C# 侧自身的字段（继承类中声明的字段）不与 Lua 同步。
    /// 无参构造函数分配引擎对象；<see cref="Delete"/>/<see cref="Kill"/> 标记销毁，
    /// 销毁状态的对象访问引擎数据会抛出 <see cref="ObjectDisposedException"/>。
    /// </summary>
    public unsafe abstract class GameObjectBase
    {
        /// <summary>std::numeric_limits&lt;double&gt;::min()，即最小正正规数</summary>
        internal const double DoubleMinNormal = 2.2250738585072014E-308;

        /// <summary>对象池容量（与引擎侧一致，启动时校验）</summary>
        internal const int PoolSize = 32768;

        /// <summary>碰撞组数量</summary>
        public const int GroupCount = 16;

        /// <summary>对象 id 到包装对象的映射（引擎对象池下标）</summary>
        internal static readonly GameObjectBase?[] IdToObject = new GameObjectBase?[PoolSize];

        /// <summary>引擎对象指针，null 表示已被引擎回收</summary>
        internal NativeGameObject* _native;

        /// <summary>语言侧是否已调用 Delete/Kill</summary>
        private bool _userDestroyed;

        /// <summary>
        /// 分配一个引擎游戏对象。引擎回调（OnFrame 等）根据子类覆写情况自动启用。
        /// </summary>
        protected GameObjectBase()
        {
            var mask = GameObjectFeatureMask.Get(GetType());
            var ptr = LuaSTGAPI.api.gameObject_new(mask);
            if (ptr == 0)
            {
                throw new OutOfMemoryException("游戏对象池已耗尽");
            }
            _native = (NativeGameObject*)ptr;
            IdToObject[_native->Id] = this;
        }

        /// <summary>包装一个已存在的引擎对象（如 Lua 侧创建的对象），内部使用</summary>
        internal GameObjectBase(NativeGameObject* native)
        {
            _native = native;
            IdToObject[native->Id] = this;
        }

        // 注意：不实现终结器。引擎对象的生命周期由引擎对象池管理，
        // 未 Delete 的对象随引擎关闭统一回收；终结器中回调引擎可能发生在引擎关闭之后。

        // ========== 生命周期 ==========

        /// <summary>对象是否仍然有效（引擎对象存在且未被语言侧销毁）</summary>
        public bool IsValid => _native != null;

        /// <summary>对象是否已被语言侧销毁（Delete/Kill）或引擎回收</summary>
        public bool IsDestroyed => _userDestroyed || _native == null;

        /// <summary>
        /// 删除对象（对应 lstg.Del）。
        /// 若启用了销毁回调，会立即调用 <see cref="OnDestroy"/>；
        /// 此后访问引擎数据将抛出异常，实际回收发生在帧末。
        /// </summary>
        public void Delete()
        {
            ThrowIfDestroyed();
            var hasCallback = LuaSTGAPI.api.gameObject_queueToFree((nuint)_native, 0) != 0;
            _userDestroyed = true;
            if (hasCallback)
            {
                OnDestroy(new DestroyEventArgs(DestroyEventType.Del));
            }
        }

        /// <summary>
        /// 杀死对象（对应 lstg.Kill）。
        /// 若启用了销毁回调，会立即调用 <see cref="OnDestroy"/>；
        /// 此后访问引擎数据将抛出异常，实际回收发生在帧末。
        /// </summary>
        public void Kill()
        {
            ThrowIfDestroyed();
            var hasCallback = LuaSTGAPI.api.gameObject_queueToFree((nuint)_native, 1) != 0;
            _userDestroyed = true;
            if (hasCallback)
            {
                OnDestroy(new DestroyEventArgs(DestroyEventType.Kill));
            }
        }

        /// <summary>
        /// 访问引擎数据前检查对象有效性。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ThrowIfDestroyed()
        {
            if (_native == null || _userDestroyed)
            {
                throw new ObjectDisposedException(GetType().Name, "游戏对象已被销毁");
            }
        }

        // ========== 引擎回调（子类按需覆写） ==========

        /// <summary>每帧更新回调（对应 Lua 类的 frame 槽位）。</summary>
        public abstract void OnFrame();

        /// <summary>销毁回调（对应 Lua 类的 del/kill 槽位；出界回收也会触发）。</summary>
        public abstract void OnDestroy(DestroyEventArgs args);

        /// <summary>碰撞回调（对应 Lua 类的 colli 槽位）。</summary>
        public abstract void OnColli(Collision collision);

        /// <summary>
        /// 渲染回调（对应 Lua 类的 render 槽位）。
        /// 默认调用引擎默认渲染。
        /// </summary>
        public virtual void OnRender()
        {
            DefaultRenderFunc();
        }

        /// <summary>是否为渲染类（对应 Lua 类定义 .render = true，影响混合模式与顶点颜色属性）</summary>
        protected internal virtual bool IsRenderClass => false;

        /// <summary>调用引擎默认渲染函数</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void DefaultRenderFunc()
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.gameObject_defaultRender((nuint)_native);
        }

        // ========== 基本信息（只读） ==========

        /// <summary>对象在对象池中的下标</summary>
        public int Id
        {
            get { ThrowIfDestroyed(); return (int)_native->Id; }
        }

        /// <summary>对象全局唯一标识符</summary>
        public ulong UniqueId
        {
            get { ThrowIfDestroyed(); return _native->UniqueId; }
        }

        /// <summary>对象状态</summary>
        public GameObjectStatus Status
        {
            get { ThrowIfDestroyed(); return (GameObjectStatus)_native->status; }
            set
            {
                ThrowIfDestroyed();
                var v = (byte)value;
                if (v != (byte)GameObjectStatus.Active && v != (byte)GameObjectStatus.Dead && v != (byte)GameObjectStatus.Killed)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "状态只允许 Normal、Dead、Killed");
                }
                _native->status = v;
            }
        }

        // ========== 位置与运动 ==========

        /// <summary>坐标 X</summary>
        public double X
        {
            get { ThrowIfDestroyed(); return _native->x; }
            set { ThrowIfDestroyed(); _native->x = value; }
        }

        /// <summary>坐标 Y</summary>
        public double Y
        {
            get { ThrowIfDestroyed(); return _native->y; }
            set { ThrowIfDestroyed(); _native->y = value; }
        }

        /// <summary>坐标增量 X（只读）</summary>
        public double Dx
        {
            get { ThrowIfDestroyed(); return _native->dx; }
        }

        /// <summary>坐标增量 Y（只读）</summary>
        public double Dy
        {
            get { ThrowIfDestroyed(); return _native->dy; }
        }

        /// <summary>上一帧坐标 X（只读）</summary>
        public double LastX
        {
            get { ThrowIfDestroyed(); return _native->last_x; }
        }

        /// <summary>上一帧坐标 Y（只读）</summary>
        public double LastY
        {
            get { ThrowIfDestroyed(); return _native->last_y; }
        }

        /// <summary>速度 X 分量</summary>
        public double Vx
        {
            get { ThrowIfDestroyed(); return _native->vx; }
            set { ThrowIfDestroyed(); _native->vx = value; }
        }

        /// <summary>速度 Y 分量</summary>
        public double Vy
        {
            get { ThrowIfDestroyed(); return _native->vy; }
            set { ThrowIfDestroyed(); _native->vy = value; }
        }

        /// <summary>加速度 X 分量</summary>
        public double Ax
        {
            get { ThrowIfDestroyed(); return _native->ax; }
            set { ThrowIfDestroyed(); _native->ax = value; }
        }

        /// <summary>加速度 Y 分量</summary>
        public double Ay
        {
            get { ThrowIfDestroyed(); return _native->ay; }
            set { ThrowIfDestroyed(); _native->ay = value; }
        }

        /// <summary>速度 X 分量上限（自动取绝对值）</summary>
        public double MaxVx
        {
            get { ThrowIfDestroyed(); return _native->max_vx; }
            set { ThrowIfDestroyed(); _native->max_vx = Math.Abs(value); }
        }

        /// <summary>速度 Y 分量上限（自动取绝对值）</summary>
        public double MaxVy
        {
            get { ThrowIfDestroyed(); return _native->max_vy; }
            set { ThrowIfDestroyed(); _native->max_vy = Math.Abs(value); }
        }

        /// <summary>速度上限（自动取绝对值）</summary>
        public double MaxV
        {
            get { ThrowIfDestroyed(); return _native->max_v; }
            set { ThrowIfDestroyed(); _native->max_v = Math.Abs(value); }
        }

        /// <summary>重力加速度</summary>
        public double Ag
        {
            get { ThrowIfDestroyed(); return _native->ag; }
            set { ThrowIfDestroyed(); _native->ag = value; }
        }

        /// <summary>速度大小（对应 Lua 属性 _speed）</summary>
        public double Speed
        {
            get
            {
                ThrowIfDestroyed();
                return Math.Sqrt(_native->vx * _native->vx + _native->vy * _native->vy);
            }
            set
            {
                ThrowIfDestroyed();
                SetSpeed(value);
            }
        }

        /// <summary>速度方向，角度制（对应 Lua 属性 _angle）</summary>
        public double SpeedDirection
        {
            get
            {
                ThrowIfDestroyed();
                return CalculateSpeedDirection() * (180.0 / Math.PI);
            }
            set
            {
                ThrowIfDestroyed();
                SetSpeedDirection(value * (Math.PI / 180.0));
            }
        }

        /// <summary>计算速度大小（与引擎 calculateSpeed 一致）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal double CalculateSpeed()
        {
            return Math.Sqrt(_native->vx * _native->vx + _native->vy * _native->vy);
        }

        /// <summary>计算速度方向（弧度，与引擎 calculateSpeedDirection 一致）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal double CalculateSpeedDirection()
        {
            if (Math.Abs(_native->vx) > DoubleMinNormal && Math.Abs(_native->vy) > DoubleMinNormal)
            {
                return Math.Atan2(_native->vy, _native->vx);
            }
            return _native->rot;
        }

        /// <summary>设置速度大小，保持方向（与引擎 setSpeed 一致）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpeed(double speed)
        {
            ThrowIfDestroyed();
            var current = CalculateSpeed();
            if (current > DoubleMinNormal)
            {
                var scale = speed / current;
                _native->vx *= scale;
                _native->vy *= scale;
            }
            else
            {
                _native->vx = Math.Cos(_native->rot) * speed;
                _native->vy = Math.Sin(_native->rot) * speed;
            }
        }

        /// <summary>设置速度方向（弧度），保持速度大小（与引擎 setSpeedDirection 一致）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpeedDirection(double direction)
        {
            ThrowIfDestroyed();
            var speed = CalculateSpeed();
            if (speed > DoubleMinNormal)
            {
                _native->vx = speed * Math.Cos(direction);
                _native->vy = speed * Math.Sin(direction);
            }
            else
            {
                _native->rot = direction;
            }
        }

        // ========== 碰撞体 ==========

        /// <summary>碰撞组（0~15）</summary>
        public long Group
        {
            get { ThrowIfDestroyed(); return _native->group; }
            set
            {
                ThrowIfDestroyed();
                if (value < 0 || value >= 16)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "碰撞组取值范围为 0~15");
                }
                if (LuaSTGAPI.api.gameObject_setGroup((nuint)_native, value) == 0)
                {
                    throw new InvalidOperationException("碰撞检测进行中不允许修改 group");
                }
            }
        }

        /// <summary>是否出界自动回收</summary>
        public bool Bound
        {
            get { ThrowIfDestroyed(); return _native->Bound; }
            set { ThrowIfDestroyed(); _native->Bound = value; }
        }

        /// <summary>是否参与碰撞</summary>
        public bool Colli
        {
            get { ThrowIfDestroyed(); return _native->Colli; }
            set { ThrowIfDestroyed(); _native->Colli = value; }
        }

        /// <summary>是否为矩形碰撞盒（修改后自动更新外接圆半径）</summary>
        public bool Rect
        {
            get { ThrowIfDestroyed(); return _native->Rect; }
            set { ThrowIfDestroyed(); _native->Rect = value; _native->UpdateCollisionCircleRadius(); }
        }

        /// <summary>
        /// 碰撞盒横向半宽（非矩形时为半径或椭圆横向半轴）。
        /// 引擎启用 GLOBAL_SCALE_COLLI_SHAPE 时按全局缩放换算。
        /// </summary>
        public double ColliA
        {
            get
            {
                ThrowIfDestroyed();
                return (LuaSTGAPI.EngineFeatureFlags & LuaSTGAPI.FeatureGlobalScaleColliShape) != 0
                    ? _native->a / GameObjectManager.GlobalImageScale
                    : _native->a;
            }
            set
            {
                ThrowIfDestroyed();
                _native->a = (LuaSTGAPI.EngineFeatureFlags & LuaSTGAPI.FeatureGlobalScaleColliShape) != 0
                    ? value * GameObjectManager.GlobalImageScale
                    : value;
                _native->UpdateCollisionCircleRadius();
            }
        }

        /// <summary>
        /// 碰撞盒纵向半宽（非矩形时为半径或椭圆纵向半轴）。
        /// 引擎启用 GLOBAL_SCALE_COLLI_SHAPE 时按全局缩放换算。
        /// </summary>
        public double ColliB
        {
            get
            {
                ThrowIfDestroyed();
                return (LuaSTGAPI.EngineFeatureFlags & LuaSTGAPI.FeatureGlobalScaleColliShape) != 0
                    ? _native->b / GameObjectManager.GlobalImageScale
                    : _native->b;
            }
            set
            {
                ThrowIfDestroyed();
                _native->b = (LuaSTGAPI.EngineFeatureFlags & LuaSTGAPI.FeatureGlobalScaleColliShape) != 0
                    ? value * GameObjectManager.GlobalImageScale
                    : value;
                _native->UpdateCollisionCircleRadius();
            }
        }

        /// <summary>与另一对象是否相交（与 lstg.ColliCheck 一致）</summary>
        public bool IsIntersect(GameObjectBase other)
        {
            ThrowIfDestroyed();
            if (other == null) throw new ArgumentNullException(nameof(other));
            other.ThrowIfDestroyed();
            return LuaSTGAPI.api.gameObject_isIntersect((nuint)_native, (nuint)other._native) != 0;
        }

        /// <summary>坐标是否在矩形范围内（与 lstg.BoxCheck 一致）</summary>
        public bool IsInRect(double l, double r, double b, double t)
        {
            ThrowIfDestroyed();
            return _native->x >= l && _native->x <= r && _native->y >= b && _native->y <= t;
        }

        // ========== 渲染 ==========

        /// <summary>图层（渲染顺序）</summary>
        public double Layer
        {
            get { ThrowIfDestroyed(); return _native->layer; }
            set
            {
                ThrowIfDestroyed();
                if (LuaSTGAPI.api.gameObject_setLayer((nuint)_native, value) == 0)
                {
                    throw new InvalidOperationException("渲染中不允许修改 layer");
                }
            }
        }

        /// <summary>横向渲染缩放</summary>
        public double HScale
        {
            get { ThrowIfDestroyed(); return _native->hscale; }
            set { ThrowIfDestroyed(); _native->hscale = value; }
        }

        /// <summary>纵向渲染缩放</summary>
        public double VScale
        {
            get { ThrowIfDestroyed(); return _native->vscale; }
            set { ThrowIfDestroyed(); _native->vscale = value; }
        }

        /// <summary>渲染旋转角，角度制（内部存储为弧度，与 Lua 侧一致做换算）</summary>
        public double Rot
        {
            get { ThrowIfDestroyed(); return _native->rot * (180.0 / Math.PI); }
            set { ThrowIfDestroyed(); _native->rot = value * (Math.PI / 180.0); }
        }

        /// <summary>渲染旋转角速度，角度制（内部存储为弧度）</summary>
        public double Omega
        {
            get { ThrowIfDestroyed(); return _native->omega * (180.0 / Math.PI); }
            set { ThrowIfDestroyed(); _native->omega = value * (Math.PI / 180.0); }
        }

        /// <summary>是否隐藏（不渲染）</summary>
        public bool Hide
        {
            get { ThrowIfDestroyed(); return _native->Hide; }
            set { ThrowIfDestroyed(); _native->Hide = value; }
        }

        /// <summary>是否根据速度方向自动设置渲染旋转角</summary>
        public bool Navi
        {
            get { ThrowIfDestroyed(); return _native->Navi; }
            set { ThrowIfDestroyed(); _native->Navi = value; }
        }

        /// <summary>动画计时器（只读）</summary>
        public long AniTimer
        {
            get { ThrowIfDestroyed(); return _native->ani_timer; }
        }

        /// <summary>顶点颜色（仅渲染类生效），0xAARRGGBB</summary>
        public uint VertexColor
        {
            get { ThrowIfDestroyed(); return _native->VertexColor; }
            set { ThrowIfDestroyed(); _native->VertexColor = value; }
        }

        /// <summary>混合模式（仅渲染类生效）</summary>
        public BlendMode BlendMode
        {
            get { ThrowIfDestroyed(); return (BlendMode)_native->blend_mode; }
            set { ThrowIfDestroyed(); _native->blend_mode = (byte)value; }
        }

        /// <summary>渲染资源名称（对应 Lua 属性 img，无资源时返回 null）</summary>
        public string? ResourceName
        {
            get
            {
                ThrowIfDestroyed();
                return _native->HasRenderResource
                    ? StringMarshal.FromUtf8(LuaSTGAPI.api.gameObject_getResourceName((nuint)_native))
                    : null;
            }
        }

        // ========== 更新控制 ==========

        /// <summary>自增计数器</summary>
        public long Timer
        {
            get { ThrowIfDestroyed(); return _native->timer; }
            set { ThrowIfDestroyed(); _native->timer = value; }
        }

        /// <summary>是否无视超级暂停</summary>
        public bool IgnoreSuperPause
        {
            get { ThrowIfDestroyed(); return _native->IgnoreSuperPause; }
            set { ThrowIfDestroyed(); _native->IgnoreSuperPause = value; }
        }

        // ========== 引擎操作 ==========

        /// <summary>设置渲染资源（对应 Lua 属性 img 赋值）。失败时抛出异常。</summary>
        public void SetResource(string resourceName)
        {
            ThrowIfDestroyed();
            using var name = new MarshaledString(resourceName);
            if (LuaSTGAPI.api.gameObject_changeResource((nuint)_native, name) == 0)
            {
                throw new ArgumentException($"资源 '{resourceName}' 不存在");
            }
        }

        /// <summary>释放渲染资源</summary>
        public void ReleaseResource()
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.gameObject_releaseResource((nuint)_native);
        }

        /// <summary>重置对象属性并释放资源（保留 id，对应 lstg.ResetObject）</summary>
        public void Reset()
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.gameObject_dirtReset((nuint)_native);
        }

        /// <summary>设置渲染状态（对应 lstg.SetImgState）</summary>
        public void SetRenderState(BlendMode blend, uint argb)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.gameObject_setResourceRenderState((nuint)_native, (byte)blend, argb);
        }

        /// <summary>设置粒子渲染状态（对应 lstg.SetParState）</summary>
        public void SetParticleRenderState(BlendMode blend, uint argb)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.gameObject_setParticleRenderState((nuint)_native, (byte)blend, argb);
        }

        /// <summary>停止粒子发射</summary>
        public void StopParticle()
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.gameObject_stopParticle((nuint)_native);
        }

        /// <summary>开始粒子发射</summary>
        public void FireParticle()
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.gameObject_fireParticle((nuint)_native);
        }

        /// <summary>获取粒子数量</summary>
        public uint ParticleCount
        {
            get { ThrowIfDestroyed(); return LuaSTGAPI.api.gameObject_getParticleCount((nuint)_native); }
        }

        /// <summary>粒子发射速率</summary>
        public int ParticleEmission
        {
            get { ThrowIfDestroyed(); return LuaSTGAPI.api.gameObject_getParticleEmission((nuint)_native); }
            set { ThrowIfDestroyed(); LuaSTGAPI.api.gameObject_setParticleEmission((nuint)_native, value); }
        }

        // ========== 内部：引擎回调分发 ==========

        internal static class GameObjectCallbacks
        {
            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
            internal static void DetachGameObject(uint id)
            {
                var obj = id < (uint)IdToObject.Length ? IdToObject[id] : null;
                if (obj != null)
                {
                    obj._native = null;
                    IdToObject[id] = null;
                }
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
            internal static void CallOnFrame(uint id)
            {
                var obj = IdToObject[id];
                if (obj == null || obj._userDestroyed)
                {
                    return;
                }
                try
                {
                    obj.OnFrame();
                }
                catch (Exception e)
                {
                    LuaSTGAPI.Log(LogLevel.Error, $"OnFrame 异常（对象 {id}）：{e}");
                }
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
            internal static void CallOnRender(uint id)
            {
                var obj = IdToObject[id];
                if (obj == null || obj._userDestroyed)
                {
                    return;
                }
                try
                {
                    obj.OnRender();
                }
                catch (Exception e)
                {
                    LuaSTGAPI.Log(LogLevel.Error, $"OnRender 异常（对象 {id}）：{e}");
                }
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
            internal static void CallOnDestroy(uint id, byte reason)
            {
                var obj = IdToObject[id];
                if (obj == null || obj._userDestroyed)
                {
                    return; // 语言侧 Delete/Kill 已调用过 OnDestroy
                }
                try
                {
                    obj.OnDestroy(new DestroyEventArgs((DestroyEventType)reason));
                }
                catch (Exception e)
                {
                    LuaSTGAPI.Log(LogLevel.Error, $"OnDestroy 异常（对象 {id}）：{e}");
                }
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
            internal static void CallOnColli(uint id, uint otherId)
            {
                var obj = IdToObject[id];
                if (obj == null || obj._userDestroyed)
                {
                    return;
                }
                var other = GameObjectManager.GetOrCreateWrapper(otherId);
                try
                {
                    obj.OnColli(new Collision(obj, other));
                }
                catch (Exception e)
                {
                    LuaSTGAPI.Log(LogLevel.Error, $"OnColli 异常（对象 {id}）：{e}");
                }
            }
        }

        /// <summary>
        /// 按类型缓存回调特性掩码。
        /// bit0=frame bit1=render bit2=colli bit3=del bit4=kill bit5=render_class bit6=create bit7=is_class
        /// </summary>
        internal static class GameObjectFeatureMask
        {
            private static readonly ConcurrentDictionary<Type, uint> Cache = new();

            internal static uint Get(Type type)
            {
                return Cache.GetOrAdd(type, static t =>
                {
                    uint mask = 1u << 7; // is_class
                    if (IsRenderClassOf(t)) mask |= 1u << 5;
                    if (Overrides(t, nameof(OnFrame))) mask |= 1u << 0;
                    if (Overrides(t, nameof(OnRender))) mask |= 1u << 1;
                    if (Overrides(t, nameof(OnColli))) mask |= 1u << 2;
                    if (Overrides(t, nameof(OnDestroy))) mask |= 1u << 3 | 1u << 4;
                    return mask;
                });
            }

            private static bool IsRenderClassOf(Type t)
            {
                // 检查 IsRenderClass 是否被覆写为 true
                var prop = t.GetProperty("IsRenderClass",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop?.PropertyType == typeof(bool) && prop.GetMethod?.DeclaringType != typeof(GameObjectBase))
                {
                    try
                    {
                        // 创建未初始化实例避免构造函数副作用，仅读取属性
                        var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);
                        return (bool)prop.GetValue(instance)!;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }

            private static bool Overrides(Type t, string methodName)
            {
                var method = t.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                return method != null && method.DeclaringType != typeof(GameObjectBase);
            }
        }
    }
}
