"""Cross-engine input on the property-sheet criteria-style wrapping decision.

Item 2 surfaced from the close of `harvester-finish-canonical-parse-success-report`:
the current implementation parses property-sheet expressions (ValidationRule,
DefaultValue, Filter, OrderBy) by wrapping each value in
`SELECT 1 FROM t WHERE <value>` and routing through the Jet SQL grammar.
For criteria-style fragments like `Is Not Null` / `In (...)` / `Between ...`,
the wrap fails because there's no left operand. A heuristic detects leading
criteria tokens (`Is`, `Like`, `In`, `Between`, `Not`, operators) and prepends
a synthetic `x ` so `Is Not Null` becomes `x Is Not Null`, which parses.

The human's recommended refinement is to expose a real `criteriaFragment`
entry rule in JetSql.g4 plus a per-value classifier so the harness picks
the right entry rule. The human asked for cross-engine input from gpt-5-4
(OpenAI) and gemini-pro (Google) before committing to the refinement.

This script routes the SAME framing to both engines and saves each response
to disk before any display side effect (Persist-Before-Display Convention).
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import query

OUT_DIR = Path("docs/strategy-input")
OUT_DIR.mkdir(parents=True, exist_ok=True)

framing = """\
You are advising on a small architecture decision in the Dabbler Access
Harvester project. The harvester's purpose is to extract Microsoft Access
metadata at sufficient structural fidelity that a downstream AI consumer
can interpret and document the original application. The downstream
consumer is an AI agent, not a human reviewer — outputs are JSON; precision,
completeness, and machine-readability matter; layouts and styling do not.

# The question

Should the property-sheet-expression parser be **refined into a real
grammar entry rule for criteria fragments**, or is the current
leading-token-sniff heuristic good enough? Are there alternatives the
team has not considered?

# Background

Access stores some property-sheet values as bare strings. These come in
two shapes:

1. **Full expressions** — `>0`, `Like "*@*"`, `Len([Foo])>0`. These are
   complete boolean / value expressions.
2. **Criteria-style fragments** — `Is Not Null`, `In ("M","F","X")`,
   `Between 1 And 100`, `Like "*Smith*"`. These are RHS-only fragments
   intended to be predicates against an implicit `[ThisField]` left
   operand. Access evaluates them as `[ThisField] <fragment>` at runtime.

The current implementation in
`tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs`
wraps each value in `SELECT 1 FROM t WHERE <value>` and parses through
the Jet SQL grammar (`JetSql.g4`). For full expressions this works
directly. For criteria-style fragments, a naked `WHERE Is Not Null`
doesn't parse — so a `NormaliseCriteria` helper sniffs the leading
token and prepends a synthetic `x ` operand:

```
"Is Not Null"           -> "x Is Not Null"
"In (\"M\",\"F\",\"X\")" -> "x In (\"M\",\"F\",\"X\")"
">5 And <10"             -> "x >5 And <10"
">0"                    -> ">0"             (full expression — no prepend)
```

The sniff matches leading `Is`, `Like`, `In`, `Between`, `Not`, or a
leading operator character.

# Known limitations of the current heuristic

1. **Multi-form criteria with no leading marker** — e.g., `"M" Or "F"`
   meaning `[Field] = "M" Or [Field] = "F"`. The sniff misses these and
   they parse as a literal string expression, counted as `parsedOk` but
   with the wrong semantic interpretation.
2. **Degenerate compound criteria** — `5 And 10` (meaning
   `[Field] = 5 And [Field] = 10` as boolean per Access syntax) is
   missed by the sniff.
3. **No structural distinction in the report** — a `parsedOk` count is
   the sum of "parsed as full expression" + "parsed as fragment after
   synthetic prepend", with no way for the downstream consumer to tell
   the two apart.

# Options the human has identified

**(a) Refine to a real grammar entry rule** (human's stated preference).
Add a `criteriaFragment` entry rule to `JetSql.g4` (probably aliased
to the existing `predicate` non-terminal with an injected implicit
left-operand) and a classifier in the harness that picks the right
entry rule per property-sheet value. Probably a single session set,
2-4 sessions sized: grammar amendment + test corpus + classifier
+ harness wiring + golden regeneration.

**(b) Accept the heuristic as-is.** Reports are stable; downstream
AI can still read the raw value from the YAML mirror; the
parsed-or-not signal is approximate but useful. Document the limit
and move on.

**(c) Catalogue the false-positive rate first.** Run the heuristic
against the full property-sheet corpus and hand-classify a sample
to measure how often the leading-token sniff misclassifies. Could
be a cheap routed analysis. If false-positive rate is <5%, (b) is
fine; if higher, (a) is warranted.

# What I'd like from you

1. Which option do you recommend, and why?
2. If you recommend (a), what shape should the grammar entry rule
   take? (e.g., aliased `predicate`, a new top-level rule that
   discards an implicit left operand, etc.) What edge cases does
   that shape need to handle that the heuristic currently doesn't?
3. If you recommend (b) or (c), what's the breaking point — at
   what scale or downstream-consumer demand would you flip to (a)?
4. Are there alternatives the team hasn't considered? (For
   example: skipping property-sheet parsing entirely and just
   recording the raw value with a `kind: "criteria-fragment" |
   "full-expression"` tag; or routing through a different parser
   that already handles criteria fragments natively.)

Keep your response under ~800 words. Concrete recommendation +
reasoning + one or two pinpoint examples beats an exhaustive
treatise.
"""

print("=" * 72)
print("Routing to gpt-5-4 (OpenAI)...")
gpt_result = query(
    model="gpt-5-4",
    content=framing,
    task_type="analysis",
    session_set="post-close-strategy-input",
    session_number=0,
)
gpt_path = OUT_DIR / "propsheet-criteria-gpt-5-4.md"
gpt_path.write_text(gpt_result.content, encoding="utf-8")
print(f"gpt-5-4: ${gpt_result.cost_usd:.4f}, "
      f"in/out tokens: {gpt_result.input_tokens}/{gpt_result.output_tokens}")
print(f"Saved to {gpt_path}")
print()

print("=" * 72)
print("Routing to gemini-pro (Google)...")
gemini_result = query(
    model="gemini-pro",
    content=framing,
    task_type="analysis",
    session_set="post-close-strategy-input",
    session_number=0,
)
gemini_path = OUT_DIR / "propsheet-criteria-gemini-pro.md"
gemini_path.write_text(gemini_result.content, encoding="utf-8")
print(f"gemini-pro: ${gemini_result.cost_usd:.4f}, "
      f"in/out tokens: {gemini_result.input_tokens}/{gemini_result.output_tokens}")
print(f"Saved to {gemini_path}")
print()

print("=" * 72)
total = gpt_result.cost_usd + gemini_result.cost_usd
print(f"Total routed cost: ${total:.4f}")
print(f"GPT response saved to: {gpt_path}")
print(f"Gemini response saved to: {gemini_path}")
