namespace AccessHarvester.SymbolDb.Models;

// Row in the [References] table. One per resolved name-use site across all
// non-deferred modules. Written by Session 2; AI-interpretation columns are
// not part of this table.
public sealed record ReferenceRow(
    string FromFqn,             // containing symbol FQN (procedure or module)
    string? ToFqn,              // resolved target FQN; null for built-ins, object-model, Me, unresolved
    string Identifier,          // name as written in source
    string Kind,                // 'call' | 'variable_read' | 'variable_write' | 'enum_value_use'
                                // | 'member_access' | 'control_access'
    string? CallerKind,         // 'ui-event-handler' | 'non-ui' | null (module-level)
    int? SourceLine,
    int? SourceColumn,
    string ResolutionStatus,    // 'resolved' | 'resolved-via-with-flattening' | 'resolved-to-object-model'
                                // | 'resolved-to-built-in' | 'resolved-to-me' | 'unresolved'
    string? ResolutionReason);  // only set when ResolutionStatus = 'unresolved'
