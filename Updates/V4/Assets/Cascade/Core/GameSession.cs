using System;

namespace Cascade.Core
{
    public enum SessionState
    {
        Ready = 0,
        Playing = 1,
        Won = 2,
        Lost = 3
    }

    public enum DefeatReason
    {
        None = 0,
        NotEnoughScore = 1,
        BoardFull = 2,
        Idle = 3
    }

    /// <summary>Параметры одного уровня, нужные ядру.</summary>
    public sealed class LevelRules
    {
        public int MinScore;
        public double Duration;
        public int Seed;
        /// <summary>В обучении поражений нет.</summary>
        public bool NoDefeat;
    }

    /// <summary>
    /// Одна игровая попытка: поле, текущая сфера, очки, множитель, победа и поражение.
    /// Время берётся снаружи (из аудио), ядро само ничего не отсчитывает по кадрам.
    /// </summary>
    public sealed class GameSession
    {
        private readonly BalanceConfig config;
        private readonly LevelRules rules;
        private readonly RhythmJudge judge;
        private readonly MultiplierTracker multiplier;
        private readonly CascadeResolver resolver = new CascadeResolver();
        private readonly DeterministicRandom random;
        private readonly SphereGenerator generator;
        private readonly MoveResult result = new MoveResult();

        public GameSession(BalanceConfig config, BeatMapData beatMap, LevelRules rules)
        {
            this.config = config;
            this.rules = rules;
            Board = new Board(config.boardSize);
            judge = new RhythmJudge(beatMap, config);
            multiplier = new MultiplierTracker(config);
            random = new DeterministicRandom(rules.Seed);
            generator = new SphereGenerator(config, random);
            State = SessionState.Ready;
        }

        public Board Board { get; private set; }
        public RhythmJudge Judge { get { return judge; } }
        public SessionState State { get; private set; }
        public DefeatReason Defeat { get; private set; }
        public int CurrentSphere { get; private set; }
        public int Score { get; private set; }
        public int Multiplier { get { return multiplier.Multiplier; } }
        public int Streak { get { return multiplier.Streak; } }
        public int Placements { get; private set; }
        public int Hits { get; private set; }
        public int Perfects { get; private set; }
        public int MaxCascade { get; private set; }
        public double IdleSeconds { get; private set; }
        public int MinScore { get { return rules.MinScore; } }
        public double Duration { get { return rules.Duration; } }
        public int Seed { get { return rules.Seed; } }

        public bool IdleWarning
        {
            get { return State == SessionState.Playing && !rules.NoDefeat && IdleSeconds >= config.idleWarningSeconds; }
        }

        /// <summary>Доля установок с оценкой PERFECT или GOOD, от 0 до 1.</summary>
        public float Accuracy
        {
            get { return Placements == 0 ? 0f : (float)Hits / Placements; }
        }

        public int Stars
        {
            get { return State == SessionState.Won ? StarsForScore(Score) : 0; }
        }

        /// <summary>Подготовка уровня: стартовые сферы в случайных клетках и первая сфера игрока.</summary>
        public void Start()
        {
            Board.Clear();
            judge.Reset();
            multiplier.Reset();
            Score = 0;
            Placements = 0;
            Hits = 0;
            Perfects = 0;
            MaxCascade = 0;
            IdleSeconds = 0;
            Defeat = DefeatReason.None;
            int toPlace = Math.Min(config.startSphereCount, Board.CellCount - 1);
            for (int i = 0; i < toPlace; i++)
            {
                int cell = RandomEmptyCell();
                Board.Set(cell, generator.Next(1));
            }
            CurrentSphere = generator.Next(1);
            State = SessionState.Playing;
        }

        /// <summary>Для обучения: заданное поле и заданная текущая сфера.</summary>
        public void LoadPreset(int[] levels, int currentSphere)
        {
            Board.Clear();
            for (int i = 0; i < levels.Length && i < Board.CellCount; i++)
            {
                if (levels[i] != Board.EmptyLevel)
                {
                    Board.Set(i, levels[i]);
                }
            }
            CurrentSphere = currentSphere;
            IdleSeconds = 0;
            State = SessionState.Playing;
        }

        public int Star2Score
        {
            get { return (int)Math.Round(rules.MinScore * (double)config.star2Factor, MidpointRounding.AwayFromZero); }
        }

        public int Star3Score
        {
            get { return (int)Math.Round(rules.MinScore * (double)config.star3Factor, MidpointRounding.AwayFromZero); }
        }

        public int StarsForScore(int score)
        {
            if (score < rules.MinScore)
            {
                return 0;
            }
            if (score >= Star3Score)
            {
                return 3;
            }
            if (score >= Star2Score)
            {
                return 2;
            }
            return 1;
        }

        /// <summary>
        /// Ход игрока. Возвращает один и тот же объект результата (читать сразу).
        /// Касание занятой клетки ход не создаёт и ничего не сбрасывает.
        /// </summary>
        public MoveResult TryPlace(int cell, double songTime)
        {
            result.Reset();
            if (State != SessionState.Playing)
            {
                return result;
            }
            if (cell < 0 || cell >= Board.CellCount || !Board.IsEmpty(cell))
            {
                return result;
            }
            if (rules.Duration > 0 && songTime >= rules.Duration)
            {
                return result;
            }

            // 1. Оценка ритма и множитель этого хода
            HitJudgement judgement = judge.Judge(songTime + config.inputOffsetMs / 1000.0);
            float accuracyBonus = 1f;
            if (judgement.Duplicate)
            {
                // Вторая сфера в ту же ритмическую позицию: ход засчитан, серия не растёт и не сбрасывается
                result.DuplicatePosition = true;
                result.Grade = HitGrade.Miss;
            }
            else if (judgement.Grade == HitGrade.Miss)
            {
                multiplier.RegisterMiss();
                result.Grade = HitGrade.Miss;
            }
            else
            {
                multiplier.RegisterHit();
                result.Grade = judgement.Grade;
                Hits++;
                if (judgement.Grade == HitGrade.Perfect)
                {
                    Perfects++;
                    accuracyBonus = config.perfectBonus;
                }
                else
                {
                    accuracyBonus = config.goodBonus;
                }
            }
            result.OffsetMs = judgement.OffsetMs;
            result.Multiplier = multiplier.Multiplier;

            // 2. Постановка, передача +1, каскады
            resolver.Resolve(Board, cell, CurrentSphere, config, result);
            result.Accepted = true;
            Placements++;
            IdleSeconds = 0;
            if (result.Explosions > MaxCascade)
            {
                MaxCascade = result.Explosions;
            }

            // 3. Очки
            // Бонус точности считаем в тысячных, чтобы 10 * 1,15 давало ровно 11,5, а не 11,4999
            int bonusPermille = (int)Math.Round(accuracyBonus * 1000.0, MidpointRounding.AwayFromZero);
            double points = (double)result.RawScore * result.Multiplier * bonusPermille / 1000.0;
            result.FinalScore = (int)Math.Round(points, MidpointRounding.AwayFromZero);
            Score += result.FinalScore;

            // 4. Пассивные автосферы (по умолчанию выключены настройкой)
            for (int i = 0; i < config.autoSpheresPerMove && !Board.IsFull; i++)
            {
                int autoCell = RandomEmptyCell();
                int level = generator.Next(1);
                Board.Set(autoCell, level);
                result.Events.Add(new MoveEvent(MoveEventType.AutoSpawned, autoCell, level, result.WaveCount + 1));
            }

            // 5. Свободное место и следующая сфера
            if (Board.IsFull)
            {
                result.BoardFull = true;
                if (!rules.NoDefeat)
                {
                    State = SessionState.Lost;
                    Defeat = DefeatReason.BoardFull;
                }
            }
            CurrentSphere = generator.Next(multiplier.Multiplier);
            result.NextSphere = CurrentSphere;
            return result;
        }

        /// <summary>Отсчёт бездействия. Передавать только активное игровое время (без паузы).</summary>
        public void Tick(double deltaSeconds)
        {
            if (State != SessionState.Playing || rules.NoDefeat)
            {
                return;
            }
            IdleSeconds += deltaSeconds;
            if (IdleSeconds >= config.idleDefeatSeconds)
            {
                State = SessionState.Lost;
                Defeat = DefeatReason.Idle;
            }
        }

        /// <summary>Музыка закончилась: победа, если набран минимальный порог.</summary>
        public void FinishSong()
        {
            if (State != SessionState.Playing)
            {
                return;
            }
            if (Score >= rules.MinScore || rules.NoDefeat)
            {
                State = SessionState.Won;
            }
            else
            {
                State = SessionState.Lost;
                Defeat = DefeatReason.NotEnoughScore;
            }
        }

        private int RandomEmptyCell()
        {
            int pick = random.NextInt(Board.EmptyCount);
            for (int i = 0; i < Board.CellCount; i++)
            {
                if (!Board.IsEmpty(i))
                {
                    continue;
                }
                if (pick == 0)
                {
                    return i;
                }
                pick--;
            }
            return 0;
        }
    }
}
