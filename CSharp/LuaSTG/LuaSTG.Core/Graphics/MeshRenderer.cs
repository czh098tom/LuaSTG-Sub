using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 网格渲染器（对应 Lua 侧 lstg.MeshRenderer）。
    /// 变换状态（position/scale/rotation）保存在 C++ 侧句柄结构中（与 Lua userdata 一致），
    /// set* 时立即用 SRT 矩阵应用到引擎；<see cref="Draw"/> 使用引擎 2D 渲染器绘制。
    /// </summary>
    public sealed unsafe class MeshRenderer : ModernGraphicsObject
    {
        internal MeshRenderer(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建网格渲染器（对应 lstg.MeshRenderer.create 无参形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static MeshRenderer Create()
        {
            var handle = LuaSTGAPI.api.mg_meshRendererCreate();
            return handle != 0
                ? new MeshRenderer((nint)handle)
                : throw new InvalidOperationException("创建 MeshRenderer 失败，详见引擎日志");
        }

        /// <summary>
        /// 创建网格渲染器并绑定网格（对应 lstg.MeshRenderer.create(mesh) 形式，经 SetMesh 组合）。
        /// </summary>
        public static MeshRenderer Create(Mesh mesh)
        {
            var renderer = Create();
            renderer.SetMesh(mesh);
            return renderer;
        }

        /// <summary>
        /// 创建网格渲染器并绑定网格与纹理（对应 lstg.MeshRenderer.create(mesh, texture) 形式）。
        /// </summary>
        public static MeshRenderer Create(Mesh mesh, Texture2D texture)
        {
            var renderer = Create(mesh);
            renderer.SetTexture(texture);
            return renderer;
        }

        /// <summary>设置位置（对应 lstg.MeshRenderer:setPosition，2 参数形式补 z=0）</summary>
        public void SetPosition(float x, float y)
            => SetPosition(x, y, 0f);

        /// <summary>设置位置（对应 lstg.MeshRenderer:setPosition）</summary>
        public void SetPosition(float x, float y, float z)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshRendererSetPosition((nuint)Handle, x, y, z);
        }

        /// <summary>设置缩放（对应 lstg.MeshRenderer:setScale，2 参数形式补 z=1）</summary>
        public void SetScale(float x, float y)
            => SetScale(x, y, 1f);

        /// <summary>设置缩放（对应 lstg.MeshRenderer:setScale）</summary>
        public void SetScale(float x, float y, float z)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshRendererSetScale((nuint)Handle, x, y, z);
        }

        /// <summary>设置旋转（对应 lstg.MeshRenderer:setRotationYawPitchRoll，弧度制）</summary>
        public void SetRotationYawPitchRoll(float yaw, float pitch, float roll)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshRendererSetRotationYawPitchRoll((nuint)Handle, yaw, pitch, roll);
        }

        /// <summary>绑定网格（对应 lstg.MeshRenderer:setMesh），null 表示解除绑定</summary>
        public void SetMesh(Mesh? mesh)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshRendererSetMesh((nuint)Handle, (nuint)(mesh?.Handle ?? 0));
        }

        /// <summary>绑定纹理对象（对应 lstg.MeshRenderer:setTexture），null 表示解除绑定</summary>
        public void SetTexture(Texture2D? texture)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshRendererSetTexture((nuint)Handle, (nuint)(texture?.Handle ?? 0));
        }

        /// <summary>
        /// 按资源名绑定纹理（对应 lstg.MeshRenderer:setTexture 的字符串参数形式，从资源池查找）。
        /// </summary>
        /// <exception cref="ArgumentException">纹理资源不存在</exception>
        public void SetTexture(string name)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            if (LuaSTGAPI.api.mg_meshRendererSetTextureByName((nuint)Handle, n) != 0)
            {
                throw new ArgumentException($"找不到纹理 '{name}'", nameof(name));
            }
        }

        /// <summary>设置传统混合模式（对应 lstg.MeshRenderer:setLegacyBlendState）</summary>
        public void SetLegacyBlendState(BlendMode blend)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshRendererSetLegacyBlendState((nuint)Handle, (byte)blend);
        }

        /// <summary>绘制（对应 lstg.MeshRenderer:draw，经引擎 2D 渲染器提交批次）</summary>
        public void Draw()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshRendererDraw((nuint)Handle);
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_meshRendererRelease((nuint)Handle);
        }
    }
}
