# Next session set — Post-Phase-4 (harvester-finish-cli-packaging closes Phase 4)

## What the plan says

Per `docs/planning/strategy.md`, the repository's active development work consists solely of Phase 4, the "mechanical harvester finish line." The current session set, `harvester-finish-cli-packaging`, is the final gate in this phase. Upon its completion, the plan declares the work in this repository complete, deferring all subsequent Access-to-Blazor application migration tasks to a future migration repository.

The `ROADMAP.md` section "What happens after Phase 4 closes" elaborates on this transition. The `dabbler-access-harvester` repository will move into a maintenance posture. The future migration repository is planned to inherit the workflow-package shape, the Option D development pattern, the Phase 4 golden output as ground truth for migration, and the specific recommendations outlined in `strategy.md` for that future work.

## Recommended next session set

**Slug:** `(none — Phase 4 complete; harvester repo enters maintenance)`

**Rationale:** This recommendation **confirms** the plan. The closure of `harvester-finish-cli-packaging` completes the final gate of Phase 4. The harvester tool has reached its planned finish line, and no immediate, blocking maintenance items were deferred from this set that would justify a deviation.

**What the human should do next:**
- Decide when to bootstrap the future migration repo, using this repository's golden output and strategic documents as its starting point.
- Schedule a maintenance pass against the harvester only if a downstream consumer surfaces a bug or a concrete feature request.
- Decide whether `MiniComplex.accdb` returns to test coverage in a future maintenance pass (deferred by `remaining-work-planner` Open Question 1).

## Carry-overs and known follow-ups

- **Verifier instability:** The GPT-5.4 verifier has returned 429 errors for three consecutive sessions, forcing a fallback to `gemini-pro`. A future maintenance action could be to swap the default `session-verification` target in configuration.
- **`dotnet tool` channel:** Publication as a global `dotnet tool` was deferred per Open Question 3. This remains a valid option for a future maintenance set if a consumer requests this distribution channel.
- **Code signing and release publication:** Code signing was deferred as out-of-scope. No formal release channel (e.g., GitHub Releases, NuGet) was provisioned, as no external consumer has yet surfaced. These are deferred until required.