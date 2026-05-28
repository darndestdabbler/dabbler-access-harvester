## Recommended Signature

```csharp
public static class QuerySqlProjector
{
    public static JsonObject Project(RawQueryRecord raw, QueryAbstraction abstraction);
}
```

```csharp
public sealed record RawQueryRecord(
    string Name,
    int QueryTypeCode,
    string QueryTypeName,
    string Sql,
    string? Connect,
    bool ReturnsRecords,
    int OdbcTimeout);
```

Matches `ParserOutputProjector` pattern: accepts pre-computed data, returns `JsonObject`, no parsing responsibility. The caller owns the parse→abstract→project pipeline. `RawQueryRecord` is deserialized from the raw JSON file before calling the projector.

---

## Answers

### 1. Signature

`Project(RawQueryRecord raw, QueryAbstraction abstraction)`. No separate parsing step inside the projector. The caller runs the parser, builds the abstraction, and hands both to the projector. The projector is a pure mapping function. If `RawQueryRecord` already exists under a different name in the codebase, use that.

### 2. ParsedSql.Select — IsAggregate and SELECT *

`Origin == ReturnColumnOrigin.Aggregate` → `IsAggregate = true`. All other origins → `false`.

When `ReturnShape` is null (parse failure or `SELECT *`), emit `"Select": null`. Do not fabricate columns. The `Raw` field preserves the original SQL for consumers who need it. If `ReturnShape` is empty list (parsed successfully but zero columns — shouldn't happen), emit `"Select": []`.

```csharp
private static JsonArray? ProjectSelect(IReadOnlyList<ReturnColumn>? columns)
{
    if (columns is null) return null;
    var arr = new JsonArray();
    foreach (var col in columns)
    {
        arr.Add(new JsonObject
        {
            ["Name"] = col.Name,
            ["Expression"] = col.SourceExpression,
            ["IsAggregate"] = col.Origin == ReturnColumnOrigin.Aggregate,
        });
    }
    return arr;
}
```

### 3. From/Joins

`From[]` = `[PrimaryFrom]` only — a single-element array. All joined tables live in `Joins[]`. Comma-joins (`FROM a, b`) are modeled by the parser as `PrimaryFrom = a` plus a `Cross` join to `b`, so they appear in `Joins` naturally.

```csharp
private static JsonArray? ProjectFrom(TableSourceDescriptor? primary)
{
    if (primary is null) return null;
    return new JsonArray
    {
        new JsonObject
        {
            ["TableName"] = primary.TableName,
            ["Alias"] = primary.Alias,
            ["IsSubquery"] = primary.IsSubquery,
        }
    };
}
```

If `PrimaryFrom.IsSubquery` is true, `TableName` will be null or contain the alias. Emit it as-is — the subquery text is in `Raw`.

### 4. PredicateNode serialization

Yes. Use `JsonSerializer.SerializeToNode(abstraction.Where, serializerOptions)` directly. The `[JsonPolymorphic]` configuration from Session 0 handles the type discriminator. Do not re-project the tree manually.

```csharp
private static readonly JsonSerializerOptions s_options = new()
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    // match existing serializer options used elsewhere
};

private static JsonNode? SerializePredicate(PredicateNode? node)
    => node is null ? null : JsonSerializer.SerializeToNode(node, s_options);
```

Same approach for any `ExpressionDescriptor` or `OrderItem` that is already `[JsonSerializable]`.

### 5. Failed-parse fallback

Do not inspect `ParseResult` in the projector. Instead, rely on the abstraction's content. The projector always emits `Parsed.Raw` and `Parsed.QueryType`. Structured fields are emitted only when non-null in the abstraction. A failed parse produces an abstraction where all structured fields are null, so the output naturally degrades:

```json
{
  "Raw": "SELECT ...",
  "QueryType": "Select",
  "Select": null,
  "From": null,
  "Joins": null,
  "Where": null,
  "GroupBy": null,
  "Having": null,
  "OrderBy": null
}
```

This matches the spec's step-3 fallback without needing a separate code path. The caller can also check diagnostics and pass `QueryAbstraction.Empty(queryName)` if it wants to force fallback, but the projector doesn't care.

### 6. QueryTypeName → schema enum

Map known values per the table. For unknown values, emit the raw `QueryTypeName` string verbatim.

```csharp
private static readonly Dictionary<string, string> s_queryTypeMap = new(StringComparer.OrdinalIgnoreCase)
{
    ["Select"]          = "Select",
    ["MakeTable"]       = "MakeTable",
    ["Append"]          = "Insert",
    ["Update"]          = "Update",
    ["Delete"]          = "Delete",
    ["Union"]           = "Union",
    ["CrossTab"]        = "Crosstab",
    ["DataDefinition"]  = "DataDefinition",
    ["SqlPassthrough"]  = "PassThrough",
};

private static string MapQueryType(string rawTypeName)
    => s_queryTypeMap.TryGetValue(rawTypeName, out var mapped) ? mapped : rawTypeName;
```

Do **not** fall back to `"Select"`. That's a lie. Emitting the raw string preserves information and surfaces unmapped types in schema validation. If the schema strictly requires an enum member, add `"Unknown"` to the enum — but don't silently misclassify.

### 7. Abstraction.SuggestedParameters

Name + Type only. The `Abstraction` section represents the intended C# method signature. Origin, IsByRef, SourceName are SQL-level concerns that belong in `Sql.Parameters`.

```csharp
private static JsonObject ProjectAbstraction(QueryAbstraction abstraction)
{
    var result = new JsonObject
    {
        ["SuggestedMethodName"] = abstraction.SuggestedMethodName,
    };

    if (abstraction.Parameters is { Count: > 0 } parameters)
    {
        var arr = new JsonArray();
        foreach (var p in parameters)
        {
            arr.Add(new JsonObject
            {
                ["Name"] = p.Name,      // already camelCase
                ["Type"] = p.Type,
            });
        }
        result["SuggestedParameters"] = arr;
    }

    return result;
}
```

---

## Wiring in EmitManifestArtifacts

Insert into the existing query loop, after abstraction building:

```csharp
// In EmitManifestArtifacts (or the query-specific sub-method)
var queriesArray = new JsonArray();

foreach (var rawFile in Directory.EnumerateFiles(rawQueriesDir, "*.json"))
{
    var rawJson = JsonNode.Parse(File.ReadAllText(rawFile));
    var raw = rawJson.Deserialize<RawQueryRecord>(s_options);

    // Already in pipeline from Session 1:
    var parseResult = JetSqlParser.Parse(raw.Sql);
    var abstraction = QueryAbstractionBuilder.Build(raw.Name, raw.Sql, parseResult);

    // NEW — Session 2:
    var queryItem = QuerySqlProjector.Project(raw, abstraction);
    queriesArray.Add(queryItem);
}

manifest["Queries"] = queriesArray;
```

The projector slots in as the final step before manifest assembly. No changes to upstream parser or abstraction builder.

---

## Gotchas Specific to D14

1. **TRANSFORM/PIVOT**: `Shape == Transform` but the abstraction models `ReturnShape` from the inner SELECT, not the pivoted output. The `Parsed.Select` will reflect pre-pivot columns. Document this; the `Raw` field has the full SQL.

2. **Subqueries in PrimaryFrom**: When `PrimaryFrom.IsSubquery == true`, `TableName` is null. The `From[0].TableName` will be null in the output. Consumers must check `IsSubquery`.

3. **Parameter Source mapping must happen in the projector**, not upstream. The abstraction uses `ParameterOrigin` (domain enum); the schema uses string literals. Map explicitly:

```csharp
private static string MapParameterSource(ParameterOrigin origin) => origin switch
{
    ParameterOrigin.Declared        => "NamedParameter",
    ParameterOrigin.FormReference   => "FormControl",
    ParameterOrigin.ReportReference => "FormControl",
    ParameterOrigin.Unknown         => "UserPrompt",
    _                               => "UserPrompt",
};
```

4. **Sql.Parameters vs Abstraction.SuggestedParameters are different projections of the same source list**. `Sql.Parameters` includes `Source` (mapped from Origin). `Abstraction.SuggestedParameters` includes only `Name` + `Type`. Both come from `abstraction.Parameters`. Don't confuse them.

5. **Null vs. omitted fields**: Follow the convention from `ParserOutputProjector`. If it uses `JsonIgnoreCondition.WhenWritingNull`, do the same. If it explicitly sets null values, do the same. Consistency matters more than either choice.

6. **PassThrough queries have no parseable Jet SQL** — the SQL is in the backend dialect (SQL Server, etc.). The parser will fail. The abstraction will have null structured fields. The projector will emit `Parsed` with only `Raw` and `QueryType` populated. This is correct behavior, not a bug.