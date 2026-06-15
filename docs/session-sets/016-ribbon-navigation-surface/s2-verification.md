# Session 2 — Cross-provider verification (routed) + closeout-grade

**Verifier:** gpt-5-4 (`route(task_type="session-verification")`), cross-provider to the
Opus 4.8 orchestrator. Prose-first + trailing JSON. **Final verdict: VERIFIED (R1).**
Closeout-grade packet: embedded the Session-1 surface (`RibbonControl`/`RibbonStructure`
+ `RibbonStructureBuilder`) S2 consumes per the cross-session-evidence convention, the
full S2 derivation code, schema delta, the design consensus, and the green-validation
evidence (952 hermetic, HHC golden byte-reproduced via fresh COM, Northwind local).
The set-wide in-IDE devil's-advocate prompt (user-run, GPT + Gemini, primary per the
015/011 precedent) is at `s2-set-wide-evaluation-prompt.md`. Design consensus is at
`s2-ribbon-entry-point-consensus.md`. Raw round output reproduced verbatim below
(never edited).

## Round 1 — VERIFIED (no issues)

gpt-5-4 addressed all six concerns in prose and returned `{"verdict":"VERIFIED","issues":[]}`.
Key confirmations:

- **Resolution & determinism (1):** name-only case-insensitive lookup is the right
  non-interpretive join key (ribbon XML carries only a callback *name*). The
  Resolved/Unresolved/Ambiguous split is correct; output is deterministic in both
  order (driven by S1's deterministic tree: onLoad → tabs/groups/controls DFS →
  contextual sets) and resolution (absence / cardinality-1 / cardinality->1, never a
  serialized chosen candidate — no order-sensitive tie-break leak). The same-module
  de-dupe is appropriate defensive behavior. Caveat (by-design, not a defect): this is
  not a model of Access runtime *bindability* (signature/visibility) — consistent with
  DW-13's faithful-mechanical discipline.
- **Explicit-unresolved (2):** every non-empty onLoad/onAction emits a row *before*
  resolution; a miss becomes `Unresolved` with the verbatim `CallbackName` retained and
  `ResolvedProcedure=null`; ambiguity is emitted with no guessed module.
- **Golden neutrality (3):** "I do not see a path in this code for `RibbonEntryPoints: []`
  to appear in a truly non-ribbon manifest." The fresh COM byte-for-byte HHC golden
  reproduction is "strong confirmation that neutrality is real, not just inferred."
- **Tree-walk completeness (4):** complete for every callback site the S1 model
  represents (root onLoad, controls under all tabs/groups, recursive children, contextual
  tab sets). DFS for nested controls is explicit and tested.
- **Model & schema (5):** the dedicated `RibbonEntryPoints` collection is "the right
  design" — reusing `EntryPoints` "would blur a documented semantic distinction." The
  schema change is "appropriately additive" (optional, not in `required`, existing
  manifests stay valid, no HHC churn); `ProcedureReference` reuse is appropriate. The
  spec-wording reconciliation is acceptable.
- **Scope discipline (6):** stays within DW-13 — no dynamic-callback evaluation,
  signature/visibility/module-kind filtering, or heuristic module selection. "Does not
  overreach into a new DW."

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```

## Round 2 — VERIFIED (post-close set-wide-review follow-up)

After the human ran the set-wide adversarial review (`s2-set-wide-evaluation-*.md`),
the resolution-bindability finding (F1/A1, Major, both reviewers) + the A5 empty-table
guard + the interpretation-guide staleness (F3/A7) were fixed (triage:
`s2-set-wide-evaluation-disposition.md`). The bindability filter — a real semantic
change — was re-routed to gpt-5-4 (cross-provider) and returned VERIFIED, no issues:

- **Bindability correctness:** limiting candidates to Public/modifier-less/Global
  procedures in standard modules is "the right filter"; treating `Unspecified` as
  bindable is correct (VBA default); excluding Private/Friend + class/form/report
  code-behind is correct.
- **Faithful-mechanical:** "stays on the faithful/mechanical side … removing
  candidates Access could never bind … a better surface, not over-interpretation."
  Reclassifying a non-bindable-only collision from `Ambiguous` → `Unresolved` is
  "the more faithful result"; the old Ambiguous "was overstating ambiguity by
  counting impossible targets."
- **Determinism:** "a pure eligibility predicate … remains deterministic and
  order-independent."
- **A5 guard** correct and side-effect-free; **docs** now "match the implementation
  much better" with no over/under-claim.

```json
{"verdict":"VERIFIED","issues":[]}
```

Re-validation: 956 Pipeline.Tests (4 new bindability tests); HHC golden byte-level
re-reproduced via fresh COM run; Northwind `cmdAbout_OnAction` still resolves to
`modRibbonCallback` under the filter. Findings A6 refuted (already guarded); F2/A8/A9
deferred as DW-22 / DW-23.

## Validation evidence (R1)
- `dotnet build`: 0 errors.
- `AccessHarvester.Pipeline.Tests`: 952 passed / 0 failed (incl. 10 new ribbon-entry-point
  hermetic tests).
- `CanonicalGoldenRegressionTests.HomeHealthCare_ReproducesGoldenOutput`: PASSED — HHC
  golden reproduced byte-level by a fresh COM run (golden-neutral proven, not asserted).
- `NorthwindLocalInvariantTests`: PASSED (proprietary, local-only, never committed) —
  `RibbonEntryPoints` populated; `cmdAbout_OnAction` → `modRibbonCallback`; nested
  `ddMRU_OnAction` surfaced; every entry carries a resolution status.
