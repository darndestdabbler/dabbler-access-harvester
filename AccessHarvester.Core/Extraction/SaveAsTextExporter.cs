using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Exports Access objects that round-trip through Access's native SaveAsText format
/// (Forms, Reports, Macros, Modules, Queries) as .txt files under the raw output directory.
/// </summary>
public sealed class SaveAsTextExporter : IObjectExporter
{
    private readonly int _objectType;
    private readonly string _collectionAccessor;
    private readonly Func<string, bool>? _nameFilter;

    private SaveAsTextExporter(
        string category,
        int objectType,
        string collectionAccessor,
        Func<string, bool>? nameFilter = null)
    {
        Category = category;
        _objectType = objectType;
        _collectionAccessor = collectionAccessor;
        _nameFilter = nameFilter;
    }

    public string Category { get; }

    public static SaveAsTextExporter Forms { get; }
        = new("Forms", AcObjectType.acForm, "AllForms");
    public static SaveAsTextExporter Reports { get; }
        = new("Reports", AcObjectType.acReport, "AllReports");
    public static SaveAsTextExporter Macros { get; }
        = new("Macros", AcObjectType.acMacro, "AllMacros");
    public static SaveAsTextExporter Modules { get; }
        = new("ModulesAndClasses", AcObjectType.acModule, "AllModules");

    public IReadOnlyList<ExportOutcome> Export(AccessComSession session, string rawCategoryDir)
    {
        Directory.CreateDirectory(rawCategoryDir);

        dynamic collection = ResolveCollection(session.Application, _collectionAccessor);

        var names = new List<string>();
        foreach (dynamic obj in collection)
        {
            string name = obj.Name;
            if (_nameFilter is null || _nameFilter(name))
                names.Add(name);
        }

        var outcomes = new List<ExportOutcome>();
        foreach (var (name, fileName, collisionError) in PlanOutputFiles(names))
        {
            if (collisionError is not null)
            {
                outcomes.Add(new ExportOutcome(name, false, collisionError));
                continue;
            }

            string outFile = Path.Combine(rawCategoryDir, fileName!);
            try
            {
                session.Application.SaveAsText(_objectType, name, outFile);
                outcomes.Add(new ExportOutcome(name, true));
            }
            catch (Exception ex)
            {
                outcomes.Add(new ExportOutcome(name, false, ex.Message));
            }
        }

        return outcomes;
    }

    /// <summary>
    /// Pure: resolves the raw <c>.txt</c> output file name for each object name,
    /// in order, sanitising the name (006 F-1-2) so a legal but path-hostile
    /// object name (Access permits <c>/</c> and <c>\</c> in object names — proven
    /// by DAO probe) no longer makes <see cref="Path.Combine(string, string)"/>
    /// target a non-existent subdirectory and fail an otherwise-valid export.
    /// When two distinct object names sanitise to the same file name the later one
    /// is reported as a collision (a non-null <c>CollisionError</c>) rather than
    /// silently overwriting the earlier object's output — the explicit-over-silent
    /// contract the set-specific constraint requires. Object names within an Access
    /// collection are unique, so a repeated sanitised name is always a true
    /// collision of two different source objects.
    /// </summary>
    internal static IReadOnlyList<(string Name, string? FileName, string? CollisionError)> PlanOutputFiles(
        IEnumerable<string> names)
    {
        // Keyed case-insensitively because the Windows file system is
        // case-insensitive: two names whose sanitised forms differ only in case
        // would still collide on disk.
        var claimed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<(string, string?, string?)>();

        foreach (var name in names)
        {
            string fileName = SanitizeFileName(name) + ".txt";
            if (claimed.TryGetValue(fileName, out var existing)
                && !string.Equals(existing, name, StringComparison.Ordinal))
            {
                plan.Add((name, null,
                    $"Sanitised output name '{fileName}' collides with already-exported object '{existing}'."));
                continue;
            }

            claimed[fileName] = name;
            plan.Add((name, fileName, null));
        }

        return plan;
    }

    // Mirrors the sibling exporters' (QueryExporter / TableExporter /
    // RelationshipExporter) SanitizeFileName: replace every character Windows
    // rejects in a file name — which includes the path separators '/' and '\'
    // and the wildcard/reserved set — with '_'. The object's true name is
    // preserved verbatim in the per-object ExportOutcome.Name, mirroring how the
    // Queries/Tables JSON payloads keep Name even when the file name is sanitised.
    internal static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static dynamic ResolveCollection(dynamic app, string accessor) => accessor switch
    {
        "AllForms" => app.CurrentProject.AllForms,
        "AllReports" => app.CurrentProject.AllReports,
        "AllMacros" => app.CurrentProject.AllMacros,
        "AllModules" => app.CurrentProject.AllModules,
        _ => throw new ArgumentException($"Unknown Access collection: {accessor}")
    };
}
