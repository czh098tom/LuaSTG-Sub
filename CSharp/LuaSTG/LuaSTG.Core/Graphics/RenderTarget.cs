using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 渲染目标（对应 Lua 侧 lstg.RenderTarget）。
    /// 通过 <see cref="Create"/> 工厂创建；<see cref="Dispose"/> 销毁。
    /// </summary>
    public sealed unsafe class RenderTarget : ModernGraphicsObject
    {
        internal RenderTarget(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建渲染目标（对应 lstg.RenderTarget.create）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static RenderTarget Create(uint width, uint height)
        {
            var handle = LuaSTGAPI.api.mg_renderTargetCreate(width, height);
            return handle != 0
                ? new RenderTarget((nint)handle)
                : throw new InvalidOperationException($"创建 RenderTarget（{width}x{height}）失败，详见引擎日志");
        }

        /// <summary>渲染目标宽度（对应 lstg.RenderTarget:getWidth，经由渲染目标纹理尺寸）</summary>
        public uint Width
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_renderTargetGetWidth((nuint)Handle); }
        }

        /// <summary>渲染目标高度（对应 lstg.RenderTarget:getHeight）</summary>
        public uint Height
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_renderTargetGetHeight((nuint)Handle); }
        }

        /// <summary>
        /// 获取渲染目标纹理（对应 lstg.RenderTarget:getTexture）。
        /// 返回的包装对象持有引擎纹理引用，无纹理时返回 null。
        /// </summary>
        public Texture2D? GetTexture()
        {
            ThrowIfDisposed();
            var texture = LuaSTGAPI.api.mg_renderTargetGetTexture((nuint)Handle);
            return texture != 0 ? new Texture2D((nint)texture) : null;
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_renderTargetRelease((nuint)Handle);
        }
    }

    /// <summary>
    /// 深度模板缓冲（对应 Lua 侧 lstg.DepthStencilBuffer）。
    /// 通过 <see cref="Create"/> 工厂创建；<see cref="Dispose"/> 销毁。
    /// </summary>
    public sealed unsafe class DepthStencilBuffer : ModernGraphicsObject
    {
        internal DepthStencilBuffer(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建深度模板缓冲（对应 lstg.DepthStencilBuffer.create）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static DepthStencilBuffer Create(uint width, uint height)
        {
            var handle = LuaSTGAPI.api.mg_depthStencilCreate(width, height);
            return handle != 0
                ? new DepthStencilBuffer((nint)handle)
                : throw new InvalidOperationException($"创建 DepthStencilBuffer（{width}x{height}）失败，详见引擎日志");
        }

        /// <summary>缓冲宽度（对应 lstg.DepthStencilBuffer:getWidth）</summary>
        public uint Width
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_depthStencilGetWidth((nuint)Handle); }
        }

        /// <summary>缓冲高度（对应 lstg.DepthStencilBuffer:getHeight）</summary>
        public uint Height
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_depthStencilGetHeight((nuint)Handle); }
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_depthStencilRelease((nuint)Handle);
        }
    }
}
