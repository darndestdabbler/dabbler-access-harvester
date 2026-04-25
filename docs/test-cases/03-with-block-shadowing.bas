' ============================================================
' TEST CASE 3: With block flattening + shadowing
' Form: frmPermitApplication
' Difficulty: HIGH
' Tests: With block context, nested With, shadowing, recordset patterns,
'        domain function calls, default member access
' ============================================================

Private Sub UpdatePermitStatus()
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    Dim sStatus As String
    
    Set db = CurrentDb()
    Set rs = db.OpenRecordset("SELECT * FROM tblPermits WHERE PermitID = " & _
                              Me!txtPermitID, dbOpenDynaset)
    
    With rs
        If Not (.BOF And .EOF) Then
            .Edit
            !LastReviewedBy = CurrentUser()
            !LastReviewedDate = Now()
            
            ' Check applicant's outstanding balance via domain function
            If DLookup("OutstandingBalance", "qryApplicantBalances", _
                       "ApplicantID = " & !ApplicantID) > 0 Then
                !Status = "Hold - Balance Due"
                sStatus = "Hold"
            Else
                ' Nested With on a different object - .Form here refers to subform, not rs
                With Me.sfrmInspections.Form
                    If .RecordsetClone.RecordCount = 0 Then
                        !Status = "Pending Inspection"
                        sStatus = "Pending"
                    ElseIf DCount("*", "tblInspections", _
                                  "PermitID = " & Me!txtPermitID & _
                                  " AND Result = 'Failed'") > 0 Then
                        !Status = "Failed Inspection"
                        sStatus = "Failed"
                    Else
                        !Status = "Approved"
                        sStatus = "Approved"
                    End If
                End With
            End If
            
            .Update
        End If
        .Close
    End With
    
    Set rs = Nothing
    Set db = Nothing
    
    Me.lblStatus.Caption = "Status: " & sStatus
End Sub
