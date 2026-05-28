using System;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Shared helper for detecting inline Access SQL strings so they are not
/// mistaken for table or query names. Covers common Access SQL prefixes
/// including SELECT, PARAMETERS, TRANSFORM, WITH, INSERT, UPDATE, DELETE.
/// </summary>
internal static class SqlDetection
{
    private static readonly string[] _sqlPrefixes =
    [
        "SELECT", "PARAMETERS", "TRANSFORM", "WITH",
        "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER",
    ];

    /// <summary>
    /// Returns true when the string looks like inline SQL rather than an object name.
    /// </summary>
    public static bool IsInlineSql(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string trimmed = value.TrimStart();
        foreach (var prefix in _sqlPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // Require a non-identifier character after the keyword so that
                // table names like "Selected" or "Params" do not false-fire.
                if (trimmed.Length == prefix.Length ||
                    !char.IsLetterOrDigit(trimmed[prefix.Length]) && trimmed[prefix.Length] != '_')
                    return true;
            }
        }
        return false;
    }
}
