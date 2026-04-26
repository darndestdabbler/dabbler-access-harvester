# Verification Round 1

## 1. Spec compliance (hazard standards)

**Verdict:** **PARTIAL**

### Spot-checks

| Hazard | Section | Concrete syntax cited? | Verdict |
|---|---|---:|---|
| H-A01 Line continuation `_` | A | Yes — exact continued assignment line shown | Pass |
| H-B01 Bang binds tighter than dot | B | Yes — `Me!cboCaseworker.Column(2)` / `Forms!frm...` | Pass |
| H-C03 Nested `With` shadowing | C | Yes — quoted nested `With` block | Pass |
| H-D08 Empty positional arguments | D | Yes — exact `DoCmd.OpenForm "frmCaseDetail", , , , ...` call | Pass |
| H-E03 Property Get / Let triplets | E | Yes — exact `Property Get` / `Property Let` block | Pass |
| H-F03 Implements naming | F | Yes — `Private Sub IAuditLogger_LogEvent(...)` | Pass |
| H-G04 ValidationRule context | G | Yes — `Between #1/1/2000# And Date()` | Pass |
| H-H03 Dynamic `OpenForm` | H | Yes — `DoCmd.OpenForm sFormName, , , sFilter` | Pass |
| H-H05 Configuration-table-driven control flow | H | No — whole-procedure architectural pattern, not a single syntax hazard | **Fail** |

### Findings

- **Major**  
  **Issue →** Abstract/non-syntax hazard in a document that is supposed to list concrete syntax hazards.  
  **Location →** `H-H05: Configuration-table-driven control flow`  
  **Fix →** Replace with one or more concrete syntax hazards extracted from the procedure, or move it to a separate “analysis patterns / unresolved-flow patterns” appendix. Example replacements: `CurrentDb.OpenRecordset("SELECT ... FROM tblFormConfig ...")`, `If rs.EOF Then Exit Sub`, or the concrete variable-to-dispatch handoff already covered by H-H04.

- **Minor**  
  **Issue →** Entry explicitly says it is “not a parsing hazard per se,” which weakens compliance with the hazard-list standard.  
  **Location →** `H-F07: Module-level state accumulator pattern`  
  **Fix →** Reframe as a concrete syntax/resolution hazard tied to the specific event procedures and module-level declarations, or move it to a separate “semantic patterns” section.

---

## 2. Field completeness

**Verdict:** **FAIL**

### Findings

- **Major**  
  **Issue →** `File` field lacks concrete line-number citations.  
  **Location →** `H-D05: Resume / Resume Next / Resume <label>`  
  **Fix →** If corpus-backed, add exact fixture line numbers. If spec-seeded only, make that explicit, e.g. `File: none in current 12-fixture corpus (spec-seeded hazard)`.

- **Major**  
  **Issue →** `File` field lacks line numbers.  
  **Location →** `H-F09: Implicit form-engine event hookup`  
  **Fix →** Add exact handler lines from fixtures 01/09/12, e.g. `01-simple-validation.bas:8`, `09-report-grouping-events.bas:25`, `12-switchboard-navigation.bas:18`.

- **Minor**  
  **Issue →** `Construct` is not a tight 1–2 line quoted syntax sample.  
  **Location →** `H-H05`  
  **Fix →** Replace the prose description with an exact excerpt from `OpenFormByConfig`, then keep the architectural explanation in `Description`.

---

## 3. MS-VBAL citation accuracy

**Verdict:** **FAIL**

### Findings

- **Major**  
  **Issue →** Citation appears wrong/self-contradictory.  
  **Location →** `H-H06` cites `§5.2.5 (New Expression — non-applicable; this is CreateObject)`  
  **Fix →** Remove `§5.2.5` and cite this as `Access ext. / no direct MS-VBAL core section`, or cite the actual validated section from `ms-vbal-summary.md` if one exists.

- **Major**  
  **Issue →** Citation appears invented / not in the canonical list provided for verification.  
  **Location →** `H-A09` cites `§3.3 (Data Types)`  
  **Fix →** Replace with the exact validated section from Session 1 summary, or mark as `MS-VBAL core type-name tokens; exact section TBD` pending correction.

- **Minor**  
  **Issue →** Vague non-canonical citation.  
  **Location →** `H-A04` cites `§3.3 separators`  
  **Fix →** Replace with the exact subsection from `ms-vbal-summary.md`, or remove the unsupported `§3.3` citation and keep the validated label/control-flow citation.

- **Minor**  
  **Issue →** Vague non-canonical citation.  
  **Location →** `H-D06` cites `§3.3 (Statement separator)`  
  **Fix →** Same fix as H-A04: use the exact validated subsection or omit the unsupported `§3.3` reference.

- **Minor**  
  **Issue →** Citation uses broad sections not in the canonical list.  
  **Location →** `H-B07` cites `§5.1, §5.2`  
  **Fix →** Replace with the exact validated subsection(s) from Session 1 summary, if present; otherwise mark as `MS-VBAL expression operator semantics; exact subsection TBD`.

- **Minor**  
  **Issue →** Citation uses broad section not in the canonical list.  
  **Location →** `H-E04` cites `§4.5 (Procedure Declarations)`  
  **Fix →** Replace with the exact validated subsection, or cite only `§6.4 Friend` plus module-scope references already validated.

- **Minor**  
  **Issue →** Citation uses broad section not in the canonical list.  
  **Location →** `H-E05` cites `§4.5 (Procedure Declarations)`  
  **Fix →** Replace with a validated subsection or keep only the validated `§5.3.1 Let Statement` citation.

- **Minor**  
  **Issue →** Citation uses broad section not in the canonical list.  
  **Location →** `H-F04` cites `§6 (class lifecycle)`  
  **Fix →** Replace with the exact validated lifecycle/event section from Session 1 summary, or mark as naming-convention/access-runtime behavior rather than cite `§6`.

- **Nit**  
  **Issue →** Unnecessary reference to an unverified section while already saying the hazard is outside MS-VBAL core.  
  **Location →** `H-G01` mentions `§5.1`  
  **Fix →** Drop the `§5.1` reference and leave this as `Outside MS-VBAL core / Access ext. / harvester-policy concern`.

---

## 4. Coverage of the critical constructs

**Verdict:** **PARTIAL**

| Critical construct | Covered by hazard(s)? | Verdict |
|---|---|---|
| Bang operator | H-B01, H-B02, H-C02 | Yes |
| Default property access | H-B03 | Yes |
| Me keyword | H-B08 | Yes |
| Set vs. Let | H-B04 | Yes |
| Line continuation | H-A01 | Yes |
| Line numbers | H-A03 | Yes |
| Type-declaration characters | H-A02 | Yes |
| Date literals | H-A05 | Yes |
| ByRef / ByVal | H-E02 | Yes |
| Optional / ParamArray / named args | H-E01 covers Optional only | **Partial / No for ParamArray and named args** |
| Property Get/Let/Set triplets | H-E03 | Yes |
| Events / WithEvents | H-F01, H-F02, H-F05, H-F09 | Yes |
| Implements | H-F03 | Yes |
| On Error / Resume | H-D04, H-D05 | Yes |
| GoTo / GoSub / Return | H-D01, H-D02, H-D03 | Yes |

### Findings

- **Major**  
  **Issue →** Critical construct family is only partially covered; no explicit hazard for `ParamArray` or named-argument call syntax.  
  **Location →** Hazard inventory, Section E / D  
  **Fix →** Add spec-seeded hazards for:
  - `ParamArray args() As Variant` (`§4.6`)
  - named arguments at call sites, e.g. `DoCmd.OpenForm FormName:="frmX", OpenArgs:="Y"` (`§5.5`)
  Mark them as spec-seeded if absent from the current corpus.

---

## 5. Cross-reference table accuracy

**Verdict:** **FAIL**

### Spot-checks

| Fixture row | Spot-check result | Verdict |
|---|---|---|
| `03-with-block-shadowing.bas` | Lists `H-B04`, but `H-B04` does not cite fixture 03 | Fail |
| `04-saved-query-form-params.sql` | Lists `H-A05`, but `H-A05` does not cite fixture 04 | Fail |
| `10-class-with-events.cls` | Listed hazards match cited entries | Pass |
| `12-switchboard-navigation.bas` | Lists `H-H03`, but `H-H03` only cites fixture 08 (and mentions fixture 11 as literal/non-unresolvable contrast) | Fail |

### Findings

- **Major**  
  **Issue →** Cross-reference includes a hazard not actually cited against the fixture.  
  **Location →** `03-with-block-shadowing.bas` row includes `H-B04`  
  **Fix →** Remove `H-B04` from the row, or expand `H-B04` to cite fixture 03 if that was the intent.

- **Major**  
  **Issue →** Cross-reference includes a hazard not actually cited against the fixture.  
  **Location →** `04-saved-query-form-params.sql` row includes `H-A05`  
  **Fix →** Remove `H-A05` from the row unless a date-literal occurrence is added and cited in `H-A05`.

- **Major**  
  **Issue →** Cross-reference includes a hazard not actually cited against the fixture.  
  **Location →** `12-switchboard-navigation.bas` row includes `H-H03`  
  **Fix →** Remove `H-H03`; fixture 12 uses literal `OpenForm` targets and belongs under resolvable navigation, not dynamic `OpenForm`.

---

## 6. Hazard count consistency

**Verdict:** **PASS**

Counted entries match the summary:

- Section A: 9 (`H-A01`..`H-A09`)
- Section B: 8 (`H-B01`..`H-B08`)
- Section C: 4 (`H-C01`..`H-C04`)
- Section D: 11 (`H-D01`..`H-D11`)
- Section E: 6 (`H-E01`..`H-E06`)
- Section F: 9 (`H-F01`..`H-F09`)
- Section G: 7 (`H-G01`..`H-G07`)
- Section H: 6 (`H-H01`..`H-H06`)

**Total:** 60

No findings.

---

## 7. Static-analysis-defeating hazards

**Verdict:** **PASS**

Section H includes all required items:

- `Eval()` → `H-H01`
- `Application.Run` with variable callback → `H-H02`
- dynamic `DoCmd.OpenForm` → `H-H03`
- `DLookup`-derived dispatch → `H-H04`
- configuration-table-driven control flow → `H-H05`
- late-bound `CreateObject` / `As Object` receivers → `H-H06`

### Finding

- **Major**  
  **Issue →** Inclusion is complete, but one of the required items is documented as an abstract pattern rather than a concrete syntax hazard.  
  **Location →** `H-H05`  
  **Fix →** Keep the concept, but restate it as concrete syntax or move it to a separate unresolved-pattern appendix.

---

## 8. Author neutrality

**Verdict:** **MOSTLY PASS**

### Finding

- **Nit**  
  **Issue →** A few entries prescribe a specific AST design rather than the required observable behavior.  
  **Location →** `H-B03`, `H-B04`, `H-C04`  
  **Fix →** If implementation flexibility is desired, soften from “the AST must use X node shape” to “the parser/AST must preserve enough information to distinguish X from Y.” If the AST shape is already a project contract, no change needed.

---

## Final verdict

**ISSUES_FOUND --**

### Major
- **Issue →** Abstract/non-syntax hazard violates the hazard-list standard.  
  **Location →** `H-H05`  
  **Fix →** Replace with concrete syntax hazards or move to a separate appendix.

- **Issue →** Missing line-numbered `File` citations.  
  **Location →** `H-D05`, `H-F09`  
  **Fix →** Add exact fixture lines or mark clearly as spec-seeded/no corpus witness.

- **Issue →** Citation accuracy problems, including at least one clearly wrong/self-contradictory citation.  
  **Location →** `H-H06`, `H-A09`  
  **Fix →** Replace with exact validated Session 1 sections or mark as `Access ext. / no direct MS-VBAL section`.

- **Issue →** Critical construct coverage is incomplete for `ParamArray` and named arguments.  
  **Location →** Hazard inventory overall  
  **Fix →** Add spec-seeded hazards for `ParamArray` and named-argument calls.

- **Issue →** Cross-reference table contains incorrect fixture→hazard mappings.  
  **Location →** Rows for fixtures `03`, `04`, `12`  
  **Fix →** Remove or correct `H-B04`, `H-A05`, and `H-H03` from those rows.

### Minor
- **Issue →** Some citations use vague/non-canonical section references not aligned with the verified section list.  
  **Location →** `H-A04`, `H-D06`, `H-B07`, `H-E04`, `H-E05`, `H-F04`, `H-G01`  
  **Fix →** Normalize all citations to the exact Session 1 validated section numbers/titles.

- **Issue →** One entry is framed as a semantic/report-schema pattern rather than a parser hazard.  
  **Location →** `H-F07`  
  **Fix →** Reframe or relocate.

### Nit
- **Issue →** Some wording is implementation-prescriptive rather than outcome-prescriptive.  
  **Location →** `H-B03`, `H-B04`, `H-C04`  
  **Fix →** Hedge to behavior requirements unless AST shape is intentional policy.

---

# Verification Round 2

## Major

- **Issue** → The document now contradicts itself on corpus grounding.
  **Location** → `Status — seed list` note (`Every hazard listed here is grounded in observed fixture syntax — no abstract concerns.`) versus H-D05, H-D12, and H-E07, which are explicitly marked spec-seeded with no corpus witness.
  **Fix** → Rewrite that status sentence to acknowledge the three synthetic/spec-seeded exceptions, or move those three hazards into a separate clearly-labeled synthetic-coverage subsection.

## Minor

- **Issue** → One cross-reference row is still self-inconsistent.
  **Location** → Cross-reference table row for `03-with-block-shadowing.bas` lists `H-B01`, but H-B01’s `File:` field cites only fixtures 02 and 09.
  **Fix** → Add a real fixture-03 witness to H-B01 if one exists; otherwise remove `H-B01` from the fixture-03 row.

- **Issue** → Some `File:` fields still include non-witness references.
  **Location** → H-B07 (`08-unresolvable.bas:25` is `If rs.EOF Then`, not an `Is` / `Is Not` comparison); H-D04 (`08-unresolvable.bas` is explicitly noted as having no `On Error` construct).
  **Fix** → Remove those references or replace them with actual witness lines.

- **Issue** → The `File:` field contract is still not fully met in several entries because references lack exact line citations or use whole-fixture shorthand.
  **Location** → H-A07 (`All fixtures (comments are pervasive)`), H-B08 (`09-report-grouping-events.bas` with no lines), H-D09 (`fixtures 05 and 11 use the Call-less form throughout`), H-E04 (`10-class-with-events.cls` with no lines).
  **Fix** → Replace each shorthand reference with explicit `file:line` citations, or remove the extra fixture references.

ISSUES_FOUND -- remaining issues listed above by severity.

---

# Verification Round 3

## Round-2 fix verification

- Status note is now internally consistent with the three spec-seeded hazards.
- Fixture-03 cross-reference row no longer lists `H-B01`.
- `H-B07` and `H-D04` no longer include the specific round-2 non-witness references.
- `H-A07`, `H-B08`, `H-D09`, and `H-E04` now have explicit line-cited File fields.

## Remaining issues

### Major

- **Issue:** Parameter-modifier order is stated incorrectly and contradicts the witnessed construct.
  - **Location:** `H-E01` → **Description**
  - **Fix:** Replace `"[ByRef|ByVal] [Optional] <name> ..."` / “Optional ... follows ByVal/ByRef” with the actual VBA order used by the witness and by the grammar, i.e. `Optional [ByRef|ByVal] <name> ...` (matching `Optional ByVal sFilterArgs As String = ""`).

- **Issue:** The description introduces `Resume 0`, but the entry’s own Construct field lists only `Resume`, `Resume Next`, and `Resume <label>`.
  - **Location:** `H-D05` → **Description**
  - **Fix:** Remove both `Resume 0` references unless you add an explicit MS-VBAL citation proving that form is valid and intended here. Otherwise keep the entry to the three listed forms only.

### Minor

- **Issue:** File field includes a non-witness contrast example.
  - **Location:** `H-B08` → **File** (`10-class-with-events.cls:42` is explicitly “not `Me`”)
  - **Fix:** Remove `10-class-with-events.cls:42` from the File field or move that contrast note into Description. File should list only actual `Me` witnesses.

- **Issue:** One listed subform has no cited witness even though the entry is not marked as partially spec-seeded.
  - **Location:** `H-D04` → **Construct** / **File**
  - **Fix:** Either add a real `file:line` witness for `On Error GoTo 0`, or explicitly note that `GoTo 0` is spec-covered but absent from the current corpus.

## Final spec compliance

- All 62 hazard IDs are present and fielded.
- The three fully spec-seeded hazards (`H-D05`, `H-D12`, `H-E07`) are now labeled consistently in the status note, their entries, and the cross-reference preamble.
- Not fully compliant yet because the two Major issues above affect grammar/spec accuracy.

## Verdict

**ISSUES_FOUND -- list remaining issues by severity.**

Block the commit on the 2 Major issues. The 2 Minor items can be deferred to a Session 4 buffer or follow-up grammar work if needed after the majors are fixed.
