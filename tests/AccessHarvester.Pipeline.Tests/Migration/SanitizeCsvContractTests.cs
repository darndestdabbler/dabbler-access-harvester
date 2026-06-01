using System;
using System.IO;
using AccessHarvester.Pipeline.Migration;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// Pins <see cref="SanitizeCsvContract"/> against the actual <see cref="CsvWriter"/>
/// output so the migration generator's documented data-input contract cannot drift
/// from the sanitize subcommand's real producer behavior (cf. the lessons-learned
/// "verify docs against the tool's actual contract" rule). The rows are consumed in
/// Session 2; this guards the contract now.
/// </summary>
public class SanitizeCsvContractTests
{
    [Fact]
    public void CsvWriter_output_matches_the_documented_contract_tokens()
    {
        using var sw = new StringWriter();
        using (var w = new CsvWriter(sw, ownsWriter: false))
            w.WriteRow(new object?[] { true, false, null, new DateTime(2020, 1, 2, 3, 4, 5), 1.5m });

        string line = sw.ToString();

        Assert.Contains(SanitizeCsvContract.BoolTrue, line);                              // "true"
        Assert.Contains(SanitizeCsvContract.BoolFalse, line);                             // "false"
        Assert.Contains("2020-01-02T03:04:05", line);                                     // ISO-8601 date format
        Assert.Contains("1.5", line);                                                     // invariant numeric
        Assert.Contains("true,false,,", line);                                            // NULL -> empty field
        Assert.Equal("yyyy-MM-ddTHH:mm:ss", SanitizeCsvContract.DateTimeFormat);
        Assert.Equal("", SanitizeCsvContract.NullToken);
        Assert.Equal(".csv", SanitizeCsvContract.FileExtension);
    }

    [Fact]
    public void CsvWriter_applies_rfc4180_quoting_for_special_characters()
    {
        using var sw = new StringWriter();
        using (var w = new CsvWriter(sw, ownsWriter: false))
            w.WriteRow(new object?[] { "a,b", "he said \"hi\"", "line1\nline2", "plain" });

        string line = sw.ToString();

        Assert.True(SanitizeCsvContract.Rfc4180Quoting);
        Assert.Contains("\"a,b\"", line);                 // comma -> quoted
        Assert.Contains("\"he said \"\"hi\"\"\"", line);  // embedded quote -> doubled inside quotes
        Assert.Contains("\"line1\nline2\"", line);        // newline -> quoted
        Assert.Contains("plain", line);                    // no special char -> unquoted
    }

    [Fact]
    public void CsvWriter_writes_a_header_row_of_column_names()
    {
        using var sw = new StringWriter();
        using (var w = new CsvWriter(sw, ownsWriter: false))
        {
            w.WriteHeader(new[] { "Id", "Name", "When" });
            w.WriteRow(new object?[] { 1, "x", null });
        }

        string text = sw.ToString();
        Assert.True(SanitizeCsvContract.HasHeaderRow);
        Assert.StartsWith("Id,Name,When", text); // header first, in column order
    }
}
