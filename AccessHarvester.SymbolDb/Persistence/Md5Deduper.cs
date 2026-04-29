using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace AccessHarvester.SymbolDb.Persistence;

// Populates Symbols.equivalence_canonical_fqn for every member of a hash-
// equivalent cluster. The canonical chosen is the alphabetically first FQN
// in the cluster — order has to be deterministic so subsequent runs against
// the same source produce the same canonical, which keeps git diffs sane
// on the checked-in symbol-db.sqlite. Within a cluster, the canonical row
// keeps equivalence_canonical_fqn = NULL (it IS the canonical); siblings
// point at it.
public sealed class Md5Deduper
{
    public Md5DedupReport Run(SqliteConnection connection)
    {
        var clusters = LoadClusters(connection);
        int clustersFound = 0;
        int siblingsTagged = 0;

        using var transaction = connection.BeginTransaction();
        foreach (var cluster in clusters)
        {
            if (cluster.Members.Count < 2) continue;
            clustersFound++;

            string canonical = cluster.Members[0]; // sorted ascending — see LoadClusters

            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE Symbols SET equivalence_canonical_fqn = $canonical WHERE fqn = $fqn;";
            var pCanon = cmd.CreateParameter(); pCanon.ParameterName = "$canonical"; cmd.Parameters.Add(pCanon);
            var pFqn = cmd.CreateParameter(); pFqn.ParameterName = "$fqn"; cmd.Parameters.Add(pFqn);

            for (int i = 1; i < cluster.Members.Count; i++)
            {
                pCanon.Value = canonical;
                pFqn.Value = cluster.Members[i];
                cmd.ExecuteNonQuery();
                siblingsTagged++;
            }
        }
        transaction.Commit();

        return new Md5DedupReport(clustersFound, siblingsTagged);
    }

    private static List<Md5Cluster> LoadClusters(SqliteConnection connection)
    {
        var clusters = new List<Md5Cluster>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT signature_string, body_hash_md5, GROUP_CONCAT(fqn, char(10)) AS fqns
            FROM Symbols
            WHERE signature_string IS NOT NULL AND body_hash_md5 IS NOT NULL
            GROUP BY signature_string, body_hash_md5
            HAVING COUNT(*) > 1
            ORDER BY signature_string, body_hash_md5;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string signature = reader.GetString(0);
            string hash = reader.GetString(1);
            var members = new List<string>(reader.GetString(2).Split('\n'));
            members.Sort(System.StringComparer.Ordinal);
            clusters.Add(new Md5Cluster(signature, hash, members));
        }
        return clusters;
    }
}

public sealed record Md5Cluster(string SignatureString, string BodyHashMd5, IReadOnlyList<string> Members);

public sealed record Md5DedupReport(int ClustersFound, int SiblingsTagged);
