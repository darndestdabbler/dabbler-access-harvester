1. **Known-limitation pin design**

This is **substantively the right design**. You did not silently weaken the invariant to “ignore schema validation on StudentICT”; instead, you encoded the fact that the corpus currently exposes a **real producer defect** and made that defect mechanically visible. That is consistent with the stop rule: the desired invariant remains “schema-valid manifest,” but the current producer does not satisfy it on this corpus, so the test pins the defect until the producer is fixed.

The three-part check is directionally good:

- `schemaResult.IsValid == false`
- error set includes the `StartColumn >= 1` class
- raw output contains a `StartColumn` of `0`

That combination prevents the most obvious dishonest softening. A mere `IsValid == false` would be too weak, because any unrelated schema failure would satisfy it. A mere `"StartColumn": 0` search would be too weak, because it would not prove the schema contract is actually being violated. Together, they show “the manifest is invalid for the reason we think it is.”

The one weakness is **implementation brittleness**, not conceptual dishonesty. Two assertions are presentation-coupled:

- matching validator error text by substring (`"StartColumn"` and `"at least 1"`)
- matching raw JSON text by exact formatting (`"\"StartColumn\": 0"`)

Those can spuriously break if either:
- the schema validator changes wording, or
- the manifest writer changes whitespace/formatting while still emitting numeric zero.

So the design is honest, but one part of the pin is a bit fragile. The more robust variant would be:
- keep `IsValid == false`
- parse the manifest structurally and assert that some `StartColumn` property is numeric `0`
- if the validator API exposes structured paths/codes, assert those instead of message prose

So: **good design, slightly brittle implementation details**.

2. **Corpus-independence and no sensitive/machine-specific assertions**

On the security/sensitivity question, this looks **clean**. The test does **not** assert:
- the plaintext password in the linked-table `Connect` string
- the absolute back-end filesystem path

It asserts only the back-end **filename** (`StudentICTDatabase_BE.accdb`), which is the right level: enough to prove linked FE/BE behavior is present, without binding to secrets or machine-local paths.

The linked-table check is also appropriately shaped:
- non-empty `Connect`
- non-empty `SourceTableName`
- at least one `Connect` references the BE filename

That is a robust current-consumer contract given DW-25.

The relationships assertion is also good:
- `Assert.Empty(manifest.Relationships)` is a real split-FE structural behavior check, not a sensitive or machine-specific one.

The DW-18 witness is similarly robust:
- asserting presence of `"FunctionClassification"` in `unresolved-items.json` is a shape/presence check, not a brittle count pin.

The one place where the test drifts away from “corpus-independent invariant” wording is the **ribbon section**. These are corpus-specific:
- `Assert.Equal("Help", ribbon.Name)`
- tab `Id == "tab1"` and `Label == "Help"`
- resolved module name `modRibbonCallbacks`

Those are not sensitive, but they are **not corpus-independent**. They can break on a benign FE edit that still preserves the structural path you actually care about: “a second custom ribbon exists, entry points are harvested, and callback resolution works.”

So the security posture is correct, but the labeling is slightly overstated. The ribbon assertions are **canary facts about this corpus**, not pure corpus-independent invariants.

3. **DW-26 root-cause correctness**

Your root-cause analysis is **high-confidence and scope-correct**.

What is well-supported:
- The schema contract is 1-based for `StartColumn`.
- HHC honors that contract (`min = 1`, no zeros).
- Northwind validates cleanly with the same validator.
- StudentICT emits `StartColumn: 0`.
- Therefore StudentICT is surfacing a real producer contract violation not seen in the prior corpora.

The “oneOf branch noise” explanation is also technically credible. JSON Schema validators often emit branch-failure chatter such as:
- “should be null”
- alternate `ItemKind` expectations
- similar losing-branch messages

when one branch of a `oneOf` is invalid for a nested reason. Since Northwind validates with the same object-valued shapes, those messages are very likely secondary noise rather than independent semantic failures.

The only caution is wording: saying `StartColumn` is the **SOLE** real failure is a slightly stronger claim than the evidence strictly proves. What the evidence proves is that it is the **only currently evidenced substantive failure class**. In practice that is good enough for a DW and for this test design. After a producer fix, you should rerun validation before declaring there are categorically no residual issues.

On scope: leaving it as a producer-fix DW and **not** changing producer behavior in this onboarding session is absolutely the right call.

4. **DW-25 correctness**

This appears **correct** as stated.

If `Pipeline.BuildTableExports` is reconstructing table records from `ir/Tables/*.ir.json`, and those IR files do not carry a derived `IsLinked`, then it follows that:
- the run-pipeline manifest cannot surface `IsLinked`
- any derivation that exists only in an off-path COM extractor will not appear in the exported manifest

That matches the observed behavior.

Given that current state, “detect linked via `Connect + SourceTableName`” is the right downstream guidance for **today’s manifest contract**. It is also more consumer-relevant than a raw Access attribute bitmask, because it identifies both:
- that the table is linked, and
- what remote/source table it targets.

The only nuance worth preserving in docs is that this is **current-manifest guidance**, not necessarily the ideal long-term semantic contract. If DW-25 adds explicit `IsLinked` and/or attributes into the run manifest later, consumers should prefer the first-class field.

So: root cause is sound, and the consumer guidance is correct.

5. **Skip-gating / no-commit discipline**

This is **correct and disciplined**.

Requiring **both FE and BE** is the right skip gate. For a split FE, the BE is not optional context; it is part of the observable linked-table surface. Gating only on FE presence would risk:
- misleading failures
- incomplete linked-table metadata
- inconsistent local behavior across machines

The local-artifact discipline also looks right:
- staging under `%TEMP%`
- cleanup in `finally`
- no committed golden
- FE/BE/`.lnk` explicitly gitignored

That makes accidental repository contamination unlikely.

I do not see anything in the shown test that would leak the password or absolute path into git-tracked artifacts. The assertions do not print `Connect` contents. The output files live only in temp and are deleted. Even if cleanup failed, remnants would be outside the repo.

So on this concern, the session is sound.

6. **Anything else that would make a downstream AI consumer misread this output**

Two things are most likely to cause misread:

- The comments repeatedly say “corpus-independent invariants,” but some ribbon assertions are actually **corpus-specific canaries**.
- The comments include exact observational facts like “20 FE tables” and “9 linked” which are fine as commentary, but they can go stale and may be misread as asserted contract unless clearly marked as observational.

Neither is a correctness flaw in the session outcome, but both are readability/interpretation risks for future automated or human consumers.

The safer framing is:
- call the linked/relationships/schema/DW-18 checks “structural invariants”
- call exact ribbon names/module names “current corpus canaries”
- keep exact counts in comments/docs explicitly labeled as observed, not asserted

Overall judgment: **the session is fundamentally sound**. The test is honest, appropriately scoped, and careful about secrets. The main issues are minor robustness/positioning issues: one brittle implementation detail in the DW-26 pin, and some ribbon assertions being more corpus-specific than the “corpus-independent invariant” label suggests.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "StudentIctLocalInvariantTests",
      "summary": "The DW-26 known-limitation pin relies on validator message prose and an exact raw JSON substring, which can spuriously fail after harmless validator or formatting changes.",
      "fix": "Keep the IsValid==false pin, but assert StartColumn==0 by parsing the manifest structurally and, if available, use structured schema error paths/codes instead of matching validator text."
    },
    {
      "severity": "Minor",
      "location": "StudentIctLocalInvariantTests",
      "summary": "Several ribbon assertions pin exact corpus-specific names and module identifiers even though the test is described as asserting corpus-independent invariants.",
      "fix": "Either relax the ribbon checks to capability-level assertions (custom ribbon present, OnLoad/OnAction entry points surfaced and resolved) or explicitly document those exact-value checks as corpus-specific canaries."
    }
  ]
}
```