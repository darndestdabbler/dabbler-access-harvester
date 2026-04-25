' ============================================================
' TEST CASE 12: Switchboard form
' Form: frmMainSwitchboard
' Difficulty: HIGH (for navigation extraction)
' Tests: switchboard pattern recognition, role-conditional menu items,
'        OpenForm with arguments and filters, OpenReport, RunCommand,
'        navigation graph reconstruction
' 
' This is the most common form-based navigation pattern in older
' Access apps. The switchboard form has command buttons whose Click
' handlers open other forms/reports. The harvester reconstructs the
' navigation graph from the click handlers.
' ============================================================

Option Compare Database
Option Explicit

Private Sub Form_Load()
    ' Customize visible buttons based on user role (set during AutoExec)
    Me.btnAdminTools.Visible = (g_UserRole = "Administrator")
    Me.btnUserManagement.Visible = (g_UserRole = "Administrator")
    Me.btnReports.Visible = (g_UserRole <> "ReadOnly")
    Me.btnNewCase.Enabled = (g_UserRole = "Caseworker" Or g_UserRole = "Administrator")
    
    ' Update header with current user/fiscal year
    Me.lblHeader.Caption = "Welcome, " & g_CurrentUserName & _
                           "  |  FY" & g_FiscalYear & _
                           "  |  " & Format(Date(), "mmmm d, yyyy")
End Sub

' --- Case Management section ---

Private Sub btnNewCase_Click()
    ' Open the case form in data entry mode (new record only)
    DoCmd.OpenForm "frmCaseDetail", , , , acFormAdd, , _
                   "NewCase|" & g_CurrentUserID & "|" & Format(Date(), "yyyy-mm-dd")
End Sub

Private Sub btnSearchCases_Click()
    DoCmd.OpenForm "frmCaseSearch"
End Sub

Private Sub btnMyOpenCases_Click()
    ' Open the case list filtered to current caseworker, status = open
    DoCmd.OpenForm "frmCaseList", , , _
                   "AssignedCaseworkerID = " & g_CurrentUserID & _
                   " AND Status IN ('Open', 'Pending Review')"
End Sub

' --- Reports section ---

Private Sub btnReports_Click()
    DoCmd.OpenForm "frmReportMenu"
End Sub

Private Sub btnQuickCaseload_Click()
    ' Open the standard caseload report directly (skip parameter form)
    ' First open the parameter form to set context
    DoCmd.OpenForm "frmDistrictReport", , , , , acHidden
    Forms!frmDistrictReport!cboDistrict = DLookup("DistrictID", "tblUsers", _
                                                  "UserID = " & g_CurrentUserID)
    Forms!frmDistrictReport!txtAsOfDate = Date()
    
    ' Now open the report in preview
    DoCmd.OpenReport "rptCaseLoadByDistrict", acViewPreview
End Sub

' --- Administration section (only visible to Administrators) ---

Private Sub btnUserManagement_Click()
    DoCmd.OpenForm "frmUserAdmin"
End Sub

Private Sub btnAdminTools_Click()
    DoCmd.OpenForm "frmAdminTools"
End Sub

Private Sub btnAuditLog_Click()
    ' Open audit log filtered to last 30 days
    DoCmd.OpenForm "frmAuditLogViewer", , , _
                   "ChangedDate >= #" & Format(Date() - 30, "yyyy-mm-dd") & "#"
End Sub

' --- Utility ---

Private Sub btnRefresh_Click()
    Me.Requery
End Sub

Private Sub btnExit_Click()
    ' Log session end before quitting
    Dim db As DAO.Database
    Set db = CurrentDb
    db.Execute "UPDATE tblSessionLog SET EndTime = Now() " & _
               "WHERE UserID = " & g_CurrentUserID & " AND EndTime IS NULL", _
               dbFailOnError
    Set db = Nothing
    
    ' Close all forms cleanly
    DoCmd.Close acForm, Me.Name
    Application.Quit acQuitSaveNone
End Sub

Private Sub btnHelp_Click()
    ' Use FollowHyperlink to open help PDF
    On Error Resume Next
    FollowHyperlink CurrentProject.Path & "\UserGuide.pdf"
    If Err.Number <> 0 Then
        MsgBox "Help file not found.", vbExclamation
    End If
End Sub
