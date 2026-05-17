# Cleanup Pass Review Report

## 1. Verdict: **VERIFIED**

The cleanup pass correctly lands all 11 claimed SR fixes. No prior fixes are regressed. The schema, sample IR, and generator remain coherent. The four findings below are all minor/low-severity new gaps introduced by the SR-3 string operator additions; none warrants blocking the pass.

---

## 2. Findings

### F-1 (Medium) — Pattern-variable collision in string operator emits

**Location:** `ModalDetailFormGenerator.cs`, `EmitOperator` switch arms for `_left`, `_right`, `_mid`.

**Observation:** Each arm introduces a hardcoded pattern variable (`__s0` for `_left`, `__s1` for `_right`, `__s2`/`__s3` for `_mid`). If the same operator appears nested inside itself — e.g., `{ _left: [{ _left: [expr, 5] }, 3] }` — the generated C# emits two `is var __s0` declarations in the same expression scope. C# will reject this with CS0128 (duplicate local variable).

**Failure mode:** Clean compile error — no silent misbehavior.

**Recommendation:** Generate a unique suffix per emission site (incrementing counter on the generator instance) instead of a hardcoded `__s0`–`__s3`.

---

### F-2 (Low) — `_mid` / `_left` boundary safety vs. Access semantics

**Location:** `ModalDetailFormGenerator.cs`, `_mid` and `_left` emit arms.

**Observation:** Access `Mid(s, start)` where `start > Len(s)` returns `""`. The emitted C# calls `Substring(start - 1)` which throws `ArgumentOutOfRangeException` when `start - 1 > s.Length`. Same issue applies to `_left` when the length argument exceeds the string length — though `Math.Min` clamps the length, the pattern is inconsistent with `_mid`.

**Recommendation:** Clamp the start index for `_mid`: `Math.Min(__s.Length, Math.Max(0, start - 1))`. The 2-arg form's `Substring` from a clamped-to-length index returns `""`, matching Access.

---

### F-3 (Low) — Documentation / schema pattern discrepancy for SR-G

**Location:** `schema-refinements.md`, SR-G recommendation text vs. the actual schema.

**Observation:** The refinements doc recommends `^on[a-z][a-zA-Z0-9]*$` (lowercase after `on`). The schema correctly uses `^on[A-Z][a-zA-Z0-9]*$` (uppercase after `on`), which matches the actual convention in the sample (`onInit`, `onSave`, etc.) and standard `on + PascalEvent` naming. The doc text is stale/incorrect; the schema is right.

**Recommendation:** Update the SR-G entry in `schema-refinements.md` to reflect the actually-landed pattern `^on[A-Z]…`.

---

### F-4 (Low) — `DateDiffAccess` / `DatePartAccess` — "w" vs "ww" conflation

**Location:** `ModalDetailFormGenerator.cs`, `DateDiffAccess` and `DatePartAccess` helper emits.

**Observation:** In Access, `"w"` means *weekday* (day-of-week ordinal) while `"ww"` means *calendar week*. The emitted helpers treat both identically in `DateDiff` (both divide by 7) and `DatePart` omits `"w"` and `"ww"` entirely (falls through to the day default). Access `DatePart("w", d)` returns 1–7 (day of week); `DatePart("ww", d)` returns the ISO-ish week number.

**Recommendation:** In `DatePartAccess`, add `"w" => (int)date.DayOfWeek + 1` (Access is 1-based, Sunday = 1) and `"ww" => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, …)`. In `DateDiffAccess`, document that `"w"` is treated as `"d"` (matching Access's `DateDiff` behavior for `"w"`).

---

## 3. Confirmation List

| Item | Status | Verification |
|---|---|---|
| **SR-J not regressed** | ✅ | `_and`, `_or`, `_not`, `_iif` all wrap operands in `System.Convert.ToBoolean(…)`. Generated `OnSave` validation expressions confirm coercion is live. |
| **SR-C not regressed** | ✅ | `if/then/else` blocks on TextBox, ComboBox, and CheckBox all enforce `_request`-only bindings when `readOnly ≠ true`. Sample's `txtRemaining` uses a `_sub` expression and is `readOnly: true` — validates correctly. |
| **SR-D not regressed** | ✅ | No ListBox/DataGrid in the control catalog `oneOf`. Schema and generator both reject unknown block types. |
| **SR-1 (field on Validate)** | ✅ | Schema: `field` is optional on rule items, `additionalProperties: false` still passes. Generator: two rules carry `field`, one does not; generated code emits `SetFieldError` only for the two with `field`. `_hasFieldErrors` flag correctly gates the stub emission. |
| **SR-3 (24 operators)** | ✅ | All 24 new operators present in schema `oneOf` branches with correct arities. Generator emits C# for each. Domain aggregates and `_like` emit `/* TODO */` placeholders. Date helpers compile and cover the standard interval codes. |
| **SR-A (cardinality)** | ✅ | `loadPlan` → `single` → `Dictionary<…>?`. `loadNeedsForClient` → `list` → `List<Dictionary<…>>?`. `savePlan` / `deletePlan` → `command` → no field declared, `await ExecuteCommandAsync(…)` with no assignment. `GetCardinality` defaults to `"single"` for pre-cleanup IRs. |
| **SR-F (state initializers)** | ✅ | `budgetId: null` → `protected object? budgetId;` (no explicit init, C# default is null). `isDirty: false` → `protected bool isDirty = false;`. |
| **SR-G (event name pattern)** | ✅ | Schema enforces `^on[A-Z][a-zA-Z0-9]*$`. All five sample events match. Generator's `MapEventNameToMethod` PascalCases correctly (`onSave` → `OnSave`). |
| **SR-H (visible / enabled)** | ✅ | `TryGetGuardExpr` returns `null` for absent or literal-`true` properties (no wrap). For non-trivial expressions, `@if (Convert.ToBoolean(…))` wraps the block; `Disabled='@(!(…))'` is emitted on controls. `@if` blocks inside `<MudStack>` children are valid Razor. |
| **SR-I (operators → methods)** | ✅ | `operators.isBudgetExceeded` → `protected object? OpIsBudgetExceeded() => …;`. `_op: "isBudgetExceeded"` → `OpIsBudgetExceeded()`. Missing `_op` reference → undefined method → clean compile error (acceptable failure mode). |
| **SR-K (debounce TODO)** | ✅ | Generator reads `debounce.delayMs` and emits `// TODO: debounce Nms` comment. Not exercised in sample but code path confirmed by inspection. |
| **SR-L (Section title suppression)** | ✅ | `_modalTitle` set to `"FY-Budgeted Service Plan"` for `modalType: modal`. Root Section title matches → suppressed. Generated `.razor` confirms no `<MudText Typo="Typo.subtitle1">` for the root Section. `lblAppTitle` Label preserved (author's choice). |
| **SR-O (dataType dispatch)** | ✅ | `txtFiscalYear` (`dataType: int`) → `MudNumericField T="int"`. `txtAnnualBudget` (`dataType: decimal`) → `MudNumericField T="decimal"`. Default `string` → `MudTextField T="string"`. `date` → `MudDatePicker` with `Date`/`DateChanged` attributes (not `Value`/`ValueChanged`). Lambda type for DatePicker correctly falls back to `System.DateTime?` when `tParam` is null. |
| **SR-P (sync handlers)** | ✅ | `OnFiscalYearChanged` contains no async actions → emitted as `protected Task OnFiscalYearChanged()` with `return Task.CompletedTask;`. No trailing `await Task.CompletedTask`. Async handlers (`OnSave`, `OnCancel`, `OnDelete`) correctly retain `async Task`. |
| **Backward compat** | ✅ | All new properties default safely: `cardinality` → `"single"`, `dataType` → `"string"`, `field` → absent (no `SetFieldError`), `operators` → absent (no methods emitted), `visible`/`enabled` → absent (no wrapping). A pre-cleanup IR produces the same output shape as before. |

---
**Verification (gpt-5-4, openai):** ISSUES_FOUND

**Issue 1** [? / ?]: S FOUND

The review under evaluation is not reliable as written. Its **VERIFIED** verdict is too strong, it misses at least two material issues, and two of its own findings contain incorrect reasoning.

### 1. It missed a real compile-breaking issue
**SR-P is not fully verified.**

The generator now emits non-async `Task` handlers for sync events, but `EmitAction(... "Validate" ...)` still emits a bare `return;`.

That is fine inside `async Task` methods, but it is **not valid** inside a non-async method returning `Task`.

So an event containing only sync actions plus `Validate` will generate code that does not compile. The reviewed answer explicitly marks SR-P as verified, which is incorrect.

### 2. It missed a real SR-1 gap
**The `SetFieldError` stub-gating logic is not fully sound.**

`_hasFieldErrors` is computed by `AnyValidateRuleHasField()` / `HasFieldRule()`, but that scan only looks at top-level `actions`, `tryActions`, and `catchActions`. It does **not** recurse into nested action arrays under `Confirm.onConfirm` / `onCancel`.

`EmitAction`, however, *does* recurse into nested `Confirm` branches and will emit `SetFieldError(...)` there.

Result: an IR with a field-scoped `Validate` only inside a nested `Confirm` branch can generate `SetFieldError(...)` calls **without** emitting the stub method, causing a compile failure.

The reviewed answer says the `_hasFieldErrors` gating is correct. It is not.

### 3. Its operator review is incomplete relative to the prompt
The original task explicitly called out `_mid`, `_instr`, and date helpers.

The reviewed answer caught `_mid`, but it missed at least:

- **`_instr` 3-arg out-of-range behavior**: start positions greater than the string length can still throw in generated C#, whereas Access returns 0.
- **Additional date-helper gaps**, especially incomplete interval handling beyond just `"w"` / `"ww"`; e.g. `"y"` is not handled explicitly.

So its “SR-3 verified except minor gaps” conclusion is incomplete.

### 4. One of its findings is partly a false positive
**F-2 is overstated / partly wrong.**

It says the same boundary issue applies to `_left` when requested length exceeds string length. But the current emit already clamps length with `Math.Min(__s.Length, requestedLen)`, so **overlong `_left` does not have that problem**.

The real boundary problem is `_mid` (and `_instr`), not `_left` in that specific case.

### 5. One recommendation is based on incorrect Access semantics
**F-4 contains wrong reasoning.**

The review says to document `DateDiff("w", ...)` as effectively `"d"` / day-difference behavior. That is not Access semantics.

So while it is right that `"w"` and `"ww"` are mishandled, its proposed interpretation of `"w"` is not trustworthy.

### 6. It overstates SR-O verification
The answer treats `dataType` dispatch as broadly verified. In the provided generator, `dataType` handling is implemented in `EmitTextBox`, but not equivalently across all schema branches that now expose `dataType` (notably `ComboBox` / `CheckBox`).

That makes the confirmation broader than the code supports.

## Bottom line
The reviewed answer should **not** have concluded `VERIFIED`. It missed substantive issues and includes some incorrect reasoning of its own.
