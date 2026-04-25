' ============================================================
' TEST CASE 2: Cascading combo with subform filtering
' Form: frmCaseManagement
' Difficulty: MEDIUM-HIGH
' Tests: bang operator, subform navigation, RowSource SQL with form references,
'        Column() access, Requery, dynamic filter construction
' ============================================================

Private Sub cboCaseworker_AfterUpdate()
    ' When caseworker changes, filter the case list and reset client combo
    Me!cboClient.RowSource = "SELECT ClientID, ClientName FROM tblClients " & _
                             "WHERE AssignedCaseworkerID = " & Me!cboCaseworker & _
                             " ORDER BY ClientName"
    Me!cboClient.Requery
    Me!cboClient = Null
    
    ' Update display fields from the caseworker combo's hidden columns
    Me.txtCaseworkerEmail = Me!cboCaseworker.Column(2)
    Me.txtCaseworkerPhone = Me!cboCaseworker.Column(3)
    
    ' Filter the subform showing this caseworker's open cases
    Me.sfrmOpenCases.Form.Filter = "CaseworkerID = " & Me!cboCaseworker & _
                                   " AND CaseStatus IN ('Open', 'Pending Review')"
    Me.sfrmOpenCases.Form.FilterOn = True
    
    ' Refresh totals shown in form footer
    Call RecalculateCaseloadTotals(Me!cboCaseworker)
End Sub
