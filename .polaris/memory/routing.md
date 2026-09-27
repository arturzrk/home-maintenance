## Intent-to-Workflow Routing

Evaluate in order. When a row matches, you MUST immediately invoke the matched command via the `Skill` tool (e.g., `Skill(skill="polaris.specify", args="<user's full original request>")`), passing the user's full original message as `args` so it lands as `$ARGUMENTS` inside the command template. Natural-language routing must produce the same on-disk artifacts as a typed slash command - do not narrate the route, write a spec inline, or ask discovery questions yourself; the skill template handles all of that.

Named commands skip this tree.

### Setup / Onboarding
| Signal | Command |
|--------|---------|
| No `.polaris/` dir, first time setup | `/polaris.onboard` |
| Has `.polaris/` but env not configured | `/polaris.setup` |
| Scaffold from registered template | `/polaris.scaffold` |
| New app from scratch | `/polaris.newapp` |
| New MCP server (FastMCP) | `/polaris.newmcp` |
| Restructure project to org standards | `/polaris.restructure` |

### Specification
| Signal | Command |
|--------|---------|
| No spec yet, describe a new feature | `/polaris.specify` |
| Change/extend existing requirements | `/polaris.specify --amend "<description>"` |
| "flesh out", "expand", "add detail to", "add X to feature Y" | `/polaris.specify --amend "<description>"` |
| Ambiguous or unclear acceptance criterion | `/polaris.clarify` |

### Planning
| Signal | Command |
|--------|---------|
| Spec exists, no plan.md yet | `/polaris.plan` |
| Create or update work packages (WPs) | `/polaris.tasks` |

### Implementation
| Signal | Command |
|--------|---------|
| Implement all pending WPs / run full pipeline | `/polaris.autopilot` |
| Implement a specific WP | `/polaris.implement WP##` |
| Resume interrupted autopilot | `/polaris.autopilot --resume` |
| "what should I work on next?" / continue where left off | `/polaris.status` then `autopilot --resume` if active |
| Submit finished WP for review | `polaris agent tasks move-task WP## --to for_review` |
| Test/build failing, regression, bug fix | `/polaris.fix` |

### Testing / Quality
| Signal | Command |
|--------|---------|
| Run project tests (unit/integration) or "test this" | `/polaris.runtests` |
| Generate E2E tests from work item | `/polaris.qa` |
| Run regression tests | `/polaris.regression` |
| Automated browser UI regression | `/polaris.regression-testing` |
| Cross-artifact consistency and coverage analysis | `/polaris.analyze` |
| "is this ready?", "anything I missed?", code ready check | `/polaris.review` |
| Risk, readiness, tech debt, 7-dimension assessment | `/polaris.assess` |
| Quality checklist for requirements validation | `/polaris.checklist` |
| Compliance audit (SOC2, ISO, HIPAA, PCI-DSS) / coding standards | `/polaris.standards` |
| Generate branded HTML/PPTX report | `/polaris.report` |

### Review / Accept
| Signal | Command |
|--------|---------|
| Deep-review WP, diff, or PR | `/polaris.review` |
| Validate and accept feature for merge | `/polaris.accept` |

### Merge / Release / Deploy
| Signal | Command |
|--------|---------|
| Merge feature branch / create PR | `/polaris.merge` |
| Deploy the app to AppCentral (scaffold CI/CD, then deploy) | `/polaris.ship` |
| Cut a release / push to production | `/polaris.ship` |
| Re-deploy after code changes | `/polaris.deploy` |
| Deploy to specific env | `/polaris.deploy dev` or `/polaris.deploy tst` |

### Research
| Signal | Command |
|--------|---------|
| Research topic, compare libraries, Phase 0 investigation | `/polaris.research` |

### Integration / Infrastructure
| Signal | Command |
|--------|---------|
| Add IAM, Studio, DB, API, queue, webhook, streaming integration | `/polaris.integrate` |
| "adopt internal app change", "IAM token change", "token permission migration" | `/polaris.adopt-internalapp-change` |
| Apply IAM / Studio / MCP / DevOps skill (code-level wiring) | `/polaris.skill` |
| Aptean Intelligent Workflow integration | `/polaris.workflow` |
| CI/CD pipelines, Docker, Helm, deployment config | `/polaris.devops` |
| Health endpoints | `/polaris.healthcheck` |

### Polaris Operations
| Signal | Command |
|--------|---------|
| Kanban status of feature or WPs | `/polaris.status` |
| Visual real-time dashboard | `/polaris.dashboard` |
| Migrate legacy specs into Polaris format | `/polaris.migrate` |
| Team constitution / governing principles | `/polaris.constitution` |

### Compound Intents
| Goal | Sequence |
|------|----------|
| Build and ship end-to-end | specify -> plan -> tasks -> autopilot -> qa -> review -> accept -> merge |
| Plan and implement | specify -> plan -> tasks -> implement (or autopilot) |
| Implement and test a WP | implement WP## -> runtests |
| Finish WP / submit for review | runtests -> move-task for_review -> review |
| Test and merge | qa -> regression -> review -> accept -> merge |
| Set up new project | onboard -> setup -> scaffold (if needed) |

### Product Skills
`polaris skill sync` maintains a generated "Product Skills" routing section in this file, delimited by the `POLARIS:CUSTOM-SKILLS:START` / `POLARIS:CUSTOM-SKILLS:END` HTML-comment marker pair. Sync owns everything between those markers: hand edits inside the block are overwritten on the next sync. Add manual routing rows outside the markers.

### Fallback
If no match: ask one question - "Are you trying to (a) start something new, (b) continue implementing, (c) test or review, or (d) ship/merge?" Then re-evaluate. Do not guess silently.

**Extension point**: new commands go under the most relevant section above.
