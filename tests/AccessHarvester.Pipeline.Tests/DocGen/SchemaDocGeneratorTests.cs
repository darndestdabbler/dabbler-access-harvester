using AccessHarvester.Pipeline.DocGen;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.DocGen;

public sealed class SchemaDocGeneratorTests
{
    private static SchemaDocGenerator CreateGenerator() => new();

    // Minimal DatabaseManifest — verifies summary section and no crashes on empty arrays.
    [Fact]
    public void Generate_MinimalDocument_ContainsDatabaseName()
    {
        const string json = """
            {
              "SchemaType": "DatabaseManifest",
              "SchemaVersion": "1.0",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "TestDb.accdb" },
              "Summary": {
                "FormCount": 0,
                "ReportCount": 0,
                "QueryCount": 0,
                "ModuleCount": 0,
                "TableCount": 0,
                "UnresolvedItemCount": 0
              },
              "Forms": [],
              "Queries": [],
              "Modules": []
            }
            """;

        string result = CreateGenerator().Generate(json);

        Assert.Contains("TestDb.accdb", result);
        Assert.Contains("## Database Overview", result);
    }

    // Form catalog section — verifies form name, record source, control list, event handler count.
    [Fact]
    public void Generate_DocumentWithForm_ContainsFormCatalogSection()
    {
        const string json = """
            {
              "SchemaType": "DatabaseManifest",
              "SchemaVersion": "1.0",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "CatalogDb.accdb" },
              "Summary": { "FormCount": 1, "QueryCount": 1 },
              "Forms": [
                {
                  "Spec": {
                    "SourceFormName": "frmOrders",
                    "DataSource": { "Kind": "NamedReference", "TargetName": "tblOrders" },
                    "Controls": [
                      { "Name": "txtOrderId", "SourceType": "TextBox", "ComponentKind": "TextInput" },
                      { "Name": "cmdSave",    "SourceType": "CommandButton", "ComponentKind": "Button" }
                    ],
                    "EventHandlers": [
                      { "HostKind": "Control", "ControlName": "cmdSave", "PropertyName": "OnClick", "EventName": "Click" }
                    ]
                  }
                }
              ]
            }
            """;

        string result = CreateGenerator().Generate(json);

        Assert.Contains("## Form Catalog", result);
        Assert.Contains("frmOrders", result);
        Assert.Contains("tblOrders", result);
        Assert.Contains("txtOrderId", result);
        Assert.Contains("Event Handlers:** 1", result);
    }

    // Dependency section — verifies subform relationships appear only when subforms exist.
    [Fact]
    public void Generate_DocumentWithSubforms_ContainsDependencySection()
    {
        const string json = """
            {
              "SchemaType": "DatabaseManifest",
              "SchemaVersion": "1.0",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "SubDb.accdb" },
              "Forms": [
                {
                  "Spec": {
                    "SourceFormName": "frmParent",
                    "Controls": [],
                    "EventHandlers": [],
                    "Subforms": [
                      {
                        "SubformControlName": "sfrmChild",
                        "ContainedObjectName": "frmChildEmbed"
                      }
                    ]
                  }
                }
              ]
            }
            """;

        string result = CreateGenerator().Generate(json);

        Assert.Contains("## Subform Dependencies", result);
        Assert.Contains("frmParent", result);
        Assert.Contains("sfrmChild", result);
        Assert.Contains("frmChildEmbed", result);
    }

    // No dependency section when no subforms exist.
    [Fact]
    public void Generate_DocumentWithoutSubforms_NoDependencySection()
    {
        const string json = """
            {
              "SchemaType": "DatabaseManifest",
              "SchemaVersion": "1.0",
              "HarvesterVersion": "1.0.0",
              "Database": { "Name": "NoSubDb.accdb" },
              "Forms": [
                {
                  "Spec": {
                    "SourceFormName": "frmSimple",
                    "Controls": [],
                    "EventHandlers": []
                  }
                }
              ]
            }
            """;

        string result = CreateGenerator().Generate(json);

        Assert.DoesNotContain("## Subform Dependencies", result);
    }
}
