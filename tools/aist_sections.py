"""Section starts against the AIST structure annotation of RWC-P (research only, not redistributed).

  .venv/Scripts/python tools/aist_sections.py [rwc-dev|rwc-test] [--each] [modes=base,phrase,voice] [voice=<folder>] [drums=<folder>|none] [READ_CHORDS_CONSTANT=value ...]

Needs the AIST annotation (docs/DATASETS.md) and, for voice, the vocals' loudness: <name>.voc.npy in scratch/net/voice/rwc-p
(orecchiante ears) unless voice= says where (scratch/sep/out/htdemucs: Demucs in torch, stereo, a random shift).
Modes: base (the old sections, --old-sections), phrase (by meaning, no voice), voice (by meaning, with the voice).
A boundary counts within 0.5 s and within 3 s; the song's first start and the end are left out.
'all' takes every AIST boundary, 'change' only those where the label changes (chorus A, chorus A is one).
"""
import sys
from collections import Counter
from pathlib import Path
import numpy as np
sys.path.insert(0, "tools")
import measure as ms, read_chords as rc

AIST = Path("scratch/real/rwc-annotations-archive/AIST_RWC-MDB-P-2001_CHORUS")
VOICE = Path("scratch/net/voice/rwc-p")
DRUMS = Path("scratch/drum/rwc-p")      # with the voice, the drums too (as orecchiante hears both from Demucs); drums=none: not
name = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("-") else "rwc-dev"
each = "--each" in sys.argv
MODES = ("base", "phrase", "voice")
for a in sys.argv[2:]:
    if a.startswith("modes="):
        MODES = tuple(a[6:].split(","))
    elif a.startswith("voice="):
        VOICE = Path(a[6:])
    elif a.startswith("drums="):
        DRUMS = Path(a[6:])
    elif "=" in a:
        k, v = a.split("=")
        setattr(rc, k, type(getattr(rc, k))(float(v) if "." in v else int(v)))


def truth(sid):
    rows = []
    for line in (AIST / f"RM-P{sid[5:]}.CHORUS.TXT").read_text(encoding="latin-1").splitlines():
        p = line.split("\t")
        if len(p) >= 3:
            rows.append((int(p[0]) / 100, int(p[1]) / 100, p[2].strip('"')))
    every = [a for a, _, _ in rows[1:]]
    change = [rows[i][0] for i in range(1, len(rows)) if rows[i][2] != rows[i - 1][2]]
    return every, change, rows


def hits(got, want, tol):
    used, n = set(), 0
    for w in want:
        best = min((abs(g - w), i) for i, g in enumerate(got) if i not in used) if len(used) < len(got) else None
        if best and best[0] <= tol:
            used.add(best[1]); n += 1
    return n


rc.use_ears()
lines, heard = ms.ears_of(name)
tot, offs, kinds = {}, {}, {}
for t, wav in ms.songs(name):
    sid = wav.stem
    every, change, rows = truth(sid)
    said = np.load(Path(heard) / (sid + ".npz"))
    given, units, per = ms.bar_lines(t, wav, lines, said)
    vf = VOICE / f"{sid}.voc.npy"
    out = {}
    for mode in MODES:
        rc.PHRASE_SECTIONS = mode != "base"
        kw = {"voice": np.load(vf)} if mode == "voice" and vf.exists() else {}
        df = DRUMS / f"{sid}.drum.npy"
        if mode == "voice" and df.exists():
            kw["drums"] = np.load(df)
        r = rc.read(wav, ms.feats(wav), lines=given, units=units, beats=per, heard=said, **kw)
        got = [b["start"] for b in r["bars"] if b.get("barOfSection") == 1][1:]
        out[mode] = got
        whole = [x for x in r["bars"] if not x.get("pickup")]
        bar = float(np.median([x["end"] - x["start"] for x in whole]))
        for i in range(1, len(rows)):
            if rows[i][2] != rows[i - 1][2]:
                key = (mode, rows[i - 1][2].split(" ")[0] + ">" + rows[i][2].split(" ")[0])
                kinds.setdefault(key, [0, 0])
                kinds[key][1] += 1
                kinds[key][0] += any(abs(g - rows[i][0]) <= 3.0 for g in got)
        for w in change:
            d = min(got, key=lambda g: abs(g - w), default=None)
            if d is not None and abs(d - w) <= 3.0:
                offs.setdefault(mode, Counter())[round((d - w) / bar)] += 1
        for kind, want in (("all", every), ("change", change)):
            for tol in (0.5, 3.0):
                k = (mode, kind, tol)
                h = hits(got, want, tol)
                a = tot.setdefault(k, [0, 0, 0])
                a[0] += h; a[1] += len(got); a[2] += len(want)
    if each:
        print(sid, "aist", [round(x) for x in change], "| base", [round(x) for x in out.get("base", [])], "| voice", [round(x) for x in out.get("voice", [])], flush=True)
rc.PHRASE_SECTIONS = False
print(f"{name}: section starts against AIST (precision / recall / F)")
for k, (h, g, w) in sorted(tot.items()):
    p, r = h / max(g, 1), h / max(w, 1)
    print(f"  {k[0]:6} {k[1]:6} ±{k[2]:.1f}s  P {p:5.1%}  R {r:5.1%}  F {2 * p * r / max(p + r, 1e-9):5.1%}  ({g} found, {w} in AIST)")
for m, c in offs.items(): print(" ", m, "offset in bars of the nearest start (AIST change, within 3 s):", sorted(c.items()))
for k, (h, n) in sorted(kinds.items(), key=lambda x: -x[1][1])[:20]:
    print(f"  {k[0]} {k[1]:24} {h:3}/{n:3}  {h / n:5.1%}")


