' ============================================================
' TEST CASE 13: Preprocessor Directives
' Form: (none)
' Difficulty: MEDIUM
' Tests: #If, #Else, #End If, #Const at module and procedure level
' ============================================================

Option Compare Database
Option Explicit

' Module-level preprocessor constant
#Const DEBUG_MODE = 1

' Module-level conditional compilation for API declares
#If VBA7 Then
    Private Declare PtrSafe Function GetTickCount64 Lib "kernel32" () As LongLong
#Else
    Private Declare Function GetTickCount Lib "kernel32" () As Long
#End If

Public Sub LogProcess(ByVal processName As String)
    Dim startTime As Long
    ' Note: GetTickCount is declared conditionally at the module level
    startTime = GetTickCount()

    ' Procedure-level conditional compilation
    #If DEBUG_MODE = 1 Then
        Debug.Print "DEBUG: Starting process '" & processName & "' at " & startTime
    #Else
        DoCmd.RunSQL "INSERT INTO tbl_Log (ProcessName, StartTime) VALUES ('" & processName & "', " & startTime & ")"
    #End If

    Debug.Print "Process logging complete."
End Sub
