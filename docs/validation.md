# Validation Rules

Strikte Validierungs-Regeln für alle User-Inputs und -Interaktionen.
Layer-Konvention: Client-Side (Blazor `EditForm` / Data-Annotations + JS), Server-Side (Service-Layer), DB-Constraint (CHECK/UNIQUE/FK).

> **Defense in Depth:** Kritische Regeln werden auf **mindestens 2 Layern** geprüft. DB-Constraints sind die letzte Verteidigungslinie.

---

## V01 — Username (UC02)

| Layer | Regel |
|-------|-------|
| Client + Server | Length: 3–50 Zeichen |
| Client + Server | Pattern: `^[A-Za-z0-9_-]+$` (keine Sonderzeichen, kein Whitespace) |
| Server + DB | UNIQUE (case-insensitive für Login, exact für Storage) |
| Server | Trim vor Speichern (kein leading/trailing whitespace) |
| DB | ASP.NET Identity-Default: `UserName` NVARCHAR(50), filtered UNIQUE Index (`IX_AppUser_UserName` WHERE NOT NULL); **kein** Format-CHECK-Constraint im SQL-Schema. Länge/Pattern werden ausschließlich Client + Server geprüft. |

**Fehlermeldung:** "Username muss 3–50 Zeichen lang sein und nur Buchstaben, Zahlen, `_` oder `-` enthalten."

---

## V02 — Email (UC02)

| Layer | Regel |
|-------|-------|
| Client + Server | Regex `^[^\s@]+@[^\s@]+\.[^\s@]+$` (basic) |
| Server | Lowercase vor Speichern |
| Server + DB | UNIQUE |
| DB | ASP.NET Identity-Default: `Email` NVARCHAR(255), filtered UNIQUE Index (`IX_AppUser_Email` WHERE NOT NULL); **kein** Format-CHECK-Constraint im SQL-Schema. Format-Regex läuft Client + Server. |

**Fehlermeldung:** "Bitte gib eine gültige Email-Adresse ein."

---

## V03 — Passwort (UC02, UC03)

| Layer | Regel |
|-------|-------|
| Client + Server | Mindestlänge 8 Zeichen |
| Client + Server | Mindestens 1 Buchstabe + 1 Zahl |
| Client | Confirm-Field muss matchen |
| Server | Hash mit ASP.NET Identity (PBKDF2) oder BCrypt |
| Server | **NIE im Klartext loggen/serialisieren** |
| DB | `PasswordHash` NVARCHAR(MAX), nullable (Identity-Default); Pflicht-Inhalt durch UC02-Flow garantiert |

**Fehlermeldung Register:** "Passwort muss mindestens 8 Zeichen lang sein und Buchstaben + Zahlen enthalten."
**Fehlermeldung Login (falsch):** "Username oder Passwort falsch." _(absichtlich generisch — kein Username-Existenz-Hinweis)_

---

## V04 — Login Rate-Limit (UC03)

| Layer | Regel |
|-------|-------|
| Server | Nach 5 Fehlversuchen innerhalb 5 Min: Block für 5 Min pro Username/IP |
| Server | Logging der fehlgeschlagenen Attempts |

**Fehlermeldung:** "Zu viele fehlgeschlagene Anmeldungen. Bitte in 5 Minuten erneut versuchen."

---

## V05 — Difficulty (UC04)

| Layer | Regel |
|-------|-------|
| Client + Server | Wert ∈ {1, 2, 3} |
| DB | `CK_Puzzle_Difficulty CHECK (Difficulty BETWEEN 1 AND 3)` |

**Fehlermeldung:** "Schwierigkeit muss 1, 2 oder 3 sein."

---

## V06 — Cage-Struktur (UC04)

| Layer | Regel |
|-------|-------|
| Client + Server | Jede der 81 Zellen (Row 0–8, Col 0–8) ist **exakt einem** Cage zugeordnet |
| Server | Cage-Zellen sind orthogonal verbunden (Killer-Sudoku-Konvention) — optional dokumentiert |
| Server + DB | Cage-Summe ∈ [1, 45] (`CK_Cage_Sum_Range`) |
| Server | Anzahl Zellen pro Cage: 1–9 (theoretisches Maximum) |
| DB | Trigger `trg_CageCell_UniquePerPuzzle` verhindert Doppel-Zuordnung |

**Fehlermeldung:**
- "Zelle ({row},{col}) ist mehreren Cages zugeordnet." (Doppel)
- "Zelle ({row},{col}) gehört keinem Cage an." (Lücke)
- "Cage-Summe muss zwischen 1 und 45 liegen."

---

## V07 — Puzzle Solvability (UC05)

| Layer | Regel |
|-------|-------|
| Server | Solver liefert exakt 1 Lösung (`SolveResult.Solutions == 1`) |
| Server | Bei 0 Lösungen: Reject mit "Puzzle ist nicht lösbar." |
| Server | Bei ≥2 Lösungen: Reject mit "Puzzle hat mehrere Lösungen — Eindeutigkeit erforderlich." |

> **Wichtig (Pattern 2):** "solution must be unique" aus README ist STRIKT. Multi-Solution wird abgelehnt, nicht nur Zero-Solution.

---

## V08 — Zell-Eingabe im Spiel (UC06)

| Layer | Regel |
|-------|-------|
| Client | Nur Tastatur-Input 1–9 (sowie Delete/Backspace für Löschen) wird akzeptiert |
| Client | 0, Buchstaben, Sonderzeichen werden ignoriert |
| Server | `CellValue ∈ {1,…,9}` oder NULL |
| DB | `CK_GameCell_Value CHECK (CellValue IS NULL OR CellValue BETWEEN 1 AND 9)` |

**Fehlermeldung:** (Client unterbindet im Idealfall stumm; Server-Fehler: "Ungültiger Zellwert.")

---

## V09 — Sum-Check Lösung (UC09)

| Regel | Wert |
|-------|------|
| Erwartete Gesamtsumme | **9 × 45 = 405** |
| Begründung | Jede der 9 Reihen enthält 1+2+…+9 = 45. 9 Reihen × 45 = 405. (Über Spalten/Nonets identisch.) |
| Anwendung | UC09 prüft `SUM(CellValue) WHERE GameId = @id` **vor** vollständiger Algorithmus-Validierung. |

| Layer | Regel |
|-------|-------|
| Server | Wenn Summe ≠ 405 → return `{IsCorrect: false}` ohne Algo-Aufruf |
| Server | Wenn Summe == 405 → vollständige Validierung (Row/Col/Nonet/Cage) |

---

## V10 — Lösungs-Validierung Vollständig (UC09)

| Layer | Regel |
|-------|-------|
| Server | Jede Row (0–8) enthält 1–9 exactly once |
| Server | Jede Column (0–8) enthält 1–9 exactly once |
| Server | Jeder Nonet (3×3-Block, 9 total) enthält 1–9 exactly once |
| Server | Pro Cage: SUM(CellValue) == Cage.Sum |
| Server | Pro Cage: keine doppelten Werte (auch wenn Sudoku-Regeln es technisch zulassen würden) |
| Server | Alle 81 Zellen befüllt (kein NULL) |

**Fehlermeldung:** "Lösung ist nicht korrekt." (Detail-Auskunft optional, beeinflusst Hint-Verhalten nicht.)

---

## V11 — Hint nur bei unvollständigem Grid (UC07)

| Layer | Regel |
|-------|-------|
| Client | Hint-Button disabled wenn alle 81 Zellen befüllt |
| Server | `HintService.GetHintAsync` wirft `InvalidOperationException` wenn Game completed, Grid voll oder Game-State nicht lösbar (statt allgemeines "Fehler"). Implementiert in `HintService.cs`. |

---

## V12 — Game-State-Konsistenz (UC10, UC13)

| Layer | Regel |
|-------|-------|
| Server | `IsCompleted == 1` setzt nur, wenn Lösung verifiziert korrekt war |
| Server | `TimeSeconds = (EndTime - StartTime) - TotalPausedSeconds` ≥ 0 |
| DB | `CK_Game_TimeSeconds`, `CK_Game_HintsUsed`, `CK_Game_Score` (alle ≥ 0) |
| DB | Filtered Index `UX_Game_ActiveOnly`: max 1 nicht-completed Game pro (UserId, PuzzleId) |

---

## V13 — Pencil Mark (UC14)

| Layer | Regel |
|-------|-------|
| Client + Server | MarkValue ∈ {1, …, 9} |
| Server | Pencil Marks nur in leeren Zellen (CellValue IS NULL) |
| Server | Beim Setzen eines finalen Wertes → alle Pencil Marks dieser Zelle löschen |
| DB | `CK_PencilMark_Value` + Composite PK verhindert Duplikate |

---

## V14 — XSS / Output Encoding (alle Screens)

| Layer | Regel |
|-------|-------|
| Server | Username/Email in Render-Output via Razor `@` (automatisches HTML-Encoding) — nie `@((MarkupString))` für User-Content |
| Client | Form-Inputs validieren Pattern auch UI-side (Maxlength etc.) |

---

## V15 — CSRF (alle POST-Endpoints)

| Layer | Regel |
|-------|-------|
| Server | ASP.NET Antiforgery Token (Standard in Blazor Server / EditForm) |
| Server | SameSite=Lax Cookie-Flag |

---

## V16 — Authorization (alle geschützten Seiten/Endpoints)

| Layer | Regel |
|-------|-------|
| Server | `[Authorize]` auf allen geschützten Razor-Pages |
| Server | User darf NUR seine eigenen Games modifizieren (`game.UserId == currentUserId`) |
| Server | Highscore und Puzzle-Liste sind read-only für alle eingeloggten User |

**Fehlermeldung:** 403 Forbidden bei Cross-User-Mutation; Redirect Login bei nicht-eingeloggt.

---

# Mapping Validation → Test-Cases

| Validation | UC | Positive Test | Negative Test | Boundary Test |
|------------|----|---|---|---|
| V01 Username | UC02 | "alice" → OK | "ab" → reject (zu kurz) | "a" × 50 → OK / 51 → reject |
| V02 Email | UC02 | "a@b.co" → OK | "noat.com" → reject | — |
| V03 Passwort | UC02/03 | "Test1234" → OK | "test" → reject | 8 Zeichen → OK / 7 → reject |
| V05 Difficulty | UC04 | 2 → OK | 4 → reject | 0/1/3/4 → 1+3 OK, 0+4 reject |
| V06 Cage | UC04 | 81 Zellen × Cages → OK | 80 Zellen → reject | Cage Sum=1 / Sum=45 → OK, Sum=46 → reject |
| V07 Solvability | UC05 | Solvable+Unique → save | Multi-Solution → reject | — |
| V08 CellValue | UC06 | 5 → OK | 0 → reject, "a" → reject | 1 → OK, 9 → OK, 10 → reject |
| V09 Sum 405 | UC09 | Korrekte Lsg Σ=405 | Σ=404 (eine 4 statt 5) | — |
| V10 Cage-Duplikat | UC09 | Cage [4,5] sum=9 → OK | Cage [4,4] sum=8 → reject (Duplikat) | — |
| V16 Auth Cross-User | UC10/UC13 | Eigener Game → OK | Fremder Game → 403 | — |

→ Diese Mapping-Tabelle wird in `test-protocol` direkt 1:1 als Test-Cases übernommen.
