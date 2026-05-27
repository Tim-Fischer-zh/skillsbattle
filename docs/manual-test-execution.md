# Manual Test Execution Log

**Executor:** Tim Fischer
**Date:** 2026-05-27
**Build:** local `docker compose -f docker-compose.yml up` at `http://localhost:8080`
**Browser:** Chrome 141 / macOS Darwin 25.5.0
**Reference:** [`test-protocol.md`](test-protocol.md) §Manual Test Cases (M001–M005)

## Execution Results

| ID | Test | Result | Evidence / Observation |
|----|------|:------:|------------------------|
| **M001** | Visual: Cage-Borders sichtbar in Chrome | ✅ **PASS** | `/puzzles/19/play` zeigt 9×9 Grid mit farbigen Cage-Hintergründen (6 Farb-Klassen `--cage-N`), Cage-Sums "45" in oberer linker Ecke jeder Cage-Region, dazu Nonet-Trennlinien (thick black) zwischen 3×3-Blöcken. Screenshot: `ss_0885hyoz6` |
| **M002** | Visual: Cage-Borders sichtbar in Firefox | ⏭ **N/A** | Test-Werkzeug (Claude-in-Chrome) bindet Chrome — kein Firefox-Bridge. Da Blazor Server side-rendering nutzt und CSS keine `-webkit-`/`-moz-`-spezifischen Regeln benutzt, ist die Darstellung browser-agnostic gleich. Empfehlung: einmalig manuell in Firefox prüfen vor Submission. |
| **M003** | Tastatur-Navigation: Pfeiltasten + 1-9 + Delete | ❌ **FAIL** | Klick auf Zelle (0,0) → active-Highlight korrekt gesetzt; ArrowRight + "5" gedrückt — Aktive Zelle bleibt (0,0), kein Wert eingetragen. `@onkeydown` Handler fehlt in `PlayPuzzle.razor`. Entspricht MED-Finding des Mockup-Validators. Workaround: User benutzt das Numpad-Panel rechts (funktioniert). |
| **M004** | Cross-Browser: Highscore in beiden Browsern | ✅ **PASS** (Chrome) | `/highscore` rendert: Filter-Pills (Alle / 1 / 2 / 3), 50-Zeilen Tabelle (Rank, Spieler, Diff, Zeit, Hints, Score), Top-3 Hervorhebung in Gold/Silber/Bronze, Spieler-Initialen als Avatar-Badge, Sortierung Score-DESC dann Zeit-ASC. Screenshot: `ss_1790p8csd`. Firefox-Anteil ⏭ N/A (siehe M002). |
| **M005** | Mobile-Viewport: Layout funktional bei 768 px | ✅ **PASS** | Window auf 768×1024 resized + `/puzzles/19/play` neu geladen: Layout wechselt von Side-by-Side (Grid links, Numpad rechts) auf Stacked (Grid oben, Numpad darunter). Touch-Targets der Cells und Numpad-Buttons ausreichend groß. Kein horizontales Scrollen, kein Overlap. Header bleibt sticky mit allen Nav-Items. Screenshot: `ss_3260vgqx5` |

## Zusatz-Beobachtungen (außerhalb der M-Tests)

- **PuzzleSeeder erzeugt degenerierte Cages** für die Seed-Daten: Puzzle 19 (Diff 2) hat 9 Cages, jeweils eine Zeile = ein Cage mit Sum=45. Σ = 9 × 45 = 405 (spec-konform), aber strukturell der triviale Sonderfall. Echte Killer-Sudoku-Puzzles haben kleinere, irreguläre Cages. Funktional erfüllt es alle README-Constraints, jedoch ist das Spielerlebnis schwach. Generator-Algorithmus für komplexere Cage-Layouts wäre eine Erweiterung.
- **Timer-Sync:** Timer auf Play-Page läuft client-seitig im Server-Round-Trip-Intervall; erst beim Pausieren wird der Server-Stand zum Client persistiert. UX-Hinweis, keine Spec-Verletzung.

## Sign-off

- [x] M001 PASS
- [ ] M002 deferred (Firefox-Run vor Submission empfohlen)
- [x] M003 FAIL — bekannt, Keyboard-Handler in `PlayPuzzle.razor` nicht implementiert
- [x] M004 PASS (Chrome); Firefox deferred (analog M002)
- [x] M005 PASS

3 PASS / 1 FAIL / 1 deferred (Cross-Browser).

Ausgeführt: Tim Fischer · 2026-05-27 ~15:15
