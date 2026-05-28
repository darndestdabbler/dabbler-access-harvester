# Next Session Set Recommendation

## Recommendation: harvester-data-classifier

**Slug:** `harvester-data-classifier`

**Status check:** Spec authored 2026-05-28; `session-state.json` shows
`status: "not-started"`, three sessions planned. Ready to start.

**Rationale:**

`harvester-data-classifier` is the third arm of the three-set
analytical pipeline (cleanup → usage → data-classification) and is
the natural next step now that both cleanup and usage identifiers
have committed golden fixtures.

The data-classifier spec lists `harvester-object-cleanup-identifier`
as a soft prereq: the `classify` subcommand can consume the
committed `object-cleanup-checklist.json` files to skip tables
flagged as confirmed stale (`Passes: true`). That prereq is now
satisfied — the golden cleanup checklists for HomeHealthCare and
NorthwindDevEdition are committed under
`samples/GoldenOutput/<db>/cleanup/`.

The set has three planned sessions:

1. **JSON Contract** — design the `classification.json` schema
   that is the keystone contract for both the editor and the
   sanitizer.
2. **Classify subcommand + standalone HTML editor** — emit
   best-guess column classifications; the editor lets a human
   reviewer confirm or override.
3. **Sanitize subcommand** — reads the reviewed JSON plus the
   source `.accdb` and emits sanitized test data with sensitive
   values replaced by mock data.

**Sequencing:** Start `harvester-data-classifier` Session 1
directly. No intermediate work is required.

**Alternative:** If the data-classifier work is deferred for
business reasons, the next natural fit is
`harvester-cli-distribution` (spec exists; bundling +
self-contained CLI for downstream consumers).
