# Solution Review And Upstream Feedback Discovery Spec

> **Purpose:** A frontier-model second-opinion review of the harvester solution.
> The repo was built across ~50 session sets orchestrated primarily by Claude
> Opus; the maintainer has directed that a Claude Fable 5 orchestrator
> double-check what was built and produce prioritized, evidence-backed
> recommendations. The set also grounds and assesses the Round 2 (2026-06-12)
> downstream-consumer feedback (switchboard navigation edges, report-chain
> linkage edges, optional reviewed-input export). This is a **discovery set**:
> read-only on shipping code; every deliverable is a document.
> **Created:** 2026-06-12
> **Session Set:** `docs/session-sets/006-solution-review-and-upstream-feedback-discovery/`
> **Prerequisite:** `005-northwind-embedded-residue-remediation` (complete —
> the review runs against the NorthwindDevEdition-free baseline)
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.
> **Status:** Draft

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
totalSessions: 4
prerequisites:
  - slug: 005-northwind-embedded-residue-remediation
    condition: complete
```

> Rationale: a `-discovery` set per the authoring guide — read-only
> investigation producing written deliverables, no shipping-code changes, so
> no UAT or E2E gates apply. Full tier keeps mandatory cross-provider
> verification on each session's findings document, which doubles as a
> false-positive screen on the review itself.

---

## Why This Set, Why Now

Two converging drivers:

1. **Capability-tier second opinion.** The solution (~47K production lines
   across 10 projects, ~42K test lines) was built with Opus-class
   orchestrators. The maintainer wants a higher-capability model to re-examine
   the architecture, correctness, contracts, and test posture before further
   consumer-driven work accumulates on top of any latent defects.
2. **Round 2 consumer feedback.** The downstream migration-orchestrator
   consumer's post-1.2.0 pilot surfaced three candidate upstream changes,
   recorded verbatim in `docs/planning/harvester-upstream-feedback.md` →
   *Round 2 (2026-06-12)*. Per the standing lesson (*ground consumer reports
   against fresh output*), these must be verified against a fresh harvester
   run before any implementation work is authored.

The repo is in consumer-driven-only posture; this set is consumer-driven on
both counts (explicit maintainer request + downstream feedback).

## Done Looks Like

- Every reviewed area has a findings document whose findings carry severity,
  evidence pointers, and a proven-vs-inferred label, screened by
  cross-provider verification.
- The three Round 2 feedback items are confirmed or refuted against fresh
  HomeHealthCare output, each with a design sketch (R2-1, R2-2) or an
  ownership recommendation (R2-3).
- A single consolidated `review-recommendations.md` ranks everything —
  review findings and feedback items — into proposed follow-up session sets
  with sizing, sequenced against the standing integration-test-rebase
  recommendation from set `005`.
- The Round 2 status table in `docs/planning/harvester-upstream-feedback.md`
  reflects the assessment outcomes.
- No shipping code, schema, golden, or test file has changed.

## Orchestrator Assignment And Delegation Deviation

The maintainer has explicitly assigned **Claude Fable 5** (`claude-fable-5`)
as orchestrator for all four sessions — the point of the set is this model's
judgment. Accordingly:

- The review analysis itself is authored **in-conversation**, not via
  `route(task_type="code-review")`. This is an explicit, human-directed
  deviation from Delegation Discipline (precedence rule 1), consistent with
  the existing convention *"when the orchestrator's model matches the routing
  target, handle the task in-conversation."* Do not re-litigate this during
  sessions.
- `route(task_type="session-verification")` remains **mandatory and
  cross-provider** for every session, per the hard constraint that survives
  any override. The verifier's job here is a false-positive screen: it
  receives the findings doc plus the files the findings cite.
- Decision-time consensus (per `docs/ai-led-session-workflow.md`) still
  applies to genuinely contested design calls in Session 4 — notably the
  R2-3 ownership question — before falling back to `AskUserQuestion`.

## Review Dimensions (applies to Sessions 1–3)

Evaluate each area against, in priority order:

1. **Correctness** — logic bugs, parsing errors, COM-interop hazards,
   encoding/culture issues (date, number, locale-sensitive formatting),
   twips/geometry conversion, off-by-one and boundary defects.
2. **Contract fidelity** — output JSON conventions (provenance
   `{ source, ... }` shape; omission semantics — never synthesize defaults;
   explicit units), schema conformance, **determinism** (consumers diff
   output across runs), explicit-`unresolved`-over-silence in the parser.
3. **COM lifecycle and resource safety** — single-chokepoint discipline
   (`AccessComSession`), RCW release ordering, exact-PID cleanup, lock-file
   handling.
4. **Lean-principle violations, both directions** — over-engineering (dead
   fallbacks, speculative abstraction, defensive catch-noise) and
   under-engineering (silent swallowing where the contract requires an
   explicit unresolved node).
5. **Test posture** — coverage gaps on high-risk logic, brittle pins,
   hermetic-vs-integration boundary correctness.
6. **Robustness against untrusted inputs** — a customer `.accdb` is untrusted
   input: path handling, identifier escaping in generated SQL (`schema.sql` /
   `data.sql`), resource exhaustion on pathological inputs.
7. **Cross-cutting architecture** — seams between Access/Core/Pipeline,
   duplication across projects, drift between schema docs and emitters.

### Findings record shape

Each finding in a `sN-review-findings.md` uses this shape so Session 4
synthesis is mechanical:

```markdown
### F-<session>-<seq>: <title>
- **Severity:** Critical | Major | Minor | Observation
- **Area:** <project — file:line>
- **Evidence:** <file:line pointers; the command/check that proves the claim,
  or the explicit label "inferred — not verified">
- **Problem:** <what is wrong and why it matters to the downstream AI consumer>
- **Recommendation:** <smallest change that fixes it — lean>
```

Per *Evidence Over Inference*: a finding whose claim can be cheaply proven
(run the narrow hermetic test, probe the output, check the schema) must be
proven before it is written as fact; anything unproven is labeled inferred.
Findings without evidence pointers do not graduate into Session 4
recommendations.

### Known issues — context, not findings

Do not re-discover these; cite them as context where relevant:

- **Integration suite red on clean checkout** — 7 tests depend on local-only
  `examples/northwind-output.json` (set `005` finding; owned by the standing
  `integration-test-rebase` recommendation, which takes the next free `NNN-`
  prefix when created).
- **`identify-cleanup` zero-date misfire** — COM date sampling reads time-only
  `DateTime` columns as the OLE zero date `1899-12-30` (~46176 days "stale");
  documented in `harvester-upstream-feedback.md` (the `002` note) as a
  deferred classifier-fix candidate. Session 2 should fold this into the
  recommendations ranking rather than re-derive it.

---

## Sessions

### Session 1 of 4: Extraction Core, COM Session, And CLI Review

**Steps:**

1. Read `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`, and this spec's Review Dimensions;
   skim `docs/access-com-zombie-cleanup.md` for the COM-lifecycle contract.
2. Review `AccessHarvester.Access` (DAO/COM metadata pull, `QueryMetadata`),
   `AccessHarvester.Core` (extractors, translators, COM session, model), and
   `AccessHarvester.Cli` (subcommand surface, exit codes, argument handling)
   against the dimensions. Give `AccessHarvester.Core/Com/AccessComSession.cs`
   line-level attention — it is the single COM chokepoint and the highest-risk
   file in the repo.
3. Assess `tests/AccessHarvester.Tests` and `tests/AccessHarvester.Integration`
   posture for these projects (coverage of high-risk logic, hermetic split) —
   read-only; cite the known-red baseline as context.
4. Record findings in `s1-review-findings.md` (findings shape above); prove
   cheap claims with narrow hermetic test runs or probes before writing them
   as fact. No COM-driven runs this session.
5. Cross-provider verification of the findings doc (include or disclose the
   cited files; require the JSON response block).
6. Closeout: update `session-state.json`, append actuals to
   `ai-assignment.md`, commit.

**Creates:** `docs/session-sets/006-solution-review-and-upstream-feedback-discovery/s1-review-findings.md`
**Touches:** set artifacts only (`session-state.json`, `activity-log.json`, `ai-assignment.md`)
**Ends with:** a verified findings doc covering Access/Core/Cli, every finding
evidence-pointed and severity-rated.
**Progress keys:** `s1-findings-authored`, `s1-verified`

---

### Session 2 of 4: Pipeline Review — Enrichment, Usage, Classification, Migration

**Steps:**

1. Read `s1-review-findings.md` (carry forward any cross-cutting threads),
   `docs/planning/object-use-identification-strategy.md`, and
   `docs/planning/object-cleanup-strategy.md`.
2. Review `AccessHarvester.Pipeline` against the dimensions: enrichers
   (geometry, label resolution, property decoding, chainability contract),
   `Enrichment/NavigationGraph.cs` (the substrate the Round 2 edge requests
   build on — note extension seams for Session 4's design sketches),
   `UsageIdentification/*` (classifier, signal index, question generator,
   checklist writer), the cleanup analyser (fold in the known zero-date
   misfire), and the migration SQL generator (identifier escaping, type
   mapping, untrusted-input robustness).
3. Assess `tests/AccessHarvester.Pipeline.Tests` coverage against the
   high-risk logic identified in step 2.
4. Record findings in `s2-review-findings.md`; prove cheap claims via the
   hermetic Pipeline tests or committed-golden probes. No COM-driven runs
   this session.
5. Cross-provider verification of the findings doc.
6. Closeout: update `session-state.json`, append actuals, commit.

**Creates:** `s2-review-findings.md`
**Touches:** set artifacts only
**Ends with:** a verified findings doc covering the Pipeline project, with
NavigationGraph extension seams noted for Session 4.
**Progress keys:** `s2-findings-authored`, `s2-verified`

---

### Session 3 of 4: Parser Stack And Symbol DB Review

**Steps:**

1. Read `docs/VBA-Parser-Migration-Harvester-Plan.md` (sections relevant to
   current state), the parser-strategy principles in `project-guidance.md`,
   and `docs/test-cases/harvester-schema.v0.json`.
2. Review `AccessHarvester.Vba.Parser`, `AccessHarvester.Vba.Resolver`,
   `AccessHarvester.Sql.Parser`, and `AccessHarvester.SymbolDb` against the
   dimensions, with parser-specific weight on: determinism, explicit
   `unresolved` provenance (test case 08 is the canonical contract),
   grammar-over-regex adherence, and resolver scope/default-member edge
   cases.
3. Assess the parser test pins (`docs/test-cases/*.expected.json`, grammar
   test projects) for contract completeness — are the pinned cases the right
   contract, and where are the gaps?
4. Record findings in `s3-review-findings.md`; prove cheap claims by running
   the narrow grammar/parser/resolver test suites (all hermetic).
5. Cross-provider verification of the findings doc.
6. Closeout: update `session-state.json`, append actuals, commit.

**Creates:** `s3-review-findings.md`
**Touches:** set artifacts only
**Ends with:** a verified findings doc covering the parser stack and symbol DB.
**Progress keys:** `s3-findings-authored`, `s3-verified`

---

### Session 4 of 4: Consumer Feedback Grounding, Synthesis, And Closeout

**Steps:**

1. Read the three findings docs and `docs/planning/harvester-upstream-feedback.md`
   → Round 2.
2. **Ground the feedback against fresh output** (per the *ground
   consumer-reports against fresh output* lesson): run the harvester
   (`extract` → `translate` → enrichment → `classify` / usage pass) against
   `samples/HomeHealthCare.accdb` on this Access-equipped host — deliberately,
   sequentially, COM-isolated per the testing conventions. Confirm or refute,
   with output-file evidence:
   - **R2-1:** the usage pass classifies `tblSwitchboardItems` as
     switchboard-navigation-data but emits no row-level navigation edges
     (captions, target objects, commands) in IR or graph output.
   - **R2-2:** no edges record which parameter dialog feeds which query and
     which query feeds which report, even where record sources, `OpenReport`
     callers, and declared-parameter tokens are all individually observable.
   - **R2-3:** the per-database tree (`AccessMetaData/<dbName>/`) and the
     classify output root are disjoint, forcing the downstream two-root copy.
   Where HomeHealthCare lacks a pattern the feedback describes (e.g., a
   full report-param chain), ground against committed goldens and label the
   residual claim "inferred — not verified."
3. Author design sketches as part of the recommendations doc:
   - **R2-1:** switchboard-row materialization into a structured navigation
     graph in IR — placement relative to `NavigationGraph.cs`, output shape
     (provenance conventions apply), and how a consumer's mechanical
     navigation lane would read it.
   - **R2-2:** report-chain linkage edges (dialog → query → report) from
     record sources, `OpenReport` callers, and parameter references —
     including how unresolvable links are emitted explicitly.
   - **R2-3:** ownership recommendation (orchestrator-side intake per
     `project-guidance.md` default vs upstream `export-reviewed-input`).
     If contested, run decision-time consensus before `AskUserQuestion`.
4. Synthesize `review-recommendations.md`: merge S1–S3 findings and the
   feedback assessments into a single prioritized list, grouped into proposed
   follow-up session sets with sizing (sessions) and prerequisites, sequenced
   against the standing `integration-test-rebase` recommendation. Lean rule:
   every recommendation states its consumer-visible value; findings below the
   value bar are listed as "recorded, not recommended."
5. Update the Round 2 status table in
   `docs/planning/harvester-upstream-feedback.md` with assessment outcomes.
6. Sweep spurious `samples/*.accdb` diffs (`git checkout -- samples/*.accdb`)
   from the COM run before staging anything.
7. Cross-provider verification — closeout grade: prose-first lead before the
   file listings, plus the required JSON response block.
8. Closeout: write `disposition.json` **before** the closeout commit,
   `change-log.md`, `next-session-set-recommendation.md` (routed analysis, per
   convention), update `session-state.json`, notify the maintainer.

**Creates:** `review-recommendations.md`, `disposition.json`, `change-log.md`,
`next-session-set-recommendation.md`
**Touches:** `docs/planning/harvester-upstream-feedback.md` (Round 2 status
table only), set artifacts
**Ends with:** the maintainer can read one document to see what the review
found, what the consumer feedback is worth, and which follow-up sets to run in
what order.
**Progress keys:** `s4-feedback-grounded`, `s4-recommendations-authored`,
`s4-verified`, `set-complete`

---

## Stop Rules

- **Read-only on shipping code.** This set changes no `.cs`, `.csproj`,
  schema, golden, sample, or test file. If a Critical finding tempts an
  in-set fix, record it, mark it Critical, and leave it for a follow-up set —
  surface to the maintainer in the session notification instead.
- **Never regenerate `examples/northwind-output.json`** or any
  NorthwindDevEdition-derived artifact — regeneration reintroduces the residue
  set `005` removed. Fresh runs target `samples/HomeHealthCare.accdb` only.
- **Do not fix the known-red integration suite** — it is owned by the standing
  `integration-test-rebase` recommendation. Cite it as context.
- **COM runs in Session 4 only**, deliberately and sequentially, with the
  spurious-`.accdb`-diff sweep before staging; Sessions 1–3 validate claims
  with hermetic tests and committed output only.
- If review scope proves larger than the four-session budget (a session
  running out of context mid-area), stop at the area boundary, record the
  uncovered remainder in the findings doc's "not reviewed" section, and let
  Session 4 scope the remainder into a follow-up set — do not silently thin
  the review.

## End-Of-Set Deliverables

- `s1-review-findings.md`, `s2-review-findings.md`, `s3-review-findings.md` —
  verified, evidence-pointed findings per area.
- `review-recommendations.md` — consolidated, prioritized recommendations and
  proposed follow-up session sets (the set's primary deliverable).
- Round 2 assessment outcomes in `docs/planning/harvester-upstream-feedback.md`.
- Standard set artifacts: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md` per session,
  `disposition.json`, `change-log.md`, `next-session-set-recommendation.md`.
