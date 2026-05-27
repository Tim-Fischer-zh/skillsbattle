/* ============================================================
   Killer Sudoku — Complete Database Schema
   Target:    Microsoft SQL Server 2022 (Express / Container)
   Database:  sudoku (deployment-DB)

   Schema-Generation:
     - Identity + Domain Tables: aus EF Core 10 Migration generiert
     - Trigger + View:           manuell am Ende ergänzt

   Deployment:
     - Production / Docker:  via docker-entrypoint.sh
     - Tests:                via MsSqlContainerFixture (gleiches Script)

   Single-Source-of-Truth gemäß README §1.3.
   ============================================================ */

IF DB_ID('sudoku') IS NULL
BEGIN
    CREATE DATABASE [sudoku];
END
GO

USE [sudoku];
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO


IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AppUser] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [UserName] nvarchar(50) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(255) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AppUser] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AppUser_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUser] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AppUser_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUser] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] int NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AppUser_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUser] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Puzzle] (
    [Id] int NOT NULL IDENTITY,
    [Difficulty] tinyint NOT NULL,
    [CreatedById] int NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Puzzle] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Puzzle_Difficulty] CHECK ([Difficulty] BETWEEN 1 AND 3),
    CONSTRAINT [FK_Puzzle_AppUser_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AppUser] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AppUser_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUser] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Cage] (
    [Id] int NOT NULL IDENTITY,
    [PuzzleId] int NOT NULL,
    [Sum] tinyint NOT NULL,
    CONSTRAINT [PK_Cage] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Cage_Sum_Range] CHECK ([Sum] BETWEEN 1 AND 45),
    CONSTRAINT [FK_Cage_Puzzle_PuzzleId] FOREIGN KEY ([PuzzleId]) REFERENCES [Puzzle] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Game] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [PuzzleId] int NOT NULL,
    [StartTime] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [EndTime] datetime2(0) NULL,
    [TimeSeconds] int NULL,
    [HintsUsed] int NOT NULL,
    [Score] int NULL,
    [IsCompleted] bit NOT NULL,
    [IsPaused] bit NOT NULL,
    [PausedAt] datetime2(0) NULL,
    [TotalPausedSeconds] int NOT NULL,
    CONSTRAINT [PK_Game] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Game_HintsUsed] CHECK ([HintsUsed] >= 0),
    CONSTRAINT [CK_Game_Score] CHECK ([Score] IS NULL OR [Score] >= 0),
    CONSTRAINT [CK_Game_TimeSeconds] CHECK ([TimeSeconds] IS NULL OR [TimeSeconds] >= 0),
    CONSTRAINT [CK_Game_TotalPaused] CHECK ([TotalPausedSeconds] >= 0),
    CONSTRAINT [FK_Game_AppUser_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUser] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Game_Puzzle_PuzzleId] FOREIGN KEY ([PuzzleId]) REFERENCES [Puzzle] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CageCell] (
    [CageId] int NOT NULL,
    [RowIdx] tinyint NOT NULL,
    [ColIdx] tinyint NOT NULL,
    CONSTRAINT [PK_CageCell] PRIMARY KEY ([CageId], [RowIdx], [ColIdx]),
    CONSTRAINT [CK_CageCell_ColRange] CHECK ([ColIdx] BETWEEN 0 AND 8),
    CONSTRAINT [CK_CageCell_RowRange] CHECK ([RowIdx] BETWEEN 0 AND 8),
    CONSTRAINT [FK_CageCell_Cage_CageId] FOREIGN KEY ([CageId]) REFERENCES [Cage] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [GameCell] (
    [GameId] int NOT NULL,
    [RowIdx] tinyint NOT NULL,
    [ColIdx] tinyint NOT NULL,
    [CellValue] tinyint NULL,
    CONSTRAINT [PK_GameCell] PRIMARY KEY ([GameId], [RowIdx], [ColIdx]),
    CONSTRAINT [CK_GameCell_ColRange] CHECK ([ColIdx] BETWEEN 0 AND 8),
    CONSTRAINT [CK_GameCell_RowRange] CHECK ([RowIdx] BETWEEN 0 AND 8),
    CONSTRAINT [CK_GameCell_Value] CHECK ([CellValue] IS NULL OR [CellValue] BETWEEN 1 AND 9),
    CONSTRAINT [FK_GameCell_Game_GameId] FOREIGN KEY ([GameId]) REFERENCES [Game] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [HintLog] (
    [Id] int NOT NULL IDENTITY,
    [GameId] int NOT NULL,
    [RowIdx] tinyint NOT NULL,
    [ColIdx] tinyint NOT NULL,
    [HintAt] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_HintLog] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_HintLog_ColRange] CHECK ([ColIdx] BETWEEN 0 AND 8),
    CONSTRAINT [CK_HintLog_RowRange] CHECK ([RowIdx] BETWEEN 0 AND 8),
    CONSTRAINT [FK_HintLog_Game_GameId] FOREIGN KEY ([GameId]) REFERENCES [Game] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PencilMark] (
    [GameId] int NOT NULL,
    [RowIdx] tinyint NOT NULL,
    [ColIdx] tinyint NOT NULL,
    [MarkValue] tinyint NOT NULL,
    CONSTRAINT [PK_PencilMark] PRIMARY KEY ([GameId], [RowIdx], [ColIdx], [MarkValue]),
    CONSTRAINT [CK_PencilMark_ColRange] CHECK ([ColIdx] BETWEEN 0 AND 8),
    CONSTRAINT [CK_PencilMark_RowRange] CHECK ([RowIdx] BETWEEN 0 AND 8),
    CONSTRAINT [CK_PencilMark_Value] CHECK ([MarkValue] BETWEEN 1 AND 9),
    CONSTRAINT [FK_PencilMark_Game_GameId] FOREIGN KEY ([GameId]) REFERENCES [Game] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [EmailIndex] ON [AppUser] ([NormalizedEmail]);

CREATE UNIQUE INDEX [IX_AppUser_Email] ON [AppUser] ([Email]) WHERE [Email] IS NOT NULL;

CREATE UNIQUE INDEX [IX_AppUser_UserName] ON [AppUser] ([UserName]) WHERE [UserName] IS NOT NULL;

CREATE UNIQUE INDEX [UserNameIndex] ON [AppUser] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [IX_Cage_PuzzleId] ON [Cage] ([PuzzleId]);

CREATE INDEX [IX_Game_PuzzleId] ON [Game] ([PuzzleId]);

CREATE UNIQUE INDEX [UX_Game_ActiveOnly] ON [Game] ([UserId], [PuzzleId]) WHERE [IsCompleted] = 0;

CREATE INDEX [IX_HintLog_GameId] ON [HintLog] ([GameId]);

CREATE INDEX [IX_Puzzle_CreatedById] ON [Puzzle] ([CreatedById]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260527110849_Initial', N'10.0.8');

COMMIT;
GO


-- ============================================================
-- Trigger + View (manuell ergänzt, nicht aus EF-Migration generiert)
-- ============================================================
GO

CREATE OR ALTER TRIGGER trg_CageCell_UniquePerPuzzle
ON [CageCell]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM [CageCell] cc
        JOIN [Cage] c1 ON c1.[Id] = cc.[CageId]
        JOIN inserted i ON i.[RowIdx] = cc.[RowIdx] AND i.[ColIdx] = cc.[ColIdx]
        JOIN [Cage] c2 ON c2.[Id] = i.[CageId]
        WHERE c1.[PuzzleId] = c2.[PuzzleId]
          AND cc.[CageId] <> i.[CageId]
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'CageCell: Cell already assigned to another cage in the same puzzle.', 1;
    END
END;
GO

CREATE OR ALTER VIEW vw_Highscore
AS
    SELECT
        g.[Id]          AS [GameId],
        g.[UserId],
        u.[UserName]    AS [Username],
        g.[PuzzleId],
        p.[Difficulty],
        g.[TimeSeconds],
        g.[HintsUsed],
        g.[Score],
        g.[EndTime]     AS [CompletedAt]
    FROM [Game] g
    INNER JOIN [AppUser] u ON u.[Id] = g.[UserId]
    INNER JOIN [Puzzle]  p ON p.[Id] = g.[PuzzleId]
    WHERE g.[IsCompleted] = 1
      AND g.[Score] IS NOT NULL;
GO
