<h1 id="section-4">Sektion 4 — 3 Zusätzliche Use Cases (selbst gewählt)</h1>

**Begründung der Auswahl** (für Doku):
- **UC12 Browse/Filter** — Praktischer Mehrwert (UX), DB-Query-relevant (Integration-Tests)
- **UC13 Pause/Resume** — Reale User-Anforderung (Sudoku-Spiele dauern lange), persistiert Game-State (Component + Integration)
- **UC14 Pencil Marks** — Standard-Feature aller Sudoku-Apps, demonstriert UI-State-Management (Component-Test)

→ Die 3 UCs decken bewusst 3 verschiedene Test-Schwerpunkte ab.

---

## UC12 — Browse / Filter Puzzles

- **Actor:** User (eingeloggt)
- **Trigger:** Navigation "Puzzles" / "Spielen"
- **Pre:** Mindestens 1 gespeichertes Puzzle existiert
- **Main:**
 1. User öffnet Puzzle-Liste
 2. Sieht alle Puzzles: Creator, Difficulty, Created-Date, ggf. eigener Best-Score
 3. User filtert nach Difficulty (1/2/3 oder Alle)
 4. User sortiert nach Created-Date oder Difficulty
 5. Klick auf Puzzle → startet UC06
- **Alt:**
 - Liste leer → "Keine Puzzles vorhanden. Erstelle das erste!"
- **Post:** —
- **AC:**
 - AC12.1: Filter zeigt nur Puzzles mit gewählter Difficulty
 - AC12.2: Liste ist paginiert (z.B. 20 pro Seite) — verhindert Performance-Probleme
 - AC12.3: Filter-Parameter sind in URL persistent (deep-link)

---

## UC13 — Pause & Resume Game

- **Actor:** User (in aktiver Game-Session)
- **Trigger:** Klick "Pause" ODER Navigations-weg von Game-Page
- **Pre:** Game läuft, mindestens 1 Eintrag im Grid
- **Main:**
 1. User klickt Pause
 2. Aktueller Zustand (alle GameCells, HintsUsed, ElapsedTime) wird persistiert
 3. Timer stoppt
 4. UI zeigt "Pausiert" + "Weiterspielen"-Button
 5. Bei "Weiterspielen": Timer läuft weiter, Grid wieder editierbar
- **Resume nach Logout:**
 - User loggt sich später wieder ein, sieht im Profil "Laufendes Spiel — fortsetzen"
- **Alt:**
 - Browser-Tab schliessen ohne Pause → State wird trotzdem persistiert (auto-save bei jedem Move)
- **Post:** Game-State persistiert, Timer pausiert
- **AC:**
 - AC13.1: ElapsedTime wird beim Pause eingefroren (TimeSeconds = sum of active intervals)
 - AC13.2: Resume restored ALLE Zellen + Pencil Marks (UC14)
 - AC13.3: Nur 1 aktives Game pro User-Puzzle-Kombi gleichzeitig (UNIQUE constraint)

---

## UC14 — Pencil Marks (Candidate Notes)

- **Actor:** User (in aktiver Game-Session)
- **Trigger:** Toggle "Pencil Mode" + Klick auf Zelle + Zahl
- **Pre:** Game läuft, Zelle ist leer (kein finaler Wert)
- **Main:**
 1. User aktiviert Pencil-Mode
 2. Klick auf leere Zelle, dann 1–9 → kleine Marke wird hinzugefügt
 3. Mehrere Marken pro Zelle möglich (z.B. "2 und 5 möglich")
 4. Pencil Marks werden in `PencilMark`-Tabelle (oder JSON in GameCell) gespeichert
 5. Sobald finaler Wert (Pencil-Mode off + Eingabe) gesetzt wird → Pencil Marks der Zelle gelöscht
- **Alt:**
 - Click auf bereits markierte Zahl → entfernt diese Marke
- **Post:** Pencil-Marks persistent, helfen User bei Logik
- **AC:**
 - AC14.1: Pencil Marks beeinflussen UC09 (Check Solution) NICHT (nur Sichtbarkeit)
 - AC14.2: Pencil Marks verschwinden, wenn finaler Wert gesetzt wird
 - AC14.3: Bis zu 9 Marks pro Zelle möglich (eine pro Zahl 1–9)

---

