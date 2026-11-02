#!/usr/bin/env bash
# scripts/complexity-gate.sh — hard CI gate: fails the build if any
# Domain/Application method's cyclomatic complexity exceeds the limit.
#
# Unlike crap-report.sh (informational, never fails the build), this is
# the actual enforcement mechanism behind CLAUDE.md's "keep cyclomatic
# complexity below 5" rule. The limit passed to ComplexityGate is a
# ratchet, not that target: see backend/tools/ComplexityGate/Program.cs
# for why 20, not 5, today.
#
# Pipeline: dotnet tool restore (ReportGenerator) -> build -> unit tests
# with coverage -> ReportGenerator HTML -> backend/tools/ComplexityGate
# checks every method against the limit.
#
# Usage: scripts/complexity-gate.sh [--help]

set -euo pipefail

ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
BACKEND="$ROOT/backend"
HTML_DIR="$ROOT/logs/crap/html"
MAX_COMPLEXITY=20

usage() {
  printf '%s\n' "Usage: scripts/complexity-gate.sh [--help]"
  printf '%s\n' ""
  printf '%s\n' "Builds the backend, runs unit tests with coverage, and fails if any"
  printf '%s\n' "Domain+Application method's cyclomatic complexity exceeds $MAX_COMPLEXITY."
  printf '%s\n' ""
  printf '%s\n' "  --help   show this message"
}

run_pipeline() {
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
  (cd "$BACKEND" && dotnet run --project tools/ComplexityGate -- "$HTML_DIR" "$MAX_COMPLEXITY")
}

parse_args() {
  case "${1:-}" in
    --help|-h) usage; exit 0 ;;
    ?*)        printf 'Unknown option: %s\n\n' "$1" >&2; usage >&2; exit 2 ;;
  esac
}

parse_args "$@"
run_pipeline
