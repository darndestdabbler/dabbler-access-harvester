using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAnEmployee;

public sealed class EmployeePrivilegeAssignmentTests
{
    [Fact]
    public void BeginPrivilegeAssignment_ScopesPrivilegeAssignmentToTheNewEmployee()
    {
        var launch = UpdateEmployeeWorkflow.BeginPrivilegeAssignment(employeeId: 26);

        Assert.Equal(26, launch.OpenArgsEmployeeId);
    }

    [Fact]
    public void ResolvePrivilegeEditorDefaultEmployeeId_UsesPositiveOpenArgsOnly()
    {
        Assert.Null(UpdateEmployeeWorkflow.ResolvePrivilegeEditorDefaultEmployeeId(null));
        Assert.Null(UpdateEmployeeWorkflow.ResolvePrivilegeEditorDefaultEmployeeId(0));
        Assert.Null(UpdateEmployeeWorkflow.ResolvePrivilegeEditorDefaultEmployeeId(-1));
        Assert.Equal(26, UpdateEmployeeWorkflow.ResolvePrivilegeEditorDefaultEmployeeId(26));
    }
}
