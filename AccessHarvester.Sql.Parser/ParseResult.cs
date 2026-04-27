using System.Collections.Generic;
using AccessHarvester.Sql.Grammar.Generated;

namespace AccessHarvester.Sql.Parser;

public sealed record ParseResult(
    JetSqlParser.ParseContext ParseTree,
    IReadOnlyList<ParseDiagnostic> Diagnostics,
    ParseMode ModeUsed);
