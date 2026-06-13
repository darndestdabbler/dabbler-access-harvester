# Change Log — 009-output-encoding-correctness

**Set:** `009-output-encoding-correctness` (006 P1b) · **Sessions:** 1 of 1 ·
**Completed:** 2026-06-13 · **Orchestrator:** Claude (Opus 4.8) ·
**Verification:** gpt-5-4 (cross-provider), R1 ISSUES_FOUND → R2 VERIFIED

## Session 1 of 1 — Translator cp1252 Decode Fix And Hermetic Encoding Pins

Session 1 is the sole (final) session of this set; it delivers the entire P1b
scope below and closes the set complete.

## What shipped

The kb/ → ir/ pipeline no longer silently corrupts non-ASCII text. Both
findings from the 006 review's P1b are implemented:

### F-1-1 (Major) — translator decodes SaveAsText as Windows-1252

`SaveAsTextTranslator.BuildPayload` read Access SaveAsText `.txt` dumps with
`File.ReadAllText(file)` / `File.ReadAllLines(file)`, which default to UTF-8.
SaveAsText output is Windows-1252 (ANSI), so a byte like `0xE9` ("é") decoded
to U+FFFD — a one-way corruption of every non-ASCII caption, label, validation
string, and VBA string literal flowing into kb/ and ir/ (the tiers the
downstream AI consumer reads). The same caption could also differ between
`manifest.json` (decoded correctly) and the ir/ file — a determinism/contract
violation across output surfaces. Invisible on the all-ASCII committed
HomeHealthCare fixtures; certain on production Access apps.

**Re-grounded with a fresh probe** (2026-06-13): cp1252 bytes `43 E9 6C` read
with the .NET default → `C�l` (U+FFFD); read with cp1252 → `Cél` (U+00E9). Bug
confirmed still live before the fix.

**Fix:** `BuildPayload` now reads both sites with
`Encoding.GetEncoding(1252)`. `File.ReadAllText(path, encoding)` still detects a
BOM (StreamReader `detectEncodingFromByteOrderMarks: true`), so a UTF-8/UTF-16
BOM is honoured and only the no-BOM case falls back to cp1252 — identical to the
BOM-detect-then-cp1252 strategy the CLI `parse` path (`RunParse`) and the
manifest path (`Pipeline.EmitManifestArtifacts`) already use. A static
constructor registers `CodePagesEncodingProvider.Instance` (idempotent) so the
cp1252 read works from any host, including one that never went through the CLI's
`Main`.

### F-1-8 (Minor, test posture) — hermetic translator pins

`SaveAsTextTranslator` previously appeared in zero test files; its seam was
exercised only behind Access-gated integration runs with all-ASCII fixtures.
New always-running `SaveAsTextTranslatorTests` (9 facts) drive the real
file-to-file `Translate` seam:

- one per signature-routing branch — `<?xml`→Macro, `Attribute VB_Name`→Module,
  `Option `→Module;
- the `keepRoot` flag — Macros keep-root vs Forms first-child promotion;
- the F-1-1 lock — the canonical `43 E9 6C`→"Cél" cp1252 round-trip (block-format
  `ReadAllLines` path), an accented Module literal (the `ReadAllText` path), an
  end-to-end `Caption ="Re/Création"` asserting no U+FFFD reaches the kb JSON,
  and a UTF-8-BOM test proving BOM precedence over the cp1252 fallback.

Inputs are built from literal cp1252 bytes (no `GetEncoding(1252)` in test
setup) so the tests exercise the translator self-bootstrapping cp1252 support
rather than masking it.

## Files

- `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs` — cp1252 read on
  both sites; static-ctor provider registration; explanatory comments.
- `tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs` — new, 9 hermetic
  facts.

## Validation

- `dotnet build` (solution): 0 errors.
- `SaveAsTextTranslatorTests`: 9/9 pass. Adjacent `AccessTextParserTests`: 5/5
  still green.
- **No golden regeneration** — ASCII bytes (0x00–0x7F) decode identically under
  cp1252 and UTF-8 and BOM detection is preserved, so the all-ASCII committed
  HomeHealthCare golden is byte-stable by construction (verifier-endorsed). The
  COM `CanonicalGoldenRegressionTests` was deliberately not run (the change is
  pure file-to-file logic below the COM layer). No `.accdb` mutations.

## Scope discipline

P1b only (translator decode + hermetic pins). The manifest path, parser, and
output-file BOM handling (F-2-3) were left untouched — they belong to P2
(`010-output-contract-fidelity-batch`, authored this session). No `DW-<n>`
entries were opened (no in-scope sibling read-site shared the bug).

## Next set

`010-output-contract-fidelity-batch` (006 P2, 2 sessions) authored this session
(spec + not-started v4 state stub).
