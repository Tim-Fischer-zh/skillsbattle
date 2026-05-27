# 3 Kontextabgrenzung

> arc42 v8.2 · Killer Sudoku App
> Quelle (autoritativ): [`skillsbattle2026_1.1.md`](../../skillsbattle2026_1.1.md)

Die Kontextabgrenzung beschreibt die Systemgrenze: **welche Akteure** mit der Anwendung interagieren, **welche Nachbarsysteme** angesprochen werden, und über **welche Schnittstellen** die Kommunikation läuft. Sie unterscheidet fachlich (welche Akteure / welche Use Cases) und technisch (welche Protokolle / Komponenten).

---

## §3.1 Fachlicher Kontext

### System-Grenze

Die Killer-Sudoku-App ist ein **lokal betriebenes Single-User-System** mit einer Web-UI. Es existieren **keine externen Fachsysteme** (kein Drittanbieter-Login, keine externen APIs, keine Bezahl-/Mailservices).

```mermaid
graph LR
    User((Benutzer<br/>Visitor / User))
    App[Killer-Sudoku-App<br/>Blazor Server]
    DB[(MS-SQL<br/>'sudoku')]

    User -- "Web-UI<br/>(HTTP/SignalR, deutsch)" --> App
    App -- "ADO.NET / EF Core<br/>(T-SQL)" --> DB

    classDef external fill:#e0f2fe,stroke:#0284c7
    classDef system fill:#fef3c7,stroke:#d97706
    classDef store fill:#dcfce7,stroke:#16a34a
    class User external
    class App system
    class DB store
```

### Akteure

| Akteur | Beschreibung | Berührungspunkte (UC) |
|--------|--------------|------------------------|
| **Visitor** | Nicht angemeldeter Browser-Nutzer. Darf nur die öffentlichen Seiten sehen. | [UC01](../use-cases.md#uc01--read-rules) (Rules), [UC02](../use-cases.md#uc02--create-user-register) (Register), [UC03](../use-cases.md#uc03--login) (Login) |
| **User** | Angemeldeter Spieler. Darf Puzzles erstellen, spielen, Highscore sehen. | [UC04](../use-cases.md#uc04--enter-puzzle)–[UC10](../use-cases.md#uc10--save-result), [UC12](../use-cases.md#uc12--browse--filter-puzzles)–[UC14](../use-cases.md#uc14--pencil-marks-candidate-notes) |
| **System** (intern, kein externer Akteur) | Triggert automatische Folge-UCs (z.B. UC10 nach erfolgreichem UC09). | [UC10](../use-cases.md#uc10--save-result), [UC11](../use-cases.md#uc11--auto-solve) (intern aufgerufen von UC05/UC07/UC09) |

### Keine externen Akteure / Nachbar-Systeme

Folgendes ist **bewusst nicht** Teil des Kontexts (siehe [Kapitel 2 §2.3 Anti-Halluzinations-Konvention](./02-constraints.md#anti-halluzinations-konvention)):

- Kein Identity-Provider (Google/Microsoft/Apple-SSO)
- Kein Email-Service (kein Email-Verifikations- oder Password-Reset-Flow im Scope)
- Keine externen Highscore-/Leaderboard-APIs
- Kein Cloud-Backend — Anwendung läuft lokal mit lokaler DB

> README §1.3 (wörtlich): "create a database named 'sudoku' on MySQL or MS-SQL server **running on your machine**." → klare Lokal-Vorgabe.

### Eingaben / Ausgaben über die System-Grenze

| Richtung | Daten | Kanal |
|----------|-------|-------|
| User → App | Login-Credentials, Puzzle-Definition (Cages + Summen), Zelleneingaben (1–9), Hint-/Check-/Pause-Aktionen | HTTP-POST (initial) + SignalR-WebSocket (Blazor-Server-Circuit) |
| App → User | Gerendertes 9×9-Grid, Cage-Borders + Summen, Timer, Hint-Ergebnis, Lösungs-Validierungs-Status, Highscore-Liste, Fehlermeldungen (deutsch) | HTML/CSS (initial) + SignalR-Patches (UI-Updates) |
| App → DB | INSERT/UPDATE/SELECT auf `AppUser`, `Puzzle`, `Cage`, `CageCell`, `Game`, `GameCell`, `PencilMark`, `HintLog` (siehe [`erm.md`](../erm.md)) | EF Core über ADO.NET / TDS-Protokoll |
| DB → App | Abfrage-Resultate, generierte Primary Keys, View-Daten (`vw_Highscore`) | TDS |

---

## §3.2 Technischer Kontext

### Komponenten & Protokolle

```mermaid
flowchart LR
    subgraph Client["Browser (Chrome/Firefox)"]
        UI[Razor-Components<br/>im DOM]
        WS[SignalR-Client<br/>WebSocket]
    end

    subgraph Server["Blazor Server (.NET 10, Kestrel)"]
        SR[SignalR-Hub<br/>Blazor-Circuit]
        Services[Service-Layer<br/>IAuth/IPuzzle/IGame/<br/>ISolver/IHint/IHighscore]
        Solver[Solver<br/>Backtracking +<br/>Constraint-Propagation]
        DBCtx[EF Core 10<br/>SudokuDbContext]
    end

    subgraph DataLayer["Daten"]
        MSSQL[(MS-SQL Express<br/>DB: 'sudoku')]
        FS[Filesystem<br/>docs/mockups/*.png<br/>nur Build-Zeit]
    end

    UI <-- "HTML/CSS<br/>(initial render)" --> SR
    WS <-- "SignalR<br/>(WSS, persistent)" --> SR
    SR --> Services
    Services --> Solver
    Services --> DBCtx
    DBCtx <-- "TDS<br/>(localhost:1433)" --> MSSQL
    Server -. "Build/PDF only" .-> FS

    classDef ext fill:#e0f2fe,stroke:#0284c7
    classDef int fill:#fef3c7,stroke:#d97706
    classDef db fill:#dcfce7,stroke:#16a34a
    class Client ext
    class Server int
    class MSSQL db
```

### Schnittstellen-Details

| Schnittstelle | Protokoll | Format | Notiz |
|---------------|-----------|--------|-------|
| Browser ↔ Server (Initial-Page-Load) | HTTPS | HTML | Erste Request liefert Razor-gerendertes HTML inkl. Blazor-Bootstrapper. |
| Browser ↔ Server (UI-Interaktion) | SignalR über WebSocket (WSS) | binär (MessagePack) | Persistente Verbindung pro Blazor-Circuit. UI-State serverseitig. |
| Server ↔ DB | TDS via `Microsoft.Data.SqlClient` | T-SQL | Local-Loopback (`localhost:1433` oder Named Pipe). |
| Server ↔ Filesystem | nur Build-Zeit | PNG (Mockups), PDF (Doku) | Keine Runtime-File-IO ausser Logs. |

### Blazor-Server-Circuit (zentrales technisches Konzept)

- **State serverseitig:** Game-State, aktuelle Zell-Auswahl, Pencil-Marks-Status leben in C#-Components auf dem Server.
- **UI-Updates push-basiert:** Server pusht Diff-Patches an den Client über SignalR.
- **Skalierung:** Eine Circuit pro Browser-Tab. Bei Disconnect → State kann via Reconnect wiederhergestellt werden (Standard-Blazor-Mechanismus; siehe auch [UC13](../use-cases.md#uc13--pause--resume-game) für explizite Pause/Resume-Semantik).

### Lokales Setup (Single-Machine-Deployment)

Aus README §1.3 ("running on your machine") folgt: Die App und die DB laufen **auf dem gleichen Host**. Es gibt keinen Multi-Tier-Deployment-Plan. Details siehe spätere Kapitel:

- [Kapitel 5 — Bausteinsicht](./05-building-blocks.md) (TBD): innere Struktur Service-Layer
- [Kapitel 7 — Verteilungssicht](./07-deployment.md) (TBD): lokales Deployment-Diagramm

---

## §3.3 Use-Case-Übersicht

Vollständige UC-Spezifikation: siehe [`../use-cases.md`](../use-cases.md). Diese Sektion liefert nur die Aktor-Sicht und das Diagramm als Kontext-Anker.

### Use-Case-Diagramm

```mermaid
graph TB
    subgraph Actors[" "]
        Visitor((Visitor))
        User((User<br/>angemeldet))
        System((System<br/>intern))
    end

    subgraph UCs["Killer-Sudoku-App"]
        UC01[UC01: Read Rules]
        UC02[UC02: Create User]
        UC03[UC03: Login]
        UC04[UC04: Enter Puzzle]
        UC05[UC05: Save New Puzzle]
        UC06[UC06: Solve Puzzle]
        UC07[UC07: Ask for Hint]
        UC08[UC08: Show High Score]
        UC09[UC09: Check Solution]
        UC10[UC10: Save Result]
        UC11[UC11: Auto Solve]
        UC12[UC12: Browse / Filter]
        UC13[UC13: Pause / Resume]
        UC14[UC14: Pencil Marks]
    end

    Visitor --> UC01
    Visitor --> UC02
    Visitor --> UC03

    User --> UC01
    User --> UC04
    User --> UC06
    User --> UC07
    User --> UC08
    User --> UC09
    User --> UC12
    User --> UC13
    User --> UC14

    UC04 -. "includes" .-> UC05
    UC05 -. "uses" .-> UC11
    UC07 -. "uses" .-> UC11
    UC09 -. "uses" .-> UC11
    UC09 -. "triggers (on success)" .-> UC10

    System --> UC10
    System --> UC11

    classDef readme fill:#fef9c3,stroke:#ca8a04
    classDef chosen fill:#dbeafe,stroke:#2563eb
    class UC01,UC02,UC03,UC04,UC05,UC06,UC07,UC08,UC09,UC10,UC11 readme
    class UC12,UC13,UC14 chosen
```

**Legende:** Gelb = README §2.1 (UC01–UC11) · Blau = selbst gewählte Zusatz-UCs §2.3 (UC12–UC14).

### Kurz-Mapping (Detail in [`use-cases.md`](../use-cases.md))

| UC | Kurzbeschreibung | Akteur | Auth |
|----|------------------|--------|------|
| [UC01](../use-cases.md#uc01--read-rules) | Regeln + Beispiel auf Startseite | Visitor / User | public |
| [UC02](../use-cases.md#uc02--create-user-register) | Account erstellen | Visitor | public |
| [UC03](../use-cases.md#uc03--login) | Anmelden | Visitor | public |
| [UC04](../use-cases.md#uc04--enter-puzzle) | Puzzle manuell anlegen (Cages + Summen, Difficulty 1–3) | User | required |
| [UC05](../use-cases.md#uc05--save-new-puzzle-solvability-check) | Puzzle speichern — nur wenn solvable + eindeutig | User | required |
| [UC06](../use-cases.md#uc06--solve-puzzle) | Puzzle spielen | User | required |
| [UC07](../use-cases.md#uc07--ask-for-a-hint) | Hint anfordern | User | required |
| [UC08](../use-cases.md#uc08--show-high-score) | Highscore-Liste | User | required |
| [UC09](../use-cases.md#uc09--check-solution) | Lösung prüfen (Sum-Check 405 → Vollvalidierung) | User | required |
| [UC10](../use-cases.md#uc10--save-result) | Game-Resultat speichern (Time, Hints, Score) | System | required |
| [UC11](../use-cases.md#uc11--auto-solve) | Solver (intern für UC05/UC07/UC09) | System | n/a |
| [UC12](../use-cases.md#uc12--browse--filter-puzzles) | Puzzle-Liste mit Filter/Sort | User | required |
| [UC13](../use-cases.md#uc13--pause--resume-game) | Spiel pausieren und fortsetzen | User | required |
| [UC14](../use-cases.md#uc14--pencil-marks-candidate-notes) | Candidate-Notes pro Zelle | User | required |

### Use-Case-Cluster (zur Test-Planung)

| Cluster | UCs | Test-Fokus |
|---------|-----|------------|
| **Authentifizierung** | UC02, UC03 | Sicherheit ([V01](../validation.md#v01--username-uc02)–[V04](../validation.md#v04--login-rate-limit-uc03), [V16](../validation.md#v16--authorization-alle-geschützten-seitenendpoints)) |
| **Puzzle-Lifecycle** | UC04, UC05, UC11 | Solver-Korrektheit (Q1), Eindeutigkeit (Q2) |
| **Spiel-Flow** | UC06, UC07, UC09, UC10, UC13, UC14 | Game-State-Konsistenz ([V12](../validation.md#v12--game-state-konsistenz-uc10-uc13)) |
| **Diskover & Stats** | UC01, UC08, UC12 | Read-Pfade, Sortierung/Pagination |

---

## Verweise

- [Kapitel 1 — Einführung und Ziele](./01-introduction.md)
- [Kapitel 2 — Randbedingungen](./02-constraints.md)
- [Use Cases UC01–UC14 (Detail)](../use-cases.md)
- [Validation Rules](../validation.md)
- [Funktionalitäts-Matrix](../functionality.md)
- [ER-Modell](../erm.md)
- [Mockup-Briefings](../mockup-briefs.md)
