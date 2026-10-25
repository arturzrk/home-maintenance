#!/usr/bin/env bash
# Tests for scripts/dev-stop.sh. Hermetic: stops real (but test-spawned,
# harmless) sleep processes recorded in a throwaway pid file, and stubs
# `docker` so no real container is touched. Run: bash scripts/dev-stop.test.sh

set -uo pipefail
set -m  # give spawned test processes their own process group, like dev.sh does

REPO_ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
STOP="$REPO_ROOT/scripts/dev-stop.sh"

failures=""

ok()  { printf 'PASS %s\n' "$1"; }
bad() { printf 'FAIL %s\n' "$1"; failures="${failures}x"; }

STUB_DIR="$(mktemp -d)"
WORK_DIR="$(mktemp -d)"
PID_FILE="$WORK_DIR/logs/dev/dev.pids"
DOCKER_LOG="$STUB_DIR/docker-calls.log"
SPAWNED=""

mkdir -p "$WORK_DIR/logs/dev"

cleanup_all() {
  for pid in $SPAWNED; do
    kill -TERM -- "-$pid" 2>/dev/null || true
  done
  rm -r "$STUB_DIR" "$WORK_DIR" 2>/dev/null || true
}
trap cleanup_all EXIT

DOCKER_FAIL_FLAG="$STUB_DIR/docker-should-fail"
printf '#!/usr/bin/env bash\nprintf "%%s\\n" "$*" >>"%s"\n[ -f "%s" ] && exit 1\nexit 0\n' \
  "$DOCKER_LOG" "$DOCKER_FAIL_FLAG" >"$STUB_DIR/docker"
chmod +x "$STUB_DIR/docker"
STUB_PATH="$STUB_DIR:/usr/bin:/bin"

# spawn_sleeper — starts a harmless background `sleep` in its own process
# group, setting SLEEPER_PID. Must be called directly (not via command
# substitution) — $(...) runs in a subshell with job control disabled,
# so the child would not get its own process group there.
spawn_sleeper() {
  sleep 100 >/dev/null 2>&1 &
  SLEEPER_PID=$!
  SPAWNED="$SPAWNED $SLEEPER_PID"
}

run_stop() {
  PATH="$STUB_PATH" DEV_PID_FILE="$PID_FILE" bash "$STOP" "$@"
}

test_help() {
  local out rc
  out="$(run_stop --help 2>&1)"; rc=$?
  if [ "$rc" -eq 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "--help exits 0 and prints usage"
  else
    bad "--help (rc=$rc)"
  fi
}

test_no_pid_file() {
  rm -f "$PID_FILE" "$DOCKER_LOG"
  local out rc
  out="$(run_stop 2>&1)"; rc=$?
  if [ "$rc" -eq 0 ] \
      && printf '%s' "$out" | grep -q 'API: no recorded pid' \
      && printf '%s' "$out" | grep -q 'frontend: no recorded pid' \
      && [ -f "$DOCKER_LOG" ] && grep -q 'compose stop mongodb' "$DOCKER_LOG"; then
    ok "no pid file: reports nothing recorded, still stops mongo"
  else
    bad "no pid file (rc=$rc)"
  fi
}

test_stops_live_recorded_pid() {
  spawn_sleeper
  local pid="$SLEEPER_PID"
  printf 'api=%s\n' "$pid" >"$PID_FILE"
  rm -f "$DOCKER_LOG"
  local out rc
  out="$(run_stop 2>&1)"; rc=$?
  sleep 0.3
  if [ "$rc" -eq 0 ] \
      && printf '%s' "$out" | grep -q "API: stopping pid $pid" \
      && ! kill -0 "$pid" 2>/dev/null \
      && [ ! -f "$PID_FILE" ]; then
    ok "default run stops a live recorded pid and clears the pid file"
  else
    bad "stop live pid (rc=$rc, still alive=$(kill -0 "$pid" 2>/dev/null && echo yes || echo no))"
  fi
}

test_dry_run_leaves_live_pid_running() {
  spawn_sleeper
  local pid="$SLEEPER_PID"
  printf 'api=%s\n' "$pid" >"$PID_FILE"
  rm -f "$DOCKER_LOG"
  local out rc
  out="$(run_stop --dry-run 2>&1)"; rc=$?
  if [ "$rc" -eq 0 ] \
      && printf '%s' "$out" | grep -q "API: would stop pid $pid" \
      && kill -0 "$pid" 2>/dev/null \
      && [ -f "$PID_FILE" ] \
      && [ ! -f "$DOCKER_LOG" ]; then
    ok "--dry-run reports the pid without stopping it or touching mongo"
  else
    bad "--dry-run (rc=$rc)"
  fi
  kill -TERM -- "-$pid" 2>/dev/null || true
}

test_stale_pid_reported_as_already_stopped() {
  printf 'api=999999\n' >"$PID_FILE"
  local out rc
  out="$(run_stop 2>&1)"; rc=$?
  if [ "$rc" -eq 0 ] && printf '%s' "$out" | grep -q 'already stopped'; then
    ok "stale/dead pid is reported as already stopped, not an error"
  else
    bad "stale pid (rc=$rc)"
  fi
}

test_tail_label_is_stopped() {
  spawn_sleeper
  local pid="$SLEEPER_PID"
  printf 'tail=%s\n' "$pid" >"$PID_FILE"
  rm -f "$DOCKER_LOG"
  local out rc
  out="$(run_stop 2>&1)"; rc=$?
  sleep 0.3
  if [ "$rc" -eq 0 ] \
      && printf '%s' "$out" | grep -q "log tail: stopping pid $pid" \
      && ! kill -0 "$pid" 2>/dev/null; then
    ok "tail label is stopped alongside api/web"
  else
    bad "tail label (rc=$rc)"
  fi
}

test_mongo_failure_still_clears_pid_file() {
  spawn_sleeper
  local pid="$SLEEPER_PID"
  printf 'api=%s\n' "$pid" >"$PID_FILE"
  rm -f "$DOCKER_LOG"
  touch "$DOCKER_FAIL_FLAG"
  local out rc
  out="$(run_stop 2>&1)"; rc=$?
  rm -f "$DOCKER_FAIL_FLAG"
  sleep 0.3
  if [ "$rc" -ne 0 ] \
      && printf '%s' "$out" | grep -q "API: stopping pid $pid" \
      && ! kill -0 "$pid" 2>/dev/null \
      && [ ! -f "$PID_FILE" ]; then
    ok "a failing mongo stop still stops recorded pids and clears the pid file"
  else
    bad "mongo failure (rc=$rc, pid_file_exists=$([ -f "$PID_FILE" ] && echo yes || echo no))"
  fi
}

test_unknown_flag() {
  local out rc
  out="$(run_stop --bogus 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "unknown flag exits non-zero and prints usage"
  else
    bad "unknown flag (rc=$rc)"
  fi
}

test_help
test_no_pid_file
test_stops_live_recorded_pid
test_dry_run_leaves_live_pid_running
test_stale_pid_reported_as_already_stopped
test_tail_label_is_stopped
test_mongo_failure_still_clears_pid_file
test_unknown_flag

if [ -n "$failures" ]; then
  printf '\nFAILED\n'
  exit 1
fi
printf '\nALL TESTS PASSED\n'
