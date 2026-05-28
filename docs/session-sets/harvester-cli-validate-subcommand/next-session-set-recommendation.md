# Next Session Set Recommendation

**From:** `harvester-cli-validate-subcommand` Session 1 (complete)
**Date:** 2026-05-28
**Recommended next set:** `harvester-cli-distribution`

## Rationale

The harvester CLI now has a complete, self-contained surface:
`extract`, `translate`, `run`, `generate-docs`, `validate`, and `parse` — all
in a single self-contained Windows binary. The `validate` subcommand
closes the last consumer-facing gap: downstream pipelines can now
confirm their IR JSON is schema-conformant without wiring up their own
validator or locating a schema file.

The next natural step is making the binary **reliably findable and
versioned** for consumer pipelines. Today the only distribution
mechanism is "ask the maintainer" or "clone and publish locally."
`Dabbler.NorthwindToBlazor` Set 001 Session 2 already has a
"smoke-validate the IR JSON" step; it cannot run the `validate`
subcommand without a reliable artifact location.

## Suggested scope for `harvester-cli-distribution`

1. Define a versioned release convention (GitHub Releases tag + artifact
   name encoding both harvester semver and schema version).
2. Document the download URL pattern in `install.md` so consumer
   orchestrators can resolve the latest artifact programmatically.
3. Optionally: add a minimal CI step (or manual release checklist) that
   publishes the artifact to GitHub Releases on a version tag.
4. Optionally: add a `--check-update` flag that prints the current
   version alongside the latest GitHub Release tag (consumer-friendly
   staleness check).

## Alternative: skip distribution, proceed to consumer integration

If `Dabbler.NorthwindToBlazor` Set 001 Session 2 is the higher
priority, the distribution problem can be deferred: the consumer can
use the local clone path until a release mechanism is formalized. In
that case, the recommended next set for this repo is whatever is
highest priority in the backlog.
