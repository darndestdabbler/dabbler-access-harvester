#!/usr/bin/env python3
"""
PoC Test: Run symbol resolver on frmCompanyDetail and modStrings
"""

import sys
from pathlib import Path
import json
from dataclasses import dataclass, field, asdict
from typing import List, Dict, Optional, Set
from enum import Enum
import re

# Import directly instead of relative imports
sys.path.insert(0, str(Path(__file__).parent))

# Inline imports to avoid relative import issues
exec(open(Path(__file__).parent / "symbol_extractor.py").read())
exec(open(Path(__file__).parent / "reference_resolver.py").read())
exec(open(Path(__file__).parent / "runner.py").read())


def main():
    # Paths to IR files
    ir_base = Path(__file__).parent.parent.parent / "tmp" / "comprehension-ir" / "AccessMetaData" / "NorthwindDevEdition" / "ir"

    frm_ir = ir_base / "Forms" / "frmCompanyDetail.ir.json"
    mod_ir = ir_base / "ModulesAndClasses" / "modStrings.ir.json"

    if not frm_ir.exists() or not mod_ir.exists():
        print(f"Error: IR files not found")
        print(f"  Looking for: {frm_ir}")
        print(f"  Looking for: {mod_ir}")
        return 1

    # Create output directory
    output_dir = Path(__file__).parent / "output"
    output_dir.mkdir(exist_ok=True)

    # Run resolver
    runner = SymbolResolverRunner(output_dir)

    print("Loading IR files...")
    runner.load_ir_file(mod_ir, "modStrings", "Module")
    runner.load_ir_file(frm_ir, "frmCompanyDetail", "Form")

    print("\nGenerating reports...")
    runner.save_reports()

    print("\nSuccess! Reports saved to:")
    print(f"  {output_dir}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
