"""Hermetic tests for response-parsing.

These exercise the JSON-extraction fallback chain that has to survive
fenced blocks, bare blocks, trailing prose, and adversarial output.
"""

from __future__ import annotations

import unittest

from interpret.parsing import extract_json_block, parse_interpretation


class ExtractJsonBlockTests(unittest.TestCase):
    def test_extracts_fenced_block(self) -> None:
        text = (
            "Some prose explaining the call.\n\n"
            "```json\n"
            '{"interpretation": "X", "justification": "Y", "confidence": 0.9}\n'
            "```\n"
        )
        self.assertEqual(
            extract_json_block(text),
            {"interpretation": "X", "justification": "Y", "confidence": 0.9},
        )

    def test_extracts_bare_block_at_end_of_response(self) -> None:
        text = (
            "Reasoning prose. "
            '{"interpretation":"X","justification":"Y","confidence":0.5}'
        )
        result = extract_json_block(text)
        self.assertIsNotNone(result)
        self.assertEqual(result["interpretation"], "X")

    def test_returns_none_when_no_json_present(self) -> None:
        self.assertIsNone(extract_json_block("Just prose, no JSON here."))

    def test_picks_last_block_when_multiple_present(self) -> None:
        # Auto-verification "merge" mode appends a verifier JSON after
        # the model's. The model's clean fenced block must still win
        # because we try fenced first.
        text = (
            "```json\n"
            '{"interpretation":"model","justification":"j","confidence":0.8}\n'
            "```\n\n"
            "Verification (verifier model): "
            '{"verdict":"VERIFIED","issues":[]}\n'
        )
        result = extract_json_block(text)
        self.assertEqual(result["interpretation"], "model")

    def test_normalises_keys_to_lowercase(self) -> None:
        text = '{"Interpretation":"X","Justification":"Y","Confidence":0.5}'
        result = extract_json_block(text)
        self.assertEqual(result["interpretation"], "X")


class ParseInterpretationTests(unittest.TestCase):
    def test_returns_full_interpretation(self) -> None:
        text = '```json\n{"interpretation":"X","justification":"Y","confidence":0.85}\n```'
        result = parse_interpretation(text)
        self.assertIsNotNone(result)
        self.assertEqual(result.interpretation, "X")
        self.assertEqual(result.justification, "Y")
        self.assertEqual(result.confidence, 0.85)

    def test_clamps_out_of_range_confidence(self) -> None:
        text = '{"interpretation":"X","justification":"","confidence":1.5}'
        result = parse_interpretation(text)
        self.assertEqual(result.confidence, 1.0)
        text2 = '{"interpretation":"X","justification":"","confidence":-0.2}'
        self.assertEqual(parse_interpretation(text2).confidence, 0.0)

    def test_string_confidence_falls_back_to_default(self) -> None:
        # Some models emit "high" instead of a number — we treat that
        # as unparseable and default to 0.5 (medium uncertainty).
        text = '{"interpretation":"X","justification":"","confidence":"high"}'
        self.assertEqual(parse_interpretation(text).confidence, 0.5)

    def test_missing_interpretation_is_failure(self) -> None:
        text = '{"justification":"Y","confidence":0.7}'
        self.assertIsNone(parse_interpretation(text))

    def test_empty_interpretation_is_failure(self) -> None:
        text = '{"interpretation":"","justification":"Y","confidence":0.7}'
        self.assertIsNone(parse_interpretation(text))


if __name__ == "__main__":
    unittest.main()
