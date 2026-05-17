using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorAddAnOrder.Ui.Pages.Generated;

public partial class FrmBudgetServicePlanBase : ComponentBase
{
    [CascadingParameter] public IMudDialogInstance? MudDialog { get; set; }
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;

    // -- state -----------------------------------------------------------
    protected object? budgetId;
    protected bool isDirty;

    // -- request result rows ---------------------------------------------
    protected Dictionary<string, object?>? loadPlan;
    protected List<Dictionary<string, object?>>? loadNeedsForClient;
    protected Dictionary<string, object?>? savePlan;
    protected Dictionary<string, object?>? deletePlan;

    protected override async Task OnInitializedAsync()
    {
        // TODO: wire request runner. Invokes named query "qryBudgetServicePlanByID".
        loadPlan = await ExecuteRequestAsync("loadPlan");
        // TODO: wire request runner. Invokes named query "qryNeedsByClient".
        loadNeedsForClient = await ExecuteRequestAsync("loadNeedsForClient");
        await Task.CompletedTask;
    }

    protected async Task OnSave()
    {
        try
        {
            var __errors = new List<string>();
            if (!((!System.Convert.ToBoolean((loadPlan?["FiscalYear"] == null))))) __errors.Add("Fiscal year is required.");
            if (!((System.Convert.ToDouble(loadPlan?["AnnualBudget"]) > System.Convert.ToDouble(0)))) __errors.Add("Annual budget must be greater than zero.");
            if (!((!System.Convert.ToBoolean((loadPlan?["ClientID"] == null))))) __errors.Add("Client must be selected.");
            if (__errors.Count > 0)
            {
                foreach (var e in __errors) Snackbar.Add(e, Severity.Warning);
                return;
            }
            // TODO: wire request runner. Invokes named query "cmdUpdateBudgetServicePlan".
            savePlan = await ExecuteRequestAsync("savePlan");
            isDirty = false;
            MudDialog?.Close(DialogResult.Ok<object?>("saved"));
        }
        catch
        {
            Snackbar.Add("Failed to save the budget service plan. Please try again.", Severity.Error);
        }
        await Task.CompletedTask;
    }

    protected async Task OnCancel()
    {
        // TODO: implement confirmation prompt (MudBlazor IDialogService).
        // Auto-confirming for now -- override OnConfirmAsync in .Custom.cs for real prompts.
        bool __confirmed = await OnConfirmAsync("Discard unsaved changes?");
        if (__confirmed)
        {
            MudDialog?.Close(DialogResult.Ok<object?>("cancelled"));
        }
        await Task.CompletedTask;
    }

    protected async Task OnDelete()
    {
        // TODO: implement confirmation prompt (MudBlazor IDialogService).
        // Auto-confirming for now -- override OnConfirmAsync in .Custom.cs for real prompts.
        bool __confirmed = await OnConfirmAsync("Permanently delete this budget service plan? This cannot be undone.");
        if (__confirmed)
        {
            // TODO: wire request runner. Invokes named query "cmdDeleteBudgetServicePlan".
            deletePlan = await ExecuteRequestAsync("deletePlan");
            MudDialog?.Close(DialogResult.Ok<object?>("deleted"));
        }
        await Task.CompletedTask;
    }

    protected async Task OnFiscalYearChanged()
    {
        isDirty = true;
        await Task.CompletedTask;
    }

    // -- runtime escape hatches (override in .Custom.cs) -----------------
    protected virtual Task<dynamic?> ExecuteRequestAsync(string requestName)
    {
        throw new NotImplementedException(
            $"ExecuteRequestAsync for {requestName} is not implemented. "
            + "Override in the partial class .Custom.cs to wire up your named-query runner.");
    }

    protected virtual Task<bool> OnConfirmAsync(string message)
    {
        // Default: auto-confirm so flows progress without a prompt.
        // Override in .Custom.cs to show a real MudDialog confirmation.
        return Task.FromResult(true);
    }

}
