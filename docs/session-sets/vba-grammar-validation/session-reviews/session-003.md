# Verification Round 1

## ISSUES_FOUND

- **Major**  
  **Issue →** The schema-validation cross-cut overstates verification status. The log says `VERIFIED after 2 rounds`, but the session summary states the route returned `ISSUES_FOUND`, the issues were fixed mechanically, and the log was **not re-verified separately**. No second verification artifact is evidenced here.  
  **Location →** `docs/spec-coverage/parse-failure-log.md`, section `Schema-validation correspondence (in-session, Session 3 of #5)`  
  **Fix →** Either rerun schema-validation and record the actual second-pass artifact/verdict, or change the section to say the first pass found issues, they were corrected mechanically, and no independent re-verification was performed.

- **Major**  
  **Issue →** The final `15/15 parsed` claim is not fully substantiated because the document itself says full-input consumption is not guaranteed unless `module` ends with `EOF` or the harness asserts EOF. That means the harness may be proving “no thrown parse error” rather than “entire fixture consumed by the grammar.”  
  **Location →** `docs/spec-coverage/parse-failure-log.md`, section `Final harness run results (after fix)` together with `Open items for #6`, item 2  
  **Fix →** Enforce full-input consumption now: make `module` end with `EOF` or assert EOF in the harness, rerun the 15-fixture corpus, and update the logged result to reflect a full-coverage parse.

- **Minor**  
  **Issue →** The #4 entry is structurally present, but its “Grammar bugs discovered and fixed” table is aggregated into a single catch-all row (`Various statement/module constructs`). That does not meet the stated “every failure encountered, classification, and resolution” bar at the same granularity used for GF-001.  
  **Location →** `docs/spec-coverage/parse-failure-log.md`, section `Grammar implementation: vba-statement-and-module-grammar (#4)`, subsection `Grammar bugs discovered and fixed`  
  **Fix →** Either enumerate the distinct authoring-time failures that were fixed during #4, or explicitly relabel the section as a milestone summary rather than an exhaustive failure log.

---

# Verification Round 2

**ISSUES_FOUND**

- **Minor Issue** → `Harness run: Session 2 of vba-grammar-validation (#5)` → `Open items for #6`, item 2  
  **Fix:** Remove or rewrite the note _“The `module` rule should end with `EOF`...”_. The grammar already has `module : NEWLINE* moduleItem* EOF ;`, so full-input consumption for the `module` entry point is already enforced. Round-1 Major Issue 2 is resolved; this leftover note is stale.

- **Minor Issue** → `Harness run: Session 3 of vba-lexical-and-expression-grammar` → `Corpus` paragraph  
  **Fix:** Change _“All 12 fixtures under `docs/test-cases/` plus the 5 target fixtures...”_ to _“All 12 fixtures under `docs/test-cases/`, including the 5 target fixtures...”_. Current wording implies double-counting beyond the 12 listed results.

Round-1 findings are otherwise addressed. The `module : NEWLINE* moduleItem* EOF ;` rule fully resolves the full-input-consumption concern for parses started at `module`.

---

# Verification Round 3

ISSUES_FOUND
