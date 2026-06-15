# 015 Session 2 — Layer B design note (re-grounding + schema decision)

> Re-grounded against the current tree (post-S1). Records the seam, the
> **explicit schema decision** (spec Step 2 requires it before editing the
> schema), and the chosen design. **Status: design decided; routed
> cross-provider critique sought before golden regen.**

## Re-grounding (confirmed on the current tree, 2026-06-15)

- **Projector still drops module-level refs.**
  `ParserOutputProjector.GroupReferencesByProcedure`
  (`ParserOutputProjector.cs:145-163`) calls `FindEnclosingProcedureScope`
  and `continue`s when it returns null — the comment at lines 65-67 states
  "Module-level references (no ProcedureScope ancestor) are dropped — the
  v1.0 schema has no module-level item type." Still true.
- **The reference EXISTS on the raw module (empirically confirmed).** A
  throwaway probe over `Public Const DemoConst = MissingThing` +
  `Public Sub Demo()` produced **exactly one** `resolvedModule.References`
  entry: `ref='MissingThing'`, `enclosingScope=ModuleScope` (NOT a
  `ProcedureScope`), `outcome=Unresolved`. So `FindEnclosingProcedureScope`
  walks `ModuleScope → ProjectScope`, finds no `ProcedureScope`, returns
  null, and the ref is dropped from `Body.References` AND from the
  procedure-scoped index Layer A added. This confirms DW-11 Layer B's
  reproducer (originally GPT-5.4, 011 S3) on the current tree.
- **`unresolved-items.json` already supports module scope.**
  `UnresolvedLocation.ProcedureName` is optional (no `required`), documented
  "Null when the producer is at module-scope." The `UnresolvedItem` `$def`
  in `harvester-schema.v1.1.json` (lines 1269-1281) has **no
  `additionalProperties: false`**. The only strict element is the `Source`
  enum (line 1273): `["Resolver","SqlFormReference","DynamicSqlSite",
  "NavigationTarget","FunctionClassification"]`.

## Schema decision (explicit, per spec Step 2 + the additive-field lesson)

**Decision: the manifest home is `unresolved-items.json` (the run-wide
index). A module-level unresolved reference surfaces as an `UnresolvedItem`
with `ProcedureName = null` under a NEW `UnresolvedSource` enum value,
`ModuleLevelReference`. No manifest-envelope version bump; no `ParserOutput`
change.**

### Why this is the smallest correct change

Applying the lesson *"Additive Field On An `additionalProperties`-Tolerant
Shared `$def` → Bump The Sub-Document Version, Not The Envelope File"*:

1. **No new property** is added to `UnresolvedItem`/`UnresolvedLocation` —
   the module-level ref reuses the existing slots (`ModuleName`, `Line`,
   `Column`, `SurroundingText`) with `ProcedureName` omitted. The location
   shape already documents module-scope, so the index already *structurally*
   admits a module-scoped item.
2. **The only schema edit is widening the `Source` enum** to add
   `"ModuleLevelReference"`. This is backward-compatible: old documents stay
   valid (they never carry the new value), and a consumer switching on the
   enum simply sees a new producer category — exactly the documented
   extension contract on `UnresolvedSource` ("Closed set; future producers
   append rather than reuse an existing value").
3. **No envelope-file bump.** Per the lesson, a full envelope `v1.1 → v1.2`
   bump is warranted only when (a) the envelope's own required/structural
   shape changes, or (b) a strict (`additionalProperties: false`) `$def`
   must admit a new property. Neither holds: `UnresolvedItem` is tolerant
   and we add no property; we widen an existing enum on an existing `$def`.
   The repo treats `harvester-schema.v1.1.json` as the **living** schema for
   the v1.1 envelope (the lesson's stated precondition), so widening the
   enum in place is correct and costs no envelope churn.
4. **No `ParserOutput` change.** The "v1.0 schema has no module-level item
   type" constraint is about `ParserOutput.Items` (procedures/declares); a
   module-level reference has no procedure `Body` to live in. We do **not**
   add a module-level item type to `ParserOutput`. The run-wide index — which
   already supports module-scoped locations — is the complete manifest signal
   DW-11 asks for. `ParserOutput.SchemaVersion` stays `1.1`.

### Alternatives considered and rejected

- **Reuse `Source = Resolver`.** Rejected: `Resolver` is reserved for
  `ResolverDiagnostic` items, and the aggregator *deliberately* does not feed
  `resolverDiagnostics` (to avoid double-emitting). Conflating module-level
  references with diagnostics loses the discriminator a consumer needs to
  filter ("show me only the module-level gaps") and muddies provenance.
- **Reuse `Source = FunctionClassification`.** Rejected: that source is
  procedure-scoped by contract (it carries `ProcedureName`); a module-level
  ref has none. Layer A correctly reused it for header refs *because those
  are procedure-scoped*; a module-level ref is categorically different.
- **Add a module-level references slot to `ParserOutput` (inline surface).**
  Rejected as not-smallest: it is an envelope structural change (a new
  top-level array on every `ParserOutput`) with broad golden churn, for no
  added completeness — the index already carries the complete signal. Spec
  explicitly says do the full envelope bump only if necessary.

## Chosen design (symmetric with Layer A)

1. **New projector enumerator** `EnumerateModuleLevelReferences(ResolvedModule)`
   — returns every `resolvedModule.References` entry whose
   `FindEnclosingProcedureScope` is null (exactly the set
   `GroupReferencesByProcedure` drops). Mirrors `EnumerateProcedureScopedReferences`.
2. **New aggregator feed** `FromModuleLevelReferences(IEnumerable<ModuleLevelReference>)`
   — emits `UnresolvedItem { Source = ModuleLevelReference, ProcedureName =
   null, ... }` for references whose (with-flattened) outcome is `Unresolved`.
   Field mapping mirrors `FromProcedureScopedReferences` exactly (Reason ←
   with-flattened reason; Line/Column ← `expr.Provenance`; SurroundingText /
   memberName ← `ExtractReferenceName`).
3. **New record** `ModuleLevelReference(string ModuleName, ResolvedReference Reference)`.
4. **New enum value** `UnresolvedSource.ModuleLevelReference` (appended last).
5. **Schema:** add `"ModuleLevelReference"` to the `Source` enum in
   `harvester-schema.v1.1.json`; document it in place.
6. **Wire into `Pipeline.cs`** alongside the existing procedure-scoped feed.

### No double-count

A module-level reference has no enclosing `ProcedureScope`, so it is, by
construction, **disjoint** from both procedure-scoped feeds (the body-only
`FunctionClassification` feed and the Layer A procedure-scoped feed) — those
only carry references *with* a `ProcedureScope`. There is no positional
overlap to dedup. (Pinned by `LayerB_ModuleLevelFeed_DoesNotDoubleCount_*`.)

### Golden impact

DW-11 establishes HHC has **zero** module-level unresolved cases, so the
module-level feed is **inert on HHC** → `unresolved-items.json` data
byte-identical. The only committed change is the **schema file** (one enum
value) + code + hermetic pins. Prove byte-level neutral via
`CanonicalGoldenRegressionTests` (no regen), exactly as S1 did. The schema
file is documentation of the contract, not a golden artifact — but
`ManifestSchemaConformanceTests` must still pass (the widened enum is a
superset, so existing golden docs remain valid).

## Verification path

1. **Red first:** the module-level reproducer pinned (probe confirmed the
   gap; the post-S1 index drops it).
2. Implement the module-level feed; turn the pin green; add no-double-count
   + inert-on-clean-module pins.
3. Prove byte-level golden neutrality (no regen) + `ManifestSchemaConformanceTests`.
4. Closeout-grade routed `session-verification` + in-IDE devil's-advocate
   pair as primary — focus the contrarian hypothesis on (a) whether any
   module-level ref can ALSO be procedure-scoped (double-count) and (b)
   whether widening the enum without an envelope bump is contract-correct.

---

## Execution result (2026-06-15 — implemented + golden regen)

**Implemented as designed**, with one determinism correction and one
prediction overturned by reality.

- **Implemented:** new `ParserOutputProjector.EnumerateModuleLevelReferences`
  (references whose `FindEnclosingProcedureScope` is null), new
  `UnresolvedItemAggregator.FromModuleLevelReferences` feed + the
  `ModuleLevelReference` record, new `UnresolvedSource.ModuleLevelReference`
  enum value (appended last), the schema enum widening, and the Pipeline.cs
  wiring. The enum serialises as a **string**
  (`[JsonConverter(typeof(JsonStringEnumConverter))]`) — golden shows
  `"Source": "ModuleLevelReference"` — so appending the value is safe
  (critique caveat #4 confirmed).

- **DW-11 "zero module-level cases" was WRONG — Layer B IS golden-affecting.**
  The golden gate caught it: HomeHealthCare `basDataOperations` lines 4-5 are
  `Public Const ERR_SAME_FY As Long = vbObjectError + 1001` /
  `Public Const ERR_CANCELED As Long = vbObjectError + 1002`. `vbObjectError`
  is a VBA intrinsic outside the resolver's built-in set, so each Const
  initializer is a **module-level `not-in-scope` reference**. Before Layer B
  these two had **no manifest signal at all**; now they reach
  `unresolved-items.json` under `ModuleLevelReference` (`ProcedureName=null`).
  This **overturns the synthetic-only-validation concern (critique caveat
  #3)**: the feed is validated against real-world data, not just pins.

- **Determinism correction (caught by the regen):** the first regen showed,
  besides the +2 items, a cluster of **same-key item reorderings**.
  `SortDeterministically`'s key `(Source, module, procedure, line, column,
  reason)` is NOT a total order — a chained member access emits several
  Unresolved links sharing position+reason and differing only by member name
  (`.Fields` vs `.Value`). `List.Sort` is a non-stable introsort, so the +2
  items perturbed the partition and reshuffled those equal-key pairs. **Fix:**
  added `SurroundingText` (member name) as the final sort tiebreaker, making
  the order a total function of the input set. Re-regen proof: the
  unresolved-items multiset is **identical except for the 2 net-new
  `ModuleLevelReference` items (0 removed)**; every other diff line is pure
  reordering into the now-principled order. `manifest.json`
  `UnresolvedItemCount` 1414 → 1416 is the mechanical consequence.

- **No double-count (confirmed):** a module-level ref has no enclosing
  `ProcedureScope`, so it is disjoint from both procedure-scoped feeds by
  construction; the multiset proof (0 removed, only the 2 module-level items
  added) is the empirical confirmation.

- **Validation:** 3 new Layer B hermetic pins (8 total in
  `UnresolvedItemsCompletenessTests`); full `AccessHarvester.Pipeline.Tests`
  **915/915**; golden gate `CanonicalGoldenRegressionTests` reproduces the
  regenerated corpus byte-identical; `ManifestSchemaConformanceTests` +
  `SchemaConformanceTests` green (7/7 integration) — the widened enum is
  schema-valid for both produced and example documents.

- **Routed pre-implementation critique (opus, architecture):
  SOUND-WITH-CAVEATS.** Caveats addressed: #3 overturned by real HHC data;
  #4 confirmed (string serialisation); #2 (per-module `ParserOutput` has no
  module-level signal — a consumer needing per-module completeness must join
  against the index) recorded as a **known limitation → new DW-17**; #1/C
  are documentation-phrasing notes left as-is (the enum doc already states the
  append-only contract).
