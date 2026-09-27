# Quality Guardrails: Auth, Concurrency, Architecture

Single, shared definition of the auth cross-check, concurrency cross-check, and
Architecture Review criteria. Referenced by `review-passes.md` (Passes 1, 2, and 4),
`standards.md`, and `comply.md` - none of them restates this content inline. If you are
updating what counts as a violation in any of these three areas, this is the only file
to edit.

## Auth Cross-Check

"Matches the declared AuthN/AuthZ model" means, in checkable terms:
- The principal source in the changed code (JWT, header, session) matches what the
  project declared - a handler that reads a different credential source than the
  declared one is a mismatch, not just an omission.
- Default-deny is actually enforced: a request that does not match a permit rule is
  denied, not silently allowed through.
- The authorization decision matches the declared model (role-based, attribute-based,
  or ownership-based) - a role-check where the project declared ownership-based access
  is a mismatch even though *some* check exists.

Generic checklist (always applies, regardless of whether a constitution exists):
- Missing auth entirely on a privileged operation (create/update/delete, admin action,
  access to another user's data).
- Privilege escalation - a lower-privileged caller can reach a higher-privileged code
  path.
- Broken access control - an authorization check exists but can be bypassed (parameter
  tampering, missing ownership check, trusting a client-supplied role claim).
- Inconsistent enforcement across structurally similar endpoints - one handler has a
  check, a near-identical sibling handler doesn't.

### Cross-checking against the project constitution

Read `.polaris/memory/constitution.md`'s `## Security & Audit Baseline` section
(covers Q6-Q9, including the AuthN/AuthZ model) if the file exists and has that section.
When it does, verify the changed code's authentication/authorization behavior actually
matches what that section declares (principal source, default-deny, decision model) -
this is in *addition to* the generic checklist above, never a replacement for it.

If `.polaris/memory/constitution.md` does not exist, or exists but has no
`## Security & Audit Baseline` section, skip the cross-check step entirely. This is
never a reason to skip the generic checklist above, and never a reason to fail the
review - a missing constitution only means one less thing to cross-check, not one
fewer thing to look for.

## Concurrency Cross-Check

Requirements, in checkable terms:
- A bounded executor is required for any background thread/task/queue-consumer work -
  an explicit max concurrency (thread pool size, task-group limit, worker count) must
  be set; a bare unbounded pool is a violation.
- Daemon threads are forbidden for must-complete work (audit-log flushers, queue
  consumers, anything whose loss would corrupt state or drop data) - daemon threads are
  acceptable only for genuinely fire-and-forget work that can be safely abandoned on a
  hard process kill.
- A documented shutdown drain expectation should exist for anything with an unbounded
  natural lifetime (a queue consumer, a background poller).

Generic checklist (always applies, regardless of whether a constitution exists):
- Unbounded `ThreadPoolExecutor`/task queue with no cap on concurrency.
- Thread or task leaks - work spawned with no corresponding join/cancellation path.
- Daemon threads used for work that must survive a process exit (see above).

### Cross-checking against the project constitution

Read `.polaris/memory/constitution.md`'s `## Non-Functional Baseline` section (covers
Q10-Q13, including background task lifecycle policy) if the file exists and has that
section. When it does, verify the changed code's background-task/thread-pool code
actually matches the declared policy (bounded executor sizing, daemon-thread rule,
drain timeout) - in *addition to* the generic checklist above, never a replacement.

If `.polaris/memory/constitution.md` does not exist, or exists but has no
`## Non-Functional Baseline` section, skip the cross-check step entirely and fall back
to the generic checklist only - never block or fail a review because the constitution
itself is absent.

## Architecture Review

Criteria, in checkable terms:
- **SOLID-violation smells**: a class/module doing multiple unrelated things (mixed
  responsibilities that would normally be split); callers depending on a concrete
  implementation where an interface/abstraction would decouple them and there is a
  concrete near-term need for more than one implementation; a subclass that violates
  its parent's behavioral contract (breaks callers that only know the parent type).
- **Coupling/cohesion heuristics**: a change in one module routinely forces changes in
  several unrelated modules (tight coupling); a module whose internal pieces have no
  clear shared purpose (low cohesion).
- **God object/function**: a function or class that is far larger than the surrounding
  file's typical size **and** handles 3 or more genuinely unrelated responsibilities
  (for example: validating input, persisting to a database, sending an email, and
  charging a payment, all in one function). Judge by responsibility count, not raw line
  count alone - a long-but-single-purpose function (a big data table, a large but
  cohesive parser) is not a God function.
- **Over-engineering**: an abstraction/indirection layer (interface, factory, plugin
  system) introduced for a single concrete implementation with no near-term second one -
  speculative generality that adds indirection without a present need.
- **Under-engineering**: the same non-trivial logic block duplicated 3 or more times
  where extracting a shared helper/module would clearly reduce risk of divergence.

Severity model: **Critical / High / Medium / Low**. Only a Critical finding fails the
pass (blocking); High, Medium, and Low findings are reported as advisory and never
block approval on their own. Use Critical sparingly - reserve it for smells that are
objectively severe and non-debatable (for example the God-function example above, or
duplicated business-critical logic that has already diverged in two of its three
copies); a plausible-but-arguable structural choice belongs at High or below.

### Cross-checking against the project constitution

There is no constitution cross-check for Architecture Review - design-pattern and
structural-quality expectations are not something `/polaris.constitution` captures
today (unlike Auth and Concurrency, which map to Q8 and Q11 respectively). This section
always runs the checklist above unconditionally; there is no fallback branch because
there is nothing to fall back from.
