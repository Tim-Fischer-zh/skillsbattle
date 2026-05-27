#!/usr/bin/env bash
# Build the AppDev_Fischer_Tim submission folder.
#
# Rebuilds Documentation.pdf and Architecture-ARC42.pdf, then copies the
# deliverables into AppDev_Fischer_Tim/.  After this script the folder is
# ready to be zipped as AppDev_Fischer_Tim.zip for upload.

set -euo pipefail

REPO="$(cd "$(dirname "$0")" && pwd)"
BUILD="$REPO/docs/build"
SUB="$REPO/AppDev_Fischer_Tim"

echo "→ rebuilding Architecture-ARC42.pdf ..."
bash "$REPO/docs/arc42_pdf/build.sh" > /dev/null

echo "→ rebuilding Documentation.pdf ..."
bash "$REPO/docs/doc_pdf/build.sh"   > /dev/null

echo "→ assembling submission folder ..."
mkdir -p "$SUB" "$SUB/src" "$SUB/bin"

cp -v "$BUILD/Documentation.pdf"          "$SUB/Documentation.pdf"
cp -v "$BUILD/Architecture-ARC42.pdf"     "$SUB/Architecture-ARC42.pdf"
cp -v "$REPO/db/sudoku.sql"               "$SUB/sudoku.sql"
cp -v "$REPO/docs/test-protocol.xlsx"     "$SUB/test-protocol.xlsx"

echo
echo "submission folder ready:"
ls -lh "$SUB/"
echo
echo "next step:"
echo "  cd $REPO"
echo "  zip -r AppDev_Fischer_Tim.zip AppDev_Fischer_Tim"
