'--------------------------------------------------------------------------------
' H-D05: Resume / Resume Next / Resume <label>
'
' Hazard: Tests three Resume statement forms per MS-VBAL §5.4.12:
' 1. Resume      -- retry the failing statement
' 2. Resume Next -- continue at the statement after the failing one
' 3. Resume <label> -- jump to a named label (also clears Err object)
' Note: Resume 0 (bare Resume synonym) is omitted per minimalism principle.
'--------------------------------------------------------------------------------
Option Explicit

Public Sub ResumeForms(resumeType As Integer)
    On Error GoTo ErrorHandler

    ' A statement that may raise an error
    Debug.Print 1 / resumeType

ResumeTarget:
    Exit Sub

ErrorHandler:
    Select Case resumeType
        Case 1
            Resume
        Case 2
            Resume Next
        Case 3
            Resume ResumeTarget
    End Select
End Sub
