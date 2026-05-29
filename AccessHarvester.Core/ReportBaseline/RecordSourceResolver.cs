using System;
using System.Text.RegularExpressions;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Classifies a raw report <c>RecordSource</c> string as either an inline
/// SQL statement or an Access object name (table or saved query), and
/// wraps object-name values in <c>SELECT * FROM [name]</c> so the executor
/// always receives a runnable SQL string.
///
/// <para>
/// Classification heuristic: if the trimmed value's first SQL token is
/// <c>SELECT</c>, <c>PARAMETERS</c>, or <c>TRANSFORM</c>
/// (case-insensitive), it is inline JET SQL. These are the three forms
/// Access accepts as a <c>RecordSource</c> SQL string:
/// <c>SELECT ...</c>, <c>PARAMETERS &lt;type-list&gt;; SELECT ...</c>,
/// and crosstab <c>TRANSFORM ... PIVOT ...</c>. Anything else is an
/// object name (table or saved query); object names cannot collide with
/// these tokens because Access reserves them. The two-way classification
/// is exhaustive for any RecordSource Access will accept.
/// </para>
/// </summary>
public static class RecordSourceResolver
{
    private static readonly Regex InlineSqlPrefix = new(
        @"^\s*(SELECT|PARAMETERS|TRANSFORM)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Classify and shape a RecordSource string. Throws when the input is
    /// null, empty, or whitespace-only — an unbound report has no
    /// RecordSource at all and should be filtered out by the caller.
    /// </summary>
    public static ResolvedRecordSource Resolve(string recordSource)
    {
        if (recordSource is null) throw new ArgumentNullException(nameof(recordSource));
        if (string.IsNullOrWhiteSpace(recordSource))
            throw new ArgumentException(
                "RecordSource is empty; unbound reports must be filtered before resolution.",
                nameof(recordSource));

        if (InlineSqlPrefix.IsMatch(recordSource))
        {
            return new ResolvedRecordSource
            {
                Original = recordSource,
                Kind = RecordSourceKind.InlineSql,
                SqlToExecute = recordSource,
            };
        }

        string name = recordSource.Trim();
        if (Regex.IsMatch(name, @"[\]\0\r\n]"))
        {
            throw new ArgumentException(
                $"RecordSource object name '{name}' contains characters that " +
                "cannot be safely bracket-quoted in Access SQL.",
                nameof(recordSource));
        }

        return new ResolvedRecordSource
        {
            Original = recordSource,
            Kind = RecordSourceKind.TableOrQueryName,
            SqlToExecute = $"SELECT * FROM [{name}]",
        };
    }
}
