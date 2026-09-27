# Review - Multi-Persona Pass Details

Full criteria for the 4 mandatory review passes in `/polaris.review`.

## Pass 1: Security Review

Load expertise from `.polaris/skills/superpowers/security-reviewer.md` (project) or `src/specify_cli/superpowers/prompts/security-reviewer.md` (builtin). Use its focus areas, checklist, and pitfalls.

Mindset: Application Security Engineer. Review ALL changed code for:

1. **Injection** - SQL injection, XSS, command injection, path traversal
2. **Auth** - Missing auth checks, privilege escalation, broken access control
3. **Auth cross-check** (see `@references/quality-guardrails.md` Auth Cross-Check section) - when `.polaris/memory/constitution.md` declares an AuthN/AuthZ model, verify changed code enforcing privileged operations matches it
4. **Secrets** - Hardcoded API keys, tokens, passwords, connection strings
5. **Input validation** - Missing/insufficient validation at system boundaries
6. **Data exposure** - Sensitive data in logs, error messages, API responses, comments

Per finding: file, line, vulnerability type, severity (Critical/High/Medium/Low), fix.

**Pass 1 Verdict: PASS or FAIL** (FAIL if any Critical or High)

---

## Pass 2: Performance Review

Load from `.polaris/skills/superpowers/perf-engineer.md` or builtin. Use its criteria.

Mindset: Performance Engineer. Review ALL changed code for:

1. **DB patterns** - N+1 queries, missing indexes, unbounded SELECT
2. **Algorithm** - O(n^2) or worse, nested iterations over large collections
3. **Resources** - Unclosed connections/streams/handles, missing cleanup in error paths
4. **Concurrency** (see `@references/quality-guardrails.md` Concurrency Cross-Check section) - bounded executor required for background threads/tasks, no daemon threads for must-complete work, explicit max concurrency; cross-checked against the constitution's Non-Functional Baseline when present, but this generic check always runs regardless
5. **Caching** - Missing cache for expensive repeated ops, no invalidation strategy
6. **Memory** - Growing collections, event listeners not removed, large object retention

Per finding: file, line, issue type, impact (High/Medium/Low), fix.

**Pass 2 Verdict: PASS or FAIL** (FAIL if any High impact)

---

## Pass 3: Standard Code Review

Load from `.polaris/skills/superpowers/standard-reviewer.md` or builtin. Use its criteria.

Review ALL changed code for:

1. **Style** - Consistent naming, formatting, project conventions
2. **Test coverage** - New/changed code has tests, edge cases covered
3. **Error handling** - Happy path AND error paths, meaningful messages
4. **Documentation** - Changed public APIs documented, non-obvious logic commented
5. **Backward compat** - No breaking changes to public interfaces without migration path

Per finding: file, line, issue type, fix.

**Pass 3 Verdict: PASS or FAIL** (FAIL if tests missing for new code or breaking changes undocumented)

---

## Pass 4: Architecture Review

Load criteria from `@references/quality-guardrails.md` Architecture Review section.

Mindset: Software Architect. Review ALL changed code for:

1. **SOLID-violation smells** - mixed responsibilities, unnecessary concrete coupling, subclasses violating parent contracts
2. **Coupling/cohesion** - a change here routinely forces changes elsewhere; modules with no clear shared purpose
3. **God object/function** - far larger than the surrounding file's typical size and touching 3+ unrelated responsibilities
4. **Over-engineering** - abstraction/indirection for a single implementation with no near-term second one
5. **Under-engineering** - the same non-trivial logic duplicated 3+ times instead of extracted

Per finding: file, line, issue type, severity (Critical/High/Medium/Low), fix, blocking (yes only if Critical).

**Pass 4 Verdict: PASS or FAIL** (FAIL only if any Critical finding; High/Medium/Low findings are reported but do not change the verdict)

---

## Review Summary Table

Output after all 4 passes:

```
## Review Summary

| Pass | Verdict | Required | Blocking |
|------|---------|----------|----------|
| Security | PASS/FAIL | yes/no | yes/no |
| Performance | PASS/FAIL | yes/no | yes/no |
| Standard | PASS/FAIL | yes/no | yes/no |
| Architecture | PASS/FAIL | yes/no | yes (Critical only) |

**Overall: APPROVED / REJECTED**
```

- **APPROVED**: all required passes are PASS
- **REJECTED**: any required pass is FAIL -> list items to fix per pass under "### Items to Fix"

## Machine-Readable Verdict (required)

End the review with EXACTLY ONE fenced JSON block and no prose after it. This is the only signal the automated quality gate reads; a missing, malformed, or duplicated block counts as REJECTED.

```json
{"verdict": "APPROVED", "reasons": ["all required passes PASS"], "evidence_checked": true}
```

Use `"verdict": "REJECTED"` with the blocking items in `reasons` when any required pass fails. Set `evidence_checked` true only if you actually ran the tests and inspected the diff.
