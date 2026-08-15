using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 纹理对象（对应 Lua 侧 lstg.Texture2D）。
    /// 通过 <see cref="CreateFromFile"/> 创建，或经
    /// <see cref="RenderTarget.GetTexture"/>/<see cref="VideoDecoder.GetTexture"/>/<see cref="Sprite.GetTexture"/>
    /// 获取（包装结构持有引擎引用，<see cref="Dispose"/> 时释放）。
    /// </summary>
    public sealed unsafe class Texture2D : ModernGraphicsObject
    {
        internal Texture2D(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 从文件创建纹理（对应 lstg.Texture2D.createFromFile）。
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="mipmapLevels">mipmap 级数，1 表示不生成 mipmap（默认，与 Lua 侧一致）</param>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static Texture2D CreateFromFile(string path, uint mipmapLevels = 1u)
        {
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.mg_textureCreateFromFile(p, mipmapLevels);
            return handle != 0
                ? new Texture2D((nint)handle)
                : throw new InvalidOperationException($"从文件 '{path}' 创建纹理失败，详见引擎日志");
        }

        /// <summary>纹理宽度（对应 lstg.Texture2D:getWidth）</summary>
        public uint Width
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_textureGetWidth((nuint)Handle); }
        }

        /// <summary>纹理高度（对应 lstg.Texture2D:getHeight）</summary>
        public uint Height
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_textureGetHeight((nuint)Handle); }
        }

        /// <summary>
        /// 设置默认采样器（对应 lstg.Texture2D:setDefaultSampler）。
        /// Lua 侧的字符串参数（"point+wrap" 等）改为 <see cref="Renderer.SamplerState"/> 枚举。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">采样器值非法</exception>
        public void SetDefaultSampler(Renderer.SamplerState sampler)
        {
            ThrowIfDisposed();
            if (LuaSTGAPI.api.mg_textureSetDefaultSampler((nuint)Handle, (byte)sampler) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampler), sampler, "未知的采样器状态");
            }
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_textureRelease((nuint)Handle);
        }
    }
}
