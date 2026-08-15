using System;
using System.Runtime.InteropServices;

namespace LuaSTG.Core
{
    /// <summary>
    /// 引擎侧 GameObject 布局描述（与 C++ 侧 CLRGameObjectLayoutInfo 保持一致），
    /// 由引擎在启动时提供，用于校验 C# 侧结构体覆写的正确性。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CLRGameObjectLayoutInfo
    {
        public uint api_version;
        public uint struct_size;
        public uint pool_size;
        public uint group_count;
        public uint feature_flags;
        public uint offset_id;
        public uint offset_last_x;
        public uint offset_last_y;
        public uint offset_x;
        public uint offset_y;
        public uint offset_dx;
        public uint offset_dy;
        public uint offset_vx;
        public uint offset_vy;
        public uint offset_ax;
        public uint offset_ay;
        public uint offset_max_vx;
        public uint offset_max_vy;
        public uint offset_max_v;
        public uint offset_ag;
        public uint offset_group;
        public uint offset_a;
        public uint offset_b;
        public uint offset_col_r;
        public uint offset_layer;
        public uint offset_hscale;
        public uint offset_vscale;
        public uint offset_rot;
        public uint offset_omega;
        public uint offset_ani_timer;
        public uint offset_res;
        public uint offset_ps;
        public uint offset_timer;
        public uint offset_vertex_color;
        public uint offset_blend_mode;
        public uint offset_features;
        public uint offset_status;
        public uint offset_flags;
    }

    /// <summary>
    /// 引擎 GameObject 的内存覆写（overlay）。
    /// 引擎数据通过此结构直接读写，与 Lua 侧始终同步、零调用开销。
    /// 布局校验见 <see cref="NativeGameObject.Validate"/>。
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 280)]
    internal unsafe struct NativeGameObject
    {
        // 0~15: callbacks 链（C# 不访问）
        // 16~47: 链表指针（C# 不访问）

        /// <summary>id(低16位) + unique_id(高48位)</summary>
        [FieldOffset(48)] private ulong _idUid;

        [FieldOffset(56)] public double last_x;
        [FieldOffset(64)] public double last_y;
        [FieldOffset(72)] public double x;
        [FieldOffset(80)] public double y;
        [FieldOffset(88)] public double dx;
        [FieldOffset(96)] public double dy;
        [FieldOffset(104)] public double vx;
        [FieldOffset(112)] public double vy;
        [FieldOffset(120)] public double ax;
        [FieldOffset(128)] public double ay;
        [FieldOffset(136)] public double max_vx;
        [FieldOffset(144)] public double max_vy;
        [FieldOffset(152)] public double max_v;
        [FieldOffset(160)] public double ag;
        [FieldOffset(168)] public long group;
        [FieldOffset(176)] public double a;
        [FieldOffset(184)] public double b;
        [FieldOffset(192)] public double col_r;
        [FieldOffset(200)] public double layer;
        [FieldOffset(208)] public double hscale;
        [FieldOffset(216)] public double vscale;
        [FieldOffset(224)] public double rot;     // 引擎内部为弧度
        [FieldOffset(232)] public double omega;   // 引擎内部为弧度
        [FieldOffset(240)] public long ani_timer;
        [FieldOffset(248)] private nint _res;
        [FieldOffset(256)] private nint _ps;
        [FieldOffset(264)] public long timer;
        [FieldOffset(272)] private uint _vertexColor; // 0xAARRGGBB（内存字节序 b,g,r,a）
        [FieldOffset(276)] public byte blend_mode;
        [FieldOffset(277)] public byte features;
        [FieldOffset(278)] public byte status;
        /// <summary>位标志：bit0=bound bit1=colli bit2=rect bit3=hide bit4=navi bit5=ignore_super_pause bit6=last_xy_touched</summary>
        [FieldOffset(279)] public byte flags;

        public const uint ExpectedSize = 280;
        public const byte OffsetLastX = 56;
        public const byte OffsetX = 72;
        public const byte OffsetVx = 104;
        public const byte OffsetGroup = 168;
        public const byte OffsetLayer = 200;
        public const byte OffsetRot = 224;
        public const ushort OffsetTimer = 264;
        public const ushort OffsetVertexColor = 272;
        public const ushort OffsetFlags = 279;

        public readonly uint Id => (uint)(_idUid & 0xFFFF);
        public readonly ulong UniqueId => _idUid >> 16;

        public readonly bool HasRenderResource => _res != 0;
        public readonly bool HasParticlePool => _ps != 0;

        // 顶点颜色（ARGB）
        public uint VertexColor
        {
            readonly get => _vertexColor;
            set => _vertexColor = value;
        }
        public readonly byte ColorA => (byte)(_vertexColor >> 24);
        public readonly byte ColorR => (byte)(_vertexColor >> 16);
        public readonly byte ColorG => (byte)(_vertexColor >> 8);
        public readonly byte ColorB => (byte)_vertexColor;

        // 布尔标志位
        private const byte FlagsBound = 1 << 0;
        private const byte FlagsColli = 1 << 1;
        private const byte FlagsRect = 1 << 2;
        private const byte FlagsHide = 1 << 3;
        private const byte FlagsNavi = 1 << 4;
        private const byte FlagsIgnoreSuperPause = 1 << 5;
        private const byte FlagsLastXYTouched = 1 << 6;

        public bool Bound
        {
            readonly get => (flags & FlagsBound) != 0;
            set { if (value) flags |= FlagsBound; else flags &= unchecked((byte)~FlagsBound); }
        }
        public bool Colli
        {
            readonly get => (flags & FlagsColli) != 0;
            set { if (value) flags |= FlagsColli; else flags &= unchecked((byte)~FlagsColli); }
        }
        public bool Rect
        {
            readonly get => (flags & FlagsRect) != 0;
            set { if (value) flags |= FlagsRect; else flags &= unchecked((byte)~FlagsRect); }
        }
        public bool Hide
        {
            readonly get => (flags & FlagsHide) != 0;
            set { if (value) flags |= FlagsHide; else flags &= unchecked((byte)~FlagsHide); }
        }
        public bool Navi
        {
            readonly get => (flags & FlagsNavi) != 0;
            set { if (value) flags |= FlagsNavi; else flags &= unchecked((byte)~FlagsNavi); }
        }
        public bool IgnoreSuperPause
        {
            readonly get => (flags & FlagsIgnoreSuperPause) != 0;
            set { if (value) flags |= FlagsIgnoreSuperPause; else flags &= unchecked((byte)~FlagsIgnoreSuperPause); }
        }
        public bool LastXYTouched
        {
            readonly get => (flags & FlagsLastXYTouched) != 0;
            set { if (value) flags |= FlagsLastXYTouched; else flags &= unchecked((byte)~FlagsLastXYTouched); }
        }

        /// <summary>碰撞体外接圆半径（与引擎 UpdateCollisionCircleRadius 保持一致）</summary>
        public void UpdateCollisionCircleRadius()
        {
            if (Rect)
            {
                col_r = Math.Sqrt(a * a + b * b);
            }
            else if (a > b)
            {
                col_r = a;
            }
            else
            {
                col_r = b;
            }
        }

        /// <summary>校验引擎侧布局与本结构体一致，不一致抛出异常</summary>
        internal static void Validate(in CLRGameObjectLayoutInfo info)
        {
            static void Check(bool ok, string what)
            {
                if (!ok)
                {
                    throw new InvalidOperationException($"GameObject 布局校验失败：{what}");
                }
            }
            Check(info.api_version == 1, "api_version");
            Check(info.struct_size == ExpectedSize, $"sizeof(GameObject) {info.struct_size} != {ExpectedSize}");
            Check(info.offset_id == 48, "offset id");
            Check(info.offset_last_x == OffsetLastX, "offset last_x");
            Check(info.offset_x == OffsetX, "offset x");
            Check(info.offset_vx == OffsetVx, "offset vx");
            Check(info.offset_group == OffsetGroup, "offset group");
            Check(info.offset_layer == OffsetLayer, "offset layer");
            Check(info.offset_rot == OffsetRot, "offset rot");
            Check(info.offset_timer == OffsetTimer, "offset timer");
            Check(info.offset_vertex_color == OffsetVertexColor, "offset vertex_color");
            Check(info.offset_flags == OffsetFlags, "offset flags");
            Check(info.offset_last_y == 64, "offset last_y");
            Check(info.offset_y == 80, "offset y");
            Check(info.offset_dx == 88, "offset dx");
            Check(info.offset_dy == 96, "offset dy");
            Check(info.offset_vy == 112, "offset vy");
            Check(info.offset_ax == 120, "offset ax");
            Check(info.offset_ay == 128, "offset ay");
            Check(info.offset_max_vx == 136, "offset max_vx");
            Check(info.offset_max_vy == 144, "offset max_vy");
            Check(info.offset_max_v == 152, "offset max_v");
            Check(info.offset_ag == 160, "offset ag");
            Check(info.offset_a == 176, "offset a");
            Check(info.offset_b == 184, "offset b");
            Check(info.offset_col_r == 192, "offset col_r");
            Check(info.offset_hscale == 208, "offset hscale");
            Check(info.offset_vscale == 216, "offset vscale");
            Check(info.offset_omega == 232, "offset omega");
            Check(info.offset_ani_timer == 240, "offset ani_timer");
            Check(info.offset_res == 248, "offset res");
            Check(info.offset_ps == 256, "offset ps");
            Check(info.offset_blend_mode == 276, "offset blend_mode");
            Check(info.offset_features == 277, "offset features");
            Check(info.offset_status == 278, "offset status");
        }
    }
}
