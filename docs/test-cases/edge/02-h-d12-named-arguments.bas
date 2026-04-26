' H-D12: Named arguments at call sites (:=)
'
' Key parsing requirement: `:=` must be tokenized as a single COLON_EQ token,
' distinct from `:` (statement separator) + `=` (assignment/comparison).
' Named arguments may also mix with positional (positional first, named after).

Option Explicit

Public Sub TestNamedArgCalls()
    ' 1. Pure named arguments (out of declaration order) -- tests :=
    DoCmd.OpenForm FormName:="frmX", DataMode:=2

    ' 2. Mixed positional then named -- positional fills slot 1, named fills later slots
    DoCmd.OpenForm "frmY", DataMode:=2

    ' 3. Named arguments on MsgBox (tests :=  with different callee)
    MsgBox Prompt:="Hello", Buttons:=64, Title:="Named Args Test"
End Sub
