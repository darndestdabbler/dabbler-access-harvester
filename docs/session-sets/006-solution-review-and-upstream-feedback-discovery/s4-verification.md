1. **Evidence discipline**

Mostly solid, but not perfect. The document’s main conclusions are grounded either in the embedded session evidence or in explicitly referenced S1–S3 findings, which you told me to treat as verified inputs. The fresh-run table in §1 maps cleanly to evidence block A. The COM premise probes for F-1-2 and F-1-6 map cleanly to block E, with the code-impact portions appropriately leaning on the cited S1 findings. The explicit inference label is also used correctly for the claim about real-world prevalence of the `[Forms]![frmX]![ctl]` query-criteria variant.

The two places where the prose drifts beyond what is actually embedded are both minor but real:

- In §1, “Fresh output also re-confirmed, **byte-for-byte against the committed-golden evidence** ...” is stronger than the supplied console evidence. The embedded evidence shows corroborating fields/bytes/symptoms, not an explicit byte-for-byte diff against committed goldens.
- In §2 R2-2, several details are presented as proven fresh-run facts even though the excerpt only shows partial examples: notably “**8 of 12 reports**” and the concrete “**TempVars!repCID / repFY that subreport record sources read**” chain. The embedded console output shows one report Open-event example, report RecordSource examples, declared-parameter firing for one query, and zero `[Forms]!` query references; it does not show the full TempVars linkage or the 8/12 count.

So the evidence discipline is good at the verdict level, but there are a couple of unsupported specificity upgrades that should be softened or explicitly evidenced.

2. **R2 verdicts**

The verdicts themselves are broadly sound.

- **R2-1 Confirmed** follows from the evidence. The usage classifier recognizes `tblSwitchboardItems` as switchboard/navigation-related; the IR for that table is schema-only with `RecordCount: 52`; the manifest lacks `NavigationGraph`; unresolved items are empty. That is enough to support the conclusion that the current output exposes the signal but not structured switchboard edges.
- **R2-3 Assessed** also follows. Evidence A proves the two-root split and the slug-casing drift. The recommendation to keep ownership downstream is a governance call, not an empirical overreach.
- **R2-2 Confirmed** is directionally justified, because the evidence does show the component surfaces are separately observable today while no edge surface exists. However, the supporting prose slightly overclaims what this specific evidence excerpt reaches. The strongest supported statement is “the ingredients are visible in raw/IR form but not connected”; the weaker point is the document sometimes speaks as if the full param-dialog → query → report chain was concretely demonstrated end-to-end in this session’s console excerpt. That is a phrasing issue, not a verdict-killer.

So: verdicts okay; some R2-2 evidentiary narration is a bit too strong.

3. **Design-sketch contract fit**

Yes. The sketches are consistent with the stated output conventions.

For **R2-1**, the sketch:
- keeps provenance explicit and shaped as `{ source, ... }`,
- avoids silent omission by proposing unresolved records for unknown command codes or missing targets,
- preserves determinism through ordered `SELECT` and builder ordering,
- respects the repo’s omission semantics by proposing a narrow, gated row-read only for switchboard-shaped tables rather than a general table-data export.

For **R2-2**, the sketch also fits:
- closed edge-kind sets are compatible with determinism and consumer readability,
- provenance is explicit per edge,
- dynamic/unmatchable cases become explicit unresolved records rather than disappearing,
- sequencing after the wiring/parser arc is consistent with “use existing modeled semantics, don’t invent ad hoc heuristics first.”

I do not see a contract-shape violation in either sketch.

4. **Prioritization coherence**

The prioritization is internally coherent.

- **P0** first is justified and consistent with the stated need to regenerate and validate goldens.
- **P1a** before **P3** is consistent with the dependency that wiring parsed/resolved VBA into output should not happen before the ingestion/parser seam is fixed.
- **P2** before **P4** is coherent because typed-query correctness matters for report-chain derivation.
- **P3** before **P4** is consistent with both sketches and the explicit dependency language.
- **P5** and **P6** being slottable around the main arc is consistent with their limited prerequisites.
- The “minimal path to P4” matches the dependency story already stated in §2.

The sizes are plausible for document-level planning purposes. I do not see a dependency inversion or scheduling contradiction.

5. **Status-table fidelity**

Yes. The three Round 2 rows accurately summarize the recommendations document.

- **R2-1** row matches the doc’s “Confirmed,” points to the same proposed P4 set, and preserves the dependency on P3.
- **R2-2** row matches the doc’s “Confirmed,” points to P4, and preserves the sequencing after the F-3-1/F-3-4 wiring arc and F-2-1 decode fix.
- **R2-3** row matches the doc’s “Assessed,” downstream-ownership recommendation, and the P2 ride-along for casing/name-form friction.

The only caveat is that the R2-2 row repeats the same slight evidence overstatement already present in `review-recommendations.md`; that is not drift between documents, just shared phrasing.

6. **Stop-rule compliance**

Compliant. Nothing in either deliverable contradicts the read-only rule.

The attestation in `review-recommendations.md` matches evidence block F:
- only the planning doc and session-set docs changed,
- the fresh run went under `tmp/`,
- the COM-touch `samples/*.accdb` diff was swept before staging.

The deliverables are recommendation/planning artifacts only. No hidden contradiction here.

7. **F-1-2 escalation handling**

Sound and honestly worded.

Given the stated Session 1 escalation condition — escalate to Major if Session 4 confirms that Access accepts `/` or `\` in object names — block E satisfies that trigger. The same probe also narrows the impact by disproving the `..` traversal-above-root branch. The doc reflects that correctly: it escalates severity because the named condition was met, but it narrows the impact statement to export failure within the output tree due to unsanitized path combination, not arbitrary above-root file placement.

That is exactly the right way to handle it: preserve the predeclared escalation rule, update the impact statement with new evidence, and avoid pretending the worse branch survived.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "docs/session-sets/006-solution-review-and-upstream-feedback-discovery/review-recommendations.md; docs/planning/harvester-upstream-feedback.md",
      "summary": "The R2-2 writeup presents some specifics as proven fresh-run facts (notably '8 of 12 reports' and concrete TempVars-fed subreport linkage) that are not established by the embedded console evidence.",
      "fix": "Rephrase those details to match the evidence actually shown, add the missing probe output/citations, or label the unsupported specifics as inferred/not verified."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/006-solution-review-and-upstream-feedback-discovery/review-recommendations.md",
      "summary": "Section 1 uses stronger-than-shown wording ('byte-for-byte against the committed-golden evidence' and exhaustive absence language about row data/output surfaces) without an explicit embedded comparison/search proving those whole-tree claims.",
      "fix": "Soften the wording to the directly supported observations or include explicit diff/search evidence demonstrating the byte-for-byte comparison and exhaustive absence claims."
    }
  ]
}
```