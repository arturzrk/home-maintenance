#!/usr/bin/env bash
# scripts/dev.sh — run the full local dev environment:
#   MongoDB  (docker compose, port 27017)
#   API      (dotnet run, http://localhost:5000)
#   Frontend (next dev,  http://localhost:3000)
#
# Ctrl+C stops the API and frontend. MongoDB is left running
# (stop it with: docker compose stop mongodb).
#
# Usage: scripts/dev.sh [--check|--help]
#   --check   verify prerequisites only
#   --help    show usage

set -euo pipefail
set -m  # background jobs get own process groups, so cleanup stops whole trees

ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
LOG_DIR="$ROOT/logs/dev"
PID_FILE="$LOG_DIR/dev.pids"
API_URL="http://localhost:5000"
WEB_URL="http://localhost:3000"

API_PID=""
WEB_PID=""
TAIL_PID=""

usage() {
  printf '%s\n' "Usage: scripts/dev.sh [--check|--help]"
  printf '%s\n' ""
  printf '%s\n' "Starts MongoDB (docker compose), API ($API_URL), frontend ($WEB_URL)."
  printf '%s\n' "Logs: logs/dev/api.log and logs/dev/web.log (tailed live once both up)."
  printf '%s\n' "Ctrl+C stops API + frontend; MongoDB keeps running (docker compose stop mongodb)."
  printf '%s\n' ""
  printf '%s\n' "  --check   verify prerequisites only"
  printf '%s\n' "  --help    show this message"
}

check_prereqs() {
  local missing=""
  local cmd
  for cmd in docker dotnet node npm curl; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
      missing="$missing $cmd"
    fi
  done
  if [ -n "$missing" ]; then
    printf 'Missing prerequisites:%s\n' "$missing" >&2
    return 1
  fi
  printf 'Prerequisites OK: docker dotnet node npm curl\n'
}

wait_for_url() {
  local url="$1" name="$2" secs="${3:-90}"
  local i
  for i in $(seq 1 "$secs"); do
    if curl -sf "$url" >/dev/null 2>&1; then
      printf '%s ready at %s\n' "$name" "$url"
      return 0
    fi
    sleep 1
  done
  printf 'Timed out waiting for %s at %s\n' "$name" "$url" >&2
  return 1
}

kill_group() {
  [ -z "$1" ] && return 0
  kill -TERM -- "-$1" 2>/dev/null || true
}

cleanup() {
  local code="${1:-0}"
  trap - INT TERM
  printf '\nStopping API and frontend...\n'
  kill_group "$TAIL_PID"
  kill_group "$API_PID"
  kill_group "$WEB_PID"
  wait 2>/dev/null || true
  rm -f "$PID_FILE"
  printf 'Done. MongoDB still running — stop it with: docker compose stop mongodb\n'
  exit "$code"
}

banner() {
  printf '%s\n' ""
  printf '%s\n' "Dev environment is up:"
  printf '%s\n' "  API:      $API_URL (health: $API_URL/health)"
  printf '%s\n' "  Frontend: $WEB_URL"
  printf '%s\n' "  Logs:     logs/dev/api.log, logs/dev/web.log"
  printf '%s\n' ""
  printf '%s\n' "Ctrl+C to stop API + frontend."
}

start_mongo() {
  printf 'Starting MongoDB...\n'
  (cd "$ROOT" && docker compose up mongodb -d --wait)
}

start_api() {
  printf 'Starting API...\n'
  dotnet run --project "$ROOT/backend/src/HomeMaintenance.API" >"$LOG_DIR/api.log" 2>&1 &
  API_PID=$!
}

start_frontend() {
  printf 'Starting frontend...\n'
  (cd "$ROOT/frontend" && npm run dev) >"$LOG_DIR/web.log" 2>&1 &
  WEB_PID=$!
}

write_pid_file() {
  {
    printf 'api=%s\n' "$API_PID"
    printf 'web=%s\n' "$WEB_PID"
    printf 'tail=%s\n' "$TAIL_PID"
  } >"$PID_FILE"
}

wait_for_api_and_frontend() {
  if ! wait_for_url "$API_URL/health" "API" 120; then
    tail -n 30 "$LOG_DIR/api.log" >&2 || true
    cleanup 1
  fi
  if ! wait_for_url "$WEB_URL" "frontend" 90; then
    tail -n 30 "$LOG_DIR/web.log" >&2 || true
    cleanup 1
  fi
}

run_dev() {
  check_prereqs
  mkdir -p "$LOG_DIR"
  trap cleanup INT TERM
  start_mongo
  start_api
  start_frontend
  wait_for_api_and_frontend
  tail -f "$LOG_DIR/api.log" "$LOG_DIR/web.log" &
  TAIL_PID=$!
  write_pid_file
  banner
  wait
}

parse_args() {
  case "${1:-}" in
    --help|-h) usage; exit 0 ;;
    --check)   check_prereqs; exit $? ;;
    --*)       printf 'Unknown option: %s\n\n' "$1" >&2; usage >&2; exit 2 ;;
  esac
}

parse_args "$@"
run_dev
