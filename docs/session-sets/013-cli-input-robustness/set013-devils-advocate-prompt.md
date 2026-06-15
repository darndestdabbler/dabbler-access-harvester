# Devil's-Advocate Review — Set 013 (CLI Input Robustness), WHOLE SET (Sessions 1 + 2)

> **For the human:** paste this to GPT-5.4 and to Gemini Pro separately. Each
> should answer independently. This is an **adversarial** review of an already
> shipped, already cross-provider-VERIFIED session set — the goal is to find what
> the routed verification missed, not to re-confirm it.

## Your stance (read first)

You are a skeptical reviewer. **Your null hypothesis is that each fix below is
WRONG, incomplete, or has an unintended side effect.** For every item there is a
*specific* contrarian hypothesis stated — your job is to steelman that hypothesis
with concrete evidence (a code path, an input, an invariant) before you concede
the fix is sound. Do **not** produce a generic "looks good" review and do **not**
be theatrically negative either: a finding must be backed by a concrete
reproducer or a named invariant it violates. If you cannot substantiate a
hypothesis after genuinely trying, say so explicitly — that is a useful result.

**Bias cautions.** (1) This code was authored by an AI and verified by another AI
(gpt-5-4, which returned VERIFIED only after one Major was fixed). Do not assume
the prior VERIFIED is correct. (2) Several claims below were closed on *thin*
evidence — a single test, a reasoning-only argument, or "byte-level golden" on
**one** database (HomeHealthCare). Mine those first; they are where a real defect
most likely survives. (3) You are seeing excerpts, not the whole tree — if a
concern depends on code you cannot see, say what you'd need to confirm it rather
than assuming the worst or the best.

## Context

A C#/.NET 10 CLI harvests Microsoft Access metadata for a downstream AI consumer.
The consumer keys off **exit codes** (`0 = all objects processed successfully`).
Set 013 closed the 006 review's "P5" lane — CLI input-validation gaps where
invalid/edge input produced a wrong or silently-degraded result instead of an
honest error. Six fixes across two sessions:

- **S1:** F-1-3 (`--types` value validation), F-1-2 (object-name path sanitisation).
- **S2:** O-1-4 (shared `ParseOptions` + unknown-switch + duplicate-option
  rejection), F-1-5 (OLE DB connection-string building), F-1-6 (bracket-aware
  `PARAMETERS` tokenisation), O-1-7 (dispatch-table drift).

Validation on this Windows host: `AccessHarvester.Tests` hermetic **691 passed /
0 failed**; Integration black-box (unknown-switch ×11 + identify-cleanup + types
×3) **15 passed**; COM tier (golden + manifest conformance + CLI extract/run/
translate) **10 passed**; F-1-6 proven golden-neutral **byte-level** (golden
reproduced without regen). Routed gpt-5-4: R1 ISSUES_FOUND (1 Major: duplicate
options silently overwritten) → fixed → R2 VERIFIED.

---

## Item 1 — F-1-3: `--types` value validation (S1)

`BuildPipeline` (`Program.cs`) validates each `--types` value against the live
known-category set and rejects an all-filtered-out result:
```csharp
var cats = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var known = pipeline.KnownCategories;                 // live union of exporter/translator/enricher categories
var knownSet = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
var unknown = cats.Where(c => !knownSet.Contains(c)).ToArray();
if (unknown.Length > 0)
    throw new ArgumentException($"Unknown --types value(s): {string.Join(", ", unknown)}. Valid categories: {string.Join(", ", known)}.");
var filtered = pipeline.WithCategories(cats);
if (filtered.IsEmpty)
    throw new ArgumentException($"--types selected no categories to process. Valid categories: {string.Join(", ", known)}.");
pipeline = filtered;
```
**Contrarian hypothesis to steelman:** the validation source (`KnownCategories`)
is dynamic but the usage text that lists valid categories is hand-maintained — is
there an input where a category is *accepted by `KnownCategories`* but the run
still silently does nothing (e.g. a category with an exporter but no objects of
that type, or a category valid for `extract` but a no-op for `translate`)? Is
`IsEmpty` computed over the right collection? Could a duplicate or
whitespace-only `--types` token slip past `RemoveEmptyEntries | TrimEntries` and
change the filter?

## Item 2 — F-1-2: object-name path sanitisation + collision guard (S1)

`SaveAsTextExporter` sanitises the object-name path component and reports a
collision instead of silently overwriting:
```csharp
internal static IReadOnlyList<(string Name, string? FileName, string? CollisionError)> PlanOutputFiles(
    IEnumerable<string> names)
{
    var claimed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var plan = new List<(string, string?, string?)>();
    foreach (var name in names)
    {
        string fileName = SanitizeFileName(name) + ".txt";
        if (claimed.TryGetValue(fileName, out var existing)
            && !string.Equals(existing, name, StringComparison.Ordinal))
        {
            plan.Add((name, null, $"Sanitised output name '{fileName}' collides with already-exported object '{existing}'."));
            continue;
        }
        claimed[fileName] = name;
        plan.Add((name, fileName, null));
    }
    return plan;
}
// SanitizeFileName replaces every char Windows disallows in a filename with '_'.
```
**Contrarian hypotheses to steelman:** (a) The collision key compares the
*original* names with `StringComparison.Ordinal` but keys the dictionary
`OrdinalIgnoreCase`. Is there a pair of distinct Access object names that are
equal under `OrdinalIgnoreCase` (so the dict treats them as the same key) but
distinct under `Ordinal` (so `existing != name` is true) — producing a *false*
collision report, OR the reverse, a *missed* collision that overwrites? (b)
`SanitizeFileName` maps many chars to `_`; can two legitimately-distinct names
that should both export end up reported as a collision so one object is **dropped
from the output** (exit 3) when Access itself would have allowed both? (c) Does
sanitisation ever produce an empty or reserved name (`CON`, `NUL`, trailing dot/
space) that Windows still rejects, so the "fix" still fails the export?

## Item 3 — O-1-4: shared `ParseOptions` (unknown + duplicate rejection) (S2)

```csharp
public static Dictionary<string, string> Parse(string[] args,
    IReadOnlyCollection<string> valueOptions, IReadOnlyCollection<string> flagOptions)
{
    var values = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);
    var flags = new HashSet<string>(flagOptions, StringComparer.OrdinalIgnoreCase);
    var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < args.Length; i++)
    {
        string token = args[i];
        if (!token.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Unexpected positional argument: {token}");
        string key = token[2..];
        bool isFlag = flags.Contains(key);
        bool isValue = values.Contains(key);
        if (!isFlag && !isValue) throw new ArgumentException($"Unknown option: --{key}");
        if (opts.ContainsKey(key)) throw new ArgumentException($"Option --{key} specified more than once.");
        if (isFlag) { opts[key] = "true"; }
        else
        {
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option --{key} requires a value.");
            opts[key] = args[++i];
        }
    }
    return opts;
}
```
Per-command accepted sets (each `Run*` / command class passes its own):

| subcommand | value options | flag options |
|---|---|---|
| extract | db, out, types | json-status, no-clean |
| translate | in, out, types | json-status, no-clean |
| run | db, out, types | json-status, no-clean |
| generate-docs | in, out | — |
| validate | in, manifest | json-status |
| parse | in | — |
| identify-usage | in, manifest, exclude-cleanup | json-status |
| identify-cleanup | in, manifest, harvest-date, threshold-days | no-com, json-status |
| classify | in, out, exclude-cleanup | json-status |
| sanitize | in, out, classification | json-status |
| report-baseline | db, out, reports, params | no-clean, include-pdf, json-status |
| generate-migration | in, out, classification, data | json-status |

**Contrarian hypotheses to steelman:** (a) **Accepted-set regression — the
highest-risk claim.** For EACH command, is the value/flag set above *exactly*
what that command reads? Find a command that reads an option (`opts["x"]`,
`opts.TryGetValue("x")`, `opts.ContainsKey("x")`) that is **missing** from its
table — that option is now rejected as "Unknown", breaking a previously-valid
invocation. Equally, find an option in the table the command never reads (dead
acceptance). (b) **Value beginning with `--`.** A value option whose legitimate
value starts with `--` (e.g. a path, a `--`-prefixed report name) is unrepresentable
— `--out --weird` throws "requires a value". Is any real value like that? (c)
**Duplicate rejection over-reach.** Is there any command for which repeating an
option was previously meaningful (e.g. `--reports a --reports b`), so the new
"specified more than once" rejection breaks a real workflow? (d) Positional-arg
handling: does any command legitimately take a bare positional (besides
bare-file `parse`, handled before `ParseOptions`)?

## Item 4 — O-1-7: single dispatch table (S2)

```csharp
private static readonly IReadOnlyDictionary<string, Func<string[], int>> Commands =
    new Dictionary<string, Func<string[], int>>(StringComparer.OrdinalIgnoreCase) { /* 12 entries */ };
// bare-file mode: args.Length == 1 && !startsWith("--") && !help && !version && !Commands.ContainsKey(args[0]) -> RunParse(args)
// dispatch:        Commands.TryGetValue(args[0], out var handler) ? handler(rest) : UnknownCommand(args[0])
```
**Contrarian hypothesis to steelman:** the prior bare-file exclusion used
`args[0].ToLowerInvariant() is not (...)`; the new one uses an
`OrdinalIgnoreCase` dictionary. `ToLowerInvariant` and `OrdinalIgnoreCase` can
disagree on Turkish-I / culture-specific casing. Is there a token (e.g. `"RUN"`,
`"İdentify-usage"`) that the old exclusion treated as a command but the new one
does not (or vice versa), changing whether it dispatches or falls into bare-file
parse? Is `Func<string[],int>` method-group binding to instance vs static
correct for all 12 entries?

## Item 5 — F-1-5: OLE DB connection-string building (S2)

Four identical sites (re-grounding found 4, spec said 2) now call:
```csharp
public static string Build(string provider, string databasePath)
{
    var builder = new OleDbConnectionStringBuilder { Provider = provider, DataSource = databasePath };
    builder["Persist Security Info"] = false;
    return builder.ConnectionString;
}
```
**Contrarian hypotheses to steelman:** (a) Does `OleDbConnectionStringBuilder`
emit `Persist Security Info=False` in a form the ACE provider reads identically
to the old literal `Persist Security Info=False;`? (b) The old string had a
trailing `;`; the builder's output may not — does any of the four readers, or the
ACE provider, care? (c) Could the builder *normalise* the provider or path in a
way that changes which ACE provider version is probed in the 16.0→12.0 fallback
loop (the `Build` is called once per provider inside the loop — is that still
correct)? (d) The round-trip test parses the produced string back and checks
`DataSource` equals input — does that actually prove the ACE provider will open
the file, or only that the builder round-trips its own format?

## Item 6 — F-1-6: bracket-aware `PARAMETERS` tokenisation (S2)

```csharp
private static string? ExtractParametersClauseBody(string sql)
{
    var prefix = ParametersClausePrefixRegex.Match(sql);    // @"\A\s*PARAMETERS\s+"
    if (!prefix.Success) return null;
    int start = prefix.Length, depth = 0;
    for (int i = start; i < sql.Length; i++)
    {
        char c = sql[i];
        if (c == '[') depth++;
        else if (c == ']') { if (depth > 0) depth--; }
        else if (c == ';' && depth == 0) return sql[start..i];
    }
    return null;
}
private static IEnumerable<string> SplitTopLevel(string s, char separator)
{
    int depth = 0, segStart = 0;
    for (int i = 0; i < s.Length; i++)
    {
        char c = s[i];
        if (c == '[') depth++;
        else if (c == ']') { if (depth > 0) depth--; }
        else if (c == separator && depth == 0) { yield return s[segStart..i]; segStart = i + 1; }
    }
    yield return s[segStart..];
}
// per-entry parse unchanged: bracket name -> first ']' -> name + remainder type.
```
HHC has two PARAMETERS queries (`[Enter Client ID:]`, `[Enter Provider ID:]`,
`[Enter Fiscal Year:]`) — none with `,`/`;` inside brackets — so the change is
claimed golden-neutral, **proven byte-level** (COM golden reproduced without
regen). **Contrarian hypotheses to steelman:** (a) **The byte-level proof is on
ONE database.** Is there an Access `PARAMETERS` form that *does* differ under the
new tokeniser AND could plausibly appear in a real customer DB — e.g. a bracketed
name containing a literal `]` (JET escapes `]` as `]]`), nested brackets, or a
`PARAMETERS` keyword inside a string literal / comment that the prefix regex
mis-anchors? The per-entry parser still closes at the *first* `]` — does the new
depth tracking and the old first-`]` parse disagree on a `]]`-escaped name,
producing a wrong parameter name? (b) Does `ExtractParametersClauseBody` returning
`null` on a no-depth-0-`;` clause silently drop a *valid* parameter set that the
old lazy regex `(.*?);` would also have dropped — i.e. is "matches prior
behaviour" actually a shared latent bug? (c) Unbalanced `]` (depth guarded at
`> 0`) — is there an input where a stray `]` makes the splitter mis-segment?

## Item 7 — DW-15 scope call: `identify-cleanup` exit 2 for usage errors

O-1-4 made every subcommand reject unknown switches. `identify-cleanup` returns
exit **2** for usage errors (its own documented inverted convention: "0 success,
2 usage error, 1 other"), unlike the contract's `1 = usage error`. The set
**preserved** exit 2 (recorded DW-15) rather than normalising to 1, arguing it's
out of P5 scope and that `identify-cleanup --bogus x` already exited 2 before this
set. **Contrarian hypothesis to steelman:** is preserving exit 2 actually the
*wrong* call for a set whose entire purpose is exit-code honesty for the
exit-code-keying consumer? A consumer reading exit 2 as "unexpected error /
tool crashed" on what is really a usage mistake is itself a wrong signal — is the
"out of scope" framing a rationalisation that ships a known exit-code inconsistency?

## Cross-session weak-evidence closures to actively re-test

1. **S1 F-1-2 collision guard** was closed on a single routed R1 review (no
   adversarial pass). The `Ordinal` vs `OrdinalIgnoreCase` interplay (Item 2a) is
   the prime suspect — construct the name pair.
2. **S1 "golden-neutral" claim for F-1-2/F-1-3** — was it proven byte-level or
   asserted? (S1 claimed a COM byte-level golden reproduction.) Does sanitisation
   being identity on HHC's Hungarian names actually generalise, or does it just
   mean HHC never exercises the risky path?
3. **S2 F-1-6 byte-level proof is single-corpus (HHC only).** Item 6a is the
   place a real defect most likely survives.
4. **S2 accepted-option-set tables (Item 3a)** were preserved by inspection, not
   by an exhaustive "every `opts[...]` read is in the table" proof — verify it
   exhaustively if you can.

## Required output (per reviewer)

For EACH of the 7 items plus the 4 cross-session closures, state: **UPHELD**
(the fix is sound — say why you could not substantiate the contrarian hypothesis)
or **CHALLENGED** (you found a concrete problem — give the reproducer/input, the
file, the severity Critical/Major/Minor, and a fix). End with a one-paragraph
overall verdict and the single highest-value thing to check next. Be specific;
cite inputs and code paths, not vibes.
