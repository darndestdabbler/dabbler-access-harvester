"""Response parsing for AI interpretation calls.

The verifier and the per-symbol interpreter both speak structured JSON
appended after a brief prose explanation. ``extract_json_block`` is
defensive against:

- backticks (``` ```json ... ``` ```) and bare ``{ ... }`` blocks
- preceding prose, trailing prose, or trailing commentary
- single-line vs. multi-line outputs
- truncated outputs (returns ``None`` rather than ``raise``)

The returned dict is normalised to lowercase keys and capped fields.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from typing import Optional


@dataclass(frozen=True)
class Interpretation:
    interpretation: str
    justification: str
    confidence: float


_FENCE_RE = re.compile(
    r"```(?:json)?\s*(\{.*?\})\s*```", re.DOTALL | re.IGNORECASE
)


def extract_json_block(text: str) -> Optional[dict]:
    """Pull a single JSON object out of a free-form response.

    Strategy:

    1. Look for a fenced ```json``` block. If present and parseable, use it.
    2. Otherwise, scan from the last ``{`` looking for a balanced object.
       (Most fragile-but-common case: the model prints prose then
       appends ``{"interpretation": ...}`` at the end.)

    Returns ``None`` if nothing parseable is found.
    """
    if not text:
        return None

    match = _FENCE_RE.search(text)
    if match:
        try:
            obj = json.loads(match.group(1))
            if isinstance(obj, dict):
                return _normalise_keys(obj)
        except json.JSONDecodeError:
            pass

    # Fall back: walk backwards from the last ``}`` and look for the
    # matching ``{``.
    last_close = text.rfind("}")
    while last_close != -1:
        depth = 0
        in_str = False
        escape = False
        start = -1
        for i in range(last_close, -1, -1):
            ch = text[i]
            if escape:
                escape = False
                continue
            if ch == "\\":
                escape = True
                continue
            if ch == '"' and not escape:
                in_str = not in_str
                continue
            if in_str:
                continue
            if ch == "}":
                depth += 1
            elif ch == "{":
                depth -= 1
                if depth == 0:
                    start = i
                    break
        if start != -1:
            candidate = text[start : last_close + 1]
            try:
                obj = json.loads(candidate)
                if isinstance(obj, dict):
                    return _normalise_keys(obj)
            except json.JSONDecodeError:
                pass
        # Try the next ``}`` to the left.
        last_close = text.rfind("}", 0, last_close)
    return None


def _normalise_keys(d: dict) -> dict:
    return {k.lower().strip(): v for k, v in d.items()}


def parse_interpretation(text: str) -> Optional[Interpretation]:
    """Parse a single-symbol interpretation response.

    The expected JSON shape is::

        {
          "interpretation": "<one paragraph>",
          "justification": "<one paragraph; cite source/callee evidence>",
          "confidence": 0.0..1.0
        }

    Returns ``None`` if the JSON block is missing or fields are
    incomplete. The runner re-prompts in that case.
    """
    obj = extract_json_block(text)
    if obj is None:
        return None
    interp = obj.get("interpretation")
    justif = obj.get("justification")
    conf = obj.get("confidence")
    if not isinstance(interp, str) or not interp.strip():
        return None
    if not isinstance(justif, str):
        justif = ""
    try:
        confval = float(conf) if conf is not None else 0.5
    except (TypeError, ValueError):
        confval = 0.5
    confval = max(0.0, min(1.0, confval))
    return Interpretation(
        interpretation=interp.strip(),
        justification=justif.strip(),
        confidence=confval,
    )
