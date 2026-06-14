# Set 011 — Session 3 re-verification (Round 2): confirm the closeout fixes

> **How to run (operator):** paste into GitHub Copilot twice (Gemini Pro, GPT-5.4)
> in the `dabbler-access-harvester` workspace; save to `s3-verification-round-2-gemini.md`
> and `s3-verification-round-2-gpt-5-4.md`. This is a **narrow** re-verify of the
> two fixes Round 1 produced — not a fresh full review.

> **Bias cautions:** authored by the orchestrator (Claude Opus 4.8), which has an
> opinion. Check whether the fixes actually resolve the Round-1 findings against
> the real repo, not merely whether the prose sounds right. Begin with prose
> analysis before the JSON block.

Round 1 (devil's-advocate, Gemini + GPT, main + set-wide addendum) returned three
live findings. This round confirms the two that were **fixed in place** and the
disclosure for the one that was **deferred**. Verify each against the current
working tree.

**R1 (C1, GPT Major) — FIXED.** §5.7 told consumers to join
`NavigationGraph.TargetName` to a `Name` field on `Forms`/`Reports` entries, and
§4 called every entry a `{ Name, ... }` record. Both were wrong (entries are
`{ Spec, ... }`; name is `Spec.SourceFormName`; Modules use `ModuleName`; only
Queries/Tables/Relationships carry top-level `Name`).
*Confirm:* `docs/harvester/interpretation-guide.md` §5.7 ("Joining edges…") now
directs the join to `Spec.SourceFormName` / the `ir/` filename stem and warns
there is no top-level `Name`; the §4 per-category bullet now gives the correct
name field per category. Does the corrected text match the actual golden
(`samples/GoldenOutput/HomeHealthCare/manifest.json`)?

**R2 (C7b, Gemini Major / GPT not-triggerable) — FIXED via doc.** A `Resolved`
cross-module reference can point to a `VBA_RESOLVE_FAILURE` module that ships as a
`ParserOutput=null` stub (reachable by contract, not triggerable today).
*Confirm:* §4 now has a `VBA_RESOLVE_FAILURE` diagnostic bullet instructing
consumers to null-check a referenced module's `ParserOutput` regardless of the
reference's resolved status (tracked as DW-12 for the latent code hardening). Is
the guidance accurate and sufficient as a consumer guard?

**R3 (C9, GPT Major / Gemini Critical) — DEFERRED, documented.** `unresolved-items.json`
is procedure-body-scoped and undercounts header/module-level unresolved refs.
Operator-adjudicated to defer the code fix (real double-emit design decision;
golden-neutral so needs synthetic pins) to **DW-11**, with the proven reproducer
captured there.
*Confirm:* `docs/planning/deferred-work.md` DW-11 captures the defect + GPT's
reproducer + the double-emit constraint + a focused owning set; and
`interpretation-guide.md` §6 now discloses the procedure-body-scoping caveat and
names the inline `ResolutionReason` as the authoritative complete signal. Is the
deferral honestly disclosed (a consumer will not mistake the index for complete)?

Is there any **new** defect introduced by these edits (e.g. an inaccurate
corrected statement, an internal contradiction in the guide)?

After the prose, output exactly:

```json
{ "verdict": "VERIFIED", "issues": [ { "severity": "Major", "location": "<...>", "summary": "<...>", "fix": "<...>" } ] }
```

`issues` is `[]` iff no Critical/Major issue remains; `verdict` is `"VERIFIED"`
only when `issues` is empty.
