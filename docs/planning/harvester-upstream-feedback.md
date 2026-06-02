# Harvester Upstream Feedback

## Purpose

Capture concrete upstream feedback for `dabbler-access-harvester` discovered
while trying to consume HomeHealthCare outputs in this orchestrator repo.

This document is intended to serve as direct input to a separate harvester-
oriented session set or follow-up task. It focuses on changes that belong
upstream rather than in the orchestrator repo.

## Scope

In scope here:

- feedback on the semantics or output shape of current harvester artifacts
- detection gaps that materially affect cleanup/use interpretation
- concrete acceptance criteria for a future harvester session

Out of scope here:

- repo-local staging/runbook/tooling work that belongs in this orchestrator repo
- downstream bridge generation design that belongs in runner/contract sessions

## Summary Of Recommended Upstream Changes

1. Make cleanup output positive-signal only: suspicious-name, stale-data, and
  future stale-like signals belong in `object-cleanup.json`; graph-absence-only
  objects do not.
2. Detect switchboards and other launcher/menu surfaces more explicitly.
3. Improve handling of parameterized queries and parameter-collection forms.
4. Represent direct navigation and other out-of-band entry paths as a first-
   class uncertainty instead of letting them read as likely deletion signals.
5. Verify `object-use-checklist.json` casing against current upstream output
  before scheduling work, because this may already be fixed upstream.

## Current Direction After 2026-06-02 Clarification

The current intended cleanup semantics are stricter than the original feedback
draft implied.

- `object-cleanup.json` should contain only objects that positively look like
  they are no longer used.
- Positive stale-like evidence includes the currently implemented suspicious-name
  and stale-date signals, plus future signals such as duplicate-shape tables and
  obsolete-module headers.
- Cross-references from non-questionable objects are positive evidence that an
  object is still active.
- The absence of cross-references is not, by itself, evidence that an object is
  invalid or deletable.

This implies the immediate cleanup fix is to stop emitting
`CrossReferenceAbsence`-only findings, then add richer stale-like signals in
later slices.

## Recommended Implementation Order

1. Narrow cleanup output so only positive stale-like signals populate
  `object-cleanup.json` and its checklist.
2. Feed launcher/menu/startup detection into usage and interpretation so
  switchboards, report menus, and related helper artifacts stop reading as
  unexplained or likely-unused objects.
3. Plumb query parameter metadata into the usage/interpretation pipeline so
  prompt-style queries and parameter-dialog flows become first-class signals.

## Possible Future Cleanup Enhancements

These are valid ideas, but not part of the immediate upstream work unless a
later session explicitly pulls them in:

- duplicate-table-shape detection for cleanup candidates
- obsolete-module-header detection from leading VBA comments

## Evidence-Backed Feedback Items

### 1. Cleanup semantics currently overstate deletability

#### Problem

HomeHealthCare's cleanup output currently surfaces many objects as possible
cleanup candidates even though the evidence is only Low-confidence reference-
graph absence. That is not the same thing as stale, archived, or safe to
remove.

#### HomeHealthCare evidence

- [migration-staging/HomeHealthCare/object-cleanup.json](migration-staging/HomeHealthCare/object-cleanup.json)
  contains 30 findings, all Low-confidence, all driven by
  `CrossReferenceAbsence`.
- [migration-staging/HomeHealthCare/object-cleanup-checklist.json](migration-staging/HomeHealthCare/object-cleanup-checklist.json)
  phrases these as "possibly unused," which reads stronger than the actual
  evidence for switchboards, report menus, utility macros, ad hoc queries, and
  other entry surfaces.
- [migration-staging/HomeHealthCare/ir/Tables/tblSwitchboardItems.ir.json](migration-staging/HomeHealthCare/ir/Tables/tblSwitchboardItems.ir.json)
  shows a live-looking switchboard table with `RecordCount: 52` and a recent
  `LastUpdated`, yet it was still surfaced as a Low cleanup finding because the
  checked reference types came back empty.

#### Requested upstream change

- Either suppress Low `CrossReferenceAbsence` cleanup findings by default,
  separate them into a clearly different bucket from stale-object candidates, or
  change the checklist wording so the user sees them as review prompts rather
  than deletion hints.

#### Acceptance criteria

- Low cross-reference-only findings are visually and semantically distinct from
  Medium/High stale candidates.
- The cleanup checklist does not imply deletability when only graph absence
  fired.
- A regression test covers a HomeHealthCare-like switchboard/menu case.

### 2. Switchboard and launcher detection is too weak

#### Problem

The current graph logic misses common Access launcher patterns such as classic
switchboards and menu forms.

#### HomeHealthCare evidence

- [migration-staging/HomeHealthCare/ir/Forms/frmSwitchboard.ir.json](migration-staging/HomeHealthCare/ir/Forms/frmSwitchboard.ir.json)
  is clearly switchboard-like: captioned home screen, no record source,
  startup-style UI traits, event procedures, and event macros.
- [migration-staging/HomeHealthCare/ir/Macros/mcrSwitchboardActions.ir.json](migration-staging/HomeHealthCare/ir/Macros/mcrSwitchboardActions.ir.json)
  contains many `OpenForm` actions that look like a central launcher menu.
- [migration-staging/HomeHealthCare/ir/Forms/frmReportMenu.ir.json](migration-staging/HomeHealthCare/ir/Forms/frmReportMenu.ir.json)
  is a modal reports menu surface and likely a launcher rather than a CRUD form.
- Despite that, [migration-staging/HomeHealthCare/object-cleanup.json](migration-staging/HomeHealthCare/object-cleanup.json)
  still flags `frmSwitchboard`, `mcrSwitchboardActions`, and
  `tblSwitchboardItems` as Low cleanup findings, and
  [migration-staging/HomeHealthCare/object-use-checklist.json](migration-staging/HomeHealthCare/object-use-checklist.json)
  still treats `frmSwitchboard` as insufficiently explained.

#### Requested upstream change

- Add heuristics or enrichments that detect switchboards, report menus, and
  other launcher forms/macros/tables as likely navigation shells or entry
  surfaces.
- Let cleanup/use reasoning consume that classification so launcher surfaces are
  not mistaken for unused objects.

#### Acceptance criteria

- A switchboard-like form can be classified as a navigation/menu shell even when
  it has no record source.
- Helper artifacts like a switchboard table or switchboard action macro are not
  surfaced as generic cleanup candidates solely because they lack inbound
  navigation.
- A regression test covers a form plus helper macro/table launcher pattern.

### 3. Parameterized query and parameter-dialog handling should be richer

#### Problem

Parameterized queries and parameter-collection forms are common Access patterns,
but the current review outputs do not elevate them clearly enough.

#### HomeHealthCare evidence

- [migration-staging/HomeHealthCare/ir/Queries/qryProviderPlansByClient_Param.ir.json](migration-staging/HomeHealthCare/ir/Queries/qryProviderPlansByClient_Param.ir.json)
  contains explicit `PARAMETERS [Enter Provider ID:] Long, [Enter Fiscal Year:]
  Long;` plus parameter references in the `WHERE` clause.
- Operator-provided Access-domain notes in
  [docs/planning/access-domain-notes.md](docs/planning/access-domain-notes.md)
  describe popup/modal parameter forms and prompt-style query parameters as
  common real-world behavior.

#### Requested upstream change

- Detect prompt-style and bracketed query parameters as a first-class signal in
  usage/interpretation output.
- Surface likely parameter-collection forms or report-open parameter flows when
  the harvester can infer them.

#### Acceptance criteria

- Parameterized queries emit an explicit parameter-related signal or annotation.
- Review artifacts can distinguish a likely parameter dialog/prompt flow from a
  generic unknown form/report.
- A regression test covers a query with bracketed prompt parameters.

### 4. Direct navigation and out-of-band entry paths need explicit treatment

#### Problem

Many Access objects remain usable even when no harvested macro/VBA opener is
found. Direct user navigation, startup behavior, and ribbon-driven access can
all keep an object live.

#### HomeHealthCare evidence

- [migration-staging/HomeHealthCare/object-cleanup.json](migration-staging/HomeHealthCare/object-cleanup.json)
  treats many forms, reports, queries, and macros as unreferenced because the
  currently checked opener/reference types came back empty.
- [migration-staging/HomeHealthCare/object-use.json](migration-staging/HomeHealthCare/object-use.json)
  asks questions like "is it launched from VBA code, another form, or an
  external application?" which shows the current model already senses the gap
  but does not promote it to a first-class concept.
- Operator-provided notes in
  [docs/planning/access-domain-notes.md](docs/planning/access-domain-notes.md)
  describe direct navigation and ribbon-driven access as common patterns.

#### Requested upstream change

- Model out-of-band entry uncertainty explicitly, rather than letting missing
  inbound references read as likely unused.
- Consider startup objects, direct object pane navigation, and ribbon-driven
  access as separate explanatory lanes.

#### Acceptance criteria

- Review artifacts can say "no harvested opener found; may still be reachable by
  direct or out-of-band navigation" instead of implying likely cleanup.
- Cleanup/use output distinguishes missing evidence from positive evidence of
  staleness.

### 5. `object-use-checklist.json` should emit the canonical checklist shape

#### Problem

The generated usage checklist currently uses camelCase keys even though the
review guidance and checklist-editor expectations are PascalCase.

#### HomeHealthCare evidence

- [migration-staging/HomeHealthCare/object-use.json](migration-staging/HomeHealthCare/object-use.json)
  is fine as a machine-readable artifact.
- The original generated usage checklist had camelCase keys and required a
  repo-local rewrite before staging.
- The cleanup checklist already uses PascalCase, so the two review artifacts are
  inconsistent.

#### Requested upstream change

- Emit `object-use-checklist.json` in the same canonical checklist schema as the
  cleanup checklist.

#### Acceptance criteria

- `object-use-checklist.json` uses PascalCase keys matching the checklist
  editor's expected schema.
- A regression test prevents casing drift.

## Candidate Session Shape For Upstream Work

If this feedback is handled in a separate harvester session set, the smallest
useful decomposition is likely:

1. switchboard/menu/launcher detection plus direct-navigation uncertainty
2. parameterized query and parameter-dialog signal improvements
3. verification of `object-use-checklist.json` casing against fresh generated
  output, only if a current upstream run still shows drift

The direct-navigation/ribbon uncertainty work can either be folded into the
cleanup/usage sessions above or scoped as a follow-up if it requires broader IR
or startup-surface detection.