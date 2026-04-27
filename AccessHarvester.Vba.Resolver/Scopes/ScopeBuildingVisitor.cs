using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Scopes;

// AST visitor that walks one VBA module's parser AST and produces the
// corresponding ModuleScope subtree under a caller-supplied
// ProjectScope. Single-use per module: construct a fresh
// ScopeBuildingVisitor for each module so the internal scope stack
// starts empty.
//
// Three kinds of name introductions land here:
//
//   * Module-level declarations (Public/Private vars, Const, Type,
//     Enum, Declare, Event, Sub, Function, Property) — added to the
//     ModuleScope's Declarations list.
//   * Procedure-local declarations (Dim, Static, Const inside a body)
//     — added to the enclosing ProcedureScope's Declarations list.
//     VBA has no block-locals, so a Dim inside a With or ForEach body
//     still belongs to the surrounding ProcedureScope.
//   * Parameters — added to the owning ProcedureScope's Declarations
//     list. Declares and Events have no body, so their parameters
//     attach directly to the ModuleScope (Scope back-pointer points at
//     the ModuleScope; no inner scope is created).
//
// Reference-driven concerns intentionally NOT handled here, deferred
// to #8 (resolver-references-and-with-blocks):
//
//   * ImplicitDeclaration creation at first use of an undeclared name.
//   * RedimStatement's "redeclares vs. resizes" decision.
//   * For-loop and ForEach-loop variable resolve-or-implicit binding.
//   * Implements directive wiring (the harvester pre-builds the
//     ClassDeclaration with ImplementedInterfaces before calling this
//     visitor; the directive is a no-op during the walk).
//
// Per the spec's Feature 2 → Standards: "the scope tree's shape is the
// input #8 consumes; bias toward shape stability" — additions to the
// surface in later sessions should be additive, not reshaped.
public sealed class ScopeBuildingVisitor : VbaSyntaxWalker
{
    private readonly Stack<Scope> _scopeStack = new();
    private ModuleScope _currentModule = null!;
    private bool _used;

    private Scope CurrentScope => _scopeStack.Peek();

    // The nearest enclosing ProcedureScope on the stack, or null if no
    // procedure is currently being walked. With and ForEach scopes are
    // skipped: locals inside their bodies still belong to the
    // surrounding procedure.
    private ProcedureScope? CurrentProcedureScope
    {
        get
        {
            foreach (var s in _scopeStack)
            {
                if (s is ProcedureScope p)
                    return p;
            }
            return null;
        }
    }

    // Public entry point. Creates a ModuleScope under `project` for
    // `module`, walks the module body, and returns the populated
    // ModuleScope. The visitor instance is single-use; calling this
    // twice on the same instance throws.
    //
    // `moduleName`, `kind`, and `owningClass` come from the harvester
    // — they are not on the parser AST because VBA modules do not name
    // themselves in the body, and a class-module-vs-standard-module
    // distinction is a file-system fact, not a syntactic one.
    public ModuleScope BuildModuleScope(
        ProjectScope project,
        Module module,
        string moduleName,
        ModuleKind kind,
        Declarations.ClassDeclaration? owningClass = null)
    {
        if (project is null) throw new ArgumentNullException(nameof(project));
        if (module is null) throw new ArgumentNullException(nameof(module));
        if (moduleName is null) throw new ArgumentNullException(nameof(moduleName));
        if (_used)
            throw new InvalidOperationException(
                "ScopeBuildingVisitor is single-use; create a new instance per module.");
        _used = true;

        // Pre-scan Option directives so the ModuleScope's Option fields
        // are populated at construction time and the scope's surface
        // stays immutable through the walk.
        var (optionExplicit, optionPrivateModule, optionBase, optionCompare)
            = ExtractOptionState(module);

        var moduleScope = new ModuleScope(
            parent: project,
            moduleName: moduleName,
            kind: kind,
            range: ToSourceRange(module.Provenance),
            isOptionExplicit: optionExplicit,
            isOptionPrivateModule: optionPrivateModule,
            optionBase: optionBase,
            optionCompare: optionCompare,
            owningClass: owningClass);

        _currentModule = moduleScope;
        _scopeStack.Push(moduleScope);
        try
        {
            foreach (var item in module.Items)
                Visit(item);
        }
        finally
        {
            _scopeStack.Pop();
        }
        return moduleScope;
    }

    private static (bool Explicit, bool PrivateModule, int Base, OptionCompare Compare)
        ExtractOptionState(Module module)
    {
        bool optionExplicit = false;
        bool optionPrivateModule = false;
        int optionBase = 0;
        OptionCompare optionCompare = OptionCompare.Binary;
        foreach (var item in module.Items)
        {
            if (item is not OptionDirective od) continue;
            switch (od.Kind)
            {
                case OptionKind.Explicit:
                    optionExplicit = true;
                    break;
                case OptionKind.PrivateModule:
                    optionPrivateModule = true;
                    break;
                case OptionKind.Base:
                    if (int.TryParse(od.Argument, out var b))
                        optionBase = b;
                    break;
                case OptionKind.Compare:
                    optionCompare = od.Argument switch
                    {
                        "Text"     => OptionCompare.Text,
                        "Database" => OptionCompare.Database,
                        _          => OptionCompare.Binary,
                    };
                    break;
            }
        }
        return (optionExplicit, optionPrivateModule, optionBase, optionCompare);
    }

    // ── Module-level items the walker must NOT recurse into ──────────
    // OptionDirective and ImplementsDirective are pre-processed (Option
    // by ExtractOptionState above; Implements by the harvester building
    // the ClassDeclaration before the walk). Preprocessor directives
    // are pass-through at the resolver layer.
    public override void VisitOptionDirective(OptionDirective n) { }
    public override void VisitImplementsDirective(ImplementsDirective n) { }
    public override void VisitPreprocessorDirective(PreprocessorDirective n) { }
    public override void VisitPreprocessorStatement(PreprocessorStatement n) { }

    // ── Module-level declarations ────────────────────────────────────

    public override void VisitModuleVariableDeclaration(ModuleVariableDeclaration n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForModuleMember(n.Access);
        foreach (var d in n.Declarators)
        {
            var decl = new Declarations.VariableDeclaration(
                name: d.Name,
                provenance: ToSourceProvenance(d.Provenance),
                declarationScope: Declarations.VariableScope.Module,
                access: access,
                isDefaultedAccess: defaulted,
                isStatic: false,
                isWithEvents: d.WithEvents,
                isAutoInstantiated: d.IsAutoInstantiated,
                type: d.Type,
                typeSuffix: d.TypeSuffix,
                isArray: d.IsArray,
                arrayBounds: d.ArrayBounds);
            CurrentScope.AddDeclaration(decl);
        }
    }

    public override void VisitModuleConstantDeclaration(ModuleConstantDeclaration n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForModuleMember(n.Access);
        foreach (var d in n.Declarators)
        {
            var decl = new Declarations.ConstantDeclaration(
                name: d.Name,
                provenance: ToSourceProvenance(d.Provenance),
                declarationScope: Declarations.VariableScope.Module,
                access: access,
                isDefaultedAccess: defaulted,
                type: d.Type,
                typeSuffix: d.TypeSuffix,
                value: d.Value);
            CurrentScope.AddDeclaration(decl);
        }
    }

    public override void VisitTypeDeclaration(TypeDeclaration n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForModuleMember(n.Access);
        var members = new List<Declarations.TypeMemberDeclaration>(n.Members.Count);
        foreach (var m in n.Members)
        {
            members.Add(new Declarations.TypeMemberDeclaration(
                name: m.Name,
                provenance: ToSourceProvenance(m.Provenance),
                type: m.Type,
                isArray: m.IsArray,
                arrayBounds: m.ArrayBounds));
        }
        var decl = new Declarations.UserDefinedTypeDeclaration(
            name: n.Name,
            provenance: ToSourceProvenance(n.Provenance),
            access: access,
            isDefaultedAccess: defaulted,
            members: members);
        CurrentScope.AddDeclaration(decl);
        // TypeMembers' Scope back-pointer stays null at this layer:
        // member-access reference resolution (#8) walks them via
        // udt.Members rather than via the lexical scope chain.
    }

    public override void VisitEnumDeclaration(EnumDeclaration n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForModuleMember(n.Access);
        var members = new List<Declarations.EnumMemberDeclaration>(n.Members.Count);
        foreach (var m in n.Members)
        {
            members.Add(new Declarations.EnumMemberDeclaration(
                name: m.Name,
                provenance: ToSourceProvenance(m.Provenance),
                value: m.Value));
        }
        var decl = new Declarations.EnumDeclaration(
            name: n.Name,
            provenance: ToSourceProvenance(n.Provenance),
            access: access,
            isDefaultedAccess: defaulted,
            members: members);
        CurrentScope.AddDeclaration(decl);
    }

    public override void VisitDeclareStatement(DeclareStatement n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForModuleMember(n.Access);
        var parameters = BuildParameters(n.Parameters);
        var decl = new Declarations.DeclareDeclaration(
            name: n.Name,
            provenance: ToSourceProvenance(n.Provenance),
            access: access,
            isDefaultedAccess: defaulted,
            isPtrSafe: n.IsPtrSafe,
            kind: n.Kind,
            libraryName: n.LibraryName,
            aliasName: n.AliasName,
            parameters: parameters,
            returnType: n.ReturnType);
        CurrentScope.AddDeclaration(decl);
        // Declare has no body — its parameters intentionally have a
        // null Scope back-pointer at this layer. Adding them to the
        // ModuleScope's Declarations would let module-level
        // TryFindLocal resolve a use-site to a Declare parameter, which
        // is wrong (Declare parameters are only meaningful at call
        // sites and bind via decl.Parameters). Setting Scope to the
        // module without registering them would break the bidirectional
        // invariant Scope.AddDeclaration enforces. Null is the
        // consistent third option.
    }

    public override void VisitEventDeclaration(EventDeclaration n)
    {
        // Per VBA, only Public Event is legal — Private Event is a
        // syntax error and the parser would not produce an
        // EventDeclaration with Access=Private. When the source omitted
        // the keyword, the parser forwards Unspecified; we record
        // Public + IsDefaulted=true.
        Declarations.AccessLevel access;
        bool defaulted;
        if (n.Access == AccessModifier.Unspecified)
        {
            access = Declarations.AccessLevel.Public;
            defaulted = true;
        }
        else
        {
            (access, defaulted) = AccessLevelCanonicalizer.ForModuleMember(n.Access);
        }
        var parameters = BuildParameters(n.Parameters);
        var decl = new Declarations.EventDeclaration(
            name: n.Name,
            provenance: ToSourceProvenance(n.Provenance),
            access: access,
            isDefaultedAccess: defaulted,
            parameters: parameters);
        CurrentScope.AddDeclaration(decl);
        // Event has no body — parameters keep Scope=null per the same
        // rationale as Declare (see VisitDeclareStatement above).
    }

    // ── Procedures ───────────────────────────────────────────────────

    public override void VisitSubProcedure(SubProcedure n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForProcedure(n.Access);
        var parameters = BuildParameters(n.Parameters);
        var decl = new Declarations.SubDeclaration(
            name: n.Name,
            provenance: ToSourceProvenance(n.Provenance),
            access: access,
            isDefaultedAccess: defaulted,
            isStatic: n.IsStatic,
            parameters: parameters);
        EnterProcedure(decl, n.Provenance, parameters, n.Body);
    }

    public override void VisitFunctionProcedure(FunctionProcedure n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForProcedure(n.Access);
        var parameters = BuildParameters(n.Parameters);
        var decl = new Declarations.FunctionDeclaration(
            name: n.Name,
            provenance: ToSourceProvenance(n.Provenance),
            access: access,
            isDefaultedAccess: defaulted,
            isStatic: n.IsStatic,
            parameters: parameters,
            returnType: n.ReturnType);
        EnterProcedure(decl, n.Provenance, parameters, n.Body);
    }

    public override void VisitPropertyProcedure(PropertyProcedure n)
    {
        var (access, defaulted) = AccessLevelCanonicalizer.ForProcedure(n.Access);
        var parameters = BuildParameters(n.Parameters);
        var decl = new Declarations.PropertyDeclaration(
            name: n.Name,
            provenance: ToSourceProvenance(n.Provenance),
            access: access,
            isDefaultedAccess: defaulted,
            isStatic: n.IsStatic,
            kind: n.Kind,
            parameters: parameters,
            returnType: n.ReturnType);
        EnterProcedure(decl, n.Provenance, parameters, n.Body);
    }

    private void EnterProcedure(
        Declarations.ProcedureDeclaration decl,
        Provenance provenance,
        IReadOnlyList<Declarations.ParameterDeclaration> parameters,
        IReadOnlyList<Statement> body)
    {
        _currentModule.AddDeclaration(decl);
        var procScope = new ProcedureScope(
            parent: _currentModule,
            procedure: decl,
            range: ToSourceRange(provenance));
        foreach (var p in parameters)
            procScope.AddDeclaration(p);
        _scopeStack.Push(procScope);
        try
        {
            foreach (var stmt in body)
                Visit(stmt);
        }
        finally
        {
            _scopeStack.Pop();
        }
    }

    // ── Body-level scopes ────────────────────────────────────────────

    public override void VisitWithStatement(WithStatement n)
    {
        var withScope = new WithBlockScope(
            parent: CurrentScope,
            target: n.Target,
            range: ToSourceRange(n.Provenance));
        _scopeStack.Push(withScope);
        try
        {
            foreach (var stmt in n.Body)
                Visit(stmt);
        }
        finally
        {
            _scopeStack.Pop();
        }
    }

    public override void VisitForEachStatement(ForEachStatement n)
    {
        var forScope = new ForEachScope(
            parent: CurrentScope,
            loopVariable: n.LoopVariable,
            loopVariableTypeSuffix: n.LoopVariableTypeSuffix,
            range: ToSourceRange(n.Provenance));
        _scopeStack.Push(forScope);
        try
        {
            foreach (var stmt in n.Body)
                Visit(stmt);
        }
        finally
        {
            _scopeStack.Pop();
        }
    }

    // ── Local declarations ───────────────────────────────────────────

    public override void VisitLocalDeclarationStatement(LocalDeclarationStatement n)
    {
        var procScope = CurrentProcedureScope
            ?? throw new InvalidOperationException(
                "LocalDeclarationStatement encountered outside any ProcedureScope.");
        bool isStatic = n.Kind == LocalDeclarationKind.Static;
        foreach (var d in n.Declarators)
        {
            var decl = new Declarations.VariableDeclaration(
                name: d.Name,
                provenance: ToSourceProvenance(d.Provenance),
                declarationScope: Declarations.VariableScope.Local,
                access: Declarations.AccessLevel.Private,
                isDefaultedAccess: false,
                isStatic: isStatic,
                isWithEvents: d.WithEvents,
                isAutoInstantiated: d.IsAutoInstantiated,
                type: d.Type,
                typeSuffix: d.TypeSuffix,
                isArray: d.IsArray,
                arrayBounds: d.ArrayBounds);
            procScope.AddDeclaration(decl);
        }
    }

    public override void VisitLocalConstantStatement(LocalConstantStatement n)
    {
        var procScope = CurrentProcedureScope
            ?? throw new InvalidOperationException(
                "LocalConstantStatement encountered outside any ProcedureScope.");
        foreach (var d in n.Declarators)
        {
            var decl = new Declarations.ConstantDeclaration(
                name: d.Name,
                provenance: ToSourceProvenance(d.Provenance),
                declarationScope: Declarations.VariableScope.Local,
                access: Declarations.AccessLevel.Private,
                isDefaultedAccess: false,
                type: d.Type,
                typeSuffix: d.TypeSuffix,
                value: d.Value);
            procScope.AddDeclaration(decl);
        }
    }

    // RedimStatement is intentionally a no-op at this layer. Whether a
    // ReDim re-sizes an existing variable or implicitly declares a new
    // one depends on whether the name is already declared in the
    // enclosing scope chain — that is reference-resolution context
    // (#8). Suppressing the walker's default recursion here also keeps
    // the visitor from descending into bound expressions whose
    // identifier uses are not yet resolvable.
    public override void VisitRedimStatement(RedimStatement n) { }

    // ── Helpers ──────────────────────────────────────────────────────

    private static List<Declarations.ParameterDeclaration> BuildParameters(
        IReadOnlyList<Parameter> astParameters)
    {
        var result = new List<Declarations.ParameterDeclaration>(astParameters.Count);
        foreach (var p in astParameters)
        {
            result.Add(new Declarations.ParameterDeclaration(
                name: p.Name,
                provenance: ToSourceProvenance(p.Provenance),
                passMode: p.PassMode,
                modifier: p.Modifier,
                type: p.Type,
                typeSuffix: p.TypeSuffix,
                isArray: p.IsArray,
                defaultValue: p.DefaultValue));
        }
        return result;
    }

    // Materialises a SourceDeclarationProvenance from a parser
    // Provenance. Provenance.Range is nullable in this transitional
    // session (parser-infrastructure Session 3 added it as additive),
    // so when it is absent we synthesise a degenerate single-character
    // SourceRange from Line+Column. Once the parser populates Range
    // unconditionally this fallback can be removed.
    private static Declarations.SourceDeclarationProvenance ToSourceProvenance(Provenance p)
        => new(ToSourceRange(p));

    // Mirrors ToSourceProvenance for the scope-construction path: every
    // non-root Scope must have a non-null SourceRange (Feature 2's
    // "source range" requirement; only ProjectScope is the synthetic
    // exception). Provenance.Range is still nullable, so we synthesise
    // a degenerate single-character range from Line+Column when it is
    // absent.
    private static SourceRange ToSourceRange(Provenance p)
        => p.Range ?? new SourceRange(
            new SourceLocation("", p.Line, p.Column),
            new SourceLocation("", p.Line, p.Column + 1));
}
