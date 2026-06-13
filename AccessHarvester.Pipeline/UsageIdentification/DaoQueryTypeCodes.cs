namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// DAO <c>QueryDefTypeEnum</c> numeric type codes — the values Access stores in
/// <c>QueryDef.Type</c> and the harvester emits as <c>QueryTypeCode</c>.
/// </summary>
/// <remarks>
/// Single source of truth for the classification layer so the usage classifier
/// and the question generator cannot drift apart (006 review F-2-1 / F-3-7: the
/// prior decode table was shifted by one slot, so real crosstabs decoded as
/// <c>MakeTable</c> and classified as possibly-unused action queries). The
/// extraction-side label decode lives in
/// <c>AccessHarvester.Core.Extraction.QueryExporter.QueryTypeName</c> and MUST
/// stay in agreement with these codes. Reference: Microsoft DAO
/// <c>QueryDefTypeEnum</c>.
/// </remarks>
internal static class DaoQueryTypeCodes
{
    public const int Select         = 0;    // dbQSelect
    public const int Crosstab       = 16;   // dbQCrosstab
    public const int Delete         = 32;   // dbQDelete
    public const int Update         = 48;   // dbQUpdate
    public const int Append         = 64;   // dbQAppend
    public const int MakeTable      = 80;   // dbQMakeTable
    public const int DataDefinition = 96;   // dbQDDL
    public const int PassThrough    = 112;  // dbQSQLPassThrough
    public const int SetOperation   = 128;  // dbQSetOperation (UNION)
    public const int SptBulk        = 144;  // dbQSPTBulk
    public const int Compound       = 160;  // dbQCompound
    public const int Procedure      = 224;  // dbQProcedure

    /// <summary>
    /// The four data-modifying action-query types (Append/Delete/Update/MakeTable).
    /// Crosstab is NOT an action query — it is a reporting shape.
    /// </summary>
    public static bool IsActionQuery(int code)
        => code is Append or Delete or Update or MakeTable;
}
