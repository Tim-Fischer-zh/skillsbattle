# Mockup Briefings — Killer Sudoku

Diese Briefings sind als **Copy-Paste-Prompts für Figma + Claude** gedacht. Pro Screen ein Block. Tim klebt den Block in Figma-Claude, iteriert, exportiert PNG nach `docs/mockups/<screen-id>.png`.

**Globale Design-Konventionen (vor dem ersten Screen einmal in Figma setzen):**

```
Design-System für Killer-Sudoku-App:
- Farbpalette:
  - Primary: #2563eb (blau-600) — Buttons, Links, Highlights
  - Background: #f8fafc (slate-50)
  - Surface: #ffffff
  - Text-Primary: #0f172a (slate-900)
  - Text-Muted: #64748b (slate-500)
  - Border: #e2e8f0 (slate-200)
  - Error: #dc2626 (red-600)
  - Success: #16a34a (green-600)
  - Cage-Border-Highlight: #475569 (slate-600), dashed
- Schrift: Inter (Sans), Monospace für Zellen-Zahlen (JetBrains Mono / SF Mono)
- Spacing-Skala: 4 / 8 / 12 / 16 / 24 / 32 / 48 px
- Border-Radius: 8 px (cards/buttons), 4 px (inputs), 0 px (Sudoku-Zellen)
- Schatten: 0 1px 3px rgba(0,0,0,0.1) für Cards
- Header-Höhe: 64 px sticky top
- Min-Page-Width: 1024 px (desktop-first; mobile als Stretch-Goal)
```

---

## Screen S1 — Home / Rules

```
Erstelle einen Landing-Screen für eine Killer-Sudoku-App.

Layout (Top → Bottom, 1024 px breit):
1. Sticky Header (64px): Logo "Killer Sudoku" links, rechts Buttons "Register" + "Login".
2. Hero-Section (oberhalb-fold, 480 px hoch):
   - H1 "Killer Sudoku" 48 px bold, links
   - Untertitel: "Sudoku mit dem Kniff: Cage-Summen."
   - 2 CTA-Buttons: "Account erstellen" (primary blau), "Login" (outline)
   - Rechts neben dem Text: ein gerendertes 4×4 Mini-Killer-Sudoku als visueller Eye-Catcher (mit 3-4 Cages, Summen sichtbar in linker oberer Ecke jeder Cage, gestrichelter Cage-Border)
3. Section "Spielregeln" (white card, padding 32px):
   - H2 "So wird gespielt"
   - 4 Regel-Cards nebeneinander mit Icon + Titel + Text:
     a) "Zahlen 1–9" — Trage in jede Zelle eine Zahl von 1 bis 9 ein
     b) "Jede Reihe, Spalte, Block einmal" — Jede 1–9 erscheint genau einmal pro Reihe, Spalte und 3×3-Block
     c) "Cage-Summen" — Die Zahlen in einem Cage summieren sich zur eingetragenen Zahl
     d) "Keine Doppel im Cage" — Innerhalb eines Cages darf jede Zahl nur einmal vorkommen
4. Section "Beispiel" (mit grossem 9×9-Killer-Sudoku-Grid voll dargestellt):
   - Cages mit gestrichelten Innen-Borders
   - Cage-Summen klein oben-links in jeder Cage-Hauptzelle
   - Grid 540×540 px
5. Footer (sticky-bottom optional): Copyright, Links.

States: nur Default.

Output: PNG-Export 1440×<height> in docs/mockups/S1-home.png.
```

---

## Screen S2 — Register

```
Erstelle einen Registrierungs-Screen.

Layout (1024 px, centered card):
1. Header (64px) wie S1, aber ohne Register/Login-Buttons.
2. Centered Card (400 px breit, padding 32px):
   - H2 "Account erstellen"
   - Input "Username" (Pflicht, helper "3–50 Zeichen, A–Z 0–9 _ -")
   - Input "Email" (Pflicht, type=email)
   - Input "Passwort" (Pflicht, type=password, helper "min 8 Zeichen, Buchstaben + Zahlen")
   - Input "Passwort bestätigen" (Pflicht, type=password)
   - Primary-Button full-width "Account erstellen"
   - Link unter Button: "Schon Account? → Login"

States:
- Default (alle leer)
- Validation-Error (Username zu kurz → rote Border + Text unter Input)
- Loading (Button-Spinner)
- Submit-Error (Username/Email bereits vergeben → roter Banner oben in Card)
- Success (kurz "Account erstellt — wird weitergeleitet…")

Output: 4 Frames im selben Mockup zeigen alle Zustände nebeneinander.
```

---

## Screen S3 — Login

```
Erstelle einen Login-Screen.

Layout (analog S2, centered Card):
1. Header
2. Card (400 px):
   - H2 "Anmelden"
   - Input "Username oder Email"
   - Input "Passwort"
   - Primary-Button "Anmelden"
   - Link: "Noch kein Account? → Registrieren"

States:
- Default
- Wrong-Credentials (generischer Banner "Username oder Passwort falsch", KEIN Hinweis welches!)
- Rate-Limited ("Zu viele Fehlversuche — in 5 Min erneut versuchen")
- Loading

Wichtig: Fehlermeldung muss generisch sein (Security: keine Auskunft, ob User existiert).
```

---

## Screen S4 — Puzzle List (Browse / Filter)

```
Erstelle einen Puzzle-Browse-Screen.

Layout:
1. Header (mit eingeloggtem User-Indicator rechts: Avatar + Username + Dropdown "Logout")
2. Page-Title "Puzzles" mit Subtitle "Wähle ein Puzzle zum Lösen"
3. Toolbar (sticky unter Header):
   - Links: Filter-Buttons "Alle" "Einfach (1)" "Mittel (2)" "Schwer (3)" (Toggle-Style, aktiv hervorgehoben)
   - Sort-Dropdown: "Neueste zuerst" / "Schwierigkeit aufsteigend"
   - Rechts: Primary-Button "+ Neues Puzzle"
4. Grid-Layout: 3 Spalten × N Reihen, Cards mit:
   - Mini-Preview des Grids (90×90 px) mit Cages-Outlines, KEINE Zahlen
   - Difficulty-Badge ("1", "2" oder "3") farblich (grün/gelb/rot)
   - Creator-Username
   - "Vor 2 Tagen" relative Zeit
   - Mein Best-Score (optional): "Mein Score: 8420" oder "Noch nicht gespielt"
   - Klick-Hover: Card hebt sich leicht, Button "Spielen" wird sichtbar
5. Pagination unten: "Seite 1 von 5" mit Pfeilen

States:
- Default (gefüllt)
- Empty ("Noch keine Puzzles. Erstelle das erste!" mit grossem Plus-Button)
- Filter angewendet (Difficulty=2, nur Mittel-Puzzles)
- Loading (Skeleton-Cards)
```

---

## Screen S5 — Enter Puzzle (Editor)

```
Erstelle einen Killer-Sudoku-Editor-Screen.

Layout:
1. Header (logged in)
2. Page-Title "Neues Puzzle anlegen" + Subtitle "Definiere die Cages und Soll-Summen"
3. Two-Column-Layout:
   - LINKS (60 %): Das 9×9 Grid (max 540×540 px), interaktiv:
     - Jede Zelle ist klickbar
     - Aktive Cage farblich hervorgehoben (z.B. hellblau)
     - Cage-Border gestrichelt
     - Cage-Summe in linker oberer Ecke der hinzugefügten Cage-Zellen
     - Bottom-Toolbar unter Grid: Buttons "[+] Neuen Cage starten" "[X] Letzte Zelle entfernen" "[Trash] Cage löschen"
   - RECHTS (40 %): Sidebar mit:
     - Difficulty-Selector: 3 Radio-Buttons (1, 2, 3) als Pill-Buttons
     - Cage-Liste (scrollbar):
       Pro Cage eine Reihe: Cage-#1 — Sum-Input (Number, 1–45) — Zellen-Count (z.B. "3 Zellen") — Delete-Icon
     - Statistik: "Zellen zugeordnet: 81 / 81" (rot wenn ≠ 81)
     - Statistik: "Anzahl Cages: 28"
     - Primary-Button "Puzzle prüfen & speichern" (disabled bis 81/81)

Workflow:
1. User klickt "+ Neuen Cage", System wechselt in Cage-Build-Mode
2. User klickt Zellen → werden zu aktivem Cage hinzugefügt (highlight)
3. User gibt Cage-Summe ein (Sidebar)
4. User klickt nächsten Cage, etc.
5. Wenn 81 Zellen zugeordnet → Speichern-Button aktiv

States:
- Empty (0 Cages, 0 Zellen zugeordnet)
- Mid-Edit (3 Cages, 7 Zellen, gerade Cage #4 im Build)
- Complete (28 Cages, 81 Zellen) — Speichern aktiv
- Error nach Save: Modal "Puzzle ist nicht lösbar — bitte überprüfe die Summen" (Reject von UC05)
- Error: Modal "Puzzle hat mehrere Lösungen — bitte ergänze Constraints"
```

---

## Screen S6 — Play Puzzle (Spiel-Screen, ZENTRAL)

```
Erstelle den zentralen Spiel-Screen für Killer-Sudoku.

Layout (Two-Column):
1. Header (logged in)
2. Mini-Header-Strip:
   - Links: "← Zurück zur Liste"
   - Mitte: Difficulty-Badge, "Erstellt von: alice"
   - Rechts: Timer "00:08:42" gross, Hint-Counter "Hints: 2"
3. LINKS (65 %): Das 9×9 Grid (max 600×600 px):
   - Cages mit gestrichelten Borders
   - Cage-Summe klein oben-links der "top-left"-Zelle des Cages
   - Zellen klickbar, aktive Zelle blau umrandet
   - Eingetragene Zahlen gross, mittig
   - Pencil-Marks (kleine 1-9 Zahlen in 3×3-Raster in der Zelle, falls aktiv)
   - Optional: Falsche Zahl rot (NUR wenn UI-Toggle "Fehler anzeigen" an — Stretch-Feature)
4. RECHTS (35 %): Toolbar als vertikale Card:
   - Number-Pad 1–9 (Buttons 3×3-Anordnung, je 60×60 px) für Mouse/Touch-Input
   - Pencil-Mode-Toggle (Switch mit Icon Stift)
   - Eraser-Button (löscht Wert der aktiven Zelle)
   - "[Hint] Tipp anfordern" (Primary-Button)
   - "[Pause] Pausieren" (Secondary)
   - "[Check] Lösung prüfen" (Success-grün, disabled bis alle Zellen gefüllt)
   - Section "Statistik" am Ende: "Hints: 2" "Pencil-Marks aktiv: ja/nein"

States:
- Default (laufend, einige Zellen gefüllt)
- Pencil-Mode (Pencil-Toggle aktiv, Number-Pad-Klick fügt kleine Marks hinzu)
- Pause (Grid disabled/gegraut, Overlay "Pausiert", Button "Weiterspielen")
- Hint-Used (kurze Toast "Hint angewendet auf Zelle (3,5) = 7. Hints: 3")
- Check → Wrong (Toast oder Modal "Lösung nicht korrekt")
- Check → Correct (Modal "🎉 Geschafft! Zeit: 8:42, Hints: 2, Score: 7480. → Highscore ansehen")

Tastatur-Mapping (in Doku notieren):
- 1–9: Wert eintragen (oder Pencil-Mark, je nach Mode)
- Delete/Backspace: Zelle leeren
- Pfeiltasten: Zell-Auswahl bewegen
- P: Pencil-Toggle
- H: Hint
- Space: Pause
```

---

## Screen S7 — Highscore

```
Erstelle einen Highscore-Listings-Screen.

Layout:
1. Header (logged in)
2. Page-Title "Highscore" + Subtitle "Die besten Resultate aller Spieler"
3. Tabs / Filter (sticky):
   - Difficulty-Filter "Alle" / "1" / "2" / "3"
4. Tabelle (full-width):
   - Spalten: Rank (#), Spieler (Username + Avatar), Difficulty (Badge), Zeit (HH:MM:SS), Hints, Score (bold)
   - Erste 3 Plätze farblich hervorgehoben (Gold/Silber/Bronze-Border-Highlight)
   - Highlighting: meine eigenen Einträge mit "(du)" hinter dem Username
5. Pagination unten

States:
- Default (Top 50)
- Empty ("Noch keine Resultate — sei der Erste!")
- Filtered (z.B. Difficulty=3)
```

---

## Globales Layout — Navigation

```
Erstelle die globale Top-Navigation als sticky Header (64 px hoch).

Inhalt (Logged-OUT):
- Links: Logo "Killer Sudoku" (klickbar, → /)
- Rechts: "Login" (link) + "Register" (primary button)

Inhalt (Logged-IN):
- Links: Logo
- Mitte: Nav-Links horizontal: "Puzzles" /puzzles, "Neues Puzzle" /puzzles/new, "Highscore" /highscore
- Rechts: User-Avatar + Username + Chevron → Dropdown (Profile-Item optional, Logout)

States:
- Logged-out
- Logged-in
- Active-Route (Link im Header dunkler markiert)
```

---

# Anti-Halluzinations-Notiz für Figma-Claude

Beim Generieren bitte folgendes NICHT erfinden, das steht NIRGENDS im Brief und ist auch nicht im Skill-7-Rahmen vorgesehen:

- Social-Login (Google/Apple) — wir machen nur Username+Email+Passwort
- Dark-Mode Toggle — nicht required für Submission
- Multiplayer / Live-Chat / Real-Time-Sharing — nicht Teil der UCs
- "AI-Solve"-Buttons, "Buy Premium"-Banner — nicht Teil der App

Nur die 7 Screens (+ Layout) wie oben spezifiziert.
