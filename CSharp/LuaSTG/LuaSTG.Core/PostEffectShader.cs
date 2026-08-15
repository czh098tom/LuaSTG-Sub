using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 后期特效着色器（对应 Lua 侧 lstg.PostEffectShader 对象类型与 lstg.CreatePostEffectShader）。
    /// 通过 <see cref="Create"/> 工厂创建；<see cref="Dispose"/> 销毁（对应 Lua userdata 的 __gc），
    /// 销毁后访问抛出 <see cref="ObjectDisposedException"/>。
    /// </summary>
    public sealed unsafe class PostEffectShader : IDisposable
    {
        /// <summary>引擎侧 IPostEffectShader 指针，0 表示已销毁</summary>
        internal nint _native;
        /// <summary>是否持有引用（借用资源池的句柄不持有，Dispose 时不释放）</summary>
        private readonly bool _owned;

        private PostEffectShader(nint native, bool owned)
        {
            _native = native;
            _owned = owned;
        }

        /// <summary>
        /// 创建后期特效着色器（对应 lstg.CreatePostEffectShader）。
        /// 失败返回 null，详见引擎日志。
        /// </summary>
        /// <param name="path">着色器文件路径</param>
        public static PostEffectShader? Create(string path)
        {
            using var p = new MarshaledString(path);
            var handle = LuaSTGAPI.api.render_createPostEffectShader(p);
            return handle == 0 ? null : new PostEffectShader((nint)handle, owned: true);
        }

        /// <summary>
        /// 查找资源池中的后效（对应传统风格 PostEffect 按名称查找的 lstg.LoadFX 资源）。
        /// 借用资源管理器持有的句柄，包装对象 Dispose 不释放底层资源。
        /// </summary>
        internal static PostEffectShader? FindBorrowed(string psName)
        {
            using var p = new MarshaledString(psName);
            var handle = LuaSTGAPI.api.render_findFX(p);
            return handle == 0 ? null : new PostEffectShader((nint)handle, owned: false);
        }

        /// <summary>是否已销毁</summary>
        public bool IsDisposed => _native == 0;

        /// <summary>引擎句柄，已销毁时抛出 <see cref="ObjectDisposedException"/></summary>
        internal nint Handle => _native != 0
            ? _native
            : throw new ObjectDisposedException(nameof(PostEffectShader), "后效着色器已销毁");

        private void ThrowIfDisposed()
        {
            if (_native == 0)
            {
                throw new ObjectDisposedException(nameof(PostEffectShader), "后效着色器已销毁");
            }
        }

        /// <summary>销毁着色器并释放引擎资源（对应 Lua userdata 的 __gc）</summary>
        public void Dispose()
        {
            if (_native != 0)
            {
                if (_owned)
                {
                    LuaSTGAPI.api.render_pfxRelease((nuint)_native);
                }
                _native = 0;
            }
        }

        ~PostEffectShader()
        {
            // 兜底释放（与 GameObjectBase 的策略一致）
            if (_native != 0 && _owned)
            {
                try
                {
                    LuaSTGAPI.api.render_pfxRelease((nuint)_native);
                }
                catch
                {
                    // 引擎可能已关闭
                }
                _native = 0;
            }
        }

        // ========== 参数设置（对应 lstg.PostEffectShader 的成员方法） ==========

        /// <summary>设置浮点参数（对应 lstg.PostEffectShader:setFloat），返回引擎是否接受</summary>
        public bool SetFloat(string name, float value)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            return LuaSTGAPI.api.render_pfxSetFloat((nuint)_native, n, value) == 0;
        }

        /// <summary>设置二元浮点参数（对应 lstg.PostEffectShader:setFloat2），返回引擎是否接受</summary>
        public bool SetFloat2(string name, float x, float y)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            return LuaSTGAPI.api.render_pfxSetFloat2((nuint)_native, n, x, y) == 0;
        }

        /// <summary>设置三元浮点参数（对应 lstg.PostEffectShader:setFloat3），返回引擎是否接受</summary>
        public bool SetFloat3(string name, float x, float y, float z)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            return LuaSTGAPI.api.render_pfxSetFloat3((nuint)_native, n, x, y, z) == 0;
        }

        /// <summary>设置四元浮点参数（对应 lstg.PostEffectShader:setFloat4），返回引擎是否接受</summary>
        public bool SetFloat4(string name, float x, float y, float z, float w)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            return LuaSTGAPI.api.render_pfxSetFloat4((nuint)_native, n, x, y, z, w) == 0;
        }

        /// <summary>绑定纹理参数（对应 lstg.PostEffectShader:setTexture），返回引擎是否接受</summary>
        /// <exception cref="ArgumentException">纹理资源不存在</exception>
        public bool SetTexture(string name, string textureName)
        {
            ThrowIfDisposed();
            using var n = new MarshaledString(name);
            using var t = new MarshaledString(textureName);
            var code = LuaSTGAPI.api.render_pfxSetTexture((nuint)_native, n, t);
            if (code == 1)
            {
                throw new ArgumentException($"找不到纹理 '{textureName}'", nameof(textureName));
            }
            return code == 0;
        }
    }

    /// <summary>
    /// 后效参数（对应传统风格 lstg.PostEffect 第 4 个参数表的一项）。
    /// Lua 侧的 number/Vector2/Vector3/Vector4/Color/纹理名分别对应
    /// <see cref="Float"/>/<see cref="Float2"/>/<see cref="Float3"/>/<see cref="Float4"/>/<see cref="Color"/>/<see cref="Texture"/>。
    /// </summary>
    public readonly struct PostEffectParam
    {
        private readonly byte _kind; // 0=float 1=float2 2=float3 3=float4 4=texture
        private readonly string _key;
        private readonly float _x;
        private readonly float _y;
        private readonly float _z;
        private readonly float _w;
        private readonly string? _textureName;

        private PostEffectParam(byte kind, string key, float x, float y, float z, float w, string? textureName)
        {
            _kind = kind;
            _key = key;
            _x = x; _y = y; _z = z; _w = w;
            _textureName = textureName;
        }

        /// <summary>浮点参数（对应参数表中的 number）</summary>
        public static PostEffectParam Float(string key, float value)
            => new(0, key, value, 0f, 0f, 0f, null);

        /// <summary>二元浮点参数（对应参数表中的 Vector2）</summary>
        public static PostEffectParam Float2(string key, float x, float y)
            => new(1, key, x, y, 0f, 0f, null);

        /// <summary>三元浮点参数（对应参数表中的 Vector3）</summary>
        public static PostEffectParam Float3(string key, float x, float y, float z)
            => new(2, key, x, y, z, 0f, null);

        /// <summary>四元浮点参数（对应参数表中的 Vector4）</summary>
        public static PostEffectParam Float4(string key, float x, float y, float z, float w)
            => new(3, key, x, y, z, w, null);

        /// <summary>颜色参数（对应参数表中的 Color 对象，归一化为 RGBA 浮点）</summary>
        /// <param name="argb">颜色（ARGB，0xAARRGGBB）</param>
        public static PostEffectParam Color(string key, uint argb)
        {
            var a = (argb >> 24) & 0xFFu;
            var r = (argb >> 16) & 0xFFu;
            var g = (argb >> 8) & 0xFFu;
            var b = argb & 0xFFu;
            return new PostEffectParam(3, key, r / 255f, g / 255f, b / 255f, a / 255f, null);
        }

        /// <summary>纹理参数（对应参数表中的纹理名字符串）</summary>
        public static PostEffectParam Texture(string key, string textureName)
            => new(4, key, 0f, 0f, 0f, 0f, textureName);

        /// <summary>将该参数应用到着色器</summary>
        internal void Apply(PostEffectShader shader)
        {
            switch (_kind)
            {
                case 0:
                    shader.SetFloat(_key, _x);
                    break;
                case 1:
                    shader.SetFloat2(_key, _x, _y);
                    break;
                case 2:
                    shader.SetFloat3(_key, _x, _y, _z);
                    break;
                case 3:
                    shader.SetFloat4(_key, _x, _y, _z, _w);
                    break;
                default:
                    shader.SetTexture(_key, _textureName!);
                    break;
            }
        }
    }

    // 后期特效（lstg.PostEffect 的三种形式）
    public static unsafe partial class Render
    {
        /// <summary>
        /// 应用后期特效，对象形式（对应 lstg.PostEffect(shader, blend)，shader 为 PostEffectShader 对象）。
        /// </summary>
        /// <exception cref="InvalidOperationException">不在渲染批次内</exception>
        public static void PostEffect(PostEffectShader shader, BlendMode blend)
        {
            if (shader is null)
            {
                throw new ArgumentNullException(nameof(shader));
            }
            if (LuaSTGAPI.api.render_postEffectDraw((nuint)shader.Handle, (byte)blend) != 0)
            {
                throw new InvalidOperationException("无效的渲染操作：不在 BeginScene/EndScene 渲染批次内");
            }
        }

        /// <summary>
        /// 应用后期特效，传统形式（对应 lstg.PostEffect(rt, ps, blend)）。
        /// 自动设置 screen_texture / screen_texture_size / viewport 标准参数。
        /// </summary>
        /// <exception cref="ArgumentException">后效或渲染目标纹理不存在</exception>
        /// <exception cref="InvalidOperationException">不在渲染批次内</exception>
        public static void PostEffect(string rtName, string psName, BlendMode blend)
            => PostEffect(rtName, psName, blend, Array.Empty<PostEffectParam>());

        /// <summary>
        /// 应用后期特效，传统形式带参数表（对应 lstg.PostEffect(rt, ps, blend, {key=value, ...})）。
        /// 先设置 screen_texture 等标准参数，再按顺序应用 <paramref name="parameters"/>，最后绘制。
        /// </summary>
        /// <exception cref="ArgumentException">后效、渲染目标纹理或参数引用的纹理不存在</exception>
        /// <exception cref="InvalidOperationException">不在渲染批次内</exception>
        public static void PostEffect(string rtName, string psName, BlendMode blend, params PostEffectParam[] parameters)
        {
            using var fx = PostEffectShader.FindBorrowed(psName);
            if (fx is null)
            {
                throw new ArgumentException($"找不到后效 '{psName}'", nameof(psName));
            }
            using var rt = new MarshaledString(rtName);
            if (LuaSTGAPI.api.render_postEffectSetScreenParams((nuint)fx.Handle, rt) != 0)
            {
                throw new ArgumentException($"纹理 '{rtName}' 不存在", nameof(rtName));
            }
            foreach (var p in parameters)
            {
                p.Apply(fx);
            }
            if (LuaSTGAPI.api.render_postEffectDraw((nuint)fx.Handle, (byte)blend) != 0)
            {
                throw new InvalidOperationException("无效的渲染操作：不在 BeginScene/EndScene 渲染批次内");
            }
        }

        /// <summary>
        /// 应用后期特效，旧版形式（对应 lstg.PostEffect(ps, rt, sampler, blend, cbdata, tdata)）。
        /// 引擎源码中该形式被标注为设计失误、待废弃，仅为兼容保留。
        /// </summary>
        /// <param name="psName">后效名</param>
        /// <param name="rtName">渲染目标纹理名</param>
        /// <param name="rtSampler">渲染目标采样器</param>
        /// <param name="blend">混合模式</param>
        /// <param name="constants">常量缓冲，最多 8 组（每组 4 个 float）</param>
        /// <param name="textures">附加纹理与采样器，最多 4 个</param>
        /// <exception cref="ArgumentException">后效或纹理不存在</exception>
        /// <exception cref="InvalidOperationException">不在渲染批次内</exception>
        public static void PostEffectAdvanced(
            string psName, string rtName,
            Renderer.SamplerState rtSampler, BlendMode blend,
            ReadOnlySpan<(float X, float Y, float Z, float W)> constants,
            params (string TextureName, Renderer.SamplerState Sampler)[] textures)
        {
            using var ps = new MarshaledString(psName);
            using var rt = new MarshaledString(rtName);

            // 常量缓冲（最多 8 组 Vector4）
            var cvCount = Math.Min(constants.Length, 8);
            double* cv = stackalloc double[32];
            for (var i = 0; i < cvCount; i++)
            {
                cv[i * 4 + 0] = constants[i].X;
                cv[i * 4 + 1] = constants[i].Y;
                cv[i * 4 + 2] = constants[i].Z;
                cv[i * 4 + 3] = constants[i].W;
            }

            // 附加纹理（最多 4 个）
            var texCount = Math.Min(textures.Length, 4);
            Span<MarshaledString> names = stackalloc MarshaledString[4];
            Span<byte> svs = stackalloc byte[4];
            try
            {
                for (var i = 0; i < texCount; i++)
                {
                    names[i] = new MarshaledString(textures[i].TextureName);
                    svs[i] = (byte)textures[i].Sampler;
                }
                var code = LuaSTGAPI.api.render_postEffectAdvanced(
                    ps, rt, (byte)rtSampler, (byte)blend,
                    cv, (uint)cvCount,
                    texCount > 0 ? names[0].Pointer : null, svs[0],
                    texCount > 1 ? names[1].Pointer : null, svs[1],
                    texCount > 2 ? names[2].Pointer : null, svs[2],
                    texCount > 3 ? names[3].Pointer : null, svs[3],
                    (uint)texCount);
                switch (code)
                {
                    case 0:
                        break;
                    case 1:
                        throw new ArgumentException($"找不到后效 '{psName}'", nameof(psName));
                    case 2:
                        throw new ArgumentException($"纹理 '{rtName}' 不存在", nameof(rtName));
                    case 3:
                        throw new ArgumentException("附加纹理不存在", nameof(textures));
                    default:
                        throw new InvalidOperationException("无效的渲染操作：不在 BeginScene/EndScene 渲染批次内");
                }
            }
            finally
            {
                for (var i = 0; i < 4; i++)
                {
                    names[i].Dispose();
                }
            }
        }
    }
}
