// LuaSTG CoreCLR 绑定：HTTP 客户端（对应 LuaBinding/external/HttpClient.cpp，Lua 侧 http.Request / http.ResponseEntity）
// 本文件以 X-macro 方式被 CLRBinding.hpp 展开，同时被 tool/clr-api-generator 解析生成 C# 侧声明
// 修改本文件后需要重新运行生成器（tool/clr-api-generator/regen_and_build.sh）
// 参数与返回值只允许使用生成器支持的 C 类型（见生成器类型映射表）
//
// 对象模型：
// - 请求/响应对象为引擎结构 http::Request / http::ResponseEntity 的堆分配实例，
//   C# 侧持有 uintptr_t 句柄，用完必须调用对应的 destroy 销毁；
// - 请求方法枚举与引擎侧 http::RequestMethod 一致：
//   0=custom 1=get 2=head 3=post 4=put 5=delete 6=patch，custom 时使用 custom_method 字符串；
// - Lua 侧 luaL_error 的场景（URL 非法、请求失败等）以返回 0 表达，
//   详细错误信息通过 http_getLastError 查询（引擎侧线程局部缓冲，下一次调用前拷贝）。

// ===== Request 生命周期 =====
// 创建请求对象并校验 URL（对应 Lua 侧工厂 get/head/post/put/delete/patch/request），
// URL 非法或方法非法返回 0
DECLARE_CLR_API(uintptr_t, http_request_create, (int32_t request_method, const char* custom_method, const char* url))
// 销毁请求对象（句柄此后失效，空句柄安全）
DECLARE_CLR_API(void, http_request_destroy, (uintptr_t request))
// 最近一次失败的错误描述（UTF-8，指向引擎侧线程局部缓冲）
DECLARE_CLR_API(const char*, http_getLastError, ())

// ===== Request 配置（对应 http.Request 的 set*Timeout / addHeader / body，超时单位毫秒） =====
DECLARE_CLR_API(void, http_request_setResolveTimeout, (uintptr_t request, int32_t timeout))
DECLARE_CLR_API(void, http_request_setConnectTimeout, (uintptr_t request, int32_t timeout))
DECLARE_CLR_API(void, http_request_setSendTimeout, (uintptr_t request, int32_t timeout))
DECLARE_CLR_API(void, http_request_setReceiveTimeout, (uintptr_t request, int32_t timeout))
// 添加请求头；同名头保留先设置的值（与 Lua 侧 emplace 语义一致）
DECLARE_CLR_API(void, http_request_addHeader, (uintptr_t request, const char* name, const char* value))
// 设置请求体（data 可为空指针表示空请求体）；仅 post/put/patch 支持，
// 方法不支持返回 0，成功返回 1
DECLARE_CLR_API(uint8_t, http_request_setBody, (uintptr_t request, const uint8_t* data, uint32_t length))

// ===== 执行（对应 http.Request.execute，同步 WinHTTP 流程） =====
// 成功返回 http::ResponseEntity 句柄，失败返回 0（错误信息用 http_getLastError 查询）
DECLARE_CLR_API(uintptr_t, http_request_execute, (uintptr_t request))

// ===== ResponseEntity（对应 http.ResponseEntity） =====
// 销毁响应对象（句柄此后失效，空句柄安全）
DECLARE_CLR_API(void, http_response_destroy, (uintptr_t response))
DECLARE_CLR_API(uint8_t, http_response_hasHeader, (uintptr_t response, const char* name))
// 头不存在时返回空指针；返回指针指向引擎内存，须立即拷贝
DECLARE_CLR_API(const char*, http_response_getHeader, (uintptr_t response, const char* name))
// 响应体字节长度
DECLARE_CLR_API(uint32_t, http_response_getBodyLength, (uintptr_t response))
// 响应体数据指针（指向引擎内存，须立即拷贝；空响应体返回空指针）
DECLARE_CLR_API(const uint8_t*, http_response_getBodyData, (uintptr_t response))
