# S1 Column-Base Decision — Cross-Provider Consensus (DW-26)

**Date:** 2026-06-15  
**Question:** Option A (normalise columns to 1-based at projection, golden-affecting) vs Option B (relax schema to minimum:0, 0-based columns, golden-neutral).  
**Orchestrator default lean (per spec):** Option A.

## DECISION (recorded before any code change)

**Outcome: OPTION A — normalise source-location columns to 1-based at projection.**

- **Cross-provider consensus:** UNANIMOUS for Option A (gpt-5-4 + gemini-pro, both
  `VERDICT: OPTION A`, full reasoning below).
- **Human sign-off:** APPROVED Option A on 2026-06-15 (public-contract + golden-affecting
  choice, so human sign-off was required per the spec; asked via AskUserQuestion).
- **Implementation caveats carried from consensus into the code change:**
  1. `+1` on BOTH `StartColumn` and `EndColumn` in `BuildLocation` (width-preserving;
     half-open `[0,11)` → `[1,12)`). Synthetic top-level stays `=1`.
  2. Document explicitly that `EndColumn` is **exclusive** (one past the last char), now
     1-based — in the schema `$def` description and the interpretation guide, because a
     reader may otherwise assume a 1-based end column is inclusive.
  3. Add a contract/release note even though the schema version is unchanged (Option A is
     a producer bug-fix against the existing `minimum:1` contract, but it changes
     serialized column values, so consumers that inferred behaviour from observed
     manifests should be warned).
  4. Regression coverage: a token at raw AST column 0 → projects `StartColumn 1`; width
     preserved after rebasing; no remaining raw-column leak path outside `BuildLocation`
     (re-grounded: the only other column site is the hardcoded top-level `=1`).


---

## gpt-5-4

Choose Option A.

Why:

- It fixes the producer to match the existing public contract instead of redefining the contract around an implementation leak.
  - The parser’s 0-based column is an internal ANTLR detail.
  - The manifest schema already says columns are `>= 1`.
  - Lines are already 1-based.
  - The synthetic module-root is already emitted as column 1.
  - Option A makes all of that coherent.

- A uniform `line:column` convention matters more than preserving today’s accidental values.
  - Humans read `line:col` as editor coordinates.
  - A downstream AI consumer will also naturally interpret `12:1` as “line 12, first visible column,” not “line 12, zeroth column.”
  - Option B bakes in a mixed-base contract: 1-based lines, 0-based columns. That is a permanent off-by-one trap.

- Option B is more dangerous than it looks because the bad convention is often invisible.
  - Most VBA is indented, so many real 0-based columns are still `>= 1`.
  - That means manifests “look” 1-based until a leading token appears.
  - StudentICTDatabase already proved this: the problem stays latent until column 0 shows up.
  - That kind of latent ambiguity is worse than a one-time mechanical correction.

- The churn cost of A is acceptable.
  - It is deterministic and mechanical: `StartColumn += 1`, `EndColumn += 1` for `BuildLocation`-derived spans.
  - The 32 synthetic top-level nodes stay at 1.
  - The semantic meaning of spans does not change; width is preserved because the range stays half-open.
  - This is exactly the kind of golden regen that is safe to do once and then stop thinking about.

- Option A reduces long-term maintenance friction.
  - It removes the need for `Math.Max(1, ...)`-style coercions.
  - It makes schema validation honest instead of lucky.
  - It prevents every future consumer from needing special-case base handling.

What the framing slightly undersells:

- The most important documentation point is not just “columns are 1-based”; it is “`EndColumn` is exclusive.”
  - After rebasing, `[0,11)` becomes `[1,12)`.
  - That is correct and width-preserving, but it must be explicit in schema/docs because some readers will assume 1-based end columns are inclusive.

- Because serialized values change, this deserves a clear release note / contract note even if you keep the same schema version.
  - Strictly speaking, A is a producer bug fix against the current schema.
  - Practically, any consumer that inferred behavior from observed manifests should be warned.

- You should add regression coverage for:
  - a token at raw column 0,
  - width preservation after rebasing,
  - no remaining raw-column leak paths outside `BuildLocation`.

If you ever need machine-native raw coordinates later, add separate 0-based offset fields. Do not make `line:column` mixed-base just to preserve parser internals.

VERDICT: OPTION A

---

## gemini-pro

Option A is the correct engineering decision.

The producer is currently violating its public contract, as defined by the `minimum: 1` constraint in the schema. The existing golden data passes validation only by coincidence. Option B proposes enshrining this bug as a feature by changing the contract. This is incorrect. The producer must be fixed to conform to the established, more intuitive contract.

**Reasoning:**

1.  **Contract Integrity:** A producer must honor its schema. The schema dictates a 1-based column convention. The fix is to make the producer compliant (Option A), not to weaken the schema to match the buggy output (Option B).

2.  **Consistency and Convention:** A uniform 1-based `line:column` system (Option A) is the universal convention in virtually all text editors and IDEs. It is the most intuitive and least error-prone representation for any consumer, AI or human. Option B's mixed-base `(1-based line, 0-based column)` is a permanent source of confusion and off-by-one errors for all downstream clients.

3.  **Internal Coherence:** Option A resolves the existing internal inconsistency where the synthetic document root was projected as 1-based while all other tokens were 0-based. It creates a single, consistent rule for all source locations.

4.  **Cost-Benefit Analysis:** The cost of Option A is a one-time, mechanical, and verifiable golden file regeneration. This is an acceptable cost for fixing a latent bug and establishing a clear, permanent contract. The "cost" of Option B is not zero; it is a permanent transfer of complexity and risk to every downstream consumer, for the lifetime of the API. Paying the one-time internal cost is vastly superior.

The framing did not miss any significant factors. The existence of the `Math.Max(1, ...)` workaround is a clear signal that the internal 0-based source is a known friction point that should be properly resolved at the boundary (projection), not with downstream patches or by weakening the public contract.

VERDICT: OPTION A
