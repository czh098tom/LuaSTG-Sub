using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 曲线激光数据（对应 Lua 侧 lstg.CurveLaser，见 LuaBinding/LW_BentLaser.cpp 与
    /// GameObject/GameObjectBentLaser.hpp/.cpp）。
    /// 无参构造在引擎侧分配 GameObjectBentLaserData，
    /// <see cref="Dispose"/>（或 <see cref="Release"/>）销毁之；
    /// 销毁后访问会抛出 <see cref="ObjectDisposedException"/>。
    /// 节点数量上限为 512（引擎 LGOBJ_MAXLASERNODE）。
    /// </summary>
    /// <remarks>
    /// 与 Lua 侧的差异：
    /// - <see cref="UpdateNode(int, double, double, double)"/> 的节点索引从 0 开始（Lua 侧从 1 开始）；
    /// - 对象形式的 <c>Update</c>/<c>UpdateNode</c> 的 active 参数按语义直接传递
    ///   （Lua 绑定中存在 `optnumber(5, 0) == 0` 的反置缺陷，未在此复刻）；
    /// - <c>Release</c> 在 Lua 侧是空操作，此处按析构语义直接销毁。
    /// </remarks>
    public sealed unsafe class BentLaserData : IDisposable
    {
        /// <summary>引擎侧曲线激光句柄，0 表示已销毁</summary>
        private nuint _handle;

        /// <summary>创建曲线激光数据</summary>
        /// <exception cref="OutOfMemoryException">引擎侧对象分配失败</exception>
        public BentLaserData()
        {
            _handle = LuaSTGAPI.api.bentLaser_create();
            if (_handle == 0)
            {
                throw new OutOfMemoryException("BentLaserData 分配失败");
            }
        }

        /// <summary>对象是否仍然有效（未被销毁）</summary>
        public bool IsValid => _handle != 0;

        /// <summary>销毁曲线激光数据（对应 Lua 侧 userdata 回收）</summary>
        public void Dispose() => Release();

        /// <summary>销毁曲线激光数据，等价于 <see cref="Dispose"/>（对应 Lua 侧方法名 Release）</summary>
        public void Release()
        {
            if (_handle != 0)
            {
                LuaSTGAPI.api.bentLaser_destroy(_handle);
                _handle = 0;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_handle == 0)
            {
                throw new ObjectDisposedException(nameof(BentLaserData), "BentLaserData 已释放");
            }
        }

        private static byte ToByte(bool value) => value ? (byte)1 : (byte)0;

        // ========== 更新 ==========

        /// <summary>
        /// 根据坐标更新节点（对应 Update 的坐标形式）。
        /// </summary>
        /// <param name="x">坐标 X</param>
        /// <param name="y">坐标 Y</param>
        /// <param name="rot">朝向（当前实现中引擎未使用）</param>
        /// <param name="length">节点数量上限</param>
        /// <param name="width">激光宽度</param>
        /// <param name="active">节点是否激活</param>
        /// <returns>是否成功（length 小于等于 1 时失败）</returns>
        public bool Update(double x, double y, double rot, int length, double width, bool active = true)
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.bentLaser_update(_handle, x, y, rot, length, width, ToByte(active)) != 0;
        }

        /// <summary>
        /// 根据游戏对象更新节点（对应 Update 的对象形式，读取对象的坐标与朝向）。
        /// </summary>
        /// <param name="gameObject">游戏对象</param>
        /// <param name="length">节点数量上限</param>
        /// <param name="width">激光宽度</param>
        /// <param name="active">节点是否激活</param>
        /// <returns>是否成功</returns>
        public bool Update(GameObjectBase gameObject, int length, double width, bool active = true)
        {
            ThrowIfDisposed();
            if (gameObject == null) throw new ArgumentNullException(nameof(gameObject));
            return LuaSTGAPI.api.bentLaser_updateByObject(
                _handle, (nuint)gameObject._native, length, width, ToByte(active)) != 0;
        }

        /// <summary>
        /// 直接修改单个节点并更新相邻节点（对应 UpdateNode 的数值形式）。
        /// </summary>
        /// <param name="index">节点索引，从 0 开始（Lua 侧从 1 开始）</param>
        /// <param name="x">坐标 X</param>
        /// <param name="y">坐标 Y</param>
        /// <param name="width">节点宽度</param>
        /// <returns>是否成功（索引越界时失败）</returns>
        public bool UpdateNode(int index, double x, double y, double width)
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.bentLaser_updateSingleNode(_handle, index, x, y, width) != 0;
        }

        /// <summary>
        /// 对某个节点开启或关闭（对应 UpdateNode 的对象形式）。
        /// </summary>
        /// <param name="gameObject">游戏对象</param>
        /// <param name="node">节点索引，为负数时从尾部数起</param>
        /// <param name="length">节点数量上限</param>
        /// <param name="width">激光宽度（当前实现中引擎未使用）</param>
        /// <param name="active">节点是否激活</param>
        /// <returns>是否成功</returns>
        public bool UpdateNode(GameObjectBase gameObject, int node, int length, double width, bool active = true)
        {
            ThrowIfDisposed();
            if (gameObject == null) throw new ArgumentNullException(nameof(gameObject));
            return LuaSTGAPI.api.bentLaser_updateNodeByObject(
                _handle, (nuint)gameObject._native, node, length, width, ToByte(active)) != 0;
        }

        /// <summary>
        /// 按坐标列表更新节点（对应 UpdatePositionByList）。
        /// </summary>
        /// <param name="positions">坐标列表，每两个元素为一组 (x, y)</param>
        /// <param name="width">激光宽度</param>
        /// <param name="index">起始索引（与 Lua 语义一致，默认 1）</param>
        /// <param name="revert">是否反向</param>
        /// <returns>是否成功</returns>
        public bool UpdatePositionByList(double[] positions, double width, int index = 1, bool revert = false)
            => UpdatePositionByList(positions.AsSpan(), width, index, revert);

        /// <summary>按坐标列表更新节点（对应 UpdatePositionByList），positions 每两个元素为一组 (x, y)</summary>
        public bool UpdatePositionByList(Span<double> positions, double width, int index = 1, bool revert = false)
        {
            ThrowIfDisposed();
            var length = positions.Length / 2;
            fixed (double* p = positions)
            {
                return LuaSTGAPI.api.bentLaser_updatePositionByList(
                    _handle, p, length, width, index, ToByte(revert)) != 0;
            }
        }

        /// <summary>
        /// 按列表更新全部节点（对应 UpdateAllNode），使用固定宽度。
        /// </summary>
        /// <param name="xs">各节点坐标 X</param>
        /// <param name="ys">各节点坐标 Y</param>
        /// <param name="width">固定节点宽度</param>
        /// <returns>是否成功（节点数超过 512 时失败）</returns>
        public bool UpdateAllNode(Span<float> xs, Span<float> ys, double width)
        {
            ThrowIfDisposed();
            if (xs.Length != ys.Length) throw new ArgumentException("xs 与 ys 长度不一致");
            fixed (float* px = xs, py = ys)
            {
                return LuaSTGAPI.api.bentLaser_updateAllNode(
                    _handle, xs.Length, px, py, null, width) != 0;
            }
        }

        /// <summary>
        /// 按列表更新全部节点（对应 UpdateAllNode），逐节点指定宽度。
        /// </summary>
        /// <param name="xs">各节点坐标 X</param>
        /// <param name="ys">各节点坐标 Y</param>
        /// <param name="widths">各节点宽度，长度须与 xs 一致</param>
        /// <returns>是否成功（节点数超过 512 时失败）</returns>
        public bool UpdateAllNode(Span<float> xs, Span<float> ys, Span<float> widths)
        {
            ThrowIfDisposed();
            if (xs.Length != ys.Length || xs.Length != widths.Length)
            {
                throw new ArgumentException("xs、ys、widths 长度不一致");
            }
            fixed (float* px = xs, py = ys, pw = widths)
            {
                return LuaSTGAPI.api.bentLaser_updateAllNode(
                    _handle, xs.Length, px, py, pw, 0.0) != 0;
            }
        }

        // ========== 采样 ==========

        /// <summary>
        /// 按长度采样曲线激光（对应 SampleByLength），返回采样点数组 (X, Y, Rot)。
        /// </summary>
        /// <param name="length">采样间距（须为正数）</param>
        public (float X, float Y, float Rot)[] SampleByLength(double length)
        {
            ThrowIfDisposed();
            var count = LuaSTGAPI.api.bentLaser_sampleByLength(_handle, length, null, null, null, 0);
            var result = new (float, float, float)[count];
            if (count > 0)
            {
                var xs = new float[count];
                var ys = new float[count];
                var rs = new float[count];
                fixed (float* px = xs, py = ys, pr = rs)
                {
                    LuaSTGAPI.api.bentLaser_sampleByLength(_handle, length, px, py, pr, count);
                }
                for (var i = 0; i < count; i++)
                {
                    result[i] = (xs[i], ys[i], rs[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// 按时间采样曲线激光（对应 SampleByTime，内部除以 60 换算为帧间隔），
        /// 返回采样点数组 (X, Y, Rot)。
        /// </summary>
        /// <param name="time">采样间隔（帧，须为正数）</param>
        public (float X, float Y, float Rot)[] SampleByTime(double time)
        {
            ThrowIfDisposed();
            var count = LuaSTGAPI.api.bentLaser_sampleByTime(_handle, time, null, null, null, 0);
            var result = new (float, float, float)[count];
            if (count > 0)
            {
                var xs = new float[count];
                var ys = new float[count];
                var rs = new float[count];
                fixed (float* px = xs, py = ys, pr = rs)
                {
                    LuaSTGAPI.api.bentLaser_sampleByTime(_handle, time, px, py, pr, count);
                }
                for (var i = 0; i < count; i++)
                {
                    result[i] = (xs[i], ys[i], rs[i]);
                }
            }
            return result;
        }

        // ========== 渲染 ==========

        /// <summary>
        /// 渲染曲线激光（对应 Render）。
        /// 引擎启用 GLOBAL_SCALE_COLLI_SHAPE 时 scale 会乘全局图像缩放系数。
        /// </summary>
        /// <param name="texName">纹理资源名称</param>
        /// <param name="blend">混合模式</param>
        /// <param name="color">顶点颜色（0xAARRGGBB，可传 <see cref="Color"/>）</param>
        /// <param name="texLeft">纹理左边界（像素）</param>
        /// <param name="texTop">纹理上边界（像素）</param>
        /// <param name="texWidth">纹理宽度（像素）</param>
        /// <param name="texHeight">纹理高度（像素）</param>
        /// <param name="scale">渲染宽度缩放</param>
        /// <exception cref="InvalidOperationException">纹理不存在或渲染失败</exception>
        public void Render(string texName, BlendMode blend, uint color,
            double texLeft, double texTop, double texWidth, double texHeight, double scale = 1.0)
        {
            ThrowIfDisposed();
            using var name = new MarshaledString(texName);
            if (LuaSTGAPI.api.bentLaser_render(
                _handle, name, (byte)blend, color,
                texLeft, texTop, texWidth, texHeight, scale) == 0)
            {
                throw new InvalidOperationException($"can't render object with texture '{texName}'.");
            }
        }

        /// <summary>渲染碰撞体（对应 RenderCollider）</summary>
        /// <param name="fillColor">填充颜色（0xAARRGGBB，可传 <see cref="Color"/>）</param>
        public void RenderCollider(uint fillColor)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.bentLaser_renderCollider(_handle, fillColor);
        }

        // ========== 碰撞检测 ==========

        /// <summary>碰撞检测（对应 CollisionCheck），rot/a/b 默认 0，rect 默认 false</summary>
        public bool CollisionCheck(double x, double y, double rot = 0.0, double a = 0.0, double b = 0.0, bool rect = false)
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.bentLaser_collisionCheck(_handle, x, y, rot, a, b, ToByte(rect)) != 0;
        }

        /// <summary>碰撞检测，指定激光宽度（对应 CollisionCheckWidth / CollisionCheckWithWidth 的数值形式）</summary>
        public bool CollisionCheckWidth(double width, double x, double y, double rot = 0.0, double a = 0.0, double b = 0.0, bool rect = false)
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.bentLaser_collisionCheckWidth(_handle, width, x, y, rot, a, b, ToByte(rect)) != 0;
        }

        /// <summary>
        /// 碰撞检测，指定激光宽度，与游戏对象比较（对应 CollisionCheckWithWidth 的对象形式，
        /// 读取对象的原始 x/y/rot/a/b/rect）。
        /// </summary>
        public bool CollisionCheckWidth(double width, GameObjectBase gameObject)
        {
            ThrowIfDisposed();
            if (gameObject == null) throw new ArgumentNullException(nameof(gameObject));
            return LuaSTGAPI.api.bentLaser_collisionCheckWithObject(
                _handle, (nuint)gameObject._native, width) != 0;
        }

        /// <summary>检查是否有节点在边界内（对应 BoundCheck）</summary>
        public bool BoundCheck()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.bentLaser_boundCheck(_handle) != 0;
        }

        // ========== 其他 ==========

        /// <summary>更改所有节点的碰撞和渲染宽度（对应 SetAllWidth）</summary>
        public void SetAllWidth(double width)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.bentLaser_setAllWidth(_handle, width);
        }

        /// <summary>
        /// 设置碰撞包络（对应 SetEnvelope）。
        /// base 会被引擎钳制到 0~1，power 会被对齐到 0.4 的倍数。
        /// </summary>
        public void SetEnvelope(double height, double @base, double rate, double power)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.bentLaser_setEnvelope(_handle, height, @base, rate, power);
        }

        /// <summary>读取碰撞包络（对应 GetEnvelope）</summary>
        public (double Height, double Base, double Rate, double Power) GetEnvelope()
        {
            ThrowIfDisposed();
            double height = 0.0, @base = 0.0, rate = 0.0, power = 0.0;
            LuaSTGAPI.api.bentLaser_getEnvelope(_handle, &height, &@base, &rate, &power);
            return (height, @base, rate, power);
        }

        /// <summary>节点数量（对应 Lua 侧元方法 __len）</summary>
        public int NodeCount
        {
            get
            {
                ThrowIfDisposed();
                return LuaSTGAPI.api.bentLaser_getNodeCount(_handle);
            }
        }
    }
}
