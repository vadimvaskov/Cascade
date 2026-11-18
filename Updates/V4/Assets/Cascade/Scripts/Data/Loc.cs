using System.Collections.Generic;

namespace Cascade.Data
{
    /// <summary>Тексты интерфейса на двух языках: английский и русский.</summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>();
        private static bool russian;

        public static bool IsRussian { get { return russian; } }
        public static string Language { get { return russian ? "ru" : "en"; } }

        static Loc()
        {
            Add("play", "PLAY", "ИГРАТЬ");
            Add("chapter", "CHAPTER", "ГЛАВА");
            Add("daily_quests", "DAILY\nQUESTS", "ЗАДАНИЯ\nДНЯ");
            Add("events", "EVENTS", "СОБЫТИЯ");
            Add("music_library", "MUSIC\nLIBRARY", "МОЯ\nМУЗЫКА");
            Add("home", "HOME", "ГЛАВНАЯ");
            Add("tracks", "TRACKS", "ТРЕКИ");
            Add("heroes", "HEROES", "ГЕРОИ");
            Add("shop", "SHOP", "МАГАЗИН");
            Add("coming_soon", "Coming soon", "Скоро");
            Add("world_map", "WORLD MAP", "КАРТА МИРА");
            Add("swipe", "SWIPE TO EXPLORE", "ЛИСТАЙ, ЧТОБЫ ИССЛЕДОВАТЬ");
            Add("track", "Track", "Трек");
            Add("best", "Best", "Рекорд");
            Add("goal", "Goal", "Цель");
            Add("score", "SCORE", "ОЧКИ");
            Add("settings", "SETTINGS", "НАСТРОЙКИ");
            Add("music", "MUSIC", "МУЗЫКА");
            Add("sfx", "SOUND EFFECTS", "ЗВУКИ");
            Add("vibration", "VIBRATION", "ВИБРАЦИЯ");
            Add("frame_pulse", "FRAME PULSE", "ПУЛЬС РАМКИ");
            Add("rhythm_diagram", "RHYTHM BAR", "РИТМ-ДИАГРАММА");
            Add("language", "LANGUAGE", "ЯЗЫК");
            Add("lang_name", "English", "Русский");
            Add("tutorial", "TUTORIAL", "ОБУЧЕНИЕ");
            Add("replay", "Replay", "Пройти");
            Add("graphics", "GRAPHICS", "ГРАФИКА");
            Add("high", "High", "Высокая");
            Add("notifications", "NOTIFICATIONS", "УВЕДОМЛЕНИЯ");
            Add("save_progress", "SAVE PROGRESS", "СОХРАНЕНИЕ");
            Add("local", "On device", "На устройстве");
            Add("support", "SUPPORT", "ПОДДЕРЖКА");
            Add("about", "ABOUT", "ОБ ИГРЕ");
            Add("pause", "PAUSE", "ПАУЗА");
            Add("continue", "CONTINUE", "ПРОДОЛЖИТЬ");
            Add("restart", "RESTART", "ЗАНОВО");
            Add("menu", "MENU", "МЕНЮ");
            Add("win_title", "LEVEL COMPLETE", "УРОВЕНЬ ПРОЙДЕН");
            Add("lose_title", "DEFEAT", "ПОРАЖЕНИЕ");
            Add("reason_score", "Not enough points", "Недостаточно очков");
            Add("reason_full", "The board is full", "Поле заполнено");
            Add("reason_idle", "The sphere flew away", "Сфера улетела");
            Add("accuracy", "Accuracy", "Точность");
            Add("max_cascade", "Best cascade", "Лучший каскад");
            Add("coins", "Coins", "Монеты");
            Add("energy", "Energy", "Энергия");
            Add("next", "NEXT", "ДАЛЕЕ");
            Add("retry", "RETRY", "ЕЩЁ РАЗ");
            Add("levels", "LEVELS", "УРОВНИ");
            Add("no_energy", "No energy. +1 every 30 minutes", "Нет энергии. +1 каждые 30 минут");
            Add("idle_warning", "Place a sphere!", "Ставь сферу!");
            Add("perfect", "PERFECT", "ИДЕАЛЬНО");
            Add("good", "GOOD", "ХОРОШО");
            Add("miss", "MISS", "МИМО");
            Add("go", "GO!", "ВПЕРЁД!");
            Add("new_record", "New record!", "Новый рекорд!");
            Add("locked", "Finish the previous track first", "Сначала пройди предыдущий трек");
            Add("close", "CLOSE", "ЗАКРЫТЬ");
            Add("early", "early", "рано");
            Add("late", "late", "поздно");
            Add("ms", "ms", "мс");
            Add("audio_sync", "AUDIO SYNC", "СИНХРОН ЗВУКА");
            Add("show_timing", "TIMING HINT", "ПОДСКАЗКА РАНО/ПОЗДНО");
            Add("sync_hint", "If the game says LATE, press +. If EARLY, press -.", "Если игра пишет «поздно», жми +. Если «рано», жми -.");
            Add("soon_title", "COMING SOON", "СКОРО");
            Add("tut_title", "HOW TO PLAY", "КАК ИГРАТЬ");
            Add("tut_1", "Tap any free cell to place the sphere that flies ahead of the hero. After each of your moves the game adds one more sphere to a random cell.", "Нажми на свободную клетку, чтобы поставить сферу, которая летит перед героем. После каждого твоего хода игра сама добавляет ещё одну сферу в случайную клетку.");
            Add("tut_2", "A new sphere gives +1 to its neighbours: blue turns yellow, yellow turns red, red turns violet.", "Новая сфера даёт +1 соседям: синяя становится жёлтой, жёлтая красной, красная фиолетовой.");
            Add("tut_3", "A violet sphere that gets +1 explodes and gives +1 to its own neighbours. That is how cascades start.", "Фиолетовая сфера от +1 взрывается и даёт +1 своим соседям. Так запускаются каскады.");
            Add("tut_4", "Place spheres in time with the markers on the rhythm bar. Three hits in a row raise the multiplier up to x10.", "Ставь сферы в такт меткам на ритм-диаграмме. Три попадания подряд повышают множитель, до x10.");
            Add("tut_5", "Reach the goal score before the track ends. Do not let the board fill up and do not wait longer than 15 seconds.", "Набери цель по очкам до конца трека. Не дай полю заполниться и не жди дольше 15 секунд.");
            Add("tut_play", "PLAY", "ИГРАТЬ");
            Add("tut_next", "NEXT", "ДАЛЬШЕ");
        }

        private static void Add(string key, string en, string ru)
        {
            Table[key] = new[] { en, ru };
        }

        public static void SetLanguage(string code)
        {
            russian = code == "ru";
        }

        public static string T(string key)
        {
            string[] values;
            if (Table.TryGetValue(key, out values))
            {
                return values[russian ? 1 : 0];
            }
            return key;
        }
    }
}
