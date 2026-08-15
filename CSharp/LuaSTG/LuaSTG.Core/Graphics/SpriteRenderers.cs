using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 精灵渲染器（对应 Lua 侧 lstg.SpriteRenderer）。
    /// 变换状态（position/scale/rotation）保存在 C++ 侧句柄结构中（与 Lua userdata 一致），
    /// setPosition/setScale/setRotation 仅标记脏，<see cref="Draw"/> 时统一应用；
    /// <see cref="SetTransform(float, float, float, float, float?)"/> 立即应用。
    /// </summary>
    public sealed unsafe class SpriteRenderer : ModernGraphicsObject
    {
        internal SpriteRenderer(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建精灵渲染器（对应 lstg.SpriteRenderer.create 无参形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static SpriteRenderer Create()
        {
            var handle = LuaSTGAPI.api.mg_spriteRendererCreate();
            return handle != 0
                ? new SpriteRenderer((nint)handle)
                : throw new InvalidOperationException("创建 SpriteRenderer 失败，详见引擎日志");
        }

        /// <summary>
        /// 创建精灵渲染器并绑定精灵（对应 lstg.SpriteRenderer.create(sprite) 形式，经 SetSprite 组合）。
        /// </summary>
        public static SpriteRenderer Create(Sprite sprite)
        {
            var renderer = Create();
            renderer.SetSprite(sprite);
            return renderer;
        }

        /// <summary>
        /// 设置变换（对应 lstg.SpriteRenderer:setTransform(x, y[, rot[, sx[, sy]]])）。
        /// 默认 rotation=0、scaleX=1、scaleY=scaleX（与 Lua 侧默认值一致），立即应用。
        /// </summary>
        public void SetTransform(float x, float y, float rotation = 0f, float scaleX = 1f, float? scaleY = null)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererSetTransform((nuint)Handle, x, y, rotation, scaleX, scaleY ?? scaleX);
        }

        /// <summary>设置位置（对应 lstg.SpriteRenderer:setPosition），draw 时应用</summary>
        public void SetPosition(float x, float y)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererSetPosition((nuint)Handle, x, y);
        }

        /// <summary>设置缩放（对应 lstg.SpriteRenderer:setScale），draw 时应用</summary>
        public void SetScale(float x, float y)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererSetScale((nuint)Handle, x, y);
        }

        /// <summary>设置旋转（对应 lstg.SpriteRenderer:setRotation），draw 时应用</summary>
        public void SetRotation(float rotation)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererSetRotation((nuint)Handle, rotation);
        }

        /// <summary>绑定精灵（对应 lstg.SpriteRenderer:setSprite）</summary>
        public void SetSprite(Sprite sprite)
        {
            ThrowIfDisposed();
            if (sprite is null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }
            LuaSTGAPI.api.mg_spriteRendererSetSprite((nuint)Handle, (nuint)sprite.Handle);
        }

        /// <summary>设置统一颜色（对应 lstg.SpriteRenderer:setColor）</summary>
        /// <param name="argb">颜色（ARGB，0xAARRGGBB）</param>
        public void SetColor(uint argb)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererSetColor((nuint)Handle, argb);
        }

        /// <summary>设置 4 顶点颜色（对应 lstg.SpriteRenderer:setColor(c1, c2, c3, c4)）</summary>
        public void SetColor(uint c1, uint c2, uint c3, uint c4)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererSetColor4((nuint)Handle, c1, c2, c3, c4);
        }

        /// <summary>设置传统混合模式（对应 lstg.SpriteRenderer:setLegacyBlendState）</summary>
        public void SetLegacyBlendState(BlendMode blend)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererSetLegacyBlendState((nuint)Handle, (byte)blend);
        }

        /// <summary>绘制（对应 lstg.SpriteRenderer:draw，经引擎 2D 渲染器提交批次）</summary>
        public void Draw()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRendererDraw((nuint)Handle);
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_spriteRendererRelease((nuint)Handle);
        }
    }

    /// <summary>
    /// 矩形精灵渲染器（对应 Lua 侧 lstg.SpriteRectRenderer）。
    /// 以屏幕坐标矩形（left/right/bottom/top）定位精灵。
    /// </summary>
    public sealed unsafe class SpriteRectRenderer : ModernGraphicsObject
    {
        internal SpriteRectRenderer(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建矩形精灵渲染器（对应 lstg.SpriteRectRenderer.create 无参形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static SpriteRectRenderer Create()
        {
            var handle = LuaSTGAPI.api.mg_spriteRectRendererCreate();
            return handle != 0
                ? new SpriteRectRenderer((nint)handle)
                : throw new InvalidOperationException("创建 SpriteRectRenderer 失败，详见引擎日志");
        }

        /// <summary>
        /// 创建矩形精灵渲染器并绑定精灵（对应 lstg.SpriteRectRenderer.create(sprite) 形式）。
        /// </summary>
        public static SpriteRectRenderer Create(Sprite sprite)
        {
            var renderer = Create();
            renderer.SetSprite(sprite);
            return renderer;
        }

        /// <summary>设置目标矩形（对应 lstg.SpriteRectRenderer:setRect(left, right, bottom, top)，Lua 参数顺序）</summary>
        public void SetRect(float left, float right, float bottom, float top)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRectRendererSetRect((nuint)Handle, left, right, bottom, top);
        }

        /// <summary>绑定精灵（对应 lstg.SpriteRectRenderer:setSprite）</summary>
        public void SetSprite(Sprite sprite)
        {
            ThrowIfDisposed();
            if (sprite is null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }
            LuaSTGAPI.api.mg_spriteRectRendererSetSprite((nuint)Handle, (nuint)sprite.Handle);
        }

        /// <summary>设置统一颜色（对应 lstg.SpriteRectRenderer:setColor）</summary>
        /// <param name="argb">颜色（ARGB，0xAARRGGBB）</param>
        public void SetColor(uint argb)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRectRendererSetColor((nuint)Handle, argb);
        }

        /// <summary>设置 4 顶点颜色（对应 lstg.SpriteRectRenderer:setColor(c1, c2, c3, c4)）</summary>
        public void SetColor(uint c1, uint c2, uint c3, uint c4)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRectRendererSetColor4((nuint)Handle, c1, c2, c3, c4);
        }

        /// <summary>设置传统混合模式（对应 lstg.SpriteRectRenderer:setLegacyBlendState）</summary>
        public void SetLegacyBlendState(BlendMode blend)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRectRendererSetLegacyBlendState((nuint)Handle, (byte)blend);
        }

        /// <summary>绘制（对应 lstg.SpriteRectRenderer:draw，经引擎 2D 渲染器提交批次）</summary>
        public void Draw()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteRectRendererDraw((nuint)Handle);
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_spriteRectRendererRelease((nuint)Handle);
        }
    }

    /// <summary>
    /// 四边形精灵渲染器（对应 Lua 侧 lstg.SpriteQuadRenderer）。
    /// 以任意四边形（2D 或 3D 顶点）定位精灵。
    /// </summary>
    public sealed unsafe class SpriteQuadRenderer : ModernGraphicsObject
    {
        internal SpriteQuadRenderer(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建四边形精灵渲染器（对应 lstg.SpriteQuadRenderer.create 无参形式）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static SpriteQuadRenderer Create()
        {
            var handle = LuaSTGAPI.api.mg_spriteQuadRendererCreate();
            return handle != 0
                ? new SpriteQuadRenderer((nint)handle)
                : throw new InvalidOperationException("创建 SpriteQuadRenderer 失败，详见引擎日志");
        }

        /// <summary>
        /// 创建四边形精灵渲染器并绑定精灵（对应 lstg.SpriteQuadRenderer.create(sprite) 形式）。
        /// </summary>
        public static SpriteQuadRenderer Create(Sprite sprite)
        {
            var renderer = Create();
            renderer.SetSprite(sprite);
            return renderer;
        }

        /// <summary>设置 2D 四边形（对应 lstg.SpriteQuadRenderer:setQuad 8 参数形式）</summary>
        public void SetQuad(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteQuadRendererSetQuad2((nuint)Handle, x1, y1, x2, y2, x3, y3, x4, y4);
        }

        /// <summary>设置 3D 四边形（对应 lstg.SpriteQuadRenderer:setQuad 12 参数形式）</summary>
        public void SetQuad(float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3, float x4, float y4, float z4)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteQuadRendererSetQuad3((nuint)Handle, x1, y1, z1, x2, y2, z2, x3, y3, z3, x4, y4, z4);
        }

        /// <summary>绑定精灵（对应 lstg.SpriteQuadRenderer:setSprite）</summary>
        public void SetSprite(Sprite sprite)
        {
            ThrowIfDisposed();
            if (sprite is null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }
            LuaSTGAPI.api.mg_spriteQuadRendererSetSprite((nuint)Handle, (nuint)sprite.Handle);
        }

        /// <summary>设置统一颜色（对应 lstg.SpriteQuadRenderer:setColor）</summary>
        /// <param name="argb">颜色（ARGB，0xAARRGGBB）</param>
        public void SetColor(uint argb)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteQuadRendererSetColor((nuint)Handle, argb);
        }

        /// <summary>设置 4 顶点颜色（对应 lstg.SpriteQuadRenderer:setColor(c1, c2, c3, c4)）</summary>
        public void SetColor(uint c1, uint c2, uint c3, uint c4)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteQuadRendererSetColor4((nuint)Handle, c1, c2, c3, c4);
        }

        /// <summary>设置传统混合模式（对应 lstg.SpriteQuadRenderer:setLegacyBlendState）</summary>
        public void SetLegacyBlendState(BlendMode blend)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteQuadRendererSetLegacyBlendState((nuint)Handle, (byte)blend);
        }

        /// <summary>绘制（对应 lstg.SpriteQuadRenderer:draw，经引擎 2D 渲染器提交批次）</summary>
        public void Draw()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteQuadRendererDraw((nuint)Handle);
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_spriteQuadRendererRelease((nuint)Handle);
        }
    }
}
