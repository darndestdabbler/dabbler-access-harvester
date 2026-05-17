"""Driver for Session 1 of dabbler-platform-discovery.

Run from inside `main/` (or whichever working tree this set runs in):

    .venv/Scripts/python.exe docs/session-sets/dabbler-platform-discovery/session-1-driver.py <step>

Steps:
    ai-assignment   - Step 3.5 author ai-assignment.md
    inventory       - Step 4 Feature 1: dabbler-platform component inventory
    schema          - Step 4 Feature 2: report.rdf.json schema doc
    verify          - Step 6 cross-provider verification
    next-orc        - Step 8 next-orchestrator recommendation for Session 2
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


def _walk_summary(root: Path, include_globs: list[str], max_per_glob: int = 200) -> str:
    """Return a flat path listing the verifier (and Gemini Pro) can ground on."""
    lines = []
    for pattern in include_globs:
        matches = sorted(p for p in root.glob(pattern) if "\\bin\\" not in str(p) and "/bin/" not in str(p) and "\\obj\\" not in str(p) and "/obj/" not in str(p))[:max_per_glob]
        for p in matches:
            try:
                rel = p.relative_to(root)
            except ValueError:
                rel = p
            lines.append(str(rel).replace("\\", "/"))
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Step 3.5: ai-assignment.md
# ---------------------------------------------------------------------------

def step_ai_assignment() -> None:
    spec_text = _read_text(SESSION_SET / "spec.md")

    prompt = f"""Author `ai-assignment.md` for the `dabbler-platform-discovery` session set,
per the schema in `docs/ai-led-session-workflow.md` Step 3.5.

This is a 3-session set; produce one block PER session (Session 1, 2, 3),
plus the next-session orchestrator recommendation pointer at the end of
each non-last block. For Session 3 (the last), omit the next-session
recommendation. Output the file in full Markdown form — it will be
written to `docs/session-sets/dabbler-platform-discovery/ai-assignment.md`
verbatim.

For each session block, fill in:
  - **Recommended orchestrator** (engine + model + effort)
  - **Rationale** (2 sentences)
  - **Estimated routed cost** (qualitative: low / moderate / high)
  - **Per-step Routing Decision table** (Step | Action | Routing Decision)
  - **Actuals (filled after the session)** — leave the placeholder fields
    in the template, do NOT fill in values

Key context for your recommendations:
  - This set is read-only discovery on sibling repos. It produces three
    structured Markdown documents (component inventory, RDF schema doc,
    migration map + PDF boundary) plus a smoke-test walkthrough.
  - The work is structured documentation, not novel synthesis — the
    spec author's own recommendation was "Sonnet 4.6 at effort=medium
    across all three sessions; Opus is over-spend." Use that as a
    grounding signal but do not just parrot it — make your own call.
  - The orchestrator running Session 1 is `claude-opus-4-7` at
    effort=high (the human chose this for Session 1; future sessions
    are open).
  - Routing budget caps and task-type defaults already live in
    `ai_router/router-config.yaml`. The orchestrator's job is to pick
    `task_type`, not the model.
  - Mandatory session-verification is cross-provider via `route()` —
    you do not need to call it out per-step unless the routing
    decision differs from the standard `task_type="session-verification"`.

Per-session step lists from the spec are below. Use them to populate
the Routing Decision table — one row per concrete step.

---SPEC EXCERPT START---
{spec_text}
---SPEC EXCERPT END---

Output the complete `ai-assignment.md` content. Do not wrap it in a code
fence — emit it as raw Markdown so it can be written to disk verbatim.
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=45,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    # Persist FIRST per the cp1252-print-crash convention.
    out_path = SESSION_SET / "ai-assignment.md"
    out_path.write_text(result.content, encoding="utf-8")

    log = _log()
    log.log_step(
        session_number=1,
        step_number=1,
        step_key="session-1/ai-assignment",
        description="Author ai-assignment.md via route(task_type=analysis)",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"Wrote {out_path}  ({result.model_name}, ${result.cost_usd:.4f})")


# ---------------------------------------------------------------------------
# Step 4 Feature 1: component inventory
# ---------------------------------------------------------------------------

def step_inventory() -> None:
    # Survey the platform component-bearing libs.
    component_tree = _walk_summary(
        PLATFORM_REPO,
        include_globs=[
            "src/Libs/Dabbler.Components/**/*.razor",
            "src/Libs/Dabbler.Components/**/*.cs",
            "src/Libs/Dabbler.Components.Compat/**/*.razor",
            "src/Libs/Dabbler.Components.Compat/**/*.cs",
            "src/Libs/Dabbler.Reports.Blazor/**/*.razor",
            "src/Libs/Dabbler.Reports.Blazor/**/*.cs",
            "src/Libs/Dabbler.Reports.Export/**/*.cs",
            "src/Libs/Dabbler.Security.Blazor/**/*.razor",
            "src/Libs/Dabbler.Security.Blazor/**/*.cs",
            "src/Libs/Dabbler.Platform.Stubs.Components/**/*.razor",
            "src/Libs/Dabbler.Platform.Stubs.Components/**/*.cs",
            "src/Libs/Dabbler.Testing.Blazor/**/*.cs",
            "Components/**/*.razor",
            "Components/**/*.cs",
            "src/Libs/*/README.md",
        ],
    )

    # Read a small selection of representative files to give the model
    # enough grounding without blowing the token budget.
    excerpts = []
    sample_paths = [
        "src/Libs/Dabbler.Components/FilterPanel/FilterPanel.razor",
        "src/Libs/Dabbler.Components/MasterDetail/DirtyAwareActionRow.razor",
        "src/Libs/Dabbler.Components/MasterDetail/DeclarativeFilterPanel.razor",
        "src/Libs/Dabbler.Reports.Blazor/DabblerReportPage.razor",
        "src/Libs/Dabbler.Reports.Blazor/ReportViewer.razor",
        "src/Libs/Dabbler.Reports.Blazor/ParameterPromptForm.razor",
        "src/Libs/Dabbler.Components/README.md",
        "src/Libs/Dabbler.Reports.Blazor/README.md",
        "src/Libs/README.md",
    ]
    for rel in sample_paths:
        p = PLATFORM_REPO / rel
        if p.is_file():
            text = p.read_text(encoding="utf-8", errors="replace")
            if len(text) > 8000:
                text = text[:8000] + "\n... (truncated)"
            excerpts.append(f"=== {rel} ===\n{text}\n")

    excerpts_str = "\n".join(excerpts)

    prompt = f"""Author `dabbler-platform-components.md` for the
`dabbler-access-harvester` repo. This is Feature 1 of the
`dabbler-platform-discovery` session set spec
(`docs/session-sets/dabbler-platform-discovery/spec.md`).

Goal: a structured, machine-readable inventory of the `dabbler-platform`
repo's public Blazor component surface, grouped by functional category,
that an interpretation AI in this repo (and a future Access-to-Blazor
migration repo) can use to know what migration targets exist.

Inventory scope (snapshot date `{SNAPSHOT_DATE}`, platform commit
`{PLATFORM_SHA[:12]}`):

The platform repo lives at `../dabbler-platform`. The path listing below
is the exhaustive set of component-bearing files in the libraries that
matter for migration targets. The platform also contains an
`AcmeCoffeeOnline` sample app under `src/Apps/` — that is a *consumer*
of the libraries and should NOT be inventoried as a component source.

---PATH LISTING START---
{component_tree}
---PATH LISTING END---

Representative file excerpts (NOT exhaustive — use these to ground
parameter names, but cite paths for every claim and do NOT fabricate
parameters you cannot see):

---EXCERPTS START---
{excerpts_str}
---EXCERPTS END---

Structure the output as:

1. **Currency header** (the exact block below — emit verbatim):

{CURRENCY_HEADER_PLATFORM}

2. **Overview** — one paragraph: what the platform's component surface
   covers and what it does NOT cover (intentional gaps to flag for the
   migration repo).

3. **Categories** — one section per functional category. Recommended
   categories (use the ones that fit; add others if the inventory
   demands):
     - Master/Detail and Filter Panels
     - Form Controls (if any are first-class platform components)
     - Reports (the Dabbler.Reports.Blazor surface)
     - Security UI (the Dabbler.Security.Blazor surface)
     - Stubs and Testing (Dabbler.Platform.Stubs.Components,
       Dabbler.Testing.Blazor) — flag as test/dev support, NOT
       migration targets
     - Top-level `Components/Pages` (if relevant)

   In each category, one entry per public component:
     - Component name and project (e.g., `FilterPanel`
       @ `src/Libs/Dabbler.Components/FilterPanel/FilterPanel.razor`)
     - **Purpose** — one sentence
     - **Required parameters** — name : type, with a one-line description
     - **Optional parameters** — same shape
     - **Stability tag** — `stable`, `in-flight`, or `unknown`
       — base this on signals like: TODO/HACK comments, README mentions
       of "scaffolding" or "WIP", or the file being under
       `Platform.Stubs.*`. If you cannot tell, write `unknown` —
       DO NOT guess.
     - **Common usage context** — one or two scenarios from Access
       app modernization (e.g., "split form on a bound RecordSource")

4. **Per-file gaps and unknowns** — bullet list of files in the path
   listing whose role you couldn't determine from the excerpts. Be
   explicit so a follow-up session can target them.

CRITICAL RULES:
  - **Do not fabricate parameters.** If a `.razor` file is not in the
    excerpts above, do not invent its `[Parameter]` set — list it under
    "Per-file gaps and unknowns" instead.
  - **Cite paths for every claim** so a downstream agent can follow up.
  - **Do not copy more than two lines of code from any single file** —
    the spec forbids copying platform source into this repo (per
    Boundaries section). Cite and paraphrase.
  - Emit raw Markdown only — no surrounding code fence.
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=50,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    out_path = _save("dabbler-platform-components.md", result.content)

    log = _log()
    log.log_step(
        session_number=1,
        step_number=2,
        step_key="session-1/component-inventory",
        description="Route Feature 1 component inventory analysis",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"Wrote {out_path}  ({result.model_name}, ${result.cost_usd:.4f})")


# ---------------------------------------------------------------------------
# Step 4 Feature 2: report.rdf.json schema doc
# ---------------------------------------------------------------------------

def step_schema() -> None:
    rdf_dir = PLATFORM_REPO / "src/Libs/Dabbler.Reports.Core/Rdf"
    rdf_paths = sorted(p for p in rdf_dir.glob("*.cs") if not p.name.startswith("RdfParse")) + \
                sorted(p for p in rdf_dir.glob("RdfParse*.cs"))

    file_listing = "\n".join(str(p.relative_to(PLATFORM_REPO)).replace("\\", "/") for p in rdf_paths)

    rdf_excerpts = []
    for p in rdf_paths:
        text = p.read_text(encoding="utf-8", errors="replace")
        if len(text) > 6000:
            text = text[:6000] + "\n... (truncated)"
        rdf_excerpts.append(f"=== {p.relative_to(PLATFORM_REPO)} ===\n{text}\n")

    # Sample report rdf.json documents
    sample_paths = [
        PLATFORM_REPO / "src/Apps/AcmeCoffeeOnline.Web/Reports/orders-by-customer.rdf.json",
        PLATFORM_REPO / "src/Apps/AcmeCoffeeOnline.Web/Reports/sales-by-product.rdf.json",
        PLATFORM_REPO / "templates/Dabbler.Templates/content/dabbler-app/DabblerApp/Reports/sample-items.rdf.json",
    ]
    sample_blocks = []
    for p in sample_paths:
        if p.is_file():
            text = p.read_text(encoding="utf-8", errors="replace")
            if len(text) > 5000:
                text = text[:5000] + "\n... (truncated)"
            sample_blocks.append(f"=== {p.relative_to(PLATFORM_REPO)} ===\n{text}\n")

    prompt = f"""Author `dabbler-platform-report-rdf-schema.md` for the
`dabbler-access-harvester` repo. This is Feature 2 of the
`dabbler-platform-discovery` session set spec
(`docs/session-sets/dabbler-platform-discovery/spec.md`).

Goal: document the `report.rdf.json` schema at sufficient fidelity that
a downstream AI agent in this repo could *generate* a valid
`report.rdf.json` document from a harvested Access report's `ir/`
representation. The spec calls out that this format is intentionally
higher-level than a syntax tree — describe that level of abstraction
clearly so downstream agents target it correctly.

Snapshot date `{SNAPSHOT_DATE}`, platform commit `{PLATFORM_SHA[:12]}`.

Source-of-truth files (full text of each follows below):

{file_listing}

---RDF MODEL SOURCE START---
{chr(10).join(rdf_excerpts)}
---RDF MODEL SOURCE END---

Sample `.rdf.json` documents from the platform's AcmeCoffeeOnline demo
app and template repo:

---SAMPLES START---
{chr(10).join(sample_blocks)}
---SAMPLES END---

Structure the output as:

1. **Currency header** (the exact block below — emit verbatim):

{CURRENCY_HEADER_PLATFORM}

2. **Overview** — two paragraphs:
   - What `report.rdf.json` represents (the level of abstraction; what
     it is and is NOT — e.g., it is NOT a raw `.rdl`/`.rpt` syntax tree).
   - Why this matters for Access-to-Blazor migration (it is the seam
     between harvested Access report metadata and platform rendering).

3. **Top-level structure** — Outline the document's root shape and the
   relationship between sections (metadata, parameters, data sources,
   bands, groups, sorts, layout). Use the C# `ReportDefinition` /
   `ReportMetadata` / band/group/sort/field/element definitions as the
   structural authority. Note which fields are required vs. optional,
   citing the JSON-binding attribute or default value where visible.

4. **Per-section reference** — one subsection per top-level concept
   (Metadata, Parameters, Data Sources, Bands, Groups, Sorts, Fields,
   Elements, Expressions, Styles, Running Totals — whichever the
   source model defines). For each:
     - Schema shape (field-by-field, with type and required/optional)
     - Semantic meaning (one sentence per non-obvious field)
     - **At least one example snippet from a sample document above**,
       cited by source path
     - Notes on validation rules from `RdfValidator.cs` /
       `RdfValidationError.cs` when relevant

5. **Worked example walkthrough** — pick ONE sample
   (e.g., `orders-by-customer.rdf.json`) and walk the reader through
   how its top-level shape maps to a hypothetical Access report
   structure. Two or three paragraphs maximum.

6. **Generation contract for AI consumers** — a short bulleted list of
   the rules an AI agent must follow to emit a valid `report.rdf.json`
   from a harvested Access report. Examples:
     - "Always emit `metadata.title` (required)."
     - "Bands must be one of `<list from BandType>` — anything else
       fails validation."
     - Anchor each rule to the source file that enforces it.

CRITICAL RULES:
  - **Do not paraphrase the C# model into Markdown loosely** — be
    schema-precise about types, nullability, required-ness, and
    JSON property names. If a field's JSON property name differs from
    its C# property name (look for `[JsonPropertyName]` or
    `JsonSerializerOptions.PropertyNamingPolicy`), say so explicitly.
  - **Anchor every claim to a source file path** (relative to
    `../dabbler-platform`).
  - **Do not copy more than ~5 contiguous lines of C# code** — cite
    and describe instead.
  - Sample `.rdf.json` snippets are fine to quote in full (they are
    documentation-class fixtures, not source code).
  - Emit raw Markdown only — no surrounding code fence.
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=55,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    out_path = _save("dabbler-platform-report-rdf-schema.md", result.content)

    log = _log()
    log.log_step(
        session_number=1,
        step_number=3,
        step_key="session-1/rdf-schema-doc",
        description="Route Feature 2 RDF schema documentation analysis",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"Wrote {out_path}  ({result.model_name}, ${result.cost_usd:.4f})")


# ---------------------------------------------------------------------------
# Step 6: cross-provider session verification
# ---------------------------------------------------------------------------

def step_verify(round_number: int = 1) -> None:
    spec_text = _read_text(SESSION_SET / "spec.md")
    ai_assignment = _read_text(SESSION_SET / "ai-assignment.md")
    inventory = _read_text(OUT_DIR / "dabbler-platform-components.md")
    schema = _read_text(OUT_DIR / "dabbler-platform-report-rdf-schema.md")

    prompt = f"""You are the independent cross-provider verifier for Session 1
of the `dabbler-platform-discovery` session set in the
`dabbler-access-harvester` repo.

Your response MUST begin with a detailed prose analysis covering each
of the concerns below BEFORE the trailing JSON block. Do not skip the
analysis. Be specific about file paths and section names when you
identify issues.

# Session goal

Session 1 of 3 produced two structured Markdown documents inside the
harvester repo's `docs/cross-repo/` directory:

1. `dabbler-platform-components.md` — inventory of public Blazor
   components in `../dabbler-platform` (Feature 1 of the spec).
2. `dabbler-platform-report-rdf-schema.md` — documentation of the
   `report.rdf.json` schema produced by
   `../dabbler-platform/src/Libs/Dabbler.Reports.Core/Rdf/`
   (Feature 2 of the spec).

# Files you should evaluate

## 1. Session set spec (the contract)

---SPEC START---
{spec_text}
---SPEC END---

## 2. ai-assignment.md authored at Step 3.5

---AI ASSIGNMENT START---
{ai_assignment}
---AI ASSIGNMENT END---

## 3. Feature 1 deliverable: dabbler-platform-components.md

---INVENTORY START---
{inventory}
---INVENTORY END---

## 4. Feature 2 deliverable: dabbler-platform-report-rdf-schema.md

---SCHEMA DOC START---
{schema}
---SCHEMA DOC END---

# Files NOT in scope for this verification (disclosure)

Per the workflow Convention "Send Related Files To The Cross-Provider
Verifier Or Disclaim What Is Out Of Scope": the following exist in the
repo but are intentionally NOT modified by this session.

- The harvester's parser, resolver, extractor, and enrichment code
  under `AccessHarvester.*` — Session 1 of this set is read-only on
  sibling repos and does not touch harvester code.
- `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`,
  `docs/ai-led-session-workflow.md` — workflow assets, not touched by
  this session.
- The sibling repos themselves (`../dabbler-platform`, `../dabbler-pdf`)
  are explicitly OUT OF SCOPE for editing per the spec's Boundaries
  section. The deliverables CITE paths in those repos but do not
  modify them. You should treat any complaint that "the platform
  repo should also be updated" as out of scope for this verification.
- `session-1-driver.py` in the session-set folder is the orchestrator
  script that ran the route() calls — it is part of the session but
  not a Feature deliverable.
- Sessions 2 and 3 deliverables (migration map, PDF boundary doc,
  smoke-test, change log) are explicitly deferred to later sessions
  per the spec's Session Plan.

# What to check

1. **Spec compliance.** Do the two deliverables produce what Session
   1 of the spec asked for? Specifically Features 1 and 2 with the
   required currency-header block.
2. **Currency header presence.** Every output file under
   `docs/cross-repo/` must carry the snapshot/commit/regenerate
   header block (Feature 5 in the spec).
3. **Boundary discipline.** No copying of platform source code into
   the harvester repo. Citations are required for every claim of a
   parameter, schema field, or behavior. Flag anywhere the inventory
   appears to invent a `[Parameter]` set or fabricate a schema field
   that isn't grounded in the cited file.
4. **Schema doc fidelity.** The RDF schema doc says it should give
   downstream AI enough fidelity to generate a valid `report.rdf.json`.
   Is the field-by-field reference comprehensive (matching the C#
   model files cited)? Are required/optional and JSON property names
   accurate? Are the validation rules (`RdfValidator.cs`) covered?
5. **Inventory completeness.** The inventory is grouped by category
   with a `stable | in-flight | unknown` stability tag per component.
   Are stability tags reasonable, or claimed without evidence? Are
   `Platform.Stubs.*` components correctly flagged as
   test/dev support and NOT migration targets?
6. **ai-assignment.md schema.** Does it match the schema from
   `docs/ai-led-session-workflow.md` Step 3.5 — one block per session
   with Recommended orchestrator, Rationale, Estimated routed cost,
   Routing Decision table, and Actuals placeholder? Sessions 1 and 2
   should also carry a Next-Session recommendation; Session 3 (last)
   should not.

# Required response format

After your prose analysis, output a JSON block in this EXACT format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or section name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
"""

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=1,
    )

    log = _log()
    log.save_session_review(
        session_number=1,
        review_text=result.content,
        round_number=round_number,
    )
    log.log_step(
        session_number=1,
        step_number=5 + round_number,
        step_key=f"session-1/verify-round-{round_number}",
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
    print(f"Verification round {round_number} saved.  ({result.model_name}, ${result.cost_usd:.4f})")
    print(f"--- response (first 400 chars) ---")
    print(result.content[:400])


# ---------------------------------------------------------------------------
# Step 8: next-session orchestrator recommendation
# ---------------------------------------------------------------------------

def step_next_orc() -> None:
    spec_text = _read_text(SESSION_SET / "spec.md")
    ai_assignment = _read_text(SESSION_SET / "ai-assignment.md")
    # Read the session-1 review for context on actuals.
    review_path = SESSION_SET / "session-reviews" / "session-001.md"
    review_text = _read_text(review_path) if review_path.is_file() else "(no review yet)"

    prompt = f"""Recommend the orchestrator (engine + model + effort) for
Session 2 of `dabbler-platform-discovery`, plus a one-sentence
rationale.

Session 1 was run by `claude-opus-4-7` at effort=high. Session 1's
deliverables were structured documentation (component inventory + RDF
schema reference), routed via `task_type=analysis` at complexity 50-55.
The session-set spec's own author recommended Sonnet 4.6 at
effort=medium across all three sessions; the orchestrator running
Session 1 was the human's override, not the routed recommendation.

Session 2 of 3 covers:
  - Feature 3: cross-reference Access construct kinds against the
    platform component inventory (analysis task, ~complexity 55).
  - Feature 4: short `dabbler-pdf` boundary doc (documentation task,
    ~complexity 30).
  - Lands two more output Markdown files under `docs/cross-repo/`.

Spec excerpt for grounding:

---SPEC START---
{spec_text}
---SPEC END---

ai-assignment.md (already authored at Step 3.5; your recommendation
will be appended as the next-session pointer at the end of the
Session 1 block):

---AI ASSIGNMENT START---
{ai_assignment}
---AI ASSIGNMENT END---

Session 1 verifier review for actuals context:

---REVIEW START---
{review_text}
---REVIEW END---

Output EXACTLY this format, no preamble, no surrounding fence:

ENGINE: <claude-code | codex | gemini-cli>
MODEL: <model id, e.g., claude-sonnet-4-6>
EFFORT: <low | medium | high | max>
RATIONALE: <one sentence>
"""

    result = route(
        content=prompt,
        task_type="analysis",
        complexity_hint=25,
        session_set=str(SESSION_SET),
        session_number=2,
    )

    out_path = SESSION_SET / "next-orc-recommendation-session-2.txt"
    out_path.write_text(result.content, encoding="utf-8")

    log = _log()
    log.log_step(
        session_number=1,
        step_number=9,
        step_key="session-1/next-orc-recommendation",
        description="Route next-orchestrator recommendation for Session 2",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print(f"Wrote {out_path}  ({result.model_name}, ${result.cost_usd:.4f})")
    print("--- recommendation ---")
    print(result.content)


# ---------------------------------------------------------------------------
# Step 8: cost report
# ---------------------------------------------------------------------------

def step_cost_report() -> None:
    print_cost_report(SESSION_SET)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    step = sys.argv[1]
    if step == "ai-assignment":
        step_ai_assignment()
    elif step == "inventory":
        step_inventory()
    elif step == "schema":
        step_schema()
    elif step == "verify":
        round_no = int(sys.argv[2]) if len(sys.argv) >= 3 else 1
        step_verify(round_number=round_no)
    elif step == "next-orc":
        step_next_orc()
    elif step == "cost-report":
        step_cost_report()
    else:
        print(f"Unknown step: {step}")
        print(__doc__)
        sys.exit(2)
