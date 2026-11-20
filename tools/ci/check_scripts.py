#!/usr/bin/env python3
"""CI check: every C# script under client/Assets parses (catches syntax slips before the 20-minute Unity step)."""
import glob, sys
import tree_sitter_c_sharp as cs
from tree_sitter import Language, Parser

p = Parser(Language(cs.language()))
bad = 0
for f in sorted(glob.glob("client/Assets/**/*.cs", recursive=True)):
    src = open(f, "rb").read()
    errs = []
    def walk(n):
        if n.type == "ERROR" or n.is_missing:
            errs.append(n.start_point[0] + 1)
        for c in n.children:
            walk(c)
    walk(p.parse(src).root_node)
    print(("ok   " if not errs else "FAIL ") + f, errs[:5])
    bad += len(errs)
sys.exit(1 if bad else 0)
