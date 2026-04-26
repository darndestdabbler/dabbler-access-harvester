## 1. Grammar fix correctness

**Issue →** `singleLineStatementList: statement (COLON statement)* COLON?` fixes the reported `stmt : REM ...` / `stmt : '...` failure, but it also accepts a bare trailing separator (`stmt :`) because the parser cannot distinguish that from `stmt : comment` once comments are on the `HIDDEN` channel.

**Location →** `AccessHarvester.Vba.Grammar/Vba.g4`, `singleLineStatementList`

**Fix →**  
- For **Session 2**, this is an acceptable minimal fix under the current lexer design.  
- If you want **strict** grammar acceptance later, make end-of-line comments parser-visible instead of hiding them, then model the tail explicitly rather than using generic `COLON?`.

**Verdict →** Correct for the bug you found; slightly over-accepting by design.

---

## 2. Harness design / corpus coverage

**Issue →** The harness correctly covers all parser-relevant fixtures it discovers (`.bas` + `.cls`), but it does **not** prove the intended corpus size, and your report says `17` fixtures while the executed parser corpus is `15/15`.

**Location →** `tests/AccessHarvester.Vba.Grammar.Tests/Modules/VbaCorpusParserTests.cs`

**Fix →**  
- Keep the current file-type filtering; excluding `.sql` / `.txt` for the `module` start rule is correct.  
- Add a small coverage lock:
  - sort enumerated files for determinism
  - assert expected counts or exact filenames
- Clarify in the session notes that the module parser harness targets **15 VBA-module fixtures**, not all 17 repo fixtures.

**Verdict →** Adequate for the VBA-module corpus; documentation/counting needs tightening.

---

## 3. Silent mis-parse / recovery risk

**Issue →** The harness throws on reported syntax errors, but it does **not** fully guarantee a clean full-input parse unless `module` consumes `EOF`. Also, it uses the default recovery strategy.

**Location →** `Parse()` in `VbaCorpusParserTests.cs`, and the `module` rule in `Vba.g4`

**Fix →**  
- Ensure the grammar start rule ends with `EOF`, **or** assert EOF after parse.  
- Set:
  ```csharp
  parser.ErrorHandler = new BailErrorStrategy();
  ```
- If you want confidence in tricky constructs beyond “accepted”, add a few parse-tree shape assertions for the edge fixtures most likely to be ambiguous.

**Verdict →** No second concrete fixture bug is evident from what you showed, but the current harness can prove “accepted” better than it can prove “cleanly and fully parsed”.

---

## 4. Session spec satisfaction

**Issue →** The implementation satisfies the substantive Session 2 goal, but the fixture-count narrative is inconsistent.

**Location →** Session report / spec accounting

**Fix →**  
- Record Session 2 as complete with this clarification:
  - grammar harness run on all `.bas` / `.cls` VBA-module fixtures
  - `.sql` / `.txt` intentionally excluded
- Recommended before Session 3:
  - EOF enforcement
  - `BailErrorStrategy`
  - corpus count/filename assertion

**Verdict →** Yes, Session 2 is satisfied. No blocker for Session 3, but the three hardening items above should be landed soon.