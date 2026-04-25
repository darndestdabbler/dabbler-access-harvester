# test-builder — Synthetic Access DB generator

Builds small, purpose-built Access `.accdb` files from JSON specs. Each spec
describes one form and a handful of controls that exercise a specific
enrichment case (twips→inches geometry, label resolution rounds, font-size
deviation, etc.). The resulting `.accdb` is committed to
`tests/AccessExporter.Tests/SyntheticDbs/` and drives integration tests for the
enrichment pipeline.

## Prerequisites

- **Windows** with **Microsoft Access** installed (2019 / 2021 / Microsoft 365 all work)
- **Python 3.9+** with **pywin32**: `pip install pywin32`

The builder uses COM automation — it will briefly launch Access (invisibly) to
create each database.

## Usage

```
cd test-builder
python build_synthetic.py                  # build every spec in specs/
python build_synthetic.py geometry_basic   # build one spec by name (no .json)
```

Output goes to `../tests/AccessExporter.Tests/SyntheticDbs/<spec>.accdb`.

## Adding a new spec

1. Write a spec JSON under `specs/<descriptive_name>.json`. See the existing
   `geometry_basic.json` for the shape. All positions/sizes are in **twips**
   (1 inch = 1440 twips) to match Access's internal unit.
2. Each control should carry a `tag` string naming the enrichment case it
   exercises (e.g., `"tag": "geometry_non_round_1500_twips"`). The tag ends up
   on the control's Access `.Tag` property and makes fixtures easier to trace
   back to their test intent.
3. Run `python build_synthetic.py <name>` to generate the `.accdb`. Commit
   both the spec and the resulting `.accdb` to the repo so downstream tests
   are hermetic.

## Supported control types

`textbox`, `label`, `combo`, `button`, `checkbox`. Add more in
`create_from_spec.py` as new enrichment phases need them.
