using System;

namespace LuaSTG.Core
{
    /// <summary>文字水平对齐方式（对应引擎 FontAlignHorizontal，RenderText 的对齐参数）</summary>
    public enum FontAlignHorizontal : byte
    {
        /// <summary>左对齐</summary>
        Left = 0,
        /// <summary>居中</summary>
        Center = 1,
        /// <summary>右对齐</summary>
        Right = 2,
    }

    /// <summary>文字垂直对齐方式（对应引擎 FontAlignVertical，RenderText 的对齐参数）</summary>
    public enum FontAlignVertical : byte
    {
        /// <summary>顶对齐</summary>
        Top = 0,
        /// <summary>居中</summary>
        Middle = 1,
        /// <summary>底对齐</summary>
        Bottom = 2,
    }

    /// <summary>
    /// TTF 文字格式标志（RenderTTF 的 format 参数，与 Lua 侧整数标志一致）。
    /// </summary>
    [Flags]
    public enum TtfTextFormat : int
    {
        /// <summary>左对齐</summary>
        Left = 0x00,
        /// <summary>水平居中</summary>
        Center = 0x01,
        /// <summary>右对齐</summary>
        Right = 0x02,
        /// <summary>顶对齐</summary>
        Top = 0x00,
        /// <summary>垂直居中</summary>
        VCenter = 0x04,
        /// <summary>底对齐</summary>
        Bottom = 0x08,
        /// <summary>自动换行</summary>
        WordBreak = 0x10,
    }

    /// <summary>
    /// 兼容渲染 API（对应 Lua 侧 lstg.Render* 系列函数，即 LuaSTG ex+ 传统 API）。
    /// BeginScene/EndScene 由 <see cref="LuaSTGAPI.BeginScene"/>/<see cref="LuaSTGAPI.EndScene"/> 提供，此处不重复定义。
    /// </summary>
    public static unsafe partial class Render
    {
        // ========== 清屏 ==========

        /// <summary>
        /// 清屏（对应 lstg.RenderClear）。
        /// 渲染目标栈非空时颜色按 alpha 预乘；注意与 <see cref="LuaSTGAPI.RenderClear"/>（分量形式、不预乘）行为不同。
        /// </summary>
        /// <param name="argb">颜色（ARGB，0xAARRGGBB）</param>
        public static void RenderClear(uint argb) => LuaSTGAPI.api.render_clearRenderTarget(argb);

        // ========== 视口与投影（传统坐标：参数为 left/right/bottom/top，Y 轴翻转） ==========

        /// <summary>设置视口（对应 lstg.SetViewport，按渲染目标高度翻转 Y 轴）</summary>
        public static void SetViewport(double l, double r, double b, double t) => SetViewport(l, r, b, t, 0.0, 1.0);

        /// <summary>设置视口（对应 lstg.SetViewport，按渲染目标高度翻转 Y 轴）</summary>
        public static void SetViewport(double l, double r, double b, double t, double znear, double zfar)
            => LuaSTGAPI.api.render_setViewportCompat(l, r, b, t, znear, zfar);

        /// <summary>设置裁剪矩形（对应 lstg.SetScissorRect，按渲染目标高度翻转 Y 轴）</summary>
        public static void SetScissorRect(double l, double r, double b, double t)
            => LuaSTGAPI.api.render_setScissorRectCompat(l, r, b, t);

        /// <summary>设置正交投影（对应 lstg.SetOrtho）</summary>
        public static void SetOrtho(double l, double r, double b, double t) => Renderer.SetOrtho(l, r, b, t);

        /// <summary>设置正交投影（对应 lstg.SetOrtho，带深度范围）</summary>
        public static void SetOrtho(double l, double r, double b, double t, double znear, double zfar)
            => Renderer.SetOrtho(l, r, b, t, znear, zfar);

        /// <summary>
        /// 设置透视投影（对应 lstg.SetPerspective）。
        /// 要求 0 &lt; fov &lt; π 且 0 &lt; znear &lt; zfar。
        /// </summary>
        public static void SetPerspective(
            double eyeX, double eyeY, double eyeZ,
            double lookatX, double lookatY, double lookatZ,
            double headupX, double headupY, double headupZ,
            double fov, double aspectRatio, double znear, double zfar)
            => Renderer.SetPerspective(
                eyeX, eyeY, eyeZ, lookatX, lookatY, lookatZ, headupX, headupY, headupZ, fov, aspectRatio, znear, zfar);

        // ========== 精灵绘制 ==========

        /// <summary>绘制精灵（对应 lstg.Render）。rot 为角度制；缩放会乘以全局图像缩放系数</summary>
        /// <param name="vscale">纵向缩放，null 时等于 <paramref name="hscale"/></param>
        /// <param name="z">深度，默认 0.5</param>
        public static void RenderSprite(string name, float x, float y, float rot = 0.0f, float hscale = 1.0f, float? vscale = null, float z = 0.5f)
            => Renderer.DrawSprite(name, x, y, rot, hscale, vscale, z);

        /// <summary>以矩形绘制精灵（对应 lstg.RenderRect）</summary>
        public static void RenderRect(string name, float l, float r, float b, float t, float z = 0.5f)
            => Renderer.DrawSpriteRect(name, l, r, b, t, z);

        /// <summary>以 4 顶点绘制精灵（对应 lstg.Render4V）</summary>
        public static void Render4V(
            string name,
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            float x3, float y3, float z3,
            float x4, float y4, float z4)
            => Renderer.DrawSprite4V(name, x1, y1, z1, x2, y2, z2, x3, y3, z3, x4, y4, z4);

        /// <summary>绘制动画序列（对应 lstg.RenderAnimation）。rot 为角度制；缩放会乘以全局图像缩放系数</summary>
        public static void RenderAnimation(string name, int aniTimer, float x, float y, float rot = 0.0f, float hscale = 1.0f, float? vscale = null, float z = 0.5f)
            => Renderer.DrawSpriteSequence(name, aniTimer, x, y, rot, hscale, vscale, z);

        /// <summary>
        /// 绘制纹理（对应 lstg.RenderTexture）。
        /// U/V 为像素坐标，引擎按纹理尺寸归一化；混合模式为传统 <see cref="BlendMode"/>。
        /// </summary>
        public static void RenderTexture(string name, BlendMode blend, Renderer.DrawVertex v1, Renderer.DrawVertex v2, Renderer.DrawVertex v3, Renderer.DrawVertex v4)
            => Renderer.DrawTexture(name, blend, v1, v2, v3, v4);

        /// <summary>绘制模型（对应 lstg.RenderModel）。roll/pitch/yaw 为角度制</summary>
        public static void RenderModel(
            string name,
            float x, float y, float z,
            float roll = 0.0f, float pitch = 0.0f, float yaw = 0.0f,
            float sx = 1.0f, float sy = 1.0f, float sz = 1.0f)
            => Renderer.DrawModel(name, x, y, z, roll, pitch, yaw, sx, sy, sz);

        // ========== 雾与深度 ==========

        /// <summary>设置雾（对应 lstg.SetFog(start, end, color)）</summary>
        /// <param name="argb">雾颜色（ARGB，0xAARRGGBB）</param>
        public static void SetFog(double start, double end, uint argb)
            => LuaSTGAPI.api.render_setFogCompat(start, end, argb);

        /// <summary>设置雾（对应 lstg.SetFog(start, end)，颜色为不透明黑）</summary>
        public static void SetFog(double start, double end)
            => LuaSTGAPI.api.render_setFogCompat(start, end, 0xFF000000u);

        /// <summary>关闭雾（对应无参数的 lstg.SetFog()）</summary>
        public static void SetFog()
            => LuaSTGAPI.api.render_setFogCompat(0.0, 0.0, 0x00000000u);

        /// <summary>设置深度缓冲开关（对应 lstg.SetZBufferEnable）</summary>
        public static void SetZBufferEnable(bool enable)
            => Renderer.SetDepthState(enable ? Renderer.DepthState.Enable : Renderer.DepthState.Disable);

        /// <summary>清空深度缓冲（对应 lstg.ClearZBuffer）</summary>
        public static void ClearZBuffer(float z = 1.0f) => Renderer.ClearDepthBuffer(z);

        // ========== 渲染目标 ==========

        /// <summary>
        /// 压入渲染目标（对应 lstg.PushRenderTarget）。
        /// 压入后视口与裁剪矩形自动重置为渲染目标尺寸。
        /// </summary>
        /// <exception cref="ArgumentException">纹理不存在或不是渲染目标</exception>
        /// <exception cref="InvalidOperationException">不在渲染批次内或压栈失败</exception>
        public static void PushRenderTarget(string name)
        {
            using var s = new MarshaledString(name);
            switch (LuaSTGAPI.api.render_pushRenderTarget(s))
            {
                case 0:
                    break;
                case 2:
                    throw new ArgumentException($"渲染目标 '{name}' 不存在", nameof(name));
                case 3:
                    throw new ArgumentException($"纹理 '{name}' 不是渲染目标", nameof(name));
                case 4:
                    throw new InvalidOperationException($"压入渲染目标 '{name}' 失败");
                default:
                    throw new InvalidOperationException("无效的渲染操作：不在 BeginScene/EndScene 渲染批次内");
            }
        }

        /// <summary>弹出渲染目标（对应 lstg.PopRenderTarget）</summary>
        /// <exception cref="InvalidOperationException">不在渲染批次内或弹栈失败</exception>
        public static void PopRenderTarget()
        {
            switch (LuaSTGAPI.api.render_popRenderTarget())
            {
                case 0:
                    break;
                case 2:
                    throw new InvalidOperationException("弹出渲染目标失败（栈已为空？）");
                default:
                    throw new InvalidOperationException("无效的渲染操作：不在 BeginScene/EndScene 渲染批次内");
            }
        }

        // ========== 文字（对应 lstg.RenderText / lstg.RenderTTF） ==========

        /// <summary>
        /// 渲染纹理字体文字（对应 lstg.RenderText）。
        /// 缩放会乘以全局图像缩放系数；渲染失败抛出 <see cref="InvalidOperationException"/>（字体不存在等，详见引擎日志）。
        /// </summary>
        public static void RenderText(
            string fontName, string text,
            float x, float y, float scale = 1.0f,
            FontAlignHorizontal halign = FontAlignHorizontal.Center,
            FontAlignVertical valign = FontAlignVertical.Middle)
        {
            using var f = new MarshaledString(fontName);
            using var t = new MarshaledString(text);
            if (LuaSTGAPI.api.render_renderText(f, t, x, y, scale, (byte)halign, (byte)valign) != 0)
            {
                throw new InvalidOperationException($"无法绘制文字（字体 '{fontName}'）");
            }
        }

        /// <summary>
        /// 渲染 TTF 字体文字（对应 lstg.RenderTTF）。
        /// 缩放会乘以全局图像缩放系数；渲染失败抛出 <see cref="InvalidOperationException"/>（字体不存在等，详见引擎日志）。
        /// </summary>
        /// <param name="argb">颜色（ARGB，0xAARRGGBB）</param>
        public static void RenderTTF(
            string fontName, string text,
            float left, float right, float bottom, float top,
            TtfTextFormat format, uint argb, float scale = 1.0f)
        {
            using var f = new MarshaledString(fontName);
            using var t = new MarshaledString(text);
            if (LuaSTGAPI.api.render_renderTTF(f, t, left, right, bottom, top, scale, (int)format, argb) != 0)
            {
                throw new InvalidOperationException($"无法渲染 TTF 字体 '{fontName}'");
            }
        }

        // ========== 截图与纹理保存 ==========

        /// <summary>保存截图（对应 lstg.Snapshot；失败仅记录引擎日志）</summary>
        public static void Snapshot(string path)
        {
            using var p = new MarshaledString(path);
            LuaSTGAPI.api.render_snapshot(p);
        }

        /// <summary>保存纹理到文件（对应 lstg.SaveTexture；失败仅记录引擎日志）</summary>
        public static void SaveTexture(string texName, string path)
        {
            using var t = new MarshaledString(texName);
            using var p = new MarshaledString(path);
            LuaSTGAPI.api.render_saveTexture(t, p);
        }

        // ========== 碰撞体绘制 ==========

        /// <summary>绘制所有对象的碰撞体（对应 lstg.DrawCollider）</summary>
        public static void DrawCollider() => LuaSTGAPI.api.render_drawCollider();

        /// <summary>绘制指定碰撞组的碰撞体（对应 lstg.RenderGroupCollider）</summary>
        /// <param name="argb">填充颜色（ARGB，0xAARRGGBB）</param>
        public static void RenderGroupCollider(int group, uint argb)
        {
            if (group < 0 || group >= GameObjectBase.GroupCount)
            {
                throw new ArgumentOutOfRangeException(nameof(group), $"碰撞组取值范围为 0~{GameObjectBase.GroupCount - 1}");
            }
            LuaSTGAPI.api.render_renderGroupCollider(group, argb);
        }
    }
}
