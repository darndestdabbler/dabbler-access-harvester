using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.Scopes;

namespace AccessHarvester.Vba.Resolver.DefaultMembers;

// Pair Property Get/Let/Set declarations within a Scope into one
// PropertyGroup per case-insensitively-distinct name. Walks
// `scope.Declarations` only — does not recurse into child scopes.
// Callers decide whether to invoke per ModuleScope (the typical case)
// or per smaller scope.
//
// Output ordering: groups are emitted in the order each name was first
// seen in `scope.Declarations`. Matches the source-order convention
// the rest of the resolver uses and keeps tests deterministic.
//
// Non-property declarations (Sub / Function / Variable / Type / etc.)
// are ignored — they are unrelated declarations even when their name
// collides with a property.
public static class PropertyGroupBuilder
{
    public static IReadOnlyList<PropertyGroup> BuildForScope(Scope scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));

        var byName = new Dictionary<string, Slot>(Declaration.NameComparer);
        var firstSeen = new List<string>();

        foreach (var declaration in scope.Declarations)
        {
            if (declaration is not PropertyDeclaration prop)
                continue;

            if (!byName.TryGetValue(prop.Name, out var slot))
            {
                slot = new Slot(prop.Name);
                byName[prop.Name] = slot;
                firstSeen.Add(prop.Name);
            }

            switch (prop.Kind)
            {
                case PropertyKind.Get: slot.Get ??= prop; break;
                case PropertyKind.Let: slot.Let ??= prop; break;
                case PropertyKind.Set: slot.Set ??= prop; break;
            }
        }

        var groups = new List<PropertyGroup>(firstSeen.Count);
        foreach (var name in firstSeen)
        {
            var slot = byName[name];
            groups.Add(new PropertyGroup(
                name: slot.SpelledName,
                scope: scope,
                get: slot.Get,
                let: slot.Let,
                set: slot.Set));
        }
        return groups;
    }

    // Internal accumulator. The first `Get`/`Let`/`Set` seen wins —
    // duplicate same-kind declarations (which would be a VBA compile
    // error) are not treated specially here; the first wins and the
    // duplicate is silently dropped at this layer. A diagnostic for
    // the duplicate-kind case is a later-session concern.
    private sealed class Slot
    {
        public string SpelledName { get; }
        public PropertyDeclaration? Get { get; set; }
        public PropertyDeclaration? Let { get; set; }
        public PropertyDeclaration? Set { get; set; }

        public Slot(string spelledName)
        {
            SpelledName = spelledName;
        }
    }
}
