"""Cross-provider code-review for the line-ending / fixture-shape
tree-amendment.

The diff covers a focused content change (~190 lines):
  - .gitattributes (new) — pins *.json, *.md, *.bas, *.yaml, *.yml,
    *.cs to LF line endings.
  - SchemaConformanceTests.cs — filter to manifest-shaped fixtures only.
  - CliGenerateDocsTests.cs / CliDeterminismTests.cs — switch from
    minimal-form.json (per-form IR, no Database.Name) to
    northwind-output.json (manifest-shaped).
  - ParseSuccessReportTests.cs — pin JsonSerializerOptions.NewLine to LF.

The bulk renormalize (355 .cs + 35 yaml/md going CRLF -> LF in workdir,
or equivalently in the index for files committed with CRLF) is
intentionally NOT shown — it's mechanical EOL normalization.
"""
from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route

DIFF = Path("tmp/diff-for-review.patch").read_text(encoding="utf-8")

PROMPT = f"""# Code Review — Tree-Amendment: Fix pre-existing test failures from line-ending mismatch + fixture-shape drift

## Context

This is a tree-amendment (out-of-session-workflow follow-up) fixing 8
pre-existing test failures on `main` that the human just authorized
fixing. The failures fall into two families:

### Family A — Line-ending mismatch (4 failures)

The repo has no `.gitattributes`. Git's default `core.autocrlf=true`
on Windows converts committed-LF files to CRLF at checkout.
Specifically:

- `samples/parse-success-reports/*.json` are committed with LF
  (`i/lf` per `git ls-files --eol`) but check out as CRLF (`w/crlf`).
  `ParseSuccessReportTests` reads the file and compares byte-for-byte
  against the generator's output — which used to emit `Environment.NewLine`
  (CRLF on Windows) but should now emit LF for cross-platform determinism.
  TWO failures.

- The C# raw string literal triple-quoted (introduced in C# 11) **does
  not normalize line endings** — it preserves the source file's
  newlines verbatim (per the C# 11 design doc, deliberate decision).
  `VbaModuleBodyLoaderTests` has inline raw-string sources expecting
  `EndsWith("Option Explicit\\n")` (LF). On a Windows checkout, the
  `.cs` file is CRLF, so the raw strings contain CRLF, and the loader
  returns CRLF. TWO failures.

### Family B — Fixture-shape drift (4 failures)

The prior session set (`harvester-finish-operations-and-interpretation-docs`,
Session 3) regenerated `examples/*.json` as verbatim copies of golden
artifacts: `minimal-form.json` and `complex-form.json` became per-form
IR records (no manifest wrapper), while `northwind-output.json`
remained manifest-shaped. The tests below assumed all examples were
manifests:

- `SchemaConformanceTests.Example_ValidatesAgainstHarvesterSchemaV1` —
  validates EVERY `examples/*.json` against the manifest schema.
  TWO failures (minimal-form.json, complex-form.json fail "required
  properties SchemaType/SchemaVersion/HarvesterVersion/Database not
  present").

- `CliGenerateDocsTests.GenerateDocs_AgainstMinimalFormManifest_WritesSchemaSummary`
  and `CliDeterminismTests.GenerateDocs_TwoInvocations_ProduceIdenticalSchemaSummary` —
  both feed `minimal-form.json` to `generate-docs`. The CLI's slug
  derivation reads `Database.Name`; absent from per-form IR, it falls
  back to `(output)` -> slug `-output-`, so the schema-summary.md
  lands at `<outDir>/-output-/` instead of the expected `<outDir>/minimal/`.
  TWO failures.

## The fix

### .gitattributes (new)

Pin `*.json`, `*.md`, `*.bas`, `*.yaml`, `*.yml`, `*.cs` to LF
line endings. Scoped to text fixture and source types; Windows-only
artifacts (`*.csproj`, `*.ps1`) keep their default behavior.

### SchemaConformanceTests.cs

Filter the discovered fixtures to only manifest-shaped files
(`SchemaType == "DatabaseManifest"`). Per-form IR records in examples/
are exempt from the manifest-schema gate; they have their own typed
contract documented in `docs/ir-schema-forms-reports.md`.

### CliGenerateDocsTests.cs / CliDeterminismTests.cs

Switch the test input from `minimal-form.json` (per-form IR) to
`northwind-output.json` (manifest, `Database.Name = "NorthwindDevEdition.accdb"`,
slug `northwinddevedition`). Update expected paths and assertion
target string accordingly.

### ParseSuccessReportTests.cs

Add `NewLine = "\\n"` to the test's `JsonSerializerOptions`. With
`WriteIndented = true`, the default `NewLine` is `Environment.NewLine`
(CRLF on Windows, LF on Linux/macOS) which makes the committed golden
platform-specific. LF is the canonical choice and matches the new
.gitattributes `eol=lf` rule.

## Validation

Full `dotnet build` + `dotnet test AccessHarvester.sln`: all 1894 tests
pass, 2 skipped (the gitignored Northwind YAML mirror skip is intact).
Zero failures across all 10 test assemblies. Build clean (0 errors,
pre-existing CA1416 platform warnings only).

## What to verify

1. **Test changes correctness**: do the test edits actually fix the
   identified failures without weakening coverage? Are there any other
   places that still assume the old fixture shape or LF-via-source-file
   convention?
2. **.gitattributes scope**: is the file-type set right? Are there
   types I should add (e.g., `*.txt`, `*.csv` if any of the harvester's
   tests read them) or remove (e.g., is pinning `*.cs` to LF too broad
   given Visual Studio's Windows conventions)?
3. **`NewLine = "\\n"` in JsonSerializerOptions**: is this the right
   API? In .NET 9+ this is a documented property of
   `JsonSerializerOptions`. Confirm.
4. **Manifest-shape discriminator**: is `SchemaType == "DatabaseManifest"`
   the right discriminator, or should the test use a different signal
   (e.g., presence of `Database` at root, or file naming convention)?
   The chosen discriminator follows the manifest's actual schema
   structure, but it could in principle be more permissive.
5. **CRLF→LF normalization side effects**: the renormalize step
   touched ~390 files (workdir EOL only). Any concerns about that
   blast radius — e.g., a script that depends on CRLF, a binary file
   accidentally treated as text?
6. **Anything I missed**: I traced the 8 failures to the two families
   above. Any other pre-existing failure modes the new .gitattributes
   should head off?

## Diff (~190 lines, content changes only)

```
{DIFF}
```

If something is missing context, prefer to call it out as a context-gap
finding rather than silently flag a Major.

After your analysis, output a JSON block in this exact format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or symbol>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
"""


def main() -> int:
    out_path = Path("tmp/tree-amendment-review.md")
    result = route(
        content=PROMPT,
        task_type="code-review",
        context="Tree-amendment: line-ending + fixture-shape pre-existing failures",
    )
    out_path.write_text(result.content, encoding="utf-8")
    print(f"[wrote {out_path}] model={result.model_name} "
          f"tokens={result.input_tokens}+{result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")
    if result.verification:
        print(f"  verifier: {result.verification.verifier_model} "
              f"verdict={result.verification.verdict}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
