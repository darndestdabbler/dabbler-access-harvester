F-1-1: The core evidence is accurate. `SaveAsTextTranslator.BuildPayload` uses `File.ReadAllText(file)` and `File.ReadAllLines(file)` with no explicit encoding, while the included `Pipeline.cs` manifest path explicitly registers code pages and reads SaveAsText text with cp1252, and `Program.RunParse` explicitly does BOM-detect-then-cp1252. P1 and P2 are consistent with that code and do prove real corruption on cp1252 input. I think **Major** is defensible because this is silent data corruption in a primary output tier. The weak spot is not the existence of the bug, but the proof labeling: there is no explicit per-finding Proven/Inferred label, and the statement that the corruption necessarily propagates into `ir/` and creates `manifest.json` vs `ir/` divergence goes beyond the included proof. That propagation is plausible, but in this packet it is still inference.

F-1-2: The code evidence is accurate: `SaveAsTextExporter` does build the raw output path directly from `obj.Name` with no sanitization, unlike the included `QueryExporter` and `TableExporter`. So there is a real defect here. Where the finding gets weaker is the impact/severity argument. The strong claim—path traversal / writing outside the output root—depends on Access allowing separator-bearing object names. The finding itself admits that point is inferred and not verified this session. Without that premise, the proven issue is “filesystem-unsafe object names can break or misplace exports”; the out-of-root write scenario is not established by the supplied source or probes. Because the document presents this as a **Major** based on that unverified exploitability, I would treat this finding as overstated unless backed by a reproducer or authoritative Access naming-rule evidence.

F-1-3: This one is solid. `BuildPipeline` blindly passes `--types` values into `WithCategories`, and `WithCategories` is just a set filter. Unknown category names therefore collapse to an empty pipeline with no error. P3 demonstrates the exact bad outcome: exit 0, empty categories, no output. **Major** is defensible because an orchestrator can treat this as success. The only overreach is the sentence that the same defect affects `extract` and `run`; that may well be true in the full program, but it is not shown in the included excerpt.

F-1-4: Accurate. The comment above `BuildFormOrReportExports` promises a diagnostic when parser output exists but spec does not, but the implementation only iterates `ir/*.ir.json` files and only emits `FORM_SPEC_LOAD_FAILURE` when an `ir` file cannot be loaded. Raw-only/parser-only entries are silently dropped. **Minor** is appropriate. The example cause (“its enrichment failed”) is illustrative inference, but the mismatch between comment and implementation is real.

F-1-5: Accurate and reasonably scoped. Both OLE DB helpers interpolate `path` directly into `Data Source=...;`, so semicolons or quoting edge cases in the database path can break connection-string parsing. **Minor** fits because this is robustness against operator-supplied input, not a security boundary in the supplied code. The recommendation is directionally right; `OleDbConnectionStringBuilder` is the stronger fix.

F-1-6: The mechanics are accurately described. `ParseParametersClause` splits on bare commas, and `ParametersClauseRegex` stops at the first semicolon. If bracketed parameter names can contain commas or semicolons, this parser will misread them. The allowed-name premise is external-domain inference, and the finding does say that. **Minor** is defensible. Again, the main document-level problem is lack of an explicit Proven/Inferred label.

F-1-7: Mostly accurate. `TableExporter` definitely uses the culture-sensitive overload of `StartsWith("MSys")`, while `OleDbDaoSchemaReader` uses explicit `OrdinalIgnoreCase` / `Ordinal`. That is a real cross-surface drift and **Minor** is fine. `QueryExporter` is a weaker example because it only checks `"~"`, where case drift is irrelevant, but the table/system-name inconsistency is enough to support the finding.

F-1-8: This is the weakest finding as written. P5 proves that no test file directly references `SaveAsTextTranslator`, and that parser tests are separate. It does **not** prove that there is zero hermetic coverage, because a CLI-level or pipeline-level test could still exercise the translator without naming the class. So the concern is sensible, but the title and evidence overclaim. I would keep it only if narrowed to “no direct translator-focused tests found” or explicitly marked as inferred. **Minor** severity is fine if restated.

On probe consistency: P1/P2 support F-1-1, P3 supports F-1-3, P4 supports the “latent on ASCII fixtures” point, and P5 supports the narrower “no direct test references” claim for F-1-8. I do not see any contradiction between the findings and the probe transcripts.

On missed Critical/Major issues in the included source: I do **not** see an obvious missed Critical or Major in the supplied excerpts. I do see one smaller missed mismatch in `BuildFormOrReportExports`: the method comment says the return set has both spec and parser output, but the code will emit `FormExport` with `ParserOutput = null`. That is not Major.

On Observations and Positive confirmations: this section is mixed. The COM lifecycle positive confirmation is well supported by the included `AccessComSession` and `ComProcessReaper` excerpts. But several observations/positives rely on files or line ranges not supplied in this verification packet, so they are not independently verifiable here as “checked and found sound.” Examples include O-1-5, O-1-8, and several positive confirmations about DAO property probing, composite-FK handling, and report/form enum separation. Also, O-1-1’s “the file is always `[]`” is stronger than the included `Array.Empty<UnresolvedItem>()` assignment alone proves without the later write site. So I would not mark those sections fully verified as written.

The biggest document-level defect is that the session spec required each finding to carry a proven-vs-inferred label, and this findings document does not provide one explicitly. It uses inline prose like “Proven by probe” or “inferred from…”, which is useful, but it is not the same as a consistent per-finding label.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "F-1-1..F-1-8",
      "summary": "The findings do not carry the required explicit proven-vs-inferred label; they only embed ad hoc prose such as 'Proven by probe' or 'inferred'.",
      "fix": "Add a dedicated per-finding label such as Proven, Inferred, or Mixed, and separate reproduced facts from downstream impact inference."
    },
    {
      "severity": "Major",
      "location": "F-1-2",
      "summary": "The unsanitized output-path defect is real, but the Major path-traversal/outside-root-write impact is not established by the supplied evidence and depends on unverified Access naming-rule assumptions.",
      "fix": "Keep the filename-sanitization defect, but mark traversal as inferred or add a reproducer/authoritative naming-rule citation before retaining Major severity; otherwise downgrade."
    },
    {
      "severity": "Minor",
      "location": "F-1-8",
      "summary": "The evidence proves no direct test references to SaveAsTextTranslator, not zero hermetic coverage, so the finding overstates what the probe establishes.",
      "fix": "Retitle/narrow the finding to 'no direct translator-focused tests found' or provide evidence that no CLI- or pipeline-level hermetic tests exercise the translator."
    },
    {
      "severity": "Minor",
      "location": "Observations / Positive confirmations",
      "summary": "Several observation and positive-confirmation entries are presented as checked, but they are not verifiable from the supplied excerpts in this packet.",
      "fix": "Include the cited source excerpts or probe outputs for those entries, or narrow the verified section to the items actually evidenced here."
    }
  ]
}
```