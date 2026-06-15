The runtime behavior looks good, and on the substantive question: **documenting + accepting + deferring the exact-epoch false-negative is an adequate resolution** here. I do **not** see this as a must-fix-now code defect, because the only clean fix is sampler provenance, and removing the epoch guard would reintroduce the much worse known misfire.

The helper extraction also looks behavior-equivalent to the prior inline CLI logic:

- COM note is prepended only when `DateSignalSource != "unavailable"`.
- Sampler degradations are always surfaced.
- Order is `com`, then `sampler`, then existing notes.
- The original instance is returned unchanged when nothing applies.

And the 5 tests do cover those intended properties.

However, I don’t think this is a clean closeout yet, because there is still one contradictory documentation statement in `CleanupAnalyser.cs`: the new XML summary for `OleEpochBaseDate` says the guard treats maxima that fall **“on or before”** the base as no signal. That is not what the code does (`==` only), and it directly conflicts with the R1 fix and the R2 acceptance rationale. Since this round’s resolution is explicitly “documentation + acceptance,” that stray wording matters.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "AccessHarvester.Pipeline/Cleanup/CleanupAnalyser.cs",
      "summary": "The new XML summary for `OleEpochBaseDate` incorrectly says the zero-date guard suppresses dates 'on or before' 1899-12-30, which contradicts the actual `==` guard and the documented acceptance of pre-epoch real dates.",
      "fix": "Rewrite that summary to say the guard suppresses only maxima whose date component is exactly 1899-12-30, and if desired mention separately that true pre-epoch maxima must still flag while exact-epoch and masked-pre-epoch cases remain accepted false-negatives deferred to DW-16."
    }
  ]
}
```