Based on the information provided, here is my recommendation.

### 1. Recommendation: Option (a)

I recommend **(a) Refine to a real grammar entry rule for criteria fragments**.

**Reasoning:** The primary consumer is an AI agent that requires precision, completeness, and machine-readability. The current heuristic fails on all three counts for a known subset of inputs:
*   **Precision:** It misinterprets valid fragments like `"M" Or "F"` as literal strings.
*   **Completeness:** It fails to parse valid patterns, creating a knowledge gap.
*   **Readability:** It conflates two semantically distinct constructs (full expressions and normalized fragments) in the output, forcing the AI to guess the original intent. This violates the harvester's core purpose.

Fixing the grammar is the only option that addresses the root cause and provides the structural fidelity the project demands. Options (b) and (c) are tactical delays that push the problem downstream, increasing the complexity and potential for error in the AI agent. Given the project's stated goals, investing in the parser now is the correct engineering decision.

### 2. Grammar Rule Shape and Edge Cases

The grammar entry rule should take the form of a new top-level rule, which I'll call `criteriaFragment`, that reuses existing predicate-parsing logic.

**Proposed Shape:**

In `JetSql.g4`, you likely have a rule structure similar to this:
```antlr
search_condition : boolean_term (OR boolean_term)* ;
boolean_term   : boolean_factor (AND boolean_factor)* ;
boolean_factor : NOT? boolean_primary ;
boolean_primary: predicate | LPAREN search_condition RPAREN ;
predicate      : expression (comp_op expression | is_null_predicate | in_predicate | between_predicate | like_predicate) ;
```

Add a new top-level entry rule:

```antlr
// New entry rule for property-sheet criteria fragments.
criteriaFragment: search_condition EOF;

// Existing entry rule for full expressions (simplified).
fullExpression: search_condition EOF;
```

**Harness Logic:**

The C# harness that invokes the parser would change:

1.  First, attempt to parse the input string using the `fullExpression` rule.
2.  If it succeeds, tag the resulting JSON with `type: "FullExpression"`.
3.  If it fails, attempt to parse the same input string using the `criteriaFragment` rule.
4.  If *that* succeeds, tag the resulting JSON with `type: "CriteriaFragment"`. The JSON object should also note the implicit left operand (e.g., `implicitOperand: "[ThisField]"`).
5.  If both fail, report a parsing error.

**Edge Cases Handled by This Approach:**

This design correctly handles the limitations of the heuristic:

*   **`"M" Or "F"`:** This fails the `fullExpression` rule (which expects a predicate like `x = "M"`) but succeeds with `criteriaFragment`, as `"M"` is a valid `expression` and the whole is a valid `search_condition`. The parser interprets it as `([ThisField] = "M") Or ([ThisField] = "F")`.
*   **`5 And 10`:** Similarly, this parses as a `search_condition` under `criteriaFragment`.
*   **`Is Not Null`:** This is already a valid predicate RHS and will parse correctly under `criteriaFragment`.
*   **`>0`:** This is ambiguous. It can be a `fullExpression` or a `criteriaFragment`. The harness logic (try `fullExpression` first) provides a deterministic resolution. Access treats this as a full expression, so the priority is correct.

### 3. Breaking Point for (b) or (c)

The breaking point has already been reached. The moment you define the consumer as an AI that needs to "interpret and document the original application," you have mandated semantic precision. The current heuristic's ambiguity and known failure modes are unacceptable for that goal. There is no percentage of failure that is "good enough" when the output structure is fundamentally misleading.

### 4. Alternatives Not Considered

The team has identified the main options. One viable alternative is a hybrid approach that stops short of a full grammar modification but improves on the heuristic.

**Alternative: Classify, Tag, and Defer (A "Smarter Heuristic")**

Instead of a simple leading-token sniff, use a more robust classifier to distinguish fragments from full expressions *before* parsing. This could be a set of more sophisticated regular expressions or even a separate, smaller parser designed only to make this distinction.

1.  **Classify:** Analyze the input string. Does it contain an explicit comparison operator (`=`, `<`, `>`) with a clear LHS? If yes, it's likely a `FullExpression`. Does it start with `In`, `Like`, `Between`, `Is Null` or consist of values joined by `And`/`Or`? It's likely a `CriteriaFragment`.
2.  **Tag:** Record the raw string in the JSON output along with the classification tag: `value: "\"M\" Or \"F\"", classification: "CriteriaFragment"`.
3.  **Parse (Optional):** Attempt to parse the value *according to its classification*. For a fragment, prepend the synthetic operand before parsing. If parsing fails, the output still contains the raw value and the high-confidence classification, which is valuable information for the AI.

This is cheaper than Option (a) but provides a much stronger signal than the current heuristic. It directly addresses the ambiguity flaw (#3), even if it doesn't parse every case correctly. However, it is still less robust than a proper grammar-based solution.