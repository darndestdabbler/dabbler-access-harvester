```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "FormComponentSpecBuilder.cs",
      "summary": "The builder violates the \"null for empty\" convention by assigning initialized empty lists to the FormComponentSpec, which will serialize as `[]` instead of `null` as required by the design spec.",
      "fix": "Before the `return` statement in `FormComponentSpecBuilder.Build`, convert any empty collection to `null` to adhere to the spec. E.g., `Sections = sections.Count > 0 ? sections : null, Controls = controls.Count > 0 ? controls : null,` and so on for all collection-based properties."
    },
    {
      "severity": "Major",
      "location": "QueryAbstractionBuilder.cs",
      "summary": "The `QueryAbstraction.Parameters` list is incomplete; the builder only populates declared parameters, omitting the implicit form/report reference parameters mentioned in the model's documentation.",
      "fix": "Update `QueryAbstractionBuilder` to walk the SQL AST (e.g., in the `DescribeBody` method), identify all implicit `FormReferenceParameterName` instances not already captured in the `PARAMETERS` clause, and append them to the `Parameters` list after the declared parameters to fulfill the documented contract."
    }
  ]
}
```