{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Vba.Parser/VbaModuleBodyLoader.cs",
      "summary": "The `CanonicalHeader` regex allows `VERSION ...` followed directly by `Attribute VB_*` lines to be stripped even when the required `BEGIN ... END` block is missing, which violates the spec's malformed-header rule.",
      "fix": "Rewrite header recognition as an alternation between `(VERSION + BEGIN...END + Attribute+)` and `(Attribute+)` shapes, or add an explicit post-match guard that rejects any match where `VERSION` is present without a complete `BEGIN...END`, and add a unit test covering `VERSION` + `Attribute` without `BEGIN`."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/vba-attribute-header-strip/audit.md",
      "summary": "The audit ledger is internally inconsistent because it says `ParseSuccessReportGenerator` was routed through `LoadAndParse(...).Result`, while the actual implementation uses `VbaModuleBodyLoader.Load(...)` followed by `VbaParserPipeline.Parse(...)`.",
      "fix": "Update the audit table and notes so P2 is described as a two-step loader-routed call (`Load` + `Parse`), matching the implemented workaround and preserving an accurate bypass audit."
    }
  ]
}