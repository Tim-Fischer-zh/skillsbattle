using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace KillerSudoku.IntegrationTests.Fixtures;

/// <summary>
/// xUnit-Fixture: startet einen MS-SQL-Container und deployed das Schema aus
/// <c>db/sudoku.sql</c> — exakt dasselbe Script, das laut README §1.3 als
/// Submission-Artefakt ausgeliefert wird und im Docker-Container via
/// <c>docker-entrypoint.sh</c> beim Start ausgeführt wird.
///
/// <para>
/// Begründung Single-Source-of-Truth: <c>sudoku.sql</c> ist Pflicht-Deliverable
/// (README §1.3) — daher pflegen wir keine parallele EF-Migration. Damit
/// gibt es keinen Drift zwischen Test- und Production-Schema, und der Test
/// verifiziert exakt den deployed Pfad.
/// </para>
/// </summary>
public sealed class MsSqlContainerFixture : IAsyncLifetime
{
    private static readonly Regex GoSplit = new(
        @"^\s*GO\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Killer$udoku2026!")
            .Build();

    /// <summary>Master-Connection (für CREATE DATABASE) — nur intern.</summary>
    private string MasterConnectionString =>
        _container.GetConnectionString() + ";TrustServerCertificate=True";

    /// <summary>Connection zur deployment-DB <c>sudoku</c>, wie auch im Docker-Container.</summary>
    public string ConnectionString
    {
        get
        {
            var b = new SqlConnectionStringBuilder(MasterConnectionString)
            {
                InitialCatalog = "sudoku",
            };
            return b.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var sqlPath = LocateSudokuSqlScript();
        var sql = await File.ReadAllTextAsync(sqlPath);

        // Stage 1: connect to master and run script. Script itself creates the
        // 'sudoku' DB and switches via USE [sudoku] before creating tables.
        await using var conn = new SqlConnection(MasterConnectionString);
        await conn.OpenAsync();

        foreach (var batch in SplitOnGo(sql))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 60 };
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private static IEnumerable<string> SplitOnGo(string script) =>
        GoSplit.Split(script);

    /// <summary>
    /// Findet <c>db/sudoku.sql</c> indem vom Test-Assembly-Pfad nach oben
    /// gegangen wird, bis ein Verzeichnis mit <c>db/sudoku.sql</c> existiert.
    /// Robust gegenüber unterschiedlichen Build-Outputs.
    /// </summary>
    private static string LocateSudokuSqlScript()
    {
        var visited = new List<string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "db", "sudoku.sql");
            visited.Add(candidate);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"sudoku.sql nicht gefunden. Geprüfte Pfade:\n  {string.Join("\n  ", visited)}");
    }
}

/// <summary>
/// xUnit-Collection-Definition: alle Tests in dieser Collection teilen sich
/// einen Container (Performance — 30 s SQL-Server-Boot wird einmal bezahlt).
/// </summary>
[CollectionDefinition(Name)]
public sealed class MsSqlCollection : ICollectionFixture<MsSqlContainerFixture>
{
    public const string Name = "MsSql";
}
