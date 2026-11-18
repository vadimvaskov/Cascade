using System.Collections.Generic;

namespace Cascade.Data
{
    public sealed class LevelDef
    {
        public string Id;
        public int World;
        public int Number;
        public string Title;
        public float Bpm;
        public float Duration;
        public int MinScore;
        public int Order;
    }

    public sealed class WorldDef
    {
        public int Index;
        public string NameEn;
        public string NameRu;
        public string Genre;
        public LevelDef[] Levels;

        public string Name
        {
            get { return Loc.IsRussian ? NameRu : NameEn; }
        }
    }

    /// <summary>
    /// Список миров и уровней. Порог очков уровня считается от длины трека: очки в секунду растут
    /// от первого уровня к последнему. Если в описании уровня указан свой порог (не 0), берётся он.
    /// </summary>
    public static class LevelCatalog
    {
        /// <summary>Сколько очков в секунду нужно для одной звезды на первом уровне.</summary>
        public static float GoalPerSecondFirst = 2900f;
        /// <summary>То же на последнем уровне; между ними порог растёт равномерно.</summary>
        public static float GoalPerSecondLast = 5800f;

        /// <summary>Пока идёт тестирование, все главы и уровни открыты.</summary>
        public static bool UnlockAllForTesting = true;

        private static readonly List<WorldDef> WorldList = new List<WorldDef>();
        private static readonly List<LevelDef> LevelList = new List<LevelDef>();

        static LevelCatalog()
        {
            AddWorld(1, "The Last Note", "Последняя нота", "Disco",
                Level("w1_disco_1", 1, 1, "Disco 1", 116.08f, 89.974f, 0),
                Level("w1_disco_2", 1, 2, "Disco 2", 110.07f, 89.974f, 0));
            AddWorld(2, "The Floating District", "Парящий квартал", "Drum & Bass",
                Level("w2_dnb_1", 2, 1, "Drum & Bass 1", 87.5f, 234.774f, 0),
                Level("w2_dnb_2", 2, 2, "Drum & Bass 2", 87.24f, 92.374f, 0));
            AddWorld(3, "The Echo Canyon", "Каньон эха", "Dubstep",
                Level("w3_dubstep_1", 3, 1, "Dubstep 1", 143.75f, 199.293f, 0),
                Level("w3_dubstep_2", 3, 2, "Dubstep 2", 145.07f, 181.934f, 0));
            AddWorld(4, "The Cascade Heights", "Высоты Каскада", "Electro Swing",
                Level("w4_electroswing_1", 4, 1, "Electro Swing 1", 105.01f, 166.013f, 0),
                Level("w4_electroswing_2", 4, 2, "Electro Swing 2", 130.54f, 155.534f, 0));
            AddWorld(5, "The Crystal Spires", "Хрустальные шпили", "Melodic Trance",
                Level("w5_trance_1", 5, 1, "Melodic Trance 1", 136.59f, 92.374f, 0),
                Level("w5_trance_2", 5, 2, "Melodic Trance 2", 101.14f, 89.694f, 0));
            AddWorld(6, "The Resonant Fields", "Резонансные поля", "Orchestral Electronic",
                Level("w6_orchestral_1", 6, 1, "Orchestral Electronic 1", 146.73f, 90.894f, 0),
                Level("w6_orchestral_2", 6, 2, "Orchestral Electronic 2", 136.94f, 91.853f, 0));
            AddWorld(7, "The Golden Hall", "Золотой зал", "Rock ’n’ Roll",
                Level("w7_rocknroll_1", 7, 1, "Rock ’n’ Roll 1", 92.66f, 118.374f, 0),
                Level("w7_rocknroll_2", 7, 2, "Rock ’n’ Roll 2", 92.68f, 117.614f, 0));
            AddWorld(8, "The Orbital Terrace", "Орбитальная терраса", "Rock Electronic",
                Level("w8_rockelectro_1", 8, 1, "Rock Electronic 1", 136.77f, 88.734f, 0),
                Level("w8_rockelectro_2", 8, 2, "Rock Electronic 2", 97.91f, 90.013f, 0));
            AddWorld(9, "The Crystal Arches", "Хрустальные арки", "Synthwave",
                Level("w9_synthwave_1", 9, 1, "Synthwave 1", 101.33f, 116.374f, 0),
                Level("w9_synthwave_2", 9, 2, "Synthwave 2", 100.05f, 89.254f, 0));
            ApplyGoals();
        }

        public static IList<WorldDef> Worlds { get { return WorldList; } }
        public static IList<LevelDef> Levels { get { return LevelList; } }

        private static LevelDef Level(string id, int world, int number, string title, float bpm, float duration, int minScore)
        {
            LevelDef level = new LevelDef();
            level.Id = id;
            level.World = world;
            level.Number = number;
            level.Title = title;
            level.Bpm = bpm;
            level.Duration = duration;
            level.MinScore = minScore;
            return level;
        }

        private static void AddWorld(int index, string nameEn, string nameRu, string genre, params LevelDef[] levels)
        {
            WorldDef world = new WorldDef();
            world.Index = index;
            world.NameEn = nameEn;
            world.NameRu = nameRu;
            world.Genre = genre;
            world.Levels = levels;
            WorldList.Add(world);
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i].Order = LevelList.Count;
                LevelList.Add(levels[i]);
            }
        }

        private static void ApplyGoals()
        {
            int count = LevelList.Count;
            for (int i = 0; i < count; i++)
            {
                LevelDef level = LevelList[i];
                if (level.MinScore > 0)
                {
                    continue;
                }
                float progress = count > 1 ? (float)i / (count - 1) : 0f;
                float perSecond = GoalPerSecondFirst + (GoalPerSecondLast - GoalPerSecondFirst) * progress;
                level.MinScore = (int)System.Math.Round(perSecond * level.Duration / 1000.0) * 1000;
            }
        }

        public static LevelDef Find(string id)
        {
            for (int i = 0; i < LevelList.Count; i++)
            {
                if (LevelList[i].Id == id)
                {
                    return LevelList[i];
                }
            }
            return null;
        }

        public static WorldDef World(int index)
        {
            for (int i = 0; i < WorldList.Count; i++)
            {
                if (WorldList[i].Index == index)
                {
                    return WorldList[i];
                }
            }
            return null;
        }

        /// <summary>Уровень открыт, если предыдущий пройден хотя бы на одну звезду.</summary>
        public static bool IsUnlocked(LevelDef level)
        {
            if (UnlockAllForTesting || level.Order == 0)
            {
                return true;
            }
            LevelDef previous = LevelList[level.Order - 1];
            return SaveSystem.GetLevel(previous.Id).stars > 0;
        }

        public static LevelDef NextAfter(LevelDef level)
        {
            int next = level.Order + 1;
            return next < LevelList.Count ? LevelList[next] : null;
        }

        /// <summary>Уровень для кнопки PLAY: первый открытый и ещё не пройденный, иначе последний сыгранный.</summary>
        public static LevelDef Recommended()
        {
            for (int i = 0; i < LevelList.Count; i++)
            {
                if (SaveSystem.GetLevel(LevelList[i].Id).stars == 0)
                {
                    return IsUnlocked(LevelList[i]) ? LevelList[i] : LevelList[System.Math.Max(0, i - 1)];
                }
            }
            LevelDef last = Find(SaveSystem.Data.lastLevelId);
            return last ?? LevelList[0];
        }

        public static int StarsInWorld(WorldDef world)
        {
            int stars = 0;
            for (int i = 0; i < world.Levels.Length; i++)
            {
                stars += SaveSystem.GetLevel(world.Levels[i].Id).stars;
            }
            return stars;
        }
    }
}
