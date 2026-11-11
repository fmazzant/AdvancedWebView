#!/usr/bin/env bash
# Waits until the app posts /done (or the timeout), then saves the results and the Markdown report.
# Usage: run-scenarios.sh <platform title> <output dir>
set -u
title="$1"
outdir="$2"
mkdir -p "$outdir"

for i in $(seq 1 60); do
  status=$(curl -s http://127.0.0.1:5100/status || true)
  if [ "$status" = "done" ]; then break; fi
  sleep 5
done
echo "status after wait: ${status:-none}"

curl -s http://127.0.0.1:5100/results > "$outdir/results.json"
python3 "$(dirname "$0")/report.py" "$outdir/results.json" "$title" > "$outdir/report.md"
cat "$outdir/report.md"
cat "$outdir/report.md" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
[ "$status" = "done" ]
