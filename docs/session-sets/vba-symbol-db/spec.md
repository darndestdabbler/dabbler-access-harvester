# VBA Symbol DB Spec

> **Purpose:** Persist the harvester's already-extracted resolver / classification / form output into a queryable SQLite symbol database, then use it as the substrate for an AI-interpretation pipeline that fills the existing `Purpose` / `Intent` / `Notes` / `Classification` slots so a downstream AI agent has a well-indexed, leaves-first-interpreted artifact for re-implementing the Access app on another platform.
> **Created:** 2026-04-28
> **Session Set:** `docs/session-sets/vba-symbol-db/`
> **Prerequisite:** `vba-symbol-resolution-and-enrichment` is **superseded** by this set (see its `change-log.md` for the why); the resolver chain it would have built on (`resolver-declarations-and-scopes`, `resolver-references-and-with-blocks`, `resolver-default-members-and-inference`, `access-resolver-symbol-table-integration`) is already complete.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. Quality bar is build + tests + cross-provider AI verification per the harvester's standard. The comprehension probe in Session 5 is *not* human UAT — it is an AI-versus-AI judgment call that the human adjudicates inline at session close.

---

## Project Overview

### What this set delivers

A SQLite database (`tools/symbol-db/symbol-db.sqlite` — checked in) that holds, for one Access database (`samples/NorthwindDevEdition.accdb` as the canonical fixture):

1. Every VBA symbol — procedures, functions, properties, module variables, enum declarations, enum values — keyed by fully-qualified name.
2. Every reference edge (caller FQN → callee FQN with call-site location) and the inverse caller-of relationship.
3. Every form/report control mapped to its bound table/query and column, plus form-level event bindings (with macros normalized into the same shape as VBA-bound events when possible).
4. Mechanical dead-code and dead-object detection so the AI interpretation pipeline never spends tokens on artifacts that are not live.
5. MD5-hash equivalence detection so duplicated procedures (a common Access pattern) get interpreted once, with the interpretation copied to all hash-equivalent twins.
6. AI-written interpretations (purpose, justification, confidence) for every live, non-duplicate symbol, populated leaves-first so each interpretation prompt can lean on its callees' interpretations rather than re-walking source.
7. A comprehension probe that takes a representative target procedure, sends three providers the symbol-table view *minus* the target's interpretation, and asks each to reconstruct what the procedure does. Human adjudicates. Outcome drives the go/no-go for the migration-readiness review.

### What this set does NOT do

- Does not build any new parser, resolver, classifier, or extractor. The harvester already has these, fully built and tested (see Inventory below).
- Does not modify the existing `raw → kb → ir` tier model. The symbol DB is a *fourth* derived artifact downstream of `ir`.
- Does not generate target-platform code (Blazor, C#, etc.). That is the migration tooling's concern; the symbol DB is the *input* to that tooling, not an output of it.
- Does not interpret deferred (likely-superseded) artifacts, dead code, or hash-equivalent duplicates. Those are surfaced explicitly so the migration tooling and the human know what was skipped and why.
- Does not run on every committed sample DB in this set. `samples/NorthwindDevEdition.accdb` is the canonical fixture; HomeHealthCare and the other samples are deferred to a possible follow-up scaling set.

### Why this shape

Two principles, both validated in conversation 2026-04-28:

1. **Spend AI tokens on judgment, not lookups.** A flat indexed symbol table replaces per-prompt graph-walking with one-shot SQL queries at packet-build time.
2. **Interpret leaves first.** A 10-deep call chain interpreted bottom-up costs exponentially less than the same chain interpreted in isolation, because each non-leaf prompt summarizes its callees instead of inlining their source.

Combined: 30–50% reduction in AI calls is the conservative estimate for a representative DB after deferral + dead-code + MD5 dedup + leaves-first ordering land. Session 1 measures the actual numbers for `NorthwindDevEdition.accdb`.

---

## Inventory of existing assets (already built, this set consumes)

This set is small because the substrate is large. Confirmed present at spec authoring time:

| Asset | Location | What it produces |
|---|---|---|
| ANTLR-driven VBA parser | `AccessHarvester.Vba.Parser/` | Full `Module → Procedure → Statement → Expression` AST including `MemberAccessExpression`, `BangAccessExpression`, `IndexOrCallExpression`. Three parse modes (Strict / Recovering / Lossy) with diagnostics. |
| Resolver — declarations + scopes | `AccessHarvester.Vba.Resolver/Declarations/`, `Scopes/` | `Declaration` model (Variable, Constant, Procedure, Type, Enum, Event, Class, Parameter, Declare, Implicit), scope tree (Project / Module / Procedure / WithBlock / ForEach), `ScopeBuildingVisitor`. |
| Resolver — references + with-block flattening | `AccessHarvester.Vba.Resolver/References/` | `ReferenceResolver`, `ResolvedReference`, `ResolvedModule`, `ResolutionOutcome` (closed union: `Resolved` / `ResolvedToObjectModel` / `ResolvedToBuiltIn` / `ResolvedToMe` / `ResolvedViaWithFlattening` / `Unresolved` with reason codes). |
| Resolver — default members + type inference | `AccessHarvester.Vba.Resolver/DefaultMembers/`, `Types/` | Default-member registry, type inference pass with confidence votes. |
| Cross-reference index | `AccessHarvester.Vba.Resolver/CrossReference/` | `CrossReferenceIndex.GetReferencesTo(decl)` — caller-of lookup. |
| Built-in function registry | `AccessHarvester.Vba.Resolver/References/BuiltInFunctionRegistry.cs` | Curated VBA built-ins (Format, IIf, Nz, DLookup, DCount, etc.) with categories and return types. |
| Access object-model integration | `AccessHarvester.Vba.Resolver/AccessIntegration/` | Synthetic declarations for forms / reports / controls injected into the resolver, so `Me.cboFoo` and `Forms!frmBar!ctl` resolve through the same scope-walking machinery as in-source declarations. |
| Function classification | `AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs` + Builder | Per-procedure: kind, public/static, parameter count, side-effect categories (Pure / DataAccess / UIBound / ExternalIO / LateBound), `IsMixed`, complexity metrics, direct + transitive dependencies, callers, unresolved references. |
| Event handler classification | `AccessHarvester.Pipeline/Enrichment/EventHandlerClassification.cs` | Same shape, scoped to event handlers; populates `EventHandlerReference.Classification`. |
| Navigation graph | `AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs` | `DoCmd.OpenForm` / `OpenReport` / `Close` / `BrowseTo` edges + ribbon / switchboard / macros / default-form entry points. |
| Form component spec | `AccessHarvester.Pipeline/Enrichment/FormComponentSpec.cs` | Per-form: sections, controls (with bound table/column resolved when statically determinable), subforms, event handlers, validation rules. **`Purpose` / `Intent` / `Notes` slots are explicitly null at the `ir/` tier, reserved for the AI consumer.** |
| Row source spec | `AccessHarvester.Pipeline/Enrichment/RowSourceSpec.cs` + Builder | Per-control row sources with cascading-driver cross-references. |
| Query abstraction | `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs` + Builder | Per-query: SELECT list, FROM, WHERE, GROUP BY, etc. typed as AST nodes via the SQL parser. |
| Validation rule extractor | `AccessHarvester.Pipeline/Enrichment/ValidationRuleExtractor.cs` | Design-time + BeforeUpdate-handler validation rules. |
| Form reference resolver (in SQL) | `AccessHarvester.Sql.Parser/FormReferences/` | Resolves `Forms!frmFoo!cboBar` references appearing inside SQL strings to concrete control declarations. |
| Macro XML parser | `AccessHarvester.Core/Enrichment/Macros/MacroXmlParser.cs` | Parses Access macro XML into action sequences. |
| Unresolved item aggregator | `AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs` | Project-wide rollup of everything the resolver could not bind. |
| Enrichment pipeline runner | `AccessHarvester.Pipeline/Enrichment/EnrichmentPipelineRunner.cs` | Stitches the per-form enrichers together. |
| Test footprint | `tests/` | 1148 tests passing across resolver / parser / grammar / harvester. |

**Implication for this set:** every column we want in the symbol DB except the AI-interpretation columns is already produced in memory by the existing pipeline. This set is plumbing (Sessions 1–3) plus the AI interpretation pass that has been deliberately deferred (Session 4) plus the comprehension gate (Session 5).

---

## Concrete fixture and probe target

- **Canonical fixture:** `samples/NorthwindDevEdition.accdb`. Already extracted to `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/` (raw + kb + ir). Already used as the test corpus by the resolver-* and enrichment-* sets.
- **Probe target (Session 5):** `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` and the 12 identifiers it directly references (7 module-level `Private m_lng*` / `m_str*` variables, 2 local `Dim` strings, `CompanyIsActive`, `GetString`, `sCompanyDelete_CompanyTypeChange`).

The probe target was chosen because the human knows its behavior cold and can adjudicate AI interpretations against ground truth without re-deriving them.

---

## SQLite schema (target shape; Session 1 confirms / refines)

Authoritative store; checked in. Audit trail via `InterpretationHistory` and `BuildRuns`. Diff strategy: don't rely on git diff of the binary; rely on session-set commits + the audit tables (queryable: "what did this run change?").

Notation: parallel-column denormalization on `Symbols` for up to three opinions, per the human's database-design call.

```sql
-- ── Module & symbol inventory ─────────────────────────────────────

CREATE TABLE Modules (
  fqn TEXT PRIMARY KEY,             -- 'Form_frmCompanyDetail' / 'modStrings'
  kind TEXT NOT NULL,               -- 'Module' | 'ClassModule' | 'Form' | 'Report'
  source_path TEXT NOT NULL,
  encoding TEXT NOT NULL,           -- 'ascii' | 'utf-16-le'
  is_deferred INTEGER NOT NULL DEFAULT 0,
  deferred_reason TEXT,
  deferred_companion_fqn TEXT
);

CREATE TABLE Symbols (
  fqn TEXT PRIMARY KEY,             -- 'Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate'
  module_fqn TEXT NOT NULL REFERENCES Modules(fqn),
  name TEXT NOT NULL,               -- 'cboCompanyTypeID_BeforeUpdate'
  kind TEXT NOT NULL,               -- 'sub' | 'function' | 'property_get' | 'property_let' | 'property_set'
                                    -- 'enum_decl' | 'enum_value' | 'module_var' | 'module_const'
                                    -- 'event_decl' | 'declare_stmt'
  access TEXT,                      -- 'Public' | 'Private' | 'Friend' | NULL (Unspecified)
  data_type TEXT,                   -- declared type or function return type
  declared_in_fqn TEXT,             -- enclosing scope FQN, NULL for module-level
  source_line_start INTEGER,
  source_line_end INTEGER,
  source_body TEXT,                 -- raw VBA source, no escaping (point 2 of human's case for SQLite)
  signature_string TEXT,            -- 'Function(ID:enumStrings:ByVal,params:Variant():ByRef:ParamArray):String'
  body_hash_md5 TEXT,               -- normalized-body MD5 (whitespace collapsed, comments stripped, line numbers stripped)
  is_dead INTEGER NOT NULL DEFAULT 0,
  dead_code_reason TEXT,            -- 'no-callers; not-event-handler; not-entry-point' / 'after-unconditional-exit' / etc.
  equivalence_canonical_fqn TEXT,   -- when this symbol is a hash-twin, point at the chosen canonical
  -- AI interpretation: parallel triplets, denormalized for easy side-by-side QA queries
  Interpretation1 TEXT,
  Justification1 TEXT,
  Confidence1 REAL,                 -- 0.0 to 1.0
  Agent1 TEXT,                      -- model id
  InterpretedAt1 TEXT,              -- ISO timestamp
  Interpretation2 TEXT,
  Justification2 TEXT,
  Confidence2 REAL,
  Agent2 TEXT,
  InterpretedAt2 TEXT,
  Interpretation3 TEXT,
  Justification3 TEXT,
  Confidence3 REAL,
  Agent3 TEXT,
  InterpretedAt3 TEXT,
  human_review_required INTEGER NOT NULL DEFAULT 0,
  human_override_interpretation TEXT  -- when set, overrides all three opinions
);
CREATE INDEX idx_symbols_module ON Symbols(module_fqn);
CREATE INDEX idx_symbols_kind ON Symbols(kind);
CREATE INDEX idx_symbols_dup ON Symbols(signature_string, body_hash_md5)
  WHERE signature_string IS NOT NULL AND body_hash_md5 IS NOT NULL;
CREATE INDEX idx_symbols_dead ON Symbols(is_dead) WHERE is_dead = 1;

-- ── Procedure parameters ──────────────────────────────────────────

CREATE TABLE Parameters (
  procedure_fqn TEXT NOT NULL REFERENCES Symbols(fqn),
  position INTEGER NOT NULL,
  name TEXT NOT NULL,
  data_type TEXT,
  passing TEXT,                     -- 'ByVal' | 'ByRef'
  is_optional INTEGER NOT NULL DEFAULT 0,
  is_paramarray INTEGER NOT NULL DEFAULT 0,
  is_array INTEGER NOT NULL DEFAULT 0,
  default_value TEXT,
  PRIMARY KEY (procedure_fqn, position)
);

-- ── Reference edges ───────────────────────────────────────────────

CREATE TABLE [References] (
  id INTEGER PRIMARY KEY,
  from_fqn TEXT NOT NULL,           -- containing symbol FQN
  to_fqn TEXT,                      -- resolved target FQN, NULL if unresolved
  identifier TEXT NOT NULL,         -- as written in source
  kind TEXT NOT NULL,               -- 'call' | 'variable_read' | 'variable_write' | 'enum_value_use' | 'member_access' | 'control_access'
  caller_kind TEXT,                 -- 'ui-event-handler' | 'non-ui' (joined from EventBindings + EventHandlerClassification)
  source_line INTEGER,
  source_column INTEGER,
  resolution_status TEXT NOT NULL,  -- 'resolved' | 'resolved-via-with-flattening' | 'resolved-to-object-model'
                                    -- 'resolved-to-built-in' | 'resolved-to-me' | 'unresolved'
  resolution_reason TEXT            -- when unresolved, the resolver's UnresolvedReasons code
);
CREATE INDEX idx_refs_from ON [References](from_fqn);
CREATE INDEX idx_refs_to ON [References](to_fqn);
CREATE INDEX idx_refs_unresolved ON [References](resolution_status) WHERE resolution_status = 'unresolved';

-- ── Member-access expressions (Me.X, Forms!Y.Z, etc.) ─────────────

CREATE TABLE MemberAccesses (
  id INTEGER PRIMARY KEY,
  from_fqn TEXT NOT NULL,
  receiver TEXT NOT NULL,           -- 'Me' | 'Forms' | 'Reports' | 'Application' | <var-fqn>
  path TEXT NOT NULL,               -- 'cboStatus' | 'frmFoo!SomeControl' | 'subfrmDetails.Form!txtField'
  resolved_target_fqn TEXT,
  source_line INTEGER
);

-- ── Form / control / event tables ─────────────────────────────────

CREATE TABLE FormControls (
  form_fqn TEXT NOT NULL REFERENCES Modules(fqn),
  control_name TEXT NOT NULL,
  control_type TEXT NOT NULL,       -- 'TextBox' | 'ComboBox' | 'Button' | ...
  label_text TEXT,
  control_source TEXT,              -- bound field name (raw)
  row_source TEXT,                  -- query/SQL backing combo/list
  bound_table_or_query TEXT,        -- resolved bound table/query when statically determinable
  bound_column TEXT,                -- resolved bound column name
  PRIMARY KEY (form_fqn, control_name)
);

CREATE TABLE EventBindings (
  form_fqn TEXT NOT NULL REFERENCES Modules(fqn),
  control_name TEXT,                -- NULL for form-level events
  event_name TEXT NOT NULL,         -- 'BeforeUpdate' | 'OnClick' | 'AfterUpdate' | ...
  handler_kind TEXT NOT NULL,       -- 'vba' | 'macro' | 'expression' | 'none'
  handler_fqn TEXT,                 -- VBA: Symbols.fqn; Macro: Macros.fqn; Expression: NULL
  handler_expression TEXT,          -- when handler_kind='expression', the literal text
  PRIMARY KEY (form_fqn, control_name, event_name)
);

-- ── Macros (when handler_kind='macro' on EventBindings) ───────────

CREATE TABLE Macros (
  fqn TEXT PRIMARY KEY,
  source_xml TEXT NOT NULL,
  is_dead INTEGER NOT NULL DEFAULT 0,
  -- Same Interpretation1/2/3 + Justification + Confidence + Agent + InterpretedAt
  -- triplets as Symbols, plus human_override.
  Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL, Agent1 TEXT, InterpretedAt1 TEXT,
  Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL, Agent2 TEXT, InterpretedAt2 TEXT,
  Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL, Agent3 TEXT, InterpretedAt3 TEXT,
  human_review_required INTEGER NOT NULL DEFAULT 0,
  human_override_interpretation TEXT
);

CREATE TABLE MacroActions (
  macro_fqn TEXT NOT NULL REFERENCES Macros(fqn),
  position INTEGER NOT NULL,
  action_name TEXT NOT NULL,        -- 'OpenForm' | 'SetValue' | 'RunCode' | ...
  arguments_json TEXT,
  PRIMARY KEY (macro_fqn, position)
);

-- ── Enums ─────────────────────────────────────────────────────────

CREATE TABLE EnumValues (
  enum_value_fqn TEXT PRIMARY KEY REFERENCES Symbols(fqn),
  enum_fqn TEXT NOT NULL,
  numeric_value INTEGER
  -- Note: runtime_string_text deliberately NOT included.
  -- Per human's call 2026-04-28: NorthwindDevEdition uses a Strings table to back
  -- localized messages, but very few Access apps do this; including the column
  -- would be DB-specific noise. The interpreter sees only the enum-value name
  -- and numeric value; it can infer "this is a key into something named
  -- enumStrings, used to gate a localized error message" from the call shape.
);

-- ── Tables (lookup-table verdict only; row data NOT stored) ───────

CREATE TABLE Tables (
  fqn TEXT PRIMARY KEY,
  primary_key_column TEXT,
  candidate_label_column TEXT,
  is_lookup_table_score INTEGER,    -- 0-100 confidence (heuristic, not over-tuned per human's 2026-04-28 call)
  is_lookup_table_reason TEXT,      -- 'small-table; PK + text label; bound by N controls'
  Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL, Agent1 TEXT, InterpretedAt1 TEXT,
  Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL, Agent2 TEXT, InterpretedAt2 TEXT,
  Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL, Agent3 TEXT, InterpretedAt3 TEXT
);

-- ── Queries ───────────────────────────────────────────────────────

CREATE TABLE Queries (
  fqn TEXT PRIMARY KEY,
  source_sql TEXT NOT NULL,
  normalized_sql TEXT,              -- whitespace collapsed, comments stripped, FROM-clause canonicalized
  sql_text_hash TEXT,               -- md5 of normalized_sql
  sql_clause_hash TEXT,             -- md5 of canonical clause structure (option b in conversation)
  is_dead INTEGER NOT NULL DEFAULT 0,
  equivalence_canonical_fqn TEXT,
  Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL, Agent1 TEXT, InterpretedAt1 TEXT,
  Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL, Agent2 TEXT, InterpretedAt2 TEXT,
  Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL, Agent3 TEXT, InterpretedAt3 TEXT
);
CREATE INDEX idx_queries_text_hash ON Queries(sql_text_hash);
CREATE INDEX idx_queries_clause_hash ON Queries(sql_clause_hash);

CREATE TABLE QueryColumns (
  query_fqn TEXT NOT NULL REFERENCES Queries(fqn),
  position INTEGER NOT NULL,
  output_name TEXT NOT NULL,
  expression_text TEXT,
  bound_table TEXT,                 -- when the column is a direct field reference
  bound_column TEXT,
  PRIMARY KEY (query_fqn, position)
);

-- ── Audit ─────────────────────────────────────────────────────────

CREATE TABLE BuildRuns (
  run_id INTEGER PRIMARY KEY,
  started_at TEXT NOT NULL,
  finished_at TEXT,
  orchestrator TEXT NOT NULL,       -- 'claude-code' | 'codex' | 'gemini-cli'
  session_set TEXT NOT NULL,
  session_number INTEGER NOT NULL,
  symbols_written INTEGER,
  references_written INTEGER,
  interpretations_written INTEGER,
  total_routed_cost_usd REAL,
  notes TEXT
);

CREATE TABLE InterpretationHistory (
  id INTEGER PRIMARY KEY,
  fqn TEXT NOT NULL,                -- target Symbol/Macro/Table/Query FQN
  fqn_kind TEXT NOT NULL,           -- 'symbol' | 'macro' | 'table' | 'query'
  opinion_slot INTEGER NOT NULL,    -- 1, 2, or 3 (which parallel column was written)
  interpretation TEXT NOT NULL,
  justification TEXT,
  confidence REAL,
  agent TEXT NOT NULL,
  prompt_hash TEXT,
  source_hash_at_time TEXT,
  written_at TEXT NOT NULL,
  build_run_id INTEGER REFERENCES BuildRuns(run_id),
  reason TEXT                       -- 'first-opinion' | 'low-confidence-escalation' | 'human-flagged' | 'copied-from-md5-twin'
);
```

### Confidence-thresholded escalation (Session 4 policy)

Per the human's 2026-04-28 call:

- Run opinion 1 on every live, non-duplicate symbol.
- If `Confidence1 < 0.75`, run opinion 2.
- If `(Confidence1 + Confidence2) / 2 < 0.70`, run opinion 3.
- Thresholds are configurable via `tools/symbol-db/interpretation-config.json` (also overridable per-symbol via `Symbols.human_review_required`).

### Multi-confidence calibration note

Models are notoriously poor at self-confidence calibration. The Confidence column is supplemented by a required `Justification` field that includes a confidence-explanation half ("high confidence because X, Y, Z"). Spot-checks against justifications over time inform per-model confidence-trust adjustments; that's recorded in `InterpretationHistory.reason` for samples that get human-reviewed.

---

## Session Plan

> Five sessions. The orchestrator running each session must elaborate the
> bullet hints below — including the mandatory Step 6 (build + test) and
> Step 8 (cross-provider verification) per `docs/ai-led-session-workflow.md`.

### Session 1 of 5: Symbols Table + Dead-Code / Dead-Object / MD5 Detection + Measurement

**Steps:**

1. Read `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`, `docs/planning/session-set-authoring-guide.md`. **Read the new lessons added at the close of `vba-symbol-resolution-and-enrichment` retirement** (orchestrator: inventory before design; human: review output sooner). They are direct prerequisites for not repeating the false start.
2. Re-confirm the inventory in this spec is current (the existing assets table). If anything has changed since spec authoring, log a deviation and proceed.
3. Design (in code, no AI route call needed for synthesis-of-existing-shapes) the SQLite schema migration that creates the tables defined above. Build with `Microsoft.Data.Sqlite` from a new `AccessHarvester.SymbolDb/` C# project.
4. Implement the `Modules` and `Symbols` extractors that walk the existing `ResolvedModule` instances for `Form_frmCompanyDetail` and `modStrings` and write rows. Includes:
   - Module encoding detection (UTF-16 LE BOM for forms; ASCII for modules).
   - Source-body capture from the parser's source-range info.
   - `signature_string` derivation (parameters with passing/paramarray/array/qualified-type encoded).
   - `body_hash_md5` after normalization (collapse whitespace, strip comments, strip leading line numbers).
5. Implement dead-code detection within procedures (statements after unconditional `Exit`, unconditional `GoTo`, `If False Then`, etc.) and dead-procedure detection (no callers in `CrossReferenceIndex`, not in `EventBindings`, not in `NavigationGraphInputs`).
6. Implement the `DeferredArtifacts` pre-filter pass: name-pattern match for `Old` / `_Old` / `Old_` / `-Old-` / etc. with companion-existence check; populate `Modules.is_deferred` / `deferred_reason` / `deferred_companion_fqn`. **Deferred modules are not parsed.**
7. Implement MD5-hash duplicate detection (self-join on `(signature_string, body_hash_md5)`) and populate `equivalence_canonical_fqn` for non-canonical members.
8. **Measurement deliverable:** generate `tools/symbol-db/measurement-northwind.md` reporting:
   - Total Modules / Symbols / dead-symbol count / deferred-module count / MD5-duplicate cluster count.
   - Estimated AI-call savings vs. naive (interpret-everything-once) baseline.
   - Per-module breakdown for `Form_frmCompanyDetail` and `modStrings`.
9. Hermetic xUnit tests in `tests/AccessHarvester.SymbolDb.Tests/`:
   - `Form_frmCompanyDetail` and `modStrings` parse → 24 procedures + 7 module variables + 1 enum (49 values) + 4 functions + 0 module variables in `modStrings` (the human can adjust if measurement reveals otherwise).
   - `cboCompanyTypeID_BeforeUpdate` parameters: `Cancel As Integer ByRef`.
   - `GetString` parameters: `ID As enumStrings ByVal`, `params() As Variant ByRef ParamArray`.
   - The seven `m_lng*` and `m_str*` declarations land as `module_var` rows with `declared_in_fqn = NULL`.
   - The two local Dims (`strDlgMsg`, `strOpenArgs`) land with `declared_in_fqn = 'Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate'`.
   - At least one MD5-duplicate cluster is detected somewhere in the canonical fixture (if measurement shows zero, document and proceed).
   - At least one deferred-artifact case (if NorthwindDevEdition has no `Old` companions, document and disable the assertion).
10. `dotnet build && dotnet test`.
11. Cross-provider verification per workflow Step 6.

**Creates:**
- `AccessHarvester.SymbolDb/` (new C# project)
- `tools/symbol-db/symbol-db.sqlite` (the build artifact, checked in)
- `tools/symbol-db/measurement-northwind.md` (Session 1 measurement report)
- `tests/AccessHarvester.SymbolDb.Tests/` (test project)

**Touches:**
- `AccessHarvester.sln` (add new projects)

**Ends with:** `dotnet test` passes (target: 1148 + new tests, all green); the SQLite db exists with `Modules`, `Symbols`, `Parameters`, `DeferredArtifacts`, dead-code flags, MD5 dedup populated for `Form_frmCompanyDetail` + `modStrings`; the measurement report quantifies expected AI-cost savings.

**Progress keys:** `session-1/sqlite-schema`, `session-1/symbols-extractor`, `session-1/dead-code-detector`, `session-1/deferral-prefilter`, `session-1/md5-dedup`, `session-1/measurement`, `session-1/build-test`, `session-1/verification`.

---

### Session 2 of 5: References + Member Accesses + Caller-Kind Annotation

**Steps:**

1. Required reading.
2. Implement the `[References]` writer that walks every `ResolvedModule.References` and writes rows. Includes:
   - Resolution status mapping (closed union to text codes).
   - `from_fqn` derivation from the enclosing scope of the reference site.
   - `to_fqn` from the bound declaration when applicable.
3. Implement the `MemberAccesses` writer specialized for `MemberAccessExpression` / `BangAccessExpression` / `ImplicitWithMemberExpression` / `ImplicitWithBangExpression` AST nodes — `Me.X`, `Forms!Y.Z`, `subfrmDetails.Form!txtField`, etc. Resolves `resolved_target_fqn` when the resolver's outcome contains a bound `Declaration`.
4. Populate `[References].caller_kind` by joining against the existing `EventHandlerClassification` and `EventBindings` data: `'ui-event-handler'` if `from_fqn` is in `EventBindings.handler_fqn` for any form-control event; `'non-ui'` otherwise.
5. Hermetic tests:
   - `cboCompanyTypeID_BeforeUpdate`'s reference set contains `CompanyIsActive` (resolved-local), `GetString` (resolved-cross-module), `sCompanyDelete_CompanyTypeChange` (resolved-cross-module enum value), `m_lngVendorProductCount` (resolved-module-var), `strDlgMsg` (resolved-local), `DoCmd` (resolved-to-object-model or resolved-to-built-in depending on registry shape), `Me` (resolved-to-me), `IsFormOpen` (probably resolved-cross-module if defined in a registered helper module; otherwise unresolved with a tracked reason).
   - Reverse-lookup: `CompanyIsActive` has at least one caller (`cboCompanyTypeID_BeforeUpdate`, plus others in the same form).
   - `caller_kind = 'ui-event-handler'` for `cboCompanyTypeID_BeforeUpdate`'s reference rows.
6. `dotnet build && dotnet test`.
7. Cross-provider verification.

**Creates:** Reference and MemberAccess writer code + tests.
**Touches:** Symbol DB build pipeline.
**Ends with:** All references and member-access expressions for `Form_frmCompanyDetail` and `modStrings` are populated; the probe target's reference list matches the expected set.
**Progress keys:** `session-2/references-writer`, `session-2/member-accesses-writer`, `session-2/caller-kind-annotation`, `session-2/build-test`, `session-2/verification`.

---

### Session 3 of 5: Column Mappings (Form Controls + Event Bindings + Queries) + Macro Normalization

**Steps:**

1. Required reading.
2. Implement the `FormControls` writer from existing `FormComponentSpec` + `RowSourceSpec` data. Every control is either bound (resolved `bound_table_or_query` + `bound_column`) or unbound (NULL). Subform controls produce nested-FQN rows.
3. Implement the `EventBindings` writer:
   - VBA-bound events: `handler_kind = 'vba'`, `handler_fqn` points at a `Symbols` row.
   - Macro-bound events: `handler_kind = 'macro'`, `handler_fqn` points at a `Macros` row (populated in step 4).
   - Expression-bound events (rare): `handler_kind = 'expression'`, `handler_expression` carries the literal text.
4. Implement the `Macros` + `MacroActions` writers from `MacroXmlParser` output. Walk every macro in the DB (per human's call: extract all macros up front; iteration cost is the same as event-bound-only and the data shape is uniform).
5. Implement the `Queries` + `QueryColumns` writers from existing `QueryAbstraction` data. Includes:
   - `normalized_sql` and `sql_text_hash` (option a from the conversation: same-FROM + same-length MD5 sniff).
   - `sql_clause_hash` (option b: clause-by-clause AST structural hash; reordering-tolerant for SELECT lists and AND/OR conjuncts).
   - Self-join detection of `equivalence_canonical_fqn` for queries.
   - `is_dead = 1` for queries with no callers anywhere (no form `RowSource` references, no VBA references via `DLookup`/etc. literal-string analysis where statically determinable, no macro `OpenQuery` references).
6. Implement the `Tables` writer:
   - PK detection from existing DAO metadata.
   - `candidate_label_column` heuristic (column-name contains Value/Text/Label/Caption/Name/Description; type is text/memo; UNIQUE).
   - `is_lookup_table_score` heuristic: small `RecordCount` (≤200) + has PK + has candidate text column + is `RowSource` of N controls. Score is 0-100; not over-tuned per human's 2026-04-28 call.
7. Hermetic tests:
   - `cboCompanyTypeID` on `Form_frmCompanyDetail`: `bound_table_or_query = 'CompanyTypes'` (or whatever the actual lookup table is named), `bound_column = 'CompanyTypeID'`.
   - The form has at least one `EventBindings` row for `cboCompanyTypeID.BeforeUpdate` with `handler_kind = 'vba'`, `handler_fqn = 'Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate'`.
   - At least one Northwind macro is parsed and normalized into Macro+MacroActions rows.
   - At least one query duplicate (text-hash or clause-hash) is detected; if zero, document.
   - At least one lookup table is detected with score ≥ 70 (likely: `CompanyTypes`, `EmployeeStatuses`, etc.).
8. `dotnet build && dotnet test`.
9. Cross-provider verification.

**Creates:** Form control, event binding, macro, query, table writer code + tests.
**Touches:** Symbol DB build pipeline.
**Ends with:** The probe target's surrounding context (the form's controls, the event binding that fires the procedure, the lookup tables it indirectly depends on) is fully resolved in the DB.
**Progress keys:** `session-3/form-controls-writer`, `session-3/event-bindings-writer`, `session-3/macro-writer`, `session-3/query-writer`, `session-3/table-writer`, `session-3/build-test`, `session-3/verification`.

---

### Session 4 of 5: AI Interpretation Pipeline (Leaves-First; Confidence-Thresholded)

**Steps:**

1. Required reading.
2. Build the Python interpretation runner under `tools/symbol-db/interpret/`. Reads from the SQLite db; writes Interpretation/Justification/Confidence/Agent/InterpretedAt columns and `InterpretationHistory` rows.
3. Topological sort of the symbol graph (leaves-first) using existing `CrossReferenceIndex` data. Cycles broken by interpreting cycle members in their as-written form first (no callee summaries available); their interpretations are tagged "first-pass; cycle member" in `InterpretationHistory.reason`.
4. Prompt template per symbol kind (`tools/symbol-db/interpret/prompts/`):
   - **Function/Sub:** purpose, behavior summary, inputs/outputs, side-effect categories (cross-checked against existing `FunctionClassification`), preconditions, error modes.
   - **Property:** what it gets/sets, invariants, side effects.
   - **Module variable:** what it represents, who reads/writes it (joined from `[References]`).
   - **Enum value:** what it represents, where used, what callers do with it.
   - **Enum declaration:** what the enum models (a finite-domain something).
   - **Macro:** action sequence summary, what it accomplishes.
   - **Lookup table:** what the lookup represents (a finite domain of X).
   - **Query:** what the query produces, for what consumer.
5. The non-leaf prompt template includes summaries of each callee's interpretation (single sentence each, drawn from `Interpretation1` of the callee). It does **not** inline callee source bodies.
6. Confidence-thresholded escalation:
   - Always run opinion 1.
   - If `Confidence1 < 0.75`, run opinion 2 (different model from opinion 1; routed via the standard `route()` cross-provider rotation).
   - If average of opinions 1+2 confidence < 0.70, run opinion 3.
   - Per-symbol override via `Symbols.human_review_required`.
7. MD5-equivalent symbols: interpretation is **copied** from the `equivalence_canonical_fqn` row, with `InterpretationHistory.reason = 'copied-from-md5-twin'`.
8. Deferred + dead symbols: skipped explicitly with an InterpretationHistory record (`reason = 'skipped-deferred'` or `'skipped-dead'`) so future runs can detect when a previously-dead symbol becomes live (a new caller appeared).
9. Run the pipeline for `modStrings` first (pure leaves: enum, four utility functions). Then run for `Form_frmCompanyDetail` (which leans on `modStrings`'s now-populated interpretations).
10. Generate `tools/symbol-db/interpretation-summary-northwind.md`: total interpretations written, per-confidence distribution, escalation rate (how often opinion 2 / 3 fired), MD5-copy rate, total cost.
11. `dotnet build && dotnet test` (no new C# code expected; sanity check).
12. Cross-provider verification of the interpretation pipeline itself (not the interpretations — that's Session 5).

**Creates:** `tools/symbol-db/interpret/` (Python runner + prompt templates), `tools/symbol-db/interpretation-summary-northwind.md`.
**Touches:** `symbol-db.sqlite` (now populated with interpretations).
**Ends with:** Every live, non-duplicate symbol in the two probe modules has at least Interpretation1 populated; cost report shows the leaves-first + dedup savings empirically.
**Progress keys:** `session-4/runner-scaffold`, `session-4/prompt-templates`, `session-4/topological-leaves-first`, `session-4/confidence-escalation`, `session-4/md5-copy`, `session-4/dry-run-modstrings`, `session-4/run-form-frmcompanydetail`, `session-4/summary-report`, `session-4/build-test`, `session-4/verification`.

---

### Session 5 of 5: Comprehension Probe + Migration-Readiness Review

**Steps:**

1. Required reading.
2. Build the comprehension packet for `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` from a SQL query against the symbol DB:
   - The procedure's `Symbols` row (with `source_body` but with all three `Interpretation*` columns blanked).
   - All 12 directly-referenced symbols' rows, each WITH its `Interpretation1` populated.
   - The form's `FormControls` rows for the controls referenced.
   - The matching `EventBindings` row.
   - The lookup-table `Tables` row for whatever `cboCompanyTypeID` binds to.
3. Author the probe prompt: "Here is the symbol-table view for this procedure. The procedure's interpretation is intentionally omitted. Tell us what the procedure does, what its preconditions are, what side effects it has, and what UI behavior the user sees."
4. Route to three providers (Gemini Pro, GPT-5.4, and Opus in-conversation as the orchestrator). All three responses saved verbatim to `docs/session-sets/vba-symbol-db/probe-results/`.
5. Author a source-grounded reference description (the human's known-cold ground truth). The orchestrator drafts; the human can adjust before adjudication.
6. Human adjudicates each provider's response against the reference. Adjudication recorded inline in `probe-results/adjudication.md` with: per-provider correct-on / incorrect-on / missing-from / hallucinated-by points.
7. **Migration-readiness review:** for the same procedure, ask the same three providers "given this symbol-table state, write the equivalent C# / Blazor code." Saved to `migration-readiness/`. Compare for convergence; convergence = the symbol DB is doing its job; divergence = identifies what's missing. **Human adjudicates the gap, if any.**
8. Author `verdict.md` with the go/no-go recommendation for full-DB scaling (Phase 2 work, possible future set):
   - **GO:** symbol DB is sufficient for downstream interpretation; scale to all of NorthwindDevEdition + the other sample DBs; consider productionizing.
   - **NO-GO:** identify missing symbol-table coverage; either add it (small follow-up) or reconsider the approach entirely.
9. End-of-set deliverables (this is the last session):
   - `change-log.md` for the set.
   - Last-session next-set recommendation (route via `task_type="analysis"` per workflow).
   - Step 9 reorganization review (post-notify).
10. `dotnet build && dotnet test`.
11. Cross-provider verification.

**Creates:** `docs/session-sets/vba-symbol-db/probe-results/` (per-provider responses + adjudication), `docs/session-sets/vba-symbol-db/migration-readiness/`, `verdict.md`, `change-log.md`.
**Touches:** None.
**Ends with:** Human-adjudicated verdict on whether the symbol DB is fit for purpose; recommendation for next session set.
**Progress keys:** `session-5/packet-build`, `session-5/probe-route-three-providers`, `session-5/reference-description`, `session-5/adjudication`, `session-5/migration-readiness`, `session-5/verdict`, `session-5/change-log`, `session-5/next-set-recommendation`, `session-5/build-test`, `session-5/verification`, `session-5/step-9-reorganization`.

---

## Routing notes

- **Session 1–3** are mechanically driven (read existing in-memory shapes, write to SQLite). `route()` is needed only for session-verification at end of each session. Per the Delegation Discipline convention: extractor code under ~50 lines per file is direct; anything over is routed for review via `code-review`.
- **Session 4** is heavily routed by definition — every interpretation is a routed call. Use the standard `route(task_type="documentation")` per-symbol; the Python runner threads `session_set` and `session_number` for cost grouping.
- **Session 5** routes the three probe responses (`task_type="analysis"`, complexity_hint=70 to bypass cost caps; cross-provider verification still applies). The migration-readiness call is `task_type="documentation"` with `complexity_hint=70`.

## Cost expectations

Conservative estimate based on the 2026-04-28 cost-saving discussion:

- **Naive baseline** (interpret-everything-once on the full Northwind probe modules): ~80 symbols × ~$0.05/interpretation ≈ $4.00.
- **With deferral + dead-code + MD5 dedup + leaves-first ordering:** ~50 symbols × ~$0.04/interpretation ≈ $2.00.
- **With confidence-thresholded escalation to opinion 2/3 on ~20% of symbols:** ~$2.50.
- **Comprehension probe (Session 5):** 3 providers × ~$0.10 ≈ $0.30 + migration-readiness ~$0.30 ≈ $0.60.

**Total set estimate: $3–4 in routed AI cost**, plus orchestrator-side time. Adjustable up if the measurement in Session 1 shows the corpus is denser than estimated.

---

## Open questions for the human

None at spec authoring. Prior conversation (2026-04-28) closed all the design decisions (parallel-column denormalization, confidence-thresholded escalation, MD5 over alpha-equivalence, deferral pre-filter scope, no runtime string text in EnumValues, no row-data extraction, all macros up-front).
