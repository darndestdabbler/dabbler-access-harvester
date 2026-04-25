' ============================================================
' TEST CASE 10: Class module with WithEvents
' Class: clsCaseAuditor (standalone class module, NOT a form/report class)
' Difficulty: HIGH
' Tests: WithEvents declarations, event sinks, class module patterns,
'        Property Get/Let/Set, Implements interface, Initialize/Terminate
' 
' Used by code that creates an instance, sets the form to monitor, and
' the class hooks the form's events to log changes for audit purposes.
' Usage:
'     Dim oAuditor As clsCaseAuditor
'     Set oAuditor = New clsCaseAuditor
'     oAuditor.AttachToForm Me
' ============================================================

Option Compare Database
Option Explicit

Implements IAuditLogger

' WithEvents binding to a form - lets this class respond to form events
Private WithEvents m_Form As Form

' Internal state
Private m_AuditEntityName As String
Private m_AuditEnabled As Boolean
Private m_LastSavedSnapshot As Object  ' Dictionary

Private Sub Class_Initialize()
    m_AuditEnabled = True
    Set m_LastSavedSnapshot = CreateObject("Scripting.Dictionary")
End Sub

Private Sub Class_Terminate()
    Set m_Form = Nothing
    Set m_LastSavedSnapshot = Nothing
End Sub

' Public method to attach the auditor to a form
Public Sub AttachToForm(frmTarget As Form)
    Set m_Form = frmTarget
    m_AuditEntityName = frmTarget.Name
    
    ' Capture initial snapshot for change tracking
    CaptureSnapshot
End Sub

' Property: get/set the entity name used in audit log entries
Public Property Get EntityName() As String
    EntityName = m_AuditEntityName
End Property

Public Property Let EntityName(ByVal sValue As String)
    m_AuditEntityName = sValue
End Property

' Property: enable/disable auditing without detaching
Public Property Get Enabled() As Boolean
    Enabled = m_AuditEnabled
End Property

Public Property Let Enabled(ByVal bValue As Boolean)
    m_AuditEnabled = bValue
End Property

' Event sink: fires when m_Form's BeforeUpdate event fires
Private Sub m_Form_BeforeUpdate(Cancel As Integer)
    If Not m_AuditEnabled Then Exit Sub
    
    ' Validate that required audit fields are populated
    If IsNull(m_Form!ModifiedBy) Or m_Form!ModifiedBy = "" Then
        m_Form!ModifiedBy = CurrentUser()
    End If
    m_Form!ModifiedDate = Now()
End Sub

' Event sink: fires when m_Form's AfterUpdate event fires
Private Sub m_Form_AfterUpdate()
    If Not m_AuditEnabled Then Exit Sub
    
    LogChanges
    CaptureSnapshot  ' Refresh snapshot for next change cycle
End Sub

' Event sink: fires when m_Form is closed
Private Sub m_Form_Close()
    Set m_Form = Nothing
End Sub

' Capture current field values for later comparison
Private Sub CaptureSnapshot()
    Dim ctl As Control
    
    m_LastSavedSnapshot.RemoveAll
    
    For Each ctl In m_Form.Controls
        ' Only snapshot bound data controls
        If TypeOf ctl Is TextBox Or TypeOf ctl Is ComboBox Or _
           TypeOf ctl Is CheckBox Then
            If Len(ctl.ControlSource) > 0 And _
               Left(ctl.ControlSource, 1) <> "=" Then
                m_LastSavedSnapshot(ctl.Name) = Nz(ctl.Value, "")
            End If
        End If
    Next ctl
End Sub

' Compare current values against snapshot, log differences
Private Sub LogChanges()
    Dim ctl As Control
    Dim sOldValue As String, sNewValue As String
    Dim db As DAO.Database
    
    Set db = CurrentDb
    
    For Each ctl In m_Form.Controls
        If TypeOf ctl Is TextBox Or TypeOf ctl Is ComboBox Or _
           TypeOf ctl Is CheckBox Then
            If m_LastSavedSnapshot.Exists(ctl.Name) Then
                sOldValue = CStr(m_LastSavedSnapshot(ctl.Name))
                sNewValue = CStr(Nz(ctl.Value, ""))
                
                If sOldValue <> sNewValue Then
                    db.Execute "INSERT INTO tblAuditLog " & _
                               "(EntityName, FieldName, OldValue, NewValue, " & _
                               "ChangedBy, ChangedDate) VALUES (" & _
                               "'" & m_AuditEntityName & "', " & _
                               "'" & ctl.Name & "', " & _
                               "'" & Replace(sOldValue, "'", "''") & "', " & _
                               "'" & Replace(sNewValue, "'", "''") & "', " & _
                               "'" & CurrentUser() & "', " & _
                               "Now())", dbFailOnError
                End If
            End If
        End If
    Next ctl
End Sub

' Implements IAuditLogger interface
Private Sub IAuditLogger_LogEvent(EventType As String, Description As String)
    Dim db As DAO.Database
    Set db = CurrentDb
    db.Execute "INSERT INTO tblAuditLog " & _
               "(EntityName, FieldName, NewValue, ChangedBy, ChangedDate) " & _
               "VALUES ('" & m_AuditEntityName & "', '" & EventType & _
               "', '" & Replace(Description, "'", "''") & "', '" & _
               CurrentUser() & "', Now())", dbFailOnError
End Sub

Private Property Get IAuditLogger_Enabled() As Boolean
    IAuditLogger_Enabled = m_AuditEnabled
End Property
