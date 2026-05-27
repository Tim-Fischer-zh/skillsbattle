using FluentAssertions;
using KillerSudoku.Core.Services;

namespace KillerSudoku.UnitTests;

public class ScoreCalculatorTests
{
    private readonly ScoreCalculator _sut = new();

    [Theory]
    [InlineData(300, 0, 9700)]    // T082-Variant
    [InlineData(600, 2, 8800)]    // T083
    [InlineData(10000, 10, 0)]    // T084 — floor (capped at 0)
    [InlineData(500, 0, 9500)]    // T094 — hints=0 boundary
    [InlineData(0, 0, 10000)]     // theoretical max
    [InlineData(99999, 99, 0)]    // T071 — Score Floor: extreme inputs cap at 0
    [InlineData(10000, 0, 0)]     // T071-edge — Score-Boundary: exact 0
    public void Calculate_KnownInputs_ReturnsExpectedScore(int time, int hints, int expected)
    {
        _sut.Calculate(time, hints).Should().Be(expected);
    }

    // T071 — Score-Floor: kein negativer Score möglich
    [Fact]
    public void T071_Calculate_ExceedingMaxPenalty_ScoreIsZero()
    {
        _sut.Calculate(timeSeconds: 50_000, hintsUsed: 100).Should().Be(0);
    }

    [Fact]
    public void Calculate_NegativeTime_Throws()
    {
        var act = () => _sut.Calculate(-1, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Calculate_NegativeHints_Throws()
    {
        var act = () => _sut.Calculate(100, -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
