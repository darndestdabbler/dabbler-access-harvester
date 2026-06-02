# Access Migration to Dabbler Platform: Recommendations & Strategy

## Executive Summary

You asked what aspects of the migration process can be fully scripted versus what *requires* AI agent involvement. While we have robust tooling in both `dabbler-access-harvester` (for extraction and AST parsing) and `dabbler-platform` (for code generation via `Dabbler.CrudSlice`), an Access application is functionally entangled with its UI and runtime state. 

**Conclusion:** We can fully script the generation of the structural foundation (Data layer, REST APIs, and simple CRUD UI), but **we must rely on AI agents to bridge the semantic gaps** in business logic (VBA), complex UI behavior, and Access-specific state management (e.g., `DoCmd`, `Forms!X!Y` bindings).

---

## 1. Should we build a Meta-Script or Playbook?

**Yes.** It is highly advised to create a centralized orchestration script (or a defined CLI pipeline) that glues the two repositories together. 

**Soup-to-Nuts Pipeline Vision:**
1. **Harvest Phase (`dabbler-access-harvester`):** Extract raw Access metadata, parse Jet SQL and VBA, and generate an Intermediate Representation (IR).
2. **Curation Phase:** Generate user-query forms/checklists (object-cleanup, object-use, data-classification) based on the IR. Receive user JSON responses to prune dead code and finalize the schema.
3. **Schema Translation:** A script translates the curated IR and user JSON into `.dabbler-entity.json` definitions.
4. **Scaffolding Phase (`dabbler-platform`):** Run the `Dabbler.CrudSlice` CLI over the generated schemas to auto-generate EF Entities, REST APIs, and Blazor CRUD pages.

Creating this meta-script standardizes the migration, ensures reproducibility, and surfaces exactly where the "gaps" (requiring AI) live.

---

## 2. Feasibility of Fully Scripted Generation

To address your specific targets:

### (c) All REST Services & (d) All Blazor Components
**Verdict: Fully Automatable (for Standard CRUD)**
Yes, we can build scripts utilizing the user-query JSON and HARVESTED tables/queries to generate `.dabbler-entity.json` files. You already have `Dabbler.CrudSlice` which turns these schemas into complete vertical slices (Entity, API, Scaffolding Extensions, Web UI). 
*Limitation:* This only handles standard CRUD operations. Bespoke UI behavior (subforms, dynamic cascading combos, tab controls with VBA state) will require AI enhancement of the generated Blazor components.

### (a) All Queries and Report Sources to SQL Server
**Verdict: Partially Automatable (Requires AI for translation)**
While we have the `JetSql.g4` grammar to parse the queries, Access queries frequently contain Access-specific idioms:
*   Resolving `Forms!MyForm!txtDate` parameters natively coupling the query to UI state.
*   Utilization of Access VBA functions (e.g., `DateAdd`, `Nz()`, or user-defined VBA methods) directly inside the SELECT clause.
*   Translating `TRANSFORM` (Crosstab) queries into SQL Server `PIVOT`.
*Recommendation:* Script the simple `SELECT`/`JOIN`/`WHERE` views mechanically. Route the complex, parameterized, or VBA-coupled statements to an AI agent that receives the AST and the target EF Core/SQL Server context to rewrite them as parameterized Stored Procedures or LINQ logic.

### (b) All VBA Code to C# Code 
**Verdict: Cannot be safely scripted (Requires AI)**
We *do* have structural metadata and an AST (via `Vba.g4`), but we lack **semantic mapping**. 
*   VBA heavily uses `DoCmd` (e.g., `DoCmd.OpenForm`, `DoCmd.RunSQL`), implicit default properties, `Recordset` cursors, and tight coupling to UI events (`Form_Load`, `btnSave_Click`).
*   A modern 3-tier app in `dabbler-platform` uses stateless REST APIs, async database contexts, and Blazor WebAssembly/Server components.
*Recommendation:* A script alone will produce garbage or uncompilable C#. Instead, the script should package the VBA AST, its dependencies, and the target Blazor component context into a prompt. An AI agent is then required to refactor the *intent* of the VBA macro into a C# API endpoint or a Blazor UI event handler.

---

## 3. The Boundary: What MUST Have AI Agent Involvement?

If we build out the scripted pipeline, the script should handle the mechanics, and the AI should handle the reasoning. The following aspects **need** AI:

1. **State & UI Decoupling (VBA to 3-Tier):** Refactoring an Access `Form_Current` event that sets UI visibilities while calculating a default value from a `DLookup()`. An AI must separate the database query (API side) from the UI state mutation (Blazor side).
2. **Access SQL Refactoring:** Migrating queries dependent on the Access Object Model (`Forms!...`) into clean, parameterized SQL Server views or EF LINQ.
3. **Data Normalization Analysis:** Access databases are notoriously denormalized. AI can assist during the curation phase by analyzing the harvested schema and recommending 3rd Normal Form (3NF) relational structures before the `.dabbler-entity.json` schemas are finalized.
4. **Business Logic Extraction:** Identifying which VBA modules represent true business logic versus purely UI navigation logic, and generating pure C# service classes for the former.

## 4. Next Steps / Action Plan

1. **Build the Integration Script:** Write a shell script or pipeline CLI in `dabbler-access-harvester` that outputs `[entity].dabbler-entity.json` artifacts based on harvested table structures.
2. **Connect to `Dabbler.CrudSlice`:** Update the pipeline to loop over these JSON schemas and invoke `tools/Dabbler.CrudSlice` in the `dabbler-platform` repo, automatically generating the foundation.
3. **Build the AI Router Handoffs:** Where the script hits complex Jet SQL or VBA files, have the script generate `.prompt.md` files (or invoke the `ai_router`) to feed the raw source and AST to an AI agent, asking it to complete the generated Blazor component or API service.