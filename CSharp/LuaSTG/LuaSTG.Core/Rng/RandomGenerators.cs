// random 模块 RNG 家族（对应 Lua 侧 random 库，LuaBinding/external/lua_random.cpp）。
// 精确移植：
// - 算法本体：Utility/xorshift.hpp（splitmix64/xoshiro/xoroshiro 家族）、
//   pcg-cpp（pcg32_oneseq/pcg32_fast/pcg64_oneseq/pcg64_fast）、
//   Utility/jsf.hpp（jsf32/jsf64）、Utility/sfc.hpp（sfc32/sfc64）；
// - integer/number/sign 的取数逻辑精确复刻 MSVC STL：引擎 RNG 的 min/max 非
//   constexpr，走 14.44 STL 的 _Rng_from_urng（取模拒绝采样）与 _Nrand_impl
//   的 tr1 回退路径（详见 MsvcRandomAdapter）；
// - seed/serialize/deserialize 语义与 lua_random.cpp 一致。
// 期望值自检见本文件末尾 RngParityTests（与 C++ 基准程序输出比对）。

using System;
using System.Globalization;

namespace LuaSTG.Core.Rng
{
    /// <summary>32 位输出的随机数发生器（原始位流接口）。</summary>
    public interface IRandomUInt32
    {
        /// <summary>产生下一个 32 位原始随机数。</summary>
        uint NextUInt32();
    }

    /// <summary>64 位输出的随机数发生器（原始位流接口）。</summary>
    public interface IRandomUInt64
    {
        /// <summary>产生下一个 64 位原始随机数。</summary>
        ulong NextUInt64();
    }

    /// <summary>
    /// random 模块随机数发生器的公共接口（对应 Lua 侧 random.* 类的方法集）。
    /// integer/number/sign 与 MSVC STL 分布语义一致；jsf/sfc 家族不支持序列化。
    /// </summary>
    public interface IRng
    {
        /// <summary>获取当前种子（对应 random.*:seed()）。</summary>
        long GetSeed();
        /// <summary>重设种子（对应 random.*:seed(v)）。</summary>
        void Seed(long seed);
        /// <summary>产生 [0, 2^63-1] 内的随机整数（对应 random.*:integer()）。</summary>
        long Integer();
        /// <summary>产生 [0, b] 内的随机整数（对应 random.*:integer(b)，b 取绝对值）。</summary>
        long Integer(long b);
        /// <summary>产生 [a, b] 内的随机整数（对应 random.*:integer(a, b)，乱序自动交换）。</summary>
        long Integer(long a, long b);
        /// <summary>产生 [0, 1] 内的随机浮点数（对应 random.*:number()）。</summary>
        double Number();
        /// <summary>产生 [0, b] 内的随机浮点数（对应 random.*:number(b)，b 取绝对值）。</summary>
        double Number(double b);
        /// <summary>产生 [a, b] 内的随机浮点数（对应 random.*:number(a, b)，乱序自动交换）。</summary>
        double Number(double a, double b);
        /// <summary>随机返回 -1 或 +1（对应 random.*:sign）。</summary>
        int Sign();
        /// <summary>克隆（对应 random.*:clone）。</summary>
        IRng Clone();
        /// <summary>序列化为字符串（对应 random.*:serialize；jsf/sfc 家族不支持，返回 null）。</summary>
        string? Serialize();
        /// <summary>从字符串反序列化（对应 random.*:deserialize；jsf/sfc 家族不支持，返回 false）。</summary>
        bool Deserialize(string data);
    }

    /// <summary>循环左移辅助（等价 C++ rotl，移位计数对位宽取模）。</summary>
    internal static class BitRot
    {
        internal static uint Rotl(uint x, int k)
            => k == 0 ? x : (x << k) | (x >> (32 - k));
        internal static ulong Rotl(ulong x, int k)
            => k == 0 ? x : (x << k) | (x >> (64 - k));
    }

    /// <summary>
    /// MSVC STL uniform_int_distribution/uniform_real_distribution 的精确复刻。
    /// 引擎（lua_random.cpp）使用这些分布包装裸 RNG，行为与 STL 版本相关。
    /// 引擎 RNG 的 min()/max() 为非 constexpr 静态函数，不满足 MSVC STL 的
    /// _Has_static_min_max，因此 uniform_int_distribution 走旧版
    /// _Rng_from_urng（取模拒绝采样），uniform_real_distribution 走
    /// _Nrand_impl 的 tr1 回退路径；此处与引擎构建使用的 MSVC 14.44 STL 一致。
    /// </summary>
    internal static class MsvcRandomAdapter
    {
        /// <summary>std::nextafter(1.0, DBL_MAX)，number() 的上界。</summary>
        internal const double NextAfterOne = 1.0000000000000002;

        /// <summary>2^-53（ldexp(v, -53) 的尺度）。</summary>
        private const double Scale53 = 1.0 / 9007199254740992.0;

        private const ulong SignBit = 0x8000000000000000UL;

        /// <summary>std::nextafter(x, DBL_MAX)。</summary>
        internal static double BitIncrement(double x) => Math.BitIncrement(x);

        // ---- uniform_int_distribution<long long>（64 位 URNG）----------------
        // _Rng_from_urng：_Bits=64，_Bmask=2^64-1，按位累积后取模拒绝采样。

        internal static long UniformInt64<TRng>(ref TRng rng, long a, long b)
            where TRng : struct, IRandomUInt64
        {
            ulong umin = (ulong)a ^ SignBit;
            ulong umax = (ulong)b ^ SignBit;
            ulong uret;
            if (umax - umin == ulong.MaxValue)
            {
                // _Get_all_bits（_Bits == 64：单次取数）
                uret = rng.NextUInt64();
            }
            else
            {
                uret = RngFromUrng64(ref rng, umax - umin + 1UL);
            }
            return (long)((uret + umin) ^ SignBit);
        }

        private static ulong RngFromUrng64<TRng>(ref TRng rng, ulong span)
            where TRng : struct, IRandomUInt64
        {
            while (true)
            {
                ulong ret = 0;
                ulong mask = 0;
                while (mask < span - 1UL)
                {
                    // _Bits=64：循环至多执行一次（_Mask 变为 2^64-1）
                    ret = rng.NextUInt64();
                    mask = ulong.MaxValue;
                }
                if (ret / span < mask / span || mask % span == span - 1UL)
                {
                    return ret % span;
                }
                // 拒绝：重置后重新取数
            }
        }

        // ---- uniform_int_distribution<long long>（32 位 URNG）----------------
        // _Rng_from_urng：_Bits=32，_Bmask=2^32-1，跨度超过 2^32 时累积两次取数。

        internal static long UniformInt32<TRng>(ref TRng rng, long a, long b)
            where TRng : struct, IRandomUInt32
        {
            ulong umin = (ulong)a ^ SignBit;
            ulong umax = (ulong)b ^ SignBit;
            ulong uret;
            if (umax - umin == ulong.MaxValue)
            {
                // _Get_all_bits（_Bits=32：第一次取数为高 32 位）
                uret = ((ulong)rng.NextUInt32() << 32) | rng.NextUInt32();
            }
            else
            {
                uret = RngFromUrng32(ref rng, umax - umin + 1UL);
            }
            return (long)((uret + umin) ^ SignBit);
        }

        private static ulong RngFromUrng32<TRng>(ref TRng rng, ulong span)
            where TRng : struct, IRandomUInt32
        {
            while (true)
            {
                ulong ret = 0;
                ulong mask = 0;
                while (mask < span - 1UL)
                {
                    // _Bits=32：先取的数位于高位；跨度 ≤ 2^32 时一次，更大时两次
                    ret = (ret << 32) | rng.NextUInt32();
                    mask = (mask << 32) | 0xFFFF_FFFFUL;
                }
                if (ret / span < mask / span || mask % span == span - 1UL)
                {
                    return ret % span;
                }
                // 拒绝：重置后重新取数
            }
        }

        // ---- _Rng_from_urng_v2（Lemire，MSVC STL 新版适配器）-------------------
        // 供 min()/max() 为 constexpr 的 RNG（pcg/jsf/sfc 家族）使用。

        internal static long UniformInt64Modern<TRng>(ref TRng rng, long a, long b)
            where TRng : struct, IRandomUInt64
        {
            ulong umin = (ulong)a ^ SignBit;
            ulong umax = (ulong)b ^ SignBit;
            ulong uret;
            if (umax - umin == ulong.MaxValue)
            {
                uret = rng.NextUInt64();
            }
            else
            {
                uret = Lemire64(ref rng, umax - umin + 1UL);
            }
            return (long)((uret + umin) ^ SignBit);
        }

        private static ulong Lemire64<TRng>(ref TRng rng, ulong span)
            where TRng : struct, IRandomUInt64
        {
            // _Bits == _Udiff_bits == 64：_Mask 恒为 2^64-1，单次取数
            ulong x = rng.NextUInt64();
            UInt128 product = (UInt128)x * span;
            ulong rem = (ulong)product;
            if (rem < span)
            {
                ulong threshold = (ulong.MaxValue - span + 1UL) % span;
                while (rem < threshold)
                {
                    x = rng.NextUInt64();
                    product = (UInt128)x * span;
                    rem = (ulong)product;
                }
            }
            return (ulong)(product >> 64);
        }

        internal static long UniformInt32Modern<TRng>(ref TRng rng, long a, long b)
            where TRng : struct, IRandomUInt32
        {
            ulong umin = (ulong)a ^ SignBit;
            ulong umax = (ulong)b ^ SignBit;
            ulong uret;
            if (umax - umin == ulong.MaxValue)
            {
                uret = ((ulong)rng.NextUInt32() << 32) | rng.NextUInt32();
            }
            else
            {
                uret = Lemire32(ref rng, umax - umin + 1UL);
            }
            return (long)((uret + umin) ^ SignBit);
        }

        private static ulong Lemire32<TRng>(ref TRng rng, ulong span)
            where TRng : struct, IRandomUInt32
        {
            // _Bits=32，_Bmask=2^32-1；跨度超过 2^32 时掩码扩展为 2^64-1（两次取数）
            ulong mask = uint.MaxValue;
            uint niter = 1;
            while (mask < span - 1UL)
            {
                mask = (mask << 32) | uint.MaxValue;
                niter += 1;
            }
            UInt128 product = (UInt128)Draw32(ref rng, niter) * span;
            ulong rem = (ulong)product & mask;
            if (rem < span)
            {
                ulong threshold = (mask - span + 1UL) % span;
                while (rem < threshold)
                {
                    product = (UInt128)Draw32(ref rng, niter) * span;
                    rem = (ulong)product & mask;
                }
            }
            int generatedBits = niter == 1 ? 32 : 64; // popcount(mask)
            return (ulong)(product >> generatedBits);
        }

        private static UInt128 Draw32<TRng>(ref TRng rng, uint niter)
            where TRng : struct, IRandomUInt32
        {
            ulong ret = rng.NextUInt32();
            if (niter > 1)
            {
                ret = (ret << 32) | rng.NextUInt32();
            }
            return (UInt128)ret;
        }

        // ---- uniform_real_distribution<double> --------------------------------
        // _Nrand_impl 的 tr1 回退路径：
        // - 64 位 URNG（max=2^64-1, min=0）：Sx = x >> 11，u = Sx * 2^-53；
        // - 32 位 URNG：uint64 尝试消耗一次取数后升级 128 位再消耗一次，
        //   Sx = ((x1 << 32) + x0) >> 11，u = Sx * 2^-53。

        internal static double UniformReal64<TRng>(ref TRng rng, double a, double b)
            where TRng : struct, IRandomUInt64
        {
            double u = (double)(rng.NextUInt64() >> 11) * Scale53;
            return u * (b - a) + a;
        }

        internal static double UniformReal32<TRng>(ref TRng rng, double a, double b)
            where TRng : struct, IRandomUInt32
        {
            // Kx=2：第一次取数取高 21 位为低位，第二次取数整体左移 21 位为高位
            uint x0 = rng.NextUInt32();
            uint x1 = rng.NextUInt32();
            ulong sx = ((ulong)x1 << 21) | (x0 >> 11);
            double u = (double)sx * Scale53;
            return u * (b - a) + a;
        }
    }

    // ==========================================================================
    // plain 家族（lua_random.cpp RandomBase：seed 直接展开，支持 serialize）
    // ==========================================================================

    /// <summary>splitmix64（Melissa O'Neill 版本，对应 random.splitmix64）。</summary>
    public struct SplitMix64 : IRandomUInt64, IRng
    {
        private ulong _x;
        private long _seed;

        public SplitMix64() { this = default; Seed(0); }
        public SplitMix64(long seed) { this = default; Seed(seed); }

        public void Seed(long seed) { _seed = seed; _x = (ulong)seed; }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong z = _x;
            _x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? unchecked(-b) : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => "splitmix64-" + _x.ToString(CultureInfo.InvariantCulture);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "splitmix64", 1, out var parts))
            {
                return false;
            }
            _x = parts[0];
            return true;
        }
    }

    /// <summary>xoshiro128+（对应 random.xoshiro128p）。</summary>
    public struct Xoshiro128P : IRandomUInt32, IRng
    {
        private uint _s0, _s1, _s2, _s3;
        private long _seed;

        public Xoshiro128P() { this = default; Seed(0); }
        public Xoshiro128P(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = (uint)gn.NextUInt64();
            _s1 = (uint)gn.NextUInt64();
            _s2 = (uint)gn.NextUInt64();
            _s3 = (uint)gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public uint NextUInt32()
        {
            uint result = _s0 + _s3;
            uint t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitRot.Rotl(_s3, 11);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt32(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt32(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt32(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal32(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt32(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro128p-{0}-{1}-{2}-{3}", _s0, _s1, _s2, _s3);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro128p", 4, out var parts))
            {
                return false;
            }
            _s0 = (uint)parts[0];
            _s1 = (uint)parts[1];
            _s2 = (uint)parts[2];
            _s3 = (uint)parts[3];
            return true;
        }
    }

    /// <summary>xoshiro128++（对应 random.xoshiro128pp）。</summary>
    public struct Xoshiro128PP : IRandomUInt32, IRng
    {
        private uint _s0, _s1, _s2, _s3;
        private long _seed;

        public Xoshiro128PP() { this = default; Seed(0); }
        public Xoshiro128PP(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = (uint)gn.NextUInt64();
            _s1 = (uint)gn.NextUInt64();
            _s2 = (uint)gn.NextUInt64();
            _s3 = (uint)gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public uint NextUInt32()
        {
            uint result = BitRot.Rotl(_s0 + _s3, 7) + _s0;
            uint t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitRot.Rotl(_s3, 11);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt32(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt32(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt32(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal32(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt32(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro128pp-{0}-{1}-{2}-{3}", _s0, _s1, _s2, _s3);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro128pp", 4, out var parts))
            {
                return false;
            }
            _s0 = (uint)parts[0];
            _s1 = (uint)parts[1];
            _s2 = (uint)parts[2];
            _s3 = (uint)parts[3];
            return true;
        }
    }

    /// <summary>xoshiro128**（对应 random.xoshiro128ss）。</summary>
    public struct Xoshiro128SS : IRandomUInt32, IRng
    {
        private uint _s0, _s1, _s2, _s3;
        private long _seed;

        public Xoshiro128SS() { this = default; Seed(0); }
        public Xoshiro128SS(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = (uint)gn.NextUInt64();
            _s1 = (uint)gn.NextUInt64();
            _s2 = (uint)gn.NextUInt64();
            _s3 = (uint)gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public uint NextUInt32()
        {
            uint result = BitRot.Rotl(_s1 * 5u, 7) * 9u;
            uint t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitRot.Rotl(_s3, 11);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt32(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt32(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt32(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal32(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt32(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro128ss-{0}-{1}-{2}-{3}", _s0, _s1, _s2, _s3);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro128ss", 4, out var parts))
            {
                return false;
            }
            _s0 = (uint)parts[0];
            _s1 = (uint)parts[1];
            _s2 = (uint)parts[2];
            _s3 = (uint)parts[3];
            return true;
        }
    }

    /// <summary>xoroshiro128+（对应 random.xoroshiro128p）。</summary>
    public struct Xoroshiro128P : IRandomUInt64, IRng
    {
        private ulong _s0, _s1;
        private long _seed;

        public Xoroshiro128P() { this = default; Seed(0); }
        public Xoroshiro128P(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong s0 = _s0;
            ulong s1 = _s1;
            ulong result = s0 + s1;
            s1 ^= s0;
            _s0 = BitRot.Rotl(s0, 24) ^ s1 ^ (s1 << 16);
            _s1 = BitRot.Rotl(s1, 37);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoroshiro128p-{0}-{1}", _s0, _s1);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoroshiro128p", 2, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            return true;
        }
    }

    /// <summary>xoroshiro128++（对应 random.xoroshiro128pp）。</summary>
    public struct Xoroshiro128PP : IRandomUInt64, IRng
    {
        private ulong _s0, _s1;
        private long _seed;

        public Xoroshiro128PP() { this = default; Seed(0); }
        public Xoroshiro128PP(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong s0 = _s0;
            ulong s1 = _s1;
            ulong result = BitRot.Rotl(s0 + s1, 17) + s0;
            s1 ^= s0;
            _s0 = BitRot.Rotl(s0, 49) ^ s1 ^ (s1 << 21);
            _s1 = BitRot.Rotl(s1, 28);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoroshiro128pp-{0}-{1}", _s0, _s1);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoroshiro128pp", 2, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            return true;
        }
    }

    /// <summary>xoroshiro128**（对应 random.xoroshiro128ss）。</summary>
    public struct Xoroshiro128SS : IRandomUInt64, IRng
    {
        private ulong _s0, _s1;
        private long _seed;

        public Xoroshiro128SS() { this = default; Seed(0); }
        public Xoroshiro128SS(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong s0 = _s0;
            ulong s1 = _s1;
            ulong result = BitRot.Rotl(s0 * 5UL, 7) * 9UL;
            s1 ^= s0;
            _s0 = BitRot.Rotl(s0, 24) ^ s1 ^ (s1 << 16);
            _s1 = BitRot.Rotl(s1, 37);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoroshiro128ss-{0}-{1}", _s0, _s1);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoroshiro128ss", 2, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            return true;
        }
    }

    /// <summary>xoshiro256+（对应 random.xoshiro256p）。</summary>
    public struct Xoshiro256P : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3;
        private long _seed;

        public Xoshiro256P() { this = default; Seed(0); }
        public Xoshiro256P(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong result = _s0 + _s3;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitRot.Rotl(_s3, 45);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro256p-{0}-{1}-{2}-{3}", _s0, _s1, _s2, _s3);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro256p", 4, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            _s2 = parts[2];
            _s3 = parts[3];
            return true;
        }
    }

    /// <summary>xoshiro256++（对应 random.xoshiro256pp）。</summary>
    public struct Xoshiro256PP : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3;
        private long _seed;

        public Xoshiro256PP() { this = default; Seed(0); }
        public Xoshiro256PP(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong result = BitRot.Rotl(_s0 + _s3, 23) + _s0;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitRot.Rotl(_s3, 45);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro256pp-{0}-{1}-{2}-{3}", _s0, _s1, _s2, _s3);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro256pp", 4, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            _s2 = parts[2];
            _s3 = parts[3];
            return true;
        }
    }

    /// <summary>xoshiro256**（对应 random.xoshiro256ss）。</summary>
    public struct Xoshiro256SS : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3;
        private long _seed;

        public Xoshiro256SS() { this = default; Seed(0); }
        public Xoshiro256SS(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong result = BitRot.Rotl(_s1 * 5UL, 7) * 9UL;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitRot.Rotl(_s3, 45);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro256ss-{0}-{1}-{2}-{3}", _s0, _s1, _s2, _s3);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro256ss", 4, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            _s2 = parts[2];
            _s3 = parts[3];
            return true;
        }
    }

    /// <summary>xoshiro512+（对应 random.xoshiro512p）。</summary>
    public struct Xoshiro512P : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7;
        private long _seed;

        public Xoshiro512P() { this = default; Seed(0); }
        public Xoshiro512P(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
            _s4 = gn.NextUInt64();
            _s5 = gn.NextUInt64();
            _s6 = gn.NextUInt64();
            _s7 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong result = _s0 + _s2;
            ulong t = _s1 << 11;
            _s2 ^= _s0;
            _s5 ^= _s1;
            _s1 ^= _s2;
            _s7 ^= _s3;
            _s3 ^= _s4;
            _s4 ^= _s5;
            _s0 ^= _s6;
            _s6 ^= _s7;
            _s6 ^= t;
            _s7 = BitRot.Rotl(_s7, 21);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro512p-{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}",
                _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro512p", 8, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            _s2 = parts[2];
            _s3 = parts[3];
            _s4 = parts[4];
            _s5 = parts[5];
            _s6 = parts[6];
            _s7 = parts[7];
            return true;
        }
    }

    /// <summary>xoshiro512++（对应 random.xoshiro512pp）。</summary>
    public struct Xoshiro512PP : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7;
        private long _seed;

        public Xoshiro512PP() { this = default; Seed(0); }
        public Xoshiro512PP(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
            _s4 = gn.NextUInt64();
            _s5 = gn.NextUInt64();
            _s6 = gn.NextUInt64();
            _s7 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong result = BitRot.Rotl(_s0 + _s2, 17) + _s2;
            ulong t = _s1 << 11;
            _s2 ^= _s0;
            _s5 ^= _s1;
            _s1 ^= _s2;
            _s7 ^= _s3;
            _s3 ^= _s4;
            _s4 ^= _s5;
            _s0 ^= _s6;
            _s6 ^= _s7;
            _s6 ^= t;
            _s7 = BitRot.Rotl(_s7, 21);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro512pp-{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}",
                _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro512pp", 8, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            _s2 = parts[2];
            _s3 = parts[3];
            _s4 = parts[4];
            _s5 = parts[5];
            _s6 = parts[6];
            _s7 = parts[7];
            return true;
        }
    }

    /// <summary>xoshiro512**（对应 random.xoshiro512ss）。</summary>
    public struct Xoshiro512SS : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7;
        private long _seed;

        public Xoshiro512SS() { this = default; Seed(0); }
        public Xoshiro512SS(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
            _s4 = gn.NextUInt64();
            _s5 = gn.NextUInt64();
            _s6 = gn.NextUInt64();
            _s7 = gn.NextUInt64();
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            ulong result = BitRot.Rotl(_s1 * 5UL, 7) * 9UL;
            ulong t = _s1 << 11;
            _s2 ^= _s0;
            _s5 ^= _s1;
            _s1 ^= _s2;
            _s7 ^= _s3;
            _s3 ^= _s4;
            _s4 ^= _s5;
            _s0 ^= _s6;
            _s6 ^= _s7;
            _s6 ^= t;
            _s7 = BitRot.Rotl(_s7, 21);
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "xoshiro512ss-{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}",
                _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoshiro512ss", 8, out var parts))
            {
                return false;
            }
            _s0 = parts[0];
            _s1 = parts[1];
            _s2 = parts[2];
            _s3 = parts[3];
            _s4 = parts[4];
            _s5 = parts[5];
            _s6 = parts[6];
            _s7 = parts[7];
            return true;
        }
    }

    /// <summary>xoroshiro1024*（对应 random.xoroshiro1024s）。</summary>
    public struct Xoroshiro1024S : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7;
        private ulong _s8, _s9, _s10, _s11, _s12, _s13, _s14, _s15;
        private int _p;
        private long _seed;

        public Xoroshiro1024S() { this = default; Seed(0); }
        public Xoroshiro1024S(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
            _s4 = gn.NextUInt64();
            _s5 = gn.NextUInt64();
            _s6 = gn.NextUInt64();
            _s7 = gn.NextUInt64();
            _s8 = gn.NextUInt64();
            _s9 = gn.NextUInt64();
            _s10 = gn.NextUInt64();
            _s11 = gn.NextUInt64();
            _s12 = gn.NextUInt64();
            _s13 = gn.NextUInt64();
            _s14 = gn.NextUInt64();
            _s15 = gn.NextUInt64();
            _p = 0;
        }
        public long GetSeed() => _seed;

        private ulong Get(int i) => i switch
        {
            0 => _s0, 1 => _s1, 2 => _s2, 3 => _s3,
            4 => _s4, 5 => _s5, 6 => _s6, 7 => _s7,
            8 => _s8, 9 => _s9, 10 => _s10, 11 => _s11,
            12 => _s12, 13 => _s13, 14 => _s14,
            _ => _s15,
        };

        private void Set(int i, ulong v)
        {
            switch (i)
            {
                case 0: _s0 = v; break;
                case 1: _s1 = v; break;
                case 2: _s2 = v; break;
                case 3: _s3 = v; break;
                case 4: _s4 = v; break;
                case 5: _s5 = v; break;
                case 6: _s6 = v; break;
                case 7: _s7 = v; break;
                case 8: _s8 = v; break;
                case 9: _s9 = v; break;
                case 10: _s10 = v; break;
                case 11: _s11 = v; break;
                case 12: _s12 = v; break;
                case 13: _s13 = v; break;
                case 14: _s14 = v; break;
                default: _s15 = v; break;
            }
        }

        public ulong NextUInt64()
        {
            int q = _p;
            _p = (_p + 1) & 15;
            ulong s0 = Get(_p);
            ulong s15 = Get(q);
            ulong result = s0 * 0x9E3779B97F4A7C13UL;
            s15 ^= s0;
            Set(q, BitRot.Rotl(s0, 25) ^ s15 ^ (s15 << 27));
            Set(_p, BitRot.Rotl(s15, 36));
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture,
                "xoroshiro1024s-{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}-{8}-{9}-{10}-{11}-{12}-{13}-{14}-{15}-{16}",
                (ulong)_p, _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7,
                _s8, _s9, _s10, _s11, _s12, _s13, _s14, _s15);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoroshiro1024s", 17, out var parts))
            {
                return false;
            }
            _p = (int)parts[0];
            for (int i = 0; i < 16; i += 1)
            {
                Set(i, parts[i + 1]);
            }
            return true;
        }
    }

    /// <summary>xoroshiro1024++（对应 random.xoroshiro1024pp）。</summary>
    public struct Xoroshiro1024PP : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7;
        private ulong _s8, _s9, _s10, _s11, _s12, _s13, _s14, _s15;
        private int _p;
        private long _seed;

        public Xoroshiro1024PP() { this = default; Seed(0); }
        public Xoroshiro1024PP(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
            _s4 = gn.NextUInt64();
            _s5 = gn.NextUInt64();
            _s6 = gn.NextUInt64();
            _s7 = gn.NextUInt64();
            _s8 = gn.NextUInt64();
            _s9 = gn.NextUInt64();
            _s10 = gn.NextUInt64();
            _s11 = gn.NextUInt64();
            _s12 = gn.NextUInt64();
            _s13 = gn.NextUInt64();
            _s14 = gn.NextUInt64();
            _s15 = gn.NextUInt64();
            _p = 0;
        }
        public long GetSeed() => _seed;

        private ulong Get(int i) => i switch
        {
            0 => _s0, 1 => _s1, 2 => _s2, 3 => _s3,
            4 => _s4, 5 => _s5, 6 => _s6, 7 => _s7,
            8 => _s8, 9 => _s9, 10 => _s10, 11 => _s11,
            12 => _s12, 13 => _s13, 14 => _s14,
            _ => _s15,
        };

        private void Set(int i, ulong v)
        {
            switch (i)
            {
                case 0: _s0 = v; break;
                case 1: _s1 = v; break;
                case 2: _s2 = v; break;
                case 3: _s3 = v; break;
                case 4: _s4 = v; break;
                case 5: _s5 = v; break;
                case 6: _s6 = v; break;
                case 7: _s7 = v; break;
                case 8: _s8 = v; break;
                case 9: _s9 = v; break;
                case 10: _s10 = v; break;
                case 11: _s11 = v; break;
                case 12: _s12 = v; break;
                case 13: _s13 = v; break;
                case 14: _s14 = v; break;
                default: _s15 = v; break;
            }
        }

        public ulong NextUInt64()
        {
            int q = _p;
            _p = (_p + 1) & 15;
            ulong s0 = Get(_p);
            ulong s15 = Get(q);
            ulong result = BitRot.Rotl(s0 + s15, 23) + s15;
            s15 ^= s0;
            Set(q, BitRot.Rotl(s0, 25) ^ s15 ^ (s15 << 27));
            Set(_p, BitRot.Rotl(s15, 36));
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture,
                "xoroshiro1024pp-{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}-{8}-{9}-{10}-{11}-{12}-{13}-{14}-{15}-{16}",
                (ulong)_p, _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7,
                _s8, _s9, _s10, _s11, _s12, _s13, _s14, _s15);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoroshiro1024pp", 17, out var parts))
            {
                return false;
            }
            _p = (int)parts[0];
            for (int i = 0; i < 16; i += 1)
            {
                Set(i, parts[i + 1]);
            }
            return true;
        }
    }

    /// <summary>xoroshiro1024**（对应 random.xoroshiro1024ss）。</summary>
    public struct Xoroshiro1024SS : IRandomUInt64, IRng
    {
        private ulong _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7;
        private ulong _s8, _s9, _s10, _s11, _s12, _s13, _s14, _s15;
        private int _p;
        private long _seed;

        public Xoroshiro1024SS() { this = default; Seed(0); }
        public Xoroshiro1024SS(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var gn = new SplitMix64(seed);
            _s0 = gn.NextUInt64();
            _s1 = gn.NextUInt64();
            _s2 = gn.NextUInt64();
            _s3 = gn.NextUInt64();
            _s4 = gn.NextUInt64();
            _s5 = gn.NextUInt64();
            _s6 = gn.NextUInt64();
            _s7 = gn.NextUInt64();
            _s8 = gn.NextUInt64();
            _s9 = gn.NextUInt64();
            _s10 = gn.NextUInt64();
            _s11 = gn.NextUInt64();
            _s12 = gn.NextUInt64();
            _s13 = gn.NextUInt64();
            _s14 = gn.NextUInt64();
            _s15 = gn.NextUInt64();
            _p = 0;
        }
        public long GetSeed() => _seed;

        private ulong Get(int i) => i switch
        {
            0 => _s0, 1 => _s1, 2 => _s2, 3 => _s3,
            4 => _s4, 5 => _s5, 6 => _s6, 7 => _s7,
            8 => _s8, 9 => _s9, 10 => _s10, 11 => _s11,
            12 => _s12, 13 => _s13, 14 => _s14,
            _ => _s15,
        };

        private void Set(int i, ulong v)
        {
            switch (i)
            {
                case 0: _s0 = v; break;
                case 1: _s1 = v; break;
                case 2: _s2 = v; break;
                case 3: _s3 = v; break;
                case 4: _s4 = v; break;
                case 5: _s5 = v; break;
                case 6: _s6 = v; break;
                case 7: _s7 = v; break;
                case 8: _s8 = v; break;
                case 9: _s9 = v; break;
                case 10: _s10 = v; break;
                case 11: _s11 = v; break;
                case 12: _s12 = v; break;
                case 13: _s13 = v; break;
                case 14: _s14 = v; break;
                default: _s15 = v; break;
            }
        }

        public ulong NextUInt64()
        {
            int q = _p;
            _p = (_p + 1) & 15;
            ulong s0 = Get(_p);
            ulong s15 = Get(q);
            ulong result = BitRot.Rotl(s0 * 5UL, 7) * 9UL;
            s15 ^= s0;
            Set(q, BitRot.Rotl(s0, 25) ^ s15 ^ (s15 << 27));
            Set(_p, BitRot.Rotl(s15, 36));
            return result;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture,
                "xoroshiro1024ss-{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}-{8}-{9}-{10}-{11}-{12}-{13}-{14}-{15}-{16}",
                (ulong)_p, _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7,
                _s8, _s9, _s10, _s11, _s12, _s13, _s14, _s15);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "xoroshiro1024ss", 17, out var parts))
            {
                return false;
            }
            _p = (int)parts[0];
            for (int i = 0; i < 16; i += 1)
            {
                Set(i, parts[i + 1]);
            }
            return true;
        }
    }

    // ==========================================================================
    // pcg 家族（lua_random.cpp RandomBasePCG：seed 经 splitmix64 seed_seq 展开）
    // ==========================================================================

    /// <summary>
    /// pcg32_oneseq（PCG-XSH-RR 64/32 单流，对应 random.pcg32_oneseq）。
    /// seed 经 splitmix64 生成 64 位初始状态，LCG 步进后输出（output_previous）。
    /// </summary>
    public struct Pcg32Oneseq : IRandomUInt32, IRng
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;

        private ulong _state;
        private long _seed;

        public Pcg32Oneseq() { this = default; Seed(0); }
        public Pcg32Oneseq(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            ulong init = (uint)sm.NextUInt64() | ((ulong)(uint)sm.NextUInt64() << 32);
            _state = unchecked((init + Increment) * Multiplier + Increment);
        }
        public long GetSeed() => _seed;

        public uint NextUInt32()
        {
            ulong old = _state;
            _state = unchecked(_state * Multiplier + Increment);
            // xsh_rr：rot = 高 5 位；xorshift 18；取高 32 位后按 rot 右旋
            uint rot = (uint)((old >> 59) & 31);
            ulong v = old ^ (old >> 18);
            uint result = (uint)(v >> 27);
            return rot == 0 ? result : (result >> (int)rot) | (result << (32 - (int)rot));
        }

        public long Integer() => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt32Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal32(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "pcg32-oneseq-{0}-{1}-{2}", Multiplier, Increment, _state);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "pcg32-oneseq", 3, out var parts))
            {
                return false;
            }
            if (parts[0] != Multiplier || parts[1] != Increment)
            {
                return false;
            }
            _state = parts[2];
            return true;
        }
    }

    /// <summary>
    /// pcg32_fast（PCG-XSH-RS 64/32 MCG，对应 random.pcg32_fast）。
    /// MCG 增量为 0，状态低 2 位固定为 3。
    /// </summary>
    public struct Pcg32Fast : IRandomUInt32, IRng
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 0UL;

        private ulong _state;
        private long _seed;

        public Pcg32Fast() { this = default; Seed(0); }
        public Pcg32Fast(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            ulong init = (uint)sm.NextUInt64() | ((ulong)(uint)sm.NextUInt64() << 32);
            _state = init | 3UL;
        }
        public long GetSeed() => _seed;

        public uint NextUInt32()
        {
            ulong old = _state;
            _state = unchecked(_state * Multiplier);
            // xsh_rs：rshift = 高 3 位；xorshift 22；按 rshift 右移
            uint rshift = (uint)((old >> 61) & 7);
            ulong v = old ^ (old >> 22);
            return (uint)(v >> (22 + (int)rshift));
        }

        public long Integer() => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt32Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal32(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "pcg32-fast-{0}-{1}-{2}", Multiplier, Increment, _state);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts(data, "pcg32-fast", 3, out var parts))
            {
                return false;
            }
            if (parts[0] != Multiplier || parts[1] != Increment)
            {
                return false;
            }
            _state = parts[2];
            return true;
        }
    }

    /// <summary>
    /// pcg64_oneseq（PCG-XSL-RR 128/64 单流，对应 random.pcg64_oneseq）。
    /// 128 位 LCG（本机 MSVC 为 pcg_uint128 的 uint_x4 实现，等价于 mod 2^128 乘加）。
    /// </summary>
    public struct Pcg64Oneseq : IRandomUInt64, IRng
    {
        private static readonly UInt128 Multiplier =
            (UInt128)2549297995355413924UL << 64 | 4865540595714422341UL;
        private static readonly UInt128 Increment =
            (UInt128)6364136223846793005UL << 64 | 1442695040888963407UL;

        private UInt128 _state;
        private long _seed;

        public Pcg64Oneseq() { this = default; Seed(0); }
        public Pcg64Oneseq(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            UInt128 init = (uint)sm.NextUInt64()
                | (UInt128)(uint)sm.NextUInt64() << 32
                | (UInt128)(uint)sm.NextUInt64() << 64
                | (UInt128)(uint)sm.NextUInt64() << 96;
            _state = (init + Increment) * Multiplier + Increment;
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            // output_previous = false：先步进再输出
            _state = _state * Multiplier + Increment;
            // xsl_rr：rot = 最高 6 位；xorshift 64；低 64 位按 rot 右旋
            uint rot = (uint)((_state >> 122) & 63);
            UInt128 v = _state ^ (_state >> 64);
            ulong result = (ulong)v;
            return rot == 0 ? result : (result >> (int)rot) | (result << (64 - (int)rot));
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "pcg64-oneseq-{0}-{1}-{2}",
                Multiplier, Increment, _state);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts128(data, "pcg64-oneseq", 3, out var parts))
            {
                return false;
            }
            if (parts[0] != Multiplier || parts[1] != Increment)
            {
                return false;
            }
            _state = parts[2];
            return true;
        }
    }

    /// <summary>pcg64_fast（PCG-XSL-RR 128/64 MCG，对应 random.pcg64_fast）。</summary>
    public struct Pcg64Fast : IRandomUInt64, IRng
    {
        private static readonly UInt128 Multiplier =
            (UInt128)2549297995355413924UL << 64 | 4865540595714422341UL;
        private static readonly UInt128 Increment = 0;

        private UInt128 _state;
        private long _seed;

        public Pcg64Fast() { this = default; Seed(0); }
        public Pcg64Fast(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            UInt128 init = (uint)sm.NextUInt64()
                | (UInt128)(uint)sm.NextUInt64() << 32
                | (UInt128)(uint)sm.NextUInt64() << 64
                | (UInt128)(uint)sm.NextUInt64() << 96;
            _state = init | 3;
        }
        public long GetSeed() => _seed;

        public ulong NextUInt64()
        {
            _state = _state * Multiplier;
            uint rot = (uint)((_state >> 122) & 63);
            UInt128 v = _state ^ (_state >> 64);
            ulong result = (ulong)v;
            return rot == 0 ? result : (result >> (int)rot) | (result << (64 - (int)rot));
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize()
            => string.Format(CultureInfo.InvariantCulture, "pcg64-fast-{0}-{1}-{2}",
                Multiplier, Increment, _state);

        public bool Deserialize(string data)
        {
            if (!RngSerializeHelper.TryParseParts128(data, "pcg64-fast", 3, out var parts))
            {
                return false;
            }
            if (parts[0] != Multiplier || parts[1] != Increment)
            {
                return false;
            }
            _state = parts[2];
            return true;
        }
    }

    // ==========================================================================
    // jsf / sfc 家族（lua_random.cpp RandomBaseOther：seed 经 splitmix64 取 1~3 个
    // 结果作为构造参数；不提供 serialize/deserialize）
    // ==========================================================================

    /// <summary>
    /// jsf32（Bob Jenkins Small Fast，jsf&lt;uint32,27,17,0&gt;，对应 random.jsf32）。
    /// </summary>
    public struct Jsf32 : IRandomUInt32, IRng
    {
        private uint _a, _b, _c, _d;
        private long _seed;

        public Jsf32() { this = default; Seed(0); }
        public Jsf32(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            Init((uint)sm.NextUInt64());
        }
        public long GetSeed() => _seed;

        private void Init(uint seed)
        {
            _a = 0xf1ea5eedu;
            _b = seed;
            _c = seed;
            _d = seed;
            for (int i = 0; i < 20; i += 1)
            {
                Advance();
            }
        }

        private void Advance()
        {
            uint e = _a - BitRot.Rotl(_b, 27);
            _a = _b ^ BitRot.Rotl(_c, 17);
            _b = _c + _d; // r = 0：不旋转
            _c = _d + e;
            _d = e + _a;
        }

        public uint NextUInt32()
        {
            Advance();
            return _d;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt32Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal32(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize() => null;
        public bool Deserialize(string data) => false;
    }

    /// <summary>
    /// jsf64（Bob Jenkins Small Fast，jsf&lt;uint64,7,13,37&gt;，对应 random.jsf64）。
    /// </summary>
    public struct Jsf64 : IRandomUInt64, IRng
    {
        private ulong _a, _b, _c, _d;
        private long _seed;

        public Jsf64() { this = default; Seed(0); }
        public Jsf64(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            Init((ulong)sm.NextUInt64());
        }
        public long GetSeed() => _seed;

        private void Init(ulong seed)
        {
            _a = 0xf1ea5eedu; // 引擎同款：uint32 常量提升为 uint64
            _b = seed;
            _c = seed;
            _d = seed;
            for (int i = 0; i < 20; i += 1)
            {
                Advance();
            }
        }

        private void Advance()
        {
            ulong e = _a - BitRot.Rotl(_b, 7);
            _a = _b ^ BitRot.Rotl(_c, 13);
            _b = _c + BitRot.Rotl(_d, 37);
            _c = _d + e;
            _d = e + _a;
        }

        public ulong NextUInt64()
        {
            Advance();
            return _d;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize() => null;
        public bool Deserialize(string data) => false;
    }

    /// <summary>
    /// sfc32（Chris Doty-Humphrey SFC，sfc&lt;uint32,21,9,3&gt;，对应 random.sfc32）。
    /// </summary>
    public struct Sfc32 : IRandomUInt32, IRng
    {
        private uint _a, _b, _c, _d;
        private long _seed;

        public Sfc32() { this = default; Seed(0); }
        public Sfc32(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            Init((uint)sm.NextUInt64(), (uint)sm.NextUInt64(), (uint)sm.NextUInt64());
        }
        public long GetSeed() => _seed;

        private void Init(uint seed1, uint seed2, uint seed3)
        {
            _a = seed3;
            _b = seed2;
            _c = seed1;
            _d = 1;
            for (int i = 0; i < 12; i += 1)
            {
                _ = NextUInt32();
            }
        }

        public uint NextUInt32()
        {
            uint tmp = _a + _b + _d;
            _d += 1;
            _a = _b ^ (_b >> 9);
            _b = _c + (_c << 3);
            _c = BitRot.Rotl(_c, 21) + tmp;
            return tmp;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt32Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal32(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal32(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt32Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize() => null;
        public bool Deserialize(string data) => false;
    }

    /// <summary>
    /// sfc64（Chris Doty-Humphrey SFC，sfc&lt;uint64,24,11,3&gt;，对应 random.sfc64）。
    /// </summary>
    public struct Sfc64 : IRandomUInt64, IRng
    {
        private ulong _a, _b, _c, _d;
        private long _seed;

        public Sfc64() { this = default; Seed(0); }
        public Sfc64(long seed) { this = default; Seed(seed); }

        public void Seed(long seed)
        {
            _seed = seed;
            var sm = new SplitMix64(seed);
            Init(sm.NextUInt64(), sm.NextUInt64(), sm.NextUInt64());
        }
        public long GetSeed() => _seed;

        private void Init(ulong seed1, ulong seed2, ulong seed3)
        {
            _a = seed3;
            _b = seed2;
            _c = seed1;
            _d = 1;
            for (int i = 0; i < 12; i += 1)
            {
                _ = NextUInt64();
            }
        }

        public ulong NextUInt64()
        {
            ulong tmp = _a + _b + _d;
            _d += 1;
            _a = _b ^ (_b >> 11);
            _b = _c + (_c << 3);
            _c = BitRot.Rotl(_c, 24) + tmp;
            return tmp;
        }

        public long Integer() => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, long.MaxValue);
        public long Integer(long b) => MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, b < 0 ? -b : b);
        public long Integer(long a, long b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformInt64Modern(ref this, a, b);
        }
        public double Number() => MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.NextAfterOne);
        public double Number(double b)
        {
            if (b < 0.0) b = -b;
            return MsvcRandomAdapter.UniformReal64(ref this, 0.0, MsvcRandomAdapter.BitIncrement(b));
        }
        public double Number(double a, double b)
        {
            if (a > b) (a, b) = (b, a);
            return MsvcRandomAdapter.UniformReal64(ref this, a, MsvcRandomAdapter.BitIncrement(b));
        }
        public int Sign() => (int)(MsvcRandomAdapter.UniformInt64Modern(ref this, 0L, 1L) * 2 - 1);
        public IRng Clone() => this;

        public string? Serialize() => null;
        public bool Deserialize(string data) => false;
    }

    // ==========================================================================
    // 序列化辅助
    // ==========================================================================

    internal static class RngSerializeHelper
    {
        /// <summary>
        /// 解析 "name-v0-v1-..." 格式的序列化字符串（十进制，至少 count 个字段）。
        /// </summary>
        internal static bool TryParseParts(string data, string name, int count, out ulong[] parts)
        {
            parts = Array.Empty<ulong>();
            if (data == null || !data.StartsWith(name + "-", StringComparison.Ordinal))
            {
                return false;
            }
            var fields = data.AsSpan(name.Length + 1).ToString()
                .Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < count)
            {
                return false;
            }
            var result = new ulong[count];
            for (int i = 0; i < count; i += 1)
            {
                if (!ulong.TryParse(fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
                {
                    return false;
                }
            }
            parts = result;
            return true;
        }

        /// <summary>128 位版本（pcg64 家族）。</summary>
        internal static bool TryParseParts128(string data, string name, int count, out UInt128[] parts)
        {
            parts = Array.Empty<UInt128>();
            if (data == null || !data.StartsWith(name + "-", StringComparison.Ordinal))
            {
                return false;
            }
            var fields = data.AsSpan(name.Length + 1).ToString()
                .Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < count)
            {
                return false;
            }
            var result = new UInt128[count];
            for (int i = 0; i < count; i += 1)
            {
                if (!UInt128.TryParse(fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
                {
                    return false;
                }
            }
            parts = result;
            return true;
        }
    }

    // ==========================================================================
    // 与 C++ 基准的逐位一致性自检（期望值由 MSVC 14.44 编译引擎同款实现的
    // 固定种子（42）基准程序生成，覆盖裸输出、integer/number/sign 分布语义、
    // serialize 格式与 deserialize 回环）
    // ==========================================================================

    /// <summary>单个 RNG 的期望基准值（seed = 42）。</summary>
    public readonly struct RngExpect
    {
        public readonly ulong[] Next;        // 前 4 个裸输出
        public readonly long Int0To10;       // integer(0, 10) 首个结果
        public readonly long IntM5To5;       // integer(-5, 5) 首个结果
        public readonly double Num01;        // number() 首个结果
        public readonly double Num2p5To7p5;  // number(2.5, 7.5) 首个结果
        public readonly long Sign;           // sign() 首个结果
        public readonly string? Serialize;   // 重新 seed 后裸输出一次的序列化串

        public RngExpect(ulong n0, ulong n1, ulong n2, ulong n3,
            long int0To10, long intM5To5, double num01, double num2p5To7p5,
            long sign, string? serialize)
        {
            Next = new[] { n0, n1, n2, n3 };
            Int0To10 = int0To10;
            IntM5To5 = intM5To5;
            Num01 = num01;
            Num2p5To7p5 = num2p5To7p5;
            Sign = sign;
            Serialize = serialize;
        }
    }

    /// <summary>
    /// RNG 移植一致性自检。Run() 返回每个检查项的通过情况，
    /// 全部通过说明 C# 实现与引擎 C++ 实现（MSVC 14.44 STL 分布）逐位一致。
    /// </summary>
    public static class RngParityTests
    {
        private static readonly (string Name, RngExpect Expect)[] Expected =
        {
        // expected values from MSVC 14.44 C++ reference (seed=42)
        ("splitmix64", new RngExpect(0xa759ea27d4727622UL, 0xbdd732262feb6e95UL, 0x28efe333b266f103UL, 0x47526757130f9f52UL, 3, -2, 0.6537157389870546, 5.7685786949352735, -1, "splitmix64-11400714819323198527")),
        ("xoshiro128p", new RngExpect(0x00000000e7821574UL, 0x000000001024c1ccUL, 0x000000001569f329UL, 0x000000002c852caeUL, 7, 2, 0.063060867810698321, 2.8153043390534918, -1, "xoshiro128p-3902179301-1241508276-2966007073-663632359")),
        ("xoshiro128pp", new RngExpect(0x00000000957d3095UL, 0x00000000faf76dedUL, 0x000000003be0ec40UL, 0x000000005c5b66cbUL, 7, 2, 0.98033797312154025, 7.4016898656077013, 1, "xoshiro128pp-3902179301-1241508276-2966007073-663632359")),
        ("xoshiro128ss", new RngExpect(0x0000000031381cafUL, 0x00000000fe0a53f8UL, 0x00000000a12d598cUL, 0x00000000ea4f3141UL, 7, 2, 0.99234509286290573, 7.4617254643145285, 1, "xoshiro128ss-3902179301-1241508276-2966007073-663632359")),
        ("xoroshiro128p", new RngExpect(0x65311c4e045de4b7UL, 0x587e68d21364419cUL, 0x49fc8a715bddfbe5UL, 0xd4fc54b24af19967UL, 7, 2, 0.39528061775887802, 4.47640308879439, 1, "xoroshiro128p-16526893343457231197-8296500104133279807")),
        ("xoroshiro128pp", new RngExpect(0xdff5f2e39de14084UL, 0xe52e3b5074dcddafUL, 0xeace7b1a47db8e18UL, 0x5490a3337163481dUL, 8, 3, 0.87484663064049539, 6.8742331532024767, -1, "xoroshiro128pp-17795100439540576339-2286018313960615296")),
        ("xoroshiro128ss", new RngExpect(0x6714802c0f61fe32UL, 0x86b37c02903eaea5UL, 0x5a45af60bacd7c97UL, 0xda1643c03a851cd7UL, 4, -1, 0.40265656543432016, 4.5132828271716008, -1, "xoroshiro128ss-16526893343457231197-8296500104133279807")),
        ("xoshiro256p", new RngExpect(0xeeac517ee7821574UL, 0xfc15bea79344af81UL, 0x2b4a90bf4453a637UL, 0xc2ab13c98202a132UL, 5, 0, 0.93231686924219348, 7.161584346210967, -1, "xoshiro256p-6763491120393914341-3630247861813242292-17003998737455810337-11401143178614417308")),
        ("xoshiro256pp", new RngExpect(0x66cdab328ee9cc4aUL, 0xb1a661aea99492c4UL, 0x5127a507275cef15UL, 0x30c8bd69c27e5260UL, 7, 2, 0.40157575592358019, 4.5078787796179007, -1, "xoshiro256pp-6763491120393914341-3630247861813242292-17003998737455810337-11401143178614417308")),
        ("xoshiro256ss", new RngExpect(0x69e85b3631381baaUL, 0x8bb3eb80fe0a5665UL, 0x50039950ba045a9aUL, 0xd2a16f45961aa4edUL, 0, -5, 0.41370172570279395, 4.5685086285139693, -1, "xoshiro256ss-6763491120393914341-3630247861813242292-17003998737455810337-11401143178614417308")),
        ("xoshiro512p", new RngExpect(0xd049cd5b86d96725UL, 0x08d3e4f24f073445UL, 0x651faddd68245632UL, 0xffb811fea2ec6caaUL, 8, 3, 0.81362613186397481, 6.5681306593198734, 1, "xoshiro512p-8727373399056362788-3630247861813242292-10355474376401651489-2255888519962918086-17039241110035738355-13000601830338809191-1688287795909263625-678573626641749856")),
        ("xoshiro512pp", new RngExpect(0xc3a6f0e680b29196UL, 0x599aa722ce9e98c8UL, 0x5266a3123b4af444UL, 0xbcd54fb3312e0598UL, 3, -2, 0.76426606776721906, 6.3213303388360949, -1, "xoshiro512pp-8727373399056362788-3630247861813242292-10355474376401651489-2255888519962918086-17039241110035738355-13000601830338809191-1688287795909263625-678573626641749856")),
        ("xoshiro512ss", new RngExpect(0x69e85b3631381baaUL, 0x8bb3eb80fe0a5665UL, 0xd590a0ffc3b31243UL, 0xa8b2f33907ddf616UL, 0, -5, 0.41370172570279395, 4.5685086285139693, -1, "xoshiro512ss-8727373399056362788-3630247861813242292-10355474376401651489-2255888519962918086-17039241110035738355-13000601830338809191-1688287795909263625-678573626641749856")),
        ("xoroshiro1024s", new RngExpect(0x647d34a27a1b610fUL, 0xc44120bd423f5739UL, 0x4601ff81b7088b16UL, 0x59e9035a8a9293fcUL, 10, 5, 0.39253548590396081, 4.462677429519804, 1, "xoroshiro1024s-1-6417002856508995283-13371622088921415711-2949826092126892291-5139283748462763858-6349198060258255764-701532786141963250-16015981125662989062-4028864712777624925-14769051326987775908-6270620877612482005-11408980392250668974-3779771651426294207-9094045341461139646-9470486766231111398-9592552252706221495-12270025419241524956")),
        ("xoroshiro1024pp", new RngExpect(0xce5c191a30250eb0UL, 0x0c3f35aa3a5ec0d6UL, 0x38cc8cdad1de88ddUL, 0xed980865b8aac12cUL, 3, -2, 0.80609280480725365, 6.5304640240362675, -1, "xoroshiro1024pp-1-6417002856508995283-13371622088921415711-2949826092126892291-5139283748462763858-6349198060258255764-701532786141963250-16015981125662989062-4028864712777624925-14769051326987775908-6270620877612482005-11408980392250668974-3779771651426294207-9094045341461139646-9470486766231111398-9592552252706221495-12270025419241524956")),
        ("xoroshiro1024ss", new RngExpect(0x69e85b3631381baaUL, 0x15780b2e0c2ec716UL, 0xbe15272cdf80b6c2UL, 0x89dceac19500853cUL, 0, -5, 0.41370172570279395, 4.5685086285139693, -1, "xoroshiro1024ss-1-6417002856508995283-13371622088921415711-2949826092126892291-5139283748462763858-6349198060258255764-701532786141963250-16015981125662989062-4028864712777624925-14769051326987775908-6270620877612482005-11408980392250668974-3779771651426294207-9094045341461139646-9470486766231111398-9592552252706221495-12270025419241524956")),
        ("pcg32_oneseq", new RngExpect(0x00000000ab769c07UL, 0x00000000101406acUL, 0x0000000022450302UL, 0x000000003d4925aaUL, 7, 2, 0.062805573611934298, 2.8140278680596715, 1, "pcg32-oneseq-6364136223846793005-1442695040888963407-13452374115624353803")),
        ("pcg32_fast", new RngExpect(0x00000000d6dc54f3UL, 0x00000000528429d0UL, 0x00000000066356ecUL, 0x000000006b7a0183UL, 9, 4, 0.3223291525708743, 4.111645762854371, 1, "pcg32-fast-6364136223846793005-0-16884142970123133223")),
        ("pcg64_oneseq", new RngExpect(0x6230c555ac188549UL, 0x023d69952a88f412UL, 0x21b6472cffd8eb91UL, 0xaa8e3ec5c31062bcUL, 4, -1, 0.3835566839371457, 4.417783419685728, -1, "pcg64-oneseq-47026247687942121848144207491837523525-117397592171526113268558934119004209487-140599537986157214572781084058869757475")),
        ("pcg64_fast", new RngExpect(0xd1ab5e53b7c69bddUL, 0x0e77110f3f2bc561UL, 0xa6ddf5468ceee530UL, 0x66ab813791324c4cUL, 9, 4, 0.81902112525838311, 6.5951056262919154, 1, "pcg64-fast-47026247687942121848144207491837523525-0-334571375229998311057713344699257026927")),
        ("jsf32", new RngExpect(0x000000006a00dab2UL, 0x00000000e5e478f2UL, 0x00000000fdf467cbUL, 0x000000001965b536UL, 4, -1, 0.89801746290504847, 6.9900873145252422, -1, null)),
        ("jsf64", new RngExpect(0xb37194993f71f6b4UL, 0x1af0f72eb02c7ea0UL, 0xf371222d6055309eUL, 0xe57bcf281ecf11dcUL, 7, 2, 0.7009518503322808, 6.0047592516614046, 1, null)),
        ("sfc32", new RngExpect(0x00000000fc88c778UL, 0x00000000cb9817efUL, 0x000000001569f700UL, 0x00000000e403b8faUL, 10, 5, 0.79528951272053239, 6.4764475636026617, 1, null)),
        ("sfc64", new RngExpect(0x859de12eea677a79UL, 0xebd26f726e8db4e1UL, 0x226eacc5b758a103UL, 0xc8b2babe9c5d5fc7UL, 5, 0, 0.52194030185128604, 5.1097015092564302, 1, null)),
        };

        private static IRng Make(string name) => name switch
        {
            "splitmix64" => new SplitMix64(),
            "xoshiro128p" => new Xoshiro128P(),
            "xoshiro128pp" => new Xoshiro128PP(),
            "xoshiro128ss" => new Xoshiro128SS(),
            "xoroshiro128p" => new Xoroshiro128P(),
            "xoroshiro128pp" => new Xoroshiro128PP(),
            "xoroshiro128ss" => new Xoroshiro128SS(),
            "xoshiro256p" => new Xoshiro256P(),
            "xoshiro256pp" => new Xoshiro256PP(),
            "xoshiro256ss" => new Xoshiro256SS(),
            "xoshiro512p" => new Xoshiro512P(),
            "xoshiro512pp" => new Xoshiro512PP(),
            "xoshiro512ss" => new Xoshiro512SS(),
            "xoroshiro1024s" => new Xoroshiro1024S(),
            "xoroshiro1024pp" => new Xoroshiro1024PP(),
            "xoroshiro1024ss" => new Xoroshiro1024SS(),
            "pcg32_oneseq" => new Pcg32Oneseq(),
            "pcg32_fast" => new Pcg32Fast(),
            "pcg64_oneseq" => new Pcg64Oneseq(),
            "pcg64_fast" => new Pcg64Fast(),
            "jsf32" => new Jsf32(),
            "jsf64" => new Jsf64(),
            "sfc32" => new Sfc32(),
            "sfc64" => new Sfc64(),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        private static ulong NextRaw(IRng rng)
            => rng is IRandomUInt64 r64 ? r64.NextUInt64() : ((IRandomUInt32)rng).NextUInt32();

        /// <summary>执行全部一致性检查，返回 (检查项, 是否通过, 失败详情)。</summary>
        public static System.Collections.Generic.IReadOnlyList<(string Name, bool Pass, string? Detail)> Run()
        {
            var results = new System.Collections.Generic.List<(string, bool, string?)>();

            foreach (var (name, expect) in Expected)
            {
                // 裸输出序列
                var rng = Make(name);
                rng.Seed(42);
                for (int i = 0; i < 4; i += 1)
                {
                    ulong v = NextRaw(rng);
                    if (v != expect.Next[i])
                    {
                        results.Add((name + ".next[" + i + "]", false,
                            "expect 0x" + expect.Next[i].ToString("x16") + ", got 0x" + v.ToString("x16")));
                    }
                }

                // 分布语义
                rng = Make(name);
                rng.Seed(42);
                long i10 = rng.Integer(0, 10);
                rng = Make(name);
                rng.Seed(42);
                long im5 = rng.Integer(-5, 5);
                rng = Make(name);
                rng.Seed(42);
                double n01 = rng.Number();
                rng = Make(name);
                rng.Seed(42);
                double nab = rng.Number(2.5, 7.5);
                rng = Make(name);
                rng.Seed(42);
                long sign = rng.Sign();
                rng = Make(name);
                rng.Seed(42);
                long seedBack = rng.GetSeed();

                if (i10 != expect.Int0To10)
                {
                    results.Add((name + ".integer(0,10)", false,
                        "expect " + expect.Int0To10 + ", got " + i10));
                }
                if (im5 != expect.IntM5To5)
                {
                    results.Add((name + ".integer(-5,5)", false,
                        "expect " + expect.IntM5To5 + ", got " + im5));
                }
                if (n01 != expect.Num01)
                {
                    results.Add((name + ".number()", false,
                        "expect " + expect.Num01.ToString("G17") + ", got " + n01.ToString("G17")));
                }
                if (nab != expect.Num2p5To7p5)
                {
                    results.Add((name + ".number(2.5,7.5)", false,
                        "expect " + expect.Num2p5To7p5.ToString("G17") + ", got " + nab.ToString("G17")));
                }
                if (sign != expect.Sign)
                {
                    results.Add((name + ".sign()", false,
                        "expect " + expect.Sign + ", got " + sign));
                }
                if (seedBack != 42)
                {
                    results.Add((name + ".getSeed()", false, "expect 42, got " + seedBack));
                }

                // 序列化 / 反序列化
                if (expect.Serialize is not null)
                {
                    rng = Make(name);
                    rng.Seed(42);
                    _ = NextRaw(rng);
                    string? ser = rng.Serialize();
                    if (ser != expect.Serialize)
                    {
                        results.Add((name + ".serialize()", false,
                            "expect [" + expect.Serialize + "], got [" + ser + "]"));
                    }
                    var other = Make(name);
                    if (!other.Deserialize(expect.Serialize))
                    {
                        results.Add((name + ".deserialize()", false, "parse failed"));
                    }
                    else if (NextRaw(other) != expect.Next[1])
                    {
                        results.Add((name + ".deserialize()", false, "state mismatch after roundtrip"));
                    }
                }
                else
                {
                    rng = Make(name);
                    if (rng.Serialize() is not null)
                    {
                        results.Add((name + ".serialize()", false, "expected unsupported"));
                    }
                }
            }

            // well512（lstg.Rand）
            {
                var w = new Well512(42);
                ulong[] wNext = { 0xd35f0383UL, 0x575bf942UL, 0xd9362112UL, 0xeb10598cUL };
                for (int i = 0; i < 4; i += 1)
                {
                    uint v = w.Next();
                    if (v != (uint)wNext[i])
                    {
                        results.Add(("well512.next[" + i + "]", false,
                            "expect 0x" + wNext[i].ToString("x8") + ", got 0x" + v.ToString("x8")));
                    }
                }
                w = new Well512(42);
                if (w.Integer(1, 6) != 6 || w.Integer(1, 6) != 5)
                {
                    results.Add(("well512.integer(1,6)", false, "sequence mismatch"));
                }
                w = new Well512(42);
                if (w.Number() != 0.215848997f)
                {
                    results.Add(("well512.number()", false,
                        "expect 0.215848997, got " + w.Number().ToString("G9")));
                }
                w = new Well512(42);
                _ = w.Number(); // C++ 基准连续两次取数
                if (w.Number(2f, 5f) != 3.93173885f)
                {
                    results.Add(("well512.number(2,5)", false, "second draw mismatch"));
                }
                w = new Well512(42);
                if (w.Sign() != 1 || w.Sign() != -1)
                {
                    results.Add(("well512.sign()", false, "sequence mismatch"));
                }
                w = new Well512(42);
                _ = w.Next();
                const string wSer =
                    "well512-15-1476515419-3107752595-1895908407-3900362577-3030691166-4081230161-2732361568-1361238961-3961642104-867618704-2837705690-3281374275-3928479052-3691474744-3088217429-3546219395-";
                if (w.Serialize() != wSer)
                {
                    results.Add(("well512.serialize()", false, "string mismatch"));
                }
                var w2 = new Well512(0);
                if (!w2.Deserialize(wSer) || w2.Next() != 0x575bf942u)
                {
                    results.Add(("well512.deserialize()", false, "roundtrip mismatch"));
                }
            }

            return results;
        }

        /// <summary>全部检查是否通过。</summary>
        public static bool AllPass()
        {
            foreach (var (_, pass, _) in Run())
            {
                if (!pass)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
