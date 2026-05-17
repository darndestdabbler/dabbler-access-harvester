# Strategic Review — Platform Tooling Fit (2026-05-17)

> **Question:** Does dabbler-platform's `crud-scaffolding` set (closed 2026-05-16)
> — specifically the `dabbler-crud-service` + `dabbler-crud-page` templates —
> meet the workflow-driven use case planned in
> `docs/session-sets/blazor-update-workflows/spec.md`? If not, is that a
> flexibility gap in the platform tooling that should be closed, or is the
> harvester pilot meant to stay hand-authored? Secondary: across both repos,
> what loose ends should close before the next major effort?

> **Trigger:** Claude Code (the active orchestrator for blazor-update-workflows
> Session 1) noticed the platform's CRUD scaffolding mid-Session-1 and paused
> rather than continue extending the existing pilot host blindly. The human
> asked for cross-provider review before deciding whether to resume.

> **Reviewers:** Driven by the human via Codex (GPT-5.5) and Gemini 3.1 Pro
> in their respective VS Code windows — both with file-system access to
> `dabbler-access-harvester` and `../dabbler-platform`. Routed cross-provider
> calls were NOT used because the question requires live cross-repo browsing
> that single-prompt routing can't replicate. See
> `[[feedback-cross-repo-review-via-in-ide-orchestrators]]` memory.

## Files

| File | Provider | Date |
|---|---|---|
| `gpt-5-5.md` | OpenAI Codex / GPT-5.5 | 2026-05-17 |
| `gemini-3-1-pro.md` | Google Gemini 3.1 Pro (Preview) | 2026-05-17 |

## Headline findings (both reviewers agree)

1. **CRUD scaffolder is correctly applied narrowly** — it's a single-entity
   CRUD accelerator. `Update an Order` is a stateful multi-entity workflow.
   "Misapplied, not useless." Platform tool should be labeled honestly with
   an applicability note, NOT reshaped around Access migration.
2. **Stop new Blazor implementation in this repo.** Phases 1 + 2 already
   proved the package shape works. Further Blazor application work belongs
   in the planned third (migration) repo, not here.
3. **`strategy.md` is stale** — needs a 2026-05-17 revision capturing the
   `generalization-validation-on-non-northwind`, `lowdefy-ir-foundation`,
   and `dabbler-platform-discovery` actuals, plus an explicit finish line.
4. **`remaining-work-planner` should be revised before starting.** Spec
   predates current strategy. Retarget to "what remains for a shippable
   mechanical harvester CLI." Exclude migration-app work.
5. **Normalize stale state files** —
   `enrichment-functions-navigation-review` (Session 4 commit shipped, state
   still says in-progress).
6. **`dabbler-platform-discovery` outputs are a dated snapshot**, not
   forever-truth. Re-check when the future migration repo actually consumes.

## Slight divergence

- **Gemini:** decisively deprioritize Blazor work in this repo.
- **GPT:** offers a single-gated-proof compromise — if you still want one
  more scale validation, run `Update an Order` only, with explicit failure
  → stop.

## Outcome (decided 2026-05-17)

The human chose **Gemini's path**: drop the stashed Session 1 work, defer
all further Blazor application work to the future migration repo. Move 1
of the housekeeping pass (this commit) lands in this directory plus
`docs/planning/strategy.md`, `docs/session-sets/enrichment-functions-navigation-review/session-state.json`,
`docs/session-sets/lowdefy-ir-foundation/change-log.md`, and
`docs/session-sets/remaining-work-planner/spec.md`.

## Recommendations addressed in this repo's housekeeping commit

- ✓ Revise `strategy.md` with 2026-05-17 actuals + explicit finish line +
  frozen-unless-reauthorized list.
- ✓ Flip `enrichment-functions-navigation-review/session-state.json` to
  `complete`.
- ✓ Author missing `lowdefy-ir-foundation/change-log.md` from the rich
  session-state notes.
- ✓ Revise `remaining-work-planner/spec.md` to focus only on shippable
  mechanical harvester gaps.
- ✓ Drop the stashed Session 1 partial implementation of
  `blazor-update-workflows`.
- ✓ File these reviews in a permanent, citeable location (this directory).

## Recommendations deferred (for `../dabbler-platform` maintainers)

Both reviewers recommend small clarifying changes to dabbler-platform that
are NOT this repo's responsibility:

- Add an applicability note in `docs/scaffolding-templates/README.md`:
  "CRUD templates are for single-entity CRUD/admin slices. Complex
  workflows and domain actions belong in hand-authored pages + domain
  services."
- Fix documentation drift flagged in
  `../dabbler-platform/FEEDBACK_REMEDIATION_SUMMARY.md` (claims around
  `AddSupplierScaffolding()` usage vs direct DI wiring, navigation menu
  entry status, Playwright/manual verification wording).
- Keep Granularity 3 (DI/nav/policy automation) deferred until real usage
  pressure justifies it.
- Do NOT reshape the CRUD scaffolder around Access migration. That would
  turn a useful platform utility into a half-migration framework.

These should be carried over to dabbler-platform via the existing
`docs/portability-feedback-for-dabbler-platform.md` channel or as a
separate platform-side ticket.
