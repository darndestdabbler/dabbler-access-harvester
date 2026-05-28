#!/usr/bin/env python3
"""
migrate_v0_to_v1.py -- Migrate harvester-schema v0 parser-output files to v1.0 DatabaseManifest.

Two mechanical transforms per docs/schema/v1-adr.md Known Issues and D20 decision:
  1. Rename all JSON object keys from camelCase to PascalCase (first-letter uppercase).
  2. Migrate known enum string VALUES to PascalCase (see tables below).
  3. Wrap the bare v0 ParserOutput envelope {schemaVersion, source, items, ...} in a
     DatabaseManifest root document.

The script is idempotent: re-running on an already-migrated file (SchemaType ==
"DatabaseManifest") is a no-op.

Fixtures that contain semantic content incompatible with v1.0 after mechanical migration
(e.g., Source: "g_CurrentUserID" -- a variable name used where an enum is required) are
documented in the stop-rule section of v1-adr.md and excluded from the v1.0 conformance
regression; those fixtures remain as valid v0-schema references.

Usage:
  python tools/migrate_v0_to_v1.py <file.json> [<file2.json> ...]
  python tools/migrate_v0_to_v1.py docs/test-cases/*.expected.json
  python tools/migrate_v0_to_v1.py --dry-run <file.json>

Exit codes:
  0 -- all files processed successfully (or were already up-to-date)
  1 -- at least one file could not be processed
"""

import json
import sys
import argparse
import pathlib
from datetime import datetime, timezone
from typing import Any

# ---- Key-mapping table --------------------------------------------------
# All JSON object keys are migrated by capitalising the first character.
KEY_RULE = "capitalize_first_char"

# ---- Enum-valued keys ---------------------------------------------------
# For keys in this set, the string VALUE also gets first-letter-capitalized
# (plus POST_CAP_OVERRIDES applied after for the one edge case).
# Both camelCase and PascalCase key spellings are included.
_ENUM_KEY_BASES = {
    "kind",            # ParserSource.Kind, Statement.Kind, HarvestedItem.ItemKind context
    "itemKind",        # HarvestedItem discriminator
    "hostKind",        # ParserSource.HostContext.HostKind
    "referenceKind",   # ResolvedReference.ReferenceKind
    "resolutionStatus",
    "purity",          # Classification.Purity
    "translatability", # Classification.Translatability
    "severity",        # ParserDiagnostic.Severity
    "reason",          # UnresolvedItem.Reason
    "literalKind",
    "purposeTags",     # array of enum strings
    "constructionMode",# SqlReference.ConstructionMode
    "context",         # ExpressionItem.Context
}
ENUM_VALUED_KEYS: frozenset[str] = frozenset(
    {k for k in _ENUM_KEY_BASES} |
    {k[0].upper() + k[1:] for k in _ENUM_KEY_BASES}
)

# ---- Global value overrides ---------------------------------------------
# Applied to ALL string values BEFORE any per-key processing.
# Used for values that can't be inferred by first-char capitalization alone:
#   - Multi-segment identifiers from v0 that do not follow simple camelCase
#     (e.g. "gosub" -> "GoSub" -- all-lowercase in v0, not "goSub").
#   - ParserQueryParameter.Source enum values, which share the "source" key
#     with the non-enum ParserOutput.Source object.  Adding "source" to
#     ENUM_VALUED_KEYS would corrupt non-enum Source fields, so the enum
#     values are mapped explicitly here instead.
GLOBAL_VALUE_OVERRIDES: dict[str, str] = {
    # Statement.Kind: all-lowercase variants that first-char-capitalize incorrectly
    "gosub": "GoSub",          # v0 uses "gosub" (not camelCase "goSub")
    # ParserQueryParameter.Source enum
    "formControl": "FormControl",
    "userPrompt": "UserPrompt",
    "namedParameter": "NamedParameter",
    "vbaExpression": "VbaExpression",
    "concatenatedVariable": "ConcatenatedVariable",
}

# ---- Post-capitalization overrides --------------------------------------
# Applied AFTER first-letter capitalization for values in ENUM_VALUED_KEYS.
# Needed where the v0 camelCase spelling produces the WRONG PascalCase result.
#   "uiBound" -> capitalize -> "UiBound"  but schema expects "UIBound"
POST_CAP_OVERRIDES: dict[str, str] = {
    "UiBound": "UIBound",
}

# ---- DatabaseManifest envelope defaults ----------------------------------
HARVESTER_VERSION_MIGRATED = "migrated-from-v0"
SCHEMA_VERSION_V1 = "1.0"


def log_tables() -> None:
    print("=== Key-mapping table ===")
    print(f"  Rule: {KEY_RULE}")
    print("  (Every camelCase key is migrated by uppercasing its first character)")
    print()
    print("=== Enum-valued keys (value also first-letter-capitalized) ===")
    for k in sorted(_ENUM_KEY_BASES):
        print(f"  {k!r} / {k[0].upper() + k[1:]!r}")
    print()
    print("=== Global value overrides (applied to all string values) ===")
    for old, new in GLOBAL_VALUE_OVERRIDES.items():
        print(f"  {old!r} -> {new!r}")
    print()
    print("=== Post-capitalization overrides ===")
    for old, new in POST_CAP_OVERRIDES.items():
        print(f"  {old!r} -> {new!r}")
    print()


def pascal_key(key: str) -> str:
    if not key:
        return key
    return key[0].upper() + key[1:]


def migrate_string_value(value: str, parent_key: str | None) -> str:
    # Global overrides first (specific multi-segment or path-ambiguous values).
    if value in GLOBAL_VALUE_OVERRIDES:
        return GLOBAL_VALUE_OVERRIDES[value]
    # Per-key first-letter capitalization for known enum-valued keys.
    if parent_key in ENUM_VALUED_KEYS and value and value[0].islower():
        value = value[0].upper() + value[1:]
        value = POST_CAP_OVERRIDES.get(value, value)
    return value


def migrate_value(value: Any, parent_key: str | None = None) -> Any:
    if isinstance(value, dict):
        return {pascal_key(k): migrate_value(v, k) for k, v in value.items()}
    if isinstance(value, list):
        return [migrate_value(item, parent_key) for item in value]
    if isinstance(value, str):
        return migrate_string_value(value, parent_key)
    return value


def is_already_migrated(doc: dict) -> bool:
    return doc.get("SchemaType") == "DatabaseManifest" and doc.get("SchemaVersion") == SCHEMA_VERSION_V1


def is_v0_parser_output(doc: dict) -> bool:
    return "schemaVersion" in doc or (
        "SchemaVersion" in doc and "Source" in doc and "Items" in doc
    )


def wrap_in_manifest(pascal_doc: dict, source_path: pathlib.Path) -> dict:
    """
    Wrap a PascalCase'd v0 ParserOutput in a DatabaseManifest envelope.

    All fixtures go into Modules[] with ModuleKind "Standard".  FormExport
    requires a Spec (FormComponentSpec) that v0 fixture data does not carry,
    so fixtures whose source was a form module are wrapped as ModuleExport
    rather than FormExport.
    """
    module_name = (
        pascal_doc.get("Source", {})
        .get("Location", {})
        .get("Module")
        or source_path.stem
    )

    module_export = {
        "ModuleName": module_name,
        "ModuleKind": "Standard",
        "ParserOutput": pascal_doc,
    }

    db_name = source_path.stem
    exported_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.0000000+00:00")

    return {
        "SchemaType": "DatabaseManifest",
        "SchemaVersion": SCHEMA_VERSION_V1,
        "HarvesterVersion": HARVESTER_VERSION_MIGRATED,
        "Database": {
            "Name": db_name,
            "FilePath": str(source_path),
            "ExportedAt": exported_at,
        },
        "Modules": [module_export],
    }


def migrate_file(path: pathlib.Path, dry_run: bool) -> bool:
    try:
        text = path.read_text(encoding="utf-8")
        doc = json.loads(text)
    except Exception as exc:
        print(f"  ERROR reading {path}: {exc}")
        return False

    if not isinstance(doc, dict):
        print(f"  SKIP {path}: root is not a JSON object")
        return True

    if is_already_migrated(doc):
        print(f"  UP-TO-DATE {path}")
        return True

    pascal_doc = migrate_value(doc)

    if is_v0_parser_output(doc):
        result = wrap_in_manifest(pascal_doc, path)
    else:
        print(f"  WARN {path}: not a recognized v0 ParserOutput shape; applying key PascalCasing only.")
        result = pascal_doc

    output = json.dumps(result, indent=2, ensure_ascii=False) + "\n"

    if dry_run:
        print(f"  DRY-RUN {path} (would write {len(output)} bytes)")
        print(output[:600] + ("..." if len(output) > 600 else ""))
    else:
        path.write_text(output, encoding="utf-8")
        print(f"  MIGRATED {path}")

    return True


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Migrate harvester-schema v0 parser-output JSON files to v1.0 DatabaseManifest."
    )
    parser.add_argument("files", nargs="+", type=pathlib.Path, help="JSON files to migrate.")
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Print transformed output without writing files."
    )
    args = parser.parse_args()

    log_tables()

    success = True
    for path in args.files:
        if not path.exists():
            print(f"  ERROR {path}: file not found")
            success = False
            continue
        ok = migrate_file(path, dry_run=args.dry_run)
        if not ok:
            success = False

    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
