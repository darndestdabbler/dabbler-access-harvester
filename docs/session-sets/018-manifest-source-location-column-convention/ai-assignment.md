# AI Assignment & Actuals — Set 018 (DW-26)

**Orchestrator:** Claude Opus 4.8 (`claude-opus-4-8`, claude-code), effort high.
**Tier:** Full (1/1). **Verification mode:** out-of-band-or-none.

## Routed / cross-provider calls (actuals)
| Purpose | Task type | Models | Outcome |
|---|---|---|---|
| Decision-time consensus on the column base (Option A vs B) | `architecture` | gpt-5-4 + gemini-pro | **UNANIMOUS Option A** (+ human sign-off via AskUserQuestion) |
| End-of-session verification R1 | `session-verification` | gpt-5-4 (cross-provider from Claude) | ISSUES_FOUND → 1 Minor (comment), fixed |
| Devil's-advocate pair R2 (PRIMARY) | in-IDE (human-run, full repo access) | gpt-5-4 + gemini-3.1-pro | ISSUES_FOUND → 1 Major (doc accuracy), fixed; core work UPHELD by both |

## Delegation notes
- The convention decision (public-contract + golden-affecting) went through cross-provider
  consensus **before** any code change, per Decision-time consensus; human sign-off was
  required and obtained.
- The substantive code change (BuildLocation `+1`) is mechanical and single-site (~4 lines)
  → handled directly; the golden delta was proven mechanically (structured JSON walk).
- The in-IDE devil's-advocate pair (run by the human) was the spec-mandated **primary**
  review and caught the index-column-base mischaracterisation the routed pass did not.

## Effort / sequencing actuals
- Re-grounding confirmed statically (SourceLocation 0-based column / 1-based line; half-open
  exclusive End; BuildLocation + hardcoded top-level the only two column sites; golden
  validates by luck) + a fresh **local** StudentICT COM run confirming schema-invalidity
  solely on the `StartColumn [minimum]` class.
- Validation run in **targeted, foreground, per-project** batches after a whole-solution
  `dotnet test` wedged a COM testhost ~3h (reaped via WMI Terminate). See change-log Step-9
  note.
