"""The charts read from one kind of file against those read from another kind of the same recordings (D3).

    py -3.12 tools/kinds_check.py <folder of charts, e.g. from .wav> <folder of charts, e.g. from .mp3>

Compares the .dettato.json of the same name: how many have the same number of bars, in those how far each bar's
start is from the other's (seconds), and how many chords are written the same.
"""
import json
import sys
from pathlib import Path

a_dir, b_dir = Path(sys.argv[1]), Path(sys.argv[2])
songs = same_bars = same_charts = chords = same_chords = 0
moves = []
for a_path in sorted(a_dir.glob("*.dettato.json")):
    b_path = b_dir / a_path.name
    if not b_path.exists():
        print(f"{a_path.name}: not in {b_dir}")
        continue
    a, b = (json.loads(p.read_text(encoding="utf-8"))["bars"] for p in (a_path, b_path))
    songs += 1
    if len(a) != len(b):
        print(f"{a_path.name}: {len(a)} bars and {len(b)}; first {a[0]['start']:.2f} and {b[0]['start']:.2f}, last ends {a[-1]['end']:.2f} and {b[-1]['end']:.2f}")
        continue
    same_bars += 1
    moves += [y["start"] - x["start"] for x, y in zip(a, b)]
    names = [([c["symbol"] for c in x["chords"]], [c["symbol"] for c in y["chords"]]) for x, y in zip(a, b)]
    chords += sum(max(len(x), len(y)) for x, y in names)
    same_chords += sum(sum(p == q for p, q in zip(x, y)) for x, y in names)
    same_charts += all(x == y for x, y in names)
moves.sort()
print(f"{songs} songs: the same number of bars in {same_bars}, every chord the same in {same_charts}")
if moves:
    mid = moves[len(moves) // 2]
    print(f"in those {same_bars}: {len(moves)} bars, the second's start later by {1000 * mid:+.1f} ms at the middle, "
          f"{1000 * moves[0]:+.1f} to {1000 * moves[-1]:+.1f} ms; chords the same {same_chords}/{chords} ({100 * same_chords / chords:.1f}%)")
