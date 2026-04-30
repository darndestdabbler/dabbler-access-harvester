using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAnEmployee;

public sealed class EmployeeIdentityConstraintTests
{
    [Fact]
    public void HasUniqueFullName_RejectsAnotherEmployeeWithSameFirstAndLastName()
    {
        var draft = new EmployeeDraft
        {
            EmployeeId = 9,
            FirstName = "Nancy",
            LastName = "Davolio",
            JobTitle = "Sales Representative",
        };

        var existing = new[]
        {
            new EmployeeIdentitySnapshot
            {
                EmployeeId = 1,
                FirstName = "Nancy",
                LastName = "Davolio",
                WindowsUserName = "nancyd",
            },
        };

        Assert.False(UpdateEmployeeWorkflow.HasUniqueFullName(draft, existing));
    }

    [Fact]
    public void HasUniqueFullName_AllowsUpdatingTheCurrentEmployeeWithoutTreatingItAsDuplicate()
    {
        var draft = new EmployeeDraft
        {
            EmployeeId = 1,
            FirstName = "Nancy",
            LastName = "Davolio",
            JobTitle = "Sales Representative",
        };

        var existing = new[]
        {
            new EmployeeIdentitySnapshot
            {
                EmployeeId = 1,
                FirstName = "Nancy",
                LastName = "Davolio",
                WindowsUserName = "nancyd",
            },
        };

        Assert.True(UpdateEmployeeWorkflow.HasUniqueFullName(draft, existing));
    }

    [Fact]
    public void HasUniqueWindowsUserName_RejectsAnotherEmployeeWithSameWindowsUserName()
    {
        var draft = new EmployeeDraft
        {
            EmployeeId = 9,
            FirstName = "Margaret",
            LastName = "Peacock",
            JobTitle = "Sales Manager",
            WindowsUserName = "nancyd",
        };

        var existing = new[]
        {
            new EmployeeIdentitySnapshot
            {
                EmployeeId = 1,
                FirstName = "Nancy",
                LastName = "Davolio",
                WindowsUserName = "nancyd",
            },
        };

        Assert.False(UpdateEmployeeWorkflow.HasUniqueWindowsUserName(draft, existing));
    }

    [Fact]
    public void HasUniqueWindowsUserName_AllowsBlankWindowsUserName()
    {
        var draft = new EmployeeDraft
        {
            EmployeeId = 9,
            FirstName = "Margaret",
            LastName = "Peacock",
            JobTitle = "Sales Manager",
            WindowsUserName = null,
        };

        var existing = new[]
        {
            new EmployeeIdentitySnapshot
            {
                EmployeeId = 1,
                FirstName = "Nancy",
                LastName = "Davolio",
                WindowsUserName = "nancyd",
            },
        };

        Assert.True(UpdateEmployeeWorkflow.HasUniqueWindowsUserName(draft, existing));
    }
}
