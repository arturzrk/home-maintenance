"""Aptean Intelligence Studio component registry.

Provides verbatim JSON templates for Studio components so the
/polaris.agent-builder command never has to guess field names or IDs.

Usage:
    python .polaris/scripts/components.py list
    python .polaris/scripts/components.py search <keyword>
    python .polaris/scripts/components.py detail <ComponentType>
    python .polaris/scripts/components.py detail-multi <id1> <id2> ...
"""

from __future__ import annotations

import json
import sys

# ---------------------------------------------------------------------------
# Component registry
# Each entry is the verbatim "node" template.  UI-only metadata keys that
# must be stripped from the final output are NOT included here so callers
# do not need to filter them.
#
# Keys stripped per agent-builder.md Step 3:
#   info, placeholder, display_name, options, options_metadata, combobox,
#   toggle, dialog_inputs, required, dynamic, real_time_refresh,
#   refresh_button, refresh_on_open, title_case, runtime, input_types,
#   multiline, trace_as_*, code_ref, lf_version
# ---------------------------------------------------------------------------

_REGISTRY: dict[str, dict] = {
    "Agent": {
        "display_name": "Agent",
        "base_classes": ["Message"],
        "description": "Define the agent's instructions, then connect tools and a model to it.",
        "documentation": "",
        "field_order": [
            "input_value",
            "system_prompt",
            "llm",
            "tools",
            "memory",
            "max_iterations",
            "handle_parsing_errors",
            "verbose",
        ],
        "outputs": [
            {
                "name": "response",
                "cache": True,
                "types": ["Message"],
                "value": "__UNDEFINED__",
                "method": "run_agent",
                "selected": "Message",
                "tool_mode": False,
                "allows_loop": False,
                "display_name": "Response",
                "group_outputs": False,
                "real_time_refresh": False,
            }
        ],
        "template": {
            "_type": "Component",
            "input_value": {
                "type": "str",
                "_input_type": "MessageTextInput",
                "value": "",
                "advanced": False,
                "show": True,
                "load_from_db": False,
                "list": False,
                "list_add_label": "Add More",
                "tool_mode": True,
            },
            "system_prompt": {
                "type": "str",
                "_input_type": "MultilineInput",
                "value": "",
                "advanced": False,
                "show": True,
                "load_from_db": False,
                "list": False,
                "list_add_label": "Add More",
            },
            "llm": {
                "type": "other",
                "_input_type": "HandleInput",
                "value": "",
                "advanced": False,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
                "input_types": ["LanguageModel"],
            },
            "tools": {
                "type": "other",
                "_input_type": "HandleInput",
                "value": "",
                "advanced": False,
                "show": True,
                "list": True,
                "list_add_label": "Add More",
                "input_types": ["Tool"],
            },
            "memory": {
                "type": "other",
                "_input_type": "HandleInput",
                "value": "",
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
                "input_types": ["BaseChatMessageHistory"],
            },
            "max_iterations": {
                "type": "int",
                "_input_type": "IntInput",
                "value": 15,
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "handle_parsing_errors": {
                "type": "bool",
                "_input_type": "BoolInput",
                "value": True,
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "verbose": {
                "type": "bool",
                "_input_type": "BoolInput",
                "value": True,
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
        },
    },

    "ApteanAzureOpenAIModel": {
        "display_name": "Aptean Azure OpenAI",
        "base_classes": ["AzureLanguageModel"],
        "description": "Generates text using Aptean-managed Azure OpenAI deployments.",
        "documentation": "",
        "field_order": [
            "model_name",
            "temperature",
            "max_tokens",
            "top_p",
            "stream",
        ],
        "outputs": [
            {
                "name": "model_output",
                "cache": True,
                "types": ["AzureLanguageModel"],
                "value": "__UNDEFINED__",
                "method": "build_model",
                "selected": "AzureLanguageModel",
                "tool_mode": False,
                "allows_loop": False,
                "display_name": "Language Model",
                "group_outputs": False,
                "real_time_refresh": False,
            }
        ],
        "template": {
            "_type": "Component",
            "model_name": {
                "type": "str",
                "_input_type": "DropdownInput",
                "value": "gpt-4o",
                "advanced": False,
                "show": True,
                "options": ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-35-turbo"],
            },
            "temperature": {
                "type": "float",
                "_input_type": "FloatInput",
                "value": 0.1,
                "advanced": False,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "max_tokens": {
                "type": "int",
                "_input_type": "IntInput",
                "value": 4096,
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "top_p": {
                "type": "float",
                "_input_type": "FloatInput",
                "value": 1.0,
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "stream": {
                "type": "bool",
                "_input_type": "BoolInput",
                "value": False,
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
        },
    },

    "DigitalWorker": {
        "display_name": "Digital Worker",
        "base_classes": ["Message"],
        "description": "Coworker-style agent with a built-in model selector and optional skills.",
        "documentation": "",
        "field_order": [
            "input_value",
            "system_prompt",
            "skills",
            "tools",
            "enable_fast_path",
            "max_iterations",
        ],
        "outputs": [
            {
                "name": "response",
                "cache": True,
                "types": ["Message"],
                "value": "__UNDEFINED__",
                "method": "run_agent",
                "selected": "Message",
                "tool_mode": False,
                "allows_loop": False,
                "display_name": "Response",
                "group_outputs": False,
                "real_time_refresh": False,
            }
        ],
        "template": {
            "_type": "Component",
            "input_value": {
                "type": "str",
                "_input_type": "MessageTextInput",
                "value": "",
                "advanced": False,
                "show": True,
                "load_from_db": False,
                "list": False,
                "list_add_label": "Add More",
                "tool_mode": True,
            },
            "system_prompt": {
                "type": "str",
                "_input_type": "MultilineInput",
                "value": "",
                "advanced": False,
                "show": True,
                "load_from_db": False,
                "list": False,
                "list_add_label": "Add More",
            },
            "skills": {
                "type": "str",
                "_input_type": "StrInput",
                "value": [],
                "advanced": False,
                "show": True,
                "list": True,
                "list_add_label": "Add Skill Slug",
            },
            "tools": {
                "type": "other",
                "_input_type": "HandleInput",
                "value": "",
                "advanced": False,
                "show": True,
                "list": True,
                "list_add_label": "Add More",
                "input_types": ["Tool"],
            },
            "enable_fast_path": {
                "type": "bool",
                "_input_type": "BoolInput",
                "value": True,
                "advanced": False,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "max_iterations": {
                "type": "int",
                "_input_type": "IntInput",
                "value": 15,
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
        },
    },

    "ApiToolBuilder": {
        "display_name": "API Tool Builder",
        "base_classes": ["Tool"],
        "description": "Expose REST API endpoints as agent tools.",
        "documentation": "",
        "field_order": [
            "base_url",
            "default_headers_json",
            "api_tools",
            "tools_metadata",
        ],
        "outputs": [
            {
                "name": "component_as_tool",
                "cache": True,
                "types": ["Tool"],
                "value": "__UNDEFINED__",
                "method": "build_tool",
                "selected": "Tool",
                "tool_mode": False,
                "allows_loop": False,
                "display_name": "Toolset",
                "group_outputs": False,
                "real_time_refresh": False,
            }
        ],
        "template": {
            "_type": "Component",
            "base_url": {
                "type": "str",
                "_input_type": "MessageTextInput",
                "value": "",
                "advanced": False,
                "show": True,
                "load_from_db": False,
                "list": False,
                "list_add_label": "Add More",
            },
            "default_headers_json": {
                "type": "str",
                "_input_type": "MessageTextInput",
                "value": "{}",
                "advanced": True,
                "show": True,
                "load_from_db": False,
                "list": False,
                "list_add_label": "Add More",
            },
            "api_tools": {
                "type": "table",
                "_input_type": "TableInput",
                "value": [],
                "advanced": False,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "tools_metadata": {
                "type": "str",
                "_input_type": "StrInput",
                "value": "[]",
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
        },
    },

    "DatabaseToolBuilder": {
        "display_name": "Database Tool Builder",
        "base_classes": ["Tool"],
        "description": "Expose SQL queries as agent tools.",
        "documentation": "",
        "field_order": [
            "database_url",
            "allow_write_queries",
            "sql_tools",
            "tools_metadata",
        ],
        "outputs": [
            {
                "name": "component_as_tool",
                "cache": True,
                "types": ["Tool"],
                "value": "__UNDEFINED__",
                "method": "build_tool",
                "selected": "Tool",
                "tool_mode": False,
                "allows_loop": False,
                "display_name": "Toolset",
                "group_outputs": False,
                "real_time_refresh": False,
            }
        ],
        "template": {
            "_type": "Component",
            "database_url": {
                "type": "str",
                "_input_type": "MessageTextInput",
                "value": "",
                "advanced": False,
                "show": True,
                "load_from_db": False,
                "list": False,
                "list_add_label": "Add More",
            },
            "allow_write_queries": {
                "type": "bool",
                "_input_type": "BoolInput",
                "value": False,
                "advanced": False,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "sql_tools": {
                "type": "table",
                "_input_type": "TableInput",
                "value": [],
                "advanced": False,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
            "tools_metadata": {
                "type": "str",
                "_input_type": "StrInput",
                "value": "[]",
                "advanced": True,
                "show": True,
                "list": False,
                "list_add_label": "Add More",
            },
        },
    },
}


def _cmd_list() -> None:
    rows = []
    for name, comp in sorted(_REGISTRY.items()):
        rows.append(f"{name:<35} {comp.get('display_name', name):<35} {comp.get('description', '')}")
    print("\n".join(rows))


def _cmd_search(keyword: str) -> None:
    kw = keyword.lower()
    found = []
    for name, comp in sorted(_REGISTRY.items()):
        haystack = " ".join([
            name,
            comp.get("display_name", ""),
            comp.get("description", ""),
        ]).lower()
        if kw in haystack:
            found.append(f"{name:<35} {comp.get('display_name', name)}")
    if found:
        print("\n".join(found))
    else:
        print(f"No components matching '{keyword}'")


def _cmd_detail(component_type: str) -> None:
    comp = _REGISTRY.get(component_type)
    if comp is None:
        print(f"ERROR: Unknown component '{component_type}'", file=sys.stderr)
        print(f"Available: {', '.join(sorted(_REGISTRY))}", file=sys.stderr)
        sys.exit(1)
    print(json.dumps(comp, indent=2))


def _cmd_detail_multi(component_types: list[str]) -> None:
    result = {}
    missing = []
    for ct in component_types:
        comp = _REGISTRY.get(ct)
        if comp is None:
            missing.append(ct)
        else:
            result[ct] = comp
    if missing:
        print(f"ERROR: Unknown component(s): {', '.join(missing)}", file=sys.stderr)
        print(f"Available: {', '.join(sorted(_REGISTRY))}", file=sys.stderr)
        sys.exit(1)
    print(json.dumps(result, indent=2))


def main() -> None:
    args = sys.argv[1:]
    if not args or args[0] == "list":
        _cmd_list()
    elif args[0] == "search":
        if len(args) < 2:
            print("Usage: components.py search <keyword>", file=sys.stderr)
            sys.exit(1)
        _cmd_search(args[1])
    elif args[0] == "detail":
        if len(args) < 2:
            print("Usage: components.py detail <ComponentType>", file=sys.stderr)
            sys.exit(1)
        _cmd_detail(args[1])
    elif args[0] == "detail-multi":
        if len(args) < 2:
            print("Usage: components.py detail-multi <id1> <id2> ...", file=sys.stderr)
            sys.exit(1)
        _cmd_detail_multi(args[1:])
    else:
        print(f"Unknown subcommand '{args[0]}'. Use: list, search, detail, detail-multi", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
