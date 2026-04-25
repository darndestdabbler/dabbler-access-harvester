"""
Regenerate AccessExporter.Core/Resources/property-defaults.json from a
FormAndReportDefaults.accdb.yaml (produced by the VBA extractor in
samples/MasterDbPropertyExtractor.accdb) plus the kb/ output of running
AccessExporter's extract+translate pipeline on FormAndReportDefaults.accdb.

The defaults YAML tells us what the default VALUE of each property is (as a
string like "True", "False", "0", "Aptos"). The kb JSON tells us what STRING
the Access SaveAsText format uses for each control type (e.g., ControlType 109
→ "TextBox", 107 → "OptionGroup"). We cross-reference the two by control Name
— the defaults form contains exactly one of each control type, named
auto-sequentially by Access (Text0, Label1, Command3, ...), and the kb output
preserves those names.

The script extracts Boolean defaults only (properties whose default is "True"
or "False"), indexed by SaveAsText Type string. That's the data PropertyDecoder
needs to translate SaveAsText's "NotDefault" markers into real booleans.

Access version compatibility: defaults captured here are specific to the
Access version that built the source .accdb. Older Access versions may have
different defaults for some properties, and some properties may not exist at
all. To support older DBs, build a defaults database in that version, re-run
the VBA extractor to produce a comparable YAML, re-run the AccessExporter
pipeline to produce comparable kb JSON, and then invoke this script with the
`--output` flag pointing at a separate file name (e.g.,
property-defaults-access2010.json). PropertyDecoder can be taught to pick the
right file based on a version hint discovered from the source DB.

USAGE
-----
    cd tools
    python generate_property_defaults.py \
        --yaml ../samples/FormsAndReportsYaml/FormAndReportDefaults.accdb.yaml \
        --kb-forms ../sample-output/AccessMetaData/FormAndReportDefaults/kb/Forms \
        --kb-reports ../sample-output/AccessMetaData/FormAndReportDefaults/kb/Reports \
        --output ../AccessExporter.Core/Resources/property-defaults.json

All paths are optional — defaults assume the repository layout above.

The kb/ paths assume you've already run:
    dotnet run --project ../AccessExporter.Cli/AccessExporter.Cli.csproj -- run \
        --db ../samples/FormAndReportDefaults.accdb \
        --out ../sample-output --types Forms,Reports
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import re
import sys

# ----------------------------------------------------------------------------
# Tiny YAML reader — the extractor's YAML is a flat dict-of-dicts structure
# and we don't want a PyYAML dependency for one file. Strings are all
# double-quoted in the source, which keeps parsing trivial.
# ----------------------------------------------------------------------------

_QUOTED = re.compile(r'^"(.*)"$')


def _strip_quotes(s: str) -> str:
    m = _QUOTED.match(s)
    return m.group(1) if m else s


def _parse_yaml(path: str) -> dict:
    """Parse the known shape of FormAndReportDefaults.accdb.yaml. Returns
    {'Forms': [{'Name': str, 'Properties': dict, 'Controls': [{'Name': str,
    'ControlType': str, 'Properties': dict}]}], 'Reports': [...]}.

    Scoped: indentation-driven like real YAML, but only understands the
    constructs this specific extractor emits (lists with `- Name:`, nested
    dicts, quoted-string scalars). Binary goo inside PrtMip/PrtDevMode gets
    preserved verbatim; we never read those fields so it doesn't matter.
    """
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()

    root: dict = {}
    # A stack of (indent, container). Container is either a dict (we're
    # filling keys) or a list (we're appending dicts).
    stack: list = [(-1, root)]

    def _container_at(indent: int):
        while stack and stack[-1][0] >= indent:
            stack.pop()
        return stack[-1][1]

    for raw in lines:
        line = raw.rstrip("\n")
        if not line.strip():
            continue
        if line.lstrip().startswith("#"):
            continue
        indent = len(line) - len(line.lstrip(" "))
        content = line.strip()

        if content.startswith("- "):
            container = _container_at(indent)
            new = {}
            if isinstance(container, list):
                container.append(new)
            else:
                # The parent is a dict but we're opening a list; that
                # shouldn't happen in this extractor's output.
                raise RuntimeError(f"Unexpected list dash at line: {raw!r}")
            stack.append((indent, new))
            rest = content[2:].strip()
            if ":" in rest:
                key, _, value = rest.partition(":")
                value = value.strip()
                if value == "" or value.endswith(":"):
                    nested = {} if not value.endswith(":") else []
                    new[key.strip()] = nested
                    stack.append((indent + 2, nested))
                else:
                    new[key.strip()] = _strip_quotes(value)
            continue

        if ":" in content:
            key, _, value = content.partition(":")
            key = key.strip()
            value = value.strip()
            container = _container_at(indent)
            if not isinstance(container, dict):
                raise RuntimeError(f"Expected dict container at indent {indent} for line {raw!r}")
            if value == "":
                # Nested container opens next; peek a later non-blank line to
                # decide whether it's a list or a dict.
                nested: object = {}
                # Scan ahead for the first non-blank descendant.
                i = lines.index(raw) + 1
                while i < len(lines):
                    l2 = lines[i].rstrip("\n")
                    if l2.strip() and not l2.lstrip().startswith("#"):
                        child_indent = len(l2) - len(l2.lstrip(" "))
                        if child_indent > indent:
                            if l2.lstrip().startswith("- "):
                                nested = []
                            break
                        else:
                            break
                    i += 1
                container[key] = nested
                stack.append((indent, nested))
            else:
                container[key] = _strip_quotes(value)

    return root


# ----------------------------------------------------------------------------
# Extract boolean defaults from the parsed YAML
# ----------------------------------------------------------------------------


def _boolean_defaults(props: dict) -> dict:
    """Return the subset of properties whose value is the string 'True' or
    'False', as real booleans."""
    out = {}
    for k, v in props.items():
        if v == "True":
            out[k] = True
        elif v == "False":
            out[k] = False
    return out


# ----------------------------------------------------------------------------
# Build ControlType int → SaveAsText Type string mapping from kb JSON
# ----------------------------------------------------------------------------


def _collect_controls(kb_node: dict, out: dict) -> None:
    """Walk a kb form/report JSON and record {controlName: saveAsTextType}."""
    if not isinstance(kb_node, dict):
        return
    t = kb_node.get("Type", "")
    props = kb_node.get("Properties") or {}
    name = props.get("Name") if isinstance(props, dict) else None
    if name and t and t not in ("Form", "Report", "Section", "Begin", "DocumentRoot"):
        out[name] = t
    for child in kb_node.get("Children") or []:
        _collect_controls(child, out)


def _read_kb_type_map(kb_dir: str) -> dict:
    """Read every *.json in kb_dir and return {controlName: Type-string}."""
    out = {}
    for fn in os.listdir(kb_dir):
        if fn.endswith(".json"):
            with open(os.path.join(kb_dir, fn), "r", encoding="utf-8") as f:
                _collect_controls(json.load(f), out)
    return out


# ----------------------------------------------------------------------------
# Main generation
# ----------------------------------------------------------------------------


def _file_sha(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(65536), b""):
            h.update(block)
    return h.hexdigest()


def generate(yaml_path: str, kb_forms_dir: str, kb_reports_dir: str) -> dict:
    parsed = _parse_yaml(yaml_path)
    name_to_type = {}
    name_to_type.update(_read_kb_type_map(kb_forms_dir))
    name_to_type.update(_read_kb_type_map(kb_reports_dir))

    forms = parsed.get("Forms") or []
    reports = parsed.get("Reports") or []

    defaults_by_kind: dict = {}
    controltype_number_to_name: dict = {}

    if forms:
        form = forms[0]
        defaults_by_kind["Form"] = _boolean_defaults(form.get("Properties") or {})
        for ctl in form.get("Controls") or []:
            ct_num = ctl.get("ControlType", "")
            type_string = name_to_type.get(ctl.get("Name", ""))
            if type_string is None:
                continue
            controltype_number_to_name.setdefault(ct_num, type_string)
            # If we've seen this Type string already, merge — prefer any existing
            # entries (first-one-wins would drop properties from later duplicates,
            # but the defaults form has one of each type so it shouldn't happen).
            defaults_by_kind.setdefault(type_string, {})
            defaults_by_kind[type_string].update(
                _boolean_defaults(ctl.get("Properties") or {})
            )

    if reports:
        report = reports[0]
        defaults_by_kind["Report"] = _boolean_defaults(report.get("Properties") or {})
        for ctl in report.get("Controls") or []:
            ct_num = ctl.get("ControlType", "")
            type_string = name_to_type.get(ctl.get("Name", ""))
            if type_string is None:
                continue
            controltype_number_to_name.setdefault(ct_num, type_string)
            defaults_by_kind.setdefault(type_string, {})
            for k, v in _boolean_defaults(ctl.get("Properties") or {}).items():
                # Don't overwrite a Form-side default with a Report-side one —
                # most controls behave identically on both, but if they diverge
                # we prefer the Form value (arbitrary but consistent).
                defaults_by_kind[type_string].setdefault(k, v)

    # Sort for reproducible diffs.
    defaults_by_kind = {k: dict(sorted(v.items())) for k, v in sorted(defaults_by_kind.items())}
    controltype_number_to_name = dict(sorted(controltype_number_to_name.items(), key=lambda kv: int(kv[0])))

    return {
        "_comment": (
            "Boolean property defaults for Access Forms, Reports, and each control type. "
            "When SaveAsText emits a property with the literal value 'NotDefault', the "
            "effective value is the inverse of the default listed here. Regenerate via "
            "tools/generate_property_defaults.py."
        ),
        "_generatedBy": "tools/generate_property_defaults.py",
        "_generatedAtUtc": datetime.datetime.utcnow().replace(microsecond=0).isoformat() + "Z",
        "_sourceYaml": os.path.relpath(yaml_path).replace("\\", "/"),
        "_sourceYamlSha256": _file_sha(yaml_path),
        "_accessVersionNote": (
            "Captured from the Access version that built FormAndReportDefaults.accdb. "
            "Older Access versions may have different defaults for some properties. "
            "To support an older version, rebuild the defaults DB in that version, "
            "re-extract its YAML + kb JSON, and regenerate this file with a "
            "version-specific --output name (e.g., property-defaults-access2010.json)."
        ),
        "controlTypeNumberToSaveAsTextName": controltype_number_to_name,
        "defaults": defaults_by_kind,
    }


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.abspath(os.path.join(here, ".."))

    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--yaml",
                   default=os.path.join(repo, "samples", "FormsAndReportsYaml", "FormAndReportDefaults.accdb.yaml"))
    p.add_argument("--kb-forms",
                   default=os.path.join(repo, "sample-output", "AccessMetaData", "FormAndReportDefaults", "kb", "Forms"))
    p.add_argument("--kb-reports",
                   default=os.path.join(repo, "sample-output", "AccessMetaData", "FormAndReportDefaults", "kb", "Reports"))
    p.add_argument("--output",
                   default=os.path.join(repo, "AccessExporter.Core", "Resources", "property-defaults.json"))
    args = p.parse_args()

    for label, path in (("YAML", args.yaml), ("kb/Forms", args.kb_forms), ("kb/Reports", args.kb_reports)):
        if not os.path.exists(path):
            print(f"ERROR: {label} not found: {path}", file=sys.stderr)
            if "kb/" in label:
                print("  Run the AccessExporter pipeline on samples/FormAndReportDefaults.accdb first:",
                      file=sys.stderr)
                print("    dotnet run --project ../AccessExporter.Cli/AccessExporter.Cli.csproj -- run \\",
                      file=sys.stderr)
                print("        --db ../samples/FormAndReportDefaults.accdb \\", file=sys.stderr)
                print("        --out ../sample-output --types Forms,Reports", file=sys.stderr)
            sys.exit(1)

    result = generate(args.yaml, args.kb_forms, args.kb_reports)

    os.makedirs(os.path.dirname(args.output), exist_ok=True)
    with open(args.output, "w", encoding="utf-8", newline="\n") as f:
        json.dump(result, f, indent=2)
        f.write("\n")

    print(f"Wrote {args.output}")
    print(f"  Kinds: {len(result['defaults'])}")
    print(f"  Form booleans: {len(result['defaults'].get('Form', {}))}")
    print(f"  Report booleans: {len(result['defaults'].get('Report', {}))}")


if __name__ == "__main__":
    main()
