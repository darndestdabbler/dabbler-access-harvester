using System;
using System.Globalization;

namespace AccessHarvester.TranslationProbe;

public enum ProbeStringId
{
    CompanyDeleteCompanyTypeChange,
}

public interface IFormContext
{
    string Name { get; }
    long CompanyId { get; }
    string MessageCaption { get; }
    IComboBoxControl CompanyTypeIdControl { get; }
}

public interface IComboBoxControl
{
    void Undo();
}

public interface IDaoCounts
{
    long Count(string tableName, string criteria);
}

public interface IStringRepository
{
    string GetString(ProbeStringId id, params object[] args);
}

public interface IDialogService
{
    void OpenForm(string formName, string openArgs);
    bool IsFormOpen(string formName);
    void CloseForm(string formName);
}

public interface IErrorHandler
{
    void HandleError(string objectName, string procedureName, bool showUserMessage = false);
}

public readonly record struct CompanyDetailProbeState(
    long CompanyContactCount,
    long CustomerOrderCount,
    long ShipperOrderCount,
    long VendorPurchaseOrderCount,
    long VendorProductCount);

public sealed class FrmCompanyDetailTranslationProbe
{
    private readonly IDaoCounts _daoCounts;
    private readonly IStringRepository _strings;
    private readonly IDialogService _dialogs;
    private readonly IErrorHandler _errors;

    private long _companyContactCount;
    private long _customerOrderCount;
    private long _shipperOrderCount;
    private long _vendorPurchaseOrderCount;
    private long _vendorProductCount;

    public FrmCompanyDetailTranslationProbe(
        IDaoCounts daoCounts,
        IStringRepository strings,
        IDialogService dialogs,
        IErrorHandler errors)
    {
        _daoCounts = daoCounts;
        _strings = strings;
        _dialogs = dialogs;
        _errors = errors;
    }

    public CompanyDetailProbeState SnapshotState() =>
        new(
            _companyContactCount,
            _customerOrderCount,
            _shipperOrderCount,
            _vendorPurchaseOrderCount,
            _vendorProductCount);

    public bool CboCompanyTypeIdBeforeUpdate(IFormContext form)
    {
        try
        {
            if (!CompanyIsActive(form) && _vendorProductCount <= 0)
            {
                return false;
            }

            string dialogMessage = _strings.GetString(
                ProbeStringId.CompanyDeleteCompanyTypeChange,
                "change the Company Type",
                _customerOrderCount,
                _shipperOrderCount,
                _vendorPurchaseOrderCount,
                $"Vendor Products ({_vendorProductCount})");

            string openArgs = ProbeVbaFormatting.StringFormat(
                "Header={0}&Message={1}&Actions={2}&FormCaption={3}",
                "Company Type Change Not Allowed",
                dialogMessage,
                "vbOK",
                form.MessageCaption);

            _dialogs.OpenForm("frmGenericDialog", openArgs);

            if (_dialogs.IsFormOpen("frmGenericDialog"))
            {
                _dialogs.CloseForm("frmGenericDialog");
            }

            form.CompanyTypeIdControl.Undo();
            return true;
        }
        catch (Exception)
        {
            _errors.HandleError(form.Name, "cboCompanyTypeID_BeforeUpdate", true);
            return false;
        }
    }

    private bool CompanyIsActive(IFormContext form)
    {
        try
        {
            _companyContactCount = _daoCounts.Count(
                "Contacts",
                ProbeVbaFormatting.StringFormatSql("CompanyID={0}", form.CompanyId));
            _customerOrderCount = _daoCounts.Count(
                "Orders",
                ProbeVbaFormatting.StringFormatSql("CustomerID={0}", form.CompanyId));
            _shipperOrderCount = _daoCounts.Count(
                "Orders",
                ProbeVbaFormatting.StringFormatSql("ShipperID={0}", form.CompanyId));
            _vendorPurchaseOrderCount = _daoCounts.Count(
                "PurchaseOrders",
                ProbeVbaFormatting.StringFormatSql("VendorID={0}", form.CompanyId));
            _vendorProductCount = _daoCounts.Count(
                "ProductVendors",
                ProbeVbaFormatting.StringFormatSql("VendorID={0}", form.CompanyId));

            long activeOrderCount =
                _customerOrderCount +
                _shipperOrderCount +
                _vendorPurchaseOrderCount;

            return activeOrderCount > 0;
        }
        catch (Exception)
        {
            _errors.HandleError(form.Name, "CompanyIsActive");
            return false;
        }
    }
}

internal static class ProbeVbaFormatting
{
    public static string StringFormat(string template, params object?[] args)
    {
        string result = template;
        for (int i = 0; i < args.Length; i++)
        {
            string replacement = Convert.ToString(args[i], CultureInfo.InvariantCulture) ?? string.Empty;
            result = result.Replace("{" + i + "}", replacement, StringComparison.Ordinal);
        }

        return result;
    }

    public static string StringFormatSql(string template, params object?[] args)
    {
        object?[] sqlArgs = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            sqlArgs[i] = FormatSqlLiteral(args[i]);
        }

        return StringFormat(template, sqlArgs);
    }

    private static string FormatSqlLiteral(object? value)
    {
        return value switch
        {
            null => "NULL",
            string text => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'",
            DateTime date => "#" + date.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture) + "#",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
