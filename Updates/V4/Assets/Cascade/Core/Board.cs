using System;

namespace Cascade.Core
{
    /// <summary>
    /// Состояние поля. Клетка хранит уровень сферы: 0 — пусто, 1 синяя, 2 жёлтая, 3 красная, 4 фиолетовая.
    /// Индекс клетки = строка * размер + столбец, строка 0 — верхняя.
    /// </summary>
    public sealed class Board
    {
        public const int EmptyLevel = 0;
        public const int MaxStableLevel = 4;

        private readonly int size;
        private readonly int[] cells;
        private int occupied;

        public Board(int size)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException("size");
            }
            this.size = size;
            cells = new int[size * size];
        }

        public int Size { get { return size; } }
        public int CellCount { get { return cells.Length; } }
        public int OccupiedCount { get { return occupied; } }
        public int EmptyCount { get { return cells.Length - occupied; } }
        public bool IsFull { get { return occupied == cells.Length; } }

        public int Index(int row, int col)
        {
            return row * size + col;
        }

        public int Get(int index)
        {
            return cells[index];
        }

        public bool IsEmpty(int index)
        {
            return cells[index] == EmptyLevel;
        }

        public void Set(int index, int level)
        {
            if (level < EmptyLevel || level > MaxStableLevel)
            {
                throw new ArgumentOutOfRangeException("level");
            }
            int previous = cells[index];
            if (previous == EmptyLevel && level != EmptyLevel)
            {
                occupied++;
            }
            else if (previous != EmptyLevel && level == EmptyLevel)
            {
                occupied--;
            }
            cells[index] = level;
        }

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            occupied = 0;
        }

        /// <summary>Соседи только по сторонам: вверх, вниз, влево, вправо. Возвращает их количество.</summary>
        public int GetNeighbors(int index, int[] buffer)
        {
            int row = index / size;
            int col = index - row * size;
            int count = 0;
            if (row > 0)
            {
                buffer[count++] = index - size;
            }
            if (row < size - 1)
            {
                buffer[count++] = index + size;
            }
            if (col > 0)
            {
                buffer[count++] = index - 1;
            }
            if (col < size - 1)
            {
                buffer[count++] = index + 1;
            }
            return count;
        }

        public int[] ToArray()
        {
            int[] copy = new int[cells.Length];
            Array.Copy(cells, copy, cells.Length);
            return copy;
        }
    }
}
