"""Graduated-rubric pass/fail computation for the AI-comprehension bench.

Pass thresholds by difficulty:
    low    >= 0.85
    medium >= 0.65
    high   >= 0.40

A bench run passes only if the *mean* score within each populated tier
clears that tier's threshold. Tiers with no questions are skipped (not
counted as zero).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable

THRESHOLDS = {
    "low": 0.85,
    "medium": 0.65,
    "high": 0.40,
}


@dataclass(frozen=True)
class QuestionScore:
    id: str
    difficulty: str
    score: float


@dataclass
class TierResult:
    difficulty: str
    threshold: float
    n: int
    mean: float
    passed: bool


@dataclass
class BenchResult:
    tiers: list[TierResult]
    overall_passed: bool


def compute_pass(scores: Iterable[QuestionScore]) -> BenchResult:
    by_tier: dict[str, list[float]] = {k: [] for k in THRESHOLDS}
    n = 0
    for s in scores:
        n += 1
        if s.difficulty not in by_tier:
            raise ValueError(
                f"Question {s.id!r} has unknown difficulty {s.difficulty!r}; "
                f"expected one of {list(THRESHOLDS)}."
            )
        by_tier[s.difficulty].append(s.score)

    if n == 0:
        # An empty score set must not vacuously pass — that turns a bench
        # misconfiguration into a green CI signal.
        return BenchResult(tiers=[], overall_passed=False)

    tiers: list[TierResult] = []
    overall = True
    for difficulty, threshold in THRESHOLDS.items():
        bucket = by_tier[difficulty]
        if not bucket:
            continue
        mean = sum(bucket) / len(bucket)
        passed = mean >= threshold
        tiers.append(
            TierResult(
                difficulty=difficulty,
                threshold=threshold,
                n=len(bucket),
                mean=mean,
                passed=passed,
            )
        )
        overall = overall and passed

    return BenchResult(tiers=tiers, overall_passed=overall)


def diff_against_baseline(
    current: Iterable[QuestionScore],
    baseline: Iterable[QuestionScore],
) -> dict[str, float]:
    """Return per-tier mean delta (current - baseline)."""
    cur_by_tier: dict[str, list[float]] = {k: [] for k in THRESHOLDS}
    base_by_tier: dict[str, list[float]] = {k: [] for k in THRESHOLDS}
    for s in current:
        cur_by_tier.setdefault(s.difficulty, []).append(s.score)
    for s in baseline:
        base_by_tier.setdefault(s.difficulty, []).append(s.score)

    delta: dict[str, float] = {}
    for difficulty in THRESHOLDS:
        cur = cur_by_tier[difficulty]
        base = base_by_tier[difficulty]
        if not cur or not base:
            continue
        delta[difficulty] = (sum(cur) / len(cur)) - (sum(base) / len(base))
    return delta
