"""
Python helpers for building synthetic Access databases via COM automation.

Pattern adapted from c:\\Users\\denmi\\source\\repos\\MiniComplexTestData\\builder (reference only;
MCTD is not modified). Focused on the minimum needed for spec-driven synthetic forms.

Windows + Microsoft Access + pywin32 required to run.
"""

import os
import time


# ======================================================================
# Access / DAO constants (from win32com late-binding we don't get enums, so inline)
# ======================================================================

# Control type ids (CreateControl)
acTextBox = 109
acLabel = 100
acCommandButton = 104
acCheckBox = 106
acComboBox = 111
acListBox = 110
acSubForm = 112
acLine = 102
acRectangle = 101
acImage = 103

# Form sections
SECTION_DETAIL = 0
SECTION_HEADER = 1
SECTION_FOOTER = 2

# Form view modes
FORM_VIEW_SINGLE = 0
FORM_VIEW_CONTINUOUS = 1
FORM_VIEW_DATASHEET = 2

# acObjectType
acForm = 2
acReport = 3

# acCloseSave
acSaveYes = 1

# Twips per inch
TPI = 1440

# Report sections
REPORT_SECTION_DETAIL = 0
REPORT_SECTION_HEADER = 1
REPORT_SECTION_FOOTER = 2
REPORT_SECTION_GROUP_LEVEL_1_HEADER = 5
REPORT_SECTION_GROUP_LEVEL_1_FOOTER = 6

# Report/view commands
acCmdReportHdrFtr = 37


# ======================================================================
# COM helpers — identical shape to MCTD so translators built around either
# work interchangeably.
# ======================================================================

def _p(ctl, prop_name, value=None):
    """Get or set a control property via its Properties collection."""
    if value is None:
        return ctl.Properties(prop_name).Value
    ctl.Properties(prop_name).Value = value


def _set_props(ctl, **kwargs):
    """Set multiple properties, silently skipping ones Access rejects on this control type."""
    for prop_name, value in kwargs.items():
        if value is None:
            continue
        try:
            ctl.Properties(prop_name).Value = value
        except Exception:
            pass


# ======================================================================
# Control-creation helpers
# ======================================================================

def _add_label(form, section, left, top, width, height, caption, name=None,
               font_size=8, font_bold=False, fore_color=0, tag=None):
    ctl = form.Application.CreateControl(
        form.Name, acLabel, section, "", "", left, top, width, height
    )
    _set_props(ctl, Caption=caption, FontSize=font_size, FontBold=font_bold,
               ForeColor=fore_color)
    if name:
        _p(ctl, "Name", name)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


def _add_textbox(form, section, left, top, width, height, control_source="", name=None,
                 label_text=None, locked=False, enabled=True,
                 format_prop=None, input_mask=None, tag=None,
                 font_size=None, font_bold=None):
    ctl = form.Application.CreateControl(
        form.Name, acTextBox, section, "", control_source, left, top, width, height
    )
    if name:
        _p(ctl, "Name", name)
    _p(ctl, "Locked", locked)
    _p(ctl, "Enabled", enabled)

    if label_text is not None:
        # CreateControl(..., Parent=ctl.Name, ...) creates the label as a child
        # of the control, which is how SaveAsText emits attached labels (Round 1
        # of LabelResolver looks at Children for a Label node). Auto-label-on-
        # CreateControl is off by default in COM; this is the reliable path.
        _create_attached_label(form, section, ctl, left, top, height, label_text)

    _set_props(ctl, Format=format_prop, InputMask=input_mask,
               FontSize=font_size, FontBold=font_bold)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


def _create_attached_label(form, section, parent_ctl, parent_left, parent_top, parent_height, caption):
    """Create a Label as a child of `parent_ctl`, positioned just to the left.
    SaveAsText emits this as a Label child of the parent — what LabelResolver
    Round 1 looks for."""
    label_w = 1440
    label_left = max(0, parent_left - label_w - 60)
    label = form.Application.CreateControl(
        form.Name, acLabel, section, parent_ctl.Name, "",
        label_left, parent_top, label_w, parent_height
    )
    _set_props(label, Caption=caption)
    return label


def _add_combo(form, section, left, top, width, height, name=None,
               row_source="", row_source_type="Table/Query", control_source="",
               column_count=1, column_widths="", bound_column=1,
               limit_to_list=True, label_text=None, tag=None):
    ctl = form.Application.CreateControl(
        form.Name, acComboBox, section, "", control_source, left, top, width, height
    )
    if name:
        _p(ctl, "Name", name)
    _p(ctl, "RowSource", row_source)
    _p(ctl, "RowSourceType", row_source_type)
    _p(ctl, "ColumnCount", column_count)
    if column_widths:
        _p(ctl, "ColumnWidths", column_widths)
    _p(ctl, "BoundColumn", bound_column)
    _p(ctl, "LimitToList", limit_to_list)

    if label_text is not None:
        _create_attached_label(form, section, ctl, left, top, height, label_text)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


def _add_button(form, section, left, top, width, height, caption, name=None, tag=None):
    ctl = form.Application.CreateControl(
        form.Name, acCommandButton, section, "", "", left, top, width, height
    )
    _p(ctl, "Caption", caption)
    if name:
        _p(ctl, "Name", name)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


def _add_checkbox(form, section, left, top, width, height, control_source="", name=None,
                  label_text=None, default_value=None, tag=None):
    ctl = form.Application.CreateControl(
        form.Name, acCheckBox, section, "", control_source, left, top, width, height
    )
    if name:
        _p(ctl, "Name", name)
    if default_value is not None:
        _p(ctl, "DefaultValue", str(default_value))
    if label_text is not None:
        _create_attached_label(form, section, ctl, left, top, height, label_text)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


def _add_subform(form, section, left, top, width, height, source_object, name=None,
                 link_master_fields=None, link_child_fields=None, tag=None):
    ctl = form.Application.CreateControl(
        form.Name, acSubForm, section, "", "", left, top, width, height
    )
    if name:
        _p(ctl, "Name", name)
    _p(ctl, "SourceObject", source_object)
    if link_master_fields:
        _p(ctl, "LinkMasterFields", link_master_fields)
    if link_child_fields:
        _p(ctl, "LinkChildFields", link_child_fields)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


# ======================================================================
# Form lifecycle helpers
# ======================================================================

def _create_form(access_app, caption=None, record_source="", default_view=FORM_VIEW_SINGLE,
                 has_header=False, has_footer=False,
                 width=None, detail_height=None,
                 navigation_buttons=False, record_selectors=False,
                 allow_additions=False, allow_deletions=False, allow_edits=True,
                 pop_up=False, modal=False, border_style=0, scroll_bars=0):
    """Create a form in design view; returns (form, temp_name)."""
    form = access_app.CreateForm()
    temp_name = form.Name

    form.RecordSource = record_source
    form.DefaultView = default_view
    form.Caption = caption or temp_name
    form.ScrollBars = scroll_bars
    form.NavigationButtons = navigation_buttons
    form.RecordSelectors = record_selectors
    form.AllowAdditions = allow_additions
    form.AllowDeletions = allow_deletions
    form.AllowEdits = allow_edits
    form.PopUp = pop_up
    form.Modal = modal
    form.BorderStyle = border_style

    if has_header or has_footer:
        access_app.DoCmd.RunCommand(36)  # acCmdFormHdrFtr

    if has_header:
        form.Section(SECTION_HEADER).Height = int(TPI * 0.5)
    elif has_footer:
        form.Section(SECTION_HEADER).Height = 0
    if has_footer:
        form.Section(SECTION_FOOTER).Height = int(TPI * 0.4)
    elif has_header:
        form.Section(SECTION_FOOTER).Height = 0
    if detail_height:
        form.Section(SECTION_DETAIL).Height = detail_height

    if width:
        form.Width = width

    return form, temp_name


def _close_form(access_app, form, temp_name, desired_name):
    """Save and close the form, renaming to desired_name."""
    access_app.DoCmd.Close(acForm, temp_name, acSaveYes)
    access_app.DoCmd.Rename(desired_name, acForm, temp_name)


def _create_report(access_app, caption=None, record_source="",
                   has_report_header=False, has_report_footer=False,
                   width=None, detail_height=None):
    """Create a report in design view; returns (report, temp_name)."""
    report = access_app.CreateReport()
    temp_name = report.Name

    report.RecordSource = record_source
    report.Caption = caption or temp_name

    if has_report_header or has_report_footer:
        access_app.DoCmd.RunCommand(acCmdReportHdrFtr)

    if detail_height:
        report.Section(REPORT_SECTION_DETAIL).Height = detail_height
    if width:
        report.Width = width

    return report, temp_name


def _close_report(access_app, report, temp_name, desired_name):
    """Save and close the report, renaming to desired_name."""
    access_app.DoCmd.Close(acReport, temp_name, acSaveYes)
    access_app.DoCmd.Rename(desired_name, acReport, temp_name)


def _report_create_control(report, control_type, section, parent_name, control_source,
                           left, top, width, height):
    return report.Application.CreateReportControl(
        report.Name,
        control_type,
        section,
        parent_name,
        control_source,
        left,
        top,
        width,
        height,
    )


def _create_attached_report_label(report, section, parent_ctl, parent_left, parent_top,
                                  parent_height, caption):
    label_w = 1440
    label_left = max(0, parent_left - label_w - 60)
    label = _report_create_control(
        report, acLabel, section, parent_ctl.Name, "",
        label_left, parent_top, label_w, parent_height
    )
    _set_props(label, Caption=caption)
    return label


def _add_report_label(report, section, left, top, width, height, caption, name=None,
                      font_size=8, font_bold=False, fore_color=0, tag=None):
    ctl = _report_create_control(report, acLabel, section, "", "", left, top, width, height)
    _set_props(ctl, Caption=caption, FontSize=font_size, FontBold=font_bold,
               ForeColor=fore_color)
    if name:
        _p(ctl, "Name", name)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


def _add_report_textbox(report, section, left, top, width, height, control_source="", name=None,
                        label_text=None, locked=False, enabled=True,
                        format_prop=None, input_mask=None, tag=None,
                        font_size=None, font_bold=None):
    ctl = _report_create_control(
        report, acTextBox, section, "", control_source, left, top, width, height
    )
    if name:
        _p(ctl, "Name", name)
    if label_text is not None:
        _create_attached_report_label(
            report, section, ctl, left, top, height, label_text
        )
    _set_props(ctl, Format=format_prop, InputMask=input_mask,
               FontSize=font_size, FontBold=font_bold)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


def _add_report_subreport(report, section, left, top, width, height, source_object,
                          name=None, tag=None):
    ctl = _report_create_control(
        report, acSubForm, section, "", "", left, top, width, height
    )
    if name:
        _p(ctl, "Name", name)
    _p(ctl, "SourceObject", source_object)
    if tag:
        _p(ctl, "Tag", tag)
    return ctl


# ======================================================================
# Application lifecycle
# ======================================================================

def open_new_database(db_path):
    """Delete an existing .accdb at db_path, then create a fresh one.
    Returns a started Access.Application with CurrentDatabase opened."""
    import win32com.client

    if os.path.exists(db_path):
        os.remove(db_path)
        time.sleep(0.5)

    os.makedirs(os.path.dirname(os.path.abspath(db_path)), exist_ok=True)

    # Dispatch will attach to a running Access instance if one exists; skipping
    # Visible here avoids the "invalid reference to property Visible" failure seen
    # when attaching to an already-running instance (new invisible instances are
    # invisible by default, so this is harmless). The quit_app helper below makes
    # sure we don't leave zombies behind.
    access_app = win32com.client.Dispatch("Access.Application")
    try:
        access_app.UserControl = False
    except Exception:
        pass
    try:
        access_app.AutomationSecurity = 1  # msoAutomationSecurityLow
    except Exception:
        pass
    access_app.NewCurrentDatabase(db_path)
    access_app.DoCmd.SetWarnings(False)
    return access_app


def quit_app(access_app):
    """Close the current DB and quit Access cleanly."""
    try:
        access_app.DoCmd.SetWarnings(True)
    except Exception:
        pass
    try:
        access_app.CloseCurrentDatabase()
    except Exception:
        pass
    access_app.Quit()
    import gc
    gc.collect()
