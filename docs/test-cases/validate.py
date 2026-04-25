#!/usr/bin/env python3
"""Validate all test case expected outputs against the harvester schema."""
import json
import sys
from pathlib import Path
from jsonschema import Draft202012Validator

base = Path(__file__).parent
schema_path = base / "harvester-schema.v0.json"

with open(schema_path) as f:
    schema = json.load(f)

validator = Draft202012Validator(schema)

expected_files = sorted(base.glob("*.expected.json"))
print(f"Found {len(expected_files)} expected output files\n")

all_passed = True
for ef in expected_files:
    with open(ef) as f:
        instance = json.load(f)
    errors = sorted(validator.iter_errors(instance), key=lambda e: e.path)
    if errors:
        all_passed = False
        print(f"FAIL: {ef.name}")
        for err in errors[:10]:
            path = ".".join(str(p) for p in err.path) or "<root>"
            print(f"  {path}: {err.message[:200]}")
        if len(errors) > 10:
            print(f"  ... and {len(errors) - 10} more errors")
    else:
        print(f"PASS: {ef.name}")

print()
sys.exit(0 if all_passed else 1)
