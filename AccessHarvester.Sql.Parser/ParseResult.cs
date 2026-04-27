using System.Collections.Generic;
using AccessHarvester.Sql.Grammar.Generated;

namespace AccessHarvester.Sql.Parser;

// ParseTree holds the sqlStatement context so both Parse() (from the
// `parse` entry rule) and ParseFragment() (from the `sqlFragment` entry
// rule) can return the same result type.
public sealed record ParseResult(
    JetSqlParser.SqlStatementContext ParseTree,
    IReadOnlyList<ParseDiagnostic> Diagnostics,
    ParseMode ModeUsed);
