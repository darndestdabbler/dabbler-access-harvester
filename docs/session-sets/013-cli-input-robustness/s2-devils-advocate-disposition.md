# Set 013 — whole-set devil's-advocate disposition

Human-driven adversarial review of the complete set (S1 + S2) per the prompt
`set013-devils-advocate-prompt.md`. Raw replies: `set013-devils-advocate-gpt-5-4.md`
(GPT-5.4) and `s1-devils-advocate-reply-gemini.md` (Gemini 3.1 Pro). Conducted
post-closeout 2026-06-15; the two findings both providers raised were remediated
in a follow-up commit (the set's routed `session-verification` had already
returned VERIFIED — this is the non-overlapping-blind-spot pass paying off).

| Item | GPT | Gemini | Disposition |
|---|---|---|---|
| 1 — F-1-3 `--types` validation | UPHELD | UPHELD | No change. |
| 2 — F-1-2 sanitisation + collision guard | UPHELD | CHALLENGED (data loss) | **No change — by design.** The set-specific constraint mandated "reportable condition, not a silent overwrite." The current behaviour *reports* the collision (exit 3 + message) — it is a disclosed per-object failure, not silent data loss. GPT endorsed it as the contracted, honest behaviour. Gemini's suffix-on-collision (`_1.txt`) is a different (silent-recovery) design the spec deliberately did not choose. |
| 3 — O-1-4 shared parser | UPHELD | CHALLENGED (value starting with `--`) | **No change — pre-existing + workaround.** All seven original copies used the same `--`-prefix heuristic; a path literally starting with `--` is representable absolute or `./`/`.\`-prefixed. GPT could not turn it into a concrete regression. (A future `--key=value` / `--` terminator is a larger CLI-syntax change, out of scope.) |
| 4 — O-1-7 dispatch table | UPHELD | UPHELD | No change. |
| 5 — F-1-5 connection strings | UPHELD | UPHELD | No change. |
| 6 — F-1-6 bracket tokenisation | **CHALLENGED** (escaped `]]`) | **CHALLENGED (High)** (escaped `]]`) | **FIXED.** Both providers found that a doubled `]]` (Jet/Access escaped literal `]`) was misparsed — GPT reproduced `PARAMETERS [a]]b] Text;` → wrong name `a`/type `]b] Text`; Gemini added that a `,`/`;` *after* the `]]` would mis-split because the depth scanners zeroed depth on the first `]`. Fixed in all three sites (the two depth scanners + the per-entry name parse via `FindClosingBracket` + `]]`→`]` unescape). New pins: `ParseParameters_EscapedBracketInName_*`. Re-proven golden-neutral byte-level (HHC has no `]]`). |
| 7 — DW-15 `identify-cleanup` exit 2 | CHALLENGED (Minor) | CHALLENGED | **FIXED (human-approved).** Both argued exit 2 for a usage mistake defeats the set's exit-code-honesty mandate. Normalised onto the CLI-wide contract: usage errors → 1, unexpected → 2 (directory-not-found stays 1, matching `validate`). DW-15 → CLOSED. Pinned: `identify-cleanup` joined the uniform exit-1 theory + two added usage-error pins. |
| X-session 1 — F-1-2 collision `Ordinal`/`OrdinalIgnoreCase` | UPHELD | (covered by Item 2) | No change. |
| X-session 2 — S1 no-regen claim | UPHELD | — | No change (narrow corpus-neutrality claim is supportable). |
| X-session 3 — F-1-6 single-corpus proof | CHALLENGED | (= Item 6) | Resolved by the Item 6 fix; the golden-neutral claim was corpus-neutrality, not general correctness — the general-correctness gap (`]]`) is now closed. |
| X-session 4 — accepted-option-set preservation | UPHELD (exhaustive) | — | No change (GPT verified every `opts[...]` read is in its command's table). |

## Net result

Two real defects the routed `session-verification` (VERIFIED) had missed, both
caught by the adversarial pair, both remediated: the **escaped-`]]` PARAMETERS
misparse** (a correctness bug in shipped parameter metadata) and the
**`identify-cleanup` exit-code inconsistency** (DW-15, closed). The four
no-change items are each either spec-mandated design (F-1-2 fail-honest) or a
pre-existing limitation with an easy workaround (O-1-4 `--`-value), with the
no-change rationale recorded above. Validation after remediation: hermetic
`QueryMetadataExtractorTests` 19/19, `CliUnknownSwitchTests` 14/14, COM golden +
manifest conformance 6/6 (F-1-6 still golden-neutral byte-level).
