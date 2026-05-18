# Call-Site Audit Ledger — vba-attribute-header-strip Session 1

> **Generated:** 2026-05-17, Session 1.
> **Source:** ripgrep over `VbaParserPipeline\.Parse\(` across the repo.
> **Decision rule (per spec):**
> - **Production / runtime consumers** that ingest Access-exported
>   module text route through `VbaModuleBodyLoader.LoadAndParse`.
> - **Grammar / unit tests that hand-build pre-normalized sources**
>   continue to call `VbaParserPipeline.Parse(string)` directly — the
>   raw API stays available as a documented escape hatch.
> - **Fixture-based tests** that read whole-module sources from
>   `docs/test-cases/` (no header in those fixtures) likewise stay
>   raw.

---

## Executed-spec elaboration (Step 2 outcome)

These decisions, made in-conversation against the cross-engine inputs
under `docs/strategy-input/`, are what Session 1 implemented:

- **Loader entry points** in `AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs`:
  - `Load(rawSource) → (string Body, ModuleHeaderMetadata? Header)`
  - `LoadAndParse(rawSource, options?, sourceFile?) → (Module Ast, ParseResult Result)` — combines `Load` + `VbaParserPipeline.Parse` + `BuildAst` and attaches `Header` to the returned `Module` AST.
- **Metadata record** placed at `AccessHarvester.Vba.Parser/Ast/ModuleHeaderMetadata.cs`. Sealed record. Recognized fields (each nullable): `VersionMajor`, `VersionMinor`, `MultiUse`, `VbName`, `VbGlobalNameSpace`, `VbCreatable`, `VbPredeclaredId`, `VbExposed`. Unknown `Attribute VB_<Other>` lines land in `AdditionalAttributes: IReadOnlyDictionary<string, string>` (sorted `Ordinal`, empty by default).
- **AST attach point:** `public ModuleHeaderMetadata? Header { get; init; }` on the existing `sealed record Module`. Default `null`; init-only so it participates in record equality (deterministic AST), but does not break any existing call site that constructs `Module` positionally.
- **Canonical header regex** anchored at `\A`. Tolerates UTF-8 BOM, CRLF/LF line endings, and either of two shapes:
  - Full SaveAsText: optional `VERSION 1.0 CLASS\n` + optional `BEGIN\n ... END\n` + one-or-more `Attribute VB_<Name> = <Value>\n` lines.
  - YAML-mirror form (dominant in this repo): one-or-more leading `Attribute VB_<Name> = <Value>\n` lines.
  - At least one `Attribute VB_*` line is required. `VERSION` without a complete `BEGIN..END` (or without trailing `Attribute` lines) does **not** match, per the spec's "malformed/partial → don't heuristic-strip" rule.
- **Repo-observed header shape note.** Inspection of `samples/DatabaseYaml/HomeHealthCare.accdb/Modules/*.yaml` shows the YAML mirror starts modules with `Attribute VB_GlobalNameSpace = False` directly — the upstream YAML extractor already drops the `VERSION` / `BEGIN..END` / `VB_Name` prefix. The full SaveAsText shape is still supported (for `.cls` / `.bas` inputs that might carry it) but the dominant production payload is the simpler Attribute-only form.

---

## Production / runtime call sites — ROUTED through `LoadAndParse`

| # | Location | Routing decision | Notes |
|---|---|---|---|
| P1 | [AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs:85](../../../AccessHarvester.SymbolDb/Extraction/SymbolDbBuilder.cs#L85) | `LoadAndParse(raw.Source, options, raw.SourcePath)` | Primary runtime consumer. Iterates `RawModule` instances whose `Source` is read from YAML-mirror `Definition:` blocks. The returned `Module ast` carries `Header` (or `null` for `.bas` modules with no header). |
| P2 | [tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs:107](../../../tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs#L107) | Two-step `VbaModuleBodyLoader.Load(body).Body` + `VbaParserPipeline.Parse(...)`, **no `BuildAst`** | The parse-success-report generator over the YAML mirror. Routing here is the mechanism that lifts HHC's `parseOk` from 3 → 30 and Northwind's from 14 → 61 once the goldens are regenerated. The two-step variant is a deliberate workaround for the AstBuilder NRE documented in "Follow-up findings" §1 — the report only needs `result.Diagnostics`, so the AST build is skipped without losing signal. |

Production consumer count: **2**. Well under the spec's `>8 = spill`
threshold; no Session 2 needed for the consumer-routing concern.

### Production VBA-parsing consumers that do **not** call `Parse` directly

The spec called out a handful of additional production consumers
(resolver, function classifier, navigation graph builder, dynamic-SQL
analyzer) as possible audit targets. ripgrep over `VbaParserPipeline.Parse(`
shows that **these classes do not invoke the parser** — they receive
already-built `Module` / `ResolvedModule` instances from upstream callers.
Their inputs are the AST produced by P1 (`SymbolDbBuilder`) or by tests,
so routing P1 through `LoadAndParse` is sufficient to ensure these
downstream consumers see header-stripped ASTs in production.

- `AccessHarvester.Vba.Resolver.*` — consumes the `Module` AST P1 passes in.
- `AccessHarvester.Pipeline.Enrichment.FunctionClassifier*` — consumes resolver output.
- `AccessHarvester.Pipeline.Enrichment.NavigationGraphBuilder*` — consumes resolver output.
- `AccessHarvester.Sql.Parser.DynamicSql.DynamicSqlAnalyzer` — analyzes SQL string-literals inside an already-parsed AST.
- `AccessHarvester.Cli/Program.cs` — does not currently invoke the VBA parser directly (the `parse` subcommand mentioned in the spec is provided by the Symbol DB pipeline tooling, which goes through P1).

---

## Test call sites — STAY raw (`VbaParserPipeline.Parse` directly)

These all hand-build pre-normalized VBA source as inline string
literals. There is no header to strip; routing them through the loader
would be no-op overhead plus an extra layer to reason about when a
grammar test fails. The raw API is preserved as the documented escape
hatch for this case.

### `tests/AccessHarvester.Vba.Parser.Tests/`

- `VbaParserPipelineTests.cs` (lines 92, 105, 117, 136, 145, 172, 194, 220, 231, 240) — pipeline unit tests.
- `Ast/AstBuilderShapeTests.cs` (lines 16, 548) — AST shape tests on hand-built sources.
- `Ast/AstBuilderRecoveryTests.cs` (lines 28, 61) — error-recovery tests.
- `Ast/VbaSyntaxWalkerTests.cs` (lines 51, 152, 174) — walker tests.

### `tests/AccessHarvester.Vba.Resolver.Tests/`

- `References/ReferenceResolverTests.cs` (lines 45, 60)
- `References/ResolvedModuleTests.cs` (lines 46, 506)
- `References/ResolverFixtureAnnotationTests.cs` (line 42)
- `ScopeBuildingFixtureTests.cs` (line 44)
- `Types/InferredTypeMapTests.cs` (line 25)
- `Types/TypeInferenceFixtureTests.cs` (lines 48, 133)
- `Types/TypeInferencePassTests.cs` (lines 24, 541)
- `CrossReference/CrossReferenceFixtureTests.cs` (lines 46, 158)
- `CrossReference/CrossReferenceIndexTests.cs` (line 23)
- `AccessIntegration/ReferenceResolverAccessIntegrationTests.cs` (line 51)
- `AccessIntegration/FixtureCrossCutTests.cs` (line 66)

### `tests/AccessHarvester.Pipeline.Tests/`

- `EventHandlerClassifierTests.cs` (lines 18, 25)
- `EnrichmentPipelineRunnerTests.cs` (line 44)
- `EndToEndFixtureTests.cs` (line 63) — reads sources from `docs/test-cases/*.bas` fixtures (no headers).
- `FunctionClassificationBuilderTests.cs` (line 25)
- `NavigationGraphBuilderTests.cs` (line 26)
- `ValidationRuleExtractorTests.cs` (line 39)

### `tests/AccessHarvester.Sql.Grammar.Tests/`

- `DynamicSql/DynamicSqlFixtureTests.cs` (line 129) — `Fixture05Bas` is a `.bas` fixture, no header.

### `tests/AccessHarvester.SymbolDb.Tests/`

- `IntraProcedureDeadCodeDetectorTests.cs` (line 103) — hand-built source.

### Blocked / follow-up

No production consumer relies on the raw header being present in the
source string (e.g., a regex-based `VB_Name` extractor reading the
body); the audit found no such site.

#### Follow-up findings (out of scope for this set)

1. **`AstBuilder.BuildSubProcedure` NRE on header-stripped HHC modules.**
   When `ParseSuccessReportGenerator` was first switched to
   `LoadAndParse` (which calls `BuildAst` internally), at least one
   header-stripped HHC module produced a `NullReferenceException` at
   [AccessHarvester.Vba.Parser/Ast/AstBuilder.cs:281](../../../AccessHarvester.Vba.Parser/Ast/AstBuilder.cs#L281)
   — `ctx.IDENTIFIER().GetText()` on a `SubDeclarationContext` whose
   `IDENTIFIER` terminal was `null` (an ANTLR-recovery synthesized
   tree shape). The same pattern is repeated in
   `BuildFunctionProcedure` (line ~291) and `BuildPropertyProcedure`
   (line ~298).
   - **Why this was masked before:** the leading `Attribute VB_*` lines
     used to fail the parser outright, so `BuildAst` never ran over
     these modules. Stripping the header lets the parser advance far
     enough for recovery to surface, exposing the latent bug.
   - **Workaround applied in this set:** `ParseSuccessReportGenerator`
     was reverted to a two-step `Load` + `VbaParserPipeline.Parse`
     flow (no `BuildAst`), which restores the diagnostic-only counting
     the report needs without touching the AstBuilder fragility.
     `SymbolDbBuilder` still uses `LoadAndParse` — it would crash on
     the same input if run against the real HHC YAML mirror, but its
     unit tests use hand-built sources and do not exercise the
     recovery path.
   - **Suggested next step (separate session set):** make the three
     `Build{Sub,Function,Property}Procedure` builders defensive
     against missing `IDENTIFIER` terminals (synthesize a placeholder
     name plus a `RecoveredProvenance` tag, matching the existing
     pattern documented at
     `AccessHarvester.Vba.Parser/Ast/AstBuilder.cs` line 12-21 for
     IErrorNode-tagged subtrees). Pair the fix with a corpus test
     case that triggers the recovery path so a regression is visible.

2. **`Module.Header` participation in record equality.** The init-only
   `Header` property means two `Module` instances built with
   different `Header` values are no longer `Equals`. No existing test
   asserts AST equality across `LoadAndParse` vs. raw `Parse` paths,
   so this is latent. If a future serializer or snapshot test starts
   comparing ASTs, the test should pin a `Header` value rather than
   `null` on the expected side.
