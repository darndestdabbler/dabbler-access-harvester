using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Pure, deterministic parser turning an Office Fluent <c>customUI</c> XML
/// string (the value of a <c>USysRibbons.RibbonXML</c> row) into a
/// <see cref="RibbonStructure"/>. No I/O, no environment; identical input
/// yields an identical structure.
/// </summary>
/// <remarks>
/// <para>
/// <b>Namespace-tolerant.</b> Elements are matched by <i>local name</i>, so
/// both the <c>…/2006/01/customui</c> and <c>…/2009/07/customui</c> namespaces
/// (and prefixed variants such as <c>mso:customUI</c>) parse identically. The
/// parser does not validate against either schema.
/// </para>
/// <para>
/// <b>Faithful-mechanical.</b> Within the projected sections (see the
/// structured-view scope below), every authored tab/group/control is surfaced —
/// custom (<c>id</c>) and built-in (<c>idMso</c>) alike — nesting is preserved
/// recursively, and any attribute without a typed slot (<c>id</c>/<c>idMso</c>/
/// <c>label</c>/<c>onAction</c>) is preserved verbatim in source order on the
/// node's <c>Attributes</c> bag. An attribute that is present but empty
/// (<c>label=""</c>) is surfaced as <c>""</c> (distinct from an <i>absent</i>
/// attribute, which is null) — the parser does not normalise values. The parser
/// does not interpret the ribbon (no <c>imageMso</c> decode, no dynamic
/// <c>get*</c> evaluation) beyond surfacing the static <c>onAction</c> callback
/// into <see cref="RibbonControl.OnAction"/>. See the design note at
/// <c>docs/session-sets/016-ribbon-navigation-surface/s1-ribbon-model-consensus.md</c>.
/// </para>
/// <para>
/// <b>Structured-view scope (deliberate boundary, not a silent drop).</b> The
/// structured tree models the ribbon's <c>&lt;tabs&gt;</c> and
/// <c>&lt;contextualTabs&gt;</c> — the navigation surface DW-13 targets. Other
/// customUI sections (<c>&lt;qat&gt;</c>, <c>&lt;officeMenu&gt;</c>,
/// <c>&lt;backstage&gt;</c>) and root-level <c>&lt;customUI&gt;</c> attributes
/// beyond <c>onLoad</c> (e.g. <c>loadImage</c>) are NOT projected into this
/// structure. They are never lost: the raw, byte-verbatim customUI XML is
/// preserved in <c>raw/Navigation/ribbon-xml.json</c>, which is the lossless
/// source of record for any section this navigation-focused view does not
/// model. Surfacing <c>onAction</c> entry points from those other sections in
/// the structured view is tracked as deferred work (see DW-21).
/// </para>
/// </remarks>
public static class RibbonXmlParser
{
    // customUI attribute names are strictly camelCase (case-sensitive), so an
    // exact local-name match is correct and avoids mis-slotting a genuinely
    // different attribute.
    private const string IdAttr = "id";
    private const string IdMsoAttr = "idMso";
    private const string LabelAttr = "label";
    private const string OnActionAttr = "onAction";
    private const string OnLoadAttr = "onLoad";

    /// <summary>
    /// Parses one <c>customUI</c> document. Returns null when
    /// <paramref name="xml"/> is null/whitespace (no ribbon content). Throws
    /// <see cref="FormatException"/> when the XML is malformed or carries no
    /// <c>&lt;ribbon&gt;</c> element, so the caller can degrade explicitly
    /// rather than silently dropping a broken ribbon.
    /// </summary>
    public static RibbonStructure? Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"Ribbon customUI XML is not well-formed: {ex.Message}", ex);
        }

        var root = doc.Root;
        if (root is null || !IsLocal(root, "customUI"))
            throw new FormatException(
                "Ribbon customUI XML root is not a <customUI> element.");

        var ribbon = Children(root).FirstOrDefault(e => IsLocal(e, "ribbon"));
        if (ribbon is null)
            throw new FormatException(
                "Ribbon customUI XML has no <ribbon> element.");

        var tabsEl = Children(ribbon).FirstOrDefault(e => IsLocal(e, "tabs"));
        var tabs = tabsEl is null
            ? Array.Empty<RibbonTab>()
            : Children(tabsEl).Where(e => IsLocal(e, "tab")).Select(ParseTab).ToArray();

        var contextualEl = Children(ribbon).FirstOrDefault(e => IsLocal(e, "contextualTabs"));
        var contextual = contextualEl is null
            ? Array.Empty<RibbonContextualTabSet>()
            : Children(contextualEl).Where(e => IsLocal(e, "tabSet"))
                .Select(ParseTabSet).ToArray();

        return new RibbonStructure
        {
            Attributes = UnslottedAttributes(ribbon),
            OnLoad = AttrValue(root, OnLoadAttr),
            Tabs = tabs,
            ContextualTabSets = contextual,
        };
    }

    private static RibbonContextualTabSet ParseTabSet(XElement el) => new()
    {
        Id = AttrValue(el, IdAttr),
        IdMso = AttrValue(el, IdMsoAttr),
        Attributes = UnslottedAttributes(el),
        Tabs = Children(el).Where(e => IsLocal(e, "tab")).Select(ParseTab).ToArray(),
    };

    private static RibbonTab ParseTab(XElement el) => new()
    {
        Id = AttrValue(el, IdAttr),
        IdMso = AttrValue(el, IdMsoAttr),
        Label = AttrValue(el, LabelAttr),
        Attributes = UnslottedAttributes(el),
        Groups = Children(el).Where(e => IsLocal(e, "group")).Select(ParseGroup).ToArray(),
    };

    private static RibbonGroup ParseGroup(XElement el) => new()
    {
        Id = AttrValue(el, IdAttr),
        IdMso = AttrValue(el, IdMsoAttr),
        Label = AttrValue(el, LabelAttr),
        Attributes = UnslottedAttributes(el),
        // Every child element of a group is a control (button, box, splitButton,
        // menu, dropDown, gallery, comboBox, control, menuSeparator, …).
        Controls = Children(el).Select(ParseControl).ToArray(),
    };

    private static RibbonControl ParseControl(XElement el) => new()
    {
        ControlType = el.Name.LocalName,
        Id = AttrValue(el, IdAttr),
        IdMso = AttrValue(el, IdMsoAttr),
        Label = AttrValue(el, LabelAttr),
        OnAction = AttrValue(el, OnActionAttr),
        Attributes = UnslottedAttributes(el),
        // Recurse: a splitButton/menu/box/gallery nests further controls.
        Children = Children(el).Select(ParseControl).ToArray(),
    };

    // ── helpers ────────────────────────────────────────────────────────────

    private static bool IsLocal(XElement el, string localName)
        => string.Equals(el.Name.LocalName, localName, StringComparison.Ordinal);

    private static IEnumerable<XElement> Children(XElement el) => el.Elements();

    private static string? AttrValue(XElement el, string localName)
        => el.Attributes()
             .FirstOrDefault(a => !a.IsNamespaceDeclaration
                                  && string.Equals(a.Name.LocalName, localName, StringComparison.Ordinal))
             ?.Value;

    // Every attribute that is NOT a namespace declaration and NOT a promoted
    // typed slot (id / idMso / label / onAction), in document order, verbatim.
    private static readonly HashSet<string> SlottedAttrs =
        new(StringComparer.Ordinal) { IdAttr, IdMsoAttr, LabelAttr, OnActionAttr };

    private static IReadOnlyList<RibbonAttribute> UnslottedAttributes(XElement el)
    {
        var list = new List<RibbonAttribute>();
        foreach (var a in el.Attributes())
        {
            if (a.IsNamespaceDeclaration) continue;
            if (SlottedAttrs.Contains(a.Name.LocalName)) continue;
            list.Add(new RibbonAttribute { Name = a.Name.LocalName, Value = a.Value });
        }
        return list;
    }
}
