' ============================================================
' TEST CASE 7: Legacy patterns from older codebases
' Module: modLegacyImport (originally written ~2002, modified periodically)
' Difficulty: VERY HIGH
' Tests: GoTo control flow, GoSub/Return, Variant abuse, late-bound COM,
'        Type-declaration suffixes, line numbers, On Error Resume Next abuse,
'        unsafe string operations
' ============================================================

Option Compare Database

Public Function ImportClientBatch(sFilePath$) As Variant
10  Dim oExcel As Object
20  Dim oWB As Object
30  Dim oSht As Object
40  Dim i&, j&, lRow&, lCol&
50  Dim vData
60  Dim sErr$
70  Dim db As Object
80  Dim rs As Object
    
90  On Error Resume Next
    
100 Set oExcel = CreateObject("Excel.Application")
110 If oExcel Is Nothing Then GoTo NoExcel
    
120 Set oWB = oExcel.Workbooks.Open(sFilePath$)
130 If Err.Number <> 0 Then GoTo OpenFailed
140 Err.Clear
    
150 Set oSht = oWB.Sheets(1)
160 vData = oSht.UsedRange.Value
    
170 Set db = CurrentDb
180 Set rs = db.OpenRecordset("tblClients", 2)  ' 2 = dbOpenDynaset
    
190 lRow = UBound(vData, 1)
200 lCol = UBound(vData, 2)
    
    ' Skip header row
210 For i = 2 To lRow
220     If IsEmpty(vData(i, 1)) Then GoTo NextRow
        
230     GoSub ValidateRow
240     If sErr <> "" Then
250         Debug.Print "Row " & i & ": " & sErr
260         GoTo NextRow
        End If
        
270     rs.AddNew
280     rs!ClientName = Nz(vData(i, 1), "")
290     rs!DateOfBirth = vData(i, 2)
300     rs!SSN = "***-**-" & Right(CStr(vData(i, 3)), 4)
310     rs!IntakeDate = IIf(IsDate(vData(i, 4)), vData(i, 4), Date)
320     rs!Status = "New"
330     rs.Update
        
NextRow:
340 Next i
    
350 rs.Close
360 oWB.Close False
370 oExcel.Quit
    
380 Set rs = Nothing
390 Set db = Nothing
400 Set oSht = Nothing
410 Set oWB = Nothing
420 Set oExcel = Nothing
    
430 ImportClientBatch = lRow - 1  ' Number of data rows processed
440 Exit Function
    
ValidateRow:
450 sErr = ""
460 If IsNull(vData(i, 1)) Or vData(i, 1) = "" Then
470     sErr = "Missing name"
480     Return
    End If
490 If Not IsDate(vData(i, 2)) Then
500     sErr = "Invalid date of birth"
510     Return
    End If
520 If Len(CStr(vData(i, 3))) < 4 Then
530     sErr = "SSN too short"
540     Return
    End If
550 Return
    
NoExcel:
560 ImportClientBatch = "Excel not available"
570 Exit Function
    
OpenFailed:
580 ImportClientBatch = "Could not open: " & sFilePath$
590 If Not oExcel Is Nothing Then oExcel.Quit
600 Set oExcel = Nothing
610 Exit Function
End Function
