-- ============================================================
-- TEST CASE 4: Saved query with form parameters, IIf, joins, and date math
-- Query name: qryActiveCasesByDistrict
-- Difficulty: MEDIUM
-- Tests: Forms!X!Y in WHERE, IIf in SELECT list, JOIN parsing,
--        date arithmetic, Nz wrapping, parameter resolution
-- ============================================================

PARAMETERS [Forms]![frmDistrictReport]![cboDistrict] Long, 
           [Forms]![frmDistrictReport]![txtAsOfDate] DateTime;

SELECT 
    c.CaseID,
    c.CaseNumber,
    c.OpenedDate,
    Nz(cw.CaseworkerName, "Unassigned") AS Caseworker,
    cl.ClientLastName & ", " & cl.ClientFirstName AS ClientName,
    DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) AS DaysOpen,
    IIf(DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) > 90,
        "Overdue",
        IIf(DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) > 60,
            "At Risk",
            "On Track")) AS StatusFlag,
    c.Priority,
    c.LastActivityDate
FROM ((tblCases AS c
    LEFT JOIN tblCaseworkers AS cw ON c.AssignedCaseworkerID = cw.CaseworkerID)
    INNER JOIN tblClients AS cl ON c.ClientID = cl.ClientID)
WHERE 
    c.DistrictID = [Forms]![frmDistrictReport]![cboDistrict]
    AND c.CaseStatus IN ('Open', 'Pending Review', 'Awaiting Documents')
    AND c.OpenedDate <= [Forms]![frmDistrictReport]![txtAsOfDate]
    AND (c.ClosedDate IS NULL OR c.ClosedDate > [Forms]![frmDistrictReport]![txtAsOfDate])
ORDER BY 
    IIf(c.Priority = 'High', 1, IIf(c.Priority = 'Medium', 2, 3)),
    c.OpenedDate;
