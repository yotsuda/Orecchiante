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
5. writing (0.18.0, Lily#'s `steps`, Orecchiante's request 24): a groove as a one-bar phrase of a steps chart,
   `phrase beatA { steps { hh "x.x. x.x. x.x. x.x." sn ".... x... .... x..." bd "x... ..x. x.x. ...." } }`, a row a
   drum struck (`hh`, `sn`, `bd`; a drum it never strikes left out), a character a 16th (`x` struck, `.` not), a beat
   a run of four; a bar near no groove the same chart inline. In 6/8 the beat is the dotted quarter (`steps 4.`) of six
   16ths, the grid's four put on 0, 2, 3 and 4 of them (SIX: the 8ths fall nearest the second and fourth). A bar of no
   hits is rests. In a section, a bar the same as the one before is `%`. Each bar's `drums` in the file beside the
   chart: {drum: its row}.
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
DRUMS = ("hh", "sn", "bd")
SIX = (0, 2, 3, 4)          # 6/8: the grid's four 16ths of a dotted quarter, among its six (bass.SIX)

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


def step_row(starts, n16, six=False):
    """One drum's row of a bar (step 5): a character a 16th, a beat a run of four (six in 6/8)."""
    beats = [["."] * (6 if six else 4) for _ in range(n16 // 4)]
    for q in starts:
        beats[q // 4][SIX[q % 4] if six else q % 4] = "x"
    return " ".join("".join(b) for b in beats)


def lines_of(hitset, n16, six=False):
    """{drum: its row} for the drums struck (step 5)."""
    out = {}
    for drum in DRUMS:
        starts = sorted(q for q, d in hitset if d == drum)
        if starts:
            out[drum] = step_row(starts, n16, six)
    return out


def chart_of(rows, six=False):
    """The steps chart of a bar's rows (step 5)."""
    return f"steps{' 4.' if six else ''} {{ " + " ".join(f'{d} "{rows[d]}"' for d in DRUMS if d in rows) + " }"


def rest_bar(n16, six=False):
    return bass.bar_music([], n16, None, six=six)


def letters(i):
    s = ""
    i += 1
    while i:
        i, r = divmod(i - 1, 26)
        s = chr(65 + r) + s
    return s


def read(flac, side):
    """{"grooves": [{"name", "chart"}], "bars": [{"kit": the bar's text in the staff, "drums": {drum: the row as
    read}}]}."""
    r = rises(flac)
    bars = side["bars"]
    six = side.get("time") == "6/8"
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
        ls = lines_of(p, n, six)
        named.append({"name": f"beat{tag}", "chart": chart_of(ls, six) if ls else None, "rest": rest_bar(n, six)})
    out = []
    for k in range(len(bars)):
        raw = lines_of(sets[k], lens[k], six)
        if assign[k] >= 0:
            g = named[assign[k]]
            kit = g["name"] if g["chart"] else g["rest"]
        elif raw:
            kit = chart_of(raw, six)
        else:
            kit = rest_bar(lens[k], six)
        out.append({"kit": kit, "drums": raw})
    return {"grooves": [g for g in named if g["chart"]], "bars": out, "assign": assign, "patterns": pats, "sets": sets}


if __name__ == "__main__":
    side = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    res = read(sys.argv[1], side)
    for g in res["grooves"]:
        print(f"phrase {g['name']} {{ {g['chart']} }}")
    for k, b in enumerate(res["bars"]):
        print(k + 1, b["kit"])
