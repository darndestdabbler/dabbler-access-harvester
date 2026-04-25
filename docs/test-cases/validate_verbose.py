#!/usr/bin/env python3
"""Validate with full error detail."""
import json
import sys
from pathlib import Path
from jsonschema import Draft202012Validator

base = Path(__file__).parent
with open(base / "harvester-schema.v0.json") as f:
    schema = json.load(f)
validator = Draft202012Validator(schema)

target = sys.argv[1] if len(sys.argv) > 1 else None
expected_files = sorted(base.glob("*.expected.json"))

for ef in expected_files:
    if target and target not in ef.name:
        continue
    with open(ef) as f:
        instance = json.load(f)
    errors = sorted(validator.iter_errors(instance), key=lambda e: list(e.absolute_path))
    if not errors:
        print(f"PASS: {ef.name}")
        continue
    print(f"\n=== {ef.name}: {len(errors)} errors ===")
    for err in errors:
        path = ".".join(str(p) for p in err.absolute_path) or "<root>"
        # Show only the validator + schema path + concise message
        schema_path = ".".join(str(p) for p in err.absolute_schema_path)
        msg = err.message
        if len(msg) > 300:
            msg = msg[:297] + "..."
        print(f"  [{path}] ({schema_path}): {msg}")
