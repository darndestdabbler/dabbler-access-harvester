**Recommendation: (a), with one tweak: make it a real `criteriaFragment` rule plus property-aware routing / dual-parse fallback.**

Why: your downstream consumer is an AI agent reading JSON, so **silent semantic success is worse than an honest parse miss**. The current heuristic does exactly the dangerous thing: it can return `parsedOk` for `"M" Or "F"` while meaningfully misreading it. That’s fine for a human-facing coverage report; it’s not fine for machine-consumed structure.

I would **not** make this a false-positive-rate decision first. `(c)` is useful for sizing the test corpus, but you already know the heuristic fails on an entire *class* of valid Access criteria, not just oddball inputs.

## Shape of the grammar

I would **not** just alias `criteriaFragment` to existing `predicate`. Access criteria fragments are not normal SQL predicates:

- they can omit the LHS
- they can omit it **per conjunct/disjunct**
- a bare literal can mean implicit equality

So make a dedicated entry rule, e.g.:

```antlr
criteriaFragment
  : criteriaOr EOF
  ;

criteriaOr
  : criteriaAnd (OR criteriaAnd)*
  ;

criteriaAnd
  : criteriaAtom (AND criteriaAtom)*
  ;

criteriaAtom
  : NOT? '(' criteriaOr ')'
  | NOT? IS NOT? NULL
  | NOT? LIKE valueExpr
  | NOT? IN '(' exprList ')'
  | NOT? BETWEEN valueExpr AND valueExpr
  | comparisonOp valueExpr
  | valueExpr   // bare literal/expression => implicit "="
  ;
```

Then in normalization/AST-building, interpret each atom as:

- `comparisonOp expr` → `[ThisField] <op> expr`
- `IS NULL` / `IN (...)` / `LIKE ...` / `BETWEEN ...` → `[ThisField] ...`
- bare `valueExpr` → `[ThisField] = valueExpr`

That handles the cases your heuristic misses:

- `"M" Or "F"` → `[ThisField]="M" Or [ThisField]="F"`
- `5 And 10` → `[ThisField]=5 And [ThisField]=10`
- `>5 And <10` → `[ThisField]>5 And [ThisField]<10`
- `Between 1 And 100 Or Is Null`
- parenthesized forms

This is better than textually prepending `x ` because the implicit LHS belongs to **each atom**, not just the first token.

## Classifier / harness

Don’t rely on token sniffing. Use:

1. **property metadata first** if available  
   `ValidationRule`, `Filter`, etc. often have predictable modes.
2. Then parse with the expected rule(s):
   - `fullExpression`
   - `criteriaFragment`

If both succeed, emit that fact:

```json
{
  "parseKind": "ambiguous",
  "expressionParsed": true,
  "criteriaParsed": true
}
```

At minimum, split reporting into:

- `parsedAsExpression`
- `parsedAsCriteria`
- `ambiguous`
- `failed`

That fixes limitation #3 immediately.

## If you *don’t* do (a)

The heuristic is acceptable only if this output remains a **coarse coverage metric** and the downstream consumer does **not** trust parsed structure. The breaking point is low: once parsed property-sheet values are used semantically, I’d flip to `(a)` at roughly **any known silent-misparse class** or, pragmatically, **>1–2%** misclassification in an important property family.

## Alternatives you should consider

1. **Property-aware routing table**  
   Best complement to `(a)`. Syntax alone is not the whole story.

2. **Dual parse + explicit tagging**  
   Parse as both expression and criteria when needed; preserve ambiguity instead of guessing.

3. **Raw-only + kind tag**  
   Viable as a temporary fallback:
   ```json
   { "raw": "\"M\" Or \"F\"", "kind": "criteria-fragment" }
   ```
   Better than wrong structure, but weaker than a real parse.

4. **Different parser/tooling**  
   Probably not worth it. Access/Jet criteria syntax is niche; integrating another parser is likely more work than extending your grammar.

**Bottom line:** implement `(a)`. The current heuristic is “good enough” only for rough stats, not for machine-readable semantic extraction.