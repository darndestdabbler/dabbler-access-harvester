"""Session 3 metadata-fixture manifests for access-metadata-extraction."""

FIXTURES = [
    {
        "name": "01-simple-validation",
        "forms": [
            {
                "name": "frmCustomers",
                "controls": [
                    {"type": "textbox", "name": "txtCustomerName", "label_text": "Customer Name", "left": 1440, "top": 720, "width": 2880, "height": 360},
                    {"type": "textbox", "name": "txtPhone", "label_text": "Phone Number", "left": 1440, "top": 1260, "width": 2880, "height": 360},
                ],
            }
        ],
        "expected": {
            "Forms": [
                {
                    "Name": "frmCustomers",
                    "Children": [
                        {
                            "Type": "Section",
                            "Name": "Detail",
                            "Children": [
                                {"Type": "TextBox", "Name": "txtCustomerName", "Label": {"Source": "attached", "Text": "Customer Name"}},
                                {"Type": "TextBox", "Name": "txtPhone", "Label": {"Source": "attached", "Text": "Phone Number"}},
                            ],
                        }
                    ],
                }
            ]
        },
    },
    {
        "name": "02-cascading-combo-subform",
        "tables": [
            {
                "name": "tblCaseworkers",
                "fields": [
                    {"name": "CaseworkerID", "sql_type": "COUNTER", "data_type": "Long"},
                    {"name": "CaseworkerName", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100},
                    {"name": "Email", "sql_type": "TEXT(255)", "data_type": "Text", "size": 255},
                    {"name": "Phone", "sql_type": "TEXT(50)", "data_type": "Text", "size": 50},
                ],
                "primary_key": ["CaseworkerID"],
            },
            {
                "name": "tblClients",
                "fields": [
                    {"name": "ClientID", "sql_type": "COUNTER", "data_type": "Long"},
                    {"name": "ClientName", "sql_type": "TEXT(150)", "data_type": "Text", "size": 150},
                    {"name": "AssignedCaseworkerID", "sql_type": "LONG", "data_type": "Long"},
                ],
                "primary_key": ["ClientID"],
            },
            {
                "name": "tblCases",
                "fields": [
                    {"name": "CaseID", "sql_type": "COUNTER", "data_type": "Long"},
                    {"name": "CaseworkerID", "sql_type": "LONG", "data_type": "Long"},
                    {"name": "CaseStatus", "sql_type": "TEXT(50)", "data_type": "Text", "size": 50},
                ],
                "primary_key": ["CaseID"],
            },
        ],
        "relationships": [
            {"name": "relCaseworkerClients", "parent_table": "tblCaseworkers", "child_table": "tblClients", "fields": [{"parent": "CaseworkerID", "child": "AssignedCaseworkerID"}]},
            {"name": "relCaseworkerCases", "parent_table": "tblCaseworkers", "child_table": "tblCases", "fields": [{"parent": "CaseworkerID", "child": "CaseworkerID"}]},
        ],
        "forms": [
            {"name": "sfrmOpenCases", "record_source": "tblCases", "controls": []},
            {
                "name": "frmCaseManagement",
                "controls": [
                    {"type": "combo", "name": "cboCaseworker", "row_source": "SELECT CaseworkerID, CaseworkerName, Email, Phone FROM tblCaseworkers ORDER BY CaseworkerName;", "column_count": 4, "bound_column": 1, "column_widths": "0;1.8;1.8;1.2", "label_text": "Caseworker", "left": 1440, "top": 720, "width": 2880, "height": 360},
                    {"type": "combo", "name": "cboClient", "row_source": "SELECT ClientID, ClientName FROM tblClients ORDER BY ClientName;", "column_count": 2, "bound_column": 1, "column_widths": "0;2.0", "label_text": "Client", "left": 1440, "top": 1260, "width": 2880, "height": 360},
                    {"type": "textbox", "name": "txtCaseworkerEmail", "label_text": "Email", "left": 4800, "top": 720, "width": 2400, "height": 360},
                    {"type": "textbox", "name": "txtCaseworkerPhone", "label_text": "Phone", "left": 4800, "top": 1260, "width": 1800, "height": 360},
                    {"type": "subform", "name": "sfrmOpenCases", "source_object": "sfrmOpenCases", "left": 1440, "top": 1980, "width": 5760, "height": 2160},
                ],
            },
        ],
        "expected": {
            "Forms": [
                {
                    "Name": "frmCaseManagement",
                    "Children": [
                        {
                            "Type": "Section",
                            "Name": "Detail",
                            "Children": [
                                {"Type": "ComboBox", "Name": "cboCaseworker", "Label": {"Source": "attached", "Text": "Caseworker"}, "Properties": {"RowSource": "SELECT CaseworkerID, CaseworkerName, Email, Phone FROM tblCaseworkers ORDER BY CaseworkerName;", "ColumnCount": 4, "BoundColumn": 1}},
                                {"Type": "ComboBox", "Name": "cboClient", "Label": {"Source": "attached", "Text": "Client"}, "Properties": {"RowSource": "SELECT ClientID, ClientName FROM tblClients ORDER BY ClientName;", "ColumnCount": 2, "BoundColumn": 1}},
                                {"Type": "TextBox", "Name": "txtCaseworkerEmail", "Label": {"Source": "attached", "Text": "Email"}},
                                {"Type": "TextBox", "Name": "txtCaseworkerPhone", "Label": {"Source": "attached", "Text": "Phone"}},
                                {"Type": "Subform", "Name": "sfrmOpenCases", "Properties": {"SourceObject": "sfrmOpenCases"}},
                            ],
                        }
                    ],
                },
                {"Name": "sfrmOpenCases", "RecordSource": "tblCases"},
            ],
            "Tables": [
                {"Name": "tblCaseworkers", "Fields": [{"Name": "CaseworkerID", "DataType": "Long"}, {"Name": "CaseworkerName", "DataType": "Text", "Size": 100}, {"Name": "Email", "DataType": "Text", "Size": 255}, {"Name": "Phone", "DataType": "Text", "Size": 50}], "PrimaryKey": ["CaseworkerID"]},
                {"Name": "tblClients", "Fields": [{"Name": "ClientID", "DataType": "Long"}, {"Name": "ClientName", "DataType": "Text", "Size": 150}, {"Name": "AssignedCaseworkerID", "DataType": "Long"}], "PrimaryKey": ["ClientID"]},
                {"Name": "tblCases", "Fields": [{"Name": "CaseID", "DataType": "Long"}, {"Name": "CaseworkerID", "DataType": "Long"}, {"Name": "CaseStatus", "DataType": "Text", "Size": 50}], "PrimaryKey": ["CaseID"]},
            ],
            "Relationships": [
                {"Name": "relCaseworkerClients", "ParentTable": "tblCaseworkers", "ChildTable": "tblClients", "Fields": [{"ParentField": "CaseworkerID", "ChildField": "AssignedCaseworkerID"}]},
                {"Name": "relCaseworkerCases", "ParentTable": "tblCaseworkers", "ChildTable": "tblCases", "Fields": [{"ParentField": "CaseworkerID", "ChildField": "CaseworkerID"}]},
            ],
        },
    },
    {
        "name": "03-with-block-shadowing",
        "tables": [
            {"name": "tblApplicants", "fields": [{"name": "ApplicantID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "OutstandingBalance", "sql_type": "CURRENCY", "data_type": "Currency"}], "primary_key": ["ApplicantID"]},
            {"name": "tblPermits", "fields": [{"name": "PermitID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "ApplicantID", "sql_type": "LONG", "data_type": "Long"}, {"name": "LastReviewedBy", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "LastReviewedDate", "sql_type": "DATETIME", "data_type": "DateTime"}, {"name": "Status", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}], "primary_key": ["PermitID"]},
            {"name": "tblInspections", "fields": [{"name": "InspectionID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "PermitID", "sql_type": "LONG", "data_type": "Long"}, {"name": "Result", "sql_type": "TEXT(50)", "data_type": "Text", "size": 50}], "primary_key": ["InspectionID"]},
        ],
        "queries": [
            {"name": "qryApplicantBalances", "sql": "SELECT ApplicantID, OutstandingBalance FROM tblApplicants;", "query_type": "Select"},
        ],
        "relationships": [
            {"name": "relApplicantPermits", "parent_table": "tblApplicants", "child_table": "tblPermits", "fields": [{"parent": "ApplicantID", "child": "ApplicantID"}]},
            {"name": "relPermitInspections", "parent_table": "tblPermits", "child_table": "tblInspections", "fields": [{"parent": "PermitID", "child": "PermitID"}]},
        ],
        "forms": [
            {"name": "sfrmInspections", "record_source": "tblInspections", "controls": []},
            {
                "name": "frmPermitApplication",
                "record_source": "tblPermits",
                "controls": [
                    {"type": "textbox", "name": "txtPermitID", "control_source": "PermitID", "label_text": "Permit ID", "left": 1440, "top": 720, "width": 1440, "height": 360},
                    {"type": "label", "name": "lblStatus", "caption": "Status:", "left": 1440, "top": 1260, "width": 2160, "height": 360},
                    {"type": "subform", "name": "sfrmInspections", "source_object": "sfrmInspections", "left": 1440, "top": 1800, "width": 5760, "height": 2160},
                ],
            },
        ],
        "expected": {
            "Forms": [
                {
                    "Name": "frmPermitApplication",
                    "RecordSource": "tblPermits",
                    "Children": [
                        {
                            "Type": "Section",
                            "Name": "Detail",
                            "Children": [
                                {"Type": "TextBox", "Name": "txtPermitID", "Label": {"Source": "attached", "Text": "Permit ID"}, "Properties": {"ControlSource": "PermitID"}},
                                {"Type": "Label", "Name": "lblStatus", "Properties": {"Caption": "Status:"}},
                                {"Type": "Subform", "Name": "sfrmInspections", "Properties": {"SourceObject": "sfrmInspections"}},
                            ],
                        }
                    ],
                },
                {"Name": "sfrmInspections", "RecordSource": "tblInspections"},
            ],
            "Queries": [
                {"Name": "qryApplicantBalances", "QueryType": "Select", "Sql": "SELECT ApplicantID, OutstandingBalance FROM tblApplicants;"},
            ],
            "Tables": [
                {"Name": "tblApplicants", "Fields": [{"Name": "ApplicantID", "DataType": "Long"}, {"Name": "OutstandingBalance", "DataType": "Currency"}], "PrimaryKey": ["ApplicantID"]},
                {"Name": "tblPermits", "Fields": [{"Name": "PermitID", "DataType": "Long"}, {"Name": "ApplicantID", "DataType": "Long"}, {"Name": "LastReviewedBy", "DataType": "Text", "Size": 100}, {"Name": "LastReviewedDate", "DataType": "DateTime"}, {"Name": "Status", "DataType": "Text", "Size": 100}], "PrimaryKey": ["PermitID"]},
                {"Name": "tblInspections", "Fields": [{"Name": "InspectionID", "DataType": "Long"}, {"Name": "PermitID", "DataType": "Long"}, {"Name": "Result", "DataType": "Text", "Size": 50}], "PrimaryKey": ["InspectionID"]},
            ],
            "Relationships": [
                {"Name": "relApplicantPermits", "ParentTable": "tblApplicants", "ChildTable": "tblPermits", "Fields": [{"ParentField": "ApplicantID", "ChildField": "ApplicantID"}]},
                {"Name": "relPermitInspections", "ParentTable": "tblPermits", "ChildTable": "tblInspections", "Fields": [{"ParentField": "PermitID", "ChildField": "PermitID"}]},
            ],
        },
    },
    {
        "name": "09-report-grouping-events",
        "tables": [
            {"name": "tblCaseDetails", "fields": [{"name": "CaseDetailID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "DistrictID", "sql_type": "LONG", "data_type": "Long"}, {"name": "CaseworkerEmail", "sql_type": "TEXT(255)", "data_type": "Text", "size": 255}, {"name": "CaseValue", "sql_type": "CURRENCY", "data_type": "Currency"}, {"name": "DaysOpen", "sql_type": "LONG", "data_type": "Long"}, {"name": "CaseNumber", "sql_type": "TEXT(50)", "data_type": "Text", "size": 50}], "primary_key": ["CaseDetailID"]},
        ],
        "queries": [
            {"name": "qryCaseLoadDetails", "sql": "SELECT DistrictID, CaseworkerEmail, CaseValue, DaysOpen, CaseNumber FROM tblCaseDetails;", "query_type": "Select"},
        ],
        "forms": [
            {
                "name": "frmDistrictReport",
                "controls": [
                    {"type": "combo", "name": "cboDistrict", "row_source": "SELECT DISTINCT DistrictID, DistrictID FROM tblCaseDetails ORDER BY DistrictID;", "column_count": 2, "bound_column": 1, "column_widths": "0;1.0", "label_text": "District", "left": 1440, "top": 720, "width": 1800, "height": 360},
                    {"type": "textbox", "name": "txtAsOfDate", "label_text": "As Of Date", "left": 1440, "top": 1260, "width": 1800, "height": 360},
                ],
            }
        ],
        "reports": [
            {
                "name": "rptCaseLoadByDistrict",
                "record_source": "qryCaseLoadDetails",
                "group_levels": [{"expression": "DistrictID", "header": True, "footer": True}],
                "controls": [
                    {"type": "textbox", "name": "txtCaseworkerEmail", "control_source": "CaseworkerEmail", "section": "group-header-0", "left": 2160, "top": 240, "width": 2400, "height": 300},
                    {"type": "label", "name": "lblEmailLabel", "caption": "Email:", "section": "group-header-0", "left": 720, "top": 240, "width": 1200, "height": 300},
                    {"type": "textbox", "name": "txtCaseValue", "control_source": "CaseValue", "section": "detail", "left": 720, "top": 240, "width": 1200, "height": 300},
                    {"type": "textbox", "name": "DaysOpen", "control_source": "DaysOpen", "section": "detail", "left": 2160, "top": 240, "width": 960, "height": 300},
                    {"type": "textbox", "name": "txtCaseNumber", "control_source": "CaseNumber", "section": "detail", "left": 3360, "top": 240, "width": 1440, "height": 300},
                    {"type": "textbox", "name": "txtGroupTotal", "section": "group-footer-0", "left": 720, "top": 240, "width": 1200, "height": 300},
                    {"type": "textbox", "name": "txtGroupCount", "section": "group-footer-0", "left": 2160, "top": 240, "width": 960, "height": 300},
                    {"type": "textbox", "name": "txtGroupAvg", "section": "group-footer-0", "left": 3360, "top": 240, "width": 1200, "height": 300},
                    {"type": "textbox", "name": "txtGrandTotal", "section": "report-footer", "left": 720, "top": 240, "width": 1440, "height": 300},
                ],
            }
        ],
        "expected": {
            "Forms": [
                {
                    "Name": "frmDistrictReport",
                    "Children": [
                        {
                            "Type": "Section",
                            "Name": "Detail",
                            "Children": [
                                {"Type": "ComboBox", "Name": "cboDistrict", "Label": {"Source": "attached", "Text": "District"}, "Properties": {"RowSource": "SELECT DISTINCT DistrictID, DistrictID FROM tblCaseDetails ORDER BY DistrictID;", "ColumnCount": 2, "BoundColumn": 1}},
                                {"Type": "TextBox", "Name": "txtAsOfDate", "Label": {"Source": "attached", "Text": "As Of Date"}},
                            ],
                        }
                    ],
                }
            ],
            "Reports": [
                {
                    "Name": "rptCaseLoadByDistrict",
                    "RecordSource": "qryCaseLoadDetails",
                    "GroupLevels": [{"Level": 0, "GroupOn": "DistrictID", "HasHeader": True, "HasFooter": True}],
                    "Children": [
                        {"Type": "GroupLevel0Header", "Name": "GroupLevel0Header", "Children": [{"Type": "Label", "Name": "lblEmailLabel", "Properties": {"Caption": "Email:"}}, {"Type": "TextBox", "Name": "txtCaseworkerEmail", "Properties": {"ControlSource": "CaseworkerEmail"}}]},
                        {"Type": "Section", "Name": "Detail", "Children": [{"Type": "TextBox", "Name": "txtCaseValue", "Properties": {"ControlSource": "CaseValue"}}, {"Type": "TextBox", "Name": "DaysOpen", "Properties": {"ControlSource": "DaysOpen"}}, {"Type": "TextBox", "Name": "txtCaseNumber", "Properties": {"ControlSource": "CaseNumber"}}]},
                        {"Type": "GroupLevel0Footer", "Name": "GroupLevel0Footer", "Children": [{"Type": "TextBox", "Name": "txtGroupTotal"}, {"Type": "TextBox", "Name": "txtGroupCount"}, {"Type": "TextBox", "Name": "txtGroupAvg"}]},
                        {"Type": "ReportFooter", "Name": "ReportFooter", "Children": [{"Type": "TextBox", "Name": "txtGrandTotal"}]},
                    ],
                }
            ],
            "Queries": [
                {"Name": "qryCaseLoadDetails", "QueryType": "Select", "Sql": "SELECT DistrictID, CaseworkerEmail, CaseValue, DaysOpen, CaseNumber FROM tblCaseDetails;"},
            ],
            "Tables": [
                {"Name": "tblCaseDetails", "Fields": [{"Name": "CaseDetailID", "DataType": "Long"}, {"Name": "DistrictID", "DataType": "Long"}, {"Name": "CaseworkerEmail", "DataType": "Text", "Size": 255}, {"Name": "CaseValue", "DataType": "Currency"}, {"Name": "DaysOpen", "DataType": "Long"}, {"Name": "CaseNumber", "DataType": "Text", "Size": 50}], "PrimaryKey": ["CaseDetailID"]},
            ],
        },
    },
    {
        "name": "10-class-with-events",
        "tables": [
            {"name": "tblSomeData", "fields": [{"name": "ID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "FieldA", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "FieldB", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "FieldC", "sql_type": "YESNO", "data_type": "Boolean"}, {"name": "ModifiedBy", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "ModifiedDate", "sql_type": "DATETIME", "data_type": "DateTime"}], "primary_key": ["ID"]},
            {"name": "tblAuditLog", "fields": [{"name": "LogID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "EntityName", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "FieldName", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "OldValue", "sql_type": "TEXT(255)", "data_type": "Text", "size": 255}, {"name": "NewValue", "sql_type": "TEXT(255)", "data_type": "Text", "size": 255}, {"name": "ChangedBy", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "ChangedDate", "sql_type": "DATETIME", "data_type": "DateTime"}], "primary_key": ["LogID"]},
        ],
        "forms": [
            {
                "name": "frmSomeAuditedForm",
                "record_source": "tblSomeData",
                "controls": [
                    {"type": "textbox", "name": "txtFieldA", "control_source": "FieldA", "label_text": "Field A", "left": 1440, "top": 720, "width": 2400, "height": 360},
                    {"type": "combo", "name": "cboFieldB", "control_source": "FieldB", "row_source": "\"A\";\"B\"", "row_source_type": "Value List", "column_count": 1, "bound_column": 1, "label_text": "Field B", "left": 1440, "top": 1260, "width": 2400, "height": 360},
                    {"type": "checkbox", "name": "chkFieldC", "control_source": "FieldC", "label_text": "Field C", "left": 1440, "top": 1800, "width": 1800, "height": 360},
                    {"type": "textbox", "name": "ModifiedBy", "control_source": "ModifiedBy", "label_text": "Modified By", "left": 1440, "top": 2340, "width": 2400, "height": 360},
                    {"type": "textbox", "name": "ModifiedDate", "control_source": "ModifiedDate", "label_text": "Modified Date", "left": 1440, "top": 2880, "width": 2400, "height": 360},
                ],
            }
        ],
        "expected": {
            "Forms": [
                {
                    "Name": "frmSomeAuditedForm",
                    "RecordSource": "tblSomeData",
                    "Children": [
                        {
                            "Type": "Section",
                            "Name": "Detail",
                            "Children": [
                                {"Type": "TextBox", "Name": "txtFieldA", "Label": {"Source": "attached", "Text": "Field A"}, "Properties": {"ControlSource": "FieldA"}},
                                {"Type": "ComboBox", "Name": "cboFieldB", "Label": {"Source": "attached", "Text": "Field B"}, "Properties": {"ControlSource": "FieldB"}},
                                {"Type": "CheckBox", "Name": "chkFieldC", "Label": {"Source": "attached", "Text": "Field C"}, "Properties": {"ControlSource": "FieldC"}},
                                {"Type": "TextBox", "Name": "ModifiedBy", "Label": {"Source": "attached", "Text": "Modified By"}, "Properties": {"ControlSource": "ModifiedBy"}},
                                {"Type": "TextBox", "Name": "ModifiedDate", "Label": {"Source": "attached", "Text": "Modified Date"}, "Properties": {"ControlSource": "ModifiedDate"}},
                            ],
                        }
                    ],
                }
            ],
            "Tables": [
                {"Name": "tblSomeData", "Fields": [{"Name": "ID", "DataType": "Long"}, {"Name": "FieldA", "DataType": "Text", "Size": 100}, {"Name": "FieldB", "DataType": "Text", "Size": 100}, {"Name": "FieldC", "DataType": "Boolean"}, {"Name": "ModifiedBy", "DataType": "Text", "Size": 100}, {"Name": "ModifiedDate", "DataType": "DateTime"}], "PrimaryKey": ["ID"]},
                {"Name": "tblAuditLog", "Fields": [{"Name": "LogID", "DataType": "Long"}, {"Name": "EntityName", "DataType": "Text", "Size": 100}, {"Name": "FieldName", "DataType": "Text", "Size": 100}, {"Name": "OldValue", "DataType": "Text", "Size": 255}, {"Name": "NewValue", "DataType": "Text", "Size": 255}, {"Name": "ChangedBy", "DataType": "Text", "Size": 100}, {"Name": "ChangedDate", "DataType": "DateTime"}], "PrimaryKey": ["LogID"]},
            ],
        },
    },
    {
        "name": "11-autoexec-startup-chain",
        "tables": [
            {"name": "tblRoles", "fields": [{"name": "RoleID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "RoleName", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}], "primary_key": ["RoleID"]},
            {"name": "tblUsers", "fields": [{"name": "UserID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "UserName", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "RoleID", "sql_type": "LONG", "data_type": "Long"}, {"name": "WinLogon", "sql_type": "TEXT(100)", "data_type": "Text", "size": 100}, {"name": "Active", "sql_type": "YESNO", "data_type": "Boolean"}], "primary_key": ["UserID"]},
            {"name": "tblSessionLog", "fields": [{"name": "LogID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "UserID", "sql_type": "LONG", "data_type": "Long"}, {"name": "StartTime", "sql_type": "DATETIME", "data_type": "DateTime"}, {"name": "AppVersion", "sql_type": "TEXT(50)", "data_type": "Text", "size": 50}], "primary_key": ["LogID"]},
        ],
        "relationships": [
            {"name": "relRolesUsers", "parent_table": "tblRoles", "child_table": "tblUsers", "fields": [{"parent": "RoleID", "child": "RoleID"}]},
        ],
        "forms": [
            {"name": "frmAdminSwitchboard", "controls": []},
            {"name": "frmCaseworkerHome", "controls": []},
            {"name": "frmSupervisorDashboard", "controls": []},
            {"name": "frmReadOnlyView", "controls": []},
            {"name": "frmMainSwitchboard", "controls": []},
        ],
        "expected": {
            "Forms": [{"Name": "frmAdminSwitchboard"}, {"Name": "frmCaseworkerHome"}, {"Name": "frmSupervisorDashboard"}, {"Name": "frmReadOnlyView"}, {"Name": "frmMainSwitchboard"}],
            "Tables": [
                {"Name": "tblRoles", "Fields": [{"Name": "RoleID", "DataType": "Long"}, {"Name": "RoleName", "DataType": "Text", "Size": 100}], "PrimaryKey": ["RoleID"]},
                {"Name": "tblUsers", "Fields": [{"Name": "UserID", "DataType": "Long"}, {"Name": "UserName", "DataType": "Text", "Size": 100}, {"Name": "RoleID", "DataType": "Long"}, {"Name": "WinLogon", "DataType": "Text", "Size": 100}, {"Name": "Active", "DataType": "Boolean"}], "PrimaryKey": ["UserID"]},
                {"Name": "tblSessionLog", "Fields": [{"Name": "LogID", "DataType": "Long"}, {"Name": "UserID", "DataType": "Long"}, {"Name": "StartTime", "DataType": "DateTime"}, {"Name": "AppVersion", "DataType": "Text", "Size": 50}], "PrimaryKey": ["LogID"]},
            ],
            "Relationships": [{"Name": "relRolesUsers", "ParentTable": "tblRoles", "ChildTable": "tblUsers", "Fields": [{"ParentField": "RoleID", "ChildField": "RoleID"}]}],
        },
    },
    {
        "name": "12-switchboard-navigation",
        "tables": [
            {"name": "tblUsers", "fields": [{"name": "UserID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "DistrictID", "sql_type": "LONG", "data_type": "Long"}], "primary_key": ["UserID"]},
            {"name": "tblSessionLog", "fields": [{"name": "LogID", "sql_type": "COUNTER", "data_type": "Long"}, {"name": "UserID", "sql_type": "LONG", "data_type": "Long"}, {"name": "EndTime", "sql_type": "DATETIME", "data_type": "DateTime"}], "primary_key": ["LogID"]},
        ],
        "forms": [
            {"name": "frmMainSwitchboard", "controls": [{"type": "label", "name": "lblHeader", "caption": "Welcome", "left": 720, "top": 360, "width": 2880, "height": 360}, {"type": "button", "name": "btnAdminTools", "caption": "Admin Tools", "left": 720, "top": 900, "width": 1800, "height": 360}, {"type": "button", "name": "btnUserManagement", "caption": "User Management", "left": 720, "top": 1380, "width": 1800, "height": 360}, {"type": "button", "name": "btnReports", "caption": "Reports", "left": 720, "top": 1860, "width": 1800, "height": 360}, {"type": "button", "name": "btnNewCase", "caption": "New Case", "left": 720, "top": 2340, "width": 1800, "height": 360}, {"type": "button", "name": "btnSearchCases", "caption": "Search Cases", "left": 720, "top": 2820, "width": 1800, "height": 360}, {"type": "button", "name": "btnMyOpenCases", "caption": "My Open Cases", "left": 720, "top": 3300, "width": 1800, "height": 360}, {"type": "button", "name": "btnQuickCaseload", "caption": "Quick Caseload", "left": 3000, "top": 900, "width": 1800, "height": 360}, {"type": "button", "name": "btnAuditLog", "caption": "Audit Log", "left": 3000, "top": 1380, "width": 1800, "height": 360}, {"type": "button", "name": "btnRefresh", "caption": "Refresh", "left": 3000, "top": 1860, "width": 1800, "height": 360}, {"type": "button", "name": "btnExit", "caption": "Exit", "left": 3000, "top": 2340, "width": 1800, "height": 360}, {"type": "button", "name": "btnHelp", "caption": "Help", "left": 3000, "top": 2820, "width": 1800, "height": 360}]},
            {"name": "frmCaseDetail", "controls": []},
            {"name": "frmCaseSearch", "controls": []},
            {"name": "frmCaseList", "controls": []},
            {"name": "frmReportMenu", "controls": []},
            {"name": "frmUserAdmin", "controls": []},
            {"name": "frmAdminTools", "controls": []},
            {"name": "frmAuditLogViewer", "controls": []},
            {"name": "frmDistrictReport", "controls": [{"type": "combo", "name": "cboDistrict", "row_source": "SELECT DistrictID, DistrictID FROM tblUsers ORDER BY DistrictID;", "column_count": 2, "bound_column": 1, "column_widths": "0;1.0", "label_text": "District", "left": 1440, "top": 720, "width": 1800, "height": 360}, {"type": "textbox", "name": "txtAsOfDate", "label_text": "As Of Date", "left": 1440, "top": 1260, "width": 1800, "height": 360}]},
        ],
        "reports": [
            {"name": "rptCaseLoadByDistrict", "controls": []},
        ],
        "expected": {
            "Forms": [
                {"Name": "frmMainSwitchboard", "Children": [{"Type": "Section", "Name": "Detail", "Children": [{"Type": "Label", "Name": "lblHeader", "Properties": {"Caption": "Welcome"}}, {"Type": "CommandButton", "Name": "btnAdminTools", "Properties": {"Caption": "Admin Tools"}}, {"Type": "CommandButton", "Name": "btnUserManagement", "Properties": {"Caption": "User Management"}}, {"Type": "CommandButton", "Name": "btnReports", "Properties": {"Caption": "Reports"}}, {"Type": "CommandButton", "Name": "btnNewCase", "Properties": {"Caption": "New Case"}}, {"Type": "CommandButton", "Name": "btnSearchCases", "Properties": {"Caption": "Search Cases"}}, {"Type": "CommandButton", "Name": "btnMyOpenCases", "Properties": {"Caption": "My Open Cases"}}, {"Type": "CommandButton", "Name": "btnQuickCaseload", "Properties": {"Caption": "Quick Caseload"}}, {"Type": "CommandButton", "Name": "btnAuditLog", "Properties": {"Caption": "Audit Log"}}, {"Type": "CommandButton", "Name": "btnRefresh", "Properties": {"Caption": "Refresh"}}, {"Type": "CommandButton", "Name": "btnExit", "Properties": {"Caption": "Exit"}}, {"Type": "CommandButton", "Name": "btnHelp", "Properties": {"Caption": "Help"}}]}]},
                {"Name": "frmCaseDetail"},
                {"Name": "frmCaseSearch"},
                {"Name": "frmCaseList"},
                {"Name": "frmReportMenu"},
                {"Name": "frmUserAdmin"},
                {"Name": "frmAdminTools"},
                {"Name": "frmAuditLogViewer"},
                {"Name": "frmDistrictReport", "Children": [{"Type": "Section", "Name": "Detail", "Children": [{"Type": "ComboBox", "Name": "cboDistrict", "Label": {"Source": "attached", "Text": "District"}, "Properties": {"RowSource": "SELECT DistrictID, DistrictID FROM tblUsers ORDER BY DistrictID;", "ColumnCount": 2, "BoundColumn": 1}}, {"Type": "TextBox", "Name": "txtAsOfDate", "Label": {"Source": "attached", "Text": "As Of Date"}}]}]},
            ],
            "Reports": [{"Name": "rptCaseLoadByDistrict"}],
            "Tables": [
                {"Name": "tblUsers", "Fields": [{"Name": "UserID", "DataType": "Long"}, {"Name": "DistrictID", "DataType": "Long"}], "PrimaryKey": ["UserID"]},
                {"Name": "tblSessionLog", "Fields": [{"Name": "LogID", "DataType": "Long"}, {"Name": "UserID", "DataType": "Long"}, {"Name": "EndTime", "DataType": "DateTime"}], "PrimaryKey": ["LogID"]},
            ],
        },
    },
]
