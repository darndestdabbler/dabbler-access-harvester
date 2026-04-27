using System.Collections.Generic;
using AccessHarvester.Vba.Grammar.Generated;

namespace AccessHarvester.Vba.Parser;

public sealed record ParseResult(
    VbaParser.ModuleContext ParseTree,
    IReadOnlyList<ParseDiagnostic> Diagnostics,
    ParseMode ModeUsed);
