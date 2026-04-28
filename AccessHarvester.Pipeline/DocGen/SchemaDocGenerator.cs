using System;
using System.Text;
using System.Text.Json;

namespace AccessHarvester.Pipeline.DocGen;

/// <summary>
/// Generates a human-readable markdown summary from a DatabaseManifest JSON document.
/// </summary>
public sealed class SchemaDocGenerator
{
    /// <summary>
    /// Generates a markdown summary document from the supplied DatabaseManifest JSON.
    /// </summary>
    /// <param name="manifestJson">The full DatabaseManifest JSON string.</param>
    /// <returns>Markdown string containing the per-database summary, form catalog, and dependency section.</returns>
    public string Generate(string manifestJson)
    {
        ArgumentException.ThrowIfNullOrEmpty(manifestJson);

        using var doc = JsonDocument.Parse(manifestJson);
        var root = doc.RootElement;

        var sb = new StringBuilder();
        AppendSummarySection(sb, root);
        AppendFormCatalogSection(sb, root);
        AppendDependencySection(sb, root);
        return sb.ToString();
    }

    private static void AppendSummarySection(StringBuilder sb, JsonElement root)
    {
        string dbName = root.TryGetProperty("Database", out var db) && db.TryGetProperty("Name", out var nameEl)
            ? nameEl.GetString() ?? "(unknown)"
            : "(unknown)";

        sb.AppendLine($"# Schema Summary: {dbName}");
        sb.AppendLine();

        if (root.TryGetProperty("Summary", out var summary))
        {
            sb.AppendLine("## Database Overview");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Count |");
            sb.AppendLine($"|--------|------:|");
            AppendSummaryRow(sb, summary, "Forms",            "FormCount");
            AppendSummaryRow(sb, summary, "Reports",          "ReportCount");
            AppendSummaryRow(sb, summary, "Queries",          "QueryCount");
            AppendSummaryRow(sb, summary, "Modules",          "ModuleCount");
            AppendSummaryRow(sb, summary, "Tables",           "TableCount");
            AppendSummaryRow(sb, summary, "Unresolved Items", "UnresolvedItemCount");
            sb.AppendLine();
        }
    }

    private static void AppendSummaryRow(StringBuilder sb, JsonElement summary, string label, string key)
    {
        int count = summary.TryGetProperty(key, out var el) ? el.GetInt32() : 0;
        sb.AppendLine($"| {label} | {count} |");
    }

    private static void AppendFormCatalogSection(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("Forms", out var forms) || forms.GetArrayLength() == 0)
            return;

        sb.AppendLine("## Form Catalog");
        sb.AppendLine();

        foreach (var formEntry in forms.EnumerateArray())
        {
            if (!formEntry.TryGetProperty("Spec", out var spec))
                continue;

            string formName = spec.TryGetProperty("SourceFormName", out var fn) ? fn.GetString() ?? "(unnamed)" : "(unnamed)";
            sb.AppendLine($"### {formName}");
            sb.AppendLine();

            // Record source
            if (spec.TryGetProperty("DataSource", out var ds))
            {
                string kind = ds.TryGetProperty("Kind", out var k) ? k.GetString() ?? "" : "";
                string source = kind switch
                {
                    "NamedReference" => ds.TryGetProperty("TargetName", out var t) ? t.GetString() ?? "(none)" : "(none)",
                    "InlineSql"      => "(inline SQL)",
                    "None"           => "(unbound)",
                    _                => "(unknown)",
                };
                sb.AppendLine($"**Record Source:** {source}");
                sb.AppendLine();
            }

            // Control list
            if (spec.TryGetProperty("Controls", out var controls) && controls.GetArrayLength() > 0)
            {
                sb.AppendLine("**Controls:**");
                sb.AppendLine();
                sb.AppendLine("| Name | Type |");
                sb.AppendLine("|------|------|");
                foreach (var ctrl in controls.EnumerateArray())
                {
                    string name = ctrl.TryGetProperty("Name", out var cn) ? cn.GetString() ?? "" : "";
                    string type = ctrl.TryGetProperty("SourceType", out var ct) ? ct.GetString() ?? "" : "";
                    sb.AppendLine($"| {name} | {type} |");
                }
                sb.AppendLine();
            }

            // Event handler count
            int handlerCount = spec.TryGetProperty("EventHandlers", out var handlers) ? handlers.GetArrayLength() : 0;
            sb.AppendLine($"**Event Handlers:** {handlerCount}");
            sb.AppendLine();
        }
    }

    private static void AppendDependencySection(StringBuilder sb, JsonElement root)
    {
        if (!root.TryGetProperty("Forms", out var forms) || forms.GetArrayLength() == 0)
            return;

        bool hasSubforms = false;
        foreach (var formEntry in forms.EnumerateArray())
        {
            if (!formEntry.TryGetProperty("Spec", out var spec)) continue;
            if (spec.TryGetProperty("Subforms", out var subforms) && subforms.GetArrayLength() > 0)
            {
                hasSubforms = true;
                break;
            }
        }

        if (!hasSubforms)
            return;

        sb.AppendLine("## Subform Dependencies");
        sb.AppendLine();
        sb.AppendLine("| Parent Form | Subform Control | Contained Form |");
        sb.AppendLine("|-------------|-----------------|----------------|");

        foreach (var formEntry in forms.EnumerateArray())
        {
            if (!formEntry.TryGetProperty("Spec", out var spec)) continue;
            string parentName = spec.TryGetProperty("SourceFormName", out var fn) ? fn.GetString() ?? "" : "";
            if (!spec.TryGetProperty("Subforms", out var subforms)) continue;
            foreach (var sf in subforms.EnumerateArray())
            {
                string ctrl = sf.TryGetProperty("SubformControlName", out var c) ? c.GetString() ?? "" : "";
                string contained = sf.TryGetProperty("ContainedObjectName", out var o) ? o.GetString() ?? "(unset)" : "(unset)";
                sb.AppendLine($"| {parentName} | {ctrl} | {contained} |");
            }
        }

        sb.AppendLine();
    }
}
