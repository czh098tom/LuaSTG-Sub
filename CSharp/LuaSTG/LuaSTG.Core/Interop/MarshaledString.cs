using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.Core
{
    /// <summary>
    /// C# 字符串到 UTF-8 非托管字符串的临时封送。
    /// 用法：using var s = new MarshaledString(text); api.foo(s);
    /// </summary>
    internal unsafe struct MarshaledString : IDisposable
    {
        private byte* _ptr;

        public readonly byte* Pointer => _ptr;

        public MarshaledString(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _ptr = (byte*)NativeMemory.Alloc(1);
                *_ptr = 0;
                return;
            }
            var maxBytes = Encoding.UTF8.GetMaxByteCount(text.Length) + 1;
            _ptr = (byte*)NativeMemory.Alloc((nuint)maxBytes);
            fixed (char* p = text)
            {
                var written = Encoding.UTF8.GetBytes(p, text.Length, _ptr, maxBytes);
                _ptr[written] = 0;
            }
        }

        public void Dispose()
        {
            if (_ptr != null)
            {
                NativeMemory.Free(_ptr);
                _ptr = null;
            }
        }

        public static implicit operator byte*(in MarshaledString value) => value._ptr;
    }

    internal static class StringMarshal
    {
        /// <summary>从引擎返回的 UTF-8 字符串创建托管字符串（立即拷贝）</summary>
        public static unsafe string FromUtf8(byte* ptr)
        {
            if (ptr == null)
            {
                return string.Empty;
            }
            var length = 0;
            while (ptr[length] != 0)
            {
                length++;
            }
            return Encoding.UTF8.GetString(ptr, length);
        }
    }
}
