using System;

namespace AccessHarvester.Vba.Parser.Ast;

// Void-returning visitor. Default implementations recurse into
// children, so subclasses override only the VisitX they care about.
// Per ast-shape.md §5.
public abstract class VbaSyntaxWalker
{
    public virtual void Visit(VbaSyntaxNode? node)
    {
        if (node is null) return;

        switch (node)
        {
            case Module m:                              VisitModule(m); break;
            case OptionDirective n:                     VisitOptionDirective(n); break;
            case ImplementsDirective n:                 VisitImplementsDirective(n); break;
            case ModuleVariableDeclaration n:           VisitModuleVariableDeclaration(n); break;
            case ModuleConstantDeclaration n:           VisitModuleConstantDeclaration(n); break;
            case TypeDeclaration n:                     VisitTypeDeclaration(n); break;
            case TypeMember n:                          VisitTypeMember(n); break;
            case EnumDeclaration n:                     VisitEnumDeclaration(n); break;
            case EnumMember n:                          VisitEnumMember(n); break;
            case DeclareStatement n:                    VisitDeclareStatement(n); break;
            case EventDeclaration n:                    VisitEventDeclaration(n); break;
            case PreprocessorDirective n:               VisitPreprocessorDirective(n); break;
            case SubProcedure n:                        VisitSubProcedure(n); break;
            case FunctionProcedure n:                   VisitFunctionProcedure(n); break;
            case PropertyProcedure n:                   VisitPropertyProcedure(n); break;
            case Parameter n:                           VisitParameter(n); break;
            case TypeReference n:                       VisitTypeReference(n); break;
            case VariableDeclarator n:                  VisitVariableDeclarator(n); break;
            case ConstantDeclarator n:                  VisitConstantDeclarator(n); break;
            case RedimDeclarator n:                     VisitRedimDeclarator(n); break;
            case ArrayBound n:                          VisitArrayBound(n); break;
            // Statements
            case IfStatement n:                         VisitIfStatement(n); break;
            case ElseIfClause n:                        VisitElseIfClause(n); break;
            case ForStatement n:                        VisitForStatement(n); break;
            case ForEachStatement n:                    VisitForEachStatement(n); break;
            case DoLoopStatement n:                     VisitDoLoopStatement(n); break;
            case WhileWendStatement n:                  VisitWhileWendStatement(n); break;
            case SelectCaseStatement n:                 VisitSelectCaseStatement(n); break;
            case CaseClause n:                          VisitCaseClause(n); break;
            case CaseValueExpression n:                 VisitCaseValueExpression(n); break;
            case CaseRangeExpression n:                 VisitCaseRangeExpression(n); break;
            case CaseIsCompareExpression n:             VisitCaseIsCompareExpression(n); break;
            case WithStatement n:                       VisitWithStatement(n); break;
            case OnErrorStatement n:                    VisitOnErrorStatement(n); break;
            case ResumeStatement n:                     VisitResumeStatement(n); break;
            case GotoStatement n:                       VisitGotoStatement(n); break;
            case GosubStatement n:                      VisitGosubStatement(n); break;
            case ReturnStatement n:                     VisitReturnStatement(n); break;
            case ExitStatement n:                       VisitExitStatement(n); break;
            case StopStatement n:                       VisitStopStatement(n); break;
            case EndStatement n:                        VisitEndStatement(n); break;
            case RaiseEventStatement n:                 VisitRaiseEventStatement(n); break;
            case LocalDeclarationStatement n:           VisitLocalDeclarationStatement(n); break;
            case RedimStatement n:                      VisitRedimStatement(n); break;
            case LocalConstantStatement n:              VisitLocalConstantStatement(n); break;
            case SetStatement n:                        VisitSetStatement(n); break;
            case LetStatement n:                        VisitLetStatement(n); break;
            case CallStatement n:                       VisitCallStatement(n); break;
            case LabelStatement n:                      VisitLabelStatement(n); break;
            case PreprocessorStatement n:               VisitPreprocessorStatement(n); break;
            case UnknownStatement n:                    VisitUnknownStatement(n); break;
            // Expressions
            case LiteralExpression n:                   VisitLiteralExpression(n); break;
            case TrueExpression n:                      VisitTrueExpression(n); break;
            case FalseExpression n:                     VisitFalseExpression(n); break;
            case NothingExpression n:                   VisitNothingExpression(n); break;
            case NullExpression n:                      VisitNullExpression(n); break;
            case EmptyExpression n:                     VisitEmptyExpression(n); break;
            case MeExpression n:                        VisitMeExpression(n); break;
            case DebugExpression n:                     VisitDebugExpression(n); break;
            case IdentifierExpression n:                VisitIdentifierExpression(n); break;
            case ParenthesizedExpression n:             VisitParenthesizedExpression(n); break;
            case MemberAccessExpression n:              VisitMemberAccessExpression(n); break;
            case BangAccessExpression n:                VisitBangAccessExpression(n); break;
            case ImplicitWithMemberExpression n:        VisitImplicitWithMemberExpression(n); break;
            case ImplicitWithBangExpression n:          VisitImplicitWithBangExpression(n); break;
            case IndexOrCallExpression n:               VisitIndexOrCallExpression(n); break;
            case BinaryExpression n:                    VisitBinaryExpression(n); break;
            case UnaryExpression n:                     VisitUnaryExpression(n); break;
            case NewExpression n:                       VisitNewExpression(n); break;
            case AddressOfExpression n:                 VisitAddressOfExpression(n); break;
            case TypeOfExpression n:                    VisitTypeOfExpression(n); break;
            case UnknownExpression n:                   VisitUnknownExpression(n); break;
            // Arguments
            case PositionalArgument n:                  VisitPositionalArgument(n); break;
            case NamedArgument n:                       VisitNamedArgument(n); break;
            case MissingArgument n:                     VisitMissingArgument(n); break;
            // No silent fallthrough: a missing arm is a contract gap, not a
            // recoverable runtime case. The generic test in
            // VbaSyntaxWalkerTests covers every concrete VbaSyntaxNode type
            // in the assembly to ensure this throw is unreachable in practice.
            default:
                throw new System.InvalidOperationException(
                    $"VbaSyntaxWalker.Visit has no dispatch arm for node type {node.GetType().FullName}. " +
                    "Add a case to the switch in VbaSyntaxWalker.Visit and a corresponding Visit method.");
        }
    }

    // Default for any node type the switch did not handle (or for
    // overrides that want to delegate to "walk children"). Walks every
    // VbaSyntaxNode-typed child reachable via record properties.
    public virtual void DefaultVisit(VbaSyntaxNode node)
    {
        foreach (var child in AstChildren.Of(node))
            Visit(child);
    }

    // Module hierarchy
    public virtual void VisitModule(Module n) => DefaultVisit(n);
    public virtual void VisitOptionDirective(OptionDirective n) => DefaultVisit(n);
    public virtual void VisitImplementsDirective(ImplementsDirective n) => DefaultVisit(n);
    public virtual void VisitModuleVariableDeclaration(ModuleVariableDeclaration n) => DefaultVisit(n);
    public virtual void VisitModuleConstantDeclaration(ModuleConstantDeclaration n) => DefaultVisit(n);
    public virtual void VisitTypeDeclaration(TypeDeclaration n) => DefaultVisit(n);
    public virtual void VisitTypeMember(TypeMember n) => DefaultVisit(n);
    public virtual void VisitEnumDeclaration(EnumDeclaration n) => DefaultVisit(n);
    public virtual void VisitEnumMember(EnumMember n) => DefaultVisit(n);
    public virtual void VisitDeclareStatement(DeclareStatement n) => DefaultVisit(n);
    public virtual void VisitEventDeclaration(EventDeclaration n) => DefaultVisit(n);
    public virtual void VisitPreprocessorDirective(PreprocessorDirective n) => DefaultVisit(n);

    // Procedures
    public virtual void VisitSubProcedure(SubProcedure n) => DefaultVisit(n);
    public virtual void VisitFunctionProcedure(FunctionProcedure n) => DefaultVisit(n);
    public virtual void VisitPropertyProcedure(PropertyProcedure n) => DefaultVisit(n);
    public virtual void VisitParameter(Parameter n) => DefaultVisit(n);
    public virtual void VisitTypeReference(TypeReference n) => DefaultVisit(n);
    public virtual void VisitVariableDeclarator(VariableDeclarator n) => DefaultVisit(n);
    public virtual void VisitConstantDeclarator(ConstantDeclarator n) => DefaultVisit(n);
    public virtual void VisitRedimDeclarator(RedimDeclarator n) => DefaultVisit(n);
    public virtual void VisitArrayBound(ArrayBound n) => DefaultVisit(n);

    // Statements
    public virtual void VisitIfStatement(IfStatement n) => DefaultVisit(n);
    public virtual void VisitElseIfClause(ElseIfClause n) => DefaultVisit(n);
    public virtual void VisitForStatement(ForStatement n) => DefaultVisit(n);
    public virtual void VisitForEachStatement(ForEachStatement n) => DefaultVisit(n);
    public virtual void VisitDoLoopStatement(DoLoopStatement n) => DefaultVisit(n);
    public virtual void VisitWhileWendStatement(WhileWendStatement n) => DefaultVisit(n);
    public virtual void VisitSelectCaseStatement(SelectCaseStatement n) => DefaultVisit(n);
    public virtual void VisitCaseClause(CaseClause n) => DefaultVisit(n);
    public virtual void VisitCaseValueExpression(CaseValueExpression n) => DefaultVisit(n);
    public virtual void VisitCaseRangeExpression(CaseRangeExpression n) => DefaultVisit(n);
    public virtual void VisitCaseIsCompareExpression(CaseIsCompareExpression n) => DefaultVisit(n);
    public virtual void VisitWithStatement(WithStatement n) => DefaultVisit(n);
    public virtual void VisitOnErrorStatement(OnErrorStatement n) => DefaultVisit(n);
    public virtual void VisitResumeStatement(ResumeStatement n) => DefaultVisit(n);
    public virtual void VisitGotoStatement(GotoStatement n) => DefaultVisit(n);
    public virtual void VisitGosubStatement(GosubStatement n) => DefaultVisit(n);
    public virtual void VisitReturnStatement(ReturnStatement n) => DefaultVisit(n);
    public virtual void VisitExitStatement(ExitStatement n) => DefaultVisit(n);
    public virtual void VisitStopStatement(StopStatement n) => DefaultVisit(n);
    public virtual void VisitEndStatement(EndStatement n) => DefaultVisit(n);
    public virtual void VisitRaiseEventStatement(RaiseEventStatement n) => DefaultVisit(n);
    public virtual void VisitLocalDeclarationStatement(LocalDeclarationStatement n) => DefaultVisit(n);
    public virtual void VisitRedimStatement(RedimStatement n) => DefaultVisit(n);
    public virtual void VisitLocalConstantStatement(LocalConstantStatement n) => DefaultVisit(n);
    public virtual void VisitSetStatement(SetStatement n) => DefaultVisit(n);
    public virtual void VisitLetStatement(LetStatement n) => DefaultVisit(n);
    public virtual void VisitCallStatement(CallStatement n) => DefaultVisit(n);
    public virtual void VisitLabelStatement(LabelStatement n) => DefaultVisit(n);
    public virtual void VisitPreprocessorStatement(PreprocessorStatement n) => DefaultVisit(n);
    public virtual void VisitUnknownStatement(UnknownStatement n) => DefaultVisit(n);

    // Expressions
    public virtual void VisitLiteralExpression(LiteralExpression n) => DefaultVisit(n);
    public virtual void VisitTrueExpression(TrueExpression n) => DefaultVisit(n);
    public virtual void VisitFalseExpression(FalseExpression n) => DefaultVisit(n);
    public virtual void VisitNothingExpression(NothingExpression n) => DefaultVisit(n);
    public virtual void VisitNullExpression(NullExpression n) => DefaultVisit(n);
    public virtual void VisitEmptyExpression(EmptyExpression n) => DefaultVisit(n);
    public virtual void VisitMeExpression(MeExpression n) => DefaultVisit(n);
    public virtual void VisitDebugExpression(DebugExpression n) => DefaultVisit(n);
    public virtual void VisitIdentifierExpression(IdentifierExpression n) => DefaultVisit(n);
    public virtual void VisitParenthesizedExpression(ParenthesizedExpression n) => DefaultVisit(n);
    public virtual void VisitMemberAccessExpression(MemberAccessExpression n) => DefaultVisit(n);
    public virtual void VisitBangAccessExpression(BangAccessExpression n) => DefaultVisit(n);
    public virtual void VisitImplicitWithMemberExpression(ImplicitWithMemberExpression n) => DefaultVisit(n);
    public virtual void VisitImplicitWithBangExpression(ImplicitWithBangExpression n) => DefaultVisit(n);
    public virtual void VisitIndexOrCallExpression(IndexOrCallExpression n) => DefaultVisit(n);
    public virtual void VisitBinaryExpression(BinaryExpression n) => DefaultVisit(n);
    public virtual void VisitUnaryExpression(UnaryExpression n) => DefaultVisit(n);
    public virtual void VisitNewExpression(NewExpression n) => DefaultVisit(n);
    public virtual void VisitAddressOfExpression(AddressOfExpression n) => DefaultVisit(n);
    public virtual void VisitTypeOfExpression(TypeOfExpression n) => DefaultVisit(n);
    public virtual void VisitUnknownExpression(UnknownExpression n) => DefaultVisit(n);

    // Arguments
    public virtual void VisitPositionalArgument(PositionalArgument n) => DefaultVisit(n);
    public virtual void VisitNamedArgument(NamedArgument n) => DefaultVisit(n);
    public virtual void VisitMissingArgument(MissingArgument n) => DefaultVisit(n);
}
