using LuaSTG.Core;

using static LuaSTG.Core.LuaSTGAPI;

namespace LuaSTG
{
    /// <summary>
    /// 托管游戏应用示例。游戏逻辑通过继承此类型实现，
    /// 并由 <see cref="LuaSTGAppFactory"/> 返回实例。
    /// </summary>
    public class LuaSTGApp : ILuaSTGApp
    {
        public virtual void GameInit()
        {
        }

        public virtual bool FrameFunc()
        {
            return false;
        }

        public virtual void RenderFunc()
        {
        }

        public virtual void GameExit()
        {
        }

        public virtual void FocusGainFunc()
        {
        }

        public virtual void FocusLoseFunc()
        {
        }

        public virtual void EventFunc(EngineEvent eventType, bool state)
        {
        }
    }
}
