using KillerSudoku.Core.Abstractions;

namespace KillerSudoku.Core.Services;

public sealed class ScoreCalculator : IScoreCalculator
{
    public int Calculate(int timeSeconds, int hintsUsed)
    {
        if (timeSeconds < 0 || hintsUsed < 0)
            throw new ArgumentOutOfRangeException();

        var score = 10000 - timeSeconds - hintsUsed * 300;
        return Math.Max(0, score);
    }
}
