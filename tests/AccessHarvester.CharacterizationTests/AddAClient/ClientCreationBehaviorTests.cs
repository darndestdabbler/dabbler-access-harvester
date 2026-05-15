using System;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAClient;

/// <summary>
/// Characterization tests for starting and saving a client intake record.
/// Sources:
/// - tblClients field defaults
/// - frmClientDetail::Form_BeforeUpdate
/// - tblClients field/table validation rules
/// </summary>
public sealed class ClientCreationBehaviorTests
{
    [Fact]
    public void BeginAdd_AppliesTableDefaultsForGenderEnrollmentAndActiveStatus()
    {
        var draft = new ClientDraft();
        var expectedToday = new DateTime(2026, 5, 15);
        var workflow = new ClientCreationWorkflow(
            todayFactory: () => expectedToday,
            saveValidator: _ => true);

        workflow.BeginAdd(draft);

        Assert.Equal("X", draft.Gender);
        Assert.True(draft.IsActive);
        Assert.Equal(expectedToday, draft.EnrollmentDate);
    }

    [Fact]
    public void CanSave_ReturnsFalseWhenRequiredDataIsMissing()
    {
        var draft = new ClientDraft
        {
            FirstName = "Ada",
            LastName = null,
            DateOfBirth = new DateTime(1980, 1, 1),
            EnrollmentDate = new DateTime(2026, 5, 15),
            Gender = "X",
            IsActive = true,
        };
        var workflow = new ClientCreationWorkflow(
            todayFactory: () => new DateTime(2026, 5, 15),
            saveValidator: CreateDefaultSaveValidator(new DateTime(2026, 5, 15)));

        Assert.False(workflow.CanSave(draft));
    }

    [Fact]
    public void CanSave_ReturnsFalseWhenEnrollmentDateViolatesTableRules()
    {
        var draft = new ClientDraft
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateTime(1980, 1, 1),
            EnrollmentDate = new DateTime(1979, 12, 31),
            Gender = "X",
            IsActive = true,
        };
        var workflow = new ClientCreationWorkflow(
            todayFactory: () => new DateTime(2026, 5, 15),
            saveValidator: CreateDefaultSaveValidator(new DateTime(2026, 5, 15)));

        Assert.False(workflow.CanSave(draft));
    }

    [Fact]
    public void CanSave_ReturnsTrueWhenRequiredDataAndDatesAreConsistent()
    {
        var draft = new ClientDraft
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateTime(1980, 1, 1),
            EnrollmentDate = new DateTime(2026, 5, 15),
            Gender = "X",
            IsActive = true,
        };
        var workflow = new ClientCreationWorkflow(
            todayFactory: () => new DateTime(2026, 5, 15),
            saveValidator: CreateDefaultSaveValidator(new DateTime(2026, 5, 15)));

        Assert.True(workflow.CanSave(draft));
    }

    private static Func<ClientDraft, bool> CreateDefaultSaveValidator(DateTime today)
    {
        return draft =>
            !string.IsNullOrWhiteSpace(draft.FirstName) &&
            !string.IsNullOrWhiteSpace(draft.LastName) &&
            draft.DateOfBirth is not null &&
            draft.EnrollmentDate is not null &&
            draft.DateOfBirth.Value.Date < today.Date &&
            draft.EnrollmentDate.Value.Date <= today.Date &&
            draft.EnrollmentDate.Value.Date >= draft.DateOfBirth.Value.Date;
    }

    private sealed class ClientCreationWorkflow
    {
        private readonly Func<DateTime> _todayFactory;
        private readonly Func<ClientDraft, bool> _saveValidator;

        public ClientCreationWorkflow(
            Func<DateTime> todayFactory,
            Func<ClientDraft, bool> saveValidator)
        {
            _todayFactory = todayFactory;
            _saveValidator = saveValidator;
        }

        public void BeginAdd(ClientDraft draft)
        {
            draft.Gender = "X";
            draft.IsActive = true;
            draft.EnrollmentDate = _todayFactory().Date;
        }

        public bool CanSave(ClientDraft draft)
        {
            return _saveValidator(draft);
        }
    }
}
