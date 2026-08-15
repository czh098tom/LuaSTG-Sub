// LuaSTG CoreCLR 绑定：平台与文件系统（对应 LW_Platform/LW_FileManager/LW_Archive/FontRenderer）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 约定：
// - 返回的 const char* 指向引擎侧线程局部缓冲，C# 侧必须在下一次调用前拷贝
// - 布尔返回值/参数用 uint8_t（0/1）
// - 资源包（Archive）对象为堆上句柄：uintptr_t 指向持有 core::SmartReference 的 C 结构，
//   由 fileManager_loadArchive / fileManager_getArchive 分配（retain），archive_destroy 销毁（release）
// - 枚举类 API（EnumFiles/FindFiles/EnumArchives/Archive.EnumFiles/Archive.ListFiles）
//   采用“count + byIndex”两段式：先调用枚举函数填充引擎侧缓存并返回数量，
//   再通过 fileSystem_enumGetEntryName / fileSystem_enumGetEntryArchive 按索引读取

// ===== 平台（LW_Platform.cpp -> lstg.Platform）=====

// 本地 AppData 目录（失败返回空串，与 Lua 侧一致）
DECLARE_CLR_API(const char*, platform_getLocalAppDataPath, ())
// 漫游 AppData 目录（失败返回空串）
DECLARE_CLR_API(const char*, platform_getRoamingAppDataPath, ())
// RestartWithCommandLineArguments 的参数列表分三步传递（C# 侧封装为单次调用）：
// 1. 设置参数个数（清空并预留） 2. 按索引填充 3. 提交并启用重启
DECLARE_CLR_API(void, platform_restartSetArgumentCount, (uint32_t count))
DECLARE_CLR_API(void, platform_restartSetArgument, (uint32_t index, const char* value))
DECLARE_CLR_API(void, platform_restartCommitArguments, ())
// 消息框：flags 为 Win32 MessageBox 标志位（MB_* 组合，数值与 Lua 侧一致），
// 返回值为按钮 ID（IDOK/IDCANCEL/...，见 C# 侧 MessageBoxResult）
DECLARE_CLR_API(int32_t, platform_messageBox, (const char* title, const char* text, uint32_t flags))
// 注：Execute（LUASTG_ENABLE_EXECUTE_API）与 os.execute/io.popen 一样在当前构建配置中
// 未启用（Custom/Config.h 中被注释），故不移植

// ===== 兼容 API（LW_FileManager.cpp 注册到 lstg 顶层）=====

// 加载资源包（LoadPack / LoadArchive）。password 为 nullptr 或 has_password=0 时不设置密码，
// 空串密码与 Lua 侧一致（视为设置空密码）。失败返回 0
DECLARE_CLR_API(uintptr_t, fileManager_loadArchive, (const char* path, const char* password, uint8_t has_password))
// 加载带游戏名密码的资源包（LoadPackSub），失败返回 0
DECLARE_CLR_API(uintptr_t, fileManager_loadPackSub, (const char* path))
// 卸载资源包（UnloadPack / UnloadArchive），返回是否找到并卸载
DECLARE_CLR_API(uint8_t, fileManager_unloadArchive, (const char* name))
// 释放资源内文件到磁盘（ExtractRes；USING_ENCRYPTION 未启用时 Lua 侧注册此 API）
DECLARE_CLR_API(uint8_t, fileManager_extractRes, (const char* path, const char* target))
// 按扩展名/资源包查找文件（FindFiles），填充枚举缓存并返回条目数
DECLARE_CLR_API(uint32_t, fileManager_findFiles, (const char* path, const char* ext, const char* pack_name))

// ===== lstg.FileManager =====

DECLARE_CLR_API(void, fileManager_unloadAllArchive, ())
// 资源包是否已加载
DECLARE_CLR_API(uint8_t, fileManager_archiveExist, (const char* name))
// 按路径取已加载资源包的句柄（未加载返回 0）
DECLARE_CLR_API(uintptr_t, fileManager_getArchive, (const char* name))
// 枚举所有已加载资源包（Lua 侧条目为 {路径, 优先级=0}，优先级恒为 0，故仅返回路径），返回数量
DECLARE_CLR_API(uint32_t, fileManager_enumArchives, ())
// 枚举文件（EnumFiles）：include_archives 非 0 时先枚举资源包再枚举系统目录（与 Lua 侧第 3 参数一致），
// ext 为空串时不检查扩展名。填充枚举缓存并返回条目数
DECLARE_CLR_API(uint32_t, fileManager_enumFiles, (const char* path, const char* ext, uint8_t include_archives))
// 文件是否存在：all=1 检查文件系统+资源包，all=0 仅检查系统文件
DECLARE_CLR_API(uint8_t, fileManager_fileExist, (const char* path, uint8_t all))
// 搜索路径
DECLARE_CLR_API(void, fileManager_addSearchPath, (const char* path))
DECLARE_CLR_API(void, fileManager_removeSearchPath, (const char* path))
DECLARE_CLR_API(void, fileManager_clearSearchPath, ())
// 工作目录（Lua 侧失败时附带错误消息与错误码，此处仅返回是否成功）
DECLARE_CLR_API(uint8_t, fileManager_setCurrentDirectory, (const char* path))
DECLARE_CLR_API(const char*, fileManager_getCurrentDirectory, ())
// 目录操作（Lua 侧失败时附带错误消息与错误码，此处仅返回结果）
DECLARE_CLR_API(uint8_t, fileManager_createDirectory, (const char* path))
DECLARE_CLR_API(uint8_t, fileManager_removeDirectory, (const char* path))
// 目录是否存在：all=1 检查文件系统+资源包，all=0 仅检查系统目录；空路径视为存在
DECLARE_CLR_API(uint8_t, fileManager_directoryExist, (const char* path, uint8_t all))

// ===== 枚举缓存读取（EnumFiles/FindFiles/EnumArchives/Archive 枚举共用）=====

// 按索引取枚举条目名（系统目录条目带尾部 '/' 的为目录），越界返回空串
DECLARE_CLR_API(const char*, fileSystem_enumGetEntryName, (uint32_t index, uint8_t* out_is_directory))
// 按索引取枚举条目所属资源包路径（系统文件为空串），越界返回空串
DECLARE_CLR_API(const char*, fileSystem_enumGetEntryArchive, (uint32_t index))

// ===== 资源包对象（LW_Archive.cpp -> lstgArchive userdata）=====

// 句柄是否有效（内部资源包指针非空）
DECLARE_CLR_API(uint8_t, archive_isValid, (uintptr_t handle))
// 枚举资源包内条目（EnumFiles/ListFiles：recursive=0 非递归 / 1 递归），填充枚举缓存并返回数量
DECLARE_CLR_API(uint32_t, archive_enumFiles, (uintptr_t handle, const char* directory, uint8_t recursive))
// 资源包内是否存在文件
DECLARE_CLR_API(uint8_t, archive_fileExist, (uintptr_t handle, const char* path))
// 资源包路径（无效句柄返回空串）
DECLARE_CLR_API(const char*, archive_getName, (uintptr_t handle))
// 优先级：引擎当前未实现，与 Lua 侧一致恒为 0 / 空操作
DECLARE_CLR_API(int32_t, archive_getPriority, (uintptr_t handle))
DECLARE_CLR_API(void, archive_setPriority, (uintptr_t handle, int32_t priority))
// 销毁句柄（对应 Lua 侧 userdata 的 __gc，release 引用后释放）
DECLARE_CLR_API(void, archive_destroy, (uintptr_t handle))

// ===== 字体渲染器（LW_FileManager.cpp 注册的 lstg.FontRenderer，实现在 AppFrameFontRenderer.cpp）=====

// 设置字体提供者（字体资源名），返回是否成功
DECLARE_CLR_API(uint8_t, fontRenderer_setFontProvider, (const char* name))
// 设置缩放
DECLARE_CLR_API(void, fontRenderer_setScale, (float x, float y))
// 测量文本边界（Lua 侧返回 v.a.x/v.b.x/v.b.y/v.a.y 四个值）
DECLARE_CLR_API(void, fontRenderer_measureTextBoundary, (const char* text, double* out_left, double* out_right, double* out_bottom, double* out_top))
// 测量文本步进
DECLARE_CLR_API(void, fontRenderer_measureTextAdvance, (const char* text, double* out_x, double* out_y))
// 渲染文本（blend 为 luastg::BlendMode 枚举值，color 为 0xAARRGGBB），
// out_x/out_y 返回渲染结束位置（对应 Lua 侧第 2、3 个返回值）
DECLARE_CLR_API(uint8_t, fontRenderer_renderText, (const char* text, float x, float y, float z, uint8_t blend, uint32_t argb, double* out_x, double* out_y))
// 在三维空间中渲染文本（rvec 为右向量，dvec 为下向量），out_x/out_y/out_z 返回渲染结束位置
DECLARE_CLR_API(uint8_t, fontRenderer_renderTextInSpace, (const char* text, float x, float y, float z, float rx, float ry, float rz, float dx, float dy, float dz, uint8_t blend, uint32_t argb, double* out_x, double* out_y, double* out_z))
DECLARE_CLR_API(float, fontRenderer_getFontLineHeight, ())
DECLARE_CLR_API(float, fontRenderer_getFontAscender, ())
DECLARE_CLR_API(float, fontRenderer_getFontDescender, ())
