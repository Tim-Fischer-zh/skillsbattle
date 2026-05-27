using KillerSudoku.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KillerSudoku.Data.Persistence;

public sealed class SudokuDbContext(DbContextOptions<SudokuDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<int>, int>(options)
{
    public DbSet<Puzzle> Puzzles => Set<Puzzle>();
    public DbSet<Cage> Cages => Set<Cage>();
    public DbSet<CageCell> CageCells => Set<CageCell>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameCell> GameCells => Set<GameCell>();
    public DbSet<PencilMark> PencilMarks => Set<PencilMark>();
    public DbSet<HintLog> HintLogs => Set<HintLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.ToTable("AppUser");
            e.Property(x => x.UserName).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(255);
            e.HasIndex(x => x.UserName).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Puzzle>(e =>
        {
            e.ToTable("Puzzle", t => t.HasCheckConstraint("CK_Puzzle_Difficulty", "[Difficulty] BETWEEN 1 AND 3"));
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.CreatedBy).WithMany(u => u.CreatedPuzzles).HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Cage>(e =>
        {
            e.ToTable("Cage", t => t.HasCheckConstraint("CK_Cage_Sum_Range", "[Sum] BETWEEN 1 AND 45"));
            e.HasOne(x => x.Puzzle).WithMany(p => p.Cages).HasForeignKey(x => x.PuzzleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CageCell>(e =>
        {
            e.HasKey(x => new { x.CageId, x.RowIdx, x.ColIdx });
            e.HasOne(x => x.Cage).WithMany(c => c.Cells).HasForeignKey(x => x.CageId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("CageCell", t =>
            {
                t.HasCheckConstraint("CK_CageCell_RowRange", "[RowIdx] BETWEEN 0 AND 8");
                t.HasCheckConstraint("CK_CageCell_ColRange", "[ColIdx] BETWEEN 0 AND 8");
            });
        });

        b.Entity<Game>(e =>
        {
            e.ToTable("Game", t =>
            {
                t.HasCheckConstraint("CK_Game_TimeSeconds", "[TimeSeconds] IS NULL OR [TimeSeconds] >= 0");
                t.HasCheckConstraint("CK_Game_HintsUsed", "[HintsUsed] >= 0");
                t.HasCheckConstraint("CK_Game_Score", "[Score] IS NULL OR [Score] >= 0");
                t.HasCheckConstraint("CK_Game_TotalPaused", "[TotalPausedSeconds] >= 0");
            });
            e.Property(x => x.StartTime).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.EndTime).HasColumnType("datetime2(0)");
            e.Property(x => x.PausedAt).HasColumnType("datetime2(0)");
            e.HasOne(x => x.User).WithMany(u => u.Games).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Puzzle).WithMany(p => p.Games).HasForeignKey(x => x.PuzzleId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.PuzzleId })
                .HasFilter("[IsCompleted] = 0")
                .IsUnique()
                .HasDatabaseName("UX_Game_ActiveOnly");
        });

        b.Entity<GameCell>(e =>
        {
            e.HasKey(x => new { x.GameId, x.RowIdx, x.ColIdx });
            e.HasOne(x => x.Game).WithMany(g => g.Cells).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("GameCell", t =>
            {
                t.HasCheckConstraint("CK_GameCell_RowRange", "[RowIdx] BETWEEN 0 AND 8");
                t.HasCheckConstraint("CK_GameCell_ColRange", "[ColIdx] BETWEEN 0 AND 8");
                t.HasCheckConstraint("CK_GameCell_Value", "[CellValue] IS NULL OR [CellValue] BETWEEN 1 AND 9");
            });
        });

        b.Entity<PencilMark>(e =>
        {
            e.HasKey(x => new { x.GameId, x.RowIdx, x.ColIdx, x.MarkValue });
            e.HasOne(x => x.Game).WithMany(g => g.PencilMarks).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("PencilMark", t =>
            {
                t.HasCheckConstraint("CK_PencilMark_RowRange", "[RowIdx] BETWEEN 0 AND 8");
                t.HasCheckConstraint("CK_PencilMark_ColRange", "[ColIdx] BETWEEN 0 AND 8");
                t.HasCheckConstraint("CK_PencilMark_Value", "[MarkValue] BETWEEN 1 AND 9");
            });
        });

        b.Entity<HintLog>(e =>
        {
            e.ToTable("HintLog", t =>
            {
                t.HasCheckConstraint("CK_HintLog_RowRange", "[RowIdx] BETWEEN 0 AND 8");
                t.HasCheckConstraint("CK_HintLog_ColRange", "[ColIdx] BETWEEN 0 AND 8");
            });
            e.Property(x => x.HintAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Game).WithMany(g => g.Hints).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
