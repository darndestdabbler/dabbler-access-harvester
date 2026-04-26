'--------------------------------------------------------------------------------
' H-D04 (On Error GoTo 0 sub-case): Deactivate the current error handler
'
' The parsing hazard: the `0` in `On Error GoTo 0` is a SENTINEL meaning
' "deactivate the active error handler", NOT a reference to a numeric line label
' (H-A03) and NOT the integer literal 0. Disambiguation is positional:
' after `On Error GoTo` the 0 is a deactivate sentinel; elsewhere it is a label
' reference or a numeric literal.
'--------------------------------------------------------------------------------
Option Explicit

Public Sub DemonstrateOnErrorGoToZero()
    ' 1. Activate an error handler
    On Error GoTo ErrHandler

    ' 2. Normal code that runs without error
    Debug.Print "Inside guarded region"

    ' 3. Deactivate the handler -- key grammar test: GoTo 0 as sentinel
    On Error GoTo 0

    ' 4. Code outside error-handling scope (no active handler)
    Debug.Print "Outside guarded region"
    Exit Sub

ErrHandler:
    Resume Next
End Sub
