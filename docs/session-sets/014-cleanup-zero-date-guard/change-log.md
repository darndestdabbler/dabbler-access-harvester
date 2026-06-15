# Change Log - Set 014: `014-cleanup-zero-date-guard`

This set addressed the P6 lane of the Set 006 solution review, which centered on the `identify-cleanup` zero-date misfire. This misfire occurred when a time-only Access Date/Time column's maximum value presented as the OLE zero-date (1899-12-30), leading to a spurious ~46176-day-stale finding. Adjacent findings O-1-5 (sampler timezone mislabel + silent per-column swallow) and O-2-6 (analyser lacked a zero-date floor) were also resolved.

## Shipped Changes:

*   **O-2-6 Analyser Zero-Date Guard:** The `CleanupAnalyser` now suppresses any sampled maximum whose *date component* is *exactly* 1899-12-30, classifying it as "no date signal". This was narrowed from an initial `<=` floor to `==` after a cross-provider review revealed that `<=` would wrongly conceal genuine pre-1900 stale dates.
*   **O-1-5 Sampler Timezone:** `AccessDateMaximaSampler` now preserves the Access local wall-clock value as `Unspecified` (previously mislabelled `UTC` via `SpecifyKind`). This change is behavior-preserving as the pipeline anchors both sides at offset 0.
*   **O-1-5 Per-Column Swallow Visibility:** Per-column read failures are now recorded as visible degradations via a new `ISamplerDegradationSource`. These degradations are threaded through `ComProbeResult.SamplerDegradations` and reported in the `object-cleanup.json` notes. The CLI fold for these notes is now extracted to the hermetically-pinned `CleanupDegradationNotes.Fold` helper.
*   **Decision on Time-Only Column Skip:** The analyser guard is the shipped fix. The sampler-side time-only column skip (the root cause, due to Access having a single Date/Time type and lacking a time-only type signal at the IR/sampler) is deferred as DW-16. DW-16 also encompasses two accepted and documented analyser false-negatives: a real maximum dated exactly 1899-12-30, and a real pre-epoch date masked by a sibling time-only column.

## Verification:

Verification included three adversarial rounds with gpt-5-4, which successfully identified a Major issue with the initial `<=` over-suppression and a Minor issue with the unpinned fold helper. Additionally, 62 cleanup tests and 907 `Pipeline.Tests` passed cleanly. No golden regeneration was required, as byte-level integrity was proven against the `--no-com` path.

## Milestone:

With the completion of Set 014, the entire Set 006 solution-review arc (P0 through P6, spanning sets 007-014) is now COMPLETE. The repository will now return to a consumer-driven-only development cycle. The next routed recommendation is `015-unresolved-items-completeness` (DW-11) for proactive hardening, with `STOP/hold` as a runner-up; the final decision rests with human discretion.
