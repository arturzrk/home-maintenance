# Autopilot State Persistence Schema

State file: `.polaris/autopilot-state.json`

```json
{
  "feature_slug": "<slug>",
  "current_stage": "build.implement",
  "wp_order": ["WP01", "WP02"],
  "completed_wps": ["WP01"],
  "failed_wps": {},
  "current_wp": "WP02",
  "current_attempt": 1,
  "max_attempts": 3,
  "mode": "standard|quick",
  "on_failure": "skip-and-continue",
  "started_at": "<ISO>",
  "updated_at": "<ISO>",
  "build": {
    "test_plan": {"status": "complete|pending|skipped"}
  },
  "ship": {
    "test_execution": {
      "status": "passed|failed|skipped",
      "passed": 0, "failed": 0, "total": 0
    }
  },
  "escalation": {
    "stage": "<stage>",
    "attempts": 3,
    "reason": "<why the stage could not auto-resolve>",
    "evidence_paths": [".polaris/test-evidence/<slug>/WP03-<ts>.json"],
    "report_path": ".polaris/reports/escalation-<slug>-<ts>.md",
    "recommended_action": "<one-line next step>",
    "escalated_at": "<ISO>"
  }
}
```

The `escalation` block is written by `polaris agent escalate` (see `@references/escalation.md`) when the pipeline gets stuck; it exits cleanly so `--resume` continues from the recorded point.

## Approved parameters (source of truth: spec.md `autopilot:` frontmatter)

Autopilot reads `retry_limit`, `on_failure`, `mode`, `scope`, and `accept`/`qa`/`fix`/`ship` policy from the feature's `spec.md` `autopilot:` frontmatter block (written at spec approval - the single gate). Autopilot passes `--revalidate` to accept by default (`autopilot.accept.revalidate`). Existing specs lacking the block: apply documented defaults (retry 3, on-failure skip-and-continue, mode standard, accept local+revalidate, ship env dev) and NEVER re-interview.

- **Resume**: `/polaris.autopilot --resume`
- **Abort**: `/polaris.autopilot --abort`
- State updated atomically after every stage transition.
