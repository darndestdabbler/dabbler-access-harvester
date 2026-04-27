# AST Shape — VBA Parser

> **Authored:** 2026-04-27 by Session 2 of `parser-infrastructure` (Opus 4.7
> orchestrator, in-conversation per `project-guidance.md` →
> "When the orchestrator's model matches the routing target...").
> Synthesis 2 of 2 for this set; error-recovery strategy is Session 1
> (`error-recovery-strategy.md`).
> **Inputs:** `docs/VBA-Parser-Migration-Harvester-Plan.md` Phase 2.3;
> `AccessHarvester.Vba.Grammar/Vba.g4` (full grammar — module, statement,
> expression, lexical sections); the 12 existing
> `docs/test-cases/<n>.expected.json` shapes (downstream
> harvester-schema.v0 outputs that consume this AST); Session 1's
> `error-recovery-strategy.md` §7 (Provenance forward contract).

---

## 1. Posture

The grammar produces a verbose ANTLR parse tree (`VbaParser.ModuleContext`)
tightly coupled to grammar-rule names (e.g., `IfBlockStmtContext`,
`AddSubExprContext`, `ImplicitCallStmtContext`). Downstream consumers
(resolver, classifier, schema-validator, fixture-comparison harness) need a
**stable, immutable, grammar-shape-decoupled** representation that:

- Survives grammar refactors that re-spell internal rule names but preserve
  semantics (`AddSubExpr` → `AdditiveExpr` should not break the resolver).
- Carries explicit provenance distinguishing source-derived nodes from
  recovery-reconstructed nodes (per Session 1 `error-recovery-strategy.md`
  §7 — the parser-level analogue of the project-wide "unresolvable
  constructs are explicit" principle).
- Is a fixed contract every later parser/resolver child set targets. AST
  shape stability is called out as a `project-guidance.md` standard for
  this set: even cleaner rewrites in #7 onwards require explicit
  justification.

The AST is **NOT** the harvester-schema.v0 output. The harvester-schema
records resolved control bindings, validation patterns, classification
tags, complexity scores, etc. — all downstream of the AST. The AST is
the intermediate shape that the resolver and enricher chain consumes.
Keep the two layered:

```
.bas source → ANTLR parse tree (VbaParser.ModuleContext)
            → AST (this session)        ← stable contract for #7+
            → Resolved AST              ← #7 resolver-declarations-and-scopes adds symbol bindings
            → harvester-schema.v0 JSON  ← #11+ enrichers add classification, validation, etc.
```

## 2. Design rules

These rules govern every AST node type:

1. **Records, not classes.** Every node is a `sealed record` (immutable,
   value-equality, free `with` cloning).
2. **Provenance on every node.** Per Session 1 §7. The discriminated
   union is `Provenance` with `SourceProvenance(int Line, int Column)`
   and `RecoveredProvenance(string Reason, int Line, int Column)`.
   Session 3 will introduce richer `SourceLocation` / `SourceRange`
   types and may extend `Provenance` to carry them; the discriminator
   between "source" and "recovered" is the stable contract.
3. **Lists are `IReadOnlyList<T>`.** Never `List<T>` in public surface;
   the builder's internal lists are `List<T>` until handoff.
4. **No parent pointers.** Cycles defeat record value-equality and
   immutability; consumers that need the parent track it themselves
   with a `Stack<VbaSyntaxNode>` during walks.
5. **No original-text storage on every node.** Only the few constructs
   that downstream needs to fingerprint (currently none in scope; the
   unresolvable-construct case is recorded by `RecoveredProvenance`'s
   reason string). Downstream can recompute text from `Provenance` +
   the original source string when needed.
6. **Lean variant set.** A grammar alternative becomes a distinct AST
   node type **only when** downstream cares about the distinction.
   `AddSubExpr` and `MulDivExpr` collapse into a single `BinaryExpression`
   with a `BinaryOperator` enum; `MemberAccess` and `BangAccess` stay
   separate because the resolver applies different default-member
   semantics (per H-B02 in the grammar).
7. **No nullable-when-empty.** A procedure with no parameters has
   `Parameters: []`, never `null`. Empty lists, never null lists.
8. **Lossy parse alts collapse to a sentinel.** When the parse tree
   contains an `IErrorNode` the builder cannot meaningfully descend
   into, the produced AST node uses `RecoveredProvenance` and the
   appropriate `Unknown*` placeholder (`UnknownStatement`,
   `UnknownExpression`). The grammar-level `# UnknownStmt` does not
   exist; this is the AST consumer's safety net for malformed input
   that ANTLR's recovery patched over.

## 3. Provenance

Per Session 1 `error-recovery-strategy.md` §7 (forward contract):

```csharp
public abstract record Provenance(int Line, int Column);

public sealed record SourceProvenance(int Line, int Column)
    : Provenance(Line, Column);

public sealed record RecoveredProvenance(string Reason, int Line, int Column)
    : Provenance(Line, Column);
```

Detection rule (from §7): a node is `RecoveredProvenance` iff its
underlying parse-tree subtree contains at least one `IErrorNode`. The
reason string is the message of the nearest preceding `ParseDiagnostic`
in the diagnostic list (matched by line/column ordering); when no
preceding diagnostic exists, the reason is `"unknown-recovery"`.

Session 3 will introduce `SourceLocation` (file + line/column) and
`SourceRange` (start + end). The expected migration is to add a
`SourceRange Range` field to `Provenance` (additive, not breaking),
keeping `Line` and `Column` as forwarding properties on
`SourceProvenance` / `RecoveredProvenance` for one transitional
session and removing them in Session 4. This session does not
pre-commit to that; it commits only to the §7 contract.

## 4. Node hierarchy

The hierarchy mirrors the grammar's logical groupings while collapsing
purely syntactic distinctions per rule §6 above. Names are PascalCase
nouns; abstract bases end with the category name; `*Statement`,
`*Expression`, `*Declaration` suffixes mark the kind.

### Root

```
VbaSyntaxNode (abstract)
  Provenance Provenance { get; }
```

### Module

```
Module : VbaSyntaxNode
  IReadOnlyList<ModuleItem> Items
```

### Module items

```
ModuleItem : VbaSyntaxNode (abstract)
  ProcedureDeclaration         (abstract — Sub / Function / PropertyGet/Let/Set)
  ModuleVariableDeclaration    (Public / Private / Dim / Global; one decl, n items)
  ModuleConstantDeclaration    (Public / Private Const)
  TypeDeclaration              (UDT)
  EnumDeclaration              (Enum)
  DeclareStatement             (external API import)
  EventDeclaration             (Public Event)
  ImplementsDirective          (Implements <interface>)
  OptionDirective              (Option Explicit / Compare / Base / Private Module)
  PreprocessorDirective        (#If / #ElseIf / #Else / #End If / #Const)
```

`ProcedureDeclaration` itself is abstract; concrete:

```
SubProcedure        : ProcedureDeclaration
FunctionProcedure   : ProcedureDeclaration
PropertyProcedure   : ProcedureDeclaration   (kind: Get | Let | Set)
```

All procedure kinds share:

```
ProcedureDeclaration
  string Name
  AccessModifier Access            (Public | Private | Friend | Unspecified)
  bool IsStatic
  IReadOnlyList<Parameter> Parameters
  TypeReference? ReturnType         (null on Sub / PropertyLet / PropertySet)
  IReadOnlyList<Statement> Body
```

`PropertyProcedure` adds `PropertyKind Kind` (`Get | Let | Set`).

### Parameter

```
Parameter : VbaSyntaxNode
  string Name
  string? TypeSuffix               ('$', '%', '&', '!', '#', '@' from TYPED_IDENTIFIER, else null)
  ParameterPassMode PassMode       (ByRef | ByVal | Unspecified — defaults to ByRef per §4.6 but the AST records the spelling)
  ParameterModifier Modifier       (Required | Optional | ParamArray)
  TypeReference? Type              (null = Variant by §4.5)
  bool IsArray                     (true if `()` follows the name)
  Expression? DefaultValue         (only on Optional parameters)
```

### Statement

`Statement` is abstract. Variants (matching grammar's 22 statement
alternatives with the noted collapses):

```
Statement : VbaSyntaxNode (abstract)

  ── Control flow ──────────────────────────────────────────
  IfStatement
    Expression Condition
    IReadOnlyList<Statement> ThenBody
    IReadOnlyList<ElseIfClause> ElseIfClauses
    IReadOnlyList<Statement>? ElseBody         (null = no Else; empty = Else with empty body)
    bool IsSingleLine                          (block form vs single-line form)

  ElseIfClause : VbaSyntaxNode
    Expression Condition
    IReadOnlyList<Statement> Body

  ForStatement                                  (For ... To ... Step ... Next)
    string LoopVariable
    string? LoopVariableTypeSuffix              (suffix from TYPED_IDENTIFIER, else null)
    Expression From
    Expression To
    Expression? Step
    IReadOnlyList<Statement> Body

  ForEachStatement                              (For Each ... In ... Next)
    string LoopVariable
    string? LoopVariableTypeSuffix              (suffix from TYPED_IDENTIFIER, else null)
    Expression Collection
    IReadOnlyList<Statement> Body

  DoLoopStatement                               (all four Do/Loop variants)
    DoLoopKind Kind                              (PreTestWhile | PreTestUntil | PostTestWhile | PostTestUntil | Infinite)
    Expression? Condition                        (null only on Infinite)
    IReadOnlyList<Statement> Body

  WhileWendStatement                            (legacy While ... Wend)
    Expression Condition
    IReadOnlyList<Statement> Body

  SelectCaseStatement
    Expression Selector
    IReadOnlyList<CaseClause> Cases
    IReadOnlyList<Statement>? CaseElseBody

  CaseClause : VbaSyntaxNode
    IReadOnlyList<CaseExpression> Expressions
    IReadOnlyList<Statement> Body

  CaseExpression : VbaSyntaxNode (abstract)
    CaseValueExpression                          (single expression)
      Expression Value
    CaseRangeExpression                          (Lo To Hi)
      Expression Lo
      Expression Hi
    CaseIsCompareExpression                      (Is < expr, Is = expr, etc.)
      ComparisonOperator Operator
      Expression Value

  WithStatement
    Expression Target
    IReadOnlyList<Statement> Body

  ── Error handling ────────────────────────────────────────
  OnErrorStatement
    OnErrorKind Kind                             (Goto | ResumeNext)
    string? Label                                (only on Goto; "0" sentinel allowed)

  ResumeStatement
    ResumeKind Kind                              (Bare | Next | Label)
    string? Label                                (only on Label)

  ── Control transfer ──────────────────────────────────────
  GotoStatement      { string Label }
  GosubStatement     { string Label }
  ReturnStatement    {}                          (legacy GoSub return — distinct from Function return value)
  ExitStatement      { ExitKind Kind }           (Sub | Function | Property | For | Do)
  StopStatement      {}
  EndStatement       {}                          (program-terminating End — distinct from End Sub etc.)

  ── Events ────────────────────────────────────────────────
  RaiseEventStatement
    string EventName
    IReadOnlyList<Argument> Arguments

  ── Local declarations ────────────────────────────────────
  LocalDeclarationStatement                       (Dim / Static)
    LocalDeclarationKind Kind                    (Dim | Static)
    IReadOnlyList<VariableDeclarator> Declarators

  RedimStatement
    bool Preserve
    IReadOnlyList<RedimDeclarator> Declarators

  LocalConstantStatement
    IReadOnlyList<ConstantDeclarator> Declarators

  ── Assignment / call ─────────────────────────────────────
  SetStatement                                    (Set <l-expr> = <expr>)
    Expression Target
    Expression Value

  LetStatement                                    (implicit or explicit Let)
    Expression Target
    Expression Value
    bool ExplicitLet                              (true if "Let" keyword present)

  CallStatement
    Expression Target                             (always an l-expression)
    IReadOnlyList<Argument> BareArguments         (empty if Target's IndexOrCall postfix carries the args)
    bool ExplicitCall                             (true if "Call" keyword present)

  ── Labels (statement-start position) ─────────────────────
  LabelStatement
    string Name                                   (alpha label or numeric line number, source spelling preserved)

  ── Recovery sentinel ─────────────────────────────────────
  UnknownStatement                                (subtree contained IErrorNode the builder could not interpret)
```

`VariableDeclarator`, `RedimDeclarator`, `ConstantDeclarator` are
sub-structures (non-`Statement` `VbaSyntaxNode`) representing one
declared item within a multi-decl statement. They carry name, type,
array-bounds (where applicable), and (for constants) the value
expression.

### Expression

`Expression` is abstract. Variants (collapsed per rule §6):

```
Expression : VbaSyntaxNode (abstract)

  ── Literals & special constants ──────────────────────────
  LiteralExpression
    LiteralKind Kind                              (Integer | Float | String | Date | Hex | Octal)
    string Text                                   (source spelling; canonical form is downstream)

  TrueExpression       {}
  FalseExpression      {}
  NothingExpression    {}
  NullExpression       {}
  EmptyExpression      {}
  MeExpression         {}
  DebugExpression      {}                          (the `Debug` object — "Debug.Print" etc.)

  ── Identifiers ───────────────────────────────────────────
  IdentifierExpression
    string Name
    string? TypeSuffix                            ('$', '%', '&', '!', '#', '@'; null when no TYPED_IDENTIFIER)

  ── Composition ──────────────────────────────────────────
  ParenthesizedExpression
    Expression Inner

  MemberAccessExpression                          (a.b)
    Expression Target
    string MemberName

  BangAccessExpression                            (a!b)
    Expression Target
    string MemberName

  ImplicitWithMemberExpression                    (.b — implicit With target)
    string MemberName

  ImplicitWithBangExpression                      (!b — implicit With target)
    string MemberName

  IndexOrCallExpression                           (f(args) — array-index/call/default-member ambiguous; resolver disambiguates)
    Expression Target
    IReadOnlyList<Argument> Arguments

  ── Operators ────────────────────────────────────────────
  BinaryExpression
    BinaryOperator Operator
    Expression Left
    Expression Right

  UnaryExpression
    UnaryOperator Operator
    Expression Operand

  ── Object-creation & introspection ──────────────────────
  NewExpression
    TypeReference Type

  AddressOfExpression
    Expression ProcedureReference                 (always a member-access chain or bare identifier)

  TypeOfExpression
    Expression Operand
    TypeReference Type

  ── Recovery sentinel ─────────────────────────────────────
  UnknownExpression                                (subtree contained IErrorNode the builder could not interpret)
```

`BinaryOperator` collapses the grammar's per-precedence rules:
`Power | Multiply | Divide | IntDivide | Modulo | Add | Subtract |
Concat | Equal | NotEqual | Less | Greater | LessOrEqual |
GreaterOrEqual | Like | Is | And | Or | Xor | Eqv | Imp`.
`UnaryOperator`: `Plus | Minus | Not`.

### Argument

```
Argument : VbaSyntaxNode (abstract)
  PositionalArgument  { Expression Value }
  NamedArgument       { string Name; Expression Value }
  MissingArgument     {}                          (empty positional slot — preserves H-D08)
```

### Type reference

`TypeReference` is the AST-layer wrapper around the grammar's
`typeName` / `typeSpec` / `variableTypeSpec` rules:

```
TypeReference : VbaSyntaxNode
  IReadOnlyList<string> QualifiedName             ("DAO", "Recordset")  →  DAO.Recordset
  bool IsAutoInstantiated                         (true on `New T` form)
  Expression? FixedStringSize                     (only on `String * <const-expr>`)
  bool IsArray                                    (true on array-element-type form, e.g., parameter `x() As Long`)
```

`QualifiedName` is `["Variant"]` when the source omits the `As` clause
entirely (caller supplies `Variant`), `["String"]` for `String`,
`["DAO", "Recordset"]` for `DAO.Recordset`, etc. The single-string
form is the dominant case; `IReadOnlyList<string>` accommodates
qualified types without a separate `QualifiedTypeReference` variant.

### Preprocessor directive (parsed, not evaluated)

Per Phase 2.5: directives are preserved as opaque blocks, not
evaluated. Session 3 owns the body schema; Session 2 reserves the
node type:

```
PreprocessorDirective : ModuleItem
  PreprocessorKind Kind                            (If | ElseIf | Else | EndIf | Const)
  string Body                                      (raw text after the directive head, line-trimmed)
```

The `Body` field is the literal source between the directive head and
the line terminator. Session 3 may decompose it (parsing the
condition expression for `#If`/`#ElseIf`, the name+value for
`#Const`) without changing the existing field.

## 5. Visitor infrastructure

Two visitor abstractions, per the C# AST community's converged design
(closer to Roslyn's `CSharpSyntaxVisitor`/`CSharpSyntaxRewriter` than
to per-node `Accept` double-dispatch):

```csharp
// Walks the tree; each VisitX returns void. Default implementations
// recurse into children. Override the methods you care about; the
// rest fall through to base-class child traversal.
public abstract class VbaSyntaxWalker
{
    public virtual void Visit(VbaSyntaxNode? node) { /* dispatches via switch */ }
    public virtual void DefaultVisit(VbaSyntaxNode node) { /* visits children */ }

    public virtual void VisitModule(Module node)                         => DefaultVisit(node);
    public virtual void VisitSubProcedure(SubProcedure node)             => DefaultVisit(node);
    public virtual void VisitFunctionProcedure(FunctionProcedure node)   => DefaultVisit(node);
    public virtual void VisitPropertyProcedure(PropertyProcedure node)   => DefaultVisit(node);
    public virtual void VisitIfStatement(IfStatement node)               => DefaultVisit(node);
    // ... one VisitX per concrete node type
}

// Same shape but each VisitX returns TResult. DefaultVisit returns
// default(TResult) and does not recurse — TResult-returning visitors
// are typically aggregating, so a no-op default is safer than walking
// children whose results would be discarded.
public abstract class VbaSyntaxVisitor<TResult>
{
    public virtual TResult? Visit(VbaSyntaxNode? node) { /* dispatches via switch */ }
    public virtual TResult? DefaultVisit(VbaSyntaxNode node) => default;
    // ... one VisitX per concrete node type
}
```

Dispatch is via a single C# `switch` expression in `Visit(node)`. Adding
a new node type requires (a) a new class, (b) a new case in the
switch, (c) a virtual `VisitX` on each base. The compiler does not
warn on missing switch arms, so the test suite includes a reflective
"every concrete node type has a switch arm" check (lives in the
parser-test project).

A `VbaSyntaxRewriter : VbaSyntaxVisitor<VbaSyntaxNode?>` is **not**
shipped in this session. The resolver session sets will need
node-substitution passes, but it is not yet clear whether they want a
generic walking-rewriter (every node type has a rebuild override) or
targeted rewrites (a `VbaSyntaxVisitor<VbaSyntaxNode>` subclass that
overrides only the few node types it cares about). Building a generic
rewriter for all 50+ node types now would be ~200 lines of mechanical
code with no current consumer. Lean by default — defer to the first
resolver session that actually needs it.

## 6. AstBuilder — parse tree → AST

`AstBuilder` is a (non-public, internal) ANTLR `VbaBaseVisitor<T>`
subclass that walks the parse tree and produces AST nodes. The public
surface is one extension method on `ParseResult`:

```csharp
public static class ParseResultAstExtensions
{
    public static Module BuildAst(this ParseResult result);
}
```

`BuildAst` is a pure function: same `ParseResult` always produces the
same `Module` (modulo `Provenance` line/column from the parse tree,
which is itself deterministic). It does **not** throw on
`ParseResult.ModeUsed == Lossy`; the recovered nodes are tagged via
`RecoveredProvenance` and surfaced in the tree.

`AstBuilder` cross-references `ParseResult.Diagnostics` to populate
`RecoveredProvenance.Reason` per the §7 detection rule. The diagnostic
nearest in source position to the recovered subtree wins; when no
diagnostic precedes the subtree, the reason is `"unknown-recovery"`.

The builder's failure mode for **un-parseable subtrees** (an
`IErrorNode` it cannot map to any AST shape):

- A statement-position error subtree → `UnknownStatement` with
  `RecoveredProvenance`.
- An expression-position error subtree → `UnknownExpression` with
  `RecoveredProvenance`.
- A module-item-position error subtree → swallowed (logged as a
  `RecoveredProvenance`-tagged `ModuleItem` would force a sentinel
  category that does not earn its keep; modules with such damage
  should already have a diagnostic and the consumer should consult
  `ParseResult.Diagnostics` separately).

## 7. What this session lands

- New folder `AccessHarvester.Vba.Parser/Ast/` containing:
  - `Provenance.cs` — Provenance, SourceProvenance, RecoveredProvenance.
  - `VbaSyntaxNode.cs` — root abstract record.
  - `Module.cs` — Module + ModuleItem hierarchy (10 concrete + 1
    abstract).
  - `Procedure.cs` — ProcedureDeclaration + 3 concrete +
    AccessModifier / PropertyKind enums.
  - `Parameter.cs` — Parameter + ParameterPassMode /
    ParameterModifier enums.
  - `Statement.cs` — Statement + 22 concrete + UnknownStatement +
    sub-structures (ElseIfClause, CaseClause, CaseExpression and 3
    variants, VariableDeclarator, RedimDeclarator,
    ConstantDeclarator) + supporting enums.
  - `Expression.cs` — Expression + ~25 concrete + UnknownExpression +
    BinaryOperator / UnaryOperator / LiteralKind enums.
  - `Argument.cs` — Argument + 3 variants.
  - `TypeReference.cs` — TypeReference.
  - `PreprocessorKind.cs` — enum (the directive node lives in
    `Module.cs` since it is a ModuleItem).
  - `VbaSyntaxWalker.cs` — void visitor.
  - `VbaSyntaxVisitor.cs` — generic-return visitor.
  - `AstChildren.cs` — yields the immediate VbaSyntaxNode children of
    a node (used by the walker's DefaultVisit).
  - `AstBuilder.cs` — internal ANTLR-visitor → AST builder.
  - `ParseResultAstExtensions.cs` — public `BuildAst` extension.
- New tests in `tests/AccessHarvester.Vba.Parser.Tests/Ast/`:
  - `AstBuilderShapeTests.cs` — builds AST from synthetic VBA
    snippets covering every concrete statement and expression
    variant; asserts shape, types, provenance kind.
  - `AstBuilderRecoveryTests.cs` — covers the
    `Recovered` provenance tagging for `UnknownStatement` and
    `UnknownExpression` produced from malformed input.
  - `VbaSyntaxWalkerTests.cs` — confirms the dispatch switch reaches
    every concrete node type (reflective coverage check).

## 8. What this session does NOT land

- `SourceLocation` / `SourceRange` types and end-position tracking
  (Session 3). Provenance carries `Line` / `Column` ints only.
- Preprocessor directive **body parsing** — Session 3 owns the
  fixture (`13-preprocessor.bas`) and the body schema. Session 2
  defines `PreprocessorDirective` with a single `Body` string; the
  body's interpretation lives in Session 3.
- Schema validation against `harvester-schema.v0.json` (Session 4).
- Symbol resolution / scope tree / declaration model (downstream
  resolver session sets — `resolver-declarations-and-scopes` #7
  onwards).
- The generic `VbaSyntaxRewriter` base class — deferred to the first
  resolver session that needs it (lean by default; see §5).

## 9. Forward contracts handed to later sessions

- **Session 3 (Position tracking)**: introduces `SourceLocation` and
  `SourceRange`. Expected migration: add a `SourceRange Range` field
  to `Provenance` (additive); keep `Line`/`Column` as forwarding
  properties for one transitional session, then remove. The
  `Source` vs `Recovered` discriminator is stable.
- **Session 3 (Preprocessor)**: extends `PreprocessorDirective` with
  a structured body (likely `Expression? Condition` for `#If`/
  `#ElseIf`, `string Name` + `Expression Value` for `#Const`). Adds
  but does not remove the `Body` raw-text field.
- **Session 4 (Cross-cut)**: runs `BuildAst` on every fixture in the
  corpus and asserts the result builds without throwing. Decides
  based on actual coverage whether any AST variant is over- or
  under-modeled and proposes adjustments.
- **Resolver sessions (#7+)**: when a node-substitution pass is
  needed, decide between (a) a generic `VbaSyntaxRewriter` base class
  (every node type has a rebuild override) or (b) a targeted
  `VbaSyntaxVisitor<VbaSyntaxNode>` subclass that overrides only the
  few nodes that matter and falls through `DefaultVisit` returning
  the input. Either is fine; pick based on the substitution surface.
  The "no parent pointers" rule means scope-aware rewrites must
  thread scope state through the visit manually (a `Stack<Scope>`
  field on the rewriter is the expected pattern).

## 10. Determinism and equality

All AST nodes are records with value-based equality. Two ASTs built
from the same input are `Equals`. This is the contract that lets the
fixture-comparison harness (Session 4) use straightforward
record equality rather than a hand-written deep-equality walker.

Provenance's line/column values DO participate in equality (they are
record fields). Two ASTs built from identical source text but at
different file positions are NOT equal — which is the right call,
because location IS part of the AST's meaning for downstream tooling.
The fixture-comparison harness normalizes locations before
comparison if location-independent equivalence is needed.
