1. **Golden faithfulness**

Based on the evidence provided, the regenerated goldens are faithful to a fresh run of the **already-narrowed** cleanup classifier. The key verification points all line up:

- The chosen regeneration run (`--no-com`) produced **0 findings** for both `HomeHealthCare` and `NorthwindDevEdition`.
- The committed `object-cleanup.json` files for both fixtures show:
  - `dateSignalSource: "dao-metadata"`
  - `degradationNotes: []`
  - `findings: []`
- The committed checklist files show empty review lists:
  - `Review: []`
- You explicitly confirmed via grep that none of the stale/problematic tokens remain in regenerated artifacts:
  - no `CrossReferenceAbsence`
  - no `frmSwitchboard`
  - no `mcrSwitchboard`
  - no `tblSwitchboardItems`
  - no `_Param`

That is exactly what Session 1 was supposed to accomplish: refresh stale consumer-facing goldens so they match the narrowed classifier shipped in set `001`, without reintroducing the old cross-reference-only behavior or launcher/parameter false positives. The encoding note also helps: preserving LF + UTF-8 BOM avoids accidental artifact noise and suggests this was a clean refresh rather than a format drift. On the supplied record, concern 1 checks out.

2. **Regeneration-mode decision**

Committing the `--no-com` goldens instead of COM-sampled goldens is defensible, and in this case it is the more correct release choice.

The spec says to **prefer** COM date sampling on a Windows+Access host, but it also includes a stop rule: if a fresh cleanup run surfaces behavior that looks wrong, surface it rather than “fixing” it here. That is exactly what happened for `HomeHealthCare` under COM:

- two ordinary date findings on synthetic tables, which are already weak evidence because the fixture is synthetic
- one clearly suspect `DateSignal` on `tblClientServiceNeeds` where `1899-12-30` is the Access/OLE zero-date for a time-only value, reported as ~46k days stale

That last case is not “fresh data exposing stale objects”; it is a date-interpretation edge case. Under the stop rule, baking that into committed goldens would be the wrong move for this session.

The additional reproducibility facts strengthen the decision:

- `HomeHealthCare` is synthetic, so COM freshness signals are not especially authoritative there.
- `NorthwindDevEdition`’s `.accdb` is gitignored/proprietary, so COM-derived goldens are not reproducible from a fresh clone.
- `--no-com` is deterministic from committed IR and does not depend on local Access/COM state or mutable database files.
- The maintainer explicitly confirmed `--no-com`.
- Cross-provider consensus was unanimous in favor of the deterministic path.

Documentation also appears adequate:
- the committed goldens themselves record `dateSignalSource: "dao-metadata"`
- the feedback doc records both the mode used and the reason it was used
- the COM anomaly is preserved as a deferred classifier concern rather than silently discarded

So the decision is not just acceptable; it is the session-appropriate choice consistent with release hygiene, reproducibility, and the spec’s stop rule.

3. **Dead-code-removal safety**

The detector removal appears safe, and retaining the remaining `CrossReferenceAbsence*` type/schema/writer pieces is the correct conservative interpretation of the spec.

Safety of removal:
- You verified `CrossReferenceDetector` has **zero production callers** across `AccessHarvester.Pipeline`, `AccessHarvester.Core`, and `AccessHarvester.Cli`.
- You confirmed `CleanupAnalyser` uses only `NamePatternDetector` and `DateSignal`.
- You removed only:
  - `AccessHarvester.Pipeline/Cleanup/CrossReferenceDetector.cs`
  - `tests/AccessHarvester.Pipeline.Tests/Cleanup/CrossReferenceDetectorTests.cs`
- Build stayed green.
- Cleanup-focused tests and full pipeline tests passed.
- The negative assertion test `CleanupAnalyserTests.Run_Does_Not_Emit_CrossReferenceOnly_Findings` was retained, which is important because it pins the narrowed behavior after the dead detector is gone.

Retaining `CrossReferenceAbsenceSignal`, the checklist-writer switch arm, and the schema enum entry is also the right call under the spec’s explicit guard: remove them **only if nothing else references them**. Per your own verification, those elements still reference each other. Removing them would therefore be a larger contract/schema cleanup, not a straightforward dead-code deletion. Keeping them avoids an unnecessary consumer-facing schema change in a session that was explicitly not meant to alter cleanup logic or public contracts.

So on both halves of this concern—removal and retention—the implementation matches the safest reading of the spec.

4. **Doc accuracy**

The appended resolution section appears accurate and well-scoped, and it satisfies the “annotate, don’t delete” requirement.

What is accurate:
- The overview paragraph distinguishes between:
  - usage-side fixes shipped in set `001`
  - cleanup-side narrowing shipped in set `001` baseline commit `6da3b29`
  - stale cleanup goldens refreshed, and dead detector removed, in this `002` set
- The table entries are appropriately tied to shipping sets and evidence pointers rather than vague claims.
- The evidence pointer for item 1 is especially strong because it cites both code behavior and test coverage:
  - `CleanupAnalyser` only emits `NamePattern` + `DateSignal`
  - `CleanupAnalyserTests.Run_Does_Not_Emit_CrossReferenceOnly_Findings`
  - refreshed goldens now have `findings: []`
- The switchboard, parameter-dialog, out-of-band-entry, and PascalCase checklist entries all point to the right class of evidence for those prior fixes.

What is preserved correctly:
- You state the original feedback text below is retained verbatim.
- The new section is additive and dated.
- The deferred COM/zero-date observation is clearly marked as observed in `002` but intentionally not actioned, which is exactly the right documentation posture for a stop-rule deferral.

Nothing in the supplied text suggests the original feedback was overwritten or that the resolution summary misattributes the shipping set. This concern is satisfied.

5. **Scope discipline**

Yes: the session appears to have stayed within scope and avoided cleanup/usage classifier-logic changes.

Evidence for that conclusion:
- You explicitly state `CleanupAnalyser`, `NamePatternDetector`, and usage classifier logic were unchanged by design.
- The only behavioral artifact changes are regenerated goldens, which are expected when stale committed outputs are refreshed against the already-shipped narrowed classifier.
- Removing `CrossReferenceDetector` does not change runtime behavior because it had no production caller.
- The retained negative-assertion test further demonstrates that the intended narrowed behavior remains the same.
- The COM anomaly was documented and deferred rather than “fixed,” which is exactly what scope discipline required.
- Documentation was updated, but the historical design note in `object-cleanup-strategy.md` was intentionally left untouched because it was outside the session’s touch list.

In short, the session changed artifacts, dead code, and documentation—but not classifier logic. That is consistent with the stated intent and the stop rule.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```