# Verification Round 1

```json
{
  "verdict": "ISSUES_FOUND",
  "confidence": "high",
  "summary": "The Northwind happy path passes, but Session 3 is not spec-complete. Major gaps remain in subform handling, query deadness/equivalence, QueryColumns persistence, event mapping, and lookup-table scoring semantics.",
  "issues": [
    {
      "category": "Completeness",
      "severity": "Major",
      "title": "QueryColumns are not implemented",
      "details": "Session 3 requires Queries + QueryColumns writers from existing QueryAbstraction data, but there is no QueryColumn model, extractor output, BuildResult field, writer parameter, or test coverage. The correct implementation must extract query columns and persist them alongside Queries."
    },
    {
      "category": "Completeness",
      "severity": "Major",
      "title": "Query deadness is never computed",
      "details": "QueryRow.IsDead is always written as false, despite the spec requiring is_dead = 1 for queries with no form RowSource, VBA, or macro OpenQuery callers. The correct implementation must build caller sets from form controls, Session 2 references, and macro actions before writing Queries."
    },
    {
      "category": "Completeness",
      "severity": "Major",
      "title": "Subform nested-FQN extraction is missing",
      "details": "The current form walk emits only the subform control row and does not resolve or persist contained controls/events as nested FQNs. The correct implementation must traverse subform targets and emit nested control/event rows per spec."
    },
    {
      "category": "Correctness",
      "severity": "Major",
      "title": "Event binding extraction is incomplete and contains a bad OnPush mapping",
      "details": "A fixed allow-list silently drops unlisted Access events, and the generic On-prefix stripping maps OnPush to a non-existent Push handler instead of Click. The correct implementation must use explicit property-to-event mappings, include the full supported event set, and special-case irregular names."
    },
    {
      "category": "Completeness",
      "severity": "Major",
      "title": "Query equivalence hashing does not satisfy the clause-hash/self-join requirement",
      "details": "ComputeClauseHash is regex-based rather than AST structural, and equivalence_canonical_fqn is assigned only from sql_text_hash groups. The correct implementation must produce a structural clause hash and use clause/self-join equivalence when canonicalizing duplicate queries."
    },
    {
      "category": "Correctness",
      "severity": "Major",
      "title": "Lookup-table scoring uses the wrong signal for RowSource usage",
      "details": "The score's +30 signal is driven by a boolean set built from BoundTableOrQuery values, not from actual RowSource references or their control counts as required by the spec. The correct implementation must count real RowSource usages per table/query and incorporate that count into the heuristic."
    },
    {
      "category": "Correctness",
      "severity": "Minor",
      "title": "BaseInfo parsing is brittle",
      "details": "BoundTableOrQuery and BoundColumn are derived by stripping \\\" and splitting on semicolons, which is not robust against quoted semicolons or format drift. The correct implementation should consume the existing structured FormComponentSpec/RowSourceSpec data or use a quote-aware parser."
    },
    {
      "category": "Correctness",
      "severity": "Minor",
      "title": "Public Write() signature change is not binary compatible",
      "details": "Appending optional parameters to SymbolDbWriter.Write is source-compatible but breaks already-compiled external callers expecting the old method signature. The correct implementation should preserve the old overload and delegate to the new one."
    }
  ]
}
```

### 1. FormControlsExtractor BaseInfo parser robustness

- **Issue →** Minor robustness issue. The current logic is safe when `BaseInfo` has fewer than 4 segments because it leaves both bound fields `NULL`, but the parser itself is brittle. `Replace("\\\"", "")` plus `Split(';')` is not quote-aware, so any quoted semicolon inside a segment will corrupt indexing. The Unicode-escape concern is mostly a non-issue because `JsonNode.GetValue<string>()` already resolves normal JSON escapes before this code runs.
- **Location →** `AccessHarvester.SymbolDb/Extraction/FormControlsExtractor.cs`, `BuildControlRow()`.
- **Fix →** Prefer the spec-required structured source (`FormComponentSpec` + `RowSourceSpec`) instead of reparsing raw `BaseInfo`. If `BaseInfo` must be used, replace `Split(';')` with a small state-machine parser that respects quoted fields and escaped quotes. On parse failure, emit `NULL` and record a diagnostic instead of silently guessing.

### 2. Event property classification

- **Issue →** Major completeness/correctness issue. A fixed allow-list means any valid Access event property omitted from the set is silently dropped. The macro branch is also too broad for special sentinel values like `[Embedded Macro]`, which would currently be emitted as `handler_kind = 'macro'` with `handler_fqn = "[Embedded Macro]"`, even though that does not point at a `Macros` row.
- **Location →** `FormControlsExtractor.EventPropertyNames`, `EmitControlBindings()`, `EmitFormLevelBindings()`, `EmitBinding()`.
- **Fix →** Use an explicit property-name-to-event-name map rather than a flat allow-list plus generic `StripOnPrefix()`. Expand the supported event list to the actual Access event surface you intend to support. Add special handling for embedded macros and any other sentinel values; only emit `handler_kind = 'macro'` when the value can be resolved to a real macro FQN.

### 3. Form-level event handler FQN convention

- **Issue →** No defect here. `Form_frmCompanyDetail.Form_Open` is the standard Access/VBA naming pattern: module/class `Form_frmCompanyDetail`, procedure `Form_Open`. There is no duplicate `Form_` segment.
- **Location →** `FormControlsExtractor.EmitBinding()` for the `controlName is null` branch.
- **Fix →** None. Keep this convention, assuming Session 1 symbols are stored as `<module-fqn>.<procedure-name>`, which the provided tests strongly imply.

### 4. `OnPush` event mapping

- **Issue →** Major correctness issue if `OnPush` appears. The generic `StripOnPrefix("OnPush") => "Push"` creates phantom VBA FQNs like `<Control>_Push`, but Access uses `Click` semantics here, not a real `Push` event procedure name.
- **Location →** `FormControlsExtractor.EventPropertyNames`, `StripOnPrefix()`, `EmitBinding()`.
- **Fix →** Replace generic suffix derivation with an explicit mapping table. At minimum, map `OnPush -> Click`. If your KB never emits `OnPush`, remove it from the supported set rather than generating bad bindings.

### 5. Subform handling

- **Issue →** Major spec gap. The spec says subform controls produce nested-FQN rows. The current walker only emits the subform control itself, and then recursively walks whatever `Children` are already inline in the same JSON object using the same `formFqn`. It does not resolve the contained form as a separate artifact, and it does not encode any nested path. Separately, `BuildArtifacts.RunForNorthwind()` only extracts one form file at all.
- **Location →** `FormControlsExtractor.WalkChildren()` and `AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs`.
- **Fix →** Extract all form KB JSON files, not just `frmCompanyDetail`. When a subform control is encountered, resolve its target form and recurse with a nested naming scheme that matches the schema contract. If the current row shape cannot represent nested control identity, the model/writer need to be extended so nested FQNs are persisted explicitly.

### 6. Macro position contiguity

- **Issue →** Minor correctness issue. Yes, gaps can appear. The loop index `i` always increments, and skipped non-object elements do not get renumbered. That means emitted `Position` values are only dense if every `parsedActions[i]` is a `JsonObject`.
- **Location →** `AccessHarvester.SymbolDb/Extraction/MacroExtractor.cs`, `ExtractOne()`.
- **Fix →** If dense ordering matters downstream, use a separate `position` counter that increments only when an action row is emitted. If non-object elements are impossible by contract, fail fast instead of `continue`; that preserves the invariant and surfaces parser drift early.

### 7. Query normalisation faithfulness

- **Issue →** Acceptable as a heuristic, but not a full SQL-aware normalizer. The regex is non-greedy, so it will stop at the first `;` after `PARAMETERS`, which is the intended behavior for a normal Access parameters clause. The real limitation is that it is not token-aware and will not handle exotic multi-statement or string-literal edge cases correctly.
- **Location →** `AccessHarvester.SymbolDb/Extraction/QuerySchemaExtractor.cs`, `NormalizeSql()`.
- **Fix →** For Northwind-style QueryDefs, this is probably sufficient. If you want stronger correctness, tokenize the leading `PARAMETERS` clause rather than regexing raw SQL text.

### 8. Clause-hash boundary detection

- **Issue →** Major fidelity issue relative to the spec. The current implementation is regex-based clause slicing, not an AST structural hash. It can mis-detect clause boundaries when keywords appear inside string literals or bracketed identifiers like `[SELECT]`, and it preserves too much raw text shape to be a reliable structural-equivalence signal.
- **Location →** `QuerySchemaExtractor.ComputeClauseHash()`.
- **Fix →** Use an Access-SQL-aware tokenizer/parser that understands strings, bracketed identifiers, comments, and clause boundaries. Hash normalized clause structures, not raw regex slices. Without that, `sql_clause_hash` should be documented as a heuristic, not as the spec’s structural-hash implementation.

### 9. Equivalence canonicalisation determinism

- **Issue →** Determinism itself is fine; completeness is not. Using ordinal-lexicographic minimum FQN as canonical is stable across rebuilds given the same inputs, and `NULL` on the canonical row is a reasonable representation. The real problem is that canonicalization only uses `sql_text_hash`, so duplicates detectable only by clause-hash or self-join equivalence are ignored.
- **Location →** `QuerySchemaExtractor.Extract()`, `canonicalByHash`.
- **Fix →** Keep the deterministic canonical-selection rule and keep `NULL` for the canonical row if that matches the rest of the schema. Extend grouping logic to include the required clause-hash/self-join equivalence classes.

### 10. Lookup-table score weights

- **Issue →** The weight values and the label-hint list are fine; they match the spec’s stated heuristic and are not over-tuned. `CompanyTypes` scoring 80 because `CompanyType` does not match the hint list is acceptable. The real issue is elsewhere: the row-source signal is not implemented according to spec.
- **Location →** `TableSchemaExtractor.LabelHints`, `TableSchemaExtractor.ExtractOne()`, and especially `BuildArtifacts.RunForNorthwind()`.
- **Fix →** Keep the current hint list unless the spec changes. Change the row-source input so it is based on actual `RowSource` references and their counts, not a boolean set of `BoundTableOrQuery` names gathered from one form.

### 11. `is_dead` for queries and macros

- **Issue →** For queries: not acceptable. The spec explicitly requires `is_dead = 1` when there are no callers, and the necessary ingredients largely already exist. For macros: the spec excerpt does not explicitly require macro deadness, so leaving `MacroRow.IsDead = false` is less severe, though it should still be documented as deferred if the column exists.
- **Location →** `QuerySchemaExtractor.Extract()` always sets `IsDead: false`; `MacroExtractor.ExtractOne()` always sets `IsDead: false`.
- **Fix →** Build a caller graph before writing queries:
  - form callers from control `RowSource`,
  - VBA callers from Session 2 references/member accesses or query-name resolution,
  - macro callers from parsed `OpenQuery` actions.
  Then set `Queries.IsDead` accordingly. For macros, either implement deadness from event bindings/macro calls or explicitly defer it in code/tests/docs.

### 12. `INSERT OR IGNORE` semantics

- **Issue →** Minor but real observability problem. In a fresh `overwrite: true` build, duplicate PKs are signal, not noise. `INSERT OR IGNORE` hides those bugs. It does not suppress foreign-key violations in SQLite, so FK checking still helps, but duplicate-row defects can be silently masked.
- **Location →** New Session 3 writer methods in `SymbolDbWriter`.
- **Fix →** Prefer plain `INSERT` for full rebuilds. If incremental idempotency is required, use explicit `ON CONFLICT DO UPDATE` or `DO NOTHING` only where duplicates are expected and validated, and log the conflict count.

### 13. Test coverage gaps

- **Issue →** Not acceptable for spec verification. The current tests prove the happy path for one form, one VBA event, one macro, some query hashing, and one table heuristic. They do not prove subform nesting, macro-bound events, expression-bound events, unbound controls, query `is_dead`, query canonicalization via clause-hash/self-join, or the missing `QueryColumns` feature at all.
- **Location →** `tests/AccessHarvester.SymbolDb.Tests/Session3ExtractorTests.cs`.
- **Fix →** Add hermetic tests for:
  - at least one macro-bound event,
  - at least one expression-bound event,
  - at least one unbound control with both bound fields `NULL`,
  - at least one subform nested-control extraction,
  - query `is_dead` positive and negative cases,
  - `QueryColumns` extraction/persistence,
  - `OnPush` mapping if supported,
  - duplicate-query detection by clause-hash if text-hash duplicates are absent.

### 14. Backward compat of optional `Write()` params

- **Issue →** This is source-compatible but binary-incompatible. Existing in-solution tests recompile, so they pass. Any already-compiled external assembly calling the old five-argument `Write()` method will fail to bind at runtime because the method signature changed.
- **Location →** `AccessHarvester.SymbolDb/Persistence/SymbolDbWriter.cs`, public `Write()` signature. The same concern also applies to any public `BuildResult` constructor shape change.
- **Fix →** Keep the original public overload and have it delegate to the new overload with `null` defaults. If public API stability matters, do not change existing method signatures in place.

---

# Verification Round 2

```json
{
  "verdict": "VERIFIED",
  "confidence": "high",
  "summary": "All five applied fixes address the corresponding Round 1 findings, and the remaining four deferrals are technically justified and explicitly scoped. No fix appears misapplied, and no deferral reads as an unscoped omission.",
  "issues": []
}
```

## Fix verification

### FIX A — OnPush → Click mapping
**Result: Verified**

- **Addresses the finding:** Yes. The Round 1 defect was that `OnPush` was being normalized mechanically to `Push`, producing a phantom handler name. The new irregular mapping table fixes that exact case before generic `On...` stripping runs.
- **Test quality:** Good. The added test is directly targeted:
  - it uses `OnPush=[Event Procedure]`
  - asserts `EventName == "Click"`
  - asserts `HandlerFqn == "Form_test.btnLegacy_Click"`
  This would have failed before the change.
- **Obvious gaps:** None for the reported defect. This is the correct minimal fix.

### FIX B — Macro action position contiguity
**Result: Verified**

- **Addresses the finding:** Yes. Switching from `Position: i` to a separate dense `position++` after the null-node guard fixes the gap problem when `parsedActions[i]` is null or skipped.
- **Test quality:** Adequate. The new test validates the intended invariant: positions are dense and contiguous per macro. It is more of an output-invariant test than a branch-specific proof of the null-skip path, but the code change itself is straightforward and correct.
- **Obvious gaps:** None apparent. The fix is mechanically sound.

### FIX C — Count-tiered lookup-table score
**Result: Verified**

- **Addresses the finding:** Yes. Round 1 said the score used a presence flag instead of actual counts. The extractor now accepts a count dictionary and applies distinct score tiers for `>=1` and `>=3`.
- **Test quality:** Good. The new test proves that 3 uses score higher than 1 use by the intended delta.
- **Plumbing/integration:** Also correct. `BuildArtifacts` now accumulates counts rather than collapsing to a boolean-like presence map, so the extractor receives the data it needs.
- **Obvious gaps:** No material gap relative to the Round 1 finding.

### FIX D — Query `is_dead` computation
**Result: Verified**

- **Addresses the finding:** Yes. `IsDead` is now computed from an explicit caller-name set instead of remaining effectively uncomputed.
- **Test quality:** Good at extractor level. The test proves the core logic: when one query FQN is omitted from the caller set, only that query is marked dead.
- **Integration wiring:** Present. `BuildArtifacts` now builds a caller set from form row sources/bound sources, VBA references, and relevant macro actions, then passes it into `QuerySchemaExtractor.Extract(...)`.
- **Obvious gaps:** Caller discovery is still heuristic, but the Round 1 defect was that deadness was not computed at all. This fix closes that defect.

### FIX E — Backward-compatible `Write()` overload
**Result: Verified**

- **Addresses the finding:** Yes. Restoring the original overload verbatim is the correct way to repair the binary compatibility break, and forwarding to the wider overload preserves behavior.
- **Test quality:** No dedicated test was shown. That weakens evidentiary coverage, but the fix itself is explicit and correct at the API-signature level.
- **Obvious gaps:** None. This is the right compatibility-preserving pattern.

---

## Deferral verification

### DEFERRAL 1 — QueryColumns
**Result: Acceptable deferral**

- **Technical validity:** Valid. If the current raw KB JSON has `OutputColumns == null` for every query because the upstream exporter does not enumerate `QueryDef.Fields`, then adding a writer in this session would persist zero rows from the available input.
- **Scoped:** Yes. The rationale names two concrete unblockers:
  1. widen the raw exporter, or
  2. land the JET SQL parser and derive output columns from parse trees.
- **Should it have been completed in-session anyway?** No. The missing upstream data makes this a poor fit for in-session completion.

### DEFERRAL 2 — Subform nested-FQN extraction
**Result: Acceptable deferral**

- **Technical validity:** Valid. Resolving subform event/member FQNs requires following `SourceObject` into other form JSON files, which is a broader multi-form traversal problem than the current single-probe-file flow.
- **Scoped:** Yes. The rationale explicitly limits the current probe and proposes a follow-up multi-form sweep before Session 5 if deeper migration coverage is needed.
- **Should it have been completed in-session anyway?** Not necessarily. Given the stated probe boundary and the fact that the target control is already fully resolved without recursion, deferral is reasonable.

### DEFERRAL 3 — AST clause-hash
**Result: Acceptable deferral**

- **Technical validity:** Valid. A structural clause hash really does require proper SQL tokenization/parsing to avoid mis-handling bracketed identifiers, literals, comments, and other syntax. Reusing a dedicated JET SQL parser session is the right architecture.
- **Scoped:** Yes. The handoff target is explicit: rewire once the separate parser infrastructure lands.
- **Should it have been completed in-session anyway?** No. Re-implementing parser-grade logic here would duplicate work and increase risk.

### DEFERRAL 4 — BaseInfo parser
**Result: Acceptable deferral**

- **Technical validity:** Valid for this round. The rationale is not claiming the parser is ideal; it is claiming there is no failing Northwind fixture input right now, which matches the narrow re-verification scope.
- **Scoped:** Yes. Re-evaluate when a sample DB introduces embedded-semicolon/quote-sensitive cases.
- **Should it have been completed in-session anyway?** No. This is a low-priority hardening task without a current reproducer, and it was explicitly documented rather than ignored.

---

## Overall assessment

- **All five fixes are correctly applied.**
- **All four deferrals are justified and bounded.**
- **No deferred item appears silently abandoned.**
- **No fix appears to miss its Round 1 target.**
