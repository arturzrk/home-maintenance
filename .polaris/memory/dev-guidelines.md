## CRITICAL: No Em Dashes or Smart Quotes

**NEVER use em dashes (`-`), smart quotes, or non-ASCII punctuation in ANY output.** Two layers of enforcement run automatically:

1. **Write-time chokepoint.** Polaris code that emits markdown / HTML / text routes through `specify_cli.text_sanitization.write_markdown(path, content)`, which substitutes em dashes / smart quotes / etc. before writing. New file-writing code MUST use `write_markdown` (or `sanitize_text(content)` for in-memory work) instead of raw `Path.write_text(...)`.
2. **Commit-time hook.** `pre-commit-encoding-check` auto-substitutes any remaining offenders in staged markdown and re-stages the files silently. The hook is a safety net for human-edited files and AI-tool writes that bypass Polaris code paths.

The rule still matters even with auto-fix because (a) the hook only touches `.md` files, not `.ps1`/`.txt`, and (b) producing ASCII directly avoids needless churn in the diff and keeps Aptean brand output uniform.

Use: ` - ` for asides, `:` for elaborations, `( )` for clarifications, or split into two sentences.

En dashes (`–`) allowed **only** in activity log field separators: `YYYY-MM-DDTHH:MM:SSZ – agent_id – lane=<lane> – <action>`. The substitution table deliberately leaves en dash alone so log lines stay parseable.

## CRITICAL: Commit Attribution - Aptean Polaris Only

**Do NOT add any `Co-Authored-By` trailer to commit messages.**

The `commit-msg` hook automatically appends `Co-Authored-By: Aptean Polaris <polaris@aptean.com>` to every commit. This is the only attribution required. Do not add AI agent trailers (e.g., `Co-Authored-By: Claude ...`, `Co-Authored-By: Copilot ...`, etc.). The hook handles it.

## CRITICAL: Cross-Platform Templates

**Command templates MUST work on Windows, macOS, and Linux.** Never use `find`, `ls`, `grep`, `wc`, `awk`, `sed`, `mkdir -p`, `rm -rf`, `python3`, or bash scripts - none are portable.

| Need | Use |
|------|-----|
| Find files | `git ls-files` or agent search tools |
| Search content | `git grep` or agent search tools |
| Count/sort/filter | `python -c "..."` one-liners |
| Create dirs | `python -c "from pathlib import Path; Path('x').mkdir(parents=True, exist_ok=True)"` |
| Remove dirs | `python -c "import shutil; shutil.rmtree('x')"` |
| Read JSON | `python -c "import json; ..."` |
| Run Python | `python` (not `python3`) |
| Build scripts | `.py` files only |

## CRITICAL: Never Delete Agent Directories or User Files

**NEVER call `shutil.rmtree()` on agent directories** (`.claude/`, `.amazonq/`, `.gemini/`, etc.). Polaris owns only `polaris.*` named files - never touch user files.

- Use `output_dir.mkdir(parents=True, exist_ok=True)` - never rmtree then mkdir
- Write only `polaris.*` files - never iterate and delete others
- Applies to `generate_agent_assets()`, all migrations, any code touching agent dirs

**See:** ADR `architecture/adrs/2026-03-09-1-non-destructive-agent-directory-writes.md`

## CRITICAL: Adding New Slash Commands

**When adding a new command template, you MUST update ALL of these:**

1. `src/specify_cli/missions/*/command-templates/<name>.md` (the template)
2. `src/specify_cli/cli/commands/init.py` (Next Steps or Enhancement Commands panel)
3. `README.md` (Slash Commands tables)
4. `docs/reference/slash-commands.md` (reference section)

A sync test (`tests/specify_cli/cli/commands/test_init_command_sync.py`) enforces this and will fail if any location is out of sync.

## CRITICAL: Template Source Location (READ THIS FIRST!)

**When fixing bugs or updating templates, edit the SOURCE files, NOT the agent copies!**

| What | Location | Action |
|------|----------|--------|
| **SOURCE templates** | `src/specify_cli/missions/*/command-templates/` | ✅ EDIT THESE |
| **Agent copies** | `.claude/`, `.amazonq/`, `.augment/`, etc. | ❌ DO NOT EDIT |

The directories like `.claude/commands/`, `.amazonq/prompts/`, etc. are **GENERATED COPIES** that get deployed to projects that USE polaris. They are NOT source code.

Source templates are copied to all 13 agent directories during `polaris upgrade` migrations.
