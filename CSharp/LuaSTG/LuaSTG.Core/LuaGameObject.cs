namespace LuaSTG.Core
{
    /// <summary>
    /// Lua 侧创建的游戏对象的 C# 包装（外壳）。
    /// 仅用于在 C# 侧访问 Lua 对象的引擎数据；引擎回调仍由 Lua 侧处理。
    /// 用户不应实例化此类型。
    /// </summary>
    public sealed unsafe class LuaGameObject : GameObjectBase
    {
        internal LuaGameObject(NativeGameObject* native) : base(native)
        {
        }

        // Lua 对象的回调由 Lua 侧处理，这里仅保留空实现

        public sealed override void OnFrame()
        {
        }

        public override void OnRender()
        {
        }

        public sealed override void OnDestroy(DestroyEventArgs args)
        {
        }

        public sealed override void OnColli(Collision collision)
        {
        }
    }
}
