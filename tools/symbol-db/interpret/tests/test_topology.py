"""Hermetic tests for the leaves-first topology helper.

Run with::

    cd tools/symbol-db
    ../../.venv/Scripts/python.exe -m unittest discover -s interpret/tests
"""

from __future__ import annotations

import unittest

from interpret.topology import OrderedNode, order_leaves_first


class LeavesFirstOrderTests(unittest.TestCase):
    def test_simple_chain_emits_leaves_first(self) -> None:
        # a -> b -> c (a depends on b depends on c). Order must be c, b, a.
        order = order_leaves_first(
            ["a", "b", "c"],
            {"a": {"b"}, "b": {"c"}, "c": set()},
        )
        self.assertEqual([n.fqn for n in order], ["c", "b", "a"])
        self.assertTrue(all(not n.is_cycle_member for n in order))

    def test_independent_leaves_emit_in_fqn_order(self) -> None:
        order = order_leaves_first(
            ["a", "b", "c"],
            {"a": set(), "b": set(), "c": set()},
        )
        # SCCs are emitted in FQN order when no edges constrain them.
        self.assertEqual({n.fqn for n in order}, {"a", "b", "c"})

    def test_self_loop_is_not_a_cycle(self) -> None:
        # In VBA, a function self-reference almost always means
        # `FunctionName = expr` (the return-value pattern), not real
        # recursion. The topology helper drops self-loops before
        # scheduling so single-node SCCs are not mis-tagged.
        order = order_leaves_first(
            ["GetString"],
            {"GetString": {"GetString"}},
        )
        self.assertEqual(order, [OrderedNode("GetString", is_cycle_member=False)])

    def test_multi_node_cycle_is_tagged(self) -> None:
        # a <-> b; c -> a. Expect the {a, b} component tagged as a
        # cycle and emitted before c.
        order = order_leaves_first(
            ["a", "b", "c"],
            {"a": {"b"}, "b": {"a"}, "c": {"a"}},
        )
        cycle_nodes = [n for n in order if n.is_cycle_member]
        self.assertEqual({n.fqn for n in cycle_nodes}, {"a", "b"})
        # c must come after the cycle.
        self.assertEqual(order[-1].fqn, "c")
        self.assertFalse(order[-1].is_cycle_member)

    def test_edges_outside_node_set_are_ignored(self) -> None:
        # b is not in the schedulable set; the edge a -> b should be
        # silently dropped without breaking ordering.
        order = order_leaves_first(
            ["a", "c"],
            {"a": {"b", "c"}, "c": set()},
        )
        self.assertEqual([n.fqn for n in order], ["c", "a"])

    def test_dependency_inversion_does_not_break(self) -> None:
        # GetString -> StringFormat (caller -> callee). Schedule must
        # interpret StringFormat first.
        order = order_leaves_first(
            ["GetString", "StringFormat", "StringFormatSQL"],
            {
                "GetString": {"StringFormat"},
                "StringFormat": set(),
                "StringFormatSQL": {"StringFormat"},
            },
        )
        positions = {n.fqn: i for i, n in enumerate(order)}
        self.assertLess(positions["StringFormat"], positions["GetString"])
        self.assertLess(positions["StringFormat"], positions["StringFormatSQL"])


if __name__ == "__main__":
    unittest.main()
