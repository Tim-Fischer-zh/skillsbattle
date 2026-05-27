using Bunit;
using FluentAssertions;
using KillerSudoku.Web.Components.Shared;

namespace KillerSudoku.ComponentTests;

/// <summary>
/// T003 — MiniSudokuExample renders ≥1 cell with class "cage-sum" whose value is 1..45.
/// Spec anchor: docs/use-cases.md UC01 AC01.2 + docs/test-protocol.md T003.
/// </summary>
public class MiniSudokuExampleTests : BunitContext
{
    [Fact]
    public void Render_ContainsAtLeastOneCageSum_WithValueBetween1And45()
    {
        var cut = Render<MiniSudokuExample>();

        var sums = cut.FindAll(".cage-sum");
        sums.Should().NotBeEmpty(
            "AC01.2 / T003 require at least one cage-sum visible in the top-left corner of a cage");

        foreach (var node in sums)
        {
            var raw = node.TextContent.Trim();
            int.TryParse(raw, out var value).Should().BeTrue(
                $"cage-sum must be an integer, but found '{raw}'");
            value.Should().BeInRange(1, 45,
                "cage sums in a 9×9 Killer Sudoku are bounded by 1 (single cell) and 45 (=1+2+…+9)");
        }
    }

    [Fact]
    public void Render_DrawsANineByNineGrid()
    {
        var cut = Render<MiniSudokuExample>();

        var cells = cut.FindAll(".mini-sudoku__cell");
        cells.Count.Should().Be(81, "the demo grid is a full 9×9 Killer Sudoku");
    }

    [Fact]
    public void Render_CageSumsTotalTo405()
    {
        var cut = Render<MiniSudokuExample>();

        int total = 0;
        foreach (var node in cut.FindAll(".cage-sum"))
            total += int.Parse(node.TextContent.Trim());

        total.Should().Be(405,
            "Σ aller Cage-Sums = Σ aller Zellen-Werte = 9 × 45 = 405 (README §2.3)");
    }

    [Fact]
    public void Render_WithShowValuesFalse_OmitsCellValues_ButKeepsCageSums()
    {
        var cut = Render<MiniSudokuExample>(
            ps => ps.Add(p => p.ShowValues, false));

        cut.FindAll(".mini-sudoku__value").Should().BeEmpty(
            "ShowValues=false hides solved numbers (used for hero eye-catcher)");

        cut.FindAll(".cage-sum").Should().NotBeEmpty(
            "cage sums must remain visible even when solved numbers are hidden");
    }
}
