"""Route the Lowdefy-as-IR evaluation to GPT-5.4 and Gemini Pro in parallel.

Persist-then-display per the Lessons-Learned convention.

Usage:
    .venv/Scripts/python.exe tools/evaluate_lowdefy_as_ir.py <target_model>

`target_model` is one of `gpt-5-4` or `gemini-pro`. The script is meant to be
called twice in parallel (e.g. via two backgrounded bash invocations), once
per model, so the two cross-provider responses run concurrently.
"""

from __future__ import annotations

import sys
from pathlib import Path

from ai_router import query


REPO = Path(__file__).resolve().parent.parent
OUT_DIR = REPO / "docs" / "lowdefy-evaluation"


def read_file(path: Path) -> str:
    raw = path.read_bytes()
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp1252", errors="replace")


def build_prompt() -> str:
    brief = read_file(REPO / "docs" / "evaluate-lowdefy-as-ir.md")
    harvester_ir = read_file(REPO / "docs" / "ir-schema-forms-reports.md")
    workflow_template_example = read_file(
        REPO / "docs" / "session-sets" / "workflow-package-pilot" / "packages" / "add-an-order.md"
    )
    project_guidance = (
        "Key load-bearing principles from `docs/planning/project-guidance.md`:\n\n"
        "1. **Project scope and audience** — the harvester produces structured\n"
        "   metadata at sufficient fidelity that a downstream AI can interpret\n"
        "   and document the original Access app's behavior. **AI interpretation\n"
        "   is explicitly out of scope for this repo** — this codebase is\n"
        "   mechanical extraction and deterministic enrichment.\n\n"
        "2. **Output architecture** — three-tier output: `raw/` (Access-native),\n"
        "   `kb/` (clean parsed JSON), `ir/` (`kb/` plus mechanical enrichments).\n"
        "   **Interpretive slots remain null at the `ir/` tier** — fields like\n"
        "   `purpose`, `notes`, `intent` are reserved for the AI consumer to fill\n"
        "   downstream. The harvester does not guess at semantics.\n\n"
        "3. **Lean by default** — don't add features, abstractions, or safety\n"
        "   nets beyond what the task in front of you requires. Don't introduce\n"
        "   an interface for a single implementation. Don't write a fallback\n"
        "   heuristic when the only known input format is the one the test\n"
        "   fixtures use.\n\n"
        "4. **Output JSON conventions** — provenance fields use a `{ source, ... }`\n"
        "   shape (e.g., `Label: { source: 'unresolved' }`). Numeric units are\n"
        "   explicit in field names (e.g., `GeometryIn`). Only emit fields Access\n"
        "   actually wrote — do not synthesize zeros or default values.\n\n"
        "5. **VBA parser strategy** — output is **deterministic** given the same\n"
        "   input. Unresolvable constructs are explicit (`{ source: 'unresolved',\n"
        "   reason: '<string>' }`), not silent. Real grammar over regex.\n"
    )

    initial_position = """
## Orchestrator's initial position (you may disagree)

I am Claude Opus 4.7 acting as orchestrator for the dabbler-access-harvester
repo. Before sending this brief to you and to a peer model from a different
provider, I formed an initial position. Treat it as something to argue with,
not a constraint.

### Context the orchestrator surfaces about the brief

The brief was placed in `docs/evaluate-lowdefy-as-ir.md` in the harvester
repo, but the IR it describes — "Custom IR YAML between Access-shaped YAML
and Blazor codegen, worked on for ~2 months" — **does not exist in the
harvester repo**.

What actually exists in this repo:

- **Harvester `ir/`**: Access-shaped metadata IR (`FormMetadata`,
  `ReportMetadata` C# records). Faithfully mirrors Access semantics. The
  project-guidance.md is explicit: "Interpretive slots remain null at the
  `ir/` tier. Fields like `purpose`, `notes`, `intent`, etc. are reserved
  for the AI consumer to fill downstream. The harvester does not guess at
  semantics."
- **Workflow packages**: markdown documents in
  `docs/session-sets/workflow-package-pilot/packages/`. Seven-section
  template (Workflow summary, Actors/entities, Business rules + evidence
  + confidence tags, Behavioral invariants → characterization tests,
  Source operationalization reference, Component/role mapping hints,
  Open questions / ambiguity log). This is the AI-readable layer that
  captures business invariants. It is NOT YAML/JSON; NOT codegen-shaped.
- **Blazor pilot at `output-tests/blazor-add-an-order/`**: a working
  Blazor Server + MudBlazor app that consumes the workflow packages as
  *guidance for hand-authoring* C# domain services, controllers, and
  pages. The Option D resolution from `workflow-package-pilot` is that
  the characterization-test fixtures stay unmodified and the Blazor
  domain services author parallel logic honoring the same invariants —
  no shared workflow code between tests and host. No IR-driven codegen.

The "Custom IR YAML + codegen" the brief describes plausibly lives in a
sibling repo (`../AppDefToBlazor`, `../access-to-blazor`, or
`../AccessBlazorPlanning`). I have not read those repos. If your evaluation
genuinely depends on seeing that IR, please name the specific files you
would need and we will run a follow-up.

### Initial verdicts on the eight dimensions in the brief

Treating "current IR" as "the workflow-package format plus the implied
target IR for a future Blazor codegen layer":

1. **Compactness for AI authoring** — Lowdefy primitives would likely
   *increase* AI surface area for a bounded Access→Blazor migration. A
   domain DSL with first-class `MasterDetailListPage`, `ModalDetailForm`,
   `ContinuousForm`, `ReportWithSubreports`, `Switchboard`,
   `ReportParameterForm` is more compact than Lowdefy-style generic
   block trees. Tentative: divergent-but-justified.
2. **Reviewability** — Lowdefy-style flat schemas are denser and easier
   to scan. The current workflow-package format is prose-heavy; a YAML
   IR would be more reviewable but require its own tooling. Tentative:
   Lowdefy wins on this dimension.
3. **Composability / `_ref`** — Access has no cross-file reuse mechanism.
   Whatever IR replaces this, `_ref`-style include-with-substitution
   is straightforwardly worth borrowing. Tentative: adopt.
4. **Schema validation surface** — current workflow packages are
   markdown; no formal schema validation. A JSON-Schema-validated IR is
   strictly better here. Tentative: adopt.
5. **Separation of concerns (Block/Request/Operator/Action/Event)** —
   the Block/Request split is the strongest single idea in Lowdefy.
   Access conflates them; downstream IR should not. Operators-as-tree
   and Events-as-ordered-Actions also worth borrowing. Tentative: adopt
   with C#-shaped operators (not JS).
6. **Escape hatches** — Lowdefy's `_js` operator does not map to a C#
   target. Better escape hatch: "name a partial-class method." Tentative:
   replace, not borrow.
7. **Code-gen ergonomics** — domain-DSL with first-class types pushes
   complexity into the schema rather than the emitter. Lowdefy's generic
   primitives push complexity into the emitter (it has to figure out
   "this Block tree is actually a master-detail page"). For a bounded
   target this is the wrong tradeoff. Tentative: domain DSL wins.
8. **Mapping fidelity from Access (master/detail, modal, continuous,
   subreports, parameter forms, switchboards, data macros, transactional
   `Copy from Prior FY`)** — domain DSL with first-class types maps these
   1:1. Lowdefy-style primitives require "discover the pattern" at
   codegen time. Domain DSL wins.

### The architectural fork I most want second opinions on

Three options for the Blazor-target IR layer:

- **Option A — Flat high-level domain DSL.** First-class types per
  Access pattern (`MasterDetailListPage`, `ModalDetailForm`, etc.).
  Compact for AI authoring. Schema validates the small surface. Codegen
  is a straight emit. Cost: less flexibility for cases outside scope;
  every new pattern requires a schema change.
- **Option B — Two-stage IR.** AI authors a high-level domain DSL (same
  as Option A); codegen *desugars* into a Lowdefy-style primitive IR
  (Blocks/Requests/Operators/Actions/Events) before emitting Blazor.
  AI surface stays small; codegen stays regular; new patterns can be
  added either by extending the high-level DSL or by composing
  primitives manually for one-off pages. Cost: two schemas to maintain,
  but the low-level is internal.
- **Option C — Flat Lowdefy-style primitive IR.** AI authors primitives
  directly. Maximum flexibility; biggest AI surface. Probably the
  wrong tradeoff for a bounded Access→Blazor migration, but the brief's
  seed thoughts deserve fair evaluation.

My tentative recommendation is **Option B** (two-stage), but I am
ambivalent. The maintenance cost of two schemas could be higher than
the flexibility benefit, especially early when the pattern set is
small.

### Borrowing-vs-rejecting splits I would propose to the human

Adopt from Lowdefy:
- Block/Request separation as a conceptual split (even if not the exact
  Lowdefy primitive names).
- Operator catalog as composable expression tree, with C#-shaped
  operators (`Nz`, `IIf`, `DLookup`, `DCount`, `DSum`, `Format`, plus
  standard arithmetic/comparison/logical).
- Events as ordered Actions with `try`/`catch` and debounce semantics.
- `_ref`-style cross-file reuse.
- Schema validation on every property; reject unknown properties.
- Explicit `page` vs `modal` vs `drawer` distinction at the page type
  level.

Reject from Lowdefy:
- `_js` operator (use partial-class C# methods as the escape hatch).
- Generic connector abstraction (EF Core against SQL Server / Postgres
  is fixed; "named query" / "named command" is enough).
- Generic Block composition for Reports (use first-class
  `Report`/`Subreport`/`GroupLevel` types).
- The JS-shaped operator names where C# idioms differ.

### Specific things you can disagree about

- Whether Option B's two-schema maintenance cost actually pays off, or
  whether a flat domain DSL (Option A) with explicit "fall through to
  hand-written Blazor for one-off pages" is the right pragmatic call.
- Whether the brief's "50 lines vs 500" pitch for Lowdefy genuinely
  applies to a bounded Access→Blazor migration where the domain types
  can be tighter than Lowdefy's generic Blocks.
- Whether reports should be first-class IR types at all, or expressed
  as a separate domain (since Blazor + a PDF renderer is materially
  different from Blazor + interactive form pages).
- Whether the workflow-package format (prose-heavy markdown) should be
  retained as the AI-authored layer with codegen reading both packages
  AND IR YAML, or whether the packages should be **replaced** by the
  IR YAML once the IR matures.
"""

    return f"""You are providing a cross-provider second opinion on an
architecture decision for a multi-repo Access-to-Blazor migration project.

Your peer model from a different provider is receiving the same prompt; the
orchestrator will synthesize both responses. Treat my initial position as
something to argue with. Push back hard on anything that looks wrong.

The original brief authored by the human follows verbatim. After the brief,
I include the orchestrator's framing of what context you actually have,
plus my initial position on the eight dimensions and the architectural
fork. After all of that, please produce the six-section deliverable the
brief asks for.

=========================================================================
ORIGINAL BRIEF (verbatim)
=========================================================================

{brief}

=========================================================================
HARVESTER IR SHAPE (what `ir/` actually looks like in this repo)
=========================================================================

This is the harvester's Access-shaped IR — `FormMetadata`/`ReportMetadata`
C# records that faithfully preserve Access semantics. Not the Blazor codegen
target the brief asks about. Included so you understand the upstream layer.

{harvester_ir}

=========================================================================
PROJECT GUIDANCE (the repo's principles for the upstream Access-shaped IR)
=========================================================================

Note especially: Project Scope And Audience, Output Architecture, Lean By
Default. The harvester's `ir/` is explicitly NOT the Blazor codegen target
— interpretive slots remain null at this tier.

{project_guidance}

=========================================================================
WORKFLOW PACKAGE EXAMPLE (de facto AI-readable layer today)
=========================================================================

This is the seven-section markdown format that has been the AI-authored
layer in practice for the Blazor pilot. It captures business invariants in
prose with structured sections; it is NOT YAML/JSON and is NOT directly
consumed by codegen. The Blazor pilot consumed this as guidance for
hand-authoring C#. Whether this format should be retained, supplemented by
a YAML IR, or replaced by a YAML IR is one of the open questions.

{workflow_template_example}

=========================================================================
{initial_position}

=========================================================================
DELIVERABLE
=========================================================================

Produce the six-section markdown document the brief asks for, but adapt
section 1 to "Context as provided" since you cannot read the codebase
directly — synthesize from what is included above and flag uncertainty.

Specifically:

1. **Context as provided** — your one-or-two-paragraph synthesis of what
   exists in this repo, what does not, and what the question is. Flag
   uncertainty about parts you would need to read in sibling repos.
2. **Lowdefy-alignment scorecard** — the eight dimensions, each with a
   short verdict (strong / aligned / weak / divergent-but-justified) and
   one or two sentences of reasoning. Disagree with my tentative verdicts
   where you see fit; cite the specific reason.
3. **Recommendations to adopt** — Lowdefy patterns worth importing in
   priority order. Each with a concrete sketch of what the change would
   look like.
4. **Recommendations to reject** — Lowdefy patterns that would be wrong
   here, with reasoning.
5. **Open questions** — places where the right answer depends on a
   decision the human has not yet made. Frame each decision explicitly.
   At minimum, address: (a) Option A vs Option B vs Option C above, with
   your recommended choice; (b) whether reports should be first-class IR
   types or a separate domain; (c) whether workflow packages should be
   retained, supplemented, or replaced once the IR matures.
6. **Suggested next steps** — a short ordered list of small, reversible
   changes the human could try first, before committing to a larger
   refactor.

If you are uncertain because you cannot read a sibling repo, name what
files you would need rather than speculating.
"""


def main() -> int:
    if len(sys.argv) != 2:
        print("Usage: evaluate_lowdefy_as_ir.py <gpt-5-4|gemini-pro>")
        return 2

    model = sys.argv[1]
    if model not in {"gpt-5-4", "gemini-pro"}:
        print(f"Unsupported model: {model}")
        return 2

    OUT_DIR.mkdir(parents=True, exist_ok=True)

    prompt = build_prompt()
    print(f"[{model}] prompt size: {len(prompt)} chars (approx {len(prompt) // 4} tokens)")

    result = query(
        model=model,
        content=prompt,
        task_type="architecture",
    )

    out_path = OUT_DIR / f"evaluation-{model}.md"
    out_path.write_text(result.content, encoding="utf-8")

    summary_path = OUT_DIR / f"evaluation-{model}.cost.txt"
    summary_path.write_text(
        f"model={result.model_name}\n"
        f"input_tokens={result.input_tokens}\n"
        f"output_tokens={result.output_tokens}\n"
        f"cost_usd={result.cost_usd:.4f}\n",
        encoding="utf-8",
    )

    print(f"[{model}] saved to {out_path}")
    print(f"[{model}] tokens in={result.input_tokens} out={result.output_tokens} cost=${result.cost_usd:.4f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
