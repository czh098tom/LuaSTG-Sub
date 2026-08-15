using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 视频打开参数（对应 Lua 侧 lstg.VideoDecoder.create / reopen 的 options 表）。
    /// </summary>
    public sealed class VideoDecoderOptions
    {
        /// <summary>视频流下标（Lua 键 video_stream，默认 uint.MaxValue 表示自动选择）</summary>
        public uint VideoStreamIndex { get; set; } = uint.MaxValue;
        /// <summary>输出宽度（Lua 键 width，0 表示使用视频原始宽度）</summary>
        public uint Width { get; set; }
        /// <summary>输出高度（Lua 键 height，0 表示使用视频原始高度）</summary>
        public uint Height { get; set; }
        /// <summary>纹理 alpha 预乘（Lua 键 premultiplied_alpha，默认 false）</summary>
        public bool PremultipliedAlpha { get; set; }
        /// <summary>循环播放（Lua 键 looping，默认 false）</summary>
        public bool Looping { get; set; }
        /// <summary>循环终点，秒（Lua 键 loop_end，默认 0）</summary>
        public double LoopEnd { get; set; }
        /// <summary>循环时长，秒（Lua 键 loop_duration，默认 0）</summary>
        public double LoopDuration { get; set; }
    }

    /// <summary>视频流信息（对应 Lua 侧 getVideoStreams 返回表的表项）。</summary>
    public readonly struct VideoStreamInfo
    {
        /// <summary>流下标</summary>
        public readonly uint Index;
        /// <summary>视频宽度（像素）</summary>
        public readonly uint Width;
        /// <summary>视频高度（像素）</summary>
        public readonly uint Height;
        /// <summary>帧率</summary>
        public readonly double Fps;
        /// <summary>时长（秒）</summary>
        public readonly double Duration;

        internal VideoStreamInfo(uint index, uint width, uint height, double fps, double duration)
        {
            Index = index;
            Width = width;
            Height = height;
            Fps = fps;
            Duration = duration;
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"VideoStream({Index}: {Width}x{Height}, {Fps:F2}fps, {Duration:F2}s)";
    }

    /// <summary>音频流信息（对应 Lua 侧 getAudioStreams 返回表的表项）。</summary>
    public readonly struct AudioStreamInfo
    {
        /// <summary>流下标</summary>
        public readonly uint Index;
        /// <summary>声道数</summary>
        public readonly uint Channels;
        /// <summary>采样率</summary>
        public readonly uint SampleRate;
        /// <summary>时长（秒）</summary>
        public readonly double Duration;

        internal AudioStreamInfo(uint index, uint channels, uint sampleRate, double duration)
        {
            Index = index;
            Channels = channels;
            SampleRate = sampleRate;
            Duration = duration;
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"AudioStream({Index}: {Channels}ch, {SampleRate}Hz, {Duration:F2}s)";
    }

    /// <summary>
    /// 视频解码器（对应 Lua 侧 lstg.VideoDecoder）。
    /// 通过 <see cref="Create(string, VideoDecoderOptions?)"/> 工厂创建；<see cref="Dispose"/> 销毁。
    /// </summary>
    public sealed unsafe class VideoDecoder : ModernGraphicsObject
    {
        internal VideoDecoder(nint handle) : base(handle)
        {
        }

        /// <summary>
        /// 创建视频解码器并打开视频文件（对应 lstg.VideoDecoder.create(path[, options])）。
        /// </summary>
        /// <param name="path">视频文件路径</param>
        /// <param name="options">打开参数，null 时使用默认值</param>
        /// <exception cref="InvalidOperationException">创建或打开失败，详见引擎日志</exception>
        public static VideoDecoder Create(string path, VideoDecoderOptions? options = null)
        {
            using var p = new MarshaledString(path);
            var o = options ?? new VideoDecoderOptions();
            var handle = LuaSTGAPI.api.mg_videoCreate(
                p,
                o.VideoStreamIndex, o.Width, o.Height,
                (byte)(o.PremultipliedAlpha ? 1 : 0),
                (byte)(o.Looping ? 1 : 0),
                o.LoopEnd, o.LoopDuration);
            return handle != 0
                ? new VideoDecoder((nint)handle)
                : throw new InvalidOperationException($"打开视频文件 '{path}' 失败，详见引擎日志");
        }

        /// <summary>视频宽度（对应 lstg.VideoDecoder:getWidth）</summary>
        public uint Width
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetWidth((nuint)Handle); }
        }

        /// <summary>视频高度（对应 lstg.VideoDecoder:getHeight）</summary>
        public uint Height
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetHeight((nuint)Handle); }
        }

        /// <summary>视频总时长，秒（对应 lstg.VideoDecoder:getDuration）</summary>
        public double Duration
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetDuration((nuint)Handle); }
        }

        /// <summary>当前播放时间，秒（对应 lstg.VideoDecoder:getCurrentTime）</summary>
        public double CurrentTime
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetCurrentTime((nuint)Handle); }
        }

        /// <summary>视频帧率（对应 lstg.VideoDecoder:getFPS，由帧间隔换算，无视频流时为 0）</summary>
        public double FPS
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetFPS((nuint)Handle); }
        }

        /// <summary>是否循环播放（对应 lstg.VideoDecoder:isLooping）</summary>
        public bool IsLooping
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoIsLooping((nuint)Handle) != 0; }
        }

        /// <summary>
        /// 获取当前视频帧纹理（对应 lstg.VideoDecoder:getTexture）。
        /// 返回的包装对象持有引擎纹理引用，无纹理时返回 null。
        /// </summary>
        public Texture2D? GetTexture()
        {
            ThrowIfDisposed();
            var texture = LuaSTGAPI.api.mg_videoGetTexture((nuint)Handle);
            return texture != 0 ? new Texture2D((nint)texture) : null;
        }

        /// <summary>跳转到指定时间（对应 lstg.VideoDecoder:seek），返回是否成功</summary>
        public bool Seek(double time)
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.mg_videoSeek((nuint)Handle, time) != 0;
        }

        /// <summary>解码到指定时间（对应 lstg.VideoDecoder:update），返回是否成功</summary>
        public bool Update(double time)
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.mg_videoUpdate((nuint)Handle, time) != 0;
        }

        /// <summary>设置是否循环播放（对应 lstg.VideoDecoder:setLooping）</summary>
        public void SetLooping(bool loop)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_videoSetLooping((nuint)Handle, (byte)(loop ? 1 : 0));
        }

        /// <summary>设置循环范围（对应 lstg.VideoDecoder:setLoopRange(loop_end, loop_duration)）</summary>
        public void SetLoopRange(double loopEnd, double loopDuration)
        {
            ThrowIfDisposed();
            LuaSTGAPI.api.mg_videoSetLoopRange((nuint)Handle, loopEnd, loopDuration);
        }

        /// <summary>获取循环范围（对应 lstg.VideoDecoder:getLoopRange 的双返回值）</summary>
        public (double LoopEnd, double LoopDuration) GetLoopRange()
        {
            ThrowIfDisposed();
            double loopEnd = 0.0, loopDuration = 0.0;
            LuaSTGAPI.api.mg_videoGetLoopRange((nuint)Handle, &loopEnd, &loopDuration);
            return (loopEnd, loopDuration);
        }

        /// <summary>视频流数量（对应 lstg.VideoDecoder:getVideoStreams 返回数组的长度）</summary>
        public uint VideoStreamCount
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetVideoStreamCount((nuint)Handle); }
        }

        /// <summary>获取指定视频流信息（对应 lstg.VideoDecoder:getVideoStreams 返回数组的表项）</summary>
        /// <exception cref="ArgumentOutOfRangeException">下标越界</exception>
        public VideoStreamInfo GetVideoStream(uint index)
        {
            ThrowIfDisposed();
            uint streamIndex = 0, width = 0, height = 0;
            double fps = 0.0, duration = 0.0;
            if (LuaSTGAPI.api.mg_videoGetVideoStream((nuint)Handle, index, &streamIndex, &width, &height, &fps, &duration) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "视频流下标越界");
            }
            return new VideoStreamInfo(streamIndex, width, height, fps, duration);
        }

        /// <summary>获取全部视频流信息（对应 lstg.VideoDecoder:getVideoStreams）</summary>
        public VideoStreamInfo[] GetVideoStreams()
        {
            ThrowIfDisposed();
            var count = (int)LuaSTGAPI.api.mg_videoGetVideoStreamCount((nuint)Handle);
            var result = new VideoStreamInfo[count];
            for (uint i = 0; i < (uint)count; i++)
            {
                result[i] = GetVideoStream(i);
            }
            return result;
        }

        /// <summary>音频流数量（对应 lstg.VideoDecoder:getAudioStreams 返回数组的长度）</summary>
        public uint AudioStreamCount
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetAudioStreamCount((nuint)Handle); }
        }

        /// <summary>获取指定音频流信息（对应 lstg.VideoDecoder:getAudioStreams 返回数组的表项）</summary>
        /// <exception cref="ArgumentOutOfRangeException">下标越界</exception>
        public AudioStreamInfo GetAudioStream(uint index)
        {
            ThrowIfDisposed();
            uint streamIndex = 0, channels = 0, sampleRate = 0;
            double duration = 0.0;
            if (LuaSTGAPI.api.mg_videoGetAudioStream((nuint)Handle, index, &streamIndex, &channels, &sampleRate, &duration) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "音频流下标越界");
            }
            return new AudioStreamInfo(streamIndex, channels, sampleRate, duration);
        }

        /// <summary>获取全部音频流信息（对应 lstg.VideoDecoder:getAudioStreams）</summary>
        public AudioStreamInfo[] GetAudioStreams()
        {
            ThrowIfDisposed();
            var count = (int)LuaSTGAPI.api.mg_videoGetAudioStreamCount((nuint)Handle);
            var result = new AudioStreamInfo[count];
            for (uint i = 0; i < (uint)count; i++)
            {
                result[i] = GetAudioStream(i);
            }
            return result;
        }

        /// <summary>当前选中的视频流下标（对应 lstg.VideoDecoder:getVideoStreamIndex）</summary>
        public uint VideoStreamIndex
        {
            get { ThrowIfDisposed(); return LuaSTGAPI.api.mg_videoGetVideoStreamIndex((nuint)Handle); }
        }

        /// <summary>
        /// 以指定参数重新打开视频（对应 lstg.VideoDecoder:reopen(options)）。
        /// 注意：与 Lua 侧一致，未覆盖的字段沿用上次打开参数之外均使用传入值整体替换。
        /// </summary>
        public bool Reopen(VideoDecoderOptions options)
        {
            ThrowIfDisposed();
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            return LuaSTGAPI.api.mg_videoReopen(
                (nuint)Handle,
                options.VideoStreamIndex, options.Width, options.Height,
                (byte)(options.PremultipliedAlpha ? 1 : 0),
                (byte)(options.Looping ? 1 : 0),
                options.LoopEnd, options.LoopDuration) != 0;
        }

        /// <summary>
        /// 以最后一次打开的参数重新打开视频（对应 lstg.VideoDecoder:reopen() 无参数表形式）。
        /// </summary>
        public bool Reopen()
        {
            ThrowIfDisposed();
            return LuaSTGAPI.api.mg_videoReopenLast((nuint)Handle) != 0;
        }

        /// <inheritdoc/>
        protected override void DestroyNative()
        {
            LuaSTGAPI.api.mg_videoRelease((nuint)Handle);
        }
    }
}
