#!/usr/bin/env python3
"""
VBA Reference Resolver

Resolves identifier references in VBA code to their symbol definitions.
Classifies unresolved references by reason.
"""

import re
from dataclasses import dataclass, field, asdict
from typing import List, Dict, Optional, Set, Tuple
from enum import Enum

from .symbol_extractor import Symbol, SymbolTable


class ResolutionStatus(str, Enum):
    """Classification of reference resolution"""
    RESOLVED = "resolved"
    UNRESOLVED_ENUM_VALUE = "enum_value"
    UNRESOLVED_EXTERNAL_LIB = "external_library"
    UNRESOLVED_LATE_BOUND = "late_bound"
    UNRESOLVED_BUILTIN = "vba_builtin"
    UNRESOLVED_UNKNOWN = "unknown"


@dataclass
class Reference:
    """A reference to an identifier in code"""
    identifier: str
    context_type: str  # "call", "assignment", "property_access", etc.
    line_number: int
    status: ResolutionStatus = ResolutionStatus.UNRESOLVED_UNKNOWN
    resolved_to: Optional[Symbol] = None
    reason: str = ""  # Why it couldn't be resolved
    referenced_module: Optional[str] = None  # For cross-module references


@dataclass
class ReferenceMap:
    """Map of all references found in a module"""
    module_name: str
    references: List[Reference] = field(default_factory=list)
    cross_module_calls: Dict[str, List[str]] = field(default_factory=dict)  # module -> [called procedures]

    def add_reference(self, ref: Reference) -> None:
        """Add a reference"""
        self.references.append(ref)

    def resolved_count(self) -> int:
        """Count of successfully resolved references"""
        return sum(1 for r in self.references if r.status == ResolutionStatus.RESOLVED)

    def unresolved_count(self) -> int:
        """Count of unresolved references"""
        return len(self.references) - self.resolved_count()

    def to_dict(self) -> dict:
        return {
            "module_name": self.module_name,
            "total_references": len(self.references),
            "resolved": self.resolved_count(),
            "unresolved": self.unresolved_count(),
            "cross_module_calls": self.cross_module_calls,
            "references": [
                {
                    "identifier": r.identifier,
                    "context": r.context_type,
                    "line": r.line_number,
                    "status": r.status.value,
                    "reason": r.reason,
                    "resolved_to_module": r.resolved_to.module if r.resolved_to else None,
                }
                for r in self.references
            ]
        }


class VBAReferenceResolver:
    """Resolves identifiers to their definitions"""

    # Known VBA built-in objects and libraries
    VBA_BUILTINS = {
        "DoCmd", "Me", "Debug", "Err", "App", "Screen",
        "VBA", "Access", "Excel", "Word", "Outlook",
        "CreateObject", "GetObject", "Nz", "DLookup", "DMax",
        "DMin", "DSum", "DCount", "DAvg", "Replace",
        "InStr", "Left", "Right", "Mid", "Len", "Trim",
        "Val", "Str", "UBound", "LBound", "IsArray", "IsNull",
        "IsDate", "IsNumeric", "Erase", "ReDim"
    }

    # Patterns for reference extraction
    IDENTIFIER_PATTERN = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*(?:[(\.]|=|\+|-|\*|/|And|Or|Not)")
    FUNCTION_CALL_PATTERN = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(")
    PROPERTY_ACCESS_PATTERN = re.compile(r"([A-Za-z_][A-Za-z0-9_]*)\s*\.")
    ASSIGNMENT_PATTERN = re.compile(r"(\w+)\s*=")

    def __init__(self):
        self.symbol_tables: Dict[str, SymbolTable] = {}

    def register_symbol_table(self, table: SymbolTable) -> None:
        """Register a symbol table for reference resolution"""
        self.symbol_tables[table.module_name] = table

    def resolve_references(self, module_name: str, vba_code: str) -> ReferenceMap:
        """Find and resolve all references in VBA code"""
        if module_name not in self.symbol_tables:
            raise ValueError(f"No symbol table registered for module '{module_name}'")

        ref_map = ReferenceMap(module_name)
        local_table = self.symbol_tables[module_name]

        # Extract identifiers used in code
        identifiers: Set[str] = set()
        for match in self.FUNCTION_CALL_PATTERN.finditer(vba_code):
            identifiers.add(match.group(1))
        for match in self.PROPERTY_ACCESS_PATTERN.finditer(vba_code):
            identifiers.add(match.group(1))

        # Resolve each identifier
        for identifier in identifiers:
            resolved = self._resolve_identifier(identifier, local_table)
            ref_map.add_reference(resolved)

            # Track cross-module calls
            if resolved.status == ResolutionStatus.RESOLVED and resolved.resolved_to:
                target_module = resolved.resolved_to.module
                if target_module != module_name:
                    if target_module not in ref_map.cross_module_calls:
                        ref_map.cross_module_calls[target_module] = []
                    if resolved.identifier not in ref_map.cross_module_calls[target_module]:
                        ref_map.cross_module_calls[target_module].append(resolved.identifier)

        return ref_map

    def _resolve_identifier(self, identifier: str, local_table: SymbolTable) -> Reference:
        """Try to resolve a single identifier"""
        ref = Reference(
            identifier=identifier,
            context_type="call",
            line_number=0,  # TODO: track line numbers
        )

        # Check local symbol table
        local_symbol = local_table.get_symbol(identifier)
        if local_symbol:
            ref.status = ResolutionStatus.RESOLVED
            ref.resolved_to = local_symbol
            return ref

        # Check other registered modules
        for module_name, table in self.symbol_tables.items():
            if module_name == local_table.module_name:
                continue
            symbol = table.get_symbol(identifier)
            if symbol:
                ref.status = ResolutionStatus.RESOLVED
                ref.resolved_to = symbol
                ref.referenced_module = module_name
                return ref

        # Check VBA built-ins
        if identifier in self.VBA_BUILTINS:
            ref.status = ResolutionStatus.UNRESOLVED_BUILTIN
            ref.reason = "VBA built-in object/function"
            return ref

        # Check if it looks like an enum value (assuming s<CamelCase> pattern)
        if identifier.startswith("s") and identifier[1].isupper():
            ref.status = ResolutionStatus.UNRESOLVED_ENUM_VALUE
            ref.reason = "Appears to be enum value (s-prefix convention)"
            return ref

        # Default: unknown
        ref.status = ResolutionStatus.UNRESOLVED_UNKNOWN
        ref.reason = "Identifier not found in local or registered modules"
        return ref


class DependencyGraphBuilder:
    """Builds a dependency graph from resolved references"""

    def __init__(self):
        self.edges: Dict[str, Set[str]] = {}  # procedure -> {called procedures}

    def add_call(self, caller: str, callee: str) -> None:
        """Record that caller calls callee"""
        if caller not in self.edges:
            self.edges[caller] = set()
        self.edges[caller].add(callee)

    def has_cycle(self, start: str, visited: Optional[Set[str]] = None, rec_stack: Optional[Set[str]] = None) -> bool:
        """Detect cyclic dependencies using DFS"""
        if visited is None:
            visited = set()
        if rec_stack is None:
            rec_stack = set()

        visited.add(start)
        rec_stack.add(start)

        for neighbor in self.edges.get(start, set()):
            if neighbor not in visited:
                if self.has_cycle(neighbor, visited, rec_stack):
                    return True
            elif neighbor in rec_stack:
                return True

        rec_stack.remove(start)
        return False

    def to_dict(self) -> dict:
        """Serialize to JSON"""
        return {
            "edges": {k: sorted(v) for k, v in self.edges.items()},
            "has_cycles": any(
                self.has_cycle(node) for node in self.edges
            )
        }
