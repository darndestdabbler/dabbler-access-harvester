Fix 1: Resolved. The new Section G evidence now directly establishes the two previously unsupported specifics. G1 explicitly enumerates the 8 reports, states the universe is 12 total reports, and gives the `rptX -> frmReportParam_Y` mapping, so the revised “8 of 12 … each naming its dialog” wording is now supported. G2 likewise establishes the concrete TempVars example the writeup now uses: `rptServicePlanAndShortfall` writes `TempVars!repCID` and `TempVars!repFY`, and the probe output lists exactly two record sources reading those TempVars values, `srptServicePlanEntries` and `srptServiceShortfall`. The related edit to remove TempVars from the query→report row also fixes the earlier evidence-placement problem. I do not see a regression here: the remaining generic “TempVars flows” framing is consistent with, and now grounded by, the specific probed example rather than floating unsupported.

Fix 2: Resolved. Section 1’s revised wording is now properly scoped to what the evidence actually shows: “value-for-value on the probed surfaces the S1–S3 evidence cites” no longer overclaims a whole-tree “byte-for-byte” comparison. The R2-1 absence language is also now within the evidence: G3 gives an explicit 266-file whole-tree search, shows `NavigationGraph` in zero files, and shows `ItemText` only in five files that the writeup accurately characterizes as schema/column-metadata surfaces, including the classify hit. That disclosure closes the prior precision gap. I do not see a new contradiction or drift introduced by these edits.

Overall, on this narrow re-verification, both round-1 issues are fixed and I do not detect a regression in the touched passages.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```