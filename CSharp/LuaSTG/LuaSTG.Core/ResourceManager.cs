using System;
using System.Collections.Generic;

namespace LuaSTG.Core
{
    /// <summary>
    /// 资源池类型（对应引擎侧 luastg::ResourcePoolType，
    /// 亦即 Lua 侧 SetResourceStatus/GetResourceStatus 的 "global"/"stage"/"none"）。
    /// </summary>
    public enum ResourcePoolType : int
    {
        /// <summary>无激活资源池（不可加载资源）</summary>
        None = 0,
        /// <summary>全局资源池</summary>
        Global = 1,
        /// <summary>关卡资源池</summary>
        Stage = 2,
    }

    /// <summary>
    /// 资源类型（对应引擎侧 luastg::ResourceType，取值与 Lua 侧 CheckRes/EnumRes 的数字参数一致）。
    /// </summary>
    public enum ResourceType : int
    {
        /// <summary>纹理（1）</summary>
        Texture = 1,
        /// <summary>图片精灵（2）</summary>
        Sprite = 2,
        /// <summary>动画（3）</summary>
        Animation = 3,
        /// <summary>音乐（4）</summary>
        Music = 4,
        /// <summary>音效（5）</summary>
        SoundEffect = 5,
        /// <summary>粒子特效（6）</summary>
        Particle = 6,
        /// <summary>纹理字体（7）</summary>
        SpriteFont = 7,
        /// <summary>矢量字体（8）</summary>
        TrueTypeFont = 8,
        /// <summary>后期特效着色器（9）</summary>
        FX = 9,
        /// <summary>模型（10）</summary>
        Model = 10,
    }

    /// <summary>
    /// 纹理采样器状态（取值与引擎侧 core::Graphics::IRenderer::SamplerState 一致，
    /// 亦即 Lua 侧 SetTextureSamplerState 的 "point+wrap"/"point+clamp"/"linear+wrap"/"linear+clamp"/""）。
    /// </summary>
    public enum TextureSamplerState : byte
    {
        /// <summary>点采样 + 重复寻址（"point+wrap"）</summary>
        PointWrap = 0,
        /// <summary>点采样 + 钳制寻址（"point+clamp"）</summary>
        PointClamp = 1,
        /// <summary>点采样 + 黑色边界</summary>
        PointBorderBlack = 2,
        /// <summary>点采样 + 白色边界</summary>
        PointBorderWhite = 3,
        /// <summary>线性采样 + 重复寻址（"linear+wrap"）</summary>
        LinearWrap = 4,
        /// <summary>线性采样 + 钳制寻址（"linear+clamp"，Lua 侧空串的默认值）</summary>
        LinearClamp = 5,
        /// <summary>线性采样 + 黑色边界</summary>
        LinearBorderBlack = 6,
        /// <summary>线性采样 + 白色边界</summary>
        LinearBorderWhite = 7,
    }

    /// <summary>
    /// 粒子特效定义（对应 Lua 侧 LoadPS 第二参数为 table 的形式，
    /// 字段名与 Lua table 的键一一对应，角度分量使用角度制）。
    /// </summary>
    public sealed class ParticleDefinition
    {
        /// <summary>emission：每秒发射个数</summary>
        public int Emission;
        /// <summary>lifetime：生命期（秒）</summary>
        public double Lifetime;
        /// <summary>direction：发射方向（角度制）</summary>
        public double Direction;
        /// <summary>spread：偏移角度（角度制）</summary>
        public double Spread;
        /// <summary>lifetime_min：粒子最小生命期</summary>
        public double LifetimeMin;
        /// <summary>lifetime_max：粒子最大生命期</summary>
        public double LifetimeMax;
        /// <summary>speed_min：速度最小值</summary>
        public double SpeedMin;
        /// <summary>speed_max：速度最大值</summary>
        public double SpeedMax;
        /// <summary>gravity_min：重力最小值</summary>
        public double GravityMin;
        /// <summary>gravity_max：重力最大值</summary>
        public double GravityMax;
        /// <summary>radial_min：最低径向加速度</summary>
        public double RadialMin;
        /// <summary>radial_max：最高径向加速度</summary>
        public double RadialMax;
        /// <summary>tangential_min：最低切向加速度</summary>
        public double TangentialMin;
        /// <summary>tangential_max：最高切向加速度</summary>
        public double TangentialMax;
        /// <summary>size_begin：起始大小</summary>
        public double SizeBegin;
        /// <summary>size_end：最终大小</summary>
        public double SizeEnd;
        /// <summary>size_var：大小抖动值</summary>
        public double SizeVar;
        /// <summary>angle_begin：起始自旋（角度制）</summary>
        public double AngleBegin;
        /// <summary>angle_end：最终自旋（角度制）</summary>
        public double AngleEnd;
        /// <summary>angle_var：自旋抖动值（引擎侧不做角度转换）</summary>
        public double AngleVar;
        /// <summary>color_var：颜色抖动值</summary>
        public double ColorVar;
        /// <summary>alpha_var：alpha 抖动值</summary>
        public double AlphaVar;
        /// <summary>blend："alpha" 为 true，"add" 为 false（引擎侧默认为 add）</summary>
        public bool BlendIsAlpha;
        /// <summary>color_begin：起始颜色（r、g、b、a 分量，0~1）</summary>
        public double ColorBeginR, ColorBeginG, ColorBeginB, ColorBeginA;
        /// <summary>color_end：最终颜色（r、g、b、a 分量，0~1）</summary>
        public double ColorEndR, ColorEndG, ColorEndB, ColorEndA;

        /// <summary>按引擎侧约定打包为 30 个 double（布局见 API/Resource.hpp 注释）</summary>
        internal double[] Pack()
        {
            return new double[30]
            {
                Emission, Lifetime, Direction, Spread,
                LifetimeMin, LifetimeMax,
                SpeedMin, SpeedMax, GravityMin, GravityMax,
                RadialMin, RadialMax, TangentialMin, TangentialMax,
                SizeBegin, SizeEnd, SizeVar,
                AngleBegin, AngleEnd, AngleVar,
                ColorVar, AlphaVar,
                ColorBeginR, ColorBeginG, ColorBeginB, ColorBeginA,
                ColorEndR, ColorEndG, ColorEndB, ColorEndA,
            };
        }
    }

    /// <summary>
    /// 矢量字体组中的单个字体描述（对应 Lua 侧 LoadTrueTypeFont 第二参数 table 数组的元素）。
    /// </summary>
    public sealed class TrueTypeFontDesc
    {
        /// <summary>source：字体文件路径</summary>
        public string Source = "";
        /// <summary>font_face：字面索引（一个字体文件中有多个字面）</summary>
        public uint FontFace;
        /// <summary>width：像素宽度</summary>
        public float Width;
        /// <summary>height：像素高度</summary>
        public float Height;
    }

    /// <summary>
    /// 资源管理器（对应 Lua 侧 lstg.ResourceManager / lstg 顶层的资源 API，
    /// 以及 luaopen_LuaSTG_Sub 的 lstg.ResourceManager 现代类型 API）。
    /// </summary>
    /// <remarks>
    /// 兼容 API（LoadTexture/LoadImage/...）作用于“当前资源池”
    /// （<see cref="ResourceStatus"/> 为 <see cref="ResourcePoolType.None"/> 时抛出异常，
    /// 与 Lua 侧 "can't load resource at this time." 一致）；
    /// 现代类型 API 通过 <see cref="GetResourceCollection"/> 获得的
    /// <see cref="ResourceCollection"/> 显式指定资源池。
    /// </remarks>
    public static unsafe partial class ResourceManager
    {
        // ========== 资源池状态 ==========

        /// <summary>
        /// 当前激活的资源池（对应 lstg.SetResourceStatus/GetResourceStatus，
        /// 现代类型 API 为 lstg.ResourceManager.setCurrentResourceCollection/getCurrentResourceCollection）。
        /// </summary>
        /// <exception cref="ArgumentException">赋值为无效类型</exception>
        public static ResourcePoolType ResourceStatus
        {
            get => (ResourcePoolType)LuaSTGAPI.api.res_getPoolStatus();
            set
            {
                if (LuaSTGAPI.api.res_setPoolStatus((int)value) != 0)
                    throw new ArgumentException($"invalid resource pool type '{value}'.", nameof(value));
            }
        }

        /// <summary>当前资源池（现代类型 API 名，与 <see cref="ResourceStatus"/> 等价）</summary>
        public static ResourcePoolType CurrentResourceCollection
        {
            get => ResourceStatus;
            set => ResourceStatus = value;
        }

        /// <summary>
        /// 获取指定资源池的集合视图（对应 lstg.ResourceManager.getResourceCollection）。
        /// </summary>
        /// <exception cref="ArgumentException">pool 不是 Global/Stage</exception>
        public static ResourceCollection GetResourceCollection(ResourcePoolType pool)
        {
            if (pool != ResourcePoolType.Global && pool != ResourcePoolType.Stage)
                throw new ArgumentException($"resource set '{pool}' not found", nameof(pool));
            return new ResourceCollection(pool);
        }

        /// <summary>
        /// 资源加载日志开关（对应 lstg.SetResLoadInfo）。
        /// </summary>
        public static bool ResourceLoadingLog
        {
            get => LuaSTGAPI.api.res_getResourceLoadingLog() != 0;
            set => LuaSTGAPI.api.res_setResourceLoadingLog(value ? (byte)1 : (byte)0);
        }

        /// <summary>对应 lstg.SetResLoadInfo</summary>
        public static void SetResLoadInfo(bool enable) => ResourceLoadingLog = enable;

        // ========== 全局图像缩放 ==========

        /// <summary>
        /// 全局图像缩放系数（对应 lstg.SetImageScale/GetImageScale 的无名形式）。
        /// 设置为 0 抛出异常（与 Lua 侧一致）。
        /// </summary>
        /// <exception cref="ArgumentException">value 为 0</exception>
        public static double ImageScale
        {
            get => LuaSTGAPI.api.res_getGlobalImageScale();
            set
            {
                if (LuaSTGAPI.api.res_setGlobalImageScale(value) != 0)
                    throw new ArgumentException("invalid argument for 'ImageScale', scale must not be 0.", nameof(value));
            }
        }

        /// <summary>对应 lstg.SetImageScale（全局形式）</summary>
        public static void SetImageScale(double scale) => ImageScale = scale;

        /// <summary>对应 lstg.GetImageScale（全局形式）</summary>
        public static double GetImageScale() => ImageScale;

        // ========== 加载（作用于当前资源池） ==========

        /// <summary>
        /// 加载纹理（对应 lstg.LoadTexture）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceTexture LoadTexture(string name, string path, bool mipmaps = true)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.res_loadTexture((int)pool, n, p, ToByte(mipmaps));
            if (handle == 0)
                throw new InvalidOperationException($"can't load texture from file '{path}'.");
            return new ResourceTexture(handle, name);
        }

        /// <summary>
        /// 从纹理创建图片精灵（对应 lstg.LoadImage）。
        /// </summary>
        /// <param name="x">纹理上的左边界（像素）</param>
        /// <param name="y">纹理上的上边界（像素）</param>
        /// <param name="w">宽度（像素）</param>
        /// <param name="h">高度（像素）</param>
        /// <param name="a">碰撞半宽（仅 rect 为 true 时使用）</param>
        /// <param name="b">碰撞半高（仅 rect 为 true 时使用）</param>
        /// <param name="rect">是否使用矩形碰撞盒</param>
        /// <exception cref="InvalidOperationException">无激活资源池或创建失败</exception>
        public static ResourceSprite LoadImage(string name, string texname,
            double x, double y, double w, double h, double a = 0.0, double b = 0.0, bool rect = false)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var t = new MarshaledString(texname);
            var handle = LuaSTGAPI.api.res_createSprite((int)pool, n, t, x, y, w, h, a, b, ToByte(rect));
            if (handle == 0)
                throw new InvalidOperationException($"load image failed (name='{name}', tex='{texname}').");
            return new ResourceSprite(handle, name);
        }

        /// <summary>
        /// 复制图片精灵（对应 lstg.CopyImage）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或复制失败</exception>
        public static ResourceSprite CopyImage(string name, string srcName)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var s = new MarshaledString(srcName);
            var handle = LuaSTGAPI.api.res_copySprite((int)pool, n, s);
            if (handle == 0)
                throw new InvalidOperationException($"copy image failed (name='{name}', src='{srcName}').");
            return new ResourceSprite(handle, name);
        }

        /// <summary>
        /// 从纹理创建动画精灵（对应 lstg.LoadAnimation 的纹理形式）。
        /// </summary>
        /// <param name="n">列数</param>
        /// <param name="m">行数</param>
        /// <param name="intv">帧间隔（帧）</param>
        /// <exception cref="InvalidOperationException">无激活资源池或创建失败</exception>
        public static ResourceAnimation LoadAnimation(string name, string texname,
            double x, double y, double w, double h, int n, int m, int intv,
            double a = 0.0, double b = 0.0, bool rect = false)
        {
            var pool = RequireActivePool();
            using var namePtr = new MarshaledString(name);
            using var texPtr = new MarshaledString(texname);
            var handle = LuaSTGAPI.api.res_createAnimationFromTexture(
                (int)pool, namePtr, texPtr, x, y, w, h, n, m, intv, a, b, ToByte(rect));
            if (handle == 0)
                throw new InvalidOperationException($"load animation failed (name='{name}', tex='{texname}').");
            return new ResourceAnimation(handle, name);
        }

        /// <summary>
        /// 由已有精灵序列创建动画（对应 lstg.LoadAnimation 的精灵列表形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池、精灵不存在或创建失败</exception>
        public static ResourceAnimation LoadAnimation(string name, IReadOnlyList<string> spriteNames,
            int intv, double a = 0.0, double b = 0.0, bool rect = false)
        {
            var pool = RequireActivePool();
            var names = ToArray(spriteNames);
            using var namePtr = new MarshaledString(name);
            using var strings = new MarshaledStringArray(names);
            var handle = LuaSTGAPI.api.res_createAnimationFromSprites(
                (int)pool, namePtr, strings.Pointer, strings.Count, intv, a, b, ToByte(rect));
            if (handle == 0)
                throw new InvalidOperationException($"load animation failed (name='{name}').");
            return new ResourceAnimation(handle, name);
        }

        /// <summary>
        /// 由已有精灵序列创建动画（对应 lstg.LoadAnimation 的精灵列表形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池、精灵不存在或创建失败</exception>
        public static ResourceAnimation LoadAnimation(string name, IReadOnlyList<ResourceSprite> sprites,
            int intv, double a = 0.0, double b = 0.0, bool rect = false)
        {
            if (sprites is null)
                throw new ArgumentNullException(nameof(sprites));
            var names = new string[sprites.Count];
            for (var i = 0; i < sprites.Count; i++)
            {
                names[i] = sprites[i]?.Name ?? throw new ArgumentException($"sprites[{i}] 为 null");
            }
            return LoadAnimation(name, names, intv, a, b, rect);
        }

        /// <summary>
        /// 从 psi 定义文件加载粒子特效（对应 lstg.LoadPS 的文件形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceParticle LoadPS(string name, string path, string imgName,
            double a = 0.0, double b = 0.0, bool rect = false)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            using var img = new MarshaledString(imgName);
            var handle = LuaSTGAPI.api.res_loadParticle((int)pool, n, p, img, a, b, ToByte(rect));
            if (handle == 0)
                throw new InvalidOperationException($"load particle failed (name='{name}', file='{path}', img='{imgName}').");
            return new ResourceParticle(handle, name);
        }

        /// <summary>
        /// 按定义加载粒子特效（对应 lstg.LoadPS 的 table 形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceParticle LoadPS(string name, ParticleDefinition define, string imgName,
            double a = 0.0, double b = 0.0, bool rect = false)
        {
            if (define is null)
                throw new ArgumentNullException(nameof(define));
            var pool = RequireActivePool();
            var values = define.Pack();
            using var n = new MarshaledString(name);
            using var img = new MarshaledString(imgName);
            fixed (double* pv = values)
            {
                var handle = LuaSTGAPI.api.res_loadParticleFromInfo(
                    (int)pool, n, pv, (uint)values.Length, img, a, b, ToByte(define.BlendIsAlpha), ToByte(rect));
                if (handle == 0)
                    throw new InvalidOperationException($"load particle failed (name='{name}', define=table, img='{imgName}').");
                return new ResourceParticle(handle, name);
            }
        }

        /// <summary>
        /// 加载音效（对应 lstg.LoadSound）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceSoundEffect LoadSound(string name, string path)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.res_loadSoundEffect((int)pool, n, p);
            if (handle == 0)
                throw new InvalidOperationException($"load sound failed (name={name}, path={path})");
            return new ResourceSoundEffect(handle, name);
        }

        /// <summary>
        /// 加载音乐（对应 lstg.LoadMusic）。循环起点为 max(0, loopEnd - loopDuration)。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceMusic LoadMusic(string name, string path, double loopEnd, double loopDuration,
            bool onceDecode = false)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.res_loadMusic((int)pool, n, p, loopEnd, loopDuration, ToByte(onceDecode));
            if (handle == 0)
                throw new InvalidOperationException($"load music failed (name={name}, path={path}, loop={Math.Max(0.0, loopEnd - loopDuration)}~{loopEnd})");
            return new ResourceMusic(handle, name);
        }

        /// <summary>
        /// 加载 HGE 纹理字体（对应 lstg.LoadFont 的双/三参数形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceSpriteFont LoadFont(string name, string path, bool mipmaps = true)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.res_loadSpriteFont((int)pool, n, p, ToByte(mipmaps));
            if (handle == 0)
                throw new InvalidOperationException($"can't load font from file '{path}'.");
            return new ResourceSpriteFont(handle, name);
        }

        /// <summary>
        /// 加载 fancy2d 纹理字体（对应 lstg.LoadFont 的四/五参数形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceSpriteFont LoadFont(string name, string path, string texPath, bool mipmaps = true)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            using var t = new MarshaledString(texPath);
            var handle = LuaSTGAPI.api.res_loadSpriteFontWithTexture((int)pool, n, p, t, ToByte(mipmaps));
            if (handle == 0)
                throw new InvalidOperationException($"can't load font from file '{path}'.");
            return new ResourceSpriteFont(handle, name);
        }

        /// <summary>
        /// 加载矢量字体（对应 lstg.LoadTTF）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceTrueTypeFont LoadTTF(string name, string path, float width, float height)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.res_loadTTFFont((int)pool, n, p, width, height);
            if (handle == 0)
                throw new InvalidOperationException($"load TTF font failed (name='{name}', file='{path}').");
            return new ResourceTrueTypeFont(handle, name);
        }

        /// <summary>
        /// 加载矢量字体组（对应 lstg.LoadTrueTypeFont）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceTrueTypeFont LoadTrueTypeFont(string name, IReadOnlyList<TrueTypeFontDesc> fonts)
        {
            if (fonts is null)
                throw new ArgumentNullException(nameof(fonts));
            if (fonts.Count == 0)
                throw new ArgumentException("字体组不能为空", nameof(fonts));
            var pool = RequireActivePool();
            var sources = new string[fonts.Count];
            var faces = new uint[fonts.Count];
            var sizes = new float[fonts.Count * 2];
            for (var i = 0; i < fonts.Count; i++)
            {
                var font = fonts[i] ?? throw new ArgumentException($"fonts[{i}] 为 null");
                sources[i] = font.Source;
                faces[i] = font.FontFace;
                sizes[i * 2] = font.Width;
                sizes[i * 2 + 1] = font.Height;
            }
            using var n = new MarshaledString(name);
            using var strings = new MarshaledStringArray(sources);
            fixed (uint* pf = faces)
            fixed (float* ps = sizes)
            {
                var handle = LuaSTGAPI.api.res_loadTrueTypeFont((int)pool, n, strings.Pointer, pf, ps, (uint)fonts.Count);
                if (handle == 0)
                    throw new InvalidOperationException($"load TrueType font failed (name='{name}').");
                return new ResourceTrueTypeFont(handle, name);
            }
        }

        /// <summary>
        /// 加载后期特效着色器（对应 lstg.LoadFX）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceFX LoadFX(string name, string path)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.res_loadFX((int)pool, n, p);
            if (handle == 0)
                throw new InvalidOperationException($"load fx failed (name={name}, path={path})");
            return new ResourceFX(handle, name);
        }

        /// <summary>
        /// 加载模型（对应 lstg.LoadModel）。
        /// </summary>
        /// <exception cref="InvalidOperationException">无激活资源池或加载失败</exception>
        public static ResourceModel LoadModel(string name, string modelPath)
        {
            var pool = RequireActivePool();
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(modelPath);
            var handle = LuaSTGAPI.api.res_loadModel((int)pool, n, p);
            if (handle == 0)
                throw new InvalidOperationException($"load model failed (name='{name}', model='{modelPath}').");
            return new ResourceModel(handle, name);
        }

        /// <summary>
        /// 创建渲染目标（对应 lstg.CreateRenderTarget）。
        /// width/height 传 0（缺省）时使用当前屏幕尺寸。
        /// </summary>
        /// <exception cref="ArgumentException">尺寸参数非法</exception>
        /// <exception cref="InvalidOperationException">无激活资源池或创建失败</exception>
        public static ResourceTexture CreateRenderTarget(string name, int width = 0, int height = 0, bool depthBuffer = true)
        {
            var pool = RequireActivePool();
            if ((width != 0 || height != 0) && (width < 1 || height < 1))
                throw new ArgumentException($"invalid render target size ({width}x{height}).");
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_createRenderTarget((int)pool, n, width, height, ToByte(depthBuffer));
            if (handle == 0)
                throw new InvalidOperationException($"can't create render target with name '{name}'.");
            return new ResourceTexture(handle, name);
        }

        // ========== 纹理（按名字操作） ==========

        /// <summary>
        /// 查询纹理是否为渲染目标（对应 lstg.IsRenderTarget）。
        /// </summary>
        /// <exception cref="ArgumentException">纹理不存在</exception>
        public static bool IsRenderTarget(string name)
        {
            var handle = FindOrThrow(ResourceType.Texture, name, "render target");
            return LuaSTGAPI.api.res_tex_isRenderTarget(handle) != 0;
        }

        /// <summary>
        /// 设置纹理的预乘 alpha 状态（对应 lstg.SetTexturePreMulAlphaState）。
        /// </summary>
        /// <exception cref="ArgumentException">纹理不存在</exception>
        public static void SetTexturePreMulAlphaState(string name, bool enable)
        {
            var handle = FindOrThrow(ResourceType.Texture, name, "texture");
            LuaSTGAPI.api.res_tex_setPreMulAlpha(handle, ToByte(enable));
        }

        /// <summary>
        /// 设置纹理采样器状态（对应 lstg.SetTextureSamplerState）。
        /// </summary>
        /// <exception cref="ArgumentException">纹理不存在或采样器值非法</exception>
        public static void SetTextureSamplerState(string name, TextureSamplerState sampler)
        {
            var handle = FindOrThrow(ResourceType.Texture, name, "texture");
            if (LuaSTGAPI.api.res_tex_setSamplerState(handle, (byte)sampler) != 0)
                throw new ArgumentException($"unknown sampler state '{sampler}'", nameof(sampler));
        }

        /// <summary>
        /// 获取纹理尺寸（对应 lstg.GetTextureSize）。
        /// </summary>
        /// <exception cref="ArgumentException">纹理不存在</exception>
        public static void GetTextureSize(string name, out uint width, out uint height)
        {
            uint w = 0, h = 0;
            using var n = new MarshaledString(name);
            if (LuaSTGAPI.api.res_getTextureSize(n, &w, &h) != 0)
                ThrowNotFound("texture", name);
            width = w;
            height = h;
        }

        // ========== 图片精灵（按名字操作） ==========

        /// <summary>
        /// 设置单个精灵的缩放系数（对应 lstg.SetImageScale 的带名形式）。
        /// </summary>
        /// <exception cref="ArgumentException">精灵不存在</exception>
        public static void SetImageScale(string name, double scale)
        {
            var handle = FindOrThrow(ResourceType.Sprite, name, "image");
            LuaSTGAPI.api.res_sprite_setUnitsPerPixel(handle, scale);
        }

        /// <summary>
        /// 获取单个精灵的缩放系数（对应 lstg.GetImageScale 的带名形式）。
        /// </summary>
        /// <exception cref="ArgumentException">精灵不存在</exception>
        public static double GetImageScale(string name)
        {
            var handle = FindOrThrow(ResourceType.Sprite, name, "image");
            return LuaSTGAPI.api.res_sprite_getUnitsPerPixel(handle);
        }

        /// <summary>
        /// 获取精灵的显示尺寸（对应 lstg.GetImageSize）。
        /// </summary>
        /// <exception cref="ArgumentException">精灵不存在</exception>
        public static void GetImageSize(string name, out double width, out double height)
        {
            var handle = FindOrThrow(ResourceType.Sprite, name, "image");
            double w = 0, h = 0;
            LuaSTGAPI.api.res_sprite_getSize(handle, &w, &h);
            width = w;
            height = h;
        }

        /// <summary>
        /// 设置精灵的混合模式（对应 lstg.SetImageState 的双参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">精灵不存在</exception>
        public static void SetImageState(string name, BlendMode blend)
        {
            var handle = FindOrThrow(ResourceType.Sprite, name, "image");
            LuaSTGAPI.api.res_sprite_setBlendMode(handle, (byte)blend);
        }

        /// <summary>
        /// 设置精灵的混合模式与单色（对应 lstg.SetImageState 的三参数形式）。
        /// </summary>
        /// <param name="color">ARGB 颜色（0xAARRGGBB）</param>
        /// <exception cref="ArgumentException">精灵不存在</exception>
        public static void SetImageState(string name, BlendMode blend, uint color)
            => SetImageState(name, blend, color, color, color, color);

        /// <summary>
        /// 设置精灵的混合模式与四角顶点色（对应 lstg.SetImageState 的六参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">精灵不存在</exception>
        public static void SetImageState(string name, BlendMode blend, uint c1, uint c2, uint c3, uint c4)
        {
            var handle = FindOrThrow(ResourceType.Sprite, name, "image");
            LuaSTGAPI.api.res_sprite_setBlendMode(handle, (byte)blend);
            LuaSTGAPI.api.res_sprite_setColor(handle, c1, c2, c3, c4);
        }

        /// <summary>
        /// 设置精灵的中心点（对应 lstg.SetImageCenter）。
        /// </summary>
        /// <exception cref="ArgumentException">精灵不存在</exception>
        public static void SetImageCenter(string name, double x, double y)
        {
            var handle = FindOrThrow(ResourceType.Sprite, name, "image");
            LuaSTGAPI.api.res_sprite_setCenter(handle, x, y);
        }

        // ========== 动画（按名字操作） ==========

        /// <summary>
        /// 设置动画中所有精灵的缩放系数（对应 lstg.SetAnimationScale）。
        /// </summary>
        /// <exception cref="ArgumentException">动画不存在</exception>
        /// <exception cref="InvalidOperationException">动画未克隆精灵（需逐精灵设置）</exception>
        public static void SetAnimationScale(string name, double scale)
        {
            var handle = FindOrThrow(ResourceType.Animation, name, "animation");
            ThrowIfAnimationError(LuaSTGAPI.api.res_anim_setScale(handle, scale), name, "SetAnimationScale");
        }

        /// <summary>
        /// 获取动画中精灵的缩放系数（对应 lstg.GetAnimationScale）。
        /// </summary>
        /// <exception cref="ArgumentException">动画不存在</exception>
        /// <exception cref="InvalidOperationException">动画未克隆精灵</exception>
        public static double GetAnimationScale(string name)
        {
            var handle = FindOrThrow(ResourceType.Animation, name, "animation");
            double value = 0;
            ThrowIfAnimationError(LuaSTGAPI.api.res_anim_getScale(handle, &value), name, "GetAnimationScale");
            return value;
        }

        /// <summary>
        /// 设置动画的混合模式（对应 lstg.SetAnimationState 的双参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">动画不存在</exception>
        public static void SetAnimationState(string name, BlendMode blend)
        {
            var handle = FindOrThrow(ResourceType.Animation, name, "animation");
            LuaSTGAPI.api.res_anim_setBlendMode(handle, (byte)blend);
        }

        /// <summary>
        /// 设置动画的混合模式与单色（对应 lstg.SetAnimationState 的三参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">动画不存在</exception>
        public static void SetAnimationState(string name, BlendMode blend, uint color)
            => SetAnimationState(name, blend, color, color, color, color);

        /// <summary>
        /// 设置动画的混合模式与四角顶点色（对应 lstg.SetAnimationState 的六参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">动画不存在</exception>
        public static void SetAnimationState(string name, BlendMode blend, uint c1, uint c2, uint c3, uint c4)
        {
            var handle = FindOrThrow(ResourceType.Animation, name, "animation");
            LuaSTGAPI.api.res_anim_setBlendMode(handle, (byte)blend);
            LuaSTGAPI.api.res_anim_setVertexColor(handle, c1, c2, c3, c4);
        }

        /// <summary>
        /// 设置动画中所有精灵的中心点（对应 lstg.SetAnimationCenter）。
        /// </summary>
        /// <exception cref="ArgumentException">动画不存在</exception>
        /// <exception cref="InvalidOperationException">动画未克隆精灵</exception>
        public static void SetAnimationCenter(string name, double x, double y)
        {
            var handle = FindOrThrow(ResourceType.Animation, name, "animation");
            ThrowIfAnimationError(LuaSTGAPI.api.res_anim_setCenter(handle, x, y), name, "SetAnimationCenter");
        }

        // ========== 字体（按名字操作） ==========

        /// <summary>
        /// 设置纹理字体的混合模式与颜色（对应 lstg.SetFontState）。
        /// </summary>
        /// <exception cref="ArgumentException">字体不存在</exception>
        public static void SetFontState(string name, BlendMode blend, uint color)
        {
            var handle = FindOrThrow(ResourceType.SpriteFont, name, "sprite font");
            LuaSTGAPI.api.res_font_setBlendMode(handle, (byte)blend);
            LuaSTGAPI.api.res_font_setBlendColor(handle, color);
        }

        /// <summary>
        /// 预缓存 TTF 字体的字形（对应 lstg.CacheTTFString）。
        /// 字体不存在时引擎仅记录日志（与 Lua 侧行为一致）。
        /// </summary>
        public static void CacheTTFString(string name, string text)
        {
            using var n = new MarshaledString(name);
            using var t = new MarshaledString(text);
            LuaSTGAPI.api.res_cacheTTFString(n, t);
        }

        // ========== 移除 / 查询 / 枚举 ==========

        /// <summary>
        /// 清空指定资源池（对应 lstg.RemoveResource 的单参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">pool 类型非法</exception>
        public static void RemoveResource(ResourcePoolType pool)
        {
            if (LuaSTGAPI.api.res_clearResourcePool((int)pool) != 0)
                throw new ArgumentException($"invalid argument for 'RemoveResource', requires '{ResourcePoolType.Stage}', '{ResourcePoolType.Global}' or '{ResourcePoolType.None}'.", nameof(pool));
        }

        /// <summary>
        /// 从指定资源池移除单个资源（对应 lstg.RemoveResource 的三参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">参数非法</exception>
        public static void RemoveResource(ResourcePoolType pool, ResourceType type, string name)
        {
            using var n = name != null ? new MarshaledString(name) : new MarshaledString(string.Empty);
            if (LuaSTGAPI.api.res_removeResource((int)pool, (int)type, n) != 0)
                throw new ArgumentException($"invalid argument for 'RemoveResource' (pool={pool}, type={type}).");
        }

        /// <summary>
        /// 查询资源所在池（对应 lstg.CheckRes，先查全局池再查关卡池）。
        /// </summary>
        /// <returns>资源所在池；不存在时为 <see cref="ResourcePoolType.None"/></returns>
        public static ResourcePoolType CheckRes(ResourceType type, string name)
        {
            using var n = new MarshaledString(name);
            return (ResourcePoolType)LuaSTGAPI.api.res_checkRes((int)type, n);
        }

        /// <summary>
        /// 枚举指定资源池中的资源名（对应 lstg.EnumRes 的单池形式）。
        /// </summary>
        public static string[] EnumRes(ResourcePoolType pool, ResourceType type)
        {
            var count = LuaSTGAPI.api.res_enumResCount((int)pool, (int)type);
            var result = new string[count];
            for (uint i = 0; i < count; i++)
            {
                result[i] = StringMarshal.FromUtf8(LuaSTGAPI.api.res_enumResNameByIndex((int)pool, (int)type, i));
            }
            return result;
        }

        /// <summary>
        /// 枚举资源名（对应 lstg.EnumRes，Lua 侧返回全局池与关卡池两个表，
        /// 此处按“全局池在前、关卡池在后”合并为一个数组）。
        /// </summary>
        public static string[] EnumRes(ResourceType type)
        {
            var globalNames = EnumRes(ResourcePoolType.Global, type);
            var stageNames = EnumRes(ResourcePoolType.Stage, type);
            if (stageNames.Length == 0)
                return globalNames;
            if (globalNames.Length == 0)
                return stageNames;
            var result = new string[globalNames.Length + stageNames.Length];
            globalNames.CopyTo(result, 0);
            stageNames.CopyTo(result, globalNames.Length);
            return result;
        }

        // ========== 按名字查找（包装类工厂） ==========

        /// <summary>按名字查找纹理（全局+关卡池），不存在返回 null</summary>
        public static ResourceTexture? FindTexture(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findTexture(n);
            return handle != 0 ? new ResourceTexture(handle, name) : null;
        }

        /// <summary>按名字查找精灵（关卡池优先），不存在返回 null</summary>
        public static ResourceSprite? FindSprite(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findSprite(n);
            return handle != 0 ? new ResourceSprite(handle, name) : null;
        }

        /// <summary>按名字查找动画（关卡池优先），不存在返回 null</summary>
        public static ResourceAnimation? FindAnimation(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findAnimation(n);
            return handle != 0 ? new ResourceAnimation(handle, name) : null;
        }

        /// <summary>按名字查找音乐（关卡池优先），不存在返回 null</summary>
        public static ResourceMusic? FindMusic(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findMusic(n);
            return handle != 0 ? new ResourceMusic(handle, name) : null;
        }

        /// <summary>按名字查找音效（关卡池优先），不存在返回 null</summary>
        public static ResourceSoundEffect? FindSoundEffect(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findSoundEffect(n);
            return handle != 0 ? new ResourceSoundEffect(handle, name) : null;
        }

        /// <summary>按名字查找粒子（关卡池优先），不存在返回 null</summary>
        public static ResourceParticle? FindParticle(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findParticle(n);
            return handle != 0 ? new ResourceParticle(handle, name) : null;
        }

        /// <summary>按名字查找纹理字体（关卡池优先），不存在返回 null</summary>
        public static ResourceSpriteFont? FindSpriteFont(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findSpriteFont(n);
            return handle != 0 ? new ResourceSpriteFont(handle, name) : null;
        }

        /// <summary>按名字查找矢量字体（关卡池优先），不存在返回 null</summary>
        public static ResourceTrueTypeFont? FindTrueTypeFont(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findTrueTypeFont(n);
            return handle != 0 ? new ResourceTrueTypeFont(handle, name) : null;
        }

        /// <summary>按名字查找后期特效（关卡池优先），不存在返回 null</summary>
        public static ResourceFX? FindFX(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findFX(n);
            return handle != 0 ? new ResourceFX(handle, name) : null;
        }

        /// <summary>按名字查找模型（关卡池优先），不存在返回 null</summary>
        public static ResourceModel? FindModel(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_findModel(n);
            return handle != 0 ? new ResourceModel(handle, name) : null;
        }

        // ========== 内部辅助 ==========

        /// <summary>当前资源池；为 None 时抛出异常（对应 Lua 侧 "can't load resource at this time."）</summary>
        private static ResourcePoolType RequireActivePool()
        {
            var pool = (ResourcePoolType)LuaSTGAPI.api.res_getPoolStatus();
            if (pool != ResourcePoolType.Global && pool != ResourcePoolType.Stage)
                throw new InvalidOperationException("can't load resource at this time.");
            return pool;
        }

        private static nuint FindOrThrow(ResourceType type, string name, string kind)
        {
            var handle = FindHandle(type, name);
            if (handle == 0)
                ThrowNotFound(kind, name);
            return handle;
        }

        private static nuint FindHandle(ResourceType type, string name)
        {
            using var n = new MarshaledString(name);
            return type switch
            {
                ResourceType.Texture => LuaSTGAPI.api.res_findTexture(n),
                ResourceType.Sprite => LuaSTGAPI.api.res_findSprite(n),
                ResourceType.Animation => LuaSTGAPI.api.res_findAnimation(n),
                ResourceType.Music => LuaSTGAPI.api.res_findMusic(n),
                ResourceType.SoundEffect => LuaSTGAPI.api.res_findSoundEffect(n),
                ResourceType.Particle => LuaSTGAPI.api.res_findParticle(n),
                ResourceType.SpriteFont => LuaSTGAPI.api.res_findSpriteFont(n),
                ResourceType.TrueTypeFont => LuaSTGAPI.api.res_findTrueTypeFont(n),
                ResourceType.FX => LuaSTGAPI.api.res_findFX(n),
                ResourceType.Model => LuaSTGAPI.api.res_findModel(n),
                _ => 0,
            };
        }

        internal static nuint FindHandleFor(ResourceType type, string name) => FindHandle(type, name);

        /// <summary>动画批量操作错误码转异常（0 = 成功；2 = 未克隆精灵）</summary>
        internal static void ThrowIfAnimationError(int errorCode, string name, string operation)
        {
            switch (errorCode)
            {
                case 0:
                    return;
                case 2:
                    throw new InvalidOperationException($"{operation} on animation '{name}' is invalid, please set each sprite separately.");
                default:
                    throw new InvalidOperationException($"animation operation failed ({errorCode}) on '{name}'.");
            }
        }

        private static void ThrowNotFound(string kind, string name)
            => throw new ArgumentException($"{kind} '{name}' not found.", nameof(name));

        private static byte ToByte(bool value) => value ? (byte)1 : (byte)0;

        private static string[] ToArray(IReadOnlyList<string> items)
        {
            if (items is null)
                throw new ArgumentNullException(nameof(items));
            var array = new string[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                array[i] = items[i] ?? throw new ArgumentException($"items[{i}] 为 null");
            }
            return array;
        }
    }
}
