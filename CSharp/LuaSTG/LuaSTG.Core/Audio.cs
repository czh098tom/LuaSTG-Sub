using System;

namespace LuaSTG.Core
{
    /// <summary>
    /// 音频播放状态（对应引擎侧 core::AudioPlayerState 的取值）。
    /// </summary>
    public enum AudioState : int
    {
        /// <summary>已停止</summary>
        Stopped = 0,
        /// <summary>播放中</summary>
        Playing = 1,
        /// <summary>已暂停</summary>
        Paused = 2,
    }

    /// <summary>
    /// 音乐循环范围类型（对应引擎侧 luastg::MusicRoopRangeType）。
    /// </summary>
    public enum MusicLoopRangeType : int
    {
        /// <summary>禁用循环</summary>
        Disable = 0,
        /// <summary>全曲循环</summary>
        All = 1,
        /// <summary>起点到曲末</summary>
        StartPointToEnd = 2,
        /// <summary>起点加长度</summary>
        StartPointAndLength = 3,
        /// <summary>长度加终点</summary>
        LengthAndEndPoint = 4,
        /// <summary>曲首到终点</summary>
        StartToEndPoint = 5,
        /// <summary>起点加终点</summary>
        StartPointAndEndPoint = 6,
    }

    /// <summary>
    /// 音乐循环范围单位（对应引擎侧 luastg::MusicRoopRangeUnit）。
    /// </summary>
    public enum MusicLoopRangeUnit : int
    {
        /// <summary>按采样</summary>
        Sample = 0,
        /// <summary>按秒</summary>
        Second = 1,
    }

    /// <summary>
    /// 音乐循环范围（对应 Lua 侧 SetMusicLoopRange 的 table 参数，
    /// 字段与 start_in_samples / end_in_samples / length_in_samples /
    /// start_in_seconds / end_in_seconds / length_in_seconds 一一对应）。
    /// </summary>
    /// <remarks>
    /// 组合判定优先级与 Lua 绑定一致：先按采样（Sample）匹配，再按秒（Second）匹配，
    /// 未填写任何字段时视为全曲循环（All）。
    /// </remarks>
    public sealed class MusicLoopRange
    {
        /// <summary>循环起点（按采样）</summary>
        public uint? StartInSamples { get; init; }
        /// <summary>循环终点（按采样）</summary>
        public uint? EndInSamples { get; init; }
        /// <summary>循环长度（按采样）</summary>
        public uint? LengthInSamples { get; init; }
        /// <summary>循环起点（按秒）</summary>
        public double? StartInSeconds { get; init; }
        /// <summary>循环终点（按秒）</summary>
        public double? EndInSeconds { get; init; }
        /// <summary>循环长度（按秒）</summary>
        public double? LengthInSeconds { get; init; }

        /// <summary>按字段组合解析出循环类型与单位（判定顺序与 Lua 侧一致）</summary>
        internal void Resolve(out MusicLoopRangeType type, out MusicLoopRangeUnit unit)
        {
            // 按采样
            if (StartInSamples is not null && EndInSamples is not null)
            {
                type = MusicLoopRangeType.StartPointAndEndPoint;
                unit = MusicLoopRangeUnit.Sample;
                return;
            }
            if (StartInSamples is not null && LengthInSamples is not null)
            {
                type = MusicLoopRangeType.StartPointAndLength;
                unit = MusicLoopRangeUnit.Sample;
                return;
            }
            if (LengthInSamples is not null && EndInSamples is not null)
            {
                type = MusicLoopRangeType.LengthAndEndPoint;
                unit = MusicLoopRangeUnit.Sample;
                return;
            }
            if (StartInSamples is not null)
            {
                type = MusicLoopRangeType.StartPointToEnd;
                unit = MusicLoopRangeUnit.Sample;
                return;
            }
            if (EndInSamples is not null)
            {
                type = MusicLoopRangeType.StartToEndPoint;
                unit = MusicLoopRangeUnit.Sample;
                return;
            }

            // 按秒
            if (StartInSeconds is not null && EndInSeconds is not null)
            {
                type = MusicLoopRangeType.StartPointAndEndPoint;
                unit = MusicLoopRangeUnit.Second;
                return;
            }
            if (StartInSeconds is not null && LengthInSeconds is not null)
            {
                type = MusicLoopRangeType.StartPointAndLength;
                unit = MusicLoopRangeUnit.Second;
                return;
            }
            if (LengthInSeconds is not null && EndInSeconds is not null)
            {
                type = MusicLoopRangeType.LengthAndEndPoint;
                unit = MusicLoopRangeUnit.Second;
                return;
            }
            if (StartInSeconds is not null)
            {
                type = MusicLoopRangeType.StartPointToEnd;
                unit = MusicLoopRangeUnit.Second;
                return;
            }
            if (EndInSeconds is not null)
            {
                type = MusicLoopRangeType.StartToEndPoint;
                unit = MusicLoopRangeUnit.Second;
                return;
            }

            // 全曲循环
            type = MusicLoopRangeType.All;
            unit = MusicLoopRangeUnit.Sample;
        }
    }

    /// <summary>
    /// 音频 API（对应 Lua 侧 lstg / lstg.Audio 库，见 LW_Audio.cpp）。
    /// </summary>
    public static unsafe partial class Audio
    {
        // ========== 音频设备 ==========

        /// <summary>音频引擎是否已初始化</summary>
        public static bool IsAudioEngineReady => LuaSTGAPI.api.audio_isAudioEngineReady() != 0;

        /// <summary>
        /// 枚举音频输出设备（对应 lstg.ListAudioDevice）。
        /// </summary>
        /// <param name="refresh">是否先刷新设备列表</param>
        /// <returns>设备名称数组</returns>
        /// <exception cref="InvalidOperationException">音频引擎未初始化</exception>
        public static string[] ListAudioDevice(bool refresh = false)
        {
            if (!IsAudioEngineReady)
                ThrowEngineNotInitialized();
            if (refresh)
            {
                _ = LuaSTGAPI.api.audio_refreshAudioEndpoints();
            }
            var count = LuaSTGAPI.api.audio_getAudioEndpointCount();
            var names = new string[count];
            for (uint i = 0; i < count; i++)
            {
                names[i] = StringMarshal.FromUtf8(LuaSTGAPI.api.audio_getAudioEndpointName(i));
            }
            return names;
        }

        /// <summary>
        /// 切换音频输出设备（对应 lstg.ChangeAudioDevice）。
        /// </summary>
        /// <param name="name">设备名称</param>
        /// <returns>是否切换成功</returns>
        /// <exception cref="InvalidOperationException">音频引擎未初始化</exception>
        public static bool ChangeAudioDevice(string name)
        {
            if (!IsAudioEngineReady)
                ThrowEngineNotInitialized();
            using var s = new MarshaledString(name);
            return LuaSTGAPI.api.audio_setAudioEndpoint(s) != 0;
        }

        /// <summary>
        /// 获取当前音频输出设备名称（对应 lstg.GetCurrentAudioDeviceName）。
        /// </summary>
        /// <exception cref="InvalidOperationException">音频引擎未初始化</exception>
        public static string GetCurrentAudioDeviceName()
        {
            if (!IsAudioEngineReady)
                ThrowEngineNotInitialized();
            return StringMarshal.FromUtf8(LuaSTGAPI.api.audio_getCurrentAudioEndpointName());
        }

        // ========== 声音（SoundEffect）==========

        /// <summary>
        /// 播放声音（对应 lstg.PlaySound），音量钳制到 [0,1]，声像钳制到 [-1,1]。
        /// </summary>
        /// <exception cref="ArgumentException">声音资源不存在</exception>
        public static void PlaySound(string name, float volume = 1.0f, float pan = 0.0f)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_playSound(s, volume, pan), "sound", name);
        }

        /// <summary>停止声音（对应 lstg.StopSound）</summary>
        /// <exception cref="ArgumentException">声音资源不存在</exception>
        public static void StopSound(string name)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_stopSound(s), "sound", name);
        }

        /// <summary>暂停声音（对应 lstg.PauseSound）</summary>
        /// <exception cref="ArgumentException">声音资源不存在</exception>
        public static void PauseSound(string name)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_pauseSound(s), "sound", name);
        }

        /// <summary>恢复声音（对应 lstg.ResumeSound）</summary>
        /// <exception cref="ArgumentException">声音资源不存在</exception>
        public static void ResumeSound(string name)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_resumeSound(s), "sound", name);
        }

        /// <summary>
        /// 获取声音播放状态（对应 lstg.GetSoundState，Lua 侧返回 "playing"/"stopped"/"paused"）。
        /// </summary>
        /// <exception cref="ArgumentException">声音资源不存在</exception>
        public static AudioState GetSoundState(string name)
        {
            using var s = new MarshaledString(name);
            var state = LuaSTGAPI.api.audio_getSoundState(s);
            if (state < 0)
                ThrowResourceNotFound("sound", name);
            return (AudioState)state;
        }

        /// <summary>设置全局声音音量（对应 lstg.SetSEVolume），音量钳制到 [0,1]</summary>
        public static void SetSEVolume(float volume)
            => LuaSTGAPI.api.audio_setSEVolume(volume);

        /// <summary>获取全局声音音量（对应 lstg.GetSEVolume）</summary>
        public static float GetSEVolume()
            => LuaSTGAPI.api.audio_getSEVolume();

        /// <summary>设置声音播放速度（对应 lstg.SetSESpeed）</summary>
        /// <exception cref="ArgumentException">声音资源不存在</exception>
        /// <exception cref="InvalidOperationException">设置播放速度失败</exception>
        public static void SetSESpeed(string name, float speed)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_setSESpeed(s, speed), "sound", name);
        }

        /// <summary>获取声音播放速度（对应 lstg.GetSESpeed）</summary>
        /// <exception cref="ArgumentException">声音资源不存在</exception>
        public static float GetSESpeed(string name)
        {
            double speed = 0.0;
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_getSESpeed(s, &speed), "sound", name);
            return (float)speed;
        }

        /// <summary>
        /// 更新声音（对应 lstg.UpdateSound）。
        /// 引擎侧为否决的方法，保留空实现以对齐 Lua API。
        /// </summary>
        public static void UpdateSound()
        {
        }

        // ========== 音乐（BGM）==========

        /// <summary>
        /// 播放音乐（对应 lstg.PlayMusic），音量钳制到 [0,1]。
        /// </summary>
        /// <param name="position">起始播放位置（秒）</param>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static void PlayMusic(string name, float volume = 1.0f, double position = 0.0)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_playMusic(s, volume, position), "music", name);
        }

        /// <summary>停止音乐（对应 lstg.StopMusic）</summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static void StopMusic(string name)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_stopMusic(s), "music", name);
        }

        /// <summary>暂停音乐（对应 lstg.PauseMusic）</summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static void PauseMusic(string name)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_pauseMusic(s), "music", name);
        }

        /// <summary>恢复音乐（对应 lstg.ResumeMusic）</summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static void ResumeMusic(string name)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_resumeMusic(s), "music", name);
        }

        /// <summary>
        /// 获取音乐播放状态（对应 lstg.GetMusicState，Lua 侧返回 "playing"/"paused"/"stopped"）。
        /// </summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static AudioState GetMusicState(string name)
        {
            using var s = new MarshaledString(name);
            var state = LuaSTGAPI.api.audio_getMusicState(s);
            if (state < 0)
                ThrowResourceNotFound("music", name);
            return (AudioState)state;
        }

        /// <summary>
        /// 获取音乐频谱（对应 lstg.GetMusicFFT，Lua 侧返回 table 数组）。
        /// </summary>
        /// <param name="name">音乐资源名称</param>
        /// <param name="buffer">接收频谱数据的缓冲</param>
        /// <returns>频谱总长度；缓冲不足时只写入缓冲容量内的数据</returns>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static int GetMusicFFT(string name, Span<double> buffer)
        {
            using var s = new MarshaledString(name);
            fixed (double* p = buffer)
            {
                var size = LuaSTGAPI.api.audio_getMusicFFT(s, p, (uint)buffer.Length);
                if (size < 0)
                    ThrowResourceNotFound("music", name);
                return size;
            }
        }

        /// <summary>
        /// 获取音乐频谱（对应 lstg.GetMusicFFT），返回新分配的数组。
        /// </summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static double[] GetMusicFFT(string name)
        {
            using var s = new MarshaledString(name);
            var size = LuaSTGAPI.api.audio_getMusicFFT(s, null, 0);
            if (size < 0)
                ThrowResourceNotFound("music", name);
            var result = new double[size];
            if (size > 0)
            {
                fixed (double* p = result)
                {
                    var written = LuaSTGAPI.api.audio_getMusicFFT(s, p, (uint)size);
                    if (written < 0)
                        ThrowResourceNotFound("music", name);
                }
            }
            return result;
        }

        /// <summary>
        /// 设置音乐循环范围（对应 lstg.SetMusicLoopRange）。
        /// 传入 null（或不指定 range）禁用循环，与 Lua 侧不传 table 的行为一致。
        /// </summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static void SetMusicLoopRange(string name, MusicLoopRange? range = null)
        {
            using var s = new MarshaledString(name);
            int result;
            if (range is null)
            {
                result = LuaSTGAPI.api.audio_setMusicLoopRange(
                    s, (int)MusicLoopRangeType.Disable, (int)MusicLoopRangeUnit.Sample,
                    0, 0, 0, 0.0, 0.0, 0.0);
            }
            else
            {
                range.Resolve(out var type, out var unit);
                result = LuaSTGAPI.api.audio_setMusicLoopRange(
                    s, (int)type, (int)unit,
                    range.StartInSamples ?? 0, range.EndInSamples ?? 0, range.LengthInSamples ?? 0,
                    range.StartInSeconds ?? 0.0, range.EndInSeconds ?? 0.0, range.LengthInSeconds ?? 0.0);
            }
            ThrowIfError(result, "music", name);
        }

        /// <summary>设置全局音乐音量（对应 lstg.SetBGMVolume），音量钳制到 [0,1]</summary>
        public static void SetBGMVolume(float volume)
            => LuaSTGAPI.api.audio_setBGMVolume(volume);

        /// <summary>获取全局音乐音量（对应 lstg.GetBGMVolume）</summary>
        public static float GetBGMVolume()
            => LuaSTGAPI.api.audio_getBGMVolume();

        /// <summary>
        /// 设置指定音乐的音量（对应 lstg.SetBGMVolume 的双参数形式），音量钳制到 [0,1]。
        /// </summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static void SetBGMVolume(string name, float volume)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_setMusicVolume(s, volume), "music", name);
        }

        /// <summary>
        /// 获取指定音乐的音量（对应 lstg.GetBGMVolume 的带参数形式）。
        /// </summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static float GetBGMVolume(string name)
        {
            double volume = 0.0;
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_getMusicVolume(s, &volume), "music", name);
            return (float)volume;
        }

        /// <summary>设置音乐播放速度（对应 lstg.SetBGMSpeed）</summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        /// <exception cref="InvalidOperationException">设置播放速度失败</exception>
        public static void SetBGMSpeed(string name, float speed)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_setBGMSpeed(s, speed), "music", name);
        }

        /// <summary>获取音乐播放速度（对应 lstg.GetBGMSpeed）</summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static float GetBGMSpeed(string name)
        {
            double speed = 0.0;
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_getBGMSpeed(s, &speed), "music", name);
            return (float)speed;
        }

        /// <summary>设置音乐是否循环（对应 lstg.SetBGMLoop）</summary>
        /// <exception cref="ArgumentException">音乐资源不存在</exception>
        public static void SetBGMLoop(string name, bool loop)
        {
            using var s = new MarshaledString(name);
            ThrowIfError(LuaSTGAPI.api.audio_setBGMLoop(s, loop ? (byte)1 : (byte)0), "music", name);
        }

        // ========== 错误处理 ==========

        /// <summary>按错误码抛出与 Lua 侧 luaL_error 对应的异常</summary>
        private static void ThrowIfError(int errorCode, string kind, string name)
        {
            switch (errorCode)
            {
                case 0:
                    return;
                case 1:
                    ThrowResourceNotFound(kind, name);
                    return;
                case 2:
                    throw new InvalidOperationException($"Can't set {kind}('{name}') playing speed.");
                default:
                    throw new InvalidOperationException($"audio API error {errorCode} on {kind} '{name}'.");
            }
        }

        private static void ThrowResourceNotFound(string kind, string name)
            => throw new ArgumentException($"{kind} '{name}' not found.", nameof(name));

        private static void ThrowEngineNotInitialized()
            => throw new InvalidOperationException("audio engine not initialized");
    }
}
