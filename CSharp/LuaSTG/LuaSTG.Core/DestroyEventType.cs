namespace LuaSTG.Core
{
    /// <summary>
    /// 游戏对象销毁原因（与引擎侧 CLRGameObjectDestroyReason 保持一致）。
    /// </summary>
    public enum DestroyEventType : byte
    {
        /// <summary>语言侧调用 Delete（lstg.Del）</summary>
        Del = 0,
        /// <summary>语言侧调用 Kill（lstg.Kill）</summary>
        Kill = 1,
        /// <summary>离开边界被回收</summary>
        Bound = 2,
        /// <summary>其他原因</summary>
        Other = 3,
    }

    /// <summary>
    /// 引擎窗口事件类型（与引擎侧 LuaEngine::EngineEvent 保持一致）。
    /// </summary>
    public enum EngineEvent : byte
    {
        Idle = 0,
        WindowActive = 1,
        WindowResize = 2,
    }
}
