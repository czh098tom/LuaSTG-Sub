namespace LuaSTG.Core
{
    /// <summary>
    /// LuaSTG 托管应用。
    /// 生命周期与 Lua 侧的 GameInit/FrameFunc/RenderFunc/GameExit 等全局函数一致。
    /// </summary>
    public interface ILuaSTGApp
    {
        /// <summary>应用初始化时调用（引擎全部子系统就绪后）。</summary>
        void GameInit();

        /// <summary>每帧调用。返回 true 表示应用执行完毕，请求退出。</summary>
        bool FrameFunc();

        /// <summary>每帧渲染时调用。</summary>
        void RenderFunc();

        /// <summary>应用退出时调用。</summary>
        void GameExit();

        /// <summary>窗口获得焦点时调用。</summary>
        void FocusGainFunc();

        /// <summary>窗口失去焦点时调用。</summary>
        void FocusLoseFunc();

        /// <summary>窗口事件（焦点、尺寸变化等）。</summary>
        void EventFunc(EngineEvent eventType, bool state);
    }
}
