# Next Session Set Recommendation

**From:** `harvester-sql-migration-generator` (Session 3 of 3, closed 2026-06-01)
**Recommendation: none — the harvester development arc is complete; the
repository is now consumer-driven-only.**

This was the **second and last** of the two remaining harvester sets (the
classify-timeout fix, then this SQL-migration-generator capstone). With
`generate-migration` shipped, Phase 4 of the data-classification plan is
closed and the mechanical-harvester finish line is reached. Per the
standing strategy (`project_consumer_driven_only_after_two_sets`):

- **No self-initiated session sets.** Do not author a new spec on the
  orchestrator's own initiative.
- **New sets only on a concrete downstream-consumer request** — a real
  finding, gap, or feature ask from an actual consumer of the harvester
  output (the documentation/migration agent, a human evaluator, or a
  downstream tool), the same trigger that drove
  `harvester-manifest-index-and-batch-readiness` and the report-baseline
  follow-ons.

## Known, non-blocking follow-ups (only if a consumer asks)

These were recorded during the arc and are explicitly **not** scheduled —
each waits on a real consumer need:

- **Genuine-empty-string preservation end-to-end.** The sanitize
  `CsvWriter` emits both SQL NULL and a real empty string as an unquoted
  empty field, so the migration data-load loads an empty field as NULL
  (documented + pinned in `CsvTableReader`; verifier-agreed out of scope
  for this set). If preserving genuine empty strings end-to-end becomes a
  product requirement, change the producer (quote empty strings or use a
  NULL sentinel) as sanitize-tier work.
- **Live SQL Server execution validation.** The emitted migration is
  validated hermetically (golden-script + dialect checks). An actual run
  against a live SQL Server instance remains an optional, non-gated manual
  spot-check; standing up a SQL Server CI target would be a consumer-driven
  decision, not harvester-core work.
- **Custom identity seed/increment, calculated/computed columns, explicit
  collation.** Mapped to documented defaults and flagged as degradations
  (Access does not expose these, or they are rare). Tighten only if a
  consumer's target schema needs it.

Application/runtime migration (the Blazor host, ORM models, data-load
orchestration against a live server) stays deferred to the future
migration repo, not this codebase.
