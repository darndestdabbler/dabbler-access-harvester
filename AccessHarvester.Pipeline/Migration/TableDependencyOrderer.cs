using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Pipeline.Migration;

/// <summary>The FK-safe load order plus any cycle degradations discovered.</summary>
public sealed record TableOrderResult(
    IReadOnlyList<string> OrderedTables,
    IReadOnlyList<MigrationDegradation> Degradations);

/// <summary>
/// Computes an FK-safe data-load order over the migrated tables: a parent table
/// (the side holding the referenced PK) loads before any child that references it,
/// so <c>INSERT</c>s never violate a foreign key at load time.
/// <para>
/// The dependency graph is condensed into its strongly-connected components
/// (Tarjan) and the resulting acyclic condensation is ordered with a deterministic
/// Kahn sort (ties break by the component's smallest ordinal member). A component
/// with more than one table is a genuine foreign-key cycle: it is surfaced as a
/// <see cref="MigrationDegradationKind.ForeignKeyCycle"/> degradation naming exactly
/// its members, and its members are still emitted (in ordinal order) at the correct
/// position in the condensation — so a non-cyclic table that merely <em>depends on</em>
/// a cycle still loads after it, and is never misreported as part of the cycle.
/// Self-referencing foreign keys (parent == child) are ignored for ordering: a table
/// cannot be required to load before itself, and that is not a multi-table cycle.
/// </para>
/// </summary>
public static class TableDependencyOrderer
{
    private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

    public static TableOrderResult Order(
        IReadOnlyList<MigrationResolvedTable> tables,
        IReadOnlyList<MigrationResolvedForeignKey> foreignKeys)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        if (foreignKeys is null) throw new ArgumentNullException(nameof(foreignKeys));

        // Node set + adjacency (parent -> children), in ordinal order for determinism.
        var nodes = tables.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var children = new Dictionary<string, SortedSet<string>>(IdComparer);
        foreach (var n in nodes) children[n] = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var fk in foreignKeys)
        {
            if (!children.ContainsKey(fk.ParentTable) || !children.ContainsKey(fk.ChildTable)) continue;
            if (IdComparer.Equals(fk.ParentTable, fk.ChildTable)) continue; // self-loop: ignore
            string parent = nodes.First(k => IdComparer.Equals(k, fk.ParentTable));
            string child = nodes.First(k => IdComparer.Equals(k, fk.ChildTable));
            children[parent].Add(child); // SortedSet dedupes parallel edges
        }

        var (components, componentOf) = StronglyConnectedComponents(nodes, children);
        var ordered = OrderCondensation(components, componentOf, children);

        var degradations = new List<MigrationDegradation>();
        foreach (var comp in components)
        {
            if (comp.Count <= 1) continue; // size-1 component is not a multi-table cycle
            degradations.Add(new MigrationDegradation(
                MigrationDegradationKind.ForeignKeyCycle, comp[0], null,
                $"Tables [{string.Join("], [", comp)}] form a foreign-key reference cycle; "
                + "no strict parent-before-child order exists among them. They are emitted in name order at "
                + "their position in the load sequence — disable or defer these constraints during load, or "
                + "load this group in dependency order manually."));
        }

        return new TableOrderResult(ordered, degradations);
    }

    /// <summary>
    /// Tarjan's SCC algorithm. Returns the components (each member list sorted
    /// ordinally) and a node→component-index map. DFS roots and adjacency are both
    /// visited in ordinal order so the component set is deterministic.
    /// </summary>
    private static (List<List<string>> Components, Dictionary<string, int> ComponentOf)
        StronglyConnectedComponents(IReadOnlyList<string> nodes, IReadOnlyDictionary<string, SortedSet<string>> children)
    {
        int index = 0;
        var idx = new Dictionary<string, int>(IdComparer);
        var low = new Dictionary<string, int>(IdComparer);
        var onStack = new HashSet<string>(IdComparer);
        var stack = new Stack<string>();
        var components = new List<List<string>>();
        var componentOf = new Dictionary<string, int>(IdComparer);

        void StrongConnect(string v)
        {
            idx[v] = index;
            low[v] = index;
            index++;
            stack.Push(v);
            onStack.Add(v);

            foreach (var w in children[v])
            {
                if (!idx.ContainsKey(w))
                {
                    StrongConnect(w);
                    low[v] = Math.Min(low[v], low[w]);
                }
                else if (onStack.Contains(w))
                {
                    low[v] = Math.Min(low[v], idx[w]);
                }
            }

            if (low[v] == idx[v])
            {
                var comp = new List<string>();
                string w;
                do
                {
                    w = stack.Pop();
                    onStack.Remove(w);
                    comp.Add(w);
                } while (!IdComparer.Equals(w, v));
                comp.Sort(StringComparer.Ordinal);
                int cid = components.Count;
                foreach (var m in comp) componentOf[m] = cid;
                components.Add(comp);
            }
        }

        foreach (var n in nodes)
            if (!idx.ContainsKey(n)) StrongConnect(n);

        return (components, componentOf);
    }

    /// <summary>
    /// Deterministic Kahn sort over the SCC condensation. Ready components break ties
    /// by their smallest ordinal member; each component's members are emitted in
    /// ordinal order. The condensation is acyclic by construction, so every table is
    /// emitted exactly once.
    /// </summary>
    private static List<string> OrderCondensation(
        List<List<string>> components,
        Dictionary<string, int> componentOf,
        IReadOnlyDictionary<string, SortedSet<string>> children)
    {
        int count = components.Count;
        var compChildren = new Dictionary<int, SortedSet<int>>();
        var inDegree = new int[count];
        for (int c = 0; c < count; c++) compChildren[c] = new SortedSet<int>();

        foreach (var (node, cid) in componentOf)
            foreach (var child in children[node])
            {
                int cc = componentOf[child];
                if (cc != cid && compChildren[cid].Add(cc)) inDegree[cc]++;
            }

        // Ready components ordered by their smallest ordinal member (members are sorted).
        var ready = new SortedSet<int>(Comparer<int>.Create(
            (a, b) => string.CompareOrdinal(components[a][0], components[b][0])));
        for (int c = 0; c < count; c++)
            if (inDegree[c] == 0) ready.Add(c);

        var ordered = new List<string>();
        while (ready.Count > 0)
        {
            int cid = ready.Min;
            ready.Remove(cid);
            ordered.AddRange(components[cid]);
            foreach (var cc in compChildren[cid])
                if (--inDegree[cc] == 0) ready.Add(cc);
        }

        return ordered;
    }
}
