#!/bin/bash
# =============================================================================
# Killer Sudoku — Container Entrypoint
# Startet SQL Server, wartet auf Bereitschaft, deployed Schema, startet App.
# =============================================================================
set -euo pipefail

SCHEMA_FILE="/docker-init/sudoku.sql"
SCHEMA_APPLIED_MARKER="/var/opt/mssql/.killersudoku-schema-applied"
SA_PASSWORD="${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD must be set (no default in image — pass via -e or compose .env)}"

# Construct connection string at runtime so the password never lives in the image layer.
# Caller may override via -e ConnectionStrings__Sudoku=... if needed.
if [ -z "${ConnectionStrings__Sudoku:-}" ]; then
    export ConnectionStrings__Sudoku="Server=localhost,1433;Database=sudoku;User Id=sa;Password=${SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
fi

echo "[entrypoint] Starting SQL Server (sqlservr)…"
/opt/mssql/bin/sqlservr &
SQL_PID=$!

echo "[entrypoint] Waiting for SQL Server to become ready…"
for i in $(seq 1 60); do
    if sqlcmd -S localhost,1433 -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
        echo "[entrypoint] SQL Server is up (after ${i} attempts)."
        break
    fi
    sleep 2
done

if ! sqlcmd -S localhost,1433 -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
    echo "[entrypoint] ERROR: SQL Server did not become ready within 120s." >&2
    kill -TERM "$SQL_PID" 2>/dev/null || true
    exit 1
fi

if [ ! -f "$SCHEMA_APPLIED_MARKER" ]; then
    echo "[entrypoint] Applying schema from $SCHEMA_FILE…"
    sqlcmd -S localhost,1433 -U sa -P "$SA_PASSWORD" -C -i "$SCHEMA_FILE"
    touch "$SCHEMA_APPLIED_MARKER"
    echo "[entrypoint] Schema applied."
else
    echo "[entrypoint] Schema already applied (marker exists) — skip."
fi

echo "[entrypoint] Starting ASP.NET Blazor Server on :8080 (self-contained)…"
cd /app
./KillerSudoku.Web &
APP_PID=$!

# Forward signals to both children, wait for either to exit
trap 'echo "[entrypoint] SIGTERM/SIGINT — shutting down…"; kill -TERM "$APP_PID" "$SQL_PID" 2>/dev/null || true' SIGTERM SIGINT

wait -n "$SQL_PID" "$APP_PID"
EXIT_CODE=$?

echo "[entrypoint] A child process exited (code=$EXIT_CODE) — terminating the other."
kill -TERM "$APP_PID" "$SQL_PID" 2>/dev/null || true
wait
exit "$EXIT_CODE"
