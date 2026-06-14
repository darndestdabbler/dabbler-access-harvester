# Next session set recommendation (authored by 011 Session 3 closeout)

**Recommendation source:** routed analysis (`route(task_type="analysis")` →
gemini-pro, $0.0025), per the "author the next-session-set recommendation via
routed analysis — never self-opine" convention. The routed verdict is recorded
verbatim below; the orchestrator id is mapped to a concrete dabbler model (the
routed model returned a garbled id, `c-4-o @ effort=4` — gemini-pro does not
know the dabbler model ids — but its substance, "a capable orchestrator for the
synthesis-heavy S1," is unambiguous).

## Next set: `012-switchboard-and-report-chain-edges` (006's P4)

Already authored this session (spec + not-started v4 stub). It is the ranked P4
successor on the critical path; both hard prerequisites are now met (P3 = 011,
this set; P2 = 010), and it delivers the two high-value Round-2 consumer
requests — **R2-1** (switchboard rows → structured navigation, DW-9) and
**R2-2** (dialog→query→report chains, DW-10). 3 sessions; adds **one** narrow,
shape-gated COM switchboard row-read (the deliberate exception 011's Hard
Constraints pushed here).

> Routed verdict (gemini-pro, verbatim): "The next set should be
> 012-switchboard-and-report-chain-edges. It is the planned P4 successor on the
> critical path, its prerequisites are now met, and it delivers the high-value
> R2-1 and R2-2 features. While the DW-11 defect is valid, it is small, off-path,
> and can be addressed in a subsequent focused set without blocking primary
> feature delivery. The first session of 012 is synthesis-heavy, requiring the
> generation of new shape-gated COM row-read logic, making a capable orchestrator
> essential for a strong start. NEXT SET: 012-switchboard-and-report-chain-edges.
> ORCHESTRATOR: c-4-o @ effort=4."

### Recommended Session-1 orchestrator
**Claude Code Opus 4.8 @ effort=high** (the "capable orchestrator for the
synthesis-heavy S1" the routed analysis called for). 012 S1 is genuinely
synthesis-heavy: new column-signature detection, a narrow OleDb row-read through
the `OleDbValueSampler` seam, and population of the already-modeled
`NavigationGraphInputs.Switchboard`/`Macros`/`EntryPoints` — plus a golden regen.
Sonnet @ effort=high is an acceptable cost-down alternative for the more
mechanical S2 (chain-edge producers).
**Verifier caveat:** the `session-verification` router override is calibrated for
a Claude orchestrator (→ gpt-5-4). If 012 runs under Gemini, swap the override to
a non-Google verifier first (per the verifier-selection lesson).

## Backlog NOT owned by 012 (carry forward)

- **DW-11 — `unresolved-items.json` undercount (demonstrated, golden-neutral).**
  Two layers: (A) procedure-header unresolved refs missing from the run-wide
  index but present inline; (B) module-level unresolved refs dropped at
  projection (no manifest signal — needs a schema home). Candidate: a focused
  **`unresolved-items-completeness`** set (small; P0-only). Off the R2-1/R2-2
  critical path but a real correctness defect; both proven reproducers are
  captured in `deferred-work.md` DW-11.
- **DW-12 — latent `VBA_RESOLVE_FAILURE` degradation-contract asymmetry.** Not
  currently triggerable; consumer guard documented (interpretation-guide §4).
  Fold into a future resolver-hardening set.
- **P5 `cli-input-robustness`, P6 `cleanup-zero-date-guard`** — still unauthored;
  independent (P0-only), slot anywhere.

**Suggested order:** run **012** next (critical-path P4). Slot the
`unresolved-items-completeness` set (DW-11), P5, and P6 into any gap — all are
independent of 012.
