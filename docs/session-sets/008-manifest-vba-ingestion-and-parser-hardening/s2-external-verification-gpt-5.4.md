# S2 External Verification — Copilot GPT-5.4

Date: 2026-06-13

## H1 (S2-1, ambiguity): Admitting `contextualKeywordName` into `primary` causes ambiguity
- **Verdict:** `OVERTURNED`
- **Evidence:** I ran narrow parser probes through `AccessHarvester parse` on the exact collisions the prompt calls out. All of these parsed cleanly:
  - `Sub T() : ReDim Preserve a(10) : End Sub`
  - `Sub T() : ReDim Preserve(10) : End Sub`
  - `Option Compare Database`
  - `Public Declare PtrSafe Function MessageBox Lib "user32" Alias "MessageBoxW" () As Long`
  - `Sub T() : For step = 1 To 10 : Next step : End Sub`
  - `Sub T() : step = "x" : compare = step : End Sub`
  The `ReDim` pair is the strongest discriminator: `ReDim Preserve a(10)` succeeds with `PRESERVE` as the modifier, while `ReDim Preserve(10)` simultaneously succeeds with `PRESERVE` as the array name. That means the grammar is disambiguating the anchored and identifier uses as intended rather than stealing one parse with the other.
- **Severity:** Observation
- **Recommended action:** Add the missing anchored-use pins so this stays proven by tests rather than by one-off probes.

## H2 (S2-2, error-swallow): `catch (Exception) when (HasErrorNode(item))` hides a real builder bug
- **Verdict:** `PARTIAL`
- **Evidence:** The catch is broader than the spec sketch. `BuildModuleItem` now swallows any `Exception` on any module item containing any `IErrorNode`, and `HasErrorNode(item)` walks the entire item subtree. So in principle a future builder bug anywhere inside a partially recovered procedure/type/declare would be downgraded to `UnknownModuleItem`. I could not make that concrete against the current codebase, though. The current obvious throw sites in module-item builders are exactly the recovery-mangled head-token cases this change is trying to convert into stubs (`ctx.IDENTIFIER().GetText()`, `ctx.STRING_LITERAL()[0]`, and similar shape assumptions). I do not have a real current input that proves a non-recovery bug is being hidden.
- **Severity:** Minor
- **Recommended action:** Keep the failure-scoped guard if preserving partial bodies is the priority, but add a regression pin around a malformed procedure that still preserves neighboring items, and consider surfacing the caught exception type/message in debug or test builds. The literal pre-emptive `HasErrorNode` guard would be safer for debugging, but it would also be more lossy than the current design.

## H3 (S2-1 scope): Contextual-keyword scope is too narrow
- **Verdict:** `UPHELD`
- **Evidence:** There is at least one concrete too-narrow site already created by the new scope decision: named arguments. The grammar now accepts a parameter declaration named `step`, but `namedArgument` still insists on `IDENTIFIER`, so the call site cannot spell that same legal parameter name.

  VBA probe:

  ```vba
  Sub F(step As Long)
  End Sub

  Sub T()
      F Step:=1
  End Sub
  ```

  Result: parse failure at `:=` after `Step`.

  I also got concrete parse failures for these additional candidates:

  ```vba
  Type Info
      Step As String
  End Type
  ```

  ```vba
  Public Enum E
      Step = 1
  End Enum
  ```

  ```vba
  Public Function Step() As Long
  End Function
  ```

  The named-argument case is the strongest must-fix because it is internally inconsistent with a declaration shape the grammar now explicitly allows.
- **Severity:** Major
- **Recommended action:** Extend `contextualKeywordName` to `namedArgument` immediately. Then decide, with targeted VBE or corpus confirmation, whether procedure names, type members, and enum members should join the same broadened set. Do not widen every slot blindly; fix the declaration/use symmetry hole first.

## H4 (S2-4): Capturing `TypeSuffix` without emitting it causes issues
- **Verdict:** `UPHELD`
- **Evidence:** `BuildFunctionProcedure` and `BuildPropertyProcedure(Get)` now capture the suffix into `ProcedureDeclaration.TypeSuffix`, but when the source is `Function SqlStr$()` or `Property Get Title$()` with no `As` clause they still leave `ReturnType` null. That is not just "schema work later" because the existing downstream channels already use `ReturnType` today:
  - `ScopeBuildingVisitor` copies only `n.ReturnType` into resolver `FunctionDeclaration` and `PropertyDeclaration`.
  - `TypeInferencePass.TypeSuffixEvidence()` and `TypeOfBoundDeclaration()` only look at variable/implicit/parameter declarations, not procedures.
  - `ParserOutputProjector.BuildSignature()` emits `ReturnType` only when non-null.
  - `SymbolDb.SignatureFormatter.FormatProcedure()` falls back to `Void` when `ReturnType` is null.

  So `Function SqlStr$()` now parses successfully but still projects as a function with no manifest `Signature.ReturnType`, and SymbolDb will signature-hash it as `Function(...):Void`. That is a real semantic loss on a newly accepted legal input.
- **Severity:** Major
- **Recommended action:** Populate the existing return-type channel this session. The smallest fix is to derive `ReturnType` from `TypeSuffix` whenever a function or property-get has no explicit `As` clause. This does not require a schema change; it uses an existing field that is already emitted and consumed.

## H5 (better strategy): Overlooked architectural approach
- **Verdict:** `OVERTURNED`
- **Evidence:** I do not see a materially better whole-strategy than the current one. Carrying `TypeSuffix` separately is already the repo's established representation for variables, parameters, loop variables, and identifier expressions; extending that convention to procedures is consistent with the rest of the AST and with the resolver's existing suffix-based inference model. The real problem is not the representation choice; it is the incomplete hand-off into the existing procedure `ReturnType` channel (H4). A direct "erase the suffix and synthesize a `TypeReference` in AstBuilder" approach would also throw away raw spelling and make contradictory or recovery-shaped inputs harder to represent cleanly.
- **Severity:** Observation
- **Recommended action:** Keep the separate `TypeSuffix` field. Fix the missing bridge from procedure `TypeSuffix` to existing `ReturnType` and projection logic instead of replacing the representation.

**Overall:** This slice is **not yet safe to treat as closed**. H1 does not show the feared ambiguity regression, and H2 is defensible, but H3 has a concrete symmetry hole at named arguments and H4 drops return-type information on the newly accepted suffixed procedures. I would fix those before locking in more pins or golden work.
