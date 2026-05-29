### Next session-set recommendation (after harvester-cli-distribution): `harvester-sql-migration-generator`

**Orchestrator:** `claude-opus-4-7` @ effort=medium

> Mechanical normalization: the analysis call named the model as
> "Claude 3 Opus, effort: Medium". Anthropic's current Opus is
> `claude-opus-4-7` (per [CLAUDE.md](../../../CLAUDE.md)); the
> supported effort levels are `low | medium | high | max` (per
> [lessons-learned.md](../../planning/lessons-learned.md) →
> "`effort:xhigh` Is Not A Valid Anthropic API Effort Level").
> `medium` is the nearest valid mapping of the analysis's intent.

**Rationale:** This set directly completes the final, explicitly
deferred phase of the [`harvester-data-classifier`](../harvester-data-classifier/change-log.md)
plan — Phase 4 of
[`docs/planning/data-classification-plan.md`](../../planning/data-classification-plan.md):
the SQL Server migration generator. It transforms the `sanitize`
subcommand's output from an intermediate artifact (CSV-per-table) into
a final, high-value product — a runnable SQL Server
CREATE-TABLE/INSERT migration script. Completing this work makes the
entire data classification + sanitization pipeline end-to-end useful,
unblocking downstream migration projects (the future migration repo
per the [2026-05-17 strategy reset](../../strategic-reviews/2026-05-17-platform-tooling-fit/))
by providing a zero-friction database bootstrap from a sanitized
Access source. We accept the trade-off of deferring distribution
observability (release-download telemetry, supported-versions doc),
the `integration-testing-and-acceptance` reframing decision, and
router-config repo-maintenance to deliver the last piece of core
harvester functionality.

**Estimated routed cost band:** Moderate ($0.50-$2.00)

**Sessions estimate:** 3

**Stop-rule flag:** Stop if a pre-flight analysis determines that
[`classification.json` v1.0](../../../AccessHarvester.Pipeline/Classification/classification-schema.json)
and the sanitized CSV-per-table output are insufficient to generate a
foreign-key-consistent SQL Server migration without additional
metadata (e.g., missing collation, identity-seed, or computed-column
information that the OLE DB schema reader did not capture). Surface to
the human before authoring the spec.
