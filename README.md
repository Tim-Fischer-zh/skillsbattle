# Killer Sudoku — Skills Battle 2026

Web-App für klassisches Sudoku mit Cage-Summen-Erweiterung. .NET 10 · Blazor Server · MS-SQL Server.

| | |
|---|---|
| Live-Demo | [https://web17skill.com](https://web17skill.com) |
| Image | `ghcr.io/tim-fischer-zh/killer-sudoku:latest` |
| Spec | [`skillsbattle2026_1.1.md`](skillsbattle2026_1.1.md) |
| Submission-Paket | [`AppDev_Fischer_Tim/`](AppDev_Fischer_Tim/) |

---

## Schnellstart

Wähle einen Weg:

### A — Live-Demo aufrufen (kein Setup)

Browser öffnen:

```
https://web17skill.com
```

Login-Required-Seiten benötigen einen Account → "Account erstellen" auf der Startseite.

### B — Lokal via Docker (empfohlen für Prüfer)

Voraussetzung: Docker Desktop (oder beliebige Docker-Engine).

```bash
# 1. Compose-File aus dem Repo holen oder dieses Repo clonen
git clone https://github.com/Tim-Fischer-zh/skillsbattle.git
cd skillsbattle

# 2. SA-Passwort wählen + starten
MSSQL_SA_PASSWORD='Sudoku!Strong#Pass#2026' docker compose up -d

# 3. Browser
open http://localhost:8080
```

Das `docker compose` Setup zieht zwei Images:

- `mcr.microsoft.com/mssql/server:2022-latest` für die DB
- `ghcr.io/tim-fischer-zh/killer-sudoku:latest` für die App

Datenbank-Schema wird beim ersten Start automatisch via `docker-entrypoint.sh` aus `db/sudoku.sql` angelegt. Persistenz erfolgt im Named-Volume `mssql-data` — Container-Restart erhält den Spielstand.

### C — Pull-Only ohne Compose

```bash
docker pull ghcr.io/tim-fischer-zh/killer-sudoku:latest

# Separater MS-SQL-Container
docker run -d --name killersudoku-db \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='Sudoku!Strong#Pass#2026' \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# App-Container (verbindet auf den DB-Container)
docker run -d --name killersudoku-app \
  -p 8080:8080 \
  -e 'ConnectionStrings__Sudoku=Server=host.docker.internal,1433;Database=sudoku;User Id=sa;Password=Sudoku!Strong#Pass#2026;Encrypt=True;TrustServerCertificate=True' \
  ghcr.io/tim-fischer-zh/killer-sudoku:latest
```

→ `http://localhost:8080`

### D — Native (ohne Docker, für Entwicklung)

Voraussetzung: .NET 10 SDK + lokaler MS-SQL-Server (Express oder Developer-Edition).

```bash
# DB anlegen
sqlcmd -S "(local)\SQLEXPRESS" -E -i db/sudoku.sql

# App bauen + starten
cd source
dotnet run --project src/KillerSudoku.Web
```

→ `https://localhost:5001`

Connection-String aus `source/src/KillerSudoku.Web/appsettings.Development.json` anpassen falls SQL-Server-Instanz anders heißt.

---

## Tests

```bash
cd source
dotnet test -c Release
```

128 Tests in 4 Suites:

| Suite | Tests | Dauer |
|-------|-------|-------|
| `KillerSudoku.UnitTests` | 39 | ~30 ms |
| `KillerSudoku.ComponentTests` (bUnit) | 11 | ~200 ms |
| `KillerSudoku.IntegrationTests` (Testcontainers MS-SQL) | 54 | ~45 s |
| `KillerSudoku.E2ETests` (Playwright + Container) | 24 | ~10 s |

Test-Protokoll: [`docs/test-protocol.md`](docs/test-protocol.md) (89 funktionale + 5 manuelle Tests, alle 89 als Code implementiert).

---

## Repo-Struktur

```
skillsbattle/
├── README.md                       # hier
├── skillsbattle2026_1.1.md         # Original-Aufgabenstellung
├── Dockerfile                      # Multi-Stage-Build (Web)
├── docker-compose.yml              # lokales Setup (Web + DB)
├── docker-compose.prod.yml         # Production-Setup (web17skill.com)
├── docker-entrypoint.sh            # Schema-Auto-Apply + dotnet start
├── build-submission.sh             # Submission-Paket erzeugen
│
├── source/                         # .NET 10 Solution
│   ├── src/
│   │   ├── KillerSudoku.Core/      # Domain (Solver, Validator, Generator)
│   │   ├── KillerSudoku.Data/      # EF Core + Application-Services
│   │   └── KillerSudoku.Web/       # Blazor Server (Pages, Layout)
│   └── tests/
│       ├── KillerSudoku.UnitTests/
│       ├── KillerSudoku.ComponentTests/
│       ├── KillerSudoku.IntegrationTests/
│       └── KillerSudoku.E2ETests/
│
├── db/
│   └── sudoku.sql                  # DDL inkl. PK/FK/Trigger/View
│
├── docs/                           # ARC42-Doku + Spec-Dokumente
│   ├── arc42/                      # 12 Kapitel
│   ├── arc42_pdf/                  # PDF-Source + Build
│   ├── doc_pdf/                    # Submission-Doku PDF-Source
│   ├── erm.md                      # ER-Modell
│   ├── use-cases.md                # UC01–UC14
│   ├── validation.md               # V01–V16
│   ├── mockups/                    # Screen-PNGs
│   └── test-protocol.{md,csv,xlsx} # Test-Plan (94 Tests)
│
├── AppDev_Fischer_Tim/             # Submission-Paket (in Zip)
│   ├── Documentation.pdf           # 6 Pflicht-Sektionen
│   ├── Architecture-ARC42.pdf      # Architektur-Doku
│   ├── sudoku.sql
│   └── test-protocol.xlsx
└── AppDev_Fischer_Tim.zip          # finale Submission
```

---

## Features

| UC | Feature | Status |
|----|---------|:---:|
| UC01 | Spielregeln + Beispiel auf Startseite | ✓ |
| UC02 | Account erstellen (Username + Email + Passwort) | ✓ |
| UC03 | Login mit Rate-Limit (5 Versuche / 5 min) | ✓ |
| UC04 | Puzzle eingeben (Cage-Editor mit 10 Farben, kollisionsfrei) | ✓ |
| UC05 | Speichern nur wenn eindeutig lösbar (Solver-Verifikation) | ✓ |
| UC06 | Puzzle spielen (Klick-Eingabe, Live-Timer) | ✓ |
| UC07 | Tipp anfordern (3 Strategien: Naked Single, Cage Forced, Solver Fallback) | ✓ |
| UC08 | Highscore-Liste (Score-Formel: `max(0, 10000 - time - hints×300)`) | ✓ |
| UC09 | Lösung prüfen (Sum-405-Fast-Fail, dann Row/Col/Nonet/Cage) | ✓ |
| UC10 | Score + Zeit speichern bei Completion | ✓ |
| UC11 | Auto-Solver (Backtracking + MRV + Constraint-Propagation) | ✓ |
| UC12 | Puzzles browsen + Difficulty-Filter (URL deep-linkbar) | ✓ |
| UC13 | Pause/Resume (Spielzeit exkludiert Pausen) | ✓ |
| UC14 | Pencil-Marks (Kandidaten-Annotationen) | ✓ |

Zusatz-Features:
- **Zufalls-Puzzle-Generator** im Editor (3 Difficulties, Solver-verifizierte Eindeutigkeit)
- **10-Farben-Greedy-Coloring** für Cages (keine zwei Nachbar-Cages teilen Farbe)
- **Production-Deploy** via Self-Hosted GitHub-Runner + Cloudflare-Tunnel

---

## Dokumentation

- **Projekt-Doku PDF**: [`AppDev_Fischer_Tim/Documentation.pdf`](AppDev_Fischer_Tim/Documentation.pdf) — 6 Pflicht-Sektionen (Mockups, ERM, Class Diagram, Zusatz-UCs, Validation, Test Protocol)
- **Architektur-Doku PDF**: [`AppDev_Fischer_Tim/Architecture-ARC42.pdf`](AppDev_Fischer_Tim/Architecture-ARC42.pdf) — 12 arc42-Kapitel
- **Live-Architektur-Dokumente**: [`docs/arc42/`](docs/arc42/)

PDF-Neuerzeugung:

```bash
bash build-submission.sh
```

---

## Deployment-Pipeline

Push auf `main` → GitHub-Actions-Workflow `.github/workflows/deploy.yml`:

1. **Build** auf self-hosted Runner (VPS, ist gleichzeitig Docker-Host)
2. **Push** nach `ghcr.io/tim-fischer-zh/killer-sudoku:latest` + `:<short-sha>`
3. **Deploy** via `docker compose -f docker-compose.prod.yml up -d`
4. **Smoke-Test** gegen `https://web17skill.com`

Cloudflare-Tunnel terminiert TLS und forwarded HTTP zu `127.0.0.1:80` auf der VPS (keine eingehenden Ports offen).

---

## License

Skills Battle 2026 — interne Submission, kein Open-Source-Release.
