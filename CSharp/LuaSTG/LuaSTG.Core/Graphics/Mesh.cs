using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 图元拓扑（对应引擎 core::Graphics::PrimitiveTopology 与 Lua 侧 lstg.PrimitiveTopology 表）。
    /// </summary>
    public enum PrimitiveTopology : byte
    {
        /// <summary>三角形列表（lstg.PrimitiveTopology.triangle_list）</summary>
        TriangleList = 4,
        /// <summary>三角形条带（lstg.PrimitiveTopology.triangle_strip）</summary>
        TriangleStrip = 5,
    }

    /// <summary>
    /// 网格创建参数（对应 Lua 侧 lstg.Mesh.create 的参数表）。
    /// </summary>
    public sealed class MeshOptions
    {
        /// <summary>顶点数量（Lua 键 vertex_count，必填）</summary>
        public uint VertexCount { get; set; }
        /// <summary>索引数量（Lua 键 index_count，默认 0）</summary>
        public uint IndexCount { get; set; }
        /// <summary>顶点位置不含 Z 分量（Lua 键 vertex_position_no_z，默认 false）</summary>
        public bool VertexPositionNoZ { get; set; }
        /// <summary>索引压缩为 uint16（Lua 键 vertex_index_compression，默认 true）</summary>
        public bool VertexIndexCompression { get; set; } = true;
        /// <summary>顶点色压缩为 BGRA8（Lua 键 vertex_color_compression，默认 true）</summary>
        public bool VertexColorCompression { get; set; } = true;
        /// <summary>图元拓扑（Lua 键 primitive_topology，默认三角形列表）</summary>
        public PrimitiveTopology PrimitiveTopology { get; set; } = PrimitiveTopology.TriangleList;
    }

    /// <summary>
    /// 网格数据（对应 Lua 侧 lstg.Mesh）。
    /// 通过 <see cref="Create(MeshOptions)"/> 工厂创建；设置顶点/索引数据后需 <see cref="Commit"/> 提交，
    /// 提交后可 <see cref="SetReadOnly"/> 锁定；<see cref="Dispose"/> 销毁。
    /// </summary>
    public sealed unsafe class Mesh : ModernGraphicsObject
    {
        internal Mesh(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建网格（对应 lstg.Mesh.create）。
        /// </summary>
        /// <exception cref="ArgumentNullException">options 为 null</exception>
        /// <exception cref="ArgumentException">VertexCount 为 0（Lua 侧要求 vertex_count 必填）</exception>
        /// <exception cref="InvalidOperationException">创建失败，详见引擎日志</exception>
        public static Mesh Create(MeshOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.VertexCount == 0)
            {
                throw new ArgumentException("必须指定 VertexCount（对应 Lua 键 vertex_count）", nameof(options));
            }
            var handle = LuaSTGAPI.api.mg_meshCreate(
                options.VertexCount,
                options.IndexCount,
                (byte)(options.VertexPositionNoZ ? 1 : 0),
                (byte)(options.VertexIndexCompression ? 1 : 0),
                (byte)(options.VertexColorCompression ? 1 : 0),
                (byte)options.PrimitiveTopology);
            return handle != 0
                ? new Mesh((nint)handle)
                : throw new InvalidOperationException("创建 Mesh 失败，详见引擎日志");
        }

        /// <summary>
        /// 创建网格（对应遗留构造 lstg.MeshData(vertex_count, index_count)，其余参数取默认值）。
        /// </summary>
        public static Mesh Create(uint vertexCount, uint indexCount)
            => Create(new MeshOptions { VertexCount = vertexCount, IndexCount = indexCount });

        /// <summary>顶点数量（对应 lstg.Mesh:getVertexCount）</summary>
        public uint VertexCount
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_meshGetVertexCount((nuint)Handle); }
        }

        /// <summary>索引数量（对应 lstg.Mesh:getIndexCount）</summary>
        public uint IndexCount
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_meshGetIndexCount((nuint)Handle); }
        }

        /// <summary>图元拓扑（对应 lstg.Mesh:getPrimitiveTopology）</summary>
        public PrimitiveTopology PrimitiveTopology
        {
            get { ThrowIfDisposed(); return (PrimitiveTopology)LuaSTGAPI.api.mg_meshGetPrimitiveTopology((nuint)Handle); }
        }

        /// <summary>是否已锁定为只读（对应 lstg.Mesh:isReadOnly）</summary>
        public bool IsReadOnly
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_meshIsReadOnly((nuint)Handle) != 0; }
        }

        /// <summary>设置单个顶点（对应 lstg.Mesh:setVertex 3D 位置 + ARGB 颜色重载）</summary>
        /// <param name="argb">顶点色（ARGB，0xAARRGGBB）</param>
        public void SetVertex(uint vertexIndex, float x, float y, float z, float u, float v, uint argb)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetVertex3((nuint)Handle, vertexIndex, x, y, z, u, v, argb);
        }

        /// <summary>设置单个顶点（对应 lstg.Mesh:setVertex 3D 位置 + RGBA 浮点颜色重载）</summary>
        public void SetVertex(uint vertexIndex, float x, float y, float z, float u, float v, float r, float g, float b, float a)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetVertex3F((nuint)Handle, vertexIndex, x, y, z, u, v, r, g, b, a);
        }

        /// <summary>设置单个顶点（对应 lstg.Mesh:setVertex 2D 位置 + ARGB 颜色重载）</summary>
        /// <param name="argb">顶点色（ARGB，0xAARRGGBB）</param>
        public void SetVertex(uint vertexIndex, float x, float y, float u, float v, uint argb)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetVertex2((nuint)Handle, vertexIndex, x, y, u, v, argb);
        }

        /// <summary>设置单个顶点（对应 lstg.Mesh:setVertex 2D 位置 + RGBA 浮点颜色重载）</summary>
        public void SetVertex(uint vertexIndex, float x, float y, float u, float v, float r, float g, float b, float a)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetVertex2F((nuint)Handle, vertexIndex, x, y, u, v, r, g, b, a);
        }

        /// <summary>设置顶点位置（对应 lstg.Mesh:setPosition / 遗留别名 setVertexPosition，3D）</summary>
        public void SetPosition(uint vertexIndex, float x, float y, float z)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetPosition3((nuint)Handle, vertexIndex, x, y, z);
        }

        /// <summary>设置顶点位置（对应 lstg.Mesh:setPosition 2 参数形式）</summary>
        public void SetPosition(uint vertexIndex, float x, float y)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetPosition2((nuint)Handle, vertexIndex, x, y);
        }

        /// <summary>设置顶点纹理坐标（对应 lstg.Mesh:setUv / 遗留别名 setVertexCoords）</summary>
        public void SetUv(uint vertexIndex, float u, float v)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetUv((nuint)Handle, vertexIndex, u, v);
        }

        /// <summary>设置顶点颜色（对应 lstg.Mesh:setColor ARGB 重载 / 遗留别名 setVertexColor）</summary>
        /// <param name="argb">顶点色（ARGB，0xAARRGGBB）</param>
        public void SetColor(uint vertexIndex, uint argb)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetColor((nuint)Handle, vertexIndex, argb);
        }

        /// <summary>设置顶点颜色（对应 lstg.Mesh:setColor RGBA 浮点重载）</summary>
        public void SetColor(uint vertexIndex, float r, float g, float b, float a)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetColorF((nuint)Handle, vertexIndex, r, g, b, a);
        }

        /// <summary>设置所有顶点颜色（对应遗留方法 lstg.Mesh:setAllVertexColor）</summary>
        /// <param name="argb">顶点色（ARGB，0xAARRGGBB）</param>
        public void SetAllVertexColor(uint argb)
        {
            ThrowIfDisposed();
            var count = LuaSTGAPI.api.mg_meshGetVertexCount((nuint)Handle);
            for (uint i = 0; i < count; i++)
            {
                LuaSTGAPI.api.mg_meshSetColor((nuint)Handle, i, argb);
            }
        }

        /// <summary>设置索引（对应 lstg.Mesh:setIndex）</summary>
        /// <param name="indexIndex">索引下标</param>
        /// <param name="vertexIndex">指向的顶点下标</param>
        public void SetIndex(uint indexIndex, uint vertexIndex)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetIndex((nuint)Handle, indexIndex, vertexIndex);
        }

        /// <summary>提交网格数据（对应 lstg.Mesh:commit），返回是否成功</summary>
        public bool Commit()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.mg_meshCommit((nuint)Handle) != 0;
        }

        /// <summary>锁定网格为只读（对应 lstg.Mesh:setReadOnly）</summary>
        public void SetReadOnly()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_meshSetReadOnly((nuint)Handle);
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_meshRelease((nuint)Handle);
        }
    }
}
