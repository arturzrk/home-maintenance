---
path_rewrite: false
description: Build skill bundles (SKILL.md + optional helpers) for the Aptean Intelligence Studio Skill Pool
---

**Telemetry**: Run: `polaris telemetry record skill-builder --phase start --agent {{AGENT_NAME}}`

## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

---

## What This Command Does

`/polaris.skill-builder` generates complete skill bundles (`SKILL.md` + optional `scripts/`, `examples/`, `references/` folders) for the Aptean Intelligence Studio Skill Pool.

**Scope:** This command builds skill bundles only. It never produces agent JSON, DigitalWorker definitions, or flow configurations. If the request implies building an agent ("build me a DigitalWorker", "wire this into an agent") -- acknowledge the request, build and deliver the skill bundle, then tell the user to use `/polaris.agent-builder` for the agent JSON.

**On failure:** Stop immediately. Report clearly what failed. Do not generate a partial or fallback bundle.

---

## User Input

```text
$ARGUMENTS
```

Parse intent from `$ARGUMENTS`:
- Empty -- ask the user what capability they want to build as a skill
- Any description -- build a skill bundle for that capability

---

## Step 0 - Read the relevant example first

Before building, read the example that matches the bundle type you will produce. Do this now, not after deciding.

The skill-builder files sit alongside this command. Read using the path relative to where the skill-builder skill is installed (typically `"Skill Builder Skill/examples/..."`):

```bash
# For prose-only skills:
Read "Skill Builder Skill/examples/prose-only.md"

# For script-backed skills:
Read "Skill Builder Skill/examples/script-backed/SKILL.example.md"
Read "Skill Builder Skill/examples/script-backed/scripts/lookup.py"

# For references-heavy skills:
Read "Skill Builder Skill/examples/references-heavy/SKILL.example.md"
```

Read the relevant example verbatim before writing any output. The example shows canonical structure, routing language, and formatting.

---

## Step 1 - Check whether a skill is warranted

Apply each test in order. If any test fails, stop and tell the user why a skill is not the right fit.

**Test 1 - One-off agent-specific instructions**
Would more than one DigitalWorker ever use this capability without modification?
- No -> keep it in `system_prompt`. Stop.

**Test 2 - Already covered by a platform component**
Run `python "Agent Builder Skill/scripts/components.py" search <keyword>`. If a matching MCP Server or built-in component exists, tell the user to wire that component instead of building a skill. Stop.

**Test 3 - Too short for a skill**
Draft the instructions mentally. Under 10 lines?
- Yes -> add directly to `system_prompt`. Stop.

**Test 4 - Tightly coupled to a specific flow**
Can this skill be dropped into any DigitalWorker without modification? If it references other nodes or a specific agent flow ("after the TaskRouter sends to this agent..."):
- No -> belongs in that agent's `system_prompt`. Stop.

**Test 5 - Data access that belongs in an MCP tool**
Does the skill primarily instruct the agent to call an external API or query a live system?
- Yes -> build an MCP Server tool node instead. Stop.

**Test 6 - Duplicate of an existing skill**
Check the Skill Pool for any skill covering the same domain. If one exists, offer to extend or update it rather than creating a new entry.

**The one-question test (final check):**
"If I removed this skill from the Skill Pool and added it back tomorrow, would a different DigitalWorker agent benefit from it without any changes?"
- No -> stay in `system_prompt`. Stop.
- Yes -> proceed to Step 2.

If the capability fails any test, tell the user clearly which test failed and why. Do not proceed to build.

---

## Step 2 - Decide the bundle type

Answer in order; take the first matching branch:

**Q1: Does this skill need deterministic logic, a data lookup table, or any data file >100KB?**
- YES -> **script-backed** -- include `scripts/<helper>.py` + `requirements.txt`. Read `examples/script-backed/` before proceeding.

**Q2: Does this skill require more than ~2 pages of reference material (detailed API docs, multi-section specs, large error references)?**
- YES -> **references-heavy** -- put deep content in `references/*.md`, route to it from `SKILL.md`. Read `examples/references-heavy/` before proceeding.

**Q3: Otherwise:**
- **prose-only** -- a single `SKILL.md` is sufficient. Read `examples/prose-only.md` before proceeding.

---

## Step 3 - Derive the slug

The slug is the `name` field and also the directory name. Rules:
- Lowercase kebab-case only: letters, digits, and hyphens
- Valid: `error-code-lookup`, `meeting-notes-summarizer`, `ap-payment-escalation`
- Invalid: `Error Code Lookup`, `meeting_notes`, `MySkill`, `my skill`

Derive from the capability description. If the user provided a name, use it (normalise to kebab-case).

---

## Step 4 - Build the bundle

### 4a - SKILL.md frontmatter

```yaml
---
name: <slug>
description: "<Use when...> <context sentence>. Trigger phrases: \"<phrase 1>\", \"<phrase 2>\", \"<phrase 3>\"."
version: 1.0.0
tools: Read, Bash   # only if the skill invokes tools; omit for prose-only
license: MIT        # optional; include for skills intended for sharing/distribution
---
```

**`description` rules (strictly enforced):**
1. Must start with "Use when..." (case-insensitive)
2. Follow with a context sentence (who uses it, in what situation)
3. Add 2-4 quoted trigger phrases a user or orchestrator agent would actually say
4. Wrap entire value in double quotes
5. For long descriptions use YAML line folding: end line with `\ ` (backslash + space), indent continuation two spaces
6. Escape inner double quotes as `\"` and any non-ASCII as `\uXXXX`

Example:
```yaml
description: "Use when a DigitalWorker needs to look up status codes by code number\
  \ or component name. Trigger phrases: \"look up error code\", \"what does this\
  \ error mean\", \"find the resolution for\"."
```

### 4b - SKILL.md body

**SKILL.md is the router.** Rules:
- Names sibling files, says when to read them, and says when NOT to read them
- Never duplicates content from sibling files
- Any single `.md` file >50KB must be split into `references/` and linked from SKILL.md
- Any data file (`.json`, `.csv`, `.xml`) >100KB must be accessed via a `scripts/<helper>.py` CLI. SKILL.md must include: "Never read `<file>` directly -- use `python scripts/<helper>.py ...`"
- Every file in the bundle must be <1MB

Structure for each bundle type:

**prose-only:** `## When to Use`, `## How to <Do The Thing>`, `## Output Format` (if output matters). See `examples/prose-only.md`.

**script-backed:** Open with `## Reference Files` naming the data file and the CLI commands. Include `## When to Use`, `## How to <Do The Thing>`, `## Output Guidance`. Directive to never read the data file directly. See `examples/script-backed/SKILL.example.md`.

**references-heavy:** Open with `## Reference Files` listing each file in `references/` with a one-line description of when to read it. Include `## When to Use`, `## How to Assist`. Route explicitly to the reference file for each use case. See `examples/references-heavy/SKILL.example.md`.

### 4c - scripts/<helper>.py (script-backed only)

Expose at minimum `list`, `search <query>`, and `detail <id>` subcommands.

Script path idiom -- resolve sibling data files via:
```python
DATA_FILE = Path(__file__).parent.parent / "examples" / "data.json"
```

Follow the canonical pattern from `examples/script-backed/scripts/lookup.py` exactly:
- `load()` function reads the data file
- `cmd_list()`, `cmd_search()`, `cmd_detail()` functions
- `COMMANDS` dict mapping subcommand name to `(function, num_args)`
- `if __name__ == "__main__":` dispatcher with usage/help handling

Include `requirements.txt` at the bundle root listing any non-stdlib packages. If only stdlib is used, include an empty `requirements.txt` with a comment.

### 4d - references/<topic>.md (references-heavy only)

Put deep content here. Each file should cover one bounded topic (API endpoints, error codes, configuration reference, etc.). Keep each file under 50KB. SKILL.md routes to these files -- never duplicate content between them.

---

## Step 5 - Deliver the bundle

### 5a - API Deployment (preferred -- use when Create Skill, Put Skill File, List Skill Files tools are available)

All skills created through this flow are at personal (user-level) scope.

**Step 5a.1 - Resolve scope and create or update the skill record**

1. Call `Get Skill by Name` with the proposed slug.
2. Branch on the result:
   - **Not found (404):** Call `Create Skill` with `slug_name`, `display_name` (human-readable title), `description` (starts with "Use when..."), and `content` (full SKILL.md text, WITHOUT frontmatter block -- name and description are stored as separate fields on the skill record). The response returns a `skill_id` UUID. Save it -- every subsequent file call needs it.
   - **Found, `scope: "personal"`:** Call `Update Skill` with the existing `skill_id`, updated `description`, and `content`. Do NOT call `Create Skill` -- this would create a duplicate.
   - **Found, `scope: "customer"`, `"product"`, or `"common"`:** Do NOT call `Update Skill` -- updating a higher-scope skill is forbidden (403 for non-superusers). Call `Create Skill` with the same slug -- the platform resolves by-name to the most-specific scope, so this personal-scope skill cleanly overrides. Save the returned `skill_id`.

Never set `visibility` to `COMMON` or `PRODUCT`. Never set `is_customer_level: true`.

**Step 5a.2 - Upload additional files**

For each file in `scripts/`, `examples/`, `references/`:
- Call `Put Skill File` with `skill_id`, `path` (relative path within bundle), `content` (full file text)
- Safe to call for both new and existing files

**Step 5a.3 - Verify**

Call `List Skill Files` with the `skill_id`. Confirm every expected path appears.

**Step 5a.4 - Review**

Call `Review Skill` with the `skill_id`. If warnings or errors are returned, surface them to the user and offer to fix before reporting live.

**Step 5a.5 - Report**

Tell the user:
- The slug that was created
- Each file path uploaded
- That the slug is ready to wire into a DigitalWorker: add `"<slug>"` to `template.skills.value` on the DigitalWorker node

Do NOT output redundant code blocks after a successful API deployment.

---

### 5b - Zip File Delivery (fallback -- use when API tools are not available)

**Step 5b.1 - Write files to disk**

Write all bundle files into a temporary directory named after the slug. Only zip skill-related files.

```bash
mkdir -p /tmp/<slug>/scripts
mkdir -p /tmp/<slug>/examples
mkdir -p /tmp/<slug>/references

cat > /tmp/<slug>/SKILL.md << 'EOF'
<full SKILL.md content>
EOF

# repeat for each additional file in scripts/, examples/, references/
```

**Step 5b.2 - Zip the bundle**

```bash
cd /tmp && zip -r <slug>.zip <slug>/
```

**Step 5b.3 - Copy to outputs**

```bash
cp /tmp/<slug>.zip /mnt/user-data/outputs/<slug>.zip
```

**Step 5b.4 - Present the file**

Use `present_files` to deliver `/mnt/user-data/outputs/<slug>.zip`.

**Step 5b.5 - Report**

Tell the user:
- The slug that was packaged
- Each file path included in the zip
- To unzip, run `python scripts/skill-builder/scripts/validate.py <slug>`, then upload via the Skill Pool UI
- To add `"<slug>"` to the DigitalWorker node's `template.skills.value` array once uploaded

---

## Step 6 - Validate before delivering (zip path only)

For zip delivery, run the validator BEFORE zipping and presenting the file:

```bash
python "Skill Builder Skill/scripts/validate.py" /tmp/<slug>
```

The validator checks:
- `SKILL.md` exists at bundle root
- Frontmatter present with required fields (`name`, `description`)
- `name` is kebab-case and matches the directory name
- `description` starts with "Use when"
- All files under the 1MB hard cap
- Markdown files >50KB flagged for splitting
- Data files >100KB flagged for CLI mediation
- No forbidden files (`.DS_Store`, `*.pyc`, `.env`)

If the validator returns errors, fix them and re-run before proceeding to zip. Surface any warnings to the user.

Note: the validator cannot detect slug collisions with existing Skill Pool entries. Tell the user to verify slug uniqueness in the Skill Creator UI before uploading.

---

## Mid-Flow Integration Note

When `/polaris.skill-builder` is invoked during an agent-build flow (e.g. called from `/polaris.agent-builder`):

1. Build and deliver the skill bundle in the same response.
2. The slug is immediately wired into the DigitalWorker JSON being constructed -- add it to `template.skills.value`.
3. The user receives both the bundle (to upload) and the agent JSON (to import) in one response.
4. The agent JSON can be imported before the skill is uploaded -- the platform resolves slugs at runtime, so a missing skill does not block import. It surfaces as "skill not found" only when the DigitalWorker is invoked.

---

## Platform Integration Quick Reference

To wire an uploaded skill into a DigitalWorker, add the slug to `template.skills.value` in the DigitalWorker node JSON:

```json
"skills": {
  "type": "str",
  "_input_type": "MultiselectInput",
  "value": ["existing-skill", "<new-slug>"],
  "list": true
}
```

Path in agent JSON: `data.nodes[<DigitalWorker-node-id>].data.node.template.skills.value`

The platform resolves slugs against the Skill Pool at runtime -- no skill content is embedded in the agent JSON, only the slug string.

**Telemetry**: Run: `polaris telemetry record skill-builder --phase complete --agent {{AGENT_NAME}}`
