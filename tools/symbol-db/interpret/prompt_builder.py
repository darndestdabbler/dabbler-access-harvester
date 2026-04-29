"""Per-symbol prompt assembly.

The prompt is the rendered template plus a shared header. The shared
header defines the response contract (JSON block at the end). Each
template is a tiny mustache-style scaffold whose only directives are::

    {{NAME}}                    – literal substitution
    {{#BLOCK}}...{{/BLOCK}}     – include the block iff the BLOCK key
                                  is truthy in the context

We do not need a real templating engine: the templates are
hand-curated and fixed by the spec.
"""

from __future__ import annotations

import os
import re
import sqlite3
from dataclasses import dataclass, field
from typing import Optional

from . import db


HERE = os.path.dirname(os.path.abspath(__file__))
PROMPTS_DIR = os.path.join(HERE, "prompts")


_BLOCK_RE = re.compile(r"\{\{#(\w+)\}\}(.*?)\{\{/\1\}\}", re.DOTALL)
_VAR_RE = re.compile(r"\{\{(\w+)\}\}")


@dataclass
class Prompt:
    text: str
    template_name: str
    context: dict = field(default_factory=dict)


def _read_template(name: str) -> str:
    with open(os.path.join(PROMPTS_DIR, name), encoding="utf-8") as f:
        return f.read()


def _render(template: str, context: dict) -> str:
    """Render a template with our minimal mustache-style directives."""
    def block_replace(m):
        key = m.group(1)
        body = m.group(2)
        if context.get(key):
            return body
        return ""
    text = _BLOCK_RE.sub(block_replace, template)
    text = _VAR_RE.sub(
        lambda m: str(context.get(m.group(1), "") or ""),
        text,
    )
    return text.strip() + "\n"


def _shared_header() -> str:
    return _read_template("_shared.txt").rstrip() + "\n\n"


def build_symbol_prompt(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
    callee_summaries: list[tuple[str, str]],   # (callee_fqn, summary_sentence)
    caller_summaries: list[tuple[str, str]],   # (caller_fqn, summary_sentence)
    *,
    enum_interpretation: Optional[str] = None,
    classification: Optional[str] = None,
) -> Prompt:
    """Render the per-symbol prompt for one Symbol row."""
    shared = _shared_header()
    template_name, body = _render_for_kind(
        con, sym, callee_summaries, caller_summaries,
        enum_interpretation=enum_interpretation,
        classification=classification,
    )
    return Prompt(
        text=shared + body,
        template_name=template_name,
        context={"fqn": sym.fqn, "kind": sym.kind},
    )


def _render_for_kind(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
    callee_summaries: list[tuple[str, str]],
    caller_summaries: list[tuple[str, str]],
    *,
    enum_interpretation: Optional[str],
    classification: Optional[str],
) -> tuple[str, str]:
    """Pick the template, populate its context, return (name, rendered)."""
    if sym.kind in ("function", "sub"):
        return _render_procedure(
            con, sym, callee_summaries, caller_summaries,
            classification=classification,
        )
    if sym.kind in ("property_get", "property_let", "property_set"):
        return _render_property(
            con, sym, callee_summaries, caller_summaries,
        )
    if sym.kind == "enum_decl":
        return _render_enum_decl(con, sym, caller_summaries)
    if sym.kind == "enum_value":
        return _render_enum_value(con, sym, caller_summaries, enum_interpretation)
    if sym.kind in ("module_var", "module_const"):
        return _render_module_var(con, sym)
    raise NotImplementedError(f"No prompt template for kind {sym.kind!r}")


def _render_procedure(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
    callee_summaries: list[tuple[str, str]],
    caller_summaries: list[tuple[str, str]],
    *,
    classification: Optional[str],
) -> tuple[str, str]:
    parameters = db.parameters_for(con, sym.fqn)
    params_text = "\n".join(_format_parameter(p) for p in parameters)

    binding = db.event_binding_for(con, sym.fqn)
    callees_text = _format_summary_list(callee_summaries, max_count=12)
    callers_text = _format_summary_list(caller_summaries, max_count=8)

    ctx = {
        "KIND": sym.kind,
        "ACCESS": sym.access or "Unspecified",
        "MODULE_FQN": sym.module_fqn,
        "FQN": sym.fqn,
        "SIGNATURE": sym.signature_string or "(no signature)",
        "RETURN_TYPE": sym.data_type or "(none)",
        "PARAMETERS_LIST": params_text,
        "PARAMETERS_BLOCK": bool(params_text),
        "EVENT_BINDING_BLOCK": bool(binding),
        "EVENT_NAME": (binding or {}).get("event_name", ""),
        "CONTROL_NAME": (binding or {}).get("control_name", "(form-level)"),
        "FORM_FQN": (binding or {}).get("form_fqn", ""),
        "CALLEE_SUMMARIES_BLOCK": bool(callees_text),
        "CALLEE_SUMMARIES": callees_text,
        "CALLER_SUMMARIES_BLOCK": bool(callers_text),
        "CALLER_SUMMARIES": callers_text,
        "CLASSIFICATION_BLOCK": bool(classification),
        "CLASSIFICATION": classification or "",
        "SOURCE_BODY": sym.source_body or "",
    }
    return ("function_sub.txt", _render(_read_template("function_sub.txt"), ctx))


def _render_property(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
    callee_summaries: list[tuple[str, str]],
    caller_summaries: list[tuple[str, str]],
) -> tuple[str, str]:
    callees_text = _format_summary_list(callee_summaries, max_count=8)
    callers_text = _format_summary_list(caller_summaries, max_count=8)
    ctx = {
        "KIND": sym.kind,
        "ACCESS": sym.access or "Unspecified",
        "MODULE_FQN": sym.module_fqn,
        "FQN": sym.fqn,
        "SIGNATURE": sym.signature_string or "(no signature)",
        "CALLEE_SUMMARIES_BLOCK": bool(callees_text),
        "CALLEE_SUMMARIES": callees_text,
        "CALLER_SUMMARIES_BLOCK": bool(callers_text),
        "CALLER_SUMMARIES": callers_text,
        "SOURCE_BODY": sym.source_body or "",
    }
    return ("property.txt", _render(_read_template("property.txt"), ctx))


def _render_enum_decl(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
    caller_summaries: list[tuple[str, str]],
) -> tuple[str, str]:
    rows = con.execute(
        """
        SELECT name FROM Symbols
        WHERE module_fqn = ? AND kind = 'enum_value' AND declared_in_fqn = ?
        ORDER BY source_line_start
        """,
        (sym.module_fqn, sym.fqn),
    ).fetchall()
    names = [r["name"] for r in rows]
    sample = ", ".join(names[:24])
    if len(names) > 24:
        sample += f", … (+{len(names) - 24} more)"
    callers_text = _format_summary_list(caller_summaries, max_count=8)
    ctx = {
        "MODULE_FQN": sym.module_fqn,
        "FQN": sym.fqn,
        "VALUE_COUNT": len(names),
        "SAMPLE_VALUES": sample,
        "CALLER_SUMMARIES_BLOCK": bool(callers_text),
        "CALLER_SUMMARIES": callers_text,
    }
    return ("enum_decl.txt", _render(_read_template("enum_decl.txt"), ctx))


def _render_enum_value(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
    caller_summaries: list[tuple[str, str]],
    enum_interpretation: Optional[str],
) -> tuple[str, str]:
    ev = con.execute(
        "SELECT enum_fqn, numeric_value FROM EnumValues WHERE enum_value_fqn = ?",
        (sym.fqn,),
    ).fetchone()
    enum_fqn = ev["enum_fqn"] if ev else (sym.declared_in_fqn or "(unknown)")
    numeric = ev["numeric_value"] if ev else None
    callers_text = _format_summary_list(caller_summaries, max_count=4)
    ctx = {
        "MODULE_FQN": sym.module_fqn,
        "FQN": sym.fqn,
        "NAME": sym.name,
        "NUMERIC_VALUE": str(numeric) if numeric is not None else "(unset)",
        "ENUM_FQN": enum_fqn,
        "ENUM_INTERPRETATION": enum_interpretation or "",
        "CALLER_SUMMARIES_BLOCK": bool(callers_text),
        "CALLER_SUMMARIES": callers_text,
    }
    return ("enum_value.txt", _render(_read_template("enum_value.txt"), ctx))


def _render_module_var(
    con: sqlite3.Connection,
    sym: db.SymbolRow,
) -> tuple[str, str]:
    refs = con.execute(
        """
        SELECT from_fqn, kind FROM [References]
        WHERE to_fqn = ?
        ORDER BY from_fqn
        """,
        (sym.fqn,),
    ).fetchall()
    readers: list[str] = []
    writers: list[str] = []
    for r in refs:
        # Without write-context detection, treat all access as reads
        # except when explicitly tagged variable_write (rare).
        if r["kind"] == "variable_write":
            writers.append(r["from_fqn"])
        else:
            readers.append(r["from_fqn"])
    readers_text = "\n".join(f"  - {x}" for x in sorted(set(readers))[:12])
    writers_text = "\n".join(f"  - {x}" for x in sorted(set(writers))[:12])
    ctx = {
        "ACCESS": sym.access or "Unspecified",
        "MODULE_FQN": sym.module_fqn,
        "FQN": sym.fqn,
        "NAME": sym.name,
        "DATA_TYPE": sym.data_type or "Variant",
        "READERS_BLOCK": bool(readers_text),
        "READERS_LIST": readers_text,
        "WRITERS_BLOCK": bool(writers_text),
        "WRITERS_LIST": writers_text,
    }
    return ("module_var.txt", _render(_read_template("module_var.txt"), ctx))


def build_macro_prompt(macro: db.MacroRow, actions: list[dict]) -> Prompt:
    shared = _shared_header()
    seq_lines = []
    for a in actions[:30]:
        args = a.get("arguments_json") or ""
        seq_lines.append(f"  {a['position']:>3}. {a['action_name']}  {args}")
    if len(actions) > 30:
        seq_lines.append(f"  … (+{len(actions) - 30} more actions)")
    xml_limit = 4000
    xml = (macro.source_xml or "")[:xml_limit]
    ctx = {
        "FQN": macro.fqn,
        "IS_DEAD": "yes" if macro.is_dead else "no",
        "ACTION_SEQUENCE": "\n".join(seq_lines) or "  (no actions)",
        "XML_LIMIT": str(xml_limit),
        "SOURCE_XML": xml,
    }
    body = _render(_read_template("macro.txt"), ctx)
    return Prompt(text=shared + body, template_name="macro.txt", context={"fqn": macro.fqn})


def build_table_prompt(
    con: sqlite3.Connection, table: db.TableRow
) -> Prompt:
    shared = _shared_header()
    bound = con.execute(
        """
        SELECT form_fqn, control_name, control_type
        FROM FormControls
        WHERE bound_table_or_query = ? OR row_source = ?
        ORDER BY form_fqn, control_name
        """,
        (table.fqn, table.fqn),
    ).fetchall()
    bound_text = "\n".join(
        f"  - {r['form_fqn']}.{r['control_name']} ({r['control_type']})"
        for r in bound[:12]
    )
    ctx = {
        "FQN": table.fqn,
        "PRIMARY_KEY_COLUMN": table.primary_key_column or "(none)",
        "CANDIDATE_LABEL_COLUMN": table.candidate_label_column or "(none)",
        "IS_LOOKUP_TABLE_SCORE": str(table.is_lookup_table_score or 0),
        "IS_LOOKUP_TABLE_REASON": table.is_lookup_table_reason or "(no reason)",
        "BOUND_CONTROLS_BLOCK": bool(bound_text),
        "BOUND_CONTROLS": bound_text,
    }
    body = _render(_read_template("lookup_table.txt"), ctx)
    return Prompt(text=shared + body, template_name="lookup_table.txt", context={"fqn": table.fqn})


def build_query_prompt(
    con: sqlite3.Connection, query: db.QueryRow
) -> Prompt:
    shared = _shared_header()
    consumers = con.execute(
        """
        SELECT form_fqn, control_name FROM FormControls
        WHERE bound_table_or_query = ? OR row_source = ?
        """,
        (query.fqn, query.fqn),
    ).fetchall()
    consumer_text = "\n".join(
        f"  - {r['form_fqn']}.{r['control_name']}" for r in consumers[:8]
    )
    ctx = {
        "FQN": query.fqn,
        "IS_DEAD": "yes" if query.is_dead else "no",
        "EQUIVALENCE_CANONICAL_FQN": query.equivalence_canonical_fqn or "(none)",
        "SOURCE_SQL": query.source_sql,
        "CONSUMER_SUMMARIES_BLOCK": bool(consumer_text),
        "CONSUMER_SUMMARIES": consumer_text,
    }
    body = _render(_read_template("query.txt"), ctx)
    return Prompt(text=shared + body, template_name="query.txt", context={"fqn": query.fqn})


# ── helpers ────────────────────────────────────────────────────────


def _format_parameter(p: dict) -> str:
    parts = []
    if p["passing"]:
        parts.append(p["passing"])
    if p["is_optional"]:
        parts.append("Optional")
    if p["is_paramarray"]:
        parts.append("ParamArray")
    name = p["name"]
    if p["is_array"]:
        name += "()"
    if p["data_type"]:
        name += f" As {p['data_type']}"
    default = f" = {p['default_value']}" if p["default_value"] else ""
    return f"  {p['position']:>2}. {' '.join(parts)} {name}{default}".rstrip()


def _format_summary_list(
    summaries: list[tuple[str, str]], *, max_count: int
) -> str:
    if not summaries:
        return ""
    lines = []
    for fqn, summary in summaries[:max_count]:
        # Cap each summary to ~240 chars to keep prompt size in check.
        s = (summary or "(not yet interpreted)").strip().replace("\n", " ")
        if len(s) > 240:
            s = s[:237] + "..."
        lines.append(f"  - {fqn}: {s}")
    if len(summaries) > max_count:
        lines.append(f"  … (+{len(summaries) - max_count} more)")
    return "\n".join(lines)
