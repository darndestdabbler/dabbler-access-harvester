Based on the post-fix code and the new pin, I think the confirmed Major is closed.

1. **`Contains` fully fixes the intra-feed drop.**  
   Yes. The key point is that `bodyKeys` is now a read-only snapshot of the **body-only** feed. Because it is no longer mutated during the `procedureScopedReferences` loop, a later procedure-scoped item can no longer be suppressed by an earlier procedure-scoped item. That means the original failure mode is gone even in the stronger case where two distinct procedure-scoped refs share the **same** dedup key. For example, even a repeated-name chain like `Foo.Bar.Bar` would now keep both deeper links, because neither is ever inserted into `bodyKeys`. The only remaining suppression path is collision with a preexisting body-only item, which is the intended seam.

2. **Adding `SurroundingText` does not appear to create a real double-count hole.**  
   On the facts you gave, both feeds derive the name from the **same AST node** using the **same `ExtractName`/`ExtractReferenceName` logic**. Under that invariant, the same physical body reference will produce the same:
   - module
   - procedure
   - line
   - column
   - reason
   - surrounding/member name

   So a true body overlap still yields an identical key and is still suppressed. I do not see a node-type-specific path, from the supplied design, where one feed would compute `"Bar"` and the other `"Baz"` or `null` for the **same reference**. If the extraction switch is literally shared and driven from the same node, then this part is sound.

3. **No new determinism/ordering issue is introduced.**  
   Correct. This change actually removes one order-sensitive behavior: suppression no longer depends on which procedure-scoped item is seen first. Membership against a prebuilt `HashSet` of body-only keys is deterministic for a given input, and `SortDeterministically(items)` remains the final arbiter of output ordering. So the dedup seam is now less order-dependent than before, not more.

4. **Residual seam/superset/scope concern.**  
   I do not see a new correctness regression in the seam as shown. The behavior now matches the stated intent:
   - body-only `FunctionClassification` items remain authoritative for body refs already emitted;
   - procedure-scoped refs add the missing header coverage;
   - body overlap is suppressed only against the body-only snapshot;
   - distinct procedure-scoped refs are no longer dropped just because they collide with each other on provenance.

   The stronger key also tightens the cross-feed suppression boundary so that same-start/same-reason chain links are not conflated across feeds. That is the right complement to the non-mutating loop.

Given the new reproducer was red before and green after, the full suite is green, and the golden remained byte-identical, I do not see an unresolved issue in this fix.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```