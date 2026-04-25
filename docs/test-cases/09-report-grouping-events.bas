' ============================================================
' TEST CASE 9: Report module with grouping events
' Report: rptCaseLoadByDistrict
' Difficulty: MEDIUM-HIGH
' Tests: report-specific events (Format, Print, Retreat), section references,
'        running totals via module-level state, hide/show via Visible,
'        Cancel parameter on Format event
' ============================================================

Option Compare Database
Option Explicit

' Module-level running totals - reports use these because Format events fire
' multiple times per section (Print, Retreat) and the running total accumulates
' across detail records within a group
Private m_GroupTotal As Currency
Private m_GroupCount As Long
Private m_GrandTotal As Currency

Private Sub Report_Open(Cancel As Integer)
    ' Cancel report if no parameter form is loaded
    If Not CurrentProject.AllForms("frmDistrictReport").IsLoaded Then
        MsgBox "Open frmDistrictReport first to set parameters.", vbExclamation
        Cancel = True
        Exit Sub
    End If
    
    ' Set report caption from parameter form
    Me.Caption = "Caseload by District - " & _
                 Forms!frmDistrictReport!cboDistrict.Column(1) & _
                 " as of " & Format(Forms!frmDistrictReport!txtAsOfDate, "mmm d, yyyy")
    
    m_GrandTotal = 0
End Sub

Private Sub GroupHeader0_Format(Cancel As Integer, FormatCount As Integer)
    ' Reset group running totals at the start of each group
    ' FormatCount = 1 means first format pass; > 1 means re-formatting (e.g., page break)
    If FormatCount = 1 Then
        m_GroupTotal = 0
        m_GroupCount = 0
    End If
    
    ' Hide caseworker email if it's blank (avoid showing "Email: " with nothing after)
    If IsNull(Me.txtCaseworkerEmail) Or Me.txtCaseworkerEmail = "" Then
        Me.lblEmailLabel.Visible = False
        Me.txtCaseworkerEmail.Visible = False
    Else
        Me.lblEmailLabel.Visible = True
        Me.txtCaseworkerEmail.Visible = True
    End If
End Sub

Private Sub Detail_Format(Cancel As Integer, FormatCount As Integer)
    ' Accumulate running totals (only on first format pass to avoid double-counting)
    If FormatCount = 1 Then
        m_GroupTotal = m_GroupTotal + Nz(Me.txtCaseValue, 0)
        m_GroupCount = m_GroupCount + 1
    End If
    
    ' Color code overdue cases
    If Nz(Me.DaysOpen, 0) > 90 Then
        Me.txtCaseNumber.ForeColor = RGB(192, 0, 0)
        Me.txtCaseNumber.FontBold = True
    Else
        Me.txtCaseNumber.ForeColor = RGB(0, 0, 0)
        Me.txtCaseNumber.FontBold = False
    End If
End Sub

Private Sub GroupFooter0_Format(Cancel As Integer, FormatCount As Integer)
    ' Display running totals in group footer
    Me.txtGroupTotal = m_GroupTotal
    Me.txtGroupCount = m_GroupCount
    Me.txtGroupAvg = IIf(m_GroupCount > 0, m_GroupTotal / m_GroupCount, 0)
    
    ' Add to grand total
    If FormatCount = 1 Then
        m_GrandTotal = m_GrandTotal + m_GroupTotal
    End If
End Sub

Private Sub ReportFooter_Format(Cancel As Integer, FormatCount As Integer)
    Me.txtGrandTotal = m_GrandTotal
End Sub

Private Sub Detail_Retreat()
    ' Retreat fires when Access has to go back and re-format a section
    ' (e.g., when a group doesn't fit on a page). Decrement counters that
    ' were incremented in Format, otherwise the totals will be wrong.
    m_GroupTotal = m_GroupTotal - Nz(Me.txtCaseValue, 0)
    m_GroupCount = m_GroupCount - 1
End Sub
