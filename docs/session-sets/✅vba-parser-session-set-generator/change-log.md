# Change Log

## [vba-parser-session-set-generator] - 2026-04-25

### Added
- `harvester-import-retrospective.md`: 17-row finding table (16 adopt-now, 1 no-op) based on `accessexportertemp1-import` actuals.
- `session-set-tree.md`: 18 child session sets for VBA parser plan, including DAG, session counts (66-81), parallel branches, test-case coverage, and open questions.
- 18 stub `spec.md` files: One for each child session set, including standard frontmatter, Project Overview (stub), Feature sections, abbreviated Session Plan, routing notes, and "Canonical tree row".
- `routing-playbook.md`: Routing playbook for all 18 child session sets, covering 15 task categories, session-startup checklist, orchestrator tier selection, deliverable-to-task-type mapping, verification-prompt assembly, proposed `router-config.yaml` deltas (3 new task types), Step 9 reorganization protocol, and new next-session-set recommendation protocol.

### Changed
- `router-config.yaml`: `effort: xhigh` replaced with `max`.
- `providers.anthropic.timeout_seconds`: Increased from 300 to 600 seconds.

### Next Steps

**Next session-set recommendation (after vba-parser-session-set-generator):**
`parser-foundations` AND `vba-spec-study` (run in parallel — both are DAG roots per `session-set-tree.md`)

**Recommended orchestrator for `parser-foundations`:** Claude Sonnet 4.6 @ effort=medium
**Rationale:** `parser-foundations` has 1 synthesis session (verification harness design) of 3-4 total — primarily mechanical work (ANTLR build wiring, schema reconciliation). Sonnet@medium suffices for the mechanical sessions; the single synthesis session can escalate to Opus@max if the harness design proves complex.

**Recommended orchestrator for `vba-spec-study`:** Claude Sonnet 4.6 @ effort=medium
**Rationale:** `vba-spec-study`'s 2 "synthesis sessions" are `analysis`-type calls to gemini-pro (per routing-playbook.md §3.6), not `architecture`-type Opus calls. There is no in-conversation advantage for an Opus orchestrator; Sonnet@medium is the correct default per routing-playbook.md §2.