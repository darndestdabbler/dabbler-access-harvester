using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Core.Enrichment.Macros;
using AccessHarvester.SymbolDb.Models;

namespace AccessHarvester.SymbolDb.Extraction;

// Reads macro KB JSON files and produces MacroRow + MacroActionRow lists.
// Each macro file contains _AXL: XML chunks that MacroXmlParser assembles
// and parses into a flat action list.
public static class MacroExtractor
{
    public sealed record Result(
        IReadOnlyList<MacroRow> Macros,
        IReadOnlyList<MacroActionRow> MacroActions);

    public static Result Extract(IEnumerable<string> kbJsonPaths)
    {
        var macros = new List<MacroRow>();
        var actions = new List<MacroActionRow>();

        foreach (var path in kbJsonPaths)
        {
            ExtractOne(path, macros, actions);
        }

        return new Result(macros, actions);
    }

    private static void ExtractOne(string path, List<MacroRow> macros, List<MacroActionRow> actions)
    {
        string macroName = Path.GetFileNameWithoutExtension(path);

        using var stream = File.OpenRead(path);
        var root = JsonNode.Parse(stream)?.AsObject()
            ?? throw new InvalidDataException($"Expected JSON object at root of {path}");

        string? assembledXml = MacroXmlParser.AssembleAxlChunks(root);
        string sourceXml = assembledXml ?? string.Empty;

        macros.Add(new MacroRow(Fqn: macroName, SourceXml: sourceXml, IsDead: false));

        if (!string.IsNullOrEmpty(assembledXml))
        {
            var parsedActions = MacroXmlParser.ParseToActions(assembledXml);
            int position = 0;   // dense, gap-free position for the (macro_fqn, position) PK
            for (int i = 0; i < parsedActions.Count; i++)
            {
                var actionNode = parsedActions[i]?.AsObject();
                if (actionNode is null) continue;

                string actionName = actionNode["Action"]?.GetValue<string>() ?? "";
                string? argsJson = null;
                if (actionNode["Arguments"] is JsonNode argsNode)
                    argsJson = argsNode.ToJsonString();

                actions.Add(new MacroActionRow(
                    MacroFqn: macroName,
                    Position: position++,
                    ActionName: actionName,
                    ArgumentsJson: argsJson));
            }
        }
    }
}
