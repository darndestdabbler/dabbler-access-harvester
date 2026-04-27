# Verification Round 1

ISSUES_FOUND

- Issue: `VBA0001` is not actually produced for the documented `ERROR_TOKEN` path, so the published diagnostic-code contract is not implemented.
  - Severity: Major
  - Location: `AccessHarvester.Vba.Parser/DiagnosticCollectingErrorListener.cs:59-73`, `AccessHarvester.Vba.Parser/VbaParserPipeline.cs:84-93`, `tests/AccessHarvester.Vba.Parser.Tests/VbaParserPipelineTests.cs:139-154`
  - Recommendation: Emit `VBA0001` when the lexer produces `ERROR_TOKEN` tokens instead of waiting for a parser error. The current lexer `SyntaxError` callback is effectively unreachable with the intentional catch-all rule, and the test was weakened to allow any parser diagnostic. Detect `ERROR_TOKEN` in the token stream, record line/column/text/indexes there, and tighten the `~` test to require `VBA0001`.

- Issue: The missing-`End Sub` regression test does not validate the behavior it claims; `ParseTree.GetText()` containing `Second` does not prove `Second` was parsed as its own top-level procedure.
  - Severity: Major
  - Location: `tests/AccessHarvester.Vba.Parser.Tests/VbaParserPipelineTests.cs:101-120`
  - Recommendation: Assert structurally on the parse tree: enumerate top-level `moduleItem` / `procedureDeclaration` contexts and verify there are distinct declarations for `First` and `Second`, with `Second` not nested under `First`'s body. As written, the test would still pass if recovery absorbed the second header into the first procedure's malformed body.

- Issue: The recovery sync set is incomplete for procedure headers; valid header-leading tokens such as `FRIEND` and bare `STATIC` are not anchors, so recovery can still consume part of the next procedure header before resynchronizing.
  - Severity: Major
  - Location: `AccessHarvester.Vba.Parser/StatementBoundaryErrorStrategy.cs:14-31`
  - Recommendation: Add all grammar-recognized module-item lead tokens used before `Sub`/`Function`/`Property` to the sync set, at minimum `FRIEND` and `STATIC` if `Vba.g4` exposes them. Add regression tests with `Friend Sub` and `Static Sub` following a malformed procedure body.

- Issue: `SourcePath` is exposed in the public API but ignored everywhere, despite the session design stating it should flow into diagnostic/exception messages.
  - Severity: Minor
  - Location: `AccessHarvester.Vba.Parser/ParseOptions.cs:3-6`, `AccessHarvester.Vba.Parser/VbaParserPipeline.cs:12-93`, `AccessHarvester.Vba.Parser/ThrowingErrorListener.cs:14-33`
  - Recommendation: Either thread `SourcePath` into emitted diagnostic/strict-mode exception messages now, or remove/defer the option until Session 3 so the public surface does not lock in dead configuration.

- Issue: The mid-procedure-garbage test no longer enforces the synthesis doc's stated contract of a single diagnostic for the synthetic fixture, so recovery-noise regressions would pass unnoticed.
  - Severity: Minor
  - Location: `tests/AccessHarvester.Vba.Parser.Tests/VbaParserPipelineTests.cs:86-99`
  - Recommendation: Tighten the assertion for this controlled input to the intended count/code/line contract, or at least cap the allowed diagnostics and assert the specific primary diagnostic rather than just `NotEmpty`.

---

# Verification Round 2

## ISSUES_FOUND

- **Issue:** Round-1 Issue 1 — **Closed**. `VBA0001` is now produced for `ERROR_TOKEN` via token-stream scanning, and the regression test now asserts the actual code and line.
  **Location:** `AccessHarvester.Vba.Parser/VbaParserPipeline.cs` (`ParseRecovering`, `ScanForLexerErrors`); `tests/AccessHarvester.Vba.Parser.Tests/VbaParserPipelineTests.cs` (`Recovering_OnUnrecognizedCharacter_ReportsVBA0001`)
  **Fix:** None.

- **Issue:** Round-1 Issue 2 — **Closed**. The missing-`End Sub` test now validates the structural claim by inspecting top-level `moduleItem()` procedure declarations and confirming both procedures are preserved.
  **Location:** `tests/AccessHarvester.Vba.Parser.Tests/VbaParserPipelineTests.cs` (`Recovering_OnMissingEndSub_KeepsNextProcedureAsOwnTopLevelDeclaration`, `CollectTopLevelProcedureNames`, `FindFirstIdentifierUnder`)
  **Fix:** None.

- **Issue:** Round-1 Issue 3 — **Remains open**. `FRIEND` is fixed, but `STATIC` is not behaviorally resolved. A missing-`End Sub` followed by `Static Sub Second()` can still be absorbed as a local `Static` declaration. Adding `STATIC` to `SyncTokens` and documenting the limitation does not fully close the original concern for legal next-procedure headers.
  **Location:** `AccessHarvester.Vba.Parser/StatementBoundaryErrorStrategy.cs` (`SyncTokens`); `tests/AccessHarvester.Vba.Parser.Tests/VbaParserPipelineTests.cs` (documented omission of the `Static Sub` regression)
  **Fix:** Add a real `Static Sub Second()` regression test and implement context-aware recovery that can exit the open procedure-body context when `STATIC` is followed by a procedure-header shape, or land the deferred salvage scan that restores top-level procedure detection for this case.

- **Issue:** Round-1 Issue 4 — **Closed**. `SourcePath` now propagates into recovering diagnostics, lexer-scan diagnostics, lossy diagnostics, and strict-mode exception messages.
  **Location:** `AccessHarvester.Vba.Parser/DiagnosticCollectingErrorListener.cs`; `AccessHarvester.Vba.Parser/ThrowingErrorListener.cs`; `AccessHarvester.Vba.Parser/VbaParserPipeline.cs`; source-path tests in `VbaParserPipelineTests.cs`
  **Fix:** None.

- **Issue:** Round-1 Issue 5 — **Closed**. The mid-procedure-garbage test is now materially stronger: it bounds cascade size and constrains diagnostic codes to the expected parser/lexer set.
  **Location:** `tests/AccessHarvester.Vba.Parser.Tests/VbaParserPipelineTests.cs` (`Recovering_OnMidProcedureGarbage_ReturnsBoundedDiagnosticSet`)
  **Fix:** None.

- **Issue:** New defects introduced by the submitted fixes — **None identified beyond Round-1 Issue 3 remaining behaviorally incomplete**.
  **Location:** N/A
  **Fix:** N/A

---

# Verification Round 3

`SIDE_WITH_ORCHESTRATOR`

The deferral outlined in the synthesis doc Â§6 is a sound and deliberate engineering decision, not an oversight. It correctly identifies the `STATIC` keyword's behavior during recovery as a fundamental grammar ambiguity requiring a more complex solutionâ€”the salvage scanâ€”than the statement-boundary synchronization implemented in Session 1. The strategy to defer this speculative work until Session 4 can provide corpus evidence of its necessity is a lean, data-driven approach, preventing premature implementation of a potentially unneeded feature. The documented limitation in the test code is an adequate and transparent hand-off, acknowledging the current behavior while waiting for the planned evaluation trigger. Finally, the successful resolution of the non-ambiguous `FRIEND` case demonstrates sufficient progress on the original issue class for this session, proving the core recovery mechanism's value where the grammar allows.
