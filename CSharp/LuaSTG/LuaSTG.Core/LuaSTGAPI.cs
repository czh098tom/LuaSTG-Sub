using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace LuaSTG.Core
{
    /// <summary>
    /// CoreCLR 宿主入口与引擎 API 访问层。
    /// </summary>
    public static unsafe partial class LuaSTGAPI
    {
        /// <summary>引擎提供的非托管 API 表（启动时由引擎填充）</summary>
        internal static UnmanagedAPI api;

        /// <summary>托管应用实例</summary>
        private static ILuaSTGApp? app;

        /// <summary>引擎编译特性位（见 <see cref="EngineFeatures"/>）</summary>
        internal static uint EngineFeatureFlags;

        /// <summary>对象池容量</summary>
        public static int ObjectPoolSize { get; private set; }

        // 引擎特性位（与 C++ 侧 CLRGameObjectLayoutInfo.feature_flags 对应）
        internal const uint FeatureMultiGameWorld = 1u << 0;
        internal const uint FeatureUserSystemOperation = 1u << 1;
        internal const uint FeatureGlobalScaleColliShape = 1u << 2;
        internal const uint FeatureGameObjectParticleSystemObject = 1u << 3;
        internal const uint FeatureGameObjectPropertyPause = 1u << 4;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        internal static int StartUp(UnmanagedAPI* unmanaged, ManagedAPI* managed)
        {
            try
            {
                // API 表一致性校验
                if (unmanaged->UnmanagedApiCount != UnmanagedAPI.ExpectedApiCount)
                {
                    return 2;
                }
                api = *unmanaged;

                // GameObject 布局校验
                var layout = (CLRGameObjectLayoutInfo*)api.getGameObjectLayout();
                NativeGameObject.Validate(*layout);
                EngineFeatureFlags = layout->feature_flags;
                ObjectPoolSize = (int)layout->pool_size;

                AssignManagedAPI(managed);
                LoadAppAssembly();

                return 0;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        /// <summary>
        /// 输出日志到引擎。
        /// </summary>
        public static void Log(LogLevel level, string message)
        {
            using var text = new MarshaledString(message);
            api.log((int)level, text);
        }

        /// <summary>引擎主版本号</summary>
        public static uint VersionMajor => api.getVersionMajor();

        /// <summary>引擎次版本号</summary>
        public static uint VersionMinor => api.getVersionMinor();

        /// <summary>引擎修订版本号</summary>
        public static uint VersionPatch => api.getVersionPatch();

        /// <summary>引擎版本名称</summary>
        public static string VersionName => StringMarshal.FromUtf8(api.getVersionName());

        /// <summary>引擎分支名称</summary>
        public static string BranchName => StringMarshal.FromUtf8(api.getBranchName());

        /// <summary>设置窗口化/全屏</summary>
        public static void SetWindowed(bool value) => api.setWindowed(value ? (byte)1 : (byte)0);

        /// <summary>设置垂直同步</summary>
        public static void SetVsync(bool value) => api.setVsync(value ? (byte)1 : (byte)0);

        /// <summary>设置分辨率</summary>
        public static void SetResolution(uint width, uint height) => api.setResolution(width, height);

        /// <summary>设置目标帧率</summary>
        public static void SetFPS(uint fps) => api.setTargetFPS(fps);

        /// <summary>获取当前平均帧率</summary>
        public static double GetFPS() => api.getFPS();

        /// <summary>设置窗口标题</summary>
        public static void SetTitle(string title)
        {
            using var t = new MarshaledString(title);
            api.setWindowTitle(t);
        }

        /// <summary>设置是否显示鼠标（对应 lstg.SetSplash）</summary>
        public static void SetSplash(bool value) => api.setSplash(value ? (byte)1 : (byte)0);

        private static void LoadAppAssembly()
        {
            var currAssembly = typeof(LuaSTGAPI).Assembly;
            var dir = Path.GetDirectoryName(currAssembly.Location);
            if (dir == null) return;
            Assembly mainAssembly = AssemblyLoadContext.GetLoadContext(currAssembly)
                ?.LoadFromAssemblyPath(Path.Combine(dir, "LuaSTG.dll")) ?? currAssembly;

            LoadDependencyRecursively(mainAssembly);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("LuaSTG.LuaSTGAppFactory");
                if (type != null)
                {
                    app = (Activator.CreateInstance(type) as ILuaSTGAppFactory)?.GetApplication();
                    break;
                }
            }
        }

        private static void LoadDependencyRecursively(Assembly assembly)
        {
            var resolver = new AssemblyDependencyResolver(assembly.Location);
            foreach (var an in assembly.GetReferencedAssemblies())
            {
                if (an.Name?.StartsWith("LuaSTG") ?? false)
                {
                    Assembly loaded;
                    var path = resolver.ResolveAssemblyToPath(an);
                    if (path != null)
                    {
                        loaded = Assembly.LoadFrom(path);
                    }
                    else
                    {
                        loaded = Assembly.Load(an);
                    }
                    LoadDependencyRecursively(loaded);
                }
            }
        }
    }
}
