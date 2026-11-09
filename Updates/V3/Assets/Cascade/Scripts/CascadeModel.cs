using System;
using System.Collections.Generic;
using System.Linq;

namespace Cascade.Prototype
{
    [Serializable] public class Balance
    {
        public string version = "prototype-2";
        public int size = 6, initialCount = 6, threshold = 5;
        public int weight1 = 60, weight2 = 30, weight3 = 10;
        public int placementPoints = 10, explosionPoints = 50;
        public double rhythmWindow = .120, streakTimeout = 10;
        // Kept to deserialize older balance files; hitsPerStep/maxMultiplier now define all steps.
        public int doubleAt = 4, tripleAt = 8;
        public int hitsPerStep = 4, maxMultiplier = 10;
        public bool rhythmPrimaryBonus;
        public int rhythmBonusHits, rhythmBonusBeats;
        public bool TimedRhythmBonus => rhythmPrimaryBonus && rhythmBonusHits > 0 && rhythmBonusBeats > 0;
        public float presentationSeconds = .35f;
        public void Validate()
        {
            if (size < 2 || size > 7 || initialCount < 0 || initialCount > size * size || threshold != 5 || weight1 < 0 || weight2 < 0 || weight3 < 0 || weight1 + weight2 + weight3 <= 0)
                throw new ArgumentException("Некорректный баланс поля");
            if (hitsPerStep < 1 || maxMultiplier < 1 || maxMultiplier > 10 ||
                double.IsNaN(rhythmWindow) || double.IsInfinity(rhythmWindow) || rhythmWindow <= 0 ||
                double.IsNaN(streakTimeout) || double.IsInfinity(streakTimeout) || streakTimeout <= 0)
                throw new ArgumentException("Некорректные настройки ритма");
            if(rhythmBonusHits<0||rhythmBonusBeats<0||(rhythmBonusHits==0)!=(rhythmBonusBeats==0))
                throw new ArgumentException("Некорректные настройки бонуса ритма");
        }
    }
    public sealed class StableRandom
    {
        uint state;
        public StableRandom(int seed) { state = (uint)seed; if (state == 0) state = 0x6D2B79F5; }
        public int Next(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)((ulong)state * (uint)max >> 32);
        }
    }
    public sealed class Turn
    {
        public int cell, placedCharge, nextCharge, autoCell = -1, autoCharge, destroyed, points;
        public bool full;
        public int[] afterPlacement, final;
        public readonly List<int[]> waves = new List<int[]>();
        public readonly List<int[]> afterWaves = new List<int[]>();
    }
    public sealed class Board
    {
        public readonly Balance config;
        public readonly int seed;
        public readonly int[] cells;
        readonly StableRandom random;
        public int NextCharge { get; private set; }
        public int Occupied => cells.Count(x => x > 0);
        public Board(Balance settings, int seed, int[] fixture = null, int next = 1)
        {
            settings.Validate(); config = settings; this.seed = seed; random = new StableRandom(seed); cells = new int[settings.size * settings.size];
            if (fixture != null)
            {
                if (fixture.Length != cells.Length || fixture.Any(x => x < 0 || x > 4)) throw new ArgumentException("Invalid fixture");
                Array.Copy(fixture, cells, cells.Length); NextCharge = next; return;
            }
            var pool = Enumerable.Range(0, cells.Length).ToArray();
            for (int i = 0; i < settings.initialCount; i++) { int j = i + random.Next(pool.Length - i); int t = pool[i]; pool[i] = pool[j]; pool[j] = t; cells[pool[i]] = -1; }
            for (int i = 0; i < cells.Length; i++) if (cells[i] == -1) cells[i] = Charge();
            NextCharge = Charge();
        }
        int Charge() { int n = random.Next(config.weight1 + config.weight2 + config.weight3); return n < config.weight1 ? 1 : n < config.weight1 + config.weight2 ? 2 : 3; }
        public IEnumerable<int> Neighbours(int cell)
        {
            int s = config.size, x = cell % s, y = cell / s;
            if (y > 0) yield return cell - s; if (x > 0) yield return cell - 1;
            if (x + 1 < s) yield return cell + 1; if (y + 1 < s) yield return cell + s;
        }
        public Turn Place(int cell, int multiplier = 1, bool applyRhythmBonus = false)
        {
            if (cell < 0 || cell >= cells.Length || cells[cell] != 0) return null;
            var turn = new Turn { cell = cell, placedCharge = NextCharge };
            cells[cell] = NextCharge;
            foreach (int n in Neighbours(cell)) if (cells[n] > 0)
                cells[n] = cells[n] == 4 ? 5 : Math.Min(4, cells[n] + (config.rhythmPrimaryBonus && applyRhythmBonus ? 2 : 1));
            turn.afterPlacement = (int[])cells.Clone();
            while (true)
            {
                var wave = Enumerable.Range(0, cells.Length).Where(i => cells[i] >= config.threshold).ToArray();
                if (wave.Length == 0) break;
                turn.waves.Add(wave); turn.destroyed += wave.Length;
                foreach (int i in wave) cells[i] = 0;
                var impulses = new int[cells.Length];
                foreach (int i in wave) foreach (int n in Neighbours(i)) if (cells[n] > 0) impulses[n]++;
                for (int i = 0; i < cells.Length; i++) cells[i] += impulses[i];
                turn.afterWaves.Add((int[])cells.Clone());
            }
            NextCharge = Charge(); turn.nextCharge = NextCharge;
            var free = Enumerable.Range(0, cells.Length).Where(i => cells[i] == 0).ToArray();
            if (free.Length > 0) { turn.autoCell = free[random.Next(free.Length)]; turn.autoCharge = Charge(); cells[turn.autoCell] = turn.autoCharge; }
            turn.points = (config.placementPoints + config.explosionPoints * turn.destroyed) * multiplier;
            turn.full = Occupied == cells.Length; turn.final = (int[])cells.Clone(); return turn;
        }
    }
    [Serializable] public class Exclusion { public double start, end; }
    [Serializable] public class BeatMap
    {
        public const string ProvisionalReview = "automated-review-pending-listen";
        public string trackId, version, audioSha256;
        // verified means that the complete map has also received listening review.
        // Automated analysis is a separate provenance state, never promoted implicitly.
        public bool verified, analysisPassed;
        public string reviewStatus, analysisMethod;
        public double duration;
        public double[] beats = Array.Empty<double>();
        public Exclusion[] excluded = Array.Empty<Exclusion>();
        public bool IsProvisional => !verified && analysisPassed && reviewStatus == ProvisionalReview;
        public bool IsExcluded(double t) => excluded != null && excluded.Any(x => x != null && t >= x.start && t < x.end);
        // Shared by hit judgement and temporary bonuses, so the final late hit remains eligible.
        public double HitWindowEnd(int index,double window)
        {
            double beat=beats[index];
            double right=index==beats.Length-1?window:Math.Min(window,.45*(beats[index+1]-beat));
            double end=Math.Min(duration,beat+right);
            if(excluded!=null)foreach(var e in excluded)if(e.start>beat)end=Math.Min(end,e.start);
            return end;
        }
        public double Active(double a, double b)
        {
            if (!Finite(a) || !Finite(b) || b <= a) return 0;
            double value = b - a;
            if (excluded != null)
                foreach (var e in excluded)
                    if (e != null) value -= Math.Max(0, Math.Min(b, e.end) - Math.Max(a, e.start));
            return Math.Max(0, value);
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Sha256(string value) => value != null && value.Length == 64 &&
            value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        bool StructureValid(double clipLength)
        {
            if (!Finite(duration) || !Finite(clipLength) || duration <= 0 || clipLength <= 0 ||
                Math.Abs(duration - clipLength) >= .05 || !Sha256(audioSha256) ||
                beats == null || beats.Length == 0 || excluded == null) return false;
            double previous = -1;
            foreach (double beat in beats)
            {
                if (!Finite(beat) || beat < 0 || beat >= duration || beat <= previous) return false;
                previous = beat;
            }
            double previousEnd = 0;
            foreach (var interval in excluded)
            {
                if (interval == null || !Finite(interval.start) || !Finite(interval.end) ||
                    interval.start < previousEnd || interval.end <= interval.start || interval.end > duration) return false;
                previousEnd = interval.end;
            }
            return true;
        }
        // Compatibility overload: verified maps only. Runtime should use the hash-aware overload below.
        public bool Valid(double clipLength) => verified && StructureValid(clipLength);
        public bool Valid(double clipLength, string expectedSha256, bool allowProvisional)
        {
            return (verified || (allowProvisional && IsProvisional)) && StructureValid(clipLength) &&
                Sha256(expectedSha256) && string.Equals(audioSha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }
    }
    public struct RhythmResult { public int multiplier, beat; public double error; public string kind; }
    public sealed class Rhythm
    {
        readonly BeatMap map;
        readonly Balance balance;
        readonly HashSet<int> used = new HashSet<int>();
        double presentationTime, lastHitTime = double.NaN, lastEvaluationTime = double.NegativeInfinity;
        int eventStreak;
        public int Streak => StreakAt(presentationTime);
        public int Best { get; private set; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int Multiplier => MultiplierFor(Streak);
        public int UsedBeats => used.Count;
        public Rhythm(BeatMap map, Balance settings)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            balance = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();
        }
        int MultiplierFor(int streak) => Math.Min(balance.maxMultiplier, 1 + streak / balance.hitsPerStep);
        int StreakAt(double time)
        {
            if (eventStreak == 0 || double.IsNaN(lastHitTime)) return 0;
            return map.Active(lastHitTime, time) >= balance.streakTimeout ? 0 : eventStreak;
        }
        // Rendering may observe a newer DSP position than an event delivered this frame.
        // Advancing the display therefore cannot irreversibly erase event-time state.
        public void Advance(double time)
        {
            if (!double.IsNaN(time) && !double.IsInfinity(time)) presentationTime = Math.Max(presentationTime, time);
        }
        public RhythmResult Evaluate(double originalTime, double offset)
        {
            var result = new RhythmResult { multiplier = 1, beat = -1, error = double.NaN, kind = "invalid" };
            if (double.IsNaN(originalTime) || double.IsInfinity(originalTime) || originalTime < 0 || originalTime >= map.duration ||
                double.IsNaN(offset) || double.IsInfinity(offset)) return result;
            // Callers process accepted touches in original timestamp order, never in render order.
            if (originalTime < lastEvaluationTime) { result.kind = "stale"; return result; }
            lastEvaluationTime = originalTime;
            eventStreak = StreakAt(originalTime);
            Advance(originalTime);
            double t = originalTime - offset;
            result.kind = "excluded";
            if (map.IsExcluded(originalTime) || map.IsExcluded(t)) return result;
            if (map.beats == null || map.beats.Length == 0)
            {
                eventStreak = 0; Misses++; result.kind = "miss"; return result;
            }
            int index = Array.BinarySearch(map.beats, t);
            if (index < 0)
            {
                int right = ~index;
                index = right == 0 ? 0 : right == map.beats.Length ? right - 1 :
                    t - map.beats[right - 1] <= map.beats[right] - t + 1e-9 ? right - 1 : right;
            }
            double beat = map.beats[index];
            double left = index == 0 ? balance.rhythmWindow : Math.Min(balance.rhythmWindow, .45 * (beat - map.beats[index - 1]));
            double lo = Math.Max(0, beat - left), hi = map.HitWindowEnd(index,balance.rhythmWindow);
            if (map.excluded != null) foreach (var e in map.excluded)
            {
                if (e.end <= beat) lo = Math.Max(lo, e.end);
            }
            result.beat = index; result.error = t - beat;
            if (t >= lo - 1e-9 && t <= hi + 1e-9 && !map.IsExcluded(beat))
            {
                if (used.Add(index))
                {
                    eventStreak++; Hits++; Best = Math.Max(Best, eventStreak); lastHitTime = originalTime;
                    result.kind = "hit"; result.multiplier = MultiplierFor(eventStreak);
                }
                else result.kind = "repeat";
            }
            else { eventStreak = 0; Misses++; result.kind = "miss"; }
            return result;
        }
    }
}
