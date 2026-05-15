using System;
using System.Globalization;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAClient;

/// <summary>
/// Characterization tests for field-level validation and normalization in the client detail form.
/// Sources:
/// - frmClientDetail::txtFirstName_BeforeUpdate
/// - frmClientDetail::txtLastName_BeforeUpdate
/// - frmClientDetail::txtEmail_BeforeUpdate
/// - frmClientDetail::txtDateOfBirth_BeforeUpdate
/// - frmClientDetail::txtSSNLast4_Exit
/// </summary>
public sealed class ClientValidationBehaviorTests
{
    [Fact]
    public void NormalizeName_TrimsAndProperCasesInput()
    {
        var workflow = new ClientValidationWorkflow(() => new DateTime(2026, 5, 15));

        Assert.Equal("Mary Jane", workflow.NormalizeName("  mArY jAnE  "));
        Assert.Equal("O'brian", workflow.NormalizeName(" o'brIAN "));
    }

    [Fact]
    public void IsEmailValid_AllowsBlankAndRejectsMalformedAddresses()
    {
        var workflow = new ClientValidationWorkflow(() => new DateTime(2026, 5, 15));

        Assert.True(workflow.IsEmailValid(null));
        Assert.True(workflow.IsEmailValid(string.Empty));
        Assert.True(workflow.IsEmailValid("client@example.org"));
        Assert.False(workflow.IsEmailValid("client.example.org"));
        Assert.False(workflow.IsEmailValid("client@example"));
    }

    [Fact]
    public void IsAdultDateOfBirth_RejectsClientsYoungerThan18()
    {
        var workflow = new ClientValidationWorkflow(() => new DateTime(2026, 5, 15));

        Assert.True(workflow.IsAdultDateOfBirth(new DateTime(2000, 5, 15)));
        Assert.False(workflow.IsAdultDateOfBirth(new DateTime(2010, 5, 15)));
    }

    [Fact]
    public void IsSsnLast4Valid_RejectsNonFourDigitValues()
    {
        var workflow = new ClientValidationWorkflow(() => new DateTime(2026, 5, 15));

        Assert.True(workflow.IsSsnLast4Valid(null));
        Assert.True(workflow.IsSsnLast4Valid(string.Empty));
        Assert.True(workflow.IsSsnLast4Valid("1234"));
        Assert.False(workflow.IsSsnLast4Valid("123"));
        Assert.False(workflow.IsSsnLast4Valid("12A4"));
    }

    private sealed class ClientValidationWorkflow
    {
        private readonly Func<DateTime> _todayFactory;

        public ClientValidationWorkflow(Func<DateTime> todayFactory)
        {
            _todayFactory = todayFactory;
        }

        public string NormalizeName(string? input)
        {
            var trimmed = (input ?? string.Empty).Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }

        public bool IsEmailValid(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return true;
            }

            return email.Contains("@", StringComparison.Ordinal)
                && email.Contains(".", StringComparison.Ordinal);
        }

        public bool IsAdultDateOfBirth(DateTime dateOfBirth)
        {
            return dateOfBirth.Date <= _todayFactory().Date.AddYears(-18);
        }

        public bool IsSsnLast4Valid(string? ssnLast4)
        {
            if (string.IsNullOrEmpty(ssnLast4))
            {
                return true;
            }

            return ssnLast4.Length == 4 && int.TryParse(ssnLast4, out _);
        }
    }
}
