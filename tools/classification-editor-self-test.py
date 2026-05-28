"""Headless self-test for tools/classification-editor.html.

The editor cannot be exercised in a browser from this session, so this
script proves the contract the editor commits to:

1. Loading samples/classification-fixture.json and re-saving it
   round-trips through the schema (no shape changes that would break
   validation).
2. Editor-style mutations (setting humanOverride blocks) keep the
   document schema-valid.
3. A column humanOverride that violates the FK authority allOf/if-then
   block is rejected by the schema (proves the editor's must-warn rule
   is grounded in the schema, not just UI convention).
4. The privacy invariant — scanner-emitted fields untouched — is
   preserved across mutations (the editor only writes humanOverride).

Run from the workspace root:

    .venv/Scripts/python.exe tools/classification-editor-self-test.py
"""

from __future__ import annotations

import copy
import json
import sys
from pathlib import Path

from jsonschema import Draft7Validator, ValidationError

ROOT = Path(__file__).resolve().parent.parent
SCHEMA_PATH = ROOT / "AccessHarvester.Pipeline" / "Classification" / "classification-schema.json"
FIXTURE_PATH = ROOT / "samples" / "classification-fixture.json"


def main() -> int:
    schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
    validator = Draft7Validator(schema)
    fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

    failures: list[str] = []

    # 1. Sanity: the shipped fixture validates.
    try:
        validator.validate(fixture)
    except ValidationError as e:
        failures.append(f"[sanity] fixture does not validate against schema: {e.message}")

    # 2. Round-trip with editor-style mutations stays schema-valid.
    mutated = copy.deepcopy(fixture)
    apply_editor_style_mutations(mutated)
    try:
        validator.validate(mutated)
    except ValidationError as e:
        failures.append(f"[mutations] post-mutation document failed validation: {e.message}")

    # 3. Privacy: scanner-emitted fields on every column/table unchanged.
    drift = scanner_field_drift(fixture, mutated)
    if drift:
        failures.append("[privacy] editor mutations touched scanner-emitted fields:\n  - " + "\n  - ".join(drift))

    # 4. FK authority: an FK column with a non-FK category override must be rejected.
    bad = copy.deepcopy(fixture)
    # Visit.PersonID is isForeignKey=true; force category to a disallowed value.
    visit = next(t for t in bad["tables"] if t["name"] == "Visit")
    person_id_fk = next(c for c in visit["columns"] if c["name"] == "PersonID")
    person_id_fk["humanOverride"]["category"] = "generic-non-sensitive"
    person_id_fk["humanOverride"]["reviewedBy"] = "test-harness"
    rejected = False
    try:
        validator.validate(bad)
    except ValidationError:
        rejected = True
    if not rejected:
        failures.append("[fk-authority] schema accepted an FK column with category=generic-non-sensitive (must be rejected)")

    # 5. FK authority: disallowed masking strategy also rejected.
    bad2 = copy.deepcopy(fixture)
    visit2 = next(t for t in bad2["tables"] if t["name"] == "Visit")
    person_id_fk2 = next(c for c in visit2["columns"] if c["name"] == "PersonID")
    person_id_fk2["humanOverride"]["maskingStrategy"] = "mock-replace"
    person_id_fk2["humanOverride"]["reviewedBy"] = "test-harness"
    rejected2 = False
    try:
        validator.validate(bad2)
    except ValidationError:
        rejected2 = True
    if not rejected2:
        failures.append("[fk-authority] schema accepted an FK column with maskingStrategy=mock-replace (must be rejected)")

    # 6. FK authority: allowed strategies (preserve, remap-id) pass.
    for strategy in ("preserve", "remap-id"):
        ok = copy.deepcopy(fixture)
        visit3 = next(t for t in ok["tables"] if t["name"] == "Visit")
        col = next(c for c in visit3["columns"] if c["name"] == "PersonID")
        col["humanOverride"]["maskingStrategy"] = strategy
        col["humanOverride"]["reviewedBy"] = "test-harness"
        try:
            validator.validate(ok)
        except ValidationError as e:
            failures.append(f"[fk-authority] schema rejected FK column with allowed maskingStrategy={strategy!r}: {e.message}")

    # 7. Table-level isLookup three-state — null/true/false all valid.
    for value in (None, True, False):
        ok = copy.deepcopy(fixture)
        ok["tables"][0]["humanOverride"]["isLookup"] = value
        try:
            validator.validate(ok)
        except ValidationError as e:
            failures.append(f"[table-override] schema rejected isLookup={value!r}: {e.message}")

    # 8. Clear-override resets to scanner defaults.
    cleared_col = {"category": None, "maskingStrategy": None, "preserveAutonumber": False, "reviewedBy": None, "notes": ""}
    cleared_table = {"isLookup": None, "reviewedBy": None, "notes": ""}
    ok = copy.deepcopy(fixture)
    for t in ok["tables"]:
        t["humanOverride"] = dict(cleared_table)
        for c in t["columns"]:
            c["humanOverride"] = dict(cleared_col)
    try:
        validator.validate(ok)
    except ValidationError as e:
        failures.append(f"[clear-override] all-defaults document failed validation: {e.message}")

    # Report.
    if failures:
        print("FAIL", file=sys.stderr)
        for f in failures:
            print(f"  - {f}", file=sys.stderr)
        return 1
    print("OK: 8 checks passed")
    print(f"  - schema:  {SCHEMA_PATH.relative_to(ROOT).as_posix()}")
    print(f"  - fixture: {FIXTURE_PATH.relative_to(ROOT).as_posix()}")
    return 0


def apply_editor_style_mutations(doc: dict) -> None:
    """Simulate what the editor writes when a reviewer makes plausible edits."""
    person = next(t for t in doc["tables"] if t["name"] == "Person")
    # Table-level: confirm "not a lookup" (was null).
    person["humanOverride"]["isLookup"] = False
    person["humanOverride"]["reviewedBy"] = "test-harness"
    person["humanOverride"]["notes"] = "Confirmed entity table."
    # FirstName: override masking to redact.
    first_name = next(c for c in person["columns"] if c["name"] == "FirstName")
    first_name["humanOverride"]["maskingStrategy"] = "redact"
    first_name["humanOverride"]["reviewedBy"] = "test-harness"
    # PersonID: flag preserve-autonumber.
    pk = next(c for c in person["columns"] if c["name"] == "PersonID")
    pk["humanOverride"]["preserveAutonumber"] = True
    pk["humanOverride"]["reviewedBy"] = "test-harness"
    # MedRecordNo: change category (allowed since not FK).
    med = next(c for c in person["columns"] if c["name"] == "MedRecordNo")
    med["humanOverride"]["category"] = "business-natural-key"
    med["humanOverride"]["maskingStrategy"] = "preserve"
    med["humanOverride"]["reviewedBy"] = "test-harness"
    # Visit.StatusID: FK column; switch masking from default to "remap-id" (allowed).
    visit = next(t for t in doc["tables"] if t["name"] == "Visit")
    status_id = next(c for c in visit["columns"] if c["name"] == "StatusID")
    status_id["humanOverride"]["maskingStrategy"] = "remap-id"
    status_id["humanOverride"]["reviewedBy"] = "test-harness"


SCANNER_TABLE_KEYS = (
    "name", "isLinkedTable", "rowCount", "lookupGuess",
    "nonKeyNonSystemColumnCount", "referencedByFkCount",
    "tableHint", "confidence", "firedHeuristics",
)
SCANNER_COLUMN_KEYS = (
    "name", "ordinalPosition", "dataType", "isAutoNumber", "isPrimaryKey",
    "isForeignKey", "fkTarget", "isMemo", "isOleObject", "isMultiValue",
    "isRequired", "rowCount", "nullCount", "cardinality",
    "uniquenessRatio", "relativeCardinality",
    "piiNameMatch", "piiValueMatch", "valueFormatPattern",
    "guessedCategory", "confidence", "firedHeuristics",
)


def scanner_field_drift(before: dict, after: dict) -> list[str]:
    """Return a list of scanner-emitted fields that differ between two documents.

    The privacy invariant: the editor only writes humanOverride blocks.
    Everything else (including scannedUtc, config, all column flags and
    metrics) must be byte-identical.
    """
    drift: list[str] = []
    for top in ("version", "database", "scannedUtc", "config"):
        if before.get(top) != after.get(top):
            drift.append(f"top-level field {top!r}")
    for tb, ta in zip(before["tables"], after["tables"]):
        for k in SCANNER_TABLE_KEYS:
            if tb.get(k) != ta.get(k):
                drift.append(f"table {tb['name']!r}: scanner field {k!r}")
        for cb, ca in zip(tb["columns"], ta["columns"]):
            for k in SCANNER_COLUMN_KEYS:
                if cb.get(k) != ca.get(k):
                    drift.append(f"table {tb['name']!r}, column {cb['name']!r}: scanner field {k!r}")
    return drift


if __name__ == "__main__":
    raise SystemExit(main())
