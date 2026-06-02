using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Cross-reference index built from all signal collectors over an <see cref="IrDatabase"/>.
/// Provides O(1) lookup for the classifier's rules.
/// </summary>
public sealed class SignalIndex
{
    // RecordSource: formOrReportName -> tableName/queryName
    public IReadOnlyDictionary<string, string> RecordSourceByObject { get; }

    // Objects used AS a RecordSource (by any form/report)
    public IReadOnlySet<string> UsedAsRecordSource { get; }

    // Objects used AS a RecordSource specifically by a form
    public IReadOnlySet<string> UsedAsFormRecordSource { get; }

    // Objects used AS a RecordSource specifically by a report
    public IReadOnlySet<string> UsedAsReportRecordSource { get; }

    // Objects used AS a RowSource (by any form control or VBA assignment)
    public IReadOnlySet<string> UsedAsRowSource { get; }

    // Forms/objects that ARE navigation targets (opened by another object)
    public IReadOnlySet<string> NavigationTargets { get; }

    // Forms/objects that emit navigation edges (open other objects)
    public IReadOnlySet<string> NavigationSources { get; }

    // Objects opened by an AutoExec/startup macro — first-class entry surfaces
    public IReadOnlySet<string> StartupOpenedObjects { get; }

    // Tables/queries referenced in VBA domain functions or OpenRecordset
    public IReadOnlySet<string> VbaReferencedObjects { get; }

    // Query -> set of tables/queries it references in FROM/JOIN
    public IReadOnlyDictionary<string, IReadOnlySet<string>> QuerySqlTargetsByQuery { get; }

    // Tables/queries that appear in any query's SQL
    public IReadOnlySet<string> UsedInQuerySql { get; }

    // FK: parent table -> set of child tables
    public IReadOnlyDictionary<string, IReadOnlySet<string>> FkChildrenByTable { get; }

    // FK: child table -> set of parent tables
    public IReadOnlyDictionary<string, IReadOnlySet<string>> FkParentsByTable { get; }

    // Tables whose name matches the lookup naming convention
    public IReadOnlySet<string> LookupNamedTables { get; }

    // All query names in the database (used by classifier to distinguish query RS from table RS)
    public IReadOnlySet<string> KnownQueryNames { get; }

    private SignalIndex(
        IReadOnlyDictionary<string, string> recordSourceByObject,
        IReadOnlySet<string> usedAsRecordSource,
        IReadOnlySet<string> usedAsFormRecordSource,
        IReadOnlySet<string> usedAsReportRecordSource,
        IReadOnlySet<string> usedAsRowSource,
        IReadOnlySet<string> navigationTargets,
        IReadOnlySet<string> navigationSources,
        IReadOnlySet<string> startupOpenedObjects,
        IReadOnlySet<string> vbaReferencedObjects,
        IReadOnlyDictionary<string, IReadOnlySet<string>> querySqlTargetsByQuery,
        IReadOnlySet<string> usedInQuerySql,
        IReadOnlyDictionary<string, IReadOnlySet<string>> fkChildrenByTable,
        IReadOnlyDictionary<string, IReadOnlySet<string>> fkParentsByTable,
        IReadOnlySet<string> lookupNamedTables,
        IReadOnlySet<string> knownQueryNames)
    {
        RecordSourceByObject    = recordSourceByObject;
        UsedAsRecordSource      = usedAsRecordSource;
        UsedAsFormRecordSource  = usedAsFormRecordSource;
        UsedAsReportRecordSource = usedAsReportRecordSource;
        UsedAsRowSource         = usedAsRowSource;
        NavigationTargets       = navigationTargets;
        NavigationSources       = navigationSources;
        StartupOpenedObjects    = startupOpenedObjects;
        VbaReferencedObjects    = vbaReferencedObjects;
        QuerySqlTargetsByQuery  = querySqlTargetsByQuery;
        UsedInQuerySql          = usedInQuerySql;
        FkChildrenByTable       = fkChildrenByTable;
        FkParentsByTable        = fkParentsByTable;
        LookupNamedTables       = lookupNamedTables;
        KnownQueryNames         = knownQueryNames;
    }

    /// <summary>Builds a <see cref="SignalIndex"/> from an <see cref="IrDatabase"/>.</summary>
    public static SignalIndex Build(IrDatabase db)
    {
        // Build known query name set first — needed for inline-SQL scanning below.
        var knownQueryNamesEarly = new HashSet<string>(
            db.Queries.Select(q => q.Name), StringComparer.OrdinalIgnoreCase);

        // RecordSources — split into form-level and report-level for Q2/Q3 classification.
        // Phase 1: direct named references (RecordSource is a bare table/query name, not SQL).
        var recordSourceByObject = RecordSourceCollector.Collect(db);
        var usedAsFormRecordSource = new HashSet<string>(
            db.Forms
              .Where(f => f.RecordSource != null && !SqlDetection.IsInlineSql(f.RecordSource))
              .Select(f => f.RecordSource!.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var usedAsReportRecordSource = new HashSet<string>(
            db.Reports
              .Where(r => r.RecordSource != null && !SqlDetection.IsInlineSql(r.RecordSource))
              .Select(r => r.RecordSource!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        // Phase 2: scan inline-SQL RecordSource strings for named query references.
        // A report RecordSource like "SELECT * FROM qryFoo WHERE …" counts as using qryFoo.
        foreach (var form in db.Forms.Where(f => f.RecordSource != null && SqlDetection.IsInlineSql(f.RecordSource)))
            foreach (var name in ExtractQueryNamesFromSql(form.RecordSource!, knownQueryNamesEarly))
                usedAsFormRecordSource.Add(name);
        foreach (var report in db.Reports.Where(r => r.RecordSource != null && SqlDetection.IsInlineSql(r.RecordSource)))
            foreach (var name in ExtractQueryNamesFromSql(report.RecordSource!, knownQueryNamesEarly))
                usedAsReportRecordSource.Add(name);

        var usedAsRecordSource = new HashSet<string>(
            recordSourceByObject.Values, StringComparer.OrdinalIgnoreCase);
        // Include inline-SQL-sourced query names in the broad used-as-RS set.
        foreach (var name in usedAsFormRecordSource.Concat(usedAsReportRecordSource))
            usedAsRecordSource.Add(name);

        // RowSources
        var rowSourceTriples = RowSourceCollector.Collect(db);
        var usedAsRowSource = new HashSet<string>(
            rowSourceTriples.Select(t => t.RowSourceName), StringComparer.OrdinalIgnoreCase);

        // Navigation edges
        var navEdges = NavigationEdgeCollector.Collect(db);
        var navigationTargets = new HashSet<string>(
            navEdges.Select(e => e.Target), StringComparer.OrdinalIgnoreCase);
        var navigationSources = new HashSet<string>(
            navEdges.Select(e => e.Source), StringComparer.OrdinalIgnoreCase);

        // Startup entry: objects opened by an AutoExec/startup macro. AutoExec is
        // the canonical Access macro that runs on database open; anything it opens
        // is a first-class application entry surface, not a stale/unreferenced object.
        var startupOpenedObjects = new HashSet<string>(
            navEdges.Where(e => IsStartupMacroName(e.Source)).Select(e => e.Target),
            StringComparer.OrdinalIgnoreCase);

        // VBA references
        var vbaRefs = VbaReferenceCollector.Collect(db);
        var vbaReferencedObjects = new HashSet<string>(
            vbaRefs.Select(r => r.ReferencedObject), StringComparer.OrdinalIgnoreCase);

        // Query SQL references
        var queryRefs = QueryTableReferenceCollector.Collect(db);
        var querySqlTargetsByQuery = queryRefs
            .GroupBy(r => r.QueryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(r => r.ReferencedObject),
                                                                StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        var usedInQuerySql = new HashSet<string>(
            queryRefs.Select(r => r.ReferencedObject), StringComparer.OrdinalIgnoreCase);

        // FK relationships
        var fkEdges = RelationshipCollector.Collect(db);
        var fkChildrenByTable = fkEdges
            .GroupBy(e => e.ParentTable, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(e => e.ChildTable),
                                                                StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        var fkParentsByTable = fkEdges
            .GroupBy(e => e.ChildTable, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(e => e.ParentTable),
                                                                StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        // Lookup naming convention
        var lookupNamedTables = new HashSet<string>(
            db.Tables
              .Where(t => IsLookupNamed(t.Name))
              .Select(t => t.Name),
            StringComparer.OrdinalIgnoreCase);

        return new SignalIndex(
            recordSourceByObject,
            usedAsRecordSource,
            usedAsFormRecordSource,
            usedAsReportRecordSource,
            usedAsRowSource,
            navigationTargets,
            navigationSources,
            startupOpenedObjects,
            vbaReferencedObjects,
            querySqlTargetsByQuery,
            usedInQuerySql,
            fkChildrenByTable,
            fkParentsByTable,
            lookupNamedTables,
            knownQueryNamesEarly);
    }

    // Extracts query names referenced via FROM or JOIN in an inline SQL string.
    // Only returns names that are in the knownQueryNames set (avoids false positives on table names).
    private static IEnumerable<string> ExtractQueryNamesFromSql(string sql, IReadOnlySet<string> knownQueryNames)
    {
        var tokens = sql.Split(new char[] { ' ', '\t', '\n', '\r', ',', '(', ')' },
                               StringSplitOptions.RemoveEmptyEntries);
        bool nextIsName = false;
        foreach (var token in tokens)
        {
            if (nextIsName)
            {
                string candidate = token.Trim('[', ']');
                int asIdx = candidate.IndexOf(' ');
                if (asIdx > 0) candidate = candidate[..asIdx];
                if (knownQueryNames.Contains(candidate))
                    yield return candidate;
                nextIsName = false;
            }
            if (token.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("JOIN", StringComparison.OrdinalIgnoreCase))
            {
                nextIsName = true;
            }
        }
    }

    // True when a table name matches the conventional lookup/reference naming prefixes.
    private static bool IsLookupNamed(string name)
        => name.StartsWith("tlkp", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("lkp",  StringComparison.OrdinalIgnoreCase)
        || name.Contains("_Lookup", StringComparison.OrdinalIgnoreCase)
        || name.Contains("_Codes",  StringComparison.OrdinalIgnoreCase)
        || name.Contains("_Types",  StringComparison.OrdinalIgnoreCase);

    // True when the source object is an AutoExec/startup macro (runs on db open).
    private static bool IsStartupMacroName(string name)
        => name.Equals("AutoExec", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Autoexec", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when a form name matches a conventional switchboard/menu/launcher
    /// naming pattern (e.g., frmSwitchboard, frmMainMenu, frmReportMenu,
    /// frmStartup, frmHome). Used to recognize launcher shells even when the
    /// form delegates its opens to a separate macro and so emits no navigation
    /// edges of its own. See strategy doc §2a.
    /// </summary>
    public static bool IsLauncherFormName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        // Strip a leading object-type prefix ("frm") so "frmMain" -> "Main".
        string core = name.StartsWith("frm", StringComparison.OrdinalIgnoreCase)
            ? name.Substring(3)
            : name;
        return name.Contains("Switchboard", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Menu",        StringComparison.OrdinalIgnoreCase)
            || core.Equals("Main",     StringComparison.OrdinalIgnoreCase)
            || core.Equals("Startup",  StringComparison.OrdinalIgnoreCase)
            || core.Equals("Home",     StringComparison.OrdinalIgnoreCase)
            || core.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase)
            || core.StartsWith("Startup",  StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when a table name looks like switchboard/menu navigation-configuration
    /// data (e.g., tblSwitchboardItems, tblMenuItems). Such helper tables back a
    /// launcher surface and should be explained as launcher infrastructure rather
    /// than read as generic unexplained objects. See strategy doc §2a.
    /// </summary>
    public static bool IsLauncherDataTableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Contains("Switchboard", StringComparison.OrdinalIgnoreCase)
            || name.Contains("MenuItems",   StringComparison.OrdinalIgnoreCase)
            || (name.Contains("Menu", StringComparison.OrdinalIgnoreCase)
                && name.Contains("Item", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when the object is opened by an AutoExec/startup macro.</summary>
    public bool IsStartupOpened(string objectName)
        => StartupOpenedObjects.Contains(objectName);

    /// <summary>Returns true when the table has FK children in the index.</summary>
    public bool HasFkChildren(string tableName)
        => FkChildrenByTable.ContainsKey(tableName);

    /// <summary>Returns true when the table has FK parents in the index.</summary>
    public bool HasFkParents(string tableName)
        => FkParentsByTable.ContainsKey(tableName);

    /// <summary>Returns true when the table is a lookup-named table.</summary>
    public bool IsLookupTable(string tableName)
        => LookupNamedTables.Contains(tableName);

    /// <summary>Returns true when no signal references this object anywhere.</summary>
    public bool IsCompletelyUnreferenced(string objectName)
        => !UsedAsRecordSource.Contains(objectName)
        && !UsedAsRowSource.Contains(objectName)
        && !NavigationTargets.Contains(objectName)
        && !VbaReferencedObjects.Contains(objectName)
        && !UsedInQuerySql.Contains(objectName)
        && !HasFkChildren(objectName)
        && !HasFkParents(objectName);
}
