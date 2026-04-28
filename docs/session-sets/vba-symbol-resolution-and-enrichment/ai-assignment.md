# AI Assignment — vba-symbol-resolution-and-enrichment

## Session 1: Symbol Resolution PoC (frmCompanyDetail + modStrings)

### Recommended orchestrator
claude-code (Claude Opus 4.7) @ effort=high

### Rationale
This session requires careful code parsing and cross-module reference tracking. High effort with Opus ensures robust symbol resolution, dependency graph construction, and graceful handling of edge cases (unresolved identifiers, circular refs, external libraries).

### Estimated routed cost
low (no routing expected; all work is orchestrator-direct code/analysis)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Extract VBA code from `raw/Forms/frmCompanyDetail.txt` and `raw/ModulesAndClasses/modStrings.txt`. | Orchestrator-direct |
| 2 | Parse both modules to build symbol tables (procedures, variables, enums, constants, external refs). | Orchestrator-direct |
| 3 | Build dependency graph: identify all cross-module calls and internal references. | Orchestrator-direct |
| 4 | Resolve all identifiers; annotate unresolved with reason (enum value, external lib, late-bound, etc.). | Orchestrator-direct |
| 5 | Generate report: symbol tables + dependency graph + unresolved-identifier analysis. | Orchestrator-direct |
| 6 | Create annotated IR fragment showing proposed event schema with `InferredFunctionality`. | Orchestrator-direct |
| 7 | Assess feasibility for scaling to full pipeline (cost, coverage, edge cases). | Orchestrator-direct |

### Actuals (Session 1 — filled after completion)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation:**
_To be filled at Session 1 close._

---

## Session 2: Pipeline Integration (if PoC succeeds)

> Conditional: Only triggered if Session 1 PoC demonstrates feasibility and stakeholder approval.

### Recommended orchestrator
claude-code (Claude Opus 4.7) @ effort=high

### Rationale
Integrating symbol resolution into the harvester pipeline requires careful architecture decisions and thorough testing. High effort ensures correctness across the full codebase.

### Estimated routed cost
moderate (~$1–3) for validation + verification

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Design pipeline extension: where/how to inject symbol resolution into translate → enrich → emit phases. | Orchestrator-direct |
| 2 | Implement symbol resolution module (parsing, table building, reference resolution, error handling). | Orchestrator-direct |
| 3 | Integrate into harvester pipeline; apply to both gold-standard DBs. | Orchestrator-direct |
| 4 | Validate outputs: spot-check enriched IR for correctness + coverage. | Orchestrator-direct |
| 5 | Performance benchmarks: measure time + memory cost per form/module. | Orchestrator-direct |
| 6 | Run integration tests + full test suite. | Orchestrator-direct |
| 7 | Cross-provider verification. | `route(task_type="session-verification", complexity_hint=70)` |

### Actuals (Session 2 — filled after completion)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation:**
_To be filled at Session 2 close (if executed)._

---

## Handoff to Session 3 (integration-testing-and-acceptance)

Upon Session 1 PoC success + stakeholder approval, the enriched IR will be available for Session 3's comprehension-bench work:

- **Session 3 can resume** with higher-confidence IR that includes semantic annotation
- **Comprehension questions** can now ask about behavior, intent, and cross-module logic (not just structure)
- **Baseline metrics** will improve due to richer IR context for the downstream AI to reason about

If Session 1 PoC reveals insurmountable obstacles, Session 3 will proceed with un-enriched IR (lower question quality but still viable).
