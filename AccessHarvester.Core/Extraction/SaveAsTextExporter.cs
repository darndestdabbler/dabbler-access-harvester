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
        var outcomes = new List<ExportOutcome>();

        foreach (dynamic obj in collection)
        {
            string name = obj.Name;
            if (_nameFilter is not null && !_nameFilter(name)) continue;

            string outFile = Path.Combine(rawCategoryDir, name + ".txt");
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

    private static dynamic ResolveCollection(dynamic app, string accessor) => accessor switch
    {
        "AllForms" => app.CurrentProject.AllForms,
        "AllReports" => app.CurrentProject.AllReports,
        "AllMacros" => app.CurrentProject.AllMacros,
        "AllModules" => app.CurrentProject.AllModules,
        _ => throw new ArgumentException($"Unknown Access collection: {accessor}")
    };
}
