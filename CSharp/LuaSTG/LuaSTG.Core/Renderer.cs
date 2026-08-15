using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 现代渲染器（对应 Lua 侧 lstg.Renderer 表）。
    /// BeginScene/EndScene 由 <see cref="LuaSTGAPI.BeginScene"/>/<see cref="LuaSTGAPI.EndScene"/> 提供，此处不重复定义。
    /// </summary>
    public static unsafe partial class Renderer
    {
        /// <summary>顶点色混合模式（对应引擎 IRenderer::VertexColorBlendState，lstg.Renderer.setVertexColorBlendState 参数）</summary>
        public enum VertexColorBlendState : byte
        {
            /// <summary>顶点色乘 0</summary>
            Zero = 0,
            /// <summary>顶点色乘 1</summary>
            One = 1,
            /// <summary>顶点色与纹理色相加</summary>
            Add = 2,
            /// <summary>顶点色与纹理色相乘</summary>
            Mul = 3,
        }

        /// <summary>雾模式（对应引擎 IRenderer::FogState，lstg.Renderer.setFogState 参数）</summary>
        public enum FogState : byte
        {
            /// <summary>禁用雾</summary>
            Disable = 0,
            /// <summary>线性雾</summary>
            Linear = 1,
            /// <summary>指数雾</summary>
            Exp = 2,
            /// <summary>指数平方雾</summary>
            Exp2 = 3,
        }

        /// <summary>深度测试开关（对应引擎 IRenderer::DepthState）</summary>
        public enum DepthState : byte
        {
            /// <summary>禁用</summary>
            Disable = 0,
            /// <summary>启用</summary>
            Enable = 1,
        }

        /// <summary>
        /// 渲染器混合状态（对应引擎 IRenderer::BlendState，lstg.Renderer.setBlendState 参数）。
        /// 与 <see cref="BlendMode"/>（传统混合模式）是不同的概念。
        /// </summary>
        public enum BlendState : byte
        {
            /// <summary>禁用混合</summary>
            Disable = 0,
            /// <summary>标准透明混合</summary>
            Alpha = 1,
            /// <summary>直接写入</summary>
            One = 2,
            /// <summary>取最小值</summary>
            Min = 3,
            /// <summary>取最大值</summary>
            Max = 4,
            /// <summary>相乘</summary>
            Mul = 5,
            /// <summary>滤色</summary>
            Screen = 6,
            /// <summary>相加</summary>
            Add = 7,
            /// <summary>相减</summary>
            Sub = 8,
            /// <summary>反向相减</summary>
            RevSub = 9,
            /// <summary>取反</summary>
            Inv = 10,
        }

        /// <summary>采样器状态（对应引擎 IRenderer::SamplerState）</summary>
        public enum SamplerState : byte
        {
            /// <summary>点采样，边缘重复</summary>
            PointWrap = 0,
            /// <summary>点采样，边缘钳制</summary>
            PointClamp = 1,
            /// <summary>点采样，黑边框</summary>
            PointBorderBlack = 2,
            /// <summary>点采样，白边框</summary>
            PointBorderWhite = 3,
            /// <summary>线性采样，边缘重复</summary>
            LinearWrap = 4,
            /// <summary>线性采样，边缘钳制</summary>
            LinearClamp = 5,
            /// <summary>线性采样，黑边框</summary>
            LinearBorderBlack = 6,
            /// <summary>线性采样，白边框</summary>
            LinearBorderWhite = 7,
        }

        /// <summary>
        /// 低级绘制顶点（对应引擎 IRenderer::DrawVertex）。
        /// 颜色为 ARGB（0xAARRGGBB）；U/V 在 DrawTexture 中为像素坐标。
        /// </summary>
        public readonly struct DrawVertex
        {
            /// <summary>X 坐标</summary>
            public readonly float X;
            /// <summary>Y 坐标</summary>
            public readonly float Y;
            /// <summary>Z 坐标（深度）</summary>
            public readonly float Z;
            /// <summary>纹理 U 坐标</summary>
            public readonly float U;
            /// <summary>纹理 V 坐标</summary>
            public readonly float V;
            /// <summary>顶点色（ARGB）</summary>
            public readonly uint Color;

            /// <param name="color">顶点色（ARGB，0xAARRGGBB）</param>
            public DrawVertex(float x, float y, float z, float u, float v, uint color = 0xFFFFFFFFu)
            {
                X = x; Y = y; Z = z; U = u; V = v; Color = color;
            }
        }

        // ========== 错误码到异常的翻译 ==========

        private static void ThrowScope()
            => throw new InvalidOperationException("无效的渲染操作：不在 BeginScene/EndScene 渲染批次内（Lua 侧错误 \"invalid render operation\"）");

        private static void ThrowResourceNotFound(string kind, string name)
            => throw new ArgumentException($"找不到{kind}资源 '{name}'", nameof(name));

        // ========== 清屏与深度（lstg.Renderer.clearRenderTarget / clearDepthBuffer） ==========

        /// <summary>
        /// 清空渲染目标（对应 lstg.Renderer.clearRenderTarget）。
        /// 渲染目标栈非空时颜色按 alpha 预乘。
        /// </summary>
        /// <param name="argb">颜色（ARGB，0xAARRGGBB）</param>
        public static void ClearRenderTarget(uint argb) => LuaSTGAPI.api.render_clearRenderTarget(argb);

        /// <summary>清空深度缓冲（对应 lstg.Renderer.clearDepthBuffer）</summary>
        /// <param name="z">深度值，默认 1</param>
        public static void ClearDepthBuffer(float z = 1.0f) => LuaSTGAPI.api.render_clearDepthBuffer(z);

        // ========== 投影与视口 ==========

        /// <summary>设置正交投影（对应 lstg.Renderer.setOrtho，4 参数形式，znear=0、zfar=1）</summary>
        public static void SetOrtho(double l, double r, double b, double t) => SetOrtho(l, r, b, t, 0.0, 1.0);

        /// <summary>设置正交投影（对应 lstg.Renderer.setOrtho，6 参数形式）</summary>
        public static void SetOrtho(double l, double r, double b, double t, double znear, double zfar)
            => LuaSTGAPI.api.render_setOrtho(l, r, b, t, znear, zfar);

        /// <summary>
        /// 设置透视投影（对应 lstg.Renderer.setPerspective）。
        /// 要求 0 &lt; fov &lt; π 且 0 &lt; znear &lt; zfar，否则抛出 <see cref="ArgumentException"/>。
        /// </summary>
        public static void SetPerspective(
            double eyeX, double eyeY, double eyeZ,
            double lookatX, double lookatY, double lookatZ,
            double headupX, double headupY, double headupZ,
            double fov, double aspectRatio, double znear, double zfar)
        {
            var code = LuaSTGAPI.api.render_setPerspective(
                eyeX, eyeY, eyeZ, lookatX, lookatY, lookatZ, headupX, headupY, headupZ, fov, aspectRatio, znear, zfar);
            switch (code)
            {
                case 0:
                    break;
                case 1:
                    throw new ArgumentException($"无效的参数：要求 (0 < fov < pi)，实际 (fov = {fov})", nameof(fov));
                default:
                    throw new ArgumentException($"无效的参数：要求 (0 < z_near < z_far)，实际 (z_near = {znear}, z_far = {zfar})");
            }
        }

        /// <summary>设置视口（对应 lstg.Renderer.setViewport，引擎原生坐标：Y 轴向下，4 参数形式默认 znear=0、zfar=1）</summary>
        public static void SetViewport(double left, double top, double right, double bottom)
            => SetViewport(left, top, right, bottom, 0.0, 1.0);

        /// <summary>设置视口（对应 lstg.Renderer.setViewport，引擎原生坐标：Y 轴向下，不翻转）</summary>
        public static void SetViewport(double left, double top, double right, double bottom, double znear, double zfar)
            => LuaSTGAPI.api.render_setViewport(left, top, right, bottom, znear, zfar);

        /// <summary>设置裁剪矩形（对应 lstg.Renderer.setScissorRect，引擎原生坐标：Y 轴向下，不翻转）</summary>
        public static void SetScissorRect(double left, double top, double right, double bottom)
            => LuaSTGAPI.api.render_setScissorRect(left, top, right, bottom);

        // ========== 渲染状态 ==========

        /// <summary>设置顶点色混合模式（对应 lstg.Renderer.setVertexColorBlendState）</summary>
        public static void SetVertexColorBlendState(VertexColorBlendState state)
        {
            if (LuaSTGAPI.api.render_setVertexColorBlendState((byte)state) != 0) ThrowScope();
        }

        /// <summary>设置雾状态（对应 lstg.Renderer.setFogState）</summary>
        /// <param name="state">雾模式</param>
        /// <param name="argb">雾颜色（ARGB，0xAARRGGBB）</param>
        /// <param name="densityOrZnear">Linear 模式为近平面，Exp/Exp2 模式为密度</param>
        /// <param name="zfar">远平面（Linear 模式）</param>
        public static void SetFogState(FogState state, uint argb, float densityOrZnear, float zfar = 0.0f)
        {
            if (LuaSTGAPI.api.render_setFogState((byte)state, argb, densityOrZnear, zfar) != 0) ThrowScope();
        }

        /// <summary>设置深度测试开关（对应 lstg.Renderer.setDepthState）</summary>
        public static void SetDepthState(DepthState state)
        {
            if (LuaSTGAPI.api.render_setDepthState((byte)state) != 0) ThrowScope();
        }

        /// <summary>设置渲染器混合状态（对应 lstg.Renderer.setBlendState）</summary>
        public static void SetBlendState(BlendState state)
        {
            if (LuaSTGAPI.api.render_setBlendState((byte)state) != 0) ThrowScope();
        }

        /// <summary>绑定纹理（对应 lstg.Renderer.setTexture）</summary>
        /// <exception cref="ArgumentException">纹理不存在</exception>
        public static void SetTexture(string name)
        {
            using var s = new MarshaledString(name);
            switch (LuaSTGAPI.api.render_setTexture(s))
            {
                case 0:
                    break;
                case 2:
                    ThrowResourceNotFound("纹理", name);
                    break;
                default:
                    ThrowScope();
                    break;
            }
        }

        // ========== 低级图元 ==========

        /// <summary>绘制三角形（对应 lstg.Renderer.drawTriangle）</summary>
        public static void DrawTriangle(DrawVertex v1, DrawVertex v2, DrawVertex v3)
        {
            if (LuaSTGAPI.api.render_drawTriangle(
                v1.X, v1.Y, v1.Z, v1.U, v1.V, v1.Color,
                v2.X, v2.Y, v2.Z, v2.U, v2.V, v2.Color,
                v3.X, v3.Y, v3.Z, v3.U, v3.V, v3.Color) != 0)
            {
                ThrowScope();
            }
        }

        /// <summary>绘制四边形（对应 lstg.Renderer.drawQuad）</summary>
        public static void DrawQuad(DrawVertex v1, DrawVertex v2, DrawVertex v3, DrawVertex v4)
        {
            if (LuaSTGAPI.api.render_drawQuad(
                v1.X, v1.Y, v1.Z, v1.U, v1.V, v1.Color,
                v2.X, v2.Y, v2.Z, v2.U, v2.V, v2.Color,
                v3.X, v3.Y, v3.Z, v3.U, v3.V, v3.Color,
                v4.X, v4.Y, v4.Z, v4.U, v4.V, v4.Color) != 0)
            {
                ThrowScope();
            }
        }

        // ========== 精灵绘制 ==========

        /// <summary>绘制精灵（对应 lstg.Renderer.drawSprite）。rot 为角度制；缩放会乘以全局图像缩放系数</summary>
        /// <param name="vscale">纵向缩放，null 时等于 <paramref name="hscale"/></param>
        /// <param name="z">深度，默认 0.5</param>
        public static void DrawSprite(string name, float x, float y, float rot = 0.0f, float hscale = 1.0f, float? vscale = null, float z = 0.5f)
        {
            using var s = new MarshaledString(name);
            switch (LuaSTGAPI.api.render_drawSprite(s, x, y, rot, hscale, vscale ?? hscale, z))
            {
                case 0:
                    break;
                case 2:
                    ThrowResourceNotFound("精灵", name);
                    break;
                default:
                    ThrowScope();
                    break;
            }
        }

        /// <summary>以矩形绘制精灵（对应 lstg.Renderer.drawSpriteRect）</summary>
        public static void DrawSpriteRect(string name, float l, float r, float b, float t, float z = 0.5f)
        {
            using var s = new MarshaledString(name);
            switch (LuaSTGAPI.api.render_drawSpriteRect(s, l, r, b, t, z))
            {
                case 0:
                    break;
                case 2:
                    ThrowResourceNotFound("精灵", name);
                    break;
                default:
                    ThrowScope();
                    break;
            }
        }

        /// <summary>以 4 顶点绘制精灵（对应 lstg.Renderer.drawSprite4V）</summary>
        public static void DrawSprite4V(
            string name,
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            float x3, float y3, float z3,
            float x4, float y4, float z4)
        {
            using var s = new MarshaledString(name);
            switch (LuaSTGAPI.api.render_drawSprite4V(s, x1, y1, z1, x2, y2, z2, x3, y3, z3, x4, y4, z4))
            {
                case 0:
                    break;
                case 2:
                    ThrowResourceNotFound("精灵", name);
                    break;
                default:
                    ThrowScope();
                    break;
            }
        }

        /// <summary>绘制动画序列（对应 lstg.Renderer.drawSpriteSequence）。rot 为角度制；缩放会乘以全局图像缩放系数</summary>
        /// <param name="vscale">纵向缩放，null 时等于 <paramref name="hscale"/></param>
        /// <param name="z">深度，默认 0.5</param>
        public static void DrawSpriteSequence(string name, int aniTimer, float x, float y, float rot = 0.0f, float hscale = 1.0f, float? vscale = null, float z = 0.5f)
        {
            using var s = new MarshaledString(name);
            switch (LuaSTGAPI.api.render_drawSpriteSequence(s, aniTimer, x, y, rot, hscale, vscale ?? hscale, z))
            {
                case 0:
                    break;
                case 2:
                    ThrowResourceNotFound("动画", name);
                    break;
                default:
                    ThrowScope();
                    break;
            }
        }

        /// <summary>
        /// 绘制纹理（对应 lstg.Renderer.drawTexture）。
        /// U/V 为像素坐标，引擎按纹理尺寸归一化；混合模式为传统 <see cref="BlendMode"/>。
        /// </summary>
        public static void DrawTexture(string name, BlendMode blend, DrawVertex v1, DrawVertex v2, DrawVertex v3, DrawVertex v4)
        {
            using var s = new MarshaledString(name);
            switch (LuaSTGAPI.api.render_drawTexture(
                s, (byte)blend,
                v1.X, v1.Y, v1.Z, v1.U, v1.V, v1.Color,
                v2.X, v2.Y, v2.Z, v2.U, v2.V, v2.Color,
                v3.X, v3.Y, v3.Z, v3.U, v3.V, v3.Color,
                v4.X, v4.Y, v4.Z, v4.U, v4.V, v4.Color))
            {
                case 0:
                    break;
                case 2:
                    ThrowResourceNotFound("纹理", name);
                    break;
                default:
                    ThrowScope();
                    break;
            }
        }

        /// <summary>绘制模型（对应 lstg.Renderer.drawModel）。roll/pitch/yaw 为角度制</summary>
        public static void DrawModel(
            string name,
            float x, float y, float z,
            float roll = 0.0f, float pitch = 0.0f, float yaw = 0.0f,
            float sx = 1.0f, float sy = 1.0f, float sz = 1.0f)
        {
            using var s = new MarshaledString(name);
            if (LuaSTGAPI.api.render_drawModel(s, x, y, z, roll, pitch, yaw, sx, sy, sz) != 0)
            {
                ThrowResourceNotFound("模型", name);
            }
        }
    }
}
