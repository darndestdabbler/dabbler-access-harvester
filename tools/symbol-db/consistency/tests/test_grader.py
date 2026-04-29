"""Hermetic tests for the grader prompt builder + parser.

Run with::

    cd tools/symbol-db
    ../../.venv/Scripts/python.exe -m unittest discover -s consistency/tests
"""

from __future__ import annotations

import sqlite3
import unittest

from consistency.grader import (
    DISAGREEMENT_TYPES,
    InterpretationTriplet,
    JUDGES,
    PAIRS,
    anonymize,
    build_prompt,
    canonical_pair,
    load_triplet,
    make_anonymization_seed,
    map_letter_pair_to_canonical,
    open_grades_db,
    parse_response,
    write_grades,
    write_failure,
    already_graded,
)


def _trip() -> InterpretationTriplet:
    # Payload strings are deliberately provider-name-neutral so the
    # leakage assertion below catches anonymization-metadata leaks from
    # the prompt template, not the test's own mock content.
    return InterpretationTriplet(
        fqn="ModFoo.bar", table="Symbols", stratum="sub", kind="sub",
        interpretation_1="Opinion one: foo computes the running total.",
        justification_1="Cites source span 12-30 and callee qux.",
        confidence_1=0.92,
        interpretation_2="Opinion two: foo accumulates per-row deltas.",
        justification_2="Cites the same callee but adds caller modBaz.zog.",
        confidence_2=0.81,
        interpretation_3="Opinion three: foo is a tally helper for the report.",
        justification_3="Cites caller modBaz.zog, same span.",
        confidence_3=0.74,
    )


class TestAnonymization(unittest.TestCase):
    def test_seed_is_deterministic(self):
        s1 = make_anonymization_seed("ModFoo.bar", "opus")
        s2 = make_anonymization_seed("ModFoo.bar", "opus")
        self.assertEqual(s1, s2)
        s3 = make_anonymization_seed("ModFoo.bar", "gemini-pro")
        self.assertNotEqual(s1, s3)

    def test_anonymize_uses_all_three_opinions(self):
        seed = make_anonymization_seed("X", "opus")
        mapping, payload = anonymize(_trip(), seed)
        self.assertEqual(set(mapping.keys()), {"A", "B", "C"})
        self.assertEqual(set(mapping.values()), {1, 2, 3})
        for letter in "ABC":
            op = mapping[letter]
            self.assertEqual(
                payload[letter]["interpretation"],
                getattr(_trip(), f"interpretation_{op}"),
            )

    def test_canonical_pair_orders_low_high(self):
        self.assertEqual(canonical_pair(2, 3), "2-3")
        self.assertEqual(canonical_pair(3, 1), "1-3")
        self.assertEqual(canonical_pair(2, 1), "1-2")

    def test_letter_pair_to_canonical_round_trip(self):
        # Force a known mapping by patching seed result.
        mapping = {"A": 3, "B": 1, "C": 2}
        self.assertEqual(map_letter_pair_to_canonical("AB", mapping), "1-3")
        self.assertEqual(map_letter_pair_to_canonical("BC", mapping), "1-2")
        self.assertEqual(map_letter_pair_to_canonical("AC", mapping), "2-3")


class TestPromptShape(unittest.TestCase):
    def test_prompt_contains_three_letters_only(self):
        seed = make_anonymization_seed("ModFoo.bar", "opus")
        _mapping, payload = anonymize(_trip(), seed)
        prompt = build_prompt(_trip(), payload)
        self.assertIn("Interpretation A", prompt)
        self.assertIn("Interpretation B", prompt)
        self.assertIn("Interpretation C", prompt)
        # Must not leak provider names anywhere visible to the judge.
        self.assertNotIn("gemini-pro", prompt)
        self.assertNotIn("opus", prompt)
        self.assertNotIn("gpt-5-4", prompt)
        self.assertNotIn("Anthropic", prompt)
        self.assertNotIn("OpenAI", prompt)
        self.assertNotIn("Google", prompt)

    def test_prompt_includes_all_three_payloads(self):
        seed = make_anonymization_seed("ModFoo.bar", "opus")
        _mapping, payload = anonymize(_trip(), seed)
        prompt = build_prompt(_trip(), payload)
        for letter in "ABC":
            self.assertIn(payload[letter]["interpretation"], prompt)
            self.assertIn(payload[letter]["justification"], prompt)

    def test_prompt_demands_required_json(self):
        seed = make_anonymization_seed("ModFoo.bar", "opus")
        _mapping, payload = anonymize(_trip(), seed)
        prompt = build_prompt(_trip(), payload)
        self.assertIn('"AB"', prompt)
        self.assertIn('"BC"', prompt)
        self.assertIn('"AC"', prompt)
        self.assertIn("required", prompt.lower())


class TestParser(unittest.TestCase):
    def test_parses_well_formed_response(self):
        text = """\
prose paragraph here

```json
{
  "AB": {"interpretation_agreement_pct": 95, "justification_agreement_pct": 90, "disagreement_type": "convergent", "summary": "match"},
  "BC": {"interpretation_agreement_pct": 60, "justification_agreement_pct": 50, "disagreement_type": "scope", "summary": "B narrower"},
  "AC": {"interpretation_agreement_pct": 70, "justification_agreement_pct": 65, "disagreement_type": "framing", "summary": "different vocab"}
}
```
"""
        out = parse_response(text)
        self.assertIsNotNone(out)
        self.assertEqual(out.pair_AB.interpretation_pct, 95)
        self.assertEqual(out.pair_BC.disagreement_type, "scope")
        self.assertEqual(out.pair_AC.summary, "different vocab")

    def test_rejects_missing_pair(self):
        text = """\
```json
{
  "AB": {"interpretation_agreement_pct": 95, "justification_agreement_pct": 90, "disagreement_type": "convergent", "summary": "x"},
  "BC": {"interpretation_agreement_pct": 60, "justification_agreement_pct": 50, "disagreement_type": "scope", "summary": "y"}
}
```
"""
        self.assertIsNone(parse_response(text))

    def test_rejects_out_of_range_pct(self):
        text = """\
```json
{
  "AB": {"interpretation_agreement_pct": 150, "justification_agreement_pct": 90, "disagreement_type": "convergent", "summary": "x"},
  "BC": {"interpretation_agreement_pct": 60, "justification_agreement_pct": 50, "disagreement_type": "scope", "summary": "y"},
  "AC": {"interpretation_agreement_pct": 70, "justification_agreement_pct": 65, "disagreement_type": "framing", "summary": "z"}
}
```
"""
        self.assertIsNone(parse_response(text))

    def test_rejects_unknown_disagreement_type(self):
        text = """\
```json
{
  "AB": {"interpretation_agreement_pct": 95, "justification_agreement_pct": 90, "disagreement_type": "INVALID", "summary": "x"},
  "BC": {"interpretation_agreement_pct": 60, "justification_agreement_pct": 50, "disagreement_type": "scope", "summary": "y"},
  "AC": {"interpretation_agreement_pct": 70, "justification_agreement_pct": 65, "disagreement_type": "framing", "summary": "z"}
}
```
"""
        self.assertIsNone(parse_response(text))

    def test_returns_none_on_empty_or_unparseable(self):
        self.assertIsNone(parse_response(""))
        self.assertIsNone(parse_response("no json here at all"))


class TestPersistenceRoundTrip(unittest.TestCase):
    def test_idempotent_write_and_already_graded(self):
        con = open_grades_db(":memory:")
        item = {"fqn": "ModFoo.bar", "table": "Symbols", "stratum": "sub"}
        # Build a minimal GradeResponse-ish object via parse_response.
        text = """\
```json
{
  "AB": {"interpretation_agreement_pct": 95, "justification_agreement_pct": 90, "disagreement_type": "convergent", "summary": "x"},
  "BC": {"interpretation_agreement_pct": 60, "justification_agreement_pct": 50, "disagreement_type": "scope", "summary": "y"},
  "AC": {"interpretation_agreement_pct": 70, "justification_agreement_pct": 65, "disagreement_type": "framing", "summary": "z"}
}
```
"""
        response = parse_response(text)
        mapping = {"A": 1, "B": 2, "C": 3}
        write_grades(con, item, "opus", mapping, response)
        con.commit()
        self.assertTrue(already_graded(con, item["fqn"], "opus"))

        # Repeat write - should INSERT OR REPLACE without exploding.
        write_grades(con, item, "opus", mapping, response)
        con.commit()
        self.assertEqual(
            con.execute("SELECT COUNT(*) FROM Grades WHERE judge_provider='opus'")
            .fetchone()[0],
            3,
        )

    def test_already_graded_false_with_fewer_than_three_pairs(self):
        con = open_grades_db(":memory:")
        con.execute(
            """
            INSERT INTO Grades
                (item_fqn, item_table, item_stratum, judge_provider, pair,
                 interpretation_agreement_pct, justification_agreement_pct,
                 disagreement_type, summary, created_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            ("X", "Symbols", "sub", "opus", "1-2", 95, 90, "convergent", "x", "now"),
        )
        con.commit()
        self.assertFalse(already_graded(con, "X", "opus"))

    def test_failure_row_is_recorded(self):
        con = open_grades_db(":memory:")
        item = {"fqn": "X", "table": "Symbols", "stratum": "sub"}
        write_failure(con, item, "opus", "parse_failure", raw="<garbled>")
        con.commit()
        row = con.execute(
            "SELECT reason, raw_response FROM GradeFailures WHERE item_fqn='X'"
        ).fetchone()
        self.assertIsNotNone(row)
        self.assertEqual(row["reason"], "parse_failure")


class TestLoadTripletAgainstStubDb(unittest.TestCase):
    def test_load_triplet_from_in_memory_symbols(self):
        con = sqlite3.connect(":memory:")
        con.row_factory = sqlite3.Row
        con.executescript("""
            CREATE TABLE Symbols (
                fqn TEXT PRIMARY KEY, kind TEXT,
                Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL,
                Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL,
                Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL
            );
        """)
        con.execute(
            """INSERT INTO Symbols VALUES
               ('ModFoo.bar','sub','i1','j1',0.9,'i2','j2',0.8,'i3','j3',0.7)""",
        )
        item = {"fqn": "ModFoo.bar", "table": "Symbols", "stratum": "sub"}
        trip = load_triplet(con, item)
        self.assertEqual(trip.kind, "sub")
        self.assertEqual(trip.interpretation_2, "i2")
        self.assertAlmostEqual(trip.confidence_3, 0.7)

    def test_load_triplet_raises_when_a_slot_is_null(self):
        con = sqlite3.connect(":memory:")
        con.row_factory = sqlite3.Row
        con.executescript("""
            CREATE TABLE Symbols (
                fqn TEXT PRIMARY KEY, kind TEXT,
                Interpretation1 TEXT, Justification1 TEXT, Confidence1 REAL,
                Interpretation2 TEXT, Justification2 TEXT, Confidence2 REAL,
                Interpretation3 TEXT, Justification3 TEXT, Confidence3 REAL
            );
        """)
        con.execute(
            """INSERT INTO Symbols VALUES
               ('M.x','sub','i1','j1',0.9,NULL,'j2',0.8,'i3','j3',0.7)""",
        )
        item = {"fqn": "M.x", "table": "Symbols", "stratum": "sub"}
        with self.assertRaises(ValueError):
            load_triplet(con, item)


class TestRetryFailureLogging(unittest.TestCase):
    """When a non-transient error fires on the parse-failure retry, the
    grader must log a provider_error row before re-raising so the audit
    trail isn't silently lost."""

    def test_retry_non_transient_error_persists_failure(self):
        from consistency import grader as grader_mod
        con = open_grades_db(":memory:")

        # Mock symbol-db loader: return a triplet so build phase succeeds.
        class _StubResult:
            def __init__(self, content, cost=0.001, model_id="stub"):
                self.content = content
                self.cost_usd = cost
                self.model_id = model_id
                self.model_name = model_id

        # First call returns garbled content (parse fails) so the retry
        # branch fires; second call raises a non-transient error.
        call_count = {"n": 0}
        def query_fn(model, prompt, **_kwargs):
            call_count["n"] += 1
            if call_count["n"] == 1:
                return _StubResult("no JSON here at all")
            raise RuntimeError("hard provider error - 400 Bad Request")

        # Stub load_triplet so we don't need a symbol-db.
        original = grader_mod.load_triplet
        grader_mod.load_triplet = lambda _con, _item: _trip()
        try:
            item = {"fqn": "ModFoo.bar", "table": "Symbols", "stratum": "sub"}
            with self.assertRaises(RuntimeError):
                grader_mod.grade_one(con, sqlite3.connect(":memory:"),
                                     item, "opus", query_fn)
            # The failure row should still be present even though we re-raised.
            row = con.execute(
                "SELECT reason, raw_response FROM GradeFailures "
                "WHERE item_fqn=? AND judge_provider='opus'",
                (item["fqn"],),
            ).fetchone()
            self.assertIsNotNone(row)
            self.assertEqual(row["reason"], "provider_error")
        finally:
            grader_mod.load_triplet = original


class TestConstants(unittest.TestCase):
    def test_judges_and_pairs_have_expected_shape(self):
        self.assertEqual(JUDGES, ("gemini-pro", "opus", "gpt-5-4"))
        self.assertEqual(PAIRS, ("1-2", "2-3", "1-3"))
        self.assertEqual(set(DISAGREEMENT_TYPES),
                         {"factual", "scope", "framing", "convergent"})


if __name__ == "__main__":
    unittest.main()
