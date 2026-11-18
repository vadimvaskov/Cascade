using System.Collections.Generic;

namespace Cascade.Core
{
    /// <summary>
    /// Постановка сферы, передача +1 соседям, взрывы и цепные реакции.
    /// Взрывы считаются волнами: все сферы, достигшие предела, исчезают одновременно,
    /// затем каждая из них даёт +1 оставшимся соседям. Результат не зависит от порядка обхода клеток.
    /// </summary>
    public sealed class CascadeResolver
    {
        private readonly int[] neighborBuffer = new int[4];
        private readonly List<int> exploding = new List<int>(36);
        private int[] pending = new int[36];

        public void Resolve(Board board, int cell, int level, BalanceConfig config, MoveResult result)
        {
            if (pending.Length < board.CellCount)
            {
                pending = new int[board.CellCount];
            }
            for (int i = 0; i < board.CellCount; i++)
            {
                pending[i] = 0;
            }

            board.Set(cell, level);
            result.Cell = cell;
            result.PlacedLevel = level;
            result.PlaceScore = config.scorePlace;
            result.Events.Add(new MoveEvent(MoveEventType.Placed, cell, level, 0));

            // Новая сфера даёт +1 занятым соседям и сама ничего не получает
            int count = board.GetNeighbors(cell, neighborBuffer);
            for (int i = 0; i < count; i++)
            {
                int neighbor = neighborBuffer[i];
                if (!board.IsEmpty(neighbor))
                {
                    pending[neighbor]++;
                }
            }

            int wave = 0;
            while (true)
            {
                exploding.Clear();
                bool anyImpulse = false;
                for (int i = 0; i < board.CellCount; i++)
                {
                    int impulses = pending[i];
                    if (impulses == 0)
                    {
                        continue;
                    }
                    pending[i] = 0;
                    anyImpulse = true;
                    int current = board.Get(i);
                    bool explodes = false;
                    for (int k = 0; k < impulses; k++)
                    {
                        result.UpgradeScore += config.UpgradeScore(current);
                        current++;
                        if (current > Board.MaxStableLevel)
                        {
                            explodes = true;
                            break;
                        }
                        result.Events.Add(new MoveEvent(MoveEventType.Upgraded, i, current, wave));
                    }
                    if (explodes)
                    {
                        exploding.Add(i);
                    }
                    else
                    {
                        board.Set(i, current);
                    }
                }
                if (!anyImpulse || exploding.Count == 0)
                {
                    break;
                }

                wave++;
                for (int i = 0; i < exploding.Count; i++)
                {
                    int index = exploding[i];
                    board.Set(index, Board.EmptyLevel);
                    result.Explosions++;
                    result.Events.Add(new MoveEvent(MoveEventType.Exploded, index, 0, wave));
                }
                for (int i = 0; i < exploding.Count; i++)
                {
                    int n = board.GetNeighbors(exploding[i], neighborBuffer);
                    for (int k = 0; k < n; k++)
                    {
                        int neighbor = neighborBuffer[k];
                        if (!board.IsEmpty(neighbor))
                        {
                            pending[neighbor]++;
                        }
                    }
                }
            }

            result.WaveCount = wave;
            result.ExplosionScore = result.Explosions * config.scoreExplosion;
            if (result.Explosions > 1)
            {
                int extra = result.Explosions - 1;
                result.CascadeBonus = config.cascadeBonusFactor * extra * extra;
            }
            result.RawScore = result.PlaceScore + result.UpgradeScore + result.ExplosionScore + result.CascadeBonus;
        }
    }
}
