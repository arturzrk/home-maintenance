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

test_help
test_check_all_present
test_check_missing_tool
test_unknown_flag

if [ -n "$failures" ]; then
  printf '\nFAILED\n'
  exit 1
fi
printf '\nALL TESTS PASSED\n'
