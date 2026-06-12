```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "F-3-2",
      "summary": "The symbol-db blast-radius sub-claim is slightly overstated as proven: the excerpt shows `SymbolDbBuilder.Build` does not catch `LoadAndParse` exceptions, but it does not by itself verify tool-level abort behavior beyond that method.",
      "fix": "Narrow the wording to `SymbolDbBuilder.Build` failure/exception escape, or relabel that sub-claim as `partially proven` unless a higher-level call-path citation is added."
    },
    {
      "severity": "Minor",
      "location": "F-3-5",
      "summary": "The omission of explicit `ByVal` is proven, but the stronger claim that consumers 'must read every ByVal parameter as ByRef' depends on an omission-semantics contract not cited in this finding, so the status is a bit stronger than the supplied evidence.",
      "fix": "Either cite the schema/output-convention source that defines omitted pass mode as default `ByRef`, or soften the claim/status to say the manifest cannot distinguish explicit `ByVal` from omitted/default pass mode."
    },
    {
      "severity": "Minor",
      "location": "F-3-10",
      "summary": "The BodyHasher continuation-line example is not fully supported as written: the cited regex `^\\s*\\d+\\s+` would not strip a line containing only `2`, so that sub-observation's concrete example does not demonstrate the claimed hazard.",
      "fix": "Revise the example to a continuation line that actually matches the regex (for example one beginning with a numeric literal followed by whitespace and more tokens), or downgrade that bullet to `inferred — not verified`."
    }
  ]
}
```