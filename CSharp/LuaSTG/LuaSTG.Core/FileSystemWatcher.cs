using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 文件变更动作（对应 lstg.FileSystemWatcher.FileAction，与引擎 core::FileAction 一致）。
    /// </summary>
    public enum FileAction : int
    {
        /// <summary>未知（Lua 侧未导出）</summary>
        Unknown = 0,
        /// <summary>已添加（Lua: added）</summary>
        Added = 1,
        /// <summary>已删除（Lua: removed）</summary>
        Removed = 2,
        /// <summary>已修改（Lua: modified）</summary>
        Modified = 3,
        /// <summary>重命名旧名称（Lua: renamed_old_name）</summary>
        RenamedOldName = 4,
        /// <summary>重命名新名称（Lua: renamed_new_name）</summary>
        RenamedNewName = 5,
    }

    /// <summary>文件变更事件（对应 Lua 侧 read 填入 table 的 file_name/action 字段）</summary>
    public readonly struct FileWatchEvent
    {
        /// <summary>变更的文件/目录名（相对于监视路径）</summary>
        public readonly string FileName;
        /// <summary>变更动作</summary>
        public readonly FileAction Action;

        internal FileWatchEvent(string fileName, FileAction action)
        {
            FileName = fileName;
            Action = action;
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"{Action}: {FileName}";
    }

    /// <summary>
    /// 文件系统监视器（对应 Lua 侧 lstg.FileSystemWatcher）。
    /// 经 <see cref="Create"/> 工厂创建；<see cref="Dispose"/>（对应 Lua close）销毁，
    /// 销毁后访问抛 <see cref="ObjectDisposedException"/>。
    /// </summary>
    public sealed unsafe class FileSystemWatcher : IDisposable
    {
        internal nint _handle;
        private int _cachedEventCount;

        private FileSystemWatcher(nint handle)
        {
            _handle = handle;
        }

        /// <summary>
        /// 创建文件系统监视器（对应 lstg.FileSystemWatcher.create）。
        /// </summary>
        /// <param name="path">要监视的目录路径</param>
        /// <returns>监视器实例，创建失败返回 null（Lua 侧返回 nil）</returns>
        public static FileSystemWatcher? Create(string path)
        {
            using var s = new MarshaledString(path);
            var handle = LuaSTGAPI.api.fswatcher_create(s);
            return handle != 0 ? new FileSystemWatcher((nint)handle) : null;
        }

        // ------------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------------

        /// <summary>是否已销毁</summary>
        public bool IsDisposed => _handle == 0;

        private void ThrowIfDisposed()
        {
            if (_handle == 0)
            {
                throw new ObjectDisposedException(nameof(FileSystemWatcher), "文件系统监视器已销毁");
            }
        }

        /// <summary>关闭并释放监视器（对应 lstg.FileSystemWatcher:close / Lua userdata 的 __gc）</summary>
        public void Dispose()
        {
            if (_handle != 0)
            {
                LuaSTGAPI.api.fswatcher_close((nuint)_handle);
                _handle = 0;
            }
        }

        /// <inheritdoc cref="Dispose"/>
        public void Close()
            => Dispose();

        ~FileSystemWatcher()
        {
            // 兜底释放
            if (_handle != 0)
            {
                try
                {
                    LuaSTGAPI.api.fswatcher_close((nuint)_handle);
                }
                catch
                {
                    // 引擎可能已关闭
                }
                _handle = 0;
            }
        }

        // ------------------------------------------------------------------
        // 事件读取（lstg.FileSystemWatcher:read，count + byIndex 拆分）
        // ------------------------------------------------------------------

        /// <summary>
        /// 读取当前积压的全部文件变更事件（对应 lstg.FileSystemWatcher:read）。
        /// 返回事件数量，随后用 <see cref="GetEvent(int)"/> 按下标读取；
        /// 缓存到下一次 <see cref="Read"/> 或 <see cref="Dispose"/> 前有效。
        /// </summary>
        /// <returns>本次读取到的事件数量</returns>
        public int Read()
        {
            ThrowIfDisposed();
            _cachedEventCount = checked((int)LuaSTGAPI.api.fswatcher_read((nuint)_handle));
            return _cachedEventCount;
        }

        /// <summary>
        /// 读取 <see cref="Read"/> 缓存的第 index 个事件（byIndex 拆分的读取端）。
        /// </summary>
        /// <param name="index">事件下标（0 到 Read() 返回值 - 1）</param>
        /// <returns>文件变更事件（含动作与路径）</returns>
        /// <exception cref="ArgumentOutOfRangeException">下标越界</exception>
        public FileWatchEvent GetEvent(int index)
        {
            ThrowIfDisposed();
            if (index < 0 || (uint)index >= (uint)_cachedEventCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            var action = (FileAction)LuaSTGAPI.api.fswatcher_getEventAction((nuint)_handle, (uint)index);
            var fileName = StringMarshal.FromUtf8(LuaSTGAPI.api.fswatcher_getEventFileName((nuint)_handle, (uint)index));
            return new FileWatchEvent(fileName, action);
        }

        /// <inheritdoc/>
        public override string ToString()
            => _handle != 0 ? "lstg.FileSystemWatcher" : "lstg.FileSystemWatcher (disposed)";

        /// <summary>比较是否包装同一引擎监视器（对应 Lua 侧 __eq）</summary>
        public override bool Equals(object? obj)
            => obj is FileSystemWatcher other && _handle == other._handle;

        /// <inheritdoc/>
        public override int GetHashCode()
            => _handle.GetHashCode();
    }
}
