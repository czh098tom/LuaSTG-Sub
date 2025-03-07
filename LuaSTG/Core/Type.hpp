#pragma once
#include <cstdint>
#include <cmath>
#include <limits>
#include <string_view>
#include <string>

namespace Core
{
	// 二维向量

	template<typename T>
	struct Vector2
	{
		T x{};
		T y{};

		Vector2() noexcept : x(0), y(0) {}
		Vector2(T const x, T const y) noexcept : x(x), y(y) {}

		inline Vector2 operator+(Vector2 const& r) const noexcept { return Vector2(x + r.x, y + r.y); }
		inline Vector2 operator-(Vector2 const& r) const noexcept { return Vector2(x - r.x, y - r.y); }
		inline Vector2 operator*(Vector2 const& r) const noexcept { return Vector2(x * r.x, y * r.y); }
		inline Vector2 operator/(Vector2 const& r) const noexcept { return Vector2(x / r.x, y / r.y); }

		inline Vector2 operator+(T const r) const noexcept { return Vector2(x + r, y + r); }
		inline Vector2 operator-(T const r) const noexcept { return Vector2(x - r, y - r); }
		inline Vector2 operator*(T const r) const noexcept { return Vector2(x * r, y * r); }
		inline Vector2 operator/(T const r) const noexcept { return Vector2(x / r, y / r); }

		inline Vector2 operator-() const noexcept { return Vector2(-x, -y); }

		inline Vector2& operator+=(Vector2 const& r) noexcept { x += r.x; y += r.y; return *this; }
		inline Vector2& operator-=(Vector2 const& r) noexcept { x -= r.x; y -= r.y; return *this; }
		inline Vector2& operator*=(Vector2 const& r) noexcept { x *= r.x; y *= r.y; return *this; }
		inline Vector2& operator/=(Vector2 const& r) noexcept { x /= r.x; y /= r.y; return *this; }

		inline Vector2& operator+=(T const r) noexcept { x += r; y += r; return *this; }
		inline Vector2& operator-=(T const r) noexcept { x -= r; y -= r; return *this; }
		inline Vector2& operator*=(T const r) noexcept { x *= r; y *= r; return *this; }
		inline Vector2& operator/=(T const r) noexcept { x /= r; y /= r; return *this; }

		inline bool operator==(Vector2 const& r) const noexcept { return x == r.x && y == r.y; }
		inline bool operator!=(Vector2 const& r) const noexcept { return x != r.x || y != r.y; }

		inline Vector2& normalize() noexcept
		{
			T const l = length();
			if (l >= std::numeric_limits<T>::min())
			{
				x /= l; y /= l;
			}
			else
			{
				x = T{}; y = T{};
			}
			return *this;
		}
		inline Vector2 normalized() const noexcept
		{
			T const l = length();
			if (l >= std::numeric_limits<T>::min())
			{
				return Vector2(x / l, y / l);
			}
			else
			{
				return Vector2();
			}
		}
		inline T length() const noexcept { return std::sqrt(x * x + y * y); }
		inline T angle() const noexcept { return std::atan2(y, x); }
		inline T dot(Vector2 const& r) const noexcept { return x * r.x + y * r.y; }

		inline T& operator[](size_t const i) { return (&x)[i]; }
	};

	using Vector2I = Vector2<int32_t>;
	using Vector2U = Vector2<uint32_t>;
	using Vector2F = Vector2<float>;

	// 表示一个长方形区域

	template<typename T>
	struct Rect
	{
		Vector2<T> a;
		Vector2<T> b;

		Rect() noexcept {}
		Rect(Vector2<T> const& a_, Vector2<T> const& b_) noexcept : a(a_), b(b_) {}
		Rect(T const left, T const top, T const right, T const bottom) noexcept : a(left, top), b(right, bottom) {}

		inline bool operator==(Rect const& r) const noexcept { return a == r.a && b == r.b; }
		inline bool operator!=(Rect const& r) const noexcept { return a != r.a || b != r.b; }

		inline Rect operator+(Vector2<T> const& r) const noexcept { return Rect(a + r, b + r); }
		inline Rect operator-(Vector2<T> const& r) const noexcept { return Rect(a - r, b - r); }

		//inline Rect operator*(T const r) const noexcept { return Rect(a * r, b * r); }
	};

	using RectI = Rect<int32_t>;
	using RectU = Rect<uint32_t>;
	using RectF = Rect<float>;

	// 三维向量

	template<typename T>
	struct Vector3
	{
		T x{};
		T y{};
		T z{};

		Vector3() noexcept : x(0), y(0), z(0) {}
		Vector3(T const x, T const y, T const z) noexcept : x(x), y(y), z(z) {}

		inline Vector3 operator+(Vector3 const& r) const noexcept { return Vector3(x + r.x, y + r.y, z + r.z); }
		inline Vector3 operator-(Vector3 const& r) const noexcept { return Vector3(x - r.x, y - r.y, z - r.z); }
		inline Vector3 operator*(Vector3 const& r) const noexcept { return Vector3(x * r.x, y * r.y, z * r.z); }
		inline Vector3 operator/(Vector3 const& r) const noexcept { return Vector3(x / r.x, y / r.y, z / r.z); }

		inline Vector3 operator+(T const r) const noexcept { return Vector3(x + r, y + r, z + r); }
		inline Vector3 operator-(T const r) const noexcept { return Vector3(x - r, y - r, z - r); }
		inline Vector3 operator*(T const r) const noexcept { return Vector3(x * r, y * r, z * r); }
		inline Vector3 operator/(T const r) const noexcept { return Vector3(x / r, y / r, z / r); }

		inline Vector3 operator-() const noexcept { return Vector3(-x, -y, -z); }
		
		inline Vector3& operator+=(Vector3 const& r) noexcept { x += r.x; y += r.y; z += r.z; return *this; }
		inline Vector3& operator-=(Vector3 const& r) noexcept { x -= r.x; y -= r.y; z -= r.z; return *this; }
		inline Vector3& operator*=(Vector3 const& r) noexcept { x *= r.x; y *= r.y; z *= r.z; return *this; }
		inline Vector3& operator/=(Vector3 const& r) noexcept { x /= r.x; y /= r.y; z /= r.z; return *this; }

		inline Vector3& operator+=(T const r) noexcept { x += r; y += r; z += r; return *this; }
		inline Vector3& operator-=(T const r) noexcept { x -= r; y -= r; z -= r; return *this; }
		inline Vector3& operator*=(T const r) noexcept { x *= r; y *= r; z *= r; return *this; }
		inline Vector3& operator/=(T const r) noexcept { x /= r; y /= r; z /= r; return *this; }

		inline bool operator==(Vector3 const& r) const noexcept { return x == r.x && y == r.y && z == r.z; }
		inline bool operator!=(Vector3 const& r) const noexcept { return x != r.x || y != r.y || z != r.z; }

		inline T& operator[](size_t const i) { return (&x)[i]; }

		inline Vector3& normalize() noexcept {
			T const l = length();
			if (l >= std::numeric_limits<T>::min()) {
				x /= l; y /= l; z /= l;
			}
			else {
				x = T{}; y = T{}; z = T{};
			}
			return *this;
		}
		inline Vector3 normalized() const noexcept {
			T const l = length();
			if (l >= std::numeric_limits<T>::min()) {
				return Vector3(x / l, y / l, z / l);
			}
			else {
				return Vector3();
			}
		}
		inline T length() const noexcept { return std::sqrt(x * x + y * y + z * z); }
		inline T dot(Vector3 const& r) const noexcept { return x * r.x + y * r.y + z * r.z; }
	};

	using Vector3I = Vector3<int32_t>;
	using Vector3U = Vector3<uint32_t>;
	using Vector3F = Vector3<float>;

	// 表示一个长方体区域

	template<typename T>
	struct Box
	{
		Vector3<T> a;
		Vector3<T> b;

		Box() noexcept {}
		Box(Vector3<T> const& a_, Vector3<T> const& b_) noexcept : a(a_), b(b_) {}
		Box(T const left, T const top, T const front, T const right, T const bottom, T const back) noexcept : a(left, top, front), b(right, bottom, back) {}

		inline bool operator==(Box const& r) const noexcept { return a == r.a && b == r.b; }
		inline bool operator!=(Box const& r) const noexcept { return a != r.a || b != r.b; }
	};

	using BoxI = Box<int32_t>;
	using BoxU = Box<uint32_t>;
	using BoxF = Box<float>;

	// 四维向量

	template<typename T>
	struct Vector4
	{
		T x{};
		T y{};
		T z{};
		T w{};

		Vector4() noexcept : x(0), y(0), z(0), w(0) {}
		Vector4(Vector2<T> const& xy, T const z, T const w) noexcept : x(xy.x), y(xy.y), z(z), w(w) {}
		Vector4(Vector3<T> const& xyz, T const w) noexcept : x(xyz.x), y(xyz.y), z(xyz.z), w(w) {}
		Vector4(T const x, T const y, T const z, T const w) noexcept : x(x), y(y), z(z), w(w) {}

		inline Vector4 operator+(Vector4 const& r) const noexcept { return Vector4(x + r.x, y + r.y, z + r.z, w + r.w); }
		inline Vector4 operator-(Vector4 const& r) const noexcept { return Vector4(x - r.x, y - r.y, z - r.z, w - r.w); }
		inline Vector4 operator*(Vector4 const& r) const noexcept { return Vector4(x * r.x, y * r.y, z * r.z, w * r.w); }
		inline Vector4 operator/(Vector4 const& r) const noexcept { return Vector4(x / r.x, y / r.y, z / r.z, w / r.w); }

		inline Vector4 operator+(T const r) const noexcept { return Vector4(x + r, y + r, z + r, w + r); }
		inline Vector4 operator-(T const r) const noexcept { return Vector4(x - r, y - r, z - r, w - r); }
		inline Vector4 operator*(T const r) const noexcept { return Vector4(x * r, y * r, z * r, w * r); }
		inline Vector4 operator/(T const r) const noexcept { return Vector4(x / r, y / r, z / r, w / r); }

		inline Vector4 operator-() const noexcept { return Vector4(-x, -y, -z, -w); }

		inline Vector4& operator+=(Vector4 const& r) noexcept { x += r.x; y += r.y; z += r.z; w += r.w; return *this; }
		inline Vector4& operator-=(Vector4 const& r) noexcept { x -= r.x; y -= r.y; z -= r.z; w -= r.w; return *this; }
		inline Vector4& operator*=(Vector4 const& r) noexcept { x *= r.x; y *= r.y; z *= r.z; w *= r.w; return *this; }
		inline Vector4& operator/=(Vector4 const& r) noexcept { x /= r.x; y /= r.y; z /= r.z; w /= r.w; return *this; }

		inline Vector4& operator+=(T const r) noexcept { x += r; y += r; z += z; w += r; return *this; }
		inline Vector4& operator-=(T const r) noexcept { x -= r; y -= r; z -= z; w -= r; return *this; }
		inline Vector4& operator*=(T const r) noexcept { x *= r; y *= r; z *= z; w *= r; return *this; }
		inline Vector4& operator/=(T const r) noexcept { x /= r; y /= r; z /= z; w /= r; return *this; }

		inline bool operator==(Vector4 const& r) const noexcept { return x == r.x && y == r.y && z == r.z && w == r.w; }
		inline bool operator!=(Vector4 const& r) const noexcept { return x != r.x || y != r.y || z != r.z || w != r.w; }

		inline T& operator[](size_t const i) { return (&x)[i]; }

		inline Vector4& normalize() noexcept {
			T const l = length();
			if (l >= std::numeric_limits<T>::min()) {
				x /= l; y /= l; z /= l; w /= l;
			}
			else {
				x = T{}; y = T{}; z = T{}; w = T{};
			}
			return *this;
		}
		inline Vector4 normalized() const noexcept {
			T const l = length();
			if (l >= std::numeric_limits<T>::min()) {
				return Vector4(x / l, y / l, z / l, w / l);
			}
			else {
				return Vector4();
			}
		}
		inline T length() const noexcept { return std::sqrt(x * x + y * y + z * z + w * w); }
		inline T dot(Vector4 const& r) const noexcept { return x * r.x + y * r.y + z * r.z + w * r.w; }
	};

	using Vector4I = Vector4<int32_t>;
	using Vector4U = Vector4<uint32_t>;
	using Vector4F = Vector4<float>;

	// 四阶矩阵，行主序

	template<typename T>
	struct Matrix4 {
		union {
			struct {
				T m11, m12, m13, m14;
				T m21, m22, m23, m24;
				T m31, m32, m33, m34;
				T m41, m42, m43, m44;
			} s;
			Vector4<T> rows[4];
			T m[4][4];
		} u;

		bool operator==(Matrix4 const& other) const noexcept {
			return u.s.m11 == other.u.s.m11
				&& u.s.m12 == other.u.s.m12
				&& u.s.m13 == other.u.s.m13
				&& u.s.m14 == other.u.s.m14
				&& u.s.m21 == other.u.s.m21
				&& u.s.m22 == other.u.s.m22
				&& u.s.m23 == other.u.s.m23
				&& u.s.m24 == other.u.s.m24
				&& u.s.m31 == other.u.s.m31
				&& u.s.m32 == other.u.s.m32
				&& u.s.m33 == other.u.s.m33
				&& u.s.m34 == other.u.s.m34
				&& u.s.m41 == other.u.s.m41
				&& u.s.m42 == other.u.s.m42
				&& u.s.m43 == other.u.s.m43
				&& u.s.m44 == other.u.s.m44;
		}
		bool operator!=(Matrix4 const& other) const noexcept {
			return u.s.m11 != other.u.s.m11
				|| u.s.m12 != other.u.s.m12
				|| u.s.m13 != other.u.s.m13
				|| u.s.m14 != other.u.s.m14
				|| u.s.m21 != other.u.s.m21
				|| u.s.m22 != other.u.s.m22
				|| u.s.m23 != other.u.s.m23
				|| u.s.m24 != other.u.s.m24
				|| u.s.m31 != other.u.s.m31
				|| u.s.m32 != other.u.s.m32
				|| u.s.m33 != other.u.s.m33
				|| u.s.m34 != other.u.s.m34
				|| u.s.m41 != other.u.s.m41
				|| u.s.m42 != other.u.s.m42
				|| u.s.m43 != other.u.s.m43
				|| u.s.m44 != other.u.s.m44;
		}

		static Matrix4 identity() noexcept {
			Matrix4 m{};
			m.u.s.m11 = static_cast<T>(1);
			m.u.s.m22 = static_cast<T>(1);
			m.u.s.m33 = static_cast<T>(1);
			m.u.s.m44 = static_cast<T>(1);
			return m;
		}
	};

	using Matrix4F = Matrix4<float>;

	// 颜色（有黑魔法）

	struct alignas(uint32_t) Color4B
	{
		uint8_t b;
		uint8_t g;
		uint8_t r;
		uint8_t a;

		Color4B() : b(0), g(0), r(0), a(0) {}
		Color4B(uint32_t ARGB) : b(0), g(0), r(0), a(0) { color(ARGB); }
		Color4B(uint8_t const r_, uint8_t const g_, uint8_t const b_) : b(b_), g(g_), r(r_), a(static_cast<uint8_t>(255u)) {}
		Color4B(uint8_t const r_, uint8_t const g_, uint8_t const b_, uint8_t const a_) : b(b_), g(g_), r(r_), a(a_) {}

		inline void color(uint32_t ARGB) noexcept { *((uint32_t*)(&b)) = ARGB; }
		inline uint32_t color() const noexcept { return *((uint32_t*)(&b)); }

		bool operator==(Color4B const& right) const noexcept
		{
			return color() == right.color();
		}
		bool operator!=(Color4B const& right) const noexcept
		{
			return color() != right.color();
		}
	};

	// 分数

	struct Rational
	{
		uint32_t numerator; // 分子
		uint32_t denominator; // 分母

		Rational() : numerator(0), denominator(0) {}
		Rational(uint32_t const numerator_) : numerator(numerator_), denominator(1) {}
		Rational(uint32_t const numerator_, uint32_t const denominator_) : numerator(numerator_), denominator(denominator_) {}
	};

	// 字符串

	using String = std::string;
	using StringView = std::string_view;

	// 引用计数

	struct IObject
	{
		virtual intptr_t retain() = 0;
		virtual intptr_t release() = 0;
		virtual ~IObject() {};
	};
	
	template<typename T = IObject>
	class ScopeObject
	{
	private:
		T* ptr_;
	private:
		inline void internal_retain() { if (ptr_) ptr_->retain(); }
		inline void internal_release() { if (ptr_) ptr_->release(); ptr_ = nullptr; }
	public:
		T* operator->() { return ptr_; }
		T* operator*() { return ptr_; }
		T** operator~() { internal_release(); return &ptr_; }
		ScopeObject& operator=(std::nullptr_t) { internal_release(); return *this; }
		ScopeObject& operator=(T* ptr) { if (ptr_ != ptr) { internal_release(); ptr_ = ptr; internal_retain(); } return *this; }
		ScopeObject& operator=(ScopeObject& right) { if (ptr_ != right.ptr_) { internal_release(); ptr_ = right.ptr_; internal_retain(); } return *this; }
		ScopeObject& operator=(ScopeObject const& right) { if (ptr_ != right.ptr_) { internal_release(); ptr_ = right.ptr_; internal_retain(); } return *this; }
		operator bool() { return ptr_ != nullptr; }
		ScopeObject& attach(T* ptr) { internal_release(); ptr_ = ptr; return *this; }
		T* detach()  { T* tmp_ = ptr_; ptr_ = nullptr; return tmp_; }
		ScopeObject& reset() { internal_release(); return *this; }
		T* get() const { return ptr_; }
	public:
		ScopeObject() : ptr_(nullptr) {}
		ScopeObject(T* ptr) : ptr_(ptr) { internal_retain(); }
		ScopeObject(ScopeObject& right) : ptr_(right.ptr_) { internal_retain(); }
		ScopeObject(ScopeObject const& right) : ptr_(right.ptr_) { internal_retain(); }
		ScopeObject(ScopeObject&& right) noexcept : ptr_(right.ptr_) { right.ptr_ = nullptr; }
		ScopeObject(ScopeObject const&&) = delete;
		~ScopeObject() { internal_release(); }
	};

	struct IData : public IObject
	{
		virtual void* data() = 0;
		virtual size_t size() = 0;

		static bool create(size_t size, IData** pp_data);
		static bool create(size_t size, size_t align, IData** pp_data);
	};
}
