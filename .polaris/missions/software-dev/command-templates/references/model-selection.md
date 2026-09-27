## Model Selection

Claude Code operates in two phases within any Polaris workflow. Routing between
them is now mechanical - you do not switch models by hand.

### Plan Phase -> Opus (plan tier)

When reading specs, synthesizing the architecture, deciding what to build and how it connects:
use opus (the default Opus version). Planning mistakes compound through every line of code that follows.
Opus-level reasoning here is insurance, not indulgence.

Polaris planning commands: /polaris.specify, /polaris.plan, /polaris.tasks

### Implementation Phase -> Sonnet (impl tier)

When writing code, calling tools, executing against a defined plan:
use sonnet (the default Sonnet version). Implementation is where call volume lives - this is where savings compound.

Polaris implementation commands: /polaris.implement, /polaris.autopilot, /polaris.runtests

### How routing is enforced

Routing is mechanical, not a request in prose:

- Headless and orchestrated paths: the launcher passes the agent's model flag
  (Claude Code `--model`) and the orchestrator pins each command to its tier via
  the command-to-tier table, so the running model always matches the tier.
- Interactive paths: the tier model is written as the default in the agent's
  `settings.json` at setup, so a normal session already starts on the right model.
- Aliases (`opus` / `sonnet` / `haiku`) resolve in one place. By default they
  stay un-versioned, so the agent CLI runs its current default version of each
  tier. Pin a specific version per project in `.polaris/config.yaml` under
  `model_routing` (`plan_model`, `impl_model`, `bulk_model`, and an `aliases:` map).

### When Implementation Hits Unexpected Complexity

Do not reason through ambiguity or contradiction at the impl tier. Stop the step, describe what
is unexpected, and surface it:

REPLAN NEEDED: [one sentence - what was unexpected]
SPEC REFERENCE: [which spec/section the assumption came from]
OPTIONS: [2-3 ways to resolve, with tradeoffs]

Re-enter the plan phase (Opus) with the updated context before continuing.
Never self-escalate the model mid-implementation.
