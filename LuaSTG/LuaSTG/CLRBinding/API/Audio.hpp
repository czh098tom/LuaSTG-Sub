// LuaSTG CoreCLR 绑定：音频（对应 LW_Audio，Lua 侧注册到 lstg 与 lstg.Audio）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 错误码约定（按名称操作的 API 的 int32_t 返回值）：
//   0 = 成功；1 = 资源未找到；2 = 引擎操作失败（如设置播放速度失败）
// 播放状态（int32_t，取值与 core::AudioPlayerState 一致）：
//   0 = stopped；1 = playing；2 = paused；-1 = 资源未找到
// 返回的 const char* 指向引擎侧静态缓冲，C# 侧必须在下一次调用前拷贝

// ===== 音频设备 =====
// 音频引擎是否已初始化（Lua 侧 engine not initialized 时 luaL_error）
DECLARE_CLR_API(uint8_t, audio_isAudioEngineReady, ())
// 刷新音频端点列表，返回是否成功（对应 ListAudioDevice 的 refresh 参数）
DECLARE_CLR_API(uint8_t, audio_refreshAudioEndpoints, ())
// 音频端点数量
DECLARE_CLR_API(uint32_t, audio_getAudioEndpointCount, ())
// 按索引取音频端点名称，索引越界返回空串
DECLARE_CLR_API(const char*, audio_getAudioEndpointName, (uint32_t index))
// 切换音频端点（对应 ChangeAudioDevice），返回是否成功
DECLARE_CLR_API(uint8_t, audio_setAudioEndpoint, (const char* name))
// 当前音频端点名称（对应 GetCurrentAudioDeviceName）
DECLARE_CLR_API(const char*, audio_getCurrentAudioEndpointName, ())

// ===== 声音（SoundEffect）=====
DECLARE_CLR_API(int32_t, audio_playSound, (const char* name, float volume, float pan))
DECLARE_CLR_API(int32_t, audio_stopSound, (const char* name))
DECLARE_CLR_API(int32_t, audio_pauseSound, (const char* name))
DECLARE_CLR_API(int32_t, audio_resumeSound, (const char* name))
DECLARE_CLR_API(int32_t, audio_getSoundState, (const char* name))
// 全局 SE 音量（AppFrame::SetSEVolume/GetSEVolume），音量在引擎侧钳制到 [0,1]
DECLARE_CLR_API(void, audio_setSEVolume, (float volume))
DECLARE_CLR_API(float, audio_getSEVolume, ())
DECLARE_CLR_API(int32_t, audio_setSESpeed, (const char* name, float speed))
DECLARE_CLR_API(int32_t, audio_getSESpeed, (const char* name, double* out_speed))
// UpdateSound 在 Lua 侧是“否决的方法”（空操作），仅在 C# 侧保留同名空方法，不占用引擎 API

// ===== 音乐（BGM）=====
DECLARE_CLR_API(int32_t, audio_playMusic, (const char* name, float volume, double position))
DECLARE_CLR_API(int32_t, audio_stopMusic, (const char* name))
DECLARE_CLR_API(int32_t, audio_pauseMusic, (const char* name))
DECLARE_CLR_API(int32_t, audio_resumeMusic, (const char* name))
DECLARE_CLR_API(int32_t, audio_getMusicState, (const char* name))
// 写入 FFT 数据并返回 FFT 总长度；实际写入 min(长度, capacity) 个，buffer 为空时仅查询长度
DECLARE_CLR_API(int32_t, audio_getMusicFFT, (const char* name, double* buffer, uint32_t capacity))
// 设置循环范围，range_type/range_unit 取值与 MusicRoopRangeType/MusicRoopRangeUnit 一致，
// type 为 0（Disable）时其余参数被忽略（对应 Lua 侧不传 table 的用法）
DECLARE_CLR_API(int32_t, audio_setMusicLoopRange, (const char* name, int32_t range_type, int32_t range_unit, uint32_t start_in_samples, uint32_t end_in_samples, uint32_t length_in_samples, double start_in_seconds, double end_in_seconds, double length_in_seconds))
// 全局 BGM 音量（AppFrame::SetBGMVolume/GetBGMVolume），音量在引擎侧钳制到 [0,1]
DECLARE_CLR_API(void, audio_setBGMVolume, (float volume))
DECLARE_CLR_API(float, audio_getBGMVolume, ())
// 按名称的单个音乐音量（对应 SetBGMVolume/GetBGMVolume 传名字的重载形式）
DECLARE_CLR_API(int32_t, audio_setMusicVolume, (const char* name, float volume))
DECLARE_CLR_API(int32_t, audio_getMusicVolume, (const char* name, double* out_volume))
DECLARE_CLR_API(int32_t, audio_setBGMSpeed, (const char* name, float speed))
DECLARE_CLR_API(int32_t, audio_getBGMSpeed, (const char* name, double* out_speed))
DECLARE_CLR_API(int32_t, audio_setBGMLoop, (const char* name, uint8_t loop))
