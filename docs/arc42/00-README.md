# Architektur-Dokumentation — Killer Sudoku (arc42)

**Projekt:** Skills Battle 2026 — Application Development — Killer Sudoku
**Stack:** .NET 10 · Blazor Server · MS-SQL Server Express
**Standard:** [arc42](https://arc42.org) v8.2 (12 Kapitel)
**Erstellt:** 2026-05-27 · **Author:** Tim Fischer

---

## Aufbau

Dieses Dokument folgt dem arc42-Template. Jedes Kapitel ist ein eigenständiges Markdown-File. Für die Submission werden alle Kapitel zu einem PDF zusammengeführt (`docs/arc42-build.sh`).

| # | Kapitel | Datei | Status |
|---|---------|-------|--------|
| 1 | Einführung und Ziele | [`01-introduction.md`](01-introduction.md) | DRAFT |
| 2 | Randbedingungen | [`02-constraints.md`](02-constraints.md) | DRAFT |
| 3 | Kontextabgrenzung | [`03-context.md`](03-context.md) | DRAFT |
| 4 | Lösungsstrategie | [`04-solution-strategy.md`](04-solution-strategy.md) | DRAFT |
| 5 | Bausteinsicht | [`05-building-blocks.md`](05-building-blocks.md) | DRAFT |
| 6 | Laufzeitsicht | [`06-runtime-view.md`](06-runtime-view.md) | DRAFT |
| 7 | Verteilungssicht | [`07-deployment.md`](07-deployment.md) | DRAFT |
| 8 | Querschnittliche Konzepte | [`08-cross-cutting.md`](08-cross-cutting.md) | DRAFT |
| 9 | Architekturentscheidungen | [`09-decisions.md`](09-decisions.md) | DRAFT |
| 10 | Qualitätsanforderungen | [`10-quality.md`](10-quality.md) | DRAFT |
| 11 | Risiken und technische Schulden | [`11-risks.md`](11-risks.md) | DRAFT |
| 12 | Glossar | [`12-glossary.md`](12-glossary.md) | DRAFT |

## Quell-Dokumente (in arc42 referenziert, nicht dupliziert)

- [`../use-cases.md`](../use-cases.md) — Use Cases UC01–UC14 mit AC
- [`../erm.md`](../erm.md) — ER-Modell & Design-Entscheidungen DB
- [`../functionality.md`](../functionality.md) — UC × Screen × Service × DB-Matrix
- [`../validation.md`](../validation.md) — V01–V16 Validation-Regeln + Test-Mapping
- [`../mockup-briefs.md`](../mockup-briefs.md) — Figma-Mockup-Briefings
- [`../test-protocol.md`](../test-protocol.md) — Test-Protokoll (Submission 12:00)
- [`../mockups/`](../mockups/) — Generierte Mockup-PNGs
- [`../../db/sudoku.sql`](../../db/sudoku.sql) — DB-Schema
- [`../../skillsbattle2026_1.1.md`](../../skillsbattle2026_1.1.md) — **Original Aufgabenstellung (autoritativ)**

## Konventionen

- **Cross-Refs:** `[Kapitel X](./0X-name.md)` oder `[V07](../validation.md#v07)`
- **Diagramme:** Mermaid-Code-Blocks (im PDF gerendert)
- **Code:** SQL/C# in Fenced Code Blocks mit Sprach-Tag
- **Strikte README-Wörter** (must/only/strictly/exactly/forbidden/required): wörtlich zitiert + Spec-Reference
- **Unsicherheiten:** Mit `> **UNCLEAR:** …` markiert (Pattern-Audit-Anker)
