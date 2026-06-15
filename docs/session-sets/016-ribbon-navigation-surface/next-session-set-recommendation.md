# Next-session-set recommendation — after `016-ribbon-navigation-surface`

> Authored at the 016 closeout (last-session-authors-next-set). Routed
> cross-provider (gpt-5-4, `task_type=summarization`) against the **actual
> post-016 open-deferred-work state**, then reconciled by the Opus orchestrator.
> Carries the Hard Constraints block verbatim (below) per the spec.

## Recommendation: **(a) Record a consumer-driven-only STOP**

**Do not self-initiate a new session set.** `016` cleanly closes DW-13 (the
ribbon navigation surface + ribbon entry points). Every remaining open
deferred-work item is golden-neutral, non-consumer-triggered, latent, or a
quality/completeness improvement with **no demonstrated downstream need**. This
matches the repo's standing **consumer-driven-only** posture
(`project_consumer_driven_only_after_two_sets`, reached 2026-06-01): the
harvester arc is complete and new work begins only when a downstream consumer
reports a concrete need or a real DB exercises an open gap.

The correct handoff is therefore: **done unless a consumer or a real DB says
otherwise.** The pre-scoped bundles below exist so that *when* a trigger
arrives, the owning set is already scoped — not as a backlog to burn down.

## Pre-scoped bundle candidates (start only on a concrete trigger)

| Bundle | DW items | Trigger to pull in |
|---|---|---|
| **Ribbon completeness** | DW-20 + DW-21 | A consumer needs the full ribbon→VBA call graph (dynamic `get*` callbacks as entry points) or a DB customises `<qat>`/`<officeMenu>`/`<backstage>` with `onAction`. Same surface, same fixture shape, same trigger — bundle them. |
| **Navigation completeness** | DW-14 | A real DB uses a named-argument `DoCmd` open or an indexer/`With` dialog-control reference. Spans the VBA-edge layer + chain producer together (fixing only one makes the layers disagree). |
| **Encoding / SaveAsText decode hardening** | DW-5 + DW-6 | A real DB surfaces a BOM/encoding mismatch between `RunParse` and the translator/manifest paths. Centralize decode at the exporter boundary. |
| **Parser completeness: VBA edge cases** | DW-1 + DW-2 + DW-3 + DW-4 | A real corpus fails to parse on one of these grammar forms. Handle as one parser-focused set. |
| **Reference-output completeness** | DW-8 + DW-17 + DW-19 | A consumer reports that a derived reference/feed is not surfaced or is mis-attributed in `ParserOutput` / run outputs. |
| **Resolution / degradation contract** | DW-12 | Standalone, latent — pull in only if a real DB (or a parser change) produces a `VBA_RESOLVE_FAILURE` a "Resolved" reference points at. |
| **Sampling edge** | DW-16 | Standalone — the shipped analyser guard already covers the behaviourally important path. |
| **Intrinsic-constant registry** | DW-18 | Standalone, **GOLDEN-AFFECTING** (see below). |

## One item to watch first — **DW-18** (intrinsic-constant registry)

Most likely to force action first: VBA built-in constants (`vbCrLf`, `acForm`,
…) currently surface as `not-in-scope` **false-positive** unresolved references,
which affects broad real-world VBA and is the kind of thing a downstream consumer
notices quickly. It is also the **only golden-affecting** open item — fixing it
changes `unresolved-items.json` counts across corpora, so its owning set must
plan a **coordinated golden regen** (HHC golden via `GoldenCorpusRegenerator`
`AH_REGEN_GOLDEN=1` + re-run `CanonicalGoldenRegressionTests`) and re-validate
the Northwind local invariant. Pre-flag this in that set's spec.

## Hard Constraints (carry verbatim into any set that picks these up)

- **NEVER regenerate, recreate, or commit any NorthwindDevEdition-derived
  artifact.** NorthwindDevEdition is proprietary and local-only; regeneration
  reintroduces the residue sets 003–005 removed. Northwind is for **local**
  validation only.
- **Golden regeneration is HomeHealthCare-only**, via `GoldenCorpusRegenerator`
  (`AH_REGEN_GOLDEN=1`) + re-run `CanonicalGoldenRegressionTests`. Prove any
  golden delta is explained by the fix; an unexpected delta is stop-and-investigate.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on, TRX
  `<Counters>` as the completion signal; never background a `dotnet` invocation;
  sweep spurious `samples/*.accdb` diffs (`git checkout -- samples/HomeHealthCare.accdb`)
  before staging.
- **The quarantined sanitize test stays quarantined**
  (`SanitizeCommandTests.HomeHealthCare_sanitize_…`).
- **Do not weaken assertions or tests to force green.** New behaviour must be
  pinned by hermetic tests asserting it, not silently accepted.
- **Unresolvable constructs are explicit** — never a silent drop.
- **Use NorthwindDevEdition locally** to validate output-affecting work; HHC
  alone (synthetic) is insufficient for anything Northwind exercises.
