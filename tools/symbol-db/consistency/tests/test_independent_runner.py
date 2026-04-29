"""Hermetic tests for the independent_runner.

Tests focus on:
  - Opinion-slot routing: opinion 2 uses opus, opinion 3 uses gpt-5-4.
  - Neutral-evidence prompt construction: no callee summaries, no prior
    interpretation injected.
  - Idempotency: already-written items are skipped.
  - Dry-run mode: no API calls, all items returned as 'dry_run'.
  - Provider-error handling: transient errors are recorded and run continues.

The tests do NOT make real API calls; they inject a fake ``query`` function.
"""

from __future__ import annotations

import json
import os
import sqlite3
import tempfile
import unittest
from typing import Optional
from unittest.mock import MagicMock

from consistency.independent_runner import (
    _OPINION_MODEL,
    _already_written,
    _build_neutral_prompt,
    _fqn_kind,
    _is_transient,
    _skip_reason_parse,
    _skip_reason_provider,
    _skip_reason_prompt,
    run_opinion,
)
from consistency.sampler import save_sample


# ── minimal DB fixture (reused from test_sampler) ─────────────────────────


def _make_minimal_db(path: str) -> None:
    con = sqlite3.connect(path)
    con.executescript("""
        CREATE TABLE Modules (
            fqn TEXT PRIMARY KEY, kind TEXT NOT NULL,
            source_path TEXT NOT NULL, encoding TEXT NOT NULL,
            is_deferred INTEGER NOT NULL DEFAULT 0,
            deferred_reason TEXT, deferred_companion_fqn TEXT
        );
        CREATE TABLE Symbols (
            fqn TEXT PRIMARY KEY, module_fqn TEXT NOT NULL,
            name TEXT NOT NULL, kind TEXT NOT NULL, access TEXT,
            data_type TEXT, declared_in_fqn TEXT, source_body TEXT,
            signature_string TEXT, body_hash_md5 TEXT,
            is_dead INTEGER NOT NULL DEFAULT 0, dead_code_reason TEXT,
            equivalence_canonical_fqn TEXT,
            source_line_start INTEGER, source_line_end INTEGER,
            Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL,
            Agent1 TEXT, InterpretedAt1 TEXT,
            Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL,
            Agent2 TEXT, InterpretedAt2 TEXT,
            Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL,
            Agent3 TEXT, InterpretedAt3 TEXT,
            human_review_required INTEGER NOT NULL DEFAULT 0,
            human_override_interpretation TEXT
        );
        CREATE TABLE Macros (
            fqn TEXT PRIMARY KEY, source_xml TEXT NOT NULL,
            is_dead INTEGER NOT NULL DEFAULT 0,
            Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL,
            Agent1 TEXT, InterpretedAt1 TEXT,
            Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL,
            Agent2 TEXT, InterpretedAt2 TEXT,
            Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL,
            Agent3 TEXT, InterpretedAt3 TEXT,
            human_review_required INTEGER NOT NULL DEFAULT 0,
            human_override_interpretation TEXT
        );
        CREATE TABLE Tables (
            fqn TEXT PRIMARY KEY,
            primary_key_column TEXT, candidate_label_column TEXT,
            is_lookup_table_score INTEGER, is_lookup_table_reason TEXT,
            Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL,
            Agent1 TEXT, InterpretedAt1 TEXT,
            Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL,
            Agent2 TEXT, InterpretedAt2 TEXT,
            Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL,
            Agent3 TEXT, InterpretedAt3 TEXT
        );
        CREATE TABLE Queries (
            fqn TEXT PRIMARY KEY, source_sql TEXT NOT NULL,
            normalized_sql TEXT, sql_text_hash TEXT, sql_clause_hash TEXT,
            is_dead INTEGER NOT NULL DEFAULT 0, equivalence_canonical_fqn TEXT,
            Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL,
            Agent1 TEXT, InterpretedAt1 TEXT,
            Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL,
            Agent2 TEXT, InterpretedAt2 TEXT,
            Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL,
            Agent3 TEXT, InterpretedAt3 TEXT
        );
        CREATE TABLE Parameters (
            procedure_fqn TEXT NOT NULL,
            position INTEGER NOT NULL,
            name TEXT NOT NULL,
            data_type TEXT, passing TEXT,
            is_optional INTEGER NOT NULL DEFAULT 0,
            is_paramarray INTEGER NOT NULL DEFAULT 0,
            is_array INTEGER NOT NULL DEFAULT 0,
            default_value TEXT,
            PRIMARY KEY (procedure_fqn, position)
        );
        CREATE TABLE [References] (
            id INTEGER PRIMARY KEY,
            from_fqn TEXT NOT NULL, to_fqn TEXT,
            identifier TEXT NOT NULL, kind TEXT NOT NULL,
            caller_kind TEXT, source_line INTEGER, source_column INTEGER,
            resolution_status TEXT NOT NULL, resolution_reason TEXT
        );
        CREATE TABLE EventBindings (
            form_fqn TEXT NOT NULL,
            control_name TEXT, event_name TEXT NOT NULL,
            handler_kind TEXT NOT NULL, handler_fqn TEXT,
            handler_expression TEXT,
            PRIMARY KEY (form_fqn, control_name, event_name)
        );
        CREATE TABLE FormControls (
            form_fqn TEXT NOT NULL, control_name TEXT NOT NULL,
            control_type TEXT NOT NULL, label_text TEXT,
            control_source TEXT, row_source TEXT,
            bound_table_or_query TEXT, bound_column TEXT,
            PRIMARY KEY (form_fqn, control_name)
        );
        CREATE TABLE MacroActions (
            macro_fqn TEXT NOT NULL, position INTEGER NOT NULL,
            action_name TEXT NOT NULL, arguments_json TEXT,
            PRIMARY KEY (macro_fqn, position)
        );
        CREATE TABLE BuildRuns (
            run_id INTEGER PRIMARY KEY,
            started_at TEXT NOT NULL, finished_at TEXT,
            orchestrator TEXT NOT NULL,
            session_set TEXT NOT NULL, session_number INTEGER NOT NULL,
            symbols_written INTEGER, references_written INTEGER,
            interpretations_written INTEGER, total_routed_cost_usd REAL,
            notes TEXT
        );
        CREATE TABLE InterpretationHistory (
            id INTEGER PRIMARY KEY,
            fqn TEXT NOT NULL, fqn_kind TEXT NOT NULL,
            opinion_slot INTEGER NOT NULL,
            interpretation TEXT NOT NULL, justification TEXT,
            confidence REAL, agent TEXT NOT NULL,
            prompt_hash TEXT, source_hash_at_time TEXT,
            written_at TEXT NOT NULL,
            build_run_id INTEGER, reason TEXT
        );
        CREATE TABLE EnumValues (
            enum_value_fqn TEXT PRIMARY KEY,
            enum_fqn TEXT NOT NULL, numeric_value INTEGER
        );
    """)

    mod = "modTest"
    con.execute(
        "INSERT INTO Modules VALUES (?, 'Module', 'test.bas', 'ascii', 0, NULL, NULL)",
        (mod,),
    )
    # One interpreted sub
    con.execute(
        """INSERT INTO Symbols
           (fqn, module_fqn, name, kind, access, is_dead, Interpretation1, Confidence1)
           VALUES ('modTest.MySub', ?, 'MySub', 'sub', 'Public', 0, 'does something', 0.95)""",
        (mod,),
    )
    # One interpreted macro
    con.execute(
        "INSERT INTO Macros (fqn, source_xml, is_dead, Interpretation1) "
        "VALUES ('AutoExec', '<macro/>', 0, 'startup')"
    )
    con.commit()
    con.close()


def _make_fake_result(model: str = "claude-opus-4-7", confidence: float = 0.88) -> MagicMock:
    content = (
        '{"interpretation": "does X", "justification": "because Y", '
        f'"confidence": {confidence}}}'
    )
    r = MagicMock()
    r.content = content
    r.model_id = model
    r.total_cost_usd = 0.05
    return r


class TestOpinionModelMapping(unittest.TestCase):
    def test_opinion_2_uses_opus(self):
        self.assertEqual(_OPINION_MODEL[2], "opus")

    def test_opinion_3_uses_gpt54(self):
        self.assertEqual(_OPINION_MODEL[3], "gpt-5-4")


class TestFqnKind(unittest.TestCase):
    def test_symbols_table(self):
        self.assertEqual(_fqn_kind("Symbols"), "symbol")

    def test_macros_table(self):
        self.assertEqual(_fqn_kind("Macros"), "macro")

    def test_tables_table(self):
        self.assertEqual(_fqn_kind("Tables"), "table")

    def test_queries_table(self):
        self.assertEqual(_fqn_kind("Queries"), "query")


class TestIsTransient(unittest.TestCase):
    def test_503_is_transient(self):
        self.assertTrue(_is_transient(Exception("HTTP 503")))

    def test_429_is_transient(self):
        self.assertTrue(_is_transient(Exception("rate limit 429")))

    def test_value_error_is_not_transient(self):
        self.assertFalse(_is_transient(ValueError("bad value")))


class TestSkipReasons(unittest.TestCase):
    def test_provider_reason_includes_slot(self):
        self.assertIn("2", _skip_reason_provider(2))
        self.assertIn("3", _skip_reason_provider(3))

    def test_prompt_reason_includes_slot(self):
        self.assertIn("2", _skip_reason_prompt(2))

    def test_parse_reason_includes_slot(self):
        self.assertIn("3", _skip_reason_parse(3))

    def test_reasons_are_distinct(self):
        self.assertNotEqual(_skip_reason_provider(2), _skip_reason_prompt(2))
        self.assertNotEqual(_skip_reason_provider(2), _skip_reason_parse(2))
        self.assertNotEqual(_skip_reason_prompt(2), _skip_reason_parse(2))


class TestAlreadyWritten(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False)
        self._db_path = self._tmp.name
        self._tmp.close()
        _make_minimal_db(self._db_path)
        self._con = sqlite3.connect(self._db_path)
        self._con.row_factory = sqlite3.Row

    def tearDown(self):
        self._con.close()
        os.unlink(self._db_path)

    def test_not_written_returns_false(self):
        self.assertFalse(_already_written(self._con, "modTest.MySub", 2, "Symbols"))

    def test_written_returns_true(self):
        self._con.execute(
            "UPDATE Symbols SET Interpretation2='x' WHERE fqn='modTest.MySub'"
        )
        self._con.commit()
        self.assertTrue(_already_written(self._con, "modTest.MySub", 2, "Symbols"))

    def test_macro_not_written_returns_false(self):
        self.assertFalse(_already_written(self._con, "AutoExec", 2, "Macros"))

    def test_macro_written_returns_true(self):
        self._con.execute(
            "UPDATE Macros SET Interpretation2='y' WHERE fqn='AutoExec'"
        )
        self._con.commit()
        self.assertTrue(_already_written(self._con, "AutoExec", 2, "Macros"))

    def test_unknown_fqn_returns_false(self):
        self.assertFalse(_already_written(self._con, "nonexistent.FQN", 2, "Symbols"))

    def test_table_scoping_prevents_cross_table_false_positive(self):
        """Populate InterpretationN in Symbols but NOT Macros; check is table-scoped."""
        # Use the same FQN string in both tables to simulate the collision scenario.
        shared_fqn = "sharedFQN"
        self._con.execute(
            "INSERT INTO Symbols (fqn, module_fqn, name, kind, access, is_dead, "
            "Interpretation1, Interpretation2) "
            "VALUES (?, 'modTest', 'sharedFQN', 'sub', 'Public', 0, 'i1', 'i2')",
            (shared_fqn,),
        )
        self._con.execute(
            "INSERT INTO Macros (fqn, source_xml, is_dead) VALUES (?, '<m/>', 0)",
            (shared_fqn,),
        )
        self._con.commit()

        # Written in Symbols, not written in Macros.
        self.assertTrue(_already_written(self._con, shared_fqn, 2, "Symbols"))
        self.assertFalse(_already_written(self._con, shared_fqn, 2, "Macros"))


class TestRunOpinionDryRun(unittest.TestCase):
    def setUp(self):
        self._db_tmp = tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False)
        self._db_path = self._db_tmp.name
        self._db_tmp.close()
        _make_minimal_db(self._db_path)

        self._sample_tmp = tempfile.NamedTemporaryFile(
            suffix=".json", delete=False, mode="w", encoding="utf-8"
        )
        self._sample_path = self._sample_tmp.name
        self._sample_tmp.close()
        items = [
            {"fqn": "modTest.MySub", "table": "Symbols", "stratum": "sub"},
            {"fqn": "AutoExec", "table": "Macros", "stratum": "macro"},
        ]
        save_sample(items, self._sample_path, seed=42, db_path=self._db_path)

    def tearDown(self):
        os.unlink(self._db_path)
        os.unlink(self._sample_path)

    def test_dry_run_writes_nothing(self):
        stats = run_opinion(self._db_path, self._sample_path, 2, dry_run=True)
        self.assertEqual(stats["written"], 0)
        con = sqlite3.connect(self._db_path)
        row = con.execute(
            "SELECT Interpretation2 FROM Symbols WHERE fqn='modTest.MySub'"
        ).fetchone()
        self.assertIsNone(row[0])
        con.close()

    def test_dry_run_returns_dry_run_status(self):
        stats = run_opinion(self._db_path, self._sample_path, 2, dry_run=True)
        for item in stats["per_item"]:
            self.assertEqual(item["status"], "dry_run")


class TestRunOpinionIdempotency(unittest.TestCase):
    """Already-written items are skipped on rerun."""

    def setUp(self):
        self._db_tmp = tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False)
        self._db_path = self._db_tmp.name
        self._db_tmp.close()
        _make_minimal_db(self._db_path)

        # Pre-populate Interpretation2 for MySub
        con = sqlite3.connect(self._db_path)
        con.execute(
            "UPDATE Symbols SET Interpretation2='existing', Confidence2=0.9 "
            "WHERE fqn='modTest.MySub'"
        )
        con.commit()
        con.close()

        self._sample_tmp = tempfile.NamedTemporaryFile(
            suffix=".json", delete=False, mode="w", encoding="utf-8"
        )
        self._sample_path = self._sample_tmp.name
        self._sample_tmp.close()
        items = [{"fqn": "modTest.MySub", "table": "Symbols", "stratum": "sub"}]
        save_sample(items, self._sample_path, seed=42, db_path=self._db_path)

    def tearDown(self):
        os.unlink(self._db_path)
        os.unlink(self._sample_path)

    def test_already_written_is_skipped(self):
        import consistency.independent_runner as ir
        original = ir._load_ai_router

        class _FakeAR:
            def query(self, *a, **kw):
                raise AssertionError("query() should not be called for already-written item")

        ir._load_ai_router = lambda: _FakeAR()
        try:
            stats2 = run_opinion(self._db_path, self._sample_path, 2, dry_run=False)
        finally:
            ir._load_ai_router = original

        self.assertEqual(stats2["skipped_already_written"], 1)
        self.assertEqual(stats2["written"], 0)


class TestNeutralPromptSanitization(unittest.TestCase):
    """_build_neutral_prompt must pass sanitized rows with interpretation1=None."""

    def setUp(self):
        self._tmp = tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False)
        self._db_path = self._tmp.name
        self._tmp.close()
        _make_minimal_db(self._db_path)
        self._con = sqlite3.connect(self._db_path)
        self._con.row_factory = sqlite3.Row

    def tearDown(self):
        self._con.close()
        os.unlink(self._db_path)

    def test_symbol_row_interpretation_is_cleared(self):
        """The SymbolRow passed to the prompt builder has interpretation1=None."""
        captured = []

        import consistency.independent_runner as ir
        import interpret.prompt_builder as pb
        original = pb.build_symbol_prompt

        def _capture(con, sym, **kwargs):
            captured.append(sym)
            return original(con, sym, **kwargs)

        pb.build_symbol_prompt = _capture
        try:
            _build_neutral_prompt(self._con, {"fqn": "modTest.MySub", "table": "Symbols", "stratum": "sub"})
        finally:
            pb.build_symbol_prompt = original

        self.assertEqual(len(captured), 1)
        self.assertIsNone(captured[0].interpretation1,
                         "interpretation1 must be cleared before passing to prompt builder")
        self.assertIsNone(captured[0].confidence1,
                         "confidence1 must be cleared before passing to prompt builder")

    def test_macro_row_interpretation_is_cleared(self):
        """The MacroRow passed to the prompt builder has interpretation1=None."""
        captured = []

        import interpret.prompt_builder as pb
        original = pb.build_macro_prompt

        def _capture(macro, actions):
            captured.append(macro)
            return original(macro, actions)

        pb.build_macro_prompt = _capture
        try:
            _build_neutral_prompt(self._con, {"fqn": "AutoExec", "table": "Macros", "stratum": "macro"})
        finally:
            pb.build_macro_prompt = original

        self.assertEqual(len(captured), 1)
        self.assertIsNone(captured[0].interpretation1,
                         "MacroRow.interpretation1 must be None for neutral evidence")

    def test_callee_summaries_are_empty(self):
        """build_symbol_prompt is called with callee_summaries=[]."""
        captured_kwargs = []

        import interpret.prompt_builder as pb
        original = pb.build_symbol_prompt

        def _capture(con, sym, **kwargs):
            captured_kwargs.append(kwargs)
            return original(con, sym, **kwargs)

        pb.build_symbol_prompt = _capture
        try:
            _build_neutral_prompt(self._con, {"fqn": "modTest.MySub", "table": "Symbols", "stratum": "sub"})
        finally:
            pb.build_symbol_prompt = original

        self.assertEqual(captured_kwargs[0]["callee_summaries"], [],
                         "callee_summaries must be empty for neutral evidence")
        self.assertIsNone(captured_kwargs[0]["enum_interpretation"],
                          "enum_interpretation must be None for neutral evidence")

    def test_table_row_interpretation_is_cleared(self):
        """TableRow passed to build_table_prompt has interpretation1=None even when DB has a value."""
        # Seed a non-null Interpretation1 for a lookup table.
        self._con.execute(
            "INSERT INTO Tables (fqn, is_lookup_table_score, Interpretation1) "
            "VALUES ('tblTest', 80, 'pre-existing interpretation')"
        )
        self._con.commit()

        captured = []
        import interpret.prompt_builder as pb
        original = pb.build_table_prompt

        def _capture(con, table_row):
            captured.append(table_row)
            return original(con, table_row)

        pb.build_table_prompt = _capture
        try:
            _build_neutral_prompt(self._con, {"fqn": "tblTest", "table": "Tables", "stratum": "lookup_table"})
        finally:
            pb.build_table_prompt = original

        self.assertIsNone(captured[0].interpretation1,
                          "TableRow.interpretation1 must be None for neutral evidence")

    def test_query_row_interpretation_is_cleared(self):
        """QueryRow passed to build_query_prompt has interpretation1=None even when DB has a value."""
        self._con.execute(
            "INSERT INTO Queries (fqn, source_sql, is_dead, Interpretation1) "
            "VALUES ('qryTest', 'SELECT 1', 0, 'pre-existing interpretation')"
        )
        self._con.commit()

        captured = []
        import interpret.prompt_builder as pb
        original = pb.build_query_prompt

        def _capture(con, query_row):
            captured.append(query_row)
            return original(con, query_row)

        pb.build_query_prompt = _capture
        try:
            _build_neutral_prompt(self._con, {"fqn": "qryTest", "table": "Queries", "stratum": "query"})
        finally:
            pb.build_query_prompt = original

        self.assertIsNone(captured[0].interpretation1,
                          "QueryRow.interpretation1 must be None for neutral evidence")


class TestTransientErrorHandling(unittest.TestCase):
    """Transient errors are recorded as provider-error skips and the run continues."""

    def setUp(self):
        self._db_tmp = tempfile.NamedTemporaryFile(suffix=".sqlite", delete=False)
        self._db_path = self._db_tmp.name
        self._db_tmp.close()
        _make_minimal_db(self._db_path)

        self._sample_tmp = tempfile.NamedTemporaryFile(
            suffix=".json", delete=False, mode="w", encoding="utf-8"
        )
        self._sample_path = self._sample_tmp.name
        self._sample_tmp.close()
        items = [{"fqn": "modTest.MySub", "table": "Symbols", "stratum": "sub"}]
        save_sample(items, self._sample_path, seed=42, db_path=self._db_path)

    def tearDown(self):
        os.unlink(self._db_path)
        os.unlink(self._sample_path)

    def test_transient_error_is_recorded_and_run_continues(self):
        import consistency.independent_runner as ir
        original = ir._load_ai_router

        class _FakeAR:
            def query(self, *a, **kw):
                raise Exception("HTTP 503 service unavailable")

        ir._load_ai_router = lambda: _FakeAR()
        try:
            stats = run_opinion(self._db_path, self._sample_path, 2, dry_run=False)
        finally:
            ir._load_ai_router = original

        self.assertEqual(stats["skipped_provider_error"], 1)
        self.assertEqual(stats["written"], 0)

        # Verify the skip was recorded with the correct opinion slot and reason.
        con = sqlite3.connect(self._db_path)
        row = con.execute(
            "SELECT opinion_slot, reason FROM InterpretationHistory "
            "WHERE fqn='modTest.MySub' AND opinion_slot=2"
        ).fetchone()
        con.close()
        self.assertIsNotNone(row, "Skip history row must be written")
        self.assertEqual(row[0], 2)
        self.assertIn("provider-error", row[1])

    def test_transient_error_skip_is_idempotent(self):
        """Re-running after a transient error does not duplicate the skip row."""
        import consistency.independent_runner as ir
        original = ir._load_ai_router

        class _FakeAR:
            def query(self, *a, **kw):
                raise Exception("timeout")

        ir._load_ai_router = lambda: _FakeAR()
        try:
            run_opinion(self._db_path, self._sample_path, 2, dry_run=False)
            run_opinion(self._db_path, self._sample_path, 2, dry_run=False)
        finally:
            ir._load_ai_router = original

        con = sqlite3.connect(self._db_path)
        count = con.execute(
            "SELECT COUNT(*) FROM InterpretationHistory "
            "WHERE fqn='modTest.MySub' AND opinion_slot=2 AND reason LIKE 'skipped-provider%'"
        ).fetchone()[0]
        con.close()
        self.assertEqual(count, 1, "Duplicate skip rows must not be written on rerun")

    def test_two_different_reasons_for_same_fqn_and_slot_are_both_preserved(self):
        """Dedup key includes reason: two distinct skip reasons for the same (fqn, slot) both persist."""
        from consistency.independent_runner import _write_skip_for_opinion

        con = sqlite3.connect(self._db_path)
        con.row_factory = sqlite3.Row
        run_row = con.execute(
            "INSERT INTO BuildRuns (started_at, orchestrator, session_set, session_number) "
            "VALUES ('2026-01-01T00:00:00Z', 'test', 'test', 0)"
        )
        build_run_id = run_row.lastrowid
        con.commit()

        _write_skip_for_opinion(con, "modTest.MySub", "symbol", 2, "skipped-provider-error-opinion2", build_run_id)
        _write_skip_for_opinion(con, "modTest.MySub", "symbol", 2, "skipped-parse-failure-opinion2", build_run_id)
        con.commit()

        count = con.execute(
            "SELECT COUNT(*) FROM InterpretationHistory "
            "WHERE fqn='modTest.MySub' AND opinion_slot=2"
        ).fetchone()[0]
        con.close()
        self.assertEqual(count, 2, "Two distinct reasons must each produce one row")

    def test_run_continues_past_failed_item(self):
        """A transient error on item 1 does not prevent item 2 from being processed."""
        # Sample has two items; only the second will succeed (fake query returns good JSON).
        items = [
            {"fqn": "modTest.MySub", "table": "Symbols", "stratum": "sub"},
            {"fqn": "AutoExec", "table": "Macros", "stratum": "macro"},
        ]
        import json as _json
        with tempfile.NamedTemporaryFile(
            suffix=".json", delete=False, mode="w", encoding="utf-8"
        ) as f:
            two_item_path = f.name
            _json.dump({"seed": 42, "db_path": self._db_path, "policy": {}, "total": 2, "items": items}, f)

        call_count = [0]

        import consistency.independent_runner as ir
        original = ir._load_ai_router

        class _FakeAR:
            def query(self, model, prompt, **kw):
                call_count[0] += 1
                if call_count[0] == 1:
                    raise Exception("HTTP 503 transient")
                # Second call succeeds (for the Macro item).
                return _make_fake_result()

        ir._load_ai_router = lambda: _FakeAR()
        try:
            stats = run_opinion(self._db_path, two_item_path, 2, dry_run=False)
        finally:
            ir._load_ai_router = original
            os.unlink(two_item_path)

        self.assertEqual(stats["skipped_provider_error"], 1)
        self.assertEqual(stats["written"], 1, "Item 2 should be written despite item 1 failing")


if __name__ == "__main__":
    unittest.main()
