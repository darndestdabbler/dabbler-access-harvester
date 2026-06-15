# Change Log — Set 015: `015-unresolved-items-completeness`

This set closed **DW-11** — `unresolved-items.json` (the run-wide index a consumer reads to enumerate every VBA reference the resolver could not bind) was procedure-body-scoped and **undercounted** in two distinct layers, both surfaced with proven reproducers by the 011 Session 3 devil's-advocate review. It was proactive hardening of a known aggregation bug, run under the consumer-driven-only posture after the Set 006 review arc completed (Set 014).

## Session 1 — Layer A: procedure-HEADER unresolved references reach the index

The VBA half of `unresolved-items.json` was built from `FunctionClassification.UnresolvedReferences`, and `FunctionClassificationBuilder` walks `procedureAst.Body` ONLY — so an unresolved reference in a procedure HEADER (an `Optional` parameter's default expression) was absent from the index even though `ParserOutputProjector` groups it into `Body.References`.

* **Fix — dedup-additive feed:** the aggregator additionally consumes the full procedure-scoped resolver reference set (body + header) via the new `ParserOutputProjector.EnumerateProcedureScopedReferences` (the SAME set that builds `Body.References`, so the index is a structural superset by construction), filtered to (with-flattened) Unresolved, suppressing the body overlap with a **non-mutating** dedup on `(module, procedure, line, column, reason, memberName)`. Net-new items are exactly the header refs, tagged `Source=FunctionClassification`.
* **Verification:** routed gpt-5-4 — R1 ISSUES_FOUND (1 Major: an intra-feed dedup over-suppressed distinct chained-header links, demonstrated via a red pin) → fixed (non-mutating `Contains` + member-name in the key) → R2 VERIFIED.
* **Golden:** proven byte-identical on HomeHealthCare — Layer A was golden-neutral (no regen).

## Session 2 — Layer B: module-level unresolved references get a manifest home

A MODULE-LEVEL unresolved reference (a `Const`/`Dim` initializer, an `Implements <unresolved>`, a module-scope `As New <unresolved>`) lives on `resolvedModule.References` with NO enclosing `ProcedureScope`, so the projector dropped it from both `ParserOutput.Body.References` and the index — no manifest signal at all.

* **Schema decision (recorded + routed-critiqued):** the manifest home is the run-wide index; a module-level ref surfaces under the new `UnresolvedSource.ModuleLevelReference` with `ProcedureName=null`. The smallest correct change: the `UnresolvedItem` `$def` is `additionalProperties`-tolerant and no property is added, so the only schema edit is **widening the `Source` enum** in the living `harvester-schema.v1.1.json` — no manifest-envelope version bump and no `ParserOutput` change. The enum serialises as a string (`JsonStringEnumConverter`), so appending is backward-compatible.
* **Fix:** new `ParserOutputProjector.EnumerateModuleLevelReferences` (the complement of `GroupReferencesByProcedure`), new `UnresolvedItemAggregator.FromModuleLevelReferences` feed + `ModuleLevelReference` record, and `Pipeline.cs` wiring. **No double-count:** a module-level reference has no enclosing `ProcedureScope`, so it is disjoint from both procedure-scoped feeds by construction.
* **Golden — NOT neutral (the DW-11 "zero cases" assumption was WRONG):** the golden gate caught 2 real HomeHealthCare module-level references — `Public Const ERR_SAME_FY/ERR_CANCELED = vbObjectError + N` in `basDataOperations` (`vbObjectError` is a VBA intrinsic outside the resolver's built-in set). HHC was regenerated; the proven multiset delta is **exactly +2 `ModuleLevelReference` items, 0 removed** (`UnresolvedItemCount` 1414→1416). Every other golden diff line is reordering from the determinism fix below.
* **Determinism hardening:** the regen exposed that `SortDeterministically`'s key was not a total order — same-key items differing only by member name reshuffled under `List.Sort`'s non-stable introsort once the +2 items perturbed the input. Fixed with a precomputed, **injective `CanonicalResidual`** (full-item JSON serialization) final tiebreaker; `SortDeterministically` was exposed `internal` and pinned by a `[Theory]` over every folded-collision class (module/procedure case-only, null-vs-0, null-vs-empty, `key=value` ambiguity).
* **Verification:** pre-implementation architecture critique (opus) SOUND-WITH-CAVEATS; routed cross-provider session-verification (gpt-5-4) R1 ISSUES (Major: non-total comparer) → R2 ISSUES (Major: non-injective residual + Minor) → R3 ISSUES (Minor: pin breadth) → R4 VERIFIED. In-IDE devil's-advocate prompt authored.

## Validation

* Full solution builds; `AccessHarvester.Pipeline.Tests` **922/922**; golden gate `CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests` + `SchemaConformanceTests` **7/7** integration, byte-identical.
* COM-tier runs were deliberate, sequential, foreground, TRX-logged; spurious `samples/HomeHealthCare.accdb` diff swept before staging.

## Outcome

* **DW-11 DISCHARGED** (moved to Closed, full entry retained with a CLOSED banner) — both undercount layers closed; `unresolved-items.json` is now a structural superset of the inline procedure-scoped unresolved references and additionally surfaces module-level unresolved references.
* **New DW-17:** module-level unresolved refs reach the run-wide index but NOT the per-module `ParserOutput.Body.References` — a completeness asymmetry deferred by design (adding a module-level slot to `ParserOutput` is a structural envelope change with broad golden churn, for no completeness the index doesn't already provide).
* **Next set:** recorded **consumer-driven-only handoff** (routed gemini-pro) — no DW item currently has an active consumer trigger; DW-1..8 and DW-14 noted as bundling candidates for the next trigger.
