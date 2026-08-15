using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 平台 API（对应 Lua 侧 lstg.Platform）。
    /// 注意：Lua 侧的 Execute 未移植（引擎未启用 LUASTG_ENABLE_EXECUTE_API）。
    /// </summary>
    public static unsafe partial class Platform
    {
        /// <summary>获取当前用户的本地应用数据目录，失败返回空字符串</summary>
        public static string GetLocalAppDataPath()
            => StringMarshal.FromUtf8(LuaSTGAPI.api.platform_getLocalAppDataPath());

        /// <summary>获取当前用户的漫游应用数据目录，失败返回空字符串</summary>
        public static string GetRoamingAppDataPath()
            => StringMarshal.FromUtf8(LuaSTGAPI.api.platform_getRoamingAppDataPath());

        /// <summary>以指定命令行参数重启应用（对应 RestartWithCommandLineArguments）</summary>
        public static void RestartWithCommandLineArguments(params string[] arguments)
        {
            LuaSTGAPI.api.platform_restartSetArgumentCount((uint)(arguments?.Length ?? 0));
            for (var i = 0; i < arguments.Length; i++)
            {
                using var a = new MarshaledString(arguments[i]);
                LuaSTGAPI.api.platform_restartSetArgument((uint)i, a);
            }
            LuaSTGAPI.api.platform_restartCommitArguments();
        }

        /// <summary>
        /// 弹出消息框（对应 MessageBox）。
        /// flags 为 Win32 MessageBox 标志位组合（MB_OK 等），返回用户选择（IDOK 等）。
        /// </summary>
        public static int MessageBox(string title, string text, uint flags)
        {
            using var t = new MarshaledString(title);
            using var x = new MarshaledString(text);
            return LuaSTGAPI.api.platform_messageBox(t, x, flags);
        }
    }
}
