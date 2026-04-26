# Verification Round 1

ISSUES_FOUND

1. **F1 — Major**
   - **Issue:** Known fixture gaps are identified in the coverage table but not assigned as concrete “write the test case first” work to any child set.
   - **Location:** `Test-case coverage table`; child rows `#1 parser-foundations`, `#5 vba-grammar-validation`, `#6 parser-infrastructure`; `Net additional fixtures required across the tree`.
   - **Evidence:** The table says:
     - **Phase 1.5-1.6:** “the plan calls for ≥95% parse success on a representative corpus; module-level cases are not a corpus”
     - **Phase 2.1-2.5:** “preprocessor directives lack a dedicated case”
     - OQ1 then says corpus authorship might move into `parser-foundations`, but no child row actually owns **Phase 0.1** or schedules that fixture work.
     - Row `#6` does not include a preprocessor-fixture session.
     - The summary then says the **only** additional fixture work is one DB-level corpus session in `#10`, which contradicts the two earlier gaps.
   - **Fix:** Assign the uncovered fixture work explicitly:
     - add a concrete owner for **Phase 0.1 / representative corpus**,
     - add a concrete “author preprocessor-directive fixture first” session to `#6` (or a dedicated child),
     - update session counts and the “Net additional fixtures required” summary to match.

2. **F2 — Major**
   - **Issue:** The coverage table overclaims that test case `08` covers Phase `5.4 dynamic SQL analysis`.
   - **Location:** `Test-case coverage table` → row `5.4 dynamic SQL analysis`.
   - **Evidence:** The tree maps `5.4` to `05` and `08` (“`Eval` of stored expressions”). The authoritative README describes **08** as “Constructs that defeat static analysis” using `Eval()`, dynamic form names, and `Application.Run`; it does **not** describe SQL or dynamic SQL construction. Per the README, `08` clearly supports graceful-failure/manual-review behavior, not SQL analysis.
   - **Fix:** Remove `08` from the `5.4` row unless the plan explicitly defines `5.4` to include non-SQL runtime-expression failure handling; otherwise add/schedule a real SQL-focused unresolved/dynamic case.

3. **F3 — Major**
   - **Issue:** The document does not fully apply adopt-now `#2` (“single source of truth”); canonical edges and output paths are not stable/verbatim across sections.
   - **Location:** `Adopt-now rows applied`; child rows `#1`, `#3`, `#4`, `#12`; `Dependency DAG`.
   - **Evidence:**
     - Row `#12` prerequisites are `#6, #11`, but the ASCII DAG only draws `#11 → #12`; it mentions the `#6` edge in prose (“edge below”) without actually drawing it.
     - Row `#1` output path is conditional: `AccessHarvester.Core.csproj` **or** `AccessHarvester.Vba.Grammar.csproj` “per OQ 2 resolution”, which is not a canonical verbatim-citable path.
     - Row `#3` outputs `AccessHarvester.Vba.Grammar/Vba.g4`; row `#4` shortens the same artifact to `Vba.g4`, so the path string is not consistent.
   - **Fix:** Resolve the path/name choice now or assign one canonical placeholder path and use it everywhere; normalize output paths to exact repo-relative strings; regenerate/redraw the DAG directly from the prerequisite table so every listed edge appears explicitly.

4. **F4 — Medium**
   - **Issue:** Cross-section numeric drift and column-reference drift remain in the document.
   - **Location:** `Adopt-now rows applied` bullet `#2`; `Total`; `Critical path`.
   - **Evidence:**
     - The adopt-now `#2` bullet says “output paths (column 5)” and “dependency lists (column 4)”, but those column numbers do not match the actual table.
     - The row session ranges sum to **64-79**, not **65-82**.
     - The listed critical path sums to **52-63** sessions, not **55-65**.
   - **Fix:** Recompute the totals from the table and correct the summary; remove or correct the column-number references.

5. **F5 — Medium**
   - **Issue:** `jet-sql-parser-and-resolution` looks like two children fused into one, which increases critical-path depth and reduces parallelism.
   - **Location:** Child row `#12 jet-sql-parser-and-resolution`.
   - **Evidence:** Row `#12` covers all of **Phase 5.1-5.4** in one 5-7-session child and depends on both `#6` and `#11`. But `5.1-5.2` (JET SQL grammar/parser infrastructure) can start after parser infrastructure, while `5.3-5.4` (form-reference detection/dynamic analysis) are the Access-aware pieces that plausibly wait on `#11`. Keeping them fused forces the whole SQL branch to wait for the resolver/metadata integration chain.
   - **Fix:** Split `#12` into two children:
     - one for `5.1-5.2` depending on `#6`,
     - one for `5.3-5.4` depending on the SQL-parser child plus `#11`.

---

# Verification Round 2

## ISSUES_FOUND

1. **Major**  
   **Issue:** The DAG is still not a faithful mirror of the prerequisite table, so F3 is not fully resolved. It draws an extra `accessexportertemp1-import → #2` edge that is not in row `#2`, and it still does not explicitly draw `#12 → #13` or `#15 → #17`.  
   **Location:** `Dependency DAG`; child rows `#2 vba-spec-study`, `#13 jet-sql-resolution-and-dynamic`, `#17 schema-finalization-and-docs`.  
   **Fix:** Regenerate the diagram directly from column 4 of the child table: remove the apparent `accessexportertemp1-import → #2` edge, add an explicit join from `#12` into `#13`, add an explicit join from `#15` into `#17`, then re-check the converse so every drawn edge exists in a row and every row edge is drawn.

2. **Medium**  
   **Issue:** New numeric drift was introduced in the Session 4 notes: the document says `Test-fixture authoring -- 3 instances forced` but lists four forced rows (`#5`, `#6`, `#10`, `#18`).  
   **Location:** `Notes for Sessions 3 and 4` → Session 4 bullet `Test-fixture authoring`.  
   **Fix:** Change the count to `4 instances forced`, or remove the numeric count and keep only the row list.

---

# Verification Round 3

VERIFIED
