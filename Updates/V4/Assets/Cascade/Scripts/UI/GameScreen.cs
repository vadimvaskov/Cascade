using System;
using System.Collections;
using Cascade.Audio;
using Cascade.Core;
using Cascade.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>Игровой экран: сцена с героем, очки, множитель, поле 6×6 и ритм-диаграмма.</summary>
    public sealed class GameScreen : ScreenBase
    {
        private enum Phase
        {
            Idle,
            Countdown,
            Playing,
            Paused,
            Resuming,
            Finished,
            Tutorial
        }

        private const float FeetY = 516f;
        private const float DeckFraction = 0.3117f;
        private const float LeadIn = 3.0f;
        private const float ResumeStep = 0.75f;

        private LevelDef level;
        private BalanceConfig config;
        private BeatMapData map;
        private GameSession session;
        private Conductor conductor;
        private Phase phase = Phase.Idle;
        private double duration;
        private bool tutorialMode;

        private BoardView board;
        private RhythmBar rhythm;
        private HeroStrip strip;
        private Text scoreText;
        private Text multiplierText;
        private RectTransform multiplierPlate;
        private RectTransform trackFill;
        private RectTransform starFill;
        private Image[] starIcons;
        private Text goalText;
        private Text gradeText;
        private Text timingText;
        private Text countdownText;
        private RectTransform pauseOverlay;
        private int shownMultiplier = 1;
        private int pulseIndex;
        private float resumeTimer;
        private float gradeTimer;
        private double finishedClock;
        private bool warningShown;
        private Coroutine clock;

        // Обучение
        private int tutorialStep;
        private RectTransform tutorialCard;
        private Text tutorialText;
        private Text tutorialTitle;
        private Image tutorialTarget;
        private int tutorialTargetCell = -1;
        private bool tutorialMusic;

        // Автоигра для проверки
        private bool autoPlay;
        private int autoIndex;
        private float autoAccuracy = 0.9f;

        public GameSession Session { get { return session; } }
        public string PhaseName { get { return phase.ToString(); } }

        protected override void Build()
        {
        }

        public void Begin(LevelDef def, bool tutorial)
        {
            StopClock();
            UnbindSession();
            level = def;
            tutorialMode = tutorial;
            config = App.Config;
            conductor = App.Conductor;
            map = BeatMapLoader.Load(def.Id);
            AudioClip clip = Resources.Load<AudioClip>("Music/" + def.Id);
            if (clip == null)
            {
                Debug.LogError("[Cascade] Music not found: " + def.Id);
            }
            conductor.Load(clip);
            conductor.SetVolume(SaveSystem.Data.musicVolume);
            duration = conductor.Duration > 0 ? conductor.Duration : map.Duration;
            if (map.Duration > 0 && map.Duration < duration)
            {
                duration = map.Duration;
            }

            LevelRules rules = new LevelRules();
            rules.MinScore = def.MinScore;
            rules.Duration = duration;
            rules.Seed = Environment.TickCount;
            rules.NoDefeat = tutorial;
            session = new GameSession(config, map, rules);
            session.Start();

            BuildWorld();
            BuildHud();
            BuildBoard();
            UpdateHud(true);

            if (tutorial)
            {
                StartTutorial();
            }
            else
            {
                SaveSystem.Data.lastLevelId = def.Id;
                phase = Phase.Countdown;
                conductor.Play(LeadIn);
            }
            session.Changed += OnSessionChanged;
            App.UsePlayPower();
            StartClock();
            Debug.Log("[Cascade] Level " + def.Id + " started, duration " + duration.ToString("0.0") + " s, goal " + def.MinScore
                + ", seed " + rules.Seed + (tutorial ? " (tutorial)" : ""));
        }

        // ---------- Сборка экрана ----------

        private void BuildWorld()
        {
            Image fill = UiKit.Img("BackFill", Root, null, UiKit.Night);
            UiKit.Stretch(fill.rectTransform);
            fill.transform.SetAsFirstSibling();

            RectTransform world = UiKit.Stretch(UiKit.Rect("World", Column));
            world.SetAsFirstSibling();
            Sprite bg = UiKit.GetSprite("game/bg_game");
            float feet = FeetY + App.TopInset;
            if (bg != null)
            {
                float baseHeight = UiKit.DesignWidth * bg.rect.height / bg.rect.width;
                float deck = baseHeight * DeckFraction;
                float scale = Mathf.Max(1f, feet / deck);
                float width = UiKit.DesignWidth * scale;
                float height = baseHeight * scale;
                float shiftUp = deck * scale - feet;
                Image image = UiKit.Img("Background", world, bg, Color.white);
                UiKit.Top(image.rectTransform, (UiKit.DesignWidth - width) * 0.5f, -shiftUp, width, height);
                // Плавный переход фона в тёмный низ на высоких экранах
                Image fade = UiKit.Img("BottomFade", world, "ui/grad_v");
                fade.color = UiKit.Night;
                UiKit.Top(fade.rectTransform, -200f, height - shiftUp - 220f, UiKit.DesignWidth + 400f, 222f);
            }
            RectTransform stripRect = UiKit.Rect("HeroStrip", world);
            strip = stripRect.gameObject.AddComponent<HeroStrip>();
            strip.Build(world, feet, level.Bpm);
            strip.SetSphere(session.CurrentSphere, true);
        }

        private void BuildHud()
        {
            Image avatar = UiKit.Img("Avatar", Safe, "ui/avatar");
            UiKit.Top(avatar.rectTransform, 36f, 24f, 96f, 96f);
            Image avatarRing = UiKit.Img("AvatarRing", Safe, "ui/ring");
            avatarRing.color = UiKit.Gold;
            UiKit.Top(avatarRing.rectTransform, 26f, 14f, 116f, 116f);
            Text name = UiKit.Label("Name", Safe, SaveSystem.Data.playerName, 40, UiKit.Cream, TextAnchor.MiddleLeft, false);
            UiKit.Top(name.rectTransform, 152f, 28f, 260f, 48f);
            Text title = UiKit.Label("Track", Safe, level.Title, 28, UiKit.Gold, TextAnchor.MiddleLeft, false);
            UiKit.Top(title.rectTransform, 152f, 76f, 300f, 36f);

            Image logo = UiKit.Img("Logo", Safe, "menu/logo");
            logo.preserveAspect = true;
            UiKit.Top(logo.rectTransform, 400f, 12f, 300f, 122f);

            Image pause = UiKit.Img("Pause", Safe, "ui/btn_pause");
            UiKit.Top(pause.rectTransform, 950f, 26f, 94f, 92f);
            UiKit.MakeButton(pause, Pause);

            // Прогресс трека
            Image trackBack = UiKit.Img("TrackBack", Safe, null, new Color(1f, 1f, 1f, 0.14f));
            UiKit.Top(trackBack.rectTransform, 40f, 536f, 1000f, 6f);
            Image trackBar = UiKit.Img("TrackFill", trackBack.transform, null, UiKit.Gold);
            trackFill = trackBar.rectTransform;
            trackFill.anchorMin = new Vector2(0f, 0f);
            trackFill.anchorMax = new Vector2(0f, 1f);
            trackFill.offsetMin = Vector2.zero;
            trackFill.offsetMax = Vector2.zero;

            // Очки
            Text scoreLabel = UiKit.Label("ScoreLabel", Safe, Loc.T("score"), 28, UiKit.Gold, TextAnchor.MiddleLeft, false);
            UiKit.Top(scoreLabel.rectTransform, 52f, 552f, 300f, 34f);
            Image scorePlate = UiKit.Img("ScorePlate", Safe, "game/banner_gold");
            UiKit.Top(scorePlate.rectTransform, 26f, 586f, 310f, 118f);
            scoreText = UiKit.FitLabel("Score", scorePlate.transform, "0", 58, UiKit.Ink, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(scoreText.rectTransform);
            scoreText.rectTransform.offsetMin = new Vector2(44f, 24f);
            scoreText.rectTransform.offsetMax = new Vector2(-44f, -24f);

            // Множитель
            Image multPlate = UiKit.Img("MultiplierPlate", Safe, "game/banner_purple");
            UiKit.Top(multPlate.rectTransform, 786f, 574f, 272f, 112f);
            multiplierPlate = multPlate.rectTransform;
            multiplierText = UiKit.Label("Multiplier", multPlate.transform, "x1", 76, UiKit.Gold, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(multiplierText.rectTransform);

            // Шкала звёзд: заполняется по мере набора очков до порога трёх звёзд
            Image starBack = UiKit.SlicedImg("StarBar", Safe, "ui/pill", 30f, new Color(1f, 1f, 1f, 0.16f));
            UiKit.Top(starBack.rectTransform, 362f, 626f, 396f, 12f);
            Image starBar = UiKit.SlicedImg("StarFill", starBack.transform, "ui/pill", 30f, UiKit.Gold);
            starFill = starBar.rectTransform;
            starFill.anchorMin = new Vector2(0f, 0f);
            starFill.anchorMax = new Vector2(0f, 1f);
            starFill.offsetMin = Vector2.zero;
            starFill.offsetMax = Vector2.zero;
            starIcons = new Image[3];
            float star3 = Mathf.Max(1f, session.Star3Score);
            float[] fractions = { session.MinScore / star3, session.Star2Score / star3, 1f };
            for (int i = 0; i < 3; i++)
            {
                Image star = UiKit.Img("Star" + i, starBack.transform, "ui/star");
                star.color = UiKit.Muted;
                RectTransform rt = star.rectTransform;
                rt.anchorMin = new Vector2(fractions[i], 0.5f);
                rt.anchorMax = new Vector2(fractions[i], 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(46f, 46f);
                rt.anchoredPosition = new Vector2(i == 2 ? -8f : 0f, 0f);
                starIcons[i] = star;
            }
            goalText = UiKit.Label("Goal", Safe, Loc.T("goal") + " " + UiKit.FormatNumber(session.MinScore), 27, UiKit.Cream, TextAnchor.MiddleCenter, false);
            UiKit.Top(goalText.rectTransform, 352f, 654f, 416f, 36f);
        }

        private void BuildBoard()
        {
            RectTransform boardRect = UiKit.Rect("Board", Safe);
            board = boardRect.gameObject.AddComponent<BoardView>();
            board.Build(Safe, 60f, 716f, 960f, session.Board);
            board.CellPressed = OnCellPressed;
            board.SetPulseVisible(SaveSystem.Data.framePulse);
            float boardBottom = 716f + board.Rect.sizeDelta.y;

            RectTransform rhythmRect = UiKit.Rect("RhythmBar", Safe);
            rhythm = rhythmRect.gameObject.AddComponent<RhythmBar>();
            rhythm.Build(Safe, 60f, boardBottom + 22f, 960f, 104f, map, session.Judge);
            rhythmRect.gameObject.SetActive(SaveSystem.Data.rhythmDiagram && !tutorialMode);

            gradeText = UiKit.Label("Grade", Safe, "", 60, UiKit.Gold, TextAnchor.MiddleCenter, true);
            UiKit.Top(gradeText.rectTransform, 240f, 690f, 600f, 76f);
            gradeText.enabled = false;
            timingText = UiKit.Label("Timing", Safe, "", 30, UiKit.Cream, TextAnchor.MiddleCenter, false);
            UiKit.Top(timingText.rectTransform, 240f, 752f, 600f, 36f);
            timingText.enabled = false;

            countdownText = UiKit.Label("Countdown", Safe, "", 300, UiKit.Cream, TextAnchor.MiddleCenter, true);
            UiKit.Top(countdownText.rectTransform, 240f, 716f + board.Rect.sizeDelta.y * 0.5f - 180f, 600f, 360f);
            countdownText.enabled = false;
        }

        // ---------- Игровой цикл ----------

        private void StartClock()
        {
            StopClock();
            clock = StartCoroutine(RunClock());
        }

        private void StopClock()
        {
            if (clock != null)
            {
                StopCoroutine(clock);
                clock = null;
            }
        }

        /// <summary>Тикает, только пока уровень не на паузе. На паузе корутина остановлена.</summary>
        private IEnumerator RunClock()
        {
            while (phase != Phase.Idle && phase != Phase.Paused)
            {
                Advance();
                if (phase == Phase.Paused || phase == Phase.Idle)
                {
                    break;
                }
                yield return null;
            }
            clock = null;
        }

        private void Advance()
        {
            if (phase == Phase.Idle || session == null)
            {
                return;
            }
            if (phase == Phase.Resuming)
            {
                resumeTimer -= Time.unscaledDeltaTime;
                if (resumeTimer <= 0f)
                {
                    countdownText.enabled = false;
                    Time.timeScale = 1f;
                    conductor.Resume(0.05);
                    phase = conductor.SongTime < 0 ? Phase.Countdown : Phase.Playing;
                }
                else
                {
                    ShowCountdown(Mathf.CeilToInt(resumeTimer / ResumeStep));
                }
                return;
            }
            if (phase == Phase.Paused)
            {
                return;
            }

            float dt = Time.deltaTime;
            double t = conductor.SongTime;

            if (phase == Phase.Countdown)
            {
                strip.Render(t, 1, dt);
                RenderRhythm(t);
                if (t >= 0)
                {
                    phase = Phase.Playing;
                    countdownText.text = Loc.T("go");
                    countdownText.fontSize = 150;
                    Anim.Delay(countdownText, 0.5f, delegate { countdownText.enabled = false; });
                }
                else
                {
                    countdownText.fontSize = 300;
                    ShowCountdown(Mathf.CeilToInt((float)-t));
                }
                return;
            }

            if (phase == Phase.Finished)
            {
                finishedClock += dt;
                strip.Render(finishedClock, 1, dt);
                return;
            }

            if (phase == Phase.Tutorial)
            {
                UpdateTutorial(t, dt);
                return;
            }

            // Phase.Playing
            session.Tick(dt);
            strip.Render(t, session.Multiplier, dt);
            RenderRhythm(t);
            PulseOnBeats(t);
            SetFill(trackFill, duration > 0 ? (float)(t / duration) : 0f);
            UpdateGrade(dt);
            UpdateWarning();
            if (autoPlay)
            {
                AutoPlayStep(t);
            }
            if (session.State == SessionState.Lost)
            {
                Finish();
            }
            else if (t >= duration)
            {
                session.FinishSong();
                Finish();
            }
        }

        private void PulseOnBeats(double t)
        {
            double[] times = map.Times;
            while (pulseIndex < times.Length && times[pulseIndex] <= t)
            {
                int beatLevel = map.Levels[pulseIndex];
                if (beatLevel <= 1 && t - times[pulseIndex] < 0.2)
                {
                    board.Pulse(beatLevel == 0 ? 1f : 0.55f);
                }
                pulseIndex++;
            }
        }

        private void RenderRhythm(double songTime)
        {
            if (rhythm != null && rhythm.gameObject.activeSelf)
            {
                rhythm.Render(songTime);
            }
        }

        private static void SetFill(RectTransform fill, float fraction)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        private void ShowCountdown(int number)
        {
            string textValue = number.ToString();
            if (!countdownText.enabled || countdownText.text != textValue)
            {
                countdownText.enabled = true;
                countdownText.text = textValue;
                RectTransform rt = countdownText.rectTransform;
                Anim.Run(rt, 0f, 0.25f, delegate(float k)
                {
                    float s = Mathf.Lerp(1.5f, 1f, Anim.EaseOut(k));
                    rt.localScale = new Vector3(s, s, 1f);
                }, null, true);
            }
        }

        private void OnCellPressed(int cell)
        {
            if (phase != Phase.Playing && phase != Phase.Tutorial)
            {
                return;
            }
            if (phase == Phase.Tutorial && tutorialTargetCell >= 0 && cell != tutorialTargetCell)
            {
                return;
            }
            double t = conductor.SongTime - PressAge();
            MoveResult result = session.TryPlace(cell, t);
            if (!result.Accepted)
            {
                if (cell >= 0 && cell < session.Board.CellCount && !session.Board.IsEmpty(cell))
                {
                    board.Shake(cell);
                    Haptics.Error();
                }
                return;
            }
            Haptics.Tap();
            board.Play(result);
            strip.SetSphere(result.NextSphere, false);
            bool rhythmActive = phase == Phase.Playing || tutorialMusic;
            if (rhythmActive && !result.DuplicatePosition)
            {
                ShowGrade(result.Grade);
                ShowTiming(result.OffsetMs);
                rhythm.Flash(result.Grade);
                strip.Kick(result.Grade == HitGrade.Miss ? -1f : 1f);
            }
            if (phase == Phase.Tutorial)
            {
                OnTutorialMove();
            }
        }

        /// <summary>
        /// Сколько секунд назад произошло само касание. Событие ввода обрабатывается в начале кадра,
        /// а нажали чуть раньше; эта поправка убирает ошибку до одного кадра.
        /// </summary>
        private static double PressAge()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null)
            {
                double age = Time.realtimeSinceStartupAsDouble - pointer.lastUpdateTime;
                if (age > 0 && age < 0.1)
                {
                    return age;
                }
            }
#endif
            return 0;
        }

        /// <summary>Подсказка «рано / поздно» с отклонением от ближайшей доли в миллисекундах.</summary>
        private void ShowTiming(double offsetMs)
        {
            if (!SaveSystem.Data.showTiming)
            {
                return;
            }
            int ms = Mathf.RoundToInt((float)Math.Abs(offsetMs));
            timingText.text = (offsetMs < 0 ? Loc.T("early") : Loc.T("late")) + " " + ms + " " + Loc.T("ms");
            timingText.enabled = true;
        }

        private void ShowGrade(HitGrade grade)
        {
            if (grade == HitGrade.Perfect)
            {
                gradeText.text = Loc.T("perfect");
                gradeText.color = UiKit.Gold;
            }
            else if (grade == HitGrade.Good)
            {
                gradeText.text = Loc.T("good");
                gradeText.color = UiKit.Cream;
            }
            else
            {
                gradeText.text = Loc.T("miss");
                gradeText.color = UiKit.Red;
            }
            gradeText.enabled = true;
            gradeTimer = 0.6f;
            warningShown = false;
            RectTransform rt = gradeText.rectTransform;
            Anim.Run(rt, 0.12f, delegate(float k)
            {
                float s = Mathf.Lerp(1.35f, 1f, Anim.EaseOut(k));
                rt.localScale = new Vector3(s, s, 1f);
            });
        }

        private void UpdateGrade(float dt)
        {
            if (gradeTimer > 0f)
            {
                gradeTimer -= dt;
                if (gradeTimer <= 0f)
                {
                    timingText.enabled = false;
                    if (!warningShown)
                    {
                        gradeText.enabled = false;
                    }
                }
            }
        }

        private void UpdateWarning()
        {
            if (session.IdleWarning)
            {
                int left = Mathf.CeilToInt(config.idleDefeatSeconds - (float)session.IdleSeconds);
                warningShown = true;
                gradeText.enabled = true;
                gradeText.color = UiKit.Red;
                gradeText.text = Loc.T("idle_warning") + " " + Mathf.Max(0, left);
                float s = 1f + 0.06f * Mathf.Sin(Time.time * 12f);
                gradeText.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            else if (warningShown)
            {
                warningShown = false;
                if (gradeTimer <= 0f)
                {
                    gradeText.enabled = false;
                }
            }
        }

        private void OnSessionChanged()
        {
            if (scoreText == null || session == null)
            {
                return;
            }
            UpdateHud(false);
        }

        private void UnbindSession()
        {
            if (session != null)
            {
                session.Changed -= OnSessionChanged;
            }
        }

        private void UpdateHud(bool instant)
        {
            scoreText.text = UiKit.FormatNumber(session.Score);
            int multiplier = session.Multiplier;
            multiplierText.text = "x" + multiplier;
            if (multiplier != shownMultiplier && !instant)
            {
                bool up = multiplier > shownMultiplier;
                RectTransform rt = multiplierPlate;
                Anim.Run(rt, 0.22f, delegate(float k)
                {
                    float s = Mathf.Lerp(up ? 1.3f : 0.8f, 1f, Anim.EaseOut(k));
                    rt.localScale = new Vector3(s, s, 1f);
                });
            }
            shownMultiplier = multiplier;
            float star3 = Mathf.Max(1f, session.Star3Score);
            SetFill(starFill, session.Score / star3);
            int stars = session.StarsForScore(session.Score);
            for (int i = 0; i < 3; i++)
            {
                starIcons[i].color = i < stars ? UiKit.Gold : UiKit.Muted;
            }
        }

        // ---------- Пауза ----------

        public void Pause()
        {
            if (phase != Phase.Playing && phase != Phase.Countdown)
            {
                return;
            }
            phase = Phase.Paused;
            StopClock();
            conductor.Pause();
            App.UseIdlePower();
            Time.timeScale = 0f;
            countdownText.enabled = false;
            RectTransform panel;
            pauseOverlay = Modal.Create(App.OverlayRoot, "Pause", 820f, 700f, 0.72f, out panel);
            Text title = UiKit.Label("Title", panel, Loc.T("pause"), 84, UiKit.Cream, TextAnchor.MiddleCenter, true);
            UiKit.Center(title.rectTransform, 0f, 250f, 700f, 100f);
            Modal.GoldButton(panel, 0f, 80f, 560f, Loc.T("continue"), Resume);
            Modal.PlainButton(panel, 0f, -110f, 620f, Loc.T("restart"), delegate { App.StartLevel(level); });
            Modal.PlainButton(panel, 0f, -230f, 620f, Loc.T("menu"), delegate { App.ShowMenu(); });
        }

        /// <summary>Продолжение после отсчёта 3–2–1; музыка и карта ритма стартуют синхронно.</summary>
        public void Resume()
        {
            if (phase != Phase.Paused)
            {
                return;
            }
            if (pauseOverlay != null)
            {
                Destroy(pauseOverlay.gameObject);
                pauseOverlay = null;
            }
            countdownText.fontSize = 300;
            resumeTimer = ResumeStep * 3f;
            phase = Phase.Resuming;
            App.UsePlayPower();
            StartClock();
        }

        public override void OnAppPaused()
        {
            Pause();
        }

        public override void OnClose()
        {
            StopClock();
            UnbindSession();
            Time.timeScale = 1f;
        }

        // ---------- Итог ----------

        private void Finish()
        {
            if (phase == Phase.Finished)
            {
                return;
            }
            phase = Phase.Finished;
            finishedClock = conductor.SongTime;
            bool won = session.State == SessionState.Won;
            conductor.Stop();
            gradeText.enabled = false;
            timingText.enabled = false;

            LevelProgress progress = SaveSystem.GetLevel(level.Id);
            progress.plays++;
            int stars = session.Stars;
            int coins = 0;
            bool record = false;
            int previousBest = progress.bestScore;
            if (won)
            {
                if (session.Score > progress.bestScore)
                {
                    progress.bestScore = session.Score;
                    record = true;
                }
                if (stars > progress.stars)
                {
                    progress.stars = stars;
                }
                coins = EnergyMath.CoinsForWin(config, stars, session.Score, session.MaxCascade);
                SaveSystem.Data.coins += coins;
                SaveSystem.Save();
            }
            else
            {
                // Энергия расходуется один раз, только за подтверждённое поражение
                SaveSystem.SpendEnergy(config);
                if (session.Defeat == DefeatReason.Idle)
                {
                    strip.FlyAway();
                }
            }
            Debug.Log("[Cascade] Level finished: " + (won ? "WIN" : "LOSE " + session.Defeat) + ", score " + session.Score + ", stars " + stars
                + ", accuracy " + (session.Accuracy * 100f).ToString("0") + "%, max cascade " + session.MaxCascade + ", placements " + session.Placements);
            int best = won ? progress.bestScore : previousBest;
            Anim.DelayUnscaled(this, won ? 0.5f : 1.0f, delegate { ShowResult(won, stars, coins, record, best); });
        }

        private void ShowResult(bool won, int stars, int coins, bool record, int best)
        {
            RectTransform panel;
            Modal.Create(App.OverlayRoot, "Result", 900f, won ? 1180f : 900f, 0.76f, out panel);
            float top = panel.sizeDelta.y * 0.5f;
            Text title = UiKit.FitLabel("Title", panel, Loc.T(won ? "win_title" : "lose_title"), 78, won ? UiKit.Gold : UiKit.Red, TextAnchor.MiddleCenter, true);
            UiKit.Center(title.rectTransform, 0f, top - 100f, 800f, 100f);
            float y = top - 220f;
            if (won)
            {
                for (int i = 0; i < 3; i++)
                {
                    Image star = UiKit.Img("Star", panel, "ui/star");
                    star.color = i < stars ? UiKit.Gold : new Color(1f, 1f, 1f, 0.16f);
                    float size = i == 1 ? 170f : 140f;
                    UiKit.Center(star.rectTransform, (i - 1) * 190f, y - 20f + (i == 1 ? 14f : 0f), size, size);
                    if (i < stars)
                    {
                        RectTransform rt = star.rectTransform;
                        rt.localScale = Vector3.zero;
                        Anim.Run(rt, 0.25f + i * 0.22f, 0.3f, delegate(float k)
                        {
                            float s = Anim.EaseOutBack(k);
                            rt.localScale = new Vector3(s, s, 1f);
                        }, null, true);
                    }
                }
                y -= 190f;
                if (record)
                {
                    Text recordText = UiKit.Label("Record", panel, Loc.T("new_record"), 40, UiKit.Gold, TextAnchor.MiddleCenter, true);
                    UiKit.Center(recordText.rectTransform, 0f, y + 30f, 700f, 50f);
                }
                y -= 50f;
                Modal.StatRow(panel, y, 700f, Loc.T("score"), UiKit.FormatNumber(session.Score));
                Modal.StatRow(panel, y - 70f, 700f, Loc.T("best"), UiKit.FormatNumber(best));
                Modal.StatRow(panel, y - 140f, 700f, Loc.T("accuracy"), Mathf.RoundToInt(session.Accuracy * 100f) + "%");
                Modal.StatRow(panel, y - 210f, 700f, Loc.T("max_cascade"), "x" + session.MaxCascade);
                Modal.StatRow(panel, y - 280f, 700f, Loc.T("coins"), "+" + coins);
                LevelDef next = LevelCatalog.NextAfter(level);
                Modal.GoldButton(panel, 0f, -top + 250f, 560f, Loc.T("next"), delegate
                {
                    if (next == null || !App.StartLevel(next))
                    {
                        App.ShowWorldMap();
                    }
                });
                Modal.PlainButton(panel, -200f, -top + 90f, 380f, Loc.T("retry"), delegate { App.StartLevel(level); });
                Modal.PlainButton(panel, 200f, -top + 90f, 380f, Loc.T("levels"), delegate { App.ShowWorldMap(); });
            }
            else
            {
                string reason = "reason_score";
                if (session.Defeat == DefeatReason.BoardFull)
                {
                    reason = "reason_full";
                }
                else if (session.Defeat == DefeatReason.Idle)
                {
                    reason = "reason_idle";
                }
                Text reasonText = UiKit.FitLabel("Reason", panel, Loc.T(reason), 50, UiKit.Cream, TextAnchor.MiddleCenter, true);
                UiKit.Center(reasonText.rectTransform, 0f, y + 10f, 760f, 70f);
                y -= 110f;
                Modal.StatRow(panel, y, 700f, Loc.T("score"), UiKit.FormatNumber(session.Score));
                Modal.StatRow(panel, y - 70f, 700f, Loc.T("goal"), UiKit.FormatNumber(session.MinScore));
                Modal.StatRow(panel, y - 140f, 700f, Loc.T("energy"), SaveSystem.Data.energy + " / " + config.maxEnergy);
                Modal.GoldButton(panel, 0f, -top + 250f, 560f, Loc.T("retry"), delegate
                {
                    if (!App.StartLevel(level))
                    {
                        App.ShowWorldMap();
                    }
                });
                Modal.PlainButton(panel, 0f, -top + 90f, 620f, Loc.T("levels"), delegate { App.ShowWorldMap(); });
            }
        }

        // ---------- Обучение ----------

        private void StartTutorial()
        {
            phase = Phase.Tutorial;
            Image card = UiKit.SlicedImg("TutorialCard", Safe, "ui/round16", 24f, UiKit.Panel);
            tutorialCard = card.rectTransform;
            Image frame = UiKit.SlicedImg("Frame", card.transform, "ui/frame16", 22f, new Color(0.96f, 0.74f, 0.33f, 0.55f));
            UiKit.Stretch(frame.rectTransform);
            tutorialTitle = UiKit.Label("Title", card.transform, Loc.T("tut_title"), 46, UiKit.Gold, TextAnchor.MiddleCenter, true);
            tutorialText = UiKit.FitLabel("Text", card.transform, "", 40, UiKit.Cream, TextAnchor.MiddleCenter, false);
            PlaceTutorialCard(false, false);

            tutorialTarget = UiKit.Img("Target", board.Rect, "ui/ring");
            tutorialTarget.color = UiKit.Gold;
            UiKit.Center(tutorialTarget.rectTransform, 0f, 0f, 150f, 150f);
            tutorialTarget.enabled = false;
            SetTutorialStep(0);
        }

        /// <summary>Первые шаги — карточка под полем (герой и сфера видны); шаги про ритм — карточка над полем.</summary>
        private void PlaceTutorialCard(bool top, bool withButton)
        {
            float height;
            if (top)
            {
                height = withButton ? 400f : 330f;
                UiKit.Top(tutorialCard, 60f, 150f, 960f, height);
            }
            else
            {
                height = 250f;
                UiKit.Top(tutorialCard, 60f, 716f + board.Rect.sizeDelta.y + 16f, 960f, height);
            }
            UiKit.Center(tutorialTitle.rectTransform, 0f, height * 0.5f - 48f, 860f, 56f);
            float textHeight = height - 110f - (withButton ? 130f : 0f);
            UiKit.Center(tutorialText.rectTransform, 0f, height * 0.5f - 86f - textHeight * 0.5f, 880f, textHeight);
        }

        private void SetTutorialStep(int step)
        {
            tutorialStep = step;
            int size = config.boardSize;
            int[] levels = new int[size * size];
            tutorialTargetCell = -1;
            if (step == 0)
            {
                tutorialText.text = Loc.T("tut_1");
                session.LoadPreset(levels, 1);
            }
            else if (step == 1)
            {
                tutorialText.text = Loc.T("tut_2");
                levels[2 * size + 1] = 1;
                levels[2 * size + 3] = 2;
                levels[1 * size + 2] = 3;
                session.LoadPreset(levels, 1);
                tutorialTargetCell = 2 * size + 2;
            }
            else if (step == 2)
            {
                tutorialText.text = Loc.T("tut_3");
                levels[2 * size + 2] = 4;
                levels[2 * size + 3] = 4;
                levels[2 * size + 4] = 3;
                levels[1 * size + 3] = 3;
                levels[3 * size + 3] = 4;
                levels[4 * size + 3] = 2;
                session.LoadPreset(levels, 2);
                tutorialTargetCell = 2 * size + 1;
            }
            else if (step == 3)
            {
                tutorialText.text = Loc.T("tut_4");
                PlaceTutorialCard(true, false);
                session.LoadPreset(levels, 1);
                tutorialMusic = true;
                rhythm.gameObject.SetActive(true);
                rhythm.ResetTo(0);
                session.Judge.Reset();
                conductor.Play(1.0);
            }
            else
            {
                tutorialText.text = Loc.T("tut_5");
                PlaceTutorialCard(true, true);
                Modal.GoldButton(tutorialCard, 0f, -118f, 400f, Loc.T("tut_play"), FinishTutorial);
            }
            board.SyncAll();
            strip.SetSphere(session.CurrentSphere, true);
            UpdateHud(true);
            tutorialTarget.enabled = tutorialTargetCell >= 0;
            if (tutorialTargetCell >= 0)
            {
                tutorialTarget.rectTransform.anchoredPosition = board.CellCenter(tutorialTargetCell);
            }
        }

        private void OnTutorialMove()
        {
            if (tutorialStep <= 2)
            {
                int nextStep = tutorialStep + 1;
                tutorialTarget.enabled = false;
                tutorialTargetCell = 999;
                Anim.Delay(this, tutorialStep == 2 ? 1.6f : 1.2f, delegate { SetTutorialStep(nextStep); });
            }
            else if (tutorialStep == 3 && session.Multiplier >= 2)
            {
                tutorialTargetCell = 999;
                Anim.Delay(this, 1.0f, delegate { SetTutorialStep(4); });
            }
        }

        private void UpdateTutorial(double t, float dt)
        {
            strip.Render(tutorialMusic ? t : Time.time, session.Multiplier, dt);
            if (tutorialTarget.enabled)
            {
                float s = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
                tutorialTarget.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (!tutorialMusic)
            {
                return;
            }
            RenderRhythm(t);
            PulseOnBeats(t);
            UpdateGrade(dt);
            if (t >= duration - 0.5)
            {
                // Трек в обучении закончился — запускаем заново
                session.Judge.Reset();
                rhythm.ResetTo(0);
                pulseIndex = 0;
                conductor.Stop();
                conductor.Play(1.0);
            }
        }

        private void FinishTutorial()
        {
            SaveSystem.Data.tutorialDone = true;
            SaveSystem.Save();
            conductor.Stop();
            if (!App.StartLevel(LevelCatalog.Levels[0]))
            {
                App.ShowMenu();
            }
        }

        // ---------- Проверка (автоигра и служебные команды) ----------

        public void SetAutoPlay(bool enabled, float accuracy)
        {
            autoPlay = enabled;
            autoAccuracy = accuracy;
            autoIndex = 0;
        }

        private void AutoPlayStep(double t)
        {
            double[] times = map.Times;
            while (autoIndex < times.Length && times[autoIndex] < t - 0.05)
            {
                autoIndex++;
            }
            if (autoIndex >= times.Length || times[autoIndex] > t)
            {
                return;
            }
            int beatLevel = map.Levels[autoIndex];
            autoIndex++;
            if (beatLevel > 1 || session.Board.IsFull)
            {
                return;
            }
            if (UnityEngine.Random.value > autoAccuracy)
            {
                return;
            }
            int pick = UnityEngine.Random.Range(0, session.Board.EmptyCount);
            for (int i = 0; i < session.Board.CellCount; i++)
            {
                if (!session.Board.IsEmpty(i))
                {
                    continue;
                }
                if (pick == 0)
                {
                    OnCellPressed(i);
                    return;
                }
                pick--;
            }
        }

        public void DevTap(int cell)
        {
            OnCellPressed(cell);
        }

        public void DevFinish(bool win)
        {
            if (phase != Phase.Playing)
            {
                return;
            }
            if (win)
            {
                session.FinishSong();
            }
            else
            {
                session.Tick(1000);
            }
            Finish();
        }
    }
}
