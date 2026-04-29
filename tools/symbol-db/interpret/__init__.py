"""Symbol-DB AI interpretation pipeline.

Reads from ``tools/symbol-db/symbol-db.sqlite`` and populates the
``InterpretationN`` / ``JustificationN`` / ``ConfidenceN`` / ``AgentN`` /
``InterpretedAtN`` columns on ``Symbols`` (and the parallel columns on
``Macros``, ``Tables``, ``Queries``). Every interpretation also emits an
``InterpretationHistory`` row.

The pipeline is leaves-first within a module: callees are interpreted
before callers, so each non-leaf prompt summarises its callees rather
than re-walking source. Interpretation is confidence-thresholded:
opinion 2 fires when confidence 1 < 0.75, opinion 3 when the average
of opinions 1 and 2 < 0.70 (configurable via
``tools/symbol-db/interpretation-config.json``).

MD5-equivalent symbols copy their interpretation from the canonical
twin instead of generating new ones; deferred and dead symbols are
skipped explicitly with an ``InterpretationHistory`` audit row.
"""
