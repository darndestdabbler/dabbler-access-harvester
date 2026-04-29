namespace AccessHarvester.SymbolDb.Extraction;

// Input to the SymbolDbBuilder. Represents one VBA module as it sits on
// disk before parsing -- name as it should appear in Modules.fqn, the
// declared module kind, the on-disk path the source came from, the byte
// encoding the file was stored in (per the spec: 'utf-16-le' for forms,
// 'ascii' for standard modules), and the decoded source text.
public sealed record RawModule(
    string Fqn,
    string Kind,
    string SourcePath,
    string Encoding,
    string Source);
