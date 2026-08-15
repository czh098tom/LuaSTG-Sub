using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 高精度停表（对应 Lua 侧 lstg.StopWatch，见 LuaBinding/LW_StopWatch.cpp）。
    /// 无参构造在引擎侧分配一个基于 QueryPerformanceCounter 的停表对象，
    /// <see cref="Dispose"/>（或 <see cref="Destroy"/>）销毁之；
    /// 销毁后访问会抛出 <see cref="ObjectDisposedException"/>。
    /// </summary>
    public sealed unsafe class StopWatch : IDisposable
    {
        /// <summary>引擎侧停表句柄，0 表示已销毁</summary>
        private nuint _handle;

        /// <summary>创建停表（初始归零）</summary>
        /// <exception cref="OutOfMemoryException">引擎侧对象分配失败</exception>
        public StopWatch()
        {
            _handle = LuaSTGAPI.api.stopWatch_create();
            if (_handle == 0)
            {
                throw new OutOfMemoryException("StopWatch 分配失败");
            }
        }

        /// <summary>对象是否仍然有效（未被销毁）</summary>
        public bool IsValid => _handle != 0;

        /// <summary>销毁停表（对应 Lua 侧 userdata 回收）</summary>
        public void Dispose() => Destroy();

        /// <summary>销毁停表，等价于 <see cref="Dispose"/></summary>
        public void Destroy()
        {
            if (_handle != 0)
            {
                LuaSTGAPI.api.stopWatch_destroy(_handle);
                _handle = 0;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_handle == 0)
            {
                throw new ObjectDisposedException(nameof(StopWatch), "StopWatch 已销毁");
            }
        }

        /// <summary>归零（对应 StopWatch:Reset）</summary>
        public void Reset()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.stopWatch_reset(_handle);
        }

        /// <summary>暂停（对应 StopWatch:Pause）</summary>
        public void Pause()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.stopWatch_pause(_handle);
        }

        /// <summary>继续（对应 StopWatch:Resume）</summary>
        public void Resume()
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.stopWatch_resume(_handle);
        }

        /// <summary>获得流逝时间，单位为秒（对应 StopWatch:GetElapsed）</summary>
        public double GetElapsed()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.stopWatch_getElapsed(_handle);
        }
    }
}
