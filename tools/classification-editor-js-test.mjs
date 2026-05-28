// Headless JS test for tools/classification-editor.html pure helpers.
//
// Extracts the editor's pure-helper block and runs it in a Node vm context
// against samples/classification-fixture.json and the schema itself.
// Verifies:
//   - editor SCHEMA_VERSION === schema.properties.version.const
//   - editor CATEGORIES codes set === schema.definitions.ColumnCategory.enum
//   - editor MASKING_STRATEGIES codes set === schema.definitions.MaskingStrategy.enum
//   - defaultColumnOverride / defaultTableOverride match required schema keys
//   - isPlausibleClassificationDoc accepts the fixture / rejects bad shapes
//   - findFkAuthorityViolations is clean on the fixture, flags planted violations
//   - isFkAuthorityValid agrees per-column with the schema's allOf
//   - findSuspectNotes catches each RAW_VALUE_HEURISTICS pattern (SSN, phone, email)
//
// Run from the workspace root:
//
//     node tools/classification-editor-js-test.mjs

import { readFileSync } from "node:fs";
import vm from "node:vm";

const editorHtml = readFileSync("tools/classification-editor.html", "utf-8");
const schema = JSON.parse(readFileSync(
  "AccessHarvester.Pipeline/Classification/classification-schema.json",
  "utf-8",
));

// Pull out the script content between the helpers header and the
// "App state" header. That's where the pure functions live.
const start = editorHtml.indexOf("// ===== Domain constants =====");
const end = editorHtml.indexOf("// ===== App state =====");
if (start < 0 || end < 0) {
  throw new Error("could not locate pure-helper block in editor HTML");
}
const block = editorHtml.slice(start, end);

// Run the block in a Node vm context with a fake module/exports so the
// `if (typeof module !== "undefined")` guard fires.
const context = {
  module: { exports: {} },
  console,
};
context.exports = context.module.exports;
vm.createContext(context);
vm.runInContext('"use strict";\n' + block, context);

const exported = context.module.exports;
const required = [
  "CATEGORIES", "MASKING_STRATEGIES",
  "FK_ALLOWED_CATEGORIES", "FK_ALLOWED_STRATEGIES",
  "defaultColumnOverride", "defaultTableOverride",
  "isFkAuthorityValid", "findFkAuthorityViolations",
  "findSuspectNotes", "isPlausibleClassificationDoc",
  "SCHEMA_VERSION",
];
for (const k of required) {
  if (!(k in exported)) {
    throw new Error(`editor pure helpers missing export: ${k}`);
  }
}
const {
  CATEGORIES, MASKING_STRATEGIES,
  FK_ALLOWED_CATEGORIES, FK_ALLOWED_STRATEGIES,
  defaultColumnOverride, defaultTableOverride,
  isFkAuthorityValid, findFkAuthorityViolations,
  findSuspectNotes, isPlausibleClassificationDoc,
  SCHEMA_VERSION,
} = exported;

const fixture = JSON.parse(readFileSync("samples/classification-fixture.json", "utf-8"));

const failures = [];

// 1. Editor constants exactly match the schema's enums and version const.
const schemaVersion = schema.properties?.version?.const;
if (!schemaVersion) failures.push("schema.properties.version.const missing");
if (SCHEMA_VERSION !== schemaVersion) {
  failures.push(`SCHEMA_VERSION ${JSON.stringify(SCHEMA_VERSION)} (want schema.properties.version.const = ${JSON.stringify(schemaVersion)})`);
}
const schemaCategoryEnum = schema.definitions?.ColumnCategory?.enum;
const editorCategoryCodes = CATEGORIES.map(c => c.code);
if (!Array.isArray(schemaCategoryEnum)) {
  failures.push("schema.definitions.ColumnCategory.enum missing");
} else if (!setsEqual(editorCategoryCodes, schemaCategoryEnum)) {
  failures.push(
    "CATEGORIES codes do not match schema ColumnCategory enum.\n" +
    `    editor: ${JSON.stringify(editorCategoryCodes.slice().sort())}\n` +
    `    schema: ${JSON.stringify(schemaCategoryEnum.slice().sort())}`
  );
}
const schemaMaskingEnum = schema.definitions?.MaskingStrategy?.enum;
const editorMaskingCodes = MASKING_STRATEGIES.map(m => m.code);
if (!Array.isArray(schemaMaskingEnum)) {
  failures.push("schema.definitions.MaskingStrategy.enum missing");
} else if (!setsEqual(editorMaskingCodes, schemaMaskingEnum)) {
  failures.push(
    "MASKING_STRATEGIES codes do not match schema MaskingStrategy enum.\n" +
    `    editor: ${JSON.stringify(editorMaskingCodes.slice().sort())}\n` +
    `    schema: ${JSON.stringify(schemaMaskingEnum.slice().sort())}`
  );
}
if (!FK_ALLOWED_CATEGORIES.has("foreign-key") || FK_ALLOWED_CATEGORIES.size !== 1) {
  failures.push(`FK_ALLOWED_CATEGORIES wrong: ${[...FK_ALLOWED_CATEGORIES].join(",")}`);
}
if (!FK_ALLOWED_STRATEGIES.has("preserve") || !FK_ALLOWED_STRATEGIES.has("remap-id") || FK_ALLOWED_STRATEGIES.size !== 2) {
  failures.push(`FK_ALLOWED_STRATEGIES wrong: ${[...FK_ALLOWED_STRATEGIES].join(",")}`);
}

// 2. Defaults match schema-required shape.
const colDef = defaultColumnOverride();
const expectedColKeys = ["category", "maskingStrategy", "preserveAutonumber", "reviewedBy", "notes"].sort();
const actualColKeys = Object.keys(colDef).sort();
if (JSON.stringify(actualColKeys) !== JSON.stringify(expectedColKeys)) {
  failures.push(`defaultColumnOverride keys ${JSON.stringify(actualColKeys)} (want ${JSON.stringify(expectedColKeys)})`);
}
if (colDef.preserveAutonumber !== false) failures.push("defaultColumnOverride().preserveAutonumber should be false");
if (colDef.notes !== "") failures.push("defaultColumnOverride().notes should be empty string");
const tableDef = defaultTableOverride();
const expectedTableKeys = ["isLookup", "reviewedBy", "notes"].sort();
const actualTableKeys = Object.keys(tableDef).sort();
if (JSON.stringify(actualTableKeys) !== JSON.stringify(expectedTableKeys)) {
  failures.push(`defaultTableOverride keys ${JSON.stringify(actualTableKeys)} (want ${JSON.stringify(expectedTableKeys)})`);
}

// 3. isPlausibleClassificationDoc accepts the fixture, rejects wrong shapes.
if (!isPlausibleClassificationDoc(fixture)) failures.push("isPlausibleClassificationDoc rejected the fixture");
if (isPlausibleClassificationDoc(null)) failures.push("isPlausibleClassificationDoc accepted null");
if (isPlausibleClassificationDoc({ version: "0.9", database: "x", scannedUtc: "x", tables: [] })) {
  failures.push("isPlausibleClassificationDoc accepted wrong version");
}
if (isPlausibleClassificationDoc({ version: "1.0", database: "x", scannedUtc: "x" })) {
  failures.push("isPlausibleClassificationDoc accepted missing tables array");
}

// 4. FK authority: fixture is clean.
const violations = findFkAuthorityViolations(fixture);
if (violations.length !== 0) {
  failures.push(`fixture should have no FK violations; found ${violations.length}`);
}

// 5. FK authority: planted violation is detected.
const bad = JSON.parse(JSON.stringify(fixture));
const visit = bad.tables.find(t => t.name === "Visit");
const personIdFk = visit.columns.find(c => c.name === "PersonID");
personIdFk.humanOverride.category = "direct-identifier-pii";
const planted = findFkAuthorityViolations(bad);
if (planted.length === 0) {
  failures.push("findFkAuthorityViolations missed planted PersonID FK violation");
} else if (planted[0].column !== "PersonID") {
  failures.push(`planted violation reported wrong column: ${planted[0].column}`);
}

// 6. FK authority: isFkAuthorityValid agrees per-column.
const personIdClean = fixture.tables.find(t => t.name === "Visit").columns.find(c => c.name === "PersonID");
if (!isFkAuthorityValid(personIdClean)) failures.push("isFkAuthorityValid failed on clean fixture FK");
if (isFkAuthorityValid(personIdFk)) failures.push("isFkAuthorityValid accepted planted FK violation");

// 7. FK authority: a non-FK column with any category override is valid.
const personFirstName = JSON.parse(JSON.stringify(
  fixture.tables.find(t => t.name === "Person").columns.find(c => c.name === "FirstName"),
));
personFirstName.humanOverride.category = "quasi-identifier";
if (!isFkAuthorityValid(personFirstName)) {
  failures.push("isFkAuthorityValid rejected a non-FK column with category override");
}

// 8. findSuspectNotes flags every RAW_VALUE_HEURISTICS pattern.
const PROBES = [
  { name: "SSN (dashed)",  note: "Saw 123-45-6789 in production" },
  { name: "SSN (dotted)",  note: "Reviewer pasted 123.45.6789 here" },
  { name: "US phone",      note: "Caller's number was 555-867-5309" },
  { name: "email",         note: "Reported by jane.doe@example.com" },
];
for (const probe of PROBES) {
  const planted2 = JSON.parse(JSON.stringify(fixture));
  planted2.tables[0].columns[0].humanOverride.notes = probe.note;
  const suspects = findSuspectNotes(planted2);
  if (suspects.length === 0) {
    failures.push(`findSuspectNotes missed planted ${probe.name}: ${probe.note}`);
  }
}
const cleanSuspects = findSuspectNotes(fixture);
if (cleanSuspects.length !== 0) {
  failures.push(`findSuspectNotes false-positived on clean fixture: ${JSON.stringify(cleanSuspects)}`);
}

// Helper: do two iterables contain exactly the same elements (set-wise)?
function setsEqual(a, b) {
  const sa = new Set(a);
  const sb = new Set(b);
  if (sa.size !== sb.size) return false;
  for (const x of sa) if (!sb.has(x)) return false;
  return true;
}

// Report.
if (failures.length > 0) {
  console.error("FAIL");
  for (const f of failures) console.error(`  - ${f}`);
  process.exit(1);
}
console.log("OK: 8 JS check groups passed");
console.log(`  - ${CATEGORIES.length} categories match schema enum`);
console.log(`  - ${MASKING_STRATEGIES.length} masking strategies match schema enum`);
console.log(`  - schema-version constant: ${SCHEMA_VERSION} (matches schema.properties.version.const)`);
