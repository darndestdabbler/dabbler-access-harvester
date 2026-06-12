# Session 3 Review Findings — Parser Stack And Symbol DB

> **Set:** `006-solution-review-and-upstream-feedback-discovery`
> **Session:** 3 of 4 (2026-06-12)
> **Orchestrator:** Claude Code / Claude Fable 5 (review authored in-conversation
> per the spec's Orchestrator Assignment And Delegation Deviation section)
> **Scope reviewed:** `AccessHarvester.Vba.Parser` (VbaParserPipeline,
> VbaModuleBodyLoader, AstBuilder full, error strategy/listeners),
> `AccessHarvester.Vba.Grammar/Vba.g4` (procedure-name and identifier rules),
> `AccessHarvester.Vba.Resolver` (ScopeBuildingVisitor, ReferenceResolver,
> Scope, Declaration, ResolutionOutcome, DefaultMemberRegistry full;
> production-wiring greps across all of Types/, DefaultMembers/,
> AccessIntegration/), `AccessHarvester.Sql.Parser` (SqlParserPipeline full;
> DynamicSql/ and FormReferences/ wiring-level), `AccessHarvester.SymbolDb`
> (SymbolDbBuilder, ReferencesExtractor, EntryPointHeuristic, BodyHasher,
> KbVbaSourceLoader, Md5Deduper full), the production consumption seams in
> `AccessHarvester.Core/Pipeline.cs` (module-parse loop and query-export path)
> and `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs` /
> `QuerySqlProjector.cs` (read because they are where parser-stack results
> become consumer-visible), and the `docs/test-cases/` pins.
> **Validation:** all five parser-stack hermetic suites green, run
> sequentially, foreground (2026-06-12): Vba.Grammar.Tests 294/294,
> Vba.Parser.Tests 69/69, Vba.Resolver.Tests 400/400, Sql.Grammar.Tests
> 85/85, SymbolDb.Tests 69/69. All probes below are hermetic (ephemeral
> console probe under gitignored `tmp/s3-probe/` referencing the shipping
> parser/resolver/sql-parser projects, plus committed-golden greps). No
> COM-driven runs (per spec stop rule). No shipping file changed.

## Status-label legend

Every finding carries an explicit **Status** label per the spec's
Evidence-Over-Inference rule, using exactly three values: `proven` (a named
probe/command/source check establishes the claim), `partially proven`
(mechanism proven from source or probe; a stated impact premise remains
inferred — the inferred part is named in the label line), or
`inferred — not verified`. Where a claim rests on a full in-session read of
a file, that is stated in the Evidence block.

## Summary verdict

The parser stack's *component quality* is high: the grammar is a real ANTLR
grammar (no regex half-grammar), the three-mode parse pipeline contract is
clean, `ResolutionOutcome` is a closed discriminated union with a disciplined
reason set, the SymbolDb persistence layer is deterministic by construction,
and 917 hermetic tests pass. The problems are concentrated at two seams.

First, **what the parser stack can do is not what production runs**. The
consumer-facing manifest path parses raw SaveAsText form/report `.txt` files
whole — layout DSL and all — as VBA, so the committed HomeHealthCare golden
ships with **zero parsed VBA items for all 19 forms and all 12 reports**,
silently (F-3-1, rated Critical). The resolver's Access-integration layer
(registry, synthetic control declarations, `Me` binding), default-member
resolution, type inference, dynamic-SQL analysis, and SQL form-reference
resolution all have **zero production call sites**; the manifest path
additionally builds a fresh single-module `ProjectScope` per module, so even
cross-module Public resolution — which the SymbolDb tool wires correctly —
is dead in the manifest (F-3-4). This is the same built-but-unwired pattern
Session 2 proved for the Phase 6 enrichment substrate (F-2-2), now shown to
extend across the parser stack: the substrate sessions landed, the wiring
sessions never did.

Second, **legal legacy VBA can kill whole modules**. A type-suffixed
function name (`Function SqlStr$(…)`) crashes the AST builder with a
NullReferenceException outright; a contextual keyword used as a variable
name (`Dim step As String` — accepted by Access's own VBE, proven by the
committed fixture) starts a recovery cascade that ends in the same NRE. The
committed golden proves the consumer impact: `basResetFixture` is reduced to
a stub by exactly this defect (F-3-2). A related declaration/reference
asymmetry silently de-resolves every type-suffixed module variable,
constant, and parameter (F-3-3).

Findings F-3-1 through F-3-5 are rated Critical/Major on proven evidence;
F-3-6 through F-3-10 are Minor/Observation. Positives worth preserving are
listed at the end, along with the not-reviewed remainder per the stop rule.

## Findings

### F-3-1: Manifest pipeline parses raw SaveAsText form/report dumps as whole-file VBA — all form/report code-behind is absent from consumer ParserOutput, silently
- **Severity:** Critical
- **Status:** proven (golden probes + source pointers + mechanism probe below;
  `Pipeline.cs` module-parse loop and `VbaModuleBodyLoader.cs` read in full
  in-session)
- **Area:** AccessHarvester.Core — `Pipeline.cs:279-307`;
  AccessHarvester.Vba.Parser — `VbaModuleBodyLoader.cs:95-110`
- **Evidence:** `EmitManifestArtifacts` reads `raw/Forms/*.txt` and
  `raw/Reports/*.txt` (`Pipeline.cs:279-284`) and feeds the **entire file** to
  `VbaModuleBodyLoader.LoadAndParse(text)` (`Pipeline.cs:307`). The loader's
  `CanonicalHeader` regex (`VbaModuleBodyLoader.cs:95-110`) strips only the
  module-style `VERSION x.y CLASS / BEGIN..END / Attribute VB_*` header — a
  SaveAsText *form* dump starts with `Version =21` / `Begin Form` layout DSL
  with the VBA after a `CodeBehindForm` marker, which the loader does not
  recognize. **Mechanism probe (2026-06-12):** a minimal form-shaped text
  (layout DSL + `CodeBehindForm` + a real `Private Sub Form_Load()` handler)
  run through `LoadAndParse` exactly as Pipeline does returns
  `mode=Recovering, items=0` — the handler is gone — with 1 diagnostic that
  `Pipeline.cs:307` then **discards** (`var (ast, _) = …`). **Golden probe:**
  in `samples/GoldenOutput/HomeHealthCare/manifest.json`, 0 of 19
  `Forms[].ParserOutput` and 0 of 12 `Reports[].ParserOutput` have any
  `Items`; the manifest `Diagnostics` array contains exactly one entry (the
  F-3-2 module), i.e. nothing flags the 31 empty form/report parses. The
  string `Me.` does not appear anywhere in the 1MB manifest of an
  unbound-forms application.
- **Problem:** Form and report code-behind is where an Access application's
  behavior lives (event handlers, navigation, validation — the exact
  surfaces the Round 2 consumer feedback R2-1/R2-2 asks about). The manifest
  surface a downstream AI reads says every form has no VBA semantics — no
  references, no calls, no event bindings, no classifications — and says it
  silently, as a "successful" run. The ir/ tier still carries the raw
  `VbaCode` text and enricher-derived `Procedures` signatures, so the code
  *text* is not lost, but the parsed/resolved layer the manifest schema
  promises is empty for the entire UI surface of every customer database.
  The repo already contains the correct ingestion pattern: the SymbolDb tool
  reads kb-tier `VbaCode` and strips the VBE header via
  `KbVbaSourceLoader.StripVbeHeader` (`SourceLoading/KbVbaSourceLoader.cs:66-100`).
- **Recommendation:** In `EmitManifestArtifacts`, source form/report VBA from
  the kb tier's `VbaCode` field (the `KbVbaSourceLoader` pattern) — or split
  the raw `.txt` at `CodeBehindForm` — instead of whole-file parsing; and
  stop discarding `ParseResult.Diagnostics` (fold them into the manifest
  `Diagnostics` array so a degraded parse is visible). One session,
  including regenerating the HomeHealthCare golden; pairs naturally with the
  R2-1/R2-2 follow-up work since those edges need exactly this code-behind.

### F-3-2: AstBuilder NullReferenceExceptions on legal VBA — type-suffixed procedure names crash directly; contextual keywords as identifiers crash via recovery cascade; a whole module then vanishes from output
- **Severity:** Major
- **Status:** proven (probes below) for the NRE and its manifest-path
  consequence; partially proven for the symbol-db blast radius — a full
  in-session read shows no exception handling in `SymbolDbBuilder.Build`
  Pass 2 nor at its CLI call site (`Cli/BuildArtifacts.cs:67`), so the
  exception escapes the build path, but no crash was executed and whether
  an outer tool harness catches it was not traced (that residual is
  inferred — not verified)
- **Area:** AccessHarvester.Vba.Parser — `Ast/AstBuilder.cs:281,291,302`
  (unguarded `ctx.IDENTIFIER().GetText()`), `Ast/AstBuilder.cs:45-74`
  (`BuildModuleItem` has no `HasErrorNode` guard, unlike `BuildStatement`);
  AccessHarvester.Vba.Grammar — `Vba.g4:1100,1107,1129` (procedure name
  slots accept only `IDENTIFIER`, not `TYPED_IDENTIFIER`);
  AccessHarvester.SymbolDb — `Extraction/SymbolDbBuilder.cs:78-93`
- **Evidence:** **Probe (2026-06-12), direct crash:**
  `Function SqlStr$(ByVal s As Variant)` and `Property Get Title$()` — both
  legal VBA (return type via suffix, common in legacy code) — throw
  `NullReferenceException` from `LoadAndParse`; the grammar fails the
  `IDENTIFIER` match, recovery still yields a procedure context, and
  `BuildFunctionProcedure`/`BuildPropertyProcedure` dereference the null
  token. **Probe, cascade:** the committed golden's one diagnostic —
  `VBA parse failed for 'basResetFixture': Object reference not set…` — was
  reproduced by extracting that module's `VbaCode` from
  `ir/ModulesAndClasses/basResetFixture.ir.json` and running `LoadAndParse`:
  NRE at `AstBuilder.BuildSubProcedure (AstBuilder.cs:281)`. Bisection
  localized the trigger to `Dim step As String` (line 29): `step` mismatches
  (`mismatched input 'step' expecting {TYPED_IDENTIFIER, IDENTIFIER}`),
  statement-boundary recovery ejects from the procedure body, and the later
  `Exit Sub` / `End Sub` tokens re-parse as a procedure start with no name
  token → NRE. Access's own VBE accepts `step` as an identifier — proven
  empirically by the fixture itself (the module compiled and ran in Access
  to seed the committed HomeHealthCare database). **Breadth probe:**
  `Dim <kw> As String` is rejected by the grammar for `step`, `compare`,
  `explicit`, `preserve`, `lib`, `alias`, `error` (each
  `mismatched input … expecting {TYPED_IDENTIFIER, IDENTIFIER}`); of these,
  real VBA reserves `error` but accepts the others as identifiers (proven
  for `step` via the fixture; the VBE-acceptance of `compare`, `explicit`,
  `preserve`, `lib`, `alias` is standard VBA behavior but was not
  individually re-verified in a live VBE — that sub-claim is inferred — not
  verified). **Blast radius:** in the manifest path the per-file catch
  (`Pipeline.cs:326-341`) reduces the module to a stub (golden:
  `basResetFixture` ParserOutput has 0 items); in `SymbolDbBuilder.Build`
  Pass 2 (`SymbolDbBuilder.cs:85`) there is no try/catch — nor at the CLI
  call site (`Cli/BuildArtifacts.cs:67`) — so the same input fails the
  whole `Build` call rather than skipping one module.
- **Problem:** A single legacy-style construct silently costs the consumer an
  entire module's semantics (manifest) or fails the symbol-db build pass
  (tool). The
  parser's own design contract says malformed input degrades to recovered
  nodes — an internal NRE is neither of the documented outcomes. And the
  triggering inputs are not malformed: they are legal VBA the grammar's
  identifier/name rules under-cover.
- **Recommendation:** Three small changes: (a) grammar — accept
  `TYPED_IDENTIFIER` in function/property-get name position and add the
  safely-contextual keywords (`STEP`, `COMPARE`, `EXPLICIT`, `PRESERVE`,
  `LIB`, `ALIAS`) to the identifier alternatives the way reserved-word
  escape hatches are normally done; (b) AstBuilder — guard module items the
  way `BuildStatement` already guards statements (`HasErrorNode` → recovered
  stub item; null-check name tokens → recovered stub instead of NRE);
  (c) SymbolDbBuilder — per-module catch mirroring Pipeline's. Add hermetic
  pins for each trigger input.

### F-3-3: Type-suffix asymmetry — module-level variables, constants, and parameters keep the `$%&!#@` suffix in their declaration Name while references strip it, so every such declaration is unresolvable
- **Severity:** Major
- **Status:** proven (probe below; all cited sources read in full in-session)
- **Area:** AccessHarvester.Vba.Parser — `Ast/AstBuilder.cs:180-181,189-190,
  216-217,360-361,372-373,382-383` (name taken from `TYPED_IDENTIFIER`
  verbatim) vs. `Ast/AstBuilder.cs:694-696` (locals strip via
  `StripTypeSuffix`) and `Ast/AstBuilder.cs:938-944` (reference sites strip);
  AccessHarvester.Vba.Resolver — `Scopes/Scope.cs:73-81` +
  `Declarations/Declaration.cs:31-32` (lookup is plain
  `OrdinalIgnoreCase` on Name)
- **Evidence:** **Probe (2026-06-12)** through the real parser → scope
  builder → resolver chain: `Public g_sTitle$` declares as Name
  `"g_sTitle$"`; the body reference `g_sTitle` resolves
  `UNRESOLVED (not-in-scope)`. Same for parameter `Sub Foo(p$)` (use of `p`
  → not-in-scope) and module `Const MSG$` (use of `MSG` → not-in-scope).
  Control case: local `Dim s$` strips correctly and `s` resolves. The
  helper's own signature records the original intent — `ExtractTypeSuffix(
  string? typedIdentifierText, string strippedName)` has an unused
  `strippedName` parameter (`AstBuilder.cs:1079-1086`).
- **Problem:** Type-declaration suffixes are precisely the legacy pattern the
  test corpus calls out (case 07). Every suffixed module variable, module
  constant, and procedure parameter in a customer database loses all its
  references: the manifest reports them `Unresolved`, the SymbolDb
  cross-reference index records zero callers against them, and SymbolDb FQNs
  carry the suffix (`Module.MSG$`) so downstream joins on name will miss.
  Because references *strip* the suffix at build time, even a suffixed use
  site (`g_sTitle$ = "x"`) cannot match its suffixed declaration. The defect
  is silent-wrong (resolution failure with a misleading `not-in-scope`
  reason), not explicit-unresolved-by-design.
- **Recommendation:** Strip the suffix into `TypeSuffix` at every AstBuilder
  declaration site exactly as `BuildVariableDeclarator` does (the unused
  `strippedName` parameter shows where to route it); pin with hermetic
  resolver tests for module var / const / parameter / event / declare
  suffixed names. Mechanical, single-file plus tests.

### F-3-4: The resolver's differentiating capabilities are unwired in production — fresh per-module ProjectScope in the manifest path; Access registry, default members, type inference, dynamic SQL, and SQL form references have zero production call sites
- **Severity:** Major
- **Status:** proven (call-site greps + source reads below; golden corroboration)
- **Area:** AccessHarvester.Core — `Pipeline.cs:320-323`;
  AccessHarvester.Vba.Resolver — `AccessIntegration/*`, `DefaultMembers/*`,
  `Types/TypeInferencePass.cs`; AccessHarvester.Sql.Parser —
  `DynamicSql/DynamicSqlAnalyzer.cs`, `FormReferences/FormReferenceResolver.cs`;
  AccessHarvester.Pipeline — `Manifest/ParserOutputProjector.cs:303`
- **Evidence:** Repo-wide greps (2026-06-12): `new AccessObjectModelRegistry`
  appears **only** in test projects; `DefaultMemberRegistry`,
  `PropertyGroupBuilder`, and `TypeInferencePass` are referenced only by
  their own project and `tests/`; `DynamicSqlAnalyzer` and
  `FormReferenceResolver` have no call sites in Core, Pipeline, or Cli.
  `Pipeline.cs:320-323` constructs `new ProjectScope()` **inside the
  per-module loop** and `new VbaReferenceResolver()` with no registry and no
  `owningClass`, so per source read of `ReferenceResolver.cs`: cross-module
  Public lookup (Step 1.5, `ReferenceResolver.cs:331`) can never fire (no
  sibling modules), bare-control and `Me`-member lookup (Steps 2 and
  `MeRegistryLookup`) can never fire (null registry / null OwningClass), and
  `DecideMemberOutcome`'s default arm hardcodes
  `default-member-not-yet-resolved` (`ReferenceResolver.cs:472-479`) — the
  shipped `DefaultMemberRegistry` is never consulted. By contrast
  `SymbolDbBuilder.cs:80` correctly shares one `ProjectScope` across all
  modules (cross-module resolution works in the symbol-db tool), proving the
  correct wiring exists in-repo. `ParserOutputProjector.cs:303` hardcodes
  `["SqlStrings"] = new JsonArray()` — the Phase 5.4 dynamic-SQL surface in
  the v1.0 schema is always emitted empty.
- **Problem:** The plan's Phase 3.5 (default members), 3.6 (type inference),
  4.4 (symbol-table integration — "the harvester's value is created" here
  per the plan), 5.3 (SQL form references), and 5.4 (dynamic SQL) are all
  built and hermetically tested, and none of them affects a single byte of
  consumer output. The manifest path resolves only intra-module,
  registry-less references. Today the form-level consequences are masked by
  F-3-1 (form modules produce no items at all); fixing F-3-1 without this
  wiring will surface a wall of `Unresolved` where the tested capability
  would have resolved controls, `Me`, and cross-module calls. An always-empty
  `SqlStrings` array also *synthesizes a default* ("this module builds no
  SQL") in exactly the way the output conventions forbid.
- **Recommendation:** A wiring-focused follow-up set: share one
  `ProjectScope` across the manifest parse loop (the SymbolDb pattern);
  build `AccessObjectModelRegistry` from the already-extracted form/report
  metadata and pass `owningClass` for Form/Report modules; consult
  `DefaultMemberRegistry` in `DecideMemberOutcome`; wire
  `DynamicSqlAnalyzer` output into `SqlStrings` (or omit the key until
  wired, per omission semantics). Sequence after F-3-1 — they ship together
  or the second masks the first.

### F-3-5: Explicit `ByVal` is never emitted in ParserOutput signatures — under the repo's documented omission convention, consumers read every ByVal parameter as ByRef
- **Severity:** Major
- **Status:** proven (source pointer + committed-golden probe below)
- **Area:** AccessHarvester.Pipeline — `Manifest/ParserOutputProjector.cs:264-273`
- **Evidence:** `BuildParameter` emits `node["ByRef"] = true` only for
  explicit `ByRef` and emits **nothing** for `ByVal`
  (`ParserOutputProjector.cs:269` — the comment even says "only emit when
  source was explicit ByVal or ByRef" but no ByVal branch exists). **Golden
  probe:** `basDataOperations.CopyProviderCatalog`'s `overwrite` parameter
  is declared `Optional ByVal overwrite As Variant` in the ir-tier `VbaCode`
  yet its manifest signature carries only `Name|Type|Optional` — bytewise
  indistinguishable from an implicitly-ByRef parameter.
- **Problem:** The repo's own output convention —
  `docs/planning/project-guidance.md` → Conventions → Output JSON
  Conventions: "Only emit fields Access actually wrote … The downstream
  consumer reads omission as 'Access default for this property'" — defines
  omission as the language default, and VBA's default pass mode is ByRef.
  Under that contract the manifest actively asserts the wrong pass mode for
  every explicit-ByVal parameter; at minimum, a consumer cannot distinguish
  explicit `ByVal` from omitted/default. For a migration
  consumer generating C# signatures, ByRef-vs-ByVal is a semantic decision
  (ref parameters / mutation contracts), not cosmetics. SymbolDb's
  `ParameterRow.Passing` handles the same mapping correctly
  (`SymbolDbBuilder.cs:236-241`), so the two surfaces also disagree.
- **Recommendation:** Emit the pass mode for both explicit forms (e.g.
  `"ByVal": true` / `"ByRef": true`, omit only when source omitted), or an
  explicit `PassMode` enum field if the v1.x schema prefers; regenerate the
  golden; pin with a hermetic projector test. Verify the v1.0 schema allows
  the key (schema change if not — it defines the consumer contract).

### F-3-6: The manifest layer discards parse diagnostics and unresolved reasons that the parser stack produces
- **Severity:** Minor
- **Status:** proven (source pointers; golden corroboration)
- **Area:** AccessHarvester.Core — `Pipeline.cs:307` (`var (ast, _) = …`
  discards `ParseResult` incl. Diagnostics and ModeUsed), `Pipeline.cs:717-729`
  (query-SQL parse: `parseResult.Diagnostics` never read; bare `catch`
  silently downgrades to Raw-only); AccessHarvester.Pipeline —
  `Manifest/ParserOutputProjector.cs:388-391` (`Unresolved` maps to bare
  `"Unresolved"` status; the closed reason set — `not-in-scope`,
  `me-in-standard-module`, `default-member-not-yet-resolved`,
  `member-access-on-unresolved-target` — is dropped)
- **Evidence:** Source reads above (all three files read in-session). Golden
  corroboration: manifest `Diagnostics` has 1 entry despite 31 form/report
  parses yielding zero items (F-3-1) and 4 crosstab queries downgrading to
  Raw-only (F-3-7); no reason string appears anywhere in the manifest.
  Contrast: SymbolDb's `ReferenceRow` carries both `ResolutionStatus` and
  `ResolutionReason` (`ReferencesExtractor.cs:39-41`) — the richer contract
  exists in-repo.
- **Problem:** The project's core parser principle is "unresolvable
  constructs are explicit, not silent." The resolver honors it; the manifest
  layer then erases the explanation. A consumer triaging an `Unresolved`
  reference cannot distinguish "name not in scope" (likely real) from
  "default member not yet resolved" (known capability gap) without the
  reason. Same for parse health: `ModeUsed=Lossy` and diagnostic counts
  never reach the consumer.
- **Recommendation:** Surface `ResolutionReason` on unresolved references in
  ParserOutput, fold per-module `ParseResult.Diagnostics` (or at least
  counts + ModeUsed) into the manifest `Diagnostics`, and replace the bare
  catch in `BuildQueryExports` with a diagnostic-emitting one. Schema
  additions are additive.

### F-3-7: Query-type knowledge — the jet-sql parser correctly parses TRANSFORM but its result is discarded: `Sql.Parsed.QueryType` re-stamps the shifted DAO decode, and the crosstab abstraction drops the inner SELECT the AST carries
- **Severity:** Minor (the underlying decode shift is Session 2's F-2-1,
  rated Major there; this finding records the parser-side facts that shape
  the fix)
- **Status:** proven (probe + golden + source pointers)
- **Area:** AccessHarvester.Pipeline — `Manifest/QuerySqlProjector.cs`
  (QueryType passthrough), `Enrichment/QueryAbstractionBuilder.cs:398`;
  AccessHarvester.Sql.Grammar — `JetSql.g4` (TRANSFORM support)
- **Evidence:** **Probe (2026-06-12):** `qxtClientServiceCosts`'s SQL from
  the committed golden parses through `SqlParserPipeline.Parse` with
  `mode=Recovering, diags=0` and builds a `TransformStatement` AST — the
  grammar handles the crosstab cleanly. **Golden:** the same query ships as
  `QueryType: "MakeTable"`, `Sql.Parsed.QueryType: "MakeTable"` (the F-2-1
  decode shift), and its `Sql.Parsed` carries only `Raw` + `QueryType` —
  18 of 22 golden queries are fully parsed, the 4 raw-only ones are exactly
  the 4 crosstabs. **Source:** `QueryAbstractionBuilder.cs:398` maps
  `TransformStatement => (QueryShape.Transform, null, null, …)` — the inner
  SELECT's select-list/FROM/JOIN/WHERE, present in the AST, is discarded.
- **Problem:** Two consumer effects: (1) the output asserts "MakeTable" on a
  statement the same run just parsed as TRANSFORM — the parser's
  contract-grade knowledge loses to the broken DAO decode; (2) crosstabs are
  the one query family that ships with no structured SQL at all, silently
  (no diagnostic — ties into F-3-6). **Answer to the Session 2 carry-forward
  question:** yes — the jet-sql parser is fit to serve as the
  contract-grade query-type source (it distinguishes TRANSFORM/SELECT/
  UNION/action statements structurally); the F-2-1 fix should
  cross-check the DAO decode against the parsed statement shape and flag
  disagreement explicitly.
- **Recommendation:** When the F-2-1 session corrects the DAO decode table,
  also (a) derive `Sql.Parsed.QueryType` from the parsed AST (DAO value
  retained as `QueryTypeCode` provenance), and (b) extend the Transform arm
  of `QueryAbstractionBuilder` to project the embedded SELECT (the AST
  already carries it). Modest, parser-adjacent, golden regen required.

### F-3-8: Emission-time name heuristics in ParserOutput — any underscored procedure becomes an "EventHandler" with a fabricated control binding, and class modules are labeled Source.Kind "Form"
- **Severity:** Minor
- **Status:** proven for the mechanism (source pointers; file read in full);
  the false-positive *rate* in real databases is inferred — not verified
- **Area:** AccessHarvester.Pipeline — `Manifest/ParserOutputProjector.cs:
  84-88,176-186,476-489`
- **Evidence:** `TryExtractEventBinding` splits any procedure name on its
  last underscore and, on success, overrides `ProcedureKind` to
  `"EventHandler"` and emits `EventBinding { ControlName, EventName }` —
  with no check that the module is a form/report or that the control
  exists (`ParserOutputProjector.cs:176-186,476-489`). A standard-module
  helper named `Save_Record` ships as an EventHandler bound to control
  `Save`, event `Record`. (No instance occurs in the committed golden —
  the four HomeHealthCare standard-module procedures happen to be
  underscore-free; the mechanism is unconditional in source.)
  `ParserOutputProjector.cs:84-88` maps every `ModuleKind.ClassModule` to
  `Source.Kind = "Form"` — the comment says "covers form, report, and class
  modules," so plain class modules and reports are mislabeled by design.
- **Problem:** This is the exact emission-vs-decision drift pattern
  `lessons-learned.md` records ("never re-derive signal text from the
  object name at output time"): the resolver/registry layer that could
  validate a binding is unwired (F-3-4), so the projector substitutes a
  name-shape guess and presents it with the same confidence as a verified
  binding. `EntryPointHeuristic` makes the same shape-guess but documents
  it as conservative liveness bias and gates it on Form/Report module kind
  (`EntryPointHeuristic.cs:34-38`) — the projector applies it ungated.
- **Recommendation:** Gate `TryExtractEventBinding` on Form/Report module
  kind now (one line); when F-3-4 wires the registry, validate the control
  name against form metadata and mark unvalidated bindings with a
  provenance field. Give class modules an honest `Source.Kind` (schema
  enum addition if needed).

### F-3-9: Test posture — the expected.json "contract" is enforced only as schema shape; no test compares harvester output against the pinned expectations; README/case drift
- **Severity:** Minor
- **Status:** proven (greps + file reads below)
- **Area:** `docs/test-cases/`; tests across
  `AccessHarvester.Pipeline.Tests` / `Vba.Resolver.Tests` / `Vba.Grammar.Tests`
- **Evidence:** `project-guidance.md` (VBA Parser Strategy) declares the
  `<n>.expected.json` files "the contract; parser changes are validated by
  re-running each input and comparing against the expected output." Greps
  (2026-06-12): the only DeepEquals-style comparison in `tests/` is
  `SyntheticFixtureSnapshotTests` (synthetic-DB goldens, not test-cases);
  `TestCaseFixtureConformanceTests` validates the expected.json files
  against **schema v1.0 shape only**; the fixture-behavior tests
  (`ResolverFixtureAnnotationTests`, `FixtureCrossCutTests`,
  `DynamicSqlFixtureTests`, grammar corpus tests) parse the `.bas` inputs
  and assert *selected annotations* on a subset of cases (02, 03, 08, 09 in
  the resolver's case). No test diffs produced output against any
  expected.json. Drift: `docs/test-cases/README.md` says "The 12 cases" and
  lists 12 file pairs while `13-preprocessor.bas` / `.expected.json` exist
  on disk; the README still says outputs are "validated against
  harvester-schema.v0.json" while the conformance test pins v1.0 (and a
  v1.1 schema exists alongside). The practical consequence is visible in
  this session's probes: 917 hermetic tests pass while `Function F$()`
  NREs, `Dim step…` cascades, and suffixed module declarations de-resolve —
  the suites pin component behavior, not the corpus-level contract.
- **Problem:** The strongest stated quality gate for the parser stack is
  aspirational. The downstream consumer's confidence derives from "the
  pinned cases pass"; today they cannot fail for behavioral regressions.
- **Recommendation:** Either implement the comparison the guidance promises
  (a conformance runner that executes the parser/resolver over each case
  input and diffs against expected.json, with a documented field-mask for
  not-yet-implemented surfaces), or amend the guidance/README to state the
  actual contract (schema-shape + selected annotations). Update the README
  case list (13) and schema-version sentence either way. Add the F-3-2
  trigger inputs as new corpus cases.

### F-3-10: Parser/AST robustness and lean observations (collected)
- **Severity:** Observation
- **Status:** proven for each mechanism (source pointers; AstBuilder,
  ReferenceResolver, BodyHasher read in full); consumer impact not
  individually assessed
- **Area:** AccessHarvester.Vba.Parser / Vba.Resolver / SymbolDb
- **Evidence and items:**
  - **Whole-block collapse on nested recovery:** `BuildStatement`
    (`AstBuilder.cs:428-429`) turns any statement whose *subtree* contains
    an error node into a single `UnknownStatement` — an error in one line
    of a 200-line `If`/`With` block discards the block's parseable
    remainder. The class comment (lines 16-20) promises tagging
    (`RecoveredProvenance`), not collapse; the error-recovery strategy doc
    promises statement-boundary resumption. Also quadratic: `HasErrorNode`
    rescans the subtree at every nesting level.
  - **Silent defaults on unknown alts:** unknown option statement →
    `OptionKind.Explicit` (`AstBuilder.cs:86`); unknown comparison operator
    → `Equal` (`AstBuilder.cs:635,821`); unknown on-error → `ResumeNext`
    (`AstBuilder.cs:651`); unknown exit kind → `Do` (`AstBuilder.cs:674`).
    All are closed-grammar-unreachable today, but each silently substitutes
    a guess where a recovered-stub would be honest if the grammar evolves.
  - **Duplication:** `BuildPreprocessorStatement` and `BuildPreprocessor`
    are byte-identical except return type (`AstBuilder.cs:223-262`); dead
    locals `commaCount`/`args`/`argIdx` in `BuildArguments`
    (`AstBuilder.cs:968-972`).
  - **BodyHasher line-number strip vs. continuations:** the multiline
    `^\s*\d+\s+` strip (`BodyHasher.cs:25-26`) matches any line beginning
    with digits followed by whitespace and more tokens — including a
    continuation line such as `x = a + _` / `2 + b`, whose leading `2 `
    would be deleted, making semantically different bodies hash-equal in a
    rare shape (a line containing only the digits would not match). Dedup-
    only impact (false twin), worth a guard or a documented accept.
  - **Unresolved identifiers labeled "call" in SymbolDb:**
    `ReferencesExtractor.ClassifyKind`'s fallback labels unresolved bare
    identifiers `"call"` (`ReferencesExtractor.cs:131`) — a guess where
    `"unknown"` would be honest; rows do carry `resolution_status` so the
    consumer can filter.
  - **ParseLossy double-parses** when the recovering pass already engaged
    lossy fallback is avoided, but explicit-Lossy requests re-run the whole
    recovering pipeline just to append a marker diagnostic
    (`VbaParserPipeline.cs:95-117`) — fine at current scale.
- **Recommendation:** Record-only; fold the first two items into whichever
  follow-up session touches AstBuilder for F-3-2 (the recovered-stub guard
  naturally replaces both the NRE and the silent defaults).

## Positives worth preserving

- **Grammar-over-regex adherence is genuine** in both grammars: Vba.g4 and
  JetSql.g4 are real ANTLR grammars; TRANSFORM/PIVOT parses cleanly (F-3-7
  probe); the only regexes in the parse path are the bounded, anchored
  header strips (`VbaModuleBodyLoader`, `KbVbaSourceLoader`), both with
  match timeouts.
- **The three-mode parse pipeline contract** (Strict/Recovering/Lossy with
  the lexer ERROR_TOKEN pre-scan in the VBA pipeline) is clean, mirrored
  across both parsers, and deterministic.
- **`ResolutionOutcome` is a closed discriminated union** with a private
  constructor and a documented, append-only reason set — exactly the
  explicit-unresolved discipline the principles require, at the resolver
  layer.
- **Determinism discipline:** `ParserOutputProjector` totally orders
  references before emission; `Md5Deduper` picks the ordinal-first FQN as
  canonical explicitly for diff stability; `BodyHasher` is a token-aware
  single-pass walker that correctly handles `""` escapes and `REM`
  boundaries rather than a regex chain.
- **SymbolDb's reference rows** carry status *and* reason — the model the
  manifest layer should adopt (F-3-6).
- **The SymbolDb ingestion path** (shared ProjectScope, kb `VbaCode`,
  `StripVbeHeader`) is the working reference implementation for fixing
  F-3-1/F-3-4's manifest-side wiring.

## Coverage and not-reviewed remainder (per stop rule)

Reviewed line-level: every file named in the Scope header above. Reviewed
wiring-level only (structure, call sites, and contracts — not line-level):
`DynamicSqlAnalyzer` (887 lines; zero production call sites makes
line-level review lower-value until wired), Sql.Parser `Ast/AstBuilder.cs`,
`SymbolDbWriter` / `SchemaMigrator` (determinism inferred from green pins +
Md5Deduper read; not independently audited), `FormControlsExtractor`,
`QuerySchemaExtractor`, `MacroExtractor`, `TypeInferencePass` internals,
and `tools/symbol-db/` CLI plumbing (`BuildArtifacts` was covered in
Session 1). These are the natural first targets if Session 4 scopes a
parser-stack follow-up set with remaining-review capacity; none of them is
on the consumer-visible critical path today (the unwired components by
definition, the writers because their outputs are local-only).

## Cross-cutting threads for Session 4

1. **The unwired-substrate pattern is now proven across three layers**
   (S2 F-2-2: Phase 6 enrichment; S3 F-3-4: resolver Phases 3.5/3.6/4.4 and
   SQL Phases 5.3/5.4; S3 F-3-1: the form ingestion seam). Session 4's
   recommendations should treat "finish the wiring" as one coherent arc —
   R2-1/R2-2 (navigation/report-chain edges) sit directly on top of it:
   NavigationGraph needs form code-behind (F-3-1) and resolved
   `DoCmd.OpenForm/OpenReport` calls (F-3-4) to materialize edges.
2. **R2-2 grounding caveat:** when Session 4 runs the fresh HomeHealthCare
   pass, expect the parameter-dialog → query → report chain evidence to be
   limited by F-3-1 (no form code-behind semantics) — ground what is
   observable and label the rest per the spec.
3. **Severity ranking input:** F-3-1 (Critical) is the cheapest
   highest-leverage fix in the stack (the working ingestion pattern already
   exists in-repo); F-3-2/F-3-3 are small, self-contained parser fixes with
   outsized legacy-database impact; F-3-4 is the larger wiring arc.
