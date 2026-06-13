using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AccessHarvester.Pipeline.Enrichment;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Projects a parsed query's <see cref="QueryAbstraction"/> + raw DAO
/// metadata into the v1.0 manifest schema's <c>QueryItem</c> JSON shape
/// (D14: <c>QueryItem.Sql.Parsed</c> + <c>QueryItem.Sql.Parameters</c>).
/// </summary>
public static class QuerySqlProjector
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    // Maps the raw DAO query-type label (QueryExporter.QueryTypeName) to the
    // manifest schema's QueryType enum. Keys must track the corrected DAO decode
    // (006 F-2-1). Unmapped labels (e.g. Compound, Procedure — never present in any
    // corpus) fall through to the raw label and rely on QueryTypeCode for truth.
    private static readonly Dictionary<string, string> QueryTypeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Select"]         = "Select",
            ["MakeTable"]      = "MakeTable",
            ["Append"]         = "Insert",
            ["Update"]         = "Update",
            ["Delete"]         = "Delete",
            ["Union"]              = "Union",
            ["Crosstab"]           = "Crosstab",
            ["DataDefinition"]     = "DataDefinition",
            ["SqlPassthrough"]     = "PassThrough",
            ["SqlPassthroughBulk"] = "PassThrough",  // dbQSPTBulk → same schema type
        };

    // 006 F-3-7: the Jet SQL parser already determines the query shape
    // structurally; Sql.Parsed.QueryType is derived from the parsed AST (below)
    // rather than re-stamping the DAO decode, so the parser's contract-grade
    // knowledge is no longer discarded. Returns null for shapes with no manifest
    // QueryType (Unknown), in which case the DAO-derived type is used as fallback.
    private static string? ShapeToSchemaType(QueryShape shape) => shape switch
    {
        QueryShape.Select    => "Select",
        QueryShape.Union     => "Union",
        QueryShape.Transform => "Crosstab",
        QueryShape.Insert    => "Insert",
        QueryShape.Update    => "Update",
        QueryShape.Delete    => "Delete",
        _ => null,
    };

    /// <summary>
    /// Projects one saved query into a <c>QueryItem</c> JSON object.
    /// </summary>
    /// <param name="name">The query's name (from the DAO QueryDef).</param>
    /// <param name="queryTypeName">Raw DAO query type string (e.g., "Select", "CrossTab").</param>
    /// <param name="sqlText">The original SQL text. Always emitted in <c>Sql.OriginalText</c> and <c>Sql.Parsed.Raw</c>.</param>
    /// <param name="abstraction">
    /// Builder output from <see cref="QueryAbstractionBuilder.Build"/>. When null
    /// (parse failure), only <c>Raw</c> is emitted in <c>Sql.Parsed</c>.
    /// </param>
    /// <param name="connect">Optional DAO Connect string (pass-through queries).</param>
    /// <param name="returnsRecords">Optional DAO ReturnsRecords flag.</param>
    /// <param name="odbcTimeout">Optional DAO ODBC timeout in seconds.</param>
    /// <param name="queryTypeCode">Optional DAO numeric type code.</param>
    public static JsonNode Project(
        string name,
        string queryTypeName,
        string sqlText,
        QueryAbstraction? abstraction,
        string? connect = null,
        bool? returnsRecords = null,
        int? odbcTimeout = null,
        int? queryTypeCode = null)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (queryTypeName is null) throw new ArgumentNullException(nameof(queryTypeName));
        if (sqlText is null) throw new ArgumentNullException(nameof(sqlText));

        string schemaQueryType = QueryTypeMap.TryGetValue(queryTypeName, out var mapped)
            ? mapped : queryTypeName;

        var node = new JsonObject
        {
            ["ItemKind"] = "Query",
            ["Name"] = name,
            ["QueryType"] = schemaQueryType,
        };

        if (queryTypeCode.HasValue) node["QueryTypeCode"] = queryTypeCode.Value;
        if (connect is not null) node["Connect"] = connect;
        if (returnsRecords.HasValue) node["ReturnsRecords"] = returnsRecords.Value;
        if (odbcTimeout.HasValue) node["OdbcTimeout"] = odbcTimeout.Value;

        node["Sql"] = BuildSqlNode(sqlText, schemaQueryType, abstraction);

        if (abstraction is not null)
        {
            var abstractionNode = BuildAbstractionNode(abstraction);
            if (abstractionNode is not null)
                node["Abstraction"] = abstractionNode;
        }

        return node;
    }

    // ── Sql section ────────────────────────────────────────────────────

    private static JsonNode BuildSqlNode(
        string sqlText, string schemaQueryType, QueryAbstraction? abstraction)
    {
        var sql = new JsonObject
        {
            ["OriginalText"] = sqlText,
        };

        var parameters = BuildParametersArray(abstraction?.Parameters);
        if (parameters is not null)
            sql["Parameters"] = parameters;

        sql["Parsed"] = BuildParsedSql(sqlText, schemaQueryType, abstraction);

        return sql;
    }

    private static JsonNode BuildParsedSql(
        string sqlText, string schemaQueryType, QueryAbstraction? abstraction)
    {
        // 006 F-3-7: prefer the parser's structurally-determined shape; fall back
        // to the DAO-derived type when the SQL did not parse or the shape is Unknown.
        // BUT never let the generic "Select" AST shape override a more-specific DAO
        // action/crosstab type: an action query like a MakeTable (SELECT ... INTO ...)
        // parses as the Select shape, which would otherwise contradict the top-level
        // QueryType (e.g. "MakeTable") and feed a consumer two disagreeing labels.
        // The DAO decode is authoritative for the query family post-F-2-1; the AST
        // only adds value where it refines Select into a distinct structure
        // (Transform→Crosstab, Union) — those still win. (010 devil's-advocate review.)
        string? astType = abstraction is not null ? ShapeToSchemaType(abstraction.Shape) : null;
        string parsedQueryType =
            astType is not null && !(astType == "Select" && schemaQueryType != "Select")
                ? astType
                : schemaQueryType;

        var parsed = new JsonObject
        {
            ["Raw"] = sqlText,
            ["QueryType"] = parsedQueryType,
        };

        if (abstraction is null)
            return parsed;

        var select = ProjectSelect(abstraction.ReturnShape);
        if (select is not null) parsed["Select"] = select;

        var from = ProjectFrom(abstraction.PrimaryFrom);
        if (from is not null) parsed["From"] = from;

        var joins = ProjectJoins(abstraction.Joins);
        if (joins is not null) parsed["Joins"] = joins;

        var where = SerializePredicate(abstraction.Where);
        if (where is not null) parsed["Where"] = where;

        var groupBy = ProjectExpressionList(abstraction.GroupBy);
        if (groupBy is not null) parsed["GroupBy"] = groupBy;

        var having = SerializePredicate(abstraction.Having);
        if (having is not null) parsed["Having"] = having;

        var orderBy = ProjectOrderBy(abstraction.OrderBy);
        if (orderBy is not null) parsed["OrderBy"] = orderBy;

        return parsed;
    }

    // ── Parameters ────────────────────────────────────────────────────

    private static JsonArray? BuildParametersArray(
        IReadOnlyList<QueryParameterAbstraction>? parameters)
    {
        if (parameters is null || parameters.Count == 0) return null;
        var arr = new JsonArray();
        foreach (var p in parameters)
        {
            var pNode = new JsonObject
            {
                ["Name"] = p.Name,
                ["Source"] = MapParameterSource(p.Origin),
            };
            if (p.Type is not null) pNode["Type"] = p.Type;
            arr.Add(pNode);
        }
        return arr;
    }

    private static string MapParameterSource(ParameterOrigin origin) => origin switch
    {
        ParameterOrigin.Declared       => "NamedParameter",
        ParameterOrigin.FormReference  => "FormControl",
        ParameterOrigin.ReportReference => "FormControl",
        _                              => "UserPrompt",
    };

    // ── Abstraction section ────────────────────────────────────────────

    private static JsonNode? BuildAbstractionNode(QueryAbstraction abstraction)
    {
        var node = new JsonObject();

        if (abstraction.SuggestedMethodName is not null)
            node["SuggestedMethodName"] = abstraction.SuggestedMethodName;

        if (abstraction.Parameters is { Count: > 0 } parameters)
        {
            var suggested = new JsonArray();
            foreach (var p in parameters)
            {
                var sp = new JsonObject { ["Name"] = p.Name };
                if (p.Type is not null) sp["Type"] = p.Type;
                suggested.Add(sp);
            }
            node["SuggestedParameters"] = suggested;
        }

        return node.Count == 0 ? null : node;
    }

    // ── Parsed SELECT list ────────────────────────────────────────────

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

    // ── FROM ──────────────────────────────────────────────────────────

    private static JsonArray? ProjectFrom(TableSourceDescriptor? primary)
    {
        if (primary is null) return null;
        var entry = new JsonObject();
        if (primary.TableName is not null) entry["TableName"] = primary.TableName;
        if (primary.Alias is not null) entry["Alias"] = primary.Alias;
        if (primary.IsSubquery) entry["IsSubquery"] = true;
        return new JsonArray { entry };
    }

    // ── JOINs ─────────────────────────────────────────────────────────

    private static JsonArray? ProjectJoins(IReadOnlyList<JoinDescriptor>? joins)
    {
        if (joins is null || joins.Count == 0) return null;
        var arr = new JsonArray();
        foreach (var j in joins)
        {
            var jNode = new JsonObject
            {
                ["Kind"] = j.Kind.ToString(),
            };
            if (j.RightSource.TableName is not null)
                jNode["RightTable"] = j.RightSource.TableName;
            if (j.RightSource.Alias is not null)
                jNode["RightTableAlias"] = j.RightSource.Alias;
            if (j.RightSource.IsSubquery)
                jNode["IsSubquery"] = true;
            jNode["OnExpression"] = SerializePredicate(j.On);
            arr.Add(jNode);
        }
        return arr;
    }

    // ── GROUP BY / ORDER BY ───────────────────────────────────────────

    private static JsonArray? ProjectExpressionList(
        IReadOnlyList<ExpressionDescriptor>? items)
    {
        if (items is null || items.Count == 0) return null;
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add(JsonSerializer.SerializeToNode(item, SerializeOptions));
        return arr;
    }

    private static JsonArray? ProjectOrderBy(IReadOnlyList<OrderByDescriptor>? items)
    {
        if (items is null || items.Count == 0) return null;
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add(JsonSerializer.SerializeToNode(item, SerializeOptions));
        return arr;
    }

    // ── Predicate serialization ───────────────────────────────────────

    private static JsonNode? SerializePredicate(PredicateNode? node)
        => node is null ? null : JsonSerializer.SerializeToNode(node, SerializeOptions);
}
