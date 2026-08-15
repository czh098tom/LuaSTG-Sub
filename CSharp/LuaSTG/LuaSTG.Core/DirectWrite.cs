using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LuaSTG.Core
{
    /// <summary>
    /// DirectWrite 文字排版模块（对应 Lua 侧 DirectWrite 全局表，
    /// 由 LuaBinding/external/lua_dwrite.cpp 的 luaopen_dwrite 注册）。
    /// 模块函数与枚举/结构/对象类型均以此类为命名空间，与 Lua 侧 DirectWrite.* 一一对应。
    /// </summary>
    public static unsafe partial class DirectWrite
    {
        // ========== 枚举（值与 DirectWrite 原生枚举 / Lua 侧枚举表一致） ==========

        /// <summary>字体拉伸度（对应 Lua 侧 DirectWrite.FontStretch 表）</summary>
        public enum FontStretch : int
        {
            /// <summary>未知（0）</summary>
            Undefined = 0,
            /// <summary>极窄（1）</summary>
            UltraCondensed = 1,
            /// <summary>较窄（2）</summary>
            ExtraCondensed = 2,
            /// <summary>窄（3）</summary>
            Condensed = 3,
            /// <summary>略窄（4）</summary>
            SemiCondensed = 4,
            /// <summary>标准（5，与 Medium 同值）</summary>
            Normal = 5,
            /// <summary>中等（5，与 Normal 同值）</summary>
            Medium = 5,
            /// <summary>略宽（6）</summary>
            SemiExpanded = 6,
            /// <summary>宽（7）</summary>
            Expanded = 7,
            /// <summary>较宽（8）</summary>
            ExtraExpanded = 8,
            /// <summary>极宽（9）</summary>
            UltraExpanded = 9,
        }

        /// <summary>字体倾斜样式（对应 Lua 侧 DirectWrite.FontStyle 表）</summary>
        public enum FontStyle : int
        {
            /// <summary>正常（0）</summary>
            Normal = 0,
            /// <summary>倾斜（1）</summary>
            Oblique = 1,
            /// <summary>斜体（2）</summary>
            Italic = 2,
        }

        /// <summary>字重（对应 Lua 侧 DirectWrite.FontWeight 表）</summary>
        public enum FontWeight : int
        {
            /// <summary>极细（100）</summary>
            Thin = 100,
            /// <summary>超细（200，与 UltraLight 同值）</summary>
            ExtraLight = 200,
            /// <summary>超细（200，与 ExtraLight 同值）</summary>
            UltraLight = 200,
            /// <summary>细（300）</summary>
            Light = 300,
            /// <summary>半细（350）</summary>
            SemiLight = 350,
            /// <summary>标准（400，与 Regular 同值）</summary>
            Normal = 400,
            /// <summary>常规（400，与 Normal 同值）</summary>
            Regular = 400,
            /// <summary>中等（500）</summary>
            Medium = 500,
            /// <summary>半粗（600，与 SemiBold 同值）</summary>
            DemiBold = 600,
            /// <summary>半粗（600，与 DemiBold 同值）</summary>
            SemiBold = 600,
            /// <summary>粗（700）</summary>
            Bold = 700,
            /// <summary>超粗（800，与 UltraBold 同值）</summary>
            ExtraBold = 800,
            /// <summary>超粗（800，与 ExtraBold 同值）</summary>
            UltraBold = 800,
            /// <summary>黑体（900，与 Heavy 同值）</summary>
            Black = 900,
            /// <summary>黑体（900，与 Black 同值；Lua 侧表中拼写为 "Heacy"）</summary>
            Heavy = 900,
            /// <summary>超黑（950，与 UltraBlack 同值）</summary>
            ExtraBlack = 950,
            /// <summary>超黑（950，与 ExtraBlack 同值）</summary>
            UltraBlack = 950,
        }

        /// <summary>行距计算方式（对应 Lua 侧 DirectWrite.LineSpacingMethod 表）</summary>
        public enum LineSpacingMethod : int
        {
            /// <summary>默认（0）</summary>
            Default = 0,
            /// <summary>统一行距（1）</summary>
            Uniform = 1,
        }

        /// <summary>文本对齐方式（对应 Lua 侧 DirectWrite.TextAlignment 表）</summary>
        public enum TextAlignment : int
        {
            /// <summary>前缘对齐（0）</summary>
            Leading = 0,
            /// <summary>后缘对齐（1）</summary>
            Trailing = 1,
            /// <summary>居中（2）</summary>
            Center = 2,
            /// <summary>两端对齐（3）</summary>
            Justified = 3,
        }

        /// <summary>段落对齐方式（对应 Lua 侧 DirectWrite.ParagraphAlignment 表）</summary>
        public enum ParagraphAlignment : int
        {
            /// <summary>近端（0）</summary>
            Near = 0,
            /// <summary>远端（1）</summary>
            Far = 1,
            /// <summary>居中（2）</summary>
            Center = 2,
        }

        /// <summary>文字流方向（对应 Lua 侧 DirectWrite.FlowDirection 表）</summary>
        public enum FlowDirection : int
        {
            /// <summary>自上而下（0）</summary>
            TopToBottom = 0,
            /// <summary>自下而上（1）</summary>
            BottomToTop = 1,
            /// <summary>自左向右（2）</summary>
            LeftToRight = 2,
            /// <summary>自右向左（3）</summary>
            RightToLeft = 3,
        }

        /// <summary>阅读方向（对应 Lua 侧 DirectWrite.ReadingDirection 表）</summary>
        public enum ReadingDirection : int
        {
            /// <summary>自左向右（0）</summary>
            LeftToRight = 0,
            /// <summary>自右向左（1）</summary>
            RightToLeft = 1,
        }

        /// <summary>换行方式（对应 Lua 侧 DirectWrite.WordWrapping 表）</summary>
        public enum WordWrapping : int
        {
            /// <summary>换行（0）</summary>
            Wrap = 0,
            /// <summary>不换行（1）</summary>
            NoWrap = 1,
        }

        // ========== 数据结构（对应 Lua 侧 userdata 结构，纯字段值类型） ==========

        /// <summary>
        /// 文本度量（对应 Lua 侧 DirectWrite.TextMetrics userdata，字段与 DWRITE_TEXT_METRICS 一致）。
        /// 通过 <see cref="TextLayout.GetMetrics"/> 填充。
        /// </summary>
        public struct TextMetrics
        {
            /// <summary>文本左缘相对布局原点的偏移</summary>
            public double Left;
            /// <summary>文本上缘相对布局原点的偏移</summary>
            public double Top;
            /// <summary>文本宽度（不含尾随空白）</summary>
            public double Width;
            /// <summary>文本宽度（含尾随空白）</summary>
            public double WidthIncludingTrailingWhitespace;
            /// <summary>文本高度</summary>
            public double Height;
            /// <summary>布局宽度</summary>
            public double LayoutWidth;
            /// <summary>布局高度</summary>
            public double LayoutHeight;
            /// <summary>双向重排最大深度（整数值）</summary>
            public double MaxBidiReorderingDepth;
            /// <summary>行数（整数值）</summary>
            public double LineCount;
        }

        /// <summary>
        /// 文本出格度量（对应 Lua 侧 DirectWrite.OverhangMetrics userdata，字段与 DWRITE_OVERHANG_METRICS 一致）。
        /// 通过 <see cref="TextLayout.GetOverhangMetrics"/> 填充。
        /// </summary>
        public struct OverhangMetrics
        {
            /// <summary>左缘出格量</summary>
            public double Left;
            /// <summary>上缘出格量</summary>
            public double Top;
            /// <summary>右缘出格量</summary>
            public double Right;
            /// <summary>下缘出格量</summary>
            public double Bottom;
        }

        // ========== 内部工具 ==========

        /// <summary>DirectWrite 对象包装基类（仿 Graphics/GraphicsObject.cs 的 ModernGraphicsObject）</summary>
        public abstract class DirectWriteObject : IDisposable
        {
            /// <summary>C++ 侧句柄，0 表示已销毁</summary>
            internal nint Handle;

            private bool _disposed;

            internal DirectWriteObject(nint handle)
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
                    throw new ObjectDisposedException(GetType().Name, "DirectWrite 对象已销毁");
                }
            }

            /// <summary>调用对应的句柄销毁 API（delete 包装结构并释放引擎引用）</summary>
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
            // （与 ModernGraphicsObject 的策略一致），使用者须显式 Dispose。
        }

        /// <summary>
        /// C# 字符串到 UTF-16 非托管字符串（const char16_t*）的临时封送。
        /// 用法：using var s = new MarshaledString16(text); api.foo(s);
        /// </summary>
        internal unsafe struct MarshaledString16 : IDisposable
        {
            private char* _ptr;

            public readonly char* Pointer => _ptr;

            public MarshaledString16(string? text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    _ptr = (char*)NativeMemory.Alloc(2);
                    *_ptr = '\0';
                    return;
                }
                var bytes = (nuint)((text.Length + 1) * sizeof(char));
                _ptr = (char*)NativeMemory.Alloc(bytes);
                fixed (char* p = text)
                {
                    Buffer.MemoryCopy(p, _ptr, bytes, (nuint)(text.Length * sizeof(char)));
                }
                _ptr[text.Length] = '\0';
            }

            public void Dispose()
            {
                if (_ptr != null)
                {
                    NativeMemory.Free(_ptr);
                    _ptr = null;
                }
            }

            public static implicit operator char*(in MarshaledString16 value) => value._ptr;
        }

        // ========== 模块函数（对应 DirectWrite.* 全局函数） ==========

        /// <summary>
        /// 从字体文件列表创建自定义字体集（对应 DirectWrite.CreateFontCollection）。
        /// 路径可来自引擎打包文件系统。失败返回 null，详见引擎日志。
        /// </summary>
        /// <param name="paths">字体文件路径列表（UTF-8 路径）</param>
        public static FontCollection? CreateFontCollection(IReadOnlyList<string>? paths)
        {
            var count = paths?.Count ?? 0;
            if (count == 0)
            {
                var empty = LuaSTGAPI.api.dwrite_createFontCollection(null, 0);
                return empty != 0 ? new FontCollection((nint)empty) : null;
            }
            var marshaled = new MarshaledString[count];
            var pointers = new byte*[count];
            try
            {
                for (var i = 0; i < count; i++)
                {
                    marshaled[i] = new MarshaledString(paths![i]);
                    pointers[i] = marshaled[i].Pointer;
                }
                fixed (byte** pinned = pointers)
                {
                    var handle = LuaSTGAPI.api.dwrite_createFontCollection(pinned, (uint)count);
                    return handle != 0 ? new FontCollection((nint)handle) : null;
                }
            }
            finally
            {
                for (var i = 0; i < count; i++)
                {
                    marshaled[i].Dispose();
                }
            }
        }

        /// <summary>
        /// 创建文本格式（对应 DirectWrite.CreateTextFormat）。
        /// fontCollection 传 null 表示使用系统字体集。失败返回 null，详见引擎日志。
        /// </summary>
        /// <param name="familyName">字体族名</param>
        /// <param name="fontCollection">自定义字体集，null 使用系统字体集</param>
        /// <param name="fontWeight">字重</param>
        /// <param name="fontStyle">倾斜样式</param>
        /// <param name="fontStretch">拉伸度</param>
        /// <param name="fontSize">字号（DIP）</param>
        /// <param name="localeName">区域名</param>
        public static TextFormat? CreateTextFormat(
            string familyName, FontCollection? fontCollection,
            FontWeight fontWeight, FontStyle fontStyle, FontStretch fontStretch,
            float fontSize, string localeName)
        {
            if (familyName is null)
            {
                throw new ArgumentNullException(nameof(familyName));
            }
            if (localeName is null)
            {
                throw new ArgumentNullException(nameof(localeName));
            }
            using var family = new MarshaledString16(familyName);
            using var locale = new MarshaledString16(localeName);
            var handle = LuaSTGAPI.api.dwrite_createTextFormat(
                family, fontCollection != null ? (nuint)fontCollection.Handle : 0u,
                (int)fontWeight, (int)fontStyle, (int)fontStretch,
                fontSize, locale);
            return handle != 0 ? new TextFormat((nint)handle) : null;
        }

        /// <summary>
        /// 创建文本度量结构（对应 DirectWrite.CreateTextMetrics，Lua 侧创建零值 userdata）。
        /// 返回零值结构，通常直接使用 <see cref="TextLayout.GetMetrics"/> 的返回值。
        /// </summary>
        public static TextMetrics CreateTextMetrics() => default;

        /// <summary>
        /// 创建出格度量结构（对应 DirectWrite.CreateOverhangMetrics，Lua 侧创建零值 userdata）。
        /// 返回零值结构，通常直接使用 <see cref="TextLayout.GetOverhangMetrics"/> 的返回值。
        /// </summary>
        public static OverhangMetrics CreateOverhangMetrics() => default;

        /// <summary>
        /// 从文本与格式创建文本布局（对应 DirectWrite.CreateTextLayout）。
        /// 失败返回 null，详见引擎日志。
        /// </summary>
        /// <param name="text">文本（UTF-16）</param>
        /// <param name="textFormat">文本格式</param>
        /// <param name="maxWidth">布局最大宽度</param>
        /// <param name="maxHeight">布局最大高度</param>
        public static TextLayout? CreateTextLayout(string text, TextFormat textFormat, float maxWidth, float maxHeight)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (textFormat is null)
            {
                throw new ArgumentNullException(nameof(textFormat));
            }
            textFormat.ThrowIfDisposed();
            using var s = new MarshaledString16(text);
            var handle = LuaSTGAPI.api.dwrite_createTextLayout(
                s, (uint)text.Length, (nuint)textFormat.Handle, maxWidth, maxHeight);
            return handle != 0 ? new TextLayout((nint)handle) : null;
        }

        /// <summary>
        /// 创建文本渲染器（对应 DirectWrite.CreateTextRenderer）。失败返回 null。
        /// </summary>
        public static TextRenderer? CreateTextRenderer()
        {
            var handle = LuaSTGAPI.api.dwrite_createTextRenderer();
            return handle != 0 ? new TextRenderer((nint)handle) : null;
        }

        /// <summary>
        /// 将文本布局栅格化为纹理并放入资源池（对应 DirectWrite.CreateTextureFromTextLayout，
        /// Lua 侧 4/5/6 参数形式的缺省值由此重载补全：outlineWidth = 0、fontColor = 白、outlineColor = 黑）。
        /// </summary>
        /// <param name="textLayout">文本布局</param>
        /// <param name="pool">资源池（Global 或 Stage，对应 Lua 侧 "global"/"stage"）</param>
        /// <param name="textureName">纹理资源名</param>
        /// <exception cref="ArgumentException">资源池类型无效或纹理已存在</exception>
        /// <exception cref="InvalidOperationException">栅格化或纹理创建失败</exception>
        public static void CreateTextureFromTextLayout(
            TextLayout textLayout, ResourcePoolType pool, string textureName)
            => CreateTextureFromTextLayout(textLayout, pool, textureName, 0.0f, Color.FromArgb(0xFFFFFFFFu), Color.FromArgb(0xFF000000u));

        /// <summary>
        /// 将文本布局栅格化为纹理并放入资源池（对应 DirectWrite.CreateTextureFromTextLayout 的 4 参数形式）。
        /// </summary>
        /// <param name="textLayout">文本布局</param>
        /// <param name="pool">资源池（Global 或 Stage）</param>
        /// <param name="textureName">纹理资源名</param>
        /// <param name="outlineWidth">描边宽度，&gt; 0.0001 时启用描边</param>
        public static void CreateTextureFromTextLayout(
            TextLayout textLayout, ResourcePoolType pool, string textureName, float outlineWidth)
            => CreateTextureFromTextLayout(textLayout, pool, textureName, outlineWidth, Color.FromArgb(0xFFFFFFFFu), Color.FromArgb(0xFF000000u));

        /// <summary>
        /// 将文本布局栅格化为纹理并放入资源池（对应 DirectWrite.CreateTextureFromTextLayout 的 6 参数形式）。
        /// </summary>
        /// <param name="textLayout">文本布局</param>
        /// <param name="pool">资源池（Global 或 Stage）</param>
        /// <param name="textureName">纹理资源名</param>
        /// <param name="outlineWidth">描边宽度，&gt; 0.0001 时启用描边</param>
        /// <param name="fontColor">文字颜色（Lua 侧未传时为白色）</param>
        /// <param name="outlineColor">描边颜色（Lua 侧未传时为黑色）</param>
        public static void CreateTextureFromTextLayout(
            TextLayout textLayout, ResourcePoolType pool, string textureName,
            float outlineWidth, Color fontColor, Color outlineColor)
        {
            if (textLayout is null)
            {
                throw new ArgumentNullException(nameof(textLayout));
            }
            if (textureName is null)
            {
                throw new ArgumentNullException(nameof(textureName));
            }
            textLayout.ThrowIfDisposed();
            using var name = new MarshaledString(textureName);
            var code = LuaSTGAPI.api.dwrite_createTextureFromTextLayout(
                (nuint)textLayout.Handle, (int)pool, name, outlineWidth, fontColor.Argb, outlineColor.Argb);
            switch (code)
            {
                case 0:
                    return;
                case 1:
                    throw new ArgumentException($"无效的资源池类型 '{pool}'", nameof(pool));
                case 2:
                    throw new ArgumentException($"纹理 '{textureName}' 已存在", nameof(textureName));
                case 8:
                    throw new ObjectDisposedException(nameof(TextLayout), "DirectWrite 对象已销毁");
                default:
                    throw new InvalidOperationException("DirectWrite.CreateTextureFromTextLayout 栅格化纹理失败");
            }
        }

        /// <summary>
        /// 将文本布局栅格化并保存为 PNG 文件（对应 DirectWrite.SaveTextLayoutToFile 的 2 参数形式，不描边）。
        /// </summary>
        /// <param name="textLayout">文本布局</param>
        /// <param name="filePath">输出文件路径</param>
        public static void SaveTextLayoutToFile(TextLayout textLayout, string filePath)
        {
            if (textLayout is null)
            {
                throw new ArgumentNullException(nameof(textLayout));
            }
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }
            textLayout.ThrowIfDisposed();
            using var path = new MarshaledString16(filePath);
            var code = LuaSTGAPI.api.dwrite_saveTextLayoutToFile((nuint)textLayout.Handle, path, 0.0f, 0);
            ThrowSaveError(code, filePath);
        }

        /// <summary>
        /// 将文本布局栅格化并保存为 PNG 文件（对应 DirectWrite.SaveTextLayoutToFile 的 3 参数形式，黑色描边）。
        /// </summary>
        /// <param name="textLayout">文本布局</param>
        /// <param name="filePath">输出文件路径</param>
        /// <param name="outlineWidth">描边宽度</param>
        public static void SaveTextLayoutToFile(TextLayout textLayout, string filePath, float outlineWidth)
        {
            if (textLayout is null)
            {
                throw new ArgumentNullException(nameof(textLayout));
            }
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }
            textLayout.ThrowIfDisposed();
            using var path = new MarshaledString16(filePath);
            var code = LuaSTGAPI.api.dwrite_saveTextLayoutToFile((nuint)textLayout.Handle, path, outlineWidth, 1);
            ThrowSaveError(code, filePath);
        }

        private static void ThrowSaveError(byte code, string filePath)
        {
            switch (code)
            {
                case 0:
                    return;
                case 5:
                    throw new ObjectDisposedException(nameof(TextLayout), "DirectWrite 对象已销毁");
                default:
                    throw new InvalidOperationException($"DirectWrite.SaveTextLayoutToFile 保存 '{filePath}' 失败");
            }
        }

        // ========== FontCollection（对应 DirectWrite.FontCollection userdata） ==========

        /// <summary>
        /// 自定义字体集（对应 Lua 侧 DirectWrite.FontCollection userdata）。
        /// 通过 <see cref="CreateFontCollection"/> 工厂创建；<see cref="Dispose"/> 销毁
        /// （注销 DirectWrite 字体集加载器），销毁后访问抛出 <see cref="ObjectDisposedException"/>。
        /// </summary>
        public sealed unsafe class FontCollection : DirectWriteObject
        {
            internal FontCollection(nint handle) : base(handle) { }

            protected override void DestroyNative()
                => LuaSTGAPI.api.dwrite_fontCollectionDestroy((nuint)Handle);

            /// <summary>
            /// 获取字体集的详细信息（对应 FontCollection:GetDebugInformation），
            /// 返回多行 UTF-8 文本转成的字符串。
            /// </summary>
            public string GetDebugInformation()
            {
                ThrowIfDisposed();
                return StringMarshal.FromUtf8(LuaSTGAPI.api.dwrite_fontCollectionGetDebugInformation((nuint)Handle));
            }
        }

        // ========== TextFormat（对应 DirectWrite.TextFormat userdata） ==========

        /// <summary>
        /// 文本格式（对应 Lua 侧 DirectWrite.TextFormat userdata）。
        /// 通过 <see cref="CreateTextFormat"/> 工厂创建；<see cref="Dispose"/> 销毁，
        /// 销毁后访问抛出 <see cref="ObjectDisposedException"/>。
        /// </summary>
        public sealed unsafe class TextFormat : DirectWriteObject
        {
            internal TextFormat(nint handle) : base(handle) { }

            protected override void DestroyNative()
                => LuaSTGAPI.api.dwrite_textFormatDestroy((nuint)Handle);
        }

        // ========== TextLayout（对应 DirectWrite.TextLayout userdata） ==========

        /// <summary>
        /// 文本布局（对应 Lua 侧 DirectWrite.TextLayout userdata）。
        /// 通过 <see cref="CreateTextLayout"/> 工厂创建；<see cref="Dispose"/> 销毁，
        /// 销毁后访问抛出 <see cref="ObjectDisposedException"/>。
        /// 带 position/length 参数的方法对应 DWRITE_TEXT_RANGE{position, length}。
        /// </summary>
        public sealed unsafe class TextLayout : DirectWriteObject
        {
            internal TextLayout(nint handle) : base(handle) { }

            protected override void DestroyNative()
                => LuaSTGAPI.api.dwrite_textLayoutDestroy((nuint)Handle);

            private static void Check(byte code, string what)
            {
                if (code == 2)
                {
                    throw new ObjectDisposedException(nameof(TextLayout), "DirectWrite 对象已销毁");
                }
                if (code != 0)
                {
                    throw new InvalidOperationException($"{what} failed");
                }
            }

            /// <summary>设置指定范围的字体集（对应 TextLayout:SetFontCollection）</summary>
            /// <exception cref="ArgumentException">字体集句柄无效</exception>
            public void SetFontCollection(FontCollection fontCollection, uint position, uint length)
            {
                ThrowIfDisposed();
                if (fontCollection is null)
                {
                    throw new ArgumentNullException(nameof(fontCollection));
                }
                fontCollection.ThrowIfDisposed();
                var code = LuaSTGAPI.api.dwrite_textLayoutSetFontCollection(
                    (nuint)Handle, (nuint)fontCollection.Handle, position, length);
                Check(code, "TextLayout:SetFontCollection");            }

            /// <summary>设置指定范围的字体族名（对应 TextLayout:SetFontFamilyName）</summary>
            public void SetFontFamilyName(string name, uint position, uint length)
            {
                ThrowIfDisposed();
                if (name is null)
                {
                    throw new ArgumentNullException(nameof(name));
                }
                using var n = new MarshaledString16(name);
                Check(LuaSTGAPI.api.dwrite_textLayoutSetFontFamilyName((nuint)Handle, n, position, length),
                    "TextLayout:SetFontFamilyName");
            }

            /// <summary>设置指定范围的区域名（对应 TextLayout:SetLocaleName）</summary>
            public void SetLocaleName(string name, uint position, uint length)
            {
                ThrowIfDisposed();
                if (name is null)
                {
                    throw new ArgumentNullException(nameof(name));
                }
                using var n = new MarshaledString16(name);
                Check(LuaSTGAPI.api.dwrite_textLayoutSetLocaleName((nuint)Handle, n, position, length),
                    "TextLayout:SetLocaleName");
            }

            /// <summary>设置指定范围的字号（对应 TextLayout:SetFontSize）</summary>
            public void SetFontSize(float fontSize, uint position, uint length)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetFontSize((nuint)Handle, fontSize, position, length),
                    "TextLayout:SetFontSize");
            }

            /// <summary>设置指定范围的倾斜样式（对应 TextLayout:SetFontStyle）</summary>
            public void SetFontStyle(FontStyle fontStyle, uint position, uint length)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetFontStyle((nuint)Handle, (int)fontStyle, position, length),
                    "TextLayout:SetFontStyle");
            }

            /// <summary>设置指定范围的字重（对应 TextLayout:SetFontWeight）</summary>
            public void SetFontWeight(FontWeight fontWeight, uint position, uint length)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetFontWeight((nuint)Handle, (int)fontWeight, position, length),
                    "TextLayout:SetFontWeight");
            }

            /// <summary>设置指定范围的拉伸度（对应 TextLayout:SetFontStretch）</summary>
            public void SetFontStretch(FontStretch fontStretch, uint position, uint length)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetFontStretch((nuint)Handle, (int)fontStretch, position, length),
                    "TextLayout:SetFontStretch");
            }

            /// <summary>设置指定范围的删除线（对应 TextLayout:SetStrikethrough）</summary>
            public void SetStrikethrough(bool enable, uint position, uint length)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetStrikethrough((nuint)Handle, enable ? (byte)1 : (byte)0, position, length),
                    "TextLayout:SetStrikethrough");
            }

            /// <summary>设置指定范围的下划线（对应 TextLayout:SetUnderline）</summary>
            public void SetUnderline(bool enable, uint position, uint length)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetUnderline((nuint)Handle, enable ? (byte)1 : (byte)0, position, length),
                    "TextLayout:SetUnderline");
            }

            /// <summary>设置增量制表位宽度（对应 TextLayout:SetIncrementalTabStop）</summary>
            public void SetIncrementalTabStop(float tabSize)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetIncrementalTabStop((nuint)Handle, tabSize),
                    "TextLayout:SetIncrementalTabStop");
            }

            /// <summary>设置行距（对应 TextLayout:SetLineSpacing）</summary>
            public void SetLineSpacing(LineSpacingMethod method, float lineSpacing, float baseline)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetLineSpacing((nuint)Handle, (int)method, lineSpacing, baseline),
                    "TextLayout:SetLineSpacing");
            }

            /// <summary>设置文本对齐（对应 TextLayout:SetTextAlignment）</summary>
            public void SetTextAlignment(TextAlignment align)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetTextAlignment((nuint)Handle, (int)align),
                    "TextLayout:SetTextAlignment");
            }

            /// <summary>设置段落对齐（对应 TextLayout:SetParagraphAlignment）</summary>
            public void SetParagraphAlignment(ParagraphAlignment align)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetParagraphAlignment((nuint)Handle, (int)align),
                    "TextLayout:SetParagraphAlignment");
            }

            /// <summary>设置文字流方向（对应 TextLayout:SetFlowDirection）</summary>
            public void SetFlowDirection(FlowDirection direction)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetFlowDirection((nuint)Handle, (int)direction),
                    "TextLayout:SetFlowDirection");
            }

            /// <summary>设置阅读方向（对应 TextLayout:SetReadingDirection）</summary>
            public void SetReadingDirection(ReadingDirection direction)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetReadingDirection((nuint)Handle, (int)direction),
                    "TextLayout:SetReadingDirection");
            }

            /// <summary>设置换行方式（对应 TextLayout:SetWordWrapping）</summary>
            public void SetWordWrapping(WordWrapping wrapping)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetWordWrapping((nuint)Handle, (int)wrapping),
                    "TextLayout:SetWordWrapping");
            }

            /// <summary>设置布局最大宽度（对应 TextLayout:SetMaxWidth）</summary>
            public void SetMaxWidth(float maxWidth)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetMaxWidth((nuint)Handle, maxWidth),
                    "TextLayout:SetMaxWidth");
            }

            /// <summary>设置布局最大高度（对应 TextLayout:SetMaxHeight）</summary>
            public void SetMaxHeight(float maxHeight)
            {
                ThrowIfDisposed();
                Check(LuaSTGAPI.api.dwrite_textLayoutSetMaxHeight((nuint)Handle, maxHeight),
                    "TextLayout:SetMaxHeight");
            }

            /// <summary>计算可换行时的最小宽度（对应 TextLayout:DetermineMinWidth）</summary>
            /// <exception cref="InvalidOperationException">引擎调用失败</exception>
            public float DetermineMinWidth()
            {
                ThrowIfDisposed();
                float minWidth = 0.0f;
                var code = LuaSTGAPI.api.dwrite_textLayoutDetermineMinWidth((nuint)Handle, &minWidth);
                if (code != 0)
                {
                    throw new InvalidOperationException("TextLayout:DetermineMinWidth failed");
                }
                return minWidth;
            }

            /// <summary>获取文本度量（对应 TextLayout:GetMetrics）</summary>
            /// <exception cref="InvalidOperationException">引擎调用失败</exception>
            public TextMetrics GetMetrics()
            {
                ThrowIfDisposed();
                var values = stackalloc double[9];
                var code = LuaSTGAPI.api.dwrite_textLayoutGetMetrics((nuint)Handle, values);
                if (code != 0)
                {
                    throw new InvalidOperationException("TextLayout:GetMetrics failed");
                }
                return new TextMetrics
                {
                    Left = values[0],
                    Top = values[1],
                    Width = values[2],
                    WidthIncludingTrailingWhitespace = values[3],
                    Height = values[4],
                    LayoutWidth = values[5],
                    LayoutHeight = values[6],
                    MaxBidiReorderingDepth = values[7],
                    LineCount = values[8],
                };
            }

            /// <summary>获取文本出格度量（对应 TextLayout:GetOverhangMetrics）</summary>
            /// <exception cref="InvalidOperationException">引擎调用失败</exception>
            public OverhangMetrics GetOverhangMetrics()
            {
                ThrowIfDisposed();
                var values = stackalloc double[4];
                var code = LuaSTGAPI.api.dwrite_textLayoutGetOverhangMetrics((nuint)Handle, values);
                if (code != 0)
                {
                    throw new InvalidOperationException("TextLayout:GetOverhangMetrics failed");
                }
                return new OverhangMetrics
                {
                    Left = values[0],
                    Top = values[1],
                    Right = values[2],
                    Bottom = values[3],
                };
            }

            /// <summary>获取布局最大高度（对应 TextLayout:GetMaxHeight）</summary>
            public float GetMaxHeight()
            {
                ThrowIfDisposed();
                return LuaSTGAPI.api.dwrite_textLayoutGetMaxHeight((nuint)Handle);
            }

            /// <summary>获取布局最大宽度（对应 TextLayout:GetMaxWidth）</summary>
            public float GetMaxWidth()
            {
                ThrowIfDisposed();
                return LuaSTGAPI.api.dwrite_textLayoutGetMaxWidth((nuint)Handle);
            }
        }

        // ========== TextRenderer（对应 DirectWrite.TextRenderer userdata） ==========

        /// <summary>
        /// 文本渲染器（对应 Lua 侧 DirectWrite.TextRenderer userdata，持有颜色/描边/阴影状态）。
        /// 通过 <see cref="CreateTextRenderer"/> 工厂创建；<see cref="Dispose"/> 销毁，
        /// 销毁后访问抛出 <see cref="ObjectDisposedException"/>。
        /// 初始状态：文字白色、描边黑色宽度 0、阴影黑色半径 0、拓展 0。
        /// </summary>
        public sealed unsafe class TextRenderer : DirectWriteObject
        {
            internal TextRenderer(nint handle) : base(handle) { }

            protected override void DestroyNative()
                => LuaSTGAPI.api.dwrite_textRendererDestroy((nuint)Handle);

            /// <summary>设置文字颜色（对应 TextRenderer:SetTextColor）</summary>
            public void SetTextColor(Color color)
            {
                ThrowIfDisposed();
                LuaSTGAPI.api.dwrite_textRendererSetTextColor((nuint)Handle, color.Argb);
            }

            /// <summary>设置描边颜色（对应 TextRenderer:SetTextOutlineColor）</summary>
            public void SetTextOutlineColor(Color color)
            {
                ThrowIfDisposed();
                LuaSTGAPI.api.dwrite_textRendererSetTextOutlineColor((nuint)Handle, color.Argb);
            }

            /// <summary>设置描边宽度（对应 TextRenderer:SetTextOutlineWidth）</summary>
            public void SetTextOutlineWidth(float width)
            {
                ThrowIfDisposed();
                LuaSTGAPI.api.dwrite_textRendererSetTextOutlineWidth((nuint)Handle, width);
            }

            /// <summary>设置阴影颜色（对应 TextRenderer:SetShadowColor）</summary>
            public void SetShadowColor(Color color)
            {
                ThrowIfDisposed();
                LuaSTGAPI.api.dwrite_textRendererSetShadowColor((nuint)Handle, color.Argb);
            }

            /// <summary>设置阴影半径（对应 TextRenderer:SetShadowRadius）</summary>
            public void SetShadowRadius(float radius)
            {
                ThrowIfDisposed();
                LuaSTGAPI.api.dwrite_textRendererSetShadowRadius((nuint)Handle, radius);
            }

            /// <summary>设置阴影拓展（对应 TextRenderer:SetShadowExtend）</summary>
            public void SetShadowExtend(float extend)
            {
                ThrowIfDisposed();
                LuaSTGAPI.api.dwrite_textRendererSetShadowExtend((nuint)Handle, extend);
            }

            /// <summary>
            /// 将文本布局渲染到渲染目标纹理（对应 TextRenderer:Render）。
            /// </summary>
            /// <param name="textureName">渲染目标纹理的资源名（须为 RenderTarget 纹理）</param>
            /// <param name="textLayout">文本布局</param>
            /// <param name="offsetX">X 偏移</param>
            /// <param name="offsetY">Y 偏移</param>
            /// <exception cref="ArgumentException">纹理不存在或不是渲染目标</exception>
            /// <exception cref="InvalidOperationException">渲染失败</exception>
            public void Render(string textureName, TextLayout textLayout, float offsetX, float offsetY)
            {
                ThrowIfDisposed();
                if (textureName is null)
                {
                    throw new ArgumentNullException(nameof(textureName));
                }
                if (textLayout is null)
                {
                    throw new ArgumentNullException(nameof(textLayout));
                }
                textLayout.ThrowIfDisposed();
                using var name = new MarshaledString(textureName);
                var code = LuaSTGAPI.api.dwrite_textRendererRender(
                    (nuint)Handle, name, (nuint)textLayout.Handle, offsetX, offsetY);
                switch (code)
                {
                    case 0:
                        return;
                    case 1:
                        throw new ArgumentException($"找不到纹理 '{textureName}'", nameof(textureName));
                    case 2:
                        throw new ArgumentException($"纹理 '{textureName}' 不是渲染目标", nameof(textureName));
                    default:
                        throw new InvalidOperationException("TextRenderer:Render 渲染失败");
                }
            }
        }
    }
}
