Reviewed against the current `main` tree at `b1df0a7`, with a guard check that the implementation files under review did not change after `c515465` (`git diff --name-only c515465..HEAD -- AccessHarvester.Core/Translation/SaveAsTextTranslator.cs AccessHarvester.Core/Pipeline.cs AccessHarvester.Cli/Program.cs tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs` returned no output). That makes the code probes below applicable to the `2067c9f..c515465` work, even though `HEAD` also contains later docs/session-set commits.

The right question is slightly narrower than the prompt's framing. The F-1-1 fix itself is not the same thing as every surrounding justification made about it. The production fix in `SaveAsTextTranslator` looks sound for the bug it claims to fix: no-BOM SaveAsText bytes now decode as cp1252, and the translator matches the manifest path on that case. The contrarian pressure mostly lands on two overclaims around that core: first, that this also "matches `RunParse`" in every BOM case; second, that the skipped golden regen was justified by an overly broad "fixtures are all-ASCII" story rather than a tighter statement about the committed `kb/`/`ir` golden corpus.

## DA-1 — PARTIAL

Severity: Minor

Evidence:

- `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs:26` self-registers the code-pages provider, and `:79` / `:91` read via `File.ReadAllText(file, cp1252)` and `File.ReadAllLines(file, cp1252)`.
- `AccessHarvester.Core/Pipeline.cs:275` also registers the provider and `:334` reads the raw module text with `Encoding.GetEncoding(1252)`.
- A direct probe showed that the translator and the manifest-style `ReadAllText(..., cp1252)` path both honour BOMs beyond the two manual BOM checks in `RunParse`:

```text
TRANSLATE frm-le caption="Café"
TRANSLATE frm-be caption="Café"
TRANSLATE mod-le hasCafe=True
TRANSLATE mod-be hasCafe=True
RUNPARSE-DECODE mod-le hasModProbe=True hasCafe=True
RUNPARSE-DECODE mod-be hasModProbe=False hasCafe=False
CLI-PARSE mod-le exit=1 hasModProbe=False hasCafe=False
CLI-PARSE mod-be exit=1 hasModProbe=False hasCafe=False
```

- A second probe showed that the translator change is still a no-BOM-only behavioral change relative to old `File.ReadAllText(file)`: BOM-bearing files decode the same either way.

```text
UTF8-BOM defaultEqCp1252=True value=Café
UTF16BE-BOM defaultEqCp1252=True value=Café
```

Ruling:

The core F-1-1 claim is intact for the actual bug: the fix corrects the no-BOM cp1252 corruption and aligns the translator with the manifest path. What does not survive falsification is the broader rationale that this now "matches `RunParse`" as a general BOM strategy. `RunParse` in `AccessHarvester.Cli/Program.cs:590-600` explicitly recognizes only `EF BB BF` and `FF FE`; it still diverges from translator/manifest behavior on a UTF-16 BE BOM file. I do not have evidence that Access SaveAsText emits UTF-16 BE, so this is not a demonstrated regression in the reviewed feature. It is still a real cross-surface inconsistency on synthetic but valid BOM-bearing input.

Recommended action:

- Do not block P2 on this.
- Tighten the claim in docs/closeout from "matches `RunParse`" to "matches the manifest path and preserves BOM detection; `RunParse` remains narrower on BOM coverage."
- If `parse` is intended to accept arbitrary BOM-bearing text files, backlog a follow-up to switch it from manual two-BOM sniffing to the same BOM-detect-then-cp1252 reader behavior.

## DA-2 — PARTIAL

Severity: Minor

Evidence:

- The exact original bug still reproduces on a no-BOM cp1252 file:

```text
DEFAULT=C�l
CP1252=Cél
```

- The committed golden corpus that this change would actually perturb if it contained non-ASCII in translated artifacts is ASCII-only where it matters:

```text
ROOT samples/GoldenOutput/HomeHealthCare/kb
FILES 86
NONASCII_FILES 0
ROOT samples/GoldenOutput/HomeHealthCare/ir
FILES 87
NONASCII_FILES 0
```

- But the broader fixture story as stated is too strong. The committed fixture mirror is not globally ASCII-only:

```text
ROOT samples/DatabaseYaml
FILES 322
NONASCII_FILES 92
```

Representative example from `samples/DatabaseYaml/HomeHealthCare.accdb/Forms/frmBudgetServicePlan.yaml`:

```text
PrtDevMode: "????�?????b??da..."
PrtDevModeW: "Y 4500 series)??�??b??da..."
```

- I also attempted the COM `CanonicalGoldenRegressionTests`, but that result is not clean evidence against the reviewed change because the local workspace is dirty (`git status --short` shows `M samples/HomeHealthCare.accdb`). The run failed with golden drift in `kb/ModulesAndClasses/basDataOperations.json`, but on a modified fixture database that is not attributable to the reviewed commit range.

Ruling:

The strong form of the closeout claim, "the committed HomeHealthCare fixtures are all-ASCII," is false. The narrower claim that matters for this session is better supported: the committed `kb/` and `ir/` golden trees are ASCII-only, so this translator-only fix would not be expected to change tracked golden bytes absent non-ASCII translated payloads. In other words, the skip decision is probably fine, but the stated justification was looser than the evidence.

Recommended action:

- Treat the no-regen decision as acceptable for this session, but rewrite the rationale to reference the committed `samples/GoldenOutput/HomeHealthCare/kb` and `.../ir` trees specifically, not "fixtures" in general.
- If someone wants a fully empirical close on future encoding fixes, rerun golden regression only after confirming the fixture DB itself is clean.

## DA-3 — OVERTURNED

Severity: None

Evidence:

- `tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs:159` pins the block-format `ReadAllLines(..., cp1252)` path with literal `43 E9 6C` bytes.
- `tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs:183` separately pins the module `ReadAllText(..., cp1252)` path.
- `tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs:206` confirms the translated `kb` JSON carries the accented caption end-to-end without U+FFFD.
- `tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs:230` checks UTF-8 BOM precedence.
- Running the focused suite on the current tree passed `9/9`.
- A repo-wide grep over `tests/**/*.cs` for `RegisterProvider(` returned no matches, so the old "tests pass only because the assembly pre-registers code pages" theory does not hold on the current tree.

Ruling:

I could not find a realistic reversion of the actual production fix that leaves all 9 tests green. If you revert the block-format cp1252 read, the test at `:159` fails. If you revert only the module-string cp1252 read, the test at `:183` fails. If you regress the end-to-end translation, the test at `:206` fails. The one devil's-advocate concession is that the UTF-8 BOM test is not discriminating with respect to the historical bug, because the pre-fix default `ReadAllText(file)` also honoured a UTF-8 BOM. So the real F-1-1 lock is carried by the two no-BOM cp1252 tests plus the end-to-end caption test, not by the BOM-precedence test. That is a framing issue, not a coverage hole.

Recommended action:

- No code change required.
- If you want the docs to be maximally precise, describe the BOM-precedence test as a non-regression guard on the new reader configuration, not as the primary lock for F-1-1.

## DA-4 — OVERTURNED

Severity: None

Evidence:

- The static constructor in `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs:18-26` is a type-local bootstrap for a non-CLI host path.
- The CLI also registers at `AccessHarvester.Cli/Program.cs:34-35`, and the manifest path independently registers at `AccessHarvester.Core/Pipeline.cs:272-275` because it also calls `GetEncoding(1252)` on raw SaveAsText text.
- A targeted grep across `AccessHarvester.Core/**/*.cs` found no remaining sibling raw SaveAsText `.txt` read sites still using the UTF-8 default. The relevant raw readers are the translator (`SaveAsTextTranslator.cs:79`, `:91`) and the manifest raw-module reader (`Pipeline.cs:334`); the other `File.ReadAllText` calls in core are JSON/IR readers.

Ruling:

The static-ctor registration is not dead defensive code. It earns its keep because this translator is directly callable outside `Program.Main()`, and the focused tests are exactly such a host shape. Yes, there is duplicated registration across CLI, pipeline, and translator, but this is idempotent bootstrap at the points that actually need `GetEncoding(1252)`, not an abstraction failure. I also do not see a same-session wrong-layer bug where sibling SaveAsText readers were left behind on UTF-8 default reads. The only live cross-surface gap I found is the narrower BOM handling in `RunParse`, which is a separate concern from where the provider gets registered.

Recommended action:

- No must-fix.
- If the duplication offends someone later, factor a small shared helper for "open Access text with BOM-detect-then-cp1252" only when there is a second real maintenance pain, not pre-emptively.

Overall verdict: safe to build on as-is. I would not stop `010-output-contract-fidelity-batch` for either finding above. The production F-1-1 fix is sound for the actual no-BOM SaveAsText corruption, the hermetic tests lock the real regression vector, and the remaining issues are scope/wording problems plus one synthetic `RunParse` BOM-width mismatch that is not evidenced as real Access SaveAsText output.

```json
{
  "verdict": "SAFE_TO_BUILD_ON",
  "findings": [
    {
      "id": "DA-1",
      "ruling": "PARTIAL",
      "severity": "Minor",
      "evidence": "Probe: translator decoded UTF-16 LE/BE BOM files to Café, while RunParse-style manual decoding failed on UTF-16 BE (`RUNPARSE-DECODE mod-be hasModProbe=False hasCafe=False`). Code: SaveAsTextTranslator.cs:79,91; Pipeline.cs:334; Program.cs:590-600.",
      "action": "Do not block P2; narrow the claim from 'matches RunParse' to 'matches the manifest path and preserves BOM detection', and backlog a RunParse BOM-breadth follow-up only if arbitrary BOM-bearing input is in scope."
    },
    {
      "id": "DA-2",
      "ruling": "PARTIAL",
      "severity": "Minor",
      "evidence": "Probe: committed golden `kb/` and `ir/` trees under samples/GoldenOutput/HomeHealthCare contain 0 non-ASCII-byte files, but broader committed fixture mirror samples/DatabaseYaml has 92 such files. COM golden run was inconclusive because `samples/HomeHealthCare.accdb` is locally modified.",
      "action": "Keep the no-regeneration decision, but restate the rationale in terms of the committed `kb/`/`ir` golden corpus rather than 'fixtures are all-ASCII'; only use a COM golden rerun as evidence when the fixture DB is clean."
    },
    {
      "id": "DA-3",
      "ruling": "OVERTURNED",
      "severity": "None",
      "evidence": "Tests at SaveAsTextTranslatorTests.cs:159,183,206 would fail under realistic reversions of the cp1252 fix; focused run passed 9/9; repo-wide grep found no `RegisterProvider(` call in tests/**/*.cs.",
      "action": "No code change required; optionally describe the UTF-8 BOM test as supportive rather than the primary F-1-1 lock."
    },
    {
      "id": "DA-4",
      "ruling": "OVERTURNED",
      "severity": "None",
      "evidence": "Static-ctor registration in SaveAsTextTranslator.cs:18-26 is needed for non-CLI hosts; manifest path separately registers at Pipeline.cs:272-275 because it independently calls GetEncoding(1252); no remaining Access raw `.txt` readers in AccessHarvester.Core were found on UTF-8 defaults.",
      "action": "No must-fix; leave the local bootstrap in place unless a later session has a concrete reason to centralize it."
    }
  ]
}
```