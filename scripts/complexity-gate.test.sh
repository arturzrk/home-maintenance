#!/usr/bin/env bash
# Tests for scripts/complexity-gate.sh. Hermetic: dotnet is stubbed to a
# no-op that logs its own invocations, so no real build/test/report
# generation happens. Run: bash scripts/complexity-gate.test.sh

set -uo pipefail

REPO_ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/complexity-gate.sh"
BASH_BIN="$(command -v bash)"

failures=""

ok()  { printf 'PASS %s\n' "$1"; }
bad() { printf 'FAIL %s\n' "$1"; failures="${failures}x"; }

STUB_DIR="$(mktemp -d)"
DOTNET_LOG="$STUB_DIR/dotnet-calls.log"
trap 'rm -r "$STUB_DIR" 2>/dev/null || true' EXIT

# Stub `dotnet` logs its full argument list (one call per line) and exits
# 0, except a run of ComplexityGate itself: that exit code is controlled
# by $STUB_GATE_EXIT so tests can simulate the gate passing or failing
# without a real build/report.
cat >"$STUB_DIR/dotnet" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "$*" >>"__DOTNET_LOG__"
if [[ "$*" == *"ComplexityGate"* ]]; then
  exit "${STUB_GATE_EXIT:-0}"
fi
exit 0
EOF
sed -i.bak "s|__DOTNET_LOG__|$DOTNET_LOG|" "$STUB_DIR/dotnet" && rm -f "$STUB_DIR/dotnet.bak"
chmod +x "$STUB_DIR/dotnet"

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
  rm -f "$DOTNET_LOG"
  local out rc
  out="$(STUB_GATE_EXIT=0 run_script 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] || [ ! -f "$DOTNET_LOG" ]; then
    bad "pipeline did not run (rc=$rc, log_exists=$([ -f "$DOTNET_LOG" ] && echo yes || echo no))"
    return
  fi

  local build_line test_line restore_line report_line gate_line
  build_line="$(grep -n '^build ' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  test_line="$(grep -n '^test .*CollectCoverage=true' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  restore_line="$(grep -n '^tool restore' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  report_line="$(grep -n 'reportgenerator' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  gate_line="$(grep -n 'run --project .*ComplexityGate' "$DOTNET_LOG" | head -1 | cut -d: -f1)"

  if [ -n "$build_line" ] && [ -n "$test_line" ] && [ -n "$restore_line" ] \
      && [ -n "$report_line" ] && [ -n "$gate_line" ] \
      && [ "$build_line" -lt "$test_line" ] \
      && [ "$test_line" -lt "$report_line" ] \
      && [ "$report_line" -lt "$gate_line" ]; then
    ok "pipeline invokes restore, build, test(+coverage), reportgenerator, then ComplexityGate in order"
  else
    bad "pipeline order (build=$build_line test=$test_line restore=$restore_line report=$report_line gate=$gate_line)"
  fi
}

test_gate_pass_exits_zero() {
  local rc
  STUB_GATE_EXIT=0 run_script >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 0 ]; then
    ok "script exits 0 when ComplexityGate passes"
  else
    bad "script should exit 0 when ComplexityGate passes (rc=$rc)"
  fi
}

test_gate_failure_propagates() {
  local rc
  STUB_GATE_EXIT=1 run_script >/dev/null 2>&1; rc=$?
  if [ "$rc" -ne 0 ]; then
    ok "script exits non-zero when ComplexityGate fails (actually fails the build, unlike crap-report.sh)"
  else
    bad "script should propagate ComplexityGate's failure (rc=$rc)"
  fi
}

test_help
test_unknown_flag
test_single_dash_typo_is_rejected
test_runs_pipeline_steps_in_order
test_gate_pass_exits_zero
test_gate_failure_propagates

if [ -n "$failures" ]; then
  printf '\nFAILED\n'
  exit 1
fi
printf '\nALL TESTS PASSED\n'
