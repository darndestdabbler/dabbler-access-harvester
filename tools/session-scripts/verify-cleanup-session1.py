"""Step 6 cross-provider verification for harvester-object-cleanup-identifier Session 1.

Persists output to disk before any printing per project-guidance.md lesson.
"""
from __future__ import annotations

import os
import pathlib
import sys

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route, SessionLog  # noqa: E402

REPO = pathlib.Path.cwd()
SESSION_SET = REPO / "docs" / "session-sets" / "harvester-object-cleanup-identifier"
SESSION_NUMBER = 1

strategy_path = REPO / "docs" / "planning" / "object-cleanup-strategy.md"
schema_path = REPO / "AccessHarvester.Pipeline" / "Cleanup" / "object-cleanup-schema.json"
spec_path = SESSION_SET / "spec.md"

strategy_text = strategy_path.read_text(encoding="utf-8")
schema_text = schema_path.read_text(encoding="utf-8")
spec_text = spec_path.read_text(encoding="utf-8")

prose_lead = """\
Your response MUST begin with a detailed prose analysis of EACH numbered
concern below BEFORE the trailing JSON block. Do not skip the analysis.
Both the prose analysis and the JSON block are required.

This is a CLOSEOUT-GRADE verification: the strategy document and JSON
Schema you are reviewing define the contract that Sessions 2 and 3 of
this session set will implement. Errors or ambiguity here propagate
into committed code and golden output files. Take the analysis
seriously even when you ultimately return VERIFIED.

Concerns to analyze in prose, one numbered section each:

1. **Signal-family coverage.** Does the strategy doc's §3 cover the
   three signal families called out in the spec (name patterns, date
   signals, cross-reference absence) at sufficient fidelity that
   Session 2 can implement them without guesswork? Are the regex
   patterns, fallback paths, and per-object-type matrices specified
   correctly?

2. **Confidence-tier algorithm.** Strategy §4 defines a categorical
   High/Medium/Low tier based on which signals fire. Is the
   combinator unambiguous? Specifically: can a Medium finding ever
   become High via cross-reference absence (no, per §4 step 2 — but
   is that the right call)? What happens when a name pattern fires
   AND cross-reference absence fires for a non-table (no date signal
   path) — is that High or Medium?

3. **JSON Schema correctness.** Does the schema at
   `AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json`
   correctly enforce the contract described in strategy §5?
   Specifically: is the `oneOf` tagged-union pattern for `Signal`
   correctly discriminated by `signalType` const values? Are
   `additionalProperties: false` and `required` lists complete on
   every definition? Does `firedSignals` having `minItems: 1` align
   with the strategy's "no fired signal → no finding" rule?

4. **Cross-reference absence semantics.** Strategy §3.3 defines the
   per-object-type reference-type matrix. Is the matrix exhaustive
   for the object types in scope? Are there reference paths
   (e.g., a form's `OnLoad` macro reference, a table's `Description`
   field referencing another table) the matrix misses? Are
   self-references handled correctly?

5. **uat-checklist-editor mapping.** Strategy §6 maps each finding to
   a Review item. Does the mapping correctly produce a
   uat-checklist-editor-compatible JSON file? Is the `Passes: false`
   default polarity (user flips to `true` to mark stale/excludable)
   correctly aligned with the existing
   `harvester-object-use-identifier` `--exclude-cleanup` contract?
   Is the bracket-prefix convention on `Subarea` reasonable?

6. **CLI surface.** Strategy §8 specifies the
   `AccessHarvester identify-cleanup` subcommand interface. Is the
   `--in` / `--manifest` mutual-exclusion clear? Are the
   `--harvest-date`, `--threshold-days`, `--no-com`, `--json-status`
   options well-defined? Will Session 2 know how to plug this into
   `Program.cs` dispatcher?

7. **Degradation paths.** When Access COM is unavailable, when query
   SQL is missing (soft prereq `harvester-body-and-query-sql`),
   when `LastUpdated` is absent — does the strategy define
   graceful behavior? Are the degradation cases observable in the
   output (`dateSignalSource: "unavailable"`)?

After the seven prose sections, output the JSON block in this exact
format. The JSON block is mandatory — do NOT omit it.

```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or section>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }
  ]
}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.

==============================================================================
OUT-OF-SCOPE DISCLOSURE
==============================================================================

The following items are present in the working tree but are NOT part of
Session 1's deliverables — please do not flag them as issues:

- `samples/HomeHealthCare.accdb` and `samples/MiniComplex.accdb` are
  marked modified by `git status`. Per `docs/planning/lessons-learned.md`,
  Access mutates timestamps on file open and these binary diffs are
  expected merge-noise unrelated to Session 1.

- Three `dotnet test` integration tests time out at 60s under the
  test harness:
    * `AccessHarvester.Integration.Cli.CliRunTests.Run_AgainstHomeHealthCare_*`
    * `AccessHarvester.Integration.Cli.CliExtractTests.Extract_WithHomeHealthCareDb_*`
    * `AccessHarvester.Integration.Cli.CliTranslateTests.Translate_AgainstHHCRaw_*`
  Direct CLI invocation against `HomeHealthCare.accdb` completes in
  under 30s — this is a test-harness parallelism / MSACCESS lock-
  contention issue. Session 1's deliverables are documentation-only
  (`docs/planning/object-cleanup-strategy.md` and a JSON Schema file
  not yet wired into any csproj); they cannot have caused these
  failures. 42 of 45 integration tests pass; all hermetic unit tests
  pass. The remaining 3 failures are an environmental issue scheduled
  for triage separately. Treat as out-of-scope for THIS verification.

- The `harvester-object-use-identifier` strategy doc
  (`docs/planning/object-use-identification-strategy.md`) is referenced
  multiple times in the Session 1 strategy. It is the contract the
  cleanup output must align with (specifically §9
  `--exclude-cleanup` semantics). It is not changed in this session.

==============================================================================
CONTEXT: SPEC EXCERPT
==============================================================================

The active spec (`docs/session-sets/harvester-object-cleanup-identifier/spec.md`)
authoritative excerpt for Session 1:

"""

verification_prompt = (
    prose_lead
    + "\n" + spec_text
    + "\n\n==============================================================================\n"
    + "DELIVERABLE 1 OF 2: `docs/planning/object-cleanup-strategy.md`\n"
    + "==============================================================================\n\n"
    + strategy_text
    + "\n\n==============================================================================\n"
    + "DELIVERABLE 2 OF 2: `AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json`\n"
    + "==============================================================================\n\n"
    + schema_text
)

# Persist prompt to disk before any other side effect.
prompt_path = REPO / "tmp" / "session-1-verification-prompt.md"
prompt_path.parent.mkdir(parents=True, exist_ok=True)
prompt_path.write_text(verification_prompt, encoding="utf-8")

result = route(
    content=verification_prompt,
    task_type="session-verification",
    complexity_hint=70,
    context="Closeout-grade verification of strategy doc + JSON Schema for harvester-object-cleanup-identifier Session 1. Pre-existing CLI integration test failures disclosed in prompt.",
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

# Persist response to disk before any printing (cp1252 safety).
response_path = REPO / "tmp" / "session-1-verification-response.md"
response_path.write_text(result.content, encoding="utf-8")

# Save via SessionLog so the official review file is created.
log = SessionLog(str(SESSION_SET))
log.save_session_review(
    session_number=SESSION_NUMBER,
    review_text=result.content,
    round_number=1,
)

print(f"Verifier model: {result.model_name}")
print(f"Cost (USD): {result.cost_usd}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Prompt saved: {prompt_path}")
print(f"Response saved: {response_path}")
print(f"Session review saved by SessionLog")
print("=" * 70)
print(result.content[:2000])
print("...[truncated]..." if len(result.content) > 2000 else "")
