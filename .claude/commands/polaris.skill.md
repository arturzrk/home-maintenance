---
description: Apply and integrate a reusable skill into the current project with guided analysis and approval. Everything runs inside Claude - no terminal commands needed.
---

## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

---

## User Input

**Telemetry**: Run: `polaris telemetry record skill --feature <slug> --phase start --agent claude`

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). Input should be a skill name (e.g., `iam`, `mcp-server`, `studio`, `devops`, `workflow`, `appcentral-shell`).

## Hard Rules

1. **NO POLARIS CLI COMMANDS** -- only the Step 1 commands below (`polaris skill list --json`, then `pip`/`pipx` for built-in skills). Use Read/Glob/Grep for everything else.
2. **NO SUBAGENTS for skill analysis** -- all skill analysis done directly with Read, Glob, Grep tools.

## Step 1. Discover Skills

Run: `polaris skill list --json`

Parse the `skills` array (the merged registry: built-in + `skills.custom` entries + skill packs). Each entry has `name`, `description`, `category`, `source` (`builtin` | `custom-repo` | `pack`), and a `repo` when registered from one.

If no skill name was provided in `$ARGUMENTS`, show the discovered names (built-in first, then custom/pack) and ask which to apply.

Look up the chosen name in the parsed array:

- **Not found**: STOP, tell the user the name is not registered. Point to `polaris skill add` (register it) or `polaris skill list` (see what is available).
- **`source: pack`**: STOP, tell the user this skill is materialised from a skill pack via `polaris skill sync` and is invoked as its own generated command -- it is not applied through this flow.
- **`source: custom-repo`**: STOP, tell the user this skill is registered from its `repo` but this flow does not yet fetch and apply custom-repo skills automatically. They can apply it manually from that repo, or re-register it with `--pack` if it follows the skill-pack layout.
- **`source: builtin`**: continue below to resolve where the package's skill templates live.

The Python package is **`specify_cli`** (NOT `polaris`). Find the install location:

```bash
pip show specify_cli 2>&1 | grep -E "^Location:"
```

If pip returns nothing: try `pipx list --short 2>&1 | grep specify_cli`.

If both fail with "not found" (not a Store-stub error): STOP and tell the user `specify_cli` is not installed.

If pip itself fails with a Microsoft Store / exit-code-49 error: tell the user to disable the Windows Python alias or run pip from their real Python install.

Parse the `Location:` value. Skills base: `<Location>/specify_cli/skills/builtin`. Store as `BASE_PATH`.

---

Load `@references/skill-steps.md` and execute Steps 2 through 10.

**Telemetry**: Run: `polaris telemetry record skill --feature <slug> --phase complete --agent claude`
