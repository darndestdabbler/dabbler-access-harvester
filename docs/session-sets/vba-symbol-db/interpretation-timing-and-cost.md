# Interpretation Timing and Cost — vba-symbol-db

> Covers Sessions 4 and 5. All times are wall-clock from `BuildRuns` unless noted.
> Per-item API response times are from `ai-router/router-metrics.jsonl`.
> All costs are in USD.

---

## Per-Provider Summary (42-item sample basis for opinions 2 & 3)

| Provider | Opinion | Items | Wall-clock total | Avg per item | Total cost | Cost per item |
|---|---|---|---|---|---|---|
| Gemini Pro | 1 | 103 | 50 min 21 s | ~29 s | $1.47 | ~$0.014 |
| Opus (claude-opus-4-6) | 2 | 42 | 24 min 50 s | ~35 s | $5.41 | ~$0.129 |
| GPT-5.4 | 3 | 42 | 36 min 8 s | ~52 s | $2.14 | ~$0.051 |

> **Note on Gemini per-item API response time:** The router metrics record a mean of
> 14.6 s per documentation call for Session 4, but those 416 recorded calls span
> multiple intermediate runs during development. The BuildRun wall-clock (50:21 for 103
> items = ~29 s/item) is the authoritative figure; it includes SQLite writes, prompt
> building, and orchestration overhead on top of the raw API latency.

---

## Session 4: Gemini Pro (Opinion 1, all 103 items)

| Metric | Value |
|---|---|
| BuildRun start (UTC) | 2026-04-29T13:06:20 |
| BuildRun end (UTC) | 2026-04-29T13:56:41 |
| **Wall-clock duration** | **50 min 21 s** |
| Items interpreted | 103 |
| Avg wall-clock per item | ~29 s |
| API response time: min / avg / max | 3.2 s / 14.6 s / 34.8 s |
| Total cost | $1.47 |
| Cost per item | ~$0.014 |
| Mean confidence | ~0.97 (98% of items ≥ 0.90) |

---

## Session 5: Opus (Opinion 2, 42-item sample)

| Metric | Value |
|---|---|
| BuildRun start (UTC) | 2026-04-29T15:24:29 |
| BuildRun end (UTC) | 2026-04-29T15:49:19 |
| **Wall-clock duration** | **24 min 50 s** |
| Items interpreted | 42 |
| Avg wall-clock per item | ~35 s |
| Total cost | $5.41 |
| Cost per item | ~$0.129 |
| Mean confidence | 0.787 |

---

## Session 5: GPT-5.4 (Opinion 3, 42-item sample)

| Metric | Value |
|---|---|
| BuildRun start (UTC) | 2026-04-29T15:49:31 |
| BuildRun end (UTC) | 2026-04-29T16:25:39 |
| **Wall-clock duration** | **36 min 8 s** |
| Items interpreted | 42 |
| Avg wall-clock per item | ~52 s |
| Total cost | $2.14 |
| Cost per item | ~$0.051 |
| Mean confidence | 0.754 |

---

## Session 5 full timeline (EDT)

| Phase | Start | End | Duration |
|---|---|---|---|
| Setup, required reading, orchestrator recommendation | 11:17 | 11:24 | ~7 min |
| Code (sampler.py, independent_runner.py, tests, sample.json) | 11:17 | 11:24 | included above |
| Opinion 2 — Opus, 42 items | 11:24 | 11:49 | 24 min 50 s |
| Opinion 3 — GPT-5.4, 42 items | 11:49 | 12:25 | 36 min 8 s |
| Build/test, sample-report, log steps | 12:25 | 12:28 | ~3 min |
| Verification round 1 (gpt-5-4) | 12:28 | ~12:30 | 2 min 10 s |
| Fix C1 + M1–M4 (5 issues) | ~12:30 | ~12:36 | ~6 min |
| Verification round 2 (gpt-5-4) | ~12:36 | ~12:38 | 2 min 3 s |
| Fix m1–m3 (3 minor test gaps) | ~12:38 | ~12:39 | ~1 min |
| Verification round 3 (gpt-5-4) | ~12:39 | ~12:40 | 1 min 25 s |
| Commit + push | ~12:40 | 12:40 | < 1 min |
| **Total session** | **11:17** | **12:40** | **~1 h 23 min** |

### Session 5 time by category

| Category | Duration |
|---|---|
| Orchestration (setup, coding, testing, logging) | ~20 min |
| Opinion 2 API calls — Opus | ~25 min |
| Opinion 3 API calls — GPT-5.4 | ~36 min |
| Verification rounds 1–3 | ~12 min |
| **Total** | **~1 h 23 min** |

---

## Cost scaling estimates — full NorthwindDevEdition

> The measurement report covers only the 2 probe modules (103 items). Full-DB item
> count requires running the pipeline against all Northwind modules; the table below
> uses per-item actuals to bracket the cost at plausible item counts.

| Scenario | Items | Gemini only | Gemini + Opus | All 3 providers |
|---|---|---|---|---|
| Conservative | 300 | ~$4 | ~$43 | ~$58 |
| Midrange | 500 | ~$7 | ~$72 | ~$98 |
| Full | 700 | ~$10 | ~$101 | ~$137 |

Per-item costs used: Gemini ~$0.014, Opus ~$0.129, GPT-5.4 ~$0.051.

For risk-tiered databases where full three-provider coverage is warranted on every
symbol, the cost is dominated by Opus (~68% of the all-3 total). A selective
escalation strategy — run all-three only on items where Session-4 confidence < 0.75
(historically < 5% of items in Northwind) — keeps Opus cost proportional to actual
uncertainty rather than flat per-item.

---

## Provider speed comparison

| Provider | Avg per item | Relative to Gemini |
|---|---|---|
| Gemini Pro | ~29 s | 1× (baseline) |
| Opus | ~35 s | 1.2× slower |
| GPT-5.4 | ~52 s | 1.8× slower |

GPT-5.4 is the slowest per-item despite being the cheapest of the two cross-providers.
For Session 6's 126 grading calls, batching 6–8 short items (enum values, lookup tables)
per call is worth implementing to reduce wall-clock time without breaking idempotency.
