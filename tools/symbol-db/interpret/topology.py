"""Leaves-first topological sort with cycle handling.

Given a directed graph of caller → callee edges, return an order in which
no node appears before any of its callees, except for nodes that
participate in a multi-node cycle. Cycle members are emitted *before*
their non-cyclic callers, in deterministic FQN order, and tagged so the
runner can mark their InterpretationHistory rows accordingly.

**Self-loop heuristic.** Single-node self-loops are dropped before
SCC detection: in VBA they almost always represent the function-name
return-value pattern (``FunctionName = expr``) rather than real
recursion, and tagging hundreds of normal procedures as cyclic would
make the cycle flag useless. The trade-off is that a genuinely
self-recursive procedure also avoids the cycle tag — distinguishing
the two would require AST-level reference-kind data that is not in
the [References] table today. Multi-node SCCs (mutual recursion) are
still flagged correctly.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable


@dataclass(frozen=True)
class OrderedNode:
    fqn: str
    is_cycle_member: bool


def order_leaves_first(
    nodes: Iterable[str],
    edges: dict[str, set[str]],
) -> list[OrderedNode]:
    """Return *nodes* in leaves-first order.

    Args:
        nodes: All node FQNs to schedule.
        edges: Mapping ``from_fqn -> {to_fqn, ...}``. Edges to nodes
            outside *nodes* are ignored, as those cross-module callees
            already have interpretations or live in skipped scope.

    Cycles are handled via Tarjan's strongly-connected-components
    algorithm: each non-trivial SCC is emitted as a unit, with its
    members tagged ``is_cycle_member=True`` and ordered by FQN inside
    the component.

    **Single-node self-loops are dropped before SCC detection** rather
    than tagged as cycles. In VBA the function-name return-value
    pattern (``FunctionName = expr``) appears in the [References]
    table as a self-edge but is not real recursion. The trade-off is
    that genuine self-recursive procedures also fail to acquire the
    cycle tag — distinguishing the two requires reference-kind /
    AST data the symbol DB does not currently carry. Multi-node SCCs
    (mutual recursion) still flag correctly.
    """
    in_set = set(nodes)
    # Restrict edges to the schedulable subgraph; drop self-loops (a
    # self-reference in VBA almost always means `FunctionName = expr`,
    # the standard return-value convention, not real recursion).
    g: dict[str, list[str]] = {n: [] for n in in_set}
    for src, dsts in edges.items():
        if src not in in_set:
            continue
        for dst in dsts:
            if dst in in_set and dst != src:
                g[src].append(dst)

    sccs = _tarjan_sccs(g)
    out: list[OrderedNode] = []
    for component in sccs:
        # Only multi-node SCCs are tagged cyclic. True recursive procedures
        # would also self-loop, but the symbol DB cannot distinguish that
        # from VBA's return-value convention without AST inspection;
        # tagging conservatively avoids false-flagging hundreds of normal
        # procedures.
        is_cycle = len(component) > 1
        for fqn in sorted(component):
            out.append(OrderedNode(fqn=fqn, is_cycle_member=is_cycle))
    return out


def _tarjan_sccs(graph: dict[str, list[str]]) -> list[list[str]]:
    """Return strongly-connected components in reverse topological order.

    The returned ordering is "leaves-first": a component appears before
    any component that depends on it. For two non-cyclic nodes A → B,
    [B], [A] is the result — B (the callee) is interpreted first.
    """
    index_counter = [0]
    stack: list[str] = []
    on_stack: dict[str, bool] = {n: False for n in graph}
    indices: dict[str, int] = {}
    lowlink: dict[str, int] = {}
    result: list[list[str]] = []

    def strongconnect(v: str) -> None:
        indices[v] = index_counter[0]
        lowlink[v] = index_counter[0]
        index_counter[0] += 1
        stack.append(v)
        on_stack[v] = True

        for w in graph.get(v, ()):
            if w not in indices:
                strongconnect(w)
                lowlink[v] = min(lowlink[v], lowlink[w])
            elif on_stack.get(w, False):
                lowlink[v] = min(lowlink[v], indices[w])

        if lowlink[v] == indices[v]:
            component: list[str] = []
            while True:
                w = stack.pop()
                on_stack[w] = False
                component.append(w)
                if w == v:
                    break
            result.append(component)

    # Visit in deterministic order.
    for v in sorted(graph):
        if v not in indices:
            strongconnect(v)

    return result
