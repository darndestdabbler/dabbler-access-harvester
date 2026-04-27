namespace AccessHarvester.Sql.Parser.Ast;

// Root of the SQL AST hierarchy. Every node carries a Provenance that
// distinguishes source-derived nodes from recovery-reconstructed ones.
// All concrete node types are sealed records; transformations produce
// new trees via `with`.
public abstract record SqlSyntaxNode(Provenance Provenance);
