using System;
using System.Collections.Generic;

/// <summary>Where one word sits in the grid: start cell, direction (dRow/dCol) and the word with its meaning.</summary>
public class WordSearchPlacement
{
    public string word;
    public string meaning;
    public string description;
    public int row;
    public int col;
    public int dRow;
    public int dCol;
    public bool found;
    public int colorIndex = -1;

    public int EndRow { get { return row + dRow * (word.Length - 1); } }
    public int EndCol { get { return col + dCol * (word.Length - 1); } }
}

/// <summary>A generated puzzle: the 10x10 letters and where each word was hidden.</summary>
public class WordSearchGrid
{
    public const int Size = 10;
    public char[,] letters = new char[Size, Size];
    public List<WordSearchPlacement> placements = new List<WordSearchPlacement>();
}

/// <summary>
/// WordSearchGenerator
/// Hides a puzzle's words in a 10x10 grid. Words run across, down or diagonally down-right (never backwards); words may cross where
/// their letters match. The remaining cells are filled with random letters, mostly taken from the puzzle's own words so the grid
/// looks natural. The same seed always gives the same grid, so every puzzle keeps one fixed layout.
/// </summary>
public static class WordSearchGenerator
{
    private static readonly int[,] Directions = { { 0, 1 }, { 1, 0 }, { 1, 1 } };
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static WordSearchGrid Generate(IList<WordSearchWord> words, int seed)
    {
        var ordered = new List<WordSearchWord>(words);
        ordered.Sort((a, b) => b.word.Length.CompareTo(a.word.Length));

        for (int attempt = 0; attempt < 400; attempt++)
        {
            var rng = new Random(seed + attempt * 101);
            WordSearchGrid grid = TryPlace(ordered, rng);
            if (grid != null)
            {
                Fill(grid, ordered, rng);
                return grid;
            }
        }
        return null;
    }

    private static WordSearchGrid TryPlace(List<WordSearchWord> ordered, Random rng)
    {
        var grid = new WordSearchGrid();
        for (int r = 0; r < WordSearchGrid.Size; r++)
        {
            for (int c = 0; c < WordSearchGrid.Size; c++)
            {
                grid.letters[r, c] = '\0';
            }
        }

        foreach (WordSearchWord w in ordered)
        {
            string text = w.word.Trim().ToUpperInvariant();
            bool placed = false;
            for (int tries = 0; tries < 300 && !placed; tries++)
            {
                int d = rng.Next(Directions.GetLength(0));
                int dr = Directions[d, 0];
                int dc = Directions[d, 1];
                int r = rng.Next(WordSearchGrid.Size);
                int c = rng.Next(WordSearchGrid.Size);
                if (!Fits(grid, text, r, c, dr, dc))
                {
                    continue;
                }
                for (int i = 0; i < text.Length; i++)
                {
                    grid.letters[r + dr * i, c + dc * i] = text[i];
                }
                grid.placements.Add(new WordSearchPlacement { word = text, meaning = w.meaning, description = w.description, row = r, col = c, dRow = dr, dCol = dc });
                placed = true;
            }
            if (!placed)
            {
                return null;
            }
        }
        return grid;
    }

    private static bool Fits(WordSearchGrid grid, string text, int r, int c, int dr, int dc)
    {
        int endR = r + dr * (text.Length - 1);
        int endC = c + dc * (text.Length - 1);
        if (endR >= WordSearchGrid.Size || endC >= WordSearchGrid.Size)
        {
            return false;
        }
        bool crossesAnother = false;
        for (int i = 0; i < text.Length; i++)
        {
            char existing = grid.letters[r + dr * i, c + dc * i];
            if (existing != '\0')
            {
                if (existing != text[i])
                {
                    return false;
                }
                crossesAnother = true;
            }
        }
        // a word may not sit completely on top of letters that already belong to other words
        return !(crossesAnother && AllCellsFilled(grid, text, r, c, dr, dc));
    }

    private static bool AllCellsFilled(WordSearchGrid grid, string text, int r, int c, int dr, int dc)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (grid.letters[r + dr * i, c + dc * i] == '\0')
            {
                return false;
            }
        }
        return true;
    }

    private static void Fill(WordSearchGrid grid, List<WordSearchWord> words, Random rng)
    {
        var pool = new List<char>();
        foreach (WordSearchWord w in words)
        {
            foreach (char ch in w.word.ToUpperInvariant())
            {
                if (Alphabet.IndexOf(ch) >= 0)
                {
                    pool.Add(ch);
                }
            }
        }
        for (int r = 0; r < WordSearchGrid.Size; r++)
        {
            for (int c = 0; c < WordSearchGrid.Size; c++)
            {
                if (grid.letters[r, c] == '\0')
                {
                    bool fromWords = pool.Count > 0 && rng.Next(100) < 60;
                    grid.letters[r, c] = fromWords ? pool[rng.Next(pool.Count)] : Alphabet[rng.Next(Alphabet.Length)];
                }
            }
        }
    }
}
