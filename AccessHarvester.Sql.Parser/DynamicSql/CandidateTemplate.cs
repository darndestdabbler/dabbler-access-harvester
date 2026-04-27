using System.Collections.Generic;

namespace AccessHarvester.Sql.Parser.DynamicSql;

// One concrete SQL template emitted by DynamicSqlAnalyzer. Text is the
// rendered SQL with `?` substituted for every form-control hole and
// `*` substituted for every unknown / unbound-variable hole;
// ParameterBindings mirror the placeholder order so consumers can
// align them by position.
//
// The placeholder convention follows Phase 5.4's request to
// distinguish parameter-resolvable holes (`?`) from
// parameter-unresolvable holes (`*`): downstream tooling can re-render
// a JET SQL parameter declaration for `?` placeholders but must defer
// `*` placeholders to the manual-review queue.
public sealed record CandidateTemplate(
    string Text,
    IReadOnlyList<ParameterBinding> ParameterBindings);
