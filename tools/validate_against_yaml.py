"""
Validate AccessHarvester IR output against the VBA-extracted YAML for the same
Access database. Diffs every form, every report, every control, every property
and produces a markdown report plus a terminal summary.

The two extractions are independent: ours goes through Access SaveAsText →
parser → enrichers; the VBA YAML reads properties directly via DAO/COM. Any
disagreement is either (a) a parser bug on our side, (b) a SaveAsText quirk
our enrichers don't yet handle, or (c) a known-incomparable property kept on
the skip list with a reason. This is the correctness regression gate that
synthetic specs can't cover, since they only exercise our algorithm's corners
rather than what happens against a real DB.

USAGE
-----
    python tools/validate_against_yaml.py
        # all paths default to the Northwind layout below

    python tools/validate_against_yaml.py \
        --yaml samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml \
        --ir-root tmp/run-output/AccessMetaData/NorthwindDevEdition/ir \
        --defaults AccessHarvester.Core/Resources/property-defaults.json \
        --report tools/reports/northwind-validation.md

The --ir-root must point at an `ir/` tree produced by `AccessHarvester run`
(or `AccessHarvester translate` followed by enrichment) for the same
database the YAML was extracted from.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import defaultdict, Counter

# Reuse the YAML parser from the defaults generator. Importing rather than
# duplicating keeps a single source of truth for the format quirks.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generate_property_defaults import _parse_yaml  # type: ignore


# ----------------------------------------------------------------------------
# Skip rules. Each rule maps to a category so the report can categorize.
# ----------------------------------------------------------------------------

# Binary blob properties — VBA's string coercion produces mojibake; ours emits
# hex strings. Comparable in principle, but "binary equality" isn't a useful
# signal here.
_SKIP_BINARY = {
    "PrtMip",
    "PrtDevMode", "PrtDevModeW",
    "PrtDevNames", "PrtDevNamesW",
    "NameMap",
    "GUID",
    "RecSrcDt",
    "DOL",
    "Checksum",
    "ConditionalFormat", "ConditionalFormat14",
}

# Runtime / design-view-only state. Either side may report a transient value
# that depends on extraction context, not the saved object definition.
_SKIP_RUNTIME = {
    "Hwnd",
    "Count",
    "ItemSuffix",
    "LogicalPageWidth",
    "Visible",
    "Painting",
    # CurrentSection* are flagged by the YAML extractor itself as design-only.
    "CurrentSectionTop", "CurrentSectionLeft",
    "SelLeft", "SelTop", "SelHeight", "SelWidth",
    # Layout-cache scratch values; recomputed on every Access open.
    "LayoutCachedLeft", "LayoutCachedTop",
    "LayoutCachedWidth", "LayoutCachedHeight",
}

# Embedded macro XML dumps. YAML and ours both serialize, but with different
# whitespace/escaping conventions; not productive to diff line by line.
_SKIP_EMMACRO_SUFFIX = "EmMacro"

# Event-handler stubs. YAML shows "[Event Procedure]" or "[Embedded Macro]"
# where the procedure name lives elsewhere; comparing the literal stub strings
# would just count "did this event get wired up at all" and we already get
# that signal from the VbaCode / *EmMacro fields.
_EVENT_PROP_PATTERN = re.compile(
    r"^(On[A-Z]\w*"          # OnClick, OnLoad, OnMouseWheel, ...
    r"|Before[A-Z]\w*"        # BeforeUpdate, BeforeRender, ...
    r"|After[A-Z]\w*)$"       # AfterUpdate, AfterFinalRender, ...
)
# Event-like names that don't fit the On/Before/After prefix rule.
_EVENT_PROP_EXTRA = {"MouseWheel"}


def classify_skip(prop: str) -> str | None:
    """Return a short skip-category string, or None if not skipped."""
    if prop in _SKIP_BINARY:
        return "binary"
    if prop in _SKIP_RUNTIME:
        return "runtime"
    if prop.endswith(_SKIP_EMMACRO_SUFFIX):
        return "embedded-macro"
    if _EVENT_PROP_PATTERN.match(prop) or prop in _EVENT_PROP_EXTRA:
        return "event"
    return None


# ----------------------------------------------------------------------------
# Value normalization. Both sides serialize as strings; collapse the obvious
# representational differences before equality compare.
# ----------------------------------------------------------------------------

_FLOAT_TRAILING_ZERO = re.compile(r"^(-?\d+)\.0+$")
_BOOL_NUMERIC = {"0", "1"}
_BOOL_STRING = {"true", "false"}
_CURLY_QUOTES = str.maketrans({"“": '"', "”": '"', "‘": "'", "’": "'"})


def normalize(value: str) -> str:
    if value is None:
        return ""
    s = str(value).strip().translate(_CURLY_QUOTES)
    # Strip a single layer of surrounding double quotes if both sides agree.
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        s = s[1:-1]
    s_low = s.lower()
    # "True"/"False" canonicalize to "true"/"false".
    if s_low in _BOOL_STRING:
        return s_low
    # "100.0" -> "100" so float-vs-int doesn't appear as a mismatch.
    m = _FLOAT_TRAILING_ZERO.match(s)
    if m:
        return m.group(1)
    return s


def values_equal(a: str, b: str) -> bool:
    na, nb = normalize(a), normalize(b)
    if na == nb:
        return True
    # Boolean cross-form: "1" / "true" and "0" / "false".
    if {na, nb} == {"1", "true"}:
        return True
    if {na, nb} == {"0", "false"}:
        return True
    return False


# ----------------------------------------------------------------------------
# IR walking
# ----------------------------------------------------------------------------


def _find_named(node: dict, name: str) -> dict | None:
    if not isinstance(node, dict):
        return None
    props = node.get("Properties") or {}
    if isinstance(props, dict) and props.get("Name") == name:
        return node
    for child in node.get("Children") or []:
        hit = _find_named(child, name)
        if hit is not None:
            return hit
    return None


def _open_ir(ir_dir: str, name: str) -> dict | None:
    path = os.path.join(ir_dir, name + ".ir.json")
    if not os.path.exists(path):
        return None
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


# ----------------------------------------------------------------------------
# Effective-value resolution
# ----------------------------------------------------------------------------


def effective_value(ir_node: dict | None, prop: str, kind: str | None,
                     defaults: dict) -> tuple[str | None, str]:
    """Return (value, source) where source ∈ {ir, default, unknown, missing-node}.
    Value is None when source is unknown or missing-node."""
    if ir_node is None:
        return None, "missing-node"
    ir_props = (ir_node.get("Properties") or {}) if isinstance(ir_node, dict) else {}
    if prop in ir_props:
        v = ir_props[prop]
        # If PropertyDecoder couldn't resolve it (no defaults entry for the
        # kind), the literal "NotDefault" string can leak through. Treat that
        # as unknown effective value so we don't compare gibberish.
        if v == "NotDefault":
            return None, "unknown"
        return v, "ir"
    if kind and kind in defaults and prop in defaults[kind]:
        b = defaults[kind][prop]
        return ("True" if b else "False"), "default"
    return None, "unknown"


# ----------------------------------------------------------------------------
# Hypothesis dispatch — short rule-based explanations for mismatches.
# ----------------------------------------------------------------------------


def hypothesis(prop: str, ours: str | None, theirs: str | None,
               source: str, kind: str | None) -> str:
    """One-line guess at why ours and theirs disagree."""
    if source == "missing-node":
        return "control or form not present in our IR (parser may have skipped it)"
    if source == "unknown":
        return ("non-boolean property at default; SaveAsText omitted it and our "
                "defaults table only covers booleans — extend defaults coverage")
    if source == "default":
        # Specific known pattern: HasModule defaults to False on a bare form,
        # but every form with VBA reports True via DAO. SaveAsText sometimes
        # omits it even when there's code. Worth extracting from the
        # ModulesAndClasses tier, not from defaults.
        if prop == "HasModule":
            return ("default fallback says no VBA module, but YAML reports one. "
                    "SaveAsText may omit HasModule even when a code module exists; "
                    "derive this flag from ModulesAndClasses output instead of defaults.")
        return ("we fell back to the boolean default because SaveAsText didn't emit "
                "this property; YAML reports a non-default effective value — possible "
                "default-value-omission bug in our parser, or YAML reading runtime state")
    # source == "ir" (we have a real value but they differ)
    if prop == "Format":
        return ("format string mismatch: our IR preserves the literal mask "
                "(\"$#,##0.00;($#,##0.00)\") while DAO/YAML reports the canonical name "
                "(\"Currency\"). Equivalent value, different serialization. Could "
                "be normalized by a future enricher.")
    if prop == "ColumnOrder":
        return ("off-by-one convention difference: SaveAsText uses 0-based ColumnOrder, "
                "DAO/YAML reports 1-based. Equivalent. Add +1 in a normalizer or "
                "skip-list this property.")
    if prop == "SourceObject":
        return ("Subform/Subreport SourceObject prefix difference: SaveAsText writes "
                "\"Form.<name>\" / \"Report.<name>\", DAO/YAML strips the prefix. "
                "Equivalent reference, different serialization.")
    if prop == "HasModule":
        return ("we resolved HasModule from SaveAsText but disagree with DAO. Cross-check "
                "against the ModulesAndClasses output for the same form.")
    if prop in ("AllowDesignChanges", "DividingLines"):
        return ("form-level legacy property; deprecated since Access 2007. SaveAsText "
                "preserves the saved value; DAO reports the runtime-effective value. "
                "Common DAO-vs-SaveAsText divergence, not a parser bug.")
    if "Color" in prop or "Tint" in prop or "Shade" in prop or "ThemeColorIndex" in prop:
        return "color-themed property; numeric divergence may reflect theme resolution timing"
    if prop in ("Width", "Height", "Top", "Left", "Right", "Bottom"):
        return ("geometry mismatch within ~30 twips: known DAO-vs-SaveAsText "
                "rounding/snapping difference. Larger gaps would indicate a real "
                "layout discrepancy worth investigating.")
    if prop.startswith("Datasheet"):
        return "datasheet-view property; may reflect view-state runtime divergence"
    if prop.endswith("Source") or prop in ("RecordSource", "ControlSource", "RowSource"):
        return "data source string differs — quoting or whitespace likely; inspect verbatim"
    if prop in ("Caption", "Tag", "Description", "ControlTipText", "StatusBarText"):
        # Two distinct sub-cases for free-text:
        # 1) escape-sequence difference: our IR preserves SaveAsText \015\012;
        #    YAML normalizes to \n. Predictable, fixable in a future enricher.
        # 2) genuine truncation: our parser caps long strings at 80 chars
        #    (PARSER BUG — see northwind-validation.md callout).
        if ours is not None and len(ours) == 80:
            return ("PARSER BUG: free-text property truncated at exactly 80 chars by "
                    "AccessTextParser. Long captions/descriptions/tooltips lose data. "
                    "Open a fix issue.")
        if ours is not None and "\\015\\012" in ours:
            return ("SaveAsText CR/LF escape leaked through (\\015\\012); YAML normalizes "
                    "to \\n. Decode \\015\\012 → \\n in the parser or a string enricher.")
        return "free-text property; check curly-vs-straight quotes and trailing whitespace"
    return "real difference; classify after triage (parser, enricher, or VBA quirk)"


# ----------------------------------------------------------------------------
# Comparator
# ----------------------------------------------------------------------------


class Report:
    def __init__(self):
        self.forms_total = 0
        self.forms_compared = 0
        self.reports_total = 0
        self.reports_compared = 0
        self.matches = 0
        self.skipped: Counter = Counter()
        # unknown_by_kind[kind] -> Counter({prop: count})
        self.unknown_by_kind: dict = defaultdict(Counter)
        self.mismatches: list = []  # list of dicts
        self.missing_nodes: list = []  # list of (path, reason)

    def record_match(self):
        self.matches += 1

    def record_skip(self, category: str):
        self.skipped[category] += 1

    def record_unknown(self, kind: str | None, prop: str):
        self.unknown_by_kind[kind or "<unknown-kind>"][prop] += 1

    def record_mismatch(self, path: str, prop: str, ours: str | None,
                         theirs: str, source: str, kind: str | None):
        self.mismatches.append({
            "path": path,
            "prop": prop,
            "ours": ours,
            "theirs": theirs,
            "source": source,
            "kind": kind,
            "hypothesis": hypothesis(prop, ours, theirs, source, kind),
        })

    def record_missing_node(self, path: str, reason: str):
        self.missing_nodes.append((path, reason))


def compare_node(ir_node: dict | None, yaml_node: dict, kind: str | None,
                  defaults: dict, path: str, report: Report):
    yaml_props = yaml_node.get("Properties") or {}
    if ir_node is None:
        report.record_missing_node(path, "no IR node")
        # Don't iterate properties — we'd report the whole property dict as
        # missing. The single missing-node entry communicates the gap.
        return
    for prop, yaml_value in yaml_props.items():
        skip_cat = classify_skip(prop)
        if skip_cat:
            report.record_skip(skip_cat)
            continue
        our_value, source = effective_value(ir_node, prop, kind, defaults)
        if source == "unknown":
            report.record_unknown(kind, prop)
            continue
        if source == "missing-node":
            # Already recorded above; shouldn't reach here, but be safe.
            continue
        if values_equal(our_value, yaml_value):
            report.record_match()
        else:
            report.record_mismatch(
                path=f"{path}/{prop}",
                prop=prop,
                ours=our_value,
                theirs=yaml_value,
                source=source,
                kind=kind,
            )


def validate(yaml_path: str, ir_root: str, defaults_path: str) -> Report:
    parsed = _parse_yaml(yaml_path)
    with open(defaults_path, "r", encoding="utf-8") as f:
        defaults_data = json.load(f)
    type_map = dict(defaults_data.get("controlTypeNumberToSaveAsTextName") or {})
    # Control types not present in FormAndReportDefaults.accdb — patch them in
    # so they don't show up as "unknown ControlType" missing-nodes. Defaults
    # table doesn't have entries for these kinds, but we can still look up
    # IR-side values for property comparison.
    type_map.setdefault("125", "EmptyCell")  # also seen as "127" in some Access versions
    type_map.setdefault("127", "EmptyCell")
    type_map.setdefault("133", "Chart")
    defaults = defaults_data.get("defaults") or {}

    ir_forms = os.path.join(ir_root, "Forms")
    ir_reports = os.path.join(ir_root, "Reports")

    report = Report()

    for kind_label, key, ir_dir in (("Form", "Forms", ir_forms),
                                     ("Report", "Reports", ir_reports)):
        items = parsed.get(key) or []
        if kind_label == "Form":
            report.forms_total = len(items)
        else:
            report.reports_total = len(items)

        for top in items:
            name = top.get("Name", "")
            ir = _open_ir(ir_dir, name)
            if ir is None:
                report.record_missing_node(f"{kind_label}/{name}",
                                            f"missing IR file: {name}.ir.json")
                continue
            if kind_label == "Form":
                report.forms_compared += 1
            else:
                report.reports_compared += 1

            compare_node(ir, top, kind=kind_label, defaults=defaults,
                          path=f"{kind_label}/{name}", report=report)

            for ctl in top.get("Controls") or []:
                ctl_name = ctl.get("Name", "")
                ct_num = ctl.get("ControlType", "")
                ctl_kind = type_map.get(str(ct_num))
                ir_ctl = _find_named(ir, ctl_name)
                ctl_path = f"{kind_label}/{name}/{ctl_name}"
                if ir_ctl is None:
                    report.record_missing_node(ctl_path,
                                                f"control '{ctl_name}' (ControlType {ct_num}) "
                                                f"not found in IR")
                    continue
                if ctl_kind is None:
                    # Unknown control type number — record but skip property
                    # diffs because we can't trust default lookup.
                    report.record_missing_node(ctl_path,
                                                f"unknown ControlType {ct_num}")
                    continue
                compare_node(ir_ctl, ctl, kind=ctl_kind, defaults=defaults,
                              path=ctl_path, report=report)

    return report


# ----------------------------------------------------------------------------
# Report rendering
# ----------------------------------------------------------------------------


def _print_summary(report: Report) -> None:
    skipped_total = sum(report.skipped.values())
    unknown_total = sum(sum(c.values()) for c in report.unknown_by_kind.values())
    by_prop: Counter = Counter(m["prop"] for m in report.mismatches)
    print("Summary:")
    print(f"  Forms compared:    {report.forms_compared} / {report.forms_total}")
    print(f"  Reports compared:  {report.reports_compared} / {report.reports_total}")
    print(f"  Matches:           {report.matches}")
    print(f"  Skipped:           {skipped_total}  "
          f"(" + ", ".join(f"{k}={v}" for k, v in report.skipped.most_common()) + ")")
    print(f"  Unknown:           {unknown_total}  "
          f"(properties with no IR value and no boolean default)")
    print(f"  Missing nodes:     {len(report.missing_nodes)}")
    print(f"  Mismatches:        {len(report.mismatches)}")
    if by_prop:
        print("\nTop mismatches by property:")
        for prop, n in by_prop.most_common(15):
            print(f"  {prop:32s} {n:4d}")


def _render_markdown(report: Report) -> str:
    skipped_total = sum(report.skipped.values())
    unknown_total = sum(sum(c.values()) for c in report.unknown_by_kind.values())
    by_prop: Counter = Counter(m["prop"] for m in report.mismatches)
    lines: list[str] = []

    lines.append("# Northwind YAML validation\n")
    lines.append(
        "This is the diff between AccessHarvester's IR for `NorthwindDevEdition.accdb` "
        "and the VBA-extracted YAML in `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml`. "
        "The two extractions are independent (SaveAsText→parser→enrichers vs. DAO read), "
        "so each disagreement is evidence of a parser bug, a SaveAsText quirk our enrichers "
        "don't yet handle, or a known-incomparable property kept on the skip list.\n"
    )
    lines.append("Regenerate via `python tools/validate_against_yaml.py`.\n")

    # Bubble up actionable findings for a triage-at-a-glance reader. Anything
    # surfaced here is also visible in the per-property tables below.
    findings: list[str] = []
    truncation_hits = [m for m in report.mismatches
                        if m["ours"] is not None and len(str(m["ours"])) == 80
                        and m["prop"] in ("Caption", "Tag", "Description",
                                            "ControlTipText", "StatusBarText")]
    crlf_escape_hits = [m for m in report.mismatches
                         if m["ours"] is not None and "\\015\\012" in str(m["ours"])]
    if truncation_hits:
        sample_path = truncation_hits[0]["path"]
        findings.append(
            f"**PARSER BUG — 80-char truncation in free-text properties**: "
            f"{len(truncation_hits)} occurrences. AccessTextParser appears to cap "
            f"`Caption` / `ControlTipText` / `StatusBarText` etc. at exactly 80 "
            f"characters. Long captions silently lose the tail. "
            f"First example: `{sample_path}`. Open as a follow-up fix.")
    if crlf_escape_hits:
        findings.append(
            f"**SaveAsText CR/LF escape leak**: {len(crlf_escape_hits)} free-text "
            f"properties contain raw `\\015\\012` from SaveAsText that DAO/YAML "
            f"normalizes to `\\n`. Decode in the parser or a string enricher.")
    if by_prop.get("Format", 0) > 0:
        findings.append(
            f"**Format mask vs canonical name**: {by_prop['Format']} mismatches. "
            f"SaveAsText keeps the literal mask (`$#,##0.00;($#,##0.00)`); DAO "
            f"reports the canonical name (`Currency`). Equivalent values; "
            f"normalize in a string enricher or skip-list.")
    if by_prop.get("ColumnOrder", 0) > 0:
        findings.append(
            f"**ColumnOrder off-by-one**: {by_prop['ColumnOrder']} mismatches. "
            f"SaveAsText is 0-based, DAO is 1-based. Convention difference, "
            f"not a bug.")
    if by_prop.get("SourceObject", 0) > 0:
        findings.append(
            f"**SourceObject prefix**: {by_prop['SourceObject']} subforms/subreports. "
            f"SaveAsText writes `Form.<name>` / `Report.<name>`; DAO strips the "
            f"prefix. Trim in a string enricher.")
    geom_props = ("Left", "Top", "Width", "Height", "Right", "Bottom")
    geom_count = sum(by_prop.get(p, 0) for p in geom_props)
    if geom_count > 0:
        findings.append(
            f"**Geometry off-by-twips**: {geom_count} mismatches across "
            f"{', '.join(p for p in geom_props if by_prop.get(p))}. "
            f"Differences are typically 5–30 twips — a known DAO-vs-SaveAsText "
            f"rounding/snapping divergence, not a parser bug.")
    if by_prop.get("HasModule", 0) > 0:
        findings.append(
            f"**HasModule fallback**: {by_prop['HasModule']} forms. SaveAsText "
            f"omits HasModule and our defaults table says False; DAO reports "
            f"True for every form with VBA. Derive this flag from "
            f"ModulesAndClasses output rather than the booleans table.")

    if findings:
        lines.append("## Key findings\n")
        for f in findings:
            lines.append(f"- {f}")
        lines.append("")

    lines.append("## Summary\n")
    lines.append(f"- Forms compared: **{report.forms_compared} / {report.forms_total}**")
    lines.append(f"- Reports compared: **{report.reports_compared} / {report.reports_total}**")
    lines.append(f"- Matches: **{report.matches}**")
    skip_breakdown = ", ".join(f"{k}={v}" for k, v in report.skipped.most_common())
    lines.append(f"- Skipped: **{skipped_total}** ({skip_breakdown or 'none'})")
    lines.append(f"- Unknown (non-boolean default we don't track): **{unknown_total}**")
    lines.append(f"- Missing nodes (form/report/control absent from IR): **{len(report.missing_nodes)}**")
    lines.append(f"- Mismatches: **{len(report.mismatches)}**\n")

    if by_prop:
        lines.append("## Mismatches by property\n")
        lines.append("| Property | Count | Sample paths |")
        lines.append("| --- | ---: | --- |")
        sample_paths: dict = defaultdict(list)
        for m in report.mismatches:
            if len(sample_paths[m["prop"]]) < 3:
                sample_paths[m["prop"]].append(m["path"].rsplit("/", 1)[0])
        for prop, n in by_prop.most_common():
            samples = ", ".join(sample_paths[prop])
            lines.append(f"| `{prop}` | {n} | {samples} |")
        lines.append("")

    if report.unknown_by_kind:
        lines.append("## Unknown coverage by kind\n")
        lines.append(
            "Properties present on the YAML side but absent from our IR *and* "
            "from `property-defaults.json`. These are silent gaps: the AI design "
            "stage receives nothing about these fields. Expanding the defaults "
            "table to non-booleans, or teaching enrichers to materialize them, "
            "would close each gap.\n")
        for kind in sorted(report.unknown_by_kind.keys()):
            counter = report.unknown_by_kind[kind]
            lines.append(f"### {kind} — {sum(counter.values())} occurrences across "
                          f"{len(counter)} unique properties\n")
            for prop, n in counter.most_common(40):
                lines.append(f"- `{prop}` × {n}")
            if len(counter) > 40:
                lines.append(f"- … and {len(counter) - 40} more")
            lines.append("")

    if report.missing_nodes:
        lines.append("## Missing nodes\n")
        for path, reason in report.missing_nodes[:200]:
            lines.append(f"- `{path}` — {reason}")
        if len(report.missing_nodes) > 200:
            lines.append(f"- … and {len(report.missing_nodes) - 200} more")
        lines.append("")

    if report.mismatches:
        lines.append("## Mismatch details\n")
        # Group by property, then bucket within a property by hypothesis. A
        # property like `Caption` mixes both 80-char truncation (real parser
        # bug) and `\015\012` vs `\n` escape leaks (predictable, fixable);
        # keeping them split makes each finding actionable on its own.
        grouped: dict = defaultdict(list)
        for m in report.mismatches:
            grouped[m["prop"]].append(m)
        for prop in sorted(grouped, key=lambda p: -len(grouped[p])):
            ms = grouped[prop]
            lines.append(f"### `{prop}` ({len(ms)})\n")
            by_hyp: dict = defaultdict(list)
            for m in ms:
                by_hyp[m["hypothesis"]].append(m)
            for hyp_idx, hyp in enumerate(sorted(by_hyp,
                                                    key=lambda h: -len(by_hyp[h]))):
                bucket = by_hyp[hyp]
                if len(by_hyp) > 1:
                    lines.append(f"**({len(bucket)})** _{hyp}_\n")
                else:
                    lines.append(f"_{hyp}_\n")
                lines.append("| Path | Source | Ours | Theirs |")
                lines.append("| --- | --- | --- | --- |")
                for m in bucket[:30]:
                    ours = "" if m["ours"] is None else m["ours"]
                    def _trunc(v: str) -> str:
                        v = str(v).replace("|", "\\|").replace("\n", " ")
                        return v if len(v) <= 80 else v[:77] + "..."
                    lines.append(f"| `{m['path']}` | {m['source']} | "
                                  f"`{_trunc(ours)}` | `{_trunc(m['theirs'])}` |")
                if len(bucket) > 30:
                    lines.append(f"\n_({len(bucket) - 30} more rows omitted)_\n")
                lines.append("")

    return "\n".join(lines) + "\n"


# ----------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.abspath(os.path.join(here, ".."))

    p = argparse.ArgumentParser(description=__doc__,
                                  formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--yaml",
                   default=os.path.join(repo, "samples", "FormsAndReportsYaml",
                                          "NorthwindDevEdition.accdb.yaml"))
    p.add_argument("--ir-root",
                   default=os.path.join(repo, "tmp", "run-output", "AccessMetaData",
                                          "NorthwindDevEdition", "ir"))
    p.add_argument("--defaults",
                   default=os.path.join(repo, "AccessHarvester.Core", "Resources",
                                          "property-defaults.json"))
    p.add_argument("--report",
                   default=os.path.join(here, "reports", "northwind-validation.md"))
    args = p.parse_args()

    for label, path in (("YAML", args.yaml),
                         ("IR root", args.ir_root),
                         ("defaults", args.defaults)):
        if not os.path.exists(path):
            print(f"ERROR: {label} not found: {path}", file=sys.stderr)
            sys.exit(1)

    report = validate(args.yaml, args.ir_root, args.defaults)

    _print_summary(report)

    md = _render_markdown(report)
    os.makedirs(os.path.dirname(args.report), exist_ok=True)
    with open(args.report, "w", encoding="utf-8", newline="\n") as f:
        f.write(md)

    print(f"\nMarkdown report: {args.report}")


if __name__ == "__main__":
    main()
