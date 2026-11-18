using System;
using Cascade.Core;
using UnityEngine;

namespace Cascade.Data
{
    /// <summary>Чтение карты ритма трека из Resources/BeatMaps/&lt;id&gt;.json.</summary>
    public static class BeatMapLoader
    {
        [Serializable]
        private sealed class BeatMapJson
        {
            public string trackId = "";
            public float bpm;
            public float duration;
            public bool swing;
            public float[] times = new float[0];
            public int[] levels = new int[0];
        }

        public static BeatMapData Load(string trackId)
        {
            TextAsset asset = Resources.Load<TextAsset>("BeatMaps/" + trackId);
            BeatMapData map = new BeatMapData();
            if (asset == null)
            {
                Debug.LogError("[Cascade] Beat map not found: " + trackId);
                return map;
            }
            BeatMapJson json = JsonUtility.FromJson<BeatMapJson>(asset.text);
            int count = Math.Min(json.times.Length, json.levels.Length);
            map.Times = new double[count];
            map.Levels = new int[count];
            for (int i = 0; i < count; i++)
            {
                map.Times[i] = json.times[i];
                map.Levels[i] = json.levels[i];
            }
            map.Duration = json.duration;
            map.Bpm = json.bpm;
            return map;
        }
    }
}
