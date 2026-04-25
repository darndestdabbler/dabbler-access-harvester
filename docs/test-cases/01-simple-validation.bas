' ============================================================
' TEST CASE 1: Simple validation handler
' Form: frmCustomers
' Difficulty: LOW
' Tests: basic Me reference, control resolution, simple control flow
' ============================================================

Private Sub Form_BeforeUpdate(Cancel As Integer)
    If IsNull(Me.txtCustomerName) Or Me.txtCustomerName = "" Then
        MsgBox "Customer Name is required.", vbExclamation
        Cancel = True
        Me.txtCustomerName.SetFocus
        Exit Sub
    End If
    
    If IsNull(Me.txtPhone) Then
        MsgBox "Phone Number is required.", vbExclamation
        Cancel = True
        Me.txtPhone.SetFocus
    End If
End Sub
