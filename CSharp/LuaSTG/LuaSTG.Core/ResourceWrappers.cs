using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LuaSTG.Core
{
    /// <summary>
    /// 引擎资源对象的 C# 包装基类（对应现代类型 API 的
    /// LuaSTG.Sub.ResourceTexture / ResourceSprite / ResourceSpriteSequence 等 userdata）。
    /// </summary>
    /// <remarks>
    /// 与 GameObject 包装的差异：引擎资源对象以“名字”标识、归引擎资源池所有，
    /// 因此包装类没有也无从提供匿名分配的无参构造——只能通过
    /// <see cref="ResourceManager"/> 的带名字工厂方法（LoadTexture/CreateRenderTarget 等）
    /// 或按名字查找（FindSprite 等）获得。
    /// <see cref="Destroy"/> 将资源从资源池移除（等价 lstg.RemoveResource），
    /// 销毁后访问引擎数据抛出 <see cref="ObjectDisposedException"/>。
    /// 句柄是借用指针：若同一资源被其他途径（如按名 RemoveResource）移出资源池，
    /// 本包装无法感知，继续访问属未定义行为。
    /// </remarks>
    public abstract unsafe class ResourceBase
    {
        internal nuint _native;
        private protected readonly string _name;

        private protected ResourceBase(nuint native, string name)
        {
            _native = native;
            _name = name ?? string.Empty;
        }

        /// <summary>资源名（引擎侧注册时使用的名字）</summary>
        public string Name => _name;

        /// <summary>资源类型</summary>
        public abstract ResourceType Type { get; }

        /// <summary>包装是否仍持有引擎句柄</summary>
        public bool IsValid => _native != 0;

        /// <summary>
        /// 从资源池移除本资源（等价 lstg.RemoveResource 指定池与名字），
        /// 之后本包装失效。
        /// </summary>
        /// <exception cref="ObjectDisposedException">已被销毁</exception>
        public void Destroy()
        {
            ThrowIfDestroyed();
            var pool = ResourceManager.CheckRes(Type, _name);
            if (pool != ResourcePoolType.None)
                ResourceManager.RemoveResource(pool, Type, _name);
            _native = 0;
        }

        private protected void ThrowIfDestroyed()
        {
            if (_native == 0)
                throw new ObjectDisposedException(GetType().Name, $"资源 '{_name}' 已被销毁");
        }

        /// <summary>按引擎句柄判断两个包装是否指向同一资源（对应 Lua 侧 __eq）</summary>
        public override bool Equals(object? obj)
            => obj is ResourceBase other && other.GetType() == GetType() && other._native == _native;

        public override int GetHashCode() => (int)(_native ^ (_native >> 32)) ^ GetType().GetHashCode();

        public static bool operator ==(ResourceBase? left, ResourceBase? right)
            => ReferenceEquals(left, right) || (left is not null && right is not null && left.Equals(right));

        public static bool operator !=(ResourceBase? left, ResourceBase? right) => !(left == right);

        public override string ToString() => $"{Type} '{_name}'";
    }

    /// <summary>
    /// 纹理资源（对应 LuaSTG.Sub.ResourceTexture；渲染目标也以此类型表示）。
    /// </summary>
    public sealed unsafe class ResourceTexture : ResourceBase
    {
        internal ResourceTexture(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.Texture;

        /// <summary>
        /// 获取纹理尺寸（对应 ResourceTexture.getWidth/getHeight）。
        /// </summary>
        /// <exception cref="ObjectDisposedException">已被销毁</exception>
        public void GetSize(out uint width, out uint height)
        {
            ThrowIfDestroyed();
            uint w = 0, h = 0;
            LuaSTGAPI.api.res_tex_getSize(_native, &w, &h);
            width = w;
            height = h;
        }

        /// <summary>纹理宽度（像素）</summary>
        public uint Width
        {
            get { GetSize(out var w, out _); return w; }
        }

        /// <summary>纹理高度（像素）</summary>
        public uint Height
        {
            get { GetSize(out _, out var h); return h; }
        }

        /// <summary>是否为渲染目标</summary>
        public bool IsRenderTarget
        {
            get { ThrowIfDestroyed(); return LuaSTGAPI.api.res_tex_isRenderTarget(_native) != 0; }
        }

        /// <summary>设置预乘 alpha 状态（对应 lstg.SetTexturePreMulAlphaState）</summary>
        public void SetPreMulAlphaState(bool enable)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.res_tex_setPreMulAlpha(_native, enable ? (byte)1 : (byte)0);
        }

        /// <summary>设置采样器状态（对应 lstg.SetTextureSamplerState）</summary>
        /// <exception cref="ArgumentException">采样器值非法</exception>
        public void SetSamplerState(TextureSamplerState sampler)
        {
            ThrowIfDestroyed();
            if (LuaSTGAPI.api.res_tex_setSamplerState(_native, (byte)sampler) != 0)
                throw new ArgumentException($"unknown sampler state '{sampler}'", nameof(sampler));
        }
    }

    /// <summary>
    /// 图片精灵资源（对应 LuaSTG.Sub.ResourceSprite，兼容 API 中称为 image）。
    /// 引擎数据（混合模式、颜色、中心等）每次调用都直通引擎，不在 C# 侧缓存。
    /// </summary>
    public sealed unsafe class ResourceSprite : ResourceBase
    {
        internal ResourceSprite(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.Sprite;

        /// <summary>设置精灵中心点（对应 ResourceSprite.setCenter / lstg.SetImageCenter）</summary>
        public void SetCenter(double x, double y)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.res_sprite_setCenter(_native, x, y);
        }

        /// <summary>
        /// 缩放系数（每像素单位数，对应 ResourceSprite.setUnitsPerPixel /
        /// lstg.SetImageScale/GetImageScale 的带名形式）。
        /// </summary>
        public double UnitsPerPixel
        {
            get { ThrowIfDestroyed(); return LuaSTGAPI.api.res_sprite_getUnitsPerPixel(_native); }
            set { ThrowIfDestroyed(); LuaSTGAPI.api.res_sprite_setUnitsPerPixel(_native, value); }
        }

        /// <summary>获取精灵显示尺寸（对应 lstg.GetImageSize）</summary>
        public void GetSize(out double width, out double height)
        {
            ThrowIfDestroyed();
            double w = 0, h = 0;
            LuaSTGAPI.api.res_sprite_getSize(_native, &w, &h);
            width = w;
            height = h;
        }

        /// <summary>设置混合模式（对应 lstg.SetImageState 的双参数形式）</summary>
        public void SetState(BlendMode blend)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.res_sprite_setBlendMode(_native, (byte)blend);
        }

        /// <summary>设置混合模式与单色（对应 lstg.SetImageState 的三参数形式）</summary>
        /// <param name="color">ARGB 颜色（0xAARRGGBB）</param>
        public void SetState(BlendMode blend, uint color)
            => SetState(blend, color, color, color, color);

        /// <summary>设置混合模式与四角顶点色（对应 lstg.SetImageState 的六参数形式）</summary>
        public void SetState(BlendMode blend, uint c1, uint c2, uint c3, uint c4)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.res_sprite_setBlendMode(_native, (byte)blend);
            LuaSTGAPI.api.res_sprite_setColor(_native, c1, c2, c3, c4);
        }
    }

    /// <summary>
    /// 动画（精灵序列）资源（对应 LuaSTG.Sub.ResourceSpriteSequence，兼容 API 中称为 animation）。
    /// </summary>
    public sealed unsafe class ResourceAnimation : ResourceBase
    {
        internal ResourceAnimation(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.Animation;

        /// <summary>动画包含的精灵数量</summary>
        public uint Count
        {
            get { ThrowIfDestroyed(); return LuaSTGAPI.api.res_anim_getCount(_native); }
        }

        /// <summary>动画是否克隆了精灵（决定批量设置接口是否可用）</summary>
        public bool IsSpriteCloned
        {
            get { ThrowIfDestroyed(); return LuaSTGAPI.api.res_anim_isSpriteCloned(_native) != 0; }
        }

        /// <summary>
        /// 按索引获取动画中的精灵（借用包装；精灵归动画/资源池所有）。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">索引越界</exception>
        public ResourceSprite GetSprite(uint index)
        {
            ThrowIfDestroyed();
            var handle = LuaSTGAPI.api.res_anim_getSprite(_native, index);
            if (handle == 0)
                throw new ArgumentOutOfRangeException(nameof(index), $"动画 '{_name}' 没有索引为 {index} 的精灵");
            var name = StringMarshal.FromUtf8(LuaSTGAPI.api.res_getResourceName(handle));
            return new ResourceSprite(handle, name);
        }

        /// <summary>设置所有精灵的缩放系数（对应 lstg.SetAnimationScale）</summary>
        /// <exception cref="InvalidOperationException">动画未克隆精灵</exception>
        public void SetScale(double scale)
        {
            ThrowIfDestroyed();
            ResourceManager.ThrowIfAnimationError(LuaSTGAPI.api.res_anim_setScale(_native, scale), _name, "SetAnimationScale");
        }

        /// <summary>获取精灵的缩放系数（对应 lstg.GetAnimationScale）</summary>
        /// <exception cref="InvalidOperationException">动画未克隆精灵</exception>
        public double GetScale()
        {
            ThrowIfDestroyed();
            double value = 0;
            ResourceManager.ThrowIfAnimationError(LuaSTGAPI.api.res_anim_getScale(_native, &value), _name, "GetAnimationScale");
            return value;
        }

        /// <summary>设置混合模式（对应 lstg.SetAnimationState 的双参数形式）</summary>
        public void SetState(BlendMode blend)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.res_anim_setBlendMode(_native, (byte)blend);
        }

        /// <summary>设置混合模式与单色（对应 lstg.SetAnimationState 的三参数形式）</summary>
        public void SetState(BlendMode blend, uint color)
            => SetState(blend, color, color, color, color);

        /// <summary>设置混合模式与四角顶点色（对应 lstg.SetAnimationState 的六参数形式）</summary>
        public void SetState(BlendMode blend, uint c1, uint c2, uint c3, uint c4)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.res_anim_setBlendMode(_native, (byte)blend);
            LuaSTGAPI.api.res_anim_setVertexColor(_native, c1, c2, c3, c4);
        }

        /// <summary>设置所有精灵的中心点（对应 lstg.SetAnimationCenter）</summary>
        /// <exception cref="InvalidOperationException">动画未克隆精灵</exception>
        public void SetCenter(double x, double y)
        {
            ThrowIfDestroyed();
            ResourceManager.ThrowIfAnimationError(LuaSTGAPI.api.res_anim_setCenter(_native, x, y), _name, "SetAnimationCenter");
        }
    }

    /// <summary>
    /// 音效资源（对应 lstg.LoadSound 加载的资源）。
    /// 播放控制转发到 <see cref="Audio"/>（按名字操作，与 Lua 侧共用底层实现）。
    /// </summary>
    public sealed unsafe class ResourceSoundEffect : ResourceBase
    {
        internal ResourceSoundEffect(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.SoundEffect;

        /// <summary>播放音效（对应 lstg.PlaySound），音量钳制到 [0,1]，声像钳制到 [-1,1]</summary>
        public void Play(float volume = 1.0f, float pan = 0.0f)
        {
            ThrowIfDestroyed();
            Audio.PlaySound(_name, volume, pan);
        }

        /// <summary>停止音效（对应 lstg.StopSound）</summary>
        public void Stop() { ThrowIfDestroyed(); Audio.StopSound(_name); }

        /// <summary>暂停音效（对应 lstg.PauseSound）</summary>
        public void Pause() { ThrowIfDestroyed(); Audio.PauseSound(_name); }

        /// <summary>恢复音效（对应 lstg.ResumeSound）</summary>
        public void Resume() { ThrowIfDestroyed(); Audio.ResumeSound(_name); }

        /// <summary>播放状态（对应 lstg.GetSoundState）</summary>
        public AudioState State
        {
            get { ThrowIfDestroyed(); return Audio.GetSoundState(_name); }
        }

        /// <summary>播放速度（对应 lstg.SetSESpeed/GetSESpeed）</summary>
        public float Speed
        {
            get { ThrowIfDestroyed(); return Audio.GetSESpeed(_name); }
            set { ThrowIfDestroyed(); Audio.SetSESpeed(_name, value); }
        }
    }

    /// <summary>
    /// 音乐资源（对应 lstg.LoadMusic 加载的资源）。
    /// 播放控制转发到 <see cref="Audio"/>（按名字操作，与 Lua 侧共用底层实现）。
    /// </summary>
    public sealed unsafe class ResourceMusic : ResourceBase
    {
        internal ResourceMusic(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.Music;

        /// <summary>播放音乐（对应 lstg.PlayMusic），音量钳制到 [0,1]</summary>
        public void Play(float volume = 1.0f, double position = 0.0)
        {
            ThrowIfDestroyed();
            Audio.PlayMusic(_name, volume, position);
        }

        /// <summary>停止音乐（对应 lstg.StopMusic）</summary>
        public void Stop() { ThrowIfDestroyed(); Audio.StopMusic(_name); }

        /// <summary>暂停音乐（对应 lstg.PauseMusic）</summary>
        public void Pause() { ThrowIfDestroyed(); Audio.PauseMusic(_name); }

        /// <summary>恢复音乐（对应 lstg.ResumeMusic）</summary>
        public void Resume() { ThrowIfDestroyed(); Audio.ResumeMusic(_name); }

        /// <summary>播放状态（对应 lstg.GetMusicState）</summary>
        public AudioState State
        {
            get { ThrowIfDestroyed(); return Audio.GetMusicState(_name); }
        }

        /// <summary>音量（对应 lstg.SetBGMVolume/GetBGMVolume 的带名形式）</summary>
        public float Volume
        {
            get { ThrowIfDestroyed(); return Audio.GetBGMVolume(_name); }
            set { ThrowIfDestroyed(); Audio.SetBGMVolume(_name, value); }
        }

        /// <summary>播放速度（对应 lstg.SetBGMSpeed/GetBGMSpeed）</summary>
        public float Speed
        {
            get { ThrowIfDestroyed(); return Audio.GetBGMSpeed(_name); }
            set { ThrowIfDestroyed(); Audio.SetBGMSpeed(_name, value); }
        }

        /// <summary>是否循环（对应 lstg.SetBGMLoop）</summary>
        public bool Loop
        {
            set { ThrowIfDestroyed(); Audio.SetBGMLoop(_name, value); }
        }

        /// <summary>设置循环范围（对应 lstg.SetMusicLoopRange），传 null 禁用循环</summary>
        public void SetLoopRange(MusicLoopRange? range = null)
        {
            ThrowIfDestroyed();
            Audio.SetMusicLoopRange(_name, range);
        }
    }

    /// <summary>
    /// 纹理字体资源（HGE/fancy2d 字体，对应 lstg.LoadFont 加载的资源）。
    /// </summary>
    public sealed unsafe class ResourceSpriteFont : ResourceBase
    {
        internal ResourceSpriteFont(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.SpriteFont;

        /// <summary>设置混合模式与颜色（对应 lstg.SetFontState）</summary>
        /// <param name="color">ARGB 颜色（0xAARRGGBB）</param>
        public void SetState(BlendMode blend, uint color)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.res_font_setBlendMode(_native, (byte)blend);
            LuaSTGAPI.api.res_font_setBlendColor(_native, color);
        }
    }

    /// <summary>
    /// 矢量字体资源（对应 lstg.LoadTTF/LoadTrueTypeFont 加载的资源）。
    /// </summary>
    public sealed unsafe class ResourceTrueTypeFont : ResourceBase
    {
        internal ResourceTrueTypeFont(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.TrueTypeFont;

        /// <summary>预缓存字符串的字形（对应 lstg.CacheTTFString）</summary>
        public void CacheString(string text)
        {
            ThrowIfDestroyed();
            ResourceManager.CacheTTFString(_name, text);
        }
    }

    /// <summary>
    /// 粒子特效定义资源（对应 lstg.LoadPS 加载的资源）。
    /// 粒子实例的发射/更新/渲染由粒子系统相关模块提供。
    /// </summary>
    public sealed unsafe class ResourceParticle : ResourceBase
    {
        internal ResourceParticle(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.Particle;
    }

    /// <summary>
    /// 后期特效着色器资源（对应 lstg.LoadFX 加载的资源）。
    /// 着色器参数与绘制由渲染模块的 PostEffectShader API 提供。
    /// </summary>
    public sealed unsafe class ResourceFX : ResourceBase
    {
        internal ResourceFX(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.FX;
    }

    /// <summary>
    /// 模型资源（对应 lstg.LoadModel 加载的资源）。
    /// 模型绘制由渲染模块的 RenderModel API 提供。
    /// </summary>
    public sealed unsafe class ResourceModel : ResourceBase
    {
        internal ResourceModel(nuint native, string name) : base(native, name) { }

        public override ResourceType Type => ResourceType.Model;
    }

    /// <summary>
    /// 资源集合（对应 LuaSTG.Sub.ResourceCollection，包装引擎的某个资源池）。
    /// 通过 <see cref="ResourceManager.GetResourceCollection"/> 获得；
    /// 所有操作显式作用于该池，不受“当前资源池”状态影响。
    /// </summary>
    public sealed unsafe class ResourceCollection
    {
        /// <summary>目标资源池</summary>
        public ResourcePoolType PoolType { get; }

        internal ResourceCollection(ResourcePoolType pool)
        {
            PoolType = pool;
        }

        /// <summary>
        /// 从文件创建纹理（对应 ResourceCollection.createTextureFromFile）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败</exception>
        public ResourceTexture CreateTextureFromFile(string name, string path, bool mipmap = true)
        {
            using var n = new MarshaledString(name);
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.res_loadTexture((int)PoolType, n, p, mipmap ? (byte)1 : (byte)0);
            if (handle == 0)
                throw new InvalidOperationException($"can't create texture '{name}' from file '{path}'.");
            return new ResourceTexture(handle, name);
        }

        /// <summary>
        /// 创建图片精灵（对应 ResourceCollection.createSprite）。
        /// width/height 缺省时使用纹理尺寸，x/y/a/b 缺省为 0。
        /// </summary>
        /// <exception cref="ArgumentException">纹理不存在</exception>
        /// <exception cref="InvalidOperationException">创建失败</exception>
        public ResourceSprite CreateSprite(string name, string textureName,
            double x = 0.0, double y = 0.0, double? width = null, double? height = null,
            double a = 0.0, double b = 0.0, bool rect = false)
        {
            double w, h;
            if (width.HasValue && height.HasValue)
            {
                w = width.GetValueOrDefault();
                h = height.GetValueOrDefault();
            }
            else
            {
                GetTextureSize(textureName, out var tw, out var th);
                w = width ?? tw;
                h = height ?? th;
            }
            using var n = new MarshaledString(name);
            using var t = new MarshaledString(textureName);
            var handle = LuaSTGAPI.api.res_createSprite((int)PoolType, n, t, x, y, w, h, a, b, rect ? (byte)1 : (byte)0);
            if (handle == 0)
                throw new InvalidOperationException($"load image failed (name='{name}', tex='{textureName}').");
            return new ResourceSprite(handle, name);
        }

        /// <summary>
        /// 创建图片精灵（纹理由 <see cref="ResourceTexture"/> 包装指定）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败</exception>
        public ResourceSprite CreateSprite(string name, ResourceTexture texture,
            double x = 0.0, double y = 0.0, double? width = null, double? height = null,
            double a = 0.0, double b = 0.0, bool rect = false)
        {
            if (texture is null)
                throw new ArgumentNullException(nameof(texture));
            return CreateSprite(name, texture.Name, x, y, width, height, a, b, rect);
        }

        /// <summary>
        /// 从纹理创建精灵序列（对应 ResourceCollection.createSpriteSequence 的纹理形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败</exception>
        public ResourceAnimation CreateSpriteSequence(string name, string textureName,
            double x, double y, double width, double height, int columns, int rows, int interval,
            double a = 0.0, double b = 0.0, bool rect = false)
        {
            using var n = new MarshaledString(name);
            using var t = new MarshaledString(textureName);
            var handle = LuaSTGAPI.api.res_createAnimationFromTexture(
                (int)PoolType, n, t, x, y, width, height, columns, rows, interval, a, b, rect ? (byte)1 : (byte)0);
            if (handle == 0)
                throw new InvalidOperationException($"load animation failed (name='{name}', tex='{textureName}').");
            return new ResourceAnimation(handle, name);
        }

        /// <summary>
        /// 从纹理创建精灵序列（纹理由 <see cref="ResourceTexture"/> 包装指定）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败</exception>
        public ResourceAnimation CreateSpriteSequence(string name, ResourceTexture texture,
            double x, double y, double width, double height, int columns, int rows, int interval,
            double a = 0.0, double b = 0.0, bool rect = false)
        {
            if (texture is null)
                throw new ArgumentNullException(nameof(texture));
            return CreateSpriteSequence(name, texture.Name, x, y, width, height, columns, rows, interval, a, b, rect);
        }

        /// <summary>
        /// 由已有精灵序列创建精灵序列（对应 ResourceCollection.createSpriteSequence 的精灵列表形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败</exception>
        public ResourceAnimation CreateSpriteSequence(string name, IReadOnlyList<ResourceSprite> sprites,
            int interval, double a = 0.0, double b = 0.0, bool rect = false)
        {
            if (sprites is null)
                throw new ArgumentNullException(nameof(sprites));
            var names = new string[sprites.Count];
            for (var i = 0; i < sprites.Count; i++)
            {
                names[i] = sprites[i]?.Name ?? throw new ArgumentException($"sprites[{i}] 为 null");
            }
            using var n = new MarshaledString(name);
            using var strings = new MarshaledStringArray(names);
            var handle = LuaSTGAPI.api.res_createAnimationFromSprites(
                (int)PoolType, n, strings.Pointer, strings.Count, interval, a, b, rect ? (byte)1 : (byte)0);
            if (handle == 0)
                throw new InvalidOperationException($"load animation failed (name='{name}').");
            return new ResourceAnimation(handle, name);
        }

        /// <summary>移除纹理（对应 ResourceCollection.removeTexture），不存在时为空操作</summary>
        public void RemoveTexture(string name) => RemoveResource(ResourceType.Texture, name);

        /// <summary>移除纹理（按包装的资源名）</summary>
        public void RemoveTexture(ResourceTexture texture)
            => RemoveResource(ResourceType.Texture, RequireName(texture));

        /// <summary>移除精灵（对应 ResourceCollection.removeSprite），不存在时为空操作</summary>
        public void RemoveSprite(string name) => RemoveResource(ResourceType.Sprite, name);

        /// <summary>移除精灵（按包装的资源名）</summary>
        public void RemoveSprite(ResourceSprite sprite)
            => RemoveResource(ResourceType.Sprite, RequireName(sprite));

        /// <summary>移除精灵序列（对应 ResourceCollection.removeSpriteSequence），不存在时为空操作</summary>
        public void RemoveSpriteSequence(string name) => RemoveResource(ResourceType.Animation, name);

        /// <summary>移除精灵序列（按包装的资源名）</summary>
        public void RemoveSpriteSequence(ResourceAnimation sequence)
            => RemoveResource(ResourceType.Animation, RequireName(sequence));

        /// <summary>
        /// 获取纹理（对应 ResourceCollection.getTexture）。
        /// </summary>
        /// <exception cref="InvalidOperationException">纹理不存在</exception>
        public ResourceTexture GetTexture(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_poolGetTexture((int)PoolType, n);
            if (handle == 0)
                throw new InvalidOperationException($"can't find texture '{name}'.");
            return new ResourceTexture(handle, name);
        }

        /// <summary>
        /// 获取精灵（对应 ResourceCollection.getSprite）。
        /// </summary>
        /// <exception cref="InvalidOperationException">精灵不存在</exception>
        public ResourceSprite GetSprite(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_poolGetSprite((int)PoolType, n);
            if (handle == 0)
                throw new InvalidOperationException($"can't find sprite '{name}'.");
            return new ResourceSprite(handle, name);
        }

        /// <summary>
        /// 获取精灵序列（对应 ResourceCollection.getSpriteSequence）。
        /// </summary>
        /// <exception cref="InvalidOperationException">精灵序列不存在</exception>
        public ResourceAnimation GetSpriteSequence(string name)
        {
            using var n = new MarshaledString(name);
            var handle = LuaSTGAPI.api.res_poolGetAnimation((int)PoolType, n);
            if (handle == 0)
                throw new InvalidOperationException($"can't find animation '{name}'.");
            return new ResourceAnimation(handle, name);
        }

        /// <summary>纹理是否存在（对应 ResourceCollection.isTextureExist）</summary>
        public bool IsTextureExist(string name) => IsExist(ResourceType.Texture, name);

        /// <summary>精灵是否存在（对应 ResourceCollection.isSpriteExist）</summary>
        public bool IsSpriteExist(string name) => IsExist(ResourceType.Sprite, name);

        /// <summary>精灵序列是否存在（对应 ResourceCollection.isSpriteSequenceExist）</summary>
        public bool IsSpriteSequenceExist(string name) => IsExist(ResourceType.Animation, name);

        /// <summary>同一资源池的集合视为相等（对应 Lua 侧 __eq 比较资源池指针）</summary>
        public override bool Equals(object? obj) => obj is ResourceCollection other && other.PoolType == PoolType;

        public override int GetHashCode() => PoolType.GetHashCode();

        public override string ToString() => $"ResourceCollection({PoolType})";

        // ----- 内部辅助 -----

        private void GetTextureSize(string textureName, out uint width, out uint height)
        {
            using var t = new MarshaledString(textureName);
            var handle = LuaSTGAPI.api.res_poolGetTexture((int)PoolType, t);
            if (handle == 0)
                throw new ArgumentException($"can't find texture '{textureName}'.", nameof(textureName));
            uint w = 0, h = 0;
            LuaSTGAPI.api.res_tex_getSize(handle, &w, &h);
            width = w;
            height = h;
        }

        private void RemoveResource(ResourceType type, string name)
        {
            using var n = new MarshaledString(name);
            // 与 Lua 现代绑定一致：不存在时为空操作
            if (LuaSTGAPI.api.res_poolCheckResourceExists((int)PoolType, (int)type, n) != 0)
                LuaSTGAPI.api.res_removeResource((int)PoolType, (int)type, n);
        }

        private bool IsExist(ResourceType type, string name)
        {
            using var n = new MarshaledString(name);
            return LuaSTGAPI.api.res_poolCheckResourceExists((int)PoolType, (int)type, n) != 0;
        }

        private static string RequireName(ResourceBase resource)
            => (resource ?? throw new ArgumentNullException(nameof(resource))).Name;
    }

    /// <summary>
    /// 多个 C# 字符串到 UTF-8 指针数组（byte**）的临时封送，
    /// 供接收字符串数组的引擎 API 使用。
    /// </summary>
    internal sealed unsafe class MarshaledStringArray : IDisposable
    {
        private readonly MarshaledString[] _items;
        private readonly byte** _native;

        public MarshaledStringArray(string[] items)
        {
            _items = new MarshaledString[items.Length];
            _native = (byte**)NativeMemory.Alloc((nuint)items.Length * (nuint)sizeof(byte*));
            for (var i = 0; i < items.Length; i++)
            {
                _items[i] = new MarshaledString(items[i]);
                _native[i] = _items[i];
            }
        }

        public uint Count => (uint)_items.Length;

        public byte** Pointer => _native;

        public void Dispose()
        {
            foreach (var item in _items)
            {
                item.Dispose();
            }
            NativeMemory.Free(_native);
        }
    }
}
