# VBA Parser and Access Migration Harvester
## Full Implementation Plan — Clean-Room from MS-VBAL Specification

**Purpose:** Drive a multi-session Claude Code effort to build a production-quality VBA parser, symbol resolver, and Access metadata harvester. Output is structured JSON specifications that downstream tooling consumes to generate Blazor/Razor migration artifacts.

**Context:** Hundreds of Access databases targeted for migration. State government environment with long-term maintenance, audit, and documentation requirements. Clean-room implementation from Microsoft's [MS-VBAL] specification — no derivation from Rubberduck or other GPL-licensed sources.

**Estimated total effort (AI-led):** 35–55 working days across all phases. Plan for 8–12 elapsed weeks at typical pace, with significant variance based on edge case density in real codebases.

---

## Table of Contents

1. [Project Foundations](#phase-0-project-foundations)
2. [VBA Language Reference Study and Grammar](#phase-1-vba-language-reference-study-and-grammar)
3. [Parser Infrastructure](#phase-2-parser-infrastructure)
4. [Symbol Resolution and Scope Analysis](#phase-3-symbol-resolution-and-scope-analysis)
5. [Access Object Model Integration](#phase-4-access-object-model-integration)
6. [JET SQL Parser](#phase-5-jet-sql-parser)
7. [Migration Enrichment](#phase-6-migration-enrichment)
8. [Output Schema and Downstream Interfaces](#phase-7-output-schema-and-downstream-interfaces)
9. [Testing and Quality Assurance](#phase-8-testing-and-quality-assurance)
10. [Appendices](#appendices)

---

## Project Goals

The harvester must:

- **Parse VBA correctly** for the constructs actually used in Access databases (not the entire MS-VBAL universe, but a defensible subset).
- **Resolve references** to ground every identifier in the code to its declaration — controls, procedures, fields, and Access object model members.
- **Extract metadata** from `.accdb`/`.mdb` files: forms, reports, queries, table schema, relationships, ribbons, switchboards.
- **Bridge VBA and metadata** so that `Me.txtCustomerName` in a form module resolves to the actual control on that form.
- **Produce structured output** in a versioned JSON schema that downstream tooling consumes.
- **Flag what it cannot handle** explicitly rather than silently producing incorrect results.
- **Be maintainable** across years and multiple developers, with clean architecture and comprehensive tests.

The harvester is **not** responsible for:

- Generating Blazor/Razor code (that is downstream tooling).
- Migrating data (separate concern).
- Translating VBA business logic to C# (downstream, AI-assisted, with human review).
- Replacing Access for development purposes (the harvester is read-only on `.accdb` files).

---

## Phase 0: Project Foundations

**Estimated effort:** 2–3 days

This phase establishes the infrastructure that every subsequent phase depends on. Skipping or rushing it leads to thrashing later.

### 0.1 Test Corpus Assembly

Assemble a representative corpus of VBA code samples from real Access databases.

**Sources:**
- A representative sample of 10–20 Access databases from the migration scope, chosen to cover different ages, complexity levels, and originating departments.
- Northwind Developer Edition (modern reference).
- Northwind 2007 Template (older reference for legacy patterns).
- Access samples shipped with Office documentation.
- Hand-curated edge cases drawn from MS-VBAL.

**Deliverable:** A directory of `.accdb` files plus extracted VBA modules as `.bas`/`.cls`/`.frm` files, organized by source database. Each file has metadata describing its source, version, and notable constructs.

**Acceptance criteria:** Corpus exercises every construct listed in the MS-VBAL coverage matrix (Phase 1.2) at least once.

### 0.2 Verification Harness

Build a test runner that takes a parser implementation and runs it against the entire corpus, reporting parse success rate, parse failures with locations, and parse-tree statistics.

**Components:**
- Command-line runner that accepts a parser binary and corpus path.
- Output formats: human-readable summary, machine-readable JSON for CI integration, detailed log for failure analysis.
- Diff mode: compares two parser versions on the same corpus and reports regressions.
- Performance mode: measures parse time per file and aggregate throughput.

**Acceptance criteria:** Harness can be invoked from CI, produces stable output suitable for regression detection, completes a full corpus run in under 5 minutes for a typical corpus size.

### 0.3 Initial Output Schema Design

Draft the JSON schema the harvester will produce. Will be refined throughout the project but must exist in skeletal form from day one to anchor downstream design.

**Top-level structure:**
```
- DatabaseManifest: identifies the .accdb source, version, harvester version
- TableSchemas: definitions, relationships, indexes
- QuerySpecifications: saved queries with parsed structure
- FormSpecifications: forms with controls, properties, event handlers
- ReportSpecifications: reports similarly
- ModuleSpecifications: standard and class modules
- ProcedureSpecifications: every procedure with signature, dependencies, classification
- DependencyGraph: cross-references across all entities
- UnresolvedItems: explicit list of things the harvester could not fully process
```

**Deliverable:** JSON Schema file (`harvester-schema.v0.json`) that validates example output. Initial version is intentionally incomplete; will iterate.

### 0.4 Project Setup and Tooling

Establish the development environment.

**Decisions to make:**
- Language for the harvester: C# (.NET 8+) is the natural choice given Access tooling and team alignment.
- Build system: standard `dotnet` solution with multiple projects for each major component.
- Test framework: xUnit with FluentAssertions for readable test code.
- ANTLR runtime: official ANTLR 4 C# runtime (BSD-3 licensed).
- DAO/ACE access: Microsoft.Office.Interop.Access plus Microsoft.ACE.OLEDB for reading `.accdb` files.
- Logging: Serilog with structured logging from day one.
- Source control: Git with a clear branching strategy (one branch per phase or component).

**Project layout:**
```
src/
  Harvester.Core/          - shared types, interfaces, JSON schema models
  Harvester.Vba.Grammar/   - ANTLR grammar files (.g4)
  Harvester.Vba.Parser/    - generated parser + AST layer
  Harvester.Vba.Resolver/  - symbol table, scope analysis, reference resolution
  Harvester.Sql.Grammar/   - JET SQL grammar
  Harvester.Sql.Parser/    - generated SQL parser
  Harvester.Access/        - .accdb metadata extraction via DAO/ACE
  Harvester.Pipeline/      - orchestration: ties parsers + metadata together
  Harvester.Cli/           - command-line entry point
tests/
  [project name].Tests/    - one test project per source project
  Harvester.Integration/   - end-to-end tests against real databases
docs/
  decisions/               - architecture decision records (ADRs)
  schema/                  - JSON schema documentation
  spec-coverage/           - MS-VBAL coverage matrix
```

**Acceptance criteria:** `dotnet build` succeeds, `dotnet test` runs (even if no tests yet pass meaningfully), project skeleton is committed to source control.

---

## Phase 1: VBA Language Reference Study and Grammar

**Estimated effort:** 7–12 days

This is the longest phase by elapsed time but the most foundational. Quality here determines parser quality for years.

### 1.1 MS-VBAL Specification Study

Read [MS-VBAL] (Microsoft's "VBA Language Specification" Open Specification document) and produce a study document.

**The specification covers:**
- Lexical structure (tokens, comments, line continuation, line numbers)
- Module structure (declarations, procedures, types)
- Procedures (Sub, Function, Property)
- Statements (control flow, error handling, declarations)
- Expressions (operator precedence, type coercion, member access)
- Type system (primitives, user-defined types, classes, interfaces, enums)
- Object model interaction (Set vs. Let, default members, late binding)
- Pre-processing directives (`#If`, `#Const`)

**Deliverable:** `docs/spec-study/ms-vbal-summary.md` — a prose summary organized by topic, with notes on each construct's syntactic complexity, semantic subtlety, and likely frequency in Access codebases. Includes explicit citations to MS-VBAL section numbers for traceability.

**Critical constructs to document in detail:**
- Bang operator (`!`) — implicit collection/default member access
- Default property/member access (e.g., `txtBox` meaning `txtBox.Value`)
- `Me` keyword in form/report/class modules
- `Set` versus implicit Let assignment
- Line continuation (`_`)
- Line numbers (legacy support, sometimes still encountered)
- Type-declaration character suffixes (`$`, `%`, `&`, `!`, `#`, `@`)
- Date literals (`#1/1/2024#`)
- Implicit ByRef parameter passing
- `Optional`, `ParamArray`, named arguments
- `Property Get`/`Let`/`Set` triplets
- Events and `WithEvents`
- `Implements` for interfaces
- `On Error` variants and `Resume`
- `GoTo` and `GoSub` (legacy but real)

### 1.2 Coverage Matrix

Build a coverage matrix mapping every grammar production in MS-VBAL to:
- Section reference in spec
- Whether the construct appears in test corpus
- Whether the construct appears in migration scope
- Priority (must-handle, should-handle, can-defer, won't-handle)
- Notes on parsing difficulty

**Deliverable:** `docs/spec-coverage/ms-vbal-coverage.csv` (or a Markdown table). Used as the checklist for grammar development.

### 1.3 Hazard List from Real Codebases

Without Rubberduck's bug history as a reference, you must discover edge cases empirically. Run the test corpus through trial parsers (initially the corpus is just for human inspection; once you have a draft grammar, automated discovery begins) and catalog every construct that surprises you.

**Process:**
- For each `.bas`/`.cls`/`.frm` file in the corpus, manually review for unusual constructs.
- Tag each oddity with: file location, construct description, specification section that governs it (if any), parsing difficulty estimate.
- Particular attention to: unusual operator combinations, multi-line statements, conditional compilation use, interaction between `With` blocks and `Me`, late-bound COM calls, deprecated syntax.

**Deliverable:** `docs/spec-coverage/hazard-list.md` — a living document, augmented throughout the project as new edge cases are discovered.

### 1.4 ANTLR Grammar Draft

Implement an ANTLR 4 grammar (`Vba.g4`) covering constructs marked must-handle and should-handle in the coverage matrix.

**Approach:**
- Start with lexical rules (tokens). Get whitespace, comments, line continuation, and identifiers right before anything else — these are the foundation.
- Add literal rules: numeric, string, date, boolean, special (`Nothing`, `Empty`, `Null`, `Missing`).
- Add expression grammar bottom-up: primary, member access, call, unary, binary by precedence, assignment.
- Add statement grammar: declarations, control flow, error handling, special statements.
- Add module structure: directives, declarations, procedures.
- Defer complex constructs (events, interfaces, `WithEvents`) until simpler ones are stable.

**Deliverable:** `Vba.g4` lexer + parser grammar, with comments documenting each rule's purpose and any deviations from MS-VBAL. Generated parser compiles and runs against simple inputs.

**Notes for implementation:**
- VBA is case-insensitive. ANTLR can be configured for this; use the standard pattern with case-insensitive token matching.
- VBA has reserved words that can sometimes be used as identifiers in specific contexts. Handle these via parser rules rather than lexer rules where possible.
- Statement separators are newlines OR colons (`:`). Both must be handled uniformly.
- Pre-processor directives interact with parsing in non-trivial ways; an initial implementation can treat them as opaque pass-throughs.

### 1.5 Grammar Validation Against Test Corpus

Run the draft grammar against the entire corpus via the verification harness.

**Process:**
- Generate parser, run against corpus, measure parse success rate.
- For every failure, classify: grammar bug, missing construct, malformed input, ambiguous input.
- Fix grammar issues in priority order: highest-frequency failures first.
- Iterate until parse success rate exceeds 95% on representative corpus.

**Acceptance criteria:**
- Parse success rate ≥ 95% on representative corpus.
- All failures categorized in a tracked list.
- For any failure marked "won't fix," documented justification.

### 1.6 Edge Case Coverage

Cross-reference the hazard list and ensure each item has a corresponding test case and is correctly handled by the grammar.

**Deliverable:** A test fixture file per hazard item, plus parser tests that assert each is handled correctly.

---

## Phase 2: Parser Infrastructure

**Estimated effort:** 2–3 days

### 2.1 Parser Generation Pipeline

Integrate ANTLR into the build so grammar changes regenerate the parser automatically.

**Setup:**
- ANTLR4.Build.Tasks NuGet package for build-time generation.
- Target: C# parser in the `Harvester.Vba.Parser` project namespace.
- Generated files excluded from source control but build-reproducible.

### 2.2 Error Recovery Strategy

ANTLR's default error recovery is adequate for valid input but produces poor output on malformed code. Real Access databases contain malformed VBA (commented-out code that lost its comment marker, half-finished refactorings, copy-paste errors).

**Strategy:**
- Custom error listener that records errors with location and severity but doesn't abort parsing.
- Synchronization at statement boundaries: on parse error inside a statement, skip to the next statement-terminating token (newline or colon) and resume.
- For severely malformed modules, fall back to "lossy parse" mode that extracts what's parseable and marks the rest as unparseable.

**Deliverable:** Error-tolerant parser that produces a partial AST plus a list of error locations for any input.

### 2.3 AST Layer

ANTLR's parse tree is verbose and tied to the grammar. Wrap it in a clean AST optimized for downstream consumption.

**Approach:**
- Define AST node types for each significant construct: `Module`, `Procedure`, `Statement` variants, `Expression` variants, `Declaration`, `Reference`.
- Visitor that walks the parse tree and produces the AST.
- AST nodes are immutable; transformations produce new trees.
- Each node carries a `SourceLocation` (file, start line/column, end line/column).

**Deliverable:** AST type hierarchy with visitor infrastructure, plus tests asserting that representative VBA inputs produce expected AST structures.

### 2.4 Position Tracking and Source Mapping

Every AST node and resolved declaration must carry source location information. Downstream tooling will constantly need to point back to original source for review, debugging, and migration documentation.

**Deliverable:** `SourceLocation` and `SourceRange` types used throughout, plus tests verifying positions are correctly preserved through transformations.

### 2.5 Pre-processor Handling

VBA's `#If`/`#Else`/`#End If` and `#Const` directives are conditionally evaluated based on host application context.

**Approach:**
- In Phase 2, parse but don't evaluate directives — preserve them in the AST as opaque blocks.
- In Phase 3 (or later), implement evaluation when target host context is known. For Access migration, assume Access host constants (`Access = True`, etc.) per MS-VBAL.

**Deliverable:** AST representation of directives plus tests asserting they're parsed correctly.

---

## Phase 3: Symbol Resolution and Scope Analysis

**Estimated effort:** 7–10 days

This is where the harvester's value is created. Without resolution, you have parse trees; with resolution, you have a queryable semantic graph.

### 3.1 Declaration Model

Define types representing every kind of VBA declaration.

**Declaration types:**
- `ProcedureDeclaration` (Sub, Function, Property Get/Let/Set)
- `VariableDeclaration` (Dim, Public, Private, Static, Const)
- `ParameterDeclaration` (with ByRef/ByVal/Optional/ParamArray modifiers)
- `TypeDeclaration` (user-defined Type ... End Type)
- `EnumDeclaration`
- `ClassDeclaration` (for class modules)
- `EventDeclaration`
- `ImplicitDeclaration` (Variant variables created by use without explicit Dim)

**Each declaration has:**
- Name (case-preserved from source, but compared case-insensitively)
- Scope reference (which scope it belongs to)
- Source location
- Type signature (may be `Variant` if not explicit)
- Modifiers (Public/Private/Friend, Static, etc.)
- References-to-this-declaration collection (populated during resolution)

### 3.2 Scope Tree Construction

Build the hierarchy of scopes that determines name resolution.

**Scope kinds:**
- `ProjectScope` (global, contains all modules)
- `ModuleScope` (one per standard module, class module, form module, report module)
- `ProcedureScope` (one per procedure within a module)
- `WithBlockScope` (nested scope for `With ... End With`)
- `ForEachScope` (for the loop variable in some contexts)

**Scope contains:**
- Parent scope reference
- Child scopes (ordered by source position)
- Declarations defined in this scope
- Source range covered by this scope

**Deliverable:** Scope construction visitor that walks the AST and builds the scope tree, with tests verifying scope nesting matches expected structure for representative inputs.

### 3.3 Reference Resolution

For each identifier reference in the AST, walk the scope tree to find the matching declaration.

**Resolution algorithm (simplified):**
1. Start at the scope containing the reference.
2. Look for a declaration with matching name (case-insensitive) in this scope.
3. If not found, walk up to parent scope, repeat.
4. If reached project scope without match, check for Access object model members (Forms, Reports, application-level functions).
5. If still not found, check VBA built-in functions (Format, IIf, Nz, etc.).
6. If still not found, mark as unresolved.

**Special cases:**
- `Me` in a form/report/class module resolves to the module's class.
- Bang operator (`!`) implies default member access on the preceding expression.
- Default property access: `txtBox = "value"` may mean `txtBox.Value = "value"`.
- Member access through subform/subreport controls navigates from the control to the contained form/report.

**Deliverable:** Resolver that processes a parsed module and returns a `ResolvedModule` containing the AST plus annotations linking each reference to its declaration (or marking unresolved).

### 3.4 With-Block Flattening

Inside `With expr ... End With`, statements like `.Member` and `!Member` use `expr` as their implicit qualifier.

**Approach:**
- During scope construction, `With` blocks introduce a special scope with a "this expression" reference.
- During resolution, when a reference begins with `.` or `!` inside a `With` scope, prepend the `With` expression to form the full reference, then resolve.
- Nested `With` blocks: the innermost one wins for `.` references.

**Deliverable:** Tests verifying With-block flattening for representative inputs, including nested With blocks and With blocks containing form references.

### 3.5 Default Member Resolution

VBA has pervasive implicit default property access. Without resolution, code like `txt = rs("Field")` is ambiguous about what `txt` is and what `rs("Field")` evaluates to.

**Approach:**
- Maintain a registry of types and their default members.
- For Access-relevant types: TextBox/ComboBox/etc. default to `.Value`, Recordset's `Fields(name)` default-member returns the field's value.
- During resolution, when a member access is missing on an expression of a typed value, insert the implicit default member into the resolved AST.

**Deliverable:** Default member resolver with registry covering Access object model and VBA standard types, plus tests on representative implicit-access patterns.

### 3.6 Type Inference

Many VBA variables are `Variant` either explicitly or by omission. Type inference improves resolution accuracy and downstream translation quality.

**Approach (lightweight):**
- For each Variant, look at assignment patterns: if assigned only string literals/string-typed expressions, infer string. If only numeric, infer numeric. If always set from `Set rs = ...` where rs is a Recordset, infer Recordset.
- Track confidence level: high (consistent type), medium (mostly one type with some Variant uses), low (true Variant).
- Inferred types annotate the AST but don't replace the original declaration — they're hints for downstream translation.

**Deliverable:** Type inference pass with confidence scoring. Don't overinvest here; AI-assisted translation downstream can use these hints without depending on them being exact.

### 3.7 Cross-Reference Index

Once resolution is complete, invert the reference→declaration map to produce declaration→references.

**Deliverable:** Query API: `GetReferencesTo(declaration) -> IEnumerable<Reference>`. Used extensively by migration enrichment to determine impact of changes.

---

## Phase 4: Access Object Model Integration

**Estimated effort:** 3–5 days

The VBA resolver alone cannot resolve form/report/control references because those declarations don't exist in VBA source — they exist in `.accdb` design metadata. This phase bridges the two.

### 4.1 Form Metadata Extraction

Extract form definitions from `.accdb` files via DAO/ACE.

**Per form, extract:**
- Form name
- Module name (the VBA class module backing the form)
- Record source (table name or SQL string)
- Filter, Order By, default view properties
- Control collection (every control with type, name, properties)
- Event property mappings (which controls have which events wired)
- Sub-forms and their containing controls

**Deliverable:** `FormMetadata` records for every form in a database, plus tests against Northwind verifying expected forms are extracted with expected control collections.

### 4.2 Report Metadata Extraction

Same as forms but for reports. Reports have grouping/sorting metadata that forms don't.

**Per report, extract:**
- Everything in form metadata, plus:
- Group levels with grouping fields, sort order, header/footer flags
- Sub-reports and their containing controls
- Page setup properties

### 4.3 Query and Table Metadata

Extract saved query and table definitions.

**Per query, extract:**
- Name, SQL text, query type (SELECT, MAKE TABLE, UPDATE, DELETE, INSERT, CROSSTAB)
- Parameters (declared via PARAMETERS clause or implicit `[Forms]![X]` references)
- Output column metadata if available

**Per table, extract:**
- Name, columns with types/sizes/nullability
- Primary key
- Indexes
- Relationships (foreign keys)
- Validation rules (table-level and column-level)
- Default values

**Deliverable:** `QueryMetadata` and `TableMetadata` records, plus relationship graph.

### 4.4 Symbol Table Integration

Inject the Access metadata into the VBA resolver's symbol table so that references to controls, forms, and reports resolve correctly.

**Approach:**
- Each form module's class scope is populated with synthetic declarations for every control on the form.
- The project scope is populated with synthetic declarations for every form and report (via `Forms` and `Reports` collections).
- The class hierarchy reflects subform navigation: `subformControl.Form` resolves to the contained form's class.

**Deliverable:** Resolver tests demonstrating that VBA code referencing form controls resolves correctly when the symbol table is populated with form metadata.

### 4.5 Type Library Integration

Beyond the Access object model, VBA references DAO, ADO, Office, and potentially other type libraries. Full type-library introspection is a large undertaking.

**Pragmatic approach:**
- Hand-curate a registry of the most common members for DAO (Recordset, Field, Database, QueryDef), ADO (Recordset, Connection, Command), and Office (FileDialog, etc.).
- For types not in the registry, mark references as "external — type unknown" rather than failing.
- Defer full type-library introspection to a later iteration if needed.

**Deliverable:** Curated type library registry with the most common members; resolver gracefully degrades for unknown types.

---

## Phase 5: JET SQL Parser

**Estimated effort:** 3–5 days

VBA strings containing SQL must be parsed to extract their structure. JET/ACE SQL is a distinct dialect with its own quirks.

### 5.1 JET SQL Grammar

ANTLR grammar for the JET SQL dialect. Smaller scope than VBA grammar — mostly SELECT, with limited UPDATE/INSERT/DELETE.

**Constructs to handle:**
- SELECT with all the usual clauses (WHERE, GROUP BY, HAVING, ORDER BY)
- JOIN variants (INNER, LEFT, RIGHT — JET uses SQL-89-ish syntax)
- Subqueries
- Parameter brackets (`[Forms]![frmX]![ctlY]` and named parameters like `[Enter ID]`)
- IIf, Switch, Choose in SELECT lists
- Domain functions (DLookup, DSum, etc.) called from SQL
- TRANSFORM ... PIVOT (CROSSTAB queries)
- Identifier escaping with brackets (`[Field With Spaces]`)
- Date literals (`#1/1/2024#`)
- `&` for string concatenation (vs. `||` in standard SQL)

**Deliverable:** `JetSql.g4` with parser tests against representative queries from the corpus.

### 5.2 SQL Parser Infrastructure

Same as VBA parser infrastructure: error recovery, AST layer, position tracking. Smaller in scope.

### 5.3 Form-Reference Detection in SQL

Identify `[Forms]![X]![Y]` and similar patterns inside SQL ASTs and link them to resolved form-control references via the Access metadata.

**Deliverable:** SQL AST nodes representing form references are annotated with their resolved control declarations.

### 5.4 Dynamic SQL Analysis

For SQL strings constructed via VBA concatenation, perform intra-procedural data-flow analysis to determine candidate query templates.

**Approach:**
- For each `strSql = ...` assignment in VBA, walk backward through the procedure tracking string-typed variables.
- For each concatenation, attempt to resolve constants and form-control references; mark unknowns as wildcards.
- Output: a set of candidate query templates with parameters identified, or an "unanalyzable" marker for cases too dynamic to resolve.

**Deliverable:** Dynamic SQL analyzer with reasonable coverage of common patterns. Failures route to manual review queue rather than producing wrong results.

---

## Phase 6: Migration Enrichment

**Estimated effort:** 7–10 days

This phase produces the structured outputs that downstream Blazor migration tooling consumes.

### 6.1 Query Abstraction Generation

For each query (saved or inline), produce a structured specification suitable for translation to a C# repository method.

**Specification includes:**
- Method name (derived from query name or context)
- Parameters (with types resolved from form-control references or table column types)
- Return shape (columns from SELECT list with inferred types)
- Filter predicates (WHERE clause as structured tree)
- Sort order
- JOINs as structured navigation
- Special markers: aggregations, grouping, distinct, top N

### 6.2 Form Component Specification

For each form, produce a specification for the corresponding Blazor component.

**Includes:**
- Component name (derived from form name)
- Data source binding (record source as a query reference)
- Control specifications (each control with type, binding, properties relevant to UI generation)
- Event handler references (each event handler classified — see 6.3)
- Subform relationships
- Layout hints (control positions, sizes, sections)
- Validation rules (extracted from BeforeUpdate handlers and table validation rules)

### 6.3 Event Handler Classification

For each event handler, classify by purpose to drive translation strategy.

**Classifications:**
- **Validation:** modifies `Cancel` flag based on field values; translates to Blazor validators.
- **Cascading update:** modifies other controls based on this control's value; translates to component state updates.
- **Filter/search:** modifies form's RecordSource or Filter; translates to query parameter changes.
- **Navigation:** opens other forms/reports; translates to routing.
- **Side effect:** writes to database, calls external code; translates case-by-case with human review.
- **Calculation:** computes derived values; translates to computed properties.
- **Formatting/display:** modifies appearance; translates to component CSS/conditional rendering.
- **Mixed:** does multiple things; flagged for human review and decomposition.

**Deliverable:** Classification results attached to each event handler in the output.

### 6.4 Validation Rule Extraction

Extract validation logic as structured rules.

**Sources:**
- Table-level and column-level validation rules (already structured in metadata).
- Form-level validation rules.
- BeforeUpdate event handler patterns (`If IsNull(field) Then Cancel = True`).
- Combo box `LimitToList = True` constraints.

**Output format:**
- Required field rules
- Range constraints (min/max for numbers, dates)
- Pattern constraints (regex equivalents for input masks)
- Custom predicate rules (with VBA expression preserved for human review/translation)

### 6.5 RowSource Specification

For each combo box and list box, produce a structured lookup specification.

**Includes:**
- Source type: table, query, value list, field list
- Source SQL (parsed) if applicable
- Bound column index and bound column field
- Display columns (with widths)
- Filtering predicates (especially form-control-driven cascading lookups)

### 6.6 VBA Function Classification

For each user-defined function or sub, classify by translation strategy.

**Classifications:**
- **Pure:** no side effects, no external references; direct C# translation.
- **Data access:** reads tables/queries, no UI; translates to repository or service method.
- **UI-bound:** manipulates forms/controls; becomes Blazor component logic.
- **External I/O:** file system, network, COM; case-by-case with human review.
- **Mixed:** does multiple things; flagged for decomposition.

For each function, additionally compute:
- Cyclomatic complexity (drives review priority)
- Lines of code
- Direct dependencies (called procedures, referenced controls)
- Transitive dependency closure
- Caller list (where this function is invoked from)

### 6.7 Navigation and Workflow Extraction

Reconstruct the application's navigation graph from multiple sources.

**Sources:**
- Custom ribbon XML (`USysRibbons` table)
- Switchboard items (`Switchboard Items` table if present)
- Navigation forms / autoexec macros
- `DoCmd.OpenForm` and `DoCmd.OpenReport` calls in event handlers
- Form properties: `OnLoad`, `OnOpen` events that navigate

**Output:**
- Top-level navigation structure (ribbon tabs, switchboard menus)
- Form-to-form navigation graph with triggers (which control click leads where)
- Entry points (autoexec, default form)

### 6.8 Manual Review Routing

For every construct the harvester cannot fully process, produce a structured "manual review required" record.

**Each record includes:**
- Source location
- Construct description
- Reason for routing to review (unresolved reference, dynamic SQL too complex, classification ambiguous, etc.)
- Surrounding context (enough source to understand the issue)
- Suggested next steps (e.g., "needs developer to confirm intent," "needs AI-assisted translation with full module context")

**Deliverable:** All harvester output includes an `UnresolvedItems` array. Empty array means complete coverage; populated array enumerates everything that needs human attention.

---

## Phase 7: Output Schema and Downstream Interfaces

**Estimated effort:** 1–2 days (interleaved with Phase 6)

### 7.1 JSON Schema Finalization

The schema started in Phase 0.3 evolves throughout the project. By the end of Phase 6, finalize for v1.0.

**Requirements:**
- JSON Schema (Draft 2020-12 or similar) document validates all output.
- Schema is versioned (`harvester-schema.v1.0.json`).
- Backward-compatibility plan: schema changes increment version; downstream tools can request specific versions.
- Documentation describes every field's meaning and provenance.

### 7.2 Schema Examples and Test Fixtures

For every major schema construct, provide an example fixture.

**Deliverables:**
- `examples/northwind-output.json` — full output for Northwind reference database
- `examples/minimal-form.json` — smallest valid form output
- `examples/complex-form.json` — form exercising every feature
- Per-construct example files for documentation

### 7.3 Output Validation

Every harvester run validates its own output against the schema before writing.

**Deliverable:** Validation pass that fails the harvester if output is malformed; ensures schema conformance is enforced rather than aspirational.

### 7.4 Documentation Generation

Generate human-readable documentation alongside JSON.

**Per database, produce:**
- Markdown summary of database structure
- Form-by-form catalog with screenshots (if extractable) and descriptions
- Dependency diagrams (forms-and-reports level, not field level)
- Manual review queue formatted for ticket creation

---

## Phase 8: Testing and Quality Assurance

**Estimated effort:** Integrated throughout; final integration testing 3–5 days

### 8.1 Unit Test Strategy

Per project, comprehensive unit tests covering:
- Each grammar production (positive and negative cases)
- Each resolver scenario (control reference, With block, default member, scope shadowing)
- Each metadata extraction case (form, report, query, table)
- Each enrichment classification

**Coverage target:** 80%+ line coverage on core libraries; lower acceptable for grammar files (ANTLR-generated code is tested via parser tests rather than direct coverage).

### 8.2 Integration Test Strategy

End-to-end tests run the full pipeline against known databases and assert structural properties of output.

**Test cases:**
- Northwind: known-good fixture; output diffs flag regressions.
- Minimal database: smallest valid input.
- Each unique construct from the hazard list: dedicated database exercising it.

### 8.3 Regression Suite from Real Databases

As real databases from the migration scope are processed, capture their output as regression fixtures.

**Process:**
- For each new database successfully processed, snapshot output.
- Periodic CI runs re-process all snapshot databases and diff outputs.
- Any unexpected diff fails CI and requires investigation.

### 8.4 Performance Testing

Hundreds of databases in scope means total processing time matters.

**Acceptance criteria:**
- Median database (100 forms, 200 modules, 500 queries) processes in under 10 minutes.
- Throughput sufficient that the entire scope can be re-processed overnight.
- Memory usage bounded so multiple databases can be processed in parallel without exhausting resources.

### 8.5 Acceptance Criteria for Production Readiness

The harvester is production-ready when:
- All databases in a defined "v1 scope" produce valid output.
- Manual review queue size is below a threshold (e.g., 5% of total constructs requiring human attention).
- Downstream Blazor migration tooling consumes the output successfully on a pilot database.
- Documentation is complete enough that a new developer can run the harvester without consultation.
- CI is green; regression suite passes; performance criteria met.

---

## Appendices

### Appendix A: Access-Specific VBA Constructs Requiring Special Attention

These constructs are common in Access codebases and must be handled correctly.

**Access object model references:**
- `Forms!FormName!ControlName`, `Forms!FormName.Form.RecordSource`
- `Reports!ReportName!ControlName`
- `Forms("FormName")` (collection access syntax)
- `CurrentDb`, `CurrentProject`, `Application`
- `DoCmd.OpenForm`, `DoCmd.OpenReport`, `DoCmd.RunSQL`, `DoCmd.SetWarnings`
- `DLookup`, `DSum`, `DCount`, `DMax`, `DMin`, `DAvg`, `DFirst`, `DLast`
- `Nz()`, `IIf()`, `Switch()`, `Choose()`
- `Eval()` — particularly dangerous; may evaluate dynamic expressions

**Form/report-specific:**
- `Me.Recordset`, `Me.RecordsetClone`
- `Me.Bookmark`, `Me.NewRecord`, `Me.Dirty`
- `Me.Filter`, `Me.FilterOn`, `Me.OrderBy`, `Me.OrderByOn`
- `Me.OpenArgs` (parameters passed via DoCmd.OpenForm)
- Subform navigation: `Me.subformControl.Form.RecordSource`

**Recordset patterns:**
- `Set rs = CurrentDb.OpenRecordset(...)` followed by `Do Until rs.EOF ... rs.MoveNext ... Loop`
- `Set rs = Me.RecordsetClone` for working with a form's underlying data
- `rs.FindFirst`, `rs.AbsolutePosition`, `rs.Bookmark`

### Appendix B: MS-VBAL Coverage Matrix Template

Suggested columns for the coverage matrix:

| Section | Construct | In Corpus? | In Scope? | Priority | Difficulty | Notes |
|---------|-----------|-----------|-----------|----------|------------|-------|
| 5.2.3 | Procedure declaration | ✓ | ✓ | Must | Low | |
| 5.4.2.4 | Bang operator | ✓ | ✓ | Must | High | Implicit member access |
| 5.6.13 | Type-declaration character | ✓ | ✓ | Should | Medium | Legacy syntax |
| ... | ... | ... | ... | ... | ... | ... |

### Appendix C: Recommended Tools and Libraries

- **ANTLR 4** (BSD-3): Parser generation
- **System.Text.Json** (.NET): JSON output
- **JsonSchema.Net** (MIT): Schema validation
- **Microsoft.Office.Interop.Access**: Reading .accdb files (requires Access installed)
- **Microsoft.ACE.OLEDB.16.0**: Alternative Access reader without full Access install
- **Serilog** (Apache 2.0): Structured logging
- **xUnit** (Apache 2.0): Test framework
- **FluentAssertions** (Apache 2.0): Test readability
- **Verify** (MIT): Snapshot testing for parser regression

### Appendix D: Documentation Standards

For state government context, documentation rigor matters.

**Required artifacts:**
- Architecture Decision Records (ADRs) for every significant design choice
- Public API documentation (XML doc comments) on every public type
- README for each project explaining purpose and integration points
- CHANGELOG following Keep a Changelog format
- Schema documentation auto-generated from JSON Schema
- Operations manual: how to run the harvester, interpret output, troubleshoot failures
- Recovery procedures: what to do when a database fails to process

### Appendix E: Session Planning Guide for Claude Code

This plan is structured for execution across multiple Claude Code sessions. Suggested session breakdown:

**Foundational sessions (Phase 0):**
1. Test corpus assembly and harness setup
2. Project skeleton and tooling
3. Initial schema design

**Spec study sessions (Phase 1.1–1.3):**
4. MS-VBAL specification reading and summary
5. Coverage matrix construction
6. Hazard list initial population from corpus

**Grammar sessions (Phase 1.4–1.6):**
7. Lexical grammar (tokens, comments, literals)
8. Expression grammar (precedence, member access, calls)
9. Statement grammar (control flow, declarations)
10. Module grammar (procedures, types, directives)
11. Grammar validation and edge case handling

**Parser infrastructure sessions (Phase 2):**
12. Parser pipeline integration and error recovery
13. AST layer with position tracking

**Resolver sessions (Phase 3):**
14. Declaration model and scope tree
15. Reference resolution algorithm
16. With-block flattening and default member resolution
17. Type inference and cross-reference index

**Access integration sessions (Phase 4):**
18. Form metadata extraction
19. Report, query, and table metadata
20. Symbol table integration with VBA resolver

**SQL parser sessions (Phase 5):**
21. JET SQL grammar and parser
22. Form-reference detection and dynamic SQL analysis

**Enrichment sessions (Phase 6):**
23. Query abstraction generation
24. Form component specification
25. Event handler classification
26. Validation, navigation, and function classification
27. Manual review routing

**Schema and integration sessions (Phase 7–8):**
28. JSON schema finalization and validation
29. Documentation generation
30. End-to-end integration testing
31. Performance optimization
32. Acceptance criteria validation

**Each session should:**
- Begin by reviewing the relevant section of this plan plus any prior session output.
- Have a clearly defined deliverable from this plan.
- End with tests passing for the new capability.
- Update the plan if scope changes or new edge cases are discovered.

### Appendix F: Risk Register

**Technical risks:**
- *VBA edge cases beyond test corpus:* Mitigated by hazard list maintenance and incremental coverage.
- *Performance on large databases:* Mitigated by Phase 8.4 testing and architectural attention to memory usage.
- *Type library coverage gaps:* Mitigated by graceful degradation and curated registry.

**Project risks:**
- *Scope creep into translation/migration:* Mitigated by clear separation in Project Goals; harvester produces specs, downstream tools translate.
- *Specification ambiguity in MS-VBAL:* Mitigated by empirical validation against real corpora.
- *State government compliance requirements emerging late:* Mitigated by documentation discipline from day one.

**Operational risks:**
- *Database files corrupting during processing:* Mitigated by read-only access; never modify source databases.
- *Sensitive data in databases:* Mitigated by output schema excluding actual data values; harvester captures structure, not contents.

---

## Final Notes

This plan represents 35–55 days of AI-assisted development effort to produce a parser and harvester suitable for migrating hundreds of Access databases to Blazor. The investment is justified by the scale of the migration scope.

The plan is intentionally detailed because the alternative — discovering scope, edge cases, and architectural decisions ad hoc during execution — leads to thrash that consumes more total time despite feeling faster initially.

The plan should be treated as a living document. As real Access databases reveal edge cases, as the team learns what works and doesn't, and as downstream tooling needs become clearer, this plan should be updated. Version control the plan alongside the code; review it at the start of each phase; revise it at the end.

The goal is not a perfect parser. The goal is a production-quality harvester that reduces per-database migration cost enough to make the entire portfolio of databases tractable. Optimize for that goal, not for technical completeness.
