# Change Log — 005 Northwind Embedded-Residue Remediation

> **Set:** `005-northwind-embedded-residue-remediation` (3 sessions, Full tier,
> non-destructive / `HEAD`-only). **Completed:** 2026-06-05.
> **Outcome:** the tracked working tree is **NorthwindDevEdition-free at `HEAD`**
> for structural identifiers. **No git-history rewrite or force-push** (maintainer
> decision: structural names are materially lower-sensitivity than the VBA source
> bodies `003`/`004` already purged from history; old names remain reachable in
> pre-rewrite history as accepted residue).

## Purpose

Sets `003` and `004` removed the densest NorthwindDevEdition disclosure (the
proprietary binaries, data, full VBA source bodies, and symbol-DB SQLite artifacts)
from all history. What remained, per `003`'s `embedded-residue-findings.md`, was
**structural disclosure** — the proprietary DB's object *names* (and, in places,
paraphrased business logic) embedded in keep-files. This set finished the job at
`HEAD`: it deleted spent process artifacts, re-based or deleted the corpus-pinned
tests onto the synthetic HomeHealthCare fixture, genericized the one fixture-bound
CLI helper, and redacted the incidental name references in source and docs.

## Session 1 — Refresh inventory + disposition manifest + bulk-delete spent artifacts

- Re-ran the `003` residue scan against `HEAD` and authored a reviewed per-file
  **disposition manifest** (`disposition-manifest.md` / `.csv`) classifying every
  residue path as delete / re-base / genericize / redact / keep-accepted.
- **Material finding:** the fresh scan surfaced a coupled **order-entry pilot arc**
  (4 workflow packages, a 33-file `CharacterizationTests` tree, the
  `blazor-add-an-order` MudBlazor app wired into the `.sln`, and the
  `blazor-*` session-sets) that encodes full Northwind order/product/employee
  **business logic** — denser than structural names. With maintainer authorization,
  the whole arc was deleted as in-scope spent proprietary-logic residue.
- Deleted ~198 paths (spent probe/translation/peer-review/session-review docs,
  driver scripts, superseded specs, the pilot arc) and trimmed `AccessHarvester.sln`.
  Build + hermetic tests stayed green.

## Session 2 — Test triage: re-base or delete the corpus-pinned tests

- Refined Session 1's blanket "re-base" of the 26 corpus-pinned test paths into a
  reviewed per-file disposition (`test-triage.md`): 6 keep-clean (manifest
  over-included), 5 redact-literals (distinctive identifiers renamed, generic SQL
  vectors kept, **every assertion preserved**), 6 re-base (re-pointed `FixtureLoader`
  from the local-only NorthwindDevEdition kb to the **committed HomeHealthCare** kb —
  `[SkippableFact]`s that skipped on a fresh clone now run **live**), 3 edit-trim-arm
  (dropped the NorthwindDevEdition COM/golden arms, kept the HHC arms), 1 file delete
  (`GenerateArtifacts`), and 1 project delete (`AccessHarvester.TranslationProbe.Tests`,
  a spent reimplementation of a proprietary form's logic).
- No assertion was weakened to green. Build + hermetic tests green; cross-provider
  verification VERIFIED after three rounds.

## Session 3 — Genericize C# source + redact docs + final residue scan + closeout

- **Genericized** `AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs`:
  `RunForNorthwind → RunForFixture`, parameterized over a new `FixtureObjectSet`
  record. The sole caller (`tools/symbol-db/probe-builder/`) supplies the object set
  via required CLI args, so **no proprietary identifiers remain in tracked source**;
  behavior is identical given identical inputs.
- **Redacted** incidental structural identifiers in 6 C# source files
  (comments / doc-strings / one report-prose string — no behavior change), the
  symbol-db README, and ~14 reference/planning docs + live tooling. Repointed the
  interpretation-guide's golden-corpus links to the **committed HomeHealthCare**
  corpus and fixed dead `examples/northwind-output.json` references.
- **STOP-RULE finding:** `docs/lowdefy-evaluation/prompt-for-codex.md` embedded the
  full deleted `add-an-order` workflow package verbatim (business logic denser than
  structural names). Surfaced to the maintainer (`s3-stop-rule-finding.md`); the
  maintainer chose to **delete the whole 5-file `lowdefy-evaluation/` arc** as a
  spent evaluation arc.
- The final scan caught **4 inventory-gap files** the Session-1 manifest missed
  (`ir-schema-forms-reports.md`, `AccessHarvester.IrCodegen` Program.cs + README,
  the generalization spec, and the `form-report-code-and-grouping` process records);
  all were redacted.
- Confirmed the local-only NW fixture trees stay gitignored; added a
  "NorthwindDevEdition-free at `HEAD`" policy note to `project-guidance.md`.
- **Final `HEAD` residue scan: PASS** (`final-residue-scan.md`) — zero distinctive
  NW structural identifiers outside accepted paths (`003`/`004`/`005` process
  records + 2 immutable verifier outputs). The brand/fixture token
  `Northwind` / `NorthwindDevEdition` is kept where it documents the local-only
  fixture identity.
- Build 0 errors; hermetic suite **green (1711+ tests)**. Cross-provider
  verification (gpt-5-4): **VERIFIED, 0 issues**.

## Known follow-up (recorded, not actioned)

The `AccessHarvester.Integration` CLI/schema suite has **8 pre-existing failures**
(proven independent of this set): 7 depend on a **never-committed**,
NorthwindDevEdition-derived `examples/northwind-output.json`, and 1 is a COM
`sanitize` timeout. These must **not** be "fixed" by regenerating the NW-derived
file (that would reintroduce removed residue). The compliant fix — re-basing those
Integration fixtures onto a committed HomeHealthCare-derived manifest — is the
recommended next set `006-integration-test-rebase` (see
`next-session-set-recommendation.md`); it is compliance, not feature, work.

## Net result

The repo is NorthwindDevEdition-free at `HEAD` for structural identifiers, with the
harvester / symbol-DB **engine behavior unchanged**, the test harness re-based onto
the committed synthetic fixture, and all removals reviewed rather than mechanically
grep-matched. Pre-rewrite history retaining old structural names is accepted
residue by maintainer decision.
