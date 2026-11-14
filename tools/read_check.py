#!/usr/bin/env python3
"""What `dettato read` (src/Dettato) writes, against what tools/read_chords.py writes of the same recordings.

    py -3.12 tools/read_check.py scratch/pyread/seed1 scratch/net/read/seed1 [-v]

The first folder is Python's (tools/measure.py table <set> --ears --net scratch/net --write scratch/pyread), the
second is `dettato read <recordings> --out <folder> --lines <beats> --heard <chords>`. Compared: every
<name>.dettato.json, letter for letter, and every <name>.lys but its first line (which names the program).
Where a chart differs, the first thing that does is shown (-v: for every such chart, not the first three).
"""
import argparse
import json
import sys
from pathlib import Path


def first_gap(a, b, where=""):
    """The first place two readings differ, as (where, one, the other)."""
    if type(a) is not type(b):
        return where, a, b
    if isinstance(a, dict):
        for k in list(a) + [k for k in b if k not in a]:
            if k not in a or k not in b:
                return f"{where}.{k}", a.get(k, "(none)"), b.get(k, "(none)")
            gap = first_gap(a[k], b[k], f"{where}.{k}")
            if gap:
                return gap
        return None
    if isinstance(a, list):
        for i, (x, y) in enumerate(zip(a, b)):
            gap = first_gap(x, y, f"{where}[{i}]")
            if gap:
                return gap
        return (f"{where} (length)", len(a), len(b)) if len(a) != len(b) else None
    return None if a == b else (where, a, b)


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("python", type=Path, help="a folder of what read_chords.py wrote")
    p.add_argument("net", type=Path, help="a folder of what dettato read wrote")
    p.add_argument("--songs", help="only the songs from this one to that one of the folder, as 1-50")
    p.add_argument("-v", "--verbose", action="store_true")
    args = p.parse_args()
    theirs = sorted(args.python.glob("*.dettato.json"))
    if args.songs:
        first, last = args.songs.split("-")
        theirs = theirs[int(first) - 1:int(last)]
    if not theirs:
        sys.exit(f"no <name>.dettato.json in {args.python}")
    same_json = same_lys = missing = shown = 0
    for path in theirs:
        ours = args.net / path.name
        if not ours.exists():
            missing += 1
            print(f"{path.name}: not written by dettato")
            continue
        a, b = path.read_bytes(), ours.read_bytes()
        lys_a = path.with_name(path.name.replace(".dettato.json", ".lys")).read_text(encoding="utf-8").split("\n")[1:]
        lys_b = ours.with_name(ours.name.replace(".dettato.json", ".lys")).read_text(encoding="utf-8").split("\n")[1:]
        same_json += a == b
        same_lys += lys_a == lys_b
        if a != b and (args.verbose or shown < 3):
            shown += 1
            where, x, y = first_gap(json.loads(a), json.loads(b)) or ("(the letters only)", "", "")
            print(f"{path.name}: {where}: {str(x)[:80]} in Python, {str(y)[:80]} in .NET")
        elif lys_a != lys_b and (args.verbose or shown < 3):
            shown += 1
            line = next(i for i, (x, y) in enumerate(zip(lys_a + [""], lys_b + [""])) if x != y)
            print(f"{path.name}: the chart's line {line + 2}: {lys_a[line][:80]!r} in Python, {lys_b[line][:80]!r} in .NET")
    n = len(theirs)
    print(f"{args.python} against {args.net}: {n} charts, .dettato.json the same in {same_json}, .lys in {same_lys}"
          + (f", {missing} not written" if missing else ""))
    return 0 if same_json == same_lys == n else 1


if __name__ == "__main__":
    sys.exit(main())
