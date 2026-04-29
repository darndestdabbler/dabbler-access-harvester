using System;
using System.Collections.Generic;
using AccessHarvester.SymbolDb.Models;
using AccessHarvester.SymbolDb.Schema;
using Microsoft.Data.Sqlite;

namespace AccessHarvester.SymbolDb.Persistence;

// Owns the SQLite connection and persists the rows produced by
// SymbolDbBuilder. Wraps writes in a single transaction so a failed
// run does not leave a half-populated database; the file is checked into
// the repo per the spec, and partial state is harder to triage than a
// clean rebuild.
public sealed class SymbolDbWriter : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    public SymbolDbWriter(string databasePath, bool overwrite = true)
    {
        if (overwrite && System.IO.File.Exists(databasePath))
            System.IO.File.Delete(databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _ownsConnection = true;
        EnableForeignKeys(_connection);
        SchemaMigrator.CreateSchema(_connection);
    }

    public SymbolDbWriter(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        if (_connection.State != System.Data.ConnectionState.Open) _connection.Open();
        _ownsConnection = false;
        EnableForeignKeys(_connection);
        SchemaMigrator.CreateSchema(_connection);
    }

    // SQLite ships with foreign-key enforcement off by default. Each connection
    // must opt in via PRAGMA, otherwise the REFERENCES clauses in the schema
    // are advisory only. Failing to do so was flagged by Session 1's round-1
    // cross-provider verification as a Major correctness gap.
    private static void EnableForeignKeys(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection Connection => _connection;

    // Original Sessions 1+2 overload, kept verbatim for binary compatibility
    // with assemblies compiled before Session 3 added the FormControl /
    // EventBinding / Macro / Query / Table parameters. Delegates to the
    // wider overload below with the new parameters defaulted to null.
    public void Write(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<SymbolRow> symbols,
        IReadOnlyList<ParameterRow> parameters,
        IReadOnlyList<ReferenceRow>? references,
        IReadOnlyList<MemberAccessRow>? memberAccesses)
        => Write(modules, symbols, parameters, references, memberAccesses,
                 formControls: null, eventBindings: null,
                 macros: null, macroActions: null,
                 queries: null, tables: null);

    public void Write(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<SymbolRow> symbols,
        IReadOnlyList<ParameterRow> parameters,
        IReadOnlyList<ReferenceRow>? references = null,
        IReadOnlyList<MemberAccessRow>? memberAccesses = null,
        IReadOnlyList<FormControlRow>? formControls = null,
        IReadOnlyList<EventBindingRow>? eventBindings = null,
        IReadOnlyList<MacroRow>? macros = null,
        IReadOnlyList<MacroActionRow>? macroActions = null,
        IReadOnlyList<QueryRow>? queries = null,
        IReadOnlyList<TableRow>? tables = null)
    {
        using var transaction = _connection.BeginTransaction();
        WriteModules(modules, transaction);
        WriteSymbols(symbols, transaction);
        WriteParameters(parameters, transaction);
        if (references is not null) WriteReferences(references, transaction);
        if (memberAccesses is not null) WriteMemberAccesses(memberAccesses, transaction);
        if (formControls is not null) WriteFormControls(formControls, transaction);
        if (eventBindings is not null) WriteEventBindings(eventBindings, transaction);
        if (macros is not null) WriteMacros(macros, transaction);
        if (macroActions is not null) WriteMacroActions(macroActions, transaction);
        if (queries is not null) WriteQueries(queries, transaction);
        if (tables is not null) WriteTables(tables, transaction);
        transaction.Commit();
    }

    private void WriteModules(IReadOnlyList<ModuleRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO Modules (
              fqn, kind, source_path, encoding, is_deferred, deferred_reason, deferred_companion_fqn
            ) VALUES (
              $fqn, $kind, $source_path, $encoding, $is_deferred, $deferred_reason, $deferred_companion_fqn
            );
            """;
        var pFqn = cmd.CreateParameter(); pFqn.ParameterName = "$fqn"; cmd.Parameters.Add(pFqn);
        var pKind = cmd.CreateParameter(); pKind.ParameterName = "$kind"; cmd.Parameters.Add(pKind);
        var pPath = cmd.CreateParameter(); pPath.ParameterName = "$source_path"; cmd.Parameters.Add(pPath);
        var pEnc = cmd.CreateParameter(); pEnc.ParameterName = "$encoding"; cmd.Parameters.Add(pEnc);
        var pDef = cmd.CreateParameter(); pDef.ParameterName = "$is_deferred"; cmd.Parameters.Add(pDef);
        var pReason = cmd.CreateParameter(); pReason.ParameterName = "$deferred_reason"; cmd.Parameters.Add(pReason);
        var pComp = cmd.CreateParameter(); pComp.ParameterName = "$deferred_companion_fqn"; cmd.Parameters.Add(pComp);

        foreach (var row in rows)
        {
            pFqn.Value = row.Fqn;
            pKind.Value = row.Kind;
            pPath.Value = row.SourcePath;
            pEnc.Value = row.Encoding;
            pDef.Value = row.IsDeferred ? 1 : 0;
            pReason.Value = (object?)row.DeferredReason ?? DBNull.Value;
            pComp.Value = (object?)row.DeferredCompanionFqn ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteSymbols(IReadOnlyList<SymbolRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO Symbols (
              fqn, module_fqn, name, kind, access, data_type, declared_in_fqn,
              source_line_start, source_line_end, source_body, signature_string,
              body_hash_md5, is_dead, dead_code_reason, equivalence_canonical_fqn
            ) VALUES (
              $fqn, $module_fqn, $name, $kind, $access, $data_type, $declared_in_fqn,
              $source_line_start, $source_line_end, $source_body, $signature_string,
              $body_hash_md5, $is_dead, $dead_code_reason, $equivalence_canonical_fqn
            );
            """;
        var pFqn = cmd.CreateParameter(); pFqn.ParameterName = "$fqn"; cmd.Parameters.Add(pFqn);
        var pModule = cmd.CreateParameter(); pModule.ParameterName = "$module_fqn"; cmd.Parameters.Add(pModule);
        var pName = cmd.CreateParameter(); pName.ParameterName = "$name"; cmd.Parameters.Add(pName);
        var pKind = cmd.CreateParameter(); pKind.ParameterName = "$kind"; cmd.Parameters.Add(pKind);
        var pAccess = cmd.CreateParameter(); pAccess.ParameterName = "$access"; cmd.Parameters.Add(pAccess);
        var pType = cmd.CreateParameter(); pType.ParameterName = "$data_type"; cmd.Parameters.Add(pType);
        var pDeclIn = cmd.CreateParameter(); pDeclIn.ParameterName = "$declared_in_fqn"; cmd.Parameters.Add(pDeclIn);
        var pStart = cmd.CreateParameter(); pStart.ParameterName = "$source_line_start"; cmd.Parameters.Add(pStart);
        var pEnd = cmd.CreateParameter(); pEnd.ParameterName = "$source_line_end"; cmd.Parameters.Add(pEnd);
        var pBody = cmd.CreateParameter(); pBody.ParameterName = "$source_body"; cmd.Parameters.Add(pBody);
        var pSig = cmd.CreateParameter(); pSig.ParameterName = "$signature_string"; cmd.Parameters.Add(pSig);
        var pHash = cmd.CreateParameter(); pHash.ParameterName = "$body_hash_md5"; cmd.Parameters.Add(pHash);
        var pDead = cmd.CreateParameter(); pDead.ParameterName = "$is_dead"; cmd.Parameters.Add(pDead);
        var pDeadReason = cmd.CreateParameter(); pDeadReason.ParameterName = "$dead_code_reason"; cmd.Parameters.Add(pDeadReason);
        var pEquiv = cmd.CreateParameter(); pEquiv.ParameterName = "$equivalence_canonical_fqn"; cmd.Parameters.Add(pEquiv);

        foreach (var row in rows)
        {
            pFqn.Value = row.Fqn;
            pModule.Value = row.ModuleFqn;
            pName.Value = row.Name;
            pKind.Value = row.Kind;
            pAccess.Value = (object?)row.Access ?? DBNull.Value;
            pType.Value = (object?)row.DataType ?? DBNull.Value;
            pDeclIn.Value = (object?)row.DeclaredInFqn ?? DBNull.Value;
            pStart.Value = (object?)row.SourceLineStart ?? DBNull.Value;
            pEnd.Value = (object?)row.SourceLineEnd ?? DBNull.Value;
            pBody.Value = (object?)row.SourceBody ?? DBNull.Value;
            pSig.Value = (object?)row.SignatureString ?? DBNull.Value;
            pHash.Value = (object?)row.BodyHashMd5 ?? DBNull.Value;
            pDead.Value = row.IsDead ? 1 : 0;
            pDeadReason.Value = (object?)row.DeadCodeReason ?? DBNull.Value;
            pEquiv.Value = (object?)row.EquivalenceCanonicalFqn ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteParameters(IReadOnlyList<ParameterRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO Parameters (
              procedure_fqn, position, name, data_type, passing,
              is_optional, is_paramarray, is_array, default_value
            ) VALUES (
              $procedure_fqn, $position, $name, $data_type, $passing,
              $is_optional, $is_paramarray, $is_array, $default_value
            );
            """;
        var pProc = cmd.CreateParameter(); pProc.ParameterName = "$procedure_fqn"; cmd.Parameters.Add(pProc);
        var pPos = cmd.CreateParameter(); pPos.ParameterName = "$position"; cmd.Parameters.Add(pPos);
        var pName = cmd.CreateParameter(); pName.ParameterName = "$name"; cmd.Parameters.Add(pName);
        var pType = cmd.CreateParameter(); pType.ParameterName = "$data_type"; cmd.Parameters.Add(pType);
        var pPass = cmd.CreateParameter(); pPass.ParameterName = "$passing"; cmd.Parameters.Add(pPass);
        var pOpt = cmd.CreateParameter(); pOpt.ParameterName = "$is_optional"; cmd.Parameters.Add(pOpt);
        var pParam = cmd.CreateParameter(); pParam.ParameterName = "$is_paramarray"; cmd.Parameters.Add(pParam);
        var pArray = cmd.CreateParameter(); pArray.ParameterName = "$is_array"; cmd.Parameters.Add(pArray);
        var pDefault = cmd.CreateParameter(); pDefault.ParameterName = "$default_value"; cmd.Parameters.Add(pDefault);

        foreach (var row in rows)
        {
            pProc.Value = row.ProcedureFqn;
            pPos.Value = row.Position;
            pName.Value = row.Name;
            pType.Value = (object?)row.DataType ?? DBNull.Value;
            pPass.Value = (object?)row.Passing ?? DBNull.Value;
            pOpt.Value = row.IsOptional ? 1 : 0;
            pParam.Value = row.IsParamArray ? 1 : 0;
            pArray.Value = row.IsArray ? 1 : 0;
            pDefault.Value = (object?)row.DefaultValue ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteReferences(IReadOnlyList<ReferenceRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO [References] (
              from_fqn, to_fqn, identifier, kind, caller_kind,
              source_line, source_column, resolution_status, resolution_reason
            ) VALUES (
              $from_fqn, $to_fqn, $identifier, $kind, $caller_kind,
              $source_line, $source_column, $resolution_status, $resolution_reason
            );
            """;
        var pFrom = cmd.CreateParameter(); pFrom.ParameterName = "$from_fqn"; cmd.Parameters.Add(pFrom);
        var pTo = cmd.CreateParameter(); pTo.ParameterName = "$to_fqn"; cmd.Parameters.Add(pTo);
        var pId = cmd.CreateParameter(); pId.ParameterName = "$identifier"; cmd.Parameters.Add(pId);
        var pKind = cmd.CreateParameter(); pKind.ParameterName = "$kind"; cmd.Parameters.Add(pKind);
        var pCaller = cmd.CreateParameter(); pCaller.ParameterName = "$caller_kind"; cmd.Parameters.Add(pCaller);
        var pLine = cmd.CreateParameter(); pLine.ParameterName = "$source_line"; cmd.Parameters.Add(pLine);
        var pCol = cmd.CreateParameter(); pCol.ParameterName = "$source_column"; cmd.Parameters.Add(pCol);
        var pStatus = cmd.CreateParameter(); pStatus.ParameterName = "$resolution_status"; cmd.Parameters.Add(pStatus);
        var pReason = cmd.CreateParameter(); pReason.ParameterName = "$resolution_reason"; cmd.Parameters.Add(pReason);

        foreach (var row in rows)
        {
            pFrom.Value = row.FromFqn;
            pTo.Value = (object?)row.ToFqn ?? DBNull.Value;
            pId.Value = row.Identifier;
            pKind.Value = row.Kind;
            pCaller.Value = (object?)row.CallerKind ?? DBNull.Value;
            pLine.Value = (object?)row.SourceLine ?? DBNull.Value;
            pCol.Value = (object?)row.SourceColumn ?? DBNull.Value;
            pStatus.Value = row.ResolutionStatus;
            pReason.Value = (object?)row.ResolutionReason ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteMemberAccesses(IReadOnlyList<MemberAccessRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO MemberAccesses (
              from_fqn, receiver, path, resolved_target_fqn, source_line
            ) VALUES (
              $from_fqn, $receiver, $path, $resolved_target_fqn, $source_line
            );
            """;
        var pFrom = cmd.CreateParameter(); pFrom.ParameterName = "$from_fqn"; cmd.Parameters.Add(pFrom);
        var pReceiver = cmd.CreateParameter(); pReceiver.ParameterName = "$receiver"; cmd.Parameters.Add(pReceiver);
        var pPath = cmd.CreateParameter(); pPath.ParameterName = "$path"; cmd.Parameters.Add(pPath);
        var pTarget = cmd.CreateParameter(); pTarget.ParameterName = "$resolved_target_fqn"; cmd.Parameters.Add(pTarget);
        var pLine = cmd.CreateParameter(); pLine.ParameterName = "$source_line"; cmd.Parameters.Add(pLine);

        foreach (var row in rows)
        {
            pFrom.Value = row.FromFqn;
            pReceiver.Value = row.Receiver;
            pPath.Value = row.Path;
            pTarget.Value = (object?)row.ResolvedTargetFqn ?? DBNull.Value;
            pLine.Value = (object?)row.SourceLine ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteFormControls(IReadOnlyList<FormControlRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR IGNORE INTO FormControls (
              form_fqn, control_name, control_type, label_text, control_source,
              row_source, bound_table_or_query, bound_column
            ) VALUES (
              $form_fqn, $control_name, $control_type, $label_text, $control_source,
              $row_source, $bound_table_or_query, $bound_column
            );
            """;
        var pForm = cmd.CreateParameter(); pForm.ParameterName = "$form_fqn"; cmd.Parameters.Add(pForm);
        var pName = cmd.CreateParameter(); pName.ParameterName = "$control_name"; cmd.Parameters.Add(pName);
        var pType = cmd.CreateParameter(); pType.ParameterName = "$control_type"; cmd.Parameters.Add(pType);
        var pLabel = cmd.CreateParameter(); pLabel.ParameterName = "$label_text"; cmd.Parameters.Add(pLabel);
        var pSrc = cmd.CreateParameter(); pSrc.ParameterName = "$control_source"; cmd.Parameters.Add(pSrc);
        var pRow = cmd.CreateParameter(); pRow.ParameterName = "$row_source"; cmd.Parameters.Add(pRow);
        var pBound = cmd.CreateParameter(); pBound.ParameterName = "$bound_table_or_query"; cmd.Parameters.Add(pBound);
        var pCol = cmd.CreateParameter(); pCol.ParameterName = "$bound_column"; cmd.Parameters.Add(pCol);

        foreach (var row in rows)
        {
            pForm.Value = row.FormFqn;
            pName.Value = row.ControlName;
            pType.Value = row.ControlType;
            pLabel.Value = (object?)row.LabelText ?? DBNull.Value;
            pSrc.Value = (object?)row.ControlSource ?? DBNull.Value;
            pRow.Value = (object?)row.RowSource ?? DBNull.Value;
            pBound.Value = (object?)row.BoundTableOrQuery ?? DBNull.Value;
            pCol.Value = (object?)row.BoundColumn ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteEventBindings(IReadOnlyList<EventBindingRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR IGNORE INTO EventBindings (
              form_fqn, control_name, event_name, handler_kind, handler_fqn, handler_expression
            ) VALUES (
              $form_fqn, $control_name, $event_name, $handler_kind, $handler_fqn, $handler_expression
            );
            """;
        var pForm = cmd.CreateParameter(); pForm.ParameterName = "$form_fqn"; cmd.Parameters.Add(pForm);
        var pCtrl = cmd.CreateParameter(); pCtrl.ParameterName = "$control_name"; cmd.Parameters.Add(pCtrl);
        var pEvt = cmd.CreateParameter(); pEvt.ParameterName = "$event_name"; cmd.Parameters.Add(pEvt);
        var pKind = cmd.CreateParameter(); pKind.ParameterName = "$handler_kind"; cmd.Parameters.Add(pKind);
        var pFqn = cmd.CreateParameter(); pFqn.ParameterName = "$handler_fqn"; cmd.Parameters.Add(pFqn);
        var pExpr = cmd.CreateParameter(); pExpr.ParameterName = "$handler_expression"; cmd.Parameters.Add(pExpr);

        foreach (var row in rows)
        {
            pForm.Value = row.FormFqn;
            pCtrl.Value = (object?)row.ControlName ?? DBNull.Value;
            pEvt.Value = row.EventName;
            pKind.Value = row.HandlerKind;
            pFqn.Value = (object?)row.HandlerFqn ?? DBNull.Value;
            pExpr.Value = (object?)row.HandlerExpression ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteMacros(IReadOnlyList<MacroRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR IGNORE INTO Macros (fqn, source_xml, is_dead)
            VALUES ($fqn, $source_xml, $is_dead);
            """;
        var pFqn = cmd.CreateParameter(); pFqn.ParameterName = "$fqn"; cmd.Parameters.Add(pFqn);
        var pXml = cmd.CreateParameter(); pXml.ParameterName = "$source_xml"; cmd.Parameters.Add(pXml);
        var pDead = cmd.CreateParameter(); pDead.ParameterName = "$is_dead"; cmd.Parameters.Add(pDead);

        foreach (var row in rows)
        {
            pFqn.Value = row.Fqn;
            pXml.Value = row.SourceXml;
            pDead.Value = row.IsDead ? 1 : 0;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteMacroActions(IReadOnlyList<MacroActionRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR IGNORE INTO MacroActions (macro_fqn, position, action_name, arguments_json)
            VALUES ($macro_fqn, $position, $action_name, $arguments_json);
            """;
        var pMacro = cmd.CreateParameter(); pMacro.ParameterName = "$macro_fqn"; cmd.Parameters.Add(pMacro);
        var pPos = cmd.CreateParameter(); pPos.ParameterName = "$position"; cmd.Parameters.Add(pPos);
        var pName = cmd.CreateParameter(); pName.ParameterName = "$action_name"; cmd.Parameters.Add(pName);
        var pArgs = cmd.CreateParameter(); pArgs.ParameterName = "$arguments_json"; cmd.Parameters.Add(pArgs);

        foreach (var row in rows)
        {
            pMacro.Value = row.MacroFqn;
            pPos.Value = row.Position;
            pName.Value = row.ActionName;
            pArgs.Value = (object?)row.ArgumentsJson ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteQueries(IReadOnlyList<QueryRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR IGNORE INTO Queries (
              fqn, source_sql, normalized_sql, sql_text_hash, sql_clause_hash,
              is_dead, equivalence_canonical_fqn
            ) VALUES (
              $fqn, $source_sql, $normalized_sql, $sql_text_hash, $sql_clause_hash,
              $is_dead, $equivalence_canonical_fqn
            );
            """;
        var pFqn = cmd.CreateParameter(); pFqn.ParameterName = "$fqn"; cmd.Parameters.Add(pFqn);
        var pSql = cmd.CreateParameter(); pSql.ParameterName = "$source_sql"; cmd.Parameters.Add(pSql);
        var pNorm = cmd.CreateParameter(); pNorm.ParameterName = "$normalized_sql"; cmd.Parameters.Add(pNorm);
        var pHash = cmd.CreateParameter(); pHash.ParameterName = "$sql_text_hash"; cmd.Parameters.Add(pHash);
        var pClause = cmd.CreateParameter(); pClause.ParameterName = "$sql_clause_hash"; cmd.Parameters.Add(pClause);
        var pDead = cmd.CreateParameter(); pDead.ParameterName = "$is_dead"; cmd.Parameters.Add(pDead);
        var pEquiv = cmd.CreateParameter(); pEquiv.ParameterName = "$equivalence_canonical_fqn"; cmd.Parameters.Add(pEquiv);

        foreach (var row in rows)
        {
            pFqn.Value = row.Fqn;
            pSql.Value = row.SourceSql;
            pNorm.Value = (object?)row.NormalizedSql ?? DBNull.Value;
            pHash.Value = (object?)row.SqlTextHash ?? DBNull.Value;
            pClause.Value = (object?)row.SqlClauseHash ?? DBNull.Value;
            pDead.Value = row.IsDead ? 1 : 0;
            pEquiv.Value = (object?)row.EquivalenceCanonicalFqn ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteTables(IReadOnlyList<TableRow> rows, SqliteTransaction transaction)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT OR IGNORE INTO Tables (
              fqn, primary_key_column, candidate_label_column,
              is_lookup_table_score, is_lookup_table_reason
            ) VALUES (
              $fqn, $primary_key_column, $candidate_label_column,
              $is_lookup_table_score, $is_lookup_table_reason
            );
            """;
        var pFqn = cmd.CreateParameter(); pFqn.ParameterName = "$fqn"; cmd.Parameters.Add(pFqn);
        var pPk = cmd.CreateParameter(); pPk.ParameterName = "$primary_key_column"; cmd.Parameters.Add(pPk);
        var pLabel = cmd.CreateParameter(); pLabel.ParameterName = "$candidate_label_column"; cmd.Parameters.Add(pLabel);
        var pScore = cmd.CreateParameter(); pScore.ParameterName = "$is_lookup_table_score"; cmd.Parameters.Add(pScore);
        var pReason = cmd.CreateParameter(); pReason.ParameterName = "$is_lookup_table_reason"; cmd.Parameters.Add(pReason);

        foreach (var row in rows)
        {
            pFqn.Value = row.Fqn;
            pPk.Value = (object?)row.PrimaryKeyColumn ?? DBNull.Value;
            pLabel.Value = (object?)row.CandidateLabelColumn ?? DBNull.Value;
            pScore.Value = (object?)row.IsLookupTableScore ?? DBNull.Value;
            pReason.Value = (object?)row.IsLookupTableReason ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        if (_ownsConnection)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
