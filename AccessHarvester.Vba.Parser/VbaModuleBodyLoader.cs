using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Parser;

// Shared loader for Access-exported VBA module text. Strips the
// canonical leading `VERSION / BEGIN..END / Attribute VB_*` header
// block before the parser sees it AND captures the stripped header
// as a typed `ModuleHeaderMetadata` record so downstream consumers
// can still reach the migration-relevant fields.
//
// Two entry points:
//   Load(rawSource)        → (Body, Header)
//   LoadAndParse(raw, ...) → (Module Ast, ParseResult Result)
//
// Idempotent on no-header inputs: Load(x) returns (x, null) when the
// source carries no recognizable header, and Load(Load(x).Body) is
// always (body, null).
//
// Malformed/partial headers (e.g., `VERSION` without a complete
// `BEGIN..END`) are NOT heuristic-stripped. The loader leaves the
// source untouched and lets the parser fail visibly.
//
// In-body `Attribute`-shaped text (e.g., a `Const Attribute = "..."`
// declaration buried inside a procedure) is parser territory; only
// the canonical *leading* block is touched.
public static class VbaModuleBodyLoader
{
    public static (string Body, ModuleHeaderMetadata? Header) Load(string rawSource)
    {
        if (rawSource is null)
            throw new ArgumentNullException(nameof(rawSource));

        // Strip the optional UTF-8 BOM. Whether or not we end up
        // stripping a header, the BOM should never reach the parser —
        // ANTLR treats it as an `ERROR_TOKEN`.
        string source = rawSource;
        if (source.Length > 0 && source[0] == '﻿')
            source = source.Substring(1);

        var match = CanonicalHeader.Match(source);
        if (!match.Success || match.Index != 0)
            return (source, null);

        var header = ParseHeaderFromMatch(match);
        var body = source.Substring(match.Length);
        return (body, header);
    }

    public static (Module Ast, ParseResult Result) LoadAndParse(
        string rawSource,
        ParseOptions? options = null,
        string? sourceFile = null)
    {
        var (body, header) = Load(rawSource);
        var result = VbaParserPipeline.Parse(body, options);
        var ast = result.BuildAst(sourceFile);
        if (header is not null)
            ast = ast with { Header = header };
        return (ast, result);
    }

    // ── Canonical-header recognition ────────────────────────────────

    // Two shapes are recognized. Either form starts at offset 0 (the
    // BOM is pre-stripped above).
    //
    //   Full SaveAsText form:
    //     VERSION 1.0 CLASS    (optional)
    //     BEGIN                (required if VERSION is present;
    //       <key> = <value>    can appear on its own otherwise)
    //       ...
    //     END
    //     Attribute VB_<name> = <value>   (one or more)
    //     ...
    //
    //   YAML-mirror form (dominant in this repo):
    //     Attribute VB_<name> = <value>   (one or more)
    //     ...
    //
    // Requirement: at least one `Attribute VB_*` line is required for
    // a match — that is the line family that actually breaks the
    // parser. If `VERSION` appears without a complete `BEGIN..END`
    // block or without any trailing `Attribute` line, the regex
    // fails-to-match and the source is left untouched (the spec's
    // "malformed/partial → don't heuristic-strip" rule).
    //
    // `(?m)` is intentionally NOT enabled: we anchor only at the
    // start of the source string, so a stray in-body `Attribute`
    // line cannot trigger a partial header match further in.
    private static readonly Regex CanonicalHeader = new(
        pattern: @"\A" +
                 // VERSION + BEGIN..END are coupled — either both
                 // appear together, or neither does. `VERSION` without
                 // a complete `BEGIN..END` block is the spec's
                 // "malformed/partial" case and must NOT match.
                 @"(?:" +
                     @"VERSION[ \t]+(?<verMaj>\d+)\.(?<verMin>\d+)(?:[ \t]+\w+)?[ \t]*\r?\n" +
                     @"BEGIN[ \t]*\r?\n" +
                         @"(?<beginBody>(?:[ \t]+\w+[ \t]*=[ \t]*[^\r\n]*\r?\n)*?)" +
                     @"END[ \t]*\r?\n" +
                 @")?" +
                 // One or more Attribute VB_<name> = <value> lines.
                 @"(?<attrBlock>(?:Attribute[ \t]+VB_\w+[ \t]*=[ \t]*[^\r\n]*\r?\n)+)",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromSeconds(1));

    // Inside the BEGIN..END block (e.g., `  MultiUse = -1  'True`).
    // Greedy capture up to (but not including) newline; whitespace and
    // optional `'comment` are trimmed in post-processing. Greedy-then-
    // trim avoids the .NET `$`-vs-`\r\n` interaction that bit the
    // first draft on CRLF inputs.
    private static readonly Regex BeginBodyLine = new(
        @"^[ \t]+(?<key>\w+)[ \t]*=[ \t]*(?<value>[^\r\n]*)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex AttributeLine = new(
        @"^Attribute[ \t]+VB_(?<key>\w+)[ \t]*=[ \t]*(?<value>[^\r\n]*)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static ModuleHeaderMetadata ParseHeaderFromMatch(Match headerMatch)
    {
        int? versionMajor = TryParseInt(headerMatch.Groups["verMaj"].Value);
        int? versionMinor = TryParseInt(headerMatch.Groups["verMin"].Value);

        bool? multiUse = null;
        var beginBody = headerMatch.Groups["beginBody"].Value;
        if (!string.IsNullOrEmpty(beginBody))
        {
            foreach (Match m in BeginBodyLine.Matches(beginBody))
            {
                var key = m.Groups["key"].Value;
                var value = StripTrailingComment(m.Groups["value"].Value).Trim();
                if (string.Equals(key, "MultiUse", StringComparison.Ordinal))
                    multiUse = ParseAccessBool(value);
                // Other BEGIN-block keys are ignored: Access has only
                // emitted MultiUse here in observed fixtures, and the
                // forward-compat affordance is `AdditionalAttributes`
                // on the Attribute side.
            }
        }

        string? vbName = null;
        bool? vbGlobalNameSpace = null;
        bool? vbCreatable = null;
        bool? vbPredeclaredId = null;
        bool? vbExposed = null;

        var extras = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var attrBlock = headerMatch.Groups["attrBlock"].Value;
        foreach (Match m in AttributeLine.Matches(attrBlock))
        {
            var key = m.Groups["key"].Value;
            // Trim trailing whitespace so the value capture is stable
            // across CRLF / trailing-space inputs. Quote-preservation
            // for AdditionalAttributes is handled by StripQuotes only
            // on recognized string fields.
            var rawValue = m.Groups["value"].Value.TrimEnd(' ', '\t');
            switch (key)
            {
                case "Name":
                    vbName = StripQuotes(rawValue);
                    break;
                case "GlobalNameSpace":
                    vbGlobalNameSpace = ParseVbBool(rawValue);
                    break;
                case "Creatable":
                    vbCreatable = ParseVbBool(rawValue);
                    break;
                case "PredeclaredId":
                    vbPredeclaredId = ParseVbBool(rawValue);
                    break;
                case "Exposed":
                    vbExposed = ParseVbBool(rawValue);
                    break;
                default:
                    extras[key] = rawValue;
                    break;
            }
        }

        return new ModuleHeaderMetadata
        {
            VersionMajor = versionMajor,
            VersionMinor = versionMinor,
            MultiUse = multiUse,
            VbName = vbName,
            VbGlobalNameSpace = vbGlobalNameSpace,
            VbCreatable = vbCreatable,
            VbPredeclaredId = vbPredeclaredId,
            VbExposed = vbExposed,
            AdditionalAttributes = extras.ToImmutable(),
        };
    }

    private static int? TryParseInt(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    // Access SaveAsText emits `True`/`False` for VB_* booleans.
    private static bool? ParseVbBool(string raw)
    {
        var v = raw.Trim();
        if (string.Equals(v, "True", StringComparison.Ordinal)) return true;
        if (string.Equals(v, "False", StringComparison.Ordinal)) return false;
        return null;
    }

    // Inside the BEGIN..END block VBA writes `-1` for True and `0`
    // for False (occasionally with a trailing `'True` / `'False`
    // comment which `BeginBodyLine` already strips).
    private static bool? ParseAccessBool(string raw)
    {
        if (string.Equals(raw, "-1", StringComparison.Ordinal)) return true;
        if (string.Equals(raw, "0", StringComparison.Ordinal)) return false;
        if (string.Equals(raw, "True", StringComparison.Ordinal)) return true;
        if (string.Equals(raw, "False", StringComparison.Ordinal)) return false;
        return null;
    }

    private static string StripQuotes(string raw)
    {
        var s = raw.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            return s.Substring(1, s.Length - 2);
        return s;
    }

    // The BEGIN..END block often carries an inline `'True` / `'False`
    // comment on each line (e.g., `MultiUse = -1  'True`). The comment
    // starts at the first `'` outside of a string literal, but the
    // BEGIN block does not carry string literals in any observed
    // fixture — a literal `'` is sufficient as the comment sentinel.
    private static string StripTrailingComment(string raw)
    {
        int tick = raw.IndexOf('\'');
        return tick < 0 ? raw : raw.Substring(0, tick);
    }
}
