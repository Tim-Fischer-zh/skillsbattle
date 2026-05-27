#!/bin/bash
# DB-Container entrypoint: starts sqlservr and deploys sudoku.sql once.
set -euo pipefail

MARKER=/var/opt/mssql/.killersudoku-schema-applied
SQL_FILE=/docker-init/sudoku.sql

/opt/mssql/bin/sqlservr &
SQL_PID=$!

echo "[db-init] Waiting for SQL Server to accept connections…"
for i in $(seq 1 60); do
    if /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
        echo "[db-init] SQL Server ready (after $i attempts)."
        break
    fi
    sleep 2
done

if [ ! -f "$MARKER" ]; then
    echo "[db-init] Applying schema from $SQL_FILE…"
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -I -i "$SQL_FILE"
    touch "$MARKER"
    echo "[db-init] Schema applied."
else
    echo "[db-init] Schema already applied — skipped."
fi

# Hold the foreground process
wait "$SQL_PID"
