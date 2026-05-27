using FluentAssertions;
using KillerSudoku.Core.Services;

namespace KillerSudoku.UnitTests;

public class ScoreCalculatorTests
{
    private readonly ScoreCalculator _sut = new();

    [Theory]
    [InlineData(300, 0, 9700)]   // T082-Variant
    [InlineData(600, 2, 8800)]   // T083
    [InlineData(10000, 10, 0)]   // T084 — floor
    [InlineData(500, 0, 9500)]   // T094 — hints=0 boundary
    [InlineData(0, 0, 10000)]    // theoretical max
    public void Calculate_KnownInputs_ReturnsExpectedScore(int time, int hints, int expected)
    {
        _sut.Calculate(time, hints).Should().Be(expected);
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
