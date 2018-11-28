using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SudokuSolver
{
    class Program
    {
        static void Main(string[] args)
        {
            var s = new Sudoku();
            // "World's Hardest Sudoku": https://www.conceptispuzzles.com/index.aspx?uri=info%2Farticle%2F424
            s.Parse(@"
                8.. ... ...
                ..3 6.. ...
                .7. .9. 2..

                .5. ..7 ...
                ... .45 7..
                ... 1.. .3.

                ..1 ... .68
                ..8 5.. .1.
                .9. ... 4..");
            s.Print();
            s.Solve();
            s.Print();
            Console.ReadLine();
        }
    }

    class Sudoku
    {
        private Cell[] _cells = new Cell[9 * 9];
        private Cell[][] _sets;

        public Sudoku()
        {
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = new Cell();
            var sets = new List<List<Cell>>();
            // Rows
            for (int y = 0; y < 9; y++)
            {
                var set = new List<Cell>();
                sets.Add(set);
                for (int x = 0; x < 9; x++)
                    set.Add(this[x, y]);
            }
            // Columns
            for (int x = 0; x < 9; x++)
            {
                var set = new List<Cell>();
                sets.Add(set);
                for (int y = 0; y < 9; y++)
                    set.Add(this[x, y]);
            }
            // Squares
            for (int sqY = 0; sqY < 3; sqY++)
                for (int sqX = 0; sqX < 3; sqX++)
                {
                    var set = new List<Cell>();
                    sets.Add(set);
                    for (int subSqY = 0; subSqY < 3; subSqY++)
                        for (int subSqX = 0; subSqX < 3; subSqX++)
                            set.Add(this[sqX * 3 + subSqX, sqY * 3 + subSqY]);
                }

            _sets = sets.Select(s => s.ToArray()).ToArray();
        }

        public Cell this[int x, int y] => _cells[y * 9 + x];

        public void Solve()
        {
            again:
            if (_cells.All(c => c.Count == 1))
                return; // it's solved
            bool anyChanges = false;

            // Part 1: if we have N identical cells with N possibilities then none of the other cells can have any of these possibilities
            foreach (var set in _sets)
                foreach (var cell in set)
                {
                    var duplicates = set.Where(c => c.Count == cell.Count && c != cell && c.IsSame(cell)).ToList();
                    if (duplicates.Count + 1 == cell.Count)
                        foreach (var other in set.Where(c => c != cell && !duplicates.Contains(c)))
                            anyChanges |= other.Exclude(cell);
                }
            if (anyChanges)
                goto again;

            // Part 2: if N cells all contain a specific subset of N possibilities, and no other cells contain any of these possibilities, then these N cells can't contain anything else
            for (int subsetSize = 1; subsetSize <= 8; subsetSize++)
            {
                foreach (var set in _sets)
                    foreach (var subset in set.Where(c => c.Count >= subsetSize).SelectMany(c => c.Subsets(subsetSize)))
                    {
                        var containSubset = set.Where(c => c.Count >= subsetSize && c.ContainsAll(subset)).ToList();
                        if (containSubset.Count == subsetSize)
                        {
                            var others = set.Where(c => !containSubset.Contains(c)).ToList();
                            if (!others.Any(o => subset.Any(num => o.IsPossible(num))))
                                foreach (var sub in containSubset)
                                    anyChanges |= sub.CopyFrom(subset);
                        }
                    }
                if (anyChanges)
                    goto again; // stick to shorter subsets while we're finding changes
            }

            // Part 3: for each of the remaining possibilities, assume that value for the cell and attempt to solve under that assumption
            // Simple sudokus never get here
            for (int c = 0; c < 9 * 9; c++)
            {
                if (_cells[c].Count == 1)
                    continue;

                foreach (var num in _cells[c])
                {
                    var clone = new Sudoku();
                    foreach (var pair in clone._cells.Zip(_cells, (cc, oc) => new { cc, oc }))
                        pair.cc.CopyFrom(pair.oc);
                    clone._cells[c].SetKnown(num);
                    try { clone.Solve(); }
                    catch { continue; }
                    // Success! It's solved.
                    foreach (var pair in clone._cells.Zip(_cells, (cc, oc) => new { cc, oc }))
                        pair.oc.CopyFrom(pair.cc);
                    return;
                }
            }

            throw new Exception(); // not solved - contradictory?
        }

        public void Parse(string s)
        {
            var chars = s.Where(c => char.IsDigit(c) || c == '.').ToArray();
            if (chars.Length != 9 * 9)
                throw new Exception();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] != '.')
                    this[i % 9, i / 9].SetKnown(chars[i] - '0');
        }

        public void Print()
        {
            var scr = new char[4 * 9 + 2][];
            for (int y = 0; y < 4 * 9 + 2; y++)
            {
                scr[y] = new char[4 * 9 + 4];
                for (int x = 0; x < 4 * 9 + 4; x++)
                    scr[y][x] = ' ';
            }
            for (int x = 0; x < 9; x++)
                for (int y = 0; y < 9; y++)
                {
                    int posX = x * 4 + (x >= 3 ? 2 : 0) + (x >= 6 ? 2 : 0);
                    int posY = y * 4 + (y >= 3 ? 1 : 0) + (y >= 6 ? 1 : 0);
                    int num = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            num++;
                            scr[posY + sy][posX + sx] = this[x, y].IsPossible(num) ? (char) ('0' + num) : '.';
                        }
                }
            for (int y = 0; y < 4 * 9 + 2; y++)
                Console.WriteLine(new string(scr[y]));
            Console.WriteLine();
            Console.WriteLine(_cells.Sum(c => c.Count));
            Console.WriteLine();
        }
    }


    class Cell : IEnumerable<int>
    {
        private bool[] _possibles = new bool[9];

        public int Count { get; private set; }

        public Cell()
        {
            Count = 9;
            for (int i = 0; i < 9; i++)
                _possibles[i] = true;
        }

        public Cell(IEnumerable<int> nums)
        {
            Count = 0;
            foreach (var num in nums.Distinct())
            {
                Count++;
                _possibles[num - 1] = true;
            }
        }

        public override string ToString() => string.Join(", ", this);

        public bool IsPossible(int num) => _possibles[num - 1];

        public void SetKnown(int num)
        {
            if (num < 1 || num > 9)
                throw new Exception();
            Count = 1;
            for (int i = 0; i < 9; i++)
                _possibles[i] = i == num - 1;
        }

        public bool SetPossible(int num, bool possible)
        {
            if (num < 1 || num > 9)
                throw new Exception();
            if (possible && !_possibles[num - 1])
                Count++;
            else if (!possible && _possibles[num - 1])
                Count--;
            bool changed = true;
            if (_possibles[num - 1] == possible)
                changed = false;
            else
                _possibles[num - 1] = possible;
            if (Count == 0)
                throw new Exception();
            return changed;
        }

        public bool CopyFrom(Cell source)
        {
            bool anyChanges = false;
            for (int i = 0; i < 9; i++)
                anyChanges |= SetPossible(i + 1, source._possibles[i]);
            return anyChanges;
        }

        public bool Exclude(Cell cell)
        {
            bool anyChanges = false;
            foreach (var num in cell)
                anyChanges |= SetPossible(num, false);
            return anyChanges;
        }

        public bool ContainsAll(Cell cell)
        {
            foreach (var num in cell)
                if (!IsPossible(num))
                    return false;
            return true;
        }

        public bool IsSame(Cell cell)
        {
            for (int i = 0; i < 9; i++)
                if (_possibles[i] != cell._possibles[i])
                    return false;
            return true;
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < 9; i++)
                if (_possibles[i])
                    yield return i + 1;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<Cell> Subsets(int subsetSize)
        {
            var nums = this.ToArray();
            if (nums.Length < subsetSize)
                throw new Exception();
            var indexes = new int[subsetSize];
            for (int i = 0; i < indexes.Length; i++)
                indexes[i] = i;
            while (true)
            {
                yield return new Cell(indexes.Select(i => nums[i]));
                int k = indexes.Length;
                while (true)
                {
                    k--;
                    if (k < 0)
                        yield break;
                    indexes[k]++;
                    for (int j = k + 1; j < indexes.Length; j++)
                        indexes[j] = indexes[k] + j - k;
                    if (indexes[indexes.Length - 1] < nums.Length)
                        break;
                }
            }
        }
    }
}
