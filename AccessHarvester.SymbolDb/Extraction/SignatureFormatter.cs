using System.Collections.Generic;
using System.Linq;
using System.Text;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.SymbolDb.Extraction;

// Renders a stable, hash-friendly signature string for a procedure / property /
// declare. Format: '<Kind>(<param1>,<param2>,...):<return>' where each
// parameter is '<name>:<type>:<passing>[:Optional][:ParamArray][:Array]'.
// Used both as the human-readable signature_string column and as one half of
// the (signature_string, body_hash_md5) MD5 dedup tuple.
public static class SignatureFormatter
{
    public static string FormatProcedure(ProcedureDeclaration procedure)
    {
        string kind = procedure switch
        {
            SubProcedure => "Sub",
            FunctionProcedure => "Function",
            PropertyProcedure { Kind: PropertyKind.Get } => "PropertyGet",
            PropertyProcedure { Kind: PropertyKind.Let } => "PropertyLet",
            PropertyProcedure { Kind: PropertyKind.Set } => "PropertySet",
            _ => "Procedure",
        };

        string parameters = FormatParameters(procedure.Parameters);
        string returnType = TypeReferenceFormatter.Format(procedure.ReturnType) ?? "Void";
        return $"{kind}({parameters}):{returnType}";
    }

    public static string FormatDeclare(DeclareStatement declare)
    {
        string kind = declare.Kind == DeclareKind.Sub ? "DeclareSub" : "DeclareFunction";
        string parameters = FormatParameters(declare.Parameters);
        string returnType = TypeReferenceFormatter.Format(declare.ReturnType) ?? "Void";
        return $"{kind}({parameters}):{returnType}";
    }

    private static string FormatParameters(IReadOnlyList<Parameter> parameters)
    {
        if (parameters.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < parameters.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var p = parameters[i];
            sb.Append(p.Name);
            sb.Append(':');
            sb.Append(TypeReferenceFormatter.Format(p.Type) ?? "Variant");
            sb.Append(':');
            sb.Append(p.PassMode switch
            {
                ParameterPassMode.ByVal => "ByVal",
                ParameterPassMode.ByRef => "ByRef",
                _ => "ByRef", // VBA default per §4.6
            });
            if (p.Modifier == ParameterModifier.Optional) sb.Append(":Optional");
            if (p.Modifier == ParameterModifier.ParamArray) sb.Append(":ParamArray");
            if (p.IsArray) sb.Append(":Array");
        }
        return sb.ToString();
    }
}

internal static class TypeReferenceFormatter
{
    public static string? Format(TypeReference? typeRef)
    {
        if (typeRef is null) return null;
        string qualified = string.Join(".", typeRef.QualifiedName);
        return typeRef.IsArray ? qualified + "()" : qualified;
    }
}
