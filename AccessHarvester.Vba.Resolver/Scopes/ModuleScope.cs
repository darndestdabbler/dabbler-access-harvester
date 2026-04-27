using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.Scopes;

// What kind of VBA module this scope corresponds to. Standard modules
// don't have an associated ClassDeclaration; the other three do (each
// behaves as a type for `Me`, `New`, and bang-operator default-member
// resolution in #8).
public enum ModuleKind
{
    Standard,
    Class,
    Form,
    Report,
}

// One per VBA module in the project. Always a direct child of
// ProjectScope; class/form/report modules' inner procedures and
// module-level declarations live as descendants here.
//
// `Option Explicit` and `Option Compare` state are recorded so #8 can
// decide whether an unresolved name should produce an
// ImplicitDeclaration (non-Option-Explicit) or a diagnostic (Option
// Explicit). `Option Private Module` likewise gates cross-module
// visibility for #8. Option Base affects array-bound interpretation
// and is consumed at array-construction time.
//
// `OwningClass` is non-null exactly when Kind is Class, Form, or
// Report — the ClassDeclaration is owned by this module's enclosing
// project (it appears once in the project's class registry), and the
// scope retains the back-pointer so #8 can resolve `Me` and
// `New <ClassName>` from anywhere inside the module.
public sealed class ModuleScope : Scope
{
    public string ModuleName { get; }

    public ModuleKind Kind { get; }

    // Default false. Visitor sets true if the module body contains an
    // `Option Explicit` directive.
    public bool IsOptionExplicit { get; }

    // Default false. Visitor sets true if the module body contains an
    // `Option Private Module` directive. Affects cross-module Public
    // visibility — Public declarations in such a module are visible
    // only to other modules in the same project, not to host VBA
    // consumers.
    public bool IsOptionPrivateModule { get; }

    // Default 0. Visitor sets to the explicit `Option Base 0` / `1`
    // value when present. 0 is also VBA's implicit default when no
    // directive is given, so consumers do not need to special-case
    // missing directives.
    public int OptionBase { get; }

    // Default OptionCompare.Binary (VBA's default when no directive is
    // given). Visitor sets to the spelled value when an
    // `Option Compare ...` directive is present.
    public OptionCompare OptionCompare { get; }

    // Non-null exactly when Kind != Standard. The class declaration
    // itself lives in the project's class registry (a #8 concern); the
    // back-pointer here lets in-module reference resolution skip the
    // registry round-trip.
    public ClassDeclaration? OwningClass { get; }

    public ModuleScope(
        ProjectScope parent,
        string moduleName,
        ModuleKind kind,
        SourceRange? range,
        bool isOptionExplicit,
        bool isOptionPrivateModule,
        int optionBase,
        OptionCompare optionCompare,
        ClassDeclaration? owningClass)
        : base(parent, range)
    {
        ModuleName = moduleName;
        Kind = kind;
        IsOptionExplicit = isOptionExplicit;
        IsOptionPrivateModule = isOptionPrivateModule;
        OptionBase = optionBase;
        OptionCompare = optionCompare;
        OwningClass = owningClass;
    }
}

// Mirrors the parser-AST OptionDirective's spelled Compare argument.
// Kept as a resolver-side enum so consumers do not need to parse the
// raw string; #8 will dispatch on this value when resolving
// case-sensitive vs. case-insensitive string comparisons.
public enum OptionCompare
{
    Binary,
    Text,
    Database,
}
