using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.SymbolDb.Models;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;

using ResolverProcedureDeclaration = AccessHarvester.Vba.Resolver.Declarations.ProcedureDeclaration;
using ResolverEnumDeclaration = AccessHarvester.Vba.Resolver.Declarations.EnumDeclaration;
using ResolverVariableDeclaration = AccessHarvester.Vba.Resolver.Declarations.VariableDeclaration;
using AstProcedureDeclaration = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;
using AstEnumDeclaration = AccessHarvester.Vba.Parser.Ast.EnumDeclaration;
using AstModuleVariableDeclaration = AccessHarvester.Vba.Parser.Ast.ModuleVariableDeclaration;
using AstModuleConstantDeclaration = AccessHarvester.Vba.Parser.Ast.ModuleConstantDeclaration;
using AstDeclareStatement = AccessHarvester.Vba.Parser.Ast.DeclareStatement;
using AstEventDeclaration = AccessHarvester.Vba.Parser.Ast.EventDeclaration;

namespace AccessHarvester.SymbolDb.Extraction;

// Walks a list of RawModules and produces the Module / Symbol / Parameter
// rows that Session 1 of the vba-symbol-db set persists. Composes:
//   * DeferralPreFilter — flags Old/_Old companions and skips parsing them.
//   * VbaParserPipeline — parses each non-deferred module to AST.
//   * ScopeBuildingVisitor + ReferenceResolver — produces resolver state so
//     CrossReferenceIndex can answer "who calls this declaration?" lookups.
//   * IntraProcedureDeadCodeDetector — flags dead-after-unconditional-exit.
//   * EntryPointHeuristic — keeps event handlers / public APIs / AutoExec
//     alive even when the cross-reference index reports zero callers.
//
// MD5 dedup runs after persistence (Md5Deduper); the equivalence_canonical_fqn
// column is left null on every row produced here.
public sealed class SymbolDbBuilder
{
    private readonly DeferralPreFilter _deferralFilter;

    public SymbolDbBuilder(DeferralPreFilter? deferralFilter = null)
    {
        _deferralFilter = deferralFilter ?? new DeferralPreFilter();
    }

    public BuildResult Build(IReadOnlyList<RawModule> rawModules)
    {
        if (rawModules is null) throw new ArgumentNullException(nameof(rawModules));

        var nameSet = new HashSet<string>(
            rawModules.Select(m => m.Fqn), StringComparer.OrdinalIgnoreCase);

        var moduleRows = new List<ModuleRow>(rawModules.Count);
        var deferredFqns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pass 1: deferral classification. Deferred modules are written to
        // Modules with is_deferred=1 and otherwise skipped.
        foreach (var raw in rawModules)
        {
            var outcome = _deferralFilter.Classify(raw.Fqn, nameSet);
            if (outcome.IsDeferred)
            {
                deferredFqns.Add(raw.Fqn);
                moduleRows.Add(new ModuleRow(
                    raw.Fqn, raw.Kind, raw.SourcePath, raw.Encoding,
                    IsDeferred: true,
                    DeferredReason: outcome.Reason,
                    DeferredCompanionFqn: outcome.CompanionName));
                continue;
            }

            moduleRows.Add(new ModuleRow(
                raw.Fqn, raw.Kind, raw.SourcePath, raw.Encoding,
                IsDeferred: false,
                DeferredReason: null,
                DeferredCompanionFqn: null));
        }

        // Pass 2: parse + scope-build + resolve every non-deferred module.
        var parsed = new List<ParsedModule>();
        var parseFailedFqns = new List<string>();
        var project = new ProjectScope();
        foreach (var raw in rawModules)
        {
            if (deferredFqns.Contains(raw.Fqn)) continue;

            try
            {
                var (ast, parseResult) = VbaModuleBodyLoader.LoadAndParse(
                    raw.Source,
                    new ParseOptions(ParseMode.Recovering, LossyFallbackErrorThreshold: 100, SourcePath: raw.SourcePath),
                    raw.SourcePath);
                var visitor = new ScopeBuildingVisitor();
                var moduleScope = visitor.BuildModuleScope(project, ast, raw.Fqn, ToModuleKind(raw.Kind));

                parsed.Add(new ParsedModule(raw, ast, moduleScope));
            }
            catch (Exception)
            {
                // Mirror Pipeline.cs's per-file catch: a module that fails to
                // parse / scope-build degrades that module (it keeps its Pass-1
                // ModuleRow but contributes no symbols or references) instead of
                // failing the entire Build. The FQN is recorded on
                // BuildResult.ParseFailedModuleFqns so the loss is explicit, not
                // silent. Broad catch is intentional — this is the coarse
                // build-loop resilience boundary and the failure is recorded
                // (006 review F-3-2: Pass 2 previously had no catch, so one bad
                // module failed the whole build).
                parseFailedFqns.Add(raw.Fqn);
            }
        }

        // Pass 3: resolve references for each parsed module so the cross-
        // reference index has populated callers for every declaration.
        var resolver = new ReferenceResolver();
        var resolved = parsed.Select(p => (Parsed: p, Resolved: resolver.Resolve(p.ModuleScope, p.Ast)))
            .ToList();
        var crossRef = CrossReferenceIndex.Build(resolved.Select(r => r.Resolved).ToArray());

        // Pass 4: extract symbols and parameters per module.
        var symbolRows = new List<SymbolRow>();
        var parameterRows = new List<ParameterRow>();
        foreach (var (parsed_, _) in resolved)
        {
            ExtractModuleSymbols(parsed_, crossRef, symbolRows, parameterRows);
        }

        // Pass 5: extract reference edges and member-access expressions.
        var resolvedModules = resolved.Select(r => r.Resolved).ToList();
        var (referenceRows, memberAccessRows) = ReferencesExtractor.Extract(resolvedModules);

        var statistics = ComputeStatistics(moduleRows, symbolRows, deferredFqns);
        return new BuildResult(moduleRows, symbolRows, parameterRows, referenceRows, memberAccessRows, statistics,
            ParseFailedModuleFqns: parseFailedFqns);
    }

    private static void ExtractModuleSymbols(
        ParsedModule parsed,
        CrossReferenceIndex crossRef,
        List<SymbolRow> symbolRows,
        List<ParameterRow> parameterRows)
    {
        string moduleFqn = parsed.Raw.Fqn;
        var resolverDecls = parsed.ModuleScope.Declarations;

        foreach (var item in parsed.Ast.Items)
        {
            switch (item)
            {
                case AstProcedureDeclaration proc:
                    ExtractProcedure(parsed, proc, resolverDecls, crossRef, symbolRows, parameterRows);
                    break;
                case AstModuleVariableDeclaration moduleVar:
                    ExtractModuleVariable(moduleFqn, moduleVar, resolverDecls, symbolRows);
                    break;
                case AstModuleConstantDeclaration moduleConst:
                    ExtractModuleConstant(moduleFqn, moduleConst, symbolRows);
                    break;
                case AstEnumDeclaration enumDecl:
                    ExtractEnum(moduleFqn, enumDecl, symbolRows);
                    break;
                case AstDeclareStatement declare:
                    ExtractDeclare(parsed, declare, symbolRows, parameterRows);
                    break;
                case AstEventDeclaration evt:
                    ExtractEvent(parsed, evt, symbolRows, parameterRows);
                    break;
                default:
                    // OptionDirective / ImplementsDirective / TypeDeclaration /
                    // PreprocessorDirective — not in the Session 1 symbol set.
                    break;
            }
        }
    }

    private static void ExtractProcedure(
        ParsedModule parsed,
        AstProcedureDeclaration proc,
        IReadOnlyList<Declaration> resolverDecls,
        CrossReferenceIndex crossRef,
        List<SymbolRow> symbolRows,
        List<ParameterRow> parameterRows)
    {
        string kind = proc switch
        {
            SubProcedure => "sub",
            FunctionProcedure => "function",
            PropertyProcedure { Kind: PropertyKind.Get } => "property_get",
            PropertyProcedure { Kind: PropertyKind.Let } => "property_let",
            PropertyProcedure { Kind: PropertyKind.Set } => "property_set",
            _ => "sub",
        };
        // Property Get / Let / Set procedures share a name in source. To keep
        // Symbols.fqn unique we encode the accessor kind into the FQN with a
        // `#get`/`#let`/`#set` suffix. Sub / Function names already have to be
        // unique in a VBA module so they keep the plain `<module>.<name>` shape.
        string fqn = proc is PropertyProcedure prop
            ? $"{parsed.Raw.Fqn}.{proc.Name}#{prop.Kind.ToString().ToLowerInvariant()}"
            : $"{parsed.Raw.Fqn}.{proc.Name}";

        var (startLine, endLine, body) = SourceBodyExtractor.Extract(parsed.Raw.Source, proc.Provenance.Range);
        bool intraDead = IntraProcedureDeadCodeDetector.HasDeadCode(proc, out string? deadReason);

        // Dead-procedure detection: zero callers AND not an entry point.
        var resolverDecl = FindResolverProcedure(resolverDecls, proc);
        bool isDeadProcedure = false;
        string? deadProcedureReason = null;
        if (resolverDecl is not null)
        {
            int callers = crossRef.GetReferencesTo(resolverDecl).Count;
            bool isEntryPoint = EntryPointHeuristic.IsEntryPoint(resolverDecl, parsed.ModuleScope);
            if (callers == 0 && !isEntryPoint)
            {
                isDeadProcedure = true;
                deadProcedureReason = "no-callers; not-event-handler; not-entry-point";
            }
        }

        // Combine intra-procedure dead-code with dead-procedure flag in one
        // column. The Symbols.is_dead bit signals "skip in interpretation"; the
        // reason column carries enough detail for the migration consumer.
        bool isDead = isDeadProcedure || intraDead;
        string? reason = isDeadProcedure ? deadProcedureReason : (intraDead ? deadReason : null);
        if (isDeadProcedure && intraDead) reason = $"{deadProcedureReason}; intra:{deadReason}";

        string signature = SignatureFormatter.FormatProcedure(proc);
        string? bodyHash = body is null ? null : BodyHasher.Compute(body);
        string? returnType = TypeReferenceFormatter.Format(proc.ReturnType);

        symbolRows.Add(new SymbolRow(
            Fqn: fqn,
            ModuleFqn: parsed.Raw.Fqn,
            Name: proc.Name,
            Kind: kind,
            Access: AccessLevelMapper.ToColumnValue(proc.Access),
            DataType: returnType,
            DeclaredInFqn: null,                 // module-level
            SourceLineStart: startLine,
            SourceLineEnd: endLine,
            SourceBody: body,
            SignatureString: signature,
            BodyHashMd5: bodyHash,
            IsDead: isDead,
            DeadCodeReason: reason,
            EquivalenceCanonicalFqn: null));

        for (int i = 0; i < proc.Parameters.Count; i++)
        {
            var p = proc.Parameters[i];
            parameterRows.Add(new ParameterRow(
                ProcedureFqn: fqn,
                Position: i,
                Name: p.Name,
                DataType: TypeReferenceFormatter.Format(p.Type),
                Passing: p.PassMode switch
                {
                    ParameterPassMode.ByVal => "ByVal",
                    ParameterPassMode.ByRef => "ByRef",
                    _ => "ByRef",
                },
                IsOptional: p.Modifier == ParameterModifier.Optional,
                IsParamArray: p.Modifier == ParameterModifier.ParamArray,
                IsArray: p.IsArray,
                DefaultValue: null));
        }

        LocalVariableExtractor.ExtractInto(proc, parsed.Raw.Fqn, fqn, symbolRows);
    }

    private static void ExtractModuleVariable(
        string moduleFqn,
        AstModuleVariableDeclaration moduleVar,
        IReadOnlyList<Declaration> resolverDecls,
        List<SymbolRow> symbolRows)
    {
        foreach (var declarator in moduleVar.Declarators)
        {
            string fqn = $"{moduleFqn}.{declarator.Name}";
            symbolRows.Add(new SymbolRow(
                Fqn: fqn,
                ModuleFqn: moduleFqn,
                Name: declarator.Name,
                Kind: "module_var",
                Access: AccessLevelMapper.ToColumnValue(moduleVar.Access),
                DataType: TypeReferenceFormatter.Format(declarator.Type),
                DeclaredInFqn: null,
                SourceLineStart: declarator.Provenance.Range?.Start.Line,
                SourceLineEnd: declarator.Provenance.Range?.End.Line,
                SourceBody: null,
                SignatureString: null,
                BodyHashMd5: null,
                IsDead: false,
                DeadCodeReason: null,
                EquivalenceCanonicalFqn: null));
        }
    }

    private static void ExtractModuleConstant(
        string moduleFqn,
        AstModuleConstantDeclaration moduleConst,
        List<SymbolRow> symbolRows)
    {
        foreach (var declarator in moduleConst.Declarators)
        {
            string fqn = $"{moduleFqn}.{declarator.Name}";
            symbolRows.Add(new SymbolRow(
                Fqn: fqn,
                ModuleFqn: moduleFqn,
                Name: declarator.Name,
                Kind: "module_const",
                Access: AccessLevelMapper.ToColumnValue(moduleConst.Access),
                DataType: TypeReferenceFormatter.Format(declarator.Type),
                DeclaredInFqn: null,
                SourceLineStart: declarator.Provenance.Range?.Start.Line,
                SourceLineEnd: declarator.Provenance.Range?.End.Line,
                SourceBody: null,
                SignatureString: null,
                BodyHashMd5: null,
                IsDead: false,
                DeadCodeReason: null,
                EquivalenceCanonicalFqn: null));
        }
    }

    private static void ExtractEnum(
        string moduleFqn,
        AstEnumDeclaration enumDecl,
        List<SymbolRow> symbolRows)
    {
        string enumFqn = $"{moduleFqn}.{enumDecl.Name}";
        symbolRows.Add(new SymbolRow(
            Fqn: enumFqn,
            ModuleFqn: moduleFqn,
            Name: enumDecl.Name,
            Kind: "enum_decl",
            Access: AccessLevelMapper.ToColumnValue(enumDecl.Access),
            DataType: null,
            DeclaredInFqn: null,
            SourceLineStart: enumDecl.Provenance.Range?.Start.Line,
            SourceLineEnd: enumDecl.Provenance.Range?.End.Line,
            SourceBody: null,
            SignatureString: null,
            BodyHashMd5: null,
            IsDead: false,
            DeadCodeReason: null,
            EquivalenceCanonicalFqn: null));

        foreach (var member in enumDecl.Members)
        {
            string memberFqn = $"{enumFqn}.{member.Name}";
            symbolRows.Add(new SymbolRow(
                Fqn: memberFqn,
                ModuleFqn: moduleFqn,
                Name: member.Name,
                Kind: "enum_value",
                Access: null,
                DataType: null,
                DeclaredInFqn: enumFqn,
                SourceLineStart: member.Provenance.Range?.Start.Line,
                SourceLineEnd: member.Provenance.Range?.End.Line,
                SourceBody: null,
                SignatureString: null,
                BodyHashMd5: null,
                IsDead: false,
                DeadCodeReason: null,
                EquivalenceCanonicalFqn: null));
        }
    }

    private static void ExtractDeclare(
        ParsedModule parsed,
        AstDeclareStatement declare,
        List<SymbolRow> symbolRows,
        List<ParameterRow> parameterRows)
    {
        string fqn = $"{parsed.Raw.Fqn}.{declare.Name}";
        string signature = SignatureFormatter.FormatDeclare(declare);

        symbolRows.Add(new SymbolRow(
            Fqn: fqn,
            ModuleFqn: parsed.Raw.Fqn,
            Name: declare.Name,
            Kind: "declare_stmt",
            Access: AccessLevelMapper.ToColumnValue(declare.Access),
            DataType: TypeReferenceFormatter.Format(declare.ReturnType),
            DeclaredInFqn: null,
            SourceLineStart: declare.Provenance.Range?.Start.Line,
            SourceLineEnd: declare.Provenance.Range?.End.Line,
            SourceBody: null,
            SignatureString: signature,
            BodyHashMd5: null,
            IsDead: false,
            DeadCodeReason: null,
            EquivalenceCanonicalFqn: null));

        for (int i = 0; i < declare.Parameters.Count; i++)
        {
            var p = declare.Parameters[i];
            parameterRows.Add(new ParameterRow(
                ProcedureFqn: fqn,
                Position: i,
                Name: p.Name,
                DataType: TypeReferenceFormatter.Format(p.Type),
                Passing: p.PassMode switch
                {
                    ParameterPassMode.ByVal => "ByVal",
                    ParameterPassMode.ByRef => "ByRef",
                    _ => "ByRef",
                },
                IsOptional: p.Modifier == ParameterModifier.Optional,
                IsParamArray: p.Modifier == ParameterModifier.ParamArray,
                IsArray: p.IsArray,
                DefaultValue: null));
        }
    }

    private static void ExtractEvent(
        ParsedModule parsed,
        AstEventDeclaration evt,
        List<SymbolRow> symbolRows,
        List<ParameterRow> parameterRows)
    {
        string fqn = $"{parsed.Raw.Fqn}.{evt.Name}";

        symbolRows.Add(new SymbolRow(
            Fqn: fqn,
            ModuleFqn: parsed.Raw.Fqn,
            Name: evt.Name,
            Kind: "event_decl",
            Access: AccessLevelMapper.ToColumnValue(evt.Access),
            DataType: null,
            DeclaredInFqn: null,
            SourceLineStart: evt.Provenance.Range?.Start.Line,
            SourceLineEnd: evt.Provenance.Range?.End.Line,
            SourceBody: null,
            SignatureString: null,
            BodyHashMd5: null,
            IsDead: false,
            DeadCodeReason: null,
            EquivalenceCanonicalFqn: null));

        for (int i = 0; i < evt.Parameters.Count; i++)
        {
            var p = evt.Parameters[i];
            parameterRows.Add(new ParameterRow(
                ProcedureFqn: fqn,
                Position: i,
                Name: p.Name,
                DataType: TypeReferenceFormatter.Format(p.Type),
                Passing: p.PassMode switch
                {
                    ParameterPassMode.ByVal => "ByVal",
                    ParameterPassMode.ByRef => "ByRef",
                    _ => "ByRef",
                },
                IsOptional: p.Modifier == ParameterModifier.Optional,
                IsParamArray: p.Modifier == ParameterModifier.ParamArray,
                IsArray: p.IsArray,
                DefaultValue: null));
        }
    }

    // Matches a parser-AST procedure to its resolver-side counterpart.
    // For properties, the lookup must distinguish Get / Let / Set so a
    // VBA property-triplet (same name, three accessors) is not silently
    // collapsed back to a single resolver Declaration.
    private static ResolverProcedureDeclaration? FindResolverProcedure(
        IReadOnlyList<Declaration> declarations,
        AstProcedureDeclaration astProc)
    {
        bool astIsProperty = astProc is PropertyProcedure;
        var astPropKind = (astProc as PropertyProcedure)?.Kind;
        foreach (var decl in declarations)
        {
            if (decl is not ResolverProcedureDeclaration proc) continue;
            if (!Declaration.NameComparer.Equals(proc.Name, astProc.Name)) continue;
            // Property triplets share a name; require the accessor kind
            // to match so we return the Get for Get etc. Sub / Function
            // names are unique in a module so the name match alone is
            // enough.
            if (astIsProperty && proc is AccessHarvester.Vba.Resolver.Declarations.PropertyDeclaration resolverProp)
            {
                if (resolverProp.Kind != astPropKind) continue;
            }
            return proc;
        }
        return null;
    }

    private static ModuleKind ToModuleKind(string kind) => kind switch
    {
        "Form" => ModuleKind.Form,
        "Report" => ModuleKind.Report,
        "ClassModule" => ModuleKind.Class,
        "Class" => ModuleKind.Class,
        _ => ModuleKind.Standard,
    };

    private static BuildStatistics ComputeStatistics(
        IReadOnlyList<ModuleRow> moduleRows,
        IReadOnlyList<SymbolRow> symbolRows,
        IReadOnlySet<string> deferredFqns)
    {
        return new BuildStatistics(
            ModuleCount: moduleRows.Count,
            DeferredModuleCount: deferredFqns.Count,
            SymbolCount: symbolRows.Count,
            DeadSymbolCount: symbolRows.Count(s => s.IsDead),
            ProcedureCount: symbolRows.Count(s => s.Kind == "sub"),
            FunctionCount: symbolRows.Count(s => s.Kind == "function"),
            PropertyCount: symbolRows.Count(s =>
                s.Kind == "property_get" || s.Kind == "property_let" || s.Kind == "property_set"),
            // Module-level variables only (declared_in_fqn IS NULL). Procedure-
            // local Dim rows share kind='module_var' but are scoped to their
            // enclosing procedure; including them in the module-variable count
            // would overstate a module's API surface.
            ModuleVarCount: symbolRows.Count(s => s.Kind == "module_var" && s.DeclaredInFqn is null),
            EnumDeclCount: symbolRows.Count(s => s.Kind == "enum_decl"),
            EnumValueCount: symbolRows.Count(s => s.Kind == "enum_value"));
    }

    // Internal carrier across the parse / scope-build / symbol-extraction
    // passes so we don't repeat the AST + raw-source plumbing.
    private sealed record ParsedModule(RawModule Raw, Module Ast, ModuleScope ModuleScope);
}
