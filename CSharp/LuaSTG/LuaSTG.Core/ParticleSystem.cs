using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 粒子信息数值字段编号（与引擎侧 CLRMiscObject.cpp 的 PSField 保持同步，
    /// 对应引擎 hgeParticleSystemInfo 的各 float 成员）。
    /// </summary>
    internal enum ParticleSystemInfoField : byte
    {
        Lifetime = 0,            // fLifetime 生命期
        ParticleLifeMin = 1,     // fParticleLifeMin 粒子最小生命期
        ParticleLifeMax = 2,     // fParticleLifeMax 粒子最大生命期
        Direction = 3,           // fDirection 发射方向
        Spread = 4,              // fSpread 偏移角度
        SpeedMin = 5,            // fSpeedMin 速度最小值
        SpeedMax = 6,            // fSpeedMax 速度最大值
        GravityMin = 7,          // fGravityMin 重力最小值
        GravityMax = 8,          // fGravityMax 重力最大值
        RadialAccelMin = 9,      // fRadialAccelMin 最低径向加速度
        RadialAccelMax = 10,     // fRadialAccelMax 最高径向加速度
        TangentialAccelMin = 11, // fTangentialAccelMin 最低切向加速度
        TangentialAccelMax = 12, // fTangentialAccelMax 最高切向加速度
        SizeStart = 13,          // fSizeStart 起始大小
        SizeEnd = 14,            // fSizeEnd 最终大小
        SizeVar = 15,            // fSizeVar 大小抖动值
        SpinStart = 16,          // fSpinStart 起始自旋
        SpinEnd = 17,            // fSpinEnd 最终自旋
        SpinVar = 18,            // fSpinVar 自旋抖动值
        ColorVar = 19,           // fColorVar 颜色抖动值
        AlphaVar = 20,           // fAlphaVar alpha 抖动值
    }

    /// <summary>
    /// 粒子系统实例（对应 Lua 侧 lstg.ParticleSystemData，见 LuaBinding/LW_ParticleSystem.cpp）。
    /// 构造时按名称绑定 LoadPS 加载的粒子资源并在引擎侧创建实例（持有资源引用），
    /// <see cref="Dispose"/> 销毁实例并释放资源引用；
    /// 销毁后访问会抛出 <see cref="ObjectDisposedException"/>。
    /// 引擎数据每次访问都直接读取，不缓存。
    /// </summary>
    /// <remarks>
    /// 与 Lua 侧的差异：
    /// - LuaSTG-x 风格的 get*/set* 移植为同名 PascalCase 属性；
    /// - getRenderMode/setRenderMode 使用 <see cref="BlendMode"/> 枚举而非字符串；
    /// - 颜色属性返回/接受 <see cref="Color"/>（0~255 分量），
    ///   另提供 Get/Set 方法以 0~1 浮点分量读写；
    /// - <see cref="Rotation"/> 为弧度（与 Lua 侧 getRotation/setRotation 一致），
    ///   而 <see cref="Update(double, double, double, double)"/> 的 rot 参数为角度制
    ///   （与 Lua 侧 Update 的换算一致）。
    /// </remarks>
    public sealed unsafe class ParticleSystemData : IDisposable
    {
        /// <summary>引擎侧粒子系统句柄，0 表示已销毁</summary>
        private nuint _handle;

        /// <summary>创建粒子系统实例</summary>
        /// <param name="resourceName">LoadPS 加载的粒子资源名称</param>
        /// <exception cref="ArgumentException">资源不存在或实例创建失败</exception>
        public ParticleSystemData(string resourceName)
        {
            using var name = new MarshaledString(resourceName);
            _handle = LuaSTGAPI.api.particleSystem_create(name);
            if (_handle == 0)
            {
                throw new ArgumentException(
                    $"particle system '{resourceName}' not found.", nameof(resourceName));
            }
        }

        /// <summary>对象是否仍然有效（未被销毁）</summary>
        public bool IsValid => _handle != 0;

        /// <summary>销毁粒子系统实例（对应 Lua 侧 userdata 回收）</summary>
        public void Dispose()
        {
            if (_handle != 0)
            {
                LuaSTGAPI.api.particleSystem_destroy(_handle);
                _handle = 0;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_handle == 0)
            {
                throw new ObjectDisposedException(nameof(ParticleSystemData), "ParticleSystemData 已销毁");
            }
        }

        private static byte ToByte(bool value) => value ? (byte)1 : (byte)0;

        // ========== 经典 API（对应 SetActive/GetAliveCount/SetEmission/GetEmission/Update/Render/SetOldBehavior） ==========

        /// <summary>设置是否激活（对应 SetActive / setActive）</summary>
        public void SetActive(bool value)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_setActive(_handle, ToByte(value));
        }

        /// <summary>获取存活粒子数量（对应 GetAliveCount）</summary>
        public ulong GetAliveCount()
        {
            ThrowIfDisposed();
            return (ulong)LuaSTGAPI.api.particleSystem_getAliveCount(_handle);
        }

        /// <summary>设置发射速率（对应 SetEmission）</summary>
        public void SetEmission(int value)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_setEmission(_handle, value);
        }

        /// <summary>获取发射速率（对应 GetEmission）</summary>
        public int GetEmission()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.particleSystem_getEmission(_handle);
        }

        /// <summary>更新粒子系统（对应 Update 无参数形式，默认步长 1/60 秒）</summary>
        public void Update()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_update(_handle, 1.0 / 60.0, 0.0, 0.0, 0.0, 0);
        }

        /// <summary>更新粒子系统（对应 Update 的单参数形式）</summary>
        /// <param name="delta">时间步长（秒）</param>
        public void Update(double delta)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_update(_handle, delta, 0.0, 0.0, 0.0, 0);
        }

        /// <summary>更新粒子系统并移动中心（对应 Update 的三参数形式）</summary>
        /// <param name="delta">时间步长（秒）</param>
        /// <param name="x">中心坐标 X</param>
        /// <param name="y">中心坐标 Y</param>
        public void Update(double delta, double x, double y)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_update(_handle, delta, x, y, 0.0, 1);
        }

        /// <summary>
        /// 更新粒子系统、移动中心并设置发射朝向（对应 Update 的四参数形式）。
        /// 引擎激活状态时会按旧版兼容逻辑先停用再移动再激活。
        /// </summary>
        /// <param name="delta">时间步长（秒）</param>
        /// <param name="x">中心坐标 X</param>
        /// <param name="y">中心坐标 Y</param>
        /// <param name="rot">发射朝向（角度制，引擎内部换算为弧度）</param>
        public void Update(double delta, double x, double y, double rot)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_update(_handle, delta, x, y, rot, 2);
        }

        /// <summary>渲染粒子系统（对应 Render），等比缩放</summary>
        /// <param name="scale">缩放系数</param>
        public void Render(double scale)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_render(_handle, scale);
        }

        /// <summary>设置旧行为开关（对应 SetOldBehavior）</summary>
        public void SetOldBehavior(bool value)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_setOldBehavior(_handle, ToByte(value));
        }

        // ========== LuaSTG-x 风格属性 ==========

        /// <summary>是否激活（对应 isActive）</summary>
        public bool IsActive
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_isActive(_handle) != 0; }
        }

        /// <summary>存活粒子数量（对应 getAliveCount）</summary>
        public ulong AliveCount
        {
            get { ThrowIfDisposed(); return (ulong)LuaSTGAPI.api.particleSystem_getAliveCount(_handle); }
        }

        /// <summary>每秒发射个数（对应 getEmissionFreq / setEmissionFreq）</summary>
        public int EmissionFreq
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getEmissionFreq(_handle); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setEmissionFreq(_handle, value); }
        }

        /// <summary>使用相对值还是绝对值（对应 getRelative / setRelative）</summary>
        public bool Relative
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_isRelative(_handle) != 0; }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setRelative(_handle, ToByte(value)); }
        }

        /// <summary>资源名称（对应 getResource）</summary>
        public string Resource
        {
            get { ThrowIfDisposed(); return StringMarshal.FromUtf8(LuaSTGAPI.api.particleSystem_getResourceName(_handle)); }
        }

        /// <summary>发射朝向，弧度（对应 getRotation / setRotation）</summary>
        public float Rotation
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getRotation(_handle); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setRotation(_handle, value); }
        }

        /// <summary>随机数种子（对应 getSeed / setSeed）</summary>
        public uint Seed
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getSeed(_handle); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setSeed(_handle, value); }
        }

        /// <summary>渲染混合模式（对应 getRenderMode / setRenderMode）</summary>
        public BlendMode RenderMode
        {
            get { ThrowIfDisposed(); return (BlendMode)LuaSTGAPI.api.particleSystem_getBlendMode(_handle); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setBlendMode(_handle, (byte)value); }
        }

        /// <summary>中心坐标 X（对应 getCenter 的 x 分量）</summary>
        public float CenterX
        {
            get { ThrowIfDisposed(); GetCenter(out var x, out _); return x; }
        }

        /// <summary>中心坐标 Y（对应 getCenter 的 y 分量）</summary>
        public float CenterY
        {
            get { ThrowIfDisposed(); GetCenter(out _, out var y); return y; }
        }

        /// <summary>读取中心坐标（对应 getCenter）</summary>
        public void GetCenter(out float x, out float y)
        {
            ThrowIfDisposed();
            float tx = 0.0f, ty = 0.0f;
            LuaSTGAPI.api.particleSystem_getCenter(_handle, &tx, &ty);
            x = tx;
            y = ty;
        }

        /// <summary>设置中心坐标（对应 setCenter）</summary>
        public void SetCenter(double x, double y)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_setCenter(_handle, x, y);
        }

        // ----- 粒子信息数值字段（对应各 get*/set*） -----

        /// <summary>粒子系统生命期（对应 getLifetime / setLifetime）</summary>
        public float Lifetime
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.Lifetime); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.Lifetime, value); }
        }

        /// <summary>粒子最小生命期（对应 getLifeMin / setLifeMin）</summary>
        public float LifeMin
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.ParticleLifeMin); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.ParticleLifeMin, value); }
        }

        /// <summary>粒子最大生命期（对应 getLifeMax / setLifeMax）</summary>
        public float LifeMax
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.ParticleLifeMax); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.ParticleLifeMax, value); }
        }

        /// <summary>发射方向（对应 getDirection / setDirection）</summary>
        public float Direction
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.Direction); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.Direction, value); }
        }

        /// <summary>偏移角度（对应 getSpread / setSpread）</summary>
        public float Spread
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.Spread); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.Spread, value); }
        }

        /// <summary>速度最小值（对应 getSpeedMin / setSpeedMin）</summary>
        public float SpeedMin
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SpeedMin); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SpeedMin, value); }
        }

        /// <summary>速度最大值（对应 getSpeedMax / setSpeedMax）</summary>
        public float SpeedMax
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SpeedMax); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SpeedMax, value); }
        }

        /// <summary>重力最小值（对应 getGravityMin / setGravityMin）</summary>
        public float GravityMin
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.GravityMin); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.GravityMin, value); }
        }

        /// <summary>重力最大值（对应 getGravityMax / setGravityMax）</summary>
        public float GravityMax
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.GravityMax); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.GravityMax, value); }
        }

        /// <summary>最低径向加速度（对应 getRadialAccelMin / setRadialAccelMin）</summary>
        public float RadialAccelMin
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.RadialAccelMin); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.RadialAccelMin, value); }
        }

        /// <summary>最高径向加速度（对应 getRadialAccelMax / setRadialAccelMax）</summary>
        public float RadialAccelMax
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.RadialAccelMax); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.RadialAccelMax, value); }
        }

        /// <summary>最低切向加速度（对应 getTangentialAccelMin / setTangentialAccelMin）</summary>
        public float TangentialAccelMin
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.TangentialAccelMin); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.TangentialAccelMin, value); }
        }

        /// <summary>最高切向加速度（对应 getTangentialAccelMax / setTangentialAccelMax）</summary>
        public float TangentialAccelMax
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.TangentialAccelMax); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.TangentialAccelMax, value); }
        }

        /// <summary>起始大小（对应 getSizeStart / setSizeStart）</summary>
        public float SizeStart
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SizeStart); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SizeStart, value); }
        }

        /// <summary>最终大小（对应 getSizeEnd / setSizeEnd）</summary>
        public float SizeEnd
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SizeEnd); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SizeEnd, value); }
        }

        /// <summary>大小抖动值（对应 getSizeVar / setSizeVar）</summary>
        public float SizeVar
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SizeVar); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SizeVar, value); }
        }

        /// <summary>起始自旋（对应 getSpinStart / setSpinStart）</summary>
        public float SpinStart
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SpinStart); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SpinStart, value); }
        }

        /// <summary>最终自旋（对应 getSpinEnd / setSpinEnd）</summary>
        public float SpinEnd
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SpinEnd); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SpinEnd, value); }
        }

        /// <summary>自旋抖动值（对应 getSpinVar / setSpinVar）</summary>
        public float SpinVar
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.SpinVar); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.SpinVar, value); }
        }

        /// <summary>颜色抖动值（对应 getColorVar / setColorVar）</summary>
        public float ColorVar
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.ColorVar); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.ColorVar, value); }
        }

        /// <summary>alpha 抖动值（对应 getAlphaVar / setAlphaVar）</summary>
        public float AlphaVar
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.particleSystem_getInfoField(_handle, (byte)ParticleSystemInfoField.AlphaVar); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setInfoField(_handle, (byte)ParticleSystemInfoField.AlphaVar, value); }
        }

        // ----- 粒子信息颜色字段（对应 getColorStart/getColorEnd 及 set 版本） -----

        /// <summary>起始颜色（对应 getColorStart / setColorStart 的 Color 形式）</summary>
        public Color ColorStart
        {
            get { ThrowIfDisposed(); return Color.FromArgb(LuaSTGAPI.api.particleSystem_getColorField(_handle, 0)); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setColorField(_handle, 0, value); }
        }

        /// <summary>最终颜色（对应 getColorEnd / setColorEnd 的 Color 形式）</summary>
        public Color ColorEnd
        {
            get { ThrowIfDisposed(); return Color.FromArgb(LuaSTGAPI.api.particleSystem_getColorField(_handle, 1)); }
            set { ThrowIfDisposed(); LuaSTGAPI.api.particleSystem_setColorField(_handle, 1, value); }
        }

        /// <summary>以 0~1 浮点分量读取起始颜色（对应 getColorStart 的 table 形式）</summary>
        public void GetColorStart(out float r, out float g, out float b, out float a)
        {
            ThrowIfDisposed();
            float tr = 0.0f, tg = 0.0f, tb = 0.0f, ta = 0.0f;
            LuaSTGAPI.api.particleSystem_getColorFieldF(_handle, 0, &tr, &tg, &tb, &ta);
            r = tr;
            g = tg;
            b = tb;
            a = ta;
        }

        /// <summary>以 0~1 浮点分量读取最终颜色（对应 getColorEnd 的 table 形式）</summary>
        public void GetColorEnd(out float r, out float g, out float b, out float a)
        {
            ThrowIfDisposed();
            float tr = 0.0f, tg = 0.0f, tb = 0.0f, ta = 0.0f;
            LuaSTGAPI.api.particleSystem_getColorFieldF(_handle, 1, &tr, &tg, &tb, &ta);
            r = tr;
            g = tg;
            b = tb;
            a = ta;
        }

        /// <summary>以 0~1 浮点分量设置起始颜色（对应 setColorStart 的 table 形式）</summary>
        public void SetColorStart(float r, float g, float b, float a)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_setColorFieldF(_handle, 0, r, g, b, a);
        }

        /// <summary>以 0~1 浮点分量设置最终颜色（对应 setColorEnd 的 table 形式）</summary>
        public void SetColorEnd(float r, float g, float b, float a)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.particleSystem_setColorFieldF(_handle, 1, r, g, b, a);
        }
    }
}
