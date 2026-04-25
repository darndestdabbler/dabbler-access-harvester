using System.Text;
using AccessHarvester.Core.Model;

namespace AccessHarvester.Core.Translation;

/// <summary>
/// Parses Access's SaveAsText output format into an <see cref="AccessNode"/> tree.
/// </summary>
/// <remarks>
/// The format has three kinds of <c>Begin</c>/<c>End</c> pairs that must be
/// distinguished:
/// <list type="bullet">
///   <item><c>Begin TypeName</c> — named block (Form, Section, TextBox, etc.).
///   Pushes a new child node of that type.</item>
///   <item><c>Begin</c> alone — anonymous scope (used as a wrapper around default-
///   property templates and around the control-bag in a Section). Pushes an
///   anonymous child with <c>Type="Begin"</c>.</item>
///   <item><c>PropName = Begin</c> … <c>End</c> — multi-line <em>value</em> (used
///   for binary-hex properties like GUID, NameMap, RecSrcDt, DOL). Does NOT push
///   a scope; the lines between Begin and End accumulate as the property's value.</item>
/// </list>
/// The preliminary AccessJsonParser.ParseBlockFormat this was ported from did not
/// distinguish the third case, so every <c>PropName = Begin … End</c> caused a
/// spurious stack pop — which silently truncated every Form/Report's Children to
/// empty. Fixed here.
/// <para>
/// Two additional fixes (Session 04 — MacroActionsEnricher prerequisites):
/// </para>
/// <list type="number">
///   <item><b>Repeated-key accumulation.</b> SaveAsText macros emit the same
///   <c>Argument</c> key multiple times in a single block (one per positional
///   argument). Originally each write overwrote the previous one — silently
///   discarding all but the last value. Now repeated writes accumulate the
///   values into a <c>List&lt;object&gt;</c>.</item>
///   <item><b>Quoted-string continuation.</b> Long string values that don't fit
///   on one line are split into multiple consecutive lines, each a complete
///   quoted string (e.g. <c>Comment ="part1"</c> on one line, <c>"part2"</c>
///   on the next). The parser now concatenates those continuations back into
///   a single property value. Without this, <c>_AXL:</c> XML chunks lose their
///   tails to spurious flag-property keys.</item>
/// </list>
/// <para>
/// The standalone-macro file format is a flat sequence of root-level
/// <c>Begin</c> blocks (no enclosing <c>Begin Macro ...</c>). Pass
/// <paramref name="returnRoot"/>=true (used by the Macros translator) to get
/// the document root with all Begin children intact, instead of the default
/// "first child becomes main object" behavior used by Forms and Reports.
/// </para>
/// </remarks>
public static class AccessTextParser
{
    public static AccessNode ParseBlockFormat(string[] lines, bool returnRoot = false)
    {
        var root = new AccessNode { Type = "DocumentRoot" };
        var stack = new Stack<AccessNode>();
        stack.Push(root);

        bool inVbaSection = false;
        var vbaBuilder = new StringBuilder();

        bool inMultilineValue = false;
        string? multilineKey = null;
        var multilineBuilder = new StringBuilder();

        // Tracks the last property whose value was a quoted string, so a
        // following bare quoted-string line can be appended to that value as
        // a continuation. Cleared by any non-continuation line.
        string? lastStringPropertyKey = null;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (inVbaSection)
            {
                vbaBuilder.AppendLine(rawLine);
                continue;
            }

            if (inMultilineValue)
            {
                if (line == "End")
                {
                    SetOrAccumulate(stack.Peek().Properties, multilineKey!, multilineBuilder.ToString());
                    inMultilineValue = false;
                    multilineKey = null;
                    multilineBuilder.Clear();
                }
                else
                {
                    if (multilineBuilder.Length > 0)
                        multilineBuilder.Append('\n');
                    multilineBuilder.Append(line);
                }
                continue;
            }

            if (line == "CodeBehindForm" || line.StartsWith("Attribute VB_"))
            {
                inVbaSection = true;
                vbaBuilder.AppendLine(rawLine);
                lastStringPropertyKey = null;
                continue;
            }

            // Quoted-string continuation: a bare "..." line directly following
            // a property whose value was a quoted string. Append (no separator)
            // to that property's existing string value.
            if (lastStringPropertyKey is not null
                && line.Length >= 2 && line[0] == '"' && line[^1] == '"')
            {
                string append = line.Substring(1, line.Length - 2);
                var props = stack.Peek().Properties;
                if (props.TryGetValue(lastStringPropertyKey, out var existing) && existing is string s)
                {
                    props[lastStringPropertyKey] = s + append;
                }
                continue;
            }

            if (line.StartsWith("Begin "))
            {
                var newNode = new AccessNode { Type = line.Substring(6).Trim() };
                stack.Peek().Children.Add(newNode);
                stack.Push(newNode);
                lastStringPropertyKey = null;
            }
            else if (line == "Begin")
            {
                var newNode = new AccessNode { Type = "Begin" };
                stack.Peek().Children.Add(newNode);
                stack.Push(newNode);
                lastStringPropertyKey = null;
            }
            else if (line == "End")
            {
                if (stack.Count > 1) stack.Pop();
                lastStringPropertyKey = null;
            }
            else
            {
                int eqIndex = line.IndexOf('=');
                if (eqIndex > 0)
                {
                    string key = line.Substring(0, eqIndex).Trim();
                    string value = line.Substring(eqIndex + 1).Trim();

                    if (value == "Begin")
                    {
                        inMultilineValue = true;
                        multilineKey = key;
                        multilineBuilder.Clear();
                        lastStringPropertyKey = null;
                    }
                    else
                    {
                        bool wasQuoted = false;
                        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                        {
                            value = value.Substring(1, value.Length - 2);
                            wasQuoted = true;
                        }
                        SetOrAccumulate(stack.Peek().Properties, key, value);
                        lastStringPropertyKey = wasQuoted ? key : null;
                    }
                }
                else
                {
                    SetOrAccumulate(stack.Peek().Properties, line, true);
                    lastStringPropertyKey = null;
                }
            }
        }

        if (returnRoot)
        {
            if (vbaBuilder.Length > 0) root.VbaCode = vbaBuilder.ToString();
            return root;
        }

        var mainObject = root.Children.FirstOrDefault() ?? root;
        if (vbaBuilder.Length > 0)
        {
            mainObject.VbaCode = vbaBuilder.ToString();
        }

        return mainObject;
    }

    /// <summary>
    /// Sets <paramref name="key"/>=<paramref name="value"/> in
    /// <paramref name="props"/>. If the key already exists, accumulates: the
    /// existing scalar becomes the first element of a new <c>List&lt;object&gt;</c>
    /// and the new value is appended; if the key already holds a list, the new
    /// value is appended to it.
    /// </summary>
    private static void SetOrAccumulate(IDictionary<string, object> props, string key, object value)
    {
        if (props.TryGetValue(key, out var existing))
        {
            if (existing is List<object> list)
            {
                list.Add(value);
            }
            else
            {
                props[key] = new List<object> { existing, value };
            }
        }
        else
        {
            props[key] = value;
        }
    }
}
