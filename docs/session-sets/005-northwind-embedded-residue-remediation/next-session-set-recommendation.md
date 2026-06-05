# Next Session Set Recommendation

**Document Date:** 2026-06-05
**Status:** Recommended
**Author:** System

### 1. Executive Summary

A follow-up session set is warranted immediately. The `AccessHarvester.Integration` test suite is currently broken due to a dependency on an uncommitted, Northwind-derived fixture (`examples/northwind-output.json`). This constitutes a critical repository health issue. The recommended next set, `006-integration-test-rebase`, is a small **compliance** action to restore the test suite to a green state using committed, non-proprietary test fixtures.

### 2. Analysis

Session Set `005-northwind-embedded-residue-remediation` successfully removed all Northwind-derived artifacts from the repository at `HEAD`. However, it surfaced a key finding:

*   **Broken Integration Suite:** 7 integration tests fail deterministically on any clean checkout. They depend on `examples/northwind-output.json`, a local-only file derived from the now-excised Northwind source.
*   **Compliance Gap:** Regenerating this file would reintroduce the residue that Set 005 just removed. The current state prevents any developer from validating changes against the integration test suite, blocking all future work.
*   **Posture Exception:** While the repository is in a "consumer-driven-only" posture, fixing a broken test suite is core maintenance and falls under the exception for compliance and residue remediation work.

Deferring this fix is unacceptable as it leaves the primary validation mechanism for the repository non-functional.

### 3. Recommended Next Session Set

*   **Identifier:** `006-integration-test-rebase`
*   **Classification:** **Compliance Work**. This is not feature development; it is essential maintenance to restore repository integrity.
*   **Estimated Size:** **Small** (1-2 sessions).
*   **Scope:**
    1.  Identify the 7 failing tests within the `AccessHarvester.Integration` test suite.
    2.  Re-base these tests to use a committed, compliant `HomeHealthCare`-derived manifest as the test fixture, following the pattern established in Session Set 002.
    3.  Update all associated test assertions to align with the new HHC-based fixture data.
    4.  Investigate and resolve the environmental timeout observed in the COM `sanitize` test.
*   **Goal:** The `AccessHarvester.Integration` test suite passes reliably on a clean checkout, with all test fixtures being committed, compliant, and free of Northwind residue.

### 4. Recommended Orchestrator

*   **Engine/Model:** A cost-effective, high-speed model (e.g., Claude 3 Haiku, GPT-3.5 Turbo).
*   **Rationale:** The task is a mechanical re-basing of test code and fixtures. It is well-defined, repetitive, and does not require the advanced reasoning or higher cost of a frontier model like GPT-4. The priority is efficient execution for a simple code modification task.
