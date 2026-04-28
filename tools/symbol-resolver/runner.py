#!/usr/bin/env python3
"""
Symbol Resolver Runner

Main entry point for symbol resolution on VBA code.
Loads IR files, extracts symbols, resolves references, generates reports.
"""

import json
import sys
from pathlib import Path
from typing import Dict

from .symbol_extractor import VBASymbolExtractor, SymbolTable
from .reference_resolver import VBAReferenceResolver, DependencyGraphBuilder


class SymbolResolverRunner:
    """Orchestrates symbol extraction and reference resolution"""

    def __init__(self, output_dir: Path = Path(".")):
        self.output_dir = Path(output_dir)
        self.extractors: Dict[str, VBASymbolExtractor] = {}
        self.symbol_tables: Dict[str, SymbolTable] = {}
        self.resolver = VBAReferenceResolver()

    def load_ir_file(self, ir_path: Path, module_name: str, module_type: str = "Module") -> None:
        """Load VBA code from an IR JSON file and extract symbols"""
        with open(ir_path, "r", encoding="utf-8") as f:
            ir_data = json.load(f)

        vba_code = ir_data.get("VbaCode", "")
        if not vba_code:
            print(f"Warning: No VbaCode found in {ir_path}")
            return

        # Unescape the VBA code (it comes escaped in JSON)
        vba_code = vba_code.replace("\\r\\n", "\n").replace("\\\"", "\"")

        # Extract symbols
        extractor = VBASymbolExtractor(module_name, module_type)
        symbol_table = extractor.extract(vba_code)

        self.extractors[module_name] = extractor
        self.symbol_tables[module_name] = symbol_table
        self.resolver.register_symbol_table(symbol_table)

        print(f"Extracted {len(symbol_table.symbols)} symbols from {module_name}")

    def generate_report(self) -> Dict:
        """Generate comprehensive report of symbol resolution"""
        report = {
            "modules": {},
            "cross_module_references": {},
            "dependency_graph": None,
        }

        # Symbol table reports
        for module_name, table in self.symbol_tables.items():
            report["modules"][module_name] = {
                "type": table.module_type,
                "symbol_count": len(table.symbols),
                "symbols": table.to_dict()["symbols"],
            }

        # Reference resolution
        ref_maps = {}
        dependency_builder = DependencyGraphBuilder()

        for module_name in self.symbol_tables.keys():
            vba_code = self.extractors[module_name].vba_code
            ref_map = self.resolver.resolve_references(module_name, vba_code)
            ref_maps[module_name] = ref_map

            report["cross_module_references"][module_name] = ref_map.to_dict()

            # Build dependency graph
            for symbol in self.symbol_tables[module_name].list_symbols():
                if symbol.kind.value in ["procedure", "function"]:
                    # Extract called procedures from references
                    for ref in ref_map.references:
                        # Simple heuristic: if a procedure calls another
                        dependency_builder.add_call(symbol.name, ref.identifier)

        report["dependency_graph"] = dependency_builder.to_dict()

        return report

    def save_reports(self, base_name: str = "symbol-resolution") -> None:
        """Save all reports to JSON files"""
        report = self.generate_report()

        # Save main report
        main_file = self.output_dir / f"{base_name}-report.json"
        with open(main_file, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2, default=str)
        print(f"Saved main report to {main_file}")

        # Save symbol tables
        for module_name, table in self.symbol_tables.items():
            sym_file = self.output_dir / f"{module_name}-symbols.json"
            with open(sym_file, "w", encoding="utf-8") as f:
                json.dump(table.to_dict(), f, indent=2, default=str)
            print(f"Saved symbol table for {module_name} to {sym_file}")

        # Save reference maps
        for module_name in self.symbol_tables.keys():
            vba_code = self.extractors[module_name].vba_code
            ref_map = self.resolver.resolve_references(module_name, vba_code)
            ref_file = self.output_dir / f"{module_name}-references.json"
            with open(ref_file, "w", encoding="utf-8") as f:
                json.dump(ref_map.to_dict(), f, indent=2, default=str)
            print(f"Saved reference map for {module_name} to {ref_file}")


def main():
    """Example usage"""
    import argparse

    parser = argparse.ArgumentParser(description="VBA Symbol Resolver")
    parser.add_argument("ir_files", nargs="+", help="IR JSON files to process")
    parser.add_argument("--output", "-o", default=".", help="Output directory for reports")
    args = parser.parse_args()

    runner = SymbolResolverRunner(Path(args.output))

    for ir_file_path in args.ir_files:
        path = Path(ir_file_path)
        module_name = path.stem.replace(".ir", "")
        runner.load_ir_file(path, module_name)

    runner.save_reports()


if __name__ == "__main__":
    main()
