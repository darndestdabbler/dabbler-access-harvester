# Set 011 — Session 3 (closeout) devil's-advocate verification prompt

> **How to run this (operator):** Paste this whole file into GitHub Copilot
> Chat **twice** — once asking **Gemini Pro** and once asking **GPT-5.4** — in
> the `dabbler-access-harvester` workspace (the reviewers are path-aware and can
> open any file referenced below). Save Gemini's raw answer to
> `docs/session-sets/011-resolver-and-navigation-wiring/s3-verification-gemini.md`
> and GPT's raw answer to
> `…/s3-verification-gpt-5-4.md` (do not edit the saved outputs). Then hand both
> back to the orchestrator for synthesis.

---

> **Bias cautions:** This prompt was authored by an AI agent (Claude Opus 4.8,
> the session orchestrator) that may have an opinion on the answer. Its framing
> may inadvertently constrain you to in-scope refinements when the right answer
> is to question the scope. The work being reviewed may be presented as further
> along than it should be. Before answering as posed, briefly check whether this
> is the right question. If a different question would be more useful, answer
> that one too.

**Your role is a devil's-advocate verifier.** Your null hypothesis is that this
closeout is *wrong* — that it overclaims, misdescribes shipped output, defers
dishonestly, or closes the set prematurely. For each numbered concern below I
have written a **specific contrarian hypothesis**; your job is to genuinely try
to make that hypothesis true against the actual repository state, and only
concede it false when the evidence forces you to. Do not produce a generic
"looks good" review, and do not produce theatrically negative findings either —
find the *real* defects if they exist.

**Response format requirement:** Your response MUST begin with a detailed prose
analysis addressing each concern C1–C6 by number, BEFORE the JSON block. Both
the prose analysis and the trailing JSON block are required; do not skip the
prose.

---

## What this session set is

Set `011-resolver-and-navigation-wiring` is "006's P3": it wires capabilities
the harvester had **built and hermetically tested but never wired into
production output**. It has three sessions:

- **Session 1 (F-3-4, VERIFIED):** shared one `ProjectScope` across the manifest
  parse loop; built `AccessObjectModelRegistry` + passed `owningClass` for
  Form/Report modules; consulted `DefaultMemberRegistry`; **omitted** the
  `SqlStrings` key (schema-valid; `DynamicSqlAnalyzer` wiring deferred → DW-8).
  Golden effect: resolved references `3182 → 4103` (+921), every transition
  mapped to F-3-4.
- **Session 2 (F-2-2, VERIFIED):** wired `NavigationGraphBuilder` →
  `manifest.NavigationGraph` and `UnresolvedItemAggregator` →
  `unresolved-items.json` off the existing `resolvedModules`+`crossRefs` seam;
  wired `EnrichmentPipelineRunner` (event-handler / validation / row-source
  classifications) + table-field validation; removed the 010 interim
  "unresolved-items always empty" note. **Re-grounding DISPROVED the spec's
  R2-1 premise** — `tblSwitchboardItems` ships `(no data file)` and no harvested
  artifact carries switchboard rows, so R2-1 (switchboard tree) and R2-2
  (dialog→query→report chains) were **deferred to a P4 set** via **DW-9** and
  **DW-10**, by cross-provider consensus (gpt-5-4 + gemini-pro both Option A) +
  human sign-off. Also fixed a pre-existing S1 bug: `ResolvedTarget` emitting
  schema-invalid `"Kind": null` for 325 ObjectModel targets → omit-null.
- **Session 3 (this session, closeout):** the subject of this review.

**Current shipped golden** (`samples/GoldenOutput/HomeHealthCare/`), confirmed
this session to reproduce **byte-identically from a fresh COM extraction**
(`CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests`, 6/6
passed):

```
manifest.NavigationGraph: { EntryPoints: [] (0), Edges: 22, UnresolvedTargets: 22, Macros: [] (0) }
                          (Ribbon, Switchboard keys ABSENT — omitted when null)
  Edges actions: 13 OpenForm + 8 Close + 1 OpenReport; target kinds: 21 Form + 1 Report
  UnresolvedTargets reasons: 22 non-literal-target
manifest.ValidationRules: 76   RowSourceSpecs: 19
manifest.FunctionClassifications: 233   EventHandlerClassifications: 81
manifest.UnresolvedItems: 1414   (unresolved-items.json byte-identical to it)
manifest.SchemaVersion: "1.1"
```

## What Session 3 actually changed (this is what you are verifying)

**No C# source and no golden output changed this session** — it is
documentation + session artifacts + the next-set authoring + a validation pass.
Files changed:

1. `docs/harvester/interpretation-guide.md` —
   - new **§5.7 "The navigation graph (`manifest.NavigationGraph`)"** (the main
     deliverable: edge/unresolved-target shapes, join keys, the
     omit-null-vs-empty-array split, the R2-1/R2-2 deferral, determinism);
   - added `NavigationGraph` to the **§4 envelope key list** + the empty-run
     example;
   - **fixed stale `SchemaVersion "1.0"` → `"1.1"`** in the §4 narrative + the
     empty-run example;
   - added a navigation reading-order step in §3.2 and a §8 known-limitation.
2. `docs/planning/deferred-work.md` — pointed DW-9 / DW-10 owning-set candidate
   at the concrete new slug `012-switchboard-and-report-chain-edges`; corrected
   DW-9's mechanical-backstop wording (Switchboard/Ribbon **omitted**, not empty).
3. `docs/session-sets/012-switchboard-and-report-chain-edges/` — **NEW** next-set
   `spec.md` + not-started v4 `session-state.json` stub.
4. Set 011 session artifacts (`ai-assignment.md`, `activity-log.json`,
   `session-state.json`, `session-events.jsonl`) + `tools/vahs011_s3_log.py`.

Authoritative source for the NavigationGraph shape:
`AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs`. Schema version:
`AccessHarvester.Pipeline/Manifest/DatabaseManifest.cs`
(`SchemaVersionLiteral = "1.1"`) and `AccessHarvester.Cli/Program.cs`
(`SupportedSchemaVersions = ["1.0","1.1"]`).

---

## Concerns to review (each carries a specific contrarian hypothesis to steelman)

**C1 — Does §5.7 accurately describe shipped output?**
*Contrarian hypothesis to make true:* "A consumer coding against §5.7 would get
something wrong." Check the edge and unresolved-target record shapes against
`NavigationGraph.cs` and the actual golden; check the claim that the four list
facets (`EntryPoints`, `Edges`, `UnresolvedTargets`, `Macros`) are always
present while `Ribbon`/`Switchboard` are **absent when null** (verify against
the golden's actual key set); check the join-key claims
(`SourceModule`+`SourceProcedure` ↔ `FunctionClassifications` /
`EventHandlerClassifications`; `TargetName` ↔ `Forms`/`Reports` stubs); check
the HHC counts (22 edges = 13 OpenForm/8 Close/1 OpenReport; 22 unresolved).

**C2 — Is the `SchemaVersion 1.0 → 1.1` correction right and complete?**
*Contrarian hypothesis:* "The 1.1 change is wrong or partial." Confirm the
envelope literal is actually `1.1`; confirm both `1.0` and `1.1` are accepted by
`validate`; grep the guide for any **remaining** stale `1.0` envelope claim the
fix missed (note: per-record sub-document version strings such as
`ParserOutput.SchemaVersion` are legitimately a *different* version — do not
flag those).

**C3 — Is the R2-1/R2-2 deferral represented honestly, or does the doc let a
consumer believe the switchboard tree / report chains are delivered?**
*Contrarian hypothesis:* "Somewhere the documentation implies R2-1/R2-2 are
done." Read §5.7's "What is NOT populated today" block, the §8 limitation, and
DW-9/DW-10. A consumer must come away knowing the `Switchboard`/`Ribbon`/
`EntryPoints`/`Macros` facets and the chain edges are **not** available yet.

**C4 — Is the next-set spec (`012-switchboard-and-report-chain-edges/spec.md`)
faithful and scope-disciplined?**
*Contrarian hypothesis:* "The 012 spec misframes the P4 work" — it contradicts
the 006 R2-1/R2-2 design sketches
(`docs/session-sets/006-solution-review-and-upstream-feedback-discovery/review-recommendations.md`
§2), mis-carries the Hard Constraints block, sets an unsound 3-session split, or
quietly expands scope (especially: is the new switchboard row-read actually
constrained to *narrow, shape-gated* per the set-specific constraint, not a
general row-data export surface?).

**C5 — Is the "committed golden equals a fresh run" grounding actually
established?**
*Contrarian hypothesis:* "What was run does not prove the claim." The session
ran `CanonicalGoldenRegressionTests` (full pipeline COM extraction compared to
the committed golden) + `ManifestSchemaConformanceTests`, 6/6 green, with no C#
source changed since S2's regen. Argue why that would still leave the golden
possibly stale or the documentation possibly describing non-reproducible output
— or concede it is sound.

**C6 — Is closing set 011 now premature?**
*Contrarian hypothesis:* "An End-Of-Set Deliverable is unmet, or an S1/S2
deliverable that S3's docs describe is actually absent or wrong in the shipped
golden." Cross-check the set's End-Of-Set Deliverables (`spec.md`) against the
golden: F-3-4 resolution firing in production (the +921 resolved refs, `Kind`
omitted not null), F-2-2 surfaces present (`NavigationGraph` populated,
`unresolved-items.json` populated, classification arrays non-empty). The S1/S2
work is **prior-session** evidence the verifier cannot assume — verify it is
actually in the current golden, not merely claimed.

---

After your prose analysis of C1–C6, output a JSON block in **exactly** this
format:

```json
{
  "verdict": "VERIFIED",
  "issues": [
    {
      "severity": "Critical",
      "location": "<file or concern id>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }
  ]
}
```

If verdict is `VERIFIED`, `issues` must be the empty array `[]`. Do not omit the
JSON block. `verdict` is `"VERIFIED"` only if you found no Critical or Major
issue; otherwise `"ISSUES_FOUND"`.
