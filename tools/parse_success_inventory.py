"""Property-sheet expression inventory pass for Phase 4 GAP #1.

Reads the committed YAML mirrors of the two canonical fixtures
(`samples/DatabaseYaml/{HomeHealthCare,NorthwindDevEdition}.accdb/`)
and enumerates non-empty expression-bearing property-sheet fields:

  - `ValidationRule` (table/column scope)
  - `DefaultValue`   (table/column scope)
  - `Format`         (table/column + form control scope)
  - `InputMask`      (table/column + form control scope)
  - `Filter`         (form/report scope)
  - `OrderBy`        (form/report scope)
  - `RecordSource`   (form scope; SQL or table name)
  - `RowSource`      (combo/listbox control scope; SQL or value-list)

For each field, counts non-empty occurrences and unique values, and
prints up to N samples so the human can eyeball the dialect mix.

The verdict (whether to fold property-sheet expressions into the
parse-success report or exclude from gating) is a separate writeup
under `docs/session-sets/harvester-finish-canonical-parse-success-report/property-sheet-inventory.md`.

Hermetic and read-only: never writes outside `tools/_inventory_out/`.
"""
from __future__ import annotations

import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Iterable

REPO_ROOT = Path(__file__).resolve().parent.parent
YAML_ROOT = REPO_ROOT / "samples" / "DatabaseYaml"
FIXTURES = ("HomeHealthCare.accdb", "NorthwindDevEdition.accdb")

# Property fields and the canonical scope label used in the inventory.
SCOPED_FIELDS: tuple[tuple[str, str], ...] = (
    ("ValidationRule", "any"),
    ("DefaultValue", "any"),
    ("Format", "any"),
    ("InputMask", "any"),
    ("Filter", "any"),
    ("OrderBy", "any"),
    ("RecordSource", "any"),
    ("RowSource", "any"),
    ("ControlSource", "any"),
)

# `Key: "value"` where value is not empty and not just `null` / `True` / `False`.
# Captures the inner value text without the surrounding quotes.
# Match `Key: "value"` where value is a YAML-style double-quoted scalar
# whose body may contain backslash-escaped inner quotes (e.g.,
# `In (\"M\",\"F\",\"X\")` for `In ("M","F","X")`). A naive `[^"]*` group
# stops at the first inner quote and skips these lines entirely.
LINE_PATTERN = re.compile(
    r'^(?P<indent>\s*)(?P<key>[A-Za-z]+):\s*"(?P<value>(?:[^"\\]|\\.)*)"\s*$',
    re.MULTILINE,
)


def iter_yaml_files(fixture_root: Path) -> Iterable[Path]:
    """All `*.yaml` files under the fixture mirror, source-sorted."""
    return sorted(p for p in fixture_root.rglob("*.yaml") if p.is_file())


def collect_fixture(fixture_root: Path) -> dict:
    """Walk a fixture YAML mirror and tally property-sheet fields."""
    by_field: dict[str, Counter] = {key: Counter() for key, _ in SCOPED_FIELDS}
    samples: dict[str, list[tuple[str, str]]] = {key: [] for key, _ in SCOPED_FIELDS}
    files_seen = 0
    for yaml_path in iter_yaml_files(fixture_root):
        files_seen += 1
        # YAML mirrors mix encodings (some Caption values carry windows-
        # specific glyphs the YAML was not normalised against). Read as
        # bytes and decode with replacement — the keys we match are all
        # ASCII so undecodable bytes never appear inside a captured group.
        raw = yaml_path.read_bytes()
        text = raw.decode("utf-8", errors="replace")
        rel = str(yaml_path.relative_to(yaml_path.parents[2]))
        for m in LINE_PATTERN.finditer(text):
            key = m.group("key")
            value = m.group("value")
            if key not in by_field:
                continue
            # Exclude empty and trivially-not-an-expression placeholders.
            if value == "":
                continue
            by_field[key][value] += 1
            if len(samples[key]) < 10:
                samples[key].append((rel, value))
    out: dict = {
        "fixture": fixture_root.name,
        "files_scanned": files_seen,
        "by_field": {},
    }
    for key, counter in by_field.items():
        total_occurrences = sum(counter.values())
        unique_values = len(counter)
        out["by_field"][key] = {
            "total_occurrences": total_occurrences,
            "unique_values": unique_values,
            "samples": samples[key],
            "top_5": counter.most_common(5),
        }
    return out


def classify_value(value: str) -> str:
    """Coarse dialect classification of a raw property value.

    Lets us tell at a glance whether a fixture's values are mostly
    literals (numeric, boolean, "True"/"False"), simple constants, or
    full Access expressions.
    """
    v = value.strip()
    if v.lower() in ("true", "false", "yes", "no"):
        return "boolean-literal"
    if v.lstrip("-").isdigit():
        return "integer-literal"
    if re.fullmatch(r"-?\d+\.\d+", v.lstrip("-")):
        return "decimal-literal"
    # Quoted string of the form "X", "Pending", etc. (After YAML quote
    # removal that means \"X\"-style escaped quotes inside the raw text.)
    if re.fullmatch(r'\\".+\\"', v):
        return "string-literal"
    # Format-string conventions like "Short Date", "Yes/No", "@@@-@@@@".
    if re.fullmatch(r"[A-Za-z][A-Za-z0-9 /]+", v):
        return "format-keyword"
    # Mask-template chars.
    if re.fullmatch(r"[0-9A-Za-z\\\-\(\) ;_!@#&]+", v) and any(c in v for c in "@9#&"):
        return "mask-template"
    # Anything containing an operator or function call is treated as an
    # Access expression — needs grammar coverage.
    if any(ch in v for ch in "=<>") or re.search(r"\b[A-Za-z_]\w*\s*\(", v):
        return "access-expression"
    # `In ("A","B")` and `Is Null` etc.
    if re.match(r"^(In|Is|Like|Between|Not)\b", v, re.IGNORECASE):
        return "access-expression"
    # Quoted strings inside ValidationRule like `"M"`, `"X"` after YAML
    # escaping (`\"X\"`).
    if v.startswith('\\"') and v.endswith('\\"'):
        return "string-literal"
    return "other"


def main() -> int:
    if not YAML_ROOT.exists():
        print(f"YAML root not found: {YAML_ROOT}", file=sys.stderr)
        return 2

    out_dir = REPO_ROOT / "tools" / "_inventory_out"
    out_dir.mkdir(exist_ok=True)

    full: dict = {"fixtures": []}
    summary_lines: list[str] = []

    for fixture in FIXTURES:
        root = YAML_ROOT / fixture
        if not root.exists():
            print(f"WARN: {root} missing", file=sys.stderr)
            continue
        data = collect_fixture(root)
        # Layer dialect classification on top.
        for key, info in data["by_field"].items():
            dialect_counter: Counter = Counter()
            for value, count in info["top_5"]:
                dialect_counter[classify_value(value)] += count
            info["dialect_top5"] = dialect_counter.most_common()
        full["fixtures"].append(data)

    out_path = out_dir / "property-sheet-inventory.json"
    out_path.write_text(
        json.dumps(full, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    # Pretty-print summary for the human and the verdict author.
    for fixture in full["fixtures"]:
        summary_lines.append(f"\n=== {fixture['fixture']} ({fixture['files_scanned']} YAML files) ===")
        for key, info in fixture["by_field"].items():
            occ = info["total_occurrences"]
            uniq = info["unique_values"]
            if occ == 0:
                continue
            summary_lines.append(
                f"  {key:<14}  occurrences={occ:<5}  unique={uniq:<4}"
                f"  dialect_top5={info['dialect_top5']}"
            )
            for value, count in info["top_5"]:
                dialect = classify_value(value)
                summary_lines.append(f"      [{dialect}] x{count}  {value!r}")
    print("\n".join(summary_lines))
    print(f"\n[wrote {out_path.relative_to(REPO_ROOT)}]")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
