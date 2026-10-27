#!/usr/bin/env bash
# scripts/frontend-crap-report.sh — report-only CRAP (Change Risk
# Anti-Pattern) score per function: complexity^2 * (1 - coverage)^3 +
# complexity. High score = complex AND undertested; that combination is
# the actual risk, not complexity alone. This never fails the build --
# it's informational. Frontend sibling of scripts/crap-report.sh.
#
# Pipeline: jest with coverage (coverage-final.json) -> frontend/tools/
# crap-report combines that with ESLint's complexity rule and ranks
# every function.
#
# Usage: scripts/frontend-crap-report.sh [--help]

set -euo pipefail

ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
FRONTEND="$ROOT/frontend"
OUT_DIR="$ROOT/logs/crap"
REPORT_PATH="$OUT_DIR/frontend-crap-report.md"

usage() {
  printf '%s\n' "Usage: scripts/frontend-crap-report.sh [--help]"
  printf '%s\n' ""
  printf '%s\n' "Runs the frontend Jest suite with coverage, then ranks every"
  printf '%s\n' "function by CRAP score (complexity^2 * (1-coverage)^3 + complexity)."
  printf '%s\n' "Report-only: never fails the build."
  printf '%s\n' ""
  printf '%s\n' "Output: logs/crap/frontend-crap-report.md (full) and"
  printf '%s\n' "logs/crap/frontend-crap-report-top15.md (just the top 15; the same"
  printf '%s\n' "top 15 is also printed to stdout)."
  printf '%s\n' ""
  printf '%s\n' "  --help   show this message"
}

run_pipeline() {
  mkdir -p "$OUT_DIR"
  (cd "$FRONTEND" && npm test -- --coverage --coverageReporters=json --passWithNoTests)
  (cd "$FRONTEND" && node tools/crap-report/index.mjs coverage/coverage-final.json "$REPORT_PATH")
}

parse_args() {
  case "${1:-}" in
    --help|-h) usage; exit 0 ;;
    ?*)        printf 'Unknown option: %s\n\n' "$1" >&2; usage >&2; exit 2 ;;
  esac
}

parse_args "$@"
run_pipeline
