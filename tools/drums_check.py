"""The .NET drum staff against tools/drums.py's: for charts orecchiante wrote with their parts, each bar's `drums` (the
lines as read) in the file beside the chart, and the chart's grooves, against what tools/drums.py reads from the same
drum part and bars. Not to the letter (the spectrum is single precision in librosa): the share of bars read the same,
and whether the grooves are.
  .venv/Scripts/python tools/drums_check.py <x.orecchiante.json> ...   [-v: the bars that differ]
"""
import json, re, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import drums  # noqa: E402

same = total = 0
for f in [a for a in sys.argv[1:] if not a.startswith("-")]:
    side = json.loads(Path(f).read_text(encoding="utf-8"))
    flac = Path(f).parent / side["parts"]["drums"]
    ours = drums.read(flac, side)
    net = [b.get("drums") for b in side["bars"]]
    if all(x is None for x in net):
        print(f"{Path(f).name}: no drums from .net")
        continue
    s = sum(1 for o, n in zip(ours["bars"], net) if o["drums"] == n)
    same += s
    total += len(net)
    if "-v" in sys.argv:
        for k, (o, n) in enumerate(zip(ours["bars"], net)):
            if o["drums"] != n:
                print(f"  bar {k + 1}: python {o['drums']}\n          .net   {n}")
    lys = Path(f).with_name(Path(f).name.replace(".orecchiante.json", ".lys")).read_text(encoding="utf-8")
    net_grooves = re.findall(r"^phrase (\w+) \{ (.*) \| \}$", lys, re.M)
    py_grooves = [(name, line) for g in ours["grooves"] for name, line in g["phrases"]]
    print(f"{Path(f).name}: bars read the same {s}/{len(net)}, grooves {'the same' if net_grooves == py_grooves else 'differ'}"
          f" ({len(ours['grooves'])} python, {sum(1 for n, _ in net_grooves if n.startswith('hats') or n.startswith('back') or n.startswith('kick'))} phrases .net)")
print(f"all: bars {same}/{total}")
