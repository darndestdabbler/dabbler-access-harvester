```json
{
  "schemaType": "DatabaseManifest",
  "schemaVersion": "string",
  "harvesterVersion": "string",
  "database": {
    "name": "string",
    "filePath": "string",
    "properties": "object"
  },
  "summary": {
    "moduleCount": "number",
    "formCount": "number",
    "reportCount": "number",
    "queryCount": "number",
    "tableCount": "number",
    "unresolvedItemCount": "number"
  },
  "tables": "Array<TableDefinition>",
  "relationships": "Array<RelationshipDefinition>",
  "queries": "Array<QueryAbstraction>",
  "modules": "Array<ModuleExport>"
}
```