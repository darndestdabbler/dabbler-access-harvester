using System;
using System.Collections.Generic;

namespace AccessHarvester.CharacterizationTests.UpdateAnEmployee;

internal sealed class EmployeeDraft
{
    public long? EmployeeId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? JobTitle { get; set; }

    public string? WindowsUserName { get; set; }
}

internal sealed class EmployeeIdentitySnapshot
{
    public long EmployeeId { get; set; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public string? WindowsUserName { get; init; }
}

internal sealed class EmployeeDeleteDependencies
{
    public int OrderCount { get; init; }

    public int PurchaseOrderCount { get; init; }

    public int SuperviseeCount { get; init; }

    public int PrivilegeCount { get; init; }
}

internal sealed class MruRemoval
{
    public required string TableName { get; init; }

    public long PkValue { get; init; }
}

internal sealed class EmployeeDeletePreparation
{
    public required bool CanDelete { get; init; }

    public required IReadOnlyList<string> BlockingReasons { get; init; }

    public required IReadOnlyList<MruRemoval> MruRemovals { get; init; }

    public string? ConfirmationQueueName { get; init; }
}

internal sealed class PrivilegeAssignmentLaunch
{
    public required long OpenArgsEmployeeId { get; init; }
}

internal static class UpdateEmployeeWorkflow
{
    public static bool CanSave(EmployeeDraft employee)
    {
        return !string.IsNullOrWhiteSpace(employee.FirstName)
            && !string.IsNullOrWhiteSpace(employee.LastName)
            && !string.IsNullOrWhiteSpace(employee.JobTitle);
    }

    public static bool HasUniqueFullName(
        EmployeeDraft employee,
        IEnumerable<EmployeeIdentitySnapshot> existingEmployees)
    {
        if (string.IsNullOrWhiteSpace(employee.FirstName)
            || string.IsNullOrWhiteSpace(employee.LastName))
        {
            return true;
        }

        foreach (var existing in existingEmployees)
        {
            if (employee.EmployeeId.HasValue && existing.EmployeeId == employee.EmployeeId.Value)
            {
                continue;
            }

            if (string.Equals(existing.FirstName, employee.FirstName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.LastName, employee.LastName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasUniqueWindowsUserName(
        EmployeeDraft employee,
        IEnumerable<EmployeeIdentitySnapshot> existingEmployees)
    {
        if (string.IsNullOrWhiteSpace(employee.WindowsUserName))
        {
            return true;
        }

        foreach (var existing in existingEmployees)
        {
            if (employee.EmployeeId.HasValue && existing.EmployeeId == employee.EmployeeId.Value)
            {
                continue;
            }

            if (string.Equals(existing.WindowsUserName, employee.WindowsUserName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static EmployeeDeletePreparation PrepareDelete(
        EmployeeDeleteDependencies dependencies,
        string employeeDisplayName,
        long? ordersMruEmployeeId,
        long? purchaseOrdersMruEmployeeId)
    {
        var blockingReasons = new List<string>();

        if (dependencies.OrderCount > 0)
        {
            blockingReasons.Add("orders");
        }

        if (dependencies.PurchaseOrderCount > 0)
        {
            blockingReasons.Add("purchase-orders");
        }

        if (dependencies.SuperviseeCount > 0)
        {
            blockingReasons.Add("supervisees");
        }

        if (dependencies.PrivilegeCount > 0)
        {
            blockingReasons.Add("privileges");
        }

        if (blockingReasons.Count > 0)
        {
            return new EmployeeDeletePreparation
            {
                CanDelete = false,
                BlockingReasons = blockingReasons,
                MruRemovals = Array.Empty<MruRemoval>(),
                ConfirmationQueueName = null,
            };
        }

        var removals = new List<MruRemoval>();
        if (ordersMruEmployeeId.HasValue)
        {
            removals.Add(new MruRemoval
            {
                TableName = "Orders",
                PkValue = ordersMruEmployeeId.Value,
            });
        }

        if (purchaseOrdersMruEmployeeId.HasValue)
        {
            removals.Add(new MruRemoval
            {
                TableName = "PurchaseOrders",
                PkValue = purchaseOrdersMruEmployeeId.Value,
            });
        }

        return new EmployeeDeletePreparation
        {
            CanDelete = true,
            BlockingReasons = Array.Empty<string>(),
            MruRemovals = removals,
            ConfirmationQueueName = employeeDisplayName,
        };
    }

    public static PrivilegeAssignmentLaunch BeginPrivilegeAssignment(long employeeId)
    {
        return new PrivilegeAssignmentLaunch
        {
            OpenArgsEmployeeId = employeeId,
        };
    }

    public static long? ResolvePrivilegeEditorDefaultEmployeeId(long? openArgsEmployeeId)
    {
        if (!openArgsEmployeeId.HasValue || openArgsEmployeeId.Value <= 0)
        {
            return null;
        }

        return openArgsEmployeeId.Value;
    }
}
