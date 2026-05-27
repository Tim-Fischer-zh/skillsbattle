"""Shared sanitization library for submission-PDF preprocessors.

Reusable rules and helpers so that arc42_pdf/_preprocess.py and
doc_pdf/_preprocess.py apply identical cleanup behavior.
"""

from __future__ import annotations
import re


# Subsection headings to drop entirely (heading + all content until next
# heading of equal or higher rank).
SECTION_HEADINGS_TO_DROP = re.compile(
    r"""^(?P<hashes>\#{1,6})\s+
        .*?
        (?:Anti[-\s]?Halluzinat\w*
          |Pattern[-\s]?Audit\b)
        .*$
    """,
    re.IGNORECASE | re.VERBOSE,
)

# Inline phrases / parentheticals to strip silently.
INLINE_STRIPS = [
    re.compile(r"\s*\(\s*Anti[-\s]?Halluzinations?[-\s]?Anker\s*\)", re.IGNORECASE),
    re.compile(r"\s*\(\s*Anti[-\s]?Halluzinations?[-\s]?Notiz\s*\)", re.IGNORECASE),
    re.compile(r"\s*\(\s*Anti[-\s]?Halluzinations?[-\s]?Hinweis\s*\)", re.IGNORECASE),
    re.compile(r"\s*\(\s*Pattern[-\s]?Audit[-\s]?Anker\s*\)", re.IGNORECASE),
    re.compile(r"\s*—\s*Anti[-\s]?Halluzinations?[-\s]?Anker", re.IGNORECASE),
    re.compile(r"\s*\(\s*siehe\s+Anti[-\s]?Halluzinations?[^)]*\)", re.IGNORECASE),
    re.compile(r",?\s*siehe\s+Anti[-\s]?Halluzinations?[^.;)]*", re.IGNORECASE),
    re.compile(r"\s*—\s*Pattern[-\s]?Audit[-\s]?Anker", re.IGNORECASE),
    re.compile(r"\s*,?\s*gem[äa]ss?\s+globaler\s+Konvention\s+`~/\.claude/[^`]+`", re.IGNORECASE),
    re.compile(r"`~/\.claude/[^`]+`", re.IGNORECASE),
    re.compile(r"Figma[-\s]?Claude", re.IGNORECASE),
    re.compile(r"\bClaude[-\s]?Konvention\w*", re.IGNORECASE),
    re.compile(r"\s*/\s*Skills[-\s]?Battle[-\s]?Skill[-\s]?\d+[-\s]?Kontext", re.IGNORECASE),
    # Specific stray sentences observed in source docs
    re.compile(
        r"Diese Briefings sind als \*\*Copy-Paste-Prompts.*?exportiert PNG nach[^.]*\.\s*",
        re.IGNORECASE | re.DOTALL,
    ),
    re.compile(
        r"Diese Matrix ist die Brücke zwischen[^.]*\.\s*",
        re.IGNORECASE,
    ),
]

# Text replacements (non-link).
PLAIN_REPLACEMENTS = [
    (re.compile(r"\bf[üu]r\s+Figma\s*\+\s*Claude\b", re.IGNORECASE), "für das Mockup-Design"),
    (re.compile(r"\bCopy[-\s]?Paste[-\s]?Prompts?\s+f[üu]r\s+Figma\s*\+\s*Claude", re.IGNORECASE),
     "Mockup-Briefings"),
    (re.compile(r"\bsiehe\s+`?\.claude/[^`\s]+`?", re.IGNORECASE), ""),
]

# Map repo filenames to readable submission-friendly labels.
FILE_LABELS = {
    "use-cases.md":             "Use-Cases-Dokument",
    "validation.md":            "Validation-Regeln",
    "functionality.md":         "Funktionalitäts-Matrix",
    "erm.md":                   "ER-Modell",
    "mockup-briefs.md":         "Mockup-Briefings",
    "test-protocol.md":         "Test-Protokoll",
    "test-protocol.csv":        "Test-Protokoll",
    "test-protocol.xlsx":       "Test-Protokoll",
    "sudoku.sql":               "Datenbank-Skript",
    "class-diagram.md":         "Klassendiagramm",
    "skillsbattle2026_1.1.md":  "Aufgabenstellung",
    "mockups/":                 "Mockup-Verzeichnis",
}


# ---------------------------------------------------------------------------

def heading_level(line: str) -> int | None:
    m = re.match(r"^(#{1,6})\s", line)
    return len(m.group(1)) if m else None


def remove_marked_sections(text: str) -> str:
    """Drop any subsection whose heading matches SECTION_HEADINGS_TO_DROP."""
    lines = text.split("\n")
    out: list[str] = []
    drop_level: int | None = None
    in_code = False
    for line in lines:
        if line.lstrip().startswith("```"):
            in_code = not in_code
            if drop_level is None:
                out.append(line)
            continue
        if in_code:
            if drop_level is None:
                out.append(line)
            continue
        lvl = heading_level(line)
        if drop_level is not None:
            if lvl is not None and lvl <= drop_level:
                drop_level = lvl if SECTION_HEADINGS_TO_DROP.match(line) else None
                if drop_level is None:
                    out.append(line)
        else:
            if lvl is not None and SECTION_HEADINGS_TO_DROP.match(line):
                drop_level = lvl
            else:
                out.append(line)
    return "\n".join(out)


def strip_inline_phrases(text: str) -> str:
    for pat in INLINE_STRIPS:
        text = pat.sub("", text)
    for pat, repl in PLAIN_REPLACEMENTS:
        text = pat.sub(repl, text)
    return text


def looks_like_identifier(text: str) -> bool:
    return bool(re.match(r"^[A-Z]{1,4}-?\d+(\.\d+)?$", text.replace("`", "").strip()))


def _label_from_target(target: str, link_text: str) -> str | None:
    candidate = target.rsplit("/", 1)[-1].split("#", 1)[0]
    if candidate in FILE_LABELS:
        return FILE_LABELS[candidate]
    for key in FILE_LABELS:
        if key in link_text:
            return FILE_LABELS[key]
    return None


def replace_external_link(m: re.Match) -> str:
    """[text](relative-path-or-anchor) → bold label, no link."""
    raw_text = m.group(1)
    target = m.group(2)
    clean = raw_text.replace("`", "").strip()
    if looks_like_identifier(clean):
        return f"**{clean}**"
    label = _label_from_target(target, raw_text)
    if label:
        if clean.endswith((".md", ".sql", ".csv", ".xlsx")) or clean == label \
           or clean.startswith("../") or clean == target.lstrip("./"):
            return f"**{label}**"
        return clean
    return f"**{clean}**"


def sanitize_links_arc42(text: str) -> str:
    """Used by the ARC42 preprocessor where intra-arc42 chapter links must
    be rewritten to #chapter-N anchors.

    Markdown image references (`![alt](path)`) are skipped via the
    `(?<!!)` negative-lookbehind so screenshots remain embedded.
    """
    def repl_chapter(m: re.Match) -> str:
        n = int(m.group(2))
        link_text = m.group(1)
        if re.search(
            r"Anti[-\s]?Halluzinat|Pattern[-\s]?Audit|\bClaude\b|Figma\s*\+\s*Claude",
            link_text,
            re.IGNORECASE,
        ):
            link_text = f"Kapitel {n}"
        target = "#toc" if n == 0 else f"#chapter-{n}"
        return f"[{link_text}]({target})"

    text = re.sub(
        r"(?<!!)\[([^\]]+)\]\((?:\./)?(\d{2})-[A-Za-z0-9_-]+\.md(?:#[^)]*)?\)",
        repl_chapter,
        text,
    )
    text = re.sub(r"(?<!!)\[([^\]]+)\]\((\.\.[^)]*)\)", replace_external_link, text)
    text = _sanitize_bare_backticks(text)
    return text


def sanitize_links_doc(text: str) -> str:
    """Used by the main-Documentation preprocessor. Image references
    (`![alt](path)`) are preserved unchanged."""
    text = re.sub(r"(?<!!)\[([^\]]+)\]\((\.\.[^)]*)\)", replace_external_link, text)
    text = re.sub(r"(?<!!)\[([^\]]+)\]\((\.[^)]*)\)", replace_external_link, text)
    def repl_same_folder(m: re.Match) -> str:
        target = m.group(2)
        return replace_external_link(re.match(
            r"\[([^\]]+)\]\(([^)]+)\)",
            f"[{m.group(1)}]({target})"
        ))
    text = re.sub(
        r"(?<!!)\[([^\]]+)\]\(((?:use-cases|validation|functionality|erm|mockup-briefs"
        r"|test-protocol|class-diagram)\.(?:md|csv|xlsx)(?:#[^)]*)?)\)",
        repl_same_folder,
        text,
    )
    text = _sanitize_bare_backticks(text)
    return text


def _sanitize_bare_backticks(text: str) -> str:
    """Replace bare backtick references to repo files with friendly labels."""
    def repl_bare(m: re.Match) -> str:
        name = m.group(1)
        return f"**{FILE_LABELS.get(name, name)}**"

    return re.sub(
        r"`(use-cases\.md|validation\.md|functionality\.md|erm\.md"
        r"|mockup-briefs\.md|test-protocol\.(?:md|csv|xlsx)"
        r"|class-diagram\.md|sudoku\.sql|skillsbattle2026_1\.1\.md)`",
        repl_bare,
        text,
    )


def light_polish(text: str) -> str:
    text = re.sub(r"\(\s*\)", "", text)
    text = re.sub(r"\(\s*siehe\s*\.?\s*\)", "", text)
    text = re.sub(r"\(\s*[,;:]+\s*\)", "", text)
    text = re.sub(r"\s*,\s*siehe\s*\.\s*", ".", text)
    text = re.sub(r"\s*,\s*\)", ")", text)
    text = re.sub(r"\(\s*,\s*", "(", text)
    text = re.sub(r"  +", " ", text)
    text = re.sub(r"[ \t]+\n", "\n", text)
    text = re.sub(r"\n{4,}", "\n\n\n", text)
    return text


def replace_first_h1_with_anchor(text: str, anchor_id: str, prefix: str | None = None) -> str:
    """Rewrite the first '# ...' line as an HTML heading with explicit id."""
    lines = text.split("\n")
    for i, line in enumerate(lines):
        m = re.match(r"^# (.+?)\s*$", line)
        if m:
            title = m.group(1)
            display = f"{prefix} {title}" if prefix else title
            lines[i] = f'<h1 id="{anchor_id}">{display}</h1>'
            if i + 1 < len(lines) and lines[i + 1].strip():
                lines.insert(i + 1, "")
            return "\n".join(lines)
    return f'<h1 id="{anchor_id}">{prefix or anchor_id}</h1>\n\n' + text
