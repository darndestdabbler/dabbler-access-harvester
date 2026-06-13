namespace AccessHarvester.Vba.Parser.Ast;

// Generic-return visitor. Default implementations return default(T) and
// do NOT recurse into children — TResult-returning visitors are
// typically aggregating, so a no-op default is safer than walking
// children whose results would be discarded. Per ast-shape.md §5.
public abstract class VbaSyntaxVisitor<TResult>
{
    public virtual TResult? Visit(VbaSyntaxNode? node)
    {
        if (node is null) return default;

        return node switch
        {
            Module n                                    => VisitModule(n),
            OptionDirective n                           => VisitOptionDirective(n),
            ImplementsDirective n                       => VisitImplementsDirective(n),
            ModuleVariableDeclaration n                 => VisitModuleVariableDeclaration(n),
            ModuleConstantDeclaration n                 => VisitModuleConstantDeclaration(n),
            TypeDeclaration n                           => VisitTypeDeclaration(n),
            TypeMember n                                => VisitTypeMember(n),
            EnumDeclaration n                           => VisitEnumDeclaration(n),
            EnumMember n                                => VisitEnumMember(n),
            DeclareStatement n                          => VisitDeclareStatement(n),
            EventDeclaration n                          => VisitEventDeclaration(n),
            PreprocessorDirective n                     => VisitPreprocessorDirective(n),
            UnknownModuleItem n                         => VisitUnknownModuleItem(n),
            SubProcedure n                              => VisitSubProcedure(n),
            FunctionProcedure n                         => VisitFunctionProcedure(n),
            PropertyProcedure n                         => VisitPropertyProcedure(n),
            Parameter n                                 => VisitParameter(n),
            TypeReference n                             => VisitTypeReference(n),
            VariableDeclarator n                        => VisitVariableDeclarator(n),
            ConstantDeclarator n                        => VisitConstantDeclarator(n),
            RedimDeclarator n                           => VisitRedimDeclarator(n),
            ArrayBound n                                => VisitArrayBound(n),

            IfStatement n                               => VisitIfStatement(n),
            ElseIfClause n                              => VisitElseIfClause(n),
            ForStatement n                              => VisitForStatement(n),
            ForEachStatement n                          => VisitForEachStatement(n),
            DoLoopStatement n                           => VisitDoLoopStatement(n),
            WhileWendStatement n                        => VisitWhileWendStatement(n),
            SelectCaseStatement n                       => VisitSelectCaseStatement(n),
            CaseClause n                                => VisitCaseClause(n),
            CaseValueExpression n                       => VisitCaseValueExpression(n),
            CaseRangeExpression n                       => VisitCaseRangeExpression(n),
            CaseIsCompareExpression n                   => VisitCaseIsCompareExpression(n),
            WithStatement n                             => VisitWithStatement(n),
            OnErrorStatement n                          => VisitOnErrorStatement(n),
            ResumeStatement n                           => VisitResumeStatement(n),
            GotoStatement n                             => VisitGotoStatement(n),
            GosubStatement n                            => VisitGosubStatement(n),
            ReturnStatement n                           => VisitReturnStatement(n),
            ExitStatement n                             => VisitExitStatement(n),
            StopStatement n                             => VisitStopStatement(n),
            EndStatement n                              => VisitEndStatement(n),
            RaiseEventStatement n                       => VisitRaiseEventStatement(n),
            LocalDeclarationStatement n                 => VisitLocalDeclarationStatement(n),
            RedimStatement n                            => VisitRedimStatement(n),
            LocalConstantStatement n                    => VisitLocalConstantStatement(n),
            SetStatement n                              => VisitSetStatement(n),
            LetStatement n                              => VisitLetStatement(n),
            CallStatement n                             => VisitCallStatement(n),
            LabelStatement n                            => VisitLabelStatement(n),
            PreprocessorStatement n                     => VisitPreprocessorStatement(n),
            UnknownStatement n                          => VisitUnknownStatement(n),

            LiteralExpression n                         => VisitLiteralExpression(n),
            TrueExpression n                            => VisitTrueExpression(n),
            FalseExpression n                           => VisitFalseExpression(n),
            NothingExpression n                         => VisitNothingExpression(n),
            NullExpression n                            => VisitNullExpression(n),
            EmptyExpression n                           => VisitEmptyExpression(n),
            MeExpression n                              => VisitMeExpression(n),
            DebugExpression n                           => VisitDebugExpression(n),
            IdentifierExpression n                      => VisitIdentifierExpression(n),
            ParenthesizedExpression n                   => VisitParenthesizedExpression(n),
            MemberAccessExpression n                    => VisitMemberAccessExpression(n),
            BangAccessExpression n                      => VisitBangAccessExpression(n),
            ImplicitWithMemberExpression n              => VisitImplicitWithMemberExpression(n),
            ImplicitWithBangExpression n                => VisitImplicitWithBangExpression(n),
            IndexOrCallExpression n                     => VisitIndexOrCallExpression(n),
            BinaryExpression n                          => VisitBinaryExpression(n),
            UnaryExpression n                           => VisitUnaryExpression(n),
            NewExpression n                             => VisitNewExpression(n),
            AddressOfExpression n                       => VisitAddressOfExpression(n),
            TypeOfExpression n                          => VisitTypeOfExpression(n),
            UnknownExpression n                         => VisitUnknownExpression(n),

            PositionalArgument n                        => VisitPositionalArgument(n),
            NamedArgument n                             => VisitNamedArgument(n),
            MissingArgument n                           => VisitMissingArgument(n),

            // No silent fallthrough: a missing arm is a contract gap, not a
            // recoverable runtime case. See VbaSyntaxWalker.Visit for the
            // matching policy and the test that covers it.
            _ => throw new System.InvalidOperationException(
                $"VbaSyntaxVisitor<TResult>.Visit has no dispatch arm for node type {node.GetType().FullName}. " +
                "Add a case to the switch in VbaSyntaxVisitor<TResult>.Visit and a corresponding Visit method."),
        };
    }

    public virtual TResult? DefaultVisit(VbaSyntaxNode node) => default;

    // Module hierarchy
    public virtual TResult? VisitModule(Module n) => DefaultVisit(n);
    public virtual TResult? VisitOptionDirective(OptionDirective n) => DefaultVisit(n);
    public virtual TResult? VisitImplementsDirective(ImplementsDirective n) => DefaultVisit(n);
    public virtual TResult? VisitModuleVariableDeclaration(ModuleVariableDeclaration n) => DefaultVisit(n);
    public virtual TResult? VisitModuleConstantDeclaration(ModuleConstantDeclaration n) => DefaultVisit(n);
    public virtual TResult? VisitTypeDeclaration(TypeDeclaration n) => DefaultVisit(n);
    public virtual TResult? VisitTypeMember(TypeMember n) => DefaultVisit(n);
    public virtual TResult? VisitEnumDeclaration(EnumDeclaration n) => DefaultVisit(n);
    public virtual TResult? VisitEnumMember(EnumMember n) => DefaultVisit(n);
    public virtual TResult? VisitDeclareStatement(DeclareStatement n) => DefaultVisit(n);
    public virtual TResult? VisitEventDeclaration(EventDeclaration n) => DefaultVisit(n);
    public virtual TResult? VisitPreprocessorDirective(PreprocessorDirective n) => DefaultVisit(n);
    public virtual TResult? VisitUnknownModuleItem(UnknownModuleItem n) => DefaultVisit(n);

    public virtual TResult? VisitSubProcedure(SubProcedure n) => DefaultVisit(n);
    public virtual TResult? VisitFunctionProcedure(FunctionProcedure n) => DefaultVisit(n);
    public virtual TResult? VisitPropertyProcedure(PropertyProcedure n) => DefaultVisit(n);
    public virtual TResult? VisitParameter(Parameter n) => DefaultVisit(n);
    public virtual TResult? VisitTypeReference(TypeReference n) => DefaultVisit(n);
    public virtual TResult? VisitVariableDeclarator(VariableDeclarator n) => DefaultVisit(n);
    public virtual TResult? VisitConstantDeclarator(ConstantDeclarator n) => DefaultVisit(n);
    public virtual TResult? VisitRedimDeclarator(RedimDeclarator n) => DefaultVisit(n);
    public virtual TResult? VisitArrayBound(ArrayBound n) => DefaultVisit(n);

    public virtual TResult? VisitIfStatement(IfStatement n) => DefaultVisit(n);
    public virtual TResult? VisitElseIfClause(ElseIfClause n) => DefaultVisit(n);
    public virtual TResult? VisitForStatement(ForStatement n) => DefaultVisit(n);
    public virtual TResult? VisitForEachStatement(ForEachStatement n) => DefaultVisit(n);
    public virtual TResult? VisitDoLoopStatement(DoLoopStatement n) => DefaultVisit(n);
    public virtual TResult? VisitWhileWendStatement(WhileWendStatement n) => DefaultVisit(n);
    public virtual TResult? VisitSelectCaseStatement(SelectCaseStatement n) => DefaultVisit(n);
    public virtual TResult? VisitCaseClause(CaseClause n) => DefaultVisit(n);
    public virtual TResult? VisitCaseValueExpression(CaseValueExpression n) => DefaultVisit(n);
    public virtual TResult? VisitCaseRangeExpression(CaseRangeExpression n) => DefaultVisit(n);
    public virtual TResult? VisitCaseIsCompareExpression(CaseIsCompareExpression n) => DefaultVisit(n);
    public virtual TResult? VisitWithStatement(WithStatement n) => DefaultVisit(n);
    public virtual TResult? VisitOnErrorStatement(OnErrorStatement n) => DefaultVisit(n);
    public virtual TResult? VisitResumeStatement(ResumeStatement n) => DefaultVisit(n);
    public virtual TResult? VisitGotoStatement(GotoStatement n) => DefaultVisit(n);
    public virtual TResult? VisitGosubStatement(GosubStatement n) => DefaultVisit(n);
    public virtual TResult? VisitReturnStatement(ReturnStatement n) => DefaultVisit(n);
    public virtual TResult? VisitExitStatement(ExitStatement n) => DefaultVisit(n);
    public virtual TResult? VisitStopStatement(StopStatement n) => DefaultVisit(n);
    public virtual TResult? VisitEndStatement(EndStatement n) => DefaultVisit(n);
    public virtual TResult? VisitRaiseEventStatement(RaiseEventStatement n) => DefaultVisit(n);
    public virtual TResult? VisitLocalDeclarationStatement(LocalDeclarationStatement n) => DefaultVisit(n);
    public virtual TResult? VisitRedimStatement(RedimStatement n) => DefaultVisit(n);
    public virtual TResult? VisitLocalConstantStatement(LocalConstantStatement n) => DefaultVisit(n);
    public virtual TResult? VisitSetStatement(SetStatement n) => DefaultVisit(n);
    public virtual TResult? VisitLetStatement(LetStatement n) => DefaultVisit(n);
    public virtual TResult? VisitCallStatement(CallStatement n) => DefaultVisit(n);
    public virtual TResult? VisitLabelStatement(LabelStatement n) => DefaultVisit(n);
    public virtual TResult? VisitPreprocessorStatement(PreprocessorStatement n) => DefaultVisit(n);
    public virtual TResult? VisitUnknownStatement(UnknownStatement n) => DefaultVisit(n);

    public virtual TResult? VisitLiteralExpression(LiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitTrueExpression(TrueExpression n) => DefaultVisit(n);
    public virtual TResult? VisitFalseExpression(FalseExpression n) => DefaultVisit(n);
    public virtual TResult? VisitNothingExpression(NothingExpression n) => DefaultVisit(n);
    public virtual TResult? VisitNullExpression(NullExpression n) => DefaultVisit(n);
    public virtual TResult? VisitEmptyExpression(EmptyExpression n) => DefaultVisit(n);
    public virtual TResult? VisitMeExpression(MeExpression n) => DefaultVisit(n);
    public virtual TResult? VisitDebugExpression(DebugExpression n) => DefaultVisit(n);
    public virtual TResult? VisitIdentifierExpression(IdentifierExpression n) => DefaultVisit(n);
    public virtual TResult? VisitParenthesizedExpression(ParenthesizedExpression n) => DefaultVisit(n);
    public virtual TResult? VisitMemberAccessExpression(MemberAccessExpression n) => DefaultVisit(n);
    public virtual TResult? VisitBangAccessExpression(BangAccessExpression n) => DefaultVisit(n);
    public virtual TResult? VisitImplicitWithMemberExpression(ImplicitWithMemberExpression n) => DefaultVisit(n);
    public virtual TResult? VisitImplicitWithBangExpression(ImplicitWithBangExpression n) => DefaultVisit(n);
    public virtual TResult? VisitIndexOrCallExpression(IndexOrCallExpression n) => DefaultVisit(n);
    public virtual TResult? VisitBinaryExpression(BinaryExpression n) => DefaultVisit(n);
    public virtual TResult? VisitUnaryExpression(UnaryExpression n) => DefaultVisit(n);
    public virtual TResult? VisitNewExpression(NewExpression n) => DefaultVisit(n);
    public virtual TResult? VisitAddressOfExpression(AddressOfExpression n) => DefaultVisit(n);
    public virtual TResult? VisitTypeOfExpression(TypeOfExpression n) => DefaultVisit(n);
    public virtual TResult? VisitUnknownExpression(UnknownExpression n) => DefaultVisit(n);

    public virtual TResult? VisitPositionalArgument(PositionalArgument n) => DefaultVisit(n);
    public virtual TResult? VisitNamedArgument(NamedArgument n) => DefaultVisit(n);
    public virtual TResult? VisitMissingArgument(MissingArgument n) => DefaultVisit(n);
}
