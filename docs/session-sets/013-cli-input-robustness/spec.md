# CLI Input Robustness Spec

> **Purpose:** Close the 006 review's **P5** lane — the remaining CLI
> input-validation gaps whose consumer is downstream automation/AI keying off
> exit codes. The highest-severity item is **F-1-3**: a misspelled `--types`
> value turns a full harvest into a **silent empty success (exit 0)** — an
> orchestrator reads "all objects processed successfully" when nothing ran.
> Alongside it: **F-1-2** (a legal separator-bearing Access object name makes
> `SaveAsTextExporter`'s unsanitised `Path.Combine` target a non-existent
> subdirectory, failing that object's export on a valid DB), the **O-1-4**
> shared-`ParseOptions` consolidation that retrofits unknown-switch rejection to
> all seven subcommands at once, and the smaller **F-1-5** / **F-1-6** / **O-1-7**
> robustness fixes.
> **Created:** 2026-06-14 (authored by 012 Session 3 per the
> last-session-authors-next-set pattern; routed recommendation in
> `docs/session-sets/012-switchboard-and-report-chain-edges/next-session-set-recommendation.md`)
> **Session Set:** `docs/session-sets/013-cli-input-robustness/`
> **Prerequisite:** P0 (committed green build/test baseline) only — independent
> of the 006 P1–P4 arc, slots anywhere. The 006 review arc P0→P1a∥P1b→P2→P3→P4 is
> complete (sets 007–012); P5 and P6 are the two remaining unowned ranked sets.
> **This is 006's P5.** Source of truth: the 006 review findings
> (`006-solution-review-and-upstream-feedback-discovery/s1-review-findings.md`
> F-1-2, F-1-3, F-1-5, F-1-6; the §1 F-1-2/F-1-6 premise-proof probes; the
> O-1-4 / O-1-7 observations).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 2
prerequisites: []
verificationMode: out-of-band-or-none
```

> Rationale: no UI surface; the quality gate is build + tests + cross-provider
> verification. This set is **mostly hermetic CLI-behaviour and parsing logic**
> (exit-code correctness, option validation, connection-string building,
> bracket-aware clause tokenisation) — the bulk validates with `dotnet build` +
> the affected hermetic test projects. **F-1-6** (bracket-aware `PARAMETERS`
> tokenisation) is the one fix that can change shipped output (`IrQuery.Parameters`
> → the navigation chain edges / `unresolved-items`), so **if** S2's F-1-6 work
> changes HomeHealthCare output it triggers a single coordinated golden regen
> that session; otherwise no regen (prove the no-regen claim byte-level, never by
> assertion). `verificationMode: out-of-band-or-none` matches the inline-verify
> Full-tier pattern (007–012): the cross-provider `session-verification` runs
> inside each work session; register with `--verification-mode out-of-band-or-none`.

---

## Project Overview

The CLI's primary consumer is a downstream automation/AI orchestrator that keys
off exit codes (the usage text promises `0 = all objects processed
successfully`). Every finding here is a case where **invalid or edge-case input
produces a wrong or silently-degraded result instead of an honest error** —
exactly the explicit-over-silent contract the harvester pins elsewhere. Each
finding is **proven** in the 006 record (F-1-3 by a built-CLI probe; F-1-2 and
F-1-6 premises by Session-4 DAO probes against a throwaway HomeHealthCare copy).
This set implements the recorded recommendations; it does not re-litigate the
diagnoses, but **re-grounds every claim against the current tree** (the 006
probes are dated 2026-06-12; sets 008–012 have since landed).

### F-1-3 — A misspelled `--types` value silently produces an empty harvest, exit 0 (Major)

`BuildPipeline` (`AccessHarvester.Cli/Program.cs`) passes `--types` values
straight into `CorePipeline.WithCategories`, a pure set filter; an unknown
category name matches nothing, so the run reports `Ok: 0, Failed: 0,
Categories: []`, writes nothing, and exits 0. The same `BuildPipeline` feeds
`extract`, `translate`, and `run`. **Fix:** validate each `--types` entry against
`CorePipeline.Default()`'s known category set (union of exporter/translator/
enricher categories) and throw `ArgumentException` (the existing usage-error
exit-1 path) listing the valid values; also reject an all-filtered-out result
defensively. This is the recorded "reject unknown switches" lesson one layer
down — the switch is known, its *value* is not validated.

### F-1-2 — Unsanitised object-name path component fails export on a valid DB (Major, escalated)

Access object names accept path separators (`/`, `\`) — proven: `CreateQueryDef`
stored `probe/slash` and `probe\bs` verbatim; `.`-bearing names are rejected by
Access, so **traversal above the output root is impossible** (the worst-case
branch is refuted). The remaining proven impact: a legal separator-bearing object
name makes `SaveAsTextExporter`'s unsanitised `Path.Combine(outDir, name + ext)`
target a non-existent subdirectory, failing that object's export (recorded
failure, exit 3) on a perfectly valid database. **Fix:** apply the siblings'
existing `SanitizeFileName` to the object-name path component in
`SaveAsTextExporter` (and audit the parallel exporter/writer sites for the same
unsanitised `Path.Combine`).

### F-1-5 — OLE DB connection strings interpolate the database path unquoted (Minor)

Both OLE DB helpers build `"Provider={provider};Data Source={path};Persist
Security Info=False;"` with `path` interpolated raw
(`Com/OleDbDaoSchemaReader.cs`, `Com/OleDbValueSampler.cs`). A `;` (a legal NTFS
filename char) or a stray quote in the path breaks connection-string parsing —
and the `BuildConnectionString` catch conflates "malformed connection string"
with "ACE provider missing." **Fix:** build via `OleDbConnectionStringBuilder`
(sets and quotes `Data Source` correctly), or quote the value. Two mechanical
call sites; robustness, not a security boundary.

### F-1-6 — `PARAMETERS`-clause parsing breaks on `,`/`;` inside bracketed names (Minor)

`QueryMetadataExtractor.ParseParametersClause` splits the clause body on bare `,`
and terminates the clause at the first `;` — but Access permits `,`/`;` inside
bracketed prompt-style names (proven: DAO accepts `[Enter start date, please]`
and `[Pick one; carefully]` as single parameters). A comma splits one declared
parameter into two garbage entries; a semicolon truncates the clause. **Fix:**
bracket-aware tokenisation (track `[`…`]` nesting before splitting on `,`/`;`).
The verbatim `Sql` is preserved and the jet-sql parser is the contract-grade
re-extraction path, so the doc-only route (extend the documented limitation
list) is an acceptable leaner alternative **only if** re-grounding shows the
bracket-aware fix would churn the golden disproportionately — decide explicitly
and record it. *(F-1-6 is the one finding that can change shipped output; if the
fix changes `IrQuery.Parameters` it cascades to the navigation chain edges and
`unresolved-items` → coordinated golden regen that session.)*

### O-1-4 / O-1-7 — `ParseOptions` duplication and bare-file-mode dispatch drift

**O-1-4:** `ParseOptions` is copy-pasted seven times (`Program.cs` + six command
classes); only `GenerateMigrationCommand` rejects unknown option keys (the
recorded lesson). Lift the helper to **one** shared implementation with an
allowed-keys parameter — this retrofits unknown-switch rejection to all seven
subcommands for free. **O-1-7:** the bare-file-mode dispatch list
(`Program.cs`) must be hand-synced with the subcommand switch; a missed entry
makes `AccessHarvester <newcmd>` fall into implicit-parse. A
static-set-plus-switch-from-one-table refactor removes the drift risk.

**Non-goals:** P6 `cleanup-zero-date-guard` (the zero-date misfire + O-1-5 +
O-2-6 — its own 1-session set), the open DW items (DW-11 unresolved-items
completeness, DW-12 resolve-failure asymmetry, DW-13 ribbon, DW-14
navigation-target completeness), and any new extraction/parser capability.
Anything discovered beyond F-1-2/3/5/6 + O-1-4/O-1-7 that no set owns → a
`DW-<n>` entry, not scope creep.

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

Per the 2026-06-12 lesson *constraints travel in routed prompts*, restated in
full (carried forward from 012; the per-delta finding mapping is updated to this
set's P5 findings):

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json`
  or any NorthwindDevEdition-derived artifact** — regeneration reintroduces
  the residue sets `003`–`005` removed. All fixtures and goldens derive from
  committed HomeHealthCare artifacts only. NorthwindDevEdition is
  proprietary and local-only.
- **Golden regeneration is HomeHealthCare-only** and follows the established
  regeneration path (`samples/GoldenOutput/HomeHealthCare/`); regenerate
  once per session, after that session's behavioral changes are complete,
  and re-run `CanonicalGoldenRegressionTests`. *Most P5 fixes are CLI-behaviour
  / hermetic and do NOT touch the golden — only F-1-6 can. If a session asserts
  "no golden regen needed," prove it byte-level (a fresh COM run reproduces the
  committed golden), never by assertion.* Every golden delta must map to F-1-6.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on,
  TRX `<Counters>` as the completion signal; never background a `dotnet`
  invocation; sweep spurious `samples/*.accdb` diffs
  (`git checkout -- samples/HomeHealthCare.accdb`) before staging any commit.
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`.
- **Do not weaken assertions or tests to force green.** Intent changes require
  explicit disclosure in the verification prompt. New validation that changes an
  exit code must be pinned by a hermetic test asserting the new exit code + the
  error message, not silently accepted.

### Set-specific constraint — new validation must be honest, not lossy

- **New `--types` validation rejects unknown values with a usage error (exit 1)
  listing the valid set** — it must NOT silently drop the unknown value and
  proceed, and must NOT change the exit code of a *valid* run.
- **`SanitizeFileName` application must not collide two distinct object names**
  onto one path (if sanitisation could map two names to the same file, that is a
  reportable condition, not a silent overwrite).
- **The shared `ParseOptions` must preserve each subcommand's existing accepted
  option set exactly** — the consolidation adds unknown-switch rejection, it does
  not change which options a command accepts.

---

## Sessions

### Session 1 of 2: Exit-Code Correctness — `--types` validation + object-name sanitisation (F-1-3, F-1-2)

The two Major exit-code-correctness fixes — the highest consumer-risk items.

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `deferred-work.md`, the 006 F-1-3 / F-1-2 findings + the §1 F-1-2 probe) and
   **re-ground**: confirm `BuildPipeline` still feeds `--types` unvalidated into
   `WithCategories`; confirm the known-category set source on
   `CorePipeline.Default()`; confirm `SaveAsTextExporter`'s unsanitised
   `Path.Combine` and the siblings' existing `SanitizeFileName` helper.
2. **F-1-3** — validate each `--types` entry against the known category set;
   throw `ArgumentException` (exit-1 usage path) listing valid values; reject an
   all-filtered-out result. Apply uniformly to `extract` / `translate` / `run`
   (all fed by `BuildPipeline`). Hermetic pins: unknown value → exit 1 + message;
   valid value → unchanged exit code; empty-result guard.
3. **F-1-2** — apply `SanitizeFileName` to the object-name path component in
   `SaveAsTextExporter` (audit parallel writer sites). Hermetic pin: a
   separator-bearing object name exports to a sanitised path (no exit-3 failure),
   with the collision guard from the set-specific constraint.
4. Hermetic validation of the affected CLI/exporter projects; **no golden regen
   expected** (CLI behaviour + exporter path only) — prove byte-level if claimed.
5. Cross-provider verification; closeout.

**Progress keys:** `s1-types-validation`, `s1-name-sanitisation`,
`s1-hermetic-pins`, `s1-verified`

### Session 2 of 2: `ParseOptions` Consolidation + Connection-String / PARAMETERS Robustness + Closeout (O-1-4, F-1-5, F-1-6, O-1-7)

**Steps:**

1. Pre-session reading; re-ground the seven `ParseOptions` copies, the two OLE DB
   connection-string sites, and `ParseParametersClause` against the tree.
2. **O-1-4** — lift `ParseOptions` to one shared implementation taking an
   allowed-keys set; route all seven subcommands through it (preserving each
   command's exact accepted-option set) so unknown-switch rejection is uniform.
   Hermetic pins per command class for unknown-switch rejection.
3. **F-1-5** — build both OLE DB connection strings via
   `OleDbConnectionStringBuilder` (or quote `Data Source`). Hermetic pin: a
   `;`-bearing path produces a valid connection string, not a provider-missing
   misreport.
4. **F-1-6** — bracket-aware `PARAMETERS`-clause tokenisation (track `[`…`]`
   nesting before splitting on `,`/`;`), OR the doc-only limitation-list route if
   re-grounding shows the parse fix churns the golden disproportionately — decide
   explicitly and record. Hermetic pins for `[Enter start date, please]` /
   `[Pick one; carefully]`. **If output changes**, single coordinated
   HomeHealthCare golden regen + COM `CanonicalGoldenRegressionTests` /
   `ManifestSchemaConformanceTests`; every delta maps to F-1-6.
5. **O-1-7** — derive the bare-file-mode dispatch list and the subcommand switch
   from one table to remove the hand-sync drift.
6. Final hermetic validation; golden regen only if step 4 changed output.
7. Cross-provider verification (closeout-grade: prose-first lead + trailing JSON
   block; embed earlier-session evidence per the cross-session-evidence lesson).
8. **Author the next session set** (last-session-authors-next-set): per the 006
   ranking the remaining unowned set is **P6 `cleanup-zero-date-guard`** (1
   session, P0-only); the open DW items (DW-11/12/13/14) are also candidate
   focused sets — author against actual post-013 completion state via routed
   recommendation, carrying the Hard Constraints block verbatim.
9. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify.

**Progress keys:** `s2-parseoptions-shared`, `s2-connection-string`,
`s2-parameters-bracket-aware`, `s2-dispatch-table`, `s2-golden-or-no-regen`,
`s2-next-set-authored`, `s2-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is P5 only** (F-1-2/3/5/6 + O-1-4/O-1-7). If a fix appears to require
  P6 cleanup work, a new extraction/parser capability, or a DW item, stop at the
  boundary, record a `DW-<n>` entry, and leave it to the owning set.
- **New validation must produce an honest error, never a silent drop or a changed
  exit code on valid input.** A fix that would make a previously-failing valid run
  pass differently is a stop-and-disclose signal.
- **Every golden delta (if any) must be explained by F-1-6.** An unexplained delta
  is a stop-and-investigate signal; a "no regen needed" claim must be proven
  byte-level.
- **Do not weaken assertions or tests to force green.**

## End-Of-Set Deliverables

- F-1-3: `--types` value validation (exit-1 usage error listing valid values;
  all-filtered-out guard) across `extract`/`translate`/`run`, hermetically pinned.
- F-1-2: object-name path-component sanitisation in `SaveAsTextExporter` (+ audited
  siblings), with the no-collision guard, hermetically pinned.
- O-1-4: one shared `ParseOptions` with allowed-keys unknown-switch rejection on
  all seven subcommands, each command's accepted-option set preserved.
- F-1-5: `OleDbConnectionStringBuilder`-built connection strings at both sites.
- F-1-6: bracket-aware `PARAMETERS` tokenisation (or recorded doc-only decision);
  golden regen only if output changed, every delta explained.
- O-1-7: drift-free bare-file-mode dispatch / subcommand table.
- Next session set authored (spec + not-started v4 state stub).
- Standard set artifacts per session: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md`, `sN-issues.json` for
  findings-bearing rounds, `disposition.json`, `change-log.md`,
  `next-session-set-recommendation.md`.
