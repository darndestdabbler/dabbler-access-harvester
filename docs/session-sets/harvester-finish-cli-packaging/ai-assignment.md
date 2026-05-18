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
an explicit model-pool list in the prompt. Session 2 fills in its own
actuals at its Step 8.