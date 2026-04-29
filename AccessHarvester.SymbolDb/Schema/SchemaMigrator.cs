using Microsoft.Data.Sqlite;

namespace AccessHarvester.SymbolDb.Schema;

// Creates the full vba-symbol-db schema in an empty database. The schema
// covers the whole 5-session set; Session 1 only writes Modules / Symbols /
// Parameters / BuildRuns. Later sessions populate References, MemberAccesses,
// FormControls, EventBindings, Macros, Tables, Queries, and the AI
// interpretation columns.
public static class SchemaMigrator
{
    public static void CreateSchema(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, ModulesSql);
        ExecuteNonQuery(connection, SymbolsSql);
        ExecuteNonQuery(connection, SymbolsIndexes);
        ExecuteNonQuery(connection, ParametersSql);
        ExecuteNonQuery(connection, ReferencesSql);
        ExecuteNonQuery(connection, MemberAccessesSql);
        ExecuteNonQuery(connection, FormControlsSql);
        ExecuteNonQuery(connection, EventBindingsSql);
        ExecuteNonQuery(connection, MacrosSql);
        ExecuteNonQuery(connection, MacroActionsSql);
        ExecuteNonQuery(connection, EnumValuesSql);
        ExecuteNonQuery(connection, TablesSql);
        ExecuteNonQuery(connection, QueriesSql);
        ExecuteNonQuery(connection, QueriesIndexes);
        ExecuteNonQuery(connection, QueryColumnsSql);
        ExecuteNonQuery(connection, BuildRunsSql);
        ExecuteNonQuery(connection, InterpretationHistorySql);
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private const string ModulesSql = """
        CREATE TABLE IF NOT EXISTS Modules (
          fqn TEXT PRIMARY KEY,
          kind TEXT NOT NULL,
          source_path TEXT NOT NULL,
          encoding TEXT NOT NULL,
          is_deferred INTEGER NOT NULL DEFAULT 0,
          deferred_reason TEXT,
          deferred_companion_fqn TEXT
        );
        """;

    private const string SymbolsSql = """
        CREATE TABLE IF NOT EXISTS Symbols (
          fqn TEXT PRIMARY KEY,
          module_fqn TEXT NOT NULL REFERENCES Modules(fqn),
          name TEXT NOT NULL,
          kind TEXT NOT NULL,
          access TEXT,
          data_type TEXT,
          declared_in_fqn TEXT,
          source_line_start INTEGER,
          source_line_end INTEGER,
          source_body TEXT,
          signature_string TEXT,
          body_hash_md5 TEXT,
          is_dead INTEGER NOT NULL DEFAULT 0,
          dead_code_reason TEXT,
          equivalence_canonical_fqn TEXT,
          Interpretation1 TEXT,
          Justification1 TEXT,
          Confidence1 REAL,
          Agent1 TEXT,
          InterpretedAt1 TEXT,
          Interpretation2 TEXT,
          Justification2 TEXT,
          Confidence2 REAL,
          Agent2 TEXT,
          InterpretedAt2 TEXT,
          Interpretation3 TEXT,
          Justification3 TEXT,
          Confidence3 REAL,
          Agent3 TEXT,
          InterpretedAt3 TEXT,
          human_review_required INTEGER NOT NULL DEFAULT 0,
          human_override_interpretation TEXT
        );
        """;

    private const string SymbolsIndexes = """
        CREATE INDEX IF NOT EXISTS idx_symbols_module ON Symbols(module_fqn);
        CREATE INDEX IF NOT EXISTS idx_symbols_kind ON Symbols(kind);
        CREATE INDEX IF NOT EXISTS idx_symbols_dup ON Symbols(signature_string, body_hash_md5)
          WHERE signature_string IS NOT NULL AND body_hash_md5 IS NOT NULL;
        CREATE INDEX IF NOT EXISTS idx_symbols_dead ON Symbols(is_dead) WHERE is_dead = 1;
        """;

    private const string ParametersSql = """
        CREATE TABLE IF NOT EXISTS Parameters (
          procedure_fqn TEXT NOT NULL REFERENCES Symbols(fqn),
          position INTEGER NOT NULL,
          name TEXT NOT NULL,
          data_type TEXT,
          passing TEXT,
          is_optional INTEGER NOT NULL DEFAULT 0,
          is_paramarray INTEGER NOT NULL DEFAULT 0,
          is_array INTEGER NOT NULL DEFAULT 0,
          default_value TEXT,
          PRIMARY KEY (procedure_fqn, position)
        );
        """;

    private const string ReferencesSql = """
        CREATE TABLE IF NOT EXISTS [References] (
          id INTEGER PRIMARY KEY,
          from_fqn TEXT NOT NULL,
          to_fqn TEXT,
          identifier TEXT NOT NULL,
          kind TEXT NOT NULL,
          caller_kind TEXT,
          source_line INTEGER,
          source_column INTEGER,
          resolution_status TEXT NOT NULL,
          resolution_reason TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_refs_from ON [References](from_fqn);
        CREATE INDEX IF NOT EXISTS idx_refs_to ON [References](to_fqn);
        CREATE INDEX IF NOT EXISTS idx_refs_unresolved ON [References](resolution_status)
          WHERE resolution_status = 'unresolved';
        """;

    private const string MemberAccessesSql = """
        CREATE TABLE IF NOT EXISTS MemberAccesses (
          id INTEGER PRIMARY KEY,
          from_fqn TEXT NOT NULL,
          receiver TEXT NOT NULL,
          path TEXT NOT NULL,
          resolved_target_fqn TEXT,
          source_line INTEGER
        );
        """;

    private const string FormControlsSql = """
        CREATE TABLE IF NOT EXISTS FormControls (
          form_fqn TEXT NOT NULL REFERENCES Modules(fqn),
          control_name TEXT NOT NULL,
          control_type TEXT NOT NULL,
          label_text TEXT,
          control_source TEXT,
          row_source TEXT,
          bound_table_or_query TEXT,
          bound_column TEXT,
          PRIMARY KEY (form_fqn, control_name)
        );
        """;

    private const string EventBindingsSql = """
        CREATE TABLE IF NOT EXISTS EventBindings (
          form_fqn TEXT NOT NULL REFERENCES Modules(fqn),
          control_name TEXT,
          event_name TEXT NOT NULL,
          handler_kind TEXT NOT NULL,
          handler_fqn TEXT,
          handler_expression TEXT,
          PRIMARY KEY (form_fqn, control_name, event_name)
        );
        """;

    private const string MacrosSql = """
        CREATE TABLE IF NOT EXISTS Macros (
          fqn TEXT PRIMARY KEY,
          source_xml TEXT NOT NULL,
          is_dead INTEGER NOT NULL DEFAULT 0,
          Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL, Agent1 TEXT, InterpretedAt1 TEXT,
          Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL, Agent2 TEXT, InterpretedAt2 TEXT,
          Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL, Agent3 TEXT, InterpretedAt3 TEXT,
          human_review_required INTEGER NOT NULL DEFAULT 0,
          human_override_interpretation TEXT
        );
        """;

    private const string MacroActionsSql = """
        CREATE TABLE IF NOT EXISTS MacroActions (
          macro_fqn TEXT NOT NULL REFERENCES Macros(fqn),
          position INTEGER NOT NULL,
          action_name TEXT NOT NULL,
          arguments_json TEXT,
          PRIMARY KEY (macro_fqn, position)
        );
        """;

    private const string EnumValuesSql = """
        CREATE TABLE IF NOT EXISTS EnumValues (
          enum_value_fqn TEXT PRIMARY KEY REFERENCES Symbols(fqn),
          enum_fqn TEXT NOT NULL,
          numeric_value INTEGER
        );
        """;

    private const string TablesSql = """
        CREATE TABLE IF NOT EXISTS Tables (
          fqn TEXT PRIMARY KEY,
          primary_key_column TEXT,
          candidate_label_column TEXT,
          is_lookup_table_score INTEGER,
          is_lookup_table_reason TEXT,
          Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL, Agent1 TEXT, InterpretedAt1 TEXT,
          Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL, Agent2 TEXT, InterpretedAt2 TEXT,
          Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL, Agent3 TEXT, InterpretedAt3 TEXT
        );
        """;

    private const string QueriesSql = """
        CREATE TABLE IF NOT EXISTS Queries (
          fqn TEXT PRIMARY KEY,
          source_sql TEXT NOT NULL,
          normalized_sql TEXT,
          sql_text_hash TEXT,
          sql_clause_hash TEXT,
          is_dead INTEGER NOT NULL DEFAULT 0,
          equivalence_canonical_fqn TEXT,
          Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL, Agent1 TEXT, InterpretedAt1 TEXT,
          Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL, Agent2 TEXT, InterpretedAt2 TEXT,
          Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL, Agent3 TEXT, InterpretedAt3 TEXT
        );
        """;

    private const string QueriesIndexes = """
        CREATE INDEX IF NOT EXISTS idx_queries_text_hash ON Queries(sql_text_hash);
        CREATE INDEX IF NOT EXISTS idx_queries_clause_hash ON Queries(sql_clause_hash);
        """;

    private const string QueryColumnsSql = """
        CREATE TABLE IF NOT EXISTS QueryColumns (
          query_fqn TEXT NOT NULL REFERENCES Queries(fqn),
          position INTEGER NOT NULL,
          output_name TEXT NOT NULL,
          expression_text TEXT,
          bound_table TEXT,
          bound_column TEXT,
          PRIMARY KEY (query_fqn, position)
        );
        """;

    private const string BuildRunsSql = """
        CREATE TABLE IF NOT EXISTS BuildRuns (
          run_id INTEGER PRIMARY KEY,
          started_at TEXT NOT NULL,
          finished_at TEXT,
          orchestrator TEXT NOT NULL,
          session_set TEXT NOT NULL,
          session_number INTEGER NOT NULL,
          symbols_written INTEGER,
          references_written INTEGER,
          interpretations_written INTEGER,
          total_routed_cost_usd REAL,
          notes TEXT
        );
        """;

    private const string InterpretationHistorySql = """
        CREATE TABLE IF NOT EXISTS InterpretationHistory (
          id INTEGER PRIMARY KEY,
          fqn TEXT NOT NULL,
          fqn_kind TEXT NOT NULL,
          opinion_slot INTEGER NOT NULL,
          interpretation TEXT NOT NULL,
          justification TEXT,
          confidence REAL,
          agent TEXT NOT NULL,
          prompt_hash TEXT,
          source_hash_at_time TEXT,
          written_at TEXT NOT NULL,
          build_run_id INTEGER REFERENCES BuildRuns(run_id),
          reason TEXT
        );
        """;
}
