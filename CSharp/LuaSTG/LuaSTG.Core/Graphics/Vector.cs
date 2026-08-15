using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 二维向量（对应 Lua 侧 lstg.Vector2 对象）。
    /// 纯数学类型，直接在托管侧实现，不经过引擎 API；
    /// 字段与方法语义与引擎 core::Vector2&lt;float&gt; 保持一致。
    /// </summary>
    public struct Vector2 : IEquatable<Vector2>
    {
        /// <summary>std::numeric_limits&lt;float&gt;::min()，即最小正正规数</summary>
        private const float MinNormal = 1.17549435e-38f;

        /// <summary>X 分量</summary>
        public float x;
        /// <summary>Y 分量</summary>
        public float y;

        /// <summary>零向量</summary>
        public static Vector2 Zero => default;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>创建向量（对应 lstg.Vector2.create）</summary>
        public static Vector2 Create(float x, float y) => new Vector2(x, y);

        /// <summary>向量长度（对应 lstg.Vector2:length）</summary>
        public readonly float Length => MathF.Sqrt(x * x + y * y);

        /// <summary>向量方向角，弧度（对应 lstg.Vector2:angle，atan2(y, x)）</summary>
        public readonly float Angle => MathF.Atan2(y, x);

        /// <summary>向量方向角，角度制（对应 lstg.Vector2:degreeAngle / Length/Angle 兼容别名）</summary>
        public readonly float AngleDegrees => Angle * (180f / MathF.PI);

        /// <summary>原地向量化为单位向量（对应 lstg.Vector2:normalize）</summary>
        public void Normalize()
        {
            var l = Length;
            if (l >= MinNormal)
            {
                x /= l;
                y /= l;
            }
            else
            {
                x = 0f;
                y = 0f;
            }
        }

        /// <summary>返回单位向量，不改变自身（对应 lstg.Vector2:normalized）</summary>
        public readonly Vector2 Normalized()
        {
            var v = this;
            v.Normalize();
            return v;
        }

        /// <summary>点积（对应 lstg.Vector2:dot）</summary>
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator +(Vector2 a, float s) => new Vector2(a.x + s, a.y + s);
        public static Vector2 operator +(float s, Vector2 a) => new Vector2(s + a.x, s + a.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a, float s) => new Vector2(a.x - s, a.y - s);
        public static Vector2 operator -(float s, Vector2 a) => new Vector2(s - a.x, s - a.y);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator *(float s, Vector2 a) => new Vector2(s * a.x, s * a.y);
        public static Vector2 operator /(Vector2 a, Vector2 b) => new Vector2(a.x / b.x, a.y / b.y);
        public static Vector2 operator /(Vector2 a, float s) => new Vector2(a.x / s, a.y / s);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);

        public readonly bool Equals(Vector2 other) => x == other.x && y == other.y;
        public override readonly bool Equals(object? obj) => obj is Vector2 v && Equals(v);
        public override readonly int GetHashCode() => HashCode.Combine(x, y);
        public static bool operator ==(Vector2 a, Vector2 b) => a.Equals(b);
        public static bool operator !=(Vector2 a, Vector2 b) => !a.Equals(b);

        /// <inheritdoc/>
        public override readonly string ToString() => $"Vector2({x}, {y})";
    }

    /// <summary>
    /// 三维向量（对应 Lua 侧 lstg.Vector3 对象）。
    /// 纯数学类型，语义与引擎 core::Vector3&lt;float&gt; 保持一致。
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        private const float MinNormal = 1.17549435e-38f;

        /// <summary>X 分量</summary>
        public float x;
        /// <summary>Y 分量</summary>
        public float y;
        /// <summary>Z 分量</summary>
        public float z;

        /// <summary>零向量</summary>
        public static Vector3 Zero => default;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>创建向量（对应 lstg.Vector3.create）</summary>
        public static Vector3 Create(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>向量长度（对应 lstg.Vector3:length）</summary>
        public readonly float Length => MathF.Sqrt(x * x + y * y + z * z);

        /// <summary>原地向量化为单位向量（对应 lstg.Vector3:normalize）</summary>
        public void Normalize()
        {
            var l = Length;
            if (l >= MinNormal)
            {
                x /= l;
                y /= l;
                z /= l;
            }
            else
            {
                x = 0f;
                y = 0f;
                z = 0f;
            }
        }

        /// <summary>返回单位向量，不改变自身（对应 lstg.Vector3:normalized）</summary>
        public readonly Vector3 Normalized()
        {
            var v = this;
            v.Normalize();
            return v;
        }

        /// <summary>点积（对应 lstg.Vector3:dot）</summary>
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator +(Vector3 a, float s) => new Vector3(a.x + s, a.y + s, a.z + s);
        public static Vector3 operator +(float s, Vector3 a) => new Vector3(s + a.x, s + a.y, s + a.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a, float s) => new Vector3(a.x - s, a.y - s, a.z - s);
        public static Vector3 operator -(float s, Vector3 a) => new Vector3(s - a.x, s - a.y, s - a.z);
        public static Vector3 operator *(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new Vector3(s * a.x, s * a.y, s * a.z);
        public static Vector3 operator /(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);

        public readonly bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;
        public override readonly bool Equals(object? obj) => obj is Vector3 v && Equals(v);
        public override readonly int GetHashCode() => HashCode.Combine(x, y, z);
        public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
        public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);

        /// <inheritdoc/>
        public override readonly string ToString() => $"Vector3({x}, {y}, {z})";
    }

    /// <summary>
    /// 四维向量（对应 Lua 侧 lstg.Vector4 对象）。
    /// 纯数学类型，语义与引擎 core::Vector4&lt;float&gt; 保持一致。
    /// </summary>
    public struct Vector4 : IEquatable<Vector4>
    {
        private const float MinNormal = 1.17549435e-38f;

        /// <summary>X 分量</summary>
        public float x;
        /// <summary>Y 分量</summary>
        public float y;
        /// <summary>Z 分量</summary>
        public float z;
        /// <summary>W 分量</summary>
        public float w;

        /// <summary>零向量</summary>
        public static Vector4 Zero => default;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        /// <summary>创建向量（对应 lstg.Vector4.create）</summary>
        public static Vector4 Create(float x, float y, float z, float w) => new Vector4(x, y, z, w);

        /// <summary>向量长度（对应 lstg.Vector4:length）</summary>
        public readonly float Length => MathF.Sqrt(x * x + y * y + z * z + w * w);

        /// <summary>原地向量化为单位向量（对应 lstg.Vector4:normalize）</summary>
        public void Normalize()
        {
            var l = Length;
            if (l >= MinNormal)
            {
                x /= l;
                y /= l;
                z /= l;
                w /= l;
            }
            else
            {
                x = 0f;
                y = 0f;
                z = 0f;
                w = 0f;
            }
        }

        /// <summary>返回单位向量，不改变自身（对应 lstg.Vector4:normalized）</summary>
        public readonly Vector4 Normalized()
        {
            var v = this;
            v.Normalize();
            return v;
        }

        /// <summary>点积（对应 lstg.Vector4:dot）</summary>
        public static float Dot(Vector4 a, Vector4 b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        public static Vector4 operator +(Vector4 a, float s) => new Vector4(a.x + s, a.y + s, a.z + s, a.w + s);
        public static Vector4 operator +(float s, Vector4 a) => new Vector4(s + a.x, s + a.y, s + a.z, s + a.w);
        public static Vector4 operator -(Vector4 a, Vector4 b) => new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        public static Vector4 operator -(Vector4 a, float s) => new Vector4(a.x - s, a.y - s, a.z - s, a.w - s);
        public static Vector4 operator -(float s, Vector4 a) => new Vector4(s - a.x, s - a.y, s - a.z, s - a.w);
        public static Vector4 operator *(Vector4 a, Vector4 b) => new Vector4(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
        public static Vector4 operator *(Vector4 a, float s) => new Vector4(a.x * s, a.y * s, a.z * s, a.w * s);
        public static Vector4 operator *(float s, Vector4 a) => new Vector4(s * a.x, s * a.y, s * a.z, s * a.w);
        public static Vector4 operator /(Vector4 a, Vector4 b) => new Vector4(a.x / b.x, a.y / b.y, a.z / b.z, a.w / b.w);
        public static Vector4 operator /(Vector4 a, float s) => new Vector4(a.x / s, a.y / s, a.z / s, a.w / s);
        public static Vector4 operator -(Vector4 a) => new Vector4(-a.x, -a.y, -a.z, -a.w);

        public readonly bool Equals(Vector4 other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override readonly bool Equals(object? obj) => obj is Vector4 v && Equals(v);
        public override readonly int GetHashCode() => HashCode.Combine(x, y, z, w);
        public static bool operator ==(Vector4 a, Vector4 b) => a.Equals(b);
        public static bool operator !=(Vector4 a, Vector4 b) => !a.Equals(b);

        /// <inheritdoc/>
        public override readonly string ToString() => $"Vector4({x}, {y}, {z}, {w})";
    }
}
