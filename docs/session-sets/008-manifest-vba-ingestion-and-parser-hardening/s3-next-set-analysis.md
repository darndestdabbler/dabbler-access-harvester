Run **P1b — output-encoding-correctness**.

This session resolves a critical, silent data corruption bug (F-1-1) affecting all non-ASCII content in the consumer-facing `kb/` and `ir/` tiers. Correcting this fundamental data integrity failure delivers the highest possible consumer value for the lowest cost (1 session). While P2 addresses important contract-fidelity issues, they are degradations of service, not irreversible data loss. Fixing the encoding is the most valuable and economical next step, restoring trust in the harvester's primary output before proceeding to finer-grained fidelity improvements or new feature enablement.

```json
{
  "next_set_slug": "009-output-encoding-correctness",
  "size_sessions": 1,
  "orchestrator": "manual",
  "effort": "low",
  "can_parallel_with": [
    "010-output-contract-fidelity-batch"
  ],
  "one_line": "Fix critical, silent data corruption of non-ASCII text in consumer-facing output."
}
```