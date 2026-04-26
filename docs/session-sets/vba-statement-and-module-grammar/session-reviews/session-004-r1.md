# VERDICT: ISSUES_FOUND

## 1. Completeness

- The grammar covers the stated Phase 1.4 subset: module structure, procedure declarations, parameter lists, Type/Enum/Declare/Event/Implements/Option/module vars, core 5.x control-flow/error/declaration/assignment/call statements, and §6 preprocessor pass-through.
- No obvious production is missing inside that declared subset.
- The main problems are **over-acceptance**, not missing coverage.

## 2. Design decisions

- **a. Property accessor split** → Correct.
- **b. Parameter positional invariants** → Correct.
- **c. WithEvents tightening** → Correct at grammar level for the stated goal (`AS typeName`, no `New`, no array, no fixed-string).
- **d. Event parameter restriction** → Correct.
- **e. `preprocessorLine` in both `moduleItem` and `statementListBody`** → Correct and necessary.

## 3. Findings

### Issue → Procedure-body declarations accept module-only modifiers
**Location** → `localDeclStatement`, `declHead`, `constStatement` when used from `statement`

**Fix** →
- Restrict procedure-local variable declarations to `DIM | STATIC`.
- Split local and module const forms.

```antlr
localDeclStatement
    : (DIM | STATIC) variableDecl (COMMA variableDecl)*
    ;

localConstStatement
    : CONST constDecl (COMMA constDecl)*
    ;

moduleConstStatement
    : (PUBLIC | PRIVATE)? CONST constDecl (COMMA constDecl)*
    ;
```

Then use `localConstStatement` in `statement`, not the module-capable form.

---

### Issue → `Do ... Loop` accepts illegal dual-condition forms
**Location** → `doLoopStatement`

Current rule accepts invalid input like:

```vb
Do While x
    ...
Loop Until y
```

**Fix** → Replace the optional-at-both-ends shape with mutually exclusive alternatives.

```antlr
doLoopStatement
    : DO NEWLINE
        statementListBody
      LOOP
    | DO doLoopCondition NEWLINE
        statementListBody
      LOOP
    | DO NEWLINE
        statementListBody
      LOOP doLoopCondition
    ;
```

---

### Issue → `typeSpec` over-accepts `As New` in non-variable contexts and `New ... * expr`
**Location** → `typeSpec` and all uses in parameters, returns, events, declares, type members

Problems accepted today:
- `Sub S(x As New Foo)`
- `Function F() As New Foo`
- `Dim x As New String * 10`

**Fix** →
- Split variable declaration type syntax from general type syntax.
- Allow `NEW` only in variable declaration contexts.
- Structurally forbid `NEW` together with fixed-length-string syntax.

Example shape:

```antlr
typeSpec
    : typeName (STAR expression)?
    ;

variableTypeSpec
    : NEW typeName
    | typeSpec
    ;
```

Then use `variableTypeSpec` only in variable declarations / `ReDim` where intended.

---

### Issue → `Option` directives over-accept arbitrary identifiers/values
**Location** → `optionStatement`

Currently accepted examples that should be rejected:
- `Option Foo 3`
- `Option Private Foo`
- `Option Compare Foo`

**Fix** →
- Enforce the contextual keywords specifically (`Base`, `Module`, `Binary`, `Text`, `Database`) and restrict `Option Base` to `0 | 1`.
- Implement via dedicated lexer tokens or parser predicates on `IDENTIFIER` text.

## 4. Session 4 closing condition

- **For session bookkeeping:** yes, “no deferred issues + all tests pass” is adequate.
- **For final contract verification of the session set:** no, not by itself, because the grammar still has the over-acceptance issues above.