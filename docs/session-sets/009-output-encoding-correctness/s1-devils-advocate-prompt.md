# Interim devil's-advocate review prompt — 009 Session 1 (F-1-1 cp1252 decode + F-1-8 pins)

> **How to run (partially manual):** paste the prompt below into the in-IDE
> orchestrator pair — **Gemini Pro** AND **GPT 5.4** (via GitHub Copilot),
> separately, one verdict each. This is the subscription-paid cross-provider
> interim pass (NOT `route()`), additive to the already-completed routed
> `session-verification`. Save each raw verdict beside this file as
> `s1-external-verification-gemini.md` and `s1-external-verification-gpt-5.4.md`,
> then Opus reconciles both against the actual tree (empirical checks win over
> reasoning — the discipline cuts both ways). GPT running real probes (writing
> bytes, grepping fixtures, building) is stronger evidence than inspection;
> prefer it.

---

## PROMPT (paste verbatim into Gemini Pro, then GPT 5.4)

> *Bias cautions: This prompt was authored by an AI agent (Claude Opus 4.8) that
> implemented the very work it is asking you to review and may have an opinion on
> the answer. Its framing may inadvertently constrain you to in-scope
> refinements when the right answer is to question the scope. The work may be
> presented as further along than it should be. Before answering as posed,
> briefly check whether this is the right question. If a different question would
> be more useful, answer that one too.*

You are a **devil's-advocate** reviewer. Your **null hypothesis is that this
change is wrong, incomplete, or unsafe to build on.** Do not give a balanced
summary — try to *break* each claim, and only concede a point once you have
actively failed to falsify it. Where you can, **run an empirical probe** (write
the bytes, read them back, grep the fixtures, build the project) rather than
reasoning from the source — a probe that contradicts the claim is worth more
than any argument.

### What was changed (the work under review)

Repo: `dabbler-access-harvester` (C# / .NET 10). Branch `main`, commits
`2067c9f`..`c515465`.

**Finding F-1-1 (Major, from the 006 review).** `SaveAsTextTranslator.BuildPayload`
(`AccessHarvester.Core/Translation/SaveAsTextTranslator.cs`) read Access
SaveAsText `.txt` dumps with `File.ReadAllText(file)` / `File.ReadAllLines(file)`
— the .NET default is UTF-8. SaveAsText output is Windows-1252 (ANSI), so byte
`0xE9` ("é") decoded to U+FFFD: a one-way corruption of every non-ASCII caption,
label, validation string, and VBA literal flowing into the kb/ and ir/ tiers the
downstream AI consumer reads.

**The fix:** both read sites now pass `Encoding.GetEncoding(1252)`; a static
constructor registers `CodePagesEncodingProvider.Instance`. The claim is that
`File.ReadAllText(path, encoding)` still detects a BOM (its StreamReader uses
`detectEncodingFromByteOrderMarks: true`), so a UTF-8/UTF-16 BOM is honoured and
**only the no-BOM case** falls back to cp1252 — matching the manifest path
(`AccessHarvester.Core/Pipeline.cs`, which also reads via `GetEncoding(1252)`)
and the CLI parse path (`RunParse` in `AccessHarvester.Cli/Program.cs:581-600`,
which does an explicit `EF BB BF` / `FF FE` byte check then cp1252).

**Finding F-1-8 (test posture).** New hermetic
`tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs` (9 facts): one per
signature-routing branch (`<?xml`→Macro, `Attribute VB_Name`→Module,
`Option `→Module), the `keepRoot` flag (Macros vs Forms), and the F-1-1 lock —
cp1252 round-trips on **both** read sites (block-format `ReadAllLines`, Module
`ReadAllText`), an end-to-end caption, and a UTF-8-BOM-precedence test.

**Claims made at closeout (all four are targets for you to falsify):**
1. The fix makes the translator decode SaveAsText correctly as cp1252 while
   preserving BOM detection, and now *agrees* with the manifest path on the same
   bytes (killing the cross-surface determinism violation F-1-1 described).
2. **No golden regeneration is needed** — the committed HomeHealthCare fixtures
   are all-ASCII, so output is byte-identical under the fix. The COM
   `CanonicalGoldenRegressionTests` was therefore NOT run this session.
3. The hermetic tests *lock* the fix — they would fail if the production change
   were reverted.
4. The static-ctor provider registration is correct and necessary for non-CLI
   hosts (and consistent with the repo's lean-by-default principle).

### Specific contrarian hypotheses — steelman each, then try to prove it

**DA-1 (cross-surface divergence).** Argue that the new translator decode does
NOT actually agree with the manifest and/or `RunParse` paths on some real
SaveAsText shape — i.e. the same file still decodes to different strings across
output surfaces (the exact bug F-1-1 claims to fix). What would make this true?
Consider: StreamReader's BOM auto-detection (UTF-16 **BE**, UTF-32) is broader
than `RunParse`'s manual two-BOM check; does Access ever emit UTF-16 SaveAsText
(Forms/Reports vs Modules)? Does the OLD `File.ReadAllText(file)` *also* detect
BOMs (if so, what *exactly* changed, and is the change strictly the no-BOM
codepage)? Probe: write a UTF-16-LE-BOM and a UTF-16-BE-BOM file with a non-ASCII
char and run it through `translate` vs the manifest vs `parse`; compare.

**DA-2 ("no golden regen" is an unverified inference).** Argue that skipping
`CanonicalGoldenRegressionTests` hid a real regression. What would make this
true? The by-construction argument assumes the **raw** SaveAsText `.txt` inputs
are all-ASCII — but only the kb *output* golden was probed in 006. Probe
directly: grep the committed raw fixtures and the golden under
`samples/GoldenOutput/HomeHealthCare/` and `samples/DatabaseYaml/` for any byte
> 0x7F; if Access is available, actually run the COM golden test. If any raw
input carries a non-ASCII byte, the committed golden either already contains
U+FFFD (and the fix changes it) or the next regen diverges — either way the
"ASCII-stable, safe to skip" claim is false.

**DA-3 (tests pass for the wrong reason).** Find a reversion of the production
fix that leaves all 9 tests green — i.e. show the tests don't actually lock the
behavior. Consider: process-global `Encoding.RegisterProvider` (does another
test class in the assembly register it, making the "self-bootstrap" property
vacuous?); does the UTF-8-BOM test assert anything that would still hold if the
fix were reverted (old `ReadAllText(file)` also honours a UTF-8 BOM, so does that
test actually discriminate the fix, or only the BOM path that never regressed?).
If the BOM test is non-discriminating, the *real* F-1-1 lock rests entirely on
the two no-BOM cp1252 tests — say so.

**DA-4 (lean / wrong-layer).** Argue the static-ctor registration is dead/
redundant defensive code that violates lean-by-default (the CLI `Main` and the
manifest path already register the provider), OR that the whole fix is at the
wrong layer — e.g. the decode should happen once at the exporter/raw-write
boundary, not per-translator, so sibling read sites don't each re-litigate
encoding. What sibling read sites exist, and do any still read SaveAsText output
as UTF-8 (a gap this set scoped out)?

### Required output

For each of DA-1..DA-4: state **UPHELD / PARTIAL / OVERTURNED**, a severity
(Critical / Major / Minor) if upheld, the concrete evidence (probe output or
exact file:line), and the recommended action. Then a one-line overall verdict:
is the committed work **safe to build on as-is**, or is there a must-fix before
the P2 set (`010-output-contract-fidelity-batch`) proceeds? End with the JSON
block:

```json
{
  "verdict": "SAFE_TO_BUILD_ON" | "FIX_RECOMMENDED" | "MUST_FIX",
  "findings": [
    { "id": "DA-1", "ruling": "UPHELD|PARTIAL|OVERTURNED", "severity": "Critical|Major|Minor|None", "evidence": "<probe or file:line>", "action": "<recommended action>" }
  ]
}
```
