"""The drum part read into a kit's hits on the chart's 16ths and written as a drum staff of grooves (2026-11-23): the
Python side the .NET reader follows (src/Orecchiante/Kit.cs; tools/drums_check.py compares them). A first try with no
truth to score it on (the owner: "wrong is all right, read them first"); scored only against the generated songs'
drums (scratch/drum/try_rule.py), which are not recordings.

From the drum part Orecchiante separated (<name>.parts/drums.flac):
1. at 22050 Hz, frames of 2048 every 256 samples (librosa.stft's defaults otherwise): the power in three bands, in dB:
   low (under 150 Hz: the bass drum), middle (200 to 3000 Hz: the snare's body), high (6000 Hz and up: the hi-hat and
   the snare's wires); each band's rise = its dB less the lowest of the GAP frames before (0 when it falls).
2. at each 16th of the chart's bars: each band's most rise within 2 frames before to 2 after.
3. each band's own scale: the 90th percentile of its values over 6 dB (its strong hits), at least 12 dB.
   bass drum: the low band at KICK of its scale or more; hi-hat: the high band at HAT dB or more; snare: the middle
   band at SNARE of its scale or more, and either the high band at WIRES of its scale or more or no bass drum there.
4. grooves (the owner, 2026-11-23: a simpler way to write drums): a bar is its set of hits (16th, drum). The bars' sets,
   commonest first, each start a groove unless one already started is within NEAR hits of it (the hits one has and the
   other has not); then, three times, each bar goes to the nearest groove of its length within NEAR (the earlier on a
   tie), and each groove becomes the hits more than half its bars have. A groove of MIN_BARS bars or more is named and
   its bars are written as it (their own hits put aside); any other bar is written out.
5. writing: a groove as three one-bar phrases, one a drum (hats `hh`, back `sn`, kick `bd`; a drum it never strikes
   left out), and `drums { hats back kick }`; a bar written out the same way, inline. A hit lasts to the next of its
   drum within the beat, rests fill the rest, lengths as tools/bass.py writes them; a drum after itself is its length
   alone (`hh8 8 8`), and a name or a rest after the same length has no length (`r4 sn r sn`). In a section, a bar the same as the one before is `%`.
6. drums heard (else no staff): the drum part's mean square within 30 dB of the recording's (Kit.HeardDb says why).
   .venv/Scripts/python tools/drums.py <drums.flac> <x.orecchiante.json>      (the grooves and each bar)
"""
import json, sys
from collections import Counter
from pathlib import Path
import numpy as np

SR, HOP, NFFT = 22050, 256, 2048
LOW, MID, HIGH = 150.0, (200.0, 3000.0), 6000.0
GAP = 3
KICK, HAT, SNARE, WIRES = 0.5, 6.0, 0.7, 0.85
FLOOR, SCALE_MIN = 6.0, 12.0
NEAR, MIN_BARS, ROUNDS = 4, 3, 3
DRUMS = (("hh", "hats"), ("sn", "back"), ("bd", "kick"))

sys.path.insert(0, str(Path(__file__).parent))
import bass  # noqa: E402  (the grid and the written lengths)


def rises(flac):
    """(3, frames) each band's rise in dB (step 1)."""
    import librosa, soundfile as sf
    y, sr = sf.read(str(flac), dtype="float32", always_2d=True)
    y = librosa.resample(y.mean(axis=1), orig_sr=sr, target_sr=SR)
    S = np.abs(librosa.stft(y, n_fft=NFFT, hop_length=HOP)) ** 2
    f = librosa.fft_frequencies(sr=SR, n_fft=NFFT)
    sel = [f < LOW, (f >= MID[0]) & (f < MID[1]), f >= HIGH]
    db = np.stack([10 * np.log10(S[s].sum(axis=0) + 1e-10) for s in sel])
    r = np.zeros_like(db)
    for t in range(GAP, db.shape[1]):
        r[:, t] = db[:, t] - db[:, t - GAP:t].min(axis=1)
    return np.maximum(r, 0)


def hits(r, slots):
    """Each 16th's drums (steps 2, 3): a list of sets of 'bd', 'sn', 'hh'."""
    ft = HOP / SR
    v = np.zeros((len(slots), 3))
    for i, s in enumerate(slots):
        c = int(round(s / ft))
        if 0 <= c - 2 and c + 3 <= r.shape[1]:
            v[i] = r[:, c - 2:c + 3].max(axis=1)
    scale = [max(np.percentile(v[v[:, j] > FLOOR, j], 90) if (v[:, j] > FLOOR).any() else SCALE_MIN, SCALE_MIN) for j in range(3)]
    out = []
    for lo, mi, hi in v:
        kick = lo >= KICK * scale[0]
        h = set()
        if kick:
            h.add("bd")
        if hi >= HAT:
            h.add("hh")
        if mi >= SNARE * scale[1] and (hi >= WIRES * scale[2] or not kick):
            h.add("sn")
        out.append(h)
    return out


def grooves(sets, lens):
    """Step 4: (each bar's groove index or -1, the grooves as (16ths, frozenset of (16th, drum)))."""
    first = {}
    for k, s in enumerate(sets):
        first.setdefault((lens[k], s), k)
    count = Counter((lens[k], s) for k, s in enumerate(sets))
    pats = []
    for n, s in sorted(count, key=lambda x: (-count[x], first[x])):
        if not any(pn == n and len(s ^ p) <= NEAR for pn, p in pats):
            pats.append((n, s))

    def nearest(k):
        best, arg = None, -1
        for g, (pn, p) in enumerate(pats):
            if pn == lens[k]:
                d = len(sets[k] ^ p)
                if d <= NEAR and (best is None or d < best):
                    best, arg = d, g
        return arg

    for _ in range(ROUNDS):
        assign = [nearest(k) for k in range(len(sets))]
        new = []
        for g, (pn, p) in enumerate(pats):
            members = [sets[k] for k in range(len(sets)) if assign[k] == g]
            if members:
                c = Counter(x for m in members for x in m)
                new.append((pn, frozenset(x for x, v in c.items() if 2 * v > len(members))))
        pats = new
    assign = [nearest(k) for k in range(len(sets))]
    sizes = Counter(assign)
    keep = [g for g in range(len(pats)) if sizes[g] >= MIN_BARS]
    index = {g: i for i, g in enumerate(keep)}
    return [index.get(a, -1) for a in assign], [pats[g] for g in keep]


def line_music(starts, n16, name):
    """One drum's line of a bar (step 5): each hit to the next within its beat, rests between, written short."""
    toks, pos = [], 0
    for n, a in enumerate(starts):
        if a > pos:
            toks += [("r", d) for d in bass.pieces(pos, a - pos)]
        end = min(starts[n + 1] if n + 1 < len(starts) else n16, (a // 4 + 1) * 4)
        toks += [(name, d) for d in bass.pieces(a, end - a)]
        pos = end
    if pos < n16:
        toks += [("r", d) for d in bass.pieces(pos, n16 - pos)]
    out, last = [], (None, None)
    for i, (nm, d) in enumerate(toks):
        # a drum after itself is its length alone (`hh8 8 8`); a name keeps its length off when the length is the last one
        same = nm != "r" and nm == last[0]
        text = bass.DUR[d] if same else nm + ("" if i > 0 and d == last[1] else bass.DUR[d])
        out.append(text)
        last = (nm, d)
    return " ".join(out)


def lines_of(hitset, n16):
    """{drum: its line} for the drums struck (step 5)."""
    out = {}
    for drum, _ in DRUMS:
        starts = sorted(q for q, d in hitset if d == drum)
        if starts:
            out[drum] = line_music(starts, n16, drum)
    return out


def rest_bar(n16):
    return " ".join(f"r{bass.DUR[d]}" for d in bass.pieces(0, n16))


def letters(i):
    s = ""
    i += 1
    while i:
        i, r = divmod(i - 1, 26)
        s = chr(65 + r) + s
    return s


def read(flac, side):
    """{"grooves": [{"name", "phrases": [(phrase name, line)], "chart"}], "bars": [{"kit": the bar's text in the
    staff, "drums": {drum: the line as read}}]}."""
    r = rises(flac)
    bars = side["bars"]
    slots, where = bass.slots_of(bars)
    h = hits(r, slots[:-1])
    sets = [set() for _ in bars]
    for i, s in enumerate(h):
        k, q = where[i]
        sets[k] |= {(q, d) for d in s}
    sets = [frozenset(s) for s in sets]
    lens = [len(b["beats"]) * 4 for b in bars]
    assign, pats = grooves(sets, lens)
    named = []
    for g, (n, p) in enumerate(pats):
        tag = letters(g)
        ls = lines_of(p, n)
        phrases = [(f"{word}{tag}", ls[drum]) for drum, word in DRUMS if drum in ls]
        chart = f"drums {{ {' '.join(name for name, _ in phrases)} }}" if phrases else None
        named.append({"name": f"beat{tag}", "phrases": phrases, "chart": chart, "rest": rest_bar(n)})
    out = []
    for k in range(len(bars)):
        raw = lines_of(sets[k], lens[k])
        if assign[k] >= 0:
            g = named[assign[k]]
            kit = g["name"] if g["chart"] else g["rest"]
        elif raw:
            kit = "drums { " + " ".join("{ " + raw[d] + " }" for d, _ in DRUMS if d in raw) + " }"
        else:
            kit = rest_bar(lens[k])
        out.append({"kit": kit, "drums": raw})
    return {"grooves": [g for g in named if g["chart"]], "bars": out, "assign": assign, "patterns": pats, "sets": sets}


if __name__ == "__main__":
    side = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    res = read(sys.argv[1], side)
    for g in res["grooves"]:
        for name, line in g["phrases"]:
            print(f"phrase {name} {{ {line} | }}")
        print(f"phrase {g['name']} {{ {g['chart']} }}")
    for k, b in enumerate(res["bars"]):
        print(k + 1, b["kit"])
