
---

# Verification Round 3

This document details the verification of Session 1, Round 3 of the `harvester-object-use-identifier` design. The analysis focuses on the correctness of the updated query classification rules and the overall integrity of the specification.

### Union Query Classification (Q1)

The primary change in this round was the re-evaluation of Union queries (`QueryTypeCode` 64). Previously, they had a distinct, and likely incomplete, classification path. The new Rule Q10, in conjunction with the note clarifying that rules Q2-Q7 apply to `QueryTypeCode` in `{0, 64}`, is a correct and robust fix.

This change correctly identifies that both Select and Union queries are fundamentally data-retrieval mechanisms. By routing them through the same logic, the system leverages existing, well-defined rules based on how the query is used, rather than its internal structure. This ensures consistent classification for the most common scenarios:
-   **RecordSource for a Form:** A Union query used as a form's `RecordSource` will now correctly trigger Rule Q2, classifying it as `MainDataEntry`. This is appropriate, as such a form is used to present or interact with transactional-style data, even if it's aggregated from multiple sources.
-   **RecordSource for a Report:** A Union query used as a report's `RecordSource` will correctly trigger Rule Q3, classifying it as `Reporting`. This is the canonical use case for complex retrieval queries.
-   **Unreferenced:** An unreferenced Union query will fall through to Rule Q7, classifying it as `PossiblyUnused`. This is consistent with how unreferenced Select queries are handled.

The introduction of Rule Q11 to handle DDL (`TypeCode` 6) and Pass-through (`TypeCode` 128) queries as `StandaloneUtility` is also a sound addition. It properly categorizes these as infrastructure or utility objects, preventing them from being misclassified as `Reporting` or `Unknown`. The logic is precise and covers the full spectrum of query types correctly.

### Overall Specification Integrity (Q2)

A comprehensive review of the entire specification reveals a mature, coherent, and well-designed system. No significant issues remain.

-   **Taxonomy and Rules:** The seven classification categories are mutually exclusive and sufficiently comprehensive for typical Access applications. The rule sets for each object type (Forms, Reports, Queries, Tables) are logical, prioritized correctly, and handle primary use cases as well as edge cases (e.g., action queries vs. select queries). The explicit notes on rule priority are clear and aid implementation.
-   **Signal Collection:** The six signal collectors are well-defined, sourcing their information from appropriate IR files. The extraction logic is specific enough (e.g., ignoring value lists in `RowSource`) to be effective. The set of collectors provides a holistic view of object interactions within the database.
-   **Outputs and Schema:** Both the intermediate `object-use.json` and the final `object-use-checklist.json` are fit for purpose. The mapping from the analytical finding to the user-facing checklist item is direct and sensible. The JSON schema is detailed, correct, and aligns perfectly with the structures described in the strategy document. The decision to include all objects in the checklist, even those without questions, is a good design choice that forces explicit review and confirmation.
-   **Degradation and Prerequisites:** The handling of soft prerequisites is a sign of robust design. The specification clearly outlines how the tool should behave gracefully when optional inputs, such as the cleanup checklist or query KB files, are absent. This prevents brittle failures and ensures the tool remains useful even with incomplete harvested data.

The design is complete, internally consistent, and ready for implementation.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
