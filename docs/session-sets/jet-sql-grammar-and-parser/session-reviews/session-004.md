# Verification Round 1

```markdown
VERDICT: VERIFIED

Remaining high-priority uncovered rules:
- `formReference` mixed-access chains preserving both `!` and `.` operators, e.g. `Forms!frmX.Form!ctlY` / `Reports!rptX.Section(acDetail).Visible`
- Bracketed `[Reports]` root through the semantic-predicate path (`[Reports]![rptX]![ctlY]`), since only `[Forms]` and bare `Reports` are covered
- Negated subquery branch: `NOT IN (SELECT ...)`
- `SELECT DISTINCTROW` and combined `SELECT DISTINCT TOP n ...`
- `FALSE` literal branch
```
