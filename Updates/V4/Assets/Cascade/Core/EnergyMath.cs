using System;

namespace Cascade.Core
{
    /// <summary>Музыкальная энергия: запас, расход за поражение и восстановление по реальному времени.</summary>
    public static class EnergyMath
    {
        /// <summary>
        /// Начисляет энергию за прошедшее время. lastTicks — момент, от которого идёт отсчёт следующей единицы
        /// (DateTime.UtcNow.Ticks). Возвращает новое значение энергии и обновляет lastTicks.
        /// </summary>
        public static int Restore(int energy, ref long lastTicks, long nowTicks, int maxEnergy, double minutesPerUnit)
        {
            if (energy >= maxEnergy)
            {
                lastTicks = nowTicks;
                return maxEnergy;
            }
            if (lastTicks <= 0 || nowTicks < lastTicks)
            {
                lastTicks = nowTicks;
                return energy;
            }
            long unitTicks = (long)(minutesPerUnit * TimeSpan.TicksPerMinute);
            if (unitTicks <= 0)
            {
                lastTicks = nowTicks;
                return maxEnergy;
            }
            long gained = (nowTicks - lastTicks) / unitTicks;
            if (gained <= 0)
            {
                return energy;
            }
            long total = energy + gained;
            if (total >= maxEnergy)
            {
                lastTicks = nowTicks;
                return maxEnergy;
            }
            lastTicks += gained * unitTicks;
            return (int)total;
        }

        /// <summary>Сколько секунд осталось до следующей единицы энергии (0, если запас полный).</summary>
        public static double SecondsToNext(int energy, long lastTicks, long nowTicks, int maxEnergy, double minutesPerUnit)
        {
            if (energy >= maxEnergy)
            {
                return 0;
            }
            double unitSeconds = minutesPerUnit * 60.0;
            double passed = (nowTicks - lastTicks) / (double)TimeSpan.TicksPerSecond;
            double left = unitSeconds - passed;
            return left < 0 ? 0 : left;
        }

        /// <summary>Монеты за победу.</summary>
        public static int CoinsForWin(BalanceConfig config, int stars, int score, int maxCascade)
        {
            double coins = stars * config.coinsPerStar + score / 1000.0 * config.coinsPerThousandScore + maxCascade * config.coinsPerCascadeStep;
            return (int)Math.Round(coins, MidpointRounding.AwayFromZero);
        }
    }
}
