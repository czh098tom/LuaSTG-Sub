using System;
using System.Linq;

using LuaSTG.Core;

namespace LuaSTG
{
    public class LuaSTGAppFactory : ILuaSTGAppFactory
    {
        public ILuaSTGApp? GetApplication()
        {
            // 命令行带 --clr-selftest 时运行自测应用（用于 CoreCLR 绑定自动化验证）
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--clr-selftest"))
            {
                return new ClrSelfTestApp(Array.Exists(args, static a => a == "--lua-sync"));
            }
            return new LuaSTGApp();
        }
    }
}
