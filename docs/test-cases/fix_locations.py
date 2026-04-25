#!/usr/bin/env python3
"""Fix common location omissions in test case JSON files.

Many references, statements, and resolvedCallees were written without
location stubs. This script adds minimal valid locations so the data
validates against the schema. The added locations are plausible
placeholders; for a real harvester these would be precise."""

import json
import sys
from pathlib import Path
from copy import deepcopy

DEFAULT_LOC = {"startLine": 1, "startColumn": 1}

def ensure_location(obj, default=None):
    """Ensure obj has a 'location' key with at least startLine/startColumn."""
    if not isinstance(obj, dict):
        return
    if 'location' not in obj:
        obj['location'] = deepcopy(default or DEFAULT_LOC)
        return
    loc = obj['location']
    if not isinstance(loc, dict):
        obj['location'] = deepcopy(default or DEFAULT_LOC)
        return
    if 'startLine' not in loc:
        loc['startLine'] = (default or DEFAULT_LOC)['startLine']
    if 'startColumn' not in loc:
        loc['startColumn'] = (default or DEFAULT_LOC)['startColumn']

def fix_reference(ref, parent_loc=None):
    """Fix a resolvedReference."""
    if not isinstance(ref, dict):
        return
    ensure_location(ref, parent_loc)

def fix_call_site(call, parent_loc=None):
    if not isinstance(call, dict):
        return
    ensure_location(call, parent_loc)
    rc = call.get('resolvedCallee')
    if isinstance(rc, dict):
        ensure_location(rc, call.get('location') or parent_loc)
    for arg in call.get('arguments', []) or []:
        if isinstance(arg, dict):
            # arguments don't require location, but parsed/etc may need fixing if present
            pass

def fix_sql(sql, parent_loc=None):
    if not isinstance(sql, dict):
        return
    ensure_location(sql, parent_loc)
    for tc in sql.get('templateCandidates', []) or []:
        if not isinstance(tc, dict):
            continue
        for pb in tc.get('parameterBindings', []) or []:
            if isinstance(pb, dict):
                rr = pb.get('resolvedReference')
                if isinstance(rr, dict):
                    ensure_location(rr, sql.get('location') or parent_loc)

def fix_statement(stmt, parent_loc=None):
    if not isinstance(stmt, dict):
        return
    ensure_location(stmt, parent_loc)

def fix_item(item):
    if item.get('itemKind') == 'procedure':
        body = item.get('body', {})
        proc_loc = None  # statements should have their own; placeholder if missing
        for s in body.get('statements', []) or []:
            fix_statement(s, proc_loc)
        for r in body.get('references', []) or []:
            fix_reference(r, proc_loc)
        for c in body.get('calls', []) or []:
            fix_call_site(c, proc_loc)
        for sql in body.get('sqlStrings', []) or []:
            fix_sql(sql, proc_loc)
    elif item.get('itemKind') == 'expression':
        expr = item.get('expression', {})
        for r in expr.get('references', []) or []:
            fix_reference(r)

def main():
    base = Path(__file__).parent
    targets = sorted(base.glob("*.expected.json"))
    for f in targets:
        with open(f) as fh:
            data = json.load(fh)
        before = json.dumps(data, sort_keys=True)
        for item in data.get('items', []) or []:
            fix_item(item)
        after = json.dumps(data, sort_keys=True)
        if before != after:
            with open(f, 'w') as fh:
                json.dump(data, fh, indent=2)
                fh.write('\n')
            print(f"FIXED: {f.name}")
        else:
            print(f"unchanged: {f.name}")

if __name__ == '__main__':
    main()
