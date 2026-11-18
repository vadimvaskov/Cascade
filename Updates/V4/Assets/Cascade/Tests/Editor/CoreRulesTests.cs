using System;
using NUnit.Framework;

namespace Cascade.Core.Tests
{
    /// <summary>Проверки правил из раздела 19 ТЗ, которые относятся к ядру.</summary>
    public sealed class CoreRulesTests
    {
        private static BalanceConfig Config()
        {
            BalanceConfig config = new BalanceConfig();
            config.startSphereCount = 0;
            config.autoSpheresPerMove = 0;
            return config;
        }

        private static BeatMapData Beats(double interval, int count)
        {
            BeatMapData map = new BeatMapData();
            map.Times = new double[count];
            map.Levels = new int[count];
            for (int i = 0; i < count; i++)
            {
                map.Times[i] = 1.0 + i * interval;
                map.Levels[i] = 1;
            }
            map.Duration = 1.0 + count * interval + 1.0;
            return map;
        }

        private static GameSession Session(BalanceConfig config, BeatMapData map, int minScore)
        {
            LevelRules rules = new LevelRules();
            rules.MinScore = minScore;
            rules.Duration = map.Duration;
            rules.Seed = 12345;
            GameSession session = new GameSession(config, map, rules);
            session.Start();
            return session;
        }

        private static MoveResult Resolve(Board board, int cell, int level)
        {
            MoveResult result = new MoveResult();
            result.Reset();
            new CascadeResolver().Resolve(board, cell, level, new BalanceConfig(), result);
            return result;
        }

        [Test]
        public void PlacedSphereKeepsItsOwnLevel()
        {
            Board board = new Board(6);
            board.Set(board.Index(2, 2), 3);
            board.Set(board.Index(2, 4), 2);
            Resolve(board, board.Index(2, 3), 1);
            Assert.AreEqual(1, board.Get(board.Index(2, 3)));
        }

        [Test]
        public void ImpulseGoesToFourSidesNeverDiagonal()
        {
            Board board = new Board(6);
            int center = board.Index(2, 2);
            board.Set(board.Index(1, 2), 1);
            board.Set(board.Index(3, 2), 1);
            board.Set(board.Index(2, 1), 1);
            board.Set(board.Index(2, 3), 1);
            board.Set(board.Index(1, 1), 1);
            board.Set(board.Index(1, 3), 1);
            board.Set(board.Index(3, 1), 1);
            board.Set(board.Index(3, 3), 1);
            Resolve(board, center, 1);
            Assert.AreEqual(2, board.Get(board.Index(1, 2)));
            Assert.AreEqual(2, board.Get(board.Index(3, 2)));
            Assert.AreEqual(2, board.Get(board.Index(2, 1)));
            Assert.AreEqual(2, board.Get(board.Index(2, 3)));
            Assert.AreEqual(1, board.Get(board.Index(1, 1)));
            Assert.AreEqual(1, board.Get(board.Index(1, 3)));
            Assert.AreEqual(1, board.Get(board.Index(3, 1)));
            Assert.AreEqual(1, board.Get(board.Index(3, 3)));
        }

        [Test]
        public void EdgesDoNotWrapAround()
        {
            Board board = new Board(6);
            board.Set(board.Index(0, 5), 1);
            board.Set(board.Index(1, 0), 1);
            Resolve(board, board.Index(0, 0), 1);
            Assert.AreEqual(1, board.Get(board.Index(0, 5)));
            Assert.AreEqual(2, board.Get(board.Index(1, 0)));
        }

        [Test]
        public void RedBecomesVioletAndVioletExplodes()
        {
            Board board = new Board(6);
            int red = board.Index(0, 1);
            board.Set(red, 3);
            Resolve(board, board.Index(0, 0), 1);
            Assert.AreEqual(4, board.Get(red));

            Board second = new Board(6);
            int violet = second.Index(0, 1);
            second.Set(violet, 4);
            MoveResult result = Resolve(second, second.Index(0, 0), 1);
            Assert.IsTrue(second.IsEmpty(violet));
            Assert.AreEqual(1, result.Explosions);
        }

        [Test]
        public void ExplosionGivesImpulseBackToNeighbors()
        {
            Board board = new Board(6);
            int violet = board.Index(0, 1);
            board.Set(violet, 4);
            int placed = board.Index(0, 0);
            Resolve(board, placed, 1);
            // Взрыв фиолетовой даёт +1 поставленной сфере: синяя становится жёлтой
            Assert.AreEqual(2, board.Get(placed));
        }

        [Test]
        public void ChainReactionWithoutGravity()
        {
            Board board = new Board(6);
            int a = board.Index(2, 1);
            int b = board.Index(2, 2);
            int c = board.Index(2, 3);
            int far = board.Index(5, 5);
            board.Set(a, 4);
            board.Set(b, 4);
            board.Set(c, 4);
            board.Set(far, 2);
            MoveResult result = Resolve(board, board.Index(2, 0), 1);
            Assert.AreEqual(3, result.Explosions);
            Assert.AreEqual(3, result.WaveCount);
            Assert.IsTrue(board.IsEmpty(a));
            Assert.IsTrue(board.IsEmpty(b));
            Assert.IsTrue(board.IsEmpty(c));
            Assert.AreEqual(2, board.Get(far));
            // Поставленная сфера на месте: ничего не падает и не сдвигается
            Assert.AreEqual(2, board.Get(board.Index(2, 0)));
        }

        [Test]
        public void TwoExplosionsOfOneWaveAddUp()
        {
            // Две фиолетовые одной волны соседствуют с красной: она получает +2 и взрывается следующей волной
            Board board = new Board(6);
            int left = board.Index(1, 0);
            int top = board.Index(0, 1);
            int middle = board.Index(1, 1);
            int diagonal = board.Index(2, 2);
            board.Set(left, 4);
            board.Set(top, 4);
            board.Set(middle, 3);
            board.Set(diagonal, 1);
            MoveResult result = Resolve(board, board.Index(0, 0), 1);
            Assert.AreEqual(3, result.Explosions);
            Assert.AreEqual(2, result.WaveCount);
            Assert.IsTrue(board.IsEmpty(middle));
            Assert.AreEqual(1, board.Get(diagonal));
        }

        [Test]
        public void ScoreMatchesTable()
        {
            BalanceConfig config = new BalanceConfig();
            Board board = new Board(6);
            board.Set(board.Index(0, 1), 1);
            MoveResult simple = new MoveResult();
            simple.Reset();
            new CascadeResolver().Resolve(board, board.Index(0, 0), 1, config, simple);
            Assert.AreEqual(10 + 10, simple.RawScore);

            Board chain = new Board(6);
            chain.Set(chain.Index(2, 1), 4);
            chain.Set(chain.Index(2, 2), 4);
            MoveResult result = new MoveResult();
            result.Reset();
            new CascadeResolver().Resolve(chain, chain.Index(2, 0), 1, config, result);
            // Постановка 10; два перехода в взрыв 2*80; два взрыва 2*120; поставленная сфера получила +1 (10);
            // бонус каскада 40*(2-1)^2 = 40
            Assert.AreEqual(2, result.Explosions);
            Assert.AreEqual(10 + 160 + 240 + 10 + 40, result.RawScore);
        }

        [Test]
        public void ThreeHitsRaiseMultiplierAndMissResetsIt()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 40);
            GameSession session = Session(config, map, 0);
            Assert.AreEqual(1, session.Multiplier);
            session.TryPlace(0, map.Times[0]);
            session.TryPlace(1, map.Times[1]);
            Assert.AreEqual(1, session.Multiplier);
            MoveResult third = session.TryPlace(2, map.Times[2]);
            Assert.AreEqual(2, session.Multiplier);
            // Новый множитель действует уже на тот ход, где он получен
            Assert.AreEqual(2, third.Multiplier);
            MoveResult miss = session.TryPlace(3, map.Times[3] + 0.25);
            Assert.AreEqual(HitGrade.Miss, miss.Grade);
            Assert.IsTrue(miss.Accepted);
            Assert.AreEqual(1, session.Multiplier);
            Assert.AreEqual(0, session.Streak);
        }

        [Test]
        public void MultiplierStopsAtTen()
        {
            BalanceConfig config = Config();
            config.boardSize = 10;
            BeatMapData map = Beats(0.5, 60);
            GameSession session = Session(config, map, 0);
            for (int i = 0; i < 40; i++)
            {
                session.TryPlace(i * 2, map.Times[i]);
            }
            Assert.AreEqual(10, session.Multiplier);
        }

        [Test]
        public void SkippedPositionsDoNotResetMultiplier()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 40);
            GameSession session = Session(config, map, 0);
            session.TryPlace(0, map.Times[0]);
            session.TryPlace(1, map.Times[4]);
            session.TryPlace(2, map.Times[9]);
            Assert.AreEqual(2, session.Multiplier);
        }

        [Test]
        public void OccupiedCellIsNotAMoveAndKeepsStreak()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 40);
            GameSession session = Session(config, map, 0);
            session.TryPlace(0, map.Times[0]);
            session.TryPlace(1, map.Times[1]);
            int sphere = session.CurrentSphere;
            session.Tick(3.0);
            MoveResult rejected = session.TryPlace(1, map.Times[2] + 0.25);
            Assert.IsFalse(rejected.Accepted);
            Assert.AreEqual(2, session.Streak);
            Assert.AreEqual(sphere, session.CurrentSphere);
            // Таймер бездействия не сбрасывается касанием занятой клетки
            Assert.AreEqual(3.0, session.IdleSeconds, 0.0001);
        }

        [Test]
        public void SecondHitInSamePositionIsNeutral()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 40);
            GameSession session = Session(config, map, 0);
            session.TryPlace(0, map.Times[0]);
            MoveResult again = session.TryPlace(1, map.Times[0] + 0.01);
            Assert.IsTrue(again.Accepted);
            Assert.IsTrue(again.DuplicatePosition);
            Assert.AreEqual(1, session.Streak);
        }

        [Test]
        public void WindowsShrinkOnFastSubdivisions()
        {
            BalanceConfig config = new BalanceConfig();
            RhythmJudge slow = new RhythmJudge(Beats(1.0, 8), config);
            RhythmJudge fast = new RhythmJudge(Beats(0.1, 8), config);
            Assert.AreEqual(250.0, slow.GoodWindowMs(3), 0.001);
            Assert.AreEqual(100.0, slow.PerfectWindowMs(3), 0.001);
            Assert.Less(fast.GoodWindowMs(3), 50.0);
            Assert.LessOrEqual(fast.PerfectWindowMs(3), fast.GoodWindowMs(3));
            // Окна соседних позиций не перекрываются
            Assert.Less(fast.GoodWindowMs(3) * 2.0, 100.0);
        }

        [Test]
        public void PerfectGoodMissGrades()
        {
            BalanceConfig config = new BalanceConfig();
            BeatMapData map = Beats(0.5, 10);
            RhythmJudge judge = new RhythmJudge(map, config);
            Assert.AreEqual(HitGrade.Perfect, judge.Judge(map.Times[0] + 0.020).Grade);
            Assert.AreEqual(HitGrade.Good, judge.Judge(map.Times[1] - 0.120).Grade);
            Assert.AreEqual(HitGrade.Miss, judge.Judge(map.Times[2] + 0.220).Grade);
        }

        [Test]
        public void VioletAppearsOnlyFromMultiplierFive()
        {
            BalanceConfig config = new BalanceConfig();
            SphereGenerator generator = new SphereGenerator(config, new DeterministicRandom(7));
            for (int m = 1; m <= 4; m++)
            {
                for (int i = 0; i < 2000; i++)
                {
                    Assert.AreNotEqual(4, generator.Next(m));
                }
            }
            int previous = 0;
            for (int m = 5; m <= 10; m += 5)
            {
                int violet = 0;
                for (int i = 0; i < 20000; i++)
                {
                    if (generator.Next(m) == 4)
                    {
                        violet++;
                    }
                }
                Assert.Greater(violet, previous);
                previous = violet;
            }
            // x5 около 5 %, x10 около 30 %
            Assert.Greater(previous, 20000 * 0.26);
            Assert.Less(previous, 20000 * 0.34);
        }

        [Test]
        public void SameSeedGivesSameGame()
        {
            BalanceConfig config = new BalanceConfig();
            BeatMapData map = Beats(0.5, 40);
            GameSession first = Session(config, map, 0);
            GameSession second = Session(config, map, 0);
            CollectionAssert.AreEqual(first.Board.ToArray(), second.Board.ToArray());
            Assert.AreEqual(first.CurrentSphere, second.CurrentSphere);
            Assert.AreEqual(config.startSphereCount, first.Board.OccupiedCount);
        }

        [Test]
        public void BoardFullIsCheckedAfterCascade()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 40);
            GameSession session = Session(config, map, 0);
            int[] levels = new int[36];
            for (int i = 0; i < 36; i++)
            {
                levels[i] = 1;
            }
            levels[0] = 0;
            levels[1] = 4;
            session.LoadPreset(levels, 1);
            MoveResult cleared = session.TryPlace(0, map.Times[0]);
            // Последняя клетка занята, но каскад освободил место: поражения нет
            Assert.AreEqual(1, cleared.Explosions);
            Assert.AreEqual(SessionState.Playing, session.State);

            levels[1] = 1;
            session.LoadPreset(levels, 1);
            session.TryPlace(0, map.Times[1]);
            Assert.AreEqual(SessionState.Lost, session.State);
            Assert.AreEqual(DefeatReason.BoardFull, session.Defeat);
        }

        [Test]
        public void IdleWarningAtTenDefeatAtFifteen()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 80);
            GameSession session = Session(config, map, 0);
            session.Tick(9.9);
            Assert.IsFalse(session.IdleWarning);
            session.Tick(0.2);
            Assert.IsTrue(session.IdleWarning);
            session.TryPlace(0, map.Times[0]);
            Assert.IsFalse(session.IdleWarning);
            session.Tick(14.9);
            Assert.AreEqual(SessionState.Playing, session.State);
            session.Tick(0.2);
            Assert.AreEqual(SessionState.Lost, session.State);
            Assert.AreEqual(DefeatReason.Idle, session.Defeat);
        }

        [Test]
        public void SongEndDecidesByMinimumScore()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 40);
            GameSession poor = Session(config, map, 1000000);
            poor.TryPlace(0, map.Times[0]);
            poor.FinishSong();
            Assert.AreEqual(SessionState.Lost, poor.State);
            Assert.AreEqual(DefeatReason.NotEnoughScore, poor.Defeat);
            Assert.AreEqual(0, poor.Stars);

            GameSession rich = Session(config, map, 10);
            rich.TryPlace(0, map.Times[0]);
            rich.FinishSong();
            Assert.AreEqual(SessionState.Won, rich.State);
            Assert.GreaterOrEqual(rich.Stars, 1);
        }

        [Test]
        public void MoveAfterSongEndIsRejected()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 10);
            GameSession session = Session(config, map, 0);
            MoveResult late = session.TryPlace(0, map.Duration + 0.01);
            Assert.IsFalse(late.Accepted);
        }

        [Test]
        public void StarsByThresholds()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 10);
            GameSession session = Session(config, map, 1000);
            Assert.AreEqual(0, session.StarsForScore(999));
            Assert.AreEqual(1, session.StarsForScore(1000));
            Assert.AreEqual(1, session.StarsForScore(1299));
            Assert.AreEqual(2, session.StarsForScore(1300));
            Assert.AreEqual(3, session.StarsForScore(1700));
        }

        [Test]
        public void PerfectGivesBonusAndRounds()
        {
            BalanceConfig config = Config();
            BeatMapData map = Beats(0.5, 10);
            GameSession session = Session(config, map, 0);
            MoveResult result = session.TryPlace(0, map.Times[0]);
            Assert.AreEqual(HitGrade.Perfect, result.Grade);
            // 10 очков * x1 * 1,15 = 11,5 -> 12
            Assert.AreEqual(12, result.FinalScore);
        }

        [Test]
        public void AutoSphereAppearsAfterEachMoveAndIsPassive()
        {
            BalanceConfig config = Config();
            config.autoSpheresPerMove = 1;
            BeatMapData map = Beats(0.5, 40);
            GameSession session = Session(config, map, 0);
            MoveResult result = session.TryPlace(0, map.Times[0]);
            Assert.AreEqual(2, session.Board.OccupiedCount);
            int autoCell = -1;
            int autoLevel = 0;
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (result.Events[i].Type == MoveEventType.AutoSpawned)
                {
                    autoCell = result.Events[i].Cell;
                    autoLevel = result.Events[i].Level;
                }
            }
            Assert.AreNotEqual(-1, autoCell);
            Assert.AreNotEqual(0, autoCell);
            Assert.GreaterOrEqual(autoLevel, 1);
            Assert.LessOrEqual(autoLevel, 3);
            // Автосфера пассивная: поставленная сфера не меняется, даже если они соседи
            Assert.AreEqual(result.PlacedLevel, session.Board.Get(0));
            Assert.AreEqual(autoLevel, session.Board.Get(autoCell));
        }

        [Test]
        public void AutoSphereCanFillTheBoard()
        {
            BalanceConfig config = Config();
            config.autoSpheresPerMove = 1;
            BeatMapData map = Beats(0.5, 40);
            GameSession session = Session(config, map, 0);
            int[] levels = new int[36];
            for (int i = 0; i < 36; i++)
            {
                levels[i] = 1;
            }
            levels[0] = 0;
            levels[35] = 0;
            session.LoadPreset(levels, 1);
            session.TryPlace(0, map.Times[0]);
            // Игрок занял предпоследнюю клетку, автосфера заняла последнюю: поле заполнено
            Assert.AreEqual(SessionState.Lost, session.State);
            Assert.AreEqual(DefeatReason.BoardFull, session.Defeat);
        }

        [Test]
        public void EnergyRestoresOverTime()
        {
            long start = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc).Ticks;
            long last = start;
            int energy = EnergyMath.Restore(2, ref last, start + TimeSpan.FromMinutes(29).Ticks, 5, 30);
            Assert.AreEqual(2, energy);
            energy = EnergyMath.Restore(2, ref last, start + TimeSpan.FromMinutes(65).Ticks, 5, 30);
            Assert.AreEqual(4, energy);
            Assert.AreEqual(start + TimeSpan.FromMinutes(60).Ticks, last);
            energy = EnergyMath.Restore(4, ref last, start + TimeSpan.FromHours(10).Ticks, 5, 30);
            Assert.AreEqual(5, energy);
        }
    }
}
