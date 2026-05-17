Finding 1 is resolved. The revised F3 stub now explicitly states, in both Session 2 Step 1 and In-scope item 4, that the `run` determinism gate subsumes `extract` and `translate`, and it gives a concrete rationale tied to the dispatcher / `PrintUsage()` model (`run = extract + translate + enrich`). That closes the prior Criterion #4 coverage gap; the spec no longer reads as if `extract` and `translate` were simply omitted.

Finding 2 is resolved. The tree’s routed-cost rollup now correctly sums the per-stub ranges to `~$1.00–$3.20`, and the old `~$1.00–$1.40` number is retained only as a clearly labeled earlier rough estimate, not as the active total.

Fresh drift sweep: the requested checks pass. The tree still says 5 sets / 13 sessions, and that total matches `F1=2 + F2=4 + F3=2 + F4=3 + F5=2`. The F3 cost expectation remains `~$0.10–$0.40` in both the stub and the tree rollup table. F3’s Out-of-scope section still includes the freeze-boundary bullet. I found no other active `$1.00–$1.40` reference in the tree beyond the historical reconciliation sentence. The revised F3 steps, stop rules, and stub-summary language remain materially aligned; I did not find new material drift.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```