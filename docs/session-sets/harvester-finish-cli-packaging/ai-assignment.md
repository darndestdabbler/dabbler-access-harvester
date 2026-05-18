# ai-assignment.md — harvester-finish-cli-packaging

### Session 1 of 2: Publish Wiring + Reproducible Build

**Recommended orchestrator:** `Claude Opus 4.7 @ effort=high`

**Rationale:** Tasks are mechanical but require precise property selection and documenting the `PublishTrimmed=false` trade-off due to reflection and COM interop, which benefits from a high-effort model's synthesis.

**Estimated routed cost:** `$0.05 - $0.20` (for cross-provider verification only).

**Step-by-step guidance:**

| Step # | Task | Agent Guidance |
| :--- | :--- | :--- |
| 1 | Update `AccessHarvester.Cli.csproj` | Add a `PropertyGroup` for publish configuration. Use RID `win-x64`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, and `PublishTrimmed=false`. Add a comment explaining the trim decision is due to ANTLR runtime reflection and DAO COM interop. |
| 2 | Author publish script | Create `tools/publish/publish.ps1`. The script should contain a single `dotnet publish` command targeting the CLI project with the correct release configuration. |
| 3 | Update `.gitignore` | Add the publish output directory (e.g., `publish/`) to the project's `.gitignore` file. |
| 4 | Validate artifact | Run the new publish script. Manually invoke the resulting `<output>/AccessHarvester.exe --help` from a terminal to confirm it launches and displays usage text. |
| 5 | Run project tests | Execute `dotnet build && dotnet test` to ensure no regressions were introduced. |
| 6 | End-of-session verification | Commit work with a conventional commit message. Push to the remote. Generate the session review and notify the human orchestrator. |

**Actuals (filled post-session):**

- **Orchestrator:** `claude-code claude-opus-4-7 @ effort=high`
- **Actual routed cost:** ~$0.030 across three routed calls:
  - `route(task_type="analysis")` (ai-assignment authoring) — gemini-pro, $0.0135.
  - `route(task_type="session-verification")` — first-choice gpt-5-4 returned three 429s; fell back to `query(model="gemini-pro", task_type="session-verification")` per the verifier-failure escalation ladder (step 2), $0.0104.
  - `route(task_type="analysis")` (Session 2 next-orchestrator recommendation, this Step 8) — gemini-pro, $0.0063.
  Well inside the spec's $0.05–$0.20 band.
- **Commit hash:** filled by the closeout commit.
- **Session review:** `docs/session-sets/harvester-finish-cli-packaging/session-reviews/session-001.md`
- **Deviations from recommendation:** None on orchestrator choice. Verifier deviated from preferred pairing (gpt-5-4) because of OpenAI 429 rate-limiting; recorded in metrics with the fallback annotation.
- **Notes for next-session calibration:** Step 3.5 ai-assignment placeholder named "GPT-4o" for Session 2 — not in this repo's pool. Verifier flagged Minor (Round 1). Resolved by re-routing the Session 2 recommendation at Step 8 with an explicit model-pool list in the prompt; see `next-orchestrator-recommendation-session-2.md`.

**Next-session orchestrator recommendation (Session 2):** see [`next-orchestrator-recommendation-session-2.md`](next-orchestrator-recommendation-session-2.md) — `claude-code opus @ effort=high`, low routed cost.

---

### Session 2 of 2: Black-box Smoke Tests + Install Docs + Closeout

Recommendation has been superseded. The authoritative recommendation
for Session 2 lives in
[`next-orchestrator-recommendation-session-2.md`](next-orchestrator-recommendation-session-2.md),
authored via `route(task_type="analysis")` at Session 1 closeout with
an explicit model-pool list in the prompt.

**Actuals (filled at Session 2 Step 8):**

- **Orchestrator used:** `claude-code claude-opus-4-7 @ effort=high`
  (matches the routed recommendation).
- **Total routed cost (this session):** **~$0.0484** across three calls:
  - `route(task_type="session-verification")` — first-choice `gpt-5-4`
    returned three 429s (three-for-three pattern across the recent
    closeout-grade rounds in this repo); fell back via `query("gemini-pro", task_type="session-verification")`
    per the verifier-failure escalation ladder step 2. $0.0363.
  - `route(task_type="summarization", max_tier=1)` — change-log on
    `gemini-flash`. $0.0020.
  - `route(task_type="analysis")` — next-session-set recommendation on
    `gemini-pro`. $0.0101.
- **Set-total routed cost:** ~$0.078 across both sessions — below the
  spec's $0.10–$0.40 band.
- **Deliverables:** PublishedArtifact path-resolution helper +
  `RepoPaths.LocateDirectoryContaining`; 4 `[SkippableFact]`
  `PackagedBinarySmokeTests` (bootstrap / `--help` / parse /
  generate-docs); `--version` flag with `SchemaVersionLiteral = "1.0"`
  pinned to `DatabaseManifest.SchemaVersion`; bootstrap theory test
  covering both flag forms; `docs/harvester/install.md`; README CLI
  addendum link; closeout artifacts (change-log, next-session-set
  recommendation, disposition.json overwrite, this actuals block,
  session-002 verifier review).
- **Build/test:** 0 errors; 31 Integration tests pass (including 2 new
  bootstrap `--version` theory rows and 4 new smoke tests). The
  packaged binary at `publish/win-x64/AccessHarvester.exe` was
  regenerated mid-session (36.9 MB, same SHA256 family) so the smoke
  tests exercised the post-version-flag build.
- **Verifier verdict:** **VERIFIED** Round 1 with substantive 7-point
  prose analysis and empty issues array. The mandatory
  prose-before-JSON lead worked on the first try (this is the
  Bare-Verdict-Reminder lesson paying off).
- **Deviations from recommendation:** None on orchestrator choice. The
  Step-3.5 placeholder recommendation routing-table named
  `gpt-5-4` for `code_generation` and `prose_generation` tasks — those
  routings were not used because the work either fit the
  "orchestrator's model matches routing target" Convention
  (in-conversation prose authoring) or was mechanical-direct (test
  authoring under ~50 lines apiece). Cross-provider
  `session-verification` was forced to fall back from `gpt-5-4` to
  `gemini-pro` because OpenAI returned 429.
- **Notes for next-session calibration:** N/A — this is the last
  session of the set. The GPT-5.4 429 recurrence (three-for-three
  across recent closeout-grade rounds) is surfaced as a carry-over
  in the next-session-set recommendation.

**Next-session orchestrator recommendation:** N/A — last session of the set.
The next-session-set recommendation lives at
[`next-session-set-recommendation.md`](next-session-set-recommendation.md);
it confirms the plan (Phase 4 closes with this set; no immediate next
set in this repo; harvester moves to maintenance posture).