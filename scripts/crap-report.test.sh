#!/usr/bin/env bash
# Tests for scripts/crap-report.sh. Hermetic: dotnet is stubbed to a
# no-op that logs its own invocations, so no real build/test/report
# generation happens. Run: bash scripts/crap-report.test.sh

set -uo pipefail

REPO_ROOT="$(cd "${BASH_SOURCE[0]%/*}/.." && pwd)"
SCRIPT="$REPO_ROOT/scripts/crap-report.sh"
BASH_BIN="$(command -v bash)"

failures=""

ok()  { printf 'PASS %s\n' "$1"; }
bad() { printf 'FAIL %s\n' "$1"; failures="${failures}x"; }

STUB_DIR="$(mktemp -d)"
DOTNET_LOG="$STUB_DIR/dotnet-calls.log"
trap 'rm -r "$STUB_DIR" 2>/dev/null || true' EXIT

# Stub `dotnet` logs its full argument list (one call per line) instead
# of doing anything real -- no build, no test run, no report generation.
printf '#!/usr/bin/env bash\nprintf "%%s\\n" "$*" >>"%s"\n' "$DOTNET_LOG" >"$STUB_DIR/dotnet"
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
  out="$(run_script 2>&1)"; rc=$?
  if [ "$rc" -ne 0 ] || [ ! -f "$DOTNET_LOG" ]; then
    bad "pipeline did not run (rc=$rc, log_exists=$([ -f "$DOTNET_LOG" ] && echo yes || echo no))"
    return
  fi

  local build_line test_line restore_line report_line crap_line
  build_line="$(grep -n '^build ' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  test_line="$(grep -n '^test .*CollectCoverage=true' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  restore_line="$(grep -n '^tool restore' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  report_line="$(grep -n 'reportgenerator' "$DOTNET_LOG" | head -1 | cut -d: -f1)"
  crap_line="$(grep -n 'run --project .*CrapReport' "$DOTNET_LOG" | head -1 | cut -d: -f1)"

  if [ -n "$build_line" ] && [ -n "$test_line" ] && [ -n "$restore_line" ] \
      && [ -n "$report_line" ] && [ -n "$crap_line" ] \
      && [ "$build_line" -lt "$test_line" ] \
      && [ "$test_line" -lt "$report_line" ] \
      && [ "$report_line" -lt "$crap_line" ]; then
    ok "pipeline invokes restore, build, test(+coverage), reportgenerator, then CrapReport in order"
  else
    bad "pipeline order (build=$build_line test=$test_line restore=$restore_line report=$report_line crap=$crap_line)"
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
