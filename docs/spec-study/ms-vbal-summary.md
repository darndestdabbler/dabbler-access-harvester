# MS-VBAL Summary — VBA Language Specification Study for Parser Design

> **Purpose:** Topic-organized prose summary of MS-VBAL (Microsoft Visual Basic for Applications Language Specification) for the `vba-spec-study` session set. Each entry cites the MS-VBAL section, notes syntactic complexity, semantic subtlety, and frequency in Access codebases. Drives coverage matrix (`ms-vbal-coverage.csv`) and hazard list (`hazard-list.md`).
> **Source:** MS-VBAL specification (Microsoft Open Specifications, VBA Language Specification v5.3). Clean-room — no derivation from Rubberduck or other GPL sources.
> **Produced:** Session 1 of `vba-spec-study` (2026-04-25), Sonnet orchestrator + Opus synthesis (3 focused route(architecture) calls).
> **Session set:** `docs/session-sets/vba-spec-study/`

> **Deviation note:** The spec called for 1 architecture synthesis call per session. Due to the Opus timeout lesson (Opus at effort:max + adaptive thinking times out above ~3K input tokens; see `docs/planning/lessons-learned.md`), this was split into 3 focused calls covering: (A) Lexical + Preprocessing, (B) Modules + Procedures + Type System, (C) Statements + Expressions + Object Model. All three used Opus as generator. Content is equivalent to a single synthesis call broken by section.

## 14 Critical Constructs (Quick Reference)

The following constructs receive extended treatment in the sections below. Each is flagged **CRITICAL** at its main entry.

| # | Construct | MS-VBAL Section | Section in this doc |
|---|---|---|---|
| 1 | Bang operator (!) | §5.2.2 | Section C |
| 2 | Default property access | §5.2.3 | Section C |
| 3 | Me keyword | §5.6 | Section C |
| 4 | Set vs Let assignment | §5.3.1–5.3.2 | Section C |
| 5 | Line continuation (_) | §3.3.3 | Section A |
| 6 | Line numbers | §3.3.2 | Section A |
| 7 | Type-declaration characters (%&!#@$) | §3.3.5 | Section A |
| 8 | Date literals (#…#) | §3.4 | Section A |
| 9 | ByRef parameter passing | §4.6 | Section B |
| 10 | Optional / ParamArray / named args | §4.6 | Section B |
| 11 | Property Get/Let/Set triplets | §4.5.3 | Section B |
| 12 | Events + WithEvents | §6.1–6.2 | Section C |
| 13 | Implements | §6.3 | Section C |
| 14 | On Error + Resume | §5.4.8–5.4.12 | Section C |
| 15 | GoTo / GoSub / Return | §5.4.9–5.4.10 | Section C |

> Note: The plan's "14 critical constructs" list includes GoTo/GoSub as a combined item with On Error/Resume (items 14 and 15 above). Both are distinct enough to warrant separate entries.

---

# Section A: Lexical Structure and Preprocessing

---

## 1. White Space and Comments (§3.3.1)

VBA defines white space as sequences of space (U+0020) and horizontal tab (U+0009) characters. White space is required between adjacent alphanumeric tokens but is otherwise optional around operators and separators.

Comments take two forms: the apostrophe character `'` and the keyword `REM`. Both cause the lexer to discard all remaining characters on the logical line (after line-continuation resolution). A critical distinction: `REM` is a keyword-form comment and must appear where a statement could begin. The apostrophe form can appear after a statement on the same line, separated by `:` or whitespace. The grammar is roughly:

```
comment-body = apostrophe *non-line-termination-character
             / REM *non-line-termination-character
```

**Syntactic complexity:** Low. The only subtlety is that `REM` without a following space can be ambiguous against identifiers beginning with `Rem` (e.g., `Remediate = 1`). The spec resolves this: `REM` is only a comment keyword when followed by whitespace or end-of-line. A parser must check the next character.

**Semantic subtlety:** The apostrophe inside a string literal (`"That's fine"`) is *not* a comment marker — the lexer must track string-literal state before checking for comments.

**Frequency in Access:** Very high. Nearly every procedure contains comments.

**Interactions:** Line continuation (§3.3.3) is resolved before comment recognition, so a comment cannot be continued with `_`. A trailing `_` inside a comment is literal text, not a continuation token. However, there is an edge case: `' comment _` at end of line — the spec says line continuation is processed first, so this *does* continue the comment to the next physical line, effectively commenting it out. The parser must resolve continuation before comment stripping.

---

## 2. Line Continuation (§3.3.3)

A line-continuation sequence is defined as `<WS> <underscore> <line-terminator>`, where the underscore is `_` (U+005F). This merges the next physical line into the current logical line. The grammar:

```
line-continuation = WS underscore line-terminator
```

**Key restrictions:**

1. The `_` must be preceded by at least one white-space character. An underscore at the end of an identifier (e.g., `my_var_`) is part of the identifier, not a continuation.
2. A continuation cannot split a token. You cannot break a keyword (`Di_ \n m`) or a string literal mid-content.
3. A continuation cannot appear inside a string literal — the `_` is literal string content there.
4. Multiple continuations are permitted in sequence, producing logical lines spanning many physical lines.
5. The spec limits logical lines to a maximum of 25 physical lines (some implementations relax this).

**Syntactic complexity:** Medium. The difficulty is that continuation must be resolved very early — effectively as a pre-lexing pass that joins physical lines into logical lines before tokenization. The parser must distinguish `my_var_` (identifier ending in underscore, no trailing whitespace before newline) from `Call MyMethod _` (continuation). The decision hinges on whether the underscore is preceded by whitespace *and* immediately followed by a line terminator.

**Frequency in Access:** High. Access VBA routinely contains long SQL strings built with concatenation across multiple lines, and long method calls on DAO/ADO objects with many parameters. Example:

```vba
strSQL = "SELECT * FROM Customers " & _
         "WHERE Region = 'West' " & _
         "ORDER BY LastName"
```

**Interactions:** Must be resolved before comment processing (§3.3.1), before tokenization, and before conditional compilation (§6). A continuation inside a `#If` block's excluded region still joins lines — the preprocessor operates on logical lines.

---

## 3. Line Numbers (§3.3.2)

A line number is an integer literal appearing as the first token of a logical line. It functions as a label and can serve as a target for `GoTo`, `GoSub`, `On Error GoTo`, and `Resume`. The grammar:

```
statement-label = line-number / label-name ":"
line-number = 1*digit  ; integer value 0–65535 in practice
```

Line numbers are not required to be sequential or contiguous. They are scoped to the containing procedure. A line number is distinct from a named label (`ErrorHandler:`); both serve as branch targets but line numbers lack the trailing colon.

**Syntactic complexity:** Low. The parser must recognize an integer at column 1 of a logical line as a label rather than as the start of an expression or assignment. Ambiguity is minimal because VBA statements cannot begin with a bare integer literal in any other context (an assignment target must be an l-value identifier). However, the lexer must avoid confusing a line number with a numeric literal that begins an expression — the positional rule (first token of the line) is the discriminator.

**Semantic subtlety:** Line numbers create implicit labels in the procedure's label namespace. Duplicate line numbers within a procedure are a compile-time error. The `ERL` function returns the most recently executed line number, which is used in some Access error-logging patterns — so line numbers have runtime semantics beyond just branching.

**Frequency in Access:** Low overall, but non-negligible. Legacy Access databases converted from Access 2.0/95 era may have line-numbered procedures. Some Access error-handling frameworks (e.g., FMS or MZ-Tools patterns) auto-insert line numbers for `ERL`-based error logging. The parser must handle them correctly even though they appear in a minority of modules.

**Interactions:** Line numbers interact with `GoTo` / `GoSub` / `On Error GoTo` (§5.4.2) target resolution. They also interact with the `:` separator — `10 Print "Hello" : 20 Print "World"` places two numbered statements on one logical line.

---

## 4. Identifiers (§3.3.5)

An identifier names a variable, procedure, module, type, enum member, or other declared entity. The grammar:

```
IDENTIFIER          = untyped-name / typed-name
untyped-name        = IDENTIFIER-string
typed-name          = IDENTIFIER-string type-declaration-character
IDENTIFIER-string   = letter *(letter / digit / underscore)
                    / "[" id-text "]"
id-text             = *(non-bracket-character)
```

**Rules:**
- Must begin with a letter (A–Z, a–z). Subsequent characters may be letters, digits, or underscores.
- Maximum length is 255 characters.
- **Case-insensitive**: `myVar`, `MYVAR`, and `MyVar` are the same identifier. The parser/symbol table must normalize case for comparison.
- **Bracket escaping**: `[Select]`, `[Date]`, `[End]` allow reserved words to be used as identifiers. The brackets are delimiters, not part of the name. Bracket-escaped identifiers cannot contain `]` or line terminators. This is extremely common in Access because table/query field names frequently collide with VBA reserved words (e.g., `[Name]`, `[Date]`, `[Type]`).
- An identifier cannot be identical (case-insensitively) to a reserved word unless bracket-escaped.

**Syntactic complexity:** Medium. The core rules are simple, but the parser must:
1. Distinguish bracket-escaped identifiers from array indexing — `[Name]` at the start of an expression is an identifier, while `arr(0)` uses parentheses. Brackets `[` in VBA are always identifier delimiters, never used for indexing.
2. Recognize that a type-declaration suffix (§3.3.5.1) is part of the identifier token: `count%` is a single token, not `count` followed by `%`.
3. Handle the context-sensitivity of non-reserved keywords (§3.3.5.3) — see below.

**Semantic subtlety (Access-specific):** Access extensively uses bracketed identifiers in expressions passed to the Access expression service (e.g., `Forms![MainForm]![Subform].Form![FieldName]`). The `!` (bang) operator followed by a bracketed name is a dominant Access pattern. The parser must handle `!` both as the `Single` type suffix and as the dictionary-access/default-member operator based on context.

**Frequency in Access:** Ubiquitous. Every line of code contains identifiers.

---

## 5. Keywords — Reserved and Non-Reserved (§3.3.5.2–§3.3.5.3)

The spec partitions keywords into **reserved identifiers** (§3.3.5.2) and **non-reserved identifiers** (§3.3.5.3).

**Reserved identifiers** cannot be used as programmer-defined names (without bracket-escaping). The list includes approximately 150+ words: `Dim`, `Sub`, `Function`, `If`, `Then`, `Else`, `End`, `For`, `Next`, `Do`, `While`, `Loop`, `Select`, `Case`, `With`, `Set`, `Let`, `GoTo`, `GoSub`, `Return`, `Exit`, `Type`, `Enum`, `Public`, `Private`, `Static`, `Const`, `As`, `New`, `ByVal`, `ByRef`, `Optional`, `ParamArray`, `Nothing`, `Null`, `Empty`, `True`, `False`, `And`, `Or`, `Not`, `Xor`, `Eqv`, `Imp`, `Mod`, `Like`, `Is`, `Me`, `Debug`, `Print`, etc.

**Non-reserved identifiers** are keywords that are only recognized as keywords in specific syntactic contexts and can otherwise be used as variable names. These include: `Access`, `Error`, `Step`, `Binary`, `Base`, `Compare`, `Database`, `Text`, `Len`, `Mid`, `Explicit`, `Append`, `Output`, `Random`, `Read`, `Write`, `Shared`, `Lock`, `Reset`, `Tab`, `Spc`, `PtrSafe`, etc.

**Syntactic complexity:** High. This is one of the hardest aspects of VBA parsing. The lexer cannot unilaterally decide whether `Error` is a keyword (as in `On Error GoTo`) or an identifier (as in `Dim Error As String`). Similarly, `Mid` is both a keyword (in `Mid$(str, 1, 3) = "abc"` — the `Mid` *statement*) and a function name. The parser must either:
- Use a context-sensitive lexer that consults the parser state, or
- Lex all non-reserved words as identifiers and let the parser grammar disambiguate, or
- Use a keyword-in-context table driven by the current grammatical position.

The practical recommendation: lex everything as IDENTIFIER, then check a reserved-word table only when the grammar expects a keyword. For non-reserved words, let the parser production rules disambiguate.

**Semantic subtlety:** `Date`, `Time`, and `Error` are both built-in functions and potential identifiers. In Access, `Name` is heavily used as a field name but is also a reserved keyword (the `Name` statement renames files). This forces Access developers to bracket-escape frequently.

**Frequency in Access:** Every statement involves keywords. The ambiguity cases (non-reserved keywords used as identifiers) appear moderately often, especially `Error`, `Name`, `Type`, and `Step`.

**Interactions:** Interacts with bracket-escaping (§3.3.5), the `!` bang operator, and the `#` date-literal delimiter (which is also the `Double` type suffix).

---

## 6. Operators and Separators (§3.3 et al.)

VBA defines these operator and separator tokens:

| Category | Tokens | Notes |
|---|---|---|
| Arithmetic | `+`, `-`, `*`, `/`, `\` (integer div), `^` (exponent), `Mod` | `Mod` is a keyword-operator |
| Comparison | `=`, `<>`, `<`, `>`, `<=`, `>=`, `Like`, `Is` | `Like` and `Is` are keyword-operators |
| Logical/Bitwise | `And`, `Or`, `Not`, `Xor`, `Eqv`, `Imp` | All keyword-operators; bitwise on integers, logical on booleans |
| String | `&` (concatenation) | Also the `Long` type suffix — see below |
| Assignment | `=` | Same glyph as comparison `=` |
| Separator | `:` (statement separator), `,` (list separator), `;` (print separator), `.` (member access), `!` (bang/dictionary access) | |
| Grouping | `(`, `)` | Calls, grouping, array indexing — all parens |

**Syntactic complexity:** Medium-high. Key ambiguities:

1. **`=` is both assignment and comparison.** Context resolves: `=` after `Let`/implicit-`Let`/`Set` keyword or at statement level is assignment; within an expression it is comparison. The parser must track whether it is at statement level or inside an expression.

2. **`&` is both string concatenation and the `Long` type suffix and hex-literal prefix (`&H`).** The lexer must check what follows: `&H` starts a hex literal, `&O` starts an octal literal, `&` followed by a letter that is a valid identifier start is concatenation, `&` appended directly to an identifier with no space is a type suffix.

3. **`#` is both the `Double` type suffix and the date-literal delimiter.** `x#` is a typed identifier; `#1/1/2024#` is a date literal. The lexer must check: is `#` preceded by an identifier character (suffix) or appearing at expression start (date literal)?

4. **`!` is both the `Single` type suffix and the bang/dictionary-access operator.** `x!` after an identifier with no space is a type suffix; `rs!FieldName` or `Forms!FormName` is dictionary access. In practice: if `!` follows a complete expression and precedes an identifier, it is bang access.

5. **`-` is both subtraction and unary negation.** Standard precedence/associativity rules apply.

**Frequency in Access:** Ubiquitous. The `!` bang operator and `&` concatenation are especially prevalent in Access code.

---

## 7. Type-Declaration Characters (§3.3.5.1)

Six suffix characters can be appended to an identifier to declare its type:

| Suffix | Type | Example |
|---|---|---|
| `%` | Integer | `count%` |
| `&` | Long | `total&` |
| `!` | Single | `rate!` |
| `#` | Double | `price#` |
| `@` | Currency | `amount@` |
| `$` | String | `name$` |

The suffix is lexically part of the identifier token. `count%` and `count` are **different identifiers** — they can coexist in the same scope referring to different variables. The suffix simultaneously declares the variable's type (if no explicit `As` clause) and distinguishes the name.

The same suffixes appear on literal values: `100&` is a `Long` literal, `3.14!` is a `Single` literal. On a literal, the suffix determines the literal's type but is not part of a name.

**Syntactic complexity:** Medium. The lexer must greedily attach a suffix character to the preceding identifier or numeric literal when no whitespace intervenes. This creates ambiguity with operators:
- `x&y` — is this `x& y` (identifier `x&` followed by identifier `y`, missing operator) or `x & y` (concatenation)? The spec resolves: whitespace or context determines. In practice, `x&y` without spaces is a syntax error or is read as `x&` followed by `y` (which is also a syntax error without an operator). Idiomatic VBA uses spaces around `&`.
- `x#` — typed identifier vs. start of a date literal is resolved by position (see §6 on operators above).

**Semantic subtlety:** In Access, the `$` suffix is frequently seen on built-in string-returning functions: `Left$()`, `Mid$()`, `Right$()`, `Trim$()`, `Format$()`. These are distinct from their Variant-returning counterparts (`Left()`, `Mid()`, etc.). The `$` is part of the function name, not a type annotation on the return value. The parser must recognize these as identifier tokens with `$` suffix.

**Frequency in Access:** Medium. Older Access modules (pre-Access 2000) use type suffixes heavily. Modern Access VBA prefers explicit `As` declarations. But built-in `$` functions remain very common.

**Interactions:** Directly interacts with identifier recognition (§3.3.5), operator disambiguation (§3.3), and literal typing (§3.4).

---

## 8. Literals (§3.4)

### String Literals (§3.4.1)

Delimited by double-quote characters (`"`). An embedded double-quote is escaped by doubling: `""`. The grammar:

```
string-literal = DQUOTE *string-character DQUOTE
string-character = non-quote-character / DQUOTE DQUOTE
```

**Syntactic complexity:** Low. Standard double-quote-delimited string with doubled-quote escaping. No backslash escapes, no interpolation, no multi-line strings (a string literal cannot span a line terminator — line continuation must be used with concatenation instead).

**Access note:** Access VBA frequently embeds SQL with single quotes inside double-quoted strings: `"WHERE Name = 'Smith'"`. No special parsing challenge here — single quotes are ordinary characters within VBA string literals.

**Frequency:** Very high.

### Boolean Literals

`True` and `False` are reserved keywords that evaluate to `Boolean` values (`True` = -1, `False` = 0 when coerced to Integer).

**Syntactic complexity:** Low — just keyword recognition.

**Frequency:** High.

### Integer Literals (§3.4.2)

Three forms:

```
integer-literal  = decimal-literal [integer-type-suffix]
                 / hex-literal [integer-type-suffix]
                 / octal-literal [integer-type-suffix]
decimal-literal  = 1*digit
hex-literal      = "&H" 1*hex-digit "&"?
octal-literal    = "&O" 1*octal-digit "&"?
integer-type-suffix = "%" / "&" / "^"
```

The `&H` and `&O` prefixes are case-insensitive. A trailing `&` on hex/octal forces `Long` type. Examples: `&HFF` (Integer 255), `&HFF&` (Long 255), `&O77` (Integer 63).

**Syntactic complexity:** Medium. The `&H` prefix starts with `&`, which is also the concatenation operator and `Long` suffix. The lexer must look ahead after `&` to determine: `&H` → hex literal, `&O` → octal literal, otherwise `&` is an operator or suffix. Additionally, the optional trailing `&` on hex/octal literals must be consumed greedily.

**Frequency:** Medium. `&H` hex literals are common for Windows API constants and color values in Access.

### Float Literals (§3.4.3)

```
float-literal = 1*digit "." 1*digit [exponent] [float-type-suffix]
              / "." 1*digit [exponent] [float-type-suffix]
              / 1*digit exponent [float-type-suffix]
exponent      = ("E" / "e" / "D" / "d") ["+" / "-"] 1*digit
float-type-suffix = "!" / "#" / "@"
```

`E` exponent produces `Single` by default; `D` exponent produces `Double`. The `@` suffix produces `Currency` (scaled integer, 4 decimal places).

**Syntactic complexity:** Medium. The `#` suffix creates ambiguity with date-literal delimiters; the `.` must be distinguished from member-access operator. Standard numeric-literal lexing with lookahead resolves these.

**Frequency:** Medium.

### Date Literals (§3.4.4)

```
date-literal = "#" date-or-time-value "#"
```

The content between `#` delimiters is a date, time, or datetime value. The spec permits multiple formats: `#1/1/2024#`, `#January 1, 2024#`, `#2024-01-01#`, `#12:30:00 PM#`, `#1/1/2024 12:30 PM#`.

**Syntactic complexity: HIGH.** This is the hardest literal form to lex correctly. Challenges:

1. **The `#` delimiter is overloaded** — also the `Double` type suffix and the conditional-compilation directive prefix. The lexer must determine from context: `#` at the start of a line is a preprocessor directive; `#` following an identifier/number is a type suffix; `#` in an expression position starts a date literal.

2. **The content between `#` delimiters is essentially free-form text** parsed as a date. It can contain `/`, `-`, spaces, commas, month names, AM/PM — characters that are operators or separators in normal VBA. The lexer must switch to a date-literal scanning mode upon encountering an opening `#` in date-literal context, and scan forward to the closing `#`.

3. **Locale sensitivity:** The spec states date literals are interpreted according to the host locale for code authored in the IDE, but stored/compiled in a canonical format. A parser reading source text may encounter locale-dependent date formats. In US-locale Access databases (the overwhelming majority), `#M/D/YYYY#` is standard. The parser should accept the flexible format and defer validation.

4. **The closing `#` must be found** — if absent, the error recovery is ambiguous (is the `#` a type suffix after all?).

**Recommended approach:** When the lexer encounters `#` in an expression-start position, scan forward for the next `#` on the same logical line. Everything between is the date-literal body. Parse the body as a date in a separate sub-parser.

**Frequency in Access:** Medium-high. Access VBA uses date literals extensively for queries and form filters: `#1/1/2024#`, `#12/31/2023 11:59 PM#`.

### Nothing, Null, Empty (§3.4 / §5.6)

`Nothing` is the null-object reference (used with `Set x = Nothing`). `Null` is the database null value — a `Variant` subtype indicating missing data. `Empty` is the uninitialized `Variant` value. All three are reserved keywords that function as literal-like expressions.

**Syntactic complexity:** Low — keyword tokens.

**Semantic subtlety (Access-specific):** `Null` propagation is critical in Access. Any expression involving `Null` yields `Null` (except `Is Null` and `Nz()`). This is a semantic concern, not a parsing concern, but the parser must recognize `Null` as an expression atom, not an identifier.

**Frequency:** High in Access. `Null` checks are pervasive due to database field nullability.

---

## 9. Conditional Compilation — Preprocessor (§3.4.1 / §6)

VBA supports a simple preprocessor with these directives:

```
#Const identifier = expression
#If condition Then
#ElseIf condition Then
#Else
#End If
```

The `#` must be the first non-whitespace character on the physical line (after line-continuation resolution). Conditional-compilation expressions can use a limited set of operators and literal values — they cannot call runtime functions. Predefined constants include `VBA7`, `Win64`, `Win32`, `Mac`.

**Processing order:** Conditional compilation is resolved **before** full lexical analysis of the module body. The preprocessor determines which physical lines are included, then the included lines are tokenized and parsed. This means excluded lines need not be syntactically valid VBA — they can contain anything.

**Syntactic complexity:** Medium. The preprocessor is a separate mini-language with its own expression grammar. The parser must either:
1. Implement a two-pass architecture (preprocess, then parse), or
2. Interleave preprocessing with lexing (more complex but single-pass).

The `#` prefix creates ambiguity with date literals and `Double` suffixes. The resolution rule is positional: `#` as the first non-whitespace on a line signals a directive. `#` elsewhere is a date delimiter or type suffix.

**Semantic subtlety:** `#Const` values are scoped to the module. Project-level conditional compilation constants are set in project properties (not in source text). In Access, conditional compilation is sometimes used to switch between DAO and ADO references, or to handle 32-bit vs. 64-bit Office:

```vba
#If VBA7 Then
    Declare PtrSafe Function GetTickCount Lib "kernel32" () As Long
#Else
    Declare Function GetTickCount Lib "kernel32" () As Long
#End If
```

**Frequency in Access:** Low to moderate. Most Access modules have zero directives. Modules with Windows API declarations often have `#If VBA7` blocks. The parser must handle them but they will not dominate the workload.

**Interactions:** Conditional compilation can wrap any construct — `Declare` statements, `Type` definitions, entire procedures. The parser must not attempt to parse the syntax of excluded blocks. Conditional compilation also interacts with line continuation (§3.3.3): a continued line's continuation is resolved before directive detection, so a directive cannot be split across lines with `_`.

---

## Cross-Cutting Parser Design Considerations

1. **Processing pipeline order:** Line joining (continuation) → Preprocessor directive evaluation → Comment stripping → Tokenization → Parsing. Getting this order wrong will produce incorrect results for edge cases involving `_` inside `#If` blocks or comments after continuations.

2. **The `#` / `&` / `!` triple ambiguity** is the single most complex lexical challenge. Each character serves as both a type suffix on identifiers/literals and an operator or delimiter in other contexts. A deterministic resolution requires the lexer to track minimal parser state (specifically: "am I at the start of a line?" and "did the previous token end an identifier or literal?").

3. **Case normalization** should happen at lex time. Store the original casing for display fidelity, but use a normalized (e.g., lowercased) form for all symbol-table lookups and keyword matching.

4. **Access-specific deviation (Appendix A):** The MS-VBAL spec's Appendix A notes implementation-specific extensions. Access VBA supports the `!` bang operator for default-member/dictionary access on `Forms`, `Reports`, `Controls`, and DAO/ADO `Recordset` objects. This is not a lexical deviation but affects how `!` is parsed in expressions. Access also permits `Me` in class modules (forms and reports are class modules in Access).

---

# Section B: Modules, Procedures, and Type System

---

## §4 Module Body

### §4.1 Module Declaration Structure

Every VBA module begins with an optional declarations header consisting of zero or more `Option` statements, followed by a declarations section, followed by zero or more procedure definitions. The `Option` statements are:

- **`Option Explicit`** — Requires all variables to be declared before use. Without it, any unrecognized identifier implicitly declares a module-level Variant. In Access codebases, this is present roughly 70–80% of the time; its absence means the parser must treat any bare identifier as a potential implicit variable.
- **`Option Base 0 | 1`** — Sets the default lower bound of array declarations. Rare in practice but affects how `Dim a(10)` is interpreted (11 elements vs. 10).
- **`Option Compare Binary | Text | Database`** — Controls string comparison semantics. The `Database` option is **Access-specific** and uses the database's collation order. This is the most common setting in Access modules.
- **`Option Private Module`** — Prevents the module's Public procedures from appearing in the macro/expression namespace of the Access host. Does not affect inter-module VBA calls.

**Syntactic complexity:** Low. These are keyword-only statements with no nesting.
**Frequency:** Very high — every module has an implicit declaration section even if empty.
**Interactions:** `Option Explicit` fundamentally changes identifier resolution semantics. `Option Compare Database` interacts with all string comparison operators (`=`, `Like`, `<`, `>`).

The grammar requires all `Option` statements to precede any declarations or procedures. A parser must enforce this ordering or at minimum detect violations. Each option may appear at most once per module.

---

### §4.2 Standard Modules

A standard module (called a "code module" or "bas module" in the Access UI) has no associated object instance. All its members exist at global scope within the VBA project. Members declared `Public` (the default visibility for `Sub` and `Function`) are callable from any other module, from Access macro `RunCode` actions, from query expressions via `=FunctionName()`, and from form/report control sources.

**Syntactic complexity:** Low. No special syntax beyond what §4.1 defines.
**Semantic subtlety:** The implicit `Public` default for procedures means that a `Sub DoWork()` with no access modifier is public. Variables declared with `Dim` at module level are `Private`; this asymmetry trips up analysis. `Global` is a legacy synonym for `Public` at module level.
**Frequency:** Very high — most Access applications have multiple standard modules.
**Interactions:** Public functions in standard modules are the primary mechanism for user-defined expressions in Access queries, forms, and reports.

---

### §4.3 Class Modules

A class module defines an instantiable type. It supports:

- **`Implements <InterfaceName>`** — Declares that the class implements all members of another class used as an interface. The implementing procedures must be named `InterfaceName_MemberName`. A single class may have multiple `Implements` statements. The parser must resolve these to validate that all interface members are satisfied.
- **`Class_Initialize`** and **`Class_Terminate`** — Special event handler subs invoked on `New` and final reference release, respectively. These have no parameters.
- **Instancing** — In Access, class modules are always `PublicNotCreatable` or `Private`. Access does not support `GlobalMultiUse` or other instancing modes available in ActiveX DLL projects.

**Syntactic complexity:** Medium. The `Implements` statement creates cross-module coupling. The `InterfaceName_MemberName` naming requirement is a semantic constraint not visible in the grammar of the implementing procedure alone.
**Frequency:** Medium in Access — many Access applications use class modules for business logic, custom collections, and event sinking.
**Interactions:** `Implements` interacts with Property Get/Let/Set triplets (§4.5.3) — all three must be implemented if the interface defines a read-write property.

---

### §4.3.1 Form and Report Modules (Access-Specific Extension)

Form and report modules are **special class modules** attached to Access Form and Report objects. They extend standard class module behavior in three critical ways:

1. **Hidden control declarations.** Every control on the form/report surface is implicitly declared as a property of the module's class. If a form has a text box named `txtCustomerName`, the module behaves as if it contains an invisible `Public Property Get txtCustomerName() As TextBox` (and corresponding setter). The parser will encounter references like `Me.txtCustomerName` or bare `txtCustomerName` that resolve to these implicit members. **These declarations do not appear in the module source text.** The parser must either have access to the form's control inventory or treat unresolved `Me.X` references as potentially valid control references.

2. **Event handler naming convention.** Event handlers are `Sub` procedures named with the pattern `ControlName_EventName` or `Form_EventName` / `Report_EventName`. Examples:
   - `Private Sub Form_Load()`
   - `Private Sub cmdSave_Click()`
   - `Private Sub txtAmount_BeforeUpdate(Cancel As Integer)`
   The underscore-delimited naming convention **is the binding contract** — there is no `Handles` clause or explicit wiring. The parser must recognize these patterns to map procedures to events. The parameter signatures must match the event's declared signature exactly.

3. **`Me` keyword.** Inside a form/report module, `Me` refers to the form/report instance (typed as `Access.Form` or `Access.Report` at the general level, but with the implicit members of the specific form/report).

**Syntactic complexity:** Medium at the syntax level, but **high for semantic analysis** because of the invisible declarations.
**Frequency:** Extremely high — form and report modules are the most common location for Access VBA code.
**Interactions:** The `Cancel` parameter in `BeforeUpdate`, `BeforeInsert`, `BeforeDelConfirm`, and similar events is **ByRef Integer**. Setting `Cancel = True` aborts the operation. This is the single most important ByRef interaction in Access VBA (see §4.6).

---

### §4.4 Declarations Section

The declarations section appears after the `Option` statements and before the first procedure. It may contain:

**Variable declarations:**
- `Dim` — Private at module level, local in procedures.
- `Private` — Explicitly module-private.
- `Public` — Visible to other modules. In class/form/report modules, exposes as a property of the class instance.
- `Static` — In procedures only; variable retains value between calls.
- `Global` — Legacy synonym for `Public`; valid only in standard modules.

All of these share the grammar: `<keyword> <name> [As <type>] [, <name> [As <type>]]...`. The parser must handle the comma-separated multi-declaration form and note that `Dim a, b As Integer` declares `a` as **Variant** and `b` as Integer — each variable needs its own `As` clause.

**Constant declarations:**
`[Public | Private] Const <name> [As <type>] = <constant-expression>`. The expression must be evaluable at compile time. String constants, numeric literals, and other named constants are permitted.

**User-defined type declarations:**
```
[Public | Private] Type <name>
    <element> As <type>
    ...
End Type
```
UDTs may contain fixed-length strings (`element As String * 50`), arrays (`element(1 To 10) As Long`), and nested UDTs. They cannot contain object references. UDTs declared `Public` must be in standard modules.

**Enum declarations:**
```
[Public | Private] Enum <name>
    <member> [= <constant-expression>]
    ...
End Enum
```
Members auto-increment from 0 by default. Enum members are implicitly `Long`. Public enums inject their member names into the global namespace — the parser must handle this for identifier resolution.

**External procedure declarations (Declare):**
```
[Public | Private] Declare [PtrSafe] Sub|Function <name> Lib "<dll>" [Alias "<alias>"] ([params]) [As <type>]
```
`PtrSafe` is required in 64-bit VBA (Access 2010+ 64-bit). The parser must handle both 32-bit and 64-bit declaration variants. The `Alias` clause permits mapping to a different exported name. String parameters in `Declare` statements have special marshaling semantics — `ByVal String` passes an ANSI `char*`.

**Event declarations:**
`[Public] Event <name>([params])` — Declares an event that the class can `RaiseEvent`. Only valid in class modules (including form/report modules). Parameters must be `ByVal` or `ByRef` — no `Optional` or `ParamArray`.

**Syntactic complexity:** Medium overall; the `Declare` statement is the most complex single declaration form.
**Frequency:** Very high — virtually every module has some declarations.
**Interactions:** `Public` variables in class modules become implicit properties (interacts with Property Get/Let/Set in §4.5.3). `Type` and `Enum` definitions affect the type system (§3.3). `Declare` interacts with parameter passing conventions (§4.6).

---

## §4.5 Procedure Declarations

### §4.5.1 Sub Declaration

```
[Public | Private | Friend] [Static] Sub <name> ([params])
    ...
    [Exit Sub]
    ...
End Sub
```

A `Sub` has no return value. `Exit Sub` causes immediate return from any point. The `Static` modifier on the `Sub` line makes **all** local variables static (equivalent to declaring each with `Static` instead of `Dim`).

`Friend` is valid only in class modules and permits access from within the same VBA project but not from external COM clients.

**Syntactic complexity:** Low.
**Frequency:** Very high.
**Interactions:** When `Static` is on the procedure line, the parser must propagate static lifetime to all local `Dim` declarations.

---

### §4.5.2 Function Declaration

```
[Public | Private | Friend] [Static] Function <name> ([params]) [As <type>]
    ...
    <name> = <return-expression>
    ...
    [Exit Function]
    ...
End Function
```

The return value is assigned by assigning to the function's own name: `MyFunc = 42`. This creates parsing ambiguity — the parser must recognize that `<FunctionName> = <expr>` on the left side of an assignment is a return-value assignment, not a recursive call. If the function name appears on the **right** side or in a call context, it is a recursive invocation.

If no assignment to the function name occurs, the return value is the default for the declared type (0 for numeric, `""` for String, `Empty` for Variant, `Nothing` for Object).

**Syntactic complexity:** Medium — the function-name-as-return-value pattern requires context-sensitive parsing of the LHS of assignment statements.
**Frequency:** Very high — functions are the primary mechanism for computed values, query expressions, and form control sources in Access.
**Interactions:** The return-value-via-name-assignment pattern interacts with recursion detection. A function name on the LHS with arguments — `MyFunc(x) = value` — is **not** a return assignment but a default member/array assignment through the return value.

---

### §4.5.3 Property Procedures (CRITICAL)

Property procedures come in three forms that must be analyzed as a **coordinated triplet** sharing the same name:

```
[Public | Private | Friend] [Static] Property Get <name> ([params]) [As <type>]
    <name> = <return-expression>
End Property

[Public | Private | Friend] [Static] Property Let <name> ([params], <value-param>)
    ' assigns non-object value
End Property

[Public | Private | Friend] [Static] Property Set <name> ([params], <reference-param>)
    ' assigns object reference
End Property
```

**Critical parsing rules:**

1. **Parameter alignment.** `Property Get` has N parameters. The corresponding `Property Let`/`Property Set` must have N+1 parameters — the extra final parameter is the incoming value/reference. The types of the first N parameters must match across all members of the triplet.

2. **Type consistency.** The return type of `Property Get` must match the type of the final parameter of `Property Let` (for non-object types) or `Property Set` (for object types).

3. **Not all three are required.** A read-only property has only `Get`. A write-only property has only `Let` and/or `Set`. An object property typically has `Get` and `Set`. A Variant property may have all three.

4. **`Property Let` vs. `Property Set`.** `Let` handles value assignment (`obj.Prop = 5`). `Set` handles object reference assignment (`Set obj.Prop = otherObj`). The parser must distinguish `x.Prop = value` (invokes `Let`) from `Set x.Prop = value` (invokes `Set`).

5. **Indexed properties.** When `Property Get` takes parameters, the property is indexed: `obj.Prop(index)`. The `Let`/`Set` forms then take those same parameters plus the value parameter.

**The `Default` keyword:** A single property (or function) per class may be marked as the class's default member using the `Attribute <name>.VB_UserMemId = 0` hidden attribute (the `Default` keyword in source may also appear, depending on the editing environment). Default members enable `obj(index)` to resolve to `obj.DefaultProp(index)` and enable implicit type coercion when an object is used in a value context. The parser should track default-member attributes.

**Syntactic complexity:** HIGH. The triplet coordination, parameter alignment rules, and Let/Set distinction make this the most complex procedure-level construct.
**Semantic subtlety:** HIGH. The Let/Set distinction maps directly to the `Set` keyword on assignment statements — the parser must track whether an assignment target resolves to a `Property Let` or `Property Set` to validate the presence or absence of the `Set` keyword.
**Frequency:** Medium in standard modules; high in class modules and form/report modules (especially via public variables that compile to implicit property pairs).
**Interactions:** Interacts with `Implements` (all interface properties must be fully implemented), with assignment statement parsing (`Set` keyword), with default member resolution, and with the implicit properties created by `Public` variables in class modules.

---

### §4.5.4 Event Handlers

Event handlers are `Sub` procedures whose name follows the convention `<SourceName>_<EventName>`. There are two binding mechanisms:

1. **Form/Report event handlers (Access-specific):** The source name is either `Form`, `Report`, or a control name. The procedure must be `Private Sub`. Examples: `Private Sub Form_Open(Cancel As Integer)`, `Private Sub btnSave_Click()`. The binding is purely by naming convention — renaming a control breaks the binding.

2. **WithEvents event handlers:** When a class-level variable is declared `Private WithEvents obj As SomeClass`, event handlers are named `obj_EventName`. The parser must track `WithEvents` declarations and correlate them with the corresponding `Sub` procedures.

**Parameter signatures must exactly match the event declaration.** For the `BeforeUpdate` event, the handler must be `Private Sub ControlName_BeforeUpdate(Cancel As Integer)` — the `Cancel` parameter is `ByRef` and setting it to `True` (or any non-zero value) cancels the operation.

**Syntactic complexity:** Low at the grammar level (they are ordinary `Sub` declarations), but **high for semantic binding** — the parser must split procedure names on `_` and resolve the left portion to a control, form/report, or `WithEvents` variable.
**Frequency:** Extremely high in Access — event handlers are the primary code-entry mechanism.
**Interactions:** Interacts with the hidden control declarations in form/report modules (§4.3.1), with `WithEvents` variable declarations, and with the ByRef parameter convention for cancellation (§4.6).

---

## §4.6 Parameter Declarations (CRITICAL)

The parameter list grammar is:

```
[Optional] [ByVal | ByRef] [ParamArray] <name>[() ] [As <type>] [= <default>]
```

### ByRef (Default)

`ByRef` is the **default** parameter passing convention when neither `ByVal` nor `ByRef` is specified (exception: some specific contexts; see below). The caller's variable is passed by reference — modifications inside the procedure are visible to the caller.

**This is critical in Access** because event cancellation relies entirely on ByRef semantics:
```vba
Private Sub Form_BeforeUpdate(Cancel As Integer)
    If Not ValidateData() Then Cancel = True  ' modifies caller's Cancel variable
End Sub
```

The parser must track whether parameters are ByRef or ByVal because:
- ByRef parameters can modify caller state (side-effect analysis).
- ByRef parameters require an assignable (lvalue) argument at the call site, unless the argument is parenthesized (which forces ByVal).
- The expression `Call MySub((x))` passes `x` **ByVal** regardless of the declaration, because the parentheses create a temporary. This is a notorious VBA parsing subtlety.

**Syntactic complexity:** Low for declaration; **high for call-site analysis** due to the parenthesization-forces-ByVal rule.
**Frequency:** Extremely high.

### ByVal

`ByVal` explicitly requests copy semantics. For objects, `ByVal` copies the **reference** (not the object) — the callee gets its own reference but to the same object. For strings, a copy of the string is made.

### Optional Parameters

```vba
Sub DoWork(Required As Long, Optional Extra As Variant = "default")
```

- `Optional` parameters must come after all required parameters.
- Once one parameter is `Optional`, all subsequent parameters must also be `Optional` (or `ParamArray`).
- Default values are constant expressions. If no default is specified, the default is the type's default value (0, "", Empty for Variant).
- `IsMissing()` only works for `Optional Variant` parameters with no explicit default — it returns `True` when the argument was not supplied. For non-Variant optional parameters, `IsMissing()` always returns `False`; the parameter simply takes its default value.

**Syntactic complexity:** Medium — the parser must enforce ordering constraints and track default-value expressions.
**Frequency:** High in Access — many utility functions use optional parameters.

### ParamArray

```vba
Sub LogMessage(ParamArray Parts() As Variant)
```

- Must be the **last** parameter.
- Must be `Variant()` — no other type is permitted.
- Cannot coexist with `Optional` on other parameters (in the same parameter list — though some implementations are lenient).
- Receives zero or more arguments as a Variant array.
- Cannot be `ByRef` or `ByVal` — the array itself is always a new local array, but the elements may contain references.

**Syntactic complexity:** Low for declaration; medium for call-site resolution (the parser must collect all remaining arguments into the ParamArray).
**Frequency:** Low to medium.

### Named Arguments

At the call site, arguments may be passed by name:
```vba
MsgBox Prompt:="Hello", Title:="Greeting", Buttons:=vbOKCancel
```

The `:=` token is the named-argument assignment operator. **The parser must distinguish `:=` (named argument) from `=` (comparison operator or assignment).** In argument-list context, `Name:=Value` is unambiguously a named argument. Named arguments may appear in any order and may be mixed with positional arguments (positional arguments must come first).

**Syntactic complexity:** Medium — the `:=` token requires the lexer or parser to recognize the two-character sequence in argument context.
**Frequency:** High — heavily used with `MsgBox`, `DoCmd` methods, and `Application` methods in Access.
**Interactions:** Named arguments interact with `Optional` parameters (allowing callers to skip optional arguments) and with `Declare` statements (where named arguments are also valid).

---

## §3.3 Data Types and Type System

### Built-in Primitive Types

| Type | Size | Notes |
|------|------|-------|
| `Boolean` | 2 bytes | `True` = -1, `False` = 0. Any non-zero integer converts to `True`. |
| `Byte` | 1 byte | Unsigned, 0–255. |
| `Integer` | 2 bytes | Signed, -32768 to 32767. |
| `Long` | 4 bytes | Signed. This is the workhorse integer type in 32-bit VBA. |
| `LongLong` | 8 bytes | 64-bit signed. Only available in 64-bit VBA (VBA7). Used with `LongPtr` for pointer-sized values. |
| `LongPtr` | 4 or 8 bytes | Alias: `Long` on 32-bit, `LongLong` on 64-bit. Critical for API declarations. |
| `Single` | 4 bytes | IEEE 754 single-precision float. |
| `Double` | 8 bytes | IEEE 754 double-precision float. |
| `Currency` | 8 bytes | Fixed-point decimal, 4 decimal places. Common in Access financial applications. |
| `Decimal` | 14 bytes | Only available as a Variant subtype — cannot declare `Dim x As Decimal`; must use `CDec()`. |
| `Date` | 8 bytes | Stored as Double; integer part = days since Dec 30, 1899; fractional part = time. |
| `String` | Variable | See below for fixed-length vs. variable-length. |
| `Object` | 4/8 bytes | Generic COM object reference (late-bound). |
| `Variant` | 16 bytes | See below. |

The parser must recognize all these type keywords in `As` clauses and `Declare` statements. `LongPtr` and `LongLong` require conditional compilation awareness (`#If VBA7` / `#If Win64`).

**Syntactic complexity:** Low for type keywords.
**Frequency:** Extremely high.

### String Types

Two declaration forms:
- **Variable-length:** `Dim s As String` — length changes dynamically.
- **Fixed-length:** `Dim s As String * 50` — always exactly 50 characters, padded with spaces. The `* <length>` suffix requires specific parser handling.

Fixed-length strings are valid in UDTs and in module-level declarations. They cannot be used as function return types or in some parameter contexts. The parser must track the distinction because fixed-length strings have different assignment semantics (truncation and padding).

**Frequency:** Fixed-length strings are uncommon except in UDTs used for file I/O or API calls.

### Variant Type

`Variant` is VBA's universal container type. A Variant variable can hold:
- Any primitive value (including `Empty`, `Null`, `Error` values)
- Arrays (including arrays of Variants)
- Object references
- Nothing / Empty / Null (these are distinct states)

Key introspection functions:
- `VarType(v)` — returns a `VbVarType` enum (vbInteger, vbString, etc.)
- `TypeName(v)` — returns a string like "Integer", "String", "Nothing"
- `IsEmpty(v)` — `True` if never assigned
- `IsNull(v)` — `True` if contains `Null` (database null)
- `IsMissing(v)` — `True` if an Optional Variant parameter was not supplied
- `IsObject(v)` — `True` if contains an object reference

**Semantic subtlety:** `Null` propagation — most operations involving `Null` return `Null` (e.g., `Null + 1` yields `Null`). This is critical in Access because database fields frequently contain `Null`, and Variant is the natural type for handling them. A parser performing data-flow analysis must track Null-propagation paths.

**Undeclared variables are Variant.** When `Option Explicit` is absent, any bare identifier is an implicit module-level Variant.

**Syntactic complexity:** Low at declaration level, but **high for semantic analysis** because Variant defers type resolution to runtime.
**Frequency:** Extremely high in Access — all form control values, recordset field values, and DLookup/DSum results are Variant.

### Object Types: Early-Bound vs. Late-Bound

- **Early-bound:** `Dim rs As DAO.Recordset` — the type is known at compile time; member access is validated, IntelliSense works, and calls use vtable dispatch.
- **Late-bound:** `Dim obj As Object` — no compile-time type information; all member access is resolved at runtime via `IDispatch`. The parser cannot validate member names on late-bound objects.

`CreateObject("className")` and `GetObject()` always return late-bound references. `New ClassName` returns early-bound.

**Access-specific:** Access commonly uses both `DAO.Recordset` (early-bound via reference) and `CurrentDb.OpenRecordset(...)` (which returns an early-bound DAO.Recordset if the DAO reference is set). Missing library references convert early-bound code to unresolvable — a frequent source of Access deployment bugs.

**Frequency:** High.
**Interactions:** Late-bound objects defeat static analysis. The parser should flag late-bound calls as un-analyzable for member resolution.

### User-Defined Types (UDTs)

```vba
Public Type CustomerRecord
    ID As Long
    Name As String * 100
    Balance As Currency
    Addresses(1 To 3) As String
End Type
```

- May contain: primitives, fixed-length strings, fixed arrays, nested UDTs.
- May **not** contain: object references, dynamic arrays, Variants (in some implementations).
- `Public` UDTs must be declared in standard modules (not in class, form, or report modules).
- Members are accessed with dot notation: `rec.Name`.
- UDTs can be passed `ByRef` to procedures (passing `ByVal` is not supported for UDTs).

**Syntactic complexity:** Medium — the parser must handle fixed-length string suffixes and fixed-array bounds within the type body.
**Frequency:** Low to medium in Access codebases.
**Interactions:** UDTs interact with `Declare` statements (API calls frequently use UDTs for structured parameters) and with fixed-length strings.

### Enumerations

```vba
Public Enum CustomerStatus
    csActive = 1
    csInactive = 2
    csSuspended = 3
End Enum
```

- Members are implicitly `Long`.
- Auto-increment from 0 if no value specified; auto-increment from previous value + 1 if partially specified.
- `Public` enum members are injected into the **global namespace** — `csActive` is valid without qualification. The parser must handle this during identifier resolution.
- Enum types can be used in `As` clauses: `Dim status As CustomerStatus`. At runtime, no type enforcement occurs — any `Long` is accepted.

**Syntactic complexity:** Low.
**Frequency:** Medium.
**Interactions:** Global namespace injection means enum members can collide with other identifiers. The parser must include enum members in its symbol table at global scope.

### Arrays

**Fixed-size arrays:**
```vba
Dim a(10) As Long          ' indices 0..10 (or 1..10 if Option Base 1)
Dim b(1 To 5, 1 To 3) As String   ' 2D, explicit bounds
```

**Dynamic arrays:**
```vba
Dim c() As Long            ' declared with empty parens
ReDim c(1 To n)            ' sized at runtime
ReDim Preserve c(1 To n+1) ' resize preserving data (only last dimension)
```

Key parsing points:
- `Dim a(10)` vs. `Dim a() As Long` — parentheses with content vs. empty.
- `ReDim` can appear in executable code, not just declarations. `ReDim` can also declare new variables if `Option Explicit` is off.
- `ReDim Preserve` only allows changing the last dimension's upper bound.
- `Erase` resets a dynamic array to unallocated or a fixed array to defaults.
- `LBound(arr, dim)` and `UBound(arr, dim)` for bounds introspection.
- Array-valued Variants: a Variant can hold an array; `IsArray(v)` tests this.

**Syntactic complexity:** Medium — `ReDim` is both a declaration and an executable statement; the parser must handle bounds expressions that are runtime-evaluated.
**Frequency:** High.
**Interactions:** `ParamArray` (§4.6) produces a Variant array. Array-valued Variants interact with the Variant type system. `Option Base` (§4.1) affects default lower bounds.

---

## §4.7 Scope and Lifetime

### Module-Level Scope

| Keyword | Visibility | Notes |
|---------|-----------|-------|
| `Public` | All modules in the project (and COM clients for class modules) | Default for `Sub`/`Function` in standard modules. |
| `Private` | Current module only | Default for `Dim` at module level. |
| `Friend` | All modules in the same project, but not COM clients | Valid only in class modules. |
| `Global` | Synonym for `Public` | Valid only in standard modules. Legacy keyword. |

`Public` variables in **class modules** (including form/report modules) are exposed as read-write properties of the class instance. They are semantically equivalent to a `Property Get`/`Property Let` pair (or `Property Get`/`Property Set` for object types). The parser may wish to normalize these to explicit property triplets during analysis.

### Procedure-Level Scope

| Keyword | Lifetime | Notes |
|---------|----------|-------|
| `Dim` | Procedure duration | Reinitialized on each call. |
| `Static` | Module lifetime | Retains value between calls. |

The `Static` modifier on a procedure declaration line (`Static Sub Foo()`) makes **all** local variables in that procedure static, regardless of whether they are declared with `Dim` or `Static`. The parser must propagate this when analyzing variable lifetime.

### Name Resolution Order

VBA resolves unqualified names in this order:
1. Local variables and parameters in the current procedure.
2. Module-level variables, constants, UDTs, and enums in the current module.
3. `Public` members of other modules in the project (standard modules searched by project-defined order; ambiguity is a compile error).
4. Members of referenced type libraries (in reference priority order).
5. Enum members (globally scoped).

**Access-specific:** In form/report modules, control names are resolved between steps 1 and 2 — a local variable can shadow a control name, and a module-level variable can shadow a control name. This is a frequent source of bugs.

**Syntactic complexity:** Low at the declaration level; **high for resolution** — the parser must build a multi-layered symbol table.
**Frequency:** Relevant to every identifier reference in every module.
**Interactions:** `Option Explicit` (§4.1) eliminates implicit Variant declarations from the resolution chain. `Public` enum members (§4.4) inject names at global scope. Hidden control declarations (§4.3.1) inject names in form/report module scope.

---

## Cross-Cutting Concern Summary

| Construct | Parse Difficulty | Semantic Difficulty | Access Frequency |
|-----------|-----------------|--------------------|-----------------| 
| Option statements | Low | Low–Medium (`Option Compare Database`) | Very High |
| Variable/Const declarations | Low | Medium (implicit Variant, Dim asymmetry) | Very High |
| Sub/Function | Low–Medium | Medium (function-name return) | Very High |
| Property Get/Let/Set | **High** | **High** (triplet coordination, Let/Set) | Medium–High |
| Event handlers | Low | **High** (naming convention binding) | Very High in Access |
| ByRef/ByVal parameters | Low | **High** (parenthesization rule, Cancel pattern) | Very High |
| Optional/ParamArray | Medium | Medium | High/Low |
| Named arguments (`:=`) | Medium | Low | High |
| Variant type | Low | **High** (Null propagation, runtime typing) | Very High in Access |
| Arrays | Medium | Medium (ReDim as executable) | High |
| UDTs | Medium | Low | Low–Medium |
| Enums | Low | Medium (global namespace injection) | Medium |
| Scope resolution | N/A | **High** (multi-layer symbol table) | Every reference |

---

# Section C: Statements, Expressions, and Object Model

---

## §5.1 Expressions and Operator Precedence

**§5.1.1** defines VBA's expression evaluation model with a fixed operator precedence hierarchy, evaluated left-to-right within each precedence level. The full table, highest to lowest:

| Precedence | Operator(s) | Category |
|---|---|---|
| 1 | `^` | Exponentiation |
| 2 | Unary `-` | Negation |
| 3 | `*`, `/` | Multiplicative |
| 4 | `\` | Integer division |
| 5 | `Mod` | Modulo |
| 6 | `+`, `-` | Additive |
| 7 | `&` | String concatenation |
| 8 | `=`, `<>`, `<`, `>`, `<=`, `>=`, `Like`, `Is` | Comparison |
| 9 | `Not` | Logical negation |
| 10 | `And` | Conjunction |
| 11 | `Or` | Disjunction |
| 12 | `Xor` | Exclusive or |
| 13 | `Eqv` | Equivalence |
| 14 | `Imp` | Implication |

**Syntactic complexity: medium.** The grammar is standard precedence-climbing or Pratt-parsable. The key subtlety is that `+` and `&` both perform concatenation in some contexts: `+` on two strings concatenates but propagates `Null`, while `&` coerces operands to strings and treats `Null` as `""`. Comparison operators return `True` (-1), `False` (0), or `Null` if either operand is `Null`. The `Is` operator compares object references, not values. The `Like` operator takes a pattern string with wildcards (`*`, `?`, `#`, `[charlist]`).

**Semantic subtlety:** All logical operators are bitwise on integers and logical on Booleans. There is no short-circuit evaluation — both sides of `And`/`Or` are always evaluated. This means `If Not obj Is Nothing And obj.Value > 0` will error at runtime, a pattern frequently encountered in Access codebases.

**Frequency: ubiquitous.** Every module contains expressions. **Parsing interaction:** the `=` token is overloaded between assignment (in statement position) and comparison (in expression position); disambiguation requires context.

---

## §5.2 Member Access Expressions

### §5.2.1 Dot Operator (`.`)

The dot operator resolves member access in identifier chains: `object.member.submember`. Resolution proceeds left-to-right; each step must resolve to an entity (object, module, enum) that exposes the next member. The grammar is `<l-expression> "." <unrestricted-identifier>`.

**Syntactic complexity: low.** Standard member-access parsing. **Semantic subtlety: high.** The parser cannot resolve whether `Foo.Bar` refers to a module-qualified procedure, an object property, an enum member, or a type-qualified field without symbol table information. In Access codebases, chains like `Forms("frmMain").Controls("txtName").Value` and `CurrentDb.TableDefs("tblData").Fields("ID")` are standard.

**Frequency: ubiquitous.**

### §5.2.2 Bang Operator (`!`) — CRITICAL

**§5.2.2** defines the bang operator as syntactic sugar for default collection member access by string key. The grammar is `<l-expression> "!" <identifier>`, where the identifier after `!` is treated as a string literal key, not as a programmatic identifier.

The equivalences are:

```
Forms!MyForm          ≡  Forms("MyForm")
Me!txtName            ≡  Me.Controls("txtName")
rs!FieldName          ≡  rs.Fields("FieldName")
CurrentDb.TableDefs!tblMain.Fields!ID  ≡  CurrentDb.TableDefs("tblMain").Fields("ID")
```

**Critical parsing rule:** Bang binds tighter than dot. The expression `Forms!frm!ctl.Property` parses as `((Forms!frm)!ctl).Property`, not as `Forms!(frm!ctl).Property` or any other grouping. The parser must assign `!` higher precedence than `.` in member-access chains.

**Syntactic complexity: medium.** The token after `!` is lexed as an identifier but must be reinterpreted as a string key during AST construction. Bracket syntax is permitted: `Me![Field With Spaces]` uses the bracketed identifier as the key string.

**Semantic subtlety: high and Access-specific.** The default collection varies by object type: for `Form`, it is `Controls`; for `Recordset`, it is `Fields`; for `TableDef`, it is `Fields`. The parser cannot resolve which collection is being indexed without type information. This construct is Access-dominant — it is rare in Excel/Word VBA but pervasive in Access modules, particularly in form event handlers (`Me!txtName`) and DAO recordset manipulation (`rs!FieldName`).

**Parsing interaction with `With`:** Inside a `With Forms!frmMain` block, `!txtName` is valid and resolves as `Forms!frmMain!txtName`, i.e., the implicit With target receives the bang-access. This combination is common and must be modeled.

**Frequency: very high in Access codebases.**

### §5.2.3 Default Property Access — CRITICAL

**§5.2.3** specifies that when an object reference appears in a value-requiring context, VBA implicitly accesses the object's default member (marked with `Attribute VB_UserMemId = 0` in the class). This is invisible in source code.

Examples:
- `txtBox` in `x = txtBox` resolves to `x = txtBox.Value` because `Value` is the default property of a TextBox control.
- `rs!FieldName` in `x = rs!FieldName` resolves to `x = rs.Fields("FieldName").Value` — two levels of default property access (Fields is default of Recordset, Value is default of Field).
- `coll(1)` where `coll` is a `Collection` invokes `coll.Item(1)` because `Item` is the default member.

**Syntactic complexity: low** (nothing to parse — it is absent from the source text). **Semantic subtlety: very high.** The parser must represent the AST in a way that allows downstream semantic analysis to insert default-property dereferences. A raw identifier reference node must be distinguishable from an explicit property access node so that a later resolution pass can determine whether default-property insertion is needed.

**Parsing interaction with Set vs. Let:** This is the mechanism that makes `Set` vs. implicit `Let` semantically distinct (§5.3.2). Without `Set`, assignment invokes default-property access on the right-hand side if it is an object. With `Set`, the object reference itself is assigned.

**Frequency: extremely high.** Nearly every Access form-module expression involving controls relies on implicit default property access.

### §5.2.4 Index Expressions

**§5.2.4** covers parenthesized argument lists following an expression: `expr(arglist)`. This construct is syntactically ambiguous among:
- Array element access: `arr(i, j)`
- Collection/default-member access: `coll(key)`
- Function/method invocation: `Func(arg1, arg2)`

The grammar is `<l-expression> "(" <argument-list> ")"`. The parser **cannot distinguish these** without type information. The AST should represent all three as a generic "index or call expression" node, deferring disambiguation to semantic analysis.

**Syntactic complexity: medium.** Parentheses interact with the `Call` statement (§5.5) — `Call Sub(args)` requires parentheses, while `Sub args` forbids them for the outermost call (though a single argument may be parenthesized as a value-coercion idiom).

**Frequency: ubiquitous.**

### §5.2.5 New Expression

`New ClassName` instantiates an object. Appears in assignment (`Set x = New Collection`) or in `Dim` declarations (`Dim x As New Collection`). The `As New` form has auto-instantiation semantics: any access to `x` when it is `Nothing` triggers implicit instantiation, which means `Set x = Nothing` does not permanently release the object if `x` is subsequently referenced.

**Syntactic complexity: low.** **Frequency: moderate.**

### §5.2.6 TypeOf...Is Expression

`TypeOf <object-expression> Is <type-name>` evaluates to Boolean. Only valid inside `If` or conditional expressions. The grammar requires the `Is` keyword — `TypeOf obj Is Form` is valid; `TypeOf obj = Form` is not.

**Syntactic complexity: low.** The parser must recognize `TypeOf` as a unary prefix that consumes an expression and the `Is` keyword followed by a type name. **Frequency: low-to-moderate** in Access; used in generic control-iteration patterns.

---

## §5.3 Assignment Statements

### §5.3.1 Let Statement (Value Assignment)

The grammar is `[Let] <l-expression> "=" <expression>`. The `Let` keyword is almost never written explicitly; the implicit form `x = 5` is universal. The left-hand side must be an l-value (variable, property, array element, or object-with-default-property).

**Syntactic complexity: medium.** The `=` token is ambiguous with comparison; the parser must recognize assignment by statement position (an `=` at the start of a statement or after `Let` is assignment). Compound assignment (`+=`, etc.) does not exist in VBA.

**Frequency: ubiquitous.**

### §5.3.2 Set Statement (Object Reference Assignment) — CRITICAL

**§5.3.2** defines `Set <l-expression> "=" <expression>` as the exclusive mechanism for assigning object references. `Set obj = expr` binds the variable to the object; `Set obj = Nothing` releases the reference.

**The critical semantic distinction:** Without `Set`, `obj = expr` is a `Let` assignment that triggers default-property access on both sides. If `obj` is a TextBox and `expr` is a Recordset, then `obj = expr` assigns `expr.Fields(0).Value` to `obj.Value` (default properties cascading). With `Set`, the object reference itself is stored.

This distinction is **invisible at the syntactic level** except for the `Set` keyword. The parser must preserve whether `Set` is present as a first-class AST property of assignment nodes. Failure to do so loses semantic information that cannot be recovered.

Common Access pattern:
```vba
Dim frm As Form
Set frm = Forms!frmMain       ' object reference assignment
frm.Caption = "Title"          ' Let assignment to property

Dim ctl As Control
Set ctl = Me!txtName           ' object reference
ctl = "Hello"                  ' Let assignment — writes to ctl.Value (default property)
```

**Syntactic complexity: low** (keyword presence is trivial to detect). **Semantic subtlety: very high.** **Frequency: high in Access codebases** — every form/recordset manipulation involves `Set`.

---

## §5.4 Control Flow Statements

### §5.4.1 If...Then...Else

Block form:
```
If <condition> Then
    <statements>
[ElseIf <condition> Then
    <statements>]*
[Else
    <statements>]
End If
```

Single-line form: `If <condition> Then <statement> [Else <statement>]` — no `End If`, entire construct on one logical line (may use line continuation `_`).

**Syntactic complexity: medium.** The parser must distinguish single-line from block form. The heuristic: if a newline (not a line continuation) follows `Then`, it is a block form. `ElseIf` is one keyword, not `Else If` (though `Else If` on separate lines can appear in block form as a nested If inside an Else block, which is semantically different).

**Frequency: ubiquitous.**

### §5.4.2 Select Case

```
Select Case <test-expression>
    Case <range-list>
        <statements>
    [Case Else
        <statements>]
End Select
```

Range lists support: literal values (`Case 1, 3, 5`), ranges (`Case 1 To 10`), and relational tests (`Case Is > 10`). These may be combined: `Case 1 To 5, 7, Is > 20`.

**Syntactic complexity: medium.** The `Case Is > 10` form reuses the `Is` keyword in a non-standard way (not the object-comparison `Is`); the parser must handle this context-sensitive usage. **Frequency: moderate.**

### §5.4.3 For...Next

`For <counter> = <start> To <end> [Step <increment>]` / `Next [<counter>]`. The counter variable persists after loop exit with its final value (the value that caused the termination test to fail). `Step` may be negative.

**Syntactic complexity: low.** **Parsing note:** `Next` may optionally name the variable; `Next i, j` closes two nested loops. **Frequency: high.**

### §5.4.4 For Each...Next

`For Each <variable> In <collection-or-array>` / `Next [<variable>]`. The variable must be `Variant` or `Object` (or a specific object type matching the collection's item type).

**Syntactic complexity: low.** **Frequency: high in Access** — used for iterating `Controls`, `Forms`, `TableDefs`, `Fields`, `QueryDefs`.

### §5.4.5 Do...Loop

Four forms:

| Form | Tests | When |
|---|---|---|
| `Do While <cond>` ... `Loop` | Pre-test | Continue while true |
| `Do Until <cond>` ... `Loop` | Pre-test | Continue until true |
| `Do` ... `Loop While <cond>` | Post-test | Continue while true |
| `Do` ... `Loop Until <cond>` | Post-test | Continue until true |

**Syntactic complexity: low.** The parser must accept `While`/`Until` after either `Do` or `Loop` but not both in the same construct. **Frequency: high** — `Do Until rs.EOF` / `rs.MoveNext` / `Loop` is the canonical recordset iteration pattern in Access.

### §5.4.6 While...Wend

`While <condition>` / `Wend`. Legacy pre-test loop. No `Exit While` exists; the only exit is the condition becoming false (or `GoTo`). Functionally equivalent to `Do While...Loop` but lacks `Exit Do`.

**Syntactic complexity: low.** **Frequency: low** in modern Access code; found in legacy modules.

### §5.4.7 With...End With — CRITICAL

```
With <object-expression>
    .<member>
    !<key>
    .Method args
End With
```

**§5.4.7** establishes an implicit object context. All leading-dot (`.member`) and leading-bang (`!key`) references within the block resolve against the `With` target object. Nesting is permitted; inner `With` blocks shadow outer ones for `.`/`!` resolution.

**Syntactic complexity: medium.** The parser must track `With` block nesting to correctly associate orphan `.member` and `!key` references with their governing `With` target. An `.member` token at statement start (or in expression position with no left-hand operand) is syntactically distinct from a qualified `object.member` — it lacks a left-hand expression.

**Parsing interactions:**
- `With Forms!frmMain` then `!txtName` inside the block resolves to `Forms!frmMain!txtName` — bang-on-With is valid and common.
- `With Me` then `.Controls("x")` is equivalent to `Me.Controls("x")`.
- Nested: `With Me` containing `With .Recordset` then `.Fields("ID")` resolves against the Recordset, not Me.

**Semantic subtlety:** The With target is evaluated once at block entry. If the expression has side effects or if the referenced object is modified, the target does not re-evaluate.

**Frequency: high in Access** — used extensively in form setup code, report formatting, and DAO operations.

### §5.4.8 Exit Statements

`Exit For`, `Exit Do`, `Exit Sub`, `Exit Function`, `Exit Property`. Each transfers control out of the immediately enclosing construct of the named type.

**Syntactic complexity: low.** The parser should validate nesting (e.g., `Exit For` must be inside a `For` or `For Each`). **Frequency: high.**

### §5.4.9 GoTo — CRITICAL

`GoTo <label>` or `GoTo <line-number>`. Unconditional branch to a labeled statement within the same procedure. Labels are identifiers followed by a colon (`ErrorHandler:`) or numeric line numbers.

**Syntactic complexity: low.** The parser must recognize line labels as statement-level constructs (identifier followed by `:` at statement start) and must not confuse them with dictionary access or other colon-separated constructs (`:` also serves as a statement separator on a single line).

**Frequency: very high in Access.** Virtually every non-trivial procedure uses `GoTo` in the error handling pattern:
```vba
On Error GoTo ErrHandler
    '... body ...
    Exit Sub
ErrHandler:
    '... handle ...
    Resume Next
```

### §5.4.10 GoSub / Return — CRITICAL

`GoSub <label>` jumps to a label within the same procedure, pushing the return address. `Return` pops the address and resumes execution at the statement following the `GoSub`. This is **not** a function call — there is no new stack frame, no parameter passing, and all local variables are shared.

**Syntactic complexity: low.** **Semantic subtlety: medium.** `Return` is context-dependent — in a `GoSub` context it pops the return address; it is unrelated to function return values. The parser need not disambiguate (the keyword is unambiguous), but the AST should clearly distinguish `GoSub`/`Return` from procedure calls.

**Frequency: low-to-moderate.** Found in older Access modules; largely replaced by separate Sub/Function calls in modern code, but still present in legacy systems that a parser must handle.

### §5.4.11 On Error — CRITICAL

Three forms:
- `On Error GoTo <label>` — activates an error handler at the specified label.
- `On Error Resume Next` — suppresses errors; execution continues at the next statement.
- `On Error GoTo 0` — deactivates error handling; errors propagate to the caller.

These are **statements**, not declarations — they execute at the point reached in control flow. Multiple `On Error` statements in a single procedure are valid; the last one executed is active.

**Syntactic complexity: medium.** `On Error` is a two-keyword statement prefix. The parser must not confuse it with `On <expression> GoTo <label-list>` (the computed GoTo form, §5.4.11 note), which is a distinct statement beginning with `On` followed by a numeric expression. Disambiguation: if the token after `On` is `Error`, it is error handling; otherwise, it is computed GoTo/GoSub.

**Frequency: extremely high in Access.** The `On Error GoTo` / `Exit Sub` / `Handler:` / `Resume` pattern is the standard error-handling idiom in every Access procedure of non-trivial complexity.

### §5.4.12 Resume — CRITICAL

Three forms:
- `Resume` (or `Resume 0`) — retries the statement that caused the error.
- `Resume Next` — continues at the statement after the one that caused the error.
- `Resume <label>` — jumps to the specified label.

`Resume` is only valid inside an active error handler. It clears the error state (`Err` object).

**Syntactic complexity: low.** **Semantic subtlety: high.** `Resume` without qualification retries the failing statement, which can create infinite loops if the error condition is not resolved. `Resume <label>` is syntactically identical to `GoTo <label>` but additionally clears the error state — the parser must preserve the keyword identity.

**Frequency: very high** — paired with every `On Error GoTo` handler.

---

## §5.5 I/O and Miscellaneous Statements

**`Stop`** (§5.5): halts execution and enters debug mode. Equivalent to a breakpoint. **`End`** (§5.5): terminates all code execution, resets all module-level variables, and closes files. Both are single-keyword statements.

**`DoEvents`** (§5.5): yields processor to the OS message loop. Common in long-running Access loops to keep the UI responsive.

**`RaiseEvent`** (§5.5.8): `RaiseEvent <event-name>(<args>)`. Fires a declared event (see §6.1). Only valid in the class module that declares the event.

**`Call`** (§5.5): `Call <procedure>(<args>)` is equivalent to `<procedure> <args>`. With `Call`, parentheses around the argument list are required. Without `Call`, parentheses are forbidden for the outermost argument list (though a single argument in parentheses is valid as a value-coercion expression, which is a notorious parsing trap).

**Parsing trap:** `MySub (x)` without `Call` passes `x` by value (the parentheses force expression evaluation, breaking `ByRef`). `MySub x` passes by reference. `Call MySub(x)` passes by reference. This is a syntactic distinction with semantic consequences that the parser must model — specifically, whether the argument list is parenthesized at the call-statement level or whether individual arguments are parenthesized as sub-expressions.

**Syntactic complexity: medium** (for `Call` interaction). **Frequency: `DoEvents` — moderate; `Call` — moderate; `Stop`/`End` — low; `RaiseEvent` — low in Access.**

---

## §5.6 Special Object References

### Me — CRITICAL

`Me` is a keyword that evaluates to a reference to the current instance of the class module, form module, or report module containing the code. It is invalid in standard modules.

In Access, `Me` is overwhelmingly used in form and report code-behind modules:
- `Me.Caption` — form property
- `Me!txtName` — control by name (bang access to Controls default collection)
- `Me.txtName` — also valid (dot access to auto-generated control property)
- `Me.Recordset` — form's bound recordset
- `Me.Dirty` — form's dirty flag

**Syntactic complexity: low** — `Me` is a reserved keyword parsed as a primary expression. **Semantic subtlety: medium.** `Me!ctl` and `Me.ctl` are both valid but resolve differently: `Me!ctl` always goes through the Controls collection; `Me.ctl` resolves to the auto-generated property for that control. At the parse level, both are standard member-access expressions and need no special handling beyond recognizing `Me` as a keyword.

**Frequency: extremely high in Access form/report modules.**

### Nothing, True, False, Null, Empty

- **`Nothing`** — null object reference. Used in `Set obj = Nothing` and `If obj Is Nothing`.
- **`True`** — Boolean, value -1 (all bits set in a 16-bit Integer).
- **`False`** — Boolean, value 0.
- **`Null`** — database null; propagates through expressions (`Null + 1 = Null`). **Access-specific concern:** Null is pervasive because bound controls return Null when empty, and DAO fields contain Null for missing data. `Null` is not `Nothing` and not `Empty`.
- **`Empty`** — uninitialized Variant state; coerces to 0 or `""` depending on context.

All five are keyword-literals. **Syntactic complexity: low.** **Frequency: very high** (especially `Nothing` and `Null` in Access).

---

## §6 Object Model Features

### §6.1 Events

A class module declares an event with `[Public] Event <name>([<param-list>])`. The event is fired with `RaiseEvent <name>(<args>)` from within the same class module. Event parameters may be `ByRef`, allowing the handler to communicate back to the raiser.

Event handler procedures are named `<object-variable>_<event-name>` and are placed in the module that declares the WithEvents variable (§6.2). In Access form/report modules, the form engine auto-generates handler stubs named `<ControlName>_<EventName>` (e.g., `txtName_AfterUpdate`, `Form_Load`).

**Syntactic complexity: low.** `Event` is a declaration keyword. **Frequency: moderate** for custom events; event **handlers** are extremely frequent in Access.

### §6.2 WithEvents — CRITICAL

`Dim <variable> As <ClassName>` combined with `WithEvents`:
```vba
Dim WithEvents btn As CommandButton
```

The `WithEvents` keyword appears in the `Dim` statement. It cannot be combined with `New` (i.e., `Dim WithEvents x As New ClassName` is **invalid** — the object must be instantiated separately with `Set`). The variable must be a specific class type, not `Object` or `Variant`.

Once declared, the declaring module can contain event handler procedures named `<variable>_<event>`. The parser must recognize `WithEvents` as a modifier on variable declarations and understand that it enables the naming convention for event handler procedures.

**Syntactic complexity: low** (keyword in declaration). **Semantic subtlety: medium.** **Frequency: moderate in Access** — used in class modules that manage form/control event sinking, particularly in patterns involving dynamically created controls or centralized event handling classes.

### §6.3 Implements — CRITICAL

`Implements <InterfaceName>` appears at the module level in a class module. The class must then contain procedures for every public member of the interface, named `<InterfaceName>_<MemberName>`:

```vba
Implements ILogger

Private Sub ILogger_Log(msg As String)
    Debug.Print msg
End Sub
```

The implementing methods are typically `Private` — they are not directly callable on the implementing class; they are only accessible through a variable typed as the interface.

**Syntactic complexity: low** (single declaration statement). **Semantic subtlety: high.** The parser must:
1. Record `Implements` declarations at the module level.
2. Recognize that procedures named `Interface_Method` are interface implementations, not arbitrary underscore-named procedures.
3. Understand that the underscore in `ILogger_Log` is the interface-member separator, not part of a single identifier — but syntactically it **is** a single identifier. The parser cannot distinguish `ILogger_Log` from any other identifier containing an underscore without knowing that `ILogger` is an implemented interface.

**Parsing interaction:** Event handlers also use underscore naming (`btnSave_Click`). A procedure named `X_Y` could be an event handler for variable `X` event `Y`, an interface implementation for interface `X` member `Y`, or simply a procedure with an underscore in its name. Disambiguation requires the declaration context (`WithEvents`, `Implements`, or neither).

**Frequency: low-to-moderate in Access** — used in well-architected Access applications employing polymorphism, repository patterns, or strategy patterns. Found more in professional/enterprise Access codebases.

### §6.4 Friend

`Friend Sub|Function|Property <name>` declares a member visible to all modules within the same VBA project but invisible to external references (other Access databases referencing this one, or COM clients).

**Syntactic complexity: low** — `Friend` is an access modifier keyword in a procedure declaration. **Semantic subtlety: low.** **Frequency: low in Access.**

---

## Cross-Cutting Parsing Interactions Summary

| Interaction | Constructs Involved | Complexity |
|---|---|---|
| Assignment ambiguity | `=` as assignment vs. comparison | Context-dependent; statement position disambiguates |
| Call parenthesization | `Call Sub(x)` vs. `Sub x` vs. `Sub(x)` | Three distinct semantics for parentheses; affects ByRef/ByVal |
| Bang vs. dot precedence | `!` and `.` in member chains | `!` binds tighter; must be encoded in grammar |
| With + bang | `With obj` then `!key` | Leading `!` resolves against implicit With target |
| With + dot nesting | Nested `With` blocks | Inner shadows outer for `.`/`!` resolution |
| Set vs. Let + default property | `Set x = y` vs. `x = y` on object variables | `Set` keyword presence determines reference vs. value semantics |
| Default property cascading | `rs!Field` in value context | May involve two levels of default-property dereference |
| On Error vs. On...GoTo | `On Error GoTo` vs. `On n GoTo` | Token after `On` disambiguates |
| Underscore naming | `Interface_Method` vs. `Object_Event` vs. plain name | Requires `Implements`/`WithEvents` context to disambiguate |
| Index vs. call vs. array | `expr(args)` | Syntactically identical; requires type info to resolve |