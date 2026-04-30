using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAnEmployee;

public sealed class EmployeeSaveValidationTests
{
    [Fact]
    public void CanSave_ReturnsFalseWhenRequiredEmployeeFieldsAreMissing()
    {
        var missingFirstName = new EmployeeDraft
        {
            LastName = "Fuller",
            JobTitle = "Sales Representative",
        };
        Assert.False(UpdateEmployeeWorkflow.CanSave(missingFirstName));

        var missingLastName = new EmployeeDraft
        {
            FirstName = "Andrew",
            JobTitle = "Sales Representative",
        };
        Assert.False(UpdateEmployeeWorkflow.CanSave(missingLastName));

        var missingJobTitle = new EmployeeDraft
        {
            FirstName = "Andrew",
            LastName = "Fuller",
        };
        Assert.False(UpdateEmployeeWorkflow.CanSave(missingJobTitle));
    }

    [Fact]
    public void CanSave_ReturnsTrueWhenRequiredEmployeeFieldsArePresent()
    {
        var employee = new EmployeeDraft
        {
            FirstName = "Andrew",
            LastName = "Fuller",
            JobTitle = "Sales Representative",
        };

        Assert.True(UpdateEmployeeWorkflow.CanSave(employee));
    }
}
