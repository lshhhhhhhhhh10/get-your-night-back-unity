#!/bin/sh
# Requires Mono. Executes only documentation export, never gameplay or Unity asset import.
set -eu
PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
EXPORT_TMP=$(mktemp -d)
trap 'rm -rf "$EXPORT_TMP"' EXIT
mcs -langversion:latest -out:"$EXPORT_TMP/export.exe" "$PROJECT_DIR/tools/ExportDocsOffline.cs" "$PROJECT_DIR/Assets/CGDD/Editor/TraceDocsExporter.cs" "$PROJECT_DIR/Assets/CGDD/Editor/CgddBootstrap.cs"
mono "$EXPORT_TMP/export.exe" "$PROJECT_DIR"
