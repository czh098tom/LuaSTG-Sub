// LuaSTG CoreCLR 绑定：DirectWrite 文字排版模块实现
// 对应 Lua 绑定：LuaBinding/external/lua_dwrite.cpp（luaopen_dwrite 模块）
// 移植说明：Lua 侧在 luaopen_dwrite 时创建 DirectWrite.Factory（WIC/D2D1/DWrite 工厂与自定义
// 字体文件加载器），C# 侧对应本文件内惰性初始化的工厂单例（getFactory）。
// Lua 栈操作（luaL_check/luaL_error）改为错误码与 0 句柄返回，由 C# 侧包装
// （LuaSTG.Core/DirectWrite.cs）抛出异常，保持与 Lua 侧报错行为一致。
//
// 句柄管理模式：每个 uintptr_t 句柄指向 C++ 侧堆分配的包装结构（对应 Lua 侧 userdata），
// 包装结构内以 ComPtr 持有 DirectWrite 对象；*Destroy 负责 delete 包装结构并释放引用。
// TextMetrics / OverhangMetrics 为纯字段结构，C# 侧用托管 struct 表达，不经过句柄。

#include "CLRBinding/CLRBinding.hpp"

#include "AppFrame.h"
#include "core/FileSystem.hpp"
#include "core/Image.hpp"
#include "backend/WicImage.hpp"
#include "windows/HResultChecker.hpp"
#include "utf8.hpp"

#include <cassert>
#include <memory>
#include <new>
#include <sstream>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#define NOSERVICE
#define NOMCX
#define NOIME

#ifdef _WIN32_WINNT
#undef _WIN32_WINNT
#endif
#ifdef NTDDI_VERSION
#undef NTDDI_VERSION
#endif
#ifdef WINVER
#undef WINVER
#endif

#include <sdkddkver.h>

#include <Windows.h>
#include <wrl/client.h>
#include <wrl/wrappers/corewrappers.h>

#undef GetTextMetrics

#include <wincodec.h>
#include <d2d1_3.h>
#include <dwrite_3.h>

#undef WIN32_LEAN_AND_MEAN
#undef NOSERVICE
#undef NOMCX
#undef NOIME

using namespace luastg;

namespace
{
	// ============ d2d1 / dwrite 动态加载（与 Lua 侧一致，dwrite 未静态链接） ============

	struct ModuleLoader
	{
		HMODULE dll_d2d1;
		HMODULE dll_dwrite;
		HRESULT(WINAPI* api_D2D1CreateFactory)(D2D1_FACTORY_TYPE, REFIID, CONST D2D1_FACTORY_OPTIONS*, void**);
		HRESULT(WINAPI* api_DWriteCreateFactory)(DWRITE_FACTORY_TYPE, REFIID, IUnknown**);
		void(WINAPI* api_D2D1MakeRotateMatrix)(FLOAT, D2D1_POINT_2F, D2D1_MATRIX_3X2_F*);

		ModuleLoader()
			: dll_d2d1(NULL)
			, dll_dwrite(NULL)
			, api_D2D1CreateFactory(NULL)
			, api_DWriteCreateFactory(NULL)
			, api_D2D1MakeRotateMatrix(NULL)
		{
			dll_d2d1 = LoadLibraryW(L"d2d1.dll");
			dll_dwrite = LoadLibraryW(L"dwrite.dll");
			if (dll_d2d1)
			{
				api_D2D1CreateFactory = (decltype(api_D2D1CreateFactory))GetProcAddress(dll_d2d1, "D2D1CreateFactory");
				api_D2D1MakeRotateMatrix = (decltype(api_D2D1MakeRotateMatrix))GetProcAddress(dll_d2d1, "D2D1MakeRotateMatrix");
			}
			if (dll_dwrite)
				api_DWriteCreateFactory = (decltype(api_DWriteCreateFactory))GetProcAddress(dll_dwrite, "DWriteCreateFactory");
			assert(api_D2D1CreateFactory);
			assert(api_DWriteCreateFactory);
			assert(api_D2D1MakeRotateMatrix);
		}
		~ModuleLoader()
		{
			if (dll_d2d1) FreeLibrary(dll_d2d1);
			if (dll_dwrite) FreeLibrary(dll_dwrite);
			dll_d2d1 = NULL;
			dll_dwrite = NULL;
			api_D2D1CreateFactory = NULL;
			api_DWriteCreateFactory = NULL;
			api_D2D1MakeRotateMatrix = NULL;
		}
	};
	static struct ModuleLoader DLL;

	inline D2D1::ColorF Color4BToColorF(core::Color4B c)
	{
		return D2D1::ColorF((FLOAT)c.r / 255.0f, (FLOAT)c.g / 255.0f, (FLOAT)c.b / 255.0f, (FLOAT)c.a / 255.0f);
	}

	// ============ COM 实现基类（与 Lua 侧一致） ============

	template<typename T>
	class UnknownImplement : public T
	{
	private:
		volatile unsigned long m_ref;
	public:
		HRESULT WINAPI QueryInterface(IID const& riid, void** ppvObject)
		{
			if (riid == __uuidof(IUnknown))
			{
				AddRef();
				*ppvObject = static_cast<IUnknown*>(this);
				return S_OK;
			}
			else if (riid == __uuidof(T))
			{
				AddRef();
				*ppvObject = static_cast<T*>(this);
				return S_OK;
			}
			else
			{
				return E_NOINTERFACE;
			}
		}
		ULONG WINAPI AddRef()
		{
			return InterlockedIncrement(&m_ref);
		}
		ULONG WINAPI Release()
		{
			ULONG const ref_count = InterlockedDecrement(&m_ref);
			if (ref_count == 0u)
			{
				delete this;
			}
			return ref_count;
		}
	public:
		UnknownImplement() : m_ref(1) {}
		virtual ~UnknownImplement() {}
	};

	class DWriteFontFileStreamImplement : public UnknownImplement<IDWriteFontFileStream>
	{
	private:
		core::SmartReference<core::IData> m_data;
	public:
		HRESULT WINAPI ReadFileFragment(void const** fragmentStart, UINT64 fileOffset, UINT64 fragmentSize, void** fragmentContext) noexcept
		{
			assert(fragmentStart);
			assert(fragmentContext);
			assert(fileOffset <= UINT32_MAX && fragmentSize <= UINT32_MAX && (fileOffset + fragmentSize) <= UINT32_MAX); // only files smaller than 4GB are supported
			if ((fileOffset + fragmentSize) > m_data->size()) return E_INVALIDARG;
			*fragmentStart = static_cast<uint8_t*>(m_data->data()) + fileOffset;
			*fragmentContext = static_cast<uint8_t*>(m_data->data()) + fileOffset; // for identification only
			return S_OK;
		}
		void WINAPI ReleaseFileFragment(void* fragmentContext) noexcept
		{
			UNREFERENCED_PARAMETER(fragmentContext);
			// no additional heap memory to free
		}
		HRESULT WINAPI GetFileSize(UINT64* fileSize) noexcept
		{
			assert(fileSize);
			*fileSize = m_data->size();
			return S_OK; // always succeed
		}
		HRESULT WINAPI GetLastWriteTime(UINT64* lastWriteTime) noexcept
		{
			UNREFERENCED_PARAMETER(lastWriteTime);
			return E_NOTIMPL; // always failed (not applicable for in-memory font files)
		}
	public:
		bool loadFromFileManager(std::string_view const path)
		{
			return core::FileSystemManager::readFile(path, m_data.put()); // OOM catch by factory
		}
	public:
		DWriteFontFileStreamImplement() {}
		virtual ~DWriteFontFileStreamImplement() {}
	};

	class DWriteFontFileLoaderImplement : public UnknownImplement<IDWriteFontFileLoader>
	{
	private:
		std::unordered_map<std::string, Microsoft::WRL::ComPtr<DWriteFontFileStreamImplement>> m_cache;
	public:
		HRESULT WINAPI CreateStreamFromKey(void const* fontFileReferenceKey, UINT32 fontFileReferenceKeySize, IDWriteFontFileStream** fontFileStream) noexcept
		{
			assert(fontFileReferenceKey && fontFileReferenceKeySize > 0);
			assert(fontFileStream);
			try
			{
				std::string path((char*)fontFileReferenceKey, fontFileReferenceKeySize);
				auto it = m_cache.find(path);
				if (it != m_cache.end())
				{
					it->second->AddRef();
					*fontFileStream = it->second.Get();
					return S_OK;
				}

				Microsoft::WRL::ComPtr<DWriteFontFileStreamImplement> object;
				object.Attach(new (std::nothrow) DWriteFontFileStreamImplement());
				if (!object)
					return E_OUTOFMEMORY;
				if (!object->loadFromFileManager(path))
					return E_FAIL;

				object->AddRef();
				*fontFileStream = object.Get();

				m_cache.emplace(std::move(path), std::move(object));
				return S_OK;
			}
			catch (...)
			{
				return E_FAIL;
			}
		}
	public:
		DWriteFontFileLoaderImplement() {}
		virtual ~DWriteFontFileLoaderImplement() {}
	};

	using shared_string_list = std::shared_ptr<std::vector<std::string>>;

	class DWriteFontFileEnumeratorImplement : public UnknownImplement<IDWriteFontFileEnumerator>
	{
	private:
		Microsoft::WRL::ComPtr<IDWriteFactory> m_dwrite_factory;
		Microsoft::WRL::ComPtr<IDWriteFontFileLoader> m_dwrite_font_file_loader;
		shared_string_list m_font_file_name_list;
		LONG m_index{};
	public:
		HRESULT WINAPI MoveNext(BOOL* hasCurrentFile) noexcept
		{
			assert(hasCurrentFile);
			assert(m_font_file_name_list);
			m_index += 1;
			if (m_index >= 0 && m_index < (LONG)m_font_file_name_list->size())
			{
				*hasCurrentFile = TRUE;
			}
			else
			{
				*hasCurrentFile = FALSE;
			}
			return S_OK;
		}
		HRESULT WINAPI GetCurrentFontFile(IDWriteFontFile** fontFile) noexcept
		{
			assert(fontFile);
			assert(m_font_file_name_list);
			assert(m_index >= 0 && m_index < (LONG)m_font_file_name_list->size());
			if (m_index < 0 || m_index >(LONG)m_font_file_name_list->size())
			{
				return E_FAIL;
			}
			std::string const& path = m_font_file_name_list->at((size_t)m_index);
			if (core::FileSystemManager::hasFile(path))
			{
				return m_dwrite_factory->CreateCustomFontFileReference(
					path.data(),
					(UINT32)path.size(),
					m_dwrite_font_file_loader.Get(),
					fontFile
				);
			}
			else
			{
				std::wstring wide_path(utf8::to_wstring(path));
				return m_dwrite_factory->CreateFontFileReference(
					wide_path.c_str(),
					NULL,
					fontFile
				);
			}
		}
	public:
		void reset(IDWriteFactory* factory, IDWriteFontFileLoader* loader, shared_string_list list)
		{
			assert(factory);
			assert(loader);
			m_dwrite_factory = factory;
			m_dwrite_font_file_loader = loader;
			m_font_file_name_list = list; // bulk copy operations, OOM catch by factory
			m_index = -1;
		}
	public:
		DWriteFontFileEnumeratorImplement() {}
		virtual ~DWriteFontFileEnumeratorImplement() {}
	};

	class DWriteFontCollectionLoaderImplement : public UnknownImplement<IDWriteFontCollectionLoader>
	{
	private:
		Microsoft::WRL::ComPtr<IDWriteFactory> m_dwrite_factory;
		Microsoft::WRL::ComPtr<IDWriteFontFileLoader> m_dwrite_font_file_loader;
		shared_string_list m_font_file_name_list;
	public:
		HRESULT WINAPI CreateEnumeratorFromKey(IDWriteFactory* factory, void const* collectionKey, UINT32 collectionKeySize, IDWriteFontFileEnumerator** fontFileEnumerator) noexcept
		{
			UNREFERENCED_PARAMETER(factory);
			UNREFERENCED_PARAMETER(collectionKey);
			UNREFERENCED_PARAMETER(collectionKeySize);
			assert(m_dwrite_factory);
			assert(m_dwrite_font_file_loader);
			assert(m_font_file_name_list);
			assert(collectionKey || collectionKeySize == 0);
			assert(factory);
			assert(fontFileEnumerator);
			try
			{
				Microsoft::WRL::ComPtr<DWriteFontFileEnumeratorImplement> object;
				object.Attach(new (std::nothrow) DWriteFontFileEnumeratorImplement());
				if (!object)
					return E_OUTOFMEMORY;
				object->reset(
					m_dwrite_factory.Get(),
					m_dwrite_font_file_loader.Get(),
					m_font_file_name_list
				);
				*fontFileEnumerator = object.Detach();
				return S_OK;
			}
			catch (std::exception const&)
			{
				return E_OUTOFMEMORY;
			}
		}
	public:
		void reset(IDWriteFactory* factory, IDWriteFontFileLoader* loader, shared_string_list list)
		{
			assert(factory);
			assert(loader);
			m_dwrite_factory = factory;
			m_dwrite_font_file_loader = loader;
			m_font_file_name_list = list;
		}
	public:
		DWriteFontCollectionLoaderImplement() {}
		virtual ~DWriteFontCollectionLoaderImplement() {}
	};

	// ============ 自定义文本渲染器（描边 + 填充，与 Lua 侧一致） ============

	class DWriteTextRendererImplement : public IDWriteTextRenderer1
	{
	private:
		Microsoft::WRL::ComPtr<ID2D1Factory> d2d1_factory;
		Microsoft::WRL::ComPtr<ID2D1RenderTarget> d2d1_rt;
		Microsoft::WRL::ComPtr<IDWriteTextLayout> dwrite_text_layout;
		Microsoft::WRL::ComPtr<ID2D1Brush> d2d1_brush_outline;
		Microsoft::WRL::ComPtr<ID2D1Brush> d2d1_brush_fill;
		Microsoft::WRL::ComPtr<ID2D1StrokeStyle> d2d1_stroke_style;
		FLOAT outline_width;
		BOOL layer_text{ TRUE };
		BOOL layer_stroke{ TRUE };
	public:
		HRESULT WINAPI QueryInterface(IID const& riid, void** ppvObject)
		{
			if (riid == __uuidof(IUnknown))
			{
				AddRef();
				*ppvObject = static_cast<IUnknown*>(this);
				return S_OK;
			}
			else if (riid == __uuidof(IDWritePixelSnapping))
			{
				AddRef();
				*ppvObject = static_cast<IDWritePixelSnapping*>(this);
				return S_OK;
			}
			else if (riid == __uuidof(IDWriteTextRenderer))
			{
				AddRef();
				*ppvObject = static_cast<IDWriteTextRenderer*>(this);
				return S_OK;
			}
			else if (riid == __uuidof(IDWriteTextRenderer1))
			{
				AddRef();
				*ppvObject = static_cast<IDWriteTextRenderer1*>(this);
				return S_OK;
			}
			else
			{
				return E_NOINTERFACE;
			}
		}
		ULONG WINAPI AddRef() { return 2; }
		ULONG WINAPI Release() { return 1; }
	public:
		HRESULT WINAPI IsPixelSnappingDisabled(void* clientDrawingContext, BOOL* isDisabled) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			*isDisabled = FALSE; // recommended default value
			return S_OK;
		}
		HRESULT WINAPI GetCurrentTransform(void* clientDrawingContext, DWRITE_MATRIX* transform) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			// forward the render target's transform
			d2d1_rt->GetTransform(reinterpret_cast<D2D1_MATRIX_3X2_F*>(transform));
			return S_OK;
		}
		HRESULT WINAPI GetPixelsPerDip(void* clientDrawingContext, FLOAT* pixelsPerDip) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			float x = 0.0f, y = 0.0f;
			d2d1_rt->GetDpi(&x, &y);
			*pixelsPerDip = x / 96.0f;
			return S_OK;
		}
	public:
		HRESULT WINAPI DrawGlyphRun(
			void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			DWRITE_MEASURING_MODE measuringMode,
			DWRITE_GLYPH_RUN const* glyphRun,
			DWRITE_GLYPH_RUN_DESCRIPTION const* glyphRunDescription,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(measuringMode);
			UNREFERENCED_PARAMETER(glyphRunDescription);
			UNREFERENCED_PARAMETER(clientDrawingEffect);

			HRESULT hr = S_OK;

			// Create the path geometry.

			Microsoft::WRL::ComPtr<ID2D1PathGeometry> d2d1_path_geometry;
			hr = gHR = d2d1_factory->CreatePathGeometry(&d2d1_path_geometry);
			if (FAILED(hr)) return hr;

			// Write to the path geometry using the geometry sink.

			Microsoft::WRL::ComPtr<ID2D1GeometrySink> d2d1_geometry_sink;
			hr = gHR = d2d1_path_geometry->Open(&d2d1_geometry_sink);
			if (FAILED(hr)) return hr;

			hr = gHR = glyphRun->fontFace->GetGlyphRunOutline(
				glyphRun->fontEmSize,
				glyphRun->glyphIndices,
				glyphRun->glyphAdvances,
				glyphRun->glyphOffsets,
				glyphRun->glyphCount,
				glyphRun->isSideways,
				glyphRun->bidiLevel % 2,
				d2d1_geometry_sink.Get());
			if (FAILED(hr)) return hr;

			hr = gHR = d2d1_geometry_sink->Close();
			if (FAILED(hr)) return hr;

			D2D1::Matrix3x2F const matrix = D2D1::Matrix3x2F(
				1.0f, 0.0f,
				0.0f, 1.0f,
				baselineOriginX, baselineOriginY
			);
			Microsoft::WRL::ComPtr<ID2D1TransformedGeometry> d2d1_transformed_geometry;
			hr = gHR = d2d1_factory->CreateTransformedGeometry(
				d2d1_path_geometry.Get(),
				&matrix,
				&d2d1_transformed_geometry);
			if (FAILED(hr)) return hr;

			// Draw the outline of the glyph run

			if (layer_stroke) d2d1_rt->DrawGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_outline.Get(), outline_width, d2d1_stroke_style.Get());

			// Fill in the glyph run

			if (layer_text) d2d1_rt->FillGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_fill.Get());

			return S_OK;
		}
		HRESULT WINAPI DrawUnderline(
			void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			DWRITE_UNDERLINE const* underline,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(clientDrawingEffect);

			HRESULT hr = S_OK;

			D2D1_RECT_F rect = D2D1::RectF(
				0,
				underline->offset,
				underline->width,
				underline->offset + underline->thickness
			);

			Microsoft::WRL::ComPtr<ID2D1RectangleGeometry> d2d1_rect_geometry;
			hr = gHR = d2d1_factory->CreateRectangleGeometry(&rect, &d2d1_rect_geometry);
			if (FAILED(hr)) return hr;

			D2D1::Matrix3x2F const matrix = D2D1::Matrix3x2F(
				1.0f, 0.0f,
				0.0f, 1.0f,
				baselineOriginX, baselineOriginY
			);

			Microsoft::WRL::ComPtr<ID2D1TransformedGeometry> d2d1_transformed_geometry;
			hr = gHR = d2d1_factory->CreateTransformedGeometry(
				d2d1_rect_geometry.Get(),
				&matrix,
				&d2d1_transformed_geometry);
			if (FAILED(hr)) return hr;

			if (layer_stroke) d2d1_rt->DrawGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_outline.Get(), outline_width);
			if (layer_text) d2d1_rt->FillGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_fill.Get());

			return S_OK;
		}
		HRESULT WINAPI DrawStrikethrough(
			void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			DWRITE_STRIKETHROUGH const* strikethrough,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(clientDrawingEffect);

			HRESULT hr = S_OK;

			D2D1_RECT_F rect = D2D1::RectF(
				0,
				strikethrough->offset,
				strikethrough->width,
				strikethrough->offset + strikethrough->thickness
			);

			Microsoft::WRL::ComPtr<ID2D1RectangleGeometry> d2d1_rect_geometry;
			hr = gHR = d2d1_factory->CreateRectangleGeometry(&rect, &d2d1_rect_geometry);
			if (FAILED(hr)) return hr;

			D2D1::Matrix3x2F const matrix = D2D1::Matrix3x2F(
				1.0f, 0.0f,
				0.0f, 1.0f,
				baselineOriginX, baselineOriginY
			);

			Microsoft::WRL::ComPtr<ID2D1TransformedGeometry> d2d1_transformed_geometry;
			hr = gHR = d2d1_factory->CreateTransformedGeometry(
				d2d1_rect_geometry.Get(),
				&matrix,
				&d2d1_transformed_geometry);
			if (FAILED(hr)) return hr;

			if (layer_stroke) d2d1_rt->DrawGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_outline.Get(), outline_width);
			if (layer_text) d2d1_rt->FillGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_fill.Get());

			return S_OK;
		}
		HRESULT WINAPI DrawInlineObject(
			void* clientDrawingContext,
			FLOAT originX,
			FLOAT originY,
			IDWriteInlineObject* inlineObject,
			BOOL isSideways,
			BOOL isRightToLeft,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(originX);
			UNREFERENCED_PARAMETER(originY);
			UNREFERENCED_PARAMETER(inlineObject);
			UNREFERENCED_PARAMETER(isSideways);
			UNREFERENCED_PARAMETER(isRightToLeft);
			UNREFERENCED_PARAMETER(clientDrawingEffect);
			return E_NOTIMPL;
		}
	public:
		HRESULT WINAPI DrawGlyphRun(
			void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			DWRITE_GLYPH_ORIENTATION_ANGLE orientationAngle,
			DWRITE_MEASURING_MODE measuringMode,
			DWRITE_GLYPH_RUN const* glyphRun,
			DWRITE_GLYPH_RUN_DESCRIPTION const* glyphRunDescription,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(measuringMode);
			UNREFERENCED_PARAMETER(glyphRunDescription);
			UNREFERENCED_PARAMETER(clientDrawingEffect);

			HRESULT hr = S_OK;

			// Create the path geometry.

			Microsoft::WRL::ComPtr<ID2D1PathGeometry> d2d1_path_geometry;
			hr = gHR = d2d1_factory->CreatePathGeometry(&d2d1_path_geometry);
			if (FAILED(hr)) return hr;

			// Write to the path geometry using the geometry sink.

			Microsoft::WRL::ComPtr<ID2D1GeometrySink> d2d1_geometry_sink;
			hr = gHR = d2d1_path_geometry->Open(&d2d1_geometry_sink);
			if (FAILED(hr)) return hr;

			hr = gHR = glyphRun->fontFace->GetGlyphRunOutline(
				glyphRun->fontEmSize,
				glyphRun->glyphIndices,
				glyphRun->glyphAdvances,
				glyphRun->glyphOffsets,
				glyphRun->glyphCount,
				glyphRun->isSideways,
				glyphRun->bidiLevel % 2,
				d2d1_geometry_sink.Get());
			if (FAILED(hr)) return hr;

			hr = gHR = d2d1_geometry_sink->Close();
			if (FAILED(hr)) return hr;

			// TODO: 为什么旋转方向是这样判断的？（与 Lua 侧一致）
			FLOAT rotate_angle = 0.0f;
			UNREFERENCED_PARAMETER(orientationAngle);
			switch (dwrite_text_layout->GetReadingDirection())
			{
			case DWRITE_READING_DIRECTION_LEFT_TO_RIGHT: rotate_angle = 0.0f; break;
			case DWRITE_READING_DIRECTION_TOP_TO_BOTTOM: rotate_angle = 90.0f; break;
			default: assert(false); break;
			}
			D2D1::Matrix3x2F matrix;
			DLL.api_D2D1MakeRotateMatrix(rotate_angle, D2D1::Point2F(), &matrix);
			matrix.dx = baselineOriginX;
			matrix.dy = baselineOriginY;
			Microsoft::WRL::ComPtr<ID2D1TransformedGeometry> d2d1_transformed_geometry;
			hr = gHR = d2d1_factory->CreateTransformedGeometry(
				d2d1_path_geometry.Get(),
				&matrix,
				&d2d1_transformed_geometry);
			if (FAILED(hr)) return hr;

			// Draw the outline of the glyph run

			if (layer_stroke) d2d1_rt->DrawGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_outline.Get(), outline_width, d2d1_stroke_style.Get());

			// Fill in the glyph run

			if (layer_text) d2d1_rt->FillGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_fill.Get());

			return S_OK;
		}
		HRESULT WINAPI DrawUnderline(
			void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			DWRITE_GLYPH_ORIENTATION_ANGLE orientationAngle,
			DWRITE_UNDERLINE const* underline,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(clientDrawingEffect);

			HRESULT hr = S_OK;

			D2D1_RECT_F rect = D2D1::RectF(
				0,
				underline->offset,
				underline->width,
				underline->offset + underline->thickness
			);

			Microsoft::WRL::ComPtr<ID2D1RectangleGeometry> d2d1_rect_geometry;
			hr = gHR = d2d1_factory->CreateRectangleGeometry(&rect, &d2d1_rect_geometry);
			if (FAILED(hr)) return hr;

			// TODO: 为什么旋转方向是这样判断的？（与 Lua 侧一致）
			FLOAT rotate_angle = 0.0f;
			UNREFERENCED_PARAMETER(orientationAngle);
			switch (dwrite_text_layout->GetReadingDirection())
			{
			case DWRITE_READING_DIRECTION_LEFT_TO_RIGHT: rotate_angle = 0.0f; break;
			case DWRITE_READING_DIRECTION_TOP_TO_BOTTOM: rotate_angle = 90.0f; break;
			default: assert(false); break;
			}
			D2D1::Matrix3x2F matrix;
			DLL.api_D2D1MakeRotateMatrix(rotate_angle, D2D1::Point2F(), &matrix);
			matrix.dx = baselineOriginX;
			matrix.dy = baselineOriginY;
			Microsoft::WRL::ComPtr<ID2D1TransformedGeometry> d2d1_transformed_geometry;
			hr = gHR = d2d1_factory->CreateTransformedGeometry(
				d2d1_rect_geometry.Get(),
				&matrix,
				&d2d1_transformed_geometry);
			if (FAILED(hr)) return hr;

			if (layer_stroke) d2d1_rt->DrawGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_outline.Get(), outline_width);
			if (layer_text) d2d1_rt->FillGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_fill.Get());

			return S_OK;
		}
		HRESULT WINAPI DrawStrikethrough(
			void* clientDrawingContext,
			FLOAT baselineOriginX,
			FLOAT baselineOriginY,
			DWRITE_GLYPH_ORIENTATION_ANGLE orientationAngle,
			DWRITE_STRIKETHROUGH const* strikethrough,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(clientDrawingEffect);

			HRESULT hr = S_OK;

			D2D1_RECT_F rect = D2D1::RectF(
				0,
				strikethrough->offset,
				strikethrough->width,
				strikethrough->offset + strikethrough->thickness
			);

			Microsoft::WRL::ComPtr<ID2D1RectangleGeometry> d2d1_rect_geometry;
			hr = gHR = d2d1_factory->CreateRectangleGeometry(&rect, &d2d1_rect_geometry);
			if (FAILED(hr)) return hr;

			// TODO: 为什么旋转方向是这样判断的？（与 Lua 侧一致）
			FLOAT rotate_angle = 0.0f;
			UNREFERENCED_PARAMETER(orientationAngle);
			switch (dwrite_text_layout->GetReadingDirection())
			{
			case DWRITE_READING_DIRECTION_LEFT_TO_RIGHT: rotate_angle = 0.0f; break;
			case DWRITE_READING_DIRECTION_TOP_TO_BOTTOM: rotate_angle = 90.0f; break;
			default: assert(false); break;
			}
			D2D1::Matrix3x2F matrix;
			DLL.api_D2D1MakeRotateMatrix(rotate_angle, D2D1::Point2F(), &matrix);
			matrix.dx = baselineOriginX;
			matrix.dy = baselineOriginY;
			Microsoft::WRL::ComPtr<ID2D1TransformedGeometry> d2d1_transformed_geometry;
			hr = gHR = d2d1_factory->CreateTransformedGeometry(
				d2d1_rect_geometry.Get(),
				&matrix,
				&d2d1_transformed_geometry);
			if (FAILED(hr)) return hr;

			if (layer_stroke) d2d1_rt->DrawGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_outline.Get(), outline_width);
			if (layer_text) d2d1_rt->FillGeometry(d2d1_transformed_geometry.Get(), d2d1_brush_fill.Get());

			return S_OK;
		}
		HRESULT WINAPI DrawInlineObject(
			void* clientDrawingContext,
			FLOAT originX,
			FLOAT originY,
			DWRITE_GLYPH_ORIENTATION_ANGLE orientationAngle,
			IDWriteInlineObject* inlineObject,
			BOOL isSideways,
			BOOL isRightToLeft,
			IUnknown* clientDrawingEffect) noexcept
		{
			UNREFERENCED_PARAMETER(clientDrawingContext);
			UNREFERENCED_PARAMETER(originX);
			UNREFERENCED_PARAMETER(originY);
			UNREFERENCED_PARAMETER(orientationAngle);
			UNREFERENCED_PARAMETER(inlineObject);
			UNREFERENCED_PARAMETER(isSideways);
			UNREFERENCED_PARAMETER(isRightToLeft);
			UNREFERENCED_PARAMETER(clientDrawingEffect);
			return E_NOTIMPL;
		}
	public:
		void WINAPI SetLayerEnable(BOOL text, BOOL stroke)
		{
			layer_text = text;
			layer_stroke = stroke;
		}
	public:
		DWriteTextRendererImplement(
			ID2D1Factory* factory,
			ID2D1RenderTarget* target,
			IDWriteTextLayout* layout,
			ID2D1Brush* outline,
			ID2D1Brush* fill,
			ID2D1StrokeStyle* stroke_style,
			FLOAT width)
			: d2d1_factory(factory)
			, d2d1_rt(target)
			, dwrite_text_layout(layout)
			, d2d1_brush_outline(outline)
			, d2d1_brush_fill(fill)
			, d2d1_stroke_style(stroke_style)
			, outline_width(width) {}
		~DWriteTextRendererImplement() {}
	};

	// ============ WIC 保存辅助（保存失败时删除残缺文件） ============

	class AutoDeleteFileWIC
	{
	public:
		AutoDeleteFileWIC(Microsoft::WRL::ComPtr<IWICStream>& hFile, std::wstring_view szFile) noexcept
			: m_filename(szFile), m_handle(hFile) {}
		~AutoDeleteFileWIC()
		{
			if (!m_filename.empty())
			{
				m_handle.Reset();
				DeleteFileW(m_filename.data());
			}
		}

		AutoDeleteFileWIC(const AutoDeleteFileWIC&) = delete;
		AutoDeleteFileWIC& operator=(const AutoDeleteFileWIC&) = delete;

		AutoDeleteFileWIC(const AutoDeleteFileWIC&&) = delete;
		AutoDeleteFileWIC& operator=(const AutoDeleteFileWIC&&) = delete;

		void clear() noexcept { m_filename = m_filename.substr(0, 0); }

	private:
		std::wstring_view m_filename;
		Microsoft::WRL::ComPtr<IWICStream>& m_handle;
	};

	// ============ 工厂单例（对应 Lua 侧 DirectWrite.Factory userdata） ============
	// 惰性初始化；进程退出时有意不销毁（避免引擎关闭阶段逆序释放 COM 对象）

	struct Factory
	{
		Microsoft::WRL::ComPtr<IWICImagingFactory> wic_factory;
		Microsoft::WRL::ComPtr<ID2D1Factory> d2d1_factory;
		Microsoft::WRL::ComPtr<IDWriteFactory> dwrite_factory;
		Microsoft::WRL::ComPtr<DWriteFontFileLoaderImplement> dwrite_font_file_loader;

		bool InitComponents()
		{
			HRESULT hr = S_OK;

			hr = gHR = CoCreateInstance(
				CLSID_WICImagingFactory2,
				NULL,
				CLSCTX_INPROC_SERVER,
				IID_PPV_ARGS(&wic_factory)
			);
			if (FAILED(hr))
			{
				hr = gHR = CoCreateInstance(
					CLSID_WICImagingFactory1,
					NULL,
					CLSCTX_INPROC_SERVER,
					IID_PPV_ARGS(&wic_factory)
				);
				if (FAILED(hr))
					return false;
			}

			hr = gHR = DLL.api_DWriteCreateFactory(
				DWRITE_FACTORY_TYPE_SHARED,
				__uuidof(IDWriteFactory),
				&dwrite_factory
			);
			if (FAILED(hr))
				return false;

			D2D1_FACTORY_OPTIONS d2d1_options = {
			#ifdef _DEBUG
				.debugLevel = D2D1_DEBUG_LEVEL_INFORMATION,
			#else
				.debugLevel = D2D1_DEBUG_LEVEL_NONE,
			#endif
			};
			hr = gHR = DLL.api_D2D1CreateFactory(
				D2D1_FACTORY_TYPE_SINGLE_THREADED,
				__uuidof(ID2D1Factory),
				&d2d1_options,
				&d2d1_factory
			);
			if (FAILED(hr))
				return false;

			dwrite_font_file_loader.Attach(new (std::nothrow) DWriteFontFileLoaderImplement());
			if (!dwrite_font_file_loader)
				return false;
			hr = gHR = dwrite_factory->RegisterFontFileLoader(dwrite_font_file_loader.Get());
			if (FAILED(hr))
				return false;

			return true;
		}

		Factory() {}
		~Factory()
		{
			if (dwrite_factory && dwrite_font_file_loader)
			{
				gHR = dwrite_factory->UnregisterFontFileLoader(dwrite_font_file_loader.Get());
			}
		}
	};

	[[nodiscard]] Factory* getFactory() noexcept
	{
		static Factory* instance = []() -> Factory* {
			auto* const core = new (std::nothrow) Factory();
			if (core && !core->InitComponents())
			{
				spdlog::error("[clr] DirectWrite factory initialization failed");
				delete core;
				return nullptr;
			}
			return core; // 有意不在进程退出时销毁
		}();
		return instance;
	}

	// ============ 句柄包装结构（对应 Lua 侧 userdata 结构） ============

	struct FontCollectionHandle_t
	{
		Microsoft::WRL::ComPtr<IDWriteFactory> dwrite_factory;
		Microsoft::WRL::ComPtr<DWriteFontFileLoaderImplement> dwrite_font_file_loader; // from core
		Microsoft::WRL::ComPtr<DWriteFontCollectionLoaderImplement> dwrite_font_collection_loader;
		Microsoft::WRL::ComPtr<IDWriteFontCollection> dwrite_font_collection;
		shared_string_list font_file_name_list;
		std::string name;

		bool InitComponents()
		{
			HRESULT hr = S_OK;

			std::stringstream ss;
			ss << this;
			name = ss.str();

			dwrite_font_collection_loader.Attach(new (std::nothrow) DWriteFontCollectionLoaderImplement());
			if (!dwrite_font_collection_loader)
				return false;
			dwrite_font_collection_loader->reset(
				dwrite_factory.Get(),
				dwrite_font_file_loader.Get(),
				font_file_name_list);

			hr = gHR = dwrite_factory->RegisterFontCollectionLoader(dwrite_font_collection_loader.Get());
			if (FAILED(hr)) return false;

			hr = gHR = dwrite_factory->CreateCustomFontCollection(
				dwrite_font_collection_loader.Get(),
				name.data(),
				(UINT32)name.size(),
				&dwrite_font_collection);
			if (FAILED(hr)) return false;

			return true;
		}

		FontCollectionHandle_t() {}
		~FontCollectionHandle_t()
		{
			if (dwrite_factory && dwrite_font_collection_loader)
			{
				gHR = dwrite_factory->UnregisterFontCollectionLoader(dwrite_font_collection_loader.Get());
			}
		}
	};
	struct TextFormatHandle_t
	{
		Microsoft::WRL::ComPtr<IDWriteTextFormat> dwrite_text_format;
	};
	struct TextLayoutHandle_t
	{
		Microsoft::WRL::ComPtr<IDWriteTextLayout> dwrite_text_layout;
	};
	struct TextRendererHandle_t
	{
		core::Color4B font_color{ 255, 255, 255, 255 };
		float outline_width{};
		core::Color4B outline_color{ 0, 0, 0, 255 };
		float shadow_radius{};
		float shadow_extend{};
		core::Color4B shadow_color{ 0, 0, 0, 255 };
	};

	template<typename T>
	[[nodiscard]] T* asHandle(uintptr_t const handle) noexcept {
		return reinterpret_cast<T*>(handle);
	}

	/// char16_t* -> wchar_t*（C# 侧 char* 传入 UTF-16）
	[[nodiscard]] wchar_t const* toWide(const char16_t* s) noexcept {
		return reinterpret_cast<wchar_t const*>(s);
	}

	// ============ 字体集调试信息（UTF-8，线程局部缓冲，C# 立即拷贝） ============

	thread_local std::string g_font_collection_debug_buffer;

	void printFontCollectionInfo(IDWriteLocalizedStrings* const names, std::string_view const indent, std::stringstream& string_buffer) {
		for (UINT32 name_idx = 0; name_idx < names->GetCount(); name_idx += 1) {
			UINT32 str_len = 0;

			if (FAILED(names->GetStringLength(name_idx, &str_len))) continue;
			std::wstring name(str_len + 1, L'\0');
			if (FAILED(names->GetString(name_idx, name.data(), str_len + 1))) continue;
			if (name.back() == L'\0') name.pop_back();

			if (FAILED(names->GetLocaleNameLength(name_idx, &str_len))) continue;
			std::wstring locale_name(str_len + 1, L'\0');
			if (FAILED(names->GetLocaleName(name_idx, locale_name.data(), str_len + 1))) continue;
			if (locale_name.back() == L'\0') locale_name.pop_back();

			string_buffer << indent << "[" << name_idx << "] (" << utf8::to_string(locale_name) << ") " << utf8::to_string(name) << '\n';
		}
	}
	void printFontCollectionInfo(IDWriteFontCollection* dwrite_font_collection, std::stringstream& string_buffer) {
		for (UINT32 ff_idx = 0; ff_idx < dwrite_font_collection->GetFontFamilyCount(); ff_idx += 1) {
			string_buffer << '[' << ff_idx << "] Font Family\n";

			Microsoft::WRL::ComPtr<IDWriteFontFamily> dwrite_font_family;
			if (FAILED(dwrite_font_collection->GetFontFamily(ff_idx, &dwrite_font_family))) {
				continue;
			}

			string_buffer << "    Name:\n";
			Microsoft::WRL::ComPtr<IDWriteLocalizedStrings> dwrite_font_family_names;
			if (SUCCEEDED(dwrite_font_family->GetFamilyNames(&dwrite_font_family_names))) {
				printFontCollectionInfo(dwrite_font_family_names.Get(), "        ", string_buffer);
			}

			string_buffer << "    Font:\n";
			for (UINT32 font_idx = 0; font_idx < dwrite_font_family->GetFontCount(); font_idx += 1) {
				string_buffer << "        [" << font_idx << "] Font\n";

				Microsoft::WRL::ComPtr<IDWriteFont> dwrite_font;
				if (FAILED(dwrite_font_family->GetFont(font_idx, &dwrite_font))) {
					continue;
				}

				string_buffer << "            Name:\n";
				Microsoft::WRL::ComPtr<IDWriteLocalizedStrings> dwrite_font_face_names;
				if (SUCCEEDED(dwrite_font->GetFaceNames(&dwrite_font_face_names))) {
					printFontCollectionInfo(dwrite_font_face_names.Get(), "                ", string_buffer);
				}

				string_buffer << "            Simulations: ";
				auto const font_sim = dwrite_font->GetSimulations();
				if (font_sim == DWRITE_FONT_SIMULATIONS_NONE) string_buffer << "None";
				if (font_sim & DWRITE_FONT_SIMULATIONS_BOLD) string_buffer << "Algorithmic Emboldening";
				if ((font_sim & DWRITE_FONT_SIMULATIONS_BOLD) && (font_sim & DWRITE_FONT_SIMULATIONS_OBLIQUE)) string_buffer << ", ";
				if (font_sim & DWRITE_FONT_SIMULATIONS_OBLIQUE) string_buffer << "Algorithmic Italicization";
				string_buffer << "\n";

				switch (dwrite_font->GetStretch())
				{
				case DWRITE_FONT_STRETCH_UNDEFINED:       string_buffer << "            Stretch: Not known (0)\n";       break;
				case DWRITE_FONT_STRETCH_ULTRA_CONDENSED: string_buffer << "            Stretch: Ultra-condensed (1)\n"; break;
				case DWRITE_FONT_STRETCH_EXTRA_CONDENSED: string_buffer << "            Stretch: Extra-condensed (2)\n"; break;
				case DWRITE_FONT_STRETCH_CONDENSED:       string_buffer << "            Stretch: Condensed (3)\n";       break;
				case DWRITE_FONT_STRETCH_SEMI_CONDENSED:  string_buffer << "            Stretch: Semi-condensed (4)\n";  break;
				case DWRITE_FONT_STRETCH_NORMAL:          string_buffer << "            Stretch: Normal/Medium (5)\n";   break;
				case DWRITE_FONT_STRETCH_SEMI_EXPANDED:   string_buffer << "            Stretch: Semi-expanded (6)\n";   break;
				case DWRITE_FONT_STRETCH_EXPANDED:        string_buffer << "            Stretch: Expanded (7)\n";        break;
				case DWRITE_FONT_STRETCH_EXTRA_EXPANDED:  string_buffer << "            Stretch: Extra-expanded (8)\n";  break;
				case DWRITE_FONT_STRETCH_ULTRA_EXPANDED:  string_buffer << "            Stretch: Ultra-expanded (9)\n";  break;
				default: assert(false); break;
				}

				switch (dwrite_font->GetStyle())
				{
				case DWRITE_FONT_STYLE_NORMAL:  string_buffer << "            Slope Style: Normal\n";  break;
				case DWRITE_FONT_STYLE_OBLIQUE: string_buffer << "            Slope Style: Oblique\n"; break;
				case DWRITE_FONT_STYLE_ITALIC:  string_buffer << "            Slope Style: Italic\n";  break;
				default: assert(false); break;
				}

				auto const font_weight = dwrite_font->GetWeight();
				switch (font_weight)
				{
				case DWRITE_FONT_WEIGHT_THIN:        string_buffer << "            Weight: Thin (100)\n";                    break;
				case DWRITE_FONT_WEIGHT_EXTRA_LIGHT: string_buffer << "            Weight: Extra-light/Ultra-light (200)\n"; break;
				case DWRITE_FONT_WEIGHT_LIGHT:       string_buffer << "            Weight: Light (300)\n";                   break;
				case DWRITE_FONT_WEIGHT_SEMI_LIGHT:  string_buffer << "            Weight: Semi-light (350)\n";              break;
				case DWRITE_FONT_WEIGHT_NORMAL:      string_buffer << "            Weight: Normal/Regular (400)\n";          break;
				case DWRITE_FONT_WEIGHT_MEDIUM:      string_buffer << "            Weight: Medium (500)\n";                  break;
				case DWRITE_FONT_WEIGHT_DEMI_BOLD:   string_buffer << "            Weight: Demi-bold/Semi-bold (600)\n";     break;
				case DWRITE_FONT_WEIGHT_BOLD:        string_buffer << "            Weight: Bold (700)\n";                    break;
				case DWRITE_FONT_WEIGHT_EXTRA_BOLD:  string_buffer << "            Weight: Extra-bold/Ultra-bold (800)\n";   break;
				case DWRITE_FONT_WEIGHT_BLACK:       string_buffer << "            Weight: Black/Heavy (900)\n";             break;
				case DWRITE_FONT_WEIGHT_EXTRA_BLACK: string_buffer << "            Weight: Extra-black/Ultra-black (950)\n"; break;
				default: string_buffer << "            Weight: " << (int)font_weight << "\n"; break;
				}

				string_buffer << "            Symbol Font: " << (dwrite_font->IsSymbolFont() ? "Yes" : "No") << "\n";
			}
		}
	}

	/// 圆角描边样式（Lua 侧 CreateTextureFromTextLayout / SaveTextLayoutToFile / Render 共用）
	[[nodiscard]] Microsoft::WRL::ComPtr<ID2D1StrokeStyle> createRoundStrokeStyle(ID2D1Factory* d2d1_factory)
	{
		Microsoft::WRL::ComPtr<ID2D1StrokeStyle> d2d1_stroke_style;
		HRESULT const hr = gHR = d2d1_factory->CreateStrokeStyle(D2D1::StrokeStyleProperties(
			D2D1_CAP_STYLE_ROUND,
			D2D1_CAP_STYLE_ROUND,
			D2D1_CAP_STYLE_ROUND,
			D2D1_LINE_JOIN_ROUND), NULL, 0, &d2d1_stroke_style);
		if (FAILED(hr))
			return nullptr;
		return d2d1_stroke_style;
	}
}

namespace luastg
{
	// ============ 模块函数 ============

	uintptr_t CLRBinding::dwrite_createFontCollection(const char** paths, uint32_t count)
	{
		Factory* const core = getFactory();
		if (!core)
			return 0;

		auto* const font_collection = new (std::nothrow) FontCollectionHandle_t();
		if (!font_collection)
			return 0;

		font_collection->dwrite_factory = core->dwrite_factory;
		font_collection->dwrite_font_file_loader = core->dwrite_font_file_loader;
		font_collection->font_file_name_list = std::make_shared<std::vector<std::string>>();
		if (paths && count > 0)
		{
			font_collection->font_file_name_list->reserve(count);
			for (uint32_t i = 0; i < count; i += 1)
			{
				if (!paths[i])
				{
					delete font_collection;
					return 0;
				}
				font_collection->font_file_name_list->emplace_back(paths[i]);
			}
		}
		if (!font_collection->InitComponents())
		{
			spdlog::error("[clr] DirectWrite.CreateFontCollection init failed");
			delete font_collection;
			return 0;
		}

		return reinterpret_cast<uintptr_t>(font_collection);
	}

	uintptr_t CLRBinding::dwrite_createTextFormat(
		const char16_t* family_name, uintptr_t font_collection,
		int32_t font_weight, int32_t font_style, int32_t font_stretch,
		float font_size, const char16_t* locale_name)
	{
		Factory* const core = getFactory();
		if (!core || !family_name || !locale_name)
			return 0;

		FontCollectionHandle_t* collection = nullptr;
		if (font_collection != 0)
		{
			collection = asHandle<FontCollectionHandle_t>(font_collection);
			if (!collection || !collection->dwrite_font_collection)
				return 0;
		}

		auto* const text_format = new (std::nothrow) TextFormatHandle_t();
		if (!text_format)
			return 0;

		HRESULT const hr = gHR = core->dwrite_factory->CreateTextFormat(
			toWide(family_name),
			collection ? collection->dwrite_font_collection.Get() : NULL,
			static_cast<DWRITE_FONT_WEIGHT>(font_weight),
			static_cast<DWRITE_FONT_STYLE>(font_style),
			static_cast<DWRITE_FONT_STRETCH>(font_stretch),
			font_size,
			toWide(locale_name),
			&text_format->dwrite_text_format);
		if (FAILED(hr))
		{
			spdlog::error("[clr] DirectWrite.CreateTextFormat failed, hr = {:#x}", (uint32_t)hr);
			delete text_format;
			return 0;
		}

		return reinterpret_cast<uintptr_t>(text_format);
	}

	uintptr_t CLRBinding::dwrite_createTextLayout(
		const char16_t* text, uint32_t length, uintptr_t text_format,
		float max_width, float max_height)
	{
		Factory* const core = getFactory();
		if (!core)
			return 0;
		auto* const format = asHandle<TextFormatHandle_t>(text_format);
		if (!format || !format->dwrite_text_format)
			return 0;

		auto* const text_layout = new (std::nothrow) TextLayoutHandle_t();
		if (!text_layout)
			return 0;

		std::wstring const wide_string(
			text ? reinterpret_cast<wchar_t const*>(text) : L"",
			text ? length : 0u);

		HRESULT const hr = gHR = core->dwrite_factory->CreateTextLayout(
			wide_string.data(),
			(UINT32)wide_string.size(),
			format->dwrite_text_format.Get(),
			max_width,
			max_height,
			&text_layout->dwrite_text_layout);
		if (FAILED(hr))
		{
			spdlog::error("[clr] DirectWrite.CreateTextLayout failed, hr = {:#x}", (uint32_t)hr);
			delete text_layout;
			return 0;
		}

		return reinterpret_cast<uintptr_t>(text_layout);
	}

	uintptr_t CLRBinding::dwrite_createTextRenderer()
	{
		auto* const text_renderer = new (std::nothrow) TextRendererHandle_t();
		return text_renderer ? reinterpret_cast<uintptr_t>(text_renderer) : 0;
	}

	uint8_t CLRBinding::dwrite_createTextureFromTextLayout(
		uintptr_t text_layout, int32_t pool_type, const char* texture_name,
		float outline_width, uint32_t font_color_argb, uint32_t outline_color_argb)
	{
		Factory* const core = getFactory();
		if (!core || !texture_name)
			return 8;
		auto* const layout = asHandle<TextLayoutHandle_t>(text_layout);
		if (!layout || !layout->dwrite_text_layout)
			return 8;

		HRESULT hr = S_OK;

		core::Color4B const font_color(font_color_argb);
		core::Color4B const outline_color(outline_color_argb);

		// pre check

		luastg::ResourcePool* pool{};
		if (pool_type == 1)
			pool = LRES.GetResourcePool(luastg::ResourcePoolType::Global);
		else if (pool_type == 2)
			pool = LRES.GetResourcePool(luastg::ResourcePoolType::Stage);
		else
			return 1;
		if (pool->GetTexture(texture_name))
			return 2;

		// bitmap

		auto const texture_canvas_width = std::ceil(layout->dwrite_text_layout->GetMaxWidth() + 2.0f * outline_width);
		auto const texture_canvas_height = std::ceil(layout->dwrite_text_layout->GetMaxHeight() + 2.0f * outline_width);

		core::ImageDescription canvas_image_description{};
		canvas_image_description.size.x = static_cast<uint32_t>(texture_canvas_width);
		canvas_image_description.size.y = static_cast<uint32_t>(texture_canvas_height);
		canvas_image_description.format = core::ImageFormat::b8g8r8a8_normalized;
		canvas_image_description.color_space = core::ImageColorSpace::srgb_gamma_2_2;
		canvas_image_description.alpha_mode = core::ImageAlphaMode::premultiplied;

		core::SmartReference<core::IImage> canvas_image;
		if (!core::ImageFactory::create(canvas_image_description, canvas_image.put())) {
			return 3;
		}

		core::ImageMappedBuffer canvas_buffer{};
		if (!canvas_image->map(canvas_buffer)) {
			return 3;
		}

		Microsoft::WRL::ComPtr<IWICBitmap> my_bitmap; // GUID_WICPixelFormat32bppPBGRA
		if (!core::WicImage::createFromImage(canvas_image.get(), &canvas_buffer, reinterpret_cast<void**>(my_bitmap.GetAddressOf()))) {
			canvas_image->unmap();
			return 3;
		}

		// d2d1 rasterizer

		Microsoft::WRL::ComPtr<ID2D1RenderTarget> d2d1_rt;
		hr = gHR = core->d2d1_factory->CreateWicBitmapRenderTarget(
			my_bitmap.Get(),
			D2D1::RenderTargetProperties(),
			&d2d1_rt);
		if (FAILED(hr))
		{
			canvas_image->unmap();
			return 4;
		}

		Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> d2d1_pen;
		hr = gHR = d2d1_rt->CreateSolidColorBrush(Color4BToColorF(font_color), &d2d1_pen);
		if (FAILED(hr))
		{
			canvas_image->unmap();
			return 4;
		}

		// rasterize

		d2d1_rt->BeginDraw();
		d2d1_rt->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.0f));
		d2d1_rt->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
		if (outline_width > 0.0001f)
		{
			Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> d2d1_pen2;
			hr = gHR = d2d1_rt->CreateSolidColorBrush(Color4BToColorF(outline_color), &d2d1_pen2);
			if (FAILED(hr))
			{
				canvas_image->unmap();
				return 4;
			}

			Microsoft::WRL::ComPtr<ID2D1StrokeStyle> d2d1_stroke_style = createRoundStrokeStyle(core->d2d1_factory.Get());
			if (!d2d1_stroke_style)
			{
				canvas_image->unmap();
				return 4;
			}

			DWriteTextRendererImplement renderer(
				core->d2d1_factory.Get(),
				d2d1_rt.Get(),
				layout->dwrite_text_layout.Get(),
				d2d1_pen2.Get(),
				d2d1_pen.Get(),
				d2d1_stroke_style.Get(),
				outline_width);

			renderer.SetLayerEnable(FALSE, TRUE);
			hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, outline_width, outline_width);
			if (FAILED(hr))
			{
				canvas_image->unmap();
				return 5;
			}
			renderer.SetLayerEnable(TRUE, FALSE);
			hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, outline_width, outline_width);
			if (FAILED(hr))
			{
				canvas_image->unmap();
				return 5;
			}
		}
		else
		{
			d2d1_rt->DrawTextLayout(D2D1::Point2F(0.0f, 0.0f), layout->dwrite_text_layout.Get(), d2d1_pen.Get());
		}
		hr = gHR = d2d1_rt->EndDraw();
		if (FAILED(hr))
		{
			canvas_image->unmap();
			return 5;
		}

		// create texture

		if (!pool->CreateTexture(texture_name, (int)texture_canvas_width, (int)texture_canvas_height))
			return 6;

		auto p_texres = pool->GetTexture(texture_name);
		auto* p_texture = p_texres->GetTexture();

		// upload data

		p_texture->setPremultipliedAlpha(true);
		if (!p_texture->update(
			core::RectU(0, 0, (uint32_t)texture_canvas_width, (uint32_t)texture_canvas_height),
			canvas_buffer.data, canvas_buffer.stride
		)) {
			canvas_image->unmap();
			return 7;
		}
		canvas_image->unmap();
		canvas_image->setReadOnly();
		p_texture->setImage(canvas_image.get());

		return 0;
	}

	uint8_t CLRBinding::dwrite_saveTextLayoutToFile(uintptr_t text_layout, const char16_t* file_path, float outline_width, uint8_t use_outline)
	{
		Factory* const core = getFactory();
		if (!core || !file_path)
			return 5;
		auto* const layout = asHandle<TextLayoutHandle_t>(text_layout);
		if (!layout || !layout->dwrite_text_layout)
			return 5;

		HRESULT hr = S_OK;

		// bitmap

		auto const texture_canvas_width = std::ceil(layout->dwrite_text_layout->GetMaxWidth() + 2.0f * outline_width);
		auto const texture_canvas_height = std::ceil(layout->dwrite_text_layout->GetMaxHeight() + 2.0f * outline_width);

		Microsoft::WRL::ComPtr<IWICBitmap> wic_bitmap;
		hr = gHR = core->wic_factory->CreateBitmap(
			(UINT)texture_canvas_width,
			(UINT)texture_canvas_height,
			GUID_WICPixelFormat32bppPBGRA,
			WICBitmapCacheOnDemand,
			&wic_bitmap);
		if (FAILED(hr))
			return 1;

		// d2d1 rasterizer

		Microsoft::WRL::ComPtr<ID2D1RenderTarget> d2d1_rt;
		hr = gHR = core->d2d1_factory->CreateWicBitmapRenderTarget(
			wic_bitmap.Get(),
			D2D1::RenderTargetProperties(),
			&d2d1_rt);
		if (FAILED(hr))
			return 2;

		Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> d2d1_pen;
		hr = gHR = d2d1_rt->CreateSolidColorBrush(D2D1::ColorF(1.0f, 1.0f, 1.0f), &d2d1_pen);
		if (FAILED(hr))
			return 2;

		// rasterize

		d2d1_rt->BeginDraw();
		d2d1_rt->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.0f));
		d2d1_rt->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
		if (use_outline != 0)
		{
			Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> d2d1_pen2;
			hr = gHR = d2d1_rt->CreateSolidColorBrush(D2D1::ColorF(0.0f, 0.0f, 0.0f), &d2d1_pen2);
			if (FAILED(hr))
				return 2;

			Microsoft::WRL::ComPtr<ID2D1StrokeStyle> d2d1_stroke_style = createRoundStrokeStyle(core->d2d1_factory.Get());
			if (!d2d1_stroke_style)
				return 2;

			DWriteTextRendererImplement renderer(
				core->d2d1_factory.Get(),
				d2d1_rt.Get(),
				layout->dwrite_text_layout.Get(),
				d2d1_pen2.Get(),
				d2d1_pen.Get(),
				d2d1_stroke_style.Get(),
				outline_width);

			renderer.SetLayerEnable(FALSE, TRUE);
			hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, outline_width, outline_width);
			if (FAILED(hr))
				return 3;
			renderer.SetLayerEnable(TRUE, FALSE);
			hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, outline_width, outline_width);
			if (FAILED(hr))
				return 3;
		}
		else
		{
			d2d1_rt->DrawTextLayout(D2D1::Point2F(0.0f, 0.0f), layout->dwrite_text_layout.Get(), d2d1_pen.Get());
		}
		hr = gHR = d2d1_rt->EndDraw();
		if (FAILED(hr))
			return 3;

		// save

		Microsoft::WRL::ComPtr<IWICStream> wic_stream;
		hr = gHR = core->wic_factory->CreateStream(&wic_stream);
		if (FAILED(hr))
			return 4;

		std::wstring const wide_file_path(toWide(file_path));
		hr = gHR = wic_stream->InitializeFromFilename(wide_file_path.c_str(), GENERIC_WRITE);
		if (FAILED(hr))
			return 4;

		AutoDeleteFileWIC auto_delete(wic_stream, wide_file_path);

		Microsoft::WRL::ComPtr<IWICBitmapEncoder> wic_bitmap_encoder;
		hr = gHR = core->wic_factory->CreateEncoder(GUID_ContainerFormatPng, NULL, &wic_bitmap_encoder);
		if (FAILED(hr))
			return 4;

		hr = gHR = wic_bitmap_encoder->Initialize(wic_stream.Get(), WICBitmapEncoderNoCache);
		if (FAILED(hr))
			return 4;

		Microsoft::WRL::ComPtr<IWICBitmapFrameEncode> wic_bitmap_frame_encode;
		Microsoft::WRL::ComPtr<IPropertyBag2> property_bag;
		hr = gHR = wic_bitmap_encoder->CreateNewFrame(&wic_bitmap_frame_encode, &property_bag);
		if (FAILED(hr))
			return 4;

		hr = gHR = wic_bitmap_frame_encode->Initialize(property_bag.Get());
		if (FAILED(hr))
			return 4;

		hr = gHR = wic_bitmap_frame_encode->SetSize((UINT)texture_canvas_width, (UINT)texture_canvas_height);
		if (FAILED(hr))
			return 4;

		hr = gHR = wic_bitmap_frame_encode->SetResolution(72, 72);
		if (FAILED(hr))
			return 4;

		WICPixelFormatGUID wic_pixel_format = GUID_WICPixelFormat32bppBGRA;
		hr = gHR = wic_bitmap_frame_encode->SetPixelFormat(&wic_pixel_format);
		if (FAILED(hr))
			return 4;

		Microsoft::WRL::ComPtr<IWICMetadataQueryWriter> metawriter;
		if (SUCCEEDED(wic_bitmap_frame_encode->GetMetadataQueryWriter(&metawriter)))
		{
			PROPVARIANT value;
			PropVariantInit(&value);

			// Set Software name
			value.vt = VT_LPSTR;
			value.pszVal = const_cast<char*>("DirectXTK");
			std::ignore = metawriter->SetMetadataByName(L"/tEXt/{str=Software}", &value);

			// add gAMA chunk with gamma 1.0
			value.vt = VT_UI4;
			value.uintVal = 100000; // gama value * 100,000 -- i.e. gamma 1.0
			std::ignore = metawriter->SetMetadataByName(L"/gAMA/ImageGamma", &value);

			// remove sRGB chunk which is added by default.
			std::ignore = metawriter->RemoveMetadataByName(L"/sRGB/RenderingIntent");

			PropVariantClear(&value);
		}

		hr = gHR = wic_bitmap_frame_encode->WriteSource(wic_bitmap.Get(), NULL);
		if (FAILED(hr))
			return 4;

		hr = gHR = wic_bitmap_frame_encode->Commit();
		if (FAILED(hr))
			return 4;

		hr = gHR = wic_bitmap_encoder->Commit();
		if (FAILED(hr))
			return 4;

		auto_delete.clear();

		return 0;
	}

	// ============ FontCollection ============

	void CLRBinding::dwrite_fontCollectionDestroy(uintptr_t font_collection)
	{
		delete asHandle<FontCollectionHandle_t>(font_collection);
	}

	const char* CLRBinding::dwrite_fontCollectionGetDebugInformation(uintptr_t font_collection)
	{
		auto* const h = asHandle<FontCollectionHandle_t>(font_collection);
		if (!h || !h->dwrite_font_collection)
		{
			g_font_collection_debug_buffer.clear();
			return "";
		}
		std::stringstream ss;
		printFontCollectionInfo(h->dwrite_font_collection.Get(), ss);
		g_font_collection_debug_buffer = ss.str();
		return g_font_collection_debug_buffer.c_str();
	}

	// ============ TextFormat ============

	void CLRBinding::dwrite_textFormatDestroy(uintptr_t text_format)
	{
		delete asHandle<TextFormatHandle_t>(text_format);
	}

	// ============ TextLayout ============

	void CLRBinding::dwrite_textLayoutDestroy(uintptr_t text_layout)
	{
		delete asHandle<TextLayoutHandle_t>(text_layout);
	}

	uint8_t CLRBinding::dwrite_textLayoutSetFontCollection(uintptr_t text_layout, uintptr_t font_collection, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;
		auto* const collection = asHandle<FontCollectionHandle_t>(font_collection);
		if (!collection || !collection->dwrite_font_collection)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetFontCollection(
			collection->dwrite_font_collection.Get(),
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetFontFamilyName(uintptr_t text_layout, const char16_t* name, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout || !name)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetFontFamilyName(
			toWide(name),
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetLocaleName(uintptr_t text_layout, const char16_t* name, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout || !name)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetLocaleName(
			toWide(name),
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetFontSize(uintptr_t text_layout, float font_size, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetFontSize(
			font_size,
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetFontStyle(uintptr_t text_layout, int32_t font_style, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetFontStyle(
			static_cast<DWRITE_FONT_STYLE>(font_style),
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetFontWeight(uintptr_t text_layout, int32_t font_weight, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetFontWeight(
			static_cast<DWRITE_FONT_WEIGHT>(font_weight),
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetFontStretch(uintptr_t text_layout, int32_t font_stretch, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetFontStretch(
			static_cast<DWRITE_FONT_STRETCH>(font_stretch),
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetStrikethrough(uintptr_t text_layout, uint8_t enable, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetStrikethrough(
			enable != 0,
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetUnderline(uintptr_t text_layout, uint8_t enable, uint32_t position, uint32_t length)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetUnderline(
			enable != 0,
			DWRITE_TEXT_RANGE{
				.startPosition = position,
				.length = length,
			});
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetIncrementalTabStop(uintptr_t text_layout, float tab_size)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetIncrementalTabStop(tab_size);
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetLineSpacing(uintptr_t text_layout, int32_t method, float line_spacing, float baseline)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetLineSpacing(
			static_cast<DWRITE_LINE_SPACING_METHOD>(method), line_spacing, baseline);
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetTextAlignment(uintptr_t text_layout, int32_t align)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetTextAlignment(static_cast<DWRITE_TEXT_ALIGNMENT>(align));
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetParagraphAlignment(uintptr_t text_layout, int32_t align)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetParagraphAlignment(static_cast<DWRITE_PARAGRAPH_ALIGNMENT>(align));
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetFlowDirection(uintptr_t text_layout, int32_t direction)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetFlowDirection(static_cast<DWRITE_FLOW_DIRECTION>(direction));
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetReadingDirection(uintptr_t text_layout, int32_t direction)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetReadingDirection(static_cast<DWRITE_READING_DIRECTION>(direction));
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetWordWrapping(uintptr_t text_layout, int32_t wrapping)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetWordWrapping(static_cast<DWRITE_WORD_WRAPPING>(wrapping));
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetMaxWidth(uintptr_t text_layout, float max_width)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetMaxWidth(max_width);
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutSetMaxHeight(uintptr_t text_layout, float max_height)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;

		HRESULT const hr = gHR = h->dwrite_text_layout->SetMaxHeight(max_height);
		return FAILED(hr) ? 1 : 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutDetermineMinWidth(uintptr_t text_layout, float* out_min_width)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout)
			return 2;
		if (!out_min_width)
			return 2;

		FLOAT min_width = 0.0f;
		HRESULT const hr = gHR = h->dwrite_text_layout->DetermineMinWidth(&min_width);
		if (FAILED(hr))
			return 1;
		*out_min_width = min_width;
		return 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutGetMetrics(uintptr_t text_layout, double* out_metrics)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout || !out_metrics)
			return 2;

		DWRITE_TEXT_METRICS metrics{};
		HRESULT const hr = gHR = h->dwrite_text_layout->GetMetrics(&metrics);
		if (FAILED(hr))
			return 1;
		// 字段顺序与 C# 侧 TextMetrics 一致
		out_metrics[0] = metrics.left;
		out_metrics[1] = metrics.top;
		out_metrics[2] = metrics.width;
		out_metrics[3] = metrics.widthIncludingTrailingWhitespace;
		out_metrics[4] = metrics.height;
		out_metrics[5] = metrics.layoutWidth;
		out_metrics[6] = metrics.layoutHeight;
		out_metrics[7] = static_cast<double>(metrics.maxBidiReorderingDepth);
		out_metrics[8] = static_cast<double>(metrics.lineCount);
		return 0;
	}

	uint8_t CLRBinding::dwrite_textLayoutGetOverhangMetrics(uintptr_t text_layout, double* out_metrics)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		if (!h || !h->dwrite_text_layout || !out_metrics)
			return 2;

		DWRITE_OVERHANG_METRICS metrics{};
		HRESULT const hr = gHR = h->dwrite_text_layout->GetOverhangMetrics(&metrics);
		if (FAILED(hr))
			return 1;
		// 字段顺序与 C# 侧 OverhangMetrics 一致
		out_metrics[0] = metrics.left;
		out_metrics[1] = metrics.top;
		out_metrics[2] = metrics.right;
		out_metrics[3] = metrics.bottom;
		return 0;
	}

	float CLRBinding::dwrite_textLayoutGetMaxHeight(uintptr_t text_layout)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		return (h && h->dwrite_text_layout) ? h->dwrite_text_layout->GetMaxHeight() : 0.0f;
	}

	float CLRBinding::dwrite_textLayoutGetMaxWidth(uintptr_t text_layout)
	{
		auto* const h = asHandle<TextLayoutHandle_t>(text_layout);
		return (h && h->dwrite_text_layout) ? h->dwrite_text_layout->GetMaxWidth() : 0.0f;
	}

	// ============ TextRenderer ============

	void CLRBinding::dwrite_textRendererDestroy(uintptr_t text_renderer)
	{
		delete asHandle<TextRendererHandle_t>(text_renderer);
	}

	void CLRBinding::dwrite_textRendererSetTextColor(uintptr_t text_renderer, uint32_t argb)
	{
		auto* const h = asHandle<TextRendererHandle_t>(text_renderer);
		if (!h)
			return;
		h->font_color = core::Color4B(argb);
	}

	void CLRBinding::dwrite_textRendererSetTextOutlineColor(uintptr_t text_renderer, uint32_t argb)
	{
		auto* const h = asHandle<TextRendererHandle_t>(text_renderer);
		if (!h)
			return;
		h->outline_color = core::Color4B(argb);
	}

	void CLRBinding::dwrite_textRendererSetTextOutlineWidth(uintptr_t text_renderer, float width)
	{
		auto* const h = asHandle<TextRendererHandle_t>(text_renderer);
		if (!h)
			return;
		h->outline_width = width;
	}

	void CLRBinding::dwrite_textRendererSetShadowColor(uintptr_t text_renderer, uint32_t argb)
	{
		auto* const h = asHandle<TextRendererHandle_t>(text_renderer);
		if (!h)
			return;
		h->shadow_color = core::Color4B(argb);
	}

	void CLRBinding::dwrite_textRendererSetShadowRadius(uintptr_t text_renderer, float radius)
	{
		auto* const h = asHandle<TextRendererHandle_t>(text_renderer);
		if (!h)
			return;
		h->shadow_radius = radius;
	}

	void CLRBinding::dwrite_textRendererSetShadowExtend(uintptr_t text_renderer, float extend)
	{
		auto* const h = asHandle<TextRendererHandle_t>(text_renderer);
		if (!h)
			return;
		h->shadow_extend = extend;
	}

	uint8_t CLRBinding::dwrite_textRendererRender(uintptr_t text_renderer, const char* texture_name, uintptr_t text_layout, float offset_x, float offset_y)
	{
		auto* const self = asHandle<TextRendererHandle_t>(text_renderer);
		auto* const layout = asHandle<TextLayoutHandle_t>(text_layout);
		if (!self || !layout || !layout->dwrite_text_layout || !texture_name)
			return 4;

		auto const tex_res = LRES.FindTexture(texture_name);
		if (!tex_res)
		{
			return 1;
		}
		if (!tex_res->IsRenderTarget())
		{
			return 2;
		}

		// 获取渲染器和渲染目标

		HRESULT hr = S_OK;

		Microsoft::WRL::ComPtr<ID2D1DeviceContext> d2d1_device_context;
		d2d1_device_context = (ID2D1DeviceContext*)LAPP.getGraphicsDevice()->getNativeRendererHandle();
		assert(d2d1_device_context);

		Microsoft::WRL::ComPtr<ID2D1Bitmap1> d2d1_bitmap_target;
		d2d1_bitmap_target = (ID2D1Bitmap1*)tex_res->GetRenderTarget()->getNativeBitmap();
		assert(d2d1_bitmap_target);

		// 创建画笔

		Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> d2d1_font_color;
		hr = gHR = d2d1_device_context->CreateSolidColorBrush(Color4BToColorF(self->font_color), &d2d1_font_color);
		if (FAILED(hr))
			return 3;

		// 启动渲染，配置初始参数

		d2d1_device_context->BeginDraw();
		d2d1_device_context->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
		d2d1_device_context->SetTarget(d2d1_bitmap_target.Get());

		// 阴影

		Microsoft::WRL::ComPtr<ID2D1Bitmap1> d2d1_bitmap_text;
		if (self->shadow_radius > 0.0001f)
		{
			// 阴影效果

			Microsoft::WRL::ComPtr<ID2D1Effect> d2d1_effect_shadow;
			hr = gHR = d2d1_device_context->CreateEffect(CLSID_D2D1Shadow, &d2d1_effect_shadow);
			if (FAILED(hr))
				return 3;

			d2d1_effect_shadow->SetValue(D2D1_SHADOW_PROP_COLOR, Color4BToColorF(self->shadow_color));

			if (self->shadow_extend > 0.0001f)
			{
				// 临时的位图

				auto const extend_width = self->shadow_radius * self->shadow_extend;

				hr = gHR = d2d1_device_context->CreateBitmap(
					D2D1::SizeU(
						(UINT32)std::ceil(layout->dwrite_text_layout->GetMaxWidth() + 2.0f * extend_width),
						(UINT32)std::ceil(layout->dwrite_text_layout->GetMaxHeight() + 2.0f * extend_width)
					),
					NULL, 0,
					D2D1::BitmapProperties1(
						D2D1_BITMAP_OPTIONS_TARGET,
						D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED)
					),
					&d2d1_bitmap_text);
				if (FAILED(hr))
					return 3;

				// 阴影颜色

				Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> d2d1_shadow_color;
				hr = gHR = d2d1_device_context->CreateSolidColorBrush(Color4BToColorF(self->shadow_color), &d2d1_shadow_color);
				if (FAILED(hr))
					return 3;

				// 获取工厂

				Microsoft::WRL::ComPtr<ID2D1Factory> d2d1_factory;
				d2d1_device_context->GetFactory(&d2d1_factory);

				// 描边样式

				Microsoft::WRL::ComPtr<ID2D1StrokeStyle> d2d1_stroke_style = createRoundStrokeStyle(d2d1_factory.Get());
				if (!d2d1_stroke_style)
					return 3;

				// 配置自定义描边渲染器

				DWriteTextRendererImplement renderer(
					d2d1_factory.Get(),
					d2d1_device_context.Get(),
					layout->dwrite_text_layout.Get(),
					d2d1_shadow_color.Get(), // 使用阴影颜色
					d2d1_shadow_color.Get(), // 使用阴影颜色
					d2d1_stroke_style.Get(),
					extend_width * 2.0f); // 使用拓展宽度

				// 绘制描边文本到位图上

				d2d1_device_context->SetTarget(d2d1_bitmap_text.Get());
				d2d1_device_context->Clear(D2D1::ColorF(D2D1::ColorF::Black, 0.0f));
				renderer.SetLayerEnable(FALSE, TRUE);
				hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, extend_width, extend_width);
				if (FAILED(hr))
					return 3;
				renderer.SetLayerEnable(TRUE, FALSE);
				hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, extend_width, extend_width);
				if (FAILED(hr))
					return 3;

				// 绘制阴影效果

				auto const now_shadow_radius = 1.0f * (self->shadow_radius - extend_width);

				d2d1_device_context->SetTarget(d2d1_bitmap_target.Get());
				d2d1_effect_shadow->SetValue(D2D1_SHADOW_PROP_BLUR_STANDARD_DEVIATION, now_shadow_radius / 3.0f);
				d2d1_effect_shadow->SetInput(0, d2d1_bitmap_text.Get());
				d2d1_device_context->DrawImage(
					d2d1_effect_shadow.Get(),
					D2D1::Point2F(offset_x - extend_width, offset_y - extend_width));

				// 这个位图不能再使用了，清理掉

				d2d1_bitmap_text.Reset();
			}
			else
			{
				// 临时的位图

				hr = gHR = d2d1_device_context->CreateBitmap(
					D2D1::SizeU(
						(UINT32)std::ceil(layout->dwrite_text_layout->GetMaxWidth()),
						(UINT32)std::ceil(layout->dwrite_text_layout->GetMaxHeight())
					),
					NULL, 0,
					D2D1::BitmapProperties1(
						D2D1_BITMAP_OPTIONS_TARGET,
						D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED)
					),
					&d2d1_bitmap_text);
				if (FAILED(hr))
					return 3;

				// 绘制文本到位图上

				d2d1_device_context->SetTarget(d2d1_bitmap_text.Get());
				d2d1_device_context->Clear(D2D1::ColorF(D2D1::ColorF::Black, 0.0f));
				d2d1_device_context->DrawTextLayout(
					D2D1::Point2F(),
					layout->dwrite_text_layout.Get(),
					d2d1_font_color.Get());

				// 绘制阴影效果

				d2d1_device_context->SetTarget(d2d1_bitmap_target.Get());
				d2d1_effect_shadow->SetValue(D2D1_SHADOW_PROP_BLUR_STANDARD_DEVIATION, self->shadow_radius / 3.0f);
				d2d1_effect_shadow->SetInput(0, d2d1_bitmap_text.Get());
				d2d1_device_context->DrawImage(
					d2d1_effect_shadow.Get(),
					D2D1::Point2F(offset_x, offset_y));
			}
		}

		// 描边

		if (self->outline_width > 0.0001f)
		{
			// 描边颜色

			Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> d2d1_outline_color;
			hr = gHR = d2d1_device_context->CreateSolidColorBrush(Color4BToColorF(self->outline_color), &d2d1_outline_color);
			if (FAILED(hr))
				return 3;

			// 获取工厂

			Microsoft::WRL::ComPtr<ID2D1Factory> d2d1_factory;
			d2d1_device_context->GetFactory(&d2d1_factory);

			// 描边样式

			Microsoft::WRL::ComPtr<ID2D1StrokeStyle> d2d1_stroke_style = createRoundStrokeStyle(d2d1_factory.Get());
			if (!d2d1_stroke_style)
				return 3;

			// 配置自定义描边渲染器

			DWriteTextRendererImplement renderer(
				d2d1_factory.Get(),
				d2d1_device_context.Get(),
				layout->dwrite_text_layout.Get(),
				d2d1_outline_color.Get(),
				d2d1_font_color.Get(),
				d2d1_stroke_style.Get(),
				self->outline_width * 2.0f);

			// 绘制描边文本

			renderer.SetLayerEnable(FALSE, TRUE);
			hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, offset_x, offset_y);
			if (FAILED(hr))
				return 3;
			if (d2d1_bitmap_text)
			{
				d2d1_device_context->DrawImage(d2d1_bitmap_text.Get(), D2D1::Point2F(offset_x, offset_y));
			}
			else
			{
				renderer.SetLayerEnable(TRUE, FALSE);
				hr = gHR = layout->dwrite_text_layout->Draw(NULL, &renderer, offset_x, offset_y);
				if (FAILED(hr))
					return 3;
			}
		}

		// 不描边的情况

		else
		{
			if (d2d1_bitmap_text)
			{
				d2d1_device_context->DrawImage(d2d1_bitmap_text.Get(), D2D1::Point2F(offset_x, offset_y));
			}
			else
			{
				d2d1_device_context->DrawTextLayout(
					D2D1::Point2F(offset_x, offset_y),
					layout->dwrite_text_layout.Get(),
					d2d1_font_color.Get());
			}
		}

		// 结束渲染

		d2d1_device_context->SetTarget(NULL);
		hr = gHR = d2d1_device_context->EndDraw();
		if (FAILED(hr))
			return 3;

		return 0;
	}
}
