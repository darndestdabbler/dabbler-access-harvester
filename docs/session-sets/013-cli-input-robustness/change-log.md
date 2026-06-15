# Change Log — Set 013, CLI Input Robustness (006's P5)

**Status:** COMPLETE — 2 of 2 sessions, set closed 2026-06-14.
**Verification:** cross-provider routed `session-verification` (gpt-5-4),
VERIFIED each session (S2 after one fix round).
**Golden:** no regeneration — F-1-6 (the one output-affecting fix) proven
**golden-neutral byte-level** on HomeHealthCare.

This set closed the 006 review's **P5** lane: CLI input-validation gaps where
invalid or edge-case input produced a wrong or silently-degraded result instead
of an honest error — exactly the explicit-over-silent contract the consumer
(which keys off exit codes) depends on. With it, the 006 review arc
P0→P1a∥P1b→P2→P3→P4→P5 is complete; only **P6** (`014-cleanup-zero-date-guard`)
remains.

## Session 1 — Exit-code correctness (F-1-3, F-1-2)

- **F-1-3 — `--types` value validation.** `BuildPipeline` now validates each
  `--types` value against the live `Pipeline.KnownCategories` set and rejects an
  all-filtered-out selection, so a misspelled category is an honest exit-1 usage
  error instead of a silent empty success (exit 0). The stale usage text was
  corrected (it omitted `Navigation`, added by 012).
- **F-1-2 — object-name path sanitisation.** `SaveAsTextExporter` now sanitises
  the object-name path component (mirroring its sibling exporters) via the pure,
  testable `PlanOutputFiles`, with a collision guard that reports a non-overwrite
  failure rather than silently overwriting when two distinct names sanitise to
  the same file.
- No golden regen (proven byte-level). Verdict: VERIFIED (gpt-5-4 R1).

## Session 2 — Consolidation + connection-string / PARAMETERS robustness (O-1-4, F-1-5, F-1-6, O-1-7)

- **O-1-4 — one shared `ParseOptions`.** The seven copy-pasted `ParseOptions`
  bodies (`Program.cs` shared by six subcommands + six command classes; only
  `GenerateMigrationCommand` rejected unknown keys) were lifted to a single
  `AccessHarvester.Cli.OptionParser`. It rejects positional arguments, unknown
  `--switches`, and **repeated options** (the last added in response to the
  verifier — see below), uniformly across all twelve subcommands, with each
  command's exact accepted-option set preserved.
- **O-1-7 — drift-free dispatch.** A single `Commands` dictionary now feeds both
  the subcommand dispatch and the bare-file-mode exclusion, so the two can no
  longer drift apart when a command is added.
- **F-1-5 — OLE DB connection strings.** A new `AceConnectionString.Build` (via
  `OleDbConnectionStringBuilder`) replaced raw `Data Source={path}` interpolation
  at **all four** identical sites (re-grounding found four, not the two the spec
  named — sets 008–012 added sites since the 006 probe), so a `;`- or
  quote-bearing database path is quoted correctly instead of misreported as "ACE
  provider missing."
- **F-1-6 — bracket-aware `PARAMETERS` tokenisation.** The clause terminator `;`
  and entry separator `,` are now honoured only at bracket depth 0, so
  DAO-legal prompt names like `[Enter start date, please]` and
  `[Pick one; carefully]` are no longer shredded or truncated. This is the one
  fix that *could* change shipped output; it does **not** for HomeHealthCare
  (whose PARAMETERS names carry no name-internal `,`/`;`), **proven byte-level**
  by `CanonicalGoldenRegressionTests` reproducing the committed golden without
  regeneration.
- **Verifier-found Major (fixed).** Routed gpt-5-4 R1 flagged that the shared
  parser silently overwrote repeated options (`--out a --out b` → last wins) — a
  pre-existing gap in all seven prior copies, but squarely on this set's
  honest-error theme. Fixed by rejecting a repeated `--key` as a usage error
  (no subcommand has a repeatable option). R2 → VERIFIED.

## Deferred work recorded

- **DW-15** — `identify-cleanup` returns exit **2** (not the contract's `1`) for
  usage errors by its own pre-existing inverted convention. O-1-4 only adds
  rejection; renumbering the exit code is out of P5 scope (and the change is
  exit-code-preserving for that command). Recorded for a future
  `cli-exit-code-consistency` set and pinned by a dedicated test.

## Validation

- Hermetic `AccessHarvester.Tests`: **691 passed / 0 failed** (2 skipped).
- Black-box Integration (`CliUnknownSwitchTests` + `CliTypesValidationTests`):
  **15 passed**.
- COM tier (`CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests` +
  `CliExtract/Run/Translate`): **10 passed**; golden byte-identical, no regen.
- `dotnet build` solution: 0 errors.

## Artifacts

`s2-verification-prompt.md` / `s2-verification.md` (R1), `s2-verification-r2-prompt.md`
/ `s2-verification-r2.md` (R2), `s2-issues.json`, `set013-devils-advocate-prompt.md`
(set-wide, for human-driven GPT+Gemini), `disposition.json`,
`next-session-set-recommendation.md`, and the `014-cleanup-zero-date-guard`
spec + v4 state stub.

## Next session set

**`014-cleanup-zero-date-guard`** (006 P6, the final unowned ranked set): the
`identify-cleanup` zero-date misfire + O-1-5 + O-2-6. 1 session, P0-only,
recommended Claude Code / Opus 4.8 / effort=low. Closes the 006 review arc.
