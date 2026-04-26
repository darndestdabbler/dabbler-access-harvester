"""Build the Session 3 metadata fixtures and paired expected.json files."""

from __future__ import annotations

import json
import os
import sys
import time
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from builder import (  # noqa: E402
    REPORT_SECTION_DETAIL,
    REPORT_SECTION_FOOTER,
    REPORT_SECTION_GROUP_LEVEL_1_FOOTER,
    REPORT_SECTION_GROUP_LEVEL_1_HEADER,
    SECTION_DETAIL,
    SECTION_FOOTER,
    SECTION_HEADER,
    _add_button,
    _add_checkbox,
    _add_combo,
    _add_label,
    _add_report_label,
    _add_report_subreport,
    _add_report_textbox,
    _add_subform,
    _add_textbox,
    _close_form,
    _close_report,
    _create_form,
    _create_report,
    open_new_database,
    quit_app,
)
from metadata_fixture_specs import FIXTURES  # noqa: E402


ROOT = Path(__file__).resolve().parent
OUT_DIR = ROOT.parent / "tests" / "AccessHarvester.Tests" / "SyntheticDbs"

FORM_SECTIONS = {
    "detail": SECTION_DETAIL,
    "header": SECTION_HEADER,
    "footer": SECTION_FOOTER,
}

REPORT_SECTIONS = {
    "detail": REPORT_SECTION_DETAIL,
    "report-footer": REPORT_SECTION_FOOTER,
    "group-header-0": REPORT_SECTION_GROUP_LEVEL_1_HEADER,
    "group-footer-0": REPORT_SECTION_GROUP_LEVEL_1_FOOTER,
}


def _build_create_table_sql(table: dict) -> str:
    field_defs = [f"{field['name']} {field['sql_type']}" for field in table["fields"]]
    pk_fields = table.get("primary_key") or []
    if pk_fields:
        field_defs.append(
            f"CONSTRAINT PK_{table['name']} PRIMARY KEY ({', '.join(pk_fields)})"
        )
    return f"CREATE TABLE {table['name']} ({', '.join(field_defs)});"


def _create_tables(access_app, fixture: dict) -> None:
    db = access_app.CurrentDb()
    for table in fixture.get("tables", []):
        db.Execute(_build_create_table_sql(table))


def _create_queries(access_app, fixture: dict) -> None:
    db = access_app.CurrentDb()
    for query in fixture.get("queries", []):
        db.CreateQueryDef(query["name"], query["sql"])


def _create_relationships(access_app, fixture: dict) -> None:
    db = access_app.CurrentDb()
    for relationship in fixture.get("relationships", []):
        relation = db.CreateRelation(
            relationship["name"],
            relationship["parent_table"],
            relationship["child_table"],
        )
        for pair in relationship["fields"]:
            field = relation.CreateField(pair["parent"])
            field.ForeignName = pair["child"]
            relation.Fields.Append(field)
        db.Relations.Append(relation)


def _section_flags(controls: list[dict]) -> tuple[bool, bool]:
    sections = {control.get("section", "detail") for control in controls}
    return ("header" in sections, "footer" in sections)


def _add_form_control(form, control: dict) -> None:
    section = FORM_SECTIONS[control.get("section", "detail")]
    left = control.get("left", 720)
    top = control.get("top", 720)
    width = control.get("width", 1440)
    height = control.get("height", 360)
    ctype = control["type"]
    if ctype == "textbox":
        _add_textbox(
            form, section, left, top, width, height,
            control_source=control.get("control_source", ""),
            name=control["name"],
            label_text=control.get("label_text"),
        )
    elif ctype == "label":
        _add_label(
            form, section, left, top, width, height,
            caption=control.get("caption", ""),
            name=control["name"],
        )
    elif ctype == "combo":
        _add_combo(
            form, section, left, top, width, height,
            name=control["name"],
            row_source=control.get("row_source", ""),
            row_source_type=control.get("row_source_type", "Table/Query"),
            control_source=control.get("control_source", ""),
            column_count=control.get("column_count", 1),
            column_widths=control.get("column_widths", ""),
            bound_column=control.get("bound_column", 1),
            limit_to_list=control.get("limit_to_list", True),
            label_text=control.get("label_text"),
        )
    elif ctype == "button":
        _add_button(
            form, section, left, top, width, height,
            caption=control.get("caption", ""),
            name=control["name"],
        )
    elif ctype == "checkbox":
        _add_checkbox(
            form, section, left, top, width, height,
            control_source=control.get("control_source", ""),
            name=control["name"],
            label_text=control.get("label_text"),
        )
    elif ctype == "subform":
        _add_subform(
            form, section, left, top, width, height,
            source_object=f"Form.{control['source_object']}",
            name=control["name"],
        )
    else:
        raise ValueError(f"Unsupported form control type: {ctype}")


def _create_forms(access_app, fixture: dict) -> None:
    for form_spec in fixture.get("forms", []):
        controls = form_spec.get("controls", [])
        has_header, has_footer = _section_flags(controls)
        form, temp_name = _create_form(
            access_app,
            caption=form_spec.get("caption"),
            record_source=form_spec.get("record_source", ""),
            has_header=has_header,
            has_footer=has_footer,
            width=form_spec.get("width"),
            detail_height=form_spec.get("detail_height"),
        )
        try:
            for control in controls:
                _add_form_control(form, control)
        finally:
            _close_form(access_app, form, temp_name, form_spec["name"])


def _add_report_control(report, control: dict) -> None:
    section = REPORT_SECTIONS[control.get("section", "detail")]
    left = control.get("left", 720)
    top = control.get("top", 240)
    width = control.get("width", 1440)
    height = control.get("height", 300)
    ctype = control["type"]
    if ctype == "textbox":
        _add_report_textbox(
            report, section, left, top, width, height,
            control_source=control.get("control_source", ""),
            name=control["name"],
        )
    elif ctype == "label":
        _add_report_label(
            report, section, left, top, width, height,
            caption=control.get("caption", ""),
            name=control["name"],
        )
    elif ctype == "subreport":
        _add_report_subreport(
            report, section, left, top, width, height,
            source_object=f"Report.{control['source_object']}",
            name=control["name"],
        )
    else:
        raise ValueError(f"Unsupported report control type: {ctype}")


def _create_reports(access_app, fixture: dict) -> None:
    for report_spec in fixture.get("reports", []):
        has_report_footer = any(
            control.get("section") == "report-footer"
            for control in report_spec.get("controls", [])
        )
        report, temp_name = _create_report(
            access_app,
            caption=report_spec.get("caption"),
            record_source=report_spec.get("record_source", ""),
            has_report_footer=has_report_footer,
            width=report_spec.get("width"),
            detail_height=report_spec.get("detail_height"),
        )
        try:
            for group in report_spec.get("group_levels", []):
                access_app.CreateGroupLevel(
                    report.Name,
                    group["expression"],
                    group.get("header", False),
                    group.get("footer", False),
                )
            for control in report_spec.get("controls", []):
                _add_report_control(report, control)
        finally:
            _close_report(access_app, report, temp_name, report_spec["name"])


def _write_expected(fixture: dict) -> None:
    expected_path = OUT_DIR / f"{fixture['name']}.expected.json"
    with expected_path.open("w", encoding="utf-8") as handle:
        json.dump(fixture.get("expected", {}), handle, indent=2)
        handle.write("\n")


def build_fixture(fixture: dict) -> None:
    db_path = OUT_DIR / f"{fixture['name']}.accdb"
    print(f"\n=== Building {fixture['name']} ===")
    print(f"    Database: {db_path}")
    access_app = open_new_database(str(db_path))
    try:
        _create_tables(access_app, fixture)
        _create_queries(access_app, fixture)
        _create_relationships(access_app, fixture)
        _create_forms(access_app, fixture)
        _create_reports(access_app, fixture)
    finally:
        quit_app(access_app)
        time.sleep(0.3)
    _write_expected(fixture)
    print(f"    Expected: {OUT_DIR / (fixture['name'] + '.expected.json')}")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    wanted = set(sys.argv[1:])
    fixtures = [
        fixture for fixture in FIXTURES
        if not wanted or fixture["name"] in wanted
    ]
    if wanted and len(fixtures) != len(wanted):
        found = {fixture["name"] for fixture in fixtures}
        missing = sorted(wanted - found)
        raise SystemExit(f"Unknown fixture name(s): {', '.join(missing)}")
    for fixture in fixtures:
        build_fixture(fixture)
    print(f"\n=== Metadata fixture build complete. Output directory: {OUT_DIR} ===")


if __name__ == "__main__":
    main()
