---
description: Build or modify Aptean Intelligence Studio agent JSON for import
---


**Telemetry**: Run: `polaris telemetry record agent-builder --phase start --agent claude`

## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

---

## What This Command Does

`/polaris.agent-builder` builds Aptean Intelligence Studio agent flow definitions and saves them as `.json` files ready for import into the Studio portal.

**On build failure**: Stop immediately. Report clearly which step failed and what the error was. Do not generate fallback JSON. Do not retry silently.

---

## User Input

```text
$ARGUMENTS
```

Parse intent from `$ARGUMENTS`:
- Empty or `list` - list all agent JSONs in `.polaris/agents/`
- `edit <filename>` - load an existing `.polaris/agents/<filename>.json` and apply stated changes only; do not rebuild from scratch
- Any description / plan - build a new agent JSON from the description

---

## Step 0 - Ensure output directory exists

```python
python -c "from pathlib import Path; Path('.polaris/agents').mkdir(parents=True, exist_ok=True)"
```

---

## Step 1 - Dispatch by intent

### If intent is `list`

Glob `.polaris/agents/*.json`. For each file, read and display:
- filename
- `name` field
- `description` field
- `aptean_component_type` field
- node count (`data.nodes` length)

If no files, print: "No agents defined yet. Run `/polaris.agent-builder <description>` to create one."
Stop here.

### If intent is `edit <filename>`

1. Read `.polaris/agents/<filename>.json` (try with and without `.json` extension).
2. If not found, STOP: "File not found: `.polaris/agents/<filename>.json`"
3. Display current `name`, `description`, and node summary.
4. Apply ONLY the changes stated in `$ARGUMENTS` - do not rebuild from scratch.
5. Write back to the same file. Confirm: "Updated `.polaris/agents/<filename>.json`"
Stop here.

### If intent is a new agent description - continue to Step 2.

---

## Step 2 - Understand the plan

Read the user's description carefully and determine:

| Decision | Options |
|---|---|
| Brain type | **Agent + ApteanAzureOpenAIModel** (Option A) for Azure OpenAI-backed agents; **DigitalWorker** (Option B, Coworker) for Claude-backed agents with built-in model selector |
| Tools needed | None (minimal), MCP server tools, ApiToolBuilder, DatabaseToolBuilder, Prompt node |
| RAG needed | Yes / No |
| aptean_component_type | `"APTEAN_AGENT"` for Agent/DigitalWorker flows; `"APTEAN_RAG"` for RAG flows; `"OTHER"` when no better fit |
| mcp_enabled | `true` if any MCP server node present; `false` otherwise |
| starter_prompts | Derive from description or omit (null) |

---

## Step 3 - Look up components

**CRITICAL**: Use `python .polaris/scripts/components.py` for every component. Never guess field names or IDs.

```bash
# List all available components
python .polaris/scripts/components.py list

# Search for a component
python .polaris/scripts/components.py search <keyword>

# Get full template for one component
python .polaris/scripts/components.py detail <ComponentType>

# Get full templates for multiple components in one call (preferred)
python .polaris/scripts/components.py detail-multi <id1> <id2> ...
```

**Minimal agent (Option A)**: Run:
```bash
python .polaris/scripts/components.py detail-multi Agent ApteanAzureOpenAIModel
```

**Minimal agent (Option B - DigitalWorker)**: Run:
```bash
python .polaris/scripts/components.py detail DigitalWorker
```

**For every tool/MCP component in the plan**: Run `detail` before building. Never hand-write template fields.

Copy template fields **verbatim** from the script output. Strip only these UI-only metadata keys that must never appear in the output JSON:
`info`, `placeholder`, `display_name`, `options`, `options_metadata`, `combobox`, `toggle`, `dialog_inputs`, `required`, `dynamic`, `real_time_refresh`, `refresh_button`, `refresh_on_open`, `title_case`, `runtime`, `input_types`, `multiline`, `trace_as_*`, `code_ref`, `lf_version`

---

## Step 4 - Generate node IDs

Format: `{ComponentType}-{5CharCode}` where the 5-character suffix is randomly generated alphanumeric mixed case.

Examples: `ChatInput-qW6vS`, `Agent-QdIzN`, `ApteanAzureOpenAIModel-Rp3mK`

Every node in the graph must have a unique ID.

---

## Step 5 - Build nodes

### Node object structure (all nodes)

```json
{
  "id": "{ComponentType}-{5CharCode}",
  "type": "genericNode",
  "dragging": false,
  "draggable": true,
  "selectable": true,
  "selected": false,
  "position": { "x": 200, "y": 200 },
  "measured": { "width": 320, "height": 200 },
  "data": {
    "id": "{ComponentType}-{5CharCode}",
    "type": "{ComponentType}",
    "showNode": true,
    "node": { }
  }
}
```

**Position layout (left-to-right)**:
- ChatInput, Prompt: x 100-200
- Model/Tool nodes: x 100-400
- Agent/DigitalWorker node: x 600-700
- ChatOutput: x 1000-1100

**showNode / minimized**:
- ChatInput, ChatOutput: `"showNode": false`, add `"minimized": true`
- Agent, DigitalWorker, Model, Tool nodes: `"showNode": true`

### 5a - ChatInput node

```json
{
  "display_name": "Chat Input",
  "base_classes": ["Message"],
  "description": "Get chat inputs from the Playground.",
  "documentation": "",
  "field_order": ["input_value", "should_store_message", "sender", "sender_name", "session_id", "files"],
  "outputs": [
    {
      "name": "message",
      "cache": true,
      "types": ["Message"],
      "value": "__UNDEFINED__",
      "method": "message_response",
      "selected": "Message",
      "tool_mode": false,
      "allows_loop": false,
      "display_name": "Message",
      "group_outputs": false,
      "real_time_refresh": false
    }
  ],
  "template": {
    "_type": "Component",
    "input_value": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "",
      "advanced": false,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "should_store_message": {
      "type": "bool",
      "_input_type": "BoolInput",
      "value": true,
      "advanced": true,
      "show": true,
      "list": false,
      "list_add_label": "Add More"
    },
    "sender": {
      "type": "str",
      "_input_type": "DropdownInput",
      "value": "User",
      "advanced": true,
      "show": true
    },
    "sender_name": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "User",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "session_id": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "files": {
      "type": "file",
      "_input_type": "FileInput",
      "value": "",
      "advanced": true,
      "show": true,
      "list": true,
      "list_add_label": "Add More"
    }
  }
}
```

### 5b - ChatOutput node

```json
{
  "display_name": "Chat Output",
  "base_classes": ["Message"],
  "description": "Display a chat message in the Playground.",
  "documentation": "",
  "field_order": ["input_value", "should_store_message", "sender", "sender_name", "session_id", "data_template", "background_color", "chat_icon", "text_color", "clean_data"],
  "outputs": [
    {
      "name": "message",
      "cache": true,
      "types": ["Message"],
      "value": "__UNDEFINED__",
      "method": "message_response",
      "selected": "Message",
      "tool_mode": true,
      "allows_loop": false,
      "display_name": "Output Message",
      "group_outputs": false,
      "real_time_refresh": false
    }
  ],
  "template": {
    "_type": "Component",
    "input_value": {
      "type": "other",
      "_input_type": "HandleInput",
      "value": "",
      "advanced": false,
      "show": true,
      "list": true,
      "list_add_label": "Add More"
    },
    "sender": {
      "type": "str",
      "_input_type": "DropdownInput",
      "value": "Machine",
      "advanced": true,
      "show": true
    },
    "sender_name": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "AI",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "session_id": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "data_template": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "{text}",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "should_store_message": {
      "type": "bool",
      "_input_type": "BoolInput",
      "value": true,
      "advanced": true,
      "show": true,
      "list": false,
      "list_add_label": "Add More"
    },
    "clean_data": {
      "type": "bool",
      "_input_type": "BoolInput",
      "value": true,
      "advanced": true,
      "show": true,
      "list": false,
      "list_add_label": "Add More"
    },
    "background_color": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "chat_icon": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    },
    "text_color": {
      "type": "str",
      "_input_type": "MessageTextInput",
      "value": "",
      "advanced": true,
      "show": true,
      "load_from_db": false,
      "list": false,
      "list_add_label": "Add More"
    }
  }
}
```

### 5c - Agent node (Option A)

Copy the full template verbatim from `python .polaris/scripts/components.py detail Agent`.

Additional required literal fields on the Agent node's `data` object:
```json
"key": "Agent",
"category": "agents"
```

Set `system_prompt` value to describe what the agent should do.

### 5d - ApteanAzureOpenAIModel node (Option A)

Copy full template verbatim from `python .polaris/scripts/components.py detail ApteanAzureOpenAIModel`.

### 5e - DigitalWorker node (Option B)

Copy full template verbatim from `python .polaris/scripts/components.py detail DigitalWorker`.

Key DigitalWorker rules:
- Has a built-in model selector - no separate model node needed or permitted. Never wire a model component to it.
- Set `enable_fast_path` to `true` (always on - allows auto-selection of a faster model for simple inputs).
- To add skills: set `template.skills.value` to a list of slug strings (e.g. `["pricing-skill", "customer-history-skill"]`). Do not create edges for skills.

### 5f - Tool nodes (ApiToolBuilder, DatabaseToolBuilder, MCP servers, etc.)

Look up every tool component before building:
```bash
python .polaris/scripts/components.py detail <ComponentType>
```

**ApiToolBuilder** - key fields to configure:
- `base_url` - base URL for all endpoints. Supports `${VAR_NAME}` for global variables.
- `default_headers_json` - JSON object of headers for every request.
- `api_tools` - table of tool definitions (`_input_type: "TableInput"`).
- `tools_metadata` - required field mirroring api_tools. See schema below.

api_tools row fields: `tool_name`, `description`, `method`, `endpoint`, `headers_json`, `query_params_json`, `body_json`, `body_type`, `parameter_types`, `api_definition`, `enabled`

Variable notation for ApiToolBuilder:
- In `endpoint`: use `{param_name}` (single braces) for agent-supplied params
- In `body_json`, `headers_json`, `query_params_json`: use `{{param_name}}` (double braces)
- For global variables: use `${VAR_NAME}` in any field

`parameter_types` is a serialised JSON string:
```json
"parameter_types": "[{\"name\":\"id\",\"type\":\"str\"},{\"name\":\"status\",\"type\":\"str\"}]"
```

**DatabaseToolBuilder** - key fields:
- `database_url` - SQLAlchemy connection string.
- `sql_tools` - table of tool definitions.
- `tools_metadata` - required.
- `allow_write_queries` - default false.

SQL templates use `:param_name` placeholders - never `{}` or `{{}}`.

**tools_metadata schema** (required on both builders):
```json
{
  "name": "tool_name",
  "description": "Description shown to the agent",
  "tags": ["tool_name", "api"],
  "status": true,
  "display_name": "tool_name",
  "display_description": "Description shown to the agent",
  "readonly": false,
  "args": {
    "param_name": {
      "description": "Parameter: param_name",
      "title": "Param Name",
      "type": "string"
    }
  }
}
```

Rules: one entry per tool, `args` type is always `"string"`, `status: true` = enabled.

**MCP server nodes**: `detail` shows only `response: Message` output. When wiring to Agent tools, the relevant output is `component_as_tool: Tool`.

### 5g - Prompt node

Use when combining multiple inputs or building a dynamic system prompt.
Template string uses `{variable_name}` placeholders - each creates a named input handle.

Without variables - `custom_fields.template` is an empty list `[]`.
With variables - `custom_fields.template` lists all variable names; each variable gets a `DefaultPromptField` entry (full field set required - do not strip).

```json
{
  "custom_fields": { "template": ["user_input", "context"] },
  "field_order": ["template", "context_data", "tool_placeholder"],
  "template": {
    "_type": "Component",
    "user_input": {
      "_input_type": "DefaultPromptField",
      "advanced": false,
      "display_name": "user_input",
      "dynamic": true,
      "field_type": "str",
      "fileTypes": [],
      "file_path": "",
      "info": "",
      "input_types": ["Message"],
      "list": false,
      "load_from_db": false,
      "multiline": true,
      "name": "user_input",
      "placeholder": "",
      "required": false,
      "runtime": false,
      "show": true,
      "title_case": false,
      "type": "str",
      "value": ""
    },
    "template": {
      "_input_type": "PromptInput",
      "advanced": false,
      "list": false,
      "list_add_label": "Add More",
      "load_from_library": false,
      "show": true,
      "type": "prompt",
      "value": "Your prompt text with {user_input} here"
    },
    "context_data": {
      "_input_type": "DataInput",
      "advanced": true,
      "list": false,
      "list_add_label": "Add More",
      "show": true,
      "type": "other",
      "value": ""
    },
    "tool_placeholder": {
      "_input_type": "MessageTextInput",
      "advanced": true,
      "list": false,
      "list_add_label": "Add More",
      "load_from_db": false,
      "show": true,
      "tool_mode": true,
      "type": "str",
      "value": ""
    }
  }
}
```

---

## Step 6 - Build edges

### Edge encoding rule (CRITICAL)

Replace all `"` (U+0022) with `œ` (U+0153, Latin small letter oe) in:
- `sourceHandle` string
- `targetHandle` string
- the edge `id` string

### Edge object structure

```json
{
  "source": "{sourceNodeId}",
  "sourceHandle": "{encodedSourceHandleJSON}",
  "target": "{targetNodeId}",
  "targetHandle": "{encodedTargetHandleJSON}",
  "id": "xy-edge__{sourceNodeId}{encodedSourceHandle}-{targetNodeId}{encodedTargetHandle}",
  "data": {
    "sourceHandle": {
      "output_types": ["<OutputType>"],
      "id": "{sourceNodeId}",
      "dataType": "{ComponentType}",
      "name": "{outputName}"
    },
    "targetHandle": {
      "type": "<type>",
      "fieldName": "<fieldName>",
      "id": "{targetNodeId}",
      "inputTypes": ["<InputType>"]
    }
  }
}
```

The `sourceHandle` and `targetHandle` strings are the encoded handle JSON ONLY - they must start with `{œ`. The node ID prefix appears ONLY inside the edge `id` (`xy-edge__{sourceNodeId}{encodedSourceHandle}-{targetNodeId}{encodedTargetHandle}`). Prepending the node ID to the handle strings breaks Studio import with: `Unexpected token 'C', "ChatInput-"... is not valid JSON` (the importer decodes each handle string back to JSON).

### Standard connection table

| From | To | inputTypes | type |
|---|---|---|---|
| ChatInput `message` | Agent `input_value` | `["Message"]` | `"str"` |
| ChatInput `message` | Prompt variable | `["Message"]` | `"str"` |
| Prompt `prompt` | Agent `input_value` | `["Message"]` | `"str"` |
| Prompt `prompt` | Agent `system_prompt` | `["Message"]` | `"str"` |
| ApteanAzureOpenAIModel `model_output` | Agent `llm` | `["AzureLanguageModel"]` | `"other"` |
| Any tool `component_as_tool` | Agent `tools` | `["Tool"]` | `"other"` |
| Agent `response` | ChatOutput `input_value` | `["Data","DataFrame","Message"]` | `"other"` |
| RAG crawler output | Agent `input_value` | `["Message"]` | `"str"` |

### Minimal agent edge example (3 edges)

**Edge 1: ChatInput (message) -> Agent (input_value)**

Unencoded sourceHandle JSON: `{"output_types":["Message"],"id":"ChatInput-qW6vS","dataType":"ChatInput","name":"message"}`
Unencoded targetHandle JSON: `{"type":"str","fieldName":"input_value","id":"Agent-QdIzN","inputTypes":["Message"]}`

After encoding (replace `"` with `œ`):
```json
{
  "source": "ChatInput-qW6vS",
  "sourceHandle": "{œoutput_typesœ:[œMessageœ],œidœ:œChatInput-qW6vSœ,œdataTypeœ:œChatInputœ,œnameœ:œmessageœ}",
  "target": "Agent-QdIzN",
  "targetHandle": "{œtypeœ:œstrœ,œfieldNameœ:œinput_valueœ,œidœ:œAgent-QdIzNœ,œinputTypesœ:[œMessageœ]}",
  "id": "xy-edge__ChatInput-qW6vS{œoutput_typesœ:[œMessageœ],œidœ:œChatInput-qW6vSœ,œdataTypeœ:œChatInputœ,œnameœ:œmessageœ}-Agent-QdIzN{œtypeœ:œstrœ,œfieldNameœ:œinput_valueœ,œidœ:œAgent-QdIzNœ,œinputTypesœ:[œMessageœ]}",
  "data": {
    "sourceHandle": {
      "output_types": ["Message"],
      "id": "ChatInput-qW6vS",
      "dataType": "ChatInput",
      "name": "message"
    },
    "targetHandle": {
      "type": "str",
      "fieldName": "input_value",
      "id": "Agent-QdIzN",
      "inputTypes": ["Message"]
    }
  }
}
```

Apply the same encoding pattern to all other edges.

---

## Step 7 - Assemble the top-level JSON

```json
{
  "name": "<agent name from description>",
  "description": "<one-line description>",
  "icon": null,
  "icon_bg_color": null,
  "gradient": null,
  "data": {
    "nodes": [ ],
    "edges": [ ],
    "viewport": { "x": 0, "y": 0, "zoom": 1.0 }
  },
  "is_component": false,
  "aptean_component_type": "<APTEAN_AGENT|APTEAN_RAG|OTHER>",
  "updated_at": "2026-01-01T00:00:00+00:00",
  "webhook": false,
  "endpoint_name": null,
  "tags": [],
  "locked": false,
  "mcp_enabled": <true if any MCP node present, else false>,
  "action_name": null,
  "action_description": null,
  "starter_prompts": null,
  "access_type": "PRIVATE",
  "run_as_background": false,
  "is_sensitive": false,
  "certified": false,
  "api_key_id": null,
  "id": "",
  "user_id": "",
  "coid": "",
  "folder_id": ""
}
```

Rules:
- `id`, `user_id`, `coid`, `folder_id` - always `""` (platform assigns on import)
- `aptean_component_type` - must be exactly one of `"APTEAN_RAG"`, `"APTEAN_AGENT"`, `"APTEAN_DG"`, `"OTHER"`. Never empty string.
- `starter_prompts` - array of `{"name": "snake_case_id", "template": "Message text", "title": "Short Label"}` objects when provided, or `null`. Never plain strings.
- `mcp_enabled` - `true` when any MCP server node is in the graph.
- `is_component` - always `false`.

---

## Step 8 - Write the output file

Filename: `.polaris/agents/<snake_case_agent_name>.json`

**Extension must be exactly `.json` (lowercase). Never `.JSO`, `.JSON`, or any other variant.**

Write the complete JSON to that file.

Confirm with:
```
Created: .polaris/agents/<filename>.json

Import this file into Aptean Intelligence Studio:
  Studio portal > Import > select .polaris/agents/<filename>.json

Nodes: <count>
Edges: <count>
Brain: <Agent+ApteanAzureOpenAIModel | DigitalWorker>
Tools: <list or "none">
mcp_enabled: <true|false>
```

---

## Pre-Build Checklist

Before assembling any JSON:
- Run `.polaris/scripts/components.py detail` for every component not fully defined above
- Refer to Steps 5a-5g above for the full verbatim node structures (ChatInput, ChatOutput, Prompt) not covered by the component registry
- If the plan has skills: confirm all slugs exist in the Skill Pool; create missing ones via skill-builder; add slugs to `template.skills.value` on the DigitalWorker node
- If the plan is an edit: load the existing file first; apply only the stated changes; do not rebuild
- Set `mcp_enabled: true` on top-level JSON if any MCP server node is present
- Output file extension is always `.json` (lowercase)

**Telemetry**: Run: `polaris telemetry record agent-builder --phase complete --agent claude`
