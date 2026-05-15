using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAClient;

/// <summary>
/// Characterization tests for delete/deactivate policy on the client detail form.
/// Sources:
/// - frmClientDetail::Form_Delete
/// - frmClientDetail::chkIsActive_AfterUpdate
/// </summary>
public sealed class ClientLifecyclePolicyTests
{
    [Fact]
    public void CanDelete_ReturnsFalseWhenBudgetRowsExist()
    {
        var workflow = new ClientLifecycleWorkflow();

        Assert.False(workflow.CanDelete(budgetRowCount: 1));
    }

    [Fact]
    public void CanDelete_ReturnsTrueWhenNoBudgetRowsExist()
    {
        var workflow = new ClientLifecycleWorkflow();

        Assert.True(workflow.CanDelete(budgetRowCount: 0));
    }

    [Fact]
    public void NeedsDeactivationWarning_ReturnsTrueWhenApprovedPlansRemain()
    {
        var workflow = new ClientLifecycleWorkflow();

        Assert.True(workflow.NeedsDeactivationWarning(approvedPlanCount: 2));
        Assert.False(workflow.NeedsDeactivationWarning(approvedPlanCount: 0));
    }

    private sealed class ClientLifecycleWorkflow
    {
        public bool CanDelete(int budgetRowCount)
        {
            return budgetRowCount == 0;
        }

        public bool NeedsDeactivationWarning(int approvedPlanCount)
        {
            return approvedPlanCount > 0;
        }
    }
}
