#!/usr/bin/env bash
# Build a polished standalone ARC42 PDF from the sanitized chapter files in this folder.
#
# Pipeline:
#   1. (Re-)run _preprocess.py to refresh sanitized chapter copies.
#   2. Concatenate _00-cover → _01-toc → 01..12 into one combined.md.
#   3. mmdc renders embedded mermaid blocks → SVG.
#   4. md-to-pdf (headless Chrome) renders the final PDF with cover + page numbers.

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
OUT_DIR="$REPO/docs/build"
mkdir -p "$OUT_DIR"

# 1) refresh sanitized files
python3 "$HERE/_preprocess.py"

# 2) concatenate in correct order
COMBINED="$HERE/_combined.md"
PAGE_BREAK='<div class="page-break"></div>'
{
  cat "$HERE/_00-cover.md"
  echo
  cat "$HERE/_01-toc.md"
  echo
  for f in 01-introduction 02-constraints 03-context 04-solution-strategy \
           05-building-blocks 06-runtime-view 07-deployment 08-cross-cutting \
           09-decisions 10-quality 11-risks 12-glossary; do
    echo "$PAGE_BREAK"
    echo
    cat "$HERE/${f}.md"
    echo
  done
} > "$COMBINED"
echo "[1] combined → $COMBINED  ($(wc -l < "$COMBINED") lines)"

# 3) render mermaid via mermaid-cli with locally installed Chrome
PUP_CFG="$HERE/_puppeteer.json"
CHROME_BIN="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
cat > "$PUP_CFG" <<EOF
{
  "executablePath": "$CHROME_BIN",
  "args": ["--no-sandbox", "--disable-setuid-sandbox"]
}
EOF
export PUPPETEER_EXECUTABLE_PATH="$CHROME_BIN"
export PUPPETEER_SKIP_DOWNLOAD=true

RENDERED="$HERE/_rendered.md"
echo "[2] rendering mermaid blocks..."
npx --yes -p @mermaid-js/mermaid-cli@10 mmdc \
  --puppeteerConfigFile "$PUP_CFG" \
  --backgroundColor white \
  --input "$COMBINED" \
  --output "$RENDERED" > /dev/null

# 4) print stylesheet — professional book-like layout
CSS="$HERE/_print.css"
cat > "$CSS" <<'EOF'
@page {
  size: A4;
  margin: 22mm 18mm 22mm 18mm;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  font-size: 10.5pt;
  line-height: 1.55;
  color: #1d1d1d;
  max-width: none;
}

/* Cover page */
.cover-page {
  height: 245mm;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  text-align: center;
  page-break-after: always;
}
.cover-title {
  font-size: 46pt;
  font-weight: 800;
  letter-spacing: -0.5px;
  color: #0d2c54;
  margin-bottom: 8px;
}
.cover-subtitle {
  font-size: 22pt;
  color: #2a4d7a;
  font-weight: 400;
  margin-bottom: 6px;
}
.cover-standard {
  font-size: 13pt;
  color: #6a7c93;
  margin-bottom: 60px;
  font-style: italic;
}
.cover-meta {
  margin-top: 60px;
  width: 80%;
  max-width: 480px;
}
.cover-meta table {
  width: 100%;
  border-collapse: collapse;
  font-size: 11pt;
}
.cover-meta th, .cover-meta td {
  border: none;
  padding: 6px 12px;
  text-align: left;
}
.cover-meta td:first-child {
  width: 35%;
  color: #6a7c93;
  font-weight: 500;
}

/* Manual page-break helper */
.page-break {
  page-break-after: always;
}

/* Headings */
h1 {
  font-size: 22pt;
  color: #0d2c54;
  border-bottom: 2px solid #0d2c54;
  padding-bottom: 6px;
  margin-top: 0;
  page-break-before: always;
  page-break-after: avoid;
}
.cover-page h1, h1:first-of-type { page-break-before: auto; }

h2 {
  font-size: 15pt;
  color: #1a4d8c;
  margin-top: 24px;
  page-break-after: avoid;
}
h3 {
  font-size: 12.5pt;
  color: #2a4d7a;
  margin-top: 16px;
  page-break-after: avoid;
}
h4 {
  font-size: 11pt;
  color: #333;
  margin-top: 12px;
}

/* Inline + block code */
code {
  font-family: "SF Mono", Menlo, Consolas, monospace;
  font-size: 9pt;
  background: #f4f6fa;
  padding: 1px 5px;
  border-radius: 3px;
  border: 1px solid #e3e7ef;
}
pre {
  font-family: "SF Mono", Menlo, Consolas, monospace;
  font-size: 8.8pt;
  line-height: 1.4;
  background: #f7f9fc;
  border: 1px solid #dde3ed;
  padding: 10px 12px;
  border-radius: 5px;
  overflow-x: auto;
  page-break-inside: avoid;
}
pre code {
  background: transparent;
  border: 0;
  padding: 0;
  font-size: inherit;
}

/* Tables */
table {
  border-collapse: collapse;
  width: 100%;
  margin: 10px 0 14px 0;
  font-size: 9.5pt;
  page-break-inside: avoid;
}
th, td {
  border: 1px solid #c8d1de;
  padding: 5px 9px;
  text-align: left;
  vertical-align: top;
}
th {
  background: #eef3fa;
  font-weight: 600;
  color: #1a4d8c;
}
tr:nth-child(even) td { background: #fafbfd; }

/* Mermaid SVGs */
img, svg {
  max-width: 100%;
  height: auto;
  display: block;
  margin: 12px auto;
  page-break-inside: avoid;
}

/* Blockquotes — Spec citations etc. */
blockquote {
  border-left: 3px solid #5587c6;
  background: #eff4fb;
  padding: 8px 14px;
  margin: 10px 0;
  color: #2b3e5b;
  font-style: normal;
  border-radius: 0 3px 3px 0;
}
blockquote code { background: #dbe6f4; }

hr {
  border: 0;
  border-top: 1px solid #d4dbe6;
  margin: 16px 0;
}

a { color: #0d52a8; text-decoration: none; }
a:hover { text-decoration: underline; }

/* List spacing */
li { margin: 2px 0; }
ul, ol { margin: 6px 0 10px 0; padding-left: 24px; }
EOF

# 5) md-to-pdf with header/footer for page numbers
PDF_OPTIONS='{
  "format": "A4",
  "margin": {"top": "22mm", "right": "18mm", "bottom": "22mm", "left": "18mm"},
  "printBackground": true,
  "displayHeaderFooter": true,
  "headerTemplate": "<div style=\"font-size:8pt; color:#7a8499; width:100%; padding:0 18mm; display:flex; justify-content:space-between;\"><span>Killer Sudoku · Architecture Documentation</span><span class=\"title\"></span></div>",
  "footerTemplate": "<div style=\"font-size:8pt; color:#7a8499; width:100%; padding:0 18mm; text-align:center;\">Seite <span class=\"pageNumber\"></span> von <span class=\"totalPages\"></span></div>"
}'

echo "[3] rendering PDF..."
npx --yes md-to-pdf \
  --stylesheet "$CSS" \
  --pdf-options "$PDF_OPTIONS" \
  "$RENDERED"

mv "$HERE/_rendered.pdf" "$OUT_DIR/Architecture-ARC42.pdf"

echo
echo "========================================="
echo "  $OUT_DIR/Architecture-ARC42.pdf"
ls -lh "$OUT_DIR/Architecture-ARC42.pdf"
echo "========================================="
