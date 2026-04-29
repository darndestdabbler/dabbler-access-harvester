using System;
using System.Collections.Generic;
using AccessHarvester.TranslationProbe;
using Xunit;

namespace AccessHarvester.TranslationProbe.Tests;

public sealed class FrmCompanyDetailTranslationProbeTests
{
    [Fact]
    public void CboCompanyTypeIdBeforeUpdate_CancelsAndShowsDialog_WhenCompanyIsActive()
    {
        var combo = new FakeComboBoxControl();
        var form = new FakeFormContext(combo);
        var dao = new FakeDaoCounts(new Dictionary<string, long>
        {
            ["Contacts"] = 0,
            ["Orders:CustomerID=42"] = 2,
            ["Orders:ShipperID=42"] = 0,
            ["PurchaseOrders:VendorID=42"] = 1,
            ["ProductVendors:VendorID=42"] = 4,
        });
        var strings = new FakeStringRepository { ReturnValue = "blocked-message" };
        var dialogs = new FakeDialogService { IsOpen = true };
        var errors = new FakeErrorHandler();
        var sut = new FrmCompanyDetailTranslationProbe(dao, strings, dialogs, errors);

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(form);

        Assert.True(cancel);
        Assert.True(combo.UndoCalled);
        Assert.Equal("frmGenericDialog", dialogs.LastOpenedFormName);
        Assert.Equal(
            "Header=Company Type Change Not Allowed&Message=blocked-message&Actions=vbOK&FormCaption=Company Detail",
            dialogs.LastOpenArgs);
        Assert.Equal("frmGenericDialog", dialogs.LastClosedFormName);
        Assert.Equal(ProbeStringId.CompanyDeleteCompanyTypeChange, strings.LastId);
        Assert.Equal(
            new object[] { "change the Company Type", 2L, 0L, 1L, "Vendor Products (4)" },
            strings.LastArgs);
        Assert.Empty(errors.Calls);

        CompanyDetailProbeState state = sut.SnapshotState();
        Assert.Equal(2, state.CustomerOrderCount);
        Assert.Equal(1, state.VendorPurchaseOrderCount);
        Assert.Equal(4, state.VendorProductCount);
    }

    [Fact]
    public void CboCompanyTypeIdBeforeUpdate_DoesNothing_WhenCompanyIsInactiveAndHasNoVendorProducts()
    {
        var combo = new FakeComboBoxControl();
        var form = new FakeFormContext(combo);
        var dao = new FakeDaoCounts(new Dictionary<string, long>
        {
            ["Contacts"] = 3,
            ["Orders:CustomerID=42"] = 0,
            ["Orders:ShipperID=42"] = 0,
            ["PurchaseOrders:VendorID=42"] = 0,
            ["ProductVendors:VendorID=42"] = 0,
        });
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            new FakeStringRepository(),
            new FakeDialogService(),
            new FakeErrorHandler());

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(form);

        Assert.False(cancel);
        Assert.False(combo.UndoCalled);
    }

    [Fact]
    public void CboCompanyTypeIdBeforeUpdate_Cancels_WhenOnlyVendorProductsExist()
    {
        var combo = new FakeComboBoxControl();
        var form = new FakeFormContext(combo);
        var dao = new FakeDaoCounts(new Dictionary<string, long>
        {
            ["Contacts"] = 0,
            ["Orders:CustomerID=42"] = 0,
            ["Orders:ShipperID=42"] = 0,
            ["PurchaseOrders:VendorID=42"] = 0,
            ["ProductVendors:VendorID=42"] = 7,
        });
        var strings = new FakeStringRepository { ReturnValue = "vendor-products-block" };
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            strings,
            new FakeDialogService(),
            new FakeErrorHandler());

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(form);

        Assert.True(cancel);
        Assert.Equal(
            new object[] { "change the Company Type", 0L, 0L, 0L, "Vendor Products (7)" },
            strings.LastArgs);
    }

    [Fact]
    public void CboCompanyTypeIdBeforeUpdate_DelegatesErrorsToHandler()
    {
        var combo = new FakeComboBoxControl();
        var form = new FakeFormContext(combo);
        var dao = new FakeDaoCounts(new Dictionary<string, long>
        {
            ["Contacts"] = 0,
            ["Orders:CustomerID=42"] = 1,
            ["Orders:ShipperID=42"] = 0,
            ["PurchaseOrders:VendorID=42"] = 0,
            ["ProductVendors:VendorID=42"] = 0,
        });
        var strings = new FakeStringRepository { ExceptionToThrow = new InvalidOperationException("boom") };
        var errors = new FakeErrorHandler();
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            strings,
            new FakeDialogService(),
            errors);

        bool cancel = sut.CboCompanyTypeIdBeforeUpdate(form);

        Assert.False(cancel);
        Assert.False(combo.UndoCalled);
        var call = Assert.Single(errors.Calls);
        Assert.Equal("frmCompanyDetail", call.ObjectName);
        Assert.Equal("cboCompanyTypeID_BeforeUpdate", call.ProcedureName);
        Assert.True(call.ShowUserMessage);
    }

    [Fact]
    public void CboCompanyTypeIdBeforeUpdate_FormatsDaoCriteriaFromCompanyId()
    {
        var form = new FakeFormContext(new FakeComboBoxControl()) { CompanyId = 314 };
        var dao = new FakeDaoCounts(new Dictionary<string, long>
        {
            ["Contacts"] = 0,
            ["Orders:CustomerID=314"] = 0,
            ["Orders:ShipperID=314"] = 0,
            ["PurchaseOrders:VendorID=314"] = 0,
            ["ProductVendors:VendorID=314"] = 0,
        });
        var sut = new FrmCompanyDetailTranslationProbe(
            dao,
            new FakeStringRepository(),
            new FakeDialogService(),
            new FakeErrorHandler());

        _ = sut.CboCompanyTypeIdBeforeUpdate(form);

        Assert.Equal(
            new[]
            {
                ("Contacts", "CompanyID=314"),
                ("Orders", "CustomerID=314"),
                ("Orders", "ShipperID=314"),
                ("PurchaseOrders", "VendorID=314"),
                ("ProductVendors", "VendorID=314"),
            },
            dao.Calls);
    }

    private sealed class FakeFormContext : IFormContext
    {
        public FakeFormContext(FakeComboBoxControl comboBox)
        {
            CompanyTypeIdControl = comboBox;
        }

        public string Name { get; init; } = "frmCompanyDetail";
        public long CompanyId { get; init; } = 42;
        public string MessageCaption { get; init; } = "Company Detail";
        public IComboBoxControl CompanyTypeIdControl { get; }
    }

    private sealed class FakeComboBoxControl : IComboBoxControl
    {
        public bool UndoCalled { get; private set; }

        public void Undo()
        {
            UndoCalled = true;
        }
    }

    private sealed class FakeDaoCounts : IDaoCounts
    {
        private readonly IReadOnlyDictionary<string, long> _counts;

        public FakeDaoCounts(IReadOnlyDictionary<string, long> counts)
        {
            _counts = counts;
        }

        public List<(string TableName, string Criteria)> Calls { get; } = [];

        public long Count(string tableName, string criteria)
        {
            Calls.Add((tableName, criteria));
            if (_counts.TryGetValue(tableName + ":" + criteria, out long scopedValue))
            {
                return scopedValue;
            }

            if (_counts.TryGetValue(tableName, out long tableValue))
            {
                return tableValue;
            }

            return 0;
        }
    }

    private sealed class FakeStringRepository : IStringRepository
    {
        public ProbeStringId? LastId { get; private set; }
        public object[] LastArgs { get; private set; } = [];
        public string ReturnValue { get; init; } = "translated-message";
        public Exception? ExceptionToThrow { get; init; }

        public string GetString(ProbeStringId id, params object[] args)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            LastId = id;
            LastArgs = args;
            return ReturnValue;
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public bool IsOpen { get; init; }
        public string? LastOpenedFormName { get; private set; }
        public string? LastOpenArgs { get; private set; }
        public string? LastClosedFormName { get; private set; }

        public void OpenForm(string formName, string openArgs)
        {
            LastOpenedFormName = formName;
            LastOpenArgs = openArgs;
        }

        public bool IsFormOpen(string formName) => IsOpen;

        public void CloseForm(string formName)
        {
            LastClosedFormName = formName;
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<(string ObjectName, string ProcedureName, bool ShowUserMessage)> Calls { get; } = [];

        public void HandleError(string objectName, string procedureName, bool showUserMessage = false)
        {
            Calls.Add((objectName, procedureName, showUserMessage));
        }
    }
}
