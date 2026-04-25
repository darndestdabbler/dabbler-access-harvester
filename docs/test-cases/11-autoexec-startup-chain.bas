' ============================================================
' TEST CASE 11: AutoExec startup chain
' Components: AutoExec macro -> mdlStartup.Initialize -> form opening
' Difficulty: HIGH
' Tests: AutoExec macro extraction, RunCode + DoCmd dispatch chain,
'        global state initialization, conditional startup behavior
' 
' SOURCE A: The "AutoExec" macro (stored in MSysObjects, type=-32766)
' Macros are not VBA but have a defined action sequence. The harvester
' should extract them and represent them as structured action lists.
' 
' AutoExec macro actions (extracted from MSysObjects.LvExtra binary):
'   Action 1: SetWarnings
'     Warnings On = No
'   Action 2: RunCode
'     Function Name = Initialize()
'   Action 3: SetWarnings
'     Warnings On = Yes
' ============================================================

' SOURCE B: The mdlStartup module called by AutoExec

Option Compare Database
Option Explicit

' Application-wide globals - initialized by AutoExec, used everywhere
Public g_CurrentUserID As Long
Public g_CurrentUserName As String
Public g_UserRole As String
Public g_FiscalYear As Integer
Public g_AppStartTime As Date
Public g_DebugMode As Boolean

Public Function Initialize() As Boolean
    On Error GoTo ErrHandler
    
    g_AppStartTime = Now()
    g_DebugMode = False
    
    ' Check command-line arguments via Command()
    Dim sCommand As String
    sCommand = Command()
    If InStr(UCase(sCommand), "DEBUG") > 0 Then
        g_DebugMode = True
    End If
    
    ' Look up user from Windows login name in tblUsers
    Dim sWinUser As String
    sWinUser = Environ("USERNAME")
    
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    Set db = CurrentDb
    Set rs = db.OpenRecordset("SELECT UserID, UserName, RoleName " & _
                              "FROM tblUsers INNER JOIN tblRoles " & _
                              "ON tblUsers.RoleID = tblRoles.RoleID " & _
                              "WHERE WinLogon = '" & sWinUser & "' " & _
                              "AND Active = True", dbOpenSnapshot)
    
    If rs.EOF Then
        MsgBox "User '" & sWinUser & "' not authorized for this application. " & _
               "Contact your administrator.", vbCritical
        rs.Close
        Application.Quit acQuitSaveNone
        Exit Function
    End If
    
    g_CurrentUserID = rs!UserID
    g_CurrentUserName = rs!UserName
    g_UserRole = rs!RoleName
    
    rs.Close
    Set rs = Nothing
    
    ' Compute fiscal year (state government: July 1 start)
    g_FiscalYear = IIf(Month(Date()) >= 7, Year(Date()) + 1, Year(Date()))
    
    ' Log this session start
    db.Execute "INSERT INTO tblSessionLog (UserID, StartTime, AppVersion) " & _
               "VALUES (" & g_CurrentUserID & ", Now(), '" & GetAppVersion() & "')", _
               dbFailOnError
    
    ' Open the appropriate switchboard based on role
    Select Case g_UserRole
        Case "Administrator"
            DoCmd.OpenForm "frmAdminSwitchboard"
        Case "Caseworker"
            DoCmd.OpenForm "frmCaseworkerHome"
        Case "Supervisor"
            DoCmd.OpenForm "frmSupervisorDashboard"
        Case "ReadOnly"
            DoCmd.OpenForm "frmReadOnlyView"
        Case Else
            ' Generic fallback
            DoCmd.OpenForm "frmMainSwitchboard"
    End Select
    
    Set db = Nothing
    Initialize = True
    Exit Function
    
ErrHandler:
    MsgBox "Startup error: " & Err.Description & vbCrLf & _
           "Application will close.", vbCritical
    Initialize = False
    Application.Quit acQuitSaveNone
End Function

Public Function GetAppVersion() As String
    GetAppVersion = "2.4.1"  ' Updated manually on each release
End Function
