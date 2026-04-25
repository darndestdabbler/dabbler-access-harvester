# Verification Round 1

# ISSUES_FOUND

18/18 stubs exist; no extra slugs. Session-plan ranges align with tree ranges. Defects are in fidelity to the canonical tree, prerequisite-path formatting, routing-note calibration, and lean-ness.

**CRITICAL** Issue → Tree-as-source requirement is not applied; the stubs do not reference their canonical tree rows verbatim.  
**Location** → All 18 `docs/session-sets/*/spec.md`.

`Slug` via folder name and `Sessions (rough)` via session headings are generally aligned, but the required verbatim references for `Plan-phase coverage`, `Prereqs`, and `Outputs` are missing/paraphrased in every stub:

| Row | Stub | Non-verbatim tree fields |
|---|---|---|
| 1 | `parser-foundations` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 2 | `vba-spec-study` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 3 | `vba-lexical-and-expression-grammar` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 4 | `vba-statement-and-module-grammar` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 5 | `vba-grammar-validation` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 6 | `parser-infrastructure` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 7 | `resolver-declarations-and-scopes` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 8 | `resolver-references-and-with-blocks` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 9 | `resolver-default-members-and-inference` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 10 | `access-metadata-extraction` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 11 | `access-symbol-table-and-types` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 12 | `jet-sql-grammar-and-parser` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 13 | `jet-sql-resolution-and-dynamic` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 14 | `enrichment-queries-and-forms` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 15 | `enrichment-events-and-validation` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 16 | `enrichment-functions-navigation-review` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 17 | `schema-finalization-and-docs` | `Plan-phase coverage`, `Prereqs`, `Outputs` |
| 18 | `integration-testing-and-acceptance` | `Plan-phase coverage`, `Prereqs`, `Outputs` |

Notable concrete drift beyond paraphrase:
- Row 1 adds non-tree output details like `docs/schema/v0-vs-tiers.md` and a test-project skeleton.
- Row 9 introduces `AccessHarvester.Vba.Resolver/CrossReference/`, which is not in the canonical outputs.
- Row 17 adds ADR/named-example detail that is outside the canonical output string.

**Fix** → In each stub, add a `Canonical tree row` block copied exactly from the matching `session-set-tree.md` row, including verbatim `Slug`, `Plan-phase coverage`, `Prereqs`, `Sessions (rough)`, and `Outputs`. Keep any explanatory prose separate and clearly non-authoritative.

---

**MAJOR** Issue → `Prerequisite:` frontmatter does not use exact grep-able paths, violating the Session 3 stub standard.  
**Location** → Every `> **Prerequisite:**` line across the 18 stubs.

Examples:
- `parser-foundations` uses prose about what `accessexportertemp1-import` provides.
- `vba-spec-study` uses prose `None — runs in parallel...`.
- Internal-child stubs use names/ordinals like ``parser-foundations` (#1)` instead of exact paths.

**Fix** → Keep the tree prereq string verbatim in the canonical row block, and separately change frontmatter `Prerequisite:` to exact repo paths a future orchestrator can grep, e.g. `docs/session-sets/parser-foundations/`, `docs/session-sets/vba-spec-study/`, `docs/session-sets/accessexportertemp1-import/`. Use `None` when there is no prerequisite path.

---

**MAJOR** Issue → Routing notes are not standardized to the required routing context from adopt-now rows `#10` and `#13`.  
**Location** → `## Routing notes` across all stubs.

Specific defects:
- `architecture` is used across the set, but the notes do not consistently label it as **uncalibrated against this repo's actuals**.
- `docs/session-sets/vba-spec-study/spec.md` says it is the “first child set in the tree to invoke `architecture`”, but `docs/session-sets/parser-foundations/spec.md` Session 1 already routes `architecture`.
- `schema-validation` notes are inconsistent:
  - `parser-foundations` and `vba-spec-study` add provisional `code-review` fallbacks.
  - Most other stubs omit the inlined “mechanical-correspondence → Sonnet” context entirely.

**Fix** → Standardize routing notes everywhere:
- `architecture` → Opus/xhigh, **uncalibrated against this repo's actuals**, auto-verified; first actual use logs actuals carefully.
- `schema-validation` → mechanical-correspondence, Sonnet-pinned per adopt-now `#10`.
- Remove speculative fallback task types unless the canonical playbook explicitly includes them.
- Remove the incorrect “first child set” claim from `vba-spec-study` or move the first-use note to `parser-foundations`.

---

**MINOR** Issue → Several stubs are not lean; they pre-specify implementation details instead of only the work shape.  
**Location** → Examples:
- `parser-foundations` Feature 1 hardcodes diff/perf/no-parser modes.
- `parser-infrastructure` hardcodes lossy-parse behavior.
- `resolver-references-and-with-blocks` encodes a detailed multi-step resolution algorithm.
- `access-metadata-extraction` hardcodes field-shape conventions and geometry/provenance details.
- `integration-testing-and-acceptance` hardcodes concrete coverage/performance thresholds in stub text.

**Fix** → Trim these sections to deliverable shape, dependency context, and the one or two decision surfaces per synthesis session. Defer algorithm details, exact thresholds, and field-level schema specifics to “Session 1 of this set must elaborate the step lists before running them.”

---

# Verification Round 2

# ISSUES_FOUND

**MAJOR** Issue → Round-1 routing-note standardization fix is incomplete. The `## Canonical tree row` blocks are present for all 18 stubs and the `> **Prerequisite:**` frontmatter lines are now grep-able/correct, but four routing notes still miss required standard text: two `schema-validation` notes omit the Sonnet-pinned retrospective `#10` clause, and two conditional notes that mention `architecture` omit the required **uncalibrated against this repo's actuals** label.  
**Location** →  
- `docs/session-sets/vba-lexical-and-expression-grammar/spec.md` → `## Routing notes` → “Mechanical tightening (Session 3)”  
- `docs/session-sets/vba-statement-and-module-grammar/spec.md` → `## Routing notes` → “Mechanical tightening (Session 3)”  
- `docs/session-sets/resolver-references-and-with-blocks/spec.md` → `## Routing notes` → “ResolvedModule shape (Session 3)”  
- `docs/session-sets/resolver-default-members-and-inference/spec.md` → `## Routing notes` → “Cross-reference index (Session 3)”  
**Fix** → Standardize those notes to match the adopted playbook language: every `schema-validation` note should explicitly say mechanical-correspondence and Sonnet-pinned per retrospective `#10`; every note that mentions `architecture`, even conditionally, should carry the **uncalibrated against this repo's actuals** label.

**MAJOR** Issue → A new human-blocking Open Question was introduced even though the canonical tree says only the two parent Open Questions may carry forward, and both defaults are already applied. This reopens decision-making at stub level.  
**Location** → `docs/session-sets/integration-testing-and-acceptance/spec.md` → `## Open questions for the human`  
**Fix** → Remove the sample-DB sourcing item from `## Open questions for the human` and rewrite it as default execution guidance/assumptions. If repo-policy approval is truly required, amend the canonical tree first instead of creating a new stub-level fork.

---

# Verification Round 3

# VERIFIED

- `vba-lexical-and-expression-grammar` → `Mechanical tightening (Session 3)` now includes mechanical-correspondence and Sonnet-pinned per retrospective adopt-now `#10`.
- `vba-statement-and-module-grammar` → `Mechanical tightening (Session 3)` now includes mechanical-correspondence and Sonnet-pinned per retrospective adopt-now `#10`.
- `resolver-references-and-with-blocks` → `ResolvedModule shape (Session 3)` now includes the required **uncalibrated against this repo's actuals** label for conditional `architecture` routing.
- `resolver-default-members-and-inference` → `Cross-reference index (Session 3)` now includes the required **uncalibrated against this repo's actuals** label for conditional `architecture` routing.
- `integration-testing-and-acceptance` no longer carries a human-blocking stub-level open question; the sample-DB item has been rewritten as default execution guidance.
