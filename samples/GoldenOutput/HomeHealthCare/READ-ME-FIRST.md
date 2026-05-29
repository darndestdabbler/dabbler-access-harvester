# Dabbler Access Harvester output

The canonical entry point for this dataset is `manifest.json`.

This output conforms to `harvester-schema` version 1.1. The
manifest's `Documentation.Url` field points at the interpretation
guide so a binary-only consumer can read the schema without the
source repo. The manifest's `FileMap` field lists the on-disk
globs for every per-object file under `ir/`.

Interpretation guide:
https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md
