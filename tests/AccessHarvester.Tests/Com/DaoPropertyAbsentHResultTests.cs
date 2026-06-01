using System.Runtime.Versioning;
using AccessHarvester.Core.Com;
using Xunit;

namespace AccessHarvester.Tests.Com;

// Hermetic unit tests for the DAO "property is absent" HRESULT classification
// used by OleDbDaoSchemaReader's per-property catch filters. No Access / COM
// instance is required — the predicate is pure. Marked Windows-only because
// AccessHarvester.Core is assembly-gated to Windows.
[SupportedOSPlatform("windows")]
public sealed class DaoPropertyAbsentHResultTests
{
    // The three DAO HRESULTs that mean "this property does not exist on this
    // field" and must be swallowed (the property simply isn't set) rather than
    // recorded as a SchemaReadError degradation.
    //   0x800A0CC1 = 3265 = "Item not found in this collection."
    //   0x800A0CB3 = 3251 = "Operation is not supported for this type of object."
    //   0x800A0CC6 = 3270 = "Property not found." (folded in by
    //                harvester-fix-classify-timeout S2; was forcing 93/93
    //                spurious degradations + exit code 3 on the rebuilt
    //                HomeHealthCare.accdb fixture).
    [Theory]
    [InlineData(unchecked((int)0x800A0CC1))]
    [InlineData(unchecked((int)0x800A0CB3))]
    [InlineData(unchecked((int)0x800A0CC6))]
    public void IsDaoPropertyAbsentHResult_KnownAbsentCodes_ReturnTrue(int hresult)
    {
        Assert.True(OleDbDaoSchemaReader.IsDaoPropertyAbsentHResult(hresult));
    }

    // A genuine reader fault must NOT be classified as an absent property — it
    // has to bubble so the per-field catch records a SchemaReadError. Covers
    // E_FAIL (the generic COM failure) and a neighbouring DAO error that is a
    // real fault, not an absent-property signal.
    [Theory]
    [InlineData(unchecked((int)0x80004005))] // E_FAIL — generic COM failure
    [InlineData(unchecked((int)0x800A0BB8))] // DAO 3000-range runtime error, not absent-property
    [InlineData(0)]                          // S_OK — never an exception HRESULT
    public void IsDaoPropertyAbsentHResult_RealFaults_ReturnFalse(int hresult)
    {
        Assert.False(OleDbDaoSchemaReader.IsDaoPropertyAbsentHResult(hresult));
    }
}
