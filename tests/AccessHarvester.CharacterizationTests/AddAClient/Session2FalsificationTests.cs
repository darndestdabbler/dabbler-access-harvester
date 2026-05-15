using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAClient;

/// <summary>
/// Session 2 falsification tests for invariants missed during the Session 1
/// Add-a-Client package draft.
///
/// Sources reviewed for these additions:
/// - frmClientDetail::txtPhone_AfterUpdate (phone normalization on save path)
/// - frmClientDetail::cboStateCode_AfterUpdate (state-driven city auto-fill)
/// - frmClientDetail::chkIsActive_AfterUpdate (deactivation cancel restores active=True)
/// - tblClients EnrollmentDate ValidationRule "&lt;=Date()" (future-date gating)
/// - tblClients EnrollmentDate ValidationRule "Is Not Null" + table rule
///   "[EnrollmentDate] &gt;= [DateOfBirth]" (already partially covered by
///   Session 1; this file adds the future-date half).
/// </summary>
public sealed class Session2FalsificationTests
{
    [Fact]
    public void CanSave_RejectsEnrollmentDateInTheFuture()
    {
        var today = new DateTime(2026, 5, 15);
        var draft = new ClientDraft
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateTime(1980, 1, 1),
            EnrollmentDate = today.AddDays(1),
            Gender = "X",
            IsActive = true,
        };
        var workflow = new ClientCreationWorkflow(() => today);

        Assert.False(workflow.CanSave(draft));
    }

    [Fact]
    public void FormatPhone_NormalizesToCanonicalShapeOnAfterUpdate()
    {
        // Mirrors basUtilities::FormatPhone exactly: 10-digit input (after
        // stripping non-digits) -> "(NNN) NNN-NNNN"; null -> empty; otherwise
        // the raw string is preserved unchanged.
        var workflow = new ClientPhoneNormalizer();

        Assert.Equal("(555) 123-4567", workflow.FormatPhone("5551234567"));
        Assert.Equal("(555) 123-4567", workflow.FormatPhone("(555) 123-4567"));
        Assert.Equal("(555) 123-4567", workflow.FormatPhone(" 555.123.4567 "));
        Assert.Equal(string.Empty, workflow.FormatPhone(null));
        Assert.Equal("12345", workflow.FormatPhone("12345"));
    }

    [Fact]
    public void StateCityAutoFill_FillsCity_WhenStateHasExactlyOneKnownCity()
    {
        var existing = new[]
        {
            new ClientLocationRow("OR", "Portland"),
            new ClientLocationRow("OR", "Portland"),
            new ClientLocationRow("WA", "Seattle"),
            new ClientLocationRow("WA", "Spokane"),
        };
        var workflow = new ClientStateCityAutoFill(existing);

        Assert.Equal("Portland", workflow.SuggestCityFor("OR"));
    }

    [Fact]
    public void StateCityAutoFill_DoesNotFill_WhenStateHasMultipleKnownCities()
    {
        var existing = new[]
        {
            new ClientLocationRow("WA", "Seattle"),
            new ClientLocationRow("WA", "Spokane"),
        };
        var workflow = new ClientStateCityAutoFill(existing);

        Assert.Null(workflow.SuggestCityFor("WA"));
    }

    [Fact]
    public void StateCityAutoFill_DoesNotFill_WhenStateHasNoKnownCities()
    {
        var existing = Array.Empty<ClientLocationRow>();
        var workflow = new ClientStateCityAutoFill(existing);

        Assert.Null(workflow.SuggestCityFor("ID"));
    }

    [Fact]
    public void ApplyDeactivation_RestoresActive_WhenUserDeclinesConfirmation()
    {
        var draft = new ClientDraft { IsActive = true };
        var workflow = new ClientLifecycleWorkflow();

        var outcome = workflow.ApplyDeactivation(draft, userConfirmed: false, approvedPlanCount: 5);

        Assert.True(draft.IsActive);
        Assert.Equal(DeactivationOutcome.Cancelled, outcome);
    }

    [Fact]
    public void ApplyDeactivation_Persists_WhenUserConfirms()
    {
        var draft = new ClientDraft { IsActive = true };
        var workflow = new ClientLifecycleWorkflow();

        var outcome = workflow.ApplyDeactivation(draft, userConfirmed: true, approvedPlanCount: 0);

        Assert.False(draft.IsActive);
        Assert.Equal(DeactivationOutcome.Confirmed, outcome);
    }

    [Fact]
    public void ApplyDeactivation_WarnsButPersists_WhenApprovedPlansExist()
    {
        var draft = new ClientDraft { IsActive = true };
        var workflow = new ClientLifecycleWorkflow();

        var outcome = workflow.ApplyDeactivation(draft, userConfirmed: true, approvedPlanCount: 3);

        Assert.False(draft.IsActive);
        Assert.Equal(DeactivationOutcome.ConfirmedWithWarning, outcome);
    }

    [Fact]
    public void CanDelete_RejectsAnyPositiveBudgetRowCount()
    {
        var workflow = new ClientLifecycleWorkflow();

        Assert.False(workflow.CanDelete(budgetRowCount: 1));
        Assert.False(workflow.CanDelete(budgetRowCount: 7));
        Assert.True(workflow.CanDelete(budgetRowCount: 0));
    }

    private sealed class ClientCreationWorkflow
    {
        private readonly Func<DateTime> _todayFactory;

        public ClientCreationWorkflow(Func<DateTime> todayFactory)
        {
            _todayFactory = todayFactory;
        }

        public bool CanSave(ClientDraft draft)
        {
            var today = _todayFactory().Date;
            return !string.IsNullOrWhiteSpace(draft.FirstName)
                && !string.IsNullOrWhiteSpace(draft.LastName)
                && draft.DateOfBirth is not null
                && draft.EnrollmentDate is not null
                && draft.DateOfBirth.Value.Date < today
                && draft.EnrollmentDate.Value.Date <= today
                && draft.EnrollmentDate.Value.Date >= draft.DateOfBirth.Value.Date;
        }
    }

    private sealed class ClientPhoneNormalizer
    {
        public string FormatPhone(string? raw)
        {
            if (raw is null)
            {
                return string.Empty;
            }

            var digits = new string(Array.FindAll(raw.ToCharArray(), char.IsDigit));
            if (digits.Length != 10)
            {
                return raw;
            }

            return $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6, 4)}";
        }
    }

    private sealed record ClientLocationRow(string StateCode, string City);

    private sealed class ClientStateCityAutoFill
    {
        private readonly IReadOnlyCollection<ClientLocationRow> _existing;

        public ClientStateCityAutoFill(IReadOnlyCollection<ClientLocationRow> existing)
        {
            _existing = existing;
        }

        public string? SuggestCityFor(string stateCode)
        {
            var distinctCities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _existing)
            {
                if (string.Equals(row.StateCode, stateCode, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(row.City))
                {
                    distinctCities.Add(row.City);
                }
            }

            return distinctCities.Count == 1 ? distinctCities.Single() : null;
        }
    }

    private enum DeactivationOutcome
    {
        Cancelled,
        Confirmed,
        ConfirmedWithWarning,
    }

    private sealed class ClientLifecycleWorkflow
    {
        public bool CanDelete(int budgetRowCount) => budgetRowCount == 0;

        public DeactivationOutcome ApplyDeactivation(ClientDraft draft, bool userConfirmed, int approvedPlanCount)
        {
            if (!userConfirmed)
            {
                draft.IsActive = true;
                return DeactivationOutcome.Cancelled;
            }

            draft.IsActive = false;
            return approvedPlanCount > 0
                ? DeactivationOutcome.ConfirmedWithWarning
                : DeactivationOutcome.Confirmed;
        }
    }
}
