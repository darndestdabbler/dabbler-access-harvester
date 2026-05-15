using System;

namespace AccessHarvester.CharacterizationTests.AddAClient;

internal sealed class ClientDraft
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public DateTime? EnrollmentDate { get; set; }

    public string? Gender { get; set; }

    public bool IsActive { get; set; }

    public string? Email { get; set; }

    public string? SsnLast4 { get; set; }
}
