# Runtests Mutation Testing (Optional Manual Technique)

NOT a gate. Nothing in Polaris runs, scores, or enforces mutation testing, and
no lane transition or acceptance step depends on it. This is a hand-run
technique an agent MAY use to spot-check whether a work package's tests have
real assertions. Enforced test quality comes from the hollow-test check and the
WS1-B evidence gate (see ADR-32 and ADR-37), not from this document.

## How to do it by hand

1. Identify files changed in this WP via `git diff`
2. For each changed file, introduce 3-5 targeted mutations one at a time:
   - Negate a conditional (`if x > 0` becomes `if x <= 0`)
   - Remove a function call or return statement
   - Change a boundary value (`>=` to `>`)
   - Swap a string literal or constant
3. For each mutation, run only the tests relevant to that file
4. Note **killed** (tests caught it) vs **survived** (tests missed it) mutants
5. Where a mutant survives, that is a hint to add a targeted assertion - then
   revert the mutation and keep the new test
6. Always revert every mutation before committing

## Example notes

```
Mutation spot-check (manual)
----------------------------
Files mutated:    3
Mutations tried:  12
Killed:           10
Survived:          2

Surviving mutants (add assertions):
  - auth_service.py:45 - changed >= to > (boundary check)
  - order_model.py:112 - removed validation call (missing negative test)
```

There is no threshold and no pass/fail verdict: this is a reasoning aid, not a
measured metric.
