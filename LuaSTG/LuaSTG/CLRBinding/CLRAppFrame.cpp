#include "AppFrame.h"
#include "CLRBinding/CLRBinding.hpp"

namespace luastg
{
	/// @brief 将托管侧回调注册到对象池（实现在 CLRGameObject.cpp）
	void RegisterCLRGameObjectCallbacks();

	bool AppFrame::InitCLR() noexcept
	{
		assert(!m_CLR_active);
		try {
			m_CLR_host = new CLRHost();
		}
		catch (std::bad_alloc const&) {
			spdlog::error("[clr] 无法为 CLRHost 分配内存");
			m_CLR_host = nullptr;
			return false;
		}
		m_CLR_functions = new ManagedAPI();

		if (!InitCLRBinding(L".\\Managed\\", m_CLR_functions)) {
			delete m_CLR_functions;
			m_CLR_functions = nullptr;
			delete m_CLR_host;
			m_CLR_host = nullptr;
			return false;
		}

		RegisterCLRGameObjectCallbacks();

		m_CLR_active = true;
		spdlog::info("[clr] CoreCLR 运行时就绪");
		return true;
	}

	void AppFrame::ShutdownCLR() noexcept
	{
		m_CLR_active = false;
		delete m_CLR_functions;
		m_CLR_functions = nullptr;
		delete m_CLR_host;
		m_CLR_host = nullptr;
		// 注意：CoreCLR 运行时本身随进程结束卸载，不做显式卸载以保证稳定性
		spdlog::info("[clr] CoreCLR 已关闭");
	}

	bool AppFrame::CLRCallbackFrameFunc() noexcept
	{
		if (!m_CLR_active || m_CLR_functions->FrameFunc == nullptr)
			return true;
		return m_CLR_functions->FrameFunc() == 0;
	}

	void AppFrame::CLRCallbackRenderFunc() noexcept
	{
		if (m_CLR_active && m_CLR_functions->RenderFunc != nullptr)
			m_CLR_functions->RenderFunc();
	}

	void AppFrame::CLRCallbackFocusLoseFunc() noexcept
	{
		if (m_CLR_active && m_CLR_functions->FocusLoseFunc != nullptr)
			m_CLR_functions->FocusLoseFunc();
	}

	void AppFrame::CLRCallbackFocusGainFunc() noexcept
	{
		if (m_CLR_active && m_CLR_functions->FocusGainFunc != nullptr)
			m_CLR_functions->FocusGainFunc();
	}

	void AppFrame::CLRCallbackEventFunc(uint8_t const event_type, uint8_t const state) noexcept
	{
		if (m_CLR_active && m_CLR_functions->EventFunc != nullptr)
			m_CLR_functions->EventFunc(event_type, state);
	}
}
