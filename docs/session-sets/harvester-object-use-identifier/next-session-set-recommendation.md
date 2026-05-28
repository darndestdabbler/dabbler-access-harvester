# Next Session Set Recommendation

## Recommendation: harvester-object-cleanup-identifier

**Slug:** `harvester-object-cleanup-identifier`

**Rationale:**
`harvester-object-use-identifier` soft-depends on `harvester-object-cleanup-identifier`.
The cleanup identifier produces an `object-cleanup-checklist.json` that lists objects
confirmed stale (Passes=true). The `identify-usage --exclude-cleanup <path>` flag reads
that file and skips stale objects, giving a usage analysis that only covers current objects.

Currently both HomeHealthCare and NorthwindDevEdition golden fixture runs were produced
without an exclusion list (`ExcludedCount: 0` in both). Completing the cleanup identifier
and running the two-pass analysis would improve precision of the usage classification output.

**Sequencing:** `harvester-object-cleanup-identifier` → re-run `identify-usage --exclude-cleanup`
→ commit updated golden fixtures.

**Alternative:** If the cleanup identifier is already in-flight or its output is not needed
immediately, the next step could be a documentation-generation session set that reads the
reviewed `object-use-checklist.json` and produces structured per-object summaries for the
downstream migration repo.
