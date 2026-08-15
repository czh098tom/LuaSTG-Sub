#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"
#include "GameObject/GameObjectBentLaser.hpp"
#include "GameObject/GameObjectPool.h"
#include "GameResource/ResourceParticle.hpp"
#include "core/Color.hpp"
#include "core/Vector2.hpp"
#include "core/Vector4.hpp"
#include "windows/CleanWindows.hpp"
#include <DirectXMath.h>
#include <cstring>
#include <new>

// 杂项对象 API 实现（对应 LuaBinding/LW_Color.cpp / LW_StopWatch.cpp / LW_BentLaser.cpp / LW_ParticleSystem.cpp）
// 说明：
// - Color 在 C# 侧为值类型，仅 HSV 换算经由引擎（与 Lua 侧共用 DirectXMath 实现，保证结果一致）
// - StopWatch 句柄为 C++ 侧 new 的查询性能计数器实现（等价 LW_StopWatch.cpp 的 fcyStopWatch）
// - BentLaserData 句柄为 GameObjectBentLaser*（AllocInstance/FreeInstance 管理生命周期）
// - ParticleSystemData 句柄持有 IResourceParticle 裸指针（retain/release）与 IPooledParticle*，
//   与 LW_ParticleSystem.cpp 的 UserData 持有方式一致

using namespace luastg;

namespace
{
	////////////////////////////////////////////////////////////////////////////////
	/// 高精度停表类（与 LW_StopWatch.cpp 的 fcyStopWatch 等价）
	////////////////////////////////////////////////////////////////////////////////
	struct CLRStopWatch
	{
		int64_t m_cFreq;     ///< CPU频率
		int64_t m_cLast;     ///< 上一次时间
		int64_t m_cFixStart; ///< 暂停时的时间修复参数
		int64_t m_cFixAll;   ///< 暂停时的时间修复参数

		CLRStopWatch() noexcept
		{
			LARGE_INTEGER freq = {};
			QueryPerformanceFrequency(&freq);
			m_cFreq = freq.QuadPart;
			Reset();
		}
		void Pause() noexcept
		{
			LARGE_INTEGER t = {};
			QueryPerformanceCounter(&t);
			m_cFixStart = t.QuadPart;
		}
		void Resume() noexcept
		{
			LARGE_INTEGER t = {};
			QueryPerformanceCounter(&t);
			m_cFixAll += t.QuadPart - m_cFixStart;
		}
		void Reset() noexcept
		{
			LARGE_INTEGER t = {};
			QueryPerformanceCounter(&t);
			m_cLast = t.QuadPart;
			m_cFixAll = 0;
		}
		double GetElapsed() noexcept
		{
			LARGE_INTEGER t = {};
			QueryPerformanceCounter(&t);
			return ((double)(t.QuadPart - m_cLast - m_cFixAll)) / ((double)m_cFreq);
		}
	};

	////////////////////////////////////////////////////////////////////////////////
	/// 粒子系统实例句柄（与 LW_ParticleSystem.cpp 的 UserData 等价）
	////////////////////////////////////////////////////////////////////////////////
	struct CLRParticleSystemHandle
	{
		IResourceParticle* res = nullptr; ///< 资源（手动 retain/release）
		IParticlePool* ptr = nullptr;     ///< 粒子池实例
	};

	/// 取句柄，失败返回 nullptr
	[[nodiscard]] CLRParticleSystemHandle* getPS(uintptr_t const handle) noexcept
	{
		return reinterpret_cast<CLRParticleSystemHandle*>(handle);
	}

	////////////////////////////////////////////////////////////////////////////////
	/// HSV 换算（与 LW_Color.cpp 中的 HSV2RGB / RGB2HSV 完全一致，共用 DirectXMath）
	////////////////////////////////////////////////////////////////////////////////
	inline core::Color4B CLR_HSV2RGB(float const hue, float const saturation, float const value, float const alpha)
	{
		DirectX::XMFLOAT4 const vec(hue * 0.01f, saturation * 0.01f, value * 0.01f, alpha * 0.01f);
		DirectX::XMFLOAT4 vec2{};
		DirectX::XMStoreFloat4(&vec2, DirectX::XMColorHSVToRGB(DirectX::XMLoadFloat4(&vec)));
		return core::Color4B(
			(uint8_t)(vec2.x * 255.0f),
			(uint8_t)(vec2.y * 255.0f),
			(uint8_t)(vec2.z * 255.0f),
			(uint8_t)(vec2.w * 255.0f)
		);
	}
	// 返回 wxyz = ahsv（0~100 刻度）
	inline core::Vector4F CLR_RGB2HSV(uint8_t const red, uint8_t const green, uint8_t const blue, uint8_t const alpha)
	{
		DirectX::XMFLOAT4 const vec(red / 255.0f, green / 255.0f, blue / 255.0f, alpha / 255.0f);
		DirectX::XMFLOAT4 vec2{};
		DirectX::XMStoreFloat4(&vec2, DirectX::XMColorRGBToHSV(DirectX::XMLoadFloat4(&vec)));
		return core::Vector4(
			vec2.x * 100.0f,
			vec2.y * 100.0f,
			vec2.z * 100.0f,
			vec2.w * 100.0f
		);
	}

	////////////////////////////////////////////////////////////////////////////////
	/// hgeParticleSystemInfo 数值字段编号（与 C# 侧 ParticleSystemInfoField 保持同步）
	////////////////////////////////////////////////////////////////////////////////
	enum class PSField : uint8_t
	{
		Lifetime = 0,       // fLifetime
		ParticleLifeMin = 1,// fParticleLifeMin
		ParticleLifeMax = 2,// fParticleLifeMax
		Direction = 3,      // fDirection
		Spread = 4,         // fSpread
		SpeedMin = 5,       // fSpeedMin
		SpeedMax = 6,       // fSpeedMax
		GravityMin = 7,     // fGravityMin
		GravityMax = 8,     // fGravityMax
		RadialAccelMin = 9, // fRadialAccelMin
		RadialAccelMax = 10,// fRadialAccelMax
		TangentialAccelMin = 11, // fTangentialAccelMin
		TangentialAccelMax = 12, // fTangentialAccelMax
		SizeStart = 13,     // fSizeStart
		SizeEnd = 14,       // fSizeEnd
		SizeVar = 15,       // fSizeVar
		SpinStart = 16,     // fSpinStart
		SpinEnd = 17,       // fSpinEnd
		SpinVar = 18,       // fSpinVar
		ColorVar = 19,      // fColorVar
		AlphaVar = 20,      // fAlphaVar
	};

	[[nodiscard]] float* getPSFieldPtr(hgeParticleSystemInfo& info, uint8_t const field_id) noexcept
	{
		switch (static_cast<PSField>(field_id))
		{
		case PSField::Lifetime:        return &info.fLifetime;
		case PSField::ParticleLifeMin: return &info.fParticleLifeMin;
		case PSField::ParticleLifeMax: return &info.fParticleLifeMax;
		case PSField::Direction:       return &info.fDirection;
		case PSField::Spread:          return &info.fSpread;
		case PSField::SpeedMin:        return &info.fSpeedMin;
		case PSField::SpeedMax:        return &info.fSpeedMax;
		case PSField::GravityMin:      return &info.fGravityMin;
		case PSField::GravityMax:      return &info.fGravityMax;
		case PSField::RadialAccelMin:  return &info.fRadialAccelMin;
		case PSField::RadialAccelMax:  return &info.fRadialAccelMax;
		case PSField::TangentialAccelMin: return &info.fTangentialAccelMin;
		case PSField::TangentialAccelMax: return &info.fTangentialAccelMax;
		case PSField::SizeStart:       return &info.fSizeStart;
		case PSField::SizeEnd:         return &info.fSizeEnd;
		case PSField::SizeVar:         return &info.fSizeVar;
		case PSField::SpinStart:       return &info.fSpinStart;
		case PSField::SpinEnd:         return &info.fSpinEnd;
		case PSField::SpinVar:         return &info.fSpinVar;
		case PSField::ColorVar:        return &info.fColorVar;
		case PSField::AlphaVar:        return &info.fAlphaVar;
		default:                       return nullptr;
		}
	}

	/// 浮点颜色分量 -> ARGB（与 LW_ParticleSystem.cpp 的 Color4f_to_Color4B 一致）
	[[nodiscard]] uint32_t color4fToARGB(float const c[4]) noexcept
	{
		return core::Color4B(
			(uint8_t)std::clamp(c[0] * 255.0f, 0.0f, 255.0f),
			(uint8_t)std::clamp(c[1] * 255.0f, 0.0f, 255.0f),
			(uint8_t)std::clamp(c[2] * 255.0f, 0.0f, 255.0f),
			(uint8_t)std::clamp(c[3] * 255.0f, 0.0f, 255.0f)
		).color();
	}
	/// ARGB -> 浮点颜色分量（与 LW_ParticleSystem.cpp 的 Color4B_to_Color4f 一致）
	void argbToColor4f(uint32_t const argb, float d[4]) noexcept
	{
		core::Color4B const c(argb);
		d[0] = (float)c.r / 255.0f;
		d[1] = (float)c.g / 255.0f;
		d[2] = (float)c.b / 255.0f;
		d[3] = (float)c.a / 255.0f;
	}

	[[nodiscard]] GameObjectBentLaser* getLaser(uintptr_t const handle) noexcept
	{
		return reinterpret_cast<GameObjectBentLaser*>(handle);
	}
}

// ==============================================================================
// Color（HSV 换算）
// ==============================================================================

uint32_t luastg::CLRBinding::color_hsvToARGB(double const a, double const h, double const s, double const v)
{
	// 输入刻度 0~100，与 Lua 侧 HSVColor / AHSV 的钳制一致
	auto const ca = (float)std::clamp(a, 0.0, 100.0);
	auto const ch = (float)std::clamp(h, 0.0, 100.0);
	auto const cs = (float)std::clamp(s, 0.0, 100.0);
	auto const cv = (float)std::clamp(v, 0.0, 100.0);
	return CLR_HSV2RGB(ch, cs, cv, ca).color();
}

void luastg::CLRBinding::color_argbToHSV(uint32_t const argb, double* const a, double* const h, double* const s, double* const v)
{
	core::Color4B const c(argb);
	core::Vector4F const hsva = CLR_RGB2HSV(c.r, c.g, c.b, c.a);
	if (a) *a = hsva.w;
	if (h) *h = hsva.x;
	if (s) *s = hsva.y;
	if (v) *v = hsva.z;
}

// ==============================================================================
// StopWatch
// ==============================================================================

uintptr_t luastg::CLRBinding::stopWatch_create()
{
	auto* const p = new (std::nothrow) CLRStopWatch();
	return reinterpret_cast<uintptr_t>(p);
}

void luastg::CLRBinding::stopWatch_destroy(uintptr_t const handle)
{
	delete reinterpret_cast<CLRStopWatch*>(handle);
}

void luastg::CLRBinding::stopWatch_reset(uintptr_t const handle)
{
	if (auto* const p = reinterpret_cast<CLRStopWatch*>(handle))
		p->Reset();
}

void luastg::CLRBinding::stopWatch_pause(uintptr_t const handle)
{
	if (auto* const p = reinterpret_cast<CLRStopWatch*>(handle))
		p->Pause();
}

void luastg::CLRBinding::stopWatch_resume(uintptr_t const handle)
{
	if (auto* const p = reinterpret_cast<CLRStopWatch*>(handle))
		p->Resume();
}

double luastg::CLRBinding::stopWatch_getElapsed(uintptr_t const handle)
{
	auto* const p = reinterpret_cast<CLRStopWatch*>(handle);
	return p != nullptr ? p->GetElapsed() : 0.0;
}

// ==============================================================================
// BentLaserData
// ==============================================================================

uintptr_t luastg::CLRBinding::bentLaser_create()
{
	GameObjectBentLaser* p = nullptr;
	try
	{
		p = GameObjectBentLaser::AllocInstance(); // 可能抛出 bad_alloc
	}
	catch (std::bad_alloc const&)
	{
		p = nullptr;
	}
	return reinterpret_cast<uintptr_t>(p);
}

void luastg::CLRBinding::bentLaser_destroy(uintptr_t const handle)
{
	if (auto* const p = getLaser(handle))
	{
		GameObjectBentLaser::FreeInstance(p);
	}
}

uint8_t luastg::CLRBinding::bentLaser_update(uintptr_t const handle, double const x, double const y, double const rot, int32_t const length, double const width, uint8_t const active)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	return laser->Update((float)x, (float)y, (float)rot, length, (float)width, active != 0) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_updateByObject(uintptr_t const handle, uintptr_t const object, int32_t const length, double const width, uint8_t const active)
{
	auto* const laser = getLaser(handle);
	auto* const obj = reinterpret_cast<GameObject*>(object);
	if (laser == nullptr || obj == nullptr)
		return 0;
	// 等价于引擎 Update(size_t id, ...)：读取对象的原始坐标与朝向
	return laser->Update((float)obj->x, (float)obj->y, (float)obj->rot, length, (float)width, active != 0) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_updateSingleNode(uintptr_t const handle, int32_t const index, double const x, double const y, double const width)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr || index < 0)
		return 0;
	return laser->UpdateNodeDirect((size_t)index, (float)x, (float)y, (float)width) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_updateNodeByObject(uintptr_t const handle, uintptr_t const object, int32_t const node, int32_t const length, double const width, uint8_t const active)
{
	auto* const laser = getLaser(handle);
	auto* const obj = reinterpret_cast<GameObject*>(object);
	if (laser == nullptr || obj == nullptr)
		return 0;
	// 与 Lua 侧一致：借助对象池 id 校验对象有效性
	return laser->UpdateByNode((size_t)obj->id, node, length, (float)width, active != 0) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_updatePositionByList(uintptr_t const handle, const double* const positions, int32_t const length, double const width, int32_t const index, uint8_t const revert)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	return laser->UpdatePositionByList(positions, length, (float)width, index, revert != 0) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_updateAllNode(uintptr_t const handle, int32_t const node_count, const float* const xs, const float* const ys, const float* const widths, double const width)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	return laser->UpdateAllNodeByList(node_count, xs, ys, widths, (float)width) ? 1 : 0;
}

int32_t luastg::CLRBinding::bentLaser_sampleByLength(uintptr_t const handle, double const length, float* const out_x, float* const out_y, float* const out_rot, int32_t const capacity)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	return laser->SampleByLength((float)length, out_x, out_y, out_rot, capacity);
}

int32_t luastg::CLRBinding::bentLaser_sampleByTime(uintptr_t const handle, double const time, float* const out_x, float* const out_y, float* const out_rot, int32_t const capacity)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	// 与 Lua 侧 SampleByTime 一致：内部除以 60 转为帧间隔
	return laser->SampleByTime((float)(time / 60.0), out_x, out_y, out_rot, capacity);
}

uint8_t luastg::CLRBinding::bentLaser_render(uintptr_t const handle, const char* const tex_name, uint8_t const blend, uint32_t const argb, double const tex_left, double const tex_top, double const tex_width, double const tex_height, double const scale)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	float scale_f = (float)scale;
#ifdef GLOBAL_SCALE_COLLI_SHAPE
	scale_f *= LRES.GetGlobalImageScaleFactor();
#endif
	return laser->Render(
		tex_name,
		static_cast<BlendMode>(blend),
		core::Color4B(argb),
		(float)tex_left, (float)tex_top, (float)tex_width, (float)tex_height,
		scale_f
	) ? 1 : 0;
}

void luastg::CLRBinding::bentLaser_renderCollider(uintptr_t const handle, uint32_t const argb)
{
	if (auto* const laser = getLaser(handle))
	{
		laser->RenderCollider(core::Color4B(argb));
	}
}

uint8_t luastg::CLRBinding::bentLaser_collisionCheck(uintptr_t const handle, double const x, double const y, double const rot, double const a, double const b, uint8_t const rect)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	return laser->CollisionCheck((float)x, (float)y, (float)rot, (float)a, (float)b, rect != 0) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_collisionCheckWidth(uintptr_t const handle, double const width, double const x, double const y, double const rot, double const a, double const b, uint8_t const rect)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return 0;
	return laser->CollisionCheckW((float)x, (float)y, (float)rot, (float)a, (float)b, rect != 0, (float)width) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_collisionCheckWithObject(uintptr_t const handle, uintptr_t const object, double const width)
{
	auto* const laser = getLaser(handle);
	auto* const obj = reinterpret_cast<GameObject*>(object);
	if (laser == nullptr || obj == nullptr)
		return 0;
	// 与 Lua 侧对象形式一致：读取对象的原始 rot/a/b/rect
	return laser->CollisionCheckW((float)obj->x, (float)obj->y, (float)obj->rot, (float)obj->a, (float)obj->b, obj->rect, (float)width) ? 1 : 0;
}

uint8_t luastg::CLRBinding::bentLaser_boundCheck(uintptr_t const handle)
{
	auto* const laser = getLaser(handle);
	return laser != nullptr && laser->BoundCheck() ? 1 : 0;
}

void luastg::CLRBinding::bentLaser_setAllWidth(uintptr_t const handle, double const width)
{
	if (auto* const laser = getLaser(handle))
	{
		laser->SetAllWidth((float)width);
	}
}

void luastg::CLRBinding::bentLaser_setEnvelope(uintptr_t const handle, double const height, double const base, double const rate, double const power)
{
	if (auto* const laser = getLaser(handle))
	{
		laser->SetEnvelope((float)height, (float)base, (float)rate, (float)power);
	}
}

void luastg::CLRBinding::bentLaser_getEnvelope(uintptr_t const handle, double* const height, double* const base, double* const rate, double* const power)
{
	auto* const laser = getLaser(handle);
	if (laser == nullptr)
		return;
	float h = 0.0f, b = 0.0f, r = 0.0f, p = 0.0f;
	laser->GetEnvelope(h, b, r, p);
	if (height) *height = h;
	if (base) *base = b;
	if (rate) *rate = r;
	if (power) *power = p;
}

int32_t luastg::CLRBinding::bentLaser_getNodeCount(uintptr_t const handle)
{
	auto* const laser = getLaser(handle);
	return laser != nullptr ? laser->GetSize() : 0;
}

// ==============================================================================
// ParticleSystemData
// ==============================================================================

uintptr_t luastg::CLRBinding::particleSystem_create(const char* const ps_name)
{
	if (ps_name == nullptr)
		return 0;
	auto const p_res = LRES.FindParticle(ps_name);
	if (!p_res)
		return 0;
	auto* const h = new (std::nothrow) CLRParticleSystemHandle();
	if (h == nullptr)
		return 0;
	h->res = *p_res; // 裸指针，引用计数手动管理（与 Lua 侧 UserData 一致）
	h->ptr = nullptr;
	h->res->retain();
	if (!h->res->CreateInstance(&h->ptr) || h->ptr == nullptr)
	{
		h->res->release();
		delete h;
		return 0;
	}
	return reinterpret_cast<uintptr_t>(h);
}

void luastg::CLRBinding::particleSystem_destroy(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	if (h == nullptr)
		return;
	if (h->res)
	{
		if (h->ptr)
		{
			h->res->DestroyInstance(h->ptr);
		}
		h->res->release();
	}
	h->res = nullptr;
	h->ptr = nullptr;
	delete h;
}

void luastg::CLRBinding::particleSystem_setActive(uintptr_t const handle, uint8_t const active)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->SetActive(active != 0);
	}
}

size_t luastg::CLRBinding::particleSystem_getAliveCount(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) ? h->ptr->GetAliveCount() : 0u;
}

void luastg::CLRBinding::particleSystem_setEmission(uintptr_t const handle, int32_t const emission)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->SetEmission(emission);
	}
}

int32_t luastg::CLRBinding::particleSystem_getEmission(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) ? h->ptr->GetEmission() : 0;
}

void luastg::CLRBinding::particleSystem_update(uintptr_t const handle, double const delta, double const x, double const y, double const rot_degree, uint8_t const mode)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return;
	auto* const p = h->ptr;
	if (mode >= 1u)
	{
		if (mode >= 2u)
		{
			p->SetRotation((float)(rot_degree * L_DEG_TO_RAD));
		}
		// 兼容性处理（与 Lua 侧一致）
		if (p->IsActived())
		{
			p->SetActive(false);
			p->SetCenter(core::Vector2F((float)x, (float)y));
			p->SetActive(true);
		}
		else
		{
			p->SetCenter(core::Vector2F((float)x, (float)y));
		}
	}
	p->Update((float)delta);
}

void luastg::CLRBinding::particleSystem_render(uintptr_t const handle, double const scale)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return;
	auto const scale_f = (float)scale;
	LAPP.Render(h->ptr, scale_f, scale_f);
}

void luastg::CLRBinding::particleSystem_setOldBehavior(uintptr_t const handle, uint8_t const value)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->SetOldBehavior(value != 0);
	}
}

uint8_t luastg::CLRBinding::particleSystem_isActive(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) && h->ptr->IsActived() ? 1u : 0u;
}

int32_t luastg::CLRBinding::particleSystem_getEmissionFreq(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) ? h->ptr->GetParticleSystemInfo().nEmission : 0;
}

void luastg::CLRBinding::particleSystem_setEmissionFreq(uintptr_t const handle, int32_t const value)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->GetParticleSystemInfo().nEmission = value;
	}
}

uint8_t luastg::CLRBinding::particleSystem_isRelative(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) && h->ptr->GetParticleSystemInfo().bRelative ? 1u : 0u;
}

void luastg::CLRBinding::particleSystem_setRelative(uintptr_t const handle, uint8_t const value)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->GetParticleSystemInfo().bRelative = value != 0;
	}
}

void luastg::CLRBinding::particleSystem_getCenter(uintptr_t const handle, float* const x, float* const y)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return;
	core::Vector2F const c = h->ptr->GetCenter();
	if (x) *x = c.x;
	if (y) *y = c.y;
}

void luastg::CLRBinding::particleSystem_setCenter(uintptr_t const handle, double const x, double const y)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->SetCenter(core::Vector2F((float)x, (float)y));
	}
}

float luastg::CLRBinding::particleSystem_getRotation(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) ? h->ptr->GetRotation() : 0.0f;
}

void luastg::CLRBinding::particleSystem_setRotation(uintptr_t const handle, float const rot)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->SetRotation(rot);
	}
}

uint32_t luastg::CLRBinding::particleSystem_getSeed(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) ? h->ptr->GetSeed() : 0u;
}

void luastg::CLRBinding::particleSystem_setSeed(uintptr_t const handle, uint32_t const seed)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->SetSeed(seed);
	}
}

uint8_t luastg::CLRBinding::particleSystem_getBlendMode(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	return (h != nullptr && h->ptr != nullptr) ? static_cast<uint8_t>(h->ptr->GetBlendMode()) : 0u;
}

void luastg::CLRBinding::particleSystem_setBlendMode(uintptr_t const handle, uint8_t const blend)
{
	if (auto* const h = getPS(handle); h != nullptr && h->ptr != nullptr)
	{
		h->ptr->SetBlendMode(static_cast<BlendMode>(blend));
	}
}

const char* luastg::CLRBinding::particleSystem_getResourceName(uintptr_t const handle)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->res == nullptr)
		return nullptr;
	return h->res->GetResName().data();
}

float luastg::CLRBinding::particleSystem_getInfoField(uintptr_t const handle, uint8_t const field_id)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return 0.0f;
	auto* const p = getPSFieldPtr(h->ptr->GetParticleSystemInfo(), field_id);
	return p != nullptr ? *p : 0.0f;
}

void luastg::CLRBinding::particleSystem_setInfoField(uintptr_t const handle, uint8_t const field_id, float const value)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return;
	if (auto* const p = getPSFieldPtr(h->ptr->GetParticleSystemInfo(), field_id))
	{
		*p = value;
	}
}

uint32_t luastg::CLRBinding::particleSystem_getColorField(uintptr_t const handle, uint8_t const color_id)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return 0u;
	auto& info = h->ptr->GetParticleSystemInfo();
	switch (color_id)
	{
	case 0: return color4fToARGB(info.colColorStart);
	case 1: return color4fToARGB(info.colColorEnd);
	default: return 0u;
	}
}

void luastg::CLRBinding::particleSystem_getColorFieldF(uintptr_t const handle, uint8_t const color_id, float* const r, float* const g, float* const b, float* const a)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return;
	float const* c = nullptr;
	auto& info = h->ptr->GetParticleSystemInfo();
	switch (color_id)
	{
	case 0: c = info.colColorStart; break;
	case 1: c = info.colColorEnd; break;
	default: break;
	}
	if (c == nullptr)
		return;
	if (r) *r = c[0];
	if (g) *g = c[1];
	if (b) *b = c[2];
	if (a) *a = c[3];
}

void luastg::CLRBinding::particleSystem_setColorField(uintptr_t const handle, uint8_t const color_id, uint32_t const argb)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return;
	auto& info = h->ptr->GetParticleSystemInfo();
	switch (color_id)
	{
	case 0: argbToColor4f(argb, info.colColorStart); break;
	case 1: argbToColor4f(argb, info.colColorEnd); break;
	default: break;
	}
}

void luastg::CLRBinding::particleSystem_setColorFieldF(uintptr_t const handle, uint8_t const color_id, float const r, float const g, float const b, float const a)
{
	auto* const h = getPS(handle);
	if (h == nullptr || h->ptr == nullptr)
		return;
	auto& info = h->ptr->GetParticleSystemInfo();
	float d[4] = { r, g, b, a };
	switch (color_id)
	{
	case 0: std::memcpy(info.colColorStart, d, sizeof(d)); break;
	case 1: std::memcpy(info.colColorEnd, d, sizeof(d)); break;
	default: break;
	}
}
