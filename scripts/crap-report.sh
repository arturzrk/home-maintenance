#!/usr/bin/env bash
# scripts/crap-report.sh — report-only CRAP (Change Risk Anti-Pattern)
# score per method: complexity^2 * (1 - coverage)^3 + complexity. High
# score = complex AND undertested; that combination is the actual risk,
# not complexity alone. This never fails the build -- it's informational.
#
# Pipeline: dotnet tool restore (ReportGenerator) -> build -> unit tests
# with coverage -> ReportGenerator HTML -> backend/tools/CrapReport
# parses that HTML and ranks every method.
#
# Usage: scripts/crap-report.sh [--help]

set -euo pipefail

ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
BACKEND="$ROOT/backend"
OUT_DIR="$ROOT/logs/crap"
HTML_DIR="$OUT_DIR/html"
REPORT_PATH="$OUT_DIR/crap-report.md"

usage() {
  printf '%s\n' "Usage: scripts/crap-report.sh [--help]"
  printf '%s\n' ""
  printf '%s\n' "Builds the backend, runs unit tests with coverage, and ranks every"
  printf '%s\n' "Domain+Application method by CRAP score (complexity^2 * (1-coverage)^3"
  printf '%s\n' "+ complexity). Report-only: never fails the build."
  printf '%s\n' ""
  printf '%s\n' "Output: logs/crap/crap-report.md (plus the top 15 printed to stdout)."
  printf '%s\n' ""
  printf '%s\n' "  --help   show this message"
}

run_pipeline() {
  mkdir -p "$OUT_DIR"
  (cd "$BACKEND" && dotnet tool restore)
  (cd "$BACKEND" && dotnet build --configuration Release)
  (cd "$BACKEND" && dotnet test tests/HomeMaintenance.Unit.Tests --no-build --configuration Release \
    "/p:CollectCoverage=true" \
    "/p:Include=[HomeMaintenance.Domain]*%2c[HomeMaintenance.Application]*" \
    "/p:CoverletOutputFormat=opencover")
  (cd "$BACKEND" && dotnet tool run reportgenerator \
    "-reports:tests/HomeMaintenance.Unit.Tests/coverage.opencover.xml" \
    "-targetdir:$HTML_DIR" \
    "-reporttypes:Html")
  (cd "$BACKEND" && dotnet run --project tools/CrapReport -- "$HTML_DIR" "$REPORT_PATH")
}

parse_args() {
  case "${1:-}" in
    --help|-h) usage; exit 0 ;;
    ?*)        printf 'Unknown option: %s\n\n' "$1" >&2; usage >&2; exit 2 ;;
  esac
}

parse_args "$@"
run_pipeline
