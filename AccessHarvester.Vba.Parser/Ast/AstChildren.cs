using System.Collections.Generic;

namespace AccessHarvester.Vba.Parser.Ast;

// Yields the immediate VbaSyntaxNode children of a node, in
// source-positional order. Used by VbaSyntaxWalker.DefaultVisit and
// VbaSyntaxVisitor<T>.DefaultVisit when an override wants to delegate
// to "walk children". Centralizing this here keeps the visitor base
// classes independent of the per-node child shape.
internal static class AstChildren
{
    public static IEnumerable<VbaSyntaxNode> Of(VbaSyntaxNode node)
    {
        switch (node)
        {
            case Module m:
                foreach (var item in m.Items) yield return item;
                break;

            case OptionDirective:
                break;

            case ImplementsDirective n:
                yield return n.Interface;
                break;

            case ModuleVariableDeclaration n:
                foreach (var d in n.Declarators) yield return d;
                break;

            case ModuleConstantDeclaration n:
                foreach (var d in n.Declarators) yield return d;
                break;

            case TypeDeclaration n:
                foreach (var m in n.Members) yield return m;
                break;

            case TypeMember n:
                if (n.Type is not null) yield return n.Type;
                foreach (var b in n.ArrayBounds) yield return b;
                break;

            case EnumDeclaration n:
                foreach (var m in n.Members) yield return m;
                break;

            case EnumMember n:
                if (n.Value is not null) yield return n.Value;
                break;

            case DeclareStatement n:
                foreach (var p in n.Parameters) yield return p;
                if (n.ReturnType is not null) yield return n.ReturnType;
                break;

            case EventDeclaration n:
                foreach (var p in n.Parameters) yield return p;
                break;

            case PreprocessorDirective:
                break;

            case ProcedureDeclaration n:                // SubProcedure / FunctionProcedure / PropertyProcedure
                foreach (var p in n.Parameters) yield return p;
                if (n.ReturnType is not null) yield return n.ReturnType;
                foreach (var s in n.Body) yield return s;
                break;

            case Parameter n:
                if (n.Type is not null) yield return n.Type;
                if (n.DefaultValue is not null) yield return n.DefaultValue;
                break;

            case TypeReference n:
                if (n.FixedStringSize is not null) yield return n.FixedStringSize;
                break;

            case VariableDeclarator n:
                if (n.Type is not null) yield return n.Type;
                foreach (var b in n.ArrayBounds) yield return b;
                break;

            case ConstantDeclarator n:
                if (n.Type is not null) yield return n.Type;
                yield return n.Value;
                break;

            case RedimDeclarator n:
                foreach (var b in n.ArrayBounds) yield return b;
                if (n.Type is not null) yield return n.Type;
                break;

            case ArrayBound n:
                if (n.Lower is not null) yield return n.Lower;
                yield return n.Upper;
                break;

            case IfStatement n:
                yield return n.Condition;
                foreach (var s in n.ThenBody) yield return s;
                foreach (var c in n.ElseIfClauses) yield return c;
                if (n.ElseBody is not null)
                    foreach (var s in n.ElseBody) yield return s;
                break;

            case ElseIfClause n:
                yield return n.Condition;
                foreach (var s in n.Body) yield return s;
                break;

            case ForStatement n:
                yield return n.From;
                yield return n.To;
                if (n.Step is not null) yield return n.Step;
                foreach (var s in n.Body) yield return s;
                break;

            case ForEachStatement n:
                yield return n.Collection;
                foreach (var s in n.Body) yield return s;
                break;

            case DoLoopStatement n:
                if (n.Condition is not null) yield return n.Condition;
                foreach (var s in n.Body) yield return s;
                break;

            case WhileWendStatement n:
                yield return n.Condition;
                foreach (var s in n.Body) yield return s;
                break;

            case SelectCaseStatement n:
                yield return n.Selector;
                foreach (var c in n.Cases) yield return c;
                if (n.CaseElseBody is not null)
                    foreach (var s in n.CaseElseBody) yield return s;
                break;

            case CaseClause n:
                foreach (var e in n.Expressions) yield return e;
                foreach (var s in n.Body) yield return s;
                break;

            case CaseValueExpression n:
                yield return n.Value;
                break;

            case CaseRangeExpression n:
                yield return n.Lo;
                yield return n.Hi;
                break;

            case CaseIsCompareExpression n:
                yield return n.Value;
                break;

            case WithStatement n:
                yield return n.Target;
                foreach (var s in n.Body) yield return s;
                break;

            case OnErrorStatement:
            case ResumeStatement:
            case GotoStatement:
            case GosubStatement:
            case ReturnStatement:
            case ExitStatement:
            case StopStatement:
            case EndStatement:
                break;

            case RaiseEventStatement n:
                foreach (var a in n.Arguments) yield return a;
                break;

            case LocalDeclarationStatement n:
                foreach (var d in n.Declarators) yield return d;
                break;

            case RedimStatement n:
                foreach (var d in n.Declarators) yield return d;
                break;

            case LocalConstantStatement n:
                foreach (var d in n.Declarators) yield return d;
                break;

            case SetStatement n:
                yield return n.Target;
                yield return n.Value;
                break;

            case LetStatement n:
                yield return n.Target;
                yield return n.Value;
                break;

            case CallStatement n:
                yield return n.Target;
                foreach (var a in n.BareArguments) yield return a;
                break;

            case LabelStatement:
                break;

            case UnknownStatement:
                break;

            // Expressions
            case LiteralExpression:
            case TrueExpression:
            case FalseExpression:
            case NothingExpression:
            case NullExpression:
            case EmptyExpression:
            case MeExpression:
            case DebugExpression:
            case IdentifierExpression:
                break;

            case ParenthesizedExpression n:
                yield return n.Inner;
                break;

            case MemberAccessExpression n:
                yield return n.Target;
                break;

            case BangAccessExpression n:
                yield return n.Target;
                break;

            case ImplicitWithMemberExpression:
            case ImplicitWithBangExpression:
                break;

            case IndexOrCallExpression n:
                yield return n.Target;
                foreach (var a in n.Arguments) yield return a;
                break;

            case BinaryExpression n:
                yield return n.Left;
                yield return n.Right;
                break;

            case UnaryExpression n:
                yield return n.Operand;
                break;

            case NewExpression n:
                yield return n.Type;
                break;

            case AddressOfExpression n:
                yield return n.ProcedureReference;
                break;

            case TypeOfExpression n:
                yield return n.Operand;
                yield return n.Type;
                break;

            case UnknownExpression:
                break;

            case PositionalArgument n:
                yield return n.Value;
                break;

            case NamedArgument n:
                yield return n.Value;
                break;

            case MissingArgument:
                break;
        }
    }
}
