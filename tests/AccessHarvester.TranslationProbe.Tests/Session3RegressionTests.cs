using System;
using System.Collections.Generic;
using AccessHarvester.TranslationProbe;
using Xunit;

namespace AccessHarvester.TranslationProbe.Tests;

/// <summary>
/// Session 3 (Claude Opus 4.7) independent peer-review tests. Each test
/// pins a behavioral invariant that the Session 2 (Codex / GPT-5.5)
/// suite did not exercise, per
/// <c>docs/session-sets/vba-symbol-db-path-b-probe/peer-review/translation-review.md</c>
/// (sections U1-U4).
/// </summary>
public sealed class Session3RegressionTests
{
    [Fact]
    public void CompanyIsActive_PopulatesAllFiveCachedCounts_EvenWhenInactive()
    {
        // U1: VBA CompanyIsActive populates all five module-level
        // count fields unconditionally. Session 2's tests verify call
        // shapes but never snapshot the resulting field values.
        var dao = new RecordingDaoCounts(new Dictionary<string, long>
        {
            ["Contacts:CompanyID=42"] = 9,
            ["Orders:CustomerID=42"] = 0,
            ["Orders:ShipperID=42"] = 0,
            ["PurchaseOrders:VendorID=42"] = 0,
            ["ProductVendors:VendorID=42"] = 0,
        });
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            new NoopStringRepository(),
            new NoopDialogService(),
            new RecordingErrorHandler());

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(new ProbeFormContext());

        Assert.False(cancel);
        CompanyDetailProbeState state = sut.SnapshotState();
        Assert.Equal(9, state.CompanyContactCount);
        Assert.Equal(0, state.CustomerOrderCount);
        Assert.Equal(0, state.ShipperOrderCount);
        Assert.Equal(0, state.VendorPurchaseOrderCount);
        Assert.Equal(0, state.VendorProductCount);
    }

    [Fact]
    public void CboCompanyTypeIdBeforeUpdate_DoesNotCloseDialog_WhenAlreadyClosed()
    {
        // U2: VBA's IsFormOpen guard prevents a redundant DoCmd.Close
        // when the modal already dismissed itself. A translator who
        // dropped the guard would still pass Session 2's tests.
        var combo = new RecordingComboBoxControl();
        var form = new ProbeFormContext { ComboBoxControl = combo };
        var dao = new RecordingDaoCounts(new Dictionary<string, long>
        {
            ["Contacts:CompanyID=42"] = 0,
            ["Orders:CustomerID=42"] = 1,
            ["Orders:ShipperID=42"] = 0,
            ["PurchaseOrders:VendorID=42"] = 0,
            ["ProductVendors:VendorID=42"] = 0,
        });
        var dialogs = new RecordingDialogService { IsOpenAfterShow = false };
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            new NoopStringRepository(),
            dialogs,
            new RecordingErrorHandler());

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(form);

        Assert.True(cancel);
        Assert.True(combo.UndoCalled);
        Assert.Equal("frmGenericDialog", dialogs.LastOpenedFormName);
        Assert.Null(dialogs.LastClosedFormName);
    }

    [Fact]
    public void CompanyIsActive_DelegatesErrorWithDefaultShowUserMessage_OnDaoFailure()
    {
        // U3: The inner CompanyIsActive error path must call
        // HandleError with showUserMessage=false (default), distinct
        // from the outer handler which passes true. Session 2's
        // tests only verify the outer call's argument.
        var dao = new ThrowingDaoCounts(new InvalidOperationException("dao boom"));
        var errors = new RecordingErrorHandler();
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            new NoopStringRepository(),
            new NoopDialogService(),
            errors);

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(new ProbeFormContext());

        // Outer code observes CompanyIsActive returning false (its
        // exception was swallowed) AND _vendorProductCount left at 0,
        // so the gate falls through to the no-op return false.
        Assert.False(cancel);
        var call = Assert.Single(errors.Calls);
        Assert.Equal("frmCompanyDetail", call.ObjectName);
        Assert.Equal("CompanyIsActive", call.ProcedureName);
        Assert.False(call.ShowUserMessage);
    }

    [Fact]
    public void CboCompanyTypeIdBeforeUpdate_Cancels_WhenActiveButNoVendorProducts()
    {
        // U4: Active-orders-with-zero-vendor-products isolates the
        // LEFT side of the VBA Or. Session 2 isolates the RIGHT side
        // (test #3) but not the left.
        var combo = new RecordingComboBoxControl();
        var form = new ProbeFormContext { ComboBoxControl = combo };
        var dao = new RecordingDaoCounts(new Dictionary<string, long>
        {
            ["Contacts:CompanyID=42"] = 0,
            ["Orders:CustomerID=42"] = 3,
            ["Orders:ShipperID=42"] = 0,
            ["PurchaseOrders:VendorID=42"] = 0,
            ["ProductVendors:VendorID=42"] = 0,
        });
        var strings = new NoopStringRepository { ReturnValue = "active-no-vendors" };
        var dialogs = new RecordingDialogService { IsOpenAfterShow = true };
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            strings,
            dialogs,
            new RecordingErrorHandler());

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(form);

        Assert.True(cancel);
        Assert.True(combo.UndoCalled);
        Assert.Equal("frmGenericDialog", dialogs.LastOpenedFormName);
        Assert.Equal("frmGenericDialog", dialogs.LastClosedFormName);
        Assert.Equal(
            new object[] { "change the Company Type", 3L, 0L, 0L, "Vendor Products (0)" },
            strings.LastArgs);
    }

    private sealed class ProbeFormContext : IFormContext
    {
        public string Name { get; init; } = "frmCompanyDetail";
        public long CompanyId { get; init; } = 42;
        public string MessageCaption { get; init; } = "Company Detail";
        public IComboBoxControl ComboBoxControl { get; init; } = new RecordingComboBoxControl();
        public IComboBoxControl CompanyTypeIdControl => ComboBoxControl;
    }

    private sealed class RecordingComboBoxControl : IComboBoxControl
    {
        public bool UndoCalled { get; private set; }

        public void Undo() => UndoCalled = true;
    }

    private sealed class RecordingDaoCounts : IDaoCounts
    {
        private readonly IReadOnlyDictionary<string, long> _counts;

        public RecordingDaoCounts(IReadOnlyDictionary<string, long> counts)
        {
            _counts = counts;
        }

        public long Count(string tableName, string criteria)
        {
            string key = tableName + ":" + criteria;
            return _counts.TryGetValue(key, out long value) ? value : 0L;
        }
    }

    private sealed class ThrowingDaoCounts : IDaoCounts
    {
        private readonly Exception _exception;

        public ThrowingDaoCounts(Exception exception)
        {
            _exception = exception;
        }

        public long Count(string tableName, string criteria)
        {
            throw _exception;
        }
    }

    private sealed class NoopStringRepository : IStringRepository
    {
        public string ReturnValue { get; init; } = "msg";
        public ProbeStringId? LastId { get; private set; }
        public object[] LastArgs { get; private set; } = [];

        public string GetString(ProbeStringId id, params object[] args)
        {
            LastId = id;
            LastArgs = args;
            return ReturnValue;
        }
    }

    private sealed class NoopDialogService : IDialogService
    {
        public void OpenForm(string formName, string openArgs) { }

        public bool IsFormOpen(string formName) => false;

        public void CloseForm(string formName) { }
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public bool IsOpenAfterShow { get; init; }
        public string? LastOpenedFormName { get; private set; }
        public string? LastOpenArgs { get; private set; }
        public string? LastClosedFormName { get; private set; }

        public void OpenForm(string formName, string openArgs)
        {
            LastOpenedFormName = formName;
            LastOpenArgs = openArgs;
        }

        public bool IsFormOpen(string formName) => IsOpenAfterShow;

        public void CloseForm(string formName)
        {
            LastClosedFormName = formName;
        }
    }

    private sealed class RecordingErrorHandler : IErrorHandler
    {
        public List<(string ObjectName, string ProcedureName, bool ShowUserMessage)> Calls { get; } = [];

        public void HandleError(string objectName, string procedureName, bool showUserMessage = false)
        {
            Calls.Add((objectName, procedureName, showUserMessage));
        }
    }
}
