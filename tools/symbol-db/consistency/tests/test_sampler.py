"""Hermetic tests for the stratified sampler.

Run with::

    cd tools/symbol-db
    ../../.venv/Scripts/python.exe -m unittest discover -s consistency/tests
"""

from __future__ import annotations

import json
import os
import sqlite3
import tempfile
import unittest

from consistency.sampler import (
    POLICY,
    _fetch_stratum,
    draw_sample,
    load_sample,
    save_sample,
)


def _make_minimal_db(path: str) -> None:
    """Create a minimal in-memory fixture with enough rows to test the sampler."""
    con = sqlite3.connect(path)

    # Minimal schema subset.
    con.executescript("""
        CREATE TABLE Modules (
            fqn TEXT PRIMARY KEY,
            kind TEXT NOT NULL,
            source_path TEXT NOT NULL,
            encoding TEXT NOT NULL,
            is_deferred INTEGER NOT NULL DEFAULT 0,
            deferred_reason TEXT,
            deferred_companion_fqn TEXT
        );

        CREATE TABLE Symbols (
            fqn TEXT PRIMARY KEY,
            module_fqn TEXT NOT NULL,
            name TEXT NOT NULL,
            kind TEXT NOT NULL,
            access TEXT,
            data_type TEXT,
            declared_in_fqn TEXT,
            source_body TEXT,
            signature_string TEXT,
            body_hash_md5 TEXT,
            is_dead INTEGER NOT NULL DEFAULT 0,
            dead_code_reason TEXT,
            equivalence_canonical_fqn TEXT,
            Interpretation1 TEXT,
            Justification1 TEXT,
            Confidence1 REAL,
            Agent1 TEXT,
            InterpretedAt1 TEXT,
            Interpretation2 TEXT,
            Justification2 TEXT,
            Confidence2 REAL,
            Agent2 TEXT,
            InterpretedAt2 TEXT,
            Interpretation3 TEXT,
            Justification3 TEXT,
            Confidence3 REAL,
            Agent3 TEXT,
            InterpretedAt3 TEXT,
            human_review_required INTEGER NOT NULL DEFAULT 0,
            human_override_interpretation TEXT
        );

        CREATE TABLE Macros (
            fqn TEXT PRIMARY KEY,
            source_xml TEXT NOT NULL,
            is_dead INTEGER NOT NULL DEFAULT 0,
            Interpretation1 TEXT,
            Justification1 TEXT,
            Confidence1 REAL,
            Agent1 TEXT,
            InterpretedAt1 TEXT,
            Interpretation2 TEXT,
            Justification2 TEXT,
            Confidence2 REAL,
            Agent2 TEXT,
            InterpretedAt2 TEXT,
            Interpretation3 TEXT,
            Justification3 TEXT,
            Confidence3 REAL,
            Agent3 TEXT,
            InterpretedAt3 TEXT,
            human_review_required INTEGER NOT NULL DEFAULT 0,
            human_override_interpretation TEXT
        );

        CREATE TABLE Tables (
            fqn TEXT PRIMARY KEY,
            primary_key_column TEXT,
            candidate_label_column TEXT,
            is_lookup_table_score INTEGER,
            is_lookup_table_reason TEXT,
            Interpretation1 TEXT,
            Justification1 TEXT,
            Confidence1 REAL,
            Agent1 TEXT,
            InterpretedAt1 TEXT,
            Interpretation2 TEXT,
            Justification2 TEXT,
            Confidence2 REAL,
            Agent2 TEXT,
            InterpretedAt2 TEXT,
            Interpretation3 TEXT,
            Justification3 TEXT,
            Confidence3 REAL,
            Agent3 TEXT,
            InterpretedAt3 TEXT
        );

        CREATE TABLE Queries (
            fqn TEXT PRIMARY KEY,
            source_sql TEXT NOT NULL,
            normalized_sql TEXT,
            sql_text_hash TEXT,
            sql_clause_hash TEXT,
            is_dead INTEGER NOT NULL DEFAULT 0,
            equivalence_canonical_fqn TEXT,
            Interpretation1 TEXT,
            Justification1 TEXT,
            Confidence1 REAL,
            Agent1 TEXT,
            InterpretedAt1 TEXT,
            Interpretation2 TEXT,
            Justification2 TEXT,
            Confidence2 REAL,
            Agent2 TEXT,
            InterpretedAt2 TEXT,
            Interpretation3 TEXT,
            Justification3 TEXT,
            Confidence3 REAL,
            Agent3 TEXT,
            InterpretedAt3 TEXT
        );
    """)

    mod_fqn = "modTest"
    con.execute(
        "INSERT INTO Modules VALUES (?, 'Module', 'modTest.bas', 'ascii', 0, NULL, NULL)",
        (mod_fqn,),
    )

    def _sym(fqn, kind, declared_in=None, interp=None, is_dead=0):
        con.execute(
            """INSERT INTO Symbols
               (fqn, module_fqn, name, kind, access, data_type, declared_in_fqn,
                source_body, signature_string, body_hash_md5, is_dead,
                dead_code_reason, equivalence_canonical_fqn, Interpretation1)
               VALUES (?,?,?,?,NULL,NULL,?,NULL,NULL,NULL,?,NULL,NULL,?)""",
            (fqn, mod_fqn, fqn.split(".")[-1], kind, declared_in, is_dead, interp),
        )

    # 1 enum_decl
    _sym("modTest.MyEnum", "enum_decl", interp="An enum")

    # 3 functions
    for i in range(3):
        _sym(f"modTest.Func{i}", "function", interp="A function")
    # 1 dead function — should not appear
    _sym("modTest.DeadFunc", "function", is_dead=1, interp="dead")

    # 4 module_var (module-level, no declared_in_fqn)
    for i in range(4):
        _sym(f"modTest.m_var{i}", "module_var", declared_in=None, interp="A var")
    # 1 procedure-local module_var — should NOT appear
    _sym("modTest.localVar", "module_var", declared_in="modTest.Func0", interp="local")

    # 10 subs (more than the cap of 8)
    for i in range(10):
        _sym(f"modTest.Sub{i}", "sub", interp="A sub")

    # 12 enum_values (more than 8)
    for i in range(12):
        _sym(f"modTest.MyEnum.Val{i}", "enum_value", interp="A value")

    # 1 macro
    con.execute(
        "INSERT INTO Macros (fqn, source_xml, is_dead, Interpretation1) "
        "VALUES (?, '<macro/>', 0, ?)",
        ("AutoExec", "Auto-runs at startup"),
    )

    # 3 live queries
    for i in range(3):
        con.execute(
            "INSERT INTO Queries (fqn, source_sql, is_dead, Interpretation1) "
            "VALUES (?, ?, 0, ?)",
            (f"qryTest{i}", f"SELECT * FROM T{i}", f"Query {i}"),
        )
    # 1 dead query — should not appear
    con.execute(
        "INSERT INTO Queries (fqn, source_sql, is_dead, Interpretation1) "
        "VALUES ('qryDead', 'SELECT 1', 1, 'dead')"
    )

    # 10 lookup tables (score >= 70)
    for i in range(10):
        con.execute(
            "INSERT INTO Tables (fqn, is_lookup_table_score, Interpretation1) "
            "VALUES (?, 80, ?)",
            (f"tblLookup{i}", f"Lookup {i}"),
        )
    # 2 low-score tables — should not appear
    for i in range(2):
        con.execute(
            "INSERT INTO Tables (fqn, is_lookup_table_score, Interpretation1) "
            "VALUES (?, 30, ?)",
            (f"tblLowScore{i}", f"Low {i}"),
        )

    con.commit()
    con.close()


class TestFetchStratum(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False)
        self._db_path = self._tmp.name
        self._tmp.close()
        _make_minimal_db(self._db_path)
        self._con = sqlite3.connect(self._db_path)

    def tearDown(self):
        self._con.close()
        os.unlink(self._db_path)

    def test_enum_decl_all_returned(self):
        result = _fetch_stratum(self._con, "enum_decl")
        self.assertEqual(result, ["modTest.MyEnum"])

    def test_function_excludes_dead(self):
        result = _fetch_stratum(self._con, "function")
        self.assertEqual(len(result), 3)
        self.assertNotIn("modTest.DeadFunc", result)

    def test_module_var_excludes_procedure_locals(self):
        result = _fetch_stratum(self._con, "module_var")
        self.assertEqual(len(result), 4)
        self.assertNotIn("modTest.localVar", result)

    def test_sub_returns_full_population(self):
        result = _fetch_stratum(self._con, "sub")
        self.assertEqual(len(result), 10)

    def test_enum_value_returns_full_population(self):
        result = _fetch_stratum(self._con, "enum_value")
        self.assertEqual(len(result), 12)

    def test_macro_returns_all(self):
        result = _fetch_stratum(self._con, "macro")
        self.assertEqual(result, ["AutoExec"])

    def test_query_excludes_dead(self):
        result = _fetch_stratum(self._con, "query")
        self.assertEqual(len(result), 3)
        self.assertNotIn("qryDead", result)

    def test_lookup_table_excludes_low_score(self):
        result = _fetch_stratum(self._con, "lookup_table")
        self.assertEqual(len(result), 10)
        for fqn in result:
            self.assertIn("tblLookup", fqn)


class TestDrawSample(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False)
        self._db_path = self._tmp.name
        self._tmp.close()
        _make_minimal_db(self._db_path)

    def tearDown(self):
        os.unlink(self._db_path)

    def test_deterministic_with_same_seed(self):
        s1 = draw_sample(self._db_path, seed=42)
        s2 = draw_sample(self._db_path, seed=42)
        self.assertEqual([i["fqn"] for i in s1], [i["fqn"] for i in s2])

    def test_different_seeds_produce_different_large_strata(self):
        s1 = draw_sample(self._db_path, seed=1)
        s2 = draw_sample(self._db_path, seed=99)
        fqns1 = {i["fqn"] for i in s1 if i["stratum"] == "sub"}
        fqns2 = {i["fqn"] for i in s2 if i["stratum"] == "sub"}
        # Different seeds should produce different sub selections (almost certainly).
        # With 10 choose 8, P(same) = C(10,8)/C(10,8) = 1 only if seeds yield same
        # selection — extremely unlikely for seed 1 vs 99.
        self.assertNotEqual(fqns1, fqns2)

    def test_capped_strata_respect_max(self):
        items = draw_sample(self._db_path, seed=42)
        by_stratum = {}
        for item in items:
            by_stratum.setdefault(item["stratum"], []).append(item)
        for stratum, info in POLICY.items():
            cap = info["max_sample"]
            if cap is not None:
                self.assertLessEqual(
                    len(by_stratum.get(stratum, [])),
                    cap,
                    f"{stratum} sample exceeds cap {cap}",
                )

    def test_all_items_come_from_expected_table(self):
        items = draw_sample(self._db_path, seed=42)
        for item in items:
            expected_table = POLICY[item["stratum"]]["table"]
            self.assertEqual(
                item["table"],
                expected_table,
                f"fqn={item['fqn']} stratum={item['stratum']}: "
                f"expected table {expected_table}, got {item['table']}",
            )

    def test_save_and_load_round_trip(self):
        items = draw_sample(self._db_path, seed=42)
        with tempfile.NamedTemporaryFile(
            suffix=".json", delete=False, mode="w", encoding="utf-8"
        ) as f:
            tmp_path = f.name
        try:
            save_sample(items, tmp_path, seed=42, db_path=self._db_path)
            loaded = load_sample(tmp_path)
            self.assertEqual(
                [i["fqn"] for i in items],
                [i["fqn"] for i in loaded],
            )
        finally:
            os.unlink(tmp_path)

    def test_small_strata_always_take_all(self):
        """enum_decl (1), macro (1), query (3), function (3) — all <= cap."""
        items = draw_sample(self._db_path, seed=42)
        by_stratum = {}
        for item in items:
            by_stratum.setdefault(item["stratum"], []).append(item)
        # enum_decl: 1 in DB, should get 1
        self.assertEqual(len(by_stratum.get("enum_decl", [])), 1)
        # macro: 1 in DB, should get 1
        self.assertEqual(len(by_stratum.get("macro", [])), 1)
        # query: 3 in DB (all live), should get 3
        self.assertEqual(len(by_stratum.get("query", [])), 3)
        # function: 3 in DB (live), should get 3 (cap None = all)
        self.assertEqual(len(by_stratum.get("function", [])), 3)
        # module_var: 4 in DB (module-level), should get 4 (cap None = all)
        self.assertEqual(len(by_stratum.get("module_var", [])), 4)


if __name__ == "__main__":
    unittest.main()
