"""
Generic spec-driven form builder.

A spec is a dict (loaded from JSON) that describes a single synthetic form and
its controls. Positions and sizes are given in twips (twentieths of a point —
1 inch = 1440 twips) to match Access's internal unit and make round-trip
verification of the twips→inches enrichment unambiguous.

Spec shape:
    {
      "formName": "frmGeometryBasic",
      "caption": "Geometry Basic Test",
      "recordSource": "",
      "hasHeader": false,
      "hasFooter": false,
      "width_twips": 10080,
      "detailHeight_twips": 2160,
      "controls": [
        {
          "type": "textbox" | "label" | "combo" | "button" | "checkbox",
          "section": "detail" | "header" | "footer",
          "name": "txtFoo",
          "left_twips": 0,
          "top_twips": 0,
          "width_twips": 1440,
          "height_twips": 360,
          "tag": "geometry_origin",
          ...type-specific properties...
        },
        ...
      ]
    }
"""

from builder import (
    _create_form, _close_form,
    _add_textbox, _add_label, _add_combo, _add_button, _add_checkbox,
    SECTION_DETAIL, SECTION_HEADER, SECTION_FOOTER,
    FORM_VIEW_SINGLE,
)


_SECTION_MAP = {
    "detail": SECTION_DETAIL,
    "header": SECTION_HEADER,
    "footer": SECTION_FOOTER,
}


def _section(name):
    if name not in _SECTION_MAP:
        raise ValueError(f"Unknown section '{name}'. Expected one of: {list(_SECTION_MAP)}")
    return _SECTION_MAP[name]


def create_form_from_spec(access_app, spec):
    """Build a form from a spec dict. Saves and closes when done."""
    form_name = spec["formName"]
    print(f"  Creating {form_name}...")

    form, temp_name = _create_form(
        access_app,
        caption=spec.get("caption"),
        record_source=spec.get("recordSource", ""),
        default_view=FORM_VIEW_SINGLE,
        has_header=spec.get("hasHeader", False),
        has_footer=spec.get("hasFooter", False),
        width=spec.get("width_twips"),
        detail_height=spec.get("detailHeight_twips"),
    )

    for ctl_spec in spec.get("controls", []):
        _add_control_from_spec(form, ctl_spec)

    _close_form(access_app, form, temp_name, form_name)


def _add_control_from_spec(form, c):
    """Dispatch on control type."""
    ctype = c["type"].lower()
    section = _section(c.get("section", "detail"))
    left = int(c["left_twips"])
    top = int(c["top_twips"])
    width = int(c["width_twips"])
    height = int(c["height_twips"])
    name = c.get("name")
    tag = c.get("tag")

    if ctype == "textbox":
        _add_textbox(
            form, section, left, top, width, height,
            control_source=c.get("controlSource", ""),
            name=name,
            label_text=c.get("labelText"),
            locked=c.get("locked", False),
            enabled=c.get("enabled", True),
            format_prop=c.get("format"),
            input_mask=c.get("inputMask"),
            tag=tag,
            font_size=c.get("fontSize"),
            font_bold=c.get("fontBold"),
        )
    elif ctype == "label":
        _add_label(
            form, section, left, top, width, height,
            caption=c.get("caption", ""),
            name=name,
            font_size=c.get("fontSize", 8),
            font_bold=c.get("fontBold", False),
            fore_color=c.get("foreColor", 0),
            tag=tag,
        )
    elif ctype == "combo":
        _add_combo(
            form, section, left, top, width, height,
            name=name,
            row_source=c.get("rowSource", ""),
            row_source_type=c.get("rowSourceType", "Table/Query"),
            control_source=c.get("controlSource", ""),
            column_count=c.get("columnCount", 1),
            column_widths=c.get("columnWidths", ""),
            bound_column=c.get("boundColumn", 1),
            limit_to_list=c.get("limitToList", True),
            label_text=c.get("labelText"),
            tag=tag,
        )
    elif ctype == "button":
        _add_button(
            form, section, left, top, width, height,
            caption=c.get("caption", ""),
            name=name,
            tag=tag,
        )
    elif ctype == "checkbox":
        _add_checkbox(
            form, section, left, top, width, height,
            control_source=c.get("controlSource", ""),
            name=name,
            label_text=c.get("labelText"),
            default_value=c.get("defaultValue"),
            tag=tag,
        )
    else:
        raise ValueError(f"Unsupported control type: {c['type']!r}")
