"""The .NET bass staff against tools/bass.py's: for charts orecchiante wrote with their parts, each bar's `bass` in the file
beside the chart against what tools/bass.py reads from the same bass part and bars. Not to the letter (Bass.cs says why):
the share of bars written the same, and of the 16ths whose note (or rest) is the same.
  .venv/Scripts/python tools/bass_check.py <x.orecchiante.json> ...   [-v: the bars that differ]
"""
import json, re, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import bass  # noqa: E402

LEN = {"1": 16, "2.": 12, "2": 8, "4.": 6, "4": 4, "8.": 3, "8": 2, "16": 1}


def sixteenths(music):
    """A bar's music as what sounds on each 16th: a pitch name where a note starts, '-' where it goes on, 'r' a rest."""
    out, held = [], False
    for tok in music.replace("time ", "time_").split():
        if tok.startswith("time_"):
            continue
        m = re.fullmatch(r"([a-gr][a-z]*[,']*)(1|2\.|2|4\.|4|8\.|8|16)(~?)", tok)
        if not m:
            raise ValueError(tok)
        n = LEN[m[2]]
        head = "r" if m[1] == "r" else ("-" if held else m[1])
        out += [head] + ["-" if m[1] != "r" else "r"] * (n - 1)
        held = m[1] != "r" and m[3] == "~"
    return out


def main():
    files = [a for a in sys.argv[1:] if not a.startswith("-")]
    same_bars = all_bars = same16 = all16 = 0
    for f in files:
        side = json.loads(Path(f).read_text(encoding="utf-8"))
        flac = Path(f).parent / side["parts"]["bass"]
        ours = bass.read(flac, side)
        net = [b.get("bass") for b in side["bars"]]
        if ours is None or all(x is None for x in net):
            print(f"{Path(f).name}: python {'none' if ours is None else 'a bass'}, .net {'none' if all(x is None for x in net) else 'a bass'}")
            continue
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
        print(f"{Path(f).name}: bars the same {sum(1 for p, q in zip(ours, net) if p == q)}/{len(net)}, 16ths {s / max(a, 1):.3f}")
    print(f"all: bars {same_bars}/{all_bars} ({same_bars / max(all_bars, 1):.3f}), 16ths {same16 / max(all16, 1):.3f}")


if __name__ == "__main__":
    main()
