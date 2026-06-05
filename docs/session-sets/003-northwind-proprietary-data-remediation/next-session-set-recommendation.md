# Next Session-Set Recommendation — after 003 Northwind Proprietary-Data Remediation

> Authored at 003 Session 3 closeout. The recommendation below is **routed**
> (cross-provider analysis, do-not-self-opine), not the orchestrator's own
> opinion. Raw routed output: [`next-set-analysis-raw.md`](next-set-analysis-raw.md).

## Recommended next set: `004-northwind-symbol-db-sqlite-residue`

Set `003` is complete but **deliberately scoped to the dedicated-artifact
footprint only** (the 9-line / 415-path allowlist: the proprietary `.accdb`, its
golden tree, YAML/parse-report mirrors, symbol-db measurement docs, comprehension
drafts, example manifest). It explicitly deferred — by maintainer decision
(2026-06-05) — the **secondary embedded-residue footprint**: ~123 KEEP-files that
still disclose proprietary NorthwindDevEdition *structure* (form/report/query/
table/module/control names, procedure logic, VBA translations, workflow docs),
**including two binary SQLite DBs** (`tools/symbol-db/symbol-db.sqlite` 1.23 MB,
`tools/symbol-db/consistency/grades.sqlite` 0.67 MB) that serialize proprietary
module source bodies + AI interpretations. See `003`'s
[`embedded-residue-findings.md`](embedded-residue-findings.md) for the full
inventory and severity buckets.

The follow-up set `004-northwind-symbol-db-sqlite-residue` already exists as a
`not-started` stub (3 sessions, mirroring `003`'s arc: forward removal +
test/tooling skip-hardening → curate allowlist + backup + dry-run → execute
rewrite + force-push). It is the designated and logical next step; until it lands,
the repo's working tree and history retain extensive Northwind structural
disclosure, so the remediation is not yet complete.

## Recommended orchestrator (004 Session 1)

Session 1 ("forward removal + test/tooling skip-hardening") is judgment-heavy
triage of ~123 files plus the two binary SQLite DBs — the latter requires
generating precise SQL/Python to inspect and stage the binaries without
corrupting them, *and* re-basing the characterization/translation tests onto the
synthetic HomeHealthCare fixture.

- **Engine:** either **Codex (OpenAI, GPT-5.x)** — strong code-generation for the
  binary-SQLite inspection scripting — or **claude-code (Anthropic, Opus 4.8)** —
  large-context reasoning to hold the 123-file inventory + `embedded-residue-findings.md`
  and form a consistent per-file keep/delete/redact plan. Either is defensible;
  the binary-SQLite scripting load slightly favors Codex, the cross-file
  consistency-plan load slightly favors Claude.
- **Effort:** **medium** (careful analysis + script generation; more than plain
  text edits, less than novel synthesis).
- Cross-provider `session-verification` remains mandatory and goes to a different
  provider than the chosen orchestrator (for a Claude orchestrator → `gpt-5-4`).

## Sequencing caveats

1. **Block new releases until `004` completes.** `main` still contains proprietary
   structural disclosure; cutting a release would formalize/distribute it. (The
   already-published `v1.2.0` was re-pointed by `003`'s tag force-push and is
   accepted; this caveat governs *future* releases.)
2. **Re-base the characterization/translation tests onto HomeHealthCare BEFORE
   `004`'s destructive session**, so those tests have a valid baseline after the
   proprietary fixtures/assertions are removed/redacted — otherwise they break with
   nothing to validate against.
3. **Freeze `main` and avoid parallel sets during `004`'s execution.** Like `003`,
   `004` ends in a history rewrite + force-push; commits landing on `main` between
   curation and force-push would be orphaned or require manual re-base. Any
   long-lived branches off the old history must be re-clearned/re-based after the
   rewrite.
4. **Fresh backup at the moment of `004`'s rewrite** (do not rely on `003`'s
   bundles — they predate `004`'s work), plus the same gated go-ahead discipline
   `003` used.
