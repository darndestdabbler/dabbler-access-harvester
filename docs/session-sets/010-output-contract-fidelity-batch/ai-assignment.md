# AI Assignment Retrospective: Set 010-output-contract-fidelity-batch

## Per-Session Analysis

| Session | Task class | Orchestrator used | Verification model | Routed cost | Fit assessment |
| :------ | :--------- | :---------------- | :----------------- | :---------- | :------------- |
| 1 | Multi-file emission/decode correctness (2 Majors + 1 Minor) + contract-decision + hermetic test authoring | Claude Opus 4.8 | GPT-5-4 | ~$0.2888 | Good |
| 2 | Minors + docs + COM-tier coordinated golden regen + decision-consensus | _pending_ | _pending_ | _pending_ | _pending_ |

## Actuals Summary — Session 1 (2026-06-13)

* **Orchestrator**: Claude Opus 4.8 (1M context), effort=high.
* **Task class**: Three 006-proven findings — F-2-1 (Major, DAO query-type decode
  shifted one slot → crosstabs mislabeled `MakeTable` and mis-classified
  possibly-unused), F-3-7 (Minor, parser TRANSFORM result discarded/re-stamped),
  F-3-5 (Major, explicit `ByVal` never emitted in ParserOutput). Pure
  emission/decode logic with hermetic-testable seams; golden regen deferred to S2.
* **Re-grounding**: Fresh probes confirmed both Majors against the current tree
  (crosstab `QueryTypeCode:16 / QueryTypeName:"MakeTable"` in `ir/`; bare
  `ByRef:true` boolean in the manifest; `overwrite` param `Optional ByVal` in
  source) before any edit — evidence-over-inference.
* **Implementation** (handled directly — mechanical implementation of already-decided
  006 recommendations, not novel reasoning):
  * Centralized the DAO `QueryDefTypeEnum` codes in a new
    `DaoQueryTypeCodes` (the finding's "share one decode table"), rewired
    `UsageClassifier` + `QuestionGenerator` off it (removed the shifted private
    constants and the duplicate name-recompute that the lessons-learned
    emission-drift entry warns about), and corrected the COM-side
    `QueryExporter.QueryTypeName` decode + the strategy-doc §4 table.
  * F-3-7: `Sql.Parsed.QueryType` now derives from the parsed AST `Shape`
    (DAO code retained as `QueryTypeCode` provenance), falling back to the DAO
    type on parse failure.
  * F-3-5: emit the schema-declared `PassingMechanism` enum (the prior bare
    `ByRef` boolean was undeclared in the `Parameter` $def; a sibling emitter
    `VbaSignatureEnricher` already used `PassingMechanism`), omitting Unspecified
    per the output-omission convention.
* **Decisions recorded**: DW-3 (suffix→type for variables) explicitly **deferred**
  (golden-neutral, separate projection paths) and DW-7 newly logged for the
  deferred F-3-7 part-(b) embedded-SELECT projection.
* **Validation**: `dotnet build` 0 errors; `AccessHarvester.Pipeline.Tests`
  803/803; `AccessHarvester.Tests` affected hermetic classes incl. new
  `QueryExporterDecodeTests` 13/13. COM golden-regression deliberately not run
  (S2 owns the single coordinated regen).
* **Verification** (cross-provider GPT-5-4):
  * R1 ISSUES_FOUND ($0.164565) — two Majors, both DAO code 144 (`dbQSPTBulk`): the
    decode label collided with 112 and Q11 excluded 144. Both accepted (real
    internal inconsistency the session left) and fixed: distinct
    `SqlPassthroughBulk` label + map entry, Q11 now includes 144, strategy doc +
    new classifier pin.
  * R2 VERIFIED ($0.124255) — empty issues; the rest of the session was endorsed
    in R1.
  * Total routed verification: ~$0.2888.

## Cost/Model-Fit Analysis

Opus 4.8 was a **good fit**. The work crossed six production files plus tests and
docs with a real contract-fidelity decision (emit `PassingMechanism` vs. minimal
add-a-boolean) and two scope/defer judgments — above the mechanical single-file
threshold where a cheaper orchestrator would be appropriate, but the
implementation itself incurred no routed token cost (only the mandatory
cross-provider verification did). The one substantive R1 miss (DAO 144 internal
inconsistency) was a faithfulness gap the verifier was well-placed to catch — the
cross-provider gate working as designed.

## Forward Recommendation for Next Session (Session 2)

**Recommended orchestrator**: Claude Opus 4.8 @ effort=high.

**Rationale**: Session 2 is the heavier of the two — it carries the **single
coordinated COM-tier golden regeneration** (every delta must be explained by a
named finding's fix, a stop-and-investigate discipline), the
`CanonicalGoldenRegressionTests` confirmation, a **decision-consensus** call for
F-2-4 (Q6 implement-or-strike), several Minors (BOM, casing, F-2-5/2-7/1-4/1-7),
the O-1-2/O-1-1 doc ride-alongs, and **authoring the next session set**. The
golden-regen coordination + the explain-every-delta gate + cross-session evidence
assembly favor an Opus-class orchestrator. Host precondition: Windows + Microsoft
Access (COM tier). Cross-provider verification routes to GPT-5-4.
