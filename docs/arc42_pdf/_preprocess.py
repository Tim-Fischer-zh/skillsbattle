#!/usr/bin/env python3
"""ARC42 PDF preprocessor — produces submission-clean chapter copies.

Uses shared rules from docs/_sanitize.py.
"""

from __future__ import annotations
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
from _sanitize import (  # noqa: E402
    remove_marked_sections,
    sanitize_links_arc42,
    strip_inline_phrases,
    light_polish,
    replace_first_h1_with_anchor,
)

SRC = HERE.parent / "arc42"
DST = HERE

CHAPTERS = [
    ("01-introduction.md",      "Einführung und Ziele"),
    ("02-constraints.md",       "Randbedingungen"),
    ("03-context.md",           "Kontextabgrenzung"),
    ("04-solution-strategy.md", "Lösungsstrategie"),
    ("05-building-blocks.md",   "Bausteinsicht"),
    ("06-runtime-view.md",      "Laufzeitsicht"),
    ("07-deployment.md",        "Verteilungssicht"),
    ("08-cross-cutting.md",     "Querschnittliche Konzepte"),
    ("09-decisions.md",         "Architekturentscheidungen"),
    ("10-quality.md",           "Qualitätsanforderungen"),
    ("11-risks.md",             "Risiken und Technische Schulden"),
    ("12-glossary.md",          "Glossar"),
]


def chnum(filename: str) -> int:
    return int(filename.split("-", 1)[0])


COVER = """<a id="cover"></a>

<div class="cover-page">

<div class="cover-title">Killer&nbsp;Sudoku</div>

<div class="cover-subtitle">Architecture Documentation</div>

<div class="cover-standard">nach&nbsp;arc42&nbsp;v8.2</div>

<div class="cover-meta">

| | |
|---|---|
| **Project** | Killer Sudoku (Variant: Cage Sums) |
| **Context** | Skills Battle 2026 — Application Development |
| **Author**  | Tim Fischer |
| **Status**  | Final |
| **Date**    | 2026-05-27 |

</div>

</div>

<div class="page-break"></div>
"""


def write_cover():
    (DST / "_00-cover.md").write_text(COVER, encoding="utf-8")


def write_toc():
    rows = "\n".join(
        f"| {i+1} | [{title}](#chapter-{i+1}) |"
        for i, (_fname, title) in enumerate(CHAPTERS)
    )
    out = f"""<a id="toc"></a>

<h1 id="toc-heading">Inhaltsverzeichnis</h1>

> **Architektur-Dokumentation** — Killer Sudoku · Skills Battle 2026
> Standard: arc42 v8.2 · 12 Kapitel · Status: Final
> Author: Tim Fischer · Stand: 2026-05-27

## Kapitel

| # | Kapitel |
|---|---------|
{rows}

## Konventionen in diesem Dokument

| Konvention | Bedeutung |
|------------|-----------|
| **Querverweise** | "Kapitel X" verlinkt auf den entsprechenden Abschnitt in diesem Dokument |
| **Mermaid-Diagramme** | als SVG eingebettet (Klassen-, Sequenz-, ER-, Flow-Diagramme) |
| **Zitate** | wörtliche Auszüge aus der Aufgabenstellung sind in Anführungszeichen gesetzt |
| **ADR-Nummern** | Kapitel 9 nummeriert Entscheidungen als ADR-001 bis ADR-014 |
| **Identifier** | UCxx = Use Case · Vxx = Validation Rule · ACxx.y = Acceptance Criterion · Txxx = Test Case |

<div class="page-break"></div>
"""
    (DST / "_01-toc.md").write_text(out, encoding="utf-8")


def process_chapter(filename: str) -> str:
    raw = (SRC / filename).read_text(encoding="utf-8")
    text = raw
    text = remove_marked_sections(text)
    text = sanitize_links_arc42(text)
    text = strip_inline_phrases(text)
    text = light_polish(text)
    text = replace_first_h1_with_anchor(text, f"chapter-{chnum(filename)}")
    return text


def main():
    print(f"Source : {SRC}")
    print(f"Target : {DST}")
    DST.mkdir(parents=True, exist_ok=True)
    write_cover()
    write_toc()
    print("  wrote _00-cover.md")
    print("  wrote _01-toc.md")
    for fname, title in CHAPTERS:
        cleaned = process_chapter(fname)
        (DST / fname).write_text(cleaned, encoding="utf-8")
        print(f"  cleaned {fname}  ({chnum(fname):>2}: {title})")
    print("done.")


if __name__ == "__main__":
    main()
