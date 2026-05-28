using System.IO;
using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

/// <summary>
/// Hermetic tests for <see cref="IrLoader"/>. These tests write minimal IR JSON
/// fixtures to a temp directory and assert that the loader extracts the expected
/// structural elements.
/// </summary>
public class IrLoaderTests
{
    /// <summary>
    /// Regression guard for R2 Issue 2: in WalkChildren, the generic-properties
    /// branch used to sit BEFORE the type=="Begin" macro-action branch. Begin
    /// nodes carry Properties too, so they got consumed as generic controls and
    /// never reached MacroActions. This test embeds a Begin/OpenForm action in a
    /// form's Children and asserts MacroActions captures it.
    /// </summary>
    [Fact]
    public void Form_Embedded_Begin_Action_Is_Captured_In_MacroActions()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "iht-irloader-" + System.Guid.NewGuid().ToString("N"));
        string formsDir = Path.Combine(root, "ir", "Forms");
        Directory.CreateDirectory(formsDir);
        try
        {
            // Minimal form with a single embedded Begin macro-action child.
            string json = """
                {
                    "Properties": { "Name": "frmHome" },
                    "Children": [
                        {
                            "Type": "Begin",
                            "Properties": {
                                "Action": "OpenForm",
                                "Argument": "frmTarget"
                            }
                        }
                    ]
                }
                """;
            File.WriteAllText(Path.Combine(formsDir, "frmHome.ir.json"), json);

            var db = IrLoader.Load(root);
            var form = db.Forms.Single();
            Assert.Equal("frmHome", form.Name);
            Assert.NotEmpty(form.MacroActions);
            var act = form.MacroActions.Single();
            Assert.Equal("OpenForm", act.Action);
            Assert.Contains("frmTarget", act.Arguments);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Sibling check: a generic non-Begin control node (CommandButton) still
    /// flows through the generic-properties branch and produces an IrControl
    /// with its Properties. This guards against an over-eager fix that would
    /// remove the generic branch entirely.
    /// </summary>
    [Fact]
    public void Form_Generic_Control_Node_Is_Captured_As_Control()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "iht-irloader-" + System.Guid.NewGuid().ToString("N"));
        string formsDir = Path.Combine(root, "ir", "Forms");
        Directory.CreateDirectory(formsDir);
        try
        {
            string json = """
                {
                    "Properties": { "Name": "frmHome" },
                    "Children": [
                        {
                            "Type": "CommandButton",
                            "Properties": {
                                "Name": "btnGo",
                                "OnClick": "mcrRun"
                            }
                        }
                    ]
                }
                """;
            File.WriteAllText(Path.Combine(formsDir, "frmHome.ir.json"), json);

            var db = IrLoader.Load(root);
            var form = db.Forms.Single();
            Assert.Single(form.Controls);
            var ctl = form.Controls[0];
            Assert.Equal("CommandButton", ctl.Type);
            Assert.Equal("btnGo", ctl.Name);
            Assert.True(ctl.Properties.ContainsKey("OnClick"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
