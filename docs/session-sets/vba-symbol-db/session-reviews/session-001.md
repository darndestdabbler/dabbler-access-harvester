# Verification Round 1

## 1. Schema correctness vs spec

`SchemaMigrator` does create the full authoritative schema from the excerpt, not just the Session 1 subset. I checked the DDL against the spec table-by-table: `Modules`, `Symbols`, `Parameters`, `[References]`, `MemberAccesses`, `FormControls`, `EventBindings`, `Macros`, `MacroActions`, `EnumValues`, `Tables`, `Queries`, `QueryColumns`, `BuildRuns`, and `InterpretationHistory` are all present. The column sets match the excerpt, the declared primary keys match, and the explicit foreign keys present in the spec (`Symbols.module_fqn -> Modules(fqn)`, `Parameters.procedure_fqn -> Symbols(fqn)`, `FormControls.form_fqn -> Modules(fqn)`, `EventBindings.form_fqn -> Modules(fqn)`, `MacroActions.macro_fqn -> Macros(fqn)`, `EnumValues.enum_value_fqn -> Symbols(fqn)`, `QueryColumns.query_fqn -> Queries(fqn)`, `InterpretationHistory.build_run_id -> BuildRuns(run_id)`) are all represented in the DDL. The named indexes from the spec are also created: `idx_symbols_module`, `idx_symbols_kind`, `idx_symbols_dup`, `idx_symbols_dead`, `idx_refs_from`, `idx_refs_to`, `idx_refs_unresolved`, `idx_queries_text_hash`, and `idx_queries_clause_hash`.

The only schema-related caveat is not a DDL mismatch but an enforcement gap at runtime: SQLite foreign keys are off by default unless explicitly enabled, and this implementation never enables them. So the schema shape matches the spec, but “foreign keys are honoured” is not actually true in execution. Also, the Session 1 plan text mentions `DeferredArtifacts` once, but the authoritative schema excerpt does not define such a table; this implementation correctly follows the authoritative excerpt by storing deferral state on `Modules`.

## 2. Symbol extraction correctness

`SymbolDbBuilder` does walk every top-level module-item kind listed in `Symbols.kind`: procedures (`sub`, `function`, `property_get`, `property_let`, `property_set`), `enum_decl`, `enum_value`, `module_var`, `module_const`, `event_decl`, and `declare_stmt`. The switch in `ExtractModuleSymbols` covers each of those AST shapes explicitly. It also distinguishes module-level versus local declarations in the way Session 1 needs: top-level `Dim`/`Private` module variables are emitted with `declared_in_fqn = NULL`, while `LocalVariableExtractor` emits local `Dim`/`Const` declarations with `declared_in_fqn = <procedure FQN>`. That part matches the probe-target requirement for the seven module-level `m_lng*`/`m_str*` fields and the two local `Dim` strings.

`signature_string` generation is deterministic, and `body_hash_md5` generation is deterministic in the narrow sense that the same source text will always produce the same output. `SourceBodyExtractor` consistently slices source by line range, and `SignatureFormatter` is stable. However, there is a correctness problem in `BodyHasher`: comment stripping is regex-based and not aware of VBA string literals. An apostrophe inside a string (`"Bob's"`) or `REM` inside a string literal can be misinterpreted as a comment and stripped, which can collapse distinct procedures onto the same MD5 or alter hashes in ways the spec did not intend. That weakens the “behave correctly” bar for dedup. Separately, the signature formatter’s array encoding is not the exact spec example shape for `GetString` (`Variant():ByRef:ParamArray`); it appears to encode arrays as an additional `:Array` tag instead. That is still deterministic, but it is untested and does not obviously match the example format the spec calls out.

## 3. Dead-code detection

`IntraProcedureDeadCodeDetector` does include explicit handling for the three Session 1 shapes the spec names: statements after unconditional `Exit`, statements after unconditional `GoTo`, and literal `If False Then`. On the dead-procedure side, the implementation integrates `CrossReferenceIndex` with a conservative `EntryPointHeuristic` that treats form/report underscore-shaped procedures as event-handler candidates, treats `AutoExec` as live, and treats public/friend procedures as potentially externally callable. Given Session 1 explicitly does not yet have `EventBindings` and navigation-graph joins, that heuristic is broad but defensible for the substrate available in this session.

The weakness here is validation, not basic presence. There are no tests that exercise the required dead-code shapes at all, so the acceptance bar is not really demonstrated. There is also a coarse semantic choice in `SymbolDbBuilder`: any procedure containing one dead-code pattern is written with `is_dead = 1`, which conflates “this live procedure contains unreachable statements” with “this procedure itself is a dead artifact.” Because Session 4 is supposed to skip dead artifacts, that coupling is risky. I would not fail the session solely on the heuristic dead-procedure logic, but I do not consider the dead-code deliverable fully verified because the suite does not probe the required cases and the storage semantics are overly blunt.

## 4. DeferralPreFilter

The deferral filter is broadly in line with the Session 1 requirement. It covers suffix `_Old`, bare suffix `Old`, prefix `Old_`, and dash variants via `-Old` / `Old-`, and it requires a companion base module name to exist before classifying a module as deferred. That companion-existence check is the important safeguard the spec required, and it is implemented correctly. The builder also honors the “deferred modules are NOT parsed” rule: it classifies in pass 1, records deferred `Modules` rows, and skips those FQNs during parsing/scope-building in pass 2.

What is missing is stronger proof. The tests only exercise a subset of the naming patterns and only test classification directly; they do not assert the end-to-end behavior that deferred modules are excluded from parsing and therefore produce no `Symbols`/`Parameters` rows. The implementation looks correct by inspection, but the acceptance bar asked for the deliverable to be present and behave correctly, and the “not parsed” part is not exercised by the suite.

## 5. MD5 dedup

`Md5Deduper` satisfies the Session 1 dedup contract. It groups on `(signature_string, body_hash_md5)`, chooses the canonical deterministically by sorting FQNs alphabetically and taking the first, updates only the non-canonical siblings, and leaves the canonical row’s `equivalence_canonical_fqn` as `NULL`. The implementation uses `GROUP BY` plus `GROUP_CONCAT` rather than a literal SQL self-join, but semantically it is the required grouping pass, and the determinism property is preserved by the explicit in-memory sort.

The main caveat is upstream: dedup correctness depends on `BodyHasher`, and `BodyHasher` is not safe around string literals containing apostrophes or `REM`. So the deduper itself is fine, but the full MD5-equivalence pipeline is not robust enough to call fully correct. The zero-cluster result in `measurement-northwind.md` is acceptable because the spec explicitly allowed “if measurement shows zero, document and proceed,” and the report does document the zero case.

## 6. Persistence

`SymbolDbWriter` does the two most important persistence things correctly: it wraps `Modules`, `Symbols`, and `Parameters` writes in a single transaction, and it uses parameterized commands throughout. There is no string interpolation into SQL command text, so SQL injection risk is appropriately avoided. The write order (`Modules` first, then `Symbols`, then `Parameters`) is also the correct dependency order for the declared foreign keys.

The failure is foreign-key enforcement. In SQLite, declaring `REFERENCES` clauses is not enough; each connection must enable enforcement with `PRAGMA foreign_keys = ON`. Neither constructor in `SymbolDbWriter` does that, and no test asserts FK rejection behavior. So while the schema contains the right FK declarations, the runtime writer does not actually honour them. That is a direct miss against concern 6.

## 7. Tests

The new test project is useful, but it does not cover the acceptance bar tightly enough. It does validate that the builder runs against the canonical fixture, that `cboCompanyTypeID_BeforeUpdate` has `Cancel As Integer ByRef`, that `GetString` has the expected two parameters, that local variables can land under a procedure FQN, that module variables can land at module scope, that deferral classification works in some cases, that body hashing normalizes some whitespace/comment differences, and that persistence round-trips. Those are good smoke checks.

The gaps are still substantial. The smoke test does not assert the exact seven `m_lng*`/`m_str*` probe-target module variables; it only checks two known names and “at least 5.” It does not assert that the local `Dim` set is exactly the two expected locals. It does not assert the exact measured shape counts the session plan called out. There are no tests for exact `signature_string` formatting, no tests for the dead-code detector cases the spec explicitly named, no test that deferred modules are skipped from parsing, no schema-structure assertions, no negative FK-enforcement test, and no deterministic-canonical-choice test for MD5 sibling tagging. Because of that, the suite does not fully exercise the probe-target shapes and mechanics the spec asked Session 1 to prove.

## 8. Build + test results

The reported solution-level results are strong on surface health: `dotnet build AccessHarvester.sln` with 0 errors and `dotnet test AccessHarvester.sln --no-build` with 1542 passed / 3 skipped / 0 failed is exactly the acceptance number the prompt asked me to evaluate. The new `GenerateArtifacts` test also appears to have written `tools/symbol-db/symbol-db.sqlite` and `tools/symbol-db/measurement-northwind.md` successfully, which supports the artifact-generation deliverable.

That said, clean build and green tests are not enough to upgrade this to `VERIFIED` because the uncovered gaps are code-level, not compilation-level: FK enforcement is genuinely missing, body-hash normalization is not VBA-string-safe, and the tests are materially weaker than the probe-target and detector coverage the spec called for. The implementation is close and the schema work is largely sound, but Session 1 does not clear the acceptance bar cleanly as written.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.SymbolDb/Persistence/SymbolDbWriter.cs",
      "summary": "SQLite foreign key enforcement is never enabled, so declared REFERENCES constraints are not actually honoured at runtime.",
      "fix": "Execute `PRAGMA foreign_keys = ON;` immediately after opening every SqliteConnection and add a negative test that an invalid `module_fqn` or `procedure_fqn` insert fails."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.SymbolDb/Extraction/BodyHasher.cs",
      "summary": "Comment stripping is regex-based and can remove apostrophes or `REM` inside VBA string literals, producing incorrect `body_hash_md5` values and false duplicate matches.",
      "fix": "Replace the regex comment remover with a token-aware normalizer that tracks VBA string-literal state and strips comments only when apostrophe/REM occur outside strings; add tests for apostrophes and `REM` inside string constants."
    },
    {
      "severity": "Major",
      "location": "tests/AccessHarvester.SymbolDb.Tests",
      "summary": "The test suite is materially weaker than the Session 1 spec: it does not assert the exact probe-target shapes or the required dead-code/deferral/schema/foreign-key behaviors.",
      "fix": "Add exact assertions for the seven `m_lng*`/`m_str*` module variables and the two local Dims, verify `signature_string` text for `GetString`, add dead-code detector tests for after-Exit/after-Goto/`If False Then`, prove deferred modules are not parsed, assert schema columns/indexes, assert FK enforcement, and add a deterministic-canonical-choice dedup test."
    }
  ]
}
```

---

# Verification Round 2

M1 is adequately fixed. `SymbolDbWriter` now enables `PRAGMA foreign_keys = ON` in a dedicated helper that is invoked from both constructors, which is the right repair because SQLite foreign-key enforcement is connection-local and must be turned on every time a connection is opened. The new negative test that inserts a `Symbols` row with a nonexistent `module_fqn` and asserts a thrown `SqliteException` closes the loop at runtime rather than only trusting schema text. I do not see a remaining gap in the original round-1 finding.

M2 is also adequately fixed. The previous regex-based comment stripping failure mode is gone: `BodyHasher.StripVbaComments` is now a single-pass stateful walker that tracks whether it is inside a VBA string literal, preserves doubled-quote escapes, treats apostrophes as comments only outside strings, and recognizes `REM` only at statement-start positions with a boundary check. The added tests cover the exact failure shapes called out in round 1: apostrophes inside strings, `REM` as string content, embedded `""`, and normal trailing apostrophe comments. The implementation and the tests line up, so the original hash-corruption concern is resolved.

M3 is substantially improved and the original round-1 complaint is adequately addressed. The suite now pins the exact seven `m_lng*`/`m_str*` module variables, the exact two local `Dim` declarations in `cboCompanyTypeID_BeforeUpdate`, the literal `GetString` signature string, the named dead-code detector cases, deferred-module non-parsing, deterministic MD5 canonical selection, and foreign-key enforcement. That is enough to close the prior “materially weaker than spec” finding as stated. The remaining items I am flagging below are new logic/reporting issues uncovered in round 2, not persistence of the original round-1 weakness.

On a brief re-check of the rest of the Session 1 bar: the schema migrator generally matches the Session Set schema; persistence is transactional and now enforces FKs; deferral prefiltering correctly records deferred modules and skips parsing; MD5 sibling tagging is deterministic; dead-code detection covers the spec-named shapes and the added tests pass; and build/test/artifact generation are clean and deterministic per the reported `dotnet build`/`dotnet test` results. I did, however, find one major symbol-keying problem and one minor measurement/reporting problem. First, property procedures are keyed only as `<module>.<name>` in `SymbolDbBuilder`, so a normal VBA `Property Get Foo` plus `Property Let Foo`/`Property Set Foo` pair would collide on the `Symbols.fqn` primary key and `FindResolverProcedure` would also resolve them ambiguously by name only. Second, local `Dim` symbols are intentionally stored as `kind='module_var'`, but the statistics and measurement report count all `module_var` rows as module variables without filtering `declared_in_fqn IS NULL`, so the module-variable totals/per-module breakdown will overcount by including procedure locals.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs",
      "summary": "Property Get/Let/Set procedures with the same VBA property name are persisted under the same `Symbols.fqn`, which makes paired property accessors collide and dead-code lookup ambiguous.",
      "fix": "Encode accessor kind into the persisted property FQN (for example with a `#get`/`#let`/`#set` suffix) and update resolver-declaration matching so property symbols are identified by name plus accessor kind rather than name alone."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs / AccessHarvester.SymbolDb/Reporting/MeasurementReportBuilder.cs",
      "summary": "Session 1 statistics and the measurement report count procedure-local `Dim` rows as module variables because all `kind='module_var'` symbols are aggregated together.",
      "fix": "When computing module-variable metrics, count only rows with `kind='module_var'` and `declared_in_fqn IS NULL`, or introduce a distinct local-variable kind for reporting purposes."
    }
  ]
}
```

---

# Verification Round 3

The `(M-property-fqn-collision)` fix holds. In `AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs`, `ExtractProcedure` now assigns property accessor FQNs as `<module>.<name>#get|#let|#set` while leaving `Sub` and `Function` procedures on the unsuffixed `<module>.<name>` form. That directly removes the prior `Symbols.fqn` primary-key collision for property triplets sharing one VBA property name. The resolver-side ambiguity is also addressed in the same file: `FindResolverProcedure` now takes the AST procedure node, detects when the AST declaration is a `PropertyProcedure`, and requires a matching resolver `PropertyDeclaration.Kind` before returning the declaration used for cross-reference lookup and dead-procedure classification. That means the Get/Let/Set declarations are no longer collapsed back together during resolver matching. The regression test `PersistenceAndDedupTests.Build_PropertyTriplet_PersistsAccessorsUnderDistinctFqns` explicitly verifies the three expected persisted FQNs (`clsBag.Item#get`, `clsBag.Item#let`, `clsBag.Item#set`) and round-trips them through SQLite, which is the exact failure mode from Round 2. With build/test green and that targeted persistence test present, this fix is verified.

The `(N-module-var-stats-overcount)` fix also holds. In `AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs`, `ComputeStatistics` now computes `ModuleVarCount` with `s.Kind == "module_var" && s.DeclaredInFqn is null`, which correctly limits the headline count to module-scope variables only. That separation is supported by the extraction model itself: module-level declarations created by `ExtractModuleVariable` set `DeclaredInFqn: null`, while procedure-local declarations created by `LocalVariableExtractor` set `DeclaredInFqn` to the enclosing procedure FQN. The reporting layer was updated consistently in `AccessHarvester.SymbolDb/Reporting/MeasurementReportBuilder.cs`, where the per-module breakdown now computes `Module vars` and `Local vars` as separate columns using the same `DeclaredInFqn` null/non-null split. The regression test `PersistenceAndDedupTests.Statistics_ModuleVarCount_ExcludesProcedureLocals` pins the corrected statistics behavior on a synthetic module with one module variable and two local `Dim`s, asserting `ModuleVarCount = 1` while still confirming the locals remain present in `Symbols`. The regenerated `tools/symbol-db/measurement-northwind.md` reflects that split as well, showing `Module variables | 7` in totals and separate per-module `Module vars` vs `Local vars` columns. This fix is therefore verified.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
