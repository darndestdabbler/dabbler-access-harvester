REM
REM H-A07: REM-form comments
REM This module demonstrates the use of REM-style comments.
REM

Option Explicit

Public Sub RemCommentExamples()
    REM This is a comment at a statement position.
    Dim x As Integer
    Dim s As String

    REM Another comment line.
    x = 1 : REM This is a comment after a colon separator.

    s = "The word REM is not a comment inside a string literal."
End Sub