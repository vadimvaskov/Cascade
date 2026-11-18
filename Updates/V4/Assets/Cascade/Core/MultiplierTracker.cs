namespace Cascade.Core
{
    /// <summary>Множитель ритма: каждые N попаданий подряд дают +1, промах сбрасывает к x1.</summary>
    public sealed class MultiplierTracker
    {
        private readonly BalanceConfig config;

        public MultiplierTracker(BalanceConfig config)
        {
            this.config = config;
            Multiplier = 1;
        }

        public int Multiplier { get; private set; }
        public int Streak { get; private set; }

        public void RegisterHit()
        {
            Streak++;
            int step = config.hitsPerMultiplierStep < 1 ? 1 : config.hitsPerMultiplierStep;
            if (Streak % step == 0 && Multiplier < config.maxMultiplier)
            {
                Multiplier++;
            }
        }

        public void RegisterMiss()
        {
            Streak = 0;
            Multiplier = 1;
        }

        public void Reset()
        {
            Streak = 0;
            Multiplier = 1;
        }
    }
}
