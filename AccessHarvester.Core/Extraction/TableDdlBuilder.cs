using System.Text;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Pure-logic reconstruction of a Jet-flavored <c>CREATE TABLE</c> statement
/// for an Access table. DAO does not expose a <c>TableDef.DDL</c> property;
/// the harvester rebuilds the string from <c>TableDef.Fields</c> +
/// <c>TableDef.Indexes</c> + <c>CurrentDb().Relations</c> so downstream AI
/// consumers can read the schema as SQL rather than re-deriving it from the
/// structural collections.
/// </summary>
public static class TableDdlBuilder
{
    public sealed record DdlField(
        string Name,
        int TypeCode,
        int? Size,
        bool Required,
        bool IsAutoIncrement,
        string? DefaultValue);

    public sealed record DdlPrimaryKey(string Name, IReadOnlyList<string> Fields);

    public sealed record DdlForeignKey(
        string Name,
        string ParentTable,
        IReadOnlyList<(string ChildField, string ParentField)> FieldPairs,
        bool CascadeUpdate,
        bool CascadeDelete);

    public static string Build(
        string tableName,
        IReadOnlyList<DdlField> fields,
        DdlPrimaryKey? primaryKey,
        IReadOnlyList<DdlForeignKey> foreignKeys)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE TABLE [").Append(tableName).AppendLine("] (");

        var lines = new List<string>();
        foreach (var f in fields)
            lines.Add(FormatField(f));

        if (primaryKey is not null && primaryKey.Fields.Count > 0)
            lines.Add(FormatPrimaryKey(primaryKey));

        foreach (var fk in foreignKeys)
            lines.Add(FormatForeignKey(fk));

        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append("    ").Append(lines[i]);
            if (i < lines.Count - 1) sb.Append(',');
            sb.AppendLine();
        }

        sb.Append(");");
        return sb.ToString();
    }

    private static string FormatField(DdlField f)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(f.Name).Append("] ").Append(MapType(f));
        if (f.Required && !f.IsAutoIncrement) sb.Append(" NOT NULL");
        if (!string.IsNullOrEmpty(f.DefaultValue))
            sb.Append(" DEFAULT ").Append(f.DefaultValue);
        return sb.ToString();
    }

    private static string FormatPrimaryKey(DdlPrimaryKey pk)
    {
        var cols = string.Join(", ", pk.Fields.Select(c => "[" + c + "]"));
        return $"CONSTRAINT [{pk.Name}] PRIMARY KEY ({cols})";
    }

    private static string FormatForeignKey(DdlForeignKey fk)
    {
        var childCols = string.Join(", ", fk.FieldPairs.Select(p => "[" + p.ChildField + "]"));
        var parentCols = string.Join(", ", fk.FieldPairs.Select(p => "[" + p.ParentField + "]"));
        var sb = new StringBuilder();
        sb.Append("CONSTRAINT [").Append(fk.Name)
          .Append("] FOREIGN KEY (").Append(childCols)
          .Append(") REFERENCES [").Append(fk.ParentTable)
          .Append("] (").Append(parentCols).Append(')');
        if (fk.CascadeUpdate) sb.Append(" ON UPDATE CASCADE");
        if (fk.CascadeDelete) sb.Append(" ON DELETE CASCADE");
        return sb.ToString();
    }

    // DAO dbType* constants. Text/Binary include size; Memo/Boolean/numeric/date do not.
    private static string MapType(DdlField f) => f.TypeCode switch
    {
        1 => "BIT",
        2 => "BYTE",
        3 => "SHORT",
        4 => f.IsAutoIncrement ? "COUNTER" : "LONG",
        5 => "CURRENCY",
        6 => "SINGLE",
        7 => "DOUBLE",
        8 => "DATETIME",
        9 => f.Size.HasValue ? $"BINARY({f.Size.Value})" : "BINARY",
        10 => f.Size.HasValue ? $"TEXT({f.Size.Value})" : "TEXT",
        11 => "LONGBINARY",
        12 => "LONGTEXT",
        15 => "GUID",
        16 => "BIGINT",
        17 => f.Size.HasValue ? $"VARBINARY({f.Size.Value})" : "VARBINARY",
        18 => f.Size.HasValue ? $"CHAR({f.Size.Value})" : "CHAR",
        19 => "NUMERIC",
        20 => "DECIMAL",
        21 => "FLOAT",
        _ => $"UNKNOWN({f.TypeCode})",
    };
}
