namespace AccessHarvester.SymbolDb.Models;

// Row in the MemberAccesses table. Specialised view of member-access
// expressions (Me.X, Forms!Y.Z, subfrmDetails.Form!txtField) that the
// References table captures at a coarser grain. Written by Session 2.
public sealed record MemberAccessRow(
    string FromFqn,                 // containing symbol FQN
    string Receiver,                // 'Me' | 'Forms' | 'Reports' | 'Application' | <var-name>
    string Path,                    // member chain: 'cboStatus' | 'frmFoo!SomeControl' | etc.
    string? ResolvedTargetFqn,      // bound declaration FQN when the outcome carries one
    int? SourceLine);
