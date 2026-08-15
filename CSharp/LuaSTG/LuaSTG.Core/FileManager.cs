using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 文件系统枚举条目。
    /// 对应 Lua 侧 lstg.FileManager.EnumFiles / FindFiles 返回数组的元素：
    /// EnumFiles 条目为 {名称, 是否目录, 所属资源包}（系统文件无第三项），
    /// FindFiles 条目为 {名称, 所属资源包}（只包含文件）。
    /// </summary>
    /// <param name="Name">条目名称（系统目录条目为目录时带尾部 '/'）</param>
    /// <param name="IsDirectory">是否为目录（FindFiles 结果恒为 false）</param>
    /// <param name="ArchivePath">所属资源包路径；null 表示来自系统文件系统</param>
    public sealed record FileSystemEntry(string Name, bool IsDirectory, string? ArchivePath);

    /// <summary>
    /// 文件管理 API（对应 Lua 侧 lstg 兼容 API 与 lstg.FileManager 库，见 LW_FileManager.cpp）。
    /// </summary>
    public static unsafe partial class FileManager
    {
        // ========== 资源包加载（兼容 API LoadPack / LoadPackSub / UnloadPack）==========

        /// <summary>
        /// 加载资源包（对应 lstg.FileManager.LoadArchive 与兼容 API lstg.LoadPack）。
        /// </summary>
        /// <param name="path">资源包（zip）路径</param>
        /// <param name="password">密码；null 表示不设置密码</param>
        /// <returns>资源包对象；加载失败返回 null</returns>
        public static Archive? LoadArchive(string path, string? password = null)
        {
            using var p = new MarshaledString(path);
            using var pw = new MarshaledString(password);
            var handle = LuaSTGAPI.api.fileManager_loadArchive(p, pw, (byte)(password is null ? 0 : 1));
            return handle != 0 ? new Archive(handle) : null;
        }

        /// <summary>
        /// 加载资源包（兼容 API lstg.LoadPack，等价于 <see cref="LoadArchive"/>）。
        /// </summary>
        public static Archive? LoadPack(string path, string? password = null)
            => LoadArchive(path, password);

        /// <summary>
        /// 以游戏名作为密码加载资源包（兼容 API lstg.LoadPackSub）。
        /// </summary>
        /// <returns>资源包对象；加载失败返回 null</returns>
        public static Archive? LoadPackSub(string path)
        {
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.fileManager_loadPackSub(p);
            return handle != 0 ? new Archive(handle) : null;
        }

        /// <summary>
        /// 按路径卸载资源包（对应 lstg.FileManager.UnloadArchive 与兼容 API lstg.UnloadPack）。
        /// 注意：已创建的 <see cref="Archive"/> 包装对象不会因此失效（与 Lua 侧一致）。
        /// </summary>
        /// <returns>是否找到并卸载了资源包</returns>
        public static bool UnloadArchive(string name)
        {
            using var s = new MarshaledString(name);
            return LuaSTGAPI.api.fileManager_unloadArchive(s) != 0;
        }

        /// <summary>
        /// 按路径卸载资源包（兼容 API lstg.UnloadPack，等价于 <see cref="UnloadArchive"/>）。
        /// </summary>
        public static bool UnloadPack(string name)
            => UnloadArchive(name);

        /// <summary>卸载所有资源包（对应 lstg.FileManager.UnloadAllArchive）</summary>
        public static void UnloadAllArchive()
            => LuaSTGAPI.api.fileManager_unloadAllArchive();

        /// <summary>资源包是否已加载（对应 lstg.FileManager.ArchiveExist）</summary>
        public static bool ArchiveExist(string name)
        {
            using var s = new MarshaledString(name);
            return LuaSTGAPI.api.fileManager_archiveExist(s) != 0;
        }

        /// <summary>
        /// 按路径获取已加载资源包的对象（对应 lstg.FileManager.GetArchive）。
        /// </summary>
        /// <returns>资源包对象；未加载时返回 null</returns>
        public static Archive? GetArchive(string name)
        {
            using var s = new MarshaledString(name);
            var handle = LuaSTGAPI.api.fileManager_getArchive(s);
            return handle != 0 ? new Archive(handle) : null;
        }

        /// <summary>
        /// 枚举所有已加载资源包的路径（对应 lstg.FileManager.EnumArchives）。
        /// Lua 侧条目为 {路径, 优先级}，优先级恒为 0，故此处仅返回路径数组。
        /// </summary>
        public static string[] EnumArchives()
        {
            var count = LuaSTGAPI.api.fileManager_enumArchives();
            var result = new string[count];
            for (uint i = 0; i < count; i += 1)
            {
                byte isDir;
                result[i] = StringMarshal.FromUtf8(LuaSTGAPI.api.fileSystem_enumGetEntryName(i, &isDir));
            }
            return result;
        }

        // ========== 兼容 API ExtractRes / FindFiles ==========

        /// <summary>
        /// 释放资源中的文件到磁盘（兼容 API lstg.ExtractRes）。
        /// </summary>
        /// <exception cref="InvalidOperationException">读取或写入失败（对应 Lua 侧 luaL_error）</exception>
        public static void ExtractRes(string path, string target)
        {
            using var p = new MarshaledString(path);
            using var t = new MarshaledString(target);
            if (LuaSTGAPI.api.fileManager_extractRes(p, t) == 0)
            {
                throw new InvalidOperationException($"failed to extract resource '{path}' to '{target}'.");
            }
        }

        /// <summary>
        /// 查找文件（兼容 API lstg.FindFiles，通配符语义与 Lua 侧一致：
        /// 在资源包与系统目录中按扩展名查找文件，目录被忽略）。
        /// </summary>
        /// <param name="path">查找目录（空串表示当前目录）</param>
        /// <param name="ext">扩展名（不含 '.'，如 "lua"）；null 表示不过滤</param>
        /// <param name="packName">限定资源包路径；null 表示同时在系统目录查找</param>
        /// <returns>匹配的文件列表，条目的 <see cref="FileSystemEntry.ArchivePath"/> 为 null 时表示系统文件</returns>
        public static FileSystemEntry[] FindFiles(string path, string? ext = null, string? packName = null)
        {
            using var p = new MarshaledString(path);
            using var e = new MarshaledString(ext);
            using var pk = new MarshaledString(packName);
            var count = LuaSTGAPI.api.fileManager_findFiles(p, e, pk);
            return ReadEnumCache(count);
        }

        // ========== 文件枚举与存在性 ==========

        /// <summary>
        /// 枚举文件（对应 lstg.FileManager.EnumFiles）。
        /// </summary>
        /// <param name="path">枚举目录（空串表示当前目录）</param>
        /// <param name="ext">扩展名（不含 '.'，如 "lua"）；null 表示不过滤</param>
        /// <param name="includeArchives">是否同时枚举资源包内文件（对应 Lua 侧第 3 个参数；
        /// 资源包条目在前，系统目录条目在后；默认仅枚举系统目录）</param>
        public static FileSystemEntry[] EnumFiles(string path, string? ext = null, bool includeArchives = false)
        {
            using var p = new MarshaledString(path);
            using var e = new MarshaledString(ext);
            var count = LuaSTGAPI.api.fileManager_enumFiles(p, e, (byte)(includeArchives ? 1 : 0));
            return ReadEnumCache(count);
        }

        /// <summary>
        /// 文件是否存在（对应 lstg.FileManager.FileExist）。
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="all">true 时检查文件系统与资源包；false（默认，与 Lua 侧省略参数一致）仅检查系统文件</param>
        public static bool FileExist(string path, bool all = false)
        {
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.fileManager_fileExist(s, (byte)(all ? 1 : 0)) != 0;
        }

        // ========== 搜索路径 ==========

        /// <summary>添加搜索路径（对应 lstg.FileManager.AddSearchPath）</summary>
        public static void AddSearchPath(string path)
        {
            using var s = new MarshaledString(path);
            LuaSTGAPI.api.fileManager_addSearchPath(s);
        }

        /// <summary>移除搜索路径（对应 lstg.FileManager.RemoveSearchPath）</summary>
        public static void RemoveSearchPath(string path)
        {
            using var s = new MarshaledString(path);
            LuaSTGAPI.api.fileManager_removeSearchPath(s);
        }

        /// <summary>清空搜索路径（对应 lstg.FileManager.ClearSearchPath）</summary>
        public static void ClearSearchPath()
            => LuaSTGAPI.api.fileManager_clearSearchPath();

        // ========== 工作目录与目录操作 ==========

        /// <summary>
        /// 设置工作目录（对应 lstg.FileManager.SetCurrentDirectory）。
        /// </summary>
        /// <returns>是否成功（Lua 侧失败时附带的错误消息与错误码此处不返回）</returns>
        public static bool SetCurrentDirectory(string path)
        {
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.fileManager_setCurrentDirectory(s) != 0;
        }

        /// <summary>
        /// 获取工作目录（对应 lstg.FileManager.GetCurrentDirectory），路径分隔符为 '/'。
        /// </summary>
        /// <returns>工作目录，失败时返回空字符串（Lua 侧失败时返回 nil 与错误消息）</returns>
        public static string GetCurrentDirectory()
            => StringMarshal.FromUtf8(LuaSTGAPI.api.fileManager_getCurrentDirectory());

        /// <summary>
        /// 创建目录（递归，对应 lstg.FileManager.CreateDirectory）。
        /// </summary>
        /// <returns>是否实际创建（目录已存在时返回 false，与 Lua 侧一致）</returns>
        public static bool CreateDirectory(string path)
        {
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.fileManager_createDirectory(s) != 0;
        }

        /// <summary>
        /// 删除目录（递归，对应 lstg.FileManager.RemoveDirectory）。
        /// </summary>
        /// <returns>是否成功</returns>
        public static bool RemoveDirectory(string path)
        {
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.fileManager_removeDirectory(s) != 0;
        }

        /// <summary>
        /// 目录是否存在（对应 lstg.FileManager.DirectoryExist）。
        /// </summary>
        /// <param name="path">目录路径（空串视为存在，与 Lua 侧一致）</param>
        /// <param name="all">true 时检查文件系统与资源包；false（默认，与 Lua 侧省略参数一致）仅检查系统目录</param>
        public static bool DirectoryExist(string path, bool all = false)
        {
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.fileManager_directoryExist(s, (byte)(all ? 1 : 0)) != 0;
        }

        // ========== 内部：读取枚举缓存 ==========

        /// <summary>按数量读取引擎侧枚举缓存（必须在下一次枚举调用前完成）</summary>
        private static FileSystemEntry[] ReadEnumCache(uint count)
        {
            var result = new FileSystemEntry[count];
            for (uint i = 0; i < count; i += 1)
            {
                byte isDir;
                var name = StringMarshal.FromUtf8(LuaSTGAPI.api.fileSystem_enumGetEntryName(i, &isDir));
                var archive = StringMarshal.FromUtf8(LuaSTGAPI.api.fileSystem_enumGetEntryArchive(i));
                result[i] = new FileSystemEntry(name, isDir != 0, archive.Length == 0 ? null : archive);
            }
            return result;
        }
    }
}
