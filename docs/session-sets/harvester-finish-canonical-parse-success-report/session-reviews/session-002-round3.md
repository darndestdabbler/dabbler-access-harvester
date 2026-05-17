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