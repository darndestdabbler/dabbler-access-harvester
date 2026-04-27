# Error-Recovery Strategy — VBA Parser Pipeline

> **Authored:** 2026-04-26 by Session 1 of `parser-infrastructure` (Opus 4.7
> orchestrator, in-conversation per `project-guidance.md` →
> "When the orchestrator's model matches the routing target...").
> Synthesis 1 of 2 for this set; AST shape is Session 2.
> **Inputs:** `docs/VBA-Parser-Migration-Harvester-Plan.md` Phase 2.2;
> `docs/spec-coverage/parse-failure-log.md`; existing
> `tests/AccessHarvester.Vba.Grammar.Tests/Modules/VbaCorpusParserTests.cs`
> harness; `AccessHarvester.Vba.Grammar/Vba.g4` rules `module`,
> `moduleItem`, `block`, `statementListBody`, `singleLineStatementList`.

---

## 1. Posture

The grammar (`Vba.g4`) is complete and parses 15/15 fixtures cleanly under
`BailErrorStrategy` (the existing strict harness). Real Access modules,
however, contain malformed code: half-finished refactorings, copy-paste
errors, comment markers stripped from would-be comments. The pipeline must
parse these into a usable partial result rather than throwing.

Two non-negotiables from the spec:

1. **Errors are AST-level annotations, not exceptions.** Downstream consumers
   read errors from `ParseResult.Diagnostics`. They never wrap `Parse()` in
   `try/catch`.
2. **Provenance is explicit when degraded.** When recovery causes a node to
   be reconstructed across an error boundary, the node carries
   `Recovered { reason }` provenance — the parser-level analogue of the
   resolver's "unresolvable constructs are explicit" rule.

## 2. Three modes — Strict / Recovering / Lossy

```csharp
public enum ParseMode { Strict, Recovering, Lossy }
```

- **Strict** — `BailErrorStrategy` + throwing listener. First lex/parse error
  raises. This is the existing harness behavior, kept intact so
  `VbaCorpusParserTests` and any future "this fixture must parse cleanly"
  test can opt in.
- **Recovering** — ANTLR `DefaultErrorStrategy` + diagnostic-collecting
  listener + statement-boundary sync overrides (§4 below). Default mode for
  any production parse. Always returns a parse tree.
- **Lossy** — auto-engaged when recovering produces more than
  `LossyFallbackErrorThreshold` diagnostics (default 25), or requested
  explicitly. In Session 1 this mode shares the recovering pipeline and
  appends a single `VBA0050 lossy-fallback-engaged` diagnostic; the
  procedure-header salvage scan (§6) is the planned extension point. The
  API surface is established now so consumers do not have to be changed
  later.

The mode-selection contract:

```
RequestedMode == Strict       -> Strict; never falls back.
RequestedMode == Recovering   -> Recovering; auto-promotes to Lossy if
                                 diagnostic count > threshold.
RequestedMode == Lossy        -> Lossy directly.
```

## 3. Diagnostic shape

```csharp
public sealed record ParseDiagnostic(
    DiagnosticSeverity Severity,
    string Code,                        // VBAxxxx, stable
    string Message,                     // human-readable
    int Line, int Column,               // 1-based line, 0-based column (ANTLR convention)
    int StartIndex, int StopIndex,      // character offsets into source, inclusive
    string? OffendingTokenText = null,
    IReadOnlyList<string>? ExpectedTokens = null);
```

Stable codes lock the contract for downstream tests and AI consumers:

| Code      | Meaning                                                      |
|-----------|--------------------------------------------------------------|
| `VBA0001` | Lexer: unrecognized character (`ERROR_TOKEN` rule fired)     |
| `VBA0002` | Parser: extraneous input / mismatched token                  |
| `VBA0003` | Parser: missing token (single-token insertion recovery)      |
| `VBA0004` | Parser: no viable alternative                                |
| `VBA0050` | Pipeline: lossy fallback engaged (count exceeded threshold)  |

Codes are namespaced for the parser pipeline. The resolver and downstream
sets reserve their own ranges (`VBA1xxx` resolver, `VBA2xxx` schema, etc.).

## 4. Synchronization strategy — where recovery anchors

VBA's grammar gives the parser two natural sync points: **statement
separators** (`NEWLINE`, `COLON`) and **module-level item boundaries**
(procedure-header keywords `SUB | FUNCTION | PROPERTY`, `END`, plus
`OPTION | DECLARE | TYPE | ENUM | IMPLEMENTS | DIM | PUBLIC | PRIVATE |
GLOBAL | CONST | EVENT`).

The default `DefaultErrorStrategy.GetErrorRecoverySet` returns the FOLLOW
set of the active rule. For `block`, this naturally lands on `NEWLINE`
because the grammar threads NEWLINE between every block. For `moduleItem`,
it lands on the leading-keyword set above. We override
`GetErrorRecoverySet` to return a **union** of the rule's FOLLOW and the
above two sync sets, which keeps recovery from skipping past a procedure
header when the broken statement is mid-procedure.

This avoids the cascading-recovery failure mode where one missing `End
Sub` causes the parser to keep reading subsequent procedure declarations
as continuations of the still-open body. The procedure-header tokens are
treated as hard sync anchors at module level.

The override lives in `StatementBoundaryErrorStrategy : DefaultErrorStrategy`
under `AccessHarvester.Vba.Parser/`.

## 5. Public API — the surface Session 7+ depends on

```csharp
public static class VbaParserPipeline
{
    public static ParseResult Parse(string source, ParseOptions? options = null);
}

public sealed record ParseOptions(
    ParseMode RequestedMode = ParseMode.Recovering,
    int LossyFallbackErrorThreshold = 25,
    string? SourcePath = null);

public sealed record ParseResult(
    VbaParser.ModuleContext ParseTree,
    IReadOnlyList<ParseDiagnostic> Diagnostics,
    ParseMode ModeUsed);
```

Session 1 returns the raw ANTLR `VbaParser.ModuleContext`. Session 2 layers
the AST visitor on top — its signature will be
`Module Build(VbaParser.ModuleContext, IReadOnlyList<ParseDiagnostic>)` —
without changing this pipeline's surface.

`SourcePath` flows into diagnostic messages but **not** into the
`ParseDiagnostic` record itself; the project-wide `SourceLocation` /
`SourceRange` types arrive in Session 3 and will absorb file-path tracking
at that point. Holding off avoids two source-position types existing
simultaneously.

## 6. Lossy-fallback design — established surface, deferred salvage

The recovering pass produces a tree even on heavily malformed input — ANTLR's
default recovery is fairly aggressive. The **practical** limit is when a
structural error (missing `End Sub`, runaway string literal, or stray `End
If` between procedures) corrupts the parser's idea of the module-level
position so badly that diagnostics balloon into the dozens.

The Lossy escape hatch as designed:

1. The recovering pass runs. If `Diagnostics.Count >
   LossyFallbackErrorThreshold`, append `VBA0050` and return as `Lossy`.
2. The **planned** procedure-header salvage scan (a later session adds
   this if the corpus needs it) splits the source at lines matching
   `^\s*(Public|Private|Friend)?\s*(Static\s+)?(Sub|Function|Property)\s+`
   and re-parses each procedure region in isolation through the existing
   `module` start rule (with the body wrapped in a synthetic procedure
   shell when needed). Per-procedure parses are independent, so a
   broken-`End Sub` in one body does not poison neighbors. Failures
   degrade to a `VBA0051` per-procedure marker.

Session 1 ships only step 1 because (a) the existing 15-fixture corpus
contains no malformed input that triggers fallback, so the salvage scan
would be untested speculation; (b) implementing it requires lexical-line
scanning logic that overlaps with what the resolver already needs for
event-handler binding (`resolver-references-and-with-blocks` #8), so
deferring lets that session reuse one implementation. The mode enum,
threshold, fallback wiring, and `VBA0050` diagnostic are all in place so
the salvage scan slots in without API changes when corpus evidence
demands it.

This is "lean by default" applied: the **strategy** is committed (the
escape hatch exists, the contract is honest), but speculative salvage
code is not landed before evidence. Session 4's cross-cut against the
full corpus is the trigger that determines whether the salvage scan
becomes urgent or stays deferred.

## 7. AST provenance hook (forward contract for Session 2)

Session 2 will introduce the AST node hierarchy. Each node will carry a
`Provenance` discriminated union:

```csharp
public abstract record Provenance;
public sealed record SourceProvenance(int Line, int Column) : Provenance;
public sealed record RecoveredProvenance(string Reason, int Line, int Column) : Provenance;
```

Session 1 does not define these types (Session 2 owns the AST), but the
recovery pipeline records enough information in `ParseDiagnostic` for
Session 2's visitor to detect "this AST node was reconstructed across an
error" and tag it `Recovered`. The detection rule: any AST node whose
underlying parse-tree subtree contains an `IErrorNode` is `Recovered`;
the reason string is the nearest preceding `ParseDiagnostic.Message`.

## 8. What this session lands

- New project `AccessHarvester.Vba.Parser/` (net10, references `Vba.Grammar`).
- `ParseMode`, `ParseOptions`, `ParseDiagnostic`, `ParseResult`, and
  `DiagnosticSeverity` types.
- `DiagnosticCollectingErrorListener` (one class implementing both the
  parser and lexer error-listener interfaces — wires through the same
  collection).
- `StatementBoundaryErrorStrategy : DefaultErrorStrategy` with the
  `GetErrorRecoverySet` override.
- `VbaParserPipeline.Parse(string source, ParseOptions? options)`.
- New test project `tests/AccessHarvester.Vba.Parser.Tests/` covering:
  1. `Strict` on `01-simple-validation.bas` produces a tree, zero
     diagnostics.
  2. `Strict` on a synthetic mid-procedure-garbage source throws (the
     existing harness behavior, unchanged).
  3. `Recovering` on the same synthetic source produces a tree, exactly
     one diagnostic, and the diagnostic line matches the injected line.
  4. `Recovering` on a synthetic missing-`End Sub` source produces a tree
     and at least one diagnostic, but does **not** misclassify the next
     procedure header as a body continuation (asserts that the
     subsequent `Sub` is still parsed as a top-level `procedureDeclaration`).
  5. `Recovering` on heavily malformed input (>25 errors) auto-promotes
     to `Lossy` and includes `VBA0050`.
  6. Lexer error: an isolated `~` character (no current grammar rule)
     produces `VBA0001` with the correct column.
- `AccessHarvester.sln` updated to include both new projects.

## 9. What this session does NOT land

- AST node hierarchy (Session 2).
- `SourceLocation` / `SourceRange` types (Session 3).
- Preprocessor fixture `13-preprocessor.bas` (Session 3).
- AST-vs-source schema-validation cross-cut (Session 4).
- Removal of the `ERROR_TOKEN : .` lexer catch-all (an open item from
  `parse-failure-log.md`; gated on Session 4's confirmation that all
  token gaps are closed).
- Procedure-header salvage scan inside Lossy mode (deferred per §6).

## 10. Open items handed to later sessions in this set

- **Session 2 (AST shape)**: implement the `Provenance` discriminated
  union (§7) and the visitor that walks the parse tree, tagging nodes
  whose subtree contains `IErrorNode` as `Recovered`. Layer on top of
  `VbaParserPipeline.Parse`.
- **Session 3 (Position tracking)**: introduce `SourceLocation` /
  `SourceRange`. Migrate `ParseDiagnostic`'s line/column fields onto
  `SourceRange` (this is a breaking change to the diagnostic surface but
  is owned within this set, so no external consumer cares yet).
- **Session 4 (Cross-cut)**: run the recovering pipeline against the
  full 15-fixture corpus + the new `13-preprocessor.bas` and confirm
  zero diagnostics on the clean corpus. Decide based on the actual
  failure profile whether the procedure-header salvage scan (§6) is
  needed for the malformed-VBA case or whether ANTLR's default recovery
  plus the statement-boundary sync is sufficient.
