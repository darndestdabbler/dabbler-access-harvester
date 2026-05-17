# Verification Round 1

1. **Spec adherence.**  
The Session 2 code/doc deliverables are mostly present and aligned with the plan:

- **Diff-gate continuity / reconfirmation:** `ParseSuccessReportTests.cs` is still the live gate, and it now routes its diff-message text through `ParseSuccessReportDiff.Build(...)`. That preserves the Session 1 gate path in code.
- **Per-reason narrative:** `unresolved-reason-narrative.md` exists and is substantial.
- **Sentinel diff-message test:** `ParseSuccessReportDiffTests.cs` exists and intentionally mutates a count to assert the human-facing failure message shape.
- **Build+test:** there is **no file-level evidence** in the four in-scope files that `dotnet build && dotnet test` was actually run sequentially, and likewise no file-level evidence of the “fresh regeneration” reconfirmation step. That is a process-verification gap, but not something I can prove or disprove from these four files alone. I would expect that evidence to live in closeout artifacts, which are not in scope here.

So, from the in-scope files alone, the main substantive spec-shape gap is in the narrative: the FunctionClassification section does not actually provide one entry per `(Source, Reason)` pair; it collapses all five reasons into a cross-reference.

2. **Narrative correctness.**  
Coverage is broadly good, but there are two meaningful problems.

Coverage by source/reason looks like this:

- **Resolver:** all five expected reasons are present:  
  `not-in-scope`, `me-in-standard-module`, `default-member-not-yet-resolved`, `with-block-flattening-deferred`, `member-access-on-unresolved-target`.
- **SqlFormReference:** the six stable reasons are present:  
  `bare-collection-reference`, `form-not-in-metadata`, `report-not-in-metadata`, `control-not-on-form`, `control-not-on-report`, `host-collection-not-registered`.  
  There is also a separate subsection for `member-not-found-at-segment-<N>`.
- **DynamicSqlSite:** all three reasons are present:  
  `combinatorial-blowup`, `unbound-variable`, `unknown-expression`.
- **NavigationTarget:** both reasons are present:  
  `non-literal-target`, `missing-target`.
- **FunctionClassification:** the section states it reuses the five Resolver reasons, but it does not spell them out as separate source/reason entries.

The first major issue is the **`member-not-found-at-segment-<N>` subsection**. The document’s own framing says the source of truth is the closed `SuggestedNextStepsLookup`, that the sections cover stable reason codes, and that the “verbatim suggested-next-steps” are pinned from that lookup. But this subsection is explicitly **not** a stable lookup key; it is a dynamic family / prefix case, and the bullets shown are fallback/generic behavior, not a direct lookup entry. That makes the section useful as operator guidance, but it breaks the document’s own contract and the session’s “stable reason strings / do not invent new reasons” convention. As written, it reads too much like an exact reason entry when it is really a templated family plus fallback behavior.

The second major issue is an **internal contradiction in `DynamicSqlSite` / `unknown-expression`**. The prose says the analyzer already recognizes `Replace` and `IIf`, but the “verbatim suggested-next-steps” example says to decide whether to extend the analyzer with a builtin recognizer “e.g., Replace, IIf”. Both cannot be correct at once. That means either:
- the construct-description is stale/wrong about current analyzer support, or
- the suggested-next-steps example is stale / not an accurate copy of the current lookup wording.

Either way, that section needs to be reconciled against the actual analyzer support and the actual lookup text before commit.

Aside from those two sections, the narrative is disciplined and useful. I cannot independently re-prove every bullet is verbatim against the lookup from the material provided here, but the document is mostly structured as if it was copied carefully.

3. **Sentinel test integrity.**  
This part is good.

`ParseSuccessReportDiffTests` **does exercise the production diff-message builder**. It calls `ParseSuccessReportDiff.Build(...)` directly; it is not reconstructing the message in parallel. That satisfies the Session 2 requirement that the sentinel test cover the same message a human sees when the gate fails.

The helper extraction in `ParseSuccessReportDiff.cs` is also lean and appropriate:
- same env var constant is shared,
- equality returns `null`,
- mismatch returns the assembled failure string with fixture name, path, first divergence, snippets, and regeneration hint.

The sentinel test intentionally mutates one count and asserts the message remains actionable. That is exactly the required failure-mode check.

One nuance: the sentinel test asserts key substrings rather than the full exact message. So it is a **human-readability sentinel**, not a byte-for-byte message golden. That is acceptable for the spec as written. I cannot independently prove “byte-identical to Session 1 inline text” from the provided files because the old inline body is not included here, but the shared-helper arrangement does ensure the test now covers the same implementation the live gate uses.

4. **Diff-gate continuity.**  
No live-gate behavior change is visible in the in-scope code.

`ParseSuccessReportTests` still:
- regenerates from the YAML mirror,
- enforces the nonzero sanity checks,
- serializes with the same serializer options,
- writes on `UPDATE_PARSE_SUCCESS_REPORTS=1` or missing golden,
- otherwise diffs against the committed file and throws an `XunitException` on mismatch.

The only refactor is that the diff-message construction moved into `ParseSuccessReportDiff.Build(...)`. That extraction is narrow and does not appear to change the contract.

So on the diff-gate itself, I do **not** see a continuity regression.

5. **Determinism / convention compliance.**  
The code change is lean-by-default. The test helper extraction is small and justified. The reason-code stability convention is where the narrative has a problem:

- Most of the document respects stable exact reason strings.
- The `member-not-found-at-segment-<N>` subsection is the exception: it presents a templated pseudo-reason in a document that otherwise claims exact stable reason coverage and exact lookup-pinned steps.
- The FunctionClassification section also weakens the exact `(Source, Reason)` convention by collapsing five pairs into a source-level summary.

So the determinism/compliance concerns are narrative-only; the test/code changes are fine.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md (`SqlFormReference` / `member-not-found-at-segment-<N>`)",
      "summary": "The narrative presents `member-not-found-at-segment-<N>` like a stable lookup-backed reason entry even though it is a dynamic family/prefix case and its listed next steps are fallback behavior rather than a verbatim `SuggestedNextStepsLookup` entry.",
      "fix": "Rewrite this subsection as an explicit dynamic-prefix/fallback note instead of a normal stable-reason entry, and avoid implying that `<N>` is a literal stable reason code or that its bullets are direct lookup values."
    },
    {
      "severity": "Major",
      "location": "docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md (`DynamicSqlSite` / `unknown-expression`)",
      "summary": "The section is internally inconsistent: it says the analyzer already recognizes `Replace` and `IIf`, but the suggested-next-steps example says to consider adding support for `Replace`/`IIf`.",
      "fix": "Reconcile the prose and the suggested-next-steps against the actual analyzer support and the actual `SuggestedNextStepsLookup` text so the section does not recommend adding support for functions the narrative says are already supported."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md (`FunctionClassification` section)",
      "summary": "The FunctionClassification coverage is collapsed into a cross-reference instead of providing one explicit entry per `(Source, Reason)` pair as the Session 2 plan requested.",
      "fix": "Add five short `FunctionClassification` subentries—one per reason code—even if each mostly points back to the Resolver text, so the document has direct per-pair coverage and pinned lookup guidance in the requested shape."
    }
  ]
}
```

---

# Verification Round 2

1. **R1 Issue 1 — `member-not-found-at-segment-<N>` framing**

The revised subsection is **substantially fixed**. The new title, **“Dynamic-prefix family: `member-not-found-at-segment-<N>`”**, and especially the opening bold paragraph now make the key distinctions explicit:

- it is **not** a stable lookup-backed entry,
- `<N>` is emitted dynamically,
- consumers should match by **prefix**,
- and the listed next steps are **generic fallback**, not a verbatim `SuggestedNextStepsLookup` value.

Within that subsection itself, I do **not** see residual wording that still presents `<N>` as a literal stable reason code.

However, this fix exposed a remaining **document-wide framing mismatch** elsewhere: the opening “Authority for the reason set” text and the closing “Stability commitment” still speak as if the full documented reason set comes from `SuggestedNextStepsLookup`. That is no longer strictly true once this dynamic family is intentionally documented as an exception. So the subsection framing itself is adequate, but the global framing is not fully cleaned up.

2. **R1 Issue 2 — `unknown-expression` analyzer-support contradiction**

This is **resolved**.

The rewritten “Why the producer could not finish” paragraph now says the analyzer recognizes only a narrow set of primitives and that function calls — including `Replace`, `IIf`, `Format`, and `String$` — are treated as unknown holes, with a deferred-gap comment in `DynamicSqlAnalyzer.cs` cited as the basis. That is now consistent with the verbatim next steps telling the reader to consider adding recognizers for examples such as `Replace` and `IIf`.

I do not see any remaining internal contradiction in that section, and I do not see any other place in the document still claiming `Replace`/`IIf` are already recognized today.

3. **R1 Issue 3 — FunctionClassification per-pair coverage**

This is **resolved**.

The section now has five explicit `FunctionClassification` subentries:

- `not-in-scope`
- `me-in-standard-module`
- `default-member-not-yet-resolved`
- `with-block-flattening-deferred`
- `member-access-on-unresolved-target`

Each one is a distinct `(Source, Reason)` entry, each cross-references `Resolver` for the fuller explanation, and each pins the corresponding suggested-next-steps. That satisfies the spec intent behind “one paragraph per `(Source, Reason)` pair”: the pairs are no longer collapsed into a source-level summary.

4. **Post-fix sweep**

There **is** still some stale pre-fix language outside the repaired subsections.

The main remaining problem is that the document still overstates the authority of `SuggestedNextStepsLookup` in a couple of global places:

- **Opening authority block:**  
  “The aggregator's lookup is the source of truth for both the reason codes and the recommended next steps...”  
  That is too broad now that the doc explicitly includes one documented dynamic family whose emitted reasons are not lookup keys and whose steps come from generic fallback.

- **Stability commitment:**  
  “The reason set above is the closed set as of 2026-05-17 in the `UnresolvedItemAggregator.SuggestedNextStepsLookup` map.”  
  This is also too broad for the same reason. The documented content now includes six stable `SqlFormReference` reasons **plus** one dynamic-prefix family outside that map.

There is one softer spot in **Cross-cutting consumer guidance** (“Reason codes are stable strings... including the `member-not-found-at-segment-<N>` family by prefix”), but that sentence is at least immediately qualified by the prefix note, so I would treat it as optional tightening rather than a separate defect.

5. **New drift**

I do **not** see any new substantive contradiction introduced by the R1 fixes in the repaired sections themselves. The `unknown-expression` fix is coherent, and the `FunctionClassification` reshaping is clean.

The only remaining issue is the residual global lookup-authority wording described above. I would classify that as a **Minor** documentation inconsistency, not a new major drift.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md (opening authority block; Stability commitment)",
      "summary": "The document still globally describes `SuggestedNextStepsLookup` as the source of the full documented reason set even though the narrative now intentionally includes the dynamic `member-not-found-at-segment-<N>` family outside that lookup.",
      "fix": "Qualify the global framing to say the lookup is the source of truth for stable lookup-backed reasons and their verbatim steps, while `member-not-found-at-segment-<N>` is a documented dynamic-prefix exception that uses `GenericSuggestedNextSteps` fallback."
    }
  ]
}
```

---

# Verification Round 3

1. **R2 Minor — opening Authority block**

The opening authority framing is now materially correct. It no longer overclaims that `SuggestedNextStepsLookup` is the sole authority for everything in the document; instead, it explicitly splits authority into two parts:

- `UnresolvedItemAggregator.SuggestedNextStepsLookup` for the **stable lookup-backed reasons** and their verbatim next-step lists, and
- `UnresolvedReason.MemberNotFoundAtSegment(int)` for the documented **dynamic-prefix exception**.

That split is internally consistent with the later `SqlFormReference` dynamic-family subsection. The block also correctly states the two operational consequences of that exception: there is **no lookup key** for `member-not-found-at-segment-<N>`, and the emitted next steps come from `GenericSuggestedNextSteps` fallback rather than a verbatim lookup entry.

So, on its own terms, the new opening block is complete and coherent.

2. **R2 Minor — closing Stability commitment**

The closing section is also substantially fixed. It now makes the stable-vs-dynamic distinction explicit enough for a future author to follow:

- stable lookup-backed reasons are the closed set in `SuggestedNextStepsLookup`,
- `member-not-found-at-segment-<N>` is a documented non-lookup dynamic family emitted by `MemberNotFoundAtSegment(int)`,
- stable additions require new lookup-backed entries in the matching source section, and
- new dynamic-prefix families should be documented as clearly flagged family subsections, following the existing pattern.

That is the right pattern, and it is described clearly enough to guide future maintenance. I do not see a remaining ambiguity in the closing section itself.

3. **Residual stale phrasing anywhere else**

There is still one residual document-wide phrasing problem outside those two repaired blocks.

In **Cross-cutting consumer guidance**, the first bullet says:

- new producer reasons without a matching `SuggestedNextStepsLookup` entry “fall through to the generic hint list verbatim” and that this is “the in-output signal that the wiring needs an addition,” and then
- “When this narrative needs to grow, the trigger is a new reason code reaching the `SuggestedNextStepsLookup`...”

That wording is now too broad given the rest of the document. The narrative intentionally documents one reason family that is **supposed** to remain outside the lookup and **supposed** to use generic fallback. The closing Stability section also explicitly says a future **dynamic-prefix family** should be documented as a flagged family subsection. So this cross-cutting bullet still partially re-centers `SuggestedNextStepsLookup` as the effective gate for narrative growth, which is no longer fully true.

I would treat that as the remaining **Minor** issue. The opening and closing blocks are fixed, but this cross-cutting bullet should be qualified the same way: stable lookup-backed reasons grow via the lookup; intentional dynamic-prefix families are a separate documented path.

4. **New drift**

I do not see any new contradiction, typo, or structural mismatch introduced by the R2 edits to the opening or closing sections themselves. Those rewrites are clean and consistent with the detailed dynamic-family subsection.

The remaining problem is residual framing in the cross-cutting guidance, not a new R2-introduced shape break.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md (Cross-cutting consumer guidance)",
      "summary": "A cross-cutting guidance bullet still overgeneralizes lookup membership as the trigger for narrative growth, which conflicts with the document's explicit dynamic-prefix-family exception path.",
      "fix": "Rewrite that bullet to distinguish stable lookup-backed reasons from intentional dynamic-prefix families: stable reasons are added when they land in `SuggestedNextStepsLookup`, while new dynamic-prefix families are documented as flagged family subsections even though they are not lookup keys and may continue to use `GenericSuggestedNextSteps`."
    }
  ]
}
```
