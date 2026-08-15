namespace LuaSTG.Core
{
    /// <summary>
    /// 资源管理器（对应 Lua 侧 lstg.ResourceManager）。
    /// 资源加载、查询、图像状态等 API 将在资源模块移植阶段补充。
    /// </summary>
    public static partial class ResourceManager
    {
        /// <summary>
        /// 全局图像缩放系数（对应 lstg.SetImageScale 设置的值）。
        /// 资源模块完整移植后从引擎读取。
        /// </summary>
        internal static double GlobalImageScale { get; } = 1.0;
    }
}
