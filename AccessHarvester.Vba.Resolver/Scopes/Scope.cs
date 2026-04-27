using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.Scopes;

// Base class for every node in the resolver's scope tree. Implements
// IDeclarationScope (declared in Declarations/Declaration.cs as a
// forward marker so Declaration could hold a Scope back-pointer
// without a Scopes→Declarations circular dependency in Session 1).
//
// Scopes are mutable during construction (the visitor in
// ScopeBuildingVisitor populates Children and Declarations as it walks
// the AST) but expose only IReadOnlyList views. Mutators are internal
// so only resolver-internal code — the visitor and the declaration
// types themselves — can change scope state. After construction, the
// tree is treated as if frozen by convention; #8 reads it without
// mutating shape.
//
// Equality is reference equality. Each scope is a unique entity in the
// resolver graph (e.g., two separate `With expr` blocks even with
// identical targets are distinct WithBlockScopes), so value equality
// would conflate them.
public abstract class Scope : IDeclarationScope
{
    public Scope? Parent { get; }

    public IReadOnlyList<Scope> Children => _children;
    private readonly List<Scope> _children = new();

    // Declarations defined in this scope, in source order. Parameters
    // belong to the owning ProcedureScope; module-level Public/Private
    // variables, constants, types, enums, declares, and events belong
    // to the ModuleScope; local Dim / Static / Const variables belong
    // to the enclosing ProcedureScope (VBA has no nested-block locals
    // — locals in a With or ForEach body are still ProcedureScope
    // declarations).
    public IReadOnlyList<Declaration> Declarations => _declarations;
    private readonly List<Declaration> _declarations = new();

    // Source range of the construct that introduced this scope.
    // Null only on ProjectScope (the root has no source range — it
    // spans the whole project synthetically). Concrete scope types
    // typed-narrow the value when one is structurally guaranteed.
    public SourceRange? Range { get; }

    protected Scope(Scope? parent, SourceRange? range)
    {
        Parent = parent;
        Range = range;
        parent?.AddChild(this);
    }

    private void AddChild(Scope child) => _children.Add(child);

    // Internal append-only declaration mutator. Sets the back-pointer
    // on the declaration so the two pointers (scope→declarations and
    // declaration→scope) cannot drift. Called by the
    // ScopeBuildingVisitor; consumer code should not call this.
    internal void AddDeclaration(Declaration declaration)
    {
        _declarations.Add(declaration);
        declaration.Scope = this;
    }

    // Local-only name lookup. Returns the first declaration in this
    // scope whose Name matches case-insensitively, or null. Reference
    // resolution (#8) layers parent-walking, with-block flattening,
    // module-Public visibility, and default-member access on top of
    // this primitive — they are intentionally not implemented here so
    // the scope tree's surface stays narrow per the spec's "bias toward
    // shape stability" standard.
    public Declaration? TryFindLocal(string name)
    {
        foreach (var declaration in _declarations)
        {
            if (Declaration.NameComparer.Equals(declaration.Name, name))
                return declaration;
        }
        return null;
    }
}

// Single root of every project's scope tree. One per harvested project.
// Children are ModuleScopes — one for each VBA module in the project
// (standard, class, form, report). ProjectScope owns no declarations
// itself; module-level Public visibility is computed by #8 by walking
// up from the use site and across sibling ModuleScopes, not by
// hoisting declarations onto ProjectScope.
public sealed class ProjectScope : Scope
{
    public ProjectScope() : base(parent: null, range: null) { }
}
