# Use Cases — Killer Sudoku App

**Stack:** .NET 10, Blazor Server, MS-SQL Server Express
**Source:** `skillsbattle2026_1.1.md` §2.1 (UC1–UC11) + §2.3 (3 selbst gewählte UCs)

Notation:
- **Actor** — wer löst die Aktion aus
- **Trigger** — UI-Event oder Zustand
- **Pre** — Vorbedingung
- **Main** — Haupt-Flow (Happy Path)
- **Alt** — Alternativ-/Fehler-Flow
- **Post** — Nachbedingung
- **AC** — Acceptance Criteria (testbar, mappt 1:1 auf Test-Cases)
- **Source** — wörtliches README-Zitat (für Constraint-Audit)

---

## UC01 — Read Rules

- **Actor:** Visitor (unauthenticated) ODER User
- **Trigger:** Aufruf der Startseite (`/`)
- **Pre:** Keine
- **Main:**
  1. Anwendung zeigt Startseite
  2. Beispiel-Killer-Sudoku ist sichtbar (gerendertes Grid mit Cage-Summen)
  3. Regeln sind lesbar dargestellt (Sudoku- + Killer-Regeln)
- **Alt:** —
- **Post:** Nutzer kennt Regeln, kann zu Login/Register navigieren
- **AC:**
  - AC01.1: Startseite ist OHNE Login erreichbar
  - AC01.2: Beispiel-Grid zeigt ≥1 Cage mit sichtbarer Summe in linker oberer Ecke
  - AC01.3: Alle 4 Regeln aus README sind als Text vorhanden (1–9, row/col/nonet exactly once, cage sum match, no duplicate in cage)
- **Source:** "On the start page, an example as well as the rules of the game must be displayed."

---

## UC02 — Create User (Register)

- **Actor:** Visitor
- **Trigger:** Klick auf "Register" / "Konto erstellen"
- **Pre:** Visitor nicht eingeloggt
- **Main:**
  1. Visitor öffnet Registrierungs-Formular
  2. Gibt Username, Email, Password, Password-Confirm ein
  3. Submit
  4. Server validiert (Unique Username/Email, Passwort-Stärke, Match)
  5. Passwort wird gehasht (ASP.NET Identity / BCrypt)
  6. User wird in DB gespeichert (Tabelle `User`)
  7. Redirect zu Login (oder Auto-Login)
- **Alt:**
  - 4a Username/Email bereits vergeben → Fehlermeldung am Feld
  - 4b Passwort < Mindestlänge oder Confirm-Mismatch → Fehlermeldung
  - 4c Email-Format ungültig → Fehlermeldung
- **Post:** Neuer User-Datensatz vorhanden, kann sich einloggen
- **AC:**
  - AC02.1: Username + Email sind UNIQUE (DB-Constraint)
  - AC02.2: Password wird NIE im Klartext gespeichert
  - AC02.3: Mindest-Passwortlänge ≥ 8 Zeichen
  - AC02.4: Email-Validierung (RFC-Format-Regex)
- **Source:** "To use the application, a login is required. Define the necessary fields in the mockups and in the database."

---

## UC03 — Login

- **Actor:** Visitor mit Account
- **Trigger:** Login-Formular Submit
- **Pre:** User ist registriert
- **Main:**
  1. Visitor gibt Username (oder Email) + Passwort ein
  2. Submit
  3. Server verifiziert Hash
  4. Session/Cookie wird gesetzt (ASP.NET Core Authentication)
  5. Redirect zu Home (eingeloggter Bereich)
- **Alt:**
  - 3a Falsche Credentials → generische Fehlermeldung ("Username oder Passwort falsch", KEIN Hinweis welches)
  - 3b Rate-Limit nach 5 Fehlversuchen
- **Post:** User ist eingeloggt, kann geschützte Bereiche nutzen
- **AC:**
  - AC03.1: Falsche Credentials → keine Auskunft darüber, ob Username existiert
  - AC03.2: Geschützte Routen ohne Session → Redirect zu Login
  - AC03.3: Login-Cookie ist HttpOnly + Secure
- **Source:** "The user logs in."

---

## UC04 — Enter Puzzle

- **Actor:** User (eingeloggt)
- **Trigger:** Klick "Neues Puzzle anlegen"
- **Pre:** User eingeloggt
- **Main:**
  1. User öffnet "Enter Puzzle" Screen (leeres 9×9 Grid + Cage-Editor)
  2. User wählt Schwierigkeit (1, 2 oder 3)
  3. User definiert Cages: zusammenhängende Zell-Gruppen + Summe pro Cage
  4. Jede Zelle gehört zu **genau einem** Cage
  5. Submit "Puzzle prüfen & speichern" → wechselt zu UC05
- **Alt:**
  - 2a Difficulty außerhalb {1,2,3} → Fehler
  - 3a Zelle in mehreren Cages → Fehler ("Cell already assigned to cage X")
  - 3b Zelle keinem Cage zugeordnet → Fehler vor Speichern
  - 3c Cage-Summe < 1 oder > 45 → Fehler (1 Zelle: max 9; 9 Zellen: max 1+2+…+9=45)
- **Post:** Puzzle-Eingabe ist gültig strukturiert (noch nicht solvability-geprüft)
- **AC:**
  - AC04.1: Difficulty muss 1, 2 oder 3 sein (DB-Check-Constraint)
  - AC04.2: Jede der 81 Zellen ist exakt einem Cage zugeordnet
  - AC04.3: Cage-Summe ∈ [1, 45]
  - AC04.4: KEINE Lösung wird gespeichert (`Puzzle`-Tabelle hat keine Solution-Spalte)
- **Source:** "The user can manually enter a puzzle... specify the difficulty level from 1-3... stored in the database. No solution is recorded. Solutions must be calculated with an algorithm."

---

## UC05 — Save New Puzzle (Solvability Check)

- **Actor:** User (eingeloggt)
- **Trigger:** Submit aus UC04
- **Pre:** Puzzle-Struktur aus UC04 ist gültig
- **Main:**
  1. Server ruft Solver-Algorithmus (UC11) auf
  2. Solver prüft Existenz UND Eindeutigkeit der Lösung
  3. Falls genau 1 Lösung existiert → Puzzle wird in DB gespeichert (`Puzzle` + `Cage` + `CageCell`)
  4. Bestätigung an User
- **Alt:**
  - 2a Keine Lösung möglich → Fehler "Puzzle ist nicht lösbar", NICHT speichern
  - 2b Mehrere Lösungen möglich → Fehler "Puzzle hat mehr als eine Lösung", NICHT speichern
- **Post:** Solvable + eindeutiges Puzzle in DB; nicht-lösbares wird verworfen
- **AC:**
  - AC05.1: Puzzle wird **nur** gespeichert wenn Solver genau 1 Lösung findet
  - AC05.2: Multi-Solution-Puzzles werden abgelehnt (Spec: "solution must be unique")
  - AC05.3: Bei Reject: Eingabe bleibt im Formular erhalten (kein Datenverlust)
- **Source:** "The puzzle can only be saved if it is solvable. Check with an algorithm if the puzzle can be solved before saving." + "The solution must be unique."

---

## UC06 — Solve Puzzle

- **Actor:** User (eingeloggt)
- **Trigger:** Auswahl eines Puzzles aus der Liste (siehe UC12)
- **Pre:** Mindestens 1 gespeichertes Puzzle existiert, User eingeloggt
- **Main:**
  1. User wählt Puzzle
  2. Game-Session startet (`Game`-Datensatz: GameId, UserId, PuzzleId, StartTime, HintsUsed=0)
  3. Grid wird leer angezeigt mit Cage-Summen in den Ecken
  4. User trägt Zahlen 1–9 in Zellen ein
  5. User kann Hint anfordern (UC07), Lösung prüfen (UC09), pausieren (UC13), Pencil-Marks setzen (UC14)
- **Alt:**
  - 4a User trägt 0 oder Zahl > 9 ein → Eingabe rejected (Client + Server)
- **Post:** Game-Session ist aktiv; Inputs werden gespeichert (GameCell)
- **AC:**
  - AC06.1: Jeder User darf jedes gespeicherte Puzzle starten
  - AC06.2: Nur Werte 1–9 (oder leer) in Zellen erlaubt
  - AC06.3: Game-Start-Zeit wird gespeichert
- **Source:** "Every user can solve stored puzzles."

---

## UC07 — Ask for a Hint

- **Actor:** User (in aktiver Game-Session)
- **Trigger:** Klick "Hint"-Button
- **Pre:** Game läuft, Grid ist nicht vollständig
- **Main:**
  1. Server berechnet Solver-Output (UC11) für aktuellen Zustand
  2. Hint-Algorithmus wählt **eine** leere oder falsche Zelle aus
  3. Korrekter Wert dieser Zelle wird angezeigt + automatisch eingetragen
  4. HintsUsed wird inkrementiert + `HintLog` Eintrag (GameId, Row, Col, Timestamp)
- **Hint-Vorschlag (dokumentiert):**
  - Strategy A — "Naked Single": finde eine Zelle, in der nur 1 Wert gemäss Constraints möglich ist; biete diese mit "Hier kann nur N stehen"
  - Strategy B — "Cage-Forced": finde einen Cage mit eindeutig forcierter Belegung
  - Fallback — wenn keine Logik-Hint möglich: lass Solver einen beliebigen leeren Zellwert ausfüllen
- **Alt:**
  - Grid vollständig → Hint-Button disabled
- **Post:** Eine Zelle ist befüllt, HintsUsed +1
- **AC:**
  - AC07.1: Hint füllt nur korrekte Zelle (matcht eindeutige Lösung)
  - AC07.2: HintsUsed wird persistiert
  - AC07.3: Hint funktioniert auch wenn der User vorher falsche Werte eingetragen hat (überschreibt sie nicht, sondern wählt eine andere Zelle ODER markiert die falsche)
- **Source:** "If the user is stuck, they can ask for a hint. This should be considered for the high score at the end. Develop a suggestion for the hint, document it, and implement your solution."

---

## UC08 — Show High Score

- **Actor:** User (eingeloggt)
- **Trigger:** Navigation "Highscore"
- **Pre:** Mindestens 1 abgeschlossenes Game existiert
- **Main:**
  1. User öffnet Highscore-Page
  2. Liste der Top-N Ergebnisse: Username, Puzzle, Time, HintsUsed, Score
  3. Sortiert nach Score (absteigend)
- **Score-Formel:**
  - `Score = max(0, 10000 - TimeSeconds - HintsUsed * 300)`
  - Begründung: Zeit-Penalty linear (1 Pkt/Sek); Hints kosten 5 Min Äquivalent
  - Floor bei 0 → keine negativen Scores
- **Alt:**
  - Keine Ergebnisse → leere Liste mit Hinweis
- **Post:** —
- **AC:**
  - AC08.1: Score nach Formel berechnet, ganzzahlig
  - AC08.2: Sortierung absteigend nach Score
  - AC08.3: Ein User kann mehrfach in der Liste auftauchen (pro Game ein Eintrag)
- **Source:** "Implement rules for a high score based on the time needed and the number of hints used."

---

## UC09 — Check Solution

- **Actor:** User (in aktiver Game-Session)
- **Trigger:** Klick "Lösung prüfen" ODER alle 81 Zellen befüllt
- **Pre:** Game läuft, alle 81 Zellen befüllt (1–9)
- **Main:**
  1. Client/Server prüft: alle Zellen 1–9 befüllt?
  2. **Simple Sum-Check zuerst:** Summe aller Zellen = 9 × (1+2+…+9) = 9 × 45 = **405**
  3. Falls Summe ≠ 405 → "Lösung falsch" (schnellster Fail)
  4. Falls 405 OK → Vollständige Validierung:
     - Jede Row enthält 1–9 exactly once
     - Jede Column enthält 1–9 exactly once
     - Jeder Nonet (3×3) enthält 1–9 exactly once
     - Jeder Cage: Summe matcht + keine Duplikate
  5. Bei "alles korrekt" → UC10 (Save Result)
- **Alt:**
  - 1a Nicht alle Zellen befüllt → "Bitte zuerst alle Felder ausfüllen"
  - 4a Eine Regel verletzt → "Lösung falsch" (keine Detail-Auskunft, oder optional erste Verletzung anzeigen)
- **Post:** Solution-Status klar (richtig/falsch)
- **AC:**
  - AC09.1: Sum-Check bei Summe ≠ 405 verhindert Algorithmus-Aufruf (Performance + Pattern)
  - AC09.2: Eine richtige Lösung erfüllt ALLE 4 Regeln (Sudoku + Killer)
  - AC09.3: Cage-Duplikat-Check wird durchgeführt (Beispiel: Cage `(0,0)+(0,1)=8` mit `4+4` ist invalid)
- **Source:** "If all fields are filled, check the solution." + "Calculate the sum of all numbers in the completed Sudoku. The value can be determined unambiguously."

**Sum-Berechnung dokumentiert:** Jede Row hat 1–9 → Summe pro Row = 45. 9 Rows → Total = 9 × 45 = **405**. (Auch über Columns oder Nonets identisch.)

---

## UC10 — Save Result

- **Actor:** System (auto-triggered nach UC09 = korrekt)
- **Trigger:** UC09 bestätigt "Lösung richtig"
- **Pre:** Game-Session abgeschlossen mit korrekter Lösung
- **Main:**
  1. Server berechnet `TimeSeconds = EndTime - StartTime`
  2. Server berechnet Score (siehe UC08)
  3. `Game`-Datensatz wird mit EndTime, TimeSeconds, HintsUsed, Score, IsCompleted=1 aktualisiert
  4. `Highscore`-Eintrag wird erstellt (denormalisierte View für Listing-Performance)
- **Alt:** —
- **Post:** Ergebnis persistent, taucht im Highscore auf
- **AC:**
  - AC10.1: TimeSeconds ist ≥ 0
  - AC10.2: HintsUsed ist ≥ 0
  - AC10.3: IsCompleted bleibt 0 falls Lösung nicht korrekt war (UC09-Fail-Pfad)
- **Source:** "Save the time required and the number of hints used in the database."

---

## UC11 — Auto Solve

- **Actor:** System (intern, von UC05/UC07/UC09 aufgerufen) ODER Admin/User (UI-Button als Debug-Feature)
- **Trigger:** API-Aufruf oder UI-Button
- **Pre:** Puzzle-Struktur (Grid + Cages) ist gültig
- **Main:**
  1. Solver implementiert Backtracking mit Constraint-Propagation:
     - Standard-Sudoku-Constraints (Row/Col/Nonet)
     - Killer-Cage-Constraints (Summe + keine Duplikate in Cage)
  2. Solver gibt zurück: `{ Solutions: int, Solution?: int[81] }`
  3. Bei `Solutions == 1` → einzige Lösung in `Solution` zurückgegeben
  4. Bei `Solutions == 0` → unlösbar
  5. Bei `Solutions >= 2` → nicht eindeutig (Solver bricht nach 2. gefundener Lösung ab)
- **Alt:** —
- **Post:** Lösung verfügbar für UC05, UC07, UC09
- **AC:**
  - AC11.1: Solver erkennt 0/1/≥2 Solutions korrekt
  - AC11.2: Solver terminiert auch bei schweren Puzzles in < 2 Sekunden (Performance-Boundary)
  - AC11.3: Solver respektiert Cage-Constraint (kein Duplikat innerhalb Cage auch wenn Sudoku-Constraint es erlauben würde)
- **Source:** "Implement the possibility to automatically solve the Sudoku. You will need this function for the hints and for checking a new puzzle."

---

# 3 Zusätzliche Use Cases (selbst gewählt)

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

# Strict-Wort-Audit (für Test-Cases-Bindung)

Alle "must/only/strictly/exactly/forbidden/required" aus README + UCs:

| # | Wort/Phrase | Quelle | Code-Check (Test-Anker) |
|---|-------------|--------|-------------------------|
| S1 | "must be displayed" (start page) | README UC1 | UC01 → AC01.1+1.2+1.3 |
| S2 | "login is required" | README UC2 | UC03 → AC03.2 |
| S3 | "specify the difficulty level from 1-3" | README UC4 | UC04 → AC04.1 |
| S4 | "No solution is recorded" | README UC4 | UC04 → AC04.4 (DB-Schema) |
| S5 | "must be calculated with an algorithm" | README UC4 | UC11 |
| S6 | "only be saved if it is solvable" | README UC5 | UC05 → AC05.1 |
| S7 | "solution must be unique" | README §1 + 2.1 | UC05 → AC05.2 + UC11 → AC11.1 |
| S8 | "Each row, column, and nonet contains each number exactly once" | README §1 | UC09 → AC09.2 |
| S9 | "The sum of all numbers in a cage must match" | README §1 | UC09 → AC09.2 |
| S10 | "No number appears more than once in a cage" | README §1 | UC09 → AC09.3 |
| S11 | "If all fields are filled, check the solution" | README UC9 | UC09 → AC09.1 (Sum-Check vor Algo) |
| S12 | "The value can be determined unambiguously" (sum) | README §2.3 | UC09 → 405 |

→ Jeder Eintrag bekommt im Test-Protokoll mindestens einen positiven + einen negativen Test-Case.
