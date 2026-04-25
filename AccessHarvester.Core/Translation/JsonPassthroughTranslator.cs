using AccessHarvester.Core.Abstractions;

namespace AccessHarvester.Core.Translation;

/// <summary>
/// Trivial translator for categories whose raw artifacts are already structured JSON
/// (Queries, Tables, Relationships — extracted via DAO). Copies raw/&lt;Category&gt;/*.json
/// into kb/&lt;Category&gt;/*.json unchanged. Exists so the Translate phase remains
/// meaningful (and uniformly round-trippable) even when extraction produced JSON
/// directly.
/// </summary>
public sealed class JsonPassthroughTranslator : IObjectTranslator
{
    private JsonPassthroughTranslator(string category)
    {
        Category = category;
    }

    public string Category { get; }

    public static JsonPassthroughTranslator Queries { get; } = new("Queries");
    public static JsonPassthroughTranslator Tables { get; } = new("Tables");
    public static JsonPassthroughTranslator Relationships { get; } = new("Relationships");

    public IReadOnlyList<TranslateOutcome> Translate(string rawCategoryDir, string kbCategoryDir)
    {
        Directory.CreateDirectory(kbCategoryDir);
        if (!Directory.Exists(rawCategoryDir))
            return Array.Empty<TranslateOutcome>();

        var outcomes = new List<TranslateOutcome>();
        foreach (var file in Directory.GetFiles(rawCategoryDir, "*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            try
            {
                File.Copy(file, Path.Combine(kbCategoryDir, name + ".json"), overwrite: true);
                outcomes.Add(new TranslateOutcome(name, true));
            }
            catch (Exception ex)
            {
                outcomes.Add(new TranslateOutcome(name, false, ex.Message));
            }
        }
        return outcomes;
    }
}
