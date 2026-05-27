"""
Parse docs/test-protocol.md and emit docs/test-protocol.csv (Excel-importable).
Uses only stdlib (no openpyxl needed). Excel opens CSV natively.

Run from repo root: python3 docs/_build_csv.py
"""
import csv
import re
from pathlib import Path

MD = Path(__file__).parent / "test-protocol.md"
CSV = Path(__file__).parent / "test-protocol.csv"

current_uc = "X"
rows = []

uc_pattern = re.compile(r"^#{1,3} (UC\d{2}|Manual Test Cases)")
sep_pattern = re.compile(r"^\|\s*-+\s*\|")
header_pattern = re.compile(r"^\|\s*ID\s*\|", re.IGNORECASE)

with MD.open("r", encoding="utf-8") as f:
    for raw in f:
        line = raw.rstrip("\n")
        m = uc_pattern.search(line)
        if m:
            label = m.group(1)
            current_uc = "Manual" if label.startswith("Manual") else label
            continue
        if not line.startswith("|"):
            continue
        if sep_pattern.match(line) or header_pattern.match(line):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if not cells or not cells[0]:
            continue
        if not re.match(r"^[TM]\d+$", cells[0]):
            continue
        # Manual section has fewer columns: ID,Title,Type,Steps,Expected,Priority
        # UC sections have: ID,Title,Type,Framework,Preconditions,Steps,TestData,Expected,Priority
        if current_uc == "Manual":
            # pad to UC-shape
            padded = [
                cells[0],            # ID
                cells[1],            # Title
                cells[2],            # Type (M)
                "Excel-Steps",       # Framework placeholder
                "",                  # Preconditions
                cells[3] if len(cells) > 3 else "",  # Steps
                "",                  # TestData
                cells[4] if len(cells) > 4 else "",  # Expected
                cells[5] if len(cells) > 5 else "",  # Priority
            ]
            rows.append([current_uc] + padded)
        elif len(cells) >= 9:
            rows.append([current_uc] + cells[:9])
        else:
            # skip malformed
            continue

with CSV.open("w", encoding="utf-8-sig", newline="") as f:  # utf-8-sig → Excel BOM
    w = csv.writer(f, delimiter=";")  # semicolon: better for European Excel
    w.writerow([
        "UC", "TestID", "Title", "Type", "Framework",
        "Preconditions", "Steps", "TestData", "Expected", "Priority"
    ])
    w.writerows(rows)

# Quick stats
counts = {}
for r in rows:
    counts[r[3]] = counts.get(r[3], 0) + 1
print(f"Wrote {len(rows)} test cases to {CSV}")
print(f"Type breakdown: {counts}")
