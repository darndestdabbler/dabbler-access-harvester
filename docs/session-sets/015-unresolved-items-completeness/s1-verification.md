A. **Dedup-key parity:** For the *same body reference*, the new feed appears to mirror the old key closely enough that I do **not** see a concrete double-count bug from parity mismatch alone.

- **module**: both paths use `module.ModuleScope.ModuleName`; that is the same source object.
- **procedure**: `FunctionClassificationBuilder` uses `procedureAst.Name`; the new feed uses `ps.Procedure.Name` from the enclosing `ProcedureScope`. Given the resolver scope is built from that AST declaration, I see no shown transformation that would change the text. I do note the key omits `ProcedureKind`, but because `Line/Column` are module-global, that omission does not by itself create a same-ref parity problem.
- **line/column**: both paths use the referenced expression's `Provenance.Line/Column`. For a shared body ref, that is the same node / same coordinates.
- **reason**: `UnresolvedReasonOf` matches the body walk's `DispatchOutcome` flattening for `ResolvedViaWithFlattening`.

So on concern **A**, I do **not** have a real counterexample where a body ref present in both feeds would double-count because one of the 5 key components diverges.

B. **Over-suppression / false-negative:** I could **not** construct a realistic **header-vs-body** collision when provenance is truthful: a parameter-default expression and a body expression live at different source positions, so `(line, column)` should differ.

However, I *can* break the dedup another way, and it is important: the implementation is not just “dedup against body-only keys”. It also dedups **within the procedure-scoped feed itself** because it uses:

```csharp
if (bodyKeys.Add(DedupKey(item)))
    items.Add(item);
```

That means any later procedure-scoped item with the same key is silently dropped even if there was **no** body-only counterpart.

That is dangerous because `(module, procedure, line, column, reason)` is not proven unique per reference node. A plausible counterexample is a chained unresolved member access in a header, e.g. a default expression like:

```vb
Public Sub Demo(Optional ByVal x As Variant = Foo.Bar.Baz)
End Sub
```

If `MemberAccessExpression.Provenance` starts at the chain start (typical AST behavior), then:
- `Foo` => `not-in-scope` at `(L,C)`
- `Foo.Bar` => `member-access-on-unresolved-target` at `(L,C)`
- `Foo.Bar.Baz` => `member-access-on-unresolved-target` at `(L,C)`

`Foo.Bar` and `Foo.Bar.Baz` are legitimately distinct unresolved references, but they share the dedup key and one will be dropped by the current `Add`-based loop. That recreates an **undercount**, now inside Layer A.

Even if this exact parser happens to anchor member-access provenance at the member token instead of the chain start, the code is still relying on an unstated uniqueness invariant and is deduping more than the design claims. The design said “deduplicate against the body-only set”; the code deduplicates against the growing union.

So concern **B** is where I land: I cannot break the specific header/body-collision story, but I **can** break the implementation’s broader dedup behavior.

C. **Net-new body items / golden risk:** The HHC golden pass is strong evidence for the sampled corpus, but it is **not a proof** for all VBA constructs. A net-new body item would require `resolvedModule.References` to contain a body-scoped unresolved reference that the classification visitor does not visit/classify. From the material shown, the visitor covers the reference-bearing runtime-expression shapes I would expect (`Identifier`, `MemberAccess`, `BangAccess`, implicit-with variants, `Me`), so I do **not** have a concrete body construct that would survive dedup as a new body item. Still, HHC's “zero net-new” only proves absence on that corpus, not impossibility in general.

That said, the stronger risk I do see is not “new body items”, but “lost procedure-scoped items” from the intra-feed dedup above.

D. **Source tagging:** Using `UnresolvedSource.FunctionClassification` for header refs is defensible. These are still procedure-scoped VBA unresolved references, and reusing the existing source preserves old body items byte-for-byte. I do **not** think a new source value is warranted in this session unless the schema wants to distinguish provenance-by-pass, which the current shape does not appear to do.

E. **Superset property:** Intended, yes; guaranteed, not fully. Reusing `ParserOutputProjector.GroupReferencesByProcedure` is the right structural move, and absent dedup pathologies it makes the unresolved index a superset of inline procedure-scoped unresolved refs. But the current `HashSet.Add` loop weakens that guarantee, because if two inline unresolved refs collapse to one dedup key, the index is no longer a true superset by cardinality. So the “by construction” argument only holds if the dedup key is unique per procedure-scoped reference, and that uniqueness is not established here.

F. **Determinism:** Final sort is deterministic for the surviving set. But the same intra-feed dedup issue creates an order-sensitivity edge case: if two distinct procedure-scoped refs share the same dedup key but differ in `SurroundingText` / context, whichever one is enumerated first survives. For production inputs that enumeration may still be stable, so byte output may be stable in practice; but the stronger claim of logical input-order-independence is weakened by this collision behavior.

G. **Scope discipline:** Good. `EnumerateProcedureScopedReferences` uses `FindEnclosingProcedureScope` and excludes refs with no enclosing `ProcedureScope`, so module-level refs remain out of scope for Session 1. I see no accidental Layer B pull-in.

H. **Lean / no-silent-drop:** The shape stays lean and there is no obvious default injection. But the current dedup is a potential **silent drop** beyond the intended body-overlap suppression, which conflicts with the repo’s explicit-unresolved-over-silent-drop principle.

Overall: the main design is sound, parity for true body overlap looks correct, and module-level scope is respected. But the actual `Build` dedup implementation is too aggressive: it suppresses same-key items within the new procedure-scoped feed, not just against the old body-only feed. That is enough for me to return **ISSUES_FOUND**.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs",
      "summary": "The `procedureScopedReferences` dedup loop uses `bodyKeys.Add(...)`, which deduplicates within the new procedure-scoped feed as well as against the body-only feed and can silently drop distinct unresolved header/procedure-scoped references that share `(module, procedure, line, column, reason)`.",
      "fix": "Change the loop to suppress only keys already present from `FunctionClassification` (e.g. `if (!bodyKeys.Contains(DedupKey(item))) items.Add(item);`) rather than mutating the key set as you iterate, or strengthen the dedup key with a per-reference discriminator such as end position/member name/node identity if intra-feed dedup is truly required."
    }
  ]
}
```