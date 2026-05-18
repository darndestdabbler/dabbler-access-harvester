# VBA Attribute Header Strip Spec — **STUB**

> **Status:** STUB authored 2026-05-17 after the close of
> `harvester-finish-canonical-parse-success-report` (F1). The header
> gap was surfaced by F1's reports (HHC 29/32 modules `parseFailed`,
> Northwind 52/66) and the human decided to strip rather than extend
> the grammar; cross-engine input from gpt-5-4 (OpenAI) and gemini-pro
> (Google) was consulted on the two open spec-authoring questions —
> both engines independently recommended the same shape. Acceptance
> criteria are pinned; implementation shape (loader API surface,
> AST attachment point, audit ledger) is intentionally left for the
> set's own Session 1.
>
> **Purpose:** Introduce a shared module-body loader
> (`VbaModuleBodyLoader`) that strips the `VERSION / BEGIN…END /
> Attribute VB_*` header block from Access-exported VBA module text
> before the parser sees it, AND captures the stripped header as
> typed `ModuleHeaderMetadata` attached to the parsed `Module` AST
> node so the downstream-AI consumer can still reach the
> migration-relevant fields (`VB_PredeclaredId`, `VB_Exposed`,
> `VB_Creatable`). Audits every production / runtime VBA-parsing
> call site to route through the loader. Regenerates the
> parse-success-report goldens.
> **Created:** 2026-05-17
> **Session Set:** `docs/session-sets/vba-attribute-header-strip/`
> **Prerequisite:** `harvester-finish-canonical-parse-success-report`
> (this set regenerates its committed goldens).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. Deliverables are a new loader class, a
> new AST metadata record, audit-ledger updates across VBA-parsing
> consumers, and regenerated parse-success-report goldens.
> Automated build / unit + integration tests + cross-provider AI
> verification is the complete quality bar.

---

## Origin and authority

- **Authority for scope:** the parse-success report findings from
  [`harvester-finish-canonical-parse-success-report`](../harvester-finish-canonical-parse-success-report/change-log.md)
  → "Findings that may interest the human" → VBA `Attribute VB_*`
  grammar gap.
- **Authority for the strip-not-extend decision:** human directive
  on 2026-05-17 ("we should just remove that header from our
  artifacts. It isn't needed").
- **Authority for the spec shape (loader placement + AST
  preservation):** cross-engine consult on 2026-05-17 captured at
  [`docs/strategy-input/attribute-strip-spec-gpt-5-4.md`](../../strategy-input/attribute-strip-spec-gpt-5-4.md)
  and
  [`docs/strategy-input/attribute-strip-spec-gemini-pro.md`](../../strategy-input/attribute-strip-spec-gemini-pro.md).
  Both engines independently recommended the loader-at-production-
  consumer-sites + typed-metadata-record approach codified here.
- **Authority for canonical-fixture list:** the planner's resolved
  Open Question 1 from `remaining-work-planner` Session 1 —
  `NorthwindDevEdition.accdb` + `HomeHealthCare.accdb`. MiniComplex
  remains OUT.

---

## Project Overview

When Access exports a form or report's code-behind module via
SaveAsText, the YAML mirror's module body begins with a header
block:

```
VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "Form_frmClientEdit"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
```

Standalone `.bas` modules typically don't carry this header. The
VBA grammar (`Vba.g4`) does not recognise the `VERSION / BEGIN…END
/ Attribute VB_*` token sequence, so every code-behind module's
parse aborts. F1's report quantified the cost (HHC 91% parseFailed,
Northwind 79%). The header carries no semantically-novel content
the parser body needs — it is module-level metadata that lives
naturally as a parallel AST field, not as code-body tokens.

This set:

1. **Builds `VbaModuleBodyLoader`** in `AccessHarvester.Vba.Parser/`
   (alongside `VbaParserPipeline`). The loader's `Load(rawSource)`
   strips the canonical leading header block and returns
   `(string Body, ModuleHeaderMetadata? Header)`. Idempotent on
   no-header inputs (`Header == null`, body unchanged).
2. **Adds the `ModuleHeaderMetadata` typed record** with named
   fields for the migration-relevant attributes plus an
   `AdditionalAttributes` map for forward-compat.
3. **Attaches `Header: ModuleHeaderMetadata?`** to the
   `AccessHarvester.Vba.Parser.Ast.Module` node, populated when the
   loader is used (null when callers go through raw
   `VbaParserPipeline.Parse(string)`).
4. **Audits every production / runtime VBA-parsing call site** and
   routes through the loader. Grammar-focused / unit tests that
   hand-build pre-normalized sources continue to call
   `VbaParserPipeline.Parse(string)` directly — the raw API stays
   available as a documented escape hatch, not deprecated.
5. **Regenerates the parse-success-report goldens** via
   `UPDATE_PARSE_SUCCESS_REPORTS=1`. Expected shift: HHC parseOk
   `3 → ~32`, Northwind parseOk `14 → ~66`. If the actual shift is
   materially smaller, the strip is incomplete (Stop Rule below).

### In scope

1. `VbaModuleBodyLoader.Load(string rawSource) → (string Body,
   ModuleHeaderMetadata? Header)` and a sibling `LoadModule(string
   rawSource) → ParseResult` overload that bundles loader + parser
   so the typical consumer needs one call.
2. `ModuleHeaderMetadata` record under
   `AccessHarvester.Vba.Parser.Ast/`. Recognized fields (each
   `init`-only, all nullable except `AdditionalAttributes` which is
   non-null-empty-by-default):
   - `VersionMajor: int?` (from `VERSION 1.0 CLASS`'s `1`)
   - `VersionMinor: int?` (from `VERSION 1.0 CLASS`'s `0`)
   - `MultiUse: bool?` (from `MultiUse = -1` → `True`)
   - `VbName: string?`
   - `VbGlobalNameSpace: bool?`
   - `VbCreatable: bool?`
   - `VbPredeclaredId: bool?`
   - `VbExposed: bool?`
   - `AdditionalAttributes: IReadOnlyDictionary<string,string>` —
     any `Attribute VB_<Other>` line the recognized-fields list does
     not cover. Map is sorted by key (Ordinal) on construction.
3. `Module.Header: ModuleHeaderMetadata?` AST property, populated by
   `LoadModule`; remains null for direct `Parse(string)` callers.
4. Audit ledger (in this set's `audit.md`) of every production /
   runtime call site that handles Access-exported module text:
   - `AccessHarvester.Vba.Resolver` (scope-building entry points)
   - `AccessHarvester.Pipeline.Enrichment.FunctionClassifier`
   - `AccessHarvester.Pipeline.Enrichment.NavigationGraphBuilder`
   - `AccessHarvester.Sql.Parser.DynamicSql.DynamicSqlAnalyzer` (when
     fed module bodies via pipeline-level callers; standalone calls
     with hand-built SQL fragments stay raw)
   - `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/
     ParseSuccessReportGenerator.cs`
   - `AccessHarvester.Cli/Program.cs` (`parse` subcommand)
   - Any other consumer Session 1's grep surfaces
5. A "no production consumer bypasses the loader" verification step:
   Session 1 runs a Roslyn-or-grep audit and lists every remaining
   raw `VbaParserPipeline.Parse(string)` caller. Each remaining
   caller is either (a) a grammar/unit test that hand-builds sources,
   or (b) explicitly justified and added to a small allowlist in
   this set's `audit.md`.
6. Loader unit tests:
   - Canonical form/report header is stripped; body is preserved
     starting from the first non-header line.
   - `.bas` body with no header is unchanged; `Header == null`.
   - Loader is idempotent: `Load(Load(x).Body)` returns the same
     body and `null` Header.
   - CRLF/LF and optional UTF-8 BOM are tolerated.
   - Class modules (`.cls`) with the same header shape are handled
     (`VbName = "<ClassName>"`, `VbCreatable` typically `True` rather
     than the form/report `False`).
   - **Malformed/partial header**: if the prefix matches `VERSION`
     but the block isn't structurally complete (missing `BEGIN`,
     missing `END`, etc.), the loader **does not** heuristic-strip;
     leaves source untouched and lets parse fail visibly.
   - **`VB_Name` vs. external identity**: when `VB_Name` disagrees
     with the YAML-mirror filename, the loader records `VbName` as
     metadata but **does not** redefine module identity. The
     filename remains authoritative for object-graph identity.
7. Regenerated `samples/parse-success-reports/HomeHealthCare.accdb.
   parse-success-report.json` and `samples/parse-success-reports/
   NorthwindDevEdition.accdb.parse-success-report.json` via
   `UPDATE_PARSE_SUCCESS_REPORTS=1`.
8. End-to-end regression: a previously-failing code-behind module
   (e.g., `Form_frmAbout` from HHC) now parses cleanly via the
   normal pipeline-consumer path.

### Out of scope

- **Extending `Vba.g4` to natively parse the `Attribute VB_*` token
  family.** The decision is strip-at-loader, not grammar extension.
  If a downstream session set decides grammar extension would
  simplify the loader away, that is a separate tree-amendment.
- **Re-emitting the header into the harvester's `kb/` or `ir/`
  outputs.** The metadata lives on the in-memory AST for the
  downstream-AI consumer; whether to materialize it into JSON
  outputs is a `harvester-finish-canonical-golden-output` decision.
  If F2 needs it, F2 reads the AST and includes the fields it
  cares about.
- **Stripping non-leading `Attribute`-looking lines.** The loader
  only touches the canonical leading header block; in-body
  `Attribute`-like text (e.g., a `Const Attribute = "..."`
  declaration, however unlikely) is parser territory.
- **Refactoring the YAML mirror itself.** The committed mirrors
  preserve the header for source-of-truth fidelity; the strip is a
  load-time concern.
- **Updating the parse-success-report's per-fixture parseOk count
  expectations downward if the new parseOk rate exceeds 100%
  (i.e., new procedures surface).** If header-stripped modules
  expose more procedures than F1's report counted, the
  regeneration captures that automatically; the change-log notes
  the count shifts.
- **Strategy.md "Frozen Unless Reauthorized" items remain frozen.**

---

## Sessions

### Session 1 of 1 (or 2): Loader + AST + Audit + Regenerated Goldens

**Steps:**

1. Read this stub end-to-end plus the cross-engine input at
   [`docs/strategy-input/attribute-strip-spec-gpt-5-4.md`](../../strategy-input/attribute-strip-spec-gpt-5-4.md)
   and
   [`docs/strategy-input/attribute-strip-spec-gemini-pro.md`](../../strategy-input/attribute-strip-spec-gemini-pro.md).
2. Elaborate this stub into the executed spec: loader API surface,
   `ModuleHeaderMetadata` record placement (probably
   `AccessHarvester.Vba.Parser/Ast/ModuleHeaderMetadata.cs`), AST
   attachment point on `Module`, and the canonical header regex.
3. Author `ai-assignment.md` via routed analysis.
4. **Build `ModuleHeaderMetadata`** with the fields enumerated in
   "In scope" item 2, plus the `AdditionalAttributes` map.
5. **Build `VbaModuleBodyLoader`** with `Load` and `LoadModule`
   entry points. Strict regex for the canonical leading header
   block (no heuristic recovery on malformed inputs). Idempotent
   on no-header inputs.
6. **Attach `Header`** to `AccessHarvester.Vba.Parser.Ast.Module`.
   AST equality / hashing / visitor updates as needed for
   determinism.
7. **Audit ledger.** Grep the codebase for every
   `VbaParserPipeline.Parse(` call site. For each one, either route
   through `LoadModule` (production / runtime consumers) or
   explicitly justify keeping it raw (grammar tests, hand-built
   fixtures). Write the ledger to
   `docs/session-sets/vba-attribute-header-strip/audit.md`.
8. **Update production consumers.** Route the audited call sites
   through `LoadModule`. Re-run their tests to confirm no regression.
9. **Loader unit tests** under
   `tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs`
   covering every bullet under "In scope" item 6.
10. **Regenerate parse-success-report goldens** with
    `UPDATE_PARSE_SUCCESS_REPORTS=1 dotnet test ParseSuccessReportTests`.
    Confirm HHC parseOk and Northwind parseOk both increase as
    expected (Stop Rule if the shift is materially smaller than
    forecast).
11. **End-to-end regression test** under
    `tests/AccessHarvester.Pipeline.Tests/` (or
    `AccessHarvester.Integration/`): one previously-failing
    code-behind module from HHC now parses cleanly via the standard
    pipeline-consumer path, and the parsed `Module.Header` carries
    the expected metadata fields.
12. `dotnet build && dotnet test` sequentially.
13. End-of-session cross-provider verification.
14. Closeout: `disposition.json`, `change-log.md`,
    `next-session-set-recommendation.md`, commit + push +
    `mark_session_complete()` + notify.
15. Step 9 reorganization review AFTER notify.

**Creates:**
- `AccessHarvester.Vba.Parser/Ast/ModuleHeaderMetadata.cs`
- `AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs`
- `tests/AccessHarvester.Vba.Parser.Tests/VbaModuleBodyLoaderTests.cs`
- `docs/session-sets/vba-attribute-header-strip/audit.md`
- `docs/session-sets/vba-attribute-header-strip/ai-assignment.md`
- `docs/session-sets/vba-attribute-header-strip/change-log.md`
- `docs/session-sets/vba-attribute-header-strip/disposition.json`
- `docs/session-sets/vba-attribute-header-strip/next-session-set-recommendation.md`
- `docs/session-sets/vba-attribute-header-strip/session-reviews/session-001.md`

**Touches:**
- `AccessHarvester.Vba.Parser/Ast/Module.cs` (adds `Header` property).
- Production VBA-parsing call sites enumerated in `audit.md`:
  resolver, function classifier, navigation graph builder,
  dynamic-SQL analyzer's pipeline-level entry, parse-success-report
  generator, CLI `parse` subcommand.
- `samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json`
  (regenerated).
- `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`
  (regenerated locally; CI skips per `[SkippableFact]`).

**Ends with:** the loader ships; every production consumer routes
through it; parse-success-report goldens reflect the new parseOk
rate (>95% for both fixtures); `Module.Header` carries
migration-relevant metadata on previously-failing code-behind
modules.

**Progress keys:** `session-1/spec-elaboration`,
`session-1/module-header-metadata`,
`session-1/vba-module-body-loader`,
`session-1/ast-attachment`,
`session-1/call-site-audit`,
`session-1/consumer-routing`,
`session-1/loader-unit-tests`,
`session-1/golden-regeneration`,
`session-1/end-to-end-regression`,
`session-1/build-test`,
`session-1/verify`,
`session-1/closeout`.

> **Spill to Session 2 if the call-site audit surfaces more than
> ~8 production consumers** OR **the `Module.Header` attachment
> requires a non-trivial visitor / equality / serialization
> refactor.** Session 2 would cover the consumer-routing audit plus
> any deferred test updates; Session 1's deliverables remain the
> loader, metadata record, AST shape, and one canonical consumer's
> routing (the parse-success-report generator). Session 1's spec
> expansion makes the spill call.

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.20 per session (cross-provider
  session-verification + the `ai-assignment.md` analysis +
  closeout `summarization`). ~$0.10–$0.40 across the set.
- **In-conversation token spend:** Moderate. The loader and the
  metadata record are mechanical implementations against a pinned
  spec; the call-site audit is grep-driven; the synthesis tasks
  are the spec elaboration and the change-log.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Parse-success shift smaller than forecast** | After regeneration, HHC parseOk does not reach ~32/32 (≥30) AND Northwind does not reach ~66/66 (≥60) | Stop. Surface to the human. The strip is incomplete — either a non-canonical header variant exists in the fixtures the regex doesn't cover, or another grammar gap is masking the gain. Inspect the still-failing modules, add a corpus test for the new variant, and revise the loader before regenerating goldens. |
| **AST surface change ripples beyond `Module`** | Adding `Header` requires changes to `VbaSyntaxVisitor` / `VbaSyntaxWalker` / equality comparers / serializers that exceed ~100 lines net | Stop. Surface to the human. The right move is probably to spill the ripple work to Session 2 rather than absorbing into Session 1. The original session-set sizing is 1–2 sessions; this is the spill trigger. |
| **Production call site refuses to route through the loader** | A consumer's tests fail after routing through `LoadModule` because it relied on the raw header in the source string (e.g., a regex-based extractor reading `VB_Name` from the body) | Stop. Surface to the human. The fix is to have that consumer read `Module.Header.VbName` instead of grepping the body — but the rewrite is its own scope decision. Note the consumer in `audit.md` as "blocked: needs follow-up rewrite" and proceed with the rest of the audit. |
| **`AdditionalAttributes` map grows beyond ~3 entries per real fixture** | The recognized-field list is under-spec'd for real Access output | Stop. Add the missing field to `ModuleHeaderMetadata` as a typed field rather than leaving it in the map. The map is a forward-compat affordance, not a permanent home for fields the actual fixtures carry. |
| **Golden surfaces real defect** | Generated goldens show shape inconsistent with the schema or contain malformed values | Stop. Surface to the human. The fix lives in the producing component, not in the golden file. |

---

## Why this is a stub

The stub fixes the contract: shared loader + typed metadata record
+ AST attachment + production-consumer audit + golden regeneration
+ verification gates. It deliberately leaves the loader's regex
shape, the exact AST-attachment mechanics (record vs. property on
existing `Module`), the visitor-pattern updates, and the audit
ledger format for Session 1.

---

## Notes for downstream sessions

- `harvester-finish-canonical-golden-output` (F2) reconciles
  per-object IR counts against parse-success counts. F2's
  prerequisite is **F1 + this set** (post-regeneration goldens);
  the ROADMAP and `remaining-work-tree.md` reflect this.
- If F2 wants to materialize `Module.Header` fields into the `ir/`
  output (so the downstream-AI consumer reads the header from
  JSON without parsing VBA), that's F2's call. The AST shape this
  set ships accommodates it.
- A future session set may decide to extend `Vba.g4` to natively
  parse `Attribute VB_*` rather than strip-and-store. That would
  retire the loader; this set's `ModuleHeaderMetadata` AST shape
  would survive (just populated from a different code path).
