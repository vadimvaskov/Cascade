using Cascade.Data;
using Cascade.UI;

namespace Cascade
{
    /// <summary>
    /// Служебные команды для проверки игры из редактора (мост DevBridge): переход между экранами,
    /// автоигра, принудительное завершение уровня. На правила игры не влияют.
    /// </summary>
    public static class DevHooks
    {
        public static string Goto(string[] args)
        {
            CascadeApp app = CascadeApp.Instance;
            if (app == null || args.Length == 0)
            {
                return "no app";
            }
            switch (args[0])
            {
                case "menu":
                    app.ShowMenu();
                    return "menu";
                case "map":
                    app.ShowWorldMap();
                    return "map";
                case "settings":
                    app.ShowSettings();
                    return "settings";
                case "tutorial":
                    app.StartTutorial();
                    return "tutorial";
                case "game":
                    LevelDef level = args.Length > 1 ? LevelCatalog.Find(args[1]) : LevelCatalog.Levels[0];
                    return "game " + app.StartLevel(level);
                case "world":
                    WorldMapScreen map = app.Current as WorldMapScreen;
                    if (map == null)
                    {
                        app.ShowWorldMap();
                        map = app.Current as WorldMapScreen;
                    }
                    int index = args.Length > 1 ? int.Parse(args[1]) : 1;
                    map.OpenWorld(LevelCatalog.World(index));
                    return "world " + index;
                default:
                    return "unknown screen";
            }
        }

        public static string SkipTutorial(string[] args)
        {
            SaveSystem.Data.tutorialDone = true;
            SaveSystem.Save();
            return "tutorial marked done";
        }

        public static string ResetSave(string[] args)
        {
            SaveSystem.Reset();
            return "save reset";
        }

        public static string Lang(string[] args)
        {
            string code = args.Length > 0 ? args[0] : "en";
            SaveSystem.Data.language = code;
            Loc.SetLanguage(code);
            SaveSystem.Save();
            return "language " + code;
        }

        public static string Auto(string[] args)
        {
            GameScreen game = Game();
            if (game == null)
            {
                return "not in game";
            }
            bool on = args.Length == 0 || args[0] != "off";
            float accuracy = args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 0.9f;
            game.SetAutoPlay(on, accuracy);
            return "autoplay " + on;
        }

        public static string Tap(string[] args)
        {
            GameScreen game = Game();
            if (game == null || args.Length == 0)
            {
                return "not in game";
            }
            game.DevTap(int.Parse(args[0]));
            return State(args);
        }

        public static string End(string[] args)
        {
            GameScreen game = Game();
            if (game == null)
            {
                return "not in game";
            }
            game.DevFinish(args.Length == 0 || args[0] == "win");
            return State(args);
        }

        public static string Pause(string[] args)
        {
            GameScreen game = Game();
            if (game == null)
            {
                return "not in game";
            }
            game.Pause();
            return State(args);
        }

        public static string Resume(string[] args)
        {
            GameScreen game = Game();
            if (game == null)
            {
                return "not in game";
            }
            game.Resume();
            return State(args);
        }

        public static string State(string[] args)
        {
            CascadeApp app = CascadeApp.Instance;
            if (app == null)
            {
                return "no app";
            }
            GameScreen game = app.Current as GameScreen;
            if (game == null || game.Session == null)
            {
                return "screen=" + (app.Current == null ? "none" : app.Current.GetType().Name);
            }
            return "screen=GameScreen phase=" + game.PhaseName + " state=" + game.Session.State + " score=" + game.Session.Score
                + " mult=" + game.Session.Multiplier + " placements=" + game.Session.Placements + " songTime=" + app.Conductor.SongTime.ToString("0.00")
                + " occupied=" + game.Session.Board.OccupiedCount;
        }

        private static GameScreen Game()
        {
            CascadeApp app = CascadeApp.Instance;
            return app == null ? null : app.Current as GameScreen;
        }
    }
}
