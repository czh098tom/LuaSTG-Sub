// WELL512 随机数发生器（对应 Lua 侧 lstg.Rand）。
// 精确移植自 LuaSTG/LuaSTG/Utility/well512.hpp/.cpp（游戏编程精粹 7 版本），
// 算法与状态布局（m_state[16] + m_index + m_seed）与引擎完全一致。

using System;
using System.Globalization;

namespace LuaSTG.Core.Rng
{
    /// <summary>
    /// WELL512 随机数发生器（对应 Lua 侧 lstg.Rand / random.well512 类）。
    /// 语义与引擎 Utility/well512.cpp 完全一致：
    /// <list type="bullet">
    /// <item><see cref="Seed(uint)"/>：线性同余扩散 16 个状态字；</item>
    /// <item><see cref="Integer(int, int)"/>：<c>a + next() % (b - a + 1)</c>（含端点，取模有偏）；</item>
    /// <item><see cref="Number()"/>：<c>next() % 1000001 / 1000000.0f</c>（float 语义，与 C++ 一致）；</item>
    /// <item><see cref="Sign()"/>：<c>next() % 2 * 2 - 1</c>；</item>
    /// <item><see cref="Serialize"/>：格式 <c>well512-{index}-{s0}-...-{s15}-</c>（注意尾部 '-'）。</item>
    /// </list>
    /// 结构体为值类型，赋值即克隆（对应 Lua 侧 clone）。
    /// </summary>
    public struct Well512
    {
        private uint _state0, _state1, _state2, _state3;
        private uint _state4, _state5, _state6, _state7;
        private uint _state8, _state9, _state10, _state11;
        private uint _state12, _state13, _state14, _state15;
        private uint _index;
        private uint _seed;

        /// <summary>使用指定种子构造（对应 Lua 侧 lstg.Rand(seed) 后立即 seed）。</summary>
        public Well512(uint seed)
        {
            this = default;
            Seed(seed);
        }

        /// <summary>获取当前种子（对应 lstg.Rand:GetSeed）。</summary>
        public uint GetSeed() => _seed;

        /// <summary>重设种子（对应 lstg.Rand:seed）。状态经线性同余扩散生成。</summary>
        public void Seed(uint seed)
        {
            _seed = seed;
            _index = 0;
            _state0 = seed;
            _state1 = 1812433253u * (_state0 ^ (_state0 >> 30)) + 1u;
            _state2 = 1812433253u * (_state1 ^ (_state1 >> 30)) + 2u;
            _state3 = 1812433253u * (_state2 ^ (_state2 >> 30)) + 3u;
            _state4 = 1812433253u * (_state3 ^ (_state3 >> 30)) + 4u;
            _state5 = 1812433253u * (_state4 ^ (_state4 >> 30)) + 5u;
            _state6 = 1812433253u * (_state5 ^ (_state5 >> 30)) + 6u;
            _state7 = 1812433253u * (_state6 ^ (_state6 >> 30)) + 7u;
            _state8 = 1812433253u * (_state7 ^ (_state7 >> 30)) + 8u;
            _state9 = 1812433253u * (_state8 ^ (_state8 >> 30)) + 9u;
            _state10 = 1812433253u * (_state9 ^ (_state9 >> 30)) + 10u;
            _state11 = 1812433253u * (_state10 ^ (_state10 >> 30)) + 11u;
            _state12 = 1812433253u * (_state11 ^ (_state11 >> 30)) + 12u;
            _state13 = 1812433253u * (_state12 ^ (_state12 >> 30)) + 13u;
            _state14 = 1812433253u * (_state13 ^ (_state13 >> 30)) + 14u;
            _state15 = 1812433253u * (_state14 ^ (_state14 >> 30)) + 15u;
        }

        private uint GetState(int i)
        {
            return i switch
            {
                0 => _state0, 1 => _state1, 2 => _state2, 3 => _state3,
                4 => _state4, 5 => _state5, 6 => _state6, 7 => _state7,
                8 => _state8, 9 => _state9, 10 => _state10, 11 => _state11,
                12 => _state12, 13 => _state13, 14 => _state14,
                _ => _state15,
            };
        }

        private void SetState(int i, uint v)
        {
            switch (i)
            {
                case 0: _state0 = v; break;
                case 1: _state1 = v; break;
                case 2: _state2 = v; break;
                case 3: _state3 = v; break;
                case 4: _state4 = v; break;
                case 5: _state5 = v; break;
                case 6: _state6 = v; break;
                case 7: _state7 = v; break;
                case 8: _state8 = v; break;
                case 9: _state9 = v; break;
                case 10: _state10 = v; break;
                case 11: _state11 = v; break;
                case 12: _state12 = v; break;
                case 13: _state13 = v; break;
                case 14: _state14 = v; break;
                default: _state15 = v; break;
            }
        }

        /// <summary>产生下一个 32 位随机数（引擎 well512::next）。</summary>
        public uint Next()
        {
            int i = (int)_index;
            uint a = GetState(i);
            uint c = GetState((i + 13) & 15);
            uint b = a ^ c ^ (a << 16) ^ (c << 15);
            c = GetState((i + 9) & 15);
            c ^= (c >> 11);
            a = b ^ c;
            SetState(i, a);
            uint d = a ^ ((a << 5) & 0xDA442D24u);
            _index = (uint)((i + 15) & 15);
            a = GetState((int)_index);
            uint v = a ^ b ^ d ^ (a << 2) ^ (b << 18) ^ (c << 28);
            SetState((int)_index, v);
            return v;
        }

        /// <summary>产生 [0, bound] 内的随机数（引擎 next(bound)，取模有偏）。</summary>
        public uint Next(uint bound)
            => Next() % (bound + 1u);

        /// <summary>
        /// 产生 [a, b] 内的随机整数（对应 lstg.Rand:integer，引擎语义为取模，含端点）。
        /// a、b 乱序时自动交换；b - a 超过 int32.MaxValue 时抛出异常（与 Lua 侧 luaL_error 对应）。
        /// </summary>
        public int Integer(int a, int b)
        {
            if (a > b)
            {
                (a, b) = (b, a);
            }
            long range = (long)b - a;
            if (range > 0x7fffffffL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(b), $"range [a:{a}, b:{b}] too large, (b - a) must <= 2147483647");
            }
            return a + (int)Next((uint)range);
        }

        /// <summary>产生 [0, 1] 内的随机浮点数（对应 lstg.Rand:number，float 语义，引擎为 next(1000000)/1000000.0f）。</summary>
        public float Number()
            => Next(1000000u) / 1000000.0f;

        /// <summary>产生 [0, bound] 内的随机浮点数（对应 lstg.Rand:number(bound)）。</summary>
        public float Number(float bound)
            => Number() * bound;

        /// <summary>产生 [a, b] 内的随机浮点数（对应 lstg.Rand:number(a, b)）。</summary>
        public float Number(float a, float b)
        {
            if (a > b)
            {
                (a, b) = (b, a);
            }
            return a + Number() * (b - a);
        }

        /// <summary>随机返回 -1 或 +1（对应 lstg.Rand:sign，引擎为 next()%2*2-1）。</summary>
        public int Sign()
            => (int)(Next(1u) * 2u) - 1;

        /// <summary>克隆（结构体值拷贝，对应 lstg.Rand:clone）。</summary>
        public Well512 Clone() => this;

        /// <summary>
        /// 序列化为字符串（对应 lstg.Rand:serialize）。
        /// 格式：<c>well512-{index}-{s0}-{s1}-...-{s15}-</c>（十进制，尾部有 '-'）。
        /// </summary>
        public string Serialize()
            => string.Create(CultureInfo.InvariantCulture,
                $"well512-{_index}-{_state0}-{_state1}-{_state2}-{_state3}-{_state4}-{_state5}-{_state6}-{_state7}" +
                $"-{_state8}-{_state9}-{_state10}-{_state11}-{_state12}-{_state13}-{_state14}-{_state15}-");

        /// <summary>
        /// 从字符串反序列化（对应 lstg.Rand:deserialize）。
        /// 格式不匹配或解析失败时返回 false 且不修改状态。
        /// </summary>
        public bool Deserialize(string data)
        {
            const string head = "well512-";
            if (data == null || !data.StartsWith(head, StringComparison.Ordinal))
            {
                return false;
            }
            var parts = data.AsSpan(head.Length).ToString().Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 17)
            {
                return false;
            }
            var state = new uint[16];
            if (!uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint index))
            {
                return false;
            }
            for (int i = 0; i < 16; i += 1)
            {
                if (!uint.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out state[i]))
                {
                    return false;
                }
            }
            _index = index;
            for (int i = 0; i < 16; i += 1)
            {
                SetState(i, state[i]);
            }
            return true;
        }
    }
}
