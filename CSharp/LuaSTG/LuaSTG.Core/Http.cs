using System;

namespace LuaSTG.Core
{
    /// <summary>HTTP 请求方法（与引擎 http::RequestMethod 取值一致）</summary>
    public enum HttpRequestMethod : int
    {
        Custom = 0,
        Get = 1,
        Head = 2,
        Post = 3,
        Put = 4,
        Delete = 5,
        Patch = 6,
    }

    /// <summary>
    /// HTTP 请求（对应 Lua 侧 http.Request）。引擎对象包装：
    /// 静态工厂分配，Dispose 销毁，销毁后访问抛出异常。
    /// </summary>
    public sealed unsafe class HttpRequest : IDisposable
    {
        internal nuint _handle;

        private HttpRequest(nuint handle)
        {
            _handle = handle;
        }

        /// <summary>创建请求。失败抛出 InvalidOperationException（错误信息见 LastError）。</summary>
        public static HttpRequest Create(HttpRequestMethod method, string url, string? customMethod = null)
        {
            using var m = new MarshaledString(customMethod);
            using var u = new MarshaledString(url);
            var handle = LuaSTGAPI.api.http_request_create((int)method, m, u);
            return handle != 0
                ? new HttpRequest(handle)
                : throw new InvalidOperationException($"创建 HTTP 请求失败：{LastError}");
        }

        /// <summary>GET 请求</summary>
        public static HttpRequest Get(string url) => Create(HttpRequestMethod.Get, url);

        /// <summary>HEAD 请求</summary>
        public static HttpRequest Head(string url) => Create(HttpRequestMethod.Head, url);

        /// <summary>POST 请求</summary>
        public static HttpRequest Post(string url) => Create(HttpRequestMethod.Post, url);

        /// <summary>PUT 请求</summary>
        public static HttpRequest Put(string url) => Create(HttpRequestMethod.Put, url);

        /// <summary>DELETE 请求</summary>
        public static HttpRequest Delete(string url) => Create(HttpRequestMethod.Delete, url);

        /// <summary>PATCH 请求</summary>
        public static HttpRequest Patch(string url) => Create(HttpRequestMethod.Patch, url);

        /// <summary>最近一次错误的描述</summary>
        public static string LastError => StringMarshal.FromUtf8(LuaSTGAPI.api.http_getLastError());

        private void ThrowIfDisposed()
        {
            if (_handle == 0)
            {
                throw new ObjectDisposedException(nameof(HttpRequest));
            }
        }

        /// <summary>解析超时（毫秒）</summary>
        public int ResolveTimeout
        {
            set { ThrowIfDisposed(); LuaSTGAPI.api.http_request_setResolveTimeout(_handle, value); }
        }

        /// <summary>连接超时（毫秒）</summary>
        public int ConnectTimeout
        {
            set { ThrowIfDisposed(); LuaSTGAPI.api.http_request_setConnectTimeout(_handle, value); }
        }

        /// <summary>发送超时（毫秒）</summary>
        public int SendTimeout
        {
            set { ThrowIfDisposed(); LuaSTGAPI.api.http_request_setSendTimeout(_handle, value); }
        }

        /// <summary>接收超时（毫秒）</summary>
        public int ReceiveTimeout
        {
            set { ThrowIfDisposed(); LuaSTGAPI.api.http_request_setReceiveTimeout(_handle, value); }
        }

        /// <summary>添加请求头</summary>
        public void AddHeader(string name, string value)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            using var v = new MarshaledString(value);
            LuaSTGAPI.api.http_request_addHeader(_handle, n, v);
        }

        /// <summary>设置请求体（字节数组）</summary>
        public void SetBody(ReadOnlySpan<byte> data)
        {
            ThrowIfDisposed();
            fixed (byte* p = data)
            {
                if (LuaSTGAPI.api.http_request_setBody(_handle, p, (uint)data.Length) == 0)
                {
                    throw new InvalidOperationException($"设置请求体失败：{LastError}");
                }
            }
        }

        /// <summary>设置请求体（UTF-8 文本）</summary>
        public void SetBody(string text)
        {
            ThrowIfDisposed();
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            SetBody(bytes);
        }

        /// <summary>执行请求。失败返回 null（错误信息见 LastError）。</summary>
        public HttpResponseMessage? Execute()
        {
            ThrowIfDisposed();
            var response = LuaSTGAPI.api.http_request_execute(_handle);
            return response != 0 ? new HttpResponseMessage(response) : null;
        }

        /// <summary>销毁引擎请求对象</summary>
        public void Dispose()
        {
            if (_handle != 0)
            {
                LuaSTGAPI.api.http_request_destroy(_handle);
                _handle = 0;
            }
        }
    }

    /// <summary>HTTP 响应（对应 Lua 侧 http.ResponseEntity）。由 HttpRequest.Execute 返回。</summary>
    public sealed unsafe class HttpResponseMessage : IDisposable
    {
        internal nuint _handle;

        internal HttpResponseMessage(nuint handle)
        {
            _handle = handle;
        }

        private void ThrowIfDisposed()
        {
            if (_handle == 0)
            {
                throw new ObjectDisposedException(nameof(HttpResponseMessage));
            }
        }

        /// <summary>是否包含指定响应头</summary>
        public bool HasHeader(string name)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            return LuaSTGAPI.api.http_response_hasHeader(_handle, n) != 0;
        }

        /// <summary>获取指定响应头，不存在返回 null</summary>
        public string? GetHeader(string name)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            var value = LuaSTGAPI.api.http_response_getHeader(_handle, n);
            return value != null ? StringMarshal.FromUtf8(value) : null;
        }

        /// <summary>响应体字节数</summary>
        public uint BodyLength
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.http_response_getBodyLength(_handle); }
        }

        /// <summary>读取响应体（拷贝为新的字节数组）</summary>
        public byte[] GetBody()
        {
            ThrowIfDisposed();
            var length = BodyLength;
            var result = new byte[length];
            var source = LuaSTGAPI.api.http_response_getBodyData(_handle);
            if (length > 0 && source != null)
            {
                fixed (byte* dst = result)
                {
                    Buffer.MemoryCopy(source, dst, length, length);
                }
            }
            return result;
        }

        /// <summary>读取响应体为 UTF-8 文本</summary>
        public string GetBodyAsString()
            => System.Text.Encoding.UTF8.GetString(GetBody());

        /// <summary>销毁引擎响应对象</summary>
        public void Dispose()
        {
            if (_handle != 0)
            {
                LuaSTGAPI.api.http_response_destroy(_handle);
                _handle = 0;
            }
        }
    }
}
