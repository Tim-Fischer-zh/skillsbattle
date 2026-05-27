using Microsoft.AspNetCore.Identity;

namespace KillerSudoku.Data.Entities;

public sealed class AppUser : IdentityUser<int>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Puzzle> CreatedPuzzles { get; set; } = [];
    public ICollection<Game> Games { get; set; } = [];
}

public sealed class Puzzle
{
    public int Id { get; set; }
    public byte Difficulty { get; set; }
    public int CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Cage> Cages { get; set; } = [];
    public ICollection<Game> Games { get; set; } = [];
}

public sealed class Cage
{
    public int Id { get; set; }
    public int PuzzleId { get; set; }
    public Puzzle Puzzle { get; set; } = null!;
    public byte Sum { get; set; }
    public ICollection<CageCell> Cells { get; set; } = [];
}

public sealed class CageCell
{
    public int CageId { get; set; }
    public Cage Cage { get; set; } = null!;
    public byte RowIdx { get; set; }
    public byte ColIdx { get; set; }
}

public sealed class Game
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int PuzzleId { get; set; }
    public Puzzle Puzzle { get; set; } = null!;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public int? TimeSeconds { get; set; }
    public int HintsUsed { get; set; }
    public int? Score { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsPaused { get; set; }
    public DateTime? PausedAt { get; set; }
    public int TotalPausedSeconds { get; set; }
    public ICollection<GameCell> Cells { get; set; } = [];
    public ICollection<PencilMark> PencilMarks { get; set; } = [];
    public ICollection<HintLog> Hints { get; set; } = [];
}

public sealed class GameCell
{
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public byte RowIdx { get; set; }
    public byte ColIdx { get; set; }
    public byte? CellValue { get; set; }
}

public sealed class PencilMark
{
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public byte RowIdx { get; set; }
    public byte ColIdx { get; set; }
    public byte MarkValue { get; set; }
}

public sealed class HintLog
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public byte RowIdx { get; set; }
    public byte ColIdx { get; set; }
    public DateTime HintAt { get; set; } = DateTime.UtcNow;
}
