import sys, numpy as np
from pathlib import Path
ref = Path(sys.argv[1])
for other in sys.argv[2:]:
    moved, n, worst, count, bars = 0, 0, 0.0, 0, 0
    for f in sorted(Path(other).glob("*.beats")):
        a = [l.split("\t") for l in (ref / f.name).read_text().split("\n") if l]
        b = [l.split("\t") for l in f.read_text().split("\n") if l]
        ta, tb = np.array([float(x[0]) for x in a]), np.array([float(x[0]) for x in b])
        n += len(ta); count += len(ta) != len(tb)
        if len(ta) == len(tb):
            d = np.abs(ta - tb); moved += int((d > 0).sum()); worst = max(worst, float(d.max()))
            bars += sum(x[1] != y[1] for x, y in zip(a, b))
    print(f"{other}: beats {n}, moved {moved}, furthest {worst:.3f} s, songs with another number of beats {count}, beat numbers that differ {bars}")
