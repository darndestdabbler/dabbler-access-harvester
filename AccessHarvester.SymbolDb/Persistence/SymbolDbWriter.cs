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

    public void Write(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<SymbolRow> symbols,
        IReadOnlyList<ParameterRow> parameters)
    {
        using var transaction = _connection.BeginTransaction();
        WriteModules(modules, transaction);
        WriteSymbols(symbols, transaction);
        WriteParameters(parameters, transaction);
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

    public void Dispose()
    {
        if (_ownsConnection)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
