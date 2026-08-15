using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 颜色（对应 Lua 侧 lstg.Color，与引擎 core::Color4B 内存布局一致，存储为 0xAARRGGBB）。
    /// 值类型，无生命周期；HSV 分量与 Lua 侧一致使用 0~100 刻度。
    /// </summary>
    public readonly struct Color : IEquatable<Color>
    {
        /// <summary>0xAARRGGBB</summary>
        public readonly uint Argb;

        public Color(uint argb)
        {
            Argb = argb;
        }

        public Color(byte a, byte r, byte g, byte b)
        {
            Argb = (uint)(a << 24 | r << 16 | g << 8 | b);
        }

        /// <summary>由 ARGB 整数构造（对应 lstg.Color(argb)）</summary>
        public static Color FromArgb(uint argb) => new(argb);

        /// <summary>由分量构造（对应 lstg.Color(a, r, g, b)）</summary>
        public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);

        /// <summary>由 AHSV 构造（对应 lstg.AHSVColor(a, h, s, v)，各分量 0~100）</summary>
        public static Color FromAHSV(double a, double h, double s, double v)
        {
            return HsvToRgb(ClampUnit(h), ClampUnit(s), ClampUnit(v), ClampUnit(a));
        }

        public byte A => (byte)(Argb >> 24);
        public byte R => (byte)(Argb >> 16);
        public byte G => (byte)(Argb >> 8);
        public byte B => (byte)Argb;

        private static double ClampUnit(double v100) => Math.Clamp(v100, 0.0, 100.0) / 100.0;

        /// <summary>色相（0~100，对应 Lua 属性 h）</summary>
        public double H => RgbToHsv(R / 255.0, G / 255.0, B / 255.0).H * 100.0;

        /// <summary>饱和度（0~100，对应 Lua 属性 s）</summary>
        public double S => RgbToHsv(R / 255.0, G / 255.0, B / 255.0).S * 100.0;

        /// <summary>明度（0~100，对应 Lua 属性 v）</summary>
        public double V => RgbToHsv(R / 255.0, G / 255.0, B / 255.0).V * 100.0;

        /// <summary>同时读取 ARGB（对应 Lua 的 ARGB() 无参形式）</summary>
        public (byte A, byte R, byte G, byte B) GetARGB() => (A, R, G, B);

        /// <summary>同时读取 AHSV（0~100，对应 Lua 的 AHSV() 无参形式）</summary>
        public (double A, double H, double S, double V) GetAHSV()
        {
            var hsv = RgbToHsv(R / 255.0, G / 255.0, B / 255.0);
            return (A / 255.0 * 100.0, hsv.H * 100.0, hsv.S * 100.0, hsv.V * 100.0);
        }

        /// <summary>按 ARGB 设置（对应 Lua 的 ARGB(a,r,g,b) / ARGB(argb)）</summary>
        public Color SetARGB(byte a, byte r, byte g, byte b) => new(a, r, g, b);

        /// <summary>按 AHSV 设置（0~100，对应 Lua 的 AHSV(a,h,s,v)）</summary>
        public Color SetAHSV(double a, double h, double s, double v)
        {
            return HsvToRgb(ClampUnit(h), ClampUnit(s), ClampUnit(v), ClampUnit(a));
        }

        // ========== HSV 换算（与 DirectXMath XMColorHSVToRGB/XMColorRGBToHSV 一致） ==========

        private readonly record struct Hsv(double H, double S, double V);

        private static Color HsvToRgb(double h, double s, double v, double a)
        {
            var h6 = h * 6.0;
            var i = Math.Floor(h6);
            var f = h6 - i;
            var ii = (int)(i % 6);

            var p = v * (1.0 - s);
            var q = v * (1.0 - f * s);
            var t = v * (1.0 - (1.0 - f) * s);

            double r, g, b;
            switch (ii)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return new Color(
                (byte)Math.Round(a * 255.0),
                (byte)Math.Round(r * 255.0),
                (byte)Math.Round(g * 255.0),
                (byte)Math.Round(b * 255.0)
            );
        }

        private static Hsv RgbToHsv(double r, double g, double b)
        {
            const double epsilon = 1.192092896e-07; // g_XMEpsilon
            var min = Math.Min(r, Math.Min(g, b));
            var v = Math.Max(r, Math.Max(g, b));
            var d = v - min;
            var s = Math.Abs(v) <= epsilon ? 0.0 : d / v;
            if (d < epsilon)
            {
                return new Hsv(0.0, s, v);
            }
            double h;
            if (r == v)
            {
                h = (g - b) / d;
                if (g < b)
                {
                    h += 6.0;
                }
            }
            else if (g == v)
            {
                h = (b - r) / d + 2.0;
            }
            else
            {
                h = (r - g) / d + 4.0;
            }
            return new Hsv(h / 6.0, s, v);
        }

        // ========== 运算符（与 Lua 侧语义一致：分量运算并钳制到 0~255） ==========

        public static Color operator +(Color left, Color right) => new(
            (byte)Math.Clamp(left.A + right.A, 0, 255),
            (byte)Math.Clamp(left.R + right.R, 0, 255),
            (byte)Math.Clamp(left.G + right.G, 0, 255),
            (byte)Math.Clamp(left.B + right.B, 0, 255));

        public static Color operator +(Color left, double right) => new(
            (byte)Math.Clamp(left.A + right, 0.0, 255.0),
            (byte)Math.Clamp(left.R + right, 0.0, 255.0),
            (byte)Math.Clamp(left.G + right, 0.0, 255.0),
            (byte)Math.Clamp(left.B + right, 0.0, 255.0));

        public static Color operator +(double left, Color right) => right + left;

        public static Color operator -(Color left, Color right) => new(
            (byte)Math.Clamp(left.A - right.A, 0, 255),
            (byte)Math.Clamp(left.R - right.R, 0, 255),
            (byte)Math.Clamp(left.G - right.G, 0, 255),
            (byte)Math.Clamp(left.B - right.B, 0, 255));

        public static Color operator -(Color left, double right) => new(
            (byte)Math.Clamp(left.A - right, 0.0, 255.0),
            (byte)Math.Clamp(left.R - right, 0.0, 255.0),
            (byte)Math.Clamp(left.G - right, 0.0, 255.0),
            (byte)Math.Clamp(left.B - right, 0.0, 255.0));

        public static Color operator -(double left, Color right) => new(
            (byte)Math.Clamp(left - right.A, 0.0, 255.0),
            (byte)Math.Clamp(left - right.R, 0.0, 255.0),
            (byte)Math.Clamp(left - right.G, 0.0, 255.0),
            (byte)Math.Clamp(left - right.B, 0.0, 255.0));

        public static Color operator *(Color left, Color right) => new(
            (byte)Math.Clamp((double)left.A * right.A, 0.0, 255.0),
            (byte)Math.Clamp((double)left.R * right.R, 0.0, 255.0),
            (byte)Math.Clamp((double)left.G * right.G, 0.0, 255.0),
            (byte)Math.Clamp((double)left.B * right.B, 0.0, 255.0));

        public static Color operator *(Color left, double right) => new(
            (byte)Math.Clamp(left.A * right, 0.0, 255.0),
            (byte)Math.Clamp(left.R * right, 0.0, 255.0),
            (byte)Math.Clamp(left.G * right, 0.0, 255.0),
            (byte)Math.Clamp(left.B * right, 0.0, 255.0));

        public static Color operator *(double left, Color right) => right * left;

        public static Color operator /(Color left, Color right) => new(
            (byte)Math.Clamp(left.A / (double)right.A, 0.0, 255.0),
            (byte)Math.Clamp(left.R / (double)right.R, 0.0, 255.0),
            (byte)Math.Clamp(left.G / (double)right.G, 0.0, 255.0),
            (byte)Math.Clamp(left.B / (double)right.B, 0.0, 255.0));

        public static Color operator /(Color left, double right) => new(
            (byte)Math.Clamp(left.A / right, 0.0, 255.0),
            (byte)Math.Clamp(left.R / right, 0.0, 255.0),
            (byte)Math.Clamp(left.G / right, 0.0, 255.0),
            (byte)Math.Clamp(left.B / right, 0.0, 255.0));

        public static Color operator /(double left, Color right) => new(
            (byte)Math.Clamp(left / (double)right.A, 0.0, 255.0),
            (byte)Math.Clamp(left / (double)right.R, 0.0, 255.0),
            (byte)Math.Clamp(left / (double)right.G, 0.0, 255.0),
            (byte)Math.Clamp(left / (double)right.B, 0.0, 255.0));

        public static bool operator ==(Color left, Color right) => left.Argb == right.Argb;
        public static bool operator !=(Color left, Color right) => left.Argb != right.Argb;

        public bool Equals(Color other) => Argb == other.Argb;

        public override bool Equals(object? obj) => obj is Color other && Equals(other);

        public override int GetHashCode() => (int)Argb;

        public override string ToString() => $"Color(A={A}, R={R}, G={G}, B={B})";

        public static Color White { get; } = new(255, 255, 255, 255);
        public static Color Black { get; } = new(255, 0, 0, 0);
        public static Color TransparentWhite { get; } = new(0, 255, 255, 255);
        public static Color TransparentBlack { get; } = new(0, 0, 0, 0);

        public static implicit operator uint(Color color) => color.Argb;
    }
}
