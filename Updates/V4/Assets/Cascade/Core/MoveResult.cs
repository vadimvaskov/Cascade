using System.Collections.Generic;

namespace Cascade.Core
{
    public enum HitGrade
    {
        Miss = 0,
        Good = 1,
        Perfect = 2
    }

    public enum MoveEventType
    {
        Placed = 0,
        Upgraded = 1,
        Exploded = 2,
        AutoSpawned = 3
    }

    /// <summary>Одно событие хода для показа на экране. Wave: 0 — постановка, 1 и дальше — волны взрывов.</summary>
    public struct MoveEvent
    {
        public MoveEventType Type;
        public int Cell;
        public int Level;
        public int Wave;

        public MoveEvent(MoveEventType type, int cell, int level, int wave)
        {
            Type = type;
            Cell = cell;
            Level = level;
            Wave = wave;
        }
    }

    /// <summary>Полный итог одного хода: логика уже посчитана, графика только показывает события по порядку.</summary>
    public sealed class MoveResult
    {
        public bool Accepted;
        public int Cell;
        public int PlacedLevel;
        public readonly List<MoveEvent> Events = new List<MoveEvent>(64);
        public int Explosions;
        public int WaveCount;
        public int PlaceScore;
        public int UpgradeScore;
        public int ExplosionScore;
        public int CascadeBonus;
        public int RawScore;
        public int FinalScore;
        public int Multiplier;
        public HitGrade Grade;
        public bool DuplicatePosition;
        public double OffsetMs;
        public bool BoardFull;
        public int NextSphere;

        public void Reset()
        {
            Accepted = false;
            Cell = -1;
            PlacedLevel = 0;
            Events.Clear();
            Explosions = 0;
            WaveCount = 0;
            PlaceScore = 0;
            UpgradeScore = 0;
            ExplosionScore = 0;
            CascadeBonus = 0;
            RawScore = 0;
            FinalScore = 0;
            Multiplier = 1;
            Grade = HitGrade.Miss;
            DuplicatePosition = false;
            OffsetMs = 0;
            BoardFull = false;
            NextSphere = 0;
        }
    }
}
