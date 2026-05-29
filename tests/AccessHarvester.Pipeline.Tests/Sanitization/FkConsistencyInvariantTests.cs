using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;
using static AccessHarvester.Pipeline.Tests.Sanitization.TestFixtures;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

/// <summary>
/// The FK consistency invariant: after sanitization, every FK value must
/// still refer to a row that exists in the referenced table — i.e., the
/// remapped FK appears in the remapped PK's value set. Plan § 5: "An ID is
/// either preserved or remapped consistently everywhere it appears (PK +
/// every FK that references it, in lockstep)."
/// </summary>
public class FkConsistencyInvariantTests
{
    [Fact]
    public void Surrogate_pk_remap_propagates_to_referencing_fk()
    {
        // Person.PersonID is a surrogate PK; Visit.PersonID is a FK. After
        // sanitization, the new Visit.PersonID must equal the new Person.PersonID
        // for the matching original PersonID.
        var doc = Doc(
            Table("Person",
                Col("PersonID",  ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
                Col("FirstName", ColumnCategory.DirectIdentifierPii)),
            Table("Visit",
                Col("VisitID",  ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
                Col("PersonID", ColumnCategory.ForeignKey, fk: true, required: true,
                    fkTarget: new ClassifiedFkTarget("Person", "PersonID"))));

        var rows = new InMemoryRowSource();
        rows.Add("Person", new Dictionary<string, object?> { ["PersonID"] = 1L, ["FirstName"] = "Alice" });
        rows.Add("Person", new Dictionary<string, object?> { ["PersonID"] = 2L, ["FirstName"] = "Bob"   });
        rows.Add("Person", new Dictionary<string, object?> { ["PersonID"] = 3L, ["FirstName"] = "Carol" });

        rows.Add("Visit", new Dictionary<string, object?> { ["VisitID"] = 10L, ["PersonID"] = 1L });
        rows.Add("Visit", new Dictionary<string, object?> { ["VisitID"] = 11L, ["PersonID"] = 1L });
        rows.Add("Visit", new Dictionary<string, object?> { ["VisitID"] = 12L, ["PersonID"] = 2L });
        rows.Add("Visit", new Dictionary<string, object?> { ["VisitID"] = 13L, ["PersonID"] = 3L });

        var captured = new InMemoryCsvOutput();
        new MaskingEngine().Run(doc, rows, captured, new System.DateTimeOffset(2026, 5, 28, 12, 0, 0, System.TimeSpan.Zero));

        var personLines = captured.GetLines("Person").Skip(1).ToArray(); // skip header
        var visitLines  = captured.GetLines("Visit").Skip(1).ToArray();

        // Build the set of remapped PersonIDs that appear in Person.
        var personIds = new HashSet<string>(personLines.Select(l => l.Split(',')[0]));
        Assert.Equal(3, personIds.Count);

        // Every Visit.PersonID must be in personIds (FK target preserved).
        foreach (var line in visitLines)
        {
            string visitPersonId = line.Split(',')[1];
            Assert.Contains(visitPersonId, personIds);
        }

        // And the lockstep property: every distinct original PersonID maps
        // to exactly one remapped value, irrespective of which table the
        // mapping was first established in.
        Assert.Equal(3, visitLines.Select(l => l.Split(',')[1]).Distinct().Count());
    }

    [Fact]
    public void Self_referencing_fk_uses_same_map()
    {
        // Employee.ManagerID -> Employee.EmployeeID (org-chart pattern).
        var doc = Doc(Table("Employee",
            Col("EmployeeID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
            Col("ManagerID",  ColumnCategory.ForeignKey, fk: true,
                fkTarget: new ClassifiedFkTarget("Employee", "EmployeeID"))));

        var rows = new InMemoryRowSource();
        rows.Add("Employee", new Dictionary<string, object?> { ["EmployeeID"] = 1L, ["ManagerID"] = null });
        rows.Add("Employee", new Dictionary<string, object?> { ["EmployeeID"] = 2L, ["ManagerID"] = 1L });
        rows.Add("Employee", new Dictionary<string, object?> { ["EmployeeID"] = 3L, ["ManagerID"] = 1L });

        var captured = new InMemoryCsvOutput();
        new MaskingEngine().Run(doc, rows, captured, new System.DateTimeOffset(2026, 5, 28, 12, 0, 0, System.TimeSpan.Zero));

        var lines = captured.GetLines("Employee").Skip(1).ToArray();
        // The first row's PK is the remap target for the second + third
        // rows' ManagerID. Same key in the registry == same new value.
        string firstRowPk        = lines[0].Split(',')[0];
        string secondRowManager  = lines[1].Split(',')[1];
        string thirdRowManager   = lines[2].Split(',')[1];
        Assert.Equal(firstRowPk, secondRowManager);
        Assert.Equal(firstRowPk, thirdRowManager);
    }

    [Fact]
    public void Preserved_pk_with_fk_preserves_both()
    {
        // Business key codes don't remap by default; FKs that reference
        // them must also preserve the original value.
        var doc = Doc(
            Table("Codes",
                Col("Code", ColumnCategory.BusinessNaturalKey, pk: true, dataType: "Text")),
            Table("Event",
                Col("EventID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
                Col("Code",    ColumnCategory.ForeignKey, fk: true,
                    fkTarget: new ClassifiedFkTarget("Codes", "Code"))));

        var rows = new InMemoryRowSource();
        rows.Add("Codes", new Dictionary<string, object?> { ["Code"] = "ALPHA" });
        rows.Add("Codes", new Dictionary<string, object?> { ["Code"] = "BETA"  });
        rows.Add("Event", new Dictionary<string, object?> { ["EventID"] = 1L, ["Code"] = "ALPHA" });
        rows.Add("Event", new Dictionary<string, object?> { ["EventID"] = 2L, ["Code"] = "BETA"  });

        var captured = new InMemoryCsvOutput();
        new MaskingEngine().Run(doc, rows, captured, new System.DateTimeOffset(2026, 5, 28, 12, 0, 0, System.TimeSpan.Zero));

        var codes = captured.GetLines("Codes").Skip(1).Select(l => l.Trim()).ToArray();
        var events = captured.GetLines("Event").Skip(1).ToArray();
        Assert.Contains("ALPHA", codes);
        Assert.Contains("BETA",  codes);
        Assert.Equal("ALPHA", events[0].Split(',')[1]);
        Assert.Equal("BETA",  events[1].Split(',')[1]);
    }
}
