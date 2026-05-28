using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Classification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Classification;

public class HeuristicsEngineTests
{
    private static readonly ScanConfig Cfg = ScanConfig.Default();
    private static readonly DateTimeOffset Scanned = new(2026, 5, 28, 18, 0, 0, TimeSpan.Zero);

    private static RawColumn Col(
        string name,
        int ordinal,
        string dataType = "Text",
        bool autoNumber = false,
        bool pk = false,
        bool fk = false,
        RawFkTarget? fkTarget = null,
        bool memo = false,
        bool required = false)
        => new(name, ordinal, dataType, autoNumber, pk, fk, fkTarget,
               IsMemo: memo, IsOleObject: false, IsMultiValue: false, IsRequired: required);

    private static ColumnStatistics Stat(
        string table, string column, int rowCount, int nullCount, int cardinality,
        bool piiValueMatch = false, string? formatPattern = null)
        => new(table, column, rowCount, nullCount, cardinality, piiValueMatch, formatPattern);

    [Fact]
    public void AutoNumber_PK_is_classified_as_surrogate_autonumber_pk()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Person", IsLinkedTable: false, Columns: new[]
            {
                Col("PersonID", 0, "Long", autoNumber: true, pk: true, required: true),
                Col("Notes",    1, "Text"),
            }),
        });
        var stats = new[]
        {
            Stat("Person", "PersonID", 100, 0, 100),
            Stat("Person", "Notes",    100, 0, 80),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);

        var person = Assert.Single(result.Tables);
        var pk = person.Columns.Single(c => c.Name == "PersonID");
        Assert.Equal(ColumnCategory.SurrogateAutonumberPk, pk.GuessedCategory);
        Assert.Contains("autonumber-pk", pk.FiredHeuristics);
    }

    [Fact]
    public void Foreign_key_column_is_classified_as_foreign_key()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Visit", IsLinkedTable: false, Columns: new[]
            {
                Col("VisitID",  0, "Long", autoNumber: true, pk: true),
                Col("PersonID", 1, "Long", fk: true, fkTarget: new RawFkTarget("Person", "PersonID")),
            }),
        });
        var stats = new[]
        {
            Stat("Visit", "VisitID",  100, 0, 100),
            Stat("Visit", "PersonID", 100, 0, 80),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);

        var visit = Assert.Single(result.Tables);
        var fk = visit.Columns.Single(c => c.Name == "PersonID");
        Assert.Equal(ColumnCategory.ForeignKey, fk.GuessedCategory);
        Assert.Contains("fk-declared", fk.FiredHeuristics);
        Assert.NotNull(fk.FkTarget);
        Assert.Equal("Person", fk.FkTarget!.Table);
    }

    [Fact]
    public void PII_name_match_is_classified_as_direct_identifier_pii()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Client", IsLinkedTable: false, Columns: new[]
            {
                Col("ClientID",  0, "Long", autoNumber: true, pk: true),
                Col("FirstName", 1, "Text"),
                Col("LastName",  2, "Text"),
            }),
        });
        var stats = new[]
        {
            Stat("Client", "ClientID",  10, 0, 10),
            Stat("Client", "FirstName", 10, 0,  8),
            Stat("Client", "LastName",  10, 0,  9),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);

        var first = result.Tables[0].Columns.Single(c => c.Name == "FirstName");
        Assert.True(first.PiiNameMatch);
        Assert.Equal(ColumnCategory.DirectIdentifierPii, first.GuessedCategory);
    }

    [Fact]
    public void PII_value_match_alone_lifts_a_cryptic_column_to_PII()
    {
        // Column name does NOT match the PII vocabulary, but value-sampling
        // saw SSN-shaped values.
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Mystery", IsLinkedTable: false, Columns: new[]
            {
                Col("MysteryID", 0, "Long", autoNumber: true, pk: true),
                Col("MisleadingColumnName", 1, "Text"),
            }),
        });
        var stats = new[]
        {
            Stat("Mystery", "MysteryID",            10, 0, 10),
            Stat("Mystery", "MisleadingColumnName", 10, 0,  9, piiValueMatch: true,
                 formatPattern: "###-##-####"),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var col = result.Tables[0].Columns.Single(c => c.Name == "MisleadingColumnName");
        Assert.True(col.PiiValueMatch);
        Assert.Equal(ColumnCategory.DirectIdentifierPii, col.GuessedCategory);
    }

    [Fact]
    public void Quasi_identifier_DOB_gets_quasi_identifier_category()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Client", IsLinkedTable: false, Columns: new[]
            {
                Col("ClientID", 0, "Long", autoNumber: true, pk: true),
                Col("DOB",      1, "Date/Time"),
            }),
        });
        var stats = new[]
        {
            Stat("Client", "ClientID", 100, 0, 100),
            Stat("Client", "DOB",      100, 0,  95),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var dob = result.Tables[0].Columns.Single(c => c.Name == "DOB");
        Assert.Equal(ColumnCategory.QuasiIdentifier, dob.GuessedCategory);
    }

    [Fact]
    public void Memo_column_is_classified_as_free_text_memo()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Client", IsLinkedTable: false, Columns: new[]
            {
                Col("ClientID", 0, "Long", autoNumber: true, pk: true),
                Col("Notes",    1, "Memo", memo: true),
            }),
        });
        var stats = new[]
        {
            Stat("Client", "ClientID", 10, 0, 10),
            Stat("Client", "Notes",    10, 1,  8),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var notes = result.Tables[0].Columns.Single(c => c.Name == "Notes");
        Assert.Equal(ColumnCategory.FreeTextMemo, notes.GuessedCategory);
    }

    [Fact]
    public void Thin_lookup_table_is_marked_as_lookup()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("VisitStatus", IsLinkedTable: false, Columns: new[]
            {
                Col("StatusID",    0, "Long", autoNumber: true, pk: true),
                Col("Description", 1, "Text", required: true),
            }),
        });
        var stats = new[]
        {
            Stat("VisitStatus", "StatusID",    5, 0, 5),
            Stat("VisitStatus", "Description", 5, 0, 5),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var t = result.Tables[0];
        Assert.True(t.LookupGuess);
        Assert.Equal(TableHint.LookupReference, t.TableHint);
        Assert.Equal(1, t.NonKeyNonSystemColumnCount);
        Assert.Contains("lookup-thin-table", t.FiredHeuristics);
    }

    [Fact]
    public void PII_veto_overrides_thin_lookup_classification()
    {
        // Even though Person has only 3 non-key non-system columns,
        // PII signals on the data columns must veto the lookup guess.
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Person", IsLinkedTable: false, Columns: new[]
            {
                Col("PersonID",  0, "Long", autoNumber: true, pk: true),
                Col("FirstName", 1, "Text"),
                Col("LastName",  2, "Text"),
                Col("DOB",       3, "Date/Time"),
            }),
        });
        var stats = new[]
        {
            Stat("Person", "PersonID",  100, 0, 100),
            Stat("Person", "FirstName", 100, 0,  60),
            Stat("Person", "LastName",  100, 0,  80),
            Stat("Person", "DOB",       100, 0,  95),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var t = result.Tables[0];
        Assert.False(t.LookupGuess);
        Assert.Equal(TableHint.EntityPiiBearing, t.TableHint);
        Assert.Contains("pii-veto", t.FiredHeuristics);
    }

    [Fact]
    public void System_audit_columns_need_recurrence_across_tables()
    {
        // LastModifiedBy in one table only is NOT a system column (plan § 3.2).
        var schemaOne = new RawSchema("Test", new[]
        {
            new RawTable("OnlyTable", IsLinkedTable: false, Columns: new[]
            {
                Col("Id",              0, "Long", autoNumber: true, pk: true),
                Col("LastModifiedBy",  1, "Text"),
            }),
        });
        var statsOne = new[]
        {
            Stat("OnlyTable", "Id",             10, 0, 10),
            Stat("OnlyTable", "LastModifiedBy", 10, 0,  5),
        };
        var singleResult = HeuristicsEngine.Run(schemaOne, statsOne, Cfg, Scanned);
        var col = singleResult.Tables[0].Columns.Single(c => c.Name == "LastModifiedBy");
        Assert.NotEqual(ColumnCategory.SystemAudit, col.GuessedCategory);

        // Same column name across TWO tables IS a system column.
        var schemaTwo = new RawSchema("Test", new[]
        {
            new RawTable("Person", IsLinkedTable: false, Columns: new[]
            {
                Col("PersonID",       0, "Long", autoNumber: true, pk: true),
                Col("LastModifiedBy", 1, "Text"),
            }),
            new RawTable("Visit",  IsLinkedTable: false, Columns: new[]
            {
                Col("VisitID",        0, "Long", autoNumber: true, pk: true),
                Col("LastModifiedBy", 1, "Text"),
            }),
        });
        var statsTwo = new[]
        {
            Stat("Person", "PersonID",       10, 0, 10),
            Stat("Person", "LastModifiedBy", 10, 0,  5),
            Stat("Visit",  "VisitID",        10, 0, 10),
            Stat("Visit",  "LastModifiedBy", 10, 0,  5),
        };
        var dualResult = HeuristicsEngine.Run(schemaTwo, statsTwo, Cfg, Scanned);
        var personCol = dualResult.Tables.Single(t => t.Name == "Person").Columns.Single(c => c.Name == "LastModifiedBy");
        var visitCol  = dualResult.Tables.Single(t => t.Name == "Visit").Columns.Single(c  => c.Name == "LastModifiedBy");
        Assert.Equal(ColumnCategory.SystemAudit, personCol.GuessedCategory);
        Assert.Equal(ColumnCategory.SystemAudit, visitCol.GuessedCategory);
    }

    [Fact]
    public void Linked_tables_skip_lookup_detection_and_carry_linked_hint()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("ExternalRef", IsLinkedTable: true, Columns: new[]
            {
                Col("Id",   0, "Long", pk: true),
                Col("Name", 1, "Text"),
            }),
        });
        var stats = new[]
        {
            Stat("ExternalRef", "Id",   100, 0, 100),
            Stat("ExternalRef", "Name", 100, 0,  60),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var t = result.Tables[0];
        Assert.True(t.IsLinkedTable);
        Assert.False(t.LookupGuess);
        Assert.Equal(TableHint.Linked, t.TableHint);
        Assert.Contains("linked-table", t.FiredHeuristics);
    }

    [Fact]
    public void Referenced_by_fk_count_is_recorded_but_does_not_drive_lookup_guess()
    {
        // Per plan § 3.1: referenced-by is informational ONLY. A thin lookup
        // table referenced by one parent should still be a lookup, and a
        // PII-bearing table referenced by many parents should still NOT be.
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("Person", IsLinkedTable: false, Columns: new[]
            {
                Col("PersonID",  0, "Long", autoNumber: true, pk: true),
                Col("FirstName", 1, "Text"),
                Col("LastName",  2, "Text"),
            }),
            new RawTable("Client", IsLinkedTable: false, Columns: new[]
            {
                Col("ClientID",  0, "Long", autoNumber: true, pk: true),
                Col("PersonID",  1, "Long", fk: true, fkTarget: new RawFkTarget("Person", "PersonID")),
            }),
            new RawTable("Practitioner", IsLinkedTable: false, Columns: new[]
            {
                Col("PractID",   0, "Long", autoNumber: true, pk: true),
                Col("PersonID",  1, "Long", fk: true, fkTarget: new RawFkTarget("Person", "PersonID")),
            }),
        });
        var stats = new[]
        {
            Stat("Person",       "PersonID",  100, 0, 100),
            Stat("Person",       "FirstName", 100, 0,  60),
            Stat("Person",       "LastName",  100, 0,  80),
            Stat("Client",       "ClientID",   10, 0,  10),
            Stat("Client",       "PersonID",   10, 0,  10),
            Stat("Practitioner", "PractID",     5, 0,   5),
            Stat("Practitioner", "PersonID",    5, 0,   5),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var person = result.Tables.Single(t => t.Name == "Person");
        Assert.Equal(2, person.ReferencedByFkCount); // informational
        Assert.False(person.LookupGuess);            // PII veto wins
    }

    [Fact]
    public void Lookup_table_payload_column_is_promoted_to_lookup_reference()
    {
        // Plan § 3.5: a column in an identified lookup/reference table gets
        // the `lookup-reference` category by default, so the sanitizer's
        // preserve behavior applies. Without this promotion the column
        // would land in `generic-non-sensitive`, which has the same
        // default masking but is the wrong category-of-record.
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("VisitStatus", IsLinkedTable: false, Columns: new[]
            {
                Col("StatusID",    0, "Long", autoNumber: true, pk: true),
                Col("Description", 1, "Text", required: true),
            }),
        });
        var stats = new[]
        {
            Stat("VisitStatus", "StatusID",    5, 0, 5),
            Stat("VisitStatus", "Description", 5, 0, 5),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var t = result.Tables[0];
        Assert.True(t.LookupGuess);
        var description = t.Columns.Single(c => c.Name == "Description");
        Assert.Equal(ColumnCategory.LookupReference, description.GuessedCategory);
        Assert.Contains("lookup-table-payload", description.FiredHeuristics);
    }

    [Fact]
    public void Lookup_payload_promotion_includes_format_pattern_sensitive_columns()
    {
        // R2 fix: a payload column whose value format detector recognised a
        // stable shape lands as `sensitive-non-pii` before the post-pass.
        // The post-pass must still promote it to `lookup-reference` once
        // the table is identified as a lookup; otherwise fixed-format code
        // tables (e.g. ID + "CODE-####" pattern) end up mis-categorised.
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("StatusCode", IsLinkedTable: false, Columns: new[]
            {
                Col("CodeID",    0, "Long", autoNumber: true, pk: true),
                Col("CodeValue", 1, "Text", required: true),
            }),
        });
        var stats = new[]
        {
            Stat("StatusCode", "CodeID",    7, 0, 7),
            // Stable format pattern → ClassifyColumn lands on `sensitive-non-pii`.
            Stat("StatusCode", "CodeValue", 7, 0, 7, formatPattern: "CODE-####"),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var t = result.Tables[0];
        Assert.True(t.LookupGuess);
        var payload = t.Columns.Single(c => c.Name == "CodeValue");
        Assert.Equal(ColumnCategory.LookupReference, payload.GuessedCategory);
        Assert.Contains("lookup-table-payload", payload.FiredHeuristics);
    }

    [Fact]
    public void Lookup_payload_promotion_skips_structural_columns()
    {
        // PK and FK columns keep their category; memo columns keep memo;
        // PII columns are PII-veto-protected and the table is no longer
        // a lookup at all. This test pins the "stronger structural
        // categories stay put" half of the post-pass.
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("ThinTable", IsLinkedTable: false, Columns: new[]
            {
                Col("Id",        0, "Long", autoNumber: true, pk: true),
                Col("OtherId",   1, "Long", fk: true, fkTarget: new RawFkTarget("Other", "Id")),
            }),
            new RawTable("Other", IsLinkedTable: false, Columns: new[]
            {
                Col("Id", 0, "Long", autoNumber: true, pk: true),
            }),
        });
        var stats = new[]
        {
            Stat("ThinTable", "Id",      10, 0, 10),
            Stat("ThinTable", "OtherId", 10, 0,  5),
            Stat("Other",     "Id",       5, 0,  5),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        var thin = result.Tables.Single(t => t.Name == "ThinTable");
        var pk = thin.Columns.Single(c => c.Name == "Id");
        var fk = thin.Columns.Single(c => c.Name == "OtherId");
        Assert.Equal(ColumnCategory.SurrogateAutonumberPk, pk.GuessedCategory);
        Assert.Equal(ColumnCategory.ForeignKey,            fk.GuessedCategory);
    }

    [Fact]
    public void Tables_are_emitted_in_case_insensitive_name_order()
    {
        var schema = new RawSchema("Test", new[]
        {
            new RawTable("zebra", IsLinkedTable: false, Columns: new[]{ Col("Id", 0, pk: true) }),
            new RawTable("Apple", IsLinkedTable: false, Columns: new[]{ Col("Id", 0, pk: true) }),
            new RawTable("Mango", IsLinkedTable: false, Columns: new[]{ Col("Id", 0, pk: true) }),
        });
        var stats = new[]
        {
            Stat("zebra", "Id", 1, 0, 1),
            Stat("Apple", "Id", 1, 0, 1),
            Stat("Mango", "Id", 1, 0, 1),
        };
        var result = HeuristicsEngine.Run(schema, stats, Cfg, Scanned);
        Assert.Equal(new[] { "Apple", "Mango", "zebra" }, result.Tables.Select(t => t.Name));
    }
}
