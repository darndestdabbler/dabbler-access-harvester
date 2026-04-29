# Sample Report — vba-symbol-db Session 5

## Stratification

| Stratum | Population (DB) | Sample |
|---------|-----------------|--------|
| `enum_decl` | — | 1 |
| `macro` | — | 1 |
| `query` | — | 3 |
| `function` | — | 6 |
| `module_var` | — | 7 |
| `sub` | — | 8 |
| `enum_value` | — | 8 |
| `lookup_table` | — | 8 |
| **Total** | **103** | **42** |

## Per-Provider Results

### Opinion 2 — opus
- Written: 42
- Mean confidence: 0.787
- Provider errors (skipped): 0
- Total routed cost: $5.4093
- Agents used: claude-opus-4-6

### Opinion 3 — gpt-5-4
- Written: 42
- Mean confidence: 0.754
- Provider errors (skipped): 0
- Total routed cost: $2.1449
- Agents used: gpt-5.4

## Sampled FQNs

### enum_decl (1 items)
- `modStrings.enumStrings`

### macro (1 items)
- `AutoExec`

### query (3 items)
- `qrycboCompanyType`
- `qrycboStates`
- `qrycboTaxStatus`

### function (6 items)
- `Form_frmCompanyDetail.CompanyConfirmDelete`
- `Form_frmCompanyDetail.CompanyIsActive`
- `modStrings.GetString`
- `modStrings.StringFormat`
- `modStrings.StringFormatSQL`
- `modStrings.StringToDictionary`

### module_var (7 items)
- `Form_frmCompanyDetail.m_lngCalledFromID`
- `Form_frmCompanyDetail.m_lngCompanyContactCount`
- `Form_frmCompanyDetail.m_lngCustomerOrderCount`
- `Form_frmCompanyDetail.m_lngShipperOrderCount`
- `Form_frmCompanyDetail.m_lngVendorProductCount`
- `Form_frmCompanyDetail.m_lngVendorPurchaseOrderCount`
- `Form_frmCompanyDetail.m_strMsgCaption`

### sub (8 items)
- `Form_frmCompanyDetail.CompanyCantBeDeleted`
- `Form_frmCompanyDetail.Form_AfterUpdate`
- `Form_frmCompanyDetail.Form_BeforeDelConfirm`
- `Form_frmCompanyDetail.Form_Delete`
- `Form_frmCompanyDetail.Form_Load`
- `Form_frmCompanyDetail.cboCompanyTypeID_AfterUpdate`
- `Form_frmCompanyDetail.cmdEmail_Click`
- `Form_frmCompanyDetail.txtBusinessPhone_Click`

### enum_value (8 items)
- `modStrings.enumStrings.sAlreadyOnNewRecord`
- `modStrings.enumStrings.sCannotDelete`
- `modStrings.enumStrings.sCatalog_Quality`
- `modStrings.enumStrings.sCatalog_QualityHeader`
- `modStrings.enumStrings.sOneLineItem`
- `modStrings.enumStrings.sOrderMustBePaid`
- `modStrings.enumStrings.sPOCannotDelete`
- `modStrings.enumStrings.sReportNoData`

### lookup_table (8 items)
- `Companies`
- `CompanyTypes`
- `Employees`
- `ProductCategories`
- `Products`
- `States`
- `SystemSettings`
- `TaxStatus`

