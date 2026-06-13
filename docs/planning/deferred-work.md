# Deferred Work Register

> **Purpose:** a durable, single backlog of work **deliberately deferred during
> session execution** that is *not already owned by a planned/authored session
> set*. It exists so nothing deferred gets silently forgotten — the
> "half-implemented code / unwired seams" risk the 006 review documented across
> three layers.
>
> **This file is required reading** (see `project-guidance.md` → Conventions →
> Workflow Expectations). Every session reads it at start; every closeout checks
> whether any open item's trigger has arrived and, if so, must address it or carry
> it into the next-session-set recommendation. Removing an item is allowed **only**
> when the work has landed (cite the commit/set) or the human explicitly cancels
> it — never a silent drop.
>
> **Relationship to other records:** items already ranked/owned by the 006 review
> arc (F-3-4 resolver wiring → P3, F-2-1 query decode → P2, etc.) live in
> `docs/session-sets/006-.../next-session-set-recommendation.md` and are NOT
> duplicated here. This register is for gaps surfaced *mid-execution* that no spec
> yet owns. Cross-provider review findings that are deferred are also flagged in
> `docs/planning/fable-review-queue.md`; this register is the authoritative
> "do not forget" list.

## How to use

- **Adding:** when you defer something mid-session, add an entry with: what, the
  origin (session/finding), why it was deferred, the concrete trigger that should
  pull it back in, and a candidate owning set. Give it a stable `DW-<n>` id.
- **Discharging:** when the work lands, move the entry to **Closed** with the
  commit/set that did it and the date. Do not delete the line.
- **Mechanical backstop:** where practical, pin the deferred behavior with a
  `Skip`-ped or known-limitation test that names the `DW-<n>` id, so the gap also
  shows up on every test run, not just in this doc.

---

## Open

### DW-1 — Contextual keywords accepted as identifiers only in variable-class slots
- **What:** `STEP, COMPARE, EXPLICIT, PRESERVE, LIB, ALIAS` are accepted as
  ordinary identifiers in variable-class positions (declaration names, parameters,
  **named arguments**, loop variables, bare expressions). They are still rejected
  as: **procedure names** (`Sub Step()`, `Function Compare()`), **UDT type
  members**, **enum members**, **statement labels**, and **`Declare` names**.
- **Origin:** Set 008 Session 2 (F-3-2). Scope confirmed by the devil's-advocate
  interim review (Gemini + GPT, finding H3). The named-argument slot **was fixed**
  in S2 (it was an internal inconsistency the change itself created); the remaining
  slots were deferred.
- **Why deferred:** unproven need — **zero occurrences** in the HomeHealthCare
  fixture (32 modules) *or* the NorthwindDevEdition reference DB (66
  Microsoft-authored modules). Widening every slot risks unneeded over-acceptance;
  GPT advised "do not widen every slot blindly."
- **Trigger to pull back in:** a real customer DB (or a new corpus case) exercises
  one of these slots, OR a parser-grammar-completeness set is scoped.
- **Owning set candidate:** a future `parser-grammar-completeness` set.
- **Mechanical backstop:** known-limitation grammar test (added Set 008 S2) naming DW-1.

### DW-2 — `Report.Line` / `Report.Circle` graphics-method coordinate syntax
- **What:** the legacy graphics-drawing syntax `Me.Line (x1,y1)-(x2,y2)[, color][,
  B[F]]` and `Me.Circle (x,y), radius[, ...]` is unparsed (the parser emits
  `VBA0003 missing ')' at ','`).
- **Origin:** Set 008 Session 2 — `rptClientProfile` in the HomeHealthCare corpus
  uses `Me.Line (…)-(…)` (it is the one remaining parse failure on the fixture; the
  module comment tags it a "migration stress test"). Absent from Northwind.
- **Why deferred:** out of scope for Set 008 (manifest VBA ingestion + F-3-2/F-3-3
  hardening); the `(coord)-(coord)` step syntax is a distinct grammar feature.
- **Trigger to pull back in:** a parser-grammar-completeness set, or a consumer that
  needs report graphics methods parsed.
- **Owning set candidate:** `parser-grammar-completeness`.
- **Mechanical backstop:** the committed HomeHealthCare parse-success report records
  this as a standing `parseFailed` (1 of 32); the regression test surfaces it.

### DW-3 — Type-declaration suffix → type for VARIABLE/const/param declarations
- **What:** `Dim s$` (and suffixed module variables, constants, and parameters with
  no explicit `As`) keep `Type = null` plus a `TypeSuffix`, so a consumer reads the
  language default (Variant) rather than the suffix-implied type (`$`→String,
  `%`→Integer, `&`→Long, `!`→Single, `#`→Double, `@`→Currency). Set 008 S2 fixed
  this for **function / property-get return types** (finding H4); the **variable**
  parallel was left.
- **Origin:** Set 008 Session 2 synthesis (Gemini + GPT H4, variable parallel).
- **Why deferred:** pre-existing (not introduced by S2); type-*emission* is the
  F-3-5 class the Set 008 spec Non-Goals explicitly defer to P2. Golden-neutral
  (no suffixed variable declarations in HHC or Northwind).
- **Trigger to pull back in:** the output-contract-fidelity batch (P2) that owns
  F-3-5 (ByVal emission), which is the natural home for suffix→type emission too.
- **Owning set candidate:** `output-contract-fidelity-batch` (P2).

## Closed

_(none yet)_
