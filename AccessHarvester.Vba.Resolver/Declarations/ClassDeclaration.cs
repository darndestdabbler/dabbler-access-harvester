using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// What kind of module is housing the class. Standard modules are not
// modelled as ClassDeclarations — only modules whose default instance
// behaves as a type. Form / Report modules count because `Forms!Foo`
// resolves through the form's class.
public enum ClassDeclarationKind
{
    ClassModule,
    FormModule,
    ReportModule,
}

// One class module, form module, or report module. Each gives rise to
// a type whose members are the procedures and module-level variables
// declared in the module body. Reference resolution (#8) consumes
// these for `New <ClassName>`, `Me.Member`, `Forms!Form.Member`, and
// the bang-operator default-member access.
public sealed class ClassDeclaration : Declaration
{
    public ClassDeclarationKind Kind { get; }

    // Interfaces this class implements via `Implements <Interface>`
    // module items. Empty when the source has no Implements
    // directives. The parser-AST TypeReference is preserved verbatim;
    // binding the qualified name to a concrete declaration is #8.
    public IReadOnlyList<TypeReference> ImplementedInterfaces { get; }

    public ClassDeclaration(
        string name,
        DeclarationProvenance provenance,
        ClassDeclarationKind kind,
        IReadOnlyList<TypeReference> implementedInterfaces)
        : base(name, provenance)
    {
        Kind = kind;
        ImplementedInterfaces = implementedInterfaces;
    }
}
