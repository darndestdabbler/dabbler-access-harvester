# Change Log - 006 Solution Review And Upstream Feedback Discovery

> Set `006-solution-review-and-upstream-feedback-discovery` (4 sessions, Full tier, READ-ONLY discovery; completed 2026-06-12; orchestrator Claude Fable 5 per maintainer direction). Conducted a comprehensive review of the AccessHarvester solution, identified critical issues, and gathered upstream feedback, culminating in a prioritized set of follow-up recommendations.

## Purpose

To conduct a detailed, read-only discovery and review of the `dabbler-access-harvester` repository's `AccessHarvester` solution, identify critical defects, gather upstream feedback from a fresh `HomeHealthCare` COM run, and propose prioritized follow-up work.

## What Changed (docs only)

Zero shipping code, tests, or golden fixtures were changed. All changes are documentation artifacts generated during the review process.

*   **S1 artifacts:** `s1-review-findings.md`, `s1-verification.md`, `s1-verification-round-2.md`, `s1-issues.json`
*   **S2 artifacts:** `s2-review-findings.md`, `s2-verification.md`, `s2-verification-round-2.md`, `s2-issues.json`
*   **S3 artifacts:** `s3-review-findings.md`, `s3-verification.md`, `s3-verification-round-2.md`, `s3-issues.json`
*   **S4 artifacts:** `review-recommendations.md`, `s4-verification.md`, `s4-verification-round-2.md`, `s4-issues.json`, `change-log.md` (this file), `next-session-set-recommendation.md`, `disposition.json`; updated `docs/planning/harvester-upstream-feedback.md`.

## Review Findings Summary

*   **1 Critical:** F-3-1 (manifest pipeline silently drops all form/report code-behind by parsing raw SaveAsText dumps whole-file as VBA).
*   **9 Major (after S4's F-1-2 escalation):** F-1-1, F-1-2, F-1-3, F-2-1, F-2-2, F-3-2, F-3-3, F-3-4, F-3-5.
*   **14 Minor:** F-1-4 through F-1-8 (5), F-2-3 through F-2-7 (5), F-3-6 through F-3-9 (4).
*   **17 recorded observations:** O-1-1..O-1-8, O-2-1..O-2-8, F-3-10 (collected).

Headline pattern: High component quality, but silent failures at delivery seams (unwired substrates, incorrect ingestion, contract/decode drift).

## Round 2 Feedback Outcomes

*   **R2-1 CONFIRMED:** Switchboard navigation data on `tblSwitchboardItems` is fired but rows are unexported; no navigation graph found. This leads to the P4 proposal.
*   **R2-2 CONFIRMED:** Individual chain hops (embedded-macro `OpenReport`, report-`Open` dialog reads, `RecordSources`, declared-parameter tokens, `TempVars` flows) are observable, but no connecting edges are present. This leads to the P4 proposal.
*   **R2-3 ASSESSED:** Two-root split and casing drift reproduced. Recommendation is for downstream orchestrator-side intake per both parties' defaults, with no upstream export command needed.

## Proposed Follow-Up Sets

*   **P0 integration-test-rebase:** (1-2 sessions) Re-base 7 red tests onto committed `HomeHealthCare` fixture. Prereq for all.
*   **P1a manifest-vba-ingestion-and-parser-hardening:** (3 sessions) Addresses F-3-1 Critical + F-3-2/F-3-3/F-3-6. Prereq P0.
*   **P1b output-encoding-correctness:** (1 session) Addresses F-1-1 + hermetic translator pins. Prereq P0. Parallel-safe with P1a.
*   **P2 output-contract-fidelity-batch:** (2 sessions) Addresses F-2-1, F-3-5, F-2-3 BOM, F-2-6, ride-alongs. Prereq P0.
*   **P3 resolver-and-navigation-wiring:** (3 sessions) Addresses F-3-4 + F-2-2. Prereq P1a, P2 recommended.
*   **P4 switchboard-and-report-chain-edges:** (3 sessions) Implements R2-1/R2-2. Prereq P3 hard, P2 hard.
*   **P5 cli-input-robustness:** (2 sessions) Addresses F-1-3, F-1-2, F-1-5, F-1-6, `ParseOptions` consolidation. Prereq P0 only.
*   **P6 cleanup-zero-date-guard:** (1 session) Addresses known misfire + O-1-5 + O-2-6. Prereq P0 only.

## Verification

All sessions completed verification, addressing minor evidence-discipline issues in Round 1 and achieving Round 2 VERIFIED status.

*   **S1:** R2-VERIFIED
*   **S2:** R2-VERIFIED
*   **S3:** R2-VERIFIED
*   **S4:** R2-VERIFIED