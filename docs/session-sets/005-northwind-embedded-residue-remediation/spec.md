# Northwind Embedded-Residue Remediation Spec

> **Purpose:** Remove the remaining **NorthwindDevEdition** structural disclosure
> that sets `003` and `004` deliberately left in place — the **HIGH/MEDIUM/LOW
> embedded-residue footprint** recorded in `003`'s
> [`embedded-residue-findings.md`](../003-northwind-proprietary-data-remediation/embedded-residue-findings.md):
> ~116 KEEP-files (the original 123 minus the 7 CRITICAL SQLite/data artifacts
> `004` already purged) that still name proprietary form / report / query / table /
> module / control / procedure identifiers in spent process docs, workflow
> packages, live tests, C# source, and reference docs. `003` removed the dedicated
> proprietary artifacts; `004` removed the symbol-DB SQLite binaries + derived
> data. This set finishes the job at **`HEAD` only** — delete spent artifacts,
> re-base or delete the corpus-pinned tests, genericize/redact the C# source, and
> redact the doc references — so the **tracked working tree** carries no
> NorthwindDevEdition structural disclosure. **This set performs NO git-history
> rewrite and NO force-push** (maintainer decision 2026-06-05: structural names are
> materially lower-sensitivity than the VBA source bodies `003`/`004` purged, and a
> third force-push is not warranted for them; old names remain reachable in
> pre-rewrite history by accepted choice).
> **Created:** 2026-06-05
> **Session Set:** `docs/session-sets/005-northwind-embedded-residue-remediation/`
> **Prerequisite:** `003` and `004` complete (both are). Operates on the current
> tree only, so no structural DAG prerequisite is declared.
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification. Full
> tier; no UI, so no UAT/E2E. **Non-destructive** — `git rm`/edit at `HEAD` +
> gitignore only; the only outward-facing action is ordinary commits/pushes to
> `main`.
> **Status:** Draft

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatStyle: none
uatScope: none
prerequisites: []
totalSessions: 3
```

> Rationale: the set deletes spent artifacts, re-bases/deletes tests, and
> genericizes/redacts source + docs — all at `HEAD`. It touches no UI and changes
> no harvester *behavior* (the engine is already generic; only its corpus-pinned
> test harness, one CLI helper, and incidental name references are bound to
> Northwind). The quality gate is build + hermetic tests (green both with and
> without the local proprietary fixture) + cross-provider verification, with no
> UAT/E2E layer. Because there is no history rewrite or force-push, there is **no
> gated destructive session** — a deliberate simplification versus `003`/`004`.

---

## Why This Set, Why Now

After `004`, the densest proprietary disclosure (full VBA source bodies + AI
interpretations, serialized into SQLite + golden artifacts) is gone from all
history. What remains, per `003`'s findings, is **structural disclosure** — the
proprietary DB's object *names* and, in some docs/tests, paraphrased business
logic — embedded in keep-files. The repo is **private**, so nothing is publicly
exposed; this set executes the maintainer's standing internal policy that the
repo should be NorthwindDevEdition-free. `003` recorded that "the remediation is
not complete while residue remains," and the routed `004` next-set analysis framed
this follow-up as **compliance, not feature work**, so the repo's
"consumer-driven-only" posture does not gate it.

### The harvester engine is already generic — the residue is in the harness, not the core

A scan of the production (non-test) C# confirmed the extractors, VBA parser,
resolver, and enrichers are generic to any Access database; the only non-generic
production code is one CLI convenience helper
(`AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs` `RunForNorthwind`) hardcoded to
the dev corpus, plus two comment-only references. The bulk of the residue is:

1. **Spent process artifacts** — probe outputs, translation packets, peer reviews,
   session-reviews, driver `.py` scripts, superseded specs/change-logs. These have
   no ongoing value and are **deleted**.
2. **Workflow packages** — `add-an-order.md`, `update-an-order.md`,
   `update-a-product.md`: proprietary business logic operationalized. **Deleted.**
3. **Live tests pinned to the Northwind corpus** (~28 files) — characterization,
   translation-probe, ReportBaseline, SymbolDb, Vba.Resolver. **Per-file triage:**
   re-base onto the synthetic HomeHealthCare fixture where the coverage still
   matters; delete the spent/duplicative ones. Never weaken a live assertion.
4. **C# source + reference docs** — genericize `RunForNorthwind`; redact the
   comment/string references in source and the comparison/migration/eval docs.

### Disposition is a reviewed manifest, never a mechanical name match

As in `003`/`004`, the per-file disposition (delete / re-base / redact / keep) is
authored and reviewed in Session 1, grounded in the `003` 116-path inventory and a
fresh `HEAD` scan — not a blind `grep` sweep. Files that name only HomeHealthCare
or generic terms are out of scope.

## Done Looks Like

- A reviewed **disposition manifest** (paths + per-file action + rationale only, no
  proprietary content) covering every residue path from the refreshed inventory.
- The spent process artifacts and workflow packages are deleted at `HEAD`.
- Every corpus-pinned test is either re-based onto the HomeHealthCare fixture (with
  its live assertions preserved) or deleted with a recorded reason; the build is
  green and `dotnet test` passes both with the local proprietary fixture present
  and with it absent (the `004` skip-guards still fire cleanly).
- `RunForNorthwind` is genericized/parameterized (or removed if it is a dev-only
  convenience with no caller); the comment/string references in source and the
  reference docs are redacted to generic or HomeHealthCare equivalents.
- A final `HEAD` residue scan (the `003` identifier set, refreshed) returns **no
  tracked file** carrying a NorthwindDevEdition structural identifier, except
  paths explicitly accepted-and-recorded (e.g. the local-only gitignored fixture
  tree, and process-record files for sets `003`/`004`/`005` themselves that must
  describe what was removed).
- No git-history rewrite and no force-push were performed; pre-rewrite history
  retaining old structural names is recorded as an accepted residue.

## In Scope

1. Refreshing the `003` 116-path inventory against the current `HEAD` and authoring
   the reviewed per-file disposition manifest.
2. Deleting spent process artifacts + workflow packages at `HEAD`.
3. Per-file triage of the corpus-pinned tests: re-base onto HomeHealthCare or
   delete; never weaken a still-valid assertion.
4. Genericizing/redacting the C# source (`RunForNorthwind` + comments) and the
   non-session-set reference docs.
5. Gitignore/policy doc confirming the local-only fixture stays untracked, and a
   final tracked-tree residue scan proving completeness.

## Out Of Scope

- **Any git-history rewrite or force-push.** This set is `HEAD`-only by maintainer
  decision; old structural names in pre-rewrite history are accepted residue.
- HomeHealthCare (synthetic) and any non-Northwind fixture/data.
- The harvester/symbol-DB *engine* behavior (already generic; not changed). A
  test re-base must preserve the behavior under test, not alter the code under
  test.
- The local-only proprietary fixture tree under
  `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/` (already gitignored,
  never tracked — nothing to remove).

---

## Sessions

### Session 1 of 3: Refresh Inventory + Disposition Manifest + Bulk-Delete Spent Artifacts (non-destructive)

**Steps:**

1. Re-run the `003` residue scan against the current `HEAD` (the Northwind-specific
   identifier set from `embedded-residue-findings.md` → "How the scan was run"),
   producing the refreshed tracked-file inventory (the 7 `004`-purged paths should
   already be gone).
2. Author `disposition-manifest.md` + `disposition-manifest.csv` (path, bucket,
   action ∈ {delete, re-base, redact, genericize, keep-accepted}, one-line
   rationale; **paths only, no proprietary content**). Classify every inventory
   path. Cross-check against `003`'s HIGH/MEDIUM/LOW buckets.
3. Execute the **delete** actions for spent process artifacts + workflow packages
   (probe/translation/peer-review/session-review docs, driver `.py` scripts,
   superseded specs/change-logs, `workflow-package-pilot/packages/*`). `git rm` at
   `HEAD`; keep nothing locally that is purely a spent process record.
4. `dotnet build` then `dotnet test` (affected projects) — deletions are docs/
   scripts and must not change build/test status. Confirm green.
5. Cross-provider verification (inventory completeness, disposition-manifest
   correctness, delete-vs-keep boundary discipline, no live-code deletion).
6. Closeout.

**Creates:** `disposition-manifest.md`, `disposition-manifest.csv`.

**Touches:** the spent process-artifact + workflow-package paths (deleted).

**Ends with:** a reviewed disposition manifest covering the full footprint, and the
spent-artifact/workflow-package deletes landed with build + tests green.

**Progress keys:** `session-1/refresh-inventory`, `session-1/disposition-manifest`,
`session-1/delete-spent`, `session-1/build-test`, `session-1/verify`,
`session-1/closeout`.

---

### Session 2 of 3: Test Triage — Re-base or Delete the Corpus-Pinned Tests (non-destructive)

**Steps:**

1. For each test path the manifest marked `re-base` or (test) `delete`, inspect what
   it asserts and which code path it exercises. Decide re-base-onto-HomeHealthCare
   vs delete, recording the per-file reason in `test-triage.md`.
2. **Re-base** the keepers onto the synthetic HomeHealthCare fixture: port fixtures
   and assertions to HHC objects, preserving the behavior under test. Do **not**
   weaken or stub an assertion to make it pass — if HHC cannot exercise the path,
   record it and either keep the test as a `[SkippableFact]` on the local-only
   Northwind fixture (mirroring the `004` skip pattern) or delete it with reason.
3. **Delete** the spent/duplicative tests outright (coverage already lives in HHC
   tests), recording the reason.
4. `dotnet build` then `dotnet test` (sequentially) **twice**: with the local
   proprietary fixture present (any retained `[SkippableFact]` assertions run) and
   with it moved aside (those skip cleanly; HHC-based tests run regardless). Build +
   tests green both ways. Restore afterward.
5. Cross-provider verification (no weakened assertions, re-base fidelity, coverage
   not silently dropped, skip-guard correctness).
6. Closeout.

**Creates:** `test-triage.md`.

**Touches:** the ~28 corpus-pinned test files (re-based or deleted); possibly HHC
fixture helpers if a re-base needs a new fixture accessor.

**Ends with:** no live test names a proprietary identifier (except retained
`[SkippableFact]`s on the local-only fixture, recorded); build + tests green with
and without the local fixture.

**Progress keys:** `session-2/triage-plan`, `session-2/rebase`, `session-2/delete`,
`session-2/build-test`, `session-2/verify`, `session-2/closeout`.

---

### Session 3 of 3: Genericize C# Source + Redact Docs + Final Residue Scan + Closeout (non-destructive)

**Steps:**

1. Genericize `AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs` `RunForNorthwind`:
   parameterize it over an arbitrary fixture root + object set, or remove it if it
   has no non-test caller (confirm callers first). Preserve any generic build path.
2. Redact the comment/string references naming proprietary objects in
   `LocalVariableExtractor.cs`, `ReferenceResolver.cs`, `MeasurementReportBuilder.cs`,
   `DatabaseInfo.cs`, `TypeLibraryRegistry.cs`, `NamePatternDetector.cs` (replace
   with generic or HomeHealthCare examples; keep the explanatory intent).
3. Redact the non-session-set reference docs (`homehealthcare-northwind-comparison.md`,
   `cross-repo/access-to-platform-migration-map.md`, `lowdefy-evaluation/prompt-for-codex.md`,
   `generalization-validation-on-non-northwind/inventory.md`) to remove proprietary
   structural names while preserving the document's purpose.
4. Confirm the local-only fixture tree stays gitignored; add/update a short policy
   note (`docs/planning/project-guidance.md` or the symbol-db README) recording that
   the repo is NorthwindDevEdition-free at `HEAD` and the fixture is local-only.
5. Run the **final `HEAD` residue scan** (refreshed `003` identifier set). It must
   return only accepted-and-recorded paths (local-only fixture references,
   `003`/`004`/`005` process records describing the removal). Record the result in
   `final-residue-scan.md`.
6. `dotnet build` then `dotnet test` — green.
7. Cross-provider verification (genericization correctness, no behavior change, redaction
   completeness, final-scan rigor, scope discipline).
8. Closeout: `change-log.md`, `next-session-set-recommendation.md`, state flip to
   complete, notify, Step 9 reorganization review.

**Creates:** `final-residue-scan.md`, `change-log.md`,
`next-session-set-recommendation.md`.

**Touches:** the C# source files above; the reference docs above; a policy doc;
`.gitignore`/README if needed.

**Ends with:** the tracked tree carries no NorthwindDevEdition structural identifier
except accepted-and-recorded paths; build + tests green; the repo is
NorthwindDevEdition-free at `HEAD`.

**Progress keys:** `session-3/genericize-source`, `session-3/redact-source`,
`session-3/redact-docs`, `session-3/gitignore-policy`, `session-3/final-scan`,
`session-3/build-test`, `session-3/verify`, `session-3/closeout`.

---

## Stop Rules

- **No git-history rewrite and no force-push in this set.** It is `HEAD`-only by
  maintainer decision. If a residue item is judged to genuinely require history
  purging (e.g. a high-sensitivity disclosure missed by `003`/`004`), STOP and
  surface it to the maintainer as a finding for a separate gated set — do not
  rewrite history here.
- **Do not weaken a test assertion to make it pass after re-basing.** Re-base onto
  HomeHealthCare preserving the behavior under test, keep it as a `[SkippableFact]`
  on the local-only fixture, or delete it with a recorded reason. Never stub an
  assertion to green.
- **Do not change harvester or symbol-DB engine behavior.** This set genericizes a
  CLI helper and the test harness and redacts names; the code under test must keep
  behaving as before.
- **Do not delete a file that has ongoing value** to satisfy the residue scan.
  Spent process artifacts are deleted; live tooling, generic tests, and current
  docs are redacted/re-based, not deleted. If a file is ambiguous, mark it
  `keep-accepted` in the manifest with a rationale rather than deleting it.
- **Do not touch the local-only fixture tree** under
  `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/` — it is gitignored and
  never tracked; the developer keeps it for running `[SkippableFact]` tests locally.
- If proprietary content denser than structural names (full source bodies,
  interpretations) is discovered in a tracked file, **surface it to the maintainer**
  — that may warrant a gated history-rewrite set rather than a `HEAD`-only redaction.
