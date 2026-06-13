using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace AccessHarvester.Vba.Parser.Ast;

// Walks an ANTLR parse tree and produces a Module AST. Per
// docs/session-sets/parser-infrastructure/ast-shape.md §6.
//
// Determinism: same parse tree input always produces the same AST.
// Position fields come from ANTLR token line/column (deterministic).
//
// Recovery: any AST node whose underlying parse-tree subtree contains
// an IErrorNode is tagged RecoveredProvenance. The reason string is
// the message of the nearest preceding ParseDiagnostic (matched by
// line/column). When no preceding diagnostic exists the reason is
// "unknown-recovery".
internal sealed class AstBuilder
{
    private readonly IReadOnlyList<ParseDiagnostic> _diagnostics;
    private readonly string _sourceFile;

    public AstBuilder(IReadOnlyList<ParseDiagnostic> diagnostics, string sourceFile = "")
    {
        _diagnostics = diagnostics;
        _sourceFile = sourceFile;
    }

    public Module BuildModule(VbaParser.ModuleContext ctx)
    {
        var items = new List<ModuleItem>();
        foreach (var item in ctx.moduleItem())
        {
            var built = BuildModuleItem(item);
            if (built is not null) items.Add(built);
        }
        return new Module(MakeProvenance(ctx), items);
    }

    // ── Module items ──────────────────────────────────────────────

    private ModuleItem? BuildModuleItem(VbaParser.ModuleItemContext item)
    {
        if (item is VbaParser.BlankLineItemContext)
            return null;                                // blank lines do not become AST nodes

        try
        {
            switch (item)
            {
                case VbaParser.OptionItemContext n:
                    return BuildOption(n.optionStatement());
                case VbaParser.ImplementsItemContext n:
                    return BuildImplements(n.implementsStatement());
                case VbaParser.TypeItemContext n:
                    return BuildTypeDeclaration(n.typeDeclaration());
                case VbaParser.EnumItemContext n:
                    return BuildEnumDeclaration(n.enumDeclaration());
                case VbaParser.DeclareItemContext n:
                    return BuildDeclareStatement(n.declareStatement());
                case VbaParser.EventItemContext n:
                    return BuildEventDeclaration(n.publicEventDeclaration());
                case VbaParser.ProcedureItemContext n:
                    return BuildProcedureDeclaration(n.procedureDeclaration());
                case VbaParser.ConstItemContext n:
                    return BuildModuleConstant(n.moduleConstStatement());
                case VbaParser.VariableItemContext n:
                    return BuildModuleVariable(n.moduleVariableDecl());
                case VbaParser.PreprocessorItemContext n:
                    return BuildPreprocessor(n.preprocessorLine());
                default:
                    // Closed-grammar-unreachable alt: a recovered stub keeps
                    // the loss explicit rather than dropping the item.
                    return new UnknownModuleItem(MakeProvenance(item, recoveredOverride: true));
            }
        }
        catch (Exception ex) when (HasErrorNode(item) && IsRecoveryShapeFault(ex))
        {
            // Error recovery mangled this item's subtree beyond what the
            // per-item builder can read — e.g. trailing `Exit Sub`/`End Sub`
            // tokens re-parsed as a procedure start with no name token
            // (006 review F-3-2). The catch is doubly scoped — the subtree
            // must contain a recovery error node AND the fault must be a
            // shape-assumption violation (see IsRecoveryShapeFault) — so a
            // genuine builder bug is NOT silently masked just because the
            // item also happens to contain an unrelated syntax error
            // (006 review H2). Only recovery-shaped trees degrade, and only
            // to a stub for this one item instead of an exception that costs
            // the whole module.
            return new UnknownModuleItem(MakeProvenance(item, recoveredOverride: true));
        }
    }

    // Exceptions a per-item builder can throw when ANTLR error recovery
    // hands it a structurally-mangled subtree: a null name token
    // (`ctx.IDENTIFIER().GetText()`), an empty token list a builder indexes
    // (`literals[0]`), or a single-element accessor on an empty collection.
    // These are shape-assumption violations, not arbitrary builder logic
    // errors — narrowing the BuildModuleItem catch to them (vs a blanket
    // `catch (Exception)`) is the 006 review H2 fix.
    private static bool IsRecoveryShapeFault(Exception ex) =>
        ex is NullReferenceException
            or IndexOutOfRangeException
            or ArgumentOutOfRangeException
            or InvalidOperationException;

    private ModuleItem BuildOption(VbaParser.OptionStatementContext ctx) => ctx switch
    {
        VbaParser.OptionExplicitStmtContext n =>
            new OptionDirective(MakeProvenance(n), OptionKind.Explicit, null),
        VbaParser.OptionCompareStmtContext n =>
            new OptionDirective(MakeProvenance(n), OptionKind.Compare, n.IDENTIFIER()?.GetText()),
        VbaParser.OptionBaseStmtContext n =>
            new OptionDirective(MakeProvenance(n), OptionKind.Base, n.INTEGER_LITERAL()?.GetText()),
        VbaParser.OptionPrivateModuleStmtContext n =>
            new OptionDirective(MakeProvenance(n), OptionKind.PrivateModule, n.IDENTIFIER()?.GetText()),
        // Grammar-unreachable alt: a recovered stub is honest where a
        // synthesized `Option Explicit` would be a silent guess.
        _ => new UnknownModuleItem(MakeProvenance(ctx, recoveredOverride: true)),
    };

    private ImplementsDirective BuildImplements(VbaParser.ImplementsStatementContext ctx) =>
        new(MakeProvenance(ctx), BuildTypeReference(ctx.typeName(), isAutoInstantiated: false));

    private TypeDeclaration BuildTypeDeclaration(VbaParser.TypeDeclarationContext ctx)
    {
        var access = AccessModifierFromHead(ctx.PUBLIC()?.Symbol, ctx.PRIVATE()?.Symbol);
        var members = new List<TypeMember>();
        foreach (var m in ctx.typeMember())
        {
            // typeMember : (IDENT|TYPED_IDENT) (LPAREN arrayBoundsList? RPAREN)? (AS typeSpec)? NEWLINE | NEWLINE
            var name = m.IDENTIFIER()?.GetText() ?? m.TYPED_IDENTIFIER()?.GetText();
            if (name is null) continue;                 // bare-NEWLINE alt
            var type = m.typeSpec() is { } ts ? BuildTypeReference(ts) : null;
            IReadOnlyList<ArrayBound> bounds = m.arrayBoundsList() is { } b ? BuildArrayBounds(b) : Array.Empty<ArrayBound>();
            bool isArray = m.LPAREN() is not null;
            members.Add(new TypeMember(MakeProvenance(m), name, type, bounds, isArray));
        }
        return new TypeDeclaration(MakeProvenance(ctx), access, ctx.IDENTIFIER().GetText(), members);
    }

    private EnumDeclaration BuildEnumDeclaration(VbaParser.EnumDeclarationContext ctx)
    {
        var access = AccessModifierFromHead(ctx.PUBLIC()?.Symbol, ctx.PRIVATE()?.Symbol);
        var members = new List<EnumMember>();
        foreach (var m in ctx.enumMember())
        {
            var name = m.IDENTIFIER()?.GetText();
            if (name is null) continue;
            var value = m.expression() is { } e ? BuildExpression(e) : null;
            members.Add(new EnumMember(MakeProvenance(m), name, value));
        }
        return new EnumDeclaration(MakeProvenance(ctx), access, ctx.IDENTIFIER().GetText(), members);
    }

    private DeclareStatement BuildDeclareStatement(VbaParser.DeclareStatementContext ctx)
    {
        var access = AccessModifierFromHead(ctx.PUBLIC()?.Symbol, ctx.PRIVATE()?.Symbol);
        bool isPtrSafe = ctx.PTRSAFE() is not null;
        var kind = ctx.SUB() is not null ? DeclareKind.Sub : DeclareKind.Function;
        string name = ctx.IDENTIFIER().GetText();
        var literals = ctx.STRING_LITERAL();
        string libName = StripStringLiteral(literals[0].GetText());
        string? aliasName = literals.Length > 1 ? StripStringLiteral(literals[1].GetText()) : null;
        IReadOnlyList<Parameter> parameters = ctx.parameterList() is { } pl ? BuildParameters(pl) : Array.Empty<Parameter>();
        var ret = ctx.typeSpec() is { } rs ? BuildTypeReference(rs) : null;
        return new DeclareStatement(MakeProvenance(ctx), access, isPtrSafe, kind, name, libName, aliasName, parameters, ret);
    }

    private EventDeclaration BuildEventDeclaration(VbaParser.PublicEventDeclarationContext ctx)
    {
        var access = ctx.PUBLIC() is not null ? AccessModifier.Public : AccessModifier.Unspecified;
        string name = ctx.IDENTIFIER().GetText();
        var parameters = new List<Parameter>();
        if (ctx.eventParameterList() is { } epl)
        {
            foreach (var ep in epl.eventParameter())
            {
                var passMode = ep.BYVAL() is not null ? ParameterPassMode.ByVal
                             : ep.BYREF() is not null ? ParameterPassMode.ByRef
                             : ParameterPassMode.Unspecified;
                var (pName, suffix) = SplitDeclaredName(
                    ep.IDENTIFIER()?.GetText() ?? ep.TYPED_IDENTIFIER()?.GetText() ?? ep.contextualKeywordName()?.GetText());
                var type = ep.typeSpec() is { } ts ? BuildTypeReference(ts) : null;
                bool isArray = ep.LPAREN() is not null;
                parameters.Add(new Parameter(MakeProvenance(ep), pName, suffix, passMode, ParameterModifier.Required, type, isArray, null));
            }
        }
        return new EventDeclaration(MakeProvenance(ctx), access, name, parameters);
    }

    private ModuleVariableDeclaration BuildModuleVariable(VbaParser.ModuleVariableDeclContext ctx)
    {
        var head = ctx.moduleVariableDeclHead();
        var access = head.PUBLIC() is not null ? AccessModifier.Public
                   : head.PRIVATE() is not null ? AccessModifier.Private
                   : head.GLOBAL() is not null ? AccessModifier.Global
                   : AccessModifier.Dim;
        var declarators = new List<VariableDeclarator>();
        foreach (var item in ctx.moduleVariableDeclItem())
        {
            declarators.Add(BuildModuleVariableItem(item));
        }
        return new ModuleVariableDeclaration(MakeProvenance(ctx), access, declarators);
    }

    private VariableDeclarator BuildModuleVariableItem(VbaParser.ModuleVariableDeclItemContext ctx)
    {
        switch (ctx)
        {
            case VbaParser.WithEventsVarItemContext we:
            {
                var (name, suffix) = SplitDeclaredName(
                    we.IDENTIFIER()?.GetText() ?? we.TYPED_IDENTIFIER()?.GetText() ?? we.contextualKeywordName()?.GetText());
                var type = BuildTypeReference(we.typeName(), isAutoInstantiated: false);
                return new VariableDeclarator(
                    MakeProvenance(we), name, suffix, WithEvents: true, IsAutoInstantiated: false,
                    type, Array.Empty<ArrayBound>(), IsArray: false);
            }
            case VbaParser.RegularVarItemContext rv:
            {
                var (name, suffix) = SplitDeclaredName(
                    rv.IDENTIFIER()?.GetText() ?? rv.TYPED_IDENTIFIER()?.GetText() ?? rv.contextualKeywordName()?.GetText());
                IReadOnlyList<ArrayBound> bounds = rv.arrayBoundsList() is { } b ? BuildArrayBounds(b) : Array.Empty<ArrayBound>();
                bool isArray = rv.LPAREN() is not null;
                var (type, isNew) = BuildVariableType(rv.variableTypeSpec());
                return new VariableDeclarator(
                    MakeProvenance(rv), name, suffix, WithEvents: false, IsAutoInstantiated: isNew,
                    type, bounds, isArray);
            }
            default:
                return new VariableDeclarator(
                    MakeProvenance(ctx), "", null, false, false, null, Array.Empty<ArrayBound>(), false);
        }
    }

    private ModuleConstantDeclaration BuildModuleConstant(VbaParser.ModuleConstStatementContext ctx)
    {
        var cs = ctx.constStatement();
        var access = cs.PUBLIC() is not null ? AccessModifier.Public
                   : cs.PRIVATE() is not null ? AccessModifier.Private
                   : AccessModifier.Unspecified;
        var declarators = cs.constDecl().Select(BuildConstantDeclarator).ToList();
        return new ModuleConstantDeclaration(MakeProvenance(ctx), access, declarators);
    }

    private ConstantDeclarator BuildConstantDeclarator(VbaParser.ConstDeclContext ctx)
    {
        var (name, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText() ?? ctx.contextualKeywordName()?.GetText());
        var type = ctx.typeSpec() is { } ts ? BuildTypeReference(ts) : null;
        var value = BuildExpression(ctx.expression());
        return new ConstantDeclarator(MakeProvenance(ctx), name, suffix, type, value);
    }

    private PreprocessorStatement BuildPreprocessorStatement(VbaParser.PreprocessorLineContext ctx)
    {
        var (kind, body) = ReadPreprocessorLine(ctx);
        return new PreprocessorStatement(MakeProvenance(ctx), kind, body);
    }

    private PreprocessorDirective BuildPreprocessor(VbaParser.PreprocessorLineContext ctx)
    {
        var (kind, body) = ReadPreprocessorLine(ctx);
        return new PreprocessorDirective(MakeProvenance(ctx), kind, body);
    }

    // Shared by the statement-position and module-item-position
    // preprocessor builders. Body = the source text between the directive
    // head and the line terminator, reconstructed from the token stream
    // rather than ctx.GetText() (which strips whitespace).
    private static (PreprocessorKind Kind, string Body) ReadPreprocessorLine(VbaParser.PreprocessorLineContext ctx)
    {
        var kind = ctx.HASH_IF() is not null ? PreprocessorKind.If
                 : ctx.HASH_ELSEIF() is not null ? PreprocessorKind.ElseIf
                 : ctx.HASH_ELSE() is not null ? PreprocessorKind.Else
                 : ctx.HASH_END_IF() is not null ? PreprocessorKind.EndIf
                 : PreprocessorKind.Const;

        string body = "";
        if (ctx.Start is not null && ctx.Stop is not null && ctx.Start.InputStream is { } stream)
        {
            int headStop = ctx.Start.StopIndex;
            int tailStop = ctx.Stop.StopIndex;
            if (tailStop > headStop)
                body = stream.GetText(new Antlr4.Runtime.Misc.Interval(headStop + 1, tailStop)).TrimStart();
        }
        return (kind, body);
    }

    // ── Procedures ────────────────────────────────────────────────

    private ModuleItem BuildProcedureDeclaration(VbaParser.ProcedureDeclarationContext ctx) => ctx switch
    {
        VbaParser.SubDeclContext n         => BuildSubProcedure(n.subDeclaration()),
        VbaParser.FunctionDeclContext n    => BuildFunctionProcedure(n.functionDeclaration()),
        VbaParser.PropertyGetDeclContext n => BuildPropertyProcedure(n.propertyGetDeclaration(), PropertyKind.Get),
        VbaParser.PropertyLetDeclContext n => BuildPropertyProcedure(n.propertyLetDeclaration(), PropertyKind.Let),
        VbaParser.PropertySetDeclContext n => BuildPropertyProcedure(n.propertySetDeclaration(), PropertyKind.Set),
        _ => new UnknownModuleItem(MakeProvenance(ctx, recoveredOverride: true)),
    };

    // Procedure name tokens are null when error recovery synthesized the
    // declaration context without one (e.g. orphaned `End Sub` tokens
    // re-parsed as a procedure start — 006 review F-3-2). Each builder
    // degrades that case to a recovered stub instead of an NRE.

    private ModuleItem BuildSubProcedure(VbaParser.SubDeclarationContext ctx)
    {
        string? name = ctx.IDENTIFIER()?.GetText();
        if (name is null)
            return new UnknownModuleItem(MakeProvenance(ctx, recoveredOverride: true));
        var access = AccessFromProcedureModifier(ctx.procedureModifier());
        bool isStatic = ctx.STATIC() is not null;
        IReadOnlyList<Parameter> parameters = ctx.parameterList() is { } pl ? BuildParameters(pl) : Array.Empty<Parameter>();
        var body = BuildStatementList(ctx.statementListBody());
        return new SubProcedure(MakeProvenance(ctx), name, access, isStatic, parameters, body);
    }

    private ModuleItem BuildFunctionProcedure(VbaParser.FunctionDeclarationContext ctx)
    {
        // `Function SqlStr$(...)` declares the return type via the name's
        // type-declaration character; Name carries the stripped form so
        // references resolve against it (006 review F-3-2 / F-3-3).
        string? raw = ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText();
        if (raw is null)
            return new UnknownModuleItem(MakeProvenance(ctx, recoveredOverride: true));
        var (name, suffix) = SplitDeclaredName(raw);
        var access = AccessFromProcedureModifier(ctx.procedureModifier());
        bool isStatic = ctx.STATIC() is not null;
        IReadOnlyList<Parameter> parameters = ctx.parameterList() is { } pl ? BuildParameters(pl) : Array.Empty<Parameter>();
        // Explicit `As` wins; otherwise derive the return type from the
        // name suffix so `Function SqlStr$()` reports String, not null.
        var ret = ctx.typeSpec() is { } ts ? BuildTypeReference(ts) : TypeReferenceFromSuffix(suffix, ctx);
        var body = BuildStatementList(ctx.statementListBody());
        return new FunctionProcedure(MakeProvenance(ctx), name, access, isStatic, parameters, ret, body)
        {
            TypeSuffix = suffix,
        };
    }

    private ModuleItem BuildPropertyProcedure(VbaParser.PropertyGetDeclarationContext ctx, PropertyKind kind)
    {
        string? raw = ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText();
        if (raw is null)
            return new UnknownModuleItem(MakeProvenance(ctx, recoveredOverride: true));
        var (name, suffix) = SplitDeclaredName(raw);
        var access = AccessFromProcedureModifier(ctx.procedureModifier());
        bool isStatic = ctx.STATIC() is not null;
        IReadOnlyList<Parameter> parameters = ctx.parameterList() is { } pl ? BuildParameters(pl) : Array.Empty<Parameter>();
        // Explicit `As` wins; otherwise derive the return type from the
        // name suffix so `Property Get Title$()` reports String, not null.
        var ret = ctx.typeSpec() is { } ts ? BuildTypeReference(ts) : TypeReferenceFromSuffix(suffix, ctx);
        var body = BuildStatementList(ctx.statementListBody());
        return new PropertyProcedure(MakeProvenance(ctx), name, access, isStatic, kind, parameters, ret, body)
        {
            TypeSuffix = suffix,
        };
    }

    private ModuleItem BuildPropertyProcedure(VbaParser.PropertyLetDeclarationContext ctx, PropertyKind kind)
    {
        string? name = ctx.IDENTIFIER()?.GetText();
        if (name is null)
            return new UnknownModuleItem(MakeProvenance(ctx, recoveredOverride: true));
        var access = AccessFromProcedureModifier(ctx.procedureModifier());
        bool isStatic = ctx.STATIC() is not null;
        var parameters = BuildParameters(ctx.parameterList());
        var body = BuildStatementList(ctx.statementListBody());
        return new PropertyProcedure(MakeProvenance(ctx), name, access, isStatic, kind, parameters, null, body);
    }

    private ModuleItem BuildPropertyProcedure(VbaParser.PropertySetDeclarationContext ctx, PropertyKind kind)
    {
        string? name = ctx.IDENTIFIER()?.GetText();
        if (name is null)
            return new UnknownModuleItem(MakeProvenance(ctx, recoveredOverride: true));
        var access = AccessFromProcedureModifier(ctx.procedureModifier());
        bool isStatic = ctx.STATIC() is not null;
        var parameters = BuildParameters(ctx.parameterList());
        var body = BuildStatementList(ctx.statementListBody());
        return new PropertyProcedure(MakeProvenance(ctx), name, access, isStatic, kind, parameters, null, body);
    }

    private AccessModifier AccessFromProcedureModifier(VbaParser.ProcedureModifierContext? mod)
    {
        if (mod is null) return AccessModifier.Unspecified;
        if (mod.PUBLIC() is not null) return AccessModifier.Public;
        if (mod.PRIVATE() is not null) return AccessModifier.Private;
        if (mod.FRIEND() is not null) return AccessModifier.Friend;
        return AccessModifier.Unspecified;
    }

    // ── Parameters ────────────────────────────────────────────────

    private List<Parameter> BuildParameters(VbaParser.ParameterListContext ctx)
    {
        var parameters = new List<Parameter>();
        if (ctx.positionalParameters() is { } pp)
        {
            foreach (var rp in pp.requiredParameter())
                parameters.Add(BuildRequiredParameter(rp));
            foreach (var op in pp.optionalParameter())
                parameters.Add(BuildOptionalParameter(op));
        }
        if (ctx.paramArrayParameter() is { } pa)
            parameters.Add(BuildParamArrayParameter(pa));
        return parameters;
    }

    private Parameter BuildRequiredParameter(VbaParser.RequiredParameterContext ctx)
    {
        var passMode = ctx.BYVAL() is not null ? ParameterPassMode.ByVal
                     : ctx.BYREF() is not null ? ParameterPassMode.ByRef
                     : ParameterPassMode.Unspecified;
        var (name, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText() ?? ctx.contextualKeywordName()?.GetText());
        var type = ctx.typeSpec() is { } ts ? BuildTypeReference(ts) : null;
        bool isArray = ctx.LPAREN() is not null;
        return new Parameter(MakeProvenance(ctx), name, suffix, passMode, ParameterModifier.Required, type, isArray, null);
    }

    private Parameter BuildOptionalParameter(VbaParser.OptionalParameterContext ctx)
    {
        var passMode = ctx.BYVAL() is not null ? ParameterPassMode.ByVal
                     : ctx.BYREF() is not null ? ParameterPassMode.ByRef
                     : ParameterPassMode.Unspecified;
        var (name, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText() ?? ctx.contextualKeywordName()?.GetText());
        var type = ctx.typeSpec() is { } ts ? BuildTypeReference(ts) : null;
        bool isArray = ctx.LPAREN() is not null;
        var defaultValue = ctx.expression() is { } e ? BuildExpression(e) : null;
        return new Parameter(MakeProvenance(ctx), name, suffix, passMode, ParameterModifier.Optional, type, isArray, defaultValue);
    }

    private Parameter BuildParamArrayParameter(VbaParser.ParamArrayParameterContext ctx)
    {
        var (name, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText() ?? ctx.contextualKeywordName()?.GetText());
        var type = ctx.typeSpec() is { } ts ? BuildTypeReference(ts) : null;
        return new Parameter(MakeProvenance(ctx), name, suffix, ParameterPassMode.Unspecified, ParameterModifier.ParamArray, type, IsArray: true, null);
    }

    // ── Statements ────────────────────────────────────────────────

    private List<Statement> BuildStatementList(VbaParser.StatementListBodyContext ctx)
    {
        var result = new List<Statement>();
        foreach (var child in ctx.children ?? Enumerable.Empty<IParseTree>())
        {
            switch (child)
            {
                case VbaParser.BlockContext bc:
                    AppendBlock(bc, result);
                    break;
                case VbaParser.PreprocessorLineContext pp:
                    result.Add(BuildPreprocessorStatement(pp));
                    break;
                // NEWLINEs are skipped silently.
            }
        }
        return result;
    }

    private void AppendBlock(VbaParser.BlockContext block, List<Statement> output)
    {
        if (block.statementLabel() is { } lbl)
            output.Add(BuildLabel(lbl));
        if (block.singleLineStatementList() is { } ssl)
        {
            foreach (var stmt in ssl.statement())
                output.Add(BuildStatement(stmt));
        }
    }

    private LabelStatement BuildLabel(VbaParser.StatementLabelContext ctx)
    {
        string name = ctx.IDENTIFIER()?.GetText() ?? ctx.INTEGER_LITERAL()?.GetText() ?? "";
        return new LabelStatement(MakeProvenance(ctx), name);
    }

    private Statement BuildStatement(VbaParser.StatementContext ctx)
    {
        if (HasErrorNode(ctx))
            return new UnknownStatement(MakeProvenance(ctx, recoveredOverride: true));

        return ctx switch
        {
            VbaParser.IfBlockStmtContext n        => BuildIfBlock(n.ifBlockStatement()),
            VbaParser.IfSingleLineStmtContext n   => BuildIfSingleLine(n.ifSingleLineStatement()),
            VbaParser.ForNextStmtContext n        => BuildForNext(n.forNextStatement()),
            VbaParser.ForEachStmtContext n        => BuildForEach(n.forEachStatement()),
            VbaParser.DoLoopStmtContext n         => BuildDoLoop(n.doLoopStatement()),
            VbaParser.WhileWendStmtContext n      => BuildWhileWend(n.whileWendStatement()),
            VbaParser.SelectCaseStmtContext n     => BuildSelectCase(n.selectCaseStatement()),
            VbaParser.WithStmtContext n           => BuildWith(n.withStatement()),
            VbaParser.OnErrorStmtContext n        => BuildOnError(n.onErrorStatement()),
            VbaParser.ResumeStmtContext n         => BuildResume(n.resumeStatement()),
            VbaParser.GotoStmtContext n           => BuildGoto(n.gotoStatement()),
            VbaParser.GosubStmtContext n          => BuildGosub(n.gosubStatement()),
            VbaParser.ReturnStmtContext n         => new ReturnStatement(MakeProvenance(n)),
            VbaParser.ExitStmtContext n           => BuildExit(n.exitStatement()),
            VbaParser.StopStmtContext n           => new StopStatement(MakeProvenance(n)),
            VbaParser.EndStmtContext n            => new EndStatement(MakeProvenance(n)),
            VbaParser.RaiseEventStmtContext n     => BuildRaiseEvent(n.raiseEventStatement()),
            VbaParser.LocalDeclStmtContext n      => BuildLocalDecl(n.localDeclStatement()),
            VbaParser.RedimStmtContext n          => BuildRedim(n.redimStatement()),
            VbaParser.LocalConstStmtContext n     => BuildLocalConst(n.localConstStatement()),
            VbaParser.SetStmtContext n            => BuildSet(n.setStatement()),
            VbaParser.LetStmtContext n            => BuildLet(n.letStatement()),
            VbaParser.CallStmtContext n           => BuildCall(n.callStatement()),
            _                                     => new UnknownStatement(MakeProvenance(ctx, recoveredOverride: true)),
        };
    }

    // Inline statements share most variants with the top-level
    // statement rule but live under a distinct grammar production.
    private Statement BuildInlineStatement(VbaParser.InlineStatementContext ctx)
    {
        if (HasErrorNode(ctx))
            return new UnknownStatement(MakeProvenance(ctx, recoveredOverride: true));
        if (ctx.letStatement() is { } let) return BuildLet(let);
        if (ctx.setStatement() is { } set) return BuildSet(set);
        if (ctx.callStatement() is { } call) return BuildCall(call);
        if (ctx.gotoStatement() is { } gt) return BuildGoto(gt);
        if (ctx.gosubStatement() is { } gs) return BuildGosub(gs);
        if (ctx.returnStatement() is not null) return new ReturnStatement(MakeProvenance(ctx));
        if (ctx.exitStatement() is { } ex) return BuildExit(ex);
        if (ctx.stopStatement() is not null) return new StopStatement(MakeProvenance(ctx));
        if (ctx.endStatement() is not null) return new EndStatement(MakeProvenance(ctx));
        if (ctx.onErrorStatement() is { } oe) return BuildOnError(oe);
        if (ctx.resumeStatement() is { } re) return BuildResume(re);
        if (ctx.raiseEventStatement() is { } ree) return BuildRaiseEvent(ree);
        if (ctx.localDeclStatement() is { } ld) return BuildLocalDecl(ld);
        if (ctx.redimStatement() is { } rd) return BuildRedim(rd);
        if (ctx.localConstStatement() is { } lc) return BuildLocalConst(lc);
        return new UnknownStatement(MakeProvenance(ctx, recoveredOverride: true));
    }

    private IfStatement BuildIfBlock(VbaParser.IfBlockStatementContext ctx)
    {
        var conds = ctx.expression();
        var bodies = ctx.statementListBody();
        var thenBody = BuildStatementList(bodies[0]);

        var elseIfClauses = new List<ElseIfClause>();
        int elseIfCount = ctx.ELSEIF().Length;
        for (int i = 0; i < elseIfCount; i++)
        {
            var cond = BuildExpression(conds[i + 1]);
            var body = BuildStatementList(bodies[i + 1]);
            elseIfClauses.Add(new ElseIfClause(MakeProvenance(ctx.ELSEIF()[i].Symbol), cond, body));
        }

        IReadOnlyList<Statement>? elseBody = null;
        if (ctx.ELSE() is not null)
            elseBody = BuildStatementList(bodies[bodies.Length - 1]);

        return new IfStatement(
            MakeProvenance(ctx),
            BuildExpression(conds[0]),
            thenBody, elseIfClauses, elseBody, IsSingleLine: false);
    }

    private IfStatement BuildIfSingleLine(VbaParser.IfSingleLineStatementContext ctx)
    {
        var cond = BuildExpression(ctx.expression());
        var inlineLists = ctx.inlineStatementList();
        var thenBody = inlineLists[0].inlineStatement().Select(BuildInlineStatement).ToList();
        IReadOnlyList<Statement>? elseBody = inlineLists.Length > 1
            ? inlineLists[1].inlineStatement().Select(BuildInlineStatement).ToList()
            : null;
        return new IfStatement(MakeProvenance(ctx), cond, thenBody, Array.Empty<ElseIfClause>(), elseBody, IsSingleLine: true);
    }

    private ForStatement BuildForNext(VbaParser.ForNextStatementContext ctx)
    {
        var (varName, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER().FirstOrDefault()?.GetText()
            ?? ctx.TYPED_IDENTIFIER().FirstOrDefault()?.GetText()
            ?? ctx.contextualKeywordName().FirstOrDefault()?.GetText());
        var exprs = ctx.expression();
        var from = BuildExpression(exprs[0]);
        var to = BuildExpression(exprs[1]);
        var step = exprs.Length > 2 ? BuildExpression(exprs[2]) : null;
        var body = BuildStatementList(ctx.statementListBody());
        return new ForStatement(MakeProvenance(ctx), varName, suffix, from, to, step, body);
    }

    private ForEachStatement BuildForEach(VbaParser.ForEachStatementContext ctx)
    {
        var (varName, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER().FirstOrDefault()?.GetText()
            ?? ctx.TYPED_IDENTIFIER().FirstOrDefault()?.GetText()
            ?? ctx.contextualKeywordName().FirstOrDefault()?.GetText());
        var collection = BuildExpression(ctx.expression());
        var body = BuildStatementList(ctx.statementListBody());
        return new ForEachStatement(MakeProvenance(ctx), varName, suffix, collection, body);
    }

    private DoLoopStatement BuildDoLoop(VbaParser.DoLoopStatementContext ctx)
    {
        // Two optional doLoopCondition slots: leading (pre-test) and trailing (post-test).
        var conditions = ctx.doLoopCondition();
        var body = BuildStatementList(ctx.statementListBody());

        // The grammar lets either or both conditions be present. Any
        // two-condition source is a downstream concern; we model it as
        // PreTest if one is present, with the leading condition winning.
        DoLoopKind kind;
        Expression? cond = null;
        if (conditions.Length == 0)
        {
            kind = DoLoopKind.Infinite;
        }
        else
        {
            // Determine if the condition appears before or after the body
            // by comparing token positions. The leading condition starts
            // before the body; trailing starts after.
            var firstCond = conditions[0];
            bool isLeading = firstCond.Start.TokenIndex < ctx.statementListBody().Start.TokenIndex;
            cond = BuildExpression(firstCond.expression());
            bool isUntil = firstCond.UNTIL() is not null;
            kind = isLeading
                ? (isUntil ? DoLoopKind.PreTestUntil : DoLoopKind.PreTestWhile)
                : (isUntil ? DoLoopKind.PostTestUntil : DoLoopKind.PostTestWhile);
        }
        return new DoLoopStatement(MakeProvenance(ctx), kind, cond, body);
    }

    private WhileWendStatement BuildWhileWend(VbaParser.WhileWendStatementContext ctx)
    {
        var cond = BuildExpression(ctx.expression());
        var body = BuildStatementList(ctx.statementListBody());
        return new WhileWendStatement(MakeProvenance(ctx), cond, body);
    }

    private SelectCaseStatement BuildSelectCase(VbaParser.SelectCaseStatementContext ctx)
    {
        var selector = BuildExpression(ctx.expression());
        var cases = ctx.caseClause().Select(BuildCaseClause).ToList();
        IReadOnlyList<Statement>? elseBody = ctx.caseElseClause() is { } ce
            ? BuildCaseClauseBody(ce.caseClauseBody())
            : null;
        return new SelectCaseStatement(MakeProvenance(ctx), selector, cases, elseBody);
    }

    private CaseClause BuildCaseClause(VbaParser.CaseClauseContext ctx)
    {
        var exprs = ctx.caseExpressionList().caseExpression().Select(BuildCaseExpression).ToList();
        var body = BuildCaseClauseBody(ctx.caseClauseBody());
        return new CaseClause(MakeProvenance(ctx), exprs, body);
    }

    private List<Statement> BuildCaseClauseBody(VbaParser.CaseClauseBodyContext ctx)
    {
        var result = new List<Statement>();
        if (ctx.inlineStatementList() is { } inline)
            result.AddRange(inline.inlineStatement().Select(BuildInlineStatement));
        if (ctx.statementListBody() is { } body)
            result.AddRange(BuildStatementList(body));
        return result;
    }

    private CaseExpression BuildCaseExpression(VbaParser.CaseExpressionContext ctx) => ctx switch
    {
        VbaParser.CaseRangeContext n =>
            new CaseRangeExpression(MakeProvenance(n),
                BuildExpression(n.expression(0)), BuildExpression(n.expression(1))),
        VbaParser.CaseIsCompareContext n =>
            ComparisonOperatorFromCtx(n.comparisonOperator()) is { } op
                ? new CaseIsCompareExpression(MakeProvenance(n), op, BuildExpression(n.expression()))
                : new CaseValueExpression(MakeProvenance(n),
                    new UnknownExpression(MakeProvenance(n, recoveredOverride: true))),
        VbaParser.CaseValueContext n =>
            new CaseValueExpression(MakeProvenance(n), BuildExpression(n.expression())),
        _ => new CaseValueExpression(MakeProvenance(ctx),
            new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true))),
    };

    // Null on a grammar-unreachable operator shape: the caller emits a
    // recovered stub where a defaulted Equal would be a silent guess.
    private static BinaryOperator? ComparisonOperatorFromCtx(VbaParser.ComparisonOperatorContext ctx)
    {
        if (ctx.EQ() is not null) return BinaryOperator.Equal;
        if (ctx.NEQ() is not null) return BinaryOperator.NotEqual;
        if (ctx.LT() is not null) return BinaryOperator.Less;
        if (ctx.GT() is not null) return BinaryOperator.Greater;
        if (ctx.LE() is not null) return BinaryOperator.LessOrEqual;
        if (ctx.GE() is not null) return BinaryOperator.GreaterOrEqual;
        return null;
    }

    private WithStatement BuildWith(VbaParser.WithStatementContext ctx)
    {
        var target = BuildExpression(ctx.expression());
        var body = BuildStatementList(ctx.statementListBody());
        return new WithStatement(MakeProvenance(ctx), target, body);
    }

    private Statement BuildOnError(VbaParser.OnErrorStatementContext ctx) => ctx switch
    {
        VbaParser.OnErrorGotoContext n =>
            new OnErrorStatement(MakeProvenance(n), OnErrorKind.Goto, n.labelOrZero().GetText()),
        VbaParser.OnErrorResumeNextContext n =>
            new OnErrorStatement(MakeProvenance(n), OnErrorKind.ResumeNext, null),
        // Grammar-unreachable alt: a defaulted ResumeNext would silently
        // invert the module's error-handling semantics.
        _ => new UnknownStatement(MakeProvenance(ctx, recoveredOverride: true)),
    };

    private ResumeStatement BuildResume(VbaParser.ResumeStatementContext ctx)
    {
        if (ctx.NEXT() is not null) return new ResumeStatement(MakeProvenance(ctx), ResumeKind.Next, null);
        if (ctx.labelOrZero() is { } lz) return new ResumeStatement(MakeProvenance(ctx), ResumeKind.Label, lz.GetText());
        return new ResumeStatement(MakeProvenance(ctx), ResumeKind.Bare, null);
    }

    private GotoStatement BuildGoto(VbaParser.GotoStatementContext ctx) =>
        new(MakeProvenance(ctx), ctx.labelOrZero().GetText());

    private GosubStatement BuildGosub(VbaParser.GosubStatementContext ctx) =>
        new(MakeProvenance(ctx), ctx.labelOrZero().GetText());

    private Statement BuildExit(VbaParser.ExitStatementContext ctx)
    {
        var k = ctx.exitKind();
        ExitKind? kind = k.SUB() is not null ? ExitKind.Sub
                       : k.FUNCTION() is not null ? ExitKind.Function
                       : k.PROPERTY() is not null ? ExitKind.Property
                       : k.FOR() is not null ? ExitKind.For
                       : k.DO() is not null ? ExitKind.Do
                       : null;
        return kind is { } known
            ? new ExitStatement(MakeProvenance(ctx), known)
            // Grammar-unreachable kind: a defaulted Exit Do would silently
            // retarget the control flow.
            : new UnknownStatement(MakeProvenance(ctx, recoveredOverride: true));
    }

    private RaiseEventStatement BuildRaiseEvent(VbaParser.RaiseEventStatementContext ctx)
    {
        string name = ctx.IDENTIFIER().GetText();
        var args = ctx.argumentList() is { } al ? BuildArguments(al) : Array.Empty<Argument>();
        return new RaiseEventStatement(MakeProvenance(ctx), name, args);
    }

    private LocalDeclarationStatement BuildLocalDecl(VbaParser.LocalDeclStatementContext ctx)
    {
        var kind = ctx.localDeclHead().STATIC() is not null ? LocalDeclarationKind.Static : LocalDeclarationKind.Dim;
        var declarators = ctx.variableDecl().Select(BuildVariableDeclarator).ToList();
        return new LocalDeclarationStatement(MakeProvenance(ctx), kind, declarators);
    }

    private VariableDeclarator BuildVariableDeclarator(VbaParser.VariableDeclContext ctx)
    {
        var (name, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText() ?? ctx.contextualKeywordName()?.GetText());
        IReadOnlyList<ArrayBound> bounds = ctx.arrayBoundsList() is { } b ? BuildArrayBounds(b) : Array.Empty<ArrayBound>();
        bool isArray = ctx.LPAREN() is not null;
        var (type, isNew) = BuildVariableType(ctx.variableTypeSpec());
        return new VariableDeclarator(MakeProvenance(ctx), name, suffix, false, isNew, type, bounds, isArray);
    }

    private RedimStatement BuildRedim(VbaParser.RedimStatementContext ctx)
    {
        bool preserve = ctx.PRESERVE() is not null;
        var declarators = ctx.redimDecl().Select(BuildRedimDeclarator).ToList();
        return new RedimStatement(MakeProvenance(ctx), preserve, declarators);
    }

    private RedimDeclarator BuildRedimDeclarator(VbaParser.RedimDeclContext ctx)
    {
        var (name, suffix) = SplitDeclaredName(
            ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText() ?? ctx.contextualKeywordName()?.GetText());
        var bounds = BuildArrayBounds(ctx.arrayBoundsList());
        var type = ctx.variableTypeSpec() is { } vts ? BuildVariableType(vts).Type : null;
        return new RedimDeclarator(MakeProvenance(ctx), name, suffix, bounds, type);
    }

    private LocalConstantStatement BuildLocalConst(VbaParser.LocalConstStatementContext ctx)
    {
        var declarators = ctx.constDecl().Select(BuildConstantDeclarator).ToList();
        return new LocalConstantStatement(MakeProvenance(ctx), declarators);
    }

    private SetStatement BuildSet(VbaParser.SetStatementContext ctx) =>
        new(MakeProvenance(ctx), BuildLExpression(ctx.lExpression()), BuildExpression(ctx.expression()));

    private LetStatement BuildLet(VbaParser.LetStatementContext ctx) =>
        new(MakeProvenance(ctx),
            BuildLExpression(ctx.lExpression()),
            BuildExpression(ctx.expression()),
            ExplicitLet: ctx.LET() is not null);

    private CallStatement BuildCall(VbaParser.CallStatementContext ctx)
    {
        switch (ctx)
        {
            case VbaParser.ExplicitCallStmtContext ec:
                return new CallStatement(
                    MakeProvenance(ec),
                    BuildLExpression(ec.lExpression()),
                    BareArguments: Array.Empty<Argument>(),
                    ExplicitCall: true);
            case VbaParser.ImplicitCallStmtContext ic:
                var bare = ic.bareCallArgumentList() is { } bal
                    ? BuildBareArguments(bal)
                    : Array.Empty<Argument>();
                return new CallStatement(
                    MakeProvenance(ic),
                    BuildLExpression(ic.lExpression()),
                    bare,
                    ExplicitCall: false);
            default:
                return new CallStatement(
                    MakeProvenance(ctx),
                    new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true)),
                    Array.Empty<Argument>(),
                    false);
        }
    }

    // ── Expressions ───────────────────────────────────────────────

    private Expression BuildExpression(VbaParser.ExpressionContext ctx)
    {
        if (HasErrorNode(ctx))
            return new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true));

        return ctx switch
        {
            VbaParser.PowerExprContext n     => MakeBinary(n, BinaryOperator.Power, n.expression(0), n.expression(1)),
            VbaParser.UnarySignExprContext n => new UnaryExpression(MakeProvenance(n),
                                                  n.PLUS() is not null ? UnaryOperator.Plus : UnaryOperator.Minus,
                                                  BuildExpression(n.expression())),
            VbaParser.MulDivExprContext n    => MakeBinary(n,
                                                  n.STAR() is not null ? BinaryOperator.Multiply : BinaryOperator.Divide,
                                                  n.expression(0), n.expression(1)),
            VbaParser.IntDivExprContext n    => MakeBinary(n, BinaryOperator.IntDivide, n.expression(0), n.expression(1)),
            VbaParser.ModExprContext n       => MakeBinary(n, BinaryOperator.Modulo, n.expression(0), n.expression(1)),
            VbaParser.AddSubExprContext n    => MakeBinary(n,
                                                  n.PLUS() is not null ? BinaryOperator.Add : BinaryOperator.Subtract,
                                                  n.expression(0), n.expression(1)),
            VbaParser.ConcatExprContext n    => MakeBinary(n, BinaryOperator.Concat, n.expression(0), n.expression(1)),
            VbaParser.CompareExprContext n   => ComparisonFromCtx(n) is { } cmp
                                                  ? MakeBinary(n, cmp, n.expression(0), n.expression(1))
                                                  : new UnknownExpression(MakeProvenance(n, recoveredOverride: true)),
            VbaParser.NotExprContext n       => new UnaryExpression(MakeProvenance(n),
                                                  UnaryOperator.Not, BuildExpression(n.expression())),
            VbaParser.AndExprContext n       => MakeBinary(n, BinaryOperator.And, n.expression(0), n.expression(1)),
            VbaParser.OrExprContext n        => MakeBinary(n, BinaryOperator.Or, n.expression(0), n.expression(1)),
            VbaParser.XorExprContext n       => MakeBinary(n, BinaryOperator.Xor, n.expression(0), n.expression(1)),
            VbaParser.EqvExprContext n       => MakeBinary(n, BinaryOperator.Eqv, n.expression(0), n.expression(1)),
            VbaParser.ImpExprContext n       => MakeBinary(n, BinaryOperator.Imp, n.expression(0), n.expression(1)),
            VbaParser.TypeOfPrimaryContext n => BuildTypeOf(n.typeOfExpr()),
            VbaParser.NewPrimaryContext n    => BuildNew(n.newExpr()),
            VbaParser.AddressOfPrimaryContext n => BuildAddressOf(n.addressOfExpr()),
            VbaParser.LExprPrimaryContext n  => BuildLExpression(n.lExpression()),
            _ => new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true)),
        };
    }

    private BinaryExpression MakeBinary(ParserRuleContext n, BinaryOperator op,
        VbaParser.ExpressionContext left, VbaParser.ExpressionContext right) =>
        new(MakeProvenance(n), op, BuildExpression(left), BuildExpression(right));

    // Null when the labeled `op` token is missing or carries a
    // grammar-unreachable type: the caller emits a recovered stub where
    // a defaulted Equal would silently rewrite the comparison.
    private static BinaryOperator? ComparisonFromCtx(VbaParser.CompareExprContext ctx)
    {
        // CompareExpr captures op=(EQ|NEQ|LT|GT|LE|GE|LIKE|IS) into the labeled `op` field.
        var op = ctx.op;
        if (op is null) return null;
        return op.Type switch
        {
            VbaLexer.EQ   => BinaryOperator.Equal,
            VbaLexer.NEQ  => BinaryOperator.NotEqual,
            VbaLexer.LT   => BinaryOperator.Less,
            VbaLexer.GT   => BinaryOperator.Greater,
            VbaLexer.LE   => BinaryOperator.LessOrEqual,
            VbaLexer.GE   => BinaryOperator.GreaterOrEqual,
            VbaLexer.LIKE => BinaryOperator.Like,
            VbaLexer.IS   => BinaryOperator.Is,
            _             => null,
        };
    }

    private TypeOfExpression BuildTypeOf(VbaParser.TypeOfExprContext ctx) =>
        new(MakeProvenance(ctx),
            BuildReferenceExpression(ctx.referenceExpression()),
            BuildTypeReference(ctx.typeName(), isAutoInstantiated: false));

    private NewExpression BuildNew(VbaParser.NewExprContext ctx) =>
        new(MakeProvenance(ctx), BuildTypeReference(ctx.typeName(), isAutoInstantiated: true));

    private AddressOfExpression BuildAddressOf(VbaParser.AddressOfExprContext ctx) =>
        new(MakeProvenance(ctx), BuildProcedureReference(ctx.procedureReference()));

    private Expression BuildProcedureReference(VbaParser.ProcedureReferenceContext ctx)
    {
        // procedureReference : (IDENT|TYPED_IDENT) (DOT memberName)* | ME (DOT memberName)+
        Expression head;
        if (ctx.ME() is not null)
        {
            head = new MeExpression(MakeProvenance(ctx.ME().Symbol));
        }
        else
        {
            string text = ctx.IDENTIFIER()?.GetText() ?? ctx.TYPED_IDENTIFIER()?.GetText() ?? "";
            string name = StripTypeSuffix(text);
            string? suffix = text != name ? text[^1].ToString() : null;
            head = new IdentifierExpression(MakeProvenance(ctx), name, suffix);
        }
        foreach (var member in ctx.memberName())
            head = new MemberAccessExpression(MakeProvenance(ctx), head, member.GetText());
        return head;
    }

    private Expression BuildReferenceExpression(VbaParser.ReferenceExpressionContext ctx)
    {
        // referenceExpression is a simplified l-expression for TypeOf;
        // it has the same shapes as lExpression except no literal alts.
        // Walk children left-to-right rebuilding postfix chains.
        if (HasErrorNode(ctx))
            return new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true));

        // Use GetText to delegate to lExpression-like shape: simpler to
        // handle via direct child inspection. Falls through to identifier
        // for the bare ID/Me cases.
        if (ctx.ME() is not null && ctx.ChildCount == 1) return new MeExpression(MakeProvenance(ctx));
        if (ctx.IDENTIFIER() is { } id && ctx.ChildCount == 1)
            return new IdentifierExpression(MakeProvenance(ctx), id.GetText(), null);
        if (ctx.TYPED_IDENTIFIER() is { } tid && ctx.ChildCount == 1)
        {
            string text = tid.GetText();
            string name = StripTypeSuffix(text);
            return new IdentifierExpression(MakeProvenance(ctx), name, text != name ? text[^1].ToString() : null);
        }

        // Recurse into nested referenceExpression (for chains).
        if (ctx.referenceExpression() is { } inner)
        {
            var target = BuildReferenceExpression(inner);
            if (ctx.DOT() is not null && ctx.memberName() is { } mn)
                return new MemberAccessExpression(MakeProvenance(ctx), target, mn.GetText());
            if (ctx.BANG() is not null && ctx.memberName() is { } bn)
                return new BangAccessExpression(MakeProvenance(ctx), target, bn.GetText());
            if (ctx.LPAREN() is not null && ctx.argumentList() is { } al)
                return new IndexOrCallExpression(MakeProvenance(ctx), target, BuildArguments(al));
            return target;
        }

        // Bare DOT memberName / BANG memberName.
        if (ctx.DOT() is not null && ctx.memberName() is { } dotMember)
            return new ImplicitWithMemberExpression(MakeProvenance(ctx), dotMember.GetText());
        if (ctx.BANG() is not null && ctx.memberName() is { } bangMember)
            return new ImplicitWithBangExpression(MakeProvenance(ctx), bangMember.GetText());

        return new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true));
    }

    private Expression BuildLExpression(VbaParser.LExpressionContext ctx)
    {
        if (HasErrorNode(ctx))
            return new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true));

        return ctx switch
        {
            VbaParser.MemberAccessContext n =>
                new MemberAccessExpression(MakeProvenance(n), BuildLExpression(n.lExpression()), n.memberName().GetText()),
            VbaParser.BangAccessContext n =>
                new BangAccessExpression(MakeProvenance(n), BuildLExpression(n.lExpression()), n.memberName().GetText()),
            VbaParser.IndexOrCallContext n =>
                new IndexOrCallExpression(MakeProvenance(n), BuildLExpression(n.lExpression()), BuildArguments(n.argumentList())),
            VbaParser.ImplicitWithMemberContext n =>
                new ImplicitWithMemberExpression(MakeProvenance(n), n.memberName().GetText()),
            VbaParser.ImplicitWithBangContext n =>
                new ImplicitWithBangExpression(MakeProvenance(n), n.memberName().GetText()),
            VbaParser.PrimaryExprContext n =>
                BuildPrimary(n.primary()),
            _ => new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true)),
        };
    }

    private Expression BuildPrimary(VbaParser.PrimaryContext ctx) => ctx switch
    {
        VbaParser.LiteralPrimaryContext n  => BuildLiteral(n.literal()),
        VbaParser.MeExprContext n          => new MeExpression(MakeProvenance(n)),
        VbaParser.TrueExprContext n        => new TrueExpression(MakeProvenance(n)),
        VbaParser.FalseExprContext n       => new FalseExpression(MakeProvenance(n)),
        VbaParser.NothingExprContext n     => new NothingExpression(MakeProvenance(n)),
        VbaParser.NullExprContext n        => new NullExpression(MakeProvenance(n)),
        VbaParser.EmptyExprContext n       => new EmptyExpression(MakeProvenance(n)),
        VbaParser.DebugObjectExprContext n => new DebugExpression(MakeProvenance(n)),
        VbaParser.IdentExprContext n       => new IdentifierExpression(MakeProvenance(n), n.IDENTIFIER().GetText(), null),
        VbaParser.TypedIdentExprContext n  => BuildTypedIdent(n),
        VbaParser.ContextualKeywordExprContext n =>
            new IdentifierExpression(MakeProvenance(n), n.contextualKeywordName().GetText(), null),
        VbaParser.ParenExprContext n       => new ParenthesizedExpression(MakeProvenance(n), BuildExpression(n.expression())),
        _                                  => new UnknownExpression(MakeProvenance(ctx, recoveredOverride: true)),
    };

    private static IdentifierExpression BuildTypedIdent(VbaParser.TypedIdentExprContext ctx)
    {
        string text = ctx.TYPED_IDENTIFIER().GetText();
        string name = text[..^1];                       // strip suffix
        string suffix = text[^1].ToString();
        return new IdentifierExpression(new SourceProvenance(ctx.Start.Line, ctx.Start.Column), name, suffix);
    }

    private LiteralExpression BuildLiteral(VbaParser.LiteralContext ctx)
    {
        string text = ctx.GetText();
        LiteralKind kind;
        if (ctx.INTEGER_LITERAL() is not null) kind = LiteralKind.Integer;
        else if (ctx.FLOAT_LITERAL() is not null) kind = LiteralKind.Float;
        else if (ctx.STRING_LITERAL() is not null) kind = LiteralKind.String;
        else if (ctx.DATE_LITERAL() is not null) kind = LiteralKind.Date;
        else if (ctx.HEX_LITERAL() is not null) kind = LiteralKind.Hex;
        else if (ctx.OCTAL_LITERAL() is not null) kind = LiteralKind.Octal;
        else kind = LiteralKind.Integer;
        return new LiteralExpression(MakeProvenance(ctx), kind, text);
    }

    // ── Arguments ─────────────────────────────────────────────────

    private IReadOnlyList<Argument> BuildArguments(VbaParser.ArgumentListContext ctx)
    {
        // argumentList : argument? (COMMA argument?)*
        // The grammar threads optional argument slots between commas;
        // missing slots are H-D08 positional gaps.
        var result = new List<Argument>();
        // Walk children in order to interleave commas correctly.
        bool firstSlotConsumed = false;
        bool lastWasComma = false;
        for (int i = 0; i < ctx.ChildCount; i++)
        {
            var child = ctx.GetChild(i);
            if (child is VbaParser.ArgumentContext a)
            {
                result.Add(BuildArgument(a));
                firstSlotConsumed = true;
                lastWasComma = false;
            }
            else if (child is ITerminalNode t && t.Symbol.Type == VbaLexer.COMMA)
            {
                if (lastWasComma || !firstSlotConsumed)
                {
                    result.Add(new MissingArgument(MakeProvenance(t.Symbol)));
                }
                lastWasComma = true;
                firstSlotConsumed = true;
            }
        }
        if (lastWasComma)
            result.Add(new MissingArgument(MakeProvenance(ctx)));
        return result;
    }

    private IReadOnlyList<Argument> BuildBareArguments(VbaParser.BareCallArgumentListContext ctx)
    {
        // bareCallArgumentList : argument (COMMA argument?)*
        var result = new List<Argument>();
        bool lastWasComma = false;
        for (int i = 0; i < ctx.ChildCount; i++)
        {
            var child = ctx.GetChild(i);
            if (child is VbaParser.ArgumentContext a)
            {
                result.Add(BuildArgument(a));
                lastWasComma = false;
            }
            else if (child is ITerminalNode t && t.Symbol.Type == VbaLexer.COMMA)
            {
                if (lastWasComma) result.Add(new MissingArgument(MakeProvenance(t.Symbol)));
                lastWasComma = true;
            }
        }
        if (lastWasComma) result.Add(new MissingArgument(MakeProvenance(ctx)));
        return result;
    }

    private Argument BuildArgument(VbaParser.ArgumentContext ctx)
    {
        if (ctx.namedArgument() is { } na)
        {
            // The name slot is IDENTIFIER or a contextual keyword used as an
            // argument name (e.g. `F Step:=1` binding a parameter named `step`).
            string argName = na.IDENTIFIER()?.GetText() ?? na.contextualKeywordName()?.GetText() ?? "";
            return new NamedArgument(MakeProvenance(na), argName, BuildExpression(na.expression()));
        }
        if (ctx.expression() is { } e)
            return new PositionalArgument(MakeProvenance(ctx), BuildExpression(e));
        return new MissingArgument(MakeProvenance(ctx));
    }

    // ── Type references ───────────────────────────────────────────

    private TypeReference BuildTypeReference(VbaParser.TypeSpecContext ctx)
    {
        var qn = ctx.typeName().IDENTIFIER().Select(i => i.GetText()).ToList();
        var fixedSize = ctx.expression() is { } e ? BuildExpression(e) : null;
        return new TypeReference(MakeProvenance(ctx), qn, IsAutoInstantiated: false, fixedSize, IsArray: false);
    }

    private TypeReference BuildTypeReference(VbaParser.TypeNameContext ctx, bool isAutoInstantiated)
    {
        var qn = ctx.IDENTIFIER().Select(i => i.GetText()).ToList();
        return new TypeReference(MakeProvenance(ctx), qn, isAutoInstantiated, FixedStringSize: null, IsArray: false);
    }

    private (TypeReference? Type, bool IsNew) BuildVariableType(VbaParser.VariableTypeSpecContext? ctx)
    {
        if (ctx is null) return (null, false);
        if (ctx.NEW() is not null && ctx.typeName() is { } tn)
            return (BuildTypeReference(tn, isAutoInstantiated: true), true);
        if (ctx.typeSpec() is { } ts)
            return (BuildTypeReference(ts), false);
        return (null, false);
    }

    private List<ArrayBound> BuildArrayBounds(VbaParser.ArrayBoundsListContext ctx) =>
        ctx.arrayBound().Select(BuildArrayBound).ToList();

    private ArrayBound BuildArrayBound(VbaParser.ArrayBoundContext ctx)
    {
        var exprs = ctx.expression();
        if (exprs.Length == 1)
            return new ArrayBound(MakeProvenance(ctx), null, BuildExpression(exprs[0]));
        return new ArrayBound(MakeProvenance(ctx), BuildExpression(exprs[0]), BuildExpression(exprs[1]));
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static AccessModifier AccessModifierFromHead(IToken? pub, IToken? priv)
    {
        if (pub is not null) return AccessModifier.Public;
        if (priv is not null) return AccessModifier.Private;
        return AccessModifier.Unspecified;
    }

    private static string StripStringLiteral(string s) =>
        s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s.Substring(1, s.Length - 2) : s;

    // Splits a declared name into its suffix-stripped form plus the
    // type-declaration character, so every declaration carries the same
    // Name its references resolve against (006 review F-3-3). Null input
    // means recovery dropped the name token; "" keeps downstream
    // null-safety unchanged.
    private static (string Name, string? Suffix) SplitDeclaredName(string? rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return ("", null);
        string name = StripTypeSuffix(rawText);
        return (name, ExtractTypeSuffix(rawText, name));
    }

    private static string? ExtractTypeSuffix(string? typedIdentifierText, string strippedName)
    {
        if (string.IsNullOrEmpty(typedIdentifierText)) return null;
        if (typedIdentifierText.Length <= strippedName.Length) return null;
        char last = typedIdentifierText[^1];
        return "$%&!#@".Contains(last) ? last.ToString() : null;
    }

    private static string StripTypeSuffix(string text)
    {
        if (text.Length == 0) return text;
        char last = text[^1];
        return "$%&!#@".Contains(last) ? text[..^1] : text;
    }

    // Maps a type-declaration suffix to its implied built-in type per
    // MS-VBAL §3.3.3 ($=String, %=Integer, &=Long, !=Single, #=Double,
    // @=Currency); null when there is no suffix. Used to populate a
    // function / property-get ReturnType from `Function F$()` /
    // `Property Get P$()` so the suffix's type is not silently lost
    // downstream (006 review H4) — the raw TypeSuffix is retained
    // alongside for spelling fidelity. An explicit `As` clause always
    // wins over the suffix at the call sites.
    private TypeReference? TypeReferenceFromSuffix(string? suffix, ParserRuleContext ctx)
    {
        string? builtIn = suffix switch
        {
            "$" => "String",
            "%" => "Integer",
            "&" => "Long",
            "!" => "Single",
            "#" => "Double",
            "@" => "Currency",
            _ => null,
        };
        return builtIn is null
            ? null
            : new TypeReference(MakeProvenance(ctx), new[] { builtIn }, IsAutoInstantiated: false, FixedStringSize: null, IsArray: false);
    }

    private Provenance MakeProvenance(ParserRuleContext ctx, bool recoveredOverride = false)
    {
        var line = ctx.Start?.Line ?? 0;
        var col = ctx.Start?.Column ?? 0;
        var range = MakeRange(ctx);
        if (recoveredOverride || HasErrorNode(ctx))
            return new RecoveredProvenance(NearestDiagnosticReason(line, col), line, col, range);
        return new SourceProvenance(line, col, range);
    }

    private Provenance MakeProvenance(IToken token)
    {
        var loc = new SourceLocation(_sourceFile, token.Line, token.Column);
        var end = new SourceLocation(_sourceFile, token.Line, token.Column + (token.Text?.Length ?? 0));
        return new SourceProvenance(token.Line, token.Column, new SourceRange(loc, end));
    }

    private SourceRange? MakeRange(ParserRuleContext ctx)
    {
        if (ctx.Start is null) return null;
        var start = new SourceLocation(_sourceFile, ctx.Start.Line, ctx.Start.Column);
        var stop = ctx.Stop is not null
            ? new SourceLocation(_sourceFile, ctx.Stop.Line, ctx.Stop.Column + (ctx.Stop.Text?.Length ?? 0))
            : new SourceLocation(_sourceFile, ctx.Start.Line, ctx.Start.Column + (ctx.Start.Text?.Length ?? 0));
        return new SourceRange(start, stop);
    }

    private string NearestDiagnosticReason(int line, int column)
    {
        // Per ast-shape.md §3: nearest-preceding diagnostic by line/column.
        ParseDiagnostic? best = null;
        foreach (var d in _diagnostics)
        {
            if (d.Line > line || (d.Line == line && d.Column > column)) continue;
            if (best is null
                || d.Line > best.Line
                || (d.Line == best.Line && d.Column > best.Column))
            {
                best = d;
            }
        }
        return best?.Message ?? "unknown-recovery";
    }

    private static bool HasErrorNode(IParseTree node)
    {
        if (node is IErrorNode) return true;
        for (int i = 0; i < node.ChildCount; i++)
        {
            if (HasErrorNode(node.GetChild(i))) return true;
        }
        return false;
    }
}
