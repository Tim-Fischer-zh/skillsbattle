#!/usr/bin/env python3
"""Documentation PDF preprocessor — assembles the 6 Spec §2.6 sections.

Reads the repo-internal markdown sources, applies the shared sanitizer to
strip AI-meta and file-path references, rewrites section headings with stable
anchors, and writes the cleaned per-section files into docs/doc_pdf/.

Output sections (in order):
  1. Mockups          — from mockup-briefs.md
  2. Database Diagram — from erm.md
  3. Class Diagram    — from class-diagram.md
  4. Additional UCs   — extracted from use-cases.md §3
  5. Validation Rules — from validation.md
  6. Test Protocol    — from test-protocol.md
"""

from __future__ import annotations
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
from _sanitize import (  # noqa: E402
    remove_marked_sections,
    sanitize_links_doc,
    strip_inline_phrases,
    light_polish,
    replace_first_h1_with_anchor,
)

DOCS = HERE.parent
DST  = HERE

SECTIONS = [
    # (sequence_no, anchor_id, display_title, source_path_relative_to_DOCS, extractor_name)
    # Section 1 uses the hand-written mockup file inside doc_pdf/ that embeds
    # the actual screenshots — the original mockup-briefs.md contains internal
    # design prompts that must not appear in the submission PDF.
    (1, "section-1", "Mockups",                    "doc_pdf/_mockups.md",  None),
    (2, "section-2", "Database Diagram (ERM)",     "erm.md",               None),
    (3, "section-3", "Class Diagram",              "class-diagram.md",     None),
    (4, "section-4", "Zusätzliche Use Cases",      "use-cases.md",         "additional_ucs"),
    (5, "section-5", "Validation Rules",           "validation.md",        None),
    (6, "section-6", "Test Protocol",              "test-protocol.md",     None),
]


def extract_additional_ucs(text: str) -> str:
    """From use-cases.md keep only the '# 3 Zusätzliche Use Cases ...' section
    plus its content up to (but not including) '# Strict-Wort-Audit' header."""
    start = re.search(r"^# 3 Zus[äa]tzliche Use Cases.*$", text, re.MULTILINE)
    if not start:
        return text
    end = re.search(r"^# Strict-Wort-Audit", text, re.MULTILINE)
    chunk = text[start.start(): end.start() if end else len(text)]
    return chunk


def process_section(seq: int, anchor_id: str, title: str,
                    source_path: str, extractor: str | None) -> str:
    raw = (DOCS / source_path).read_text(encoding="utf-8")
    if extractor == "additional_ucs":
        raw = extract_additional_ucs(raw)
    text = raw
    text = remove_marked_sections(text)
    text = sanitize_links_doc(text)
    text = strip_inline_phrases(text)
    text = light_polish(text)
    text = replace_first_h1_with_anchor(text, anchor_id, prefix=f"Sektion {seq} —")
    return text


COVER = """<a id="cover"></a>

<div class="cover-page">

<div class="cover-title">Killer&nbsp;Sudoku</div>

<div class="cover-subtitle">Projekt-Dokumentation</div>

<div class="cover-standard">Mockup · ERM · Klassendiagramm · Use Cases · Validation · Test-Protokoll</div>

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
        f"| {seq} | [{title}](#{anchor}) |"
        for seq, anchor, title, _f, _e in SECTIONS
    )
    out = f"""<a id="toc"></a>

<h1 id="toc-heading">Inhaltsverzeichnis</h1>

> **Projekt-Dokumentation** — Killer Sudoku · Skills Battle 2026
> Author: Tim Fischer · Stand: 2026-05-27 · Status: Final

Dieses Dokument enthält die sechs gemäss Aufgabenstellung §2.6 geforderten Sektionen.

## Sektionen

| # | Sektion |
|---|---------|
{rows}

## Querverweise

| Konvention | Bedeutung |
|------------|-----------|
| **UCxx** | Use Case (UC01–UC11 aus Aufgabenstellung, UC12–UC14 selbst gewählt) |
| **Vxx**  | Validation Rule (V01–V16) |
| **ACxx.y** | Acceptance Criterion zu UCxx |
| **Txxx** | Test-Case-ID (T001–T134, M001–M005) |
| **ADR-NNN** | Architektur-Entscheidung (nur im Architektur-Dokument referenziert) |

> Für die ausführliche Architektur-Sicht siehe das separate Dokument *Killer Sudoku — Architecture Documentation (arc42)*.

<div class="page-break"></div>
"""
    (DST / "_01-toc.md").write_text(out, encoding="utf-8")


def main():
    print(f"Source : {DOCS}")
    print(f"Target : {DST}")
    DST.mkdir(parents=True, exist_ok=True)
    write_cover()
    write_toc()
    print("  wrote _00-cover.md")
    print("  wrote _01-toc.md")
    for seq, anchor, title, filename, extractor in SECTIONS:
        cleaned = process_section(seq, anchor, title, filename, extractor)
        out_name = f"{seq:02d}-{anchor.replace('section-', 'section-')}.md"
        (DST / out_name).write_text(cleaned, encoding="utf-8")
        print(f"  cleaned section {seq}: {title}  ({filename} → {out_name})")
    print("done.")


if __name__ == "__main__":
    main()
