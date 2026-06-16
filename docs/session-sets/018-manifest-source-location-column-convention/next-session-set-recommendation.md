# Next-Session-Set Recommendation (post-018)

**Recommendation: STOP — recorded consumer-driven-only handoff. No concrete next set authored.**

Set 018 closed DW-26 (the last self-identified correctness item that was pulled in at
human direction). The harvester arc remains **consumer-driven-only**
(`project_consumer_driven_only_after_two_sets`, reached 2026-06-01): no self-initiated
work. The standing position is to wait for a consumer/maintainer driver, then pull in the
matching deferred-work item. This file catalogs the landscape so the next driver has a
ranked starting point.

## Hard Constraints any future set MUST carry (verbatim)
- **NEVER commit/regenerate any StudentICTDatabase-derived artifact** (FE/BE `.accdb`,
  `.lnk`, or any harvested output). It is proprietary, local-only, gitignored; its linked
  `Connect` strings embed a **plaintext password + absolute BE path** (DW-24). Validate
  against it **locally** only; never reproduce the literal credential/path in any committed
  artifact — placeholders only. See `feedback_redact_corpus_credentials_from_all_committed_artifacts`.
- **Golden regeneration is HomeHealthCare-only** (`GoldenCorpusRegenerator`,
  `AH_REGEN_GOLDEN=1`). Use NorthwindDevEdition **locally** (gitignored) to validate
  output-affecting work; never commit a Northwind golden. Prove every golden delta at the
  byte/codepoint level.
- **COM runs are deliberate, sequential, foreground**, TRX `<Counters>` as the completion
  signal; never background a `dotnet` invocation. **Do not run the whole solution's COM
  tests in one `dotnet test`** — run the integration project separately and targeted (a
  whole-solution run wedged a testhost for ~3h in Set 018; see Step 9 lesson).
- The quarantined sanitize test stays quarantined.

## Open deferred-work landscape (ranked pull-in candidates)
1. **DW-18 — intrinsic-constant resolver registry** (golden-affecting; huge StudentICT
   witness: 3000+ `vbCrLf`/`vbWhite`/… false-positive `not-in-scope` unresolved refs; HHC
   carries `vbObjectError`). Highest-value correctness item; plan an HHC regen. Natural
   bundle with the parser/resolver-completeness items DW-1/DW-2/DW-4.
2. **DW-27 — `unresolved-items.json` columns are uniformly 0-based while the manifest is
   now 1-based** (NEW this set). Normalising the index to 1-based is a uniform `+1` but
   golden-affecting (unresolved-items.json + the classification/navigation `Column`/
   `SourceColumn` fields). Natural bundle with **DW-17** (per-module `ParserOutput`
   completeness asymmetry) and **DW-19** (SQL-feed scalar attribution) as a
   `resolver-provenance / unresolved-items` set.
3. **DW-25 — `IsLinked` / raw `Attributes` absent from run-pipeline manifest table
   records** (consumers must detect linked tables via `Connect` + `SourceTableName`).
4. **Ribbon completeness — DW-20** (dynamic `get*` callbacks → entry points), **DW-21**
   (non-`<tabs>` customUI sections in the structured view). (DW-22/23 if scoped.)
5. **Parser/grammar completeness — DW-1** (contextual-keyword slots), **DW-2**
   (`Line`/`Circle` graphics syntax), **DW-4** (`!`-suffixed function name BANG ambiguity),
   **DW-3** (suffix→type for variable/const/param).
6. **DW-8 + DW-19 — dynamic-SQL / SQL-form-reference wiring** into `ParserOutput`
   (DW-19's scalar attribution must become per-item when DW-8 lands).
7. **DW-14 — navigation-target completeness** (named-argument opens, indexer/`With`
   control refs, non-bang TempVars setters).
8. **DW-5/DW-6 — decode centralization / `RunParse` BOM unification** (latent).
9. **DW-7 — crosstab TRANSFORM embedded-SELECT abstraction.**
10. **DW-12 — latent resolve-failure → `null`-`ParserOutput` deref hazard** (not currently
    triggerable; documentation guard stands).

**Golden-affecting ones-to-watch:** DW-18 and DW-27 both require a coordinated HHC regen.

## If a driver appears
Verify the consumer-reported issue against a **fresh tool run** first
(`feedback_ground_consumer_reports_against_fresh_output`) before authoring; shipped fixes
are often masked by stale goldens/docs/releases. Then author the matching set carrying the
Hard Constraints above.
