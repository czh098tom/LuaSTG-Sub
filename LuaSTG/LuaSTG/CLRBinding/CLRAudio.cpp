#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"
#include "GameResource/ResourceManager.h"

#include <algorithm>
#include <string>
#include <string_view>

// 对应 Lua 侧绑定：LuaSTG/LuaSTG/LuaBinding/LW_Audio.cpp（lstg 与 lstg.Audio 两套注册的同一批函数）
// 错误处理：Lua 侧 luaL_error 的场景改为错误码返回（见 API/Audio.hpp 顶部约定），
// 由 C# 侧包装（LuaSTG.Core/Audio.cs）抛出异常，保持与 Lua 侧报错行为一致。

namespace
{
	// 返回给 C# 的字符串使用各自的线程局部缓冲（C# 侧在调用返回后立即拷贝）
	thread_local std::string g_endpoint_name_by_index;
	thread_local std::string g_current_endpoint_name;
}

namespace luastg
{
	// ===== 音频设备 =====

	uint8_t CLRBinding::audio_isAudioEngineReady()
	{
		return LAPP.getAudioEngine() != nullptr ? 1 : 0;
	}

	uint8_t CLRBinding::audio_refreshAudioEndpoints()
	{
		auto const audio_engine = LAPP.getAudioEngine();
		if (!audio_engine)
			return 0;
		return audio_engine->refreshAudioEndpoint() ? 1 : 0;
	}

	uint32_t CLRBinding::audio_getAudioEndpointCount()
	{
		auto const audio_engine = LAPP.getAudioEngine();
		if (!audio_engine)
			return 0;
		return audio_engine->getAudioEndpointCount();
	}

	const char* CLRBinding::audio_getAudioEndpointName(uint32_t const index)
	{
		auto const audio_engine = LAPP.getAudioEngine();
		if (!audio_engine || index >= audio_engine->getAudioEndpointCount())
			return "";
		g_endpoint_name_by_index = audio_engine->getAudioEndpointName(index);
		return g_endpoint_name_by_index.c_str();
	}

	uint8_t CLRBinding::audio_setAudioEndpoint(const char* const name)
	{
		auto const audio_engine = LAPP.getAudioEngine();
		if (!audio_engine || name == nullptr)
			return 0;
		return audio_engine->setAudioEndpoint(std::string_view(name)) ? 1 : 0;
	}

	const char* CLRBinding::audio_getCurrentAudioEndpointName()
	{
		auto const audio_engine = LAPP.getAudioEngine();
		if (!audio_engine)
			return "";
		g_current_endpoint_name = audio_engine->getCurrentAudioEndpointName();
		return g_current_endpoint_name.c_str();
	}

	// ===== 声音（SoundEffect）=====

	int32_t CLRBinding::audio_playSound(const char* const name, float const volume, float const pan)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceSoundEffect> p = LRES.FindSound(name);
		if (!p)
			return 1;
		p->Play(std::clamp(volume, 0.0f, 1.0f), std::clamp(pan, -1.0f, 1.0f));
		return 0;
	}

	int32_t CLRBinding::audio_stopSound(const char* const name)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceSoundEffect> p = LRES.FindSound(name);
		if (!p)
			return 1;
		p->Stop();
		return 0;
	}

	int32_t CLRBinding::audio_pauseSound(const char* const name)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceSoundEffect> p = LRES.FindSound(name);
		if (!p)
			return 1;
		p->Pause();
		return 0;
	}

	int32_t CLRBinding::audio_resumeSound(const char* const name)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceSoundEffect> p = LRES.FindSound(name);
		if (!p)
			return 1;
		p->Resume();
		return 0;
	}

	int32_t CLRBinding::audio_getSoundState(const char* const name)
	{
		if (name == nullptr)
			return -1;
		core::SmartReference<IResourceSoundEffect> p = LRES.FindSound(name);
		if (!p)
			return -1;
		// 与 Lua 侧一致：IsPlaying -> playing，IsStopped -> stopped，否则 -> paused
		if (p->IsPlaying())
			return 1;
		if (p->IsStopped())
			return 0;
		return 2;
	}

	void CLRBinding::audio_setSEVolume(float const volume)
	{
		LAPP.SetSEVolume(std::clamp(volume, 0.0f, 1.0f));
	}

	float CLRBinding::audio_getSEVolume()
	{
		return LAPP.GetSEVolume();
	}

	int32_t CLRBinding::audio_setSESpeed(const char* const name, float const speed)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceSoundEffect> p = LRES.FindSound(name);
		if (!p)
			return 1;
		if (!p->SetSpeed(speed))
			return 2;
		return 0;
	}

	int32_t CLRBinding::audio_getSESpeed(const char* const name, double* const out_speed)
	{
		if (out_speed != nullptr)
			*out_speed = 0.0;
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceSoundEffect> p = LRES.FindSound(name);
		if (!p)
			return 1;
		if (out_speed != nullptr)
			*out_speed = static_cast<double>(p->GetSpeed());
		return 0;
	}

	// ===== 音乐（BGM）=====

	int32_t CLRBinding::audio_playMusic(const char* const name, float const volume, double const position)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		p->Play(std::clamp(volume, 0.0f, 1.0f), position);
		return 0;
	}

	int32_t CLRBinding::audio_stopMusic(const char* const name)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		p->Stop();
		return 0;
	}

	int32_t CLRBinding::audio_pauseMusic(const char* const name)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		p->Pause();
		return 0;
	}

	int32_t CLRBinding::audio_resumeMusic(const char* const name)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		p->Resume();
		return 0;
	}

	int32_t CLRBinding::audio_getMusicState(const char* const name)
	{
		if (name == nullptr)
			return -1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return -1;
		// 与 Lua 侧一致：IsPlaying -> playing，IsPaused -> paused，否则 -> stopped
		if (p->IsPlaying())
			return 1;
		if (p->IsPaused())
			return 2;
		return 0;
	}

	int32_t CLRBinding::audio_getMusicFFT(const char* const name, double* const buffer, uint32_t const capacity)
	{
		if (name == nullptr)
			return -1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return -1;
		auto const player = p->GetAudioPlayer();
		if (player == nullptr)
			return -1;
		player->updateFFT();
		uint32_t const size = player->getFFTSize();
		float const* const data = player->getFFT();
		if (data == nullptr)
			return 0;
		if (buffer != nullptr && capacity > 0)
		{
			uint32_t const count = std::min(size, capacity);
			for (uint32_t i = 0; i < count; i += 1)
				buffer[i] = static_cast<double>(data[i]);
		}
		return static_cast<int32_t>(size);
	}

	int32_t CLRBinding::audio_setMusicLoopRange(const char* const name, int32_t const range_type, int32_t const range_unit,
		uint32_t const start_in_samples, uint32_t const end_in_samples, uint32_t const length_in_samples,
		double const start_in_seconds, double const end_in_seconds, double const length_in_seconds)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		MusicRoopRange range{};
		range.type = static_cast<MusicRoopRangeType>(range_type);
		range.unit = static_cast<MusicRoopRangeUnit>(range_unit);
		range.start_in_samples = start_in_samples;
		range.end_in_samples = end_in_samples;
		range.length_in_samples = length_in_samples;
		range.start_in_seconds = start_in_seconds;
		range.end_in_seconds = end_in_seconds;
		range.length_in_seconds = length_in_seconds;
		p->SetLoopRange(range);
		return 0;
	}

	void CLRBinding::audio_setBGMVolume(float const volume)
	{
		LAPP.SetBGMVolume(std::clamp(volume, 0.0f, 1.0f));
	}

	float CLRBinding::audio_getBGMVolume()
	{
		return LAPP.GetBGMVolume();
	}

	int32_t CLRBinding::audio_setMusicVolume(const char* const name, float const volume)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		p->SetVolume(std::clamp(volume, 0.0f, 1.0f));
		return 0;
	}

	int32_t CLRBinding::audio_getMusicVolume(const char* const name, double* const out_volume)
	{
		if (out_volume != nullptr)
			*out_volume = 0.0;
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		if (out_volume != nullptr)
			*out_volume = static_cast<double>(p->GetVolume());
		return 0;
	}

	int32_t CLRBinding::audio_setBGMSpeed(const char* const name, float const speed)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		if (!p->SetSpeed(speed))
			return 2;
		return 0;
	}

	int32_t CLRBinding::audio_getBGMSpeed(const char* const name, double* const out_speed)
	{
		if (out_speed != nullptr)
			*out_speed = 0.0;
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		if (out_speed != nullptr)
			*out_speed = static_cast<double>(p->GetSpeed());
		return 0;
	}

	int32_t CLRBinding::audio_setBGMLoop(const char* const name, uint8_t const loop)
	{
		if (name == nullptr)
			return 1;
		core::SmartReference<IResourceMusic> p = LRES.FindMusic(name);
		if (!p)
			return 1;
		p->SetLoop(loop != 0);
		return 0;
	}
}
