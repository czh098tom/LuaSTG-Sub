using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 现代图形引擎对象包装基类（RenderTarget/Mesh/Texture2D/Sprite/各类渲染器等）。
    /// 句柄为 C++ 侧堆分配的包装结构指针，内含引擎对象的引用计数引用（对应 Lua 侧 userdata）。
    /// 对象只能通过各派生类的静态 Create 工厂创建；<see cref="Dispose"/> 销毁（对应 Lua userdata 的 __gc），
    /// 销毁后访问抛出 <see cref="ObjectDisposedException"/>；全部成员数据经由引擎调用访问，不在托管侧缓存。
    /// </summary>
    public abstract class ModernGraphicsObject : IDisposable
    {
        /// <summary>C++ 侧句柄，0 表示已销毁</summary>
        internal nint Handle;

        private bool _disposed;

        internal ModernGraphicsObject(nint handle)
        {
            Handle = handle;
        }

        /// <summary>是否已销毁</summary>
        public bool IsDisposed => _disposed;

        /// <summary>访问句柄前检查对象有效性</summary>
        protected internal void ThrowIfDisposed()
        {
            if (_disposed || Handle == 0)
            {
                throw new ObjectDisposedException(GetType().Name, "图形对象已销毁");
            }
        }

        /// <summary>调用对应的句柄释放 API（delete 包装结构并释放引擎引用）</summary>
        protected abstract void DestroyNative();

        /// <summary>销毁对象并释放引擎资源（对应 Lua userdata 的 __gc）</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (Handle != 0)
            {
                DestroyNative();
                Handle = 0;
            }
        }

        // 注意：不实现终结器。引擎关闭后回调非托管释放可能访问已销毁的引擎对象
        // （与 GameObjectBase 的策略一致），使用者须显式 Dispose。
    }
}
