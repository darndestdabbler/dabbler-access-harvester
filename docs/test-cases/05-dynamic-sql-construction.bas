' ============================================================
' TEST CASE 5: Dynamic SQL with conditional construction
' Module: modReportRunner
' Difficulty: VERY HIGH
' Tests: dynamic SQL with branches, intra-procedural data flow,
'        multiple template candidates, OpenArgs, error handling
' ============================================================

Public Function RunFilteredCaseReport(Optional ByVal sFilterArgs As String = "") As Long
    Dim sSQL As String
    Dim sWhere As String
    Dim sOrder As String
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    Dim lCount As Long
    
    On Error GoTo ErrHandler
    
    ' Base query
    sSQL = "SELECT c.CaseID, c.CaseNumber, c.OpenedDate, c.Status " & _
           "FROM tblCases AS c "
    
    ' Build WHERE clause based on filter args
    sWhere = " WHERE 1=1 "
    
    If InStr(sFilterArgs, "ACTIVE_ONLY") > 0 Then
        sWhere = sWhere & " AND c.Status IN ('Open', 'Pending Review') "
    End If
    
    If InStr(sFilterArgs, "OVERDUE") > 0 Then
        sWhere = sWhere & " AND DateDiff('d', c.OpenedDate, Date()) > 90 "
    End If
    
    ' District filter from form if open
    If CurrentProject.AllForms("frmFilterPanel").IsLoaded Then
        If Not IsNull(Forms!frmFilterPanel!cboDistrict) Then
            sWhere = sWhere & " AND c.DistrictID = " & _
                     Forms!frmFilterPanel!cboDistrict
        End If
        
        If Not IsNull(Forms!frmFilterPanel!txtCaseworkerName) And _
           Forms!frmFilterPanel!txtCaseworkerName <> "" Then
            sWhere = sWhere & " AND c.AssignedCaseworkerID IN (" & _
                     "SELECT CaseworkerID FROM tblCaseworkers " & _
                     "WHERE CaseworkerName LIKE '*" & _
                     Replace(Forms!frmFilterPanel!txtCaseworkerName, "'", "''") & _
                     "*')"
        End If
    End If
    
    ' Sort
    If InStr(sFilterArgs, "BY_DATE") > 0 Then
        sOrder = " ORDER BY c.OpenedDate DESC "
    Else
        sOrder = " ORDER BY c.CaseNumber "
    End If
    
    ' Final assembly
    sSQL = sSQL & sWhere & sOrder
    
    ' Execute and count
    Set db = CurrentDb()
    Set rs = db.OpenRecordset(sSQL, dbOpenSnapshot)
    
    If Not rs.EOF Then
        rs.MoveLast
        lCount = rs.RecordCount
    Else
        lCount = 0
    End If
    
    rs.Close
    Set rs = Nothing
    Set db = Nothing
    
    RunFilteredCaseReport = lCount
    Exit Function
    
ErrHandler:
    MsgBox "Error in RunFilteredCaseReport: " & Err.Description, vbCritical
    RunFilteredCaseReport = -1
    If Not rs Is Nothing Then
        rs.Close
        Set rs = Nothing
    End If
    Set db = Nothing
End Function
