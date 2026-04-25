' ============================================================
' TEST CASE 8: Constructs that defeat static analysis
' Module: modConfigDriven
' Difficulty: UNRESOLVABLE (intentionally)
' Tests: Eval() with dynamic content, dynamic form name construction,
'        table-driven dispatch, reflection-style code
' 
' This case tests that the harvester FAILS GRACEFULLY rather than
' silently producing wrong output. Every construct here should
' route to the manual review queue.
' ============================================================

Public Sub OpenFormByConfig(sConfigKey As String)
    Dim sFormName As String
    Dim sFilter As String
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    
    ' Look up the form name from a configuration table
    Set db = CurrentDb
    Set rs = db.OpenRecordset("SELECT FormName, FilterTemplate, OpenArgsTemplate " & _
                              "FROM tblFormConfig WHERE ConfigKey = '" & _
                              sConfigKey & "'", dbOpenSnapshot)
    
    If rs.EOF Then
        MsgBox "No configuration for key: " & sConfigKey
        rs.Close
        Exit Sub
    End If
    
    sFormName = rs!FormName
    sFilter = rs!FilterTemplate
    
    ' Filter template might contain placeholders like {CurrentUser} or {Today}
    ' that need substitution. The substitutions themselves are eval'd.
    sFilter = Replace(sFilter, "{CurrentUser}", "'" & CurrentUser() & "'")
    sFilter = Replace(sFilter, "{Today}", "#" & Format(Date, "yyyy-mm-dd") & "#")
    
    ' Some templates contain VBA expressions to evaluate
    Dim iStart As Integer, iEnd As Integer
    iStart = InStr(sFilter, "{Eval:")
    Do While iStart > 0
        iEnd = InStr(iStart, sFilter, "}")
        If iEnd = 0 Then Exit Do
        Dim sExpr As String
        sExpr = Mid(sFilter, iStart + 6, iEnd - iStart - 6)
        sFilter = Left(sFilter, iStart - 1) & _
                  CStr(Eval(sExpr)) & _
                  Mid(sFilter, iEnd + 1)
        iStart = InStr(sFilter, "{Eval:")
    Loop
    
    rs.Close
    Set rs = Nothing
    Set db = Nothing
    
    ' Now open the form. Form name comes from configuration table - completely dynamic.
    DoCmd.OpenForm sFormName, , , sFilter
    
    ' If the form has a "module function" defined in the config, call it
    ' via Application.Run with whatever arguments were configured
    Dim sCallback As String
    sCallback = DLookup("Callback", "tblFormConfig", "ConfigKey = '" & sConfigKey & "'")
    If Not IsNull(sCallback) And sCallback <> "" Then
        Application.Run sCallback, sFormName, sFilter
    End If
End Sub
