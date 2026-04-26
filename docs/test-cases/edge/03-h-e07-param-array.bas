' H-E07: ParamArray parameter
'
' Key parsing requirements per MS-VBAL §4.6:
' 1. ParamArray must be the last parameter.
' 2. ParamArray must be typed As Variant (or unspecified, defaulting to Variant).
' 3. ParamArray is mutually exclusive with Optional and ByRef/ByVal modifiers.
' 4. At call sites, the caller may pass zero or more arguments beyond fixed params.

Option Explicit

Public Sub LogMessage(prefix As String, ParamArray args() As Variant)
    ' Grammar test: parse the ParamArray parameter in the procedure signature.
    ' Call sites with 0, 1, and multiple ParamArray arguments:
    LogMessage "INFO:"
    LogMessage "WARN:", "Something happened"
    LogMessage "DEBUG:", 1, "foo", Now
End Sub
