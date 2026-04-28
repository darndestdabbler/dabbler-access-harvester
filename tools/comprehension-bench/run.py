"""AI-comprehension bench runner.

For each entry in a curated questions.yaml:
1. Load the IR snippets named in ``supporting_ir_paths``.
2. Route ``task_type="analysis"`` with the IR + question to produce an answer.
3. Route ``task_type="analysis"`` again as a *judge* to score the answer
   against ``accepted_answers`` on a 0.0 - 1.0 scale.
4. Emit per-question records to JSONL.
5. Compute graduated-rubric pass via ``scoring.compute_pass``.

Exit code 0 when the graduated rubric passes; non-zero on any tier failure.

Usage:

    python tools/comprehension-bench/run.py \\
        --db NorthwindDevEdition \\
        --ir-root /tmp/ae-bench-out/AccessMetaData/NorthwindDevEdition \\
        --questions docs/test-cases/comprehension/NorthwindDevEdition/questions.yaml \\
        --out scores.jsonl
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

# Local import so the module can be executed as a script from anywhere.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from scoring import QuestionScore, compute_pass  # noqa: E402


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        REPO_ROOT / "ai-router" / "__init__.py",
        submodule_search_locations=[str(REPO_ROOT / "ai-router")],
    )
    module = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = module
    spec.loader.exec_module(module)
    return module


ALLOWED_DIFFICULTIES = {"low", "medium", "high"}


def load_questions(path: Path) -> list[dict]:
    """Lightweight YAML reader limited to the question-bank schema.

    Avoids a hard pyyaml dep — the schema is fixed and the inputs are
    repo-controlled. If the YAML grows complex enough to need pyyaml,
    swap this for ``yaml.safe_load`` and add the dep.
    """
    try:
        import yaml  # type: ignore
    except ImportError:
        yaml = None

    text = path.read_text(encoding="utf-8")
    if yaml is not None:
        data = yaml.safe_load(text)
        if not isinstance(data, list):
            raise ValueError(f"{path}: expected top-level list")
        return data

    raise RuntimeError(
        "pyyaml is not installed. Run `.venv/Scripts/pip install pyyaml` and retry."
    )


def validate_questions(questions: list[dict], ir_root: Path) -> list[str]:
    """Return a list of human-readable validation errors. Empty list = OK."""
    errors: list[str] = []
    seen_ids: set[str] = set()
    required_fields = ("id", "question", "accepted_answers", "supporting_ir_paths", "difficulty")
    ir_root_resolved = ir_root.resolve()
    for index, entry in enumerate(questions):
        prefix = f"questions[{index}]"
        if not isinstance(entry, dict):
            errors.append(f"{prefix}: expected mapping, got {type(entry).__name__}")
            continue
        for key in required_fields:
            if key not in entry:
                errors.append(f"{prefix}: missing required field {key!r}")
        if "id" in entry:
            qid = entry["id"]
            if not isinstance(qid, str) or not qid:
                errors.append(f"{prefix}.id: must be a non-empty string")
            elif qid in seen_ids:
                errors.append(f"{prefix}.id: duplicate {qid!r}")
            else:
                seen_ids.add(qid)
        if "question" in entry and not isinstance(entry["question"], str):
            errors.append(f"{prefix}.question: must be a string")
        if "difficulty" in entry and entry["difficulty"] not in ALLOWED_DIFFICULTIES:
            errors.append(f"{prefix}.difficulty: must be one of {sorted(ALLOWED_DIFFICULTIES)}")
        accepted = entry.get("accepted_answers")
        if not isinstance(accepted, list) or not accepted or not all(isinstance(a, str) and a for a in accepted):
            errors.append(f"{prefix}.accepted_answers: must be a non-empty list of non-empty strings")
        paths = entry.get("supporting_ir_paths")
        if not isinstance(paths, list) or not paths or not all(isinstance(p, str) and p for p in paths):
            errors.append(f"{prefix}.supporting_ir_paths: must be a non-empty list of non-empty strings")
        else:
            for rel in paths:
                # Reject absolute / drive-rooted paths up front — pathlib's
                # `ir_root / "/abs"` overrides the left operand and would
                # silently bypass the under-ir_root check below.
                if Path(rel).is_absolute() or rel.startswith(("/", "\\")):
                    errors.append(f"{prefix}.supporting_ir_paths: must be relative to --ir-root: {rel!r}")
                    continue
                full = (ir_root / rel).resolve()
                # Block traversal outside the IR root after resolving.
                try:
                    full.relative_to(ir_root_resolved)
                except ValueError:
                    errors.append(f"{prefix}.supporting_ir_paths: escapes --ir-root: {rel!r}")
                    continue
                if not full.is_file():
                    errors.append(f"{prefix}.supporting_ir_paths: not a file: {full}")
    return errors


def load_ir_snippets(ir_root: Path, paths: list[str]) -> str:
    parts: list[str] = []
    for rel in paths:
        full = ir_root / rel
        if not full.exists():
            parts.append(f"=== {rel} ===\n[FILE NOT FOUND at {full}]")
            continue
        parts.append(f"=== {rel} ===\n{full.read_text(encoding='utf-8')}")
    return "\n\n".join(parts)


def call_answer(ar, ir_snippets: str, question: str, session_set: str, session_number: int):
    return ar.route(
        content=f"=== IR snippets ===\n{ir_snippets}\n\n=== Question ===\n{question}",
        task_type="analysis",
        context=(
            "You are an AI engine reading the harvester's IR output for an Access "
            "database. Answer the question using only the IR snippets above. "
            "If the IR doesn't contain enough information to answer, say so explicitly "
            "rather than guessing."
        ),
        session_set=session_set,
        session_number=session_number,
    )


JUDGE_SCORE_RE = re.compile(r"score\s*[:=]\s*([01](?:\.\d+)?)", re.IGNORECASE)


def call_judge(ar, question: str, accepted: list[str], actual: str, session_set: str, session_number: int):
    accepted_block = "\n\n".join(f"Accepted answer {i+1}:\n{a}" for i, a in enumerate(accepted))
    return ar.route(
        content=(
            f"=== Question ===\n{question}\n\n"
            f"=== Accepted answers (any one is correct) ===\n{accepted_block}\n\n"
            f"=== Actual answer ===\n{actual}"
        ),
        task_type="analysis",
        context=(
            "Score the Actual answer against the Accepted answers on a 0.0 - 1.0 scale. "
            "Semantic equivalence to ANY accepted answer = 1.0. Partially correct = 0.5. "
            "Wrong / hallucinated = 0.0. Output a one-sentence justification, then a "
            "trailing line of the exact form `score: <float>` so a regex can extract it. "
            "Do not award points for confident-sounding wrong answers."
        ),
        session_set=session_set,
        session_number=session_number,
    )


def parse_score(judge_text: str) -> float:
    match = JUDGE_SCORE_RE.search(judge_text)
    if not match:
        return 0.0
    try:
        return max(0.0, min(1.0, float(match.group(1))))
    except ValueError:
        return 0.0


def main() -> int:
    parser = argparse.ArgumentParser(description="Run the AI-comprehension bench.")
    parser.add_argument("--db", required=True)
    parser.add_argument("--ir-root", required=True, type=Path)
    parser.add_argument("--questions", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--session-set", default="docs/session-sets/integration-testing-and-acceptance")
    parser.add_argument("--session-number", type=int, default=3)
    parser.add_argument("--limit", type=int, default=0,
                        help="Cap question count for smoke runs (0 = no cap)")
    args = parser.parse_args()

    if not args.ir_root.exists():
        sys.stderr.write(f"IR root not found: {args.ir_root}\n")
        return 2
    if not args.questions.exists():
        sys.stderr.write(f"Questions file not found: {args.questions}\n")
        return 2

    try:
        questions = load_questions(args.questions)
    except Exception as exc:
        # Catch YAML parse errors, missing-pyyaml RuntimeError, top-level-shape
        # ValueError, file I/O errors. All of these are misconfiguration, not
        # bench data — exit 2 with a concise message rather than a traceback.
        sys.stderr.write(
            f"Failed to load question bank from {args.questions}: "
            f"{type(exc).__name__}: {exc}\n"
        )
        return 2
    if args.limit > 0:
        questions = questions[: args.limit]
    if not questions:
        sys.stderr.write(
            f"No questions loaded from {args.questions} (after --limit={args.limit}). "
            "An empty question bank cannot be scored; failing fast rather than reporting "
            "a vacuous pass.\n"
        )
        return 2

    schema_errors = validate_questions(questions, args.ir_root)
    if schema_errors:
        sys.stderr.write(f"Question-bank validation failed for {args.questions}:\n")
        for err in schema_errors:
            sys.stderr.write(f"  - {err}\n")
        return 2

    ar = load_ai_router()

    args.out.parent.mkdir(parents=True, exist_ok=True)
    scores: list[QuestionScore] = []
    with args.out.open("w", encoding="utf-8") as out_fp:
        for entry in questions:
            qid = entry["id"]
            question = entry["question"]
            difficulty = entry["difficulty"]
            accepted = list(entry.get("accepted_answers") or [])
            paths = list(entry.get("supporting_ir_paths") or [])

            ir_snippets = load_ir_snippets(args.ir_root, paths)
            answer = call_answer(ar, ir_snippets, question, args.session_set, args.session_number)
            judge = call_judge(ar, question, accepted, answer.content, args.session_set, args.session_number)
            score = parse_score(judge.content)

            record = {
                "id": qid,
                "db": args.db,
                "difficulty": difficulty,
                "score": score,
                "answer_model": answer.model_name,
                "answer_cost_usd": answer.cost_usd,
                "judge_model": judge.model_name,
                "judge_cost_usd": judge.cost_usd,
                "answer_text": answer.content,
                "judge_text": judge.content,
            }
            out_fp.write(json.dumps(record, ensure_ascii=False) + "\n")
            out_fp.flush()
            scores.append(QuestionScore(id=qid, difficulty=difficulty, score=score))

    bench = compute_pass(scores)
    try:
        scores_path_display = str(args.out.relative_to(REPO_ROOT))
    except ValueError:
        # --out is outside the repo (e.g., a Windows temp path the xUnit wrapper passes).
        scores_path_display = str(args.out)
    summary = {
        "db": args.db,
        "n_questions": len(scores),
        "tiers": [
            {"difficulty": t.difficulty, "n": t.n, "mean": round(t.mean, 4),
             "threshold": t.threshold, "passed": t.passed}
            for t in bench.tiers
        ],
        "overall_passed": bench.overall_passed,
        "scores_path": scores_path_display,
    }
    print(json.dumps(summary, indent=2))
    return 0 if bench.overall_passed else 1


if __name__ == "__main__":
    sys.exit(main())
