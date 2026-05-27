/* ============================================================
   Killer Sudoku — Database Schema
   Target:    Microsoft SQL Server Express
   Database:  sudoku
   Author:    Tim Fischer
   Version:   1.0
   ============================================================ */

-- Database erstellen (idempotent)
IF DB_ID('sudoku') IS NULL
BEGIN
    CREATE DATABASE sudoku;
END
GO

USE sudoku;
GO

/* ------------------------------------------------------------
   Bestehende Objekte droppen (Reverse-FK-Reihenfolge)
   ------------------------------------------------------------ */
IF OBJECT_ID('vw_Highscore', 'V')   IS NOT NULL DROP VIEW   vw_Highscore;
IF OBJECT_ID('HintLog',      'U')   IS NOT NULL DROP TABLE  HintLog;
IF OBJECT_ID('PencilMark',   'U')   IS NOT NULL DROP TABLE  PencilMark;
IF OBJECT_ID('GameCell',     'U')   IS NOT NULL DROP TABLE  GameCell;
IF OBJECT_ID('Game',         'U')   IS NOT NULL DROP TABLE  Game;
IF OBJECT_ID('CageCell',     'U')   IS NOT NULL DROP TABLE  CageCell;
IF OBJECT_ID('Cage',         'U')   IS NOT NULL DROP TABLE  Cage;
IF OBJECT_ID('Puzzle',       'U')   IS NOT NULL DROP TABLE  Puzzle;
IF OBJECT_ID('AppUser',      'U')   IS NOT NULL DROP TABLE  AppUser;
GO

/* ------------------------------------------------------------
   AppUser — Benutzer
   ------------------------------------------------------------ */
CREATE TABLE AppUser (
    Id            INT            IDENTITY(1,1) NOT NULL,
    Username      NVARCHAR(50)   NOT NULL,
    Email         NVARCHAR(255)  NOT NULL,
    PasswordHash  NVARCHAR(500)  NOT NULL,
    CreatedAt     DATETIME2(0)   NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_AppUser        PRIMARY KEY (Id),
    CONSTRAINT UQ_AppUser_Username UNIQUE (Username),
    CONSTRAINT UQ_AppUser_Email    UNIQUE (Email),
    CONSTRAINT CK_AppUser_Username_NotEmpty CHECK (LEN(LTRIM(RTRIM(Username))) > 0),
    CONSTRAINT CK_AppUser_Email_Format      CHECK (Email LIKE '%_@_%.__%')
);
GO

/* ------------------------------------------------------------
   Puzzle — Killer-Sudoku-Definition
   WICHTIG: KEINE Solution-Spalte (UC04: "No solution is recorded")
   ------------------------------------------------------------ */
CREATE TABLE Puzzle (
    Id           INT            IDENTITY(1,1) NOT NULL,
    Difficulty   TINYINT        NOT NULL,
    CreatedById  INT            NOT NULL,
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_Puzzle_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Puzzle              PRIMARY KEY (Id),
    CONSTRAINT FK_Puzzle_CreatedBy    FOREIGN KEY (CreatedById) REFERENCES AppUser(Id),
    CONSTRAINT CK_Puzzle_Difficulty   CHECK (Difficulty BETWEEN 1 AND 3)
);
GO

/* ------------------------------------------------------------
   Cage — Gruppe von Zellen mit Soll-Summe
   ------------------------------------------------------------ */
CREATE TABLE Cage (
    Id        INT       IDENTITY(1,1) NOT NULL,
    PuzzleId  INT       NOT NULL,
    [Sum]     TINYINT   NOT NULL,

    CONSTRAINT PK_Cage           PRIMARY KEY (Id),
    CONSTRAINT FK_Cage_Puzzle    FOREIGN KEY (PuzzleId) REFERENCES Puzzle(Id) ON DELETE CASCADE,
    CONSTRAINT CK_Cage_Sum_Range CHECK ([Sum] BETWEEN 1 AND 45)
);
CREATE INDEX IX_Cage_PuzzleId ON Cage(PuzzleId);
GO

/* ------------------------------------------------------------
   CageCell — Zelle die zu einem Cage gehört
   Constraint "jede Zelle pro Puzzle in genau einem Cage" wird über
   einen AFTER-INSERT/UPDATE-Trigger durchgesetzt (siehe unten,
   ROLLBACK bei Verletzung — semantisch äquivalent zu INSTEAD-OF
   ohne die Komplexität, eine Replay-Logik zu schreiben).
   ------------------------------------------------------------ */
CREATE TABLE CageCell (
    CageId   INT      NOT NULL,
    RowIdx   TINYINT  NOT NULL,
    ColIdx   TINYINT  NOT NULL,

    CONSTRAINT PK_CageCell          PRIMARY KEY (CageId, RowIdx, ColIdx),
    CONSTRAINT FK_CageCell_Cage     FOREIGN KEY (CageId) REFERENCES Cage(Id) ON DELETE CASCADE,
    CONSTRAINT CK_CageCell_RowRange CHECK (RowIdx BETWEEN 0 AND 8),
    CONSTRAINT CK_CageCell_ColRange CHECK (ColIdx BETWEEN 0 AND 8)
);
GO

/* Trigger: Eine (RowIdx, ColIdx) darf pro Puzzle nur in einem Cage liegen */
CREATE OR ALTER TRIGGER trg_CageCell_UniquePerPuzzle
ON CageCell
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM CageCell cc
        JOIN Cage c1 ON c1.Id = cc.CageId
        JOIN inserted i ON i.RowIdx = cc.RowIdx AND i.ColIdx = cc.ColIdx
        JOIN Cage c2 ON c2.Id = i.CageId
        WHERE c1.PuzzleId = c2.PuzzleId
          AND cc.CageId <> i.CageId
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'CageCell: Cell already assigned to another cage in the same puzzle.', 1;
    END
END;
GO

/* ------------------------------------------------------------
   Game — Spiel-Session
   Pause-Mechanik: TimeSeconds wird beim Game-Ende berechnet:
     TimeSeconds = DATEDIFF(SECOND, StartTime, EndTime) - TotalPausedSeconds
   ------------------------------------------------------------ */
CREATE TABLE Game (
    Id                  INT           IDENTITY(1,1) NOT NULL,
    UserId              INT           NOT NULL,
    PuzzleId            INT           NOT NULL,
    StartTime           DATETIME2(0)  NOT NULL CONSTRAINT DF_Game_StartTime DEFAULT SYSUTCDATETIME(),
    EndTime             DATETIME2(0)  NULL,
    TimeSeconds         INT           NULL,
    HintsUsed           INT           NOT NULL CONSTRAINT DF_Game_Hints DEFAULT 0,
    Score               INT           NULL,
    IsCompleted         BIT           NOT NULL CONSTRAINT DF_Game_Completed DEFAULT 0,
    IsPaused            BIT           NOT NULL CONSTRAINT DF_Game_Paused DEFAULT 0,
    PausedAt            DATETIME2(0)  NULL,
    TotalPausedSeconds  INT           NOT NULL CONSTRAINT DF_Game_TotalPaused DEFAULT 0,

    CONSTRAINT PK_Game              PRIMARY KEY (Id),
    CONSTRAINT FK_Game_User         FOREIGN KEY (UserId)   REFERENCES AppUser(Id),
    CONSTRAINT FK_Game_Puzzle       FOREIGN KEY (PuzzleId) REFERENCES Puzzle(Id),
    CONSTRAINT CK_Game_TimeSeconds  CHECK (TimeSeconds IS NULL OR TimeSeconds >= 0),
    CONSTRAINT CK_Game_HintsUsed    CHECK (HintsUsed   >= 0),
    CONSTRAINT CK_Game_Score        CHECK (Score IS NULL OR Score >= 0),
    CONSTRAINT CK_Game_TotalPaused  CHECK (TotalPausedSeconds >= 0)
);
CREATE INDEX IX_Game_UserId    ON Game(UserId);
CREATE INDEX IX_Game_PuzzleId  ON Game(PuzzleId);
GO

/* Filtered Unique Index: max. 1 aktives Game pro User-Puzzle-Kombi (UC13 AC13.3) */
CREATE UNIQUE INDEX UX_Game_ActiveOnly
    ON Game (UserId, PuzzleId)
    WHERE IsCompleted = 0;
GO

/* ------------------------------------------------------------
   GameCell — Aktueller Spielzustand pro Zelle
   ------------------------------------------------------------ */
CREATE TABLE GameCell (
    GameId     INT      NOT NULL,
    RowIdx     TINYINT  NOT NULL,
    ColIdx     TINYINT  NOT NULL,
    CellValue  TINYINT  NULL,

    CONSTRAINT PK_GameCell          PRIMARY KEY (GameId, RowIdx, ColIdx),
    CONSTRAINT FK_GameCell_Game     FOREIGN KEY (GameId) REFERENCES Game(Id) ON DELETE CASCADE,
    CONSTRAINT CK_GameCell_RowRange CHECK (RowIdx BETWEEN 0 AND 8),
    CONSTRAINT CK_GameCell_ColRange CHECK (ColIdx BETWEEN 0 AND 8),
    CONSTRAINT CK_GameCell_Value    CHECK (CellValue IS NULL OR CellValue BETWEEN 1 AND 9)
);
GO

/* ------------------------------------------------------------
   PencilMark — Kandidaten-Annotationen pro Zelle (UC14)
   ------------------------------------------------------------ */
CREATE TABLE PencilMark (
    GameId     INT      NOT NULL,
    RowIdx     TINYINT  NOT NULL,
    ColIdx     TINYINT  NOT NULL,
    MarkValue  TINYINT  NOT NULL,

    CONSTRAINT PK_PencilMark         PRIMARY KEY (GameId, RowIdx, ColIdx, MarkValue),
    CONSTRAINT FK_PencilMark_Game    FOREIGN KEY (GameId) REFERENCES Game(Id) ON DELETE CASCADE,
    CONSTRAINT CK_PencilMark_RowRange CHECK (RowIdx BETWEEN 0 AND 8),
    CONSTRAINT CK_PencilMark_ColRange CHECK (ColIdx BETWEEN 0 AND 8),
    CONSTRAINT CK_PencilMark_Value    CHECK (MarkValue BETWEEN 1 AND 9)
);
GO

/* ------------------------------------------------------------
   HintLog — Audit-Log für Hint-Verwendung (UC07)
   ------------------------------------------------------------ */
CREATE TABLE HintLog (
    Id       INT           IDENTITY(1,1) NOT NULL,
    GameId   INT           NOT NULL,
    RowIdx   TINYINT       NOT NULL,
    ColIdx   TINYINT       NOT NULL,
    HintAt   DATETIME2(0)  NOT NULL CONSTRAINT DF_HintLog_HintAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_HintLog          PRIMARY KEY (Id),
    CONSTRAINT FK_HintLog_Game     FOREIGN KEY (GameId) REFERENCES Game(Id) ON DELETE CASCADE,
    CONSTRAINT CK_HintLog_RowRange CHECK (RowIdx BETWEEN 0 AND 8),
    CONSTRAINT CK_HintLog_ColRange CHECK (ColIdx BETWEEN 0 AND 8)
);
CREATE INDEX IX_HintLog_GameId ON HintLog(GameId);
GO

/* ------------------------------------------------------------
   vw_Highscore — View für UC08 (statt denormalisierter Tabelle)
   ------------------------------------------------------------ */
CREATE OR ALTER VIEW vw_Highscore
AS
    SELECT
        g.Id          AS GameId,
        g.UserId,
        u.Username,
        g.PuzzleId,
        p.Difficulty,
        g.TimeSeconds,
        g.HintsUsed,
        g.Score,
        g.EndTime     AS CompletedAt
    FROM Game g
    INNER JOIN AppUser u ON u.Id = g.UserId
    INNER JOIN Puzzle  p ON p.Id = g.PuzzleId
    WHERE g.IsCompleted = 1
      AND g.Score IS NOT NULL;
GO


/* ============================================================
   ENDE
   ============================================================ */
