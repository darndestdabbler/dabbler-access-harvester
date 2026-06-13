1. **F-1-1 correctness.**  
By code inspection, the production fix itself looks correct. `BuildPayload` now binds `var cp1252 = Encoding.GetEncoding(1252);` once and passes it to **both** read sites that previously defaulted to UTF-8:

- `File.ReadAllText(file, cp1252).TrimStart()` for signature probing and the `XmlContent` / `VbaCode` payload-returning branches.
- `File.ReadAllLines(file, cp1252)` for the block-format parser branch.

That is the right fallback encoding for Access SaveAsText’s ANSI output. On modern .NET, the `File.ReadAllText(path, encoding)` overload is implemented via a `StreamReader` that still performs BOM detection, so the stated behavior — “honour UTF-8/UTF-16 BOM if present, otherwise fall back to cp1252” — is accurate for `ReadAllText`. The same practical conclusion applies to `ReadAllLines(path, encoding)`, which also goes through a BOM-detecting reader path. So the **implementation** matches the intended BOM-detect-then-cp1252 strategy already used elsewhere in the repo.

The devil’s-advocate caveat is not the production code here; it is the pinning. The new tests prove the cp1252 path on the block-format branch, but they do **not** exercise non-ASCII content on a branch that returns the `ReadAllText` string directly (`<?xml` or module). So the fix appears correct, but one of the two repaired read sites is verified mainly by inspection and BCL contract, not by a targeted hermetic test.

2. **Provider registration sufficiency.**  
As production code, registering `CodePagesEncodingProvider.Instance` in `SaveAsTextTranslator`’s static constructor is sufficient for normal use of this type. There is no ordinary path to `BuildPayload` that can bypass type initialization:

- The public entry points are the static factory properties (`Forms`, `Reports`, `Macros`, `Modules`) plus the instance `Translate` method.
- Obtaining one of those singleton instances triggers type initialization.
- Calling an instance method also occurs only after type initialization has completed.
- Even reflective instance creation would still trigger the type initializer before first instance use under normal CLR semantics.

So I do **not** see a real production path where `Encoding.GetEncoding(1252)` inside `BuildPayload` runs before the static constructor registration.

However, the new tests undercut the claim that this host-independence is now pinned. `SaveAsTextTranslatorTests` registers the provider itself in its own static constructor, and that registration is global for the process. That means the tests do **not** demonstrate that the translator’s own static constructor is necessary or sufficient for a host that never went through `Main`. If someone later removed the translator’s static registration, these tests would likely remain green in the same test process. So: code sufficiency looks fine; test sufficiency on this point does not.

3. **ASCII-stability / no golden regression.**  
The ASCII-stability argument is sound. Bytes `0x00` through `0x7F` decode identically under UTF-8 and Windows-1252, so for truly all-ASCII SaveAsText fixtures:

- `ReadAllText(file)` and `ReadAllText(file, cp1252)` produce the same .NET string.
- `ReadAllLines(file)` and `ReadAllLines(file, cp1252)` produce the same line sequence.
- Downstream parser behavior and JSON serialization should therefore be unchanged.
- Because BOM detection is preserved, BOMmed UTF-8/UTF-16 inputs also retain prior behavior rather than being forced to cp1252.

Given that, “no golden regeneration expected” is a credible by-construction claim, assuming the committed HomeHealthCare fixtures really are ASCII-only as stated. I cannot independently re-scan the fixture corpus from the prompt alone, so that assumption remains external, but the encoding argument itself is correct.

Skipping the COM golden regression is also acceptable for this narrowly scoped change. The modified logic is purely file-to-file decode/routing below the COM layer, and the hermetic tests do hit the translator seam directly. A COM run would add confidence, but it is not required to justify correctness on an ASCII-stable, non-COM code path.

4. **Test adequacy (F-1-8).**  
The new tests do a good job on the basic structural branches:

- `<?xml` routes to Macro.
- `Attribute VB_Name` routes to Module.
- `Option ` routes to Module.
- `keepRoot == true` is covered via Macros block-format.
- `keepRoot == false` / first-child promotion is covered via Forms block-format.
- The cp1252 caption assertions deserialize JSON and compare decoded .NET strings, which is the right assertion level.

That said, I do not think this is fully closeout-grade yet. Two concrete gaps remain:

- **Non-ASCII is not tested on the `ReadAllText` return paths.**  
  The cp1252 regression tests both use a Forms block-format file. That ultimately validates the `ReadAllLines(file, cp1252)` path, because the non-ASCII caption is consumed by `AccessTextParser.ParseBlockFormat`. If `ReadAllText` were accidentally reverted to UTF-8 default later while `ReadAllLines` stayed fixed, the current cp1252 tests would still pass. There is no accented module/VBA or XML test to pin `VbaCode = text` / `XmlContent = text`.

- **BOM precedence is not hermetically pinned.**  
  The prose correctly relies on the BCL overload honoring BOMs, but there is no UTF-8-BOM or UTF-16-BOM fixture proving the translator still respects BOM-indicated encoding instead of blindly forcing cp1252. I would classify this as a minor but real test gap because BOM preservation is part of the claimed contract.

A third, related posture issue: because the test class globally registers the code-pages provider up front, it does not actually verify the translator’s static-constructor registration behavior.

So the tests are materially better than before, but not fully exhaustive for the exact claims this session makes.

5. **Scope discipline / no weakened assertions.**  
The production diff stays within the declared P1b boundary. The only production file changed is `SaveAsTextTranslator.cs`, and the only added test file is the translator hermetic suite. The manifest path, CLI parse path, query decode, output BOM handling, and other P2 items are untouched. I do not see scope creep in the implementation.

I also do not see existing assertions being weakened elsewhere to force green. Adjacent parser tests staying green is a good sign. The one qualification is in the **new** tests: their up-front `Encoding.RegisterProvider(...)` makes them less discriminating than the session narrative claims, because they no longer prove the translator can self-bootstrap code-page support in a non-CLI host. That is a test-posture weakness, not a production-scope violation.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs",
      "summary": "The test class globally registers CodePagesEncodingProvider before exercising the translator, so it does not actually verify that SaveAsTextTranslator's own static-constructor registration is sufficient for non-CLI hosts.",
      "fix": "Avoid process-global pre-registration in the test path under test; build the needed input bytes manually or in an isolated helper and add a test that calls Translate before any explicit Encoding.RegisterProvider in the test process."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs",
      "summary": "No non-ASCII fixture exercises a branch that returns the ReadAllText payload directly, so a regression reverting only the ReadAllText site to UTF-8 would still pass.",
      "fix": "Add at least one cp1252-encoded Module or XML fixture containing accented text and assert the decoded VbaCode or XmlContent string matches the expected characters with no U+FFFD."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs",
      "summary": "The advertised BOM-detect-then-cp1252 behavior is validated only by code inspection, not by a hermetic UTF-8/UTF-16 BOM test.",
      "fix": "Add a fixture with a UTF-8 BOM or UTF-16 BOM plus non-ASCII content and verify Translate preserves BOM-selected decoding rather than treating the file as cp1252."
    }
  ]
}
```