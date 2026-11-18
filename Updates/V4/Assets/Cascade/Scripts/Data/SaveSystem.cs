using System;
using System.Collections.Generic;
using Cascade.Core;
using UnityEngine;

namespace Cascade.Data
{
    [Serializable]
    public sealed class LevelProgress
    {
        public string id = "";
        public int stars;
        public int bestScore;
        public int plays;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public string playerName = "Vadim";
        public string language = "";
        public string hero = "boy";
        public string lastLevelId = "";
        public int coins;
        public int energy = 5;
        public long energyTicks;
        public bool tutorialDone;
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.8f;
        public bool vibration = true;
        public bool framePulse = true;
        public bool rhythmDiagram = true;
        public float audioOffsetMs;
        public bool showTiming = true;
        public List<LevelProgress> levels = new List<LevelProgress>();
    }

    /// <summary>Локальные сохранения: прогресс, звёзды, монеты, энергия и настройки. Работает без интернета.</summary>
    public static class SaveSystem
    {
        private const string Key = "cascade.save.v1";
        private static SaveData data;

        public static SaveData Data
        {
            get
            {
                if (data == null)
                {
                    Load();
                }
                return data;
            }
        }

        public static void Load()
        {
            data = null;
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<SaveData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Cascade] Save is damaged, starting fresh: " + e.Message);
                }
            }
            if (data == null)
            {
                data = new SaveData();
                data.energyTicks = DateTime.UtcNow.Ticks;
            }
            if (data.levels == null)
            {
                data.levels = new List<LevelProgress>();
            }
            if (string.IsNullOrEmpty(data.language))
            {
                data.language = Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
            }
        }

        public static void Save()
        {
            if (data == null)
            {
                return;
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            Load();
        }

        public static LevelProgress GetLevel(string id)
        {
            List<LevelProgress> levels = Data.levels;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].id == id)
                {
                    return levels[i];
                }
            }
            LevelProgress progress = new LevelProgress();
            progress.id = id;
            levels.Add(progress);
            return progress;
        }

        /// <summary>Начисляет энергию за прошедшее реальное время (в том числе пока игра была закрыта).</summary>
        public static int RefreshEnergy(BalanceConfig config)
        {
            SaveData save = Data;
            long last = save.energyTicks;
            int energy = EnergyMath.Restore(save.energy, ref last, DateTime.UtcNow.Ticks, config.maxEnergy, config.energyRestoreMinutes);
            if (energy != save.energy || last != save.energyTicks)
            {
                save.energy = energy;
                save.energyTicks = last;
            }
            return energy;
        }

        /// <summary>Пополняет энергию до максимума.</summary>
        public static void RefillEnergy(BalanceConfig config)
        {
            SaveData save = Data;
            save.energy = config.maxEnergy;
            save.energyTicks = DateTime.UtcNow.Ticks;
            Save();
        }

        public static void SpendEnergy(BalanceConfig config)
        {
            SaveData save = Data;
            RefreshEnergy(config);
            if (save.energy >= config.maxEnergy)
            {
                save.energyTicks = DateTime.UtcNow.Ticks;
            }
            if (save.energy > 0)
            {
                save.energy--;
            }
            Save();
        }
    }
}
