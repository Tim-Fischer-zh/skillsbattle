#!/usr/bin/env bash
# Build the polished standalone Documentation.pdf (Spec §2.6).

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
ARC_PDF_DIR="$REPO/docs/arc42_pdf"      # shares the print stylesheet
OUT_DIR="$REPO/docs/build"
mkdir -p "$OUT_DIR"

# 1) refresh sanitized section files
python3 "$HERE/_preprocess.py"

# 2) concatenate in correct order
COMBINED="$HERE/_combined.md"
PAGE_BREAK='<div class="page-break"></div>'
{
  cat "$HERE/_00-cover.md"
  echo
  cat "$HERE/_01-toc.md"
  echo
  for f in 01-section-1 02-section-2 03-section-3 04-section-4 05-section-5 06-section-6; do
    echo "$PAGE_BREAK"
    echo
    cat "$HERE/${f}.md"
    echo
  done
} > "$COMBINED"
echo "[1] combined → $COMBINED  ($(wc -l < "$COMBINED") lines)"

# 3) render mermaid blocks
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

# 4) reuse the arc42_pdf print stylesheet for a consistent look
CSS="$ARC_PDF_DIR/_print.css"
if [ ! -f "$CSS" ]; then
  echo "ERROR: stylesheet $CSS not found — run arc42_pdf/build.sh first" >&2
  exit 1
fi

# 5) md-to-pdf with page numbers
PDF_OPTIONS='{
  "format": "A4",
  "margin": {"top": "22mm", "right": "18mm", "bottom": "22mm", "left": "18mm"},
  "printBackground": true,
  "displayHeaderFooter": true,
  "headerTemplate": "<div style=\"font-size:8pt; color:#7a8499; width:100%; padding:0 18mm; display:flex; justify-content:space-between;\"><span>Killer Sudoku · Projekt-Dokumentation</span><span></span></div>",
  "footerTemplate": "<div style=\"font-size:8pt; color:#7a8499; width:100%; padding:0 18mm; text-align:center;\">Seite <span class=\"pageNumber\"></span> von <span class=\"totalPages\"></span></div>"
}'

echo "[3] rendering PDF..."
npx --yes md-to-pdf \
  --stylesheet "$CSS" \
  --pdf-options "$PDF_OPTIONS" \
  "$RENDERED"

mv "$HERE/_rendered.pdf" "$OUT_DIR/Documentation.pdf"

echo
echo "========================================="
echo "  $OUT_DIR/Documentation.pdf"
ls -lh "$OUT_DIR/Documentation.pdf"
echo "========================================="
