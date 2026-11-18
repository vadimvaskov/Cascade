using System;

namespace Cascade.Core
{
    /// <summary>
    /// Все числа баланса в одном месте. Игровая логика читает значения отсюда и не содержит своих чисел.
    /// Значения по умолчанию — стартовые из ТЗ от 18.11.2026; их можно менять без правки алгоритмов.
    /// </summary>
    [Serializable]
    public sealed class BalanceConfig
    {
        // Поле
        public int boardSize = 6;
        public int startSphereCount = 6;
        // Сколько сфер игра сама добавляет в случайные свободные клетки после каждого хода игрока.
        // Такие сферы пассивные: соседям +1 не дают и каскад не запускают.
        public int autoSpheresPerMove = 1;

        // Генератор сфер (веса синей, жёлтой, красной)
        public float weightBlue = 45f;
        public float weightYellow = 35f;
        public float weightRed = 20f;
        // Вероятность фиолетовой по множителю: индекс = множитель (0..10)
        public float[] violetChanceByMultiplier = { 0f, 0f, 0f, 0f, 0f, 0.05f, 0.08f, 0.12f, 0.17f, 0.23f, 0.30f };

        // Очки
        public int scorePlace = 10;
        public int scoreBlueToYellow = 10;
        public int scoreYellowToRed = 25;
        public int scoreRedToViolet = 50;
        public int scoreVioletToExplosion = 80;
        public int scoreExplosion = 120;
        public int cascadeBonusFactor = 40;
        public float perfectBonus = 1.15f;
        public float goodBonus = 1f;

        // Множитель ритма
        public int hitsPerMultiplierStep = 3;
        public int maxMultiplier = 10;

        // Окна точности, миллисекунды и доля интервала между соседними позициями
        public float perfectWindowMs = 100f;
        public float goodWindowMs = 250f;
        public float perfectIntervalFraction = 0.18f;
        public float goodIntervalFraction = 0.38f;
        public float minPerfectWindowMs = 22f;
        public float minGoodWindowMs = 50f;

        // Звёзды
        public float star2Factor = 1.3f;
        public float star3Factor = 1.7f;

        // Бездействие
        public float idleWarningSeconds = 10f;
        public float idleDefeatSeconds = 15f;

        // Энергия
        public int maxEnergy = 5;
        public float energyRestoreMinutes = 30f;

        // Монеты за победу
        public int coinsPerStar = 20;
        public float coinsPerThousandScore = 0.5f;
        public int coinsPerCascadeStep = 5;

        // Компенсация задержки касания и звука, мс (прибавляется ко времени касания)
        public float inputOffsetMs = 0f;

        public float VioletChance(int multiplier)
        {
            if (violetChanceByMultiplier == null || violetChanceByMultiplier.Length == 0)
            {
                return 0f;
            }
            int index = multiplier;
            if (index < 0)
            {
                index = 0;
            }
            if (index >= violetChanceByMultiplier.Length)
            {
                index = violetChanceByMultiplier.Length - 1;
            }
            return violetChanceByMultiplier[index];
        }

        public int UpgradeScore(int fromLevel)
        {
            switch (fromLevel)
            {
                case 1: return scoreBlueToYellow;
                case 2: return scoreYellowToRed;
                case 3: return scoreRedToViolet;
                case 4: return scoreVioletToExplosion;
                default: return 0;
            }
        }
    }
}
