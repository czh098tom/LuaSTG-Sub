#include "CLRBinding/CLRBinding.hpp"

// 引擎 HTTP 数据结构（http::Request / http::ResponseEntity / http::RequestMethod）
#include "LuaBinding/external/HttpClient.hpp"

#include <new>
#include <string>
#include <unordered_map>

#define WIN32_LEAN_AND_MEAN
#define NOSERVICE
#define NOMCX
#define NOIME
#include <Windows.h>
#include <winhttp.h>
#include <wil/resource.h>
#include <wil/result_macros.h>
#include "utf8.hpp"

#pragma comment(lib, "winhttp.lib")

// HTTP 客户端 API 实现（对应 LuaBinding/external/HttpClient.cpp 的 http.Request / http.ResponseEntity）
// 引擎侧说明：
// - http::Request / http::ResponseEntity 为纯数据结构（方法、URL、头表、请求/响应体、超时），
//   网络流程（WinHTTP 同步请求）位于 Lua 绑定 RequestBinding::execute 内，无法从外部调用，
//   因此本文件将 setHeaders / parseResponseHeaders / verifyUrl / execute 的流程原样移植；
// - Lua 侧 luaL_error 的场景在此以返回 0 + http_getLastError 表达，由 C# 侧转为异常；
// - 与 Lua 版的一处刻意差异：WinHttpOpenRequest 的请求动词按 get/head/post/put/delete/patch/custom
//   如实传递。Lua 版固定为 "POST" / "GET" 二选一（put/delete/patch/head 均按 GET 发送），
//   与其工厂方法语义不符，属于引擎实现缺陷，此处按 API 语义修正。

namespace
{
	/// @brief 最近一次失败的错误描述（C# 侧在下一次调用前拷贝）
	thread_local std::string g_last_error;

	void setLastError(std::string_view const& message)
	{
		g_last_error.assign(message);
	}

	// ==== 以下工具函数移植自 LuaBinding/external/HttpClient.cpp（去除 Lua 依赖）====

	void setHeaders(HINTERNET const handle, std::unordered_map<std::string, std::string> const& headers)
	{
		if (handle == nullptr) {
			return;
		}
		if (headers.empty()) {
			return;
		}
		std::string headers_buffer;
		for (auto const& [name, value] : headers) {
			headers_buffer.append(name);
			headers_buffer.append(": ");
			headers_buffer.append(value);
			headers_buffer.append("\r\n");
		}
		if (headers_buffer.ends_with("\r\n")) {
			headers_buffer.pop_back();
			headers_buffer.pop_back();
		}

		auto const headers_wide = utf8::to_wstring(headers_buffer);
		THROW_IF_WIN32_BOOL_FALSE(WinHttpAddRequestHeaders(
			handle,
			headers_wide.c_str(),
			static_cast<DWORD>(headers_wide.size()),
			0
		));
	}

	std::string_view trim(std::string_view const& input)
	{
		auto const begin = input.find_first_not_of(' ');
		auto const end = input.find_last_not_of(' ');
		if (begin == std::string_view::npos || end == std::string_view::npos) {
			return {};
		}
		return input.substr(begin, end - begin + 1);
	}

	void parseResponseHeaders(std::string const& response_headers, std::unordered_map<std::string, std::string>& headers)
	{
		if (response_headers.empty()) {
			return;
		}

		std::vector<std::string_view> header_lines;
		{
			constexpr std::string_view line_break("\r\n");
			std::string_view view(response_headers);
			while (!view.empty()) {
				auto const next_line_break_index = view.find_first_of(line_break);
				if (next_line_break_index == std::string_view::npos) {
					header_lines.emplace_back(view);
					break;
				}
				if (next_line_break_index == 0) {
					view = view.substr(next_line_break_index + 2); // skip \r\n
					continue;
				}
				auto const current_line = view.substr(0, next_line_break_index);
				view = view.substr(next_line_break_index + 2); // skip \r\n
				if (!current_line.empty() && current_line != line_break) {
					header_lines.emplace_back(current_line);
				}
			}
		}

		for (auto const& line : header_lines) {
			if (line.starts_with("HTTP/")) {
				continue; // status line
			}
			auto const separator_index = line.find_first_of(':');
			if (separator_index == std::string_view::npos) {
				continue;
			}
			auto const name = line.substr(0, separator_index);
			auto const value = line.substr(separator_index + 1);
			headers.emplace(trim(name), trim(value));
		}
	}

	/// @brief 校验 URL（对应 Lua 侧 verifyUrl），成功返回 0；失败返回非 0 并设置错误信息
	int verifyUrl(std::string_view const& url)
	{
		URL_COMPONENTS url_components{};
		url_components.dwStructSize = sizeof(url_components);
		url_components.dwSchemeLength = static_cast<DWORD>(-1);
		url_components.dwHostNameLength = static_cast<DWORD>(-1);
		url_components.dwUserNameLength = static_cast<DWORD>(-1);
		url_components.dwPasswordLength = static_cast<DWORD>(-1);
		url_components.dwUrlPathLength = static_cast<DWORD>(-1);
		url_components.dwExtraInfoLength = static_cast<DWORD>(-1);
		auto const url_wide = utf8::to_wstring(url);
		auto const result = WinHttpCrackUrl(
			url_wide.c_str(), static_cast<DWORD>(url_wide.size()), 0, &url_components);
		if (!result) {
			switch (GetLastError()) {
			case ERROR_WINHTTP_INVALID_URL:
				setLastError("invalid url");
				return 1;
			case ERROR_WINHTTP_UNRECOGNIZED_SCHEME:
				setLastError("unsupported scheme");
				return 2;
			default:
				setLastError("unknown error");
				return 3;
			}
		}
		return 0;
	}

	/// @brief 请求方法对应的请求动词（对应 Lua 侧 getRequestMethodName，补充 custom 的处理）
	std::wstring getRequestMethodVerb(http::Request const* self)
	{
		switch (self->request_method) {
		case http::RequestMethod::get:
			return L"GET";
		case http::RequestMethod::head:
			return L"HEAD";
		case http::RequestMethod::post:
			return L"POST";
		case http::RequestMethod::put:
			return L"PUT";
		case http::RequestMethod::del:
			return L"DELETE";
		case http::RequestMethod::patch:
			return L"PATCH";
		default:
			return utf8::to_wstring(self->custom_request_method);
		}
	}

	/// @brief WinHTTP 同步请求流程（移植自 Lua 侧 RequestBinding::execute，去除 Lua 调用）
	/// @note 与 Lua 版一致：执行成功后 self->headers / self->body 被响应的头表与响应体覆盖
	void executeRequest(http::Request* self)
	{
		BOOL br{};

		// decode url

		URL_COMPONENTS url_components{};
		url_components.dwStructSize = sizeof(url_components);
		url_components.dwSchemeLength = static_cast<DWORD>(-1);
		url_components.dwHostNameLength = static_cast<DWORD>(-1);
		url_components.dwUserNameLength = static_cast<DWORD>(-1);
		url_components.dwPasswordLength = static_cast<DWORD>(-1);
		url_components.dwUrlPathLength = static_cast<DWORD>(-1);
		url_components.dwExtraInfoLength = static_cast<DWORD>(-1);
		auto const url_wide = utf8::to_wstring(self->url);
		br = WinHttpCrackUrl(url_wide.c_str(), static_cast<DWORD>(url_wide.size()), 0, &url_components);
		THROW_IF_WIN32_BOOL_FALSE_MSG(br, "WinHttpCrackUrl failed");

		// open session

		wil::unique_winhttp_hinternet session;
		session.reset(WinHttpOpen(
			nullptr,
			WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
			WINHTTP_NO_PROXY_NAME,
			WINHTTP_NO_PROXY_BYPASS,
			0
		));
		THROW_LAST_ERROR_IF_NULL_MSG(session.get(), "WinHttpOpen failed");

		br = WinHttpSetTimeouts(
			session.get(),
			self->resolve_timeout,
			self->connect_timeout,
			self->send_timeout,
			self->receive_timeout);
		THROW_IF_WIN32_BOOL_FALSE_MSG(br, "WinHttpSetTimeouts failed");

		// open connect

		std::wstring schema(url_components.lpszScheme, url_components.dwSchemeLength);
		std::wstring host_name(url_components.lpszHostName, url_components.dwHostNameLength);
		wil::unique_winhttp_hinternet connect;
		connect.reset(WinHttpConnect(
			session.get(),
			host_name.c_str(),
			schema == L"https" ? INTERNET_DEFAULT_HTTPS_PORT : INTERNET_DEFAULT_HTTP_PORT,
			0
		));
		THROW_LAST_ERROR_IF_NULL_MSG(connect.get(), "WinHttpConnect failed");

		// open request

		std::wstring path(url_components.lpszUrlPath);
		if (path.empty()) {
			path.push_back(L'/');
		}
		auto const verb = getRequestMethodVerb(self);
		wil::unique_winhttp_hinternet request;
		request.reset(WinHttpOpenRequest(
			connect.get(),
			verb.c_str(),
			path.c_str(),
			nullptr,
			WINHTTP_NO_REFERER,
			WINHTTP_DEFAULT_ACCEPT_TYPES,
			schema == L"https" ? WINHTTP_FLAG_SECURE : 0
		));
		THROW_LAST_ERROR_IF_NULL_MSG(request.get(), "WinHttpOpenRequest failed");

		// send request

		setHeaders(request.get(), self->headers);

		auto const body_size = static_cast<DWORD>(self->body.size());
		br = WinHttpSendRequest(
			request.get(),
			WINHTTP_NO_ADDITIONAL_HEADERS, 0,
			body_size > 0 ? self->body.data() : WINHTTP_NO_REQUEST_DATA, body_size,
			body_size, 0
		);
		THROW_IF_WIN32_BOOL_FALSE_MSG(br, "WinHttpSendRequest failed");

		// receive response

		THROW_IF_WIN32_BOOL_FALSE(WinHttpReceiveResponse(request.get(), nullptr));

		DWORD response_headers_size{};
		SetLastError(ERROR_SUCCESS);
		br = WinHttpQueryHeaders(
			request.get(),
			WINHTTP_QUERY_RAW_HEADERS_CRLF,
			WINHTTP_HEADER_NAME_BY_INDEX,
			WINHTTP_NO_OUTPUT_BUFFER,
			&response_headers_size,
			WINHTTP_NO_HEADER_INDEX
		);
		if (!br && GetLastError() != ERROR_INSUFFICIENT_BUFFER) {
			THROW_IF_WIN32_BOOL_FALSE_MSG(br, "WinHttpQueryHeaders failed");
		}

		std::wstring response_headers_buffer((response_headers_size + 1) / sizeof(std::wstring::value_type), L'\0');
		SetLastError(ERROR_SUCCESS);
		br = WinHttpQueryHeaders(
			request.get(),
			WINHTTP_QUERY_RAW_HEADERS_CRLF,
			WINHTTP_HEADER_NAME_BY_INDEX,
			response_headers_buffer.data(),
			&response_headers_size,
			WINHTTP_NO_HEADER_INDEX
		);
		THROW_IF_WIN32_BOOL_FALSE_MSG(br, "WinHttpQueryHeaders failed");

		response_headers_buffer.resize(response_headers_size / sizeof(std::wstring::value_type));
		auto const response_headers = utf8::to_string(response_headers_buffer);
		self->headers.clear();
		parseResponseHeaders(response_headers, self->headers);

		std::string buffer;
		for (;;) {
			DWORD bytes_available{};
			br = WinHttpQueryDataAvailable(request.get(), &bytes_available);
			THROW_IF_WIN32_BOOL_FALSE_MSG(br, "WinHttpQueryDataAvailable failed");
			if (bytes_available == 0) {
				break;
			}

			std::string temp_buffer(bytes_available, '\0');
			DWORD bytes_read{};
			br = WinHttpReadData(request.get(), temp_buffer.data(), bytes_available, &bytes_read);
			THROW_IF_WIN32_BOOL_FALSE_MSG(br, "WinHttpReadData failed");
			if (bytes_read == 0) {
				break;
			}
			temp_buffer.resize(bytes_read);
			buffer.append(temp_buffer);
		}

		self->body = std::move(buffer);
	}

	http::Request* asRequest(uintptr_t const handle)
	{
		return reinterpret_cast<http::Request*>(handle);
	}

	http::ResponseEntity* asResponse(uintptr_t const handle)
	{
		return reinterpret_cast<http::ResponseEntity*>(handle);
	}
}

// 引擎 API 实现
namespace luastg
{
	uintptr_t CLRBinding::http_request_create(int32_t const request_method, const char* const custom_method, const char* const url)
	{
		if (url == nullptr || url[0] == '\0') {
			setLastError("invalid url");
			return 0;
		}
		if (request_method < static_cast<int32_t>(http::RequestMethod::custom)
			|| request_method > static_cast<int32_t>(http::RequestMethod::patch)) {
			setLastError("unknown request method");
			return 0;
		}
		auto const method = static_cast<http::RequestMethod>(request_method);
		if (method == http::RequestMethod::custom && (custom_method == nullptr || custom_method[0] == '\0')) {
			setLastError("unknown request method");
			return 0;
		}
		if (verifyUrl(url) != 0) {
			return 0;
		}
		auto* const request = new (std::nothrow) http::Request();
		if (request == nullptr) {
			setLastError("out of memory");
			return 0;
		}
		request->request_method = method;
		if (method == http::RequestMethod::custom) {
			request->custom_request_method = custom_method;
		}
		request->url = url;
		return reinterpret_cast<uintptr_t>(request);
	}

	void CLRBinding::http_request_destroy(uintptr_t const request)
	{
		delete asRequest(request);
	}

	const char* CLRBinding::http_getLastError()
	{
		return g_last_error.c_str();
	}

	void CLRBinding::http_request_setResolveTimeout(uintptr_t const request, int32_t const timeout)
	{
		if (auto* const self = asRequest(request)) {
			self->resolve_timeout = timeout;
		}
	}

	void CLRBinding::http_request_setConnectTimeout(uintptr_t const request, int32_t const timeout)
	{
		if (auto* const self = asRequest(request)) {
			self->connect_timeout = timeout;
		}
	}

	void CLRBinding::http_request_setSendTimeout(uintptr_t const request, int32_t const timeout)
	{
		if (auto* const self = asRequest(request)) {
			self->send_timeout = timeout;
		}
	}

	void CLRBinding::http_request_setReceiveTimeout(uintptr_t const request, int32_t const timeout)
	{
		if (auto* const self = asRequest(request)) {
			self->receive_timeout = timeout;
		}
	}

	void CLRBinding::http_request_addHeader(uintptr_t const request, const char* const name, const char* const value)
	{
		auto* const self = asRequest(request);
		if (self == nullptr || name == nullptr || value == nullptr) {
			return;
		}
		// 与 Lua 侧一致使用 emplace：同名头保留先设置的值
		self->headers.emplace(name, value);
	}

	uint8_t CLRBinding::http_request_setBody(uintptr_t const request, const uint8_t* const data, uint32_t const length)
	{
		auto* const self = asRequest(request);
		if (self == nullptr) {
			setLastError("request object is null");
			return 0;
		}
		switch (self->request_method) {
		case http::RequestMethod::post:
		case http::RequestMethod::put:
		case http::RequestMethod::patch:
			break;
		default:
			setLastError("request method does not support request body");
			return 0;
		}
		if (data == nullptr && length > 0) {
			setLastError("body data is null");
			return 0;
		}
		if (length > 0) {
			self->body.assign(reinterpret_cast<char const*>(data), length);
		}
		else {
			self->body.clear();
		}
		return 1;
	}

	uintptr_t CLRBinding::http_request_execute(uintptr_t const request)
	{
		auto* const self = asRequest(request);
		if (self == nullptr) {
			setLastError("request object is null");
			return 0;
		}
		try {
			executeRequest(self);
		}
		catch (wil::ResultException const& e) {
			std::wstring message_buffer(65536, L'\0');
			wil::GetFailureLogString(message_buffer.data(), message_buffer.size(), e.GetFailureInfo());
			setLastError(utf8::to_string(message_buffer).c_str());
			return 0;
		}
		catch (std::exception const& e) {
			setLastError(e.what());
			return 0;
		}
		auto* const entity = new (std::nothrow) http::ResponseEntity();
		if (entity == nullptr) {
			setLastError("out of memory");
			return 0;
		}
		entity->headers = self->headers;
		entity->body = self->body;
		return reinterpret_cast<uintptr_t>(entity);
	}

	void CLRBinding::http_response_destroy(uintptr_t const response)
	{
		delete asResponse(response);
	}

	uint8_t CLRBinding::http_response_hasHeader(uintptr_t const response, const char* const name)
	{
		auto* const self = asResponse(response);
		if (self == nullptr || name == nullptr) {
			return 0;
		}
		return self->headers.contains(name) ? 1u : 0u;
	}

	const char* CLRBinding::http_response_getHeader(uintptr_t const response, const char* const name)
	{
		auto* const self = asResponse(response);
		if (self == nullptr || name == nullptr) {
			return nullptr;
		}
		auto const it = self->headers.find(name);
		return it != self->headers.end() ? it->second.c_str() : nullptr;
	}

	uint32_t CLRBinding::http_response_getBodyLength(uintptr_t const response)
	{
		auto* const self = asResponse(response);
		if (self == nullptr) {
			return 0;
		}
		return static_cast<uint32_t>(self->body.size());
	}

	const uint8_t* CLRBinding::http_response_getBodyData(uintptr_t const response)
	{
		auto* const self = asResponse(response);
		if (self == nullptr || self->body.empty()) {
			return nullptr;
		}
		return reinterpret_cast<uint8_t const*>(self->body.data());
	}
}
