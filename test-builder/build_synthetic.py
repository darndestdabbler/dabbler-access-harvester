"""
Top-level entry: iterate `test-builder/specs/*.json` and build one .accdb per spec.

Output goes to `tests/AccessExporter.Tests/SyntheticDbs/<specName>.accdb` where
<specName> is the spec file's stem (e.g., geometry_basic.json → geometry_basic.accdb).

Each .accdb contains exactly the form described by the spec — nothing else. This
keeps diffs small and enrichment tests focused.

Usage (Windows only, requires Access + pywin32):
    cd test-builder
    python build_synthetic.py                 # build all specs
    python build_synthetic.py geometry_basic  # build a single spec by name
"""

import json
import os
import sys
import time

# Ensure our own module is importable
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from builder import open_new_database, quit_app
from create_from_spec import create_form_from_spec


_HERE = os.path.dirname(os.path.abspath(__file__))
_SPECS_DIR = os.path.join(_HERE, "specs")
_OUT_DIR = os.path.abspath(os.path.join(_HERE, "..", "tests", "AccessExporter.Tests", "SyntheticDbs"))


def _load_spec(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def _build_one(spec_path):
    stem = os.path.splitext(os.path.basename(spec_path))[0]
    db_path = os.path.join(_OUT_DIR, f"{stem}.accdb")

    spec = _load_spec(spec_path)
    print(f"\n=== Building {stem}.accdb from {os.path.basename(spec_path)} ===")
    print(f"    Output: {db_path}")

    access_app = open_new_database(db_path)
    try:
        create_form_from_spec(access_app, spec)
        print(f"    Done.")
    finally:
        quit_app(access_app)
        time.sleep(0.3)


def main():
    os.makedirs(_OUT_DIR, exist_ok=True)

    if len(sys.argv) > 1:
        targets = [os.path.join(_SPECS_DIR, f"{name}.json") for name in sys.argv[1:]]
    else:
        targets = sorted(
            os.path.join(_SPECS_DIR, f)
            for f in os.listdir(_SPECS_DIR)
            if f.endswith(".json")
        )

    for spec_path in targets:
        if not os.path.exists(spec_path):
            print(f"Spec not found: {spec_path}", file=sys.stderr)
            sys.exit(1)
        _build_one(spec_path)

    print(f"\n=== All builds complete. Output directory: {_OUT_DIR} ===")


if __name__ == "__main__":
    main()
