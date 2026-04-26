# Verification Round 1

ISSUES_FOUND

- **SEVERITY:** Major  
  **ITEM:** non-retrospective-rows  
  **ISSUE:** Findings `#1`, `#2`, `#6`, and `#20` are grounded only in the VBA parser plan / `docs/test-cases/`, not in `accessexportertemp1-import` artifacts. That makes them decomposition observations, not retrospective findings from the completed import set.  
  **SUGGESTION:** Re-ground each row with a specific import-set artifact showing the lesson being transferred, or cut/move the row into Session 2 working notes instead of this retrospective.

- **SEVERITY:** Major  
  **ITEM:** wrong-session-target-for-verifier-guidance  
  **ISSUE:** Finding `#9` targets Session 3 stubs, but “list related files for the verifier” is runtime verification-prompt guidance, not stub-spec content required by the standard child-spec format. This puts the change in the wrong session and over-specifies the stub deliverable.  
  **SUGGESTION:** Move `#9` to Session 4 playbook guidance for future orchestrators/verifier prompts, or remove it from adopt-now if the stubs will not encode verifier-context lists.

- **SEVERITY:** Major  
  **ITEM:** import-specific-finding-misbucketed  
  **ISSUE:** Finding `#12` appears specific to code-import sessions from the harvester import set, while the generator spec’s Session 4 playbook scope is centered on parser work types like grammar authoring, AST design, symbol resolution, schema validation, and corpus authoring. Without parser-plan evidence that future child sets will import external code, this is likely a `no-op`, not `adopt-now`.  
  **SUGGESTION:** Rebucket `#12` to `no-op`, or add a concrete parser-plan evidence pointer showing an actual future code-import activity before keeping it as Session 4 guidance.

- **SEVERITY:** Major  
  **ITEM:** weak-or-mismatched-evidence-pointers  
  **ISSUE:** Several rows are not safely verifiable from the cited evidence: `#3` infers verification-retry cost dominance from session total cost, `#5` cites a review slug that does not obviously support “placeholder counts/dependencies,” `#14` cites a `summarization` failure while claiming `documentation` misuse, and `#18` names an unspecified `activity-log.json` rather than a concrete artifact path/section.  
  **SUGGESTION:** Rewrite affected rows with exact artifact paths plus section/round references, and ensure each citation directly supports the stated claim; for `#14`, also include the non-optional `max_tier=1` fallback from the promoted convention if that row stays.

- **SEVERITY:** Major  
  **ITEM:** missing-high-signal-routing-finding  
  **ISSUE:** The retrospective omits a directly relevant promoted convention from `project-guidance.md`: when the orchestrator already matches the routed target model, do the work in-conversation rather than paying for a same-model `route()` call, except for mandatory cross-provider `session-verification`. That is high-signal calibration data for Session 4’s routing playbook.  
  **SUGGESTION:** Add an adopt-now Session 4 finding that defaults same-model work to in-conversation execution and explicitly preserves `session-verification` as the exception.

---

# Verification Round 2

ISSUES_FOUND

- **SEVERITY:** Major  
  **ITEM:** residual-non-retrospective-row-17  
  **ISSUE:** In `harvester-import-retrospective.md` row `#17`, the finding is still grounded in parser-side materials (`docs/planning/project-guidance.md` Principles → Output Architecture and `docs/test-cases/harvester-schema.v0.json`), not in `accessexportertemp1-import` artifacts or an import-derived promoted convention. That leaves a residual round-1 problem: this is an integration-planning observation, not a retrospective lesson from the completed import set.  
  **SUGGESTION:** Remove row `#17` from the retrospective, or re-ground it with a specific import-set artifact showing why schema reconciliation should be deferred; otherwise move it to Session 2 decomposition notes.

- **SEVERITY:** Major  
  **ITEM:** row-1-evidence-still-overstates-citation  
  **ISSUE:** In `harvester-import-retrospective.md` row `#1`, the evidence still does not cleanly support the claim. The row says the four mechanical sessions “verified on round 1,” but the evidence pointer itself says Sessions `2/3/4/5` took `1-2 rounds`; and the broader statement that verification cost is “dominated by retry rounds” is still inferred from total session cost plus review-round counts rather than a directly cited per-round verification-cost artifact.  
  **SUGGESTION:** Rewrite row `#1` to a narrower claim that exactly matches the cited actuals, e.g. that multi-round synthesis sessions were materially more expensive overall than low-complexity one-round sessions, and make the round-count wording match the source verbatim.

- **SEVERITY:** Medium  
  **ITEM:** row-2-set-wide-volume-claim-not-shown  
  **ISSUE:** In `harvester-import-retrospective.md` row `#2`, the claim that internal inconsistencies were “the highest-volume verifier finding category in the import set” overreaches the citation. The evidence pointer names `session-reviews/session-001.md` rounds `1` and `2` plus `issue-logs/session-001.json`, which supports repetition within the Session 1 plan review, not a set-wide ranking across the whole import session set.  
  **SUGGESTION:** Narrow row `#2` to the Session 1 plan-author session, or add set-wide evidence from the other import-session reviews if the intent is to keep the stronger claim.

---

# Verification Round 3

ISSUES_FOUND

- **SEVERITY:** Medium  
  **ITEM:** row-1-cost-gap-still-partly-inferential  
  **ISSUE:** Row `#1` now correctly lists the per-session actuals, but the phrase “the gap concentrated in multi-round verification” still goes beyond what the cited evidence directly shows. The pointer gives total session cost plus round counts, not a breakdown isolating verification-round spend; and one of the cited 2-round sessions (Session 6 at `$0.23`) sits close to a 1-round mechanical session (Session 4 at `$0.21`), so the “concentrated in multi-round verification” framing remains interpretive rather than strictly evidenced.  
  **SUGGESTION:** Narrow the claim to a purely descriptive statement tied to the cited actuals, e.g. that the 3-round plan-author session was the clear cost outlier while the mostly mechanical sessions clustered at the low end; only keep the verification-concentration phrasing if you add direct per-round cost evidence.

- **SEVERITY:** Medium  
  **ITEM:** row-2-new-context-clause-needs-citation-tightening  
  **ISSUE:** Row `#2` successfully drops the unsupported set-wide ranking, but the rewrite still contains two citation-tightness problems: “dominant verifier finding category” is still a ranking claim unless the cited review visibly supports that count, and “The plan-author session is the closest analogue to Session 2 (decomposition) of *this* generator set” depends on this generator spec, which is not included in the row’s evidence pointer.  
  **SUGGESTION:** Soften “dominant verifier finding category” to “recurring verifier theme” unless you add an explicit count-based citation, and either add the generator spec as supporting evidence for the analogue statement or move that statement into the rationale instead of the finding text.
