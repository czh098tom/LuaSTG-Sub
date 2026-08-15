#include "CLRBinding/CLRHost.hpp"

#ifndef NOMINMAX
	#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
	#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <cassert>
#include <spdlog/spdlog.h>
#include <nethost.h>

namespace luastg
{
	namespace
	{
		void* loadLibrary(const char_t* path)
		{
			HMODULE const h = ::LoadLibraryW(path);
			if (h == nullptr)
			{
				spdlog::error("[clr] 加载动态库失败：{}，GetLastError = {}", reinterpret_cast<const char*>(path), ::GetLastError());
				return nullptr;
			}
			return static_cast<void*>(h);
		}
		void* getExport(void* h, const char* name)
		{
			void* const f = static_cast<void*>(::GetProcAddress(static_cast<HMODULE>(h), name));
			if (f == nullptr)
			{
				spdlog::error("[clr] 无法找到导出符号：{}", name);
				return nullptr;
			}
			return f;
		}
	}

	bool CLRHost::initHostfxr()
	{
		// 获取 hostfxr 库的路径
		char_t buffer[MAX_PATH];
		size_t buffer_size = sizeof(buffer) / sizeof(char_t);
		int const rc = ::get_hostfxr_path(buffer, &buffer_size, nullptr);
		if (rc != 0)
		{
			spdlog::error("[clr] 未找到 hostfxr（错误码 0x{:08x}），请确认已安装 .NET 运行时", rc);
			return false;
		}

		// 加载 hostfxr 并获取需要的导出函数
		void* const lib = loadLibrary(buffer);
		if (lib == nullptr)
		{
			return false;
		}
		_init_runtime = reinterpret_cast<hostfxr_initialize_for_runtime_config_fn>(getExport(lib, "hostfxr_initialize_for_runtime_config"));
		_get_delegate = reinterpret_cast<hostfxr_get_runtime_delegate_fn>(getExport(lib, "hostfxr_get_runtime_delegate"));
		_close_context = reinterpret_cast<hostfxr_close_fn>(getExport(lib, "hostfxr_close"));
		return _init_runtime && _get_delegate && _close_context;
	}

	bool CLRHost::initializeRuntime(const char_t* const runtime_config_path)
	{
		hostfxr_handle context = nullptr;
		int rc = _init_runtime(runtime_config_path, nullptr, &context);
		if (rc != 0 || context == nullptr)
		{
			spdlog::error("[clr] 初始化 CoreCLR 运行时失败（0x{:08x}）：{}", rc, reinterpret_cast<const char*>(runtime_config_path));
			if (context != nullptr)
			{
				_close_context(context);
			}
			return false;
		}

		// 获取 load_assembly_and_get_function_pointer 委托，其余所有托管程序集都可以由
		// 托管侧入口自己加载
		void* load_assembly_and_get_function_pointer = nullptr;
		rc = _get_delegate(context, hdt_load_assembly_and_get_function_pointer, &load_assembly_and_get_function_pointer);
		if (rc != 0 || load_assembly_and_get_function_pointer == nullptr)
		{
			spdlog::error("[clr] 获取 load_assembly_and_get_function_pointer 委托失败（0x{:08x}）", rc);
			_close_context(context);
			return false;
		}

		_close_context(context);

		_load_assembly_and_get_function_pointer = reinterpret_cast<load_assembly_and_get_function_pointer_fn>(load_assembly_and_get_function_pointer);
		return true;
	}

	bool CLRHost::init(const char_t* const runtime_config_path)
	{
		return initHostfxr() && initializeRuntime(runtime_config_path);
	}

	int CLRHost::loadAssemblyAndGetFunctionPointer(
		const char_t* const assembly_path,
		const char_t* const type_name,
		const char_t* const method_name,
		const char_t* const delegate_type_name,
		/*out*/ void** const delegate
	) const
	{
		assert(_load_assembly_and_get_function_pointer != nullptr);
		return _load_assembly_and_get_function_pointer(
			assembly_path,
			type_name,
			method_name,
			delegate_type_name,
			nullptr,
			delegate
		);
	}
}
