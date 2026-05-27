using KillerSudoku.Core.Models;

namespace KillerSudoku.UnitTests.Fixtures;

/// <summary>
/// Triviales Sudoku gemäß bekannter Latin-Square-Formel
/// grid[r,c] = ((r / 3) + (r * 3) + c) % 9 + 1
///
/// Erfüllt: Row/Col/Nonet 1-9 exactly once, Sum = 405.
/// Wird in Tests als bekannte valide Lösung verwendet — Mutationen zerstören
/// einzelne Constraints kontrolliert.
/// </summary>
internal static class TrivialSudoku
{
    public static byte[,] Grid()
    {
        var g = new byte[9, 9];
        for (int r = 0; r < 9; r++)
        for (int c = 0; c < 9; c++)
            g[r, c] = (byte)((r / 3 + r * 3 + c) % 9 + 1);
        return g;
    }

    public static byte[,] WithSwap(byte[,] src, int r1, int c1, int r2, int c2)
    {
        var copy = (byte[,])src.Clone();
        (copy[r1, c1], copy[r2, c2]) = (copy[r2, c2], copy[r1, c1]);
        return copy;
    }

    public static byte[,] WithCell(byte[,] src, int r, int c, byte v)
    {
        var copy = (byte[,])src.Clone();
        copy[r, c] = v;
        return copy;
    }

    /// <summary>9 Row-Cages, jede Cage = ganze Reihe (sum 45). Triviale Cage-Definition für Validator-Smoke.</summary>
    public static IReadOnlyList<CageInputDto> RowCages()
    {
        var list = new List<CageInputDto>(9);
        for (byte r = 0; r < 9; r++)
        {
            var cells = new List<(byte Row, byte Col)>(9);
            for (byte c = 0; c < 9; c++) cells.Add((r, c));
            list.Add(new CageInputDto(45, cells));
        }
        return list;
    }
}
