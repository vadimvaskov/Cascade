namespace Cascade.Core
{
    /// <summary>Случайная следующая сфера. Фиолетовая появляется только начиная с множителя из таблицы.</summary>
    public sealed class SphereGenerator
    {
        private readonly DeterministicRandom random;
        private readonly BalanceConfig config;

        public SphereGenerator(BalanceConfig config, DeterministicRandom random)
        {
            this.config = config;
            this.random = random;
        }

        public int Next(int multiplier)
        {
            float violet = config.VioletChance(multiplier);
            if (violet > 0f && random.NextDouble() < violet)
            {
                return 4;
            }
            float total = config.weightBlue + config.weightYellow + config.weightRed;
            if (total <= 0f)
            {
                return 1;
            }
            double roll = random.NextDouble() * total;
            if (roll < config.weightBlue)
            {
                return 1;
            }
            if (roll < config.weightBlue + config.weightYellow)
            {
                return 2;
            }
            return 3;
        }
    }
}
