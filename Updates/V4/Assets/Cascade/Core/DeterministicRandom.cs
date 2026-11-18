namespace Cascade.Core
{
    /// <summary>Свой генератор случайных чисел: одинаковый результат на всех платформах при одном зерне.</summary>
    public sealed class DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = (uint)seed;
            if (state == 0)
            {
                state = 0x9E3779B9u;
            }
            // Прогрев, чтобы близкие зёрна давали разные последовательности
            for (int i = 0; i < 8; i++)
            {
                NextUInt();
            }
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Число в диапазоне [0, 1).</summary>
        public double NextDouble()
        {
            return (NextUInt() >> 8) / 16777216.0;
        }

        /// <summary>Целое в диапазоне [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
            {
                return 0;
            }
            int value = (int)(NextDouble() * maxExclusive);
            if (value >= maxExclusive)
            {
                value = maxExclusive - 1;
            }
            return value;
        }
    }
}
