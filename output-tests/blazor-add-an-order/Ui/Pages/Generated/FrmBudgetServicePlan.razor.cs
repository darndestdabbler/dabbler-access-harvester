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
    protected bool isDirty = false;

    // -- request result rows ---------------------------------------------
    protected Dictionary<string, object?>? loadPlan;
    protected List<Dictionary<string, object?>>? loadNeedsForClient;

    protected override async Task OnInitializedAsync()
    {
        // Invokes named query / command "qryBudgetServicePlanByID" (cardinality=single).
        loadPlan = await ExecuteRequestAsync("loadPlan");
        // Invokes named query / command "qryNeedsByClient" (cardinality=list).
        loadNeedsForClient = await ExecuteRequestListAsync("loadNeedsForClient");
    }

    protected async Task OnSave()
    {
        try
        {
            var __errors = new List<string>();
            if (!((!System.Convert.ToBoolean((loadPlan?["FiscalYear"] == null)))))
            {
                __errors.Add("Fiscal year is required.");
                SetFieldError("txtFiscalYear", "Fiscal year is required.");
            }
            if (!((System.Convert.ToDouble(loadPlan?["AnnualBudget"]) > System.Convert.ToDouble(0))))
            {
                __errors.Add("Annual budget must be greater than zero.");
                SetFieldError("txtAnnualBudget", "Annual budget must be greater than zero.");
            }
            if (!((!System.Convert.ToBoolean((loadPlan?["ClientID"] == null)))))
            {
                __errors.Add("Client must be selected.");
            }
            if (__errors.Count > 0)
            {
                foreach (var e in __errors) Snackbar.Add(e, Severity.Warning);
                return;
            }
            // Invokes named query / command "cmdUpdateBudgetServicePlan" (cardinality=command).
            await ExecuteCommandAsync("savePlan");
            isDirty = false;
            MudDialog?.Close(DialogResult.Ok<object?>("saved"));
        }
        catch
        {
            Snackbar.Add("Failed to save the budget service plan. Please try again.", Severity.Error);
        }
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
    }

    protected async Task OnDelete()
    {
        // TODO: implement confirmation prompt (MudBlazor IDialogService).
        // Auto-confirming for now -- override OnConfirmAsync in .Custom.cs for real prompts.
        bool __confirmed = await OnConfirmAsync("Permanently delete this budget service plan? This cannot be undone.");
        if (__confirmed)
        {
            // Invokes named query / command "cmdDeleteBudgetServicePlan" (cardinality=command).
            await ExecuteCommandAsync("deletePlan");
            MudDialog?.Close(DialogResult.Ok<object?>("deleted"));
        }
    }

    protected Task OnFiscalYearChanged()
    {
        isDirty = true;
        return Task.CompletedTask;
    }

    // -- named operators (SR-I; referenced by `_op` expressions) ---------
    protected object? OpIsBudgetExceeded() => (System.Convert.ToDouble(loadPlan?["TotalPlanned"]) > System.Convert.ToDouble(loadPlan?["AnnualBudget"]));

    // -- runtime escape hatches (override in .Custom.cs) -----------------
    protected virtual Task<Dictionary<string, object?>?> ExecuteRequestAsync(string requestName)
    {
        throw new NotImplementedException(
            $"ExecuteRequestAsync for {requestName} is not implemented. "
            + "Override in the partial class .Custom.cs to wire up your named-query runner.");
    }

    protected virtual Task<List<Dictionary<string, object?>>?> ExecuteRequestListAsync(string requestName)
    {
        throw new NotImplementedException(
            $"ExecuteRequestListAsync for {requestName} is not implemented. "
            + "Override in the partial class .Custom.cs.");
    }

    protected virtual Task ExecuteCommandAsync(string commandName)
    {
        throw new NotImplementedException(
            $"ExecuteCommandAsync for {commandName} is not implemented. "
            + "Override in the partial class .Custom.cs.");
    }

    protected virtual Task<bool> OnConfirmAsync(string message)
    {
        // Default: auto-confirm so flows progress without a prompt.
        // Override in .Custom.cs to show a real MudDialog confirmation.
        return Task.FromResult(true);
    }

    protected virtual void SetFieldError(string fieldId, string message)
    {
        // Default: surface via snackbar with a field-id hint.
        // Override in .Custom.cs to anchor errors on the specific MudBlazor control.
        Snackbar.Add($"[{fieldId}] {message}", Severity.Warning);
    }

    // -- Access date helpers (SR-3 _dateadd / _datediff / _datepart) ----
    protected static System.DateTime DateAddAccess(string interval, int number, System.DateTime date) => interval switch
    {
        "yyyy" => date.AddYears(number),
        "q"    => date.AddMonths(number * 3),
        "m"    => date.AddMonths(number),
        "ww"   => date.AddDays(number * 7),
        "w"    => date.AddDays(number),
        "d"    => date.AddDays(number),
        "h"    => date.AddHours(number),
        "n"    => date.AddMinutes(number),
        "s"    => date.AddSeconds(number),
        _      => date.AddDays(number),
    };

    protected static long DateDiffAccess(string interval, System.DateTime a, System.DateTime b) => interval switch
    {
        "yyyy" => b.Year - a.Year,
        "q"    => ((b.Year - a.Year) * 4) + ((b.Month - 1) / 3) - ((a.Month - 1) / 3),
        "m"    => ((b.Year - a.Year) * 12) + (b.Month - a.Month),
        "ww"   => (long)(b.Date - a.Date).TotalDays / 7,
        "w"    => (long)(b.Date - a.Date).TotalDays / 7,
        "d"    => (long)(b.Date - a.Date).TotalDays,
        "h"    => (long)(b - a).TotalHours,
        "n"    => (long)(b - a).TotalMinutes,
        "s"    => (long)(b - a).TotalSeconds,
        _      => (long)(b.Date - a.Date).TotalDays,
    };

    protected static int DatePartAccess(string interval, System.DateTime date) => interval switch
    {
        "yyyy" => date.Year,
        "q"    => ((date.Month - 1) / 3) + 1,
        "m"    => date.Month,
        "d"    => date.Day,
        "h"    => date.Hour,
        "n"    => date.Minute,
        "s"    => date.Second,
        _      => date.Day,
    };

}
