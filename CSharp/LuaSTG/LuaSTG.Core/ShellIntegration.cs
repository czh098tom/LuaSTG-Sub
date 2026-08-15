using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 系统集成（对应 Lua 侧 lstg.ShellIntegration，静态类）：用系统默认方式打开文件/目录/URL。
    /// </summary>
    public static unsafe partial class ShellIntegration
    {
        /// <summary>用系统默认方式打开文件（对应 lstg.ShellIntegration.openFile）</summary>
        /// <returns>是否成功</returns>
        public static bool OpenFile(string path)
        {
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.shell_openFile(s) != 0;
        }

        /// <summary>用资源管理器打开目录（对应 lstg.ShellIntegration.openDirectory）</summary>
        /// <returns>是否成功</returns>
        public static bool OpenDirectory(string path)
        {
            using var s = new MarshaledString(path);
            return LuaSTGAPI.api.shell_openDirectory(s) != 0;
        }

        /// <summary>用系统默认浏览器打开 URL（对应 lstg.ShellIntegration.openUrl）</summary>
        /// <returns>是否成功</returns>
        public static bool OpenUrl(string url)
        {
            using var s = new MarshaledString(url);
            return LuaSTGAPI.api.shell_openUrl(s) != 0;
        }
    }
}
