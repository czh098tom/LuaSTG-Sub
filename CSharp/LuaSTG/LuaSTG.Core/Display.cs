using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 显示器（对应 Lua 侧 lstg.Display）。
    /// 静态工厂（<see cref="GetAll"/>/<see cref="GetPrimary"/>/<see cref="GetNearestFromWindow"/>）
    /// 对应 lstg.Display 模块级方法，返回的实例持有引擎对象的引用，
    /// 使用完毕应调用 <see cref="Dispose"/>（对应 Lua userdata 的 __gc）。
    /// </summary>
    public sealed unsafe class Display : IDisposable
    {
        internal nint _handle;

        internal Display(nint handle)
        {
            _handle = handle;
        }

        // ------------------------------------------------------------------
        // 静态工厂（lstg.Display 模块级方法）
        // ------------------------------------------------------------------

        /// <summary>
        /// 获取主显示器（对应 lstg.Display.getPrimary）。
        /// </summary>
        /// <returns>主显示器，失败返回 null（Lua 侧此处为 luaL_error）</returns>
        public static Display? GetPrimary()
        {
            var handle = LuaSTGAPI.api.display_getPrimary();
            return handle != 0 ? new Display((nint)handle) : null;
        }

        /// <summary>
        /// 获取距离主窗口最近的显示器（对应 lstg.Display.getNearestFromWindow；
        /// 注意：Lua 侧此方法未实现（直接报错），这里直接使用引擎接口实现）。
        /// </summary>
        /// <returns>最近的显示器，失败返回 null</returns>
        public static Display? GetNearestFromWindow()
        {
            var handle = LuaSTGAPI.api.display_getNearestFromWindow();
            return handle != 0 ? new Display((nint)handle) : null;
        }

        /// <summary>
        /// 枚举全部显示器（对应 lstg.Display.getAll）。
        /// </summary>
        public static Display[] GetAll()
        {
            // 引擎侧 getAll 拆分为 count + byIndex（内部缓存一次性释放）
            var count = LuaSTGAPI.api.display_getAllCount();
            var result = new Display[count];
            for (uint i = 0; i < count; i++)
            {
                var handle = LuaSTGAPI.api.display_getAllByIndex(i);
                result[i] = new Display((nint)handle);
            }
            LuaSTGAPI.api.display_getAllClear();
            return result;
        }

        // ------------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------------

        /// <summary>是否已销毁</summary>
        public bool IsDisposed => _handle == 0;

        /// <summary>引擎句柄，已销毁时抛 <see cref="ObjectDisposedException"/></summary>
        internal nint Handle => _handle != 0
            ? _handle
            : throw new ObjectDisposedException(nameof(Display), "显示器已销毁");

        private void ThrowIfDisposed()
        {
            if (_handle == 0)
            {
                throw new ObjectDisposedException(nameof(Display), "显示器已销毁");
            }
        }

        /// <summary>释放显示器引用（对应 Lua userdata 的 __gc）</summary>
        public void Dispose()
        {
            if (_handle != 0)
            {
                LuaSTGAPI.api.display_release((nuint)_handle);
                _handle = 0;
            }
        }

        ~Display()
        {
            // 兜底释放
            if (_handle != 0)
            {
                try
                {
                    LuaSTGAPI.api.display_release((nuint)_handle);
                }
                catch
                {
                    // 引擎可能已关闭
                }
                _handle = 0;
            }
        }

        // ------------------------------------------------------------------
        // 实例方法（lstg.Display 成员）
        // ------------------------------------------------------------------

        /// <summary>获取显示器友好名称（对应 lstg.Display:getFriendlyName）</summary>
        public string GetFriendlyName()
        {
            ThrowIfDisposed();
            return StringMarshal.FromUtf8(LuaSTGAPI.api.display_getFriendlyName((nuint)_handle));
        }

        /// <summary>获取显示器大小（对应 lstg.Display:getSize）</summary>
        public void GetSize(out uint width, out uint height)
        {
            ThrowIfDisposed();
            uint w = 0, h = 0;
            LuaSTGAPI.api.display_getSize((nuint)_handle, &w, &h);
            width = w;
            height = h;
        }

        /// <summary>获取显示器位置（对应 lstg.Display:getPosition）</summary>
        public void GetPosition(out int x, out int y)
        {
            ThrowIfDisposed();
            int px = 0, py = 0;
            LuaSTGAPI.api.display_getPosition((nuint)_handle, &px, &py);
            x = px;
            y = py;
        }

        /// <summary>获取显示器矩形（对应 lstg.Display:getRect）</summary>
        public void GetRect(out int left, out int top, out int right, out int bottom)
        {
            ThrowIfDisposed();
            int l = 0, t = 0, r = 0, b = 0;
            LuaSTGAPI.api.display_getRect((nuint)_handle, &l, &t, &r, &b);
            left = l;
            top = t;
            right = r;
            bottom = b;
        }

        /// <summary>获取工作区大小（对应 lstg.Display:getWorkAreaSize）</summary>
        public void GetWorkAreaSize(out uint width, out uint height)
        {
            ThrowIfDisposed();
            uint w = 0, h = 0;
            LuaSTGAPI.api.display_getWorkAreaSize((nuint)_handle, &w, &h);
            width = w;
            height = h;
        }

        /// <summary>获取工作区位置（对应 lstg.Display:getWorkAreaPosition）</summary>
        public void GetWorkAreaPosition(out int x, out int y)
        {
            ThrowIfDisposed();
            int px = 0, py = 0;
            LuaSTGAPI.api.display_getWorkAreaPosition((nuint)_handle, &px, &py);
            x = px;
            y = py;
        }

        /// <summary>获取工作区矩形（对应 lstg.Display:getWorkAreaRect）</summary>
        public void GetWorkAreaRect(out int left, out int top, out int right, out int bottom)
        {
            ThrowIfDisposed();
            int l = 0, t = 0, r = 0, b = 0;
            LuaSTGAPI.api.display_getWorkAreaRect((nuint)_handle, &l, &t, &r, &b);
            left = l;
            top = t;
            right = r;
            bottom = b;
        }

        /// <summary>是否为主显示器（对应 lstg.Display:isPrimary）</summary>
        public bool IsPrimary()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.display_isPrimary((nuint)_handle) != 0;
        }

        /// <summary>获取显示器缩放系数（对应 lstg.Display:getDisplayScale）</summary>
        public float GetDisplayScale()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.display_getDisplayScale((nuint)_handle);
        }

        /// <inheritdoc/>
        public override string ToString()
            => _handle != 0 ? "lstg.Display" : "lstg.Display (disposed)";

        /// <summary>比较是否包装同一引擎显示器（对应 Lua 侧 __eq）</summary>
        public override bool Equals(object? obj)
            => obj is Display other && _handle == other._handle;

        /// <inheritdoc/>
        public override int GetHashCode()
            => _handle.GetHashCode();
    }

    /// <summary>显示模式信息（对应兼容 API lstg.EnumResolutions 返回的列表元素）</summary>
    public readonly struct VideoResolution
    {
        /// <summary>宽度（像素）</summary>
        public readonly uint Width;
        /// <summary>高度（像素）</summary>
        public readonly uint Height;
        /// <summary>刷新率分子</summary>
        public readonly uint RefreshRateNumerator;
        /// <summary>刷新率分母</summary>
        public readonly uint RefreshRateDenominator;

        internal VideoResolution(uint width, uint height, uint numerator, uint denominator)
        {
            Width = width;
            Height = height;
            RefreshRateNumerator = numerator;
            RefreshRateDenominator = denominator;
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"{Width}x{Height}@{RefreshRateNumerator}/{RefreshRateDenominator}";
    }

    /// <summary>
    /// 传统显示/视频 API（对应 Lua 顶层兼容函数 lstg.ChangeVideoMode、lstg.EnumResolutions、
    /// lstg.EnumGPUs、lstg.ChangeGPU、lstg.GetCurrentGpuName、lstg.SetSwapChainScalingMode）。
    /// </summary>
    public static unsafe partial class VideoCompat
    {
        /// <summary>
        /// 修改显示模式（对应 lstg.ChangeVideoMode）。
        /// </summary>
        /// <param name="width">宽度（像素）</param>
        /// <param name="height">高度（像素）</param>
        /// <param name="windowed">true 为窗口化，false 为独占全屏</param>
        /// <param name="vsync">是否启用垂直同步</param>
        /// <returns>引擎是否接受本次设置</returns>
        public static bool ChangeVideoMode(uint width, uint height, bool windowed, bool vsync)
            => LuaSTGAPI.api.video_changeVideoMode(width, height, windowed ? (byte)1 : (byte)0, vsync ? (byte)1 : (byte)0) != 0;

        /// <summary>
        /// 枚举可用分辨率（对应 lstg.EnumResolutions）。
        /// 与 Lua 侧一致，固定返回 5 个 60Hz 的 4:3 分辨率。
        /// </summary>
        public static VideoResolution[] EnumResolutions()
        {
            var count = (int)LuaSTGAPI.api.video_enumResolutionCount();
            var result = new VideoResolution[count];
            for (uint i = 0; i < count; i++)
            {
                uint w = 0, h = 0, n = 0, d = 0;
                if (LuaSTGAPI.api.video_enumResolutionByIndex(i, &w, &h, &n, &d) != 0)
                {
                    result[i] = new VideoResolution(w, h, n, d);
                }
            }
            return result;
        }

        /// <summary>
        /// 枚举可用 GPU 名称（对应 lstg.EnumGPUs）。
        /// 渲染设备不可用时返回空数组（Lua 侧此处为 luaL_error）。
        /// </summary>
        public static string[] EnumGpus()
        {
            var count = LuaSTGAPI.api.video_getGpuCount();
            var result = new string[count];
            for (uint i = 0; i < count; i++)
            {
                result[i] = StringMarshal.FromUtf8(LuaSTGAPI.api.video_getGpuNameByIndex(i));
            }
            return result;
        }

        /// <summary>
        /// 切换首选 GPU 并重建渲染设备（对应 lstg.ChangeGPU）。
        /// </summary>
        /// <param name="name">GPU 名称（来自 <see cref="EnumGpus"/>）</param>
        /// <exception cref="InvalidOperationException">渲染设备不可用或重建失败（Lua 侧为 luaL_error）</exception>
        public static void ChangeGpu(string name)
        {
            using var s = new MarshaledString(name);
            var result = LuaSTGAPI.api.video_changeGPU(s);
            if (result == 1)
            {
                throw new InvalidOperationException("render device is not available.");
            }
            if (result == 2)
            {
                throw new InvalidOperationException("ChangeGPU failed.");
            }
        }

        /// <summary>
        /// 获取当前 GPU 名称（对应 lstg.GetCurrentGpuName）。
        /// </summary>
        /// <exception cref="InvalidOperationException">渲染设备不可用（Lua 侧为 luaL_error）</exception>
        public static string GetCurrentGpuName()
        {
            var ptr = LuaSTGAPI.api.video_getCurrentGpuName();
            if (ptr == null)
            {
                throw new InvalidOperationException("render device is not available.");
            }
            return StringMarshal.FromUtf8(ptr);
        }

        /// <summary>
        /// 设置主交换链缩放模式（对应 lstg.SetSwapChainScalingMode）。
        /// </summary>
        public static void SetSwapChainScalingMode(ScalingMode mode)
            => LuaSTGAPI.api.video_setSwapChainScalingMode((int)mode);
    }
}
