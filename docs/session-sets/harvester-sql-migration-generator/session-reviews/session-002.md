# Verification Round 1

A. **Session-1 refactor**

This refactor is structurally sound.

`MigrationSchemaResolver` now clearly owns the schema-survival decisions: migrated table set, surviving columns, resolved SQL types, identity/nullability/defaults, surviving PKs, surviving enforced FK edges, and degradations. `SqlServerDdlEmitter` is genuinely pure text assembly over that resolved model. `SqlServerDdlGenerator.GenerateDdl` is now just a thin delegate, which is the right shape.

That matters because the Session-2 additions really do consume the same resolved model:

- DDL uses `MigrationSchema`
- load ordering uses `schema.Tables` + `schema.ForeignKeys`
- data-load uses `schema.Tables`

So the important “what survived” authority is centralized instead of being re-derived in three places. That is the right seam.

On behavior preservation: I cannot byte-compare against the old Session-1 implementation from the material provided, but the evidence is strong enough here. The disclosed 52 Session-1 DDL tests and stop-rule fixture tests staying green is exactly the regression net that should catch output drift from this refactor. Nothing in the emitter suggests hidden semantic logic; it is just formatting.

So on concern A, my read is: **the refactor preserves the intended architecture and very likely preserves Session-1 behavior**.

---

B. **IDENTITY_INSERT semantics**

This design is correct, and the implementation matches the honest model.

The key point is the one you disclosed: the sanitized CSV is already the decided output of preserve/remap/mask policy. Session 2 should not re-decide ID preservation; it should load the sanitized values verbatim. Given that, the loader’s job is not “infer preserveAutonumber again”, but simply:

- if the table has an identity column, and
- that identity column is actually present in the `INSERT` column set,

then wrap the inserts for that table in `SET IDENTITY_INSERT ... ON/OFF`.

That is exactly what `SqlServerDataLoadGenerator` does:

```csharp
var identity = table.IdentityColumn;
bool identityInsert = identity is not null && insertCols.Any(...)
```

That predicate is better than re-deriving preserveAutonumber here. Re-derivation would duplicate upstream logic and could drift. The actual `INSERT` column set is the truthful runtime condition.

The ON/OFF framing is also correctly scoped:

- `ON`
- all `INSERT`s for that table
- `OFF`
- `GO`

So the identity work stays contained to one table section, which is what you want since SQL Server allows only one table with `IDENTITY_INSERT ON` per session at a time.

Also good: if the identity column is absent from the CSV/header, the code does **not** turn `IDENTITY_INSERT` on, and instead flags `DataColumnOmitted`. That is honest.

So on concern B: **yes, this is correct and honest; keying off actual presence in the INSERT column set is the right design.**

---

C. **NULL vs empty string**

This is the weak point.

The reader behavior by itself is internally coherent:

- unquoted empty field → `null`
- quoted empty field (`""`) → `""`

If the producer guarantees that every real empty string is emitted as `""`, then this is a safe, lossless contract.

But based on the disclosed producer semantics, that guarantee is not established. The dangerous sentence is:

> `CsvWriter` writes both SQL NULL and "" as an empty field, but quotes "" only when it must.

If that means a genuine empty string can be emitted as an unquoted empty field, then the read-side contract is lossy, because `CsvTableReader` will decode that value as `null`. In that case, `""` and `NULL` collapse together on reload.

So the reader implementation is only faithful **if** the writer invariant is stronger than the prose currently suggests — specifically: **non-null empty strings must always be quoted**.

The tests do not prove that end-to-end contract. `CsvTableReaderAndValueTests` manually feeds `,""` and verifies the reader distinguishes it, but there is no round-trip using the real `CsvWriter`. That means the session does not actually prove that sanitize-output CSV preserves `NULL` vs `""` in production.

So on concern C: **as stated, this is not yet safe enough to verify**. The reader logic is fine only if the producer always quotes real empty strings, and that invariant is not demonstrated here.

---

D. **Honest degradation**

Most of this is implemented correctly.

The honest degradation paths that do look good:

- **Binary/OLE omission**: `SqlValueFormatter.IsBinary` causes those columns to be excluded from the `INSERT` column list, and `DataColumnOmitted` is added. Good: no fake literal fabrication.
- **Missing CSV file**: `MissingDataFile` is emitted and the table section is commented but no rows are loaded. Honest.
- **Missing surviving CSV column**: the code emits `DataColumnOmitted` instead of pretending a value exists. Honest.
- **No silent re-masking/re-mapping**: values are loaded verbatim from sanitized CSV.

However, the **cycle degradation is not precise enough**, and that has a real correctness consequence.

`TableDependencyOrderer` uses Kahn’s algorithm, and when it cannot finish, it takes **all remaining nodes with nonzero in-degree** and reports them as the cycle:

```csharp
var remaining = nodes.Where(n => inDegree[n] > 0) ...
new MigrationDegradation(ForeignKeyCycle, remaining[0], null,
    $"Tables [{string.Join("], [", remaining)}] form a foreign-key reference cycle; ...")
ordered.AddRange(remaining);
```

That set is not necessarily “the cycle members”. It can include tables that are merely **downstream of a cycle**.

Example:

- `A -> B`
- `B -> A`   (cycle)
- `B -> AA`  (AA is not cyclic; it just depends on B)

After Kahn stalls, `A`, `B`, and `AA` all still have positive indegree. The current code will:

1. report `[A], [AA], [B]` as if they “form a cycle”, which is false for `AA`, and
2. append them in lexical order, which could place `AA` before `B`.

That second part is not just a labeling problem. It means a non-cyclic child can be ordered before its parent, even outside the actual cyclic core. If a reviewer disables only the offending cycle constraints, the script can still fail on the non-cycle FK.

So on concern D: **binary/missing-file/missing-column degradation is honest, but cycle handling is overbroad and can misorder non-cycle descendants.**

---

E. **Determinism / byte-stability**

The deterministic aspects are largely well done.

Good points:

- fixed `\n` via `SqlServerDdlEmitter.NL`
- no `Environment.NewLine`
- tables sorted by ordinal name in schema resolution
- fields ordered by `OrdinalPosition` then name
- FK relationships sorted by relationship name
- ready set in topological sort uses `SortedSet<string>(StringComparer.Ordinal)`
- child adjacency sets are sorted
- insert column order follows resolved schema order, not dictionary enumeration
- full merged degradation list in `SqlServerMigrationGenerator` is explicitly sorted with `CompareDegradations`

So the common determinism traps are mostly handled correctly.

The one caveat is that determinism does not rescue the cycle bug above. The orderer’s fallback order is deterministic, but in some cyclic graphs it is deterministically wrong: it can alphabetically interleave true cycle members with downstream dependents and violate parent-before-child where a better order still exists at the SCC level.

One smaller note: `MigrationDataLoadResult.Degradations` is not explicitly sorted inside `SqlServerDataLoadGenerator`; it is only naturally deterministic because iteration order is deterministic. The top-level `SqlServerMigrationGenerator` does sort the final merged degradations, which is the important session-level artifact.

So on concern E: **byte-stability is mostly intact, but cycle handling has a correctness flaw even though its output is deterministic.**

---

F. **Test adequacy**

The tests are strong on the mainstream Session-2 surface.

What is covered well:

- topological ordering
- deterministic tie-breaking
- deduped multi-FK edges
- self-reference handling
- simple cycle detection
- CSV quoted commas / quotes / embedded newlines / LF-only / trailing comma
- type formatting and escaping
- `NULL` formatting
- binary omission
- missing CSV file
- missing CSV column
- `IDENTITY_INSERT` on/off behavior
- parent-before-child end-to-end through `SqlServerMigrationGenerator`
- deterministic repeatability
- fixed LF output

So the suite is not flimsy. The “authored in-conversation” disclosure is fine; on merit, these are reasonable hermetic tests.

But there are two material gaps:

1. **No real writer/reader round-trip test for NULL vs empty string.**  
   The test suite proves the reader can distinguish `,` from `,""`, but it does **not** prove that the actual sanitize-side producer emits those two forms distinctly in production.

2. **No cycle-plus-downstream test.**  
   There is no test for a graph where a real cycle has a dependent child outside the cycle. That would expose the current orderer bug immediately.

A good missing regression would be something like:

- `A <-> B`
- `B -> AA`

Expected:
- degradation names only the true cyclic SCC (`A`, `B`)
- load order keeps `AA` after `B`

So on concern F: **the tests are good but not sufficient to close the two most subtle risks in this session.**

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Migration/TableDependencyOrderer.cs",
      "summary": "Cycle fallback treats every still-blocked node as part of the cycle and then appends them alphabetically, which can both misreport cycle membership and place non-cyclic children before their parents.",
      "fix": "Detect true strongly connected components (e.g. Tarjan/Kosaraju), emit ForeignKeyCycle degradations for the actual cyclic SCCs only, and topologically order the SCC condensation graph so descendants of a cycle still load after their parents; add a regression test such as A<->B with B->AA."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Migration/CsvTableReader.cs / tests/AccessHarvester.Pipeline.Tests/Migration/CsvTableReaderAndValueTests.cs",
      "summary": "The NULL-versus-empty-string contract is only safe if the CSV producer always quotes real empty strings, but that invariant is not established here and the tests only cover hand-written quoted-empty input.",
      "fix": "Guarantee at the producer boundary that every non-null empty string is emitted as a quoted empty field (or use an explicit NULL sentinel), and add a round-trip test using the real CsvWriter so null and empty string cannot collapse into each other."
    }
  ]
}
```

---

# Verification Round 2

R1 finding 1 — **confirmed fixed**. The new `TableDependencyOrderer` no longer uses the overbroad “everything left is the cycle” fallback; it now computes true SCCs with Tarjan and orders the condensation DAG with a deterministic Kahn pass. In this implementation, only SCCs with `Count > 1` are reported as `ForeignKeyCycle`, so a downstream table like `AA` in `A <-> B, B -> AA` is correctly left out of the degradation and emitted afterward at its proper topological position. The new regression test covers the original failure mode directly. I do not see a regression here, and for acyclic graphs the behavior is preserved: each table is a singleton SCC, then the condensation sort reduces to the same ordinal tie-broken Kahn ordering as before.

R1 finding 2 — **confirmed fixed for the accepted Session-2 scope**. You did not change the sanitize-tier producer, but you did close the verification gap that existed in Round 1: the reader docs now explicitly state the real cross-component contract, and the new `Real_csvwriter_roundtrip_collapses_null_and_empty_string_to_null` test uses the actual production `CsvWriter` rather than a hand-authored CSV. That is enough to make the current behavior explicit and pinned. So, to your question: **no, I do not consider the producer change mandatory within this session**. It would be a separate sanitize-tier contract change. What you have here is an adequate resolution for Session 2 because it prevents silent misunderstanding in migration and truthfully documents the present `"" -> NULL` collapse. If preserving genuine empty strings end-to-end is a product requirement, that should be handled as separate follow-up work in the sanitize component.

I also do not see evidence of regressions from these changes, and the acyclic ordering determinism claim is supported by the structure of the new algorithm and the existing/name-order tests.

```json
{ "verdict": "VERIFIED", "issues": [] }
```
