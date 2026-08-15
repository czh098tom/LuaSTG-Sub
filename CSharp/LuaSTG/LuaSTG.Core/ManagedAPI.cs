using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuaSTG.Core
{
    /// <summary>
    /// 托管侧（C#）提供给引擎的回调函数表。
    /// 与 C++ 侧 CLRBinding.hpp 中的 ManagedAPI 保持同步。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ManagedAPI
    {
        public uint ManagedApiCount;

        public delegate* unmanaged[Stdcall]<void> GameInit;
        public delegate* unmanaged[Stdcall]<byte> FrameFunc;
        public delegate* unmanaged[Stdcall]<void> RenderFunc;
        public delegate* unmanaged[Stdcall]<void> GameExit;
        public delegate* unmanaged[Stdcall]<void> FocusLoseFunc;
        public delegate* unmanaged[Stdcall]<void> FocusGainFunc;
        public delegate* unmanaged[Stdcall]<byte, byte, void> EventFunc;

        public delegate* unmanaged[Stdcall]<uint, void> DetachGameObject;
        public delegate* unmanaged[Stdcall]<uint, void> CallOnFrame;
        public delegate* unmanaged[Stdcall]<uint, void> CallOnRender;
        public delegate* unmanaged[Stdcall]<uint, byte, void> CallOnDestroy;
        public delegate* unmanaged[Stdcall]<uint, uint, void> CallOnColli;
    }

    public static unsafe partial class LuaSTGAPI
    {
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static void GameInit()
        {
            try { app?.GameInit(); }
            catch (Exception e) { Log(LogLevel.Error, $"GameInit 异常：{e}"); }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static byte FrameFunc()
        {
            try { return (byte)((app?.FrameFunc() ?? false) ? 1 : 0); }
            catch (Exception e)
            {
                Log(LogLevel.Error, $"FrameFunc 异常：{e}");
                return 1; // 异常时请求退出，避免错误刷屏
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static void RenderFunc()
        {
            try { app?.RenderFunc(); }
            catch (Exception e) { Log(LogLevel.Error, $"RenderFunc 异常：{e}"); }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static void GameExit()
        {
            try { app?.GameExit(); }
            catch (Exception e) { Log(LogLevel.Error, $"GameExit 异常：{e}"); }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static void FocusGainFunc()
        {
            try { app?.FocusGainFunc(); }
            catch (Exception e) { Log(LogLevel.Error, $"FocusGainFunc 异常：{e}"); }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static void FocusLoseFunc()
        {
            try { app?.FocusLoseFunc(); }
            catch (Exception e) { Log(LogLevel.Error, $"FocusLoseFunc 异常：{e}"); }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static void EventFunc(byte eventType, byte state)
        {
            try { app?.EventFunc((EngineEvent)eventType, state != 0); }
            catch (Exception e) { Log(LogLevel.Error, $"EventFunc 异常：{e}"); }
        }

        private static void AssignManagedAPI(ManagedAPI* managed)
        {
            managed->ManagedApiCount = 12;

            managed->GameInit = &GameInit;
            managed->FrameFunc = &FrameFunc;
            managed->RenderFunc = &RenderFunc;
            managed->GameExit = &GameExit;
            managed->FocusGainFunc = &FocusGainFunc;
            managed->FocusLoseFunc = &FocusLoseFunc;
            managed->EventFunc = &EventFunc;

            managed->DetachGameObject = &GameObjectBase.GameObjectCallbacks.DetachGameObject;
            managed->CallOnFrame = &GameObjectBase.GameObjectCallbacks.CallOnFrame;
            managed->CallOnRender = &GameObjectBase.GameObjectCallbacks.CallOnRender;
            managed->CallOnDestroy = &GameObjectBase.GameObjectCallbacks.CallOnDestroy;
            managed->CallOnColli = &GameObjectBase.GameObjectCallbacks.CallOnColli;
        }
    }
}
