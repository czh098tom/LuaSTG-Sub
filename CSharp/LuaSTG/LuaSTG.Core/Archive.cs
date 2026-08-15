using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 资源包内条目（对应 Lua 侧 lstgArchive:EnumFiles / ListFiles 返回数组的元素
    /// {名称, 是否目录}）。
    /// </summary>
    /// <param name="Name">条目名称</param>
    /// <param name="IsDirectory">是否为目录</param>
    public sealed record ArchiveEntry(string Name, bool IsDirectory);

    /// <summary>
    /// 资源包对象（对应 Lua 侧 lstg.LoadArchive 等返回的 lstgArchive userdata，
    /// 见 LW_Archive.cpp）。内部持有引擎侧资源包引用，通过 <see cref="FileManager.LoadArchive"/>
    /// 等工厂方法创建；<see cref="Destroy"/>（对应 Lua 侧 userdata 回收）销毁，
    /// 销毁后访问抛 <see cref="ObjectDisposedException"/>。
    /// </summary>
    public sealed unsafe class Archive : IDisposable
    {
        /// <summary>引擎侧句柄（指向持有 core::SmartReference&lt;IFileSystemArchive&gt; 的 C 结构）</summary>
        internal nuint _handle;

        /// <summary>语言侧是否已销毁</summary>
        private bool _disposed;

        /// <summary>包装引擎侧句柄（仅由工厂方法调用）</summary>
        internal Archive(nuint handle)
        {
            _handle = handle;
        }

        // 注意：与 GameObjectBase 一致，不实现终结器。
        // 引擎关闭后回调引擎可能发生在停用之后，未 Destroy 的句柄随进程结束统一回收。

        /// <summary>资源包是否有效（对应 lstgArchive:IsValid；已销毁的对象恒为 false）</summary>
        public bool IsValid
            => !_disposed && _handle != 0 && LuaSTGAPI.api.archive_isValid(_handle) != 0;

        /// <summary>
        /// 销毁资源包对象（对应 Lua 侧 userdata 的 __gc），释放引擎侧引用。
        /// 重复调用安全；注意这只是释放对象引用，不会从文件系统管理器卸载资源包
        /// （卸载请使用 <see cref="FileManager.UnloadArchive"/>）。
        /// </summary>
        public void Destroy()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_handle != 0)
            {
                LuaSTGAPI.api.archive_destroy(_handle);
                _handle = 0;
            }
        }

        /// <summary>等价于 <see cref="Destroy"/></summary>
        public void Dispose()
            => Destroy();

        /// <summary>访问引擎数据前检查对象有效性</summary>
        private void ThrowIfDestroyed()
        {
            if (_disposed || _handle == 0)
            {
                throw new ObjectDisposedException(nameof(Archive), "资源包对象已被销毁");
            }
        }

        // ========== 枚举与查询 ==========

        /// <summary>
        /// 枚举资源包内指定目录的条目（对应 lstgArchive:EnumFiles，非递归）。
        /// </summary>
        /// <param name="directory">目录路径（空串为根目录）</param>
        public ArchiveEntry[] EnumFiles(string directory = "")
            => EnumEntries(directory, recursive: false);

        /// <summary>
        /// 递归枚举资源包内指定目录的条目（对应 lstgArchive:ListFiles）。
        /// </summary>
        /// <param name="directory">目录路径（空串为根目录）</param>
        public ArchiveEntry[] ListFiles(string directory = "")
            => EnumEntries(directory, recursive: true);

        private ArchiveEntry[] EnumEntries(string directory, bool recursive)
        {
            ThrowIfDestroyed();
            using var d = new MarshaledString(directory);
            var count = LuaSTGAPI.api.archive_enumFiles(_handle, d, (byte)(recursive ? 1 : 0));
            var result = new ArchiveEntry[count];
            for (uint i = 0; i < count; i += 1)
            {
                byte isDir;
                var name = StringMarshal.FromUtf8(LuaSTGAPI.api.fileSystem_enumGetEntryName(i, &isDir));
                result[i] = new ArchiveEntry(name, isDir != 0);
            }
            return result;
        }

        /// <summary>资源包内是否存在文件（对应 lstgArchive:FileExist）</summary>
        public bool FileExist(string path)
        {
            ThrowIfDestroyed();
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.archive_fileExist(_handle, s) != 0;
        }

        /// <summary>
        /// 获取资源包路径（对应 lstgArchive:GetName）。
        /// </summary>
        /// <returns>加载时使用的路径；资源包已失效时返回 null（与 Lua 侧返回 nil 一致）</returns>
        public string? GetName()
        {
            ThrowIfDestroyed();
            if (LuaSTGAPI.api.archive_isValid(_handle) == 0)
            {
                return null;
            }
            return StringMarshal.FromUtf8(LuaSTGAPI.api.archive_getName(_handle));
        }

        /// <summary>
        /// 获取资源包优先级（对应 lstgArchive:GetPriority）。
        /// 引擎当前不支持资源包优先级，恒返回 0（与 Lua 侧一致）。
        /// </summary>
        public int GetPriority()
        {
            ThrowIfDestroyed();
            return LuaSTGAPI.api.archive_getPriority(_handle);
        }

        /// <summary>
        /// 设置资源包优先级（对应 lstgArchive:SetPriority）。
        /// 引擎当前不支持资源包优先级，为空操作（与 Lua 侧一致）。
        /// </summary>
        public void SetPriority(int priority)
        {
            ThrowIfDestroyed();
            LuaSTGAPI.api.archive_setPriority(_handle, priority);
        }

        /// <summary>对应 Lua 侧 __tostring</summary>
        public override string ToString()
        {
            if (IsValid)
            {
                return $"lstg.Archive(\"{StringMarshal.FromUtf8(LuaSTGAPI.api.archive_getName(_handle))}\")";
            }
            return "lstg.Archive(null)";
        }
    }
}
