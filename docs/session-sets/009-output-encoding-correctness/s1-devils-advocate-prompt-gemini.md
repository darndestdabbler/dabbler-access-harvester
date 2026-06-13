# Devil's Advocate Review (Gemini)

## DA-1 (cross-surface divergence)
**Ruling:** UPHELD
**Severity:** Major
**Evidence:** An empirical probe writing a UTF-16 BE BOM file (`FE FF 00 E9 00 61` containing the character `é`) shows that `RunParse` diverged from `Pipeline`/`SaveAsTextTranslator`. The CLI `RunParse` has a manual check for UTF-8 (`EF BB BF`) and UTF-16 LE (`FF FE`), missing UTF-16 BE, dropping to blind cp1252 resulting in corrupted output (`þÿ\x00é\x00a`). However, `SaveAsTextTranslator` uses `File.ReadAllText(file, cp1252)` which sets `detectEncodingFromByteOrderMarks: true` and successfully spots UTF-16 BE, decoding it perfectly as `é a`. The decodes **still deeply disagree** depending on the specific BOM and surface.
**Recommended Action:** Centralize the decode strategy either into a shared reader helper or push it to the extraction boundary (see DA-4).

## DA-2 ("no golden regen" is an unverified inference)
**Ruling:** UPHELD
**Severity:** Major
**Evidence:** Scanning the repository's `samples\DatabaseYaml\HomeHealthCare.accdb\Forms` reveals numerous files containing bytes > `0x7F`. For example, `frmAbout.yaml` has large binary blobs in the `PrtDevMode` and `PrtDevModeW` properties teeming with non-ASCII bytes. The previous UTF-8 decoder irrecoverably replaced these with `U+FFFD`. The new CP1252 logic will translate these bytes differently. The claim that the output is byte-identical because the input is "all-ASCII" is factually false. 
**Recommended Action:** The `CanonicalGoldenRegressionTests` MUST be run to regenerate the golden files.

## DA-3 (tests pass for the wrong reason)
**Ruling:** PARTIAL
**Severity:** Minor
**Evidence:** A probe reading a UTF-8-BOM prefixed string via `File.ReadAllText(file)` vs `File.ReadAllText(file, Encoding.GetEncoding(1252))` returns the exact same string. The `Utf8Bom_HonouredOverCp1252Fallback` test asserts behaviour that has not changed; it would pass even if the fix was wholesale reverted. While the two no-BOM cp1252 tests successfully act as the real lock mechanism, this test specifically is non-discriminating.
**Recommended Action:** Leave the test as documentation of behaviour, but understand it isn't enforcing the fix itself. 

## DA-4 (lean / wrong-layer)
**Ruling:** UPHELD
**Severity:** Major
**Evidence:** Examination of the codebase reveals that three disjoint code paths `AccessHarvester.Cli/Program.cs` (`RunParse`), `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs`, and `AccessHarvester.Core/Pipeline.cs` all separately attempt to read `raw/*.txt` files while managing their own CP1252 code-page registration logic and differing BOM parsing logic. `AccessHarvester.Core/Extraction/SaveAsTextExporter.cs` merely calls `session.Application.SaveAsText` and leaves the `1252` document on disk. The cp1252 interpretation is a property of the COM exporter, not the translating pipeline.
**Recommended Action:** Transcode `raw/` files strictly to UTF-8 inside `SaveAsTextExporter` immediately following `Application.SaveAsText`. Downstream consumers should only need to read UTF-8 without legacy encoding registrations or custom BOM logic.

```json
{
  "verdict": "MUST_FIX",
  "findings": [
    { "id": "DA-1", "ruling": "UPHELD", "severity": "Major", "evidence": "Probe of UTF-16 BE BOM via RunParse yields `þÿ\\0é\\0a` while Pipeline/SaveAsTextTranslator yields `é a`.", "action": "Centralize the decode strategy or push it to the extraction boundary." },
    { "id": "DA-2", "ruling": "UPHELD", "severity": "Major", "evidence": "Grep of `samples/DatabaseYaml/HomeHealthCare.accdb/Forms/frmAbout.yaml` shows non-ASCII bytes in `PrtDevMode` attributes which historically mapped to U+FFFD.", "action": "Run CanonicalGoldenRegressionTests." },
    { "id": "DA-3", "ruling": "PARTIAL", "severity": "Minor", "evidence": "UTF-8 BOM file yields identical results with both `File.ReadAllText(file)` and `File.ReadAllText(file, cp1252)`.", "action": "Retain test as behavioural documentation, acknowledging it isn't the fix's lock." },
    { "id": "DA-4", "ruling": "UPHELD", "severity": "Major", "evidence": "Code inspection reveals 3 disjoint areas needing CP1252 encoding logic (RunParse, Translation, Pipeline) because `SaveAsTextExporter` does not normalize to UTF-8.", "action": "Transcode to UTF-8 at the exporter/raw-write boundary." }
  ]
}
```