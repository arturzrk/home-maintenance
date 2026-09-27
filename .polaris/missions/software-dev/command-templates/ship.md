---
description: Ship an application - scaffolds pipelines and generates all deployment artifacts. Use the Polaris Ship App to deploy.
---
## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

---


# /polaris.ship - Ship Application

**Telemetry**: Run: `polaris telemetry record ship --feature <slug> --phase start --agent {{AGENT_NAME}}`

**Purpose**: Single entry point for deployment. Uses the ACR (Azure Container Apps + ACR) flow by default for every app. Users can opt into the Kubernetes (Helm + AppCentral) flow by passing `--kubernetes` (or `-k`).

**IMPORTANT**: This command is best run via the CLI: `polaris ship [env] [--kubernetes]`.
For the Kubernetes flow, the CLI runs devops and deploy as separate agent sessions (fresh context each),
which avoids context compression issues with long-running scaffolding.

If running as a slash command in an agent session, follow the steps below.


## User Input

```text
$ARGUMENTS
```

Parse arguments:
- Environment: `dev` (default), `tst`, or `all` - the first positional token that matches one of these.
- Kubernetes opt-in: if `$ARGUMENTS` contains any of `--kubernetes`, `-k`, or the bare word `kubernetes`, use the Kubernetes approach. Otherwise, use the ACR approach.



---

## Step 0 - Capture regulatory needs (MANDATORY)

Resolve the project's regulatory needs BEFORE any other step or tool call.
The agent MUST NOT execute Step 1, Step 2A, or Step 2K, MUST NOT call any
shell tool, and MUST NOT load any other template until this step finishes.

Three-tier resolution (use the FIRST one that produces a value):

### Step 0a - Honor `--region=` passthrough from CLI

If `$ARGUMENTS` contains `--region=both`, `--region=itar`, or `--region=gdpr`:
- This came from `polaris ship` CLI, which already prompted and persisted.
- Log "Region (from CLI): proceeding with default deployment flow." and proceed to Step 1.
- Do NOT prompt the user again.

### Step 0b - Read stored decision from project metadata

If `--region=` is not in `$ARGUMENTS`, read the stored project decision directly
from `.polaris/metadata.yaml`. The agent MUST NOT depend on `specify_cli` being
importable from the system `python` interpreter - in customer projects polaris is
typically installed in an isolated venv (pipx, uv tool) and
`python -c "from specify_cli..."` raises `ModuleNotFoundError`. Read the file
through stdlib only, or use the Read tool directly.

The decision lives under a top-level `deployment:` block, e.g.:

```yaml
deployment:
  compliance: both
  topology: acr
  regulatory_needs: HIPAA
  decided_at: 2026-05-09T18:20:27Z
  decided_by: alice@example.com
```

Resolve the value with this stdlib-only command:

```
python -c "
import pathlib, re, sys
md = pathlib.Path('.polaris/metadata.yaml')
if not md.exists():
    print(''); sys.exit(0)
text = md.read_text(encoding='utf-8')
m = re.search(r'(?ms)^deployment:\s*\n((?:[ \t].*\n?)*)', text)
if not m:
    print(''); sys.exit(0)
m2 = re.search(r'(?m)^[ \t]+compliance:\s*([^\s#]+)', m.group(1))
print((m2.group(1) if m2 else '').strip())
"
```

- If output is non-empty: log "Region (from metadata): proceeding with default deployment flow." and proceed to Step 1.
- If output is empty (no file, no block, or no `compliance` key): continue to Step 0c.

### Step 0c - Ask the user (only if neither passthrough nor stored value found)

ASK the user this question and WAIT for a response. Do NOT call any other tool while waiting.

> What are your application's regulatory or compliance needs?
> This is recorded in the project constitution.
>
> Regulatory needs (e.g. ITAR, GDPR, HIPAA, or 'none'):

After receiving the response, persist the decision (see below) and proceed to Step 1.

To persist, determine the topology from `$ARGUMENTS`:
- If `$ARGUMENTS` contains `--kubernetes`, `-k`, or the bare word `kubernetes`: topology is `kubernetes`.
- Otherwise: topology is `acr`.

The agent MUST write the decision to BOTH `.polaris/metadata.yaml` and
`.polaris/memory/constitution.md`. Both writes go through stdlib-only Python (no
`specify_cli` import) so they work regardless of how polaris was installed.

Replace `<topology>` literally with the resolved topology value and `<regulatory_needs>`
with the user's answer before running the script below.

```
python - <<'POLARIS_EOF'
import datetime, pathlib, re, subprocess
project = pathlib.Path('.')
topology = '<topology>'
regulatory_needs = '<regulatory_needs>'
try:
    email = subprocess.check_output(['git', 'config', 'user.email'], text=True, stderr=subprocess.DEVNULL).strip() or 'unknown@local'
except Exception:
    email = 'unknown@local'
ts = datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace('+00:00', 'Z')

md = project / '.polaris' / 'metadata.yaml'
md.parent.mkdir(parents=True, exist_ok=True)
text = md.read_text(encoding='utf-8') if md.exists() else ''
block = (
    'deployment:\n'
    '  compliance: both\n'
    f'  topology: {topology}\n'
    f'  regulatory_needs: {regulatory_needs}\n'
    f'  decided_at: {ts}\n'
    f'  decided_by: {email}\n'
)
pat = re.compile(r'(?ms)^deployment:\s*\n((?:[ \t].*\n?)*)')
if pat.search(text):
    text = pat.sub(block, text)
else:
    if text and not text.endswith('\n'):
        text += '\n'
    text += block
md.write_text(text, encoding='utf-8')
print(f'Wrote deployment block to {md}')

cm = project / '.polaris' / 'memory' / 'constitution.md'
cm.parent.mkdir(parents=True, exist_ok=True)
existing = cm.read_text(encoding='utf-8') if cm.exists() else ''
topo_h = 'ACR (Azure Container Apps)' if topology == 'acr' else 'Kubernetes (Helm + AppCentral)'
needs_display = regulatory_needs.strip() if regulatory_needs.strip() else 'None specified'
section = (
    '## Deployment Compliance & Topology\n\n'
    'These choices were made at project initialization and govern how every\n'
    'feature in this project gets deployed.\n\n'
    f'- **Regulatory needs**: {needs_display}\n'
    f'- **Deployment topology**: {topo_h}\n'
    f'- **Decided on**: {ts}\n'
    f'- **Decided by**: {email}\n\n'
    'Implications for future work:\n'
    '- All resource groups MUST be in regions consistent with the compliance posture.\n'
    '- All feature plans MUST honor the deployment topology.\n'
    '- Changing this section requires running `/polaris.constitution --amend` and a team review.\n'
)
heading = '## Deployment Compliance & Topology'
spat = re.compile(rf'(?ms)^{re.escape(heading)}\s*\n.*?(?=^## (?!#)|\Z)')
if spat.search(existing):
    text2 = spat.sub(section, existing)
else:
    if not existing:
        text2 = '# Project Constitution\n\nThis file holds project-wide governing decisions.\n\n' + section
    else:
        sep = '' if existing.endswith('\n\n') else ('\n' if existing.endswith('\n') else '\n\n')
        text2 = existing + sep + section
if not text2.endswith('\n'):
    text2 += '\n'
cm.write_text(text2, encoding='utf-8')
print(f'Wrote deployment section to {cm}')
print(f'Recorded project decision: compliance=both, topology={topology}, regulatory_needs={regulatory_needs}')
POLARIS_EOF
```

If the agent's environment lacks heredoc support (rare on Windows cmd.exe), use
the Edit/Write tool to insert the same `deployment:` block into
`.polaris/metadata.yaml` and the same `## Deployment Compliance & Topology`
section into `.polaris/memory/constitution.md` directly. Do NOT skip the persist
step; future ships rely on it to avoid re-prompting.

---

## Step 1 - Route by user selection


- If the user passed the Kubernetes opt-in: go to **Step 2K** (Kubernetes / Helm / AppCentral).
- Otherwise: go to **Step 2A** (ACR / Container Apps). This is the default for all apps.


Display the selected approach to the user. Example: "Ship: ACR deployment approach (default)" or "Ship: Kubernetes deployment approach (--kubernetes)".

---

Now load the full deployment steps: read `.claude/commands/references/ship-steps.md` and follow the step that matches the route above (Step 2K or Step 2A).

After the selected step path completes (or stops at a skip gate), run:

**Telemetry**: Run: `polaris telemetry record ship --feature <slug> --phase complete --agent {{AGENT_NAME}}`
