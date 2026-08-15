#pragma once
#include <coreclr_delegates.h>
#include <hostfxr.h>

namespace luastg
{
	/// @brief .NET CoreCLR 运行时宿主（hostfxr 模式）
	/// @note 负责加载 hostfxr 并初始化 CoreCLR 运行时，获取程序集加载相关委托
	class CLRHost
	{
	private:
		hostfxr_initialize_for_runtime_config_fn _init_runtime = nullptr;
		hostfxr_get_runtime_delegate_fn _get_delegate = nullptr;
		hostfxr_close_fn _close_context = nullptr;

		load_assembly_and_get_function_pointer_fn _load_assembly_and_get_function_pointer = nullptr;

		bool initHostfxr();
		bool initializeRuntime(const char_t* runtime_config_path);

	public:
		CLRHost() = default;
		CLRHost(const CLRHost&) = delete;
		CLRHost& operator=(const CLRHost&) = delete;

		/// @brief 初始化 CoreCLR 运行时
		/// @param runtime_config_path 运行时配置文件（runtimeconfig.json）路径
		bool init(const char_t* runtime_config_path);

		/// @brief 加载程序集并获取非托管可调用函数指针
		int loadAssemblyAndGetFunctionPointer(
			const char_t* assembly_path,
			const char_t* type_name,
			const char_t* method_name,
			const char_t* delegate_type_name,
			/*out*/ void** delegate
		) const;
	};
}
