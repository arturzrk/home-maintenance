#!/usr/bin/env bash
# Tests for scripts/dev.sh. Hermetic: prerequisite checks run against stub
# binaries on a controlled PATH — no real docker/dotnet/node needed.
# Run: bash scripts/dev.test.sh

set -uo pipefail

REPO_ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
DEV="$REPO_ROOT/scripts/dev.sh"

failures=""

ok()  { printf 'PASS %s\n' "$1"; }
bad() { printf 'FAIL %s\n' "$1"; failures="${failures}x"; }

# make_stubs <dir> <cmd>... — create no-op executable stubs in <dir>.
make_stubs() {
  local dir="$1"
  shift
  mkdir -p "$dir"
  local cmd
  for cmd in "$@"; do
    (cd "$dir" && printf '#!/usr/bin/env bash\nexit 0\n' >"$cmd" && chmod +x "$cmd")
  done
}

STUB_DIR="$(mktemp -d)"
trap 'rm -r "$STUB_DIR" 2>/dev/null || true' EXIT

test_help() {
  local out rc
  out="$(PATH="$STUB_DIR:/usr/bin:/bin" bash "$DEV" --help 2>&1)"; rc=$?
  if [ "$rc" -eq 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "--help exits 0 and prints usage"
  else
    bad "--help (rc=$rc)"
  fi
}

test_check_all_present() {
  make_stubs "$STUB_DIR" docker dotnet node npm curl
  local out rc
  out="$(PATH="$STUB_DIR:/usr/bin:/bin" bash "$DEV" --check 2>&1)"; rc=$?
  if [ "$rc" -eq 0 ] && printf '%s' "$out" | grep -qi 'ok'; then
    ok "--check passes when all prerequisites are on PATH"
  else
    bad "--check all present (rc=$rc)"
  fi
}

test_check_missing_tool() {
  rm -f "$STUB_DIR/dotnet"
  local out rc
  out="$(PATH="$STUB_DIR:/usr/bin:/bin" bash "$DEV" --check 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] && printf '%s' "$out" | grep -q 'dotnet'; then
    ok "--check fails and names the missing tool"
  else
    bad "--check missing tool (rc=$rc)"
  fi
  make_stubs "$STUB_DIR" dotnet
}

test_unknown_flag() {
  local out rc
  out="$(bash "$DEV" --bogus 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "unknown flag exits non-zero and prints usage"
  else
    bad "unknown flag (rc=$rc)"
  fi
}

test_single_dash_typo_is_rejected() {
  # PATH has no docker/dotnet/node/npm: if the typo were ever misparsed
  # as "no flags" and fell through to run_dev(), check_prereqs() would
  # fail and set -e would abort before anything real could be touched —
  # this test only passes by actually rejecting the flag up front.
  local out rc
  out="$(PATH="/usr/bin:/bin" bash "$DEV" -check 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "single-dash typo (-check) is rejected, not silently run as full dev"
  else
    bad "single-dash typo (rc=$rc)"
  fi
}

test_write_pid_file_records_all_three_pids() {
  local tmp out
  tmp="$(mktemp -d)"
  out="$(bash -c '
    source "$1"
    API_PID=11; WEB_PID=22; TAIL_PID=33
    PID_FILE="$2/dev.pids"
    write_pid_file
    cat "$PID_FILE"
  ' _ "$DEV" "$tmp" 2>&1)"
  rm -rf "$tmp"
  if printf '%s\n' "$out" | grep -qx 'api=11' \
      && printf '%s\n' "$out" | grep -qx 'web=22' \
      && printf '%s\n' "$out" | grep -qx 'tail=33'; then
    ok "write_pid_file records api/web/tail pids"
  else
    bad "write_pid_file ($out)"
  fi
}

test_wait_or_die_success_does_not_cleanup() {
  local out
  out="$(bash -c '
    source "$1"
    wait_for_url() { return 0; }
    cleanup() { printf "CLEANUP_CALLED:%s\n" "${1:-0}"; }
    wait_or_die "http://x" "X" 1 "/dev/null"
    printf "RETURNED:%s\n" "$?"
  ' _ "$DEV" 2>&1)"
  if printf '%s' "$out" | grep -q '^RETURNED:0$' && ! printf '%s' "$out" | grep -q 'CLEANUP_CALLED'; then
    ok "wait_or_die returns success without invoking cleanup"
  else
    bad "wait_or_die success ($out)"
  fi
}

test_wait_or_die_failure_invokes_cleanup() {
  local out
  out="$(bash -c '
    source "$1"
    wait_for_url() { return 1; }
    cleanup() { printf "CLEANUP_CALLED:%s\n" "${1:-0}"; }
    wait_or_die "http://x" "X" 1 "/dev/null"
  ' _ "$DEV" 2>&1)"
  if printf '%s' "$out" | grep -q '^CLEANUP_CALLED:1$'; then
    ok "wait_or_die on failure logs and invokes cleanup with code 1"
  else
    bad "wait_or_die failure ($out)"
  fi
}

test_help
test_check_all_present
test_check_missing_tool
test_unknown_flag
test_single_dash_typo_is_rejected
test_write_pid_file_records_all_three_pids
test_wait_or_die_success_does_not_cleanup
test_wait_or_die_failure_invokes_cleanup

if [ -n "$failures" ]; then
  printf '\nFAILED\n'
  exit 1
fi
printf '\nALL TESTS PASSED\n'
