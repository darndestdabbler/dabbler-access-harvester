1. EventHandler detection correctness: `TryExtractEventBinding` does implement the stated Session 1 heuristic correctly. It uses `LastIndexOf('_')`, so `My_Control_Click` becomes `("My_Control", "Click")`, which is exactly the “split on the LAST underscore” rule. The helper also correctly rejects no-underscore, leading-underscore, and trailing-underscore cases, and the included tests cover the important happy-path and boundary cases. The only caveat is semantic rather than mechanical: this heuristic can still label any underscore-named procedure as an event handler, even outside actual form/report event scenarios. Under the rule you stated for Session 1, though, the split logic itself is correct.

2. Reference-to-procedure scoping: the scope-chain walk is the right implementation strategy. `GroupReferencesByProcedure` does not assume references are attached directly to a `ProcedureScope`; it walks `EnclosingScope.Parent` until it finds one. That is exactly what you want for references nested inside `With` scopes, `For Each` scopes, and other local/nested resolver scopes. It also intentionally drops module-level references, which is consistent with your note that the v1.0 schema has no module-level item bucket. So for the concern you asked about, this looks correct.

3. ResolutionOutcome mapping: the specific `ResolvedViaWithFlattening` behavior is implemented correctly. It unwraps recursively via `InnerOutcome`, preserves the underlying status/kind/target, and lowers confidence to `0.9`. That said, there is a separate correctness problem in the same mapping area: `ResolvedToMe` sets `ReferenceKind` to `"Report"` for report modules, but hard-codes `ResolvedTarget.Kind` to `"Form"`. That means report-module `Me` references will serialize inconsistent target metadata. So the with-flattening part is good, but the overall outcome mapping is not fully correct.

4. ProcedureKind mapping: `ClassificationKindOf` and `SchemaKindOf` both correctly cover the five requested procedure families: `Sub`, `Function`, `PropertyGet`, `PropertyLet`, and `PropertySet`. The mapping is internally consistent, and `ProjectDeclare` separately handles `Declare Sub` / `Declare Function`, which are not part of those five ordinary procedure kinds. I do not see a problem in this mapping table itself.

5. SqlStrings always empty: yes, that is the right Session 1 behavior. Since SQL-string extraction was explicitly deferred, emitting `"SqlStrings": []` everywhere is the safest schema-conformant choice. It keeps the manifest shape stable for consumers and makes the omission explicit rather than ambiguous.

6. CodePagesEncodingProvider registration: yes, this is the standard .NET fix for code page 1252 availability in test/runtime contexts where legacy encodings are not preloaded. For the built-in `CodePagesEncodingProvider.Instance`, repeated registration is effectively harmless, and this is the normal remedy for xUnit/test-host failures around `Encoding.GetEncoding(1252)`. Best practice is still to do it once during startup/bootstrap, but the fix itself is correct.

7. Determinism: sorting `Directory.GetFiles(...).OrderBy(..., StringComparer.Ordinal)` is good and necessary, but by itself it is not a full determinism guarantee. It guarantees deterministic file discovery order. It does not independently guarantee deterministic ordering of all emitted nested arrays, because `ParserOutputProjector` emits `Items`, `Body.References`, and `Body.Calls` in upstream parser/resolver enumeration order without an explicit sort. If those upstream lists are stable, output will likely be stable in practice; but the code shown does not itself fully enforce that guarantee. So the answer here is: good step, but not sufficient as a complete guarantee.

8. LabelSource casing fix: if v1 ADR D4 already defines the canonical enum literals in PascalCase, then changing `attached` → `Attached` and similar values is exactly the right fix. That is an implementation/schema-conformance correction, not a semantic design change. It does not need a separate ADR unless you are proposing to change the canonical enum values themselves.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline.Manifest/ParserOutputProjector.cs",
      "summary": "ResolutionOutcome.ResolvedToMe hard-codes ResolvedTarget.Kind to \"Form\", so report-module Me references are serialized with inconsistent kind data.",
      "fix": "Map ResolvedTarget.Kind from OwningClass.Kind as well, or reuse BuildDeclarationTarget(r.OwningClass) so report modules emit \"Report\" instead of \"Form\"."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Pipeline.Manifest/ParserOutputProjector.cs; Pipeline.cs",
      "summary": "Deterministic file ordering alone does not guarantee deterministic manifest output because projected reference/call arrays are emitted in upstream enumeration order without an explicit stable sort.",
      "fix": "Apply stable ordinal sorting to emitted collections such as Body.References and Body.Calls, or formally guarantee deterministic ordering from the parser/resolver and document that contract."
    }
  ]
}
```