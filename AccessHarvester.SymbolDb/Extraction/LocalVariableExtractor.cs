using System;
using System.Collections.Generic;
using AccessHarvester.SymbolDb.Models;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.SymbolDb.Extraction;

// Walks a procedure body (and recursively into its nested control-flow
// blocks) collecting Dim / Static / Const declarations. Each becomes a
// kind='module_var' (or 'module_const') Symbols row with declared_in_fqn
// set to the enclosing procedure's FQN — e.g. a control event handler such
// as `cboCategory_BeforeUpdate` and its procedure-local `strMsg` / `strArgs`.
public static class LocalVariableExtractor
{
    public static void ExtractInto(
        ProcedureDeclaration procedure,
        string moduleFqn,
        string procedureFqn,
        List<SymbolRow> symbolRows)
    {
        WalkStatements(procedure.Body, moduleFqn, procedureFqn, symbolRows);
    }

    private static void WalkStatements(
        IReadOnlyList<Statement> statements,
        string moduleFqn,
        string procedureFqn,
        List<SymbolRow> symbolRows)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case LocalDeclarationStatement local:
                    foreach (var declarator in local.Declarators)
                    {
                        symbolRows.Add(new SymbolRow(
                            Fqn: $"{procedureFqn}.{declarator.Name}",
                            ModuleFqn: moduleFqn,
                            Name: declarator.Name,
                            Kind: "module_var",
                            Access: null,
                            DataType: TypeReferenceFormatter.Format(declarator.Type),
                            DeclaredInFqn: procedureFqn,
                            SourceLineStart: declarator.Provenance.Range?.Start.Line,
                            SourceLineEnd: declarator.Provenance.Range?.End.Line,
                            SourceBody: null,
                            SignatureString: null,
                            BodyHashMd5: null,
                            IsDead: false,
                            DeadCodeReason: null,
                            EquivalenceCanonicalFqn: null));
                    }
                    break;
                case LocalConstantStatement constant:
                    foreach (var declarator in constant.Declarators)
                    {
                        symbolRows.Add(new SymbolRow(
                            Fqn: $"{procedureFqn}.{declarator.Name}",
                            ModuleFqn: moduleFqn,
                            Name: declarator.Name,
                            Kind: "module_const",
                            Access: null,
                            DataType: TypeReferenceFormatter.Format(declarator.Type),
                            DeclaredInFqn: procedureFqn,
                            SourceLineStart: declarator.Provenance.Range?.Start.Line,
                            SourceLineEnd: declarator.Provenance.Range?.End.Line,
                            SourceBody: null,
                            SignatureString: null,
                            BodyHashMd5: null,
                            IsDead: false,
                            DeadCodeReason: null,
                            EquivalenceCanonicalFqn: null));
                    }
                    break;
                case IfStatement ifStatement:
                    WalkStatements(ifStatement.ThenBody, moduleFqn, procedureFqn, symbolRows);
                    foreach (var elseIf in ifStatement.ElseIfClauses)
                        WalkStatements(elseIf.Body, moduleFqn, procedureFqn, symbolRows);
                    if (ifStatement.ElseBody is not null)
                        WalkStatements(ifStatement.ElseBody, moduleFqn, procedureFqn, symbolRows);
                    break;
                case ForStatement forStmt:
                    WalkStatements(forStmt.Body, moduleFqn, procedureFqn, symbolRows);
                    break;
                case ForEachStatement forEach:
                    WalkStatements(forEach.Body, moduleFqn, procedureFqn, symbolRows);
                    break;
                case DoLoopStatement doLoop:
                    WalkStatements(doLoop.Body, moduleFqn, procedureFqn, symbolRows);
                    break;
                case WhileWendStatement whileStmt:
                    WalkStatements(whileStmt.Body, moduleFqn, procedureFqn, symbolRows);
                    break;
                case WithStatement withStmt:
                    WalkStatements(withStmt.Body, moduleFqn, procedureFqn, symbolRows);
                    break;
                case SelectCaseStatement selectStmt:
                    foreach (var caseClause in selectStmt.Cases)
                        WalkStatements(caseClause.Body, moduleFqn, procedureFqn, symbolRows);
                    if (selectStmt.CaseElseBody is not null)
                        WalkStatements(selectStmt.CaseElseBody, moduleFqn, procedureFqn, symbolRows);
                    break;
                default:
                    // Other statement kinds carry no nested declarations.
                    break;
            }
        }
    }
}
