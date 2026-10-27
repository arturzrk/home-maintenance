#!/usr/bin/env bash
# Tests for scripts/frontend-crap-report.sh. Hermetic: npm and node are
# stubbed to no-ops that log their own invocations, so no real test run
# or analysis happens. Run: bash scripts/frontend-crap-report.test.sh

set -uo pipefail

REPO_ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/frontend-crap-report.sh"
BASH_BIN="$(command -v bash)"

failures=""

ok()  { printf 'PASS %s\n' "$1"; }
bad() { printf 'FAIL %s\n' "$1"; failures="${failures}x"; }

STUB_DIR="$(mktemp -d)"
CALL_LOG="$STUB_DIR/calls.log"
trap 'rm -r "$STUB_DIR" 2>/dev/null || true' EXIT

for cmd in npm node; do
  printf '#!/usr/bin/env bash\nprintf "%%s %%s\\n" "%s" "$*" >>"%s"\n' "$cmd" "$CALL_LOG" >"$STUB_DIR/$cmd"
  chmod +x "$STUB_DIR/$cmd"
done

run_script() {
  PATH="$STUB_DIR:/usr/bin:/bin" "$BASH_BIN" "$SCRIPT" "$@"
}

test_help() {
  local out rc
  out="$(run_script --help 2>&1)"; rc=$?
  if [ "$rc" -eq 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "--help exits 0 and prints usage"
  else
    bad "--help (rc=$rc)"
  fi
}

test_unknown_flag() {
  local out rc
  out="$(run_script --bogus 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "unknown flag exits non-zero and prints usage"
  else
    bad "unknown flag (rc=$rc)"
  fi
}

test_single_dash_typo_is_rejected() {
  local out rc
  out="$(run_script -help 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] && printf '%s' "$out" | grep -qi 'usage'; then
    ok "single-dash typo (-help) is rejected, not silently run as the full pipeline"
  else
    bad "single-dash typo (rc=$rc)"
  fi
}

test_runs_pipeline_steps_in_order() {
  rm -f "$CALL_LOG"
  local out rc
  out="$(run_script 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] || [ ! -f "$CALL_LOG" ]; then
    bad "pipeline did not run (rc=$rc, log_exists=$([ -f "$CALL_LOG" ] && echo yes || echo no))"
    return
  fi

  local test_line crap_line
  test_line="$(grep -n '^npm .*--coverage' "$CALL_LOG" | head -1 | cut -d: -f1)"
  crap_line="$(grep -n '^node .*crap-report' "$CALL_LOG" | head -1 | cut -d: -f1)"

  if [ -n "$test_line" ] && [ -n "$crap_line" ] && [ "$test_line" -lt "$crap_line" ]; then
    ok "pipeline runs jest with coverage, then the crap-report analysis, in order"
  else
    bad "pipeline order (test=$test_line crap=$crap_line)"
  fi
}

test_help
test_unknown_flag
test_single_dash_typo_is_rejected
test_runs_pipeline_steps_in_order

if [ -n "$failures" ]; then
  printf '\nFAILED\n'
  exit 1
fi
printf '\nALL TESTS PASSED\n'
