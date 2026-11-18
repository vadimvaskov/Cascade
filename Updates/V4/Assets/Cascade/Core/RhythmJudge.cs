using System;

namespace Cascade.Core
{
    /// <summary>Карта ритма трека. Times — секунды от начала файла, по возрастанию.
    /// Levels: 0 — сильная доля (половинная), 1 — четверть, 2 — восьмая, 3 — шестнадцатая.</summary>
    public sealed class BeatMapData
    {
        public double[] Times = new double[0];
        public int[] Levels = new int[0];
        public double Duration;
        public double Bpm;
    }

    public struct HitJudgement
    {
        public HitGrade Grade;
        public int PositionIndex;
        public double OffsetMs;
        public bool Duplicate;
    }

    /// <summary>
    /// Оценка попадания в ритм. Берётся ближайшая размеченная позиция; окна PERFECT и GOOD сужаются
    /// на быстрых долях, чтобы не перекрывать соседние позиции. В одну позицию засчитывается одно попадание.
    /// </summary>
    public sealed class RhythmJudge
    {
        private readonly BeatMapData map;
        private readonly BalanceConfig config;
        private readonly bool[] used;

        public RhythmJudge(BeatMapData map, BalanceConfig config)
        {
            this.map = map;
            this.config = config;
            used = new bool[map.Times.Length];
        }

        public int PositionCount { get { return map.Times.Length; } }

        public bool IsUsed(int index)
        {
            return used[index];
        }

        public void Reset()
        {
            Array.Clear(used, 0, used.Length);
        }

        /// <summary>Индекс ближайшей позиции (при равенстве — более ранняя). -1, если карта пуста.</summary>
        public int FindNearest(double time)
        {
            double[] times = map.Times;
            int count = times.Length;
            if (count == 0)
            {
                return -1;
            }
            int low = 0;
            int high = count - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (times[mid] < time)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }
            // low — первая позиция со временем >= time
            if (low > 0 && time - times[low - 1] <= times[low] - time)
            {
                return low - 1;
            }
            return low;
        }

        /// <summary>Расстояние до ближайшей соседней позиции, мс.</summary>
        public double IntervalMs(int index)
        {
            double[] times = map.Times;
            double best = double.MaxValue;
            if (index > 0)
            {
                best = times[index] - times[index - 1];
            }
            if (index < times.Length - 1)
            {
                double next = times[index + 1] - times[index];
                if (next < best)
                {
                    best = next;
                }
            }
            if (best == double.MaxValue)
            {
                return 1000.0;
            }
            return best * 1000.0;
        }

        public double GoodWindowMs(int index)
        {
            double interval = IntervalMs(index);
            double window = Math.Min(config.goodWindowMs, interval * config.goodIntervalFraction);
            window = Math.Max(window, config.minGoodWindowMs);
            // Окно никогда не доходит до середины интервала: соседние позиции не перекрываются
            return Math.Min(window, interval * 0.49);
        }

        public double PerfectWindowMs(int index)
        {
            double interval = IntervalMs(index);
            double window = Math.Min(config.perfectWindowMs, interval * config.perfectIntervalFraction);
            window = Math.Max(window, config.minPerfectWindowMs);
            return Math.Min(window, GoodWindowMs(index));
        }

        public HitJudgement Judge(double songTime)
        {
            HitJudgement judgement = new HitJudgement();
            judgement.Grade = HitGrade.Miss;
            judgement.PositionIndex = -1;
            int index = FindNearest(songTime);
            if (index < 0)
            {
                return judgement;
            }
            double offsetMs = (songTime - map.Times[index]) * 1000.0;
            double distance = Math.Abs(offsetMs);
            judgement.OffsetMs = offsetMs;
            if (distance > GoodWindowMs(index))
            {
                return judgement;
            }
            judgement.PositionIndex = index;
            if (used[index])
            {
                judgement.Duplicate = true;
                return judgement;
            }
            used[index] = true;
            judgement.Grade = distance <= PerfectWindowMs(index) ? HitGrade.Perfect : HitGrade.Good;
            return judgement;
        }
    }
}
