"""Driver for Session 2 of dabbler-platform-discovery.

Run from inside `main/`:

    .venv/Scripts/python.exe docs/session-sets/dabbler-platform-discovery/session-2-driver.py <step>

Steps:
    migration-map   - Step 4.1 Feature 3 access-to-platform migration map
    pdf-boundary    - Step 4.2 Feature 4 dabbler-pdf boundary doc
    verify          - Step 6 cross-provider verification
    next-orc        - Step 8 next-orchestrator recommendation for Session 3
    cost-report     - Step 8 cost report
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from ai_router import route, print_cost_report
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "dabbler-platform-discovery"
OUT_DIR = REPO_ROOT / "docs" / "cross-repo"
PLATFORM_REPO = REPO_ROOT.parent / "dabbler-platform"
PDF_REPO = REPO_ROOT.parent / "dabbler-pdf"

# Pinned at Session 1 commit shas so currency-header values stay consistent
# across all three Session 2 outputs. Session 3's smoke-test may use the
# same pins (the snapshot is set-scoped, not session-scoped).
PLATFORM_SHA = "aa77020e1ca0f5d0608670539153d0a22530bd25"
PLATFORM_DATE = "2026-05-16"
PDF_SHA = "3df4134cb61b572e23a3079e54279f891bcee263"
PDF_DATE = "2026-04-22"
SNAPSHOT_DATE = "2026-05-17"

CURRENCY_HEADER_PLATFORM = f"""> **Snapshot of:** `../dabbler-platform` @ commit `{PLATFORM_SHA[:12]}` on `{PLATFORM_DATE}`
> **Captured on:** `{SNAPSHOT_DATE}`
> **Regenerate when:** the platform's component library changes
> meaningfully, or when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.
"""

CURRENCY_HEADER_PDF = f"""> **Snapshot of:** `../dabbler-pdf` @ commit `{PDF_SHA[:12]}` on `{PDF_DATE}`
> **Captured on:** `{SNAPSHOT_DATE}`
> **Regenerate when:** the PDF library's public surface changes, or
> when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.
"""


def _log() -> SessionLog:
    return SessionLog(SESSION_SET)


def _save(name: str, content: str) -> Path:
    """Persist routed output to disk BEFORE any display side effects.

    See project-guidance Convention: Persist Routed Output To Disk Before
    Any Display Or Logging Side Effects.
    """
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    path = OUT_DIR / name
    path.write_text(content, encoding="utf-8")
    return path


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


# ---------------------------------------------------------------------------
# Step 4.1: Feature 3 — access-to-platform migration map
# ---------------------------------------------------------------------------

def step_migration_map() -> None:
    inventory_text = _read_text(OUT_DIR / "dabbler-platform-components.md")
    rdf_schema_text = _read_text(OUT_DIR / "dabbler-platform-report-rdf-schema.md")
    spec_text = _read_text(SESSION_SET / "spec.md")

    prompt = f"""Author `docs/cross-repo/access-to-platform-migration-map.md` for the
`dabbler-platform-discovery` session set (Feature 3).

GOAL
----
Cross-reference each major Microsoft Access construct kind against the
dabbler-platform components and patterns inventoried in Session 1. For
every Access construct kind, name the natural migration target(s) in
dabbler-platform — components, RDF schema seams, or general MudBlazor
composition patterns the platform leans on. Flag gaps: Access constructs
with no clear platform counterpart go on a "platform-extension
candidates" list at the end of the document.

CONSTRUCT KINDS (cover each in its own subsection, in this order):
1. Form (bound and unbound)
2. Subform
3. Report
4. Subreport
5. Query (saved SELECT/parameter queries; action queries)
6. Macro (AutoExec, validation/data macros, navigation macros, embedded
   macros on controls)
7. VBA event handler (form/control events; module-level public Subs/
   Functions called from events)

For EACH construct kind, produce a subsection with this structure:

  ### <Construct kind>

  - **Access semantics (one paragraph):** what the construct does in
    Access and the migration intent (what aspect must survive translation).
  - **Primary platform target:** the dabbler-platform component(s) or
    pattern(s) that are the natural migration target, **with file paths
    grounded in the Session 1 inventory** (`docs/cross-repo/dabbler-platform-components.md`)
    or the RDF schema reference (`docs/cross-repo/dabbler-platform-report-rdf-schema.md`).
  - **Mapping notes:** how individual Access pieces correspond to
    component parameters or schema fields. Use a small table where the
    mapping has more than 2-3 rows.
  - **Stability / posture:** call out when the target is `stable`,
    `in-flight`, or `deprecated` per the Session 1 inventory's
    stability tags. If the migration target straddles a stable+deprecated
    boundary (e.g., shell composition recommended via MudBlazor primitives
    rather than via Compat shell), call that out.
  - **Open gaps:** anything Access provides that the platform target does
    NOT fully cover. Each gap becomes an entry in the
    "Platform-extension candidates" section at the end.

After the seven construct subsections, include:

  ### Platform-extension candidates

  A consolidated bulleted list of every gap surfaced above. For each
  candidate, name: (a) the Access feature it would unblock, (b) the
  proposed platform surface to extend, (c) a one-sentence justification
  rooted in migration value.

  ### Cross-cutting notes

  Document any patterns that span construct kinds — for example, the
  declarative-security-wrapper pattern (`DabblerSecured`,
  `RestrictedFormField`, `RestrictedGridColumn`) replaces ad-hoc Access
  VBA permission checks across forms, subforms, and event handlers. Keep
  this section short — 3 to 6 bullets.

CONSTRAINTS
-----------
- **Cite paths under `../dabbler-platform/` from the Session 1 inventory
  verbatim.** Do not invent component names or file paths. If a
  component appears in the inventory below, cite it; if it does not,
  the gap goes in "Platform-extension candidates".
- **For the Report and Subreport subsections, also cite RDF sections**
  (`metadata`, `dataSources`, `parameters`, `fields`, `bands`, `grouping`,
  `sorting`, `styles`, `expressions`) from the RDF schema reference —
  these are the harvester-to-platform seam.
- **Do not copy platform source code into this doc.** Synthetic minimal
  examples are fine; verbatim sample-file content is not (per the
  Session 1 boundary).
- **No interpretation outside the inventory.** When a mapping is
  uncertain (Access feature exists, platform has no direct counterpart),
  say so explicitly and surface it as a candidate rather than
  speculating.
- **Use the citation form:** `\\`<ComponentName>\\`` for components,
  `\\`<SectionName>\\`` for RDF sections, `\\`<path>\\`` for files.
- **No emojis.**

SESSION-SET CONTEXT
-------------------
- The harvester is a metadata-extraction toolchain; downstream consumers
  use this map to plan an Access-to-Blazor migration in a future repo.
- The audience for this document is a downstream AI agent or migration
  engineer reading the inventory and the RDF schema reference together.
  Their question is: "I have an Access form/subform/report/query/macro/
  VBA handler — what do I map it to?"
- Currency header (already supplied by the driver) will be prepended;
  do not include it in your output.

INPUT MATERIAL
==============

### Session 1 inventory (verbatim)

```markdown
{inventory_text}
```

### Session 1 RDF schema reference (verbatim)

```markdown
{rdf_schema_text}
```

### Spec excerpt (Feature 3 only)

```markdown
{spec_text}
```

OUTPUT REQUIREMENTS
===================
- Start with a `## Overview` section (one short paragraph + a 7-row
  summary table indexing each construct kind to its primary target).
- Then the seven `### <Construct kind>` subsections in the order
  above.
- Then `### Platform-extension candidates`.
- Then `### Cross-cutting notes`.
- No code fences around the entire document; only around code or JSON
  snippets within it.
- Plain markdown, no YAML frontmatter, no leading H1 (the driver will
  prepend a title).
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=55,
        context=(
            "Session 2 of dabbler-platform-discovery (harvester). Cross-"
            "reference Access constructs against dabbler-platform component "
            "inventory + RDF schema seams; output a migration map with a "
            "platform-extension candidates list and cross-cutting notes."
        ),
        session_set=str(SESSION_SET),
        session_number=2,
    )

    title = "# Access-to-Platform Migration Map\n\n"
    body = result.content.strip() + "\n"
    full = title + CURRENCY_HEADER_PLATFORM + "\n" + body
    path = _save("access-to-platform-migration-map.md", full)
    print(f"wrote {path}")

    _log().log_step(
        session_number=2,
        step_number=1,
        step_key="session-2/migration-map",
        description="Route Feature 3 migration map analysis",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )


# ---------------------------------------------------------------------------
# Step 4.2: Feature 4 — dabbler-pdf boundary doc
# ---------------------------------------------------------------------------

PDF_README = (PDF_REPO / "README.md").read_text(encoding="utf-8") if (PDF_REPO / "README.md").exists() else ""
PDF_CHANGELOG_PATH = PDF_REPO / "CHANGELOG.md"
PDF_CHANGELOG = PDF_CHANGELOG_PATH.read_text(encoding="utf-8") if PDF_CHANGELOG_PATH.exists() else ""

EXPORT_README_PATH = PLATFORM_REPO / "src" / "Libs" / "Dabbler.Reports.Export" / "README.md"
EXPORT_README = EXPORT_README_PATH.read_text(encoding="utf-8") if EXPORT_README_PATH.exists() else ""

EXPORT_CSPROJ_PATH = PLATFORM_REPO / "src" / "Libs" / "Dabbler.Reports.Export" / "Dabbler.Reports.Export.csproj"
EXPORT_CSPROJ = EXPORT_CSPROJ_PATH.read_text(encoding="utf-8") if EXPORT_CSPROJ_PATH.exists() else ""

PKG_PROPS_PATH = PLATFORM_REPO / "Directory.Packages.props"
PKG_PROPS_DABBLERPDF_LINE = ""
if PKG_PROPS_PATH.exists():
    for ln in PKG_PROPS_PATH.read_text(encoding="utf-8").splitlines():
        if "DabblerPdf" in ln:
            PKG_PROPS_DABBLERPDF_LINE = ln.strip()
            break

EXPORTER_CS = (PLATFORM_REPO / "src" / "Libs" / "Dabbler.Reports.Export" / "PdfExporter.cs").read_text(encoding="utf-8")
TEXT_MEASURER_CS = (PLATFORM_REPO / "src" / "Libs" / "Dabbler.Reports.Core" / "Layout" / "OpenPdfTextMeasurer.cs").read_text(encoding="utf-8")


def step_pdf_boundary() -> None:
    prompt = f"""Author `docs/cross-repo/dabbler-pdf-overview.md` for the
`dabbler-platform-discovery` session set (Feature 4).

GOAL
----
Document the BOUNDARY between dabbler-platform and dabbler-pdf at
sufficient fidelity that a future migration agent understands:

1. What dabbler-pdf is (one paragraph).
2. The public API surface that dabbler-platform calls (the contract).
3. How dabbler-platform consumes it (the seam: which packages, which
   types, which methods).
4. Setup the consuming app needs (NuGet reference, version pin,
   license obligations).
5. What is OUT of scope — internals, transitive deps, things a
   migration agent should NOT have to know.

This is a boundary doc, not a tutorial. Do not duplicate dabbler-pdf's
README — link to it (`../dabbler-pdf/README.md`) and document only the
slice the platform actually uses.

STRUCTURE
---------
Use this exact section order:

  ## What `dabbler-pdf` is

  One-paragraph description. Name the upstream project it ports
  (OpenPDF) and the license consequence. Reference the README rather
  than copying its bullet list.

  ## Public surface consumed by dabbler-platform

  A short table of the namespaces and key types dabbler-platform
  references, with the platform call site in each row. Use the
  `Reports.Export` and `Reports.Core` source citations below — do not
  invent types.

  ## How dabbler-platform calls it

  A short prose narrative (two or three paragraphs) describing the
  call path:
  - Reports.Core's text measurement seam (`OpenPdfTextMeasurer`)
  - Reports.Export's render path (`PdfExporter` → `PdfPageRenderer` →
    `PdfStyleResolver`)
  Each paragraph cites the source file paths under
  `../dabbler-platform/src/Libs/`.

  ## Setup the consuming repo needs

  Concrete bullets:
  - NuGet reference (`Directory.Packages.props` pin + `<PackageReference Include="DabblerPdf" />`)
  - Version currently pinned (cite the value from `Directory.Packages.props`)
  - .NET TFM (.NET 10)
  - License obligations (LGPL-2.1-only OR MPL-2.0) and the
    `THIRD-PARTY-NOTICES.txt` requirement noted in
    `Dabbler.Reports.Export/README.md`
  - No DI registration required for `PdfExporter` (constructed
    directly per export)

  ## Out of scope

  Bulleted list of things this doc deliberately does not cover:
  - dabbler-pdf internals (PdfArray, PdfBody, PdfCrossReferenceTable,
    etc.)
  - PdfPig (test-only transitive dep used for rendering-visibility
    tests; not shipped)
  - Any consumer beyond `Dabbler.Reports.Export` / `Dabbler.Reports.Core`
  - The Migration Map (Feature 3) covers Access-to-platform mapping;
    this doc does not.

CONSTRAINTS
-----------
- **Cite real paths only.** Use the source material below; do not
  invent file paths, type names, or method names.
- **No interpretive expansion of upstream OpenPDF behavior.** Only
  describe what the platform actually uses.
- **No emojis.**
- **Currency header is prepended by the driver** — do not include it
  in your output.
- **No leading H1.** The driver will prepend the title.

INPUT MATERIAL
==============

### dabbler-pdf README (verbatim)

```markdown
{PDF_README}
```

### dabbler-pdf CHANGELOG (excerpt, for version anchor)

```markdown
{PDF_CHANGELOG[:2000]}
```

### `Dabbler.Reports.Export/README.md` (verbatim)

```markdown
{EXPORT_README}
```

### `Dabbler.Reports.Export/Dabbler.Reports.Export.csproj` (verbatim)

```xml
{EXPORT_CSPROJ}
```

### `Directory.Packages.props` — DabblerPdf line

```
{PKG_PROPS_DABBLERPDF_LINE}
```

### `Dabbler.Reports.Export/PdfExporter.cs` (verbatim)

```csharp
{EXPORTER_CS}
```

### `Dabbler.Reports.Core/Layout/OpenPdfTextMeasurer.cs` (verbatim)

```csharp
{TEXT_MEASURER_CS}
```

OUTPUT
------
Plain markdown. Five sections in the order above. No YAML, no leading
H1.
"""

    result = route(
        content=prompt,
        task_type="documentation",
        complexity_hint=30,
        context=(
            "Session 2 of dabbler-platform-discovery (harvester). Document "
            "the dabbler-pdf boundary as the platform consumes it. Boundary "
            "doc only — not a tutorial; not a duplicate of dabbler-pdf's "
            "README."
        ),
        session_set=str(SESSION_SET),
        session_number=2,
    )

    title = "# dabbler-pdf Boundary Overview\n\n"
    body = result.content.strip() + "\n"
    full = title + CURRENCY_HEADER_PDF + "\n" + body
    path = _save("dabbler-pdf-overview.md", full)
    print(f"wrote {path}")

    _log().log_step(
        session_number=2,
        step_number=2,
        step_key="session-2/pdf-boundary",
        description="Route Feature 4 dabbler-pdf boundary doc",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "documentation",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )


# ---------------------------------------------------------------------------
# Step 6: Cross-provider session verification
# ---------------------------------------------------------------------------

VERIFICATION_TAIL = """

After your analysis, output a JSON block in this exact format:

```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or section name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }
  ]
}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
"""


def step_verify(round_number: int = 1) -> None:
    migration_map = _read_text(OUT_DIR / "access-to-platform-migration-map.md")
    pdf_overview = _read_text(OUT_DIR / "dabbler-pdf-overview.md")
    inventory = _read_text(OUT_DIR / "dabbler-platform-components.md")
    rdf_schema = _read_text(OUT_DIR / "dabbler-platform-report-rdf-schema.md")
    spec_text = _read_text(SESSION_SET / "spec.md")
    aiassign = _read_text(SESSION_SET / "ai-assignment.md")

    prompt = f"""Your response MUST begin with a detailed prose analysis for each
concern below BEFORE the JSON block. Do not produce a JSON-only response.

You are the cross-provider session verifier for Session 2 of 3 of
`dabbler-platform-discovery`, a session set in the `dabbler-access-harvester`
repo that produces a read-only structured snapshot of the sibling
`dabbler-platform` repo (and its `dabbler-pdf` dependency).

Session 2 delivers two new docs under `docs/cross-repo/`:

1. `access-to-platform-migration-map.md` (Feature 3 — Access construct
   kinds cross-referenced against the dabbler-platform component
   inventory and the RDF schema seams; gaps flagged as platform-extension
   candidates).
2. `dabbler-pdf-overview.md` (Feature 4 — boundary doc for the
   `dabbler-pdf` PDF library as the platform consumes it).

Session 1 (already VERIFIED on round 3) produced:
- `dabbler-platform-components.md` — the component inventory.
- `dabbler-platform-report-rdf-schema.md` — the RDF schema reference.

VERIFICATION OBJECTIVES
=======================

For BOTH new docs:
- Currency header is present and matches the Session 1 form (snapshot
  commit + date, regenerate-when trigger).
- No platform source code was copied verbatim into either doc (the
  Boundaries clause in spec.md forbids it; synthetic minimal examples
  are fine).
- All cited paths under `../dabbler-platform/` and `../dabbler-pdf/`
  appear to be real (matched against the inputs the session was given).
- No invented component names, file paths, RDF section names, or
  DabblerPdf type names.
- Tone is reference-doc, not tutorial; no emojis.

For `access-to-platform-migration-map.md` specifically:
- All seven Access construct kinds are covered (Form, Subform, Report,
  Subreport, Query, Macro, VBA event handler) in distinct subsections.
- Each subsection follows the spec'd structure: Access semantics,
  Primary platform target, Mapping notes, Stability/posture, Open gaps.
- Report and Subreport subsections cite RDF sections from
  `dabbler-platform-report-rdf-schema.md`.
- Platform-extension candidates section consolidates the per-construct
  Open gaps.
- Stability tags (stable/in-flight/deprecated) used in the doc match
  the Session 1 inventory's tagging — especially calling out that the
  Compat shell pieces are `deprecated`.

For `dabbler-pdf-overview.md` specifically:
- Boundary doc only — no duplication of dabbler-pdf README content
  beyond what is necessary to explain the slice the platform uses.
- Public-surface table cites real types from the source provided.
- Setup section pins the actual DabblerPdf version from
  Directory.Packages.props (do not accept a guessed version).
- License section calls out LGPL-2.1-only OR MPL-2.0 and the
  THIRD-PARTY-NOTICES.txt obligation.
- Out-of-scope section is present.

OUT OF SCOPE FOR THIS VERIFICATION
- Session 1 outputs are already VERIFIED on round 3; do not re-review
  them. They are included below as reference material the verifier
  should use to check Session 2's citations against.
- Build/test results for the harvester C# code are unrelated to this
  doc-only session; they are reported as passing as of Session 1.
- Spec interpretation disagreements raised in Session 1 round 1 (sample
  .rdf.json copying) were adjudicated by the human via Opus tiebreaker
  and adopted — do not re-litigate.

INPUT MATERIAL
==============

### spec.md

```markdown
{spec_text}
```

### ai-assignment.md (Sessions 1-3 blocks)

```markdown
{aiassign}
```

### Session 1 inventory (REFERENCE — do not re-review)

```markdown
{inventory}
```

### Session 1 RDF schema reference (REFERENCE — do not re-review)

```markdown
{rdf_schema}
```

### NEW (Session 2): access-to-platform-migration-map.md

```markdown
{migration_map}
```

### NEW (Session 2): dabbler-pdf-overview.md

```markdown
{pdf_overview}
```
{VERIFICATION_TAIL}
"""

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=2,
    )

    _log().save_session_review(
        session_number=2,
        review_text=result.content,
        round_number=round_number,
    )

    _log().log_step(
        session_number=2,
        step_number=10 + round_number,
        step_key=f"session-2/verify-round-{round_number}",
        description=f"Cross-provider session verification round {round_number}",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )

    print(f"verification round {round_number}: model={result.model_name}, cost=${result.cost_usd:.4f}")


# ---------------------------------------------------------------------------
# Step 8: Next-orchestrator recommendation for Session 3
# ---------------------------------------------------------------------------

def step_next_orc() -> None:
    spec_text = _read_text(SESSION_SET / "spec.md")
    aiassign = _read_text(SESSION_SET / "ai-assignment.md")
    review_files = sorted((SESSION_SET / "session-reviews").glob("session-002*.md"))
    review_excerpt = "\n\n".join(p.read_text(encoding="utf-8") for p in review_files)[:6000]

    prompt = f"""You are the AI router's analysis pass for choosing the orchestrator
for Session 3 of `dabbler-platform-discovery` (the LAST session in the
set).

Session 3 plan (from spec.md):
- Take a single concrete sample (one form from
  `samples/NorthwindDevEdition.accdb`) and walk through the
  migration-relevance map by hand. Note any gaps the map missed.
- Address verifier-surfaced findings.
- Generate change-log.

Inputs:
- Spec.
- ai-assignment.md (Sessions 1, 2 actuals).
- Session 2 verifier review.

Recommend the orchestrator for Session 3. Three fields:

  ENGINE: claude-code | codex | gemini-cli
  MODEL: <specific model id>
  EFFORT: low | medium | high | max
  RATIONALE: <one sentence>

Do not include any other content. Be cost-aware — Session 3 is a
walkthrough + cleanup session, not a synthesis session.

### spec.md

```markdown
{spec_text}
```

### ai-assignment.md

```markdown
{aiassign}
```

### Session 2 review (excerpt)

```markdown
{review_excerpt}
```
"""
    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=20,
        session_set=str(SESSION_SET),
        session_number=2,
    )

    out_path = SESSION_SET / "next-orc-recommendation-session-3.txt"
    out_path.write_text(result.content.strip() + "\n", encoding="utf-8")
    print(f"wrote {out_path}")
    print(result.content)

    _log().log_step(
        session_number=2,
        step_number=20,
        step_key="session-2/next-orc-recommendation",
        description="Route next-orchestrator recommendation for Session 3",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )


# ---------------------------------------------------------------------------
# Step 8: Cost report
# ---------------------------------------------------------------------------

def step_cost_report() -> None:
    print_cost_report(str(SESSION_SET))


# ---------------------------------------------------------------------------
# CLI dispatch
# ---------------------------------------------------------------------------

DISPATCH = {
    "migration-map": step_migration_map,
    "pdf-boundary": step_pdf_boundary,
    "verify": step_verify,
    "next-orc": step_next_orc,
    "cost-report": step_cost_report,
}


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    step = argv[1]
    if step == "verify":
        round_arg = int(argv[2]) if len(argv) >= 3 else 1
        step_verify(round_arg)
        return 0
    if step not in DISPATCH:
        print(f"unknown step: {step}\n")
        print(__doc__)
        return 2
    DISPATCH[step]()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
