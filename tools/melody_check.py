"""The .NET voice staff against tools/melody.py's: for charts orecchiante wrote with their parts, each bar's `vocal` in the
file beside the chart against what tools/melody.py reads from the same vocal part and bars. Not to the letter (as
tools/bass_check.py): the share of bars written the same, and of the 16ths whose note (or rest) is the same; and the
part's instrument in the chart against Python's.
  .venv/Scripts/python tools/melody_check.py <x.orecchiante.json> ...   [-v: the bars that differ]
"""
import json, re, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import melody  # noqa: E402
from bass_check import sixteenths  # noqa: E402


def main():
    files = [a for a in sys.argv[1:] if not a.startswith("-")]
    same_bars = all_bars = same16 = all16 = 0
    for f in files:
        side = json.loads(Path(f).read_text(encoding="utf-8"))
        flac = Path(f).parent / side["parts"]["vocals"]
        res = melody.read(flac, side)
        net = [b.get("vocal") for b in side["bars"]]
        if res is None or all(x is None for x in net):
            print(f"{Path(f).name}: python {'none' if res is None else 'a voice'}, .net {'none' if all(x is None for x in net) else 'a voice'}")
            continue
        inst, ours = res
        lys = Path(f).with_name(Path(f).name.replace(".orecchiante.json", ".lys")).read_text(encoding="utf-8")
        m = re.search(r"part vocal \{ instrument (\w+) \}", lys)
        s = a = 0
        for k, (p, q) in enumerate(zip(ours, net)):
            all_bars += 1
            if p == q:
                same_bars += 1
            elif "-v" in sys.argv:
                print(f"  bar {k + 1}: python {p}\n          .net   {q}")
            x, y = sixteenths(p), sixteenths(q)
            a += max(len(x), len(y))
            s += sum(1 for i, j in zip(x, y) if i == j)
        same16 += s
        all16 += a
        print(f"{Path(f).name}: bars the same {sum(1 for p, q in zip(ours, net) if p == q)}/{len(net)}, 16ths {s / max(a, 1):.3f},"
              f" instrument python {inst} .net {m[1] if m else None}")
    print(f"all: bars {same_bars}/{all_bars} ({same_bars / max(all_bars, 1):.3f}), 16ths {same16 / max(all16, 1):.3f}")


main()
