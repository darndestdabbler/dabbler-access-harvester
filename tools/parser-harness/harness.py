#!/usr/bin/env python3
"""
Parser Verification Harness — tools/parser-harness/harness.py

Usage:
  harness.py run   --corpus <path> [--parser <exe>] [--output <json>] [--verbose]
  harness.py diff  <baseline_json> <current_json>
  harness.py perf  --corpus <path> [--parser <exe>] [--runs <N>]

Parser interface contract:
  The harness invokes the parser as a subprocess:
    <parser_exe> <source_file>
  The parser must write a JSON object to stdout:
    {
      "status": "parsed" | "failed",
      "errors": [{"line": N, "column": N, "message": "..."}],
      "tree_stats": {"node_count": N, "max_depth": N, "rule_counts": {...}} | null
    }
  Non-zero exit code is treated as "failed" even without errors in stdout.

Corpus convention:
  Source files: NN-name.{bas|cls|sql|txt}  (matched by CORPUS_EXTENSIONS)
  Expected files: NN-name.expected.json     (not parsed by the harness itself)
"""
import argparse
import json
import os
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

HARNESS_VERSION = "1.0.0"
CORPUS_EXTENSIONS = {".bas", ".cls", ".sql", ".txt"}
SCHEMA_PATH = Path(__file__).parent / "harness-output.schema.json"


# ─── Corpus discovery ─────────────────────────────────────────────────────────

def discover_corpus(corpus_path: str) -> list[Path]:
    """Return source files in corpus_path sorted by name, excluding .expected.json."""
    root = Path(corpus_path)
    if not root.is_dir():
        print(f"ERROR: corpus path is not a directory: {corpus_path}", file=sys.stderr)
        sys.exit(2)
    files = sorted(
        p for p in root.iterdir()
        if p.is_file()
        and p.suffix.lower() in CORPUS_EXTENSIONS
        and not p.name.endswith(".expected.json")
    )
    return files


# ─── Parser invocation ────────────────────────────────────────────────────────

def invoke_parser(parser_exe: str, source_file: Path) -> tuple[str, float, list[dict], dict | None]:
    """
    Invoke parser_exe against source_file.
    Returns (status, parse_time_ms, errors, tree_stats).
    """
    start = time.perf_counter()
    try:
        proc = subprocess.run(
            [parser_exe, str(source_file)],
            capture_output=True,
            text=True,
            timeout=60,
        )
        elapsed_ms = (time.perf_counter() - start) * 1000
        if not proc.stdout.strip():
            return "failed", elapsed_ms, [{"line": None, "column": None, "message": f"no output from parser (exit code {proc.returncode})"}], None
        try:
            data = json.loads(proc.stdout)
        except json.JSONDecodeError as exc:
            return "failed", elapsed_ms, [{"line": None, "column": None, "message": f"invalid JSON from parser: {exc}"}], None
        status = data.get("status", "failed")
        errors = list(data.get("errors", []))
        # Non-zero exit always means failure, even if the parser reported "parsed".
        if proc.returncode != 0:
            status = "failed"
            errors.append({"line": None, "column": None, "message": f"parser exit code {proc.returncode}"})
        tree_stats = data.get("tree_stats", None)
        return status, elapsed_ms, errors, tree_stats
    except subprocess.TimeoutExpired:
        elapsed_ms = (time.perf_counter() - start) * 1000
        return "failed", elapsed_ms, [{"line": None, "column": None, "message": "parser timed out (>60s)"}], None
    except FileNotFoundError:
        elapsed_ms = (time.perf_counter() - start) * 1000
        return "failed", elapsed_ms, [{"line": None, "column": None, "message": f"parser not found: {parser_exe}"}], None


# ─── run subcommand ───────────────────────────────────────────────────────────

def cmd_run(args: argparse.Namespace) -> int:
    files = discover_corpus(args.corpus)
    corpus_rel = str(Path(args.corpus))

    file_entries: list[dict] = []
    counts = {"parsed": 0, "failed": 0, "skipped": 0, "no_parser": 0}

    for f in files:
        rel = str(f.relative_to(Path(args.corpus)))
        if args.parser:
            status, parse_time_ms, errors, tree_stats = invoke_parser(args.parser, f)
            counts[status] = counts.get(status, 0) + 1
            entry = {
                "name": f.name,
                "path": rel,
                "status": status,
                "parse_time_ms": round(parse_time_ms, 2),
                "errors": errors,
                "tree_stats": tree_stats,
            }
        else:
            counts["no_parser"] += 1
            entry = {
                "name": f.name,
                "path": rel,
                "status": "no_parser",
                "parse_time_ms": None,
                "errors": [],
                "tree_stats": None,
            }
        file_entries.append(entry)
        if args.verbose:
            _print_file_line(entry)

    total = len(files)
    attempted = counts["parsed"] + counts["failed"]
    pass_rate = (counts["parsed"] / attempted) if attempted > 0 else None

    output = {
        "meta": {
            "harness_version": HARNESS_VERSION,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "corpus_path": corpus_rel,
            "parser": args.parser,
        },
        "summary": {
            "total": total,
            "parsed": counts["parsed"],
            "failed": counts["failed"],
            "skipped": counts["skipped"],
            "no_parser": counts["no_parser"],
            "pass_rate": round(pass_rate, 4) if pass_rate is not None else None,
        },
        "files": file_entries,
    }

    if args.output:
        with open(args.output, "w", encoding="utf-8") as fh:
            json.dump(output, fh, indent=2)

    _print_run_summary(output)

    if counts["failed"] > 0:
        return 1
    if counts["no_parser"] == total:
        return 0  # smoke-test state; not a failure
    return 0


def _print_file_line(entry: dict) -> None:
    sym = {"parsed": "OK", "failed": "FAIL", "skipped": "--", "no_parser": "  "}.get(entry["status"], "?")
    t = f"{entry['parse_time_ms']:.1f}ms" if entry["parse_time_ms"] is not None else "-"
    print(f"  {sym}  {entry['name']:<50} {t}")
    for err in entry.get("errors", []):
        loc = f"{err.get('line','?')}:{err.get('column','?')}" if err.get("line") else "-"
        print(f"      [{loc}] {err['message']}")


def _print_run_summary(output: dict) -> None:
    s = output["summary"]
    parser_label = output["meta"]["parser"] or "(no parser)"
    pr = f"{s['pass_rate']:.1%}" if s["pass_rate"] is not None else "n/a"
    print()
    print("Parser Harness - Run Summary")
    print(f"  Corpus  : {output['meta']['corpus_path']}")
    print(f"  Parser  : {parser_label}")
    print(f"  Files   : {s['total']} discovered")
    if s["no_parser"] == s["total"]:
        print(f"  Result  : no parser configured - {s['total']}/{s['total']} files in no_parser state")
    else:
        print(f"  Parsed  : {s['parsed']}/{s['parsed'] + s['failed']}  ({pr})")
        if s["failed"]:
            print(f"  FAILED  : {s['failed']} file(s)")
    print()


# ─── diff subcommand ──────────────────────────────────────────────────────────

def cmd_diff(args: argparse.Namespace) -> int:
    try:
        with open(args.baseline) as f:
            baseline = json.load(f)
        with open(args.current) as f:
            current = json.load(f)
    except (FileNotFoundError, json.JSONDecodeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    base_map = {e["path"]: e for e in baseline.get("files", [])}
    curr_map = {e["path"]: e for e in current.get("files", [])}

    regressions = []
    improvements = []
    new_files = []
    removed_files = []

    for path, curr in curr_map.items():
        display = curr.get("name", path)
        if path not in base_map:
            new_files.append(display)
            continue
        base = base_map[path]
        if base["status"] == "parsed" and curr["status"] == "failed":
            regressions.append({"name": display, "was": base["status"], "now": curr["status"], "errors": curr["errors"]})
        elif base["status"] == "failed" and curr["status"] == "parsed":
            improvements.append({"name": display, "was": base["status"], "now": curr["status"]})
        # Note: failed→failed with different errors is NOT automatically a regression;
        # parser diagnostics evolve legitimately. Only status transitions (parsed→failed) count.

    for path in base_map:
        if path not in curr_map:
            removed_files.append(base_map[path].get("name", path))

    print()
    print("Parser Harness - Diff")
    print(f"  Baseline : {args.baseline}")
    print(f"  Current  : {args.current}")
    print()

    if regressions:
        print(f"  REGRESSIONS ({len(regressions)}):")
        for r in regressions:
            print(f"    FAIL  {r['name']}  [{r['was']} -> {r['now']}]")
            for err in r.get("errors", [])[:3]:
                loc = f"{err.get('line','?')}:{err.get('column','?')}" if err.get("line") else "-"
                print(f"        [{loc}] {err['message']}")
    else:
        print("  No regressions.")

    if improvements:
        print(f"  Improvements ({len(improvements)}):")
        for i in improvements:
            print(f"    OK    {i['name']}  [{i['was']} -> {i['now']}]")

    if new_files:
        print(f"  New files ({len(new_files)}): {', '.join(new_files)}")
    if removed_files:
        print(f"  Removed files ({len(removed_files)}): {', '.join(removed_files)}")

    print()
    return 1 if regressions else 0


# ─── perf subcommand ──────────────────────────────────────────────────────────

def cmd_perf(args: argparse.Namespace) -> int:
    if not args.parser:
        print("ERROR: --parser is required for perf mode", file=sys.stderr)
        return 2

    files = discover_corpus(args.corpus)
    runs = args.runs

    print()
    print(f"Parser Harness - Perf  ({runs} run(s) per file)")
    print(f"  Corpus : {args.corpus}")
    print(f"  Parser : {args.parser}")
    print()

    all_times: list[float] = []
    file_metrics: list[dict] = []
    print(f"  {'File':<50} {'Min ms':>8} {'Max ms':>8} {'Avg ms':>8}")
    print("  " + "-" * 78)
    for f in files:
        rel = str(f.relative_to(Path(args.corpus)))
        times = []
        for _ in range(runs):
            status, ms, _, _ = invoke_parser(args.parser, f)
            if status != "skipped":
                times.append(ms)
        if times:
            all_times.extend(times)
            avg = sum(times) / len(times)
            file_metrics.append({"name": f.name, "path": rel, "runs": runs,
                                  "min_ms": round(min(times), 2),
                                  "max_ms": round(max(times), 2),
                                  "avg_ms": round(avg, 2)})
            print(f"  {f.name:<50} {min(times):>8.1f} {max(times):>8.1f} {avg:>8.1f}")
        else:
            file_metrics.append({"name": f.name, "path": rel, "runs": 0,
                                  "min_ms": None, "max_ms": None, "avg_ms": None})
            print(f"  {f.name:<50} {'n/a':>8} {'n/a':>8} {'n/a':>8}")

    total_ms = (sum(all_times) / runs) if all_times else None
    throughput = (len(files) / (total_ms / 1000)) if total_ms else None

    if total_ms is not None:
        print()
        print(f"  Total corpus time (avg): {total_ms:.1f} ms")
        print(f"  Throughput             : {throughput:.1f} files/sec")
    print()

    if args.output:
        perf_out = {
            "meta": {
                "harness_version": HARNESS_VERSION,
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "corpus_path": str(Path(args.corpus)),
                "parser": args.parser,
                "runs_per_file": runs,
            },
            "summary": {
                "file_count": len(files),
                "total_corpus_avg_ms": round(total_ms, 2) if total_ms is not None else None,
                "throughput_files_per_sec": round(throughput, 2) if throughput is not None else None,
            },
            "files": file_metrics,
        }
        with open(args.output, "w", encoding="utf-8") as fh:
            json.dump(perf_out, fh, indent=2)

    return 0


# ─── Main ─────────────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser(
        prog="harness.py",
        description="Parser verification harness for the Dabbler Access Harvester.",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_run = sub.add_parser("run", help="Run parser against corpus.")
    p_run.add_argument("--corpus", required=True, help="Path to corpus directory.")
    p_run.add_argument("--parser", default=None, help="Path to parser executable. Omit for smoke-test (no parser).")
    p_run.add_argument("--output", default=None, help="Write JSON output to this file.")
    p_run.add_argument("--verbose", action="store_true", help="Print per-file results.")

    p_diff = sub.add_parser("diff", help="Compare two run outputs and report regressions.")
    p_diff.add_argument("baseline", help="Baseline JSON output file.")
    p_diff.add_argument("current", help="Current JSON output file to compare.")

    p_perf = sub.add_parser("perf", help="Measure parse performance.")
    p_perf.add_argument("--corpus", required=True, help="Path to corpus directory.")
    p_perf.add_argument("--parser", default=None, help="Path to parser executable.")
    p_perf.add_argument("--runs", type=int, default=3, help="Number of runs per file (default: 3).")
    p_perf.add_argument("--output", default=None, help="Write machine-readable perf JSON to this file.")

    args = parser.parse_args()

    if args.command == "run":
        return cmd_run(args)
    elif args.command == "diff":
        return cmd_diff(args)
    elif args.command == "perf":
        return cmd_perf(args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
