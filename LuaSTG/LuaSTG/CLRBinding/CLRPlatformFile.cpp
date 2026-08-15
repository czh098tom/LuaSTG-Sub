// LuaSTG CoreCLR 绑定：平台与文件系统模块实现
// 对应 Lua 绑定：
//   LuaBinding/LW_Platform.cpp    （lstg.Platform）
//   LuaBinding/LW_FileManager.cpp （lstg 兼容 API / lstg.FileManager / lstg.FontRenderer）
//   LuaBinding/LW_Archive.cpp     （lstgArchive userdata）
// 移植时保留引擎调用逻辑，Lua 栈操作（luaL_check/luaL_error）改为空指针校验与错误码返回，
// Lua 侧的 table 返回值改为“枚举缓存 + byIndex 读取”两段式。

#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"
#include "ApplicationRestart.hpp"
#include "core/FileSystem.hpp"
#include "utility/path.hpp"
#include "GameResource/ResourcePassword.hpp"
#include "windows/KnownDirectory.hpp"
#include "utf8.hpp"

#include <cstring>
#include <filesystem>
#include <new>

// 与 LW_Platform.cpp 一致：在引擎头之后、Windows.h 之前定义
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>

using namespace luastg;

namespace
{
	// ===== 资源包句柄（对应 LW_Archive.cpp 的 userdata Wrapper）=====

	struct ArchiveHandle
	{
		core::IFileSystemArchive* data{}; // 持有引用（已 retain）
	};

	ArchiveHandle* asArchive(uintptr_t const handle) noexcept
	{
		return reinterpret_cast<ArchiveHandle*>(handle);
	}

	uintptr_t createArchiveHandle(core::IFileSystemArchive* const archive) noexcept
	{
		if (archive == nullptr)
			return 0;
		auto* const handle = new (std::nothrow) ArchiveHandle{};
		if (handle == nullptr)
			return 0;
		handle->data = archive;
		handle->data->retain();
		return reinterpret_cast<uintptr_t>(handle);
	}

	// ===== 枚举缓存（EnumFiles/FindFiles/EnumArchives/Archive 枚举共用）=====

	struct EnumEntry
	{
		std::string name;     // 条目名（系统目录条目为目录时带尾部 '/'）
		std::string archive;  // 所属资源包路径，系统文件为空
		bool is_directory;
	};

	std::vector<EnumEntry> g_enum_cache;

	// 返回给 C# 的字符串使用线程局部缓冲（C# 侧在调用返回后立即拷贝）
	thread_local std::string g_enum_name_buffer;
	thread_local std::string g_enum_archive_buffer;
	thread_local std::string g_platform_path_buffer;
	thread_local std::string g_current_directory_buffer;
	thread_local std::string g_archive_name_buffer;

	// ===== RestartWithCommandLineArguments 的参数中转 =====

	std::vector<std::string> g_restart_arguments;

	// ===== EnumFiles/FindFiles 的配置与枚举（对应 LW_FileManager.cpp 的 _EnumFilesConfig 等）=====

	struct EnumFilesConfig
	{
		std::string searchpath;
		std::string searchpath2;
		size_t headlen = 0;
		std::string extpath;
		std::string packname;
		bool checkext = false;
		bool checkpack = false;
		bool findfiles = false;
	};

	EnumFilesConfig makeEnumFilesConfig(
		char const* const path, char const* const ext,
		char const* const pack_name, bool const find_files_mode)
	{
		EnumFilesConfig cfg;
		cfg.findfiles = find_files_mode;

		cfg.searchpath.assign(path);
		utility::path::to_slash(cfg.searchpath);
		if (!cfg.searchpath.empty() && cfg.searchpath.back() != '/') {
			cfg.searchpath.push_back('/');
		}
		else if (cfg.searchpath.empty()) {
			cfg.searchpath.push_back('.');
			cfg.headlen = 2;
		}

		cfg.searchpath2.assign(path);
		utility::path::to_slash(cfg.searchpath2);
		if (!cfg.searchpath2.empty() && cfg.searchpath2.back() != '/') {
			cfg.searchpath2.push_back('/');
		}
		else if (cfg.searchpath2 == "." || cfg.searchpath2 == "./") {
			cfg.searchpath2 = "";
		}

		if (ext != nullptr && ext[0] != '\0') {
			cfg.extpath.push_back('.');
			cfg.extpath.append(ext);
			cfg.checkext = true;
		}

		if (find_files_mode && pack_name != nullptr && pack_name[0] != '\0') {
			cfg.packname.append(pack_name);
			cfg.checkpack = true;
		}

		return cfg;
	}

	/// @brief 枚举系统目录（对应 _EnumFilesSystem，结果追加到缓存）
	void enumSystemIntoCache(EnumFilesConfig const& cfg)
	{
		std::wstring const wextpath = utf8::to_wstring(cfg.extpath);
		std::error_code ec;
		for (auto& p : std::filesystem::directory_iterator(utf8::to_wstring(cfg.searchpath), ec)) {
			bool const is_dir = p.is_directory();
			if ((cfg.checkext || cfg.findfiles) && is_dir) {
				continue; // 需要检查拓展名，那就不可能是文件夹了，或者为 FindFiles 模式（忽略文件夹）
			}
			if (p.is_regular_file() || is_dir) {
				if (cfg.checkext && p.path().extension().wstring() != wextpath) {
					continue;
				}
				std::string u8path = utf8::to_string(p.path().generic_wstring());
				if (cfg.headlen != 0)
					u8path = u8path.substr(cfg.headlen);
				if (is_dir)
					u8path.push_back('/');
				g_enum_cache.push_back(EnumEntry{ std::move(u8path), std::string(), is_dir });
			}
		}
	}

	/// @brief 枚举所有资源包（对应 _EnumFilesArchive，结果追加到缓存）
	void enumArchivesIntoCache(EnumFilesConfig const& cfg)
	{
		core::SmartReference<core::IFileSystemFileSystemEnumerator> enumerator;
		if (!core::FileSystemManager::createFileSystemEnumerator(enumerator.put())) {
			return;
		}

		core::SmartReference<core::IFileSystem> file_system;
		while (enumerator->next(file_system.put())) {
			core::SmartReference<core::IFileSystemArchive> archive;
			if (!file_system->queryInterface(archive.put())) {
				continue; // 必须是压缩包
			}

			if (cfg.checkpack && archive->getArchivePath() != cfg.packname) {
				continue; // 需要匹配压缩包名
			}

			core::SmartReference<core::IFileSystemEnumerator> e;
			if (!archive->createEnumerator(e.put(), cfg.searchpath2, false)) {
				continue;
			}

			while (e->next()) {
				if (cfg.findfiles && e->getNodeType() != core::FileSystemNodeType::file) {
					continue; // 需要检查拓展名，那就不可能是文件夹了，或者为 FindFiles 模式（仅限文件）
				}
				if (cfg.checkext && !e->getName().ends_with(cfg.extpath)) {
					continue; // 拓展名不匹配
				}

				g_enum_cache.push_back(EnumEntry{
					std::string(e->getName()),
					std::string(archive->getArchivePath()),
					e->getNodeType() == core::FileSystemNodeType::directory,
				});
			}
		}
	}
}

namespace luastg
{
	// ============ 平台（lstg.Platform）============

	const char* CLRBinding::platform_getLocalAppDataPath()
	{
		g_platform_path_buffer.clear();
		try
		{
			std::string path;
			if (::Platform::KnownDirectory::getLocalAppData(path)) {
				g_platform_path_buffer = std::move(path);
			}
		}
		catch (std::bad_alloc const&)
		{
		}
		return g_platform_path_buffer.c_str(); // 失败返回空串（与 Lua 侧一致）
	}

	const char* CLRBinding::platform_getRoamingAppDataPath()
	{
		g_platform_path_buffer.clear();
		try
		{
			std::string path;
			if (::Platform::KnownDirectory::getRoamingAppData(path)) {
				g_platform_path_buffer = std::move(path);
			}
		}
		catch (std::bad_alloc const&)
		{
		}
		return g_platform_path_buffer.c_str();
	}

	void CLRBinding::platform_restartSetArgumentCount(uint32_t const count)
	{
		g_restart_arguments.assign(static_cast<size_t>(count), std::string{});
	}

	void CLRBinding::platform_restartSetArgument(uint32_t const index, const char* const value)
	{
		if (value == nullptr)
			return;
		if (index < g_restart_arguments.size())
			g_restart_arguments[index].assign(value);
	}

	void CLRBinding::platform_restartCommitArguments()
	{
		ApplicationRestart::enableWithCommandLineArguments(g_restart_arguments);
		g_restart_arguments.clear();
	}

	int32_t CLRBinding::platform_messageBox(const char* const title, const char* const text, uint32_t const flags)
	{
		if (title == nullptr || text == nullptr)
			return 0;
		HWND hwnd = nullptr;
		if (auto* const window = LAPP.getWindow(); window != nullptr) {
			hwnd = static_cast<HWND>(window->getNativeHandle());
		}
		return static_cast<int32_t>(MessageBoxW(
			hwnd,
			utf8::to_wstring(std::string_view(text)).c_str(),
			utf8::to_wstring(std::string_view(title)).c_str(),
			static_cast<UINT>(flags)));
	}

	// ============ 兼容 API（lstg 顶层）============

	uintptr_t CLRBinding::fileManager_loadArchive(const char* const path, const char* const password, uint8_t const has_password)
	{
		if (path == nullptr)
			return 0;
		core::SmartReference<core::IFileSystemArchive> archive;
		if (!core::IFileSystemArchive::createFromFile(std::string_view(path), archive.put())) {
			spdlog::error("[luastg] 无法加载资源包'{}'，文件不存在或不是合法的资源包格式（zip）", path);
			return 0;
		}

		// 第二个参数位曾经支持设置优先级，现在已失效（Lua 侧注释）

		if (has_password != 0 && password != nullptr) {
			archive->setPassword(std::string_view(password));
		}

		core::FileSystemManager::addFileSystem(std::string_view(path), archive.get());

		return createArchiveHandle(archive.get());
	}

	uintptr_t CLRBinding::fileManager_loadPackSub(const char* const path)
	{
		if (path == nullptr)
			return 0;
		core::SmartReference<core::IFileSystemArchive> archive;
		if (!core::IFileSystemArchive::createFromFile(std::string_view(path), archive.put())) {
			spdlog::error("[luastg] 无法加载资源包'{}'，文件不存在或不是合法的资源包格式（zip）", path);
			return 0;
		}
		archive->setPassword(GetGameName());

		core::FileSystemManager::addFileSystem(std::string_view(path), archive.get());

		return createArchiveHandle(archive.get());
	}

	uint8_t CLRBinding::fileManager_unloadArchive(const char* const name)
	{
		if (name == nullptr)
			return 0;
		core::SmartReference<core::IFileSystemArchive> archive;
		if (core::FileSystemManager::getFileSystemArchiveByPath(std::string_view(name), archive.put())) {
			core::FileSystemManager::removeFileSystem(archive.get());
			return 1;
		}
		return 0;
	}

	uint8_t CLRBinding::fileManager_extractRes(const char* const path, const char* const target)
	{
		if (path == nullptr || target == nullptr)
			return 0;
		core::SmartReference<core::IData> src;
		if (!core::FileSystemManager::readFile(std::string_view(path), src.put())) {
			spdlog::error("[luastg] ExtractRes: 无法读取文件'{}'", path);
			return 0;
		}
		if (!core::FileSystemManager::writeFile(std::string_view(target), src.get())) {
			spdlog::error("[luastg] ExtractRes: 无法写入文件'{}'", target);
			return 0;
		}
		return 1;
	}

	uint32_t CLRBinding::fileManager_findFiles(const char* const path, const char* const ext, const char* const pack_name)
	{
		g_enum_cache.clear();
		if (path == nullptr)
			return 0;

		EnumFilesConfig const cfg = makeEnumFilesConfig(path, ext, pack_name, true);

		enumArchivesIntoCache(cfg);

		if (!cfg.checkpack) {
			enumSystemIntoCache(cfg);
		}

		return static_cast<uint32_t>(g_enum_cache.size());
	}

	// ============ lstg.FileManager ============

	void CLRBinding::fileManager_unloadAllArchive()
	{
		core::FileSystemManager::removeAllFileSystem();
	}

	uint8_t CLRBinding::fileManager_archiveExist(const char* const name)
	{
		if (name == nullptr)
			return 0;
		core::SmartReference<core::IFileSystemArchive> archive;
		return core::FileSystemManager::getFileSystemArchiveByPath(std::string_view(name), archive.put()) ? 1 : 0;
	}

	uintptr_t CLRBinding::fileManager_getArchive(const char* const name)
	{
		if (name == nullptr)
			return 0;
		core::SmartReference<core::IFileSystemArchive> archive;
		if (!core::FileSystemManager::getFileSystemArchiveByPath(std::string_view(name), archive.put()))
			return 0;
		return createArchiveHandle(archive.get());
	}

	uint32_t CLRBinding::fileManager_enumArchives()
	{
		g_enum_cache.clear();

		core::SmartReference<core::IFileSystemFileSystemEnumerator> enumerator;
		if (!core::FileSystemManager::createFileSystemEnumerator(enumerator.put())) {
			return 0;
		}

		core::SmartReference<core::IFileSystem> file_system;
		while (enumerator->next(file_system.put())) {
			core::SmartReference<core::IFileSystemArchive> archive;
			if (!file_system->queryInterface(archive.put())) {
				continue;
			}
			// Lua 侧条目为 {资源包路径, 优先级}，优先级恒为 0，此处仅缓存路径
			g_enum_cache.push_back(EnumEntry{ std::string(archive->getArchivePath()), std::string(), false });
		}

		return static_cast<uint32_t>(g_enum_cache.size());
	}

	uint32_t CLRBinding::fileManager_enumFiles(const char* const path, const char* const ext, uint8_t const include_archives)
	{
		g_enum_cache.clear();
		if (path == nullptr)
			return 0;

		EnumFilesConfig const cfg = makeEnumFilesConfig(path, ext, nullptr, false);

		if (include_archives != 0) {
			enumArchivesIntoCache(cfg);
		}

		enumSystemIntoCache(cfg);

		return static_cast<uint32_t>(g_enum_cache.size());
	}

	uint8_t CLRBinding::fileManager_fileExist(const char* const path, uint8_t const all)
	{
		if (path == nullptr)
			return 0;
		if (all != 0) {
			return core::FileSystemManager::hasFile(std::string_view(path)) ? 1 : 0;
		}
		return core::IFileSystemOS::getInstance()->hasFile(std::string_view(path)) ? 1 : 0;
	}

	void CLRBinding::fileManager_addSearchPath(const char* const path)
	{
		if (path == nullptr)
			return;
		core::FileSystemManager::addSearchPath(std::string_view(path));
	}

	void CLRBinding::fileManager_removeSearchPath(const char* const path)
	{
		if (path == nullptr)
			return;
		core::FileSystemManager::removeSearchPath(std::string_view(path));
	}

	void CLRBinding::fileManager_clearSearchPath()
	{
		core::FileSystemManager::removeAllSearchPath();
	}

	uint8_t CLRBinding::fileManager_setCurrentDirectory(const char* const path)
	{
		if (path == nullptr)
			return 0;
		std::error_code ec;
		std::filesystem::current_path(utf8::to_wstring(std::string_view(path)), ec);
		return ec ? 0 : 1;
	}

	const char* CLRBinding::fileManager_getCurrentDirectory()
	{
		g_current_directory_buffer.clear();
		std::error_code ec;
		std::filesystem::path const path = std::filesystem::current_path(ec);
		if (ec) {
			return g_current_directory_buffer.c_str(); // 失败返回空串（Lua 侧返回 nil+错误消息）
		}
		std::string str = utf8::to_string(path.wstring());
		utility::path::to_slash(str);
		g_current_directory_buffer = std::move(str);
		return g_current_directory_buffer.c_str();
	}

	uint8_t CLRBinding::fileManager_createDirectory(const char* const path)
	{
		if (path == nullptr)
			return 0;
		std::error_code ec;
		bool const result = std::filesystem::create_directories(utf8::to_wstring(std::string_view(path)), ec);
		return result ? 1 : 0;
	}

	uint8_t CLRBinding::fileManager_removeDirectory(const char* const path)
	{
		if (path == nullptr)
			return 0;
		std::error_code ec;
		std::uintmax_t const result = std::filesystem::remove_all(utf8::to_wstring(std::string_view(path)), ec);
		return result != static_cast<std::uintmax_t>(-1) ? 1 : 0;
	}

	uint8_t CLRBinding::fileManager_directoryExist(const char* const path, uint8_t const all)
	{
		if (path == nullptr)
			return 0;
		if (path[0] == '\0') {
			return 1; // 相对路径 "" 永远是存在的
		}
		if (all != 0) {
			return core::FileSystemManager::hasDirectory(std::string_view(path)) ? 1 : 0;
		}
		return core::IFileSystemOS::getInstance()->hasDirectory(std::string_view(path)) ? 1 : 0;
	}

	// ============ 枚举缓存读取 ============

	const char* CLRBinding::fileSystem_enumGetEntryName(uint32_t const index, uint8_t* const out_is_directory)
	{
		if (out_is_directory != nullptr)
			*out_is_directory = 0;
		if (index >= g_enum_cache.size())
			return "";
		auto const& entry = g_enum_cache[index];
		if (out_is_directory != nullptr)
			*out_is_directory = entry.is_directory ? 1 : 0;
		g_enum_name_buffer = entry.name;
		return g_enum_name_buffer.c_str();
	}

	const char* CLRBinding::fileSystem_enumGetEntryArchive(uint32_t const index)
	{
		if (index >= g_enum_cache.size())
			return "";
		g_enum_archive_buffer = g_enum_cache[index].archive;
		return g_enum_archive_buffer.c_str();
	}

	// ============ 资源包对象（lstgArchive）============

	uint8_t CLRBinding::archive_isValid(uintptr_t const handle)
	{
		auto const self = asArchive(handle);
		if (self == nullptr)
			return 0;
		return self->data != nullptr ? 1 : 0;
	}

	uint32_t CLRBinding::archive_enumFiles(uintptr_t const handle, const char* const directory, uint8_t const recursive)
	{
		g_enum_cache.clear();
		auto const self = asArchive(handle);
		if (self == nullptr || self->data == nullptr)
			return 0;

		core::SmartReference<core::IFileSystemEnumerator> enumerator;
		if (!self->data->createEnumerator(
			enumerator.put(),
			directory != nullptr ? std::string_view(directory) : std::string_view(),
			recursive != 0)) {
			return 0;
		}

		while (enumerator->next()) {
			g_enum_cache.push_back(EnumEntry{
				std::string(enumerator->getName()),
				std::string(),
				enumerator->getNodeType() == core::FileSystemNodeType::directory,
			});
		}

		return static_cast<uint32_t>(g_enum_cache.size());
	}

	uint8_t CLRBinding::archive_fileExist(uintptr_t const handle, const char* const path)
	{
		auto const self = asArchive(handle);
		if (self == nullptr || self->data == nullptr || path == nullptr)
			return 0;
		return self->data->hasFile(std::string_view(path)) ? 1 : 0;
	}

	const char* CLRBinding::archive_getName(uintptr_t const handle)
	{
		auto const self = asArchive(handle);
		if (self == nullptr || self->data == nullptr)
			return "";
		g_archive_name_buffer.assign(self->data->getArchivePath());
		return g_archive_name_buffer.c_str();
	}

	int32_t CLRBinding::archive_getPriority(uintptr_t const handle)
	{
		// 引擎当前未实现资源包优先级，与 Lua 侧一致恒为 0
		std::ignore = handle;
		return 0;
	}

	void CLRBinding::archive_setPriority(uintptr_t const handle, int32_t const priority)
	{
		// 引擎当前未实现资源包优先级，与 Lua 侧一致为空操作
		std::ignore = handle;
		std::ignore = priority;
	}

	void CLRBinding::archive_destroy(uintptr_t const handle)
	{
		auto const self = asArchive(handle);
		if (self == nullptr)
			return;
		if (self->data != nullptr) {
			self->data->release();
			self->data = nullptr;
		}
		delete self;
	}

	// ============ 字体渲染器（lstg.FontRenderer）============

	uint8_t CLRBinding::fontRenderer_setFontProvider(const char* const name)
	{
		if (name == nullptr)
			return 0;
		return LAPP.FontRenderer_SetFontProvider(name) ? 1 : 0;
	}

	void CLRBinding::fontRenderer_setScale(float const x, float const y)
	{
		LAPP.FontRenderer_SetScale(core::Vector2F(x, y));
	}

	void CLRBinding::fontRenderer_measureTextBoundary(
		const char* const text,
		double* const out_left, double* const out_right, double* const out_bottom, double* const out_top)
	{
		if (out_left != nullptr) *out_left = 0.0;
		if (out_right != nullptr) *out_right = 0.0;
		if (out_bottom != nullptr) *out_bottom = 0.0;
		if (out_top != nullptr) *out_top = 0.0;
		if (text == nullptr)
			return;
		auto const len = std::strlen(text);
		core::RectF const v = LAPP.FontRenderer_MeasureTextBoundary(text, len);
		if (out_left != nullptr) *out_left = v.a.x;
		if (out_right != nullptr) *out_right = v.b.x;
		if (out_bottom != nullptr) *out_bottom = v.b.y;
		if (out_top != nullptr) *out_top = v.a.y;
	}

	void CLRBinding::fontRenderer_measureTextAdvance(const char* const text, double* const out_x, double* const out_y)
	{
		if (out_x != nullptr) *out_x = 0.0;
		if (out_y != nullptr) *out_y = 0.0;
		if (text == nullptr)
			return;
		auto const len = std::strlen(text);
		core::Vector2F const v = LAPP.FontRenderer_MeasureTextAdvance(text, len);
		if (out_x != nullptr) *out_x = v.x;
		if (out_y != nullptr) *out_y = v.y;
	}

	uint8_t CLRBinding::fontRenderer_renderText(
		const char* const text, float const x, float const y, float const z,
		uint8_t const blend, uint32_t const argb,
		double* const out_x, double* const out_y)
	{
		if (out_x != nullptr) *out_x = x;
		if (out_y != nullptr) *out_y = y;
		if (text == nullptr)
			return 0;
		auto const len = std::strlen(text);
		core::Vector2F pos(x, y);
		bool const ret = LAPP.FontRenderer_RenderText(
			text, len,
			pos, z,
			static_cast<BlendMode>(blend),
			core::Color4B(argb));
		if (out_x != nullptr) *out_x = pos.x;
		if (out_y != nullptr) *out_y = pos.y;
		return ret ? 1 : 0;
	}

	uint8_t CLRBinding::fontRenderer_renderTextInSpace(
		const char* const text,
		float const x, float const y, float const z,
		float const rx, float const ry, float const rz,
		float const dx, float const dy, float const dz,
		uint8_t const blend, uint32_t const argb,
		double* const out_x, double* const out_y, double* const out_z)
	{
		if (out_x != nullptr) *out_x = x;
		if (out_y != nullptr) *out_y = y;
		if (out_z != nullptr) *out_z = z;
		if (text == nullptr)
			return 0;
		auto const len = std::strlen(text);
		core::Vector3F pos(x, y, z);
		core::Vector3F const rvec(rx, ry, rz);
		core::Vector3F const dvec(dx, dy, dz);
		bool const ret = LAPP.FontRenderer_RenderTextInSpace(
			text, len,
			pos, rvec, dvec,
			static_cast<BlendMode>(blend),
			core::Color4B(argb));
		if (out_x != nullptr) *out_x = pos.x;
		if (out_y != nullptr) *out_y = pos.y;
		if (out_z != nullptr) *out_z = pos.z;
		return ret ? 1 : 0;
	}

	float CLRBinding::fontRenderer_getFontLineHeight()
	{
		return LAPP.FontRenderer_GetFontLineHeight();
	}

	float CLRBinding::fontRenderer_getFontAscender()
	{
		return LAPP.FontRenderer_GetFontAscender();
	}

	float CLRBinding::fontRenderer_getFontDescender()
	{
		return LAPP.FontRenderer_GetFontDescender();
	}
}
