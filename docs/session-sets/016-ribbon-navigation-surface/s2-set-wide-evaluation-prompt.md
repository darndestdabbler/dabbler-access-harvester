# Set-wide evaluation prompt — `016-ribbon-navigation-surface` (GPT + Gemini, in-IDE)

> **For the human to run** in the in-IDE GPT and Gemini orchestrators as the
> **primary** adversarial review of the *entire* session set (per the 011/015
> precedent where the in-IDE devil's-advocate pair caught findings the routed
> verification missed). Authored at the 016 S2 closeout. Paste this prompt into
> each orchestrator with the repo open; they have full file access — have them
> READ the cited files rather than trust this summary. The goal is to
> **disprove** the "VERIFIED" verdicts, not confirm them.
>
> Both sessions were closed VERIFIED by routed gpt-5-4. S1 took 3 rounds
> (empty→null attr contract, dropped-content fidelity, tie-break determinism,
> signature-only detection — all disposed/fixed). S2 passed R1 clean. Treat a
> clean routed pass as a reason for MORE scrutiny here, not less.

## What the set shipped (DW-13)

A two-session producer that takes a Microsoft Access custom ribbon
(`USysRibbons` customUI XML) all the way to a resolved navigation surface:

- **S1** — shape-gated `USysRibbons (RibbonName, RibbonXML)` COM/DAO row-read
  (`AccessHarvester.Core/Extraction/RibbonExporter.cs`,
  `RibbonSignatureDetector.cs`) → `raw/Navigation/ribbon-xml.json`; a pure
  namespace-tolerant `RibbonXmlParser`
  (`AccessHarvester.Pipeline/Enrichment/RibbonXmlParser.cs`) → recursive
  `RibbonStructure` (`NavigationGraph.cs`); a pure `RibbonStructureBuilder`;
  wired via `Pipeline.BuildNavigationInputs` so `NavigationGraph.Ribbon` is
  populated. HHC has no ribbon → null → golden-neutral.
- **S2** — `NavigationGraph.RibbonEntryPoints` (new dedicated collection):
  every control `onAction` + the root `onLoad` resolved against the harvested
  VBA modules into a `RibbonEntryPoint { Kind, CallbackName, ControlType,
  ControlId, ControlIdMso, ControlLabel, Resolution (Resolved|Unresolved|
  Ambiguous), ResolvedProcedure }`. Derivation in
  `NavigationGraphBuilder.BuildRibbonEntryPoints` / `BuildProcedureIndex`.
  Design consensus: `s2-ribbon-entry-point-consensus.md`.

Schema: `docs/test-cases/harvester-schema.v1.1.json` (`RibbonStructure`,
`RibbonEntryPoint` `$defs`). Validation: 952 hermetic Pipeline tests; HHC golden
reproduced byte-level by a fresh COM run; NorthwindDevEdition validated
**locally** (proprietary, never committed) via `NorthwindLocalInvariantTests`.

## Adversarial questions — try to find a real defect

### A. Faithful-mechanical fidelity (the core principle)
1. **Resolution semantics.** S2 matches `onAction`/`onLoad` to a procedure by
   **name only, case-insensitive**, across ALL modules, ignoring signature and
   visibility. Is there a realistic Access ribbon where this mis-classifies —
   e.g. a callback name that also happens to be a private helper in an unrelated
   module, producing a spurious `Ambiguous`? Is `Ambiguous`-with-no-module the
   right call there, or does it hide the *real* (public, correctly-signed)
   target? Should visibility/signature be a tiebreaker, or is that interpretation
   the set rightly refused? Argue both sides.
2. **`onLoad` inclusion.** S2 resolves the root `onLoad` as a `RibbonEntryPoint`
   (`Kind=OnLoad`), which DW-20 originally listed as deferred. Is this a
   justified consensus-backed inclusion, or scope creep past the spec's literal
   "for each `RibbonControl.OnAction`"? Did narrowing DW-20 leave the
   deferred-work ledger accurate?
3. **Dropped callbacks.** The `get*` dynamic callbacks (DW-20) and non-`<tabs>`
   customUI sections (DW-21) are NOT surfaced as entry points. Is the
   raw-tier-preserves-everything claim actually true (read `RibbonExporter` —
   does it write the verbatim `RibbonXML`)? Could a consumer be misled into
   thinking `RibbonEntryPoints` is the complete ribbon→VBA edge set?

### B. Determinism & golden neutrality
4. **Determinism.** Output order is the S1 tree walk (onLoad → tabs/groups/
   controls DFS → contextual sets). Is the S1 tree itself byte-deterministic
   across COM runs (DAO row order, XML attribute order)? Resolution claims to be
   order-independent — verify `BuildProcedureIndex` cannot produce a different
   `ResolvedProcedure` depending on module iteration order.
5. **Golden leak.** `RibbonEntryPoints` is nullable, null when `Ribbon` is null,
   omitted via `WhenWritingNull`. Find ANY path where a non-ribbon manifest could
   gain `"RibbonEntryPoints": []` or where the HHC golden could drift. (The fresh
   COM golden reproduction passed — try to construct a counterexample anyway.)

### C. Cross-session & contract integrity
6. **S1 claims re-tested.** S1's R1 raised empty→null attribute contract,
   silently-dropped customUI content, tie-break determinism, and signature-only
   detection. Were the *dispositions* (some accepted-as-rejected, some fixed)
   actually sound, or did a closed-on-reasoning finding leave a real gap S2 now
   builds on? Specifically re-test: does signature-only `USysRibbons` detection
   risk a false ribbon on an unrelated table with `RibbonName`+`RibbonXML`
   columns, and would that then produce bogus `RibbonEntryPoints`?
7. **Schema contract.** Is the `RibbonEntryPoint` `$def` (additive, optional, not
   in `required`) genuinely backward-compatible? Does `harvester-schema.v1.1`
   stay correct without an envelope bump? Does the manifest actually validate
   against it (the Northwind local test asserts this — is the assertion
   meaningful)?
8. **`ProcedureReference` reuse.** S2 reuses `ProcedureReference` for
   `ResolvedProcedure`. For a resolved ribbon callback `ModuleName` is always
   set; for macro `RunCode` entries it is null. Is overloading the same type
   across two different "module known vs not" semantics a latent consumer trap?

### D. Anything else
9. What would a downstream AI consumer of this manifest get WRONG about the
   application's ribbon navigation if it trusted `RibbonEntryPoints` literally?
   Name the single most likely misinterpretation.

## Output format
For each finding: severity (Critical/Major/Minor), file:location, one-sentence
summary, and a concrete fix. If you genuinely find nothing, say so explicitly
and name the one area you were least able to disprove. Do not pad.
