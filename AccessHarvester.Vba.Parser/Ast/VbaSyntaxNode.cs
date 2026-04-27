namespace AccessHarvester.Vba.Parser.Ast;

// Root of the AST hierarchy. Every node carries a Provenance that
// distinguishes source-derived nodes from recovery-reconstructed ones
// (per ast-shape.md §3). All concrete node types are sealed records;
// transformations produce new trees via `with`.
public abstract record VbaSyntaxNode(Provenance Provenance);
