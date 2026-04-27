using System.Collections.Generic;
using AccessHarvester.Sql.Parser.Ast;

namespace AccessHarvester.Sql.Parser.FormReferences;

// Walker that gathers every FormReference node in a SQL AST in tree-walk
// order. Stops descending into the FormReference subtree itself — its
// children (FormsRoot/ReportsRoot, RefSegment) cannot contain nested
// FormReferences, and recursing into them would inflate the walker cost
// without finding additional hits. PARAMETERS-clause form references
// reach this walker via SqlSyntaxWalker.VisitFormReferenceParameterName,
// which descends into the inner Reference.
public sealed class FormReferenceCollector : SqlSyntaxWalker
{
    private readonly List<FormReference> _hits = new();

    public IReadOnlyList<FormReference> Collected => _hits;

    public override void VisitFormReference(FormReference n)
    {
        _hits.Add(n);
        // Intentionally not calling base.VisitFormReference — see class
        // comment.
    }
}
