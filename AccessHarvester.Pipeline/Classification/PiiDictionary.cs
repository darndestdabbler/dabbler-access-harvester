using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// PII vocabulary + value-pattern regexes seeded from Microsoft Presidio's
/// open-source recognizers (plan § 3.4). Presidio itself is NOT a runtime
/// dependency — only its patterns and context-word lists are lifted as a
/// seed and extended with the harvester's domain terms (MedId, DLNo, etc.).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DefaultPiiNamePatterns"/> are glob-style (use <c>*</c> as the
/// only wildcard) so they can be expressed as a flat YAML/JSON list of
/// strings in <c>ScanConfig</c>. Matching is case-insensitive.
/// </para>
/// <para>
/// <see cref="DefaultPiiValuePatterns"/> map a pattern id (the public
/// identifier persisted in <c>ScanConfig.piiValuePatterns</c>) to the
/// regex applied during value sampling. The regex itself is fixed in the
/// scanner; only the identifiers travel into the JSON artifact so the
/// privacy invariant (plan § 4) is structurally preserved.
/// </para>
/// </remarks>
public static class PiiDictionary
{
    /// <summary>Audit / system column names. Plan § 3.2 requires recurrence across multiple tables on top of the name match.</summary>
    public static readonly IReadOnlyList<string> DefaultSystemColumnPatterns = new[]
    {
        "LastModified*",
        "Modified*",
        "Created*",
        "Updated*",
        "UserId",
        "Timestamp",
        "RowVersion",
        "Version",
    };

    /// <summary>PII column names. Seeded from Presidio's recognizer context-word lists plus harvester-domain additions.</summary>
    public static readonly IReadOnlyList<string> DefaultPiiNamePatterns = new[]
    {
        "First*",
        "Last*",
        "FullName",
        "FName",
        "LName",
        "MiddleName",
        "MName",
        "SSN*",
        "SS#",
        "Soc",
        "Soc*",
        "SocialSec*",
        "DOB",
        "DateOfBirth",
        "BirthDate",
        "Email*",
        "EMail",
        "Phone*",
        "Fax*",
        "Cell*",
        "Mobile*",
        "Addr*",
        "Address*",
        "Street*",
        "City",
        "State",
        "Zip*",
        "PostalCode",
        "Country",
        "MedId",
        "MedRecord*",
        "MRN*",
        "DLNo",
        "LicNo",
        "License*",
        "Passport*",
        "TaxID",
        "EIN",
        "Account*",
        "CreditCard*",
        "CC#",
    };

    /// <summary>Public identifiers for value-sample regex patterns (persisted into ScanConfig.piiValuePatterns).</summary>
    public static readonly IReadOnlyList<string> DefaultPiiValuePatternIds = new[]
    {
        "us-ssn",
        "us-phone",
        "us-zip",
        "email",
        "credit-card",
    };

    /// <summary>The id → regex mapping the scanner applies during value sampling. Raw matches are NEVER stored.</summary>
    public static IReadOnlyDictionary<string, Regex> DefaultPiiValuePatterns { get; } =
        new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase)
        {
            ["us-ssn"]      = new Regex(@"^\d{3}-\d{2}-\d{4}$",                                    RegexOptions.Compiled),
            ["us-phone"]    = new Regex(@"^(\+?1[-. ]?)?(\(?\d{3}\)?[-. ]?)\d{3}[-. ]?\d{4}$",     RegexOptions.Compiled),
            ["us-zip"]      = new Regex(@"^\d{5}(-\d{4})?$",                                       RegexOptions.Compiled),
            ["email"]       = new Regex(@"^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$",                RegexOptions.Compiled | RegexOptions.IgnoreCase),
            ["credit-card"] = new Regex(@"^\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}$",                  RegexOptions.Compiled),
        };

    /// <summary>Translates a glob-style pattern (only <c>*</c> is meaningful) into a case-insensitive anchored <see cref="Regex"/>.</summary>
    public static Regex CompileGlob(string glob)
    {
        if (glob is null) throw new ArgumentNullException(nameof(glob));
        var sb = new System.Text.StringBuilder("^");
        foreach (char c in glob)
        {
            if (c == '*') sb.Append(".*");
            else sb.Append(Regex.Escape(c.ToString()));
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
