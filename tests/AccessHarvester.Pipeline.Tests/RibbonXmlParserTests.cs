using System;
using System.Linq;
using AccessHarvester.Pipeline.Enrichment;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

/// <summary>
/// Hermetic pins for <see cref="RibbonXmlParser"/> over a HAND-AUTHORED
/// (synthetic, committed) customUI fixture — the regression backstop for the
/// DW-13 ribbon producer. NorthwindDevEdition's real ribbon is validated only
/// LOCALLY (proprietary; never committed) by
/// <c>AccessHarvester.Integration.Pipeline.NorthwindLocalInvariantTests</c>.
/// </summary>
public class RibbonXmlParserTests
{
    // A hand-authored customUI document modelled on the real shape (2006/01
    // namespace, custom id-bearing controls with onAction, built-in idMso
    // controls with no id/label/onAction, sub-group nesting via box/splitButton/
    // menu, declarative separators, unslotted attributes, and a contextualTabs
    // section). Kept small but structurally representative.
    private const string Fixture = """
<customUI xmlns="http://schemas.microsoft.com/office/2006/01/customui" onLoad="ribbonLoaded">
  <ribbon startFromScratch="false">
    <tabs>
      <tab id="tHome" label="Home">
        <group id="gOrders" label="Orders">
          <button id="cmdOrders" label="Orders" size="large" imageMso="Cat" onAction="cmdOrders_OnAction"/>
          <button idMso="FileSave"/>
          <menuSeparator id="sep1"/>
          <splitButton id="sbPaste">
            <button id="cmdPaste" label="Paste" onAction="cmdPaste_OnAction"/>
            <menu id="mPaste">
              <button id="cmdPasteSpecial" label="Paste Special" onAction="cmdPasteSpecial_OnAction"/>
              <button idMso="PasteAppend"/>
            </menu>
          </splitButton>
        </group>
      </tab>
      <tab idMso="TabAddIns" label="AddIns" visible="true"/>
    </tabs>
    <contextualTabs>
      <tabSet idMso="TabSetFormTools">
        <tab idMso="TabFormToolsDesign" label="Design" visible="true"/>
      </tabSet>
    </contextualTabs>
  </ribbon>
</customUI>
""";

    private static RibbonStructure Parse() =>
        RibbonXmlParser.Parse(Fixture) ?? throw new Xunit.Sdk.XunitException("expected non-null");

    [Fact]
    public void NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(RibbonXmlParser.Parse(null));
        Assert.Null(RibbonXmlParser.Parse(""));
        Assert.Null(RibbonXmlParser.Parse("   \r\n  "));
    }

    [Fact]
    public void Malformed_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => RibbonXmlParser.Parse("<customUI><ribbon><tabs>"));
    }

    [Fact]
    public void NonCustomUiRoot_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => RibbonXmlParser.Parse("<root><ribbon/></root>"));
    }

    [Fact]
    public void NoRibbonElement_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => RibbonXmlParser.Parse(
            "<customUI xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\"/>"));
    }

    [Fact]
    public void Root_Carries_OnLoad_And_RibbonAttributes()
    {
        var r = Parse();
        Assert.Equal("ribbonLoaded", r.OnLoad);
        // <ribbon startFromScratch="false"> — surfaced verbatim, not interpreted.
        var attr = Assert.Single(r.Attributes);
        Assert.Equal("startFromScratch", attr.Name);
        Assert.Equal("false", attr.Value);
    }

    [Fact]
    public void Tabs_PreserveSourceOrder_AndCustomVsBuiltIn()
    {
        var r = Parse();
        Assert.Equal(2, r.Tabs.Count);

        var home = r.Tabs[0];
        Assert.Equal("tHome", home.Id);
        Assert.Null(home.IdMso);
        Assert.Equal("Home", home.Label);

        var addIns = r.Tabs[1];
        Assert.Null(addIns.Id);                       // built-in tab: no custom id
        Assert.Equal("TabAddIns", addIns.IdMso);
        Assert.Equal("AddIns", addIns.Label);
        Assert.Contains(addIns.Attributes, a => a.Name == "visible" && a.Value == "true");
        Assert.Empty(addIns.Groups);
    }

    [Fact]
    public void CustomButton_CarriesOnAction_AndUnslottedAttributesVerbatim()
    {
        var group = Parse().Tabs[0].Groups.Single();
        Assert.Equal("gOrders", group.Id);
        var orders = group.Controls.First(c => c.Id == "cmdOrders");

        Assert.Equal("button", orders.ControlType);
        Assert.Equal("Orders", orders.Label);
        Assert.Equal("cmdOrders_OnAction", orders.OnAction);
        // imageMso + size preserved verbatim (NOT interpreted), in source order.
        Assert.Equal(new[] { "size", "imageMso" }, orders.Attributes.Select(a => a.Name).ToArray());
        Assert.Equal("Cat", orders.Attributes.Single(a => a.Name == "imageMso").Value);
    }

    [Fact]
    public void BuiltInIdMsoControl_IsFullNode_WithNoIdLabelOnAction()
    {
        var controls = Parse().Tabs[0].Groups.Single().Controls;
        var save = controls.Single(c => c.IdMso == "FileSave");
        Assert.Equal("button", save.ControlType);
        Assert.Null(save.Id);
        Assert.Null(save.Label);
        Assert.Null(save.OnAction);
        Assert.Empty(save.Attributes);
        Assert.Empty(save.Children);
    }

    [Fact]
    public void DeclarativeControl_HasNullOnAction()
    {
        var sep = Parse().Tabs[0].Groups.Single().Controls.Single(c => c.ControlType == "menuSeparator");
        Assert.Equal("sep1", sep.Id);
        Assert.Null(sep.OnAction);
    }

    [Fact]
    public void Nesting_IsPreservedRecursively_NotFlattened()
    {
        var controls = Parse().Tabs[0].Groups.Single().Controls;
        // Group's direct controls in source order: button, button(idMso), menuSeparator, splitButton.
        Assert.Equal(new[] { "button", "button", "menuSeparator", "splitButton" },
            controls.Select(c => c.ControlType).ToArray());

        var split = controls.Single(c => c.ControlType == "splitButton");
        Assert.Equal("sbPaste", split.Id);
        // splitButton's children: a button then a menu (NOT flattened into the group).
        Assert.Equal(new[] { "button", "menu" }, split.Children.Select(c => c.ControlType).ToArray());

        var menu = split.Children.Single(c => c.ControlType == "menu");
        Assert.Equal(new[] { "cmdPasteSpecial", null }, menu.Children.Select(c => c.Id).ToArray());
        Assert.Equal("cmdPasteSpecial_OnAction",
            menu.Children.Single(c => c.Id == "cmdPasteSpecial").OnAction);
    }

    [Fact]
    public void ContextualTabs_ModeledAsParallelCollection()
    {
        var r = Parse();
        var set = Assert.Single(r.ContextualTabSets);
        Assert.Equal("TabSetFormTools", set.IdMso);
        var tab = Assert.Single(set.Tabs);
        Assert.Equal("TabFormToolsDesign", tab.IdMso);
        Assert.Equal("Design", tab.Label);
    }

    [Fact]
    public void NamespaceTolerant_PrefixedAnd2009Namespace_ParseIdentically()
    {
        // Prefixed (mso:) + the 2009/07 namespace must parse to the same shape
        // as the default-namespaced 2006/01 document (matching is by local name).
        const string prefixed = """
<mso:customUI xmlns:mso="http://schemas.microsoft.com/office/2009/07/customui">
  <mso:ribbon>
    <mso:tabs>
      <mso:tab id="tHome" label="Home">
        <mso:group id="g1" label="G">
          <mso:button id="b1" label="B" onAction="B_OnAction"/>
        </mso:group>
      </mso:tab>
    </mso:tabs>
  </mso:ribbon>
</mso:customUI>
""";
        var r = RibbonXmlParser.Parse(prefixed);
        Assert.NotNull(r);
        Assert.Equal("tHome", r!.Tabs.Single().Id);
        Assert.Equal("B_OnAction", r.Tabs.Single().Groups.Single().Controls.Single().OnAction);
    }

    [Fact]
    public void PresentButEmptyAttribute_IsPreservedAsEmpty_DistinctFromAbsent()
    {
        // Faithful-mechanical: an authored empty label="" is surfaced as "" (the
        // attribute is present), NOT normalised to null (which means absent).
        const string xml = """
<customUI xmlns="http://schemas.microsoft.com/office/2006/01/customui">
  <ribbon><tabs><tab id="t" label="">
    <group id="g"><button id="b1" label="" onAction="X"/><button id="b2" onAction="Y"/></group>
  </tab></tabs></ribbon>
</customUI>
""";
        var r = RibbonXmlParser.Parse(xml)!;
        Assert.Equal("", r.Tabs[0].Label);                 // present but empty
        var controls = r.Tabs[0].Groups[0].Controls;
        Assert.Equal("", controls.Single(c => c.Id == "b1").Label);   // present empty
        Assert.Null(controls.Single(c => c.Id == "b2").Label);        // absent
    }

    [Fact]
    public void Parse_IsDeterministic()
    {
        // List-backed record members compare by reference, so compare the
        // serialized shape: identical input must yield byte-identical JSON.
        var a = System.Text.Json.JsonSerializer.Serialize(RibbonXmlParser.Parse(Fixture));
        var b = System.Text.Json.JsonSerializer.Serialize(RibbonXmlParser.Parse(Fixture));
        Assert.Equal(a, b);
    }
}
