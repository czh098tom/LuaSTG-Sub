using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 精灵（对应 Lua 侧 lstg.Sprite）。
    /// 描述纹理矩形、中心与单位像素比例，供各精灵渲染器使用；
    /// 通过 <see cref="Create(Texture2D, float, float, float, float)"/> 等工厂创建；<see cref="Dispose"/> 销毁。
    /// </summary>
    public sealed unsafe class Sprite : ModernGraphicsObject
    {
        internal Sprite(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建精灵（对应 lstg.Sprite.create(texture, x, y, width, height)，不设置中心，unit_per_pixel=1）。
        /// </summary>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static Sprite Create(Texture2D texture, float x, float y, float width, float height)
        {
            return Create(texture, x, y, width, height, hasCenter: false, 0f, 0f, 1f);
        }

        /// <summary>
        /// 创建精灵（对应 lstg.Sprite.create(texture, x, y, width, height, cx, cy)，unit_per_pixel=1）。
        /// </summary>
        public static Sprite Create(Texture2D texture, float x, float y, float width, float height, float centerX, float centerY)
        {
            return Create(texture, x, y, width, height, hasCenter: true, centerX, centerY, 1f);
        }

        /// <summary>
        /// 创建精灵（对应 lstg.Sprite.create 完整形式）。
        /// </summary>
        /// <param name="unitPerPixel">单位像素比例（Lua 键 unit_per_pixel，默认 1）</param>
        public static Sprite Create(Texture2D texture, float x, float y, float width, float height, float centerX, float centerY, float unitPerPixel)
        {
            return Create(texture, x, y, width, height, hasCenter: true, centerX, centerY, unitPerPixel);
        }

        private static Sprite Create(Texture2D texture, float x, float y, float width, float height, bool hasCenter, float centerX, float centerY, float unitPerPixel)
        {
            if (texture is null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            if (texture.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Texture2D), "纹理已销毁");
            }
            var handle = LuaSTGAPI.api.mg_spriteCreate(
                (nuint)texture.Handle, x, y, width, height,
                (byte)(hasCenter ? 1 : 0), centerX, centerY, unitPerPixel);
            return handle != 0
                ? new Sprite((nint)handle)
                : throw new InvalidOperationException("创建 Sprite 失败，详见引擎日志");
        }

        /// <summary>绑定纹理（对应 lstg.Sprite:setTexture）</summary>
        public void SetTexture(Texture2D texture)
        {
            ThrowIfDisposed();
            if (texture is null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            LuaSTGAPI.api.mg_spriteSetTexture((nuint)Handle, (nuint)texture.Handle);
        }

        /// <summary>
        /// 获取纹理（对应 lstg.Sprite:getTexture）。
        /// 返回的包装对象持有引擎纹理引用，无纹理时返回 null。
        /// </summary>
        public Texture2D? GetTexture()
        {
            ThrowIfDisposed();
            var texture = LuaSTGAPI.api.mg_spriteGetTexture((nuint)Handle);
            return texture != 0 ? new Texture2D((nint)texture) : null;
        }

        /// <summary>设置纹理矩形（对应 lstg.Sprite:setTextureRect，参数为左上角坐标与宽高）</summary>
        public void SetTextureRect(float x, float y, float width, float height)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteSetTextureRect((nuint)Handle, x, y, width, height);
        }

        /// <summary>获取纹理矩形（对应 lstg.Sprite:getTextureRect 的 4 返回值）</summary>
        public (float X, float Y, float Width, float Height) GetTextureRect()
        {
            ThrowIfDisposed();
            float x = 0f, y = 0f, width = 0f, height = 0f;
            LuaSTGAPI.api.mg_spriteGetTextureRect((nuint)Handle, &x, &y, &width, &height);
            return (x, y, width, height);
        }

        /// <summary>设置中心（对应 lstg.Sprite:setCenter）</summary>
        public void SetCenter(float x, float y)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteSetCenter((nuint)Handle, x, y);
        }

        /// <summary>获取中心（对应 lstg.Sprite:getCenter 的 2 返回值）</summary>
        public (float X, float Y) GetCenter()
        {
            ThrowIfDisposed();
            float x = 0f, y = 0f;
            LuaSTGAPI.api.mg_spriteGetCenter((nuint)Handle, &x, &y);
            return (x, y);
        }

        /// <summary>设置单位像素比例（对应 lstg.Sprite:setUnitPerPixel）</summary>
        public void SetUnitPerPixel(float value)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_spriteSetUnitPerPixel((nuint)Handle, value);
        }

        /// <summary>获取单位像素比例（对应 lstg.Sprite:getUnitPerPixel）</summary>
        public float GetUnitPerPixel()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.mg_spriteGetUnitPerPixel((nuint)Handle);
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_spriteRelease((nuint)Handle);
        }
    }
}
