#!/usr/bin/env python3
"""
VBA Symbol Extractor

Extracts symbols (procedures, variables, enums, constants) from VBA code.
Builds symbol tables for later reference resolution.
"""

import re
import json
from dataclasses import dataclass, field, asdict
from typing import List, Dict, Optional, Set, Tuple
from enum import Enum


class SymbolKind(str, Enum):
    """Symbol classification"""
    PROCEDURE = "procedure"
    FUNCTION = "function"
    VARIABLE = "variable"
    CONSTANT = "constant"
    ENUM_DECL = "enum_declaration"
    ENUM_VALUE = "enum_value"
    CLASS = "class"
    MODULE = "module"
    TYPE_DECL = "type_declaration"


class AccessModifier(str, Enum):
    PUBLIC = "Public"
    PRIVATE = "Private"
    FRIEND = "Friend"
    UNKNOWN = "Unknown"


@dataclass
class Parameter:
    """Function/Sub parameter definition"""
    name: str
    data_type: str
    passing_mechanism: str  # ByVal, ByRef
    is_optional: bool = False
    is_paramarray: bool = False
    is_array: bool = False
    default_value: Optional[str] = None


@dataclass
class Symbol:
    """Represents a single symbol in VBA code"""
    name: str
    kind: SymbolKind
    module: str  # Module/Class containing this symbol
    access_modifier: AccessModifier = AccessModifier.PUBLIC
    data_type: Optional[str] = None  # Return type or variable type
    parameters: List[Parameter] = field(default_factory=list)
    line_number: int = 0
    parent_scope: Optional[str] = None  # e.g., enum name for enum values
    full_name: str = field(default="")  # module.name

    def __post_init__(self):
        if not self.full_name:
            self.full_name = f"{self.module}.{self.name}"


@dataclass
class SymbolTable:
    """Symbol table for a module/class"""
    module_name: str
    module_type: str  # "Module", "Class", "Form"
    symbols: List[Symbol] = field(default_factory=list)
    references: Dict[str, List[str]] = field(default_factory=dict)  # symbol -> referenced symbols

    def add_symbol(self, symbol: Symbol) -> None:
        """Add a symbol to the table"""
        self.symbols.append(symbol)

    def get_symbol(self, name: str) -> Optional[Symbol]:
        """Retrieve a symbol by name"""
        return next((s for s in self.symbols if s.name == name), None)

    def list_symbols(self, kind: Optional[SymbolKind] = None) -> List[Symbol]:
        """List all symbols, optionally filtered by kind"""
        if kind:
            return [s for s in self.symbols if s.kind == kind]
        return self.symbols

    def to_dict(self) -> dict:
        """Serialize to JSON-compatible dictionary"""
        return {
            "module_name": self.module_name,
            "module_type": self.module_type,
            "symbols": [asdict(s) for s in self.symbols],
            "references": self.references,
        }


class VBASymbolExtractor:
    """Extracts symbols from VBA code"""

    # Regex patterns for VBA constructs
    ENUM_DECL_PATTERN = re.compile(r"Public\s+Enum\s+(\w+)", re.IGNORECASE)
    ENUM_VALUE_PATTERN = re.compile(r"^\s*(\w+)\s*=\s*(\d+)", re.MULTILINE)
    END_ENUM_PATTERN = re.compile(r"^\s*End\s+Enum\s*$", re.IGNORECASE | re.MULTILINE)

    FUNCTION_PATTERN = re.compile(
        r"^(Public|Private|Friend)?\s*(Static)?\s*Function\s+(\w+)\s*\((.*?)\)\s*As\s+(\w+)",
        re.IGNORECASE | re.MULTILINE
    )
    SUB_PATTERN = re.compile(
        r"^(Public|Private|Friend)?\s*(Static)?\s*Sub\s+(\w+)\s*\((.*?)\)",
        re.IGNORECASE | re.MULTILINE
    )

    PARAM_PATTERN = re.compile(
        r"(\w+)\s+As\s+(\w+(?:\.\w+)?)"  # name As type
    )
    PARAM_ARRAY_PATTERN = re.compile(r"ParamArray\s+(\w+)")
    BYREF_PATTERN = re.compile(r"ByRef\s+", re.IGNORECASE)
    BYVAL_PATTERN = re.compile(r"ByVal\s+", re.IGNORECASE)

    DIM_PATTERN = re.compile(
        r"^\s*Dim\s+(\w+)\s+As\s+(\w+(?:\.\w+)?)",
        re.IGNORECASE | re.MULTILINE
    )
    CONST_PATTERN = re.compile(
        r"^\s*(Public|Private)?\s*Const\s+(\w+)\s*=",
        re.IGNORECASE | re.MULTILINE
    )

    def __init__(self, module_name: str, module_type: str = "Module"):
        self.module_name = module_name
        self.module_type = module_type
        self.symbol_table = SymbolTable(module_name, module_type)
        self.vba_code = ""

    def extract(self, vba_code: str) -> SymbolTable:
        """Extract all symbols from VBA code"""
        self.vba_code = vba_code

        self._extract_enums()
        self._extract_functions()
        self._extract_subs()
        self._extract_variables()
        self._extract_constants()

        return self.symbol_table

    def _extract_enums(self) -> None:
        """Extract enum declarations and their values"""
        for enum_match in self.ENUM_DECL_PATTERN.finditer(self.vba_code):
            enum_name = enum_match.group(1)
            start_pos = enum_match.end()

            # Find the matching 'End Enum'
            end_enum_match = self.END_ENUM_PATTERN.search(self.vba_code, start_pos)
            if end_enum_match:
                enum_body = self.vba_code[start_pos:end_enum_match.start()]

                # Add enum declaration symbol
                enum_symbol = Symbol(
                    name=enum_name,
                    kind=SymbolKind.ENUM_DECL,
                    module=self.module_name,
                    access_modifier=AccessModifier.PUBLIC,
                    line_number=self._get_line_number(enum_match.start()),
                )
                self.symbol_table.add_symbol(enum_symbol)

                # Extract enum values
                for value_match in self.ENUM_VALUE_PATTERN.finditer(enum_body):
                    value_name = value_match.group(1)
                    value_num = value_match.group(2)

                    enum_val_symbol = Symbol(
                        name=value_name,
                        kind=SymbolKind.ENUM_VALUE,
                        module=self.module_name,
                        parent_scope=enum_name,
                        data_type="Long",
                        line_number=self._get_line_number(value_match.start()),
                    )
                    self.symbol_table.add_symbol(enum_val_symbol)

    def _extract_functions(self) -> None:
        """Extract function declarations"""
        for match in self.FUNCTION_PATTERN.finditer(self.vba_code):
            access_str = match.group(1) or "Public"
            func_name = match.group(3)
            params_str = match.group(4)
            return_type = match.group(5)

            params = self._parse_parameters(params_str)

            symbol = Symbol(
                name=func_name,
                kind=SymbolKind.FUNCTION,
                module=self.module_name,
                access_modifier=AccessModifier(access_str) if access_str in ["Public", "Private", "Friend"] else AccessModifier.PUBLIC,
                data_type=return_type,
                parameters=params,
                line_number=self._get_line_number(match.start()),
            )
            self.symbol_table.add_symbol(symbol)

    def _extract_subs(self) -> None:
        """Extract sub declarations"""
        for match in self.SUB_PATTERN.finditer(self.vba_code):
            access_str = match.group(1) or "Public"
            sub_name = match.group(3)
            params_str = match.group(4)

            params = self._parse_parameters(params_str)

            symbol = Symbol(
                name=sub_name,
                kind=SymbolKind.PROCEDURE,
                module=self.module_name,
                access_modifier=AccessModifier(access_str) if access_str in ["Public", "Private", "Friend"] else AccessModifier.PUBLIC,
                parameters=params,
                line_number=self._get_line_number(match.start()),
            )
            self.symbol_table.add_symbol(symbol)

    def _extract_variables(self) -> None:
        """Extract variable declarations"""
        for match in self.DIM_PATTERN.finditer(self.vba_code):
            var_name = match.group(1)
            var_type = match.group(2)

            symbol = Symbol(
                name=var_name,
                kind=SymbolKind.VARIABLE,
                module=self.module_name,
                data_type=var_type,
                access_modifier=AccessModifier.PRIVATE,
                line_number=self._get_line_number(match.start()),
            )
            self.symbol_table.add_symbol(symbol)

    def _extract_constants(self) -> None:
        """Extract constant declarations"""
        for match in self.CONST_PATTERN.finditer(self.vba_code):
            access_str = match.group(1) or "Public"
            const_name = match.group(2)

            symbol = Symbol(
                name=const_name,
                kind=SymbolKind.CONSTANT,
                module=self.module_name,
                access_modifier=AccessModifier(access_str) if access_str in ["Public", "Private"] else AccessModifier.PUBLIC,
                line_number=self._get_line_number(match.start()),
            )
            self.symbol_table.add_symbol(symbol)

    def _parse_parameters(self, params_str: str) -> List[Parameter]:
        """Parse parameter list from function/sub signature"""
        if not params_str.strip():
            return []

        params = []
        # Simple split by comma (doesn't handle complex types with commas)
        for param_part in params_str.split(","):
            param_part = param_part.strip()
            if not param_part:
                continue

            # Check for ByRef/ByVal
            is_byref = bool(self.BYREF_PATTERN.search(param_part))
            param_part_clean = self.BYREF_PATTERN.sub("", param_part)
            param_part_clean = self.BYVAL_PATTERN.sub("", param_part_clean).strip()

            # Check for ParamArray
            is_paramarray = bool(self.PARAM_ARRAY_PATTERN.search(param_part_clean))

            # Extract name and type
            match = self.PARAM_PATTERN.search(param_part_clean)
            if match:
                param_name = match.group(1)
                param_type = match.group(2)

                params.append(Parameter(
                    name=param_name,
                    data_type=param_type,
                    passing_mechanism="ByRef" if is_byref else "ByVal",
                    is_paramarray=is_paramarray,
                ))

        return params

    def _get_line_number(self, pos: int) -> int:
        """Calculate line number from character position"""
        return self.vba_code[:pos].count("\n") + 1
