using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 字体渲染器 API（对应 Lua 侧 lstg.FontRenderer 库，见 LW_FileManager.cpp；
    /// 引擎实现在 AppFrameFontRenderer.cpp 的 FontRenderer_* 公开方法）。
    /// </summary>
    public static unsafe partial class FontRenderer
    {
        /// <summary>
        /// 设置字体提供者（对应 lstg.FontRenderer.SetFontProvider）。
        /// </summary>
        /// <param name="name">字体资源（TTF）名称</param>
        /// <returns>是否成功（字体资源不存在时为 false，引擎侧同时记录错误日志）</returns>
        public static bool SetFontProvider(string name)
        {
            using var s = new MarshaledString(name);
            return LuaSTGAPI.api.fontRenderer_setFontProvider(s) != 0;
        }

        /// <summary>设置文本缩放（对应 lstg.FontRenderer.SetScale）</summary>
        public static void SetScale(float x, float y)
            => LuaSTGAPI.api.fontRenderer_setScale(x, y);

        /// <summary>
        /// 测量文本边界（对应 lstg.FontRenderer.MeasureTextBoundary，
        /// 返回值顺序与 Lua 侧一致：v.a.x / v.b.x / v.b.y / v.a.y）。
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="left">边界左侧</param>
        /// <param name="right">边界右侧</param>
        /// <param name="bottom">边界下侧</param>
        /// <param name="top">边界上侧</param>
        public static void MeasureTextBoundary(string text, out float left, out float right, out float bottom, out float top)
        {
            double l = 0.0, r = 0.0, b = 0.0, t = 0.0;
            using var s = new MarshaledString(text);
            LuaSTGAPI.api.fontRenderer_measureTextBoundary(s, &l, &r, &b, &t);
            left = (float)l;
            right = (float)r;
            bottom = (float)b;
            top = (float)t;
        }

        /// <summary>
        /// 测量文本步进（对应 lstg.FontRenderer.MeasureTextAdvance）。
        /// </summary>
        public static void MeasureTextAdvance(string text, out float x, out float y)
        {
            double ox = 0.0, oy = 0.0;
            using var s = new MarshaledString(text);
            LuaSTGAPI.api.fontRenderer_measureTextAdvance(s, &ox, &oy);
            x = (float)ox;
            y = (float)oy;
        }

        /// <summary>
        /// 渲染文本（对应 lstg.FontRenderer.RenderText）。
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="x">起点 X</param>
        /// <param name="y">起点 Y</param>
        /// <param name="z">深度</param>
        /// <param name="blend">混合模式</param>
        /// <param name="color">颜色，0xAARRGGBB</param>
        /// <param name="endX">渲染结束位置 X（对应 Lua 侧第 2 个返回值）</param>
        /// <param name="endY">渲染结束位置 Y（对应 Lua 侧第 3 个返回值）</param>
        /// <returns>是否成功</returns>
        public static bool RenderText(string text, float x, float y, float z, BlendMode blend, uint color, out float endX, out float endY)
        {
            double ox = x, oy = y;
            using var s = new MarshaledString(text);
            var result = LuaSTGAPI.api.fontRenderer_renderText(s, x, y, z, (byte)blend, color, &ox, &oy);
            endX = (float)ox;
            endY = (float)oy;
            return result != 0;
        }

        /// <summary>
        /// 在三维空间中渲染文本（对应 lstg.FontRenderer.RenderTextInSpace）。
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="x">起点 X</param>
        /// <param name="y">起点 Y</param>
        /// <param name="z">起点 Z</param>
        /// <param name="rx">右向量 X</param>
        /// <param name="ry">右向量 Y</param>
        /// <param name="rz">右向量 Z</param>
        /// <param name="dx">下向量 X</param>
        /// <param name="dy">下向量 Y</param>
        /// <param name="dz">下向量 Z</param>
        /// <param name="blend">混合模式</param>
        /// <param name="color">颜色，0xAARRGGBB</param>
        /// <param name="endX">渲染结束位置 X（对应 Lua 侧第 2 个返回值）</param>
        /// <param name="endY">渲染结束位置 Y（对应 Lua 侧第 3 个返回值）</param>
        /// <param name="endZ">渲染结束位置 Z（对应 Lua 侧第 4 个返回值）</param>
        /// <returns>是否成功</returns>
        public static bool RenderTextInSpace(string text, float x, float y, float z,
            float rx, float ry, float rz, float dx, float dy, float dz,
            BlendMode blend, uint color,
            out float endX, out float endY, out float endZ)
        {
            double ox = x, oy = y, oz = z;
            using var s = new MarshaledString(text);
            var result = LuaSTGAPI.api.fontRenderer_renderTextInSpace(
                s, x, y, z, rx, ry, rz, dx, dy, dz, (byte)blend, color, &ox, &oy, &oz);
            endX = (float)ox;
            endY = (float)oy;
            endZ = (float)oz;
            return result != 0;
        }

        /// <summary>获取字体行高（对应 lstg.FontRenderer.GetFontLineHeight）</summary>
        public static float GetFontLineHeight()
            => LuaSTGAPI.api.fontRenderer_getFontLineHeight();

        /// <summary>获取字体上升部（对应 lstg.FontRenderer.GetFontAscender）</summary>
        public static float GetFontAscender()
            => LuaSTGAPI.api.fontRenderer_getFontAscender();

        /// <summary>获取字体下降部（对应 lstg.FontRenderer.GetFontDescender）</summary>
        public static float GetFontDescender()
            => LuaSTGAPI.api.fontRenderer_getFontDescender();
    }
}
