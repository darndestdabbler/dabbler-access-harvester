# Next Session Set Recommendation (post `002`)

## Headline

The mechanical harvester development arc is complete. With v1.2.0 released, the repository is now in a consumer-driven-only posture. No new feature work will be self-initiated.

## Recommended Next Set

- **Set:** `003-northwind-proprietary-data-remediation` (3 sessions)
- **Justification:** This set removes proprietary derived-metadata files currently committed to the public repository. The longer this data remains public, the more widely it propagates through clones.
- **CAVEAT: DO NOT AUTO-RUN.** Session 3 performs a destructive `git` history rewrite (`force-push`). It requires explicit maintainer authorization and coordination.

## Deferred / Opportunistic

- **Issue:** The COM cleanup run misclassifies time-only `DateTime` columns, reading them as the OLE zero-date (1899-12-30).
- **Action:** This is a candidate for a small fix-set if a consumer reports an impact.

## If No Consumer Signal

The repository can remain idle until a new consumer issue is reported or the maintainer explicitly authorizes and schedules set `003`.