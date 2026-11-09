using System;
using System.Collections.Generic;
using System.Linq;

namespace Cascade.Prototype
{
    /// <summary>
    /// Estimates a signed timing offset without changing any game settings.
    /// Beats occur at 0, Interval, ...; the first WarmupBeats are ignored.
    /// Positive offsets mean late taps and match Rhythm's originalTime - offset convention.
    /// </summary>
    public sealed class RhythmCalibration
    {
        public const int RequiredTaps = 10;
        public const int MaximumOffsetMs = 400;
        const double Epsilon = 1e-9;
        readonly double acceptanceWindow;
        readonly HashSet<int> usedBeats = new HashSet<int>();
        readonly List<Sample> samples = new List<Sample>();

        struct Sample
        {
            public int beat;
            public double error;
        }

        public int TotalBeats { get; }
        public double Interval { get; }
        public int WarmupBeats { get; }
        public int Count => samples.Count;
        // Enough samples have arrived; TryEstimate still checks their consistency.
        public bool IsComplete => Count >= RequiredTaps;

        public RhythmCalibration(int totalBeats = 16, double interval = 1, int warmupBeats = 2)
        {
            if (warmupBeats < 0) throw new ArgumentOutOfRangeException(nameof(warmupBeats));
            if (totalBeats <= warmupBeats || (long)totalBeats - warmupBeats < RequiredTaps)
                throw new ArgumentOutOfRangeException(nameof(totalBeats), "Calibration needs at least ten beats after warmup.");
            if (!Finite(interval) || interval <= 0 || !Finite(interval * (totalBeats - 1)))
                throw new ArgumentOutOfRangeException(nameof(interval));
            TotalBeats = totalBeats;
            Interval = interval;
            WarmupBeats = warmupBeats;
            // Shorter optional intervals must not assign the same tap to overlapping beats.
            acceptanceWindow = Math.Min(MaximumOffsetMs / 1000.0, interval * .45);
        }

        public bool AddTap(double elapsedFromScheduledStart)
        {
            if (!Finite(elapsedFromScheduledStart) || elapsedFromScheduledStart < 0 ||
                elapsedFromScheduledStart > (TotalBeats - 1) * Interval + acceptanceWindow + Epsilon)
                return false;
            int beat = (int)Math.Round(elapsedFromScheduledStart / Interval, MidpointRounding.AwayFromZero);
            if (beat < WarmupBeats || beat >= TotalBeats) return false;
            double error = elapsedFromScheduledStart - beat * Interval;
            if (Math.Abs(error) > acceptanceWindow + Epsilon || !usedBeats.Add(beat)) return false;
            samples.Add(new Sample { beat = beat, error = error });
            return true;
        }

        public bool TryEstimate(out int offsetMs, out string reason)
        {
            offsetMs = 0;
            reason = "Нужно не меньше 10 отдельных попаданий.";
            if (!IsComplete) return false;

            var ordered = samples.Select(x => x.error).OrderBy(x => x).ToArray();
            double median = Quantile(ordered, .5);
            var deviations = ordered.Select(x => Math.Abs(x - median)).OrderBy(x => x).ToArray();
            double mad = Quantile(deviations, .5);
            reason = "Ритм получился неровным. Повтори калибровку.";
            if (mad > .045 + Epsilon || Quantile(ordered, .75) - Quantile(ordered, .25) > .100 + Epsilon)
                return false;

            double tolerance = Math.Max(.040, Math.Min(.120, 3 * 1.4826 * mad));
            var consistent = samples.Where(x => Math.Abs(x.error - median) <= tolerance + Epsilon)
                .OrderBy(x => x.beat).ToArray();
            if (consistent.Length < Math.Max(8, (int)Math.Ceiling(Count * .75))) return false;

            // A steadily changing tapping tempo must not become a constant latency correction.
            // Median pairwise slope is robust to a stray early or late tap.
            var slopes = new List<double>();
            for (int i = 0; i < consistent.Length; i++)
                for (int j = i + 1; j < consistent.Length; j++)
                    slopes.Add((consistent[j].error - consistent[i].error) / (consistent[j].beat - consistent[i].beat));
            slopes.Sort();
            double drift = Quantile(slopes.ToArray(), .5) * (consistent[consistent.Length - 1].beat - consistent[0].beat);
            if (Math.Abs(drift) > .080 + Epsilon)
            {
                reason = "Темп касаний менялся. Попробуй держать ровный ритм.";
                return false;
            }

            double estimate = Quantile(consistent.Select(x => x.error).OrderBy(x => x).ToArray(), .5);
            offsetMs = Math.Max(-MaximumOffsetMs, Math.Min(MaximumOffsetMs,
                (int)Math.Round(estimate * 1000, MidpointRounding.AwayFromZero)));
            reason = "";
            return true;
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        static double Quantile(double[] sorted, double fraction)
        {
            double position = (sorted.Length - 1) * fraction;
            int left = (int)Math.Floor(position), right = (int)Math.Ceiling(position);
            return sorted[left] + (sorted[right] - sorted[left]) * (position - left);
        }
    }
}
