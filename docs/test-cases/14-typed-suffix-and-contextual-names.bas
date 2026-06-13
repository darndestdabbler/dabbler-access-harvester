' ============================================================
' TEST CASE 14: Type-suffixed names and contextual keywords
' Module: modSuffixAndContextual
' Difficulty: MEDIUM
' Tests (008 Session 2, F-3-2 / F-3-3 + review fixes):
'   - Type-declaration suffix on a Function return name (SqlStr$)
'   - Type-declaration suffix on a module variable (g_sTitle$) and a
'     local (msg$), resolved by their suffix-stripped references
'   - Contextual keyword `step` as a parameter name AND a local name
'   - Named-argument call binding a contextual-keyword parameter
'     (step:=1) — the review H3 declaration/use symmetry
'   - Anchored `Option Compare Database` still wins over the keyword
' Every construct here parses cleanly (no recovery); the deliberately
' un-handled variants live in docs/planning/deferred-work.md (DW-1/DW-4).
' ============================================================

Option Compare Database
Option Explicit

Public g_sTitle$

Public Function SqlStr$(ByVal s As Variant)
    SqlStr = "'" & s & "'"
End Function

Public Sub RunStep(step As Long)
    Dim msg$
    msg = SqlStr(step)
    g_sTitle = msg
End Sub

Public Sub Caller()
    RunStep step:=1
End Sub
