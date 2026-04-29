using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AccessHarvester.SymbolDb.Models;

namespace AccessHarvester.SymbolDb.Extraction;

// Reads query KB JSON files and produces QueryRow lists. Computes:
//   sql_text_hash  — MD5 of the normalised SQL (whitespace-collapsed, uppercased,
//                    PARAMETERS clause stripped).
//   sql_clause_hash — per-clause structural hash: each clause's content is
//                     hashed independently and the hashes are concatenated into
//                     a fixed-format string. Reordering-tolerant within SELECT
//                     and WHERE conjuncts only (not between clause types).
//   equivalence_canonical_fqn — if two queries share the same sql_text_hash, the
//                     lexicographically-first fqn is the canonical; siblings point
//                     to it.
//   is_dead — not computable from KB JSON alone (requires VBA + macro reference
//             graphs populated in Sessions 1-2). All queries are emitted with
//             is_dead = 0; a post-Session-3 annotation pass can update these.
public static class QuerySchemaExtractor
{
    public static IReadOnlyList<QueryRow> Extract(IEnumerable<string> kbJsonPaths)
    {
        // Phase 1: build raw rows.
        var raw = new List<(string Fqn, string SourceSql, string? NormalizedSql,
                            string? TextHash, string? ClauseHash)>();

        foreach (var path in kbJsonPaths)
        {
            using var stream = File.OpenRead(path);
            var root = JsonNode.Parse(stream)?.AsObject();
            if (root is null) continue;

            string fqn = root["Name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(path);
            string sourceSql = root["Sql"]?.GetValue<string>() ?? string.Empty;

            string? normalizedSql = null;
            string? textHash = null;
            string? clauseHash = null;

            if (!string.IsNullOrWhiteSpace(sourceSql))
            {
                normalizedSql = NormalizeSql(sourceSql);
                textHash = Md5Hex(normalizedSql);
                clauseHash = ComputeClauseHash(sourceSql);
            }

            raw.Add((fqn, sourceSql, normalizedSql, textHash, clauseHash));
        }

        // Phase 2: compute equivalence groups by text hash.
        // Within each group, the lexicographically-first fqn is canonical.
        var canonicalByHash = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (fqn, _, _, textHash, _) in raw)
        {
            if (textHash is null) continue;
            if (!canonicalByHash.TryGetValue(textHash, out var existing) ||
                StringComparer.Ordinal.Compare(fqn, existing) < 0)
            {
                canonicalByHash[textHash] = fqn;
            }
        }

        // Phase 3: build final rows.
        var result = new List<QueryRow>(raw.Count);
        foreach (var (fqn, sourceSql, normalizedSql, textHash, clauseHash) in raw)
        {
            string? canonical = null;
            if (textHash is not null && canonicalByHash.TryGetValue(textHash, out var c) && c != fqn)
                canonical = c;

            result.Add(new QueryRow(
                Fqn: fqn,
                SourceSql: sourceSql,
                NormalizedSql: normalizedSql,
                SqlTextHash: textHash,
                SqlClauseHash: clauseHash,
                IsDead: false,
                EquivalenceCanonicalFqn: canonical));
        }

        return result;
    }

    // Strip PARAMETERS clause; collapse whitespace; uppercase.
    public static string NormalizeSql(string sql)
    {
        // Strip leading PARAMETERS ... ; clause (case-insensitive).
        sql = Regex.Replace(sql, @"^\s*PARAMETERS\s.*?;\s*", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Collapse all whitespace runs to single space; trim.
        sql = Regex.Replace(sql, @"\s+", " ").Trim();
        return sql.ToUpperInvariant();
    }

    // Hash each SQL clause independently. Clause boundaries: SELECT, FROM,
    // WHERE, GROUP BY, HAVING, ORDER BY. Returns a pipe-separated chain of
    // "CLAUSE:<md5>" tokens so callers can see which clause differed.
    public static string? ComputeClauseHash(string sql)
    {
        // Extract clauses by splitting on clause keywords at word boundaries.
        var clausePattern = new Regex(
            @"\b(SELECT|FROM|WHERE|GROUP\s+BY|HAVING|ORDER\s+BY|UNION|PIVOT|TRANSFORM)\b",
            RegexOptions.IgnoreCase);

        var matches = clausePattern.Matches(sql);
        if (matches.Count == 0) return null;

        var sb = new StringBuilder();
        for (int i = 0; i < matches.Count; i++)
        {
            int start = matches[i].Index + matches[i].Length;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : sql.Length;
            string clauseLabel = Regex.Replace(matches[i].Value, @"\s+", "_").ToUpperInvariant();
            string clauseContent = Regex.Replace(sql[start..end], @"\s+", " ").Trim().ToUpperInvariant();
            if (sb.Length > 0) sb.Append('|');
            sb.Append(clauseLabel).Append(':').Append(Md5Hex(clauseContent));
        }

        return sb.ToString();
    }

    private static string Md5Hex(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}
