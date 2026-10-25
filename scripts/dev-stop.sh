#!/usr/bin/env bash
# scripts/dev-stop.sh — stop a local dev environment started by
# scripts/dev.sh, using the pid file it writes on startup
# (logs/dev/dev.pids), then stop the MongoDB container. Useful when a
# dev.sh session was left running (e.g. terminal closed without Ctrl+C).
#
# Usage: scripts/dev-stop.sh [--dry-run|--help]
#   --dry-run   show what would be stopped, without stopping anything
#   --help      show this message

set -euo pipefail

ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
PID_FILE="${DEV_PID_FILE:-$ROOT/logs/dev/dev.pids}"
DRY_RUN=0

usage() {
  printf '%s\n' "Usage: scripts/dev-stop.sh [--dry-run|--help]"
  printf '%s\n' ""
  printf '%s\n' "Stops the API/frontend recorded in $PID_FILE (written by"
  printf '%s\n' "scripts/dev.sh on startup), then stops the MongoDB container."
  printf '%s\n' ""
  printf '%s\n' "  --dry-run   show what would be stopped, without stopping anything"
  printf '%s\n' "  --help      show this message"
}

pid_for() {
  [ -f "$PID_FILE" ] || return 0
  sed -n "s/^$1=//p" "$PID_FILE"
}

stop_recorded() {
  local label="$1" name="$2" pid
  pid="$(pid_for "$label")"
  if [ -z "$pid" ]; then
    printf '%s: no recorded pid in %s\n' "$name" "$PID_FILE"
    return 0
  fi
  if ! kill -0 "$pid" 2>/dev/null; then
    printf '%s: pid %s not running (already stopped)\n' "$name" "$pid"
    return 0
  fi
  if [ "$DRY_RUN" = "1" ]; then
    printf '%s: would stop pid %s\n' "$name" "$pid"
    return 0
  fi
  printf '%s: stopping pid %s\n' "$name" "$pid"
  kill -TERM -- "-$pid" 2>/dev/null || true
}

stop_mongo() {
  if [ "$DRY_RUN" = "1" ]; then
    printf 'mongo: would run docker compose stop mongodb\n'
    return 0
  fi
  printf 'mongo: stopping container\n'
  (cd "$ROOT" && docker compose stop mongodb)
}

run_stop() {
  stop_recorded api API
  stop_recorded web frontend
  stop_mongo
  [ "$DRY_RUN" = "1" ] || rm -f "$PID_FILE"
}

parse_args() {
  case "${1:-}" in
    --help|-h) usage; exit 0 ;;
    --dry-run) DRY_RUN=1 ;;
    --*)       printf 'Unknown option: %s\n\n' "$1" >&2; usage >&2; exit 2 ;;
  esac
}

parse_args "$@"
run_stop
