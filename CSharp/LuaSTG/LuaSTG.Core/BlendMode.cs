namespace LuaSTG.Core
{
    /// <summary>
    /// 混合模式（与引擎侧 BlendMode 保持一致）。
    /// </summary>
    public enum BlendMode : byte
    {
        Unknown = 0,

        /// <summary>顶点色和纹理色相乘，透明度混合（正常）</summary>
        MulAlpha = 1,
        /// <summary>顶点色和纹理色相乘，线性减淡（加法）</summary>
        MulAdd = 2,
        /// <summary>顶点色和纹理色相乘，减去（减法）</summary>
        MulRev = 3,
        /// <summary>顶点色和纹理色相乘，图片被底图减</summary>
        MulSub = 4,
        /// <summary>顶点色和纹理色相加，透明度混合（正常）</summary>
        AddAlpha = 5,
        /// <summary>顶点色和纹理色相加，线性减淡（加法）</summary>
        AddAdd = 6,
        /// <summary>顶点色和纹理色相加，减去（减法）</summary>
        AddRev = 7,
        /// <summary>顶点色和纹理色相加，图片被底图减</summary>
        AddSub = 8,

        /// <summary>顶点色和纹理色相乘，反色</summary>
        AlphaBal = 9,

        /// <summary>顶点色和纹理色相乘，变暗（取小）</summary>
        MulMin = 10,
        /// <summary>顶点色和纹理色相乘，变亮（取大）</summary>
        MulMax = 11,
        /// <summary>顶点色和纹理色相乘，正片叠底（相乘）</summary>
        MulMutiply = 12,
        /// <summary>顶点色和纹理色相乘，滤色（相加减去相乘）</summary>
        MulScreen = 13,
        /// <summary>顶点色和纹理色相加，变暗（取小）</summary>
        AddMin = 14,
        /// <summary>顶点色和纹理色相加，变亮（取大）</summary>
        AddMax = 15,
        /// <summary>顶点色和纹理色相加，正片叠底（相乘）</summary>
        AddMutiply = 16,
        /// <summary>顶点色和纹理色相加，滤色（相加减去相乘）</summary>
        AddScreen = 17,

        /// <summary>无混合，直接覆盖</summary>
        One = 18,
    }
}
