"""The bass part read into notes and written as a bass staff (M5, 2026-11-23): the Python side that the .NET reader
follows (src/Orecchiante/Bass.cs; tools/bass_check.py compares them).

From the bass part Orecchiante separated (<name>.parts/bass.flac, mono, at the recording's seconds):
1. at 22050 Hz: librosa's pYIN (B0 to C4, frames of 4096 every 256 samples) and the onset strength of 64 mel bands up to
   2000 Hz (the same frames); the onsets librosa.onset.onset_detect picks from it (normalised, backtracked).
2. notes: the pitch rounded to semitones after the song's tuning (the circular mean of the voiced frames' distance from
   the nearest semitone); a note starts where the voiced pitch changes and stays STEADY frames (0.12 s), or where an
   onset falls while the pitch holds (a note struck again, 3 frames or more after its start); notes under 60 ms dropped.
3. the grid: each beat of the chart's bars (the file beside it, `beats`) cut into four 16ths. A note goes to the nearest
   16th, but to a 16th off the eighths only when it is within PULL of it (else the nearest eighth); it lasts to its
   rounded end, or to the next note when the gap is a 16th or less; of two notes on one 16th the longer stays.
4. ties: a note held over a bar line is tied there when the onset strength at the bar line (its maximum within 35 ms)
   is under TIE times the strength at the note's start; else the next bar strikes it again.
5. writing: a bar's notes and rests in 16ths, as the bass player reads them (an octave above what sounds): within a beat
   up to its end; from a beat, a whole bar from its start, a half or dotted half from a half bar, a dotted quarter or
   less anywhere; the rest tied.
6. a bass is heard (else no staff): the bass part's mean square over the recording's (the six parts summed), both at
   22050 Hz, is HEARD_DB or more (2026-11-23: 165 songs with a bass -17.8 dB and up, the same 8 songs mixed without their
   bass and separated again -24.6 dB and down; scratch/bass/nobass/).

Measured on the owner's bass parts (scratch/bass/grid.py, docs/HANDOFF.md): dev 85 songs, onsets on the grid 0.789,
pitch 0.684, with lengths 0.466.
  .venv/Scripts/python tools/bass.py <bass.flac> <x.orecchiante.json> [--out <bars.json>]   (each bar's music)
  .venv/Scripts/python tools/bass.py --notes <bass.flac> <notes.json>                     (the notes in seconds)
"""
import json, sys
from pathlib import Path
import numpy as np

SR, HOP, FRAME = 22050, 256, 4096
FMIN, FMAX = 30.867706328507756, 261.6255653005986      # B0, C4 (librosa.note_to_hz)
STEADY, MIN_NOTE, AGAIN = 10, 0.06, 3
PULL, TIE, TIE_WIN = 0.25, 0.4, 0.035
HEARD_DB = -21.0
PARTS = ("drums", "bass", "other", "vocals", "guitar", "piano")
NAMES_SHARP = ["c", "cis", "d", "dis", "e", "f", "fis", "g", "gis", "a", "ais", "b"]
NAMES_FLAT = ["c", "des", "d", "ees", "e", "f", "ges", "g", "aes", "a", "bes", "b"]
FLAT_KEYS = {"f", "bes", "ees", "aes", "des", "ges", "ces"}
FLAT_MINOR = {"d", "g", "c", "f", "bes", "ees", "aes"}
DUR = {16: "1", 12: "2.", 8: "2", 6: "4.", 4: "4", 3: "8.", 2: "8", 1: "16"}


def hear(flac):
    """(f0, voiced, onset strength, onset frames) of the bass part, all on frames of HOP at SR."""
    import librosa, soundfile as sf
    y, sr = sf.read(str(flac), dtype="float32", always_2d=True)
    y = librosa.resample(y.mean(axis=1), orig_sr=sr, target_sr=SR)
    f0, voiced, _ = librosa.pyin(y, fmin=FMIN, fmax=FMAX, sr=SR, frame_length=FRAME, hop_length=HOP)
    env = librosa.onset.onset_strength(y=y, sr=SR, hop_length=HOP, fmax=2000, n_mels=64)
    on = librosa.onset.onset_detect(onset_envelope=env, sr=SR, hop_length=HOP, backtrack=True, units="frames")
    return f0, voiced, env, on


def notes_of(f0, voiced, on):
    """[[on s, off s, midi], ...] (step 2)."""
    m = 69 + 12 * np.log2(np.nan_to_num(f0, nan=1.0) / 440)
    tune = 0.0
    if voiced.any():
        d = m[voiced] - np.round(m[voiced])
        tune = float(np.angle(np.mean(np.exp(2j * np.pi * d))) / (2 * np.pi))
    midi = np.where(voiced, np.round(m - tune), -1).astype(int)
    on = set(int(x) for x in on)
    out, cur, start = [], -1, 0
    for i in range(len(midi)):
        p = midi[i]
        steady = p >= 0 and bool(np.all(midi[i:i + STEADY] == p))
        if p != cur and (p < 0 or steady):
            if cur >= 0:
                out.append([start, i, cur])
            cur, start = p, i
        elif p == cur and p >= 0 and i in on and i - start >= AGAIN:
            out.append([start, i, cur])
            start = i
    if cur >= 0:
        out.append([start, len(midi), cur])
    t = HOP / SR
    return [[a * t, b * t, int(p)] for a, b, p in out if (b - a) * t >= MIN_NOTE]


def slots_of(bars):
    """Every 16th of the bars in order: (times incl. the last bar's end, (bar, 16th) of each, bar line indices)."""
    slots, where = [], []
    for k, b in enumerate(bars):
        beats = list(b["beats"]) + [b["end"]]
        for j in range(len(beats) - 1):
            for q in range(4):
                slots.append(beats[j] + (beats[j + 1] - beats[j]) * q / 4)
                where.append((k, j * 4 + q))
    slots.append(bars[-1]["end"])
    where.append((len(bars), 0))
    return np.array(slots), where


def strength_at(env, slots):
    """The onset strength's maximum within TIE_WIN of each 16th."""
    ft = HOP / SR
    w = int(round(TIE_WIN / ft))
    c = np.round(slots / ft).astype(int)
    return np.array([env[max(x - w, 0):x + w + 1].max() if 0 <= x - w < len(env) else 0.0 for x in c])


def nearest(slots, t):
    i = int(np.clip(np.searchsorted(slots, t), 1, len(slots) - 1))
    return i - 1 if t - slots[i - 1] < slots[i] - t else i


def round_onto(slots, notes, pull=PULL):
    """Step 3: [(16th, end 16th, midi)]. pull 0: every note on an eighth (tools/melody.py)."""
    out = {}
    for a, b, p in notes:
        i = nearest(slots, a)
        if i % 2 and abs(a - slots[i]) > pull * (slots[min(i + 1, len(slots) - 1)] - slots[i]):
            i = i - 1 if a < slots[i] or i + 1 >= len(slots) else i + 1
        e = max(nearest(slots, b), i + 1)
        if i not in out or out[i][0] - i < e - i:
            out[i] = (e, p)
    keys = sorted(out)
    res = []
    for n, i in enumerate(keys):
        e, p = out[i]
        if n + 1 < len(keys):
            nxt = keys[n + 1]
            if nxt - e <= 1:
                e = nxt
            e = min(e, nxt)
        res.append((i, e, p))
    return res


SIX = (0, 2, 3, 4)  # 6/8: the grid's four 16ths of a dotted quarter, among its six (the 8ths nearest the 2nd and 4th)


def written(q, six=False):
    """A place on the grid as a 16th of the bar: itself, or in 6/8 among the dotted quarter's six."""
    return q // 4 * 6 + SIX[q % 4] if six else q


def pieces(pos, length, beat16=4):
    """beat16: the beat in 16ths, 4, or 6 in 6/8 (a note within its dotted quarter, the bar's 2. whole)."""
    out = []
    while length > 0:
        if pos % beat16:
            d = max(x for x in DUR if x <= min(length, beat16 - pos % beat16))
        elif beat16 == 6:
            d = max(x for x in DUR if x <= length and (x <= 6 or (x == 12 and pos % 12 == 0)))
        else:
            d = max(x for x in DUR if x <= length and (x <= 6 or (x in (8, 12) and pos % 8 == 0) or (x == 16 and pos == 0)))
        out.append(d)
        pos += d
        length -= d
    return out


def pitch_name(midi, names, up=12):
    """up: written this many semitones above the sound (12: the bass player's octave; 0: the voice)."""
    w = midi + up
    o = w // 12 - 4
    return names[w % 12] + ("'" * o if o > 0 else "," * -o)


def names_for(key):
    """The spelling of the file's key ("Bb major"): flats in the keys written with flats."""
    tonic, mode = (key.split() + ["major"])[:2] if key else ("C", "major")
    name = tonic[0].lower() + {"#": "is", "b": "es"}.get(tonic[1:], "")
    return NAMES_FLAT if (name in FLAT_KEYS if mode == "major" else name in FLAT_MINOR) else NAMES_SHARP


def bar_music(notes, n16, names, up=12, six=False):
    """notes: [(start, end, midi, tied on)] on the bar's grid -> the bar's music (step 5). six: 6/8, the grid's places
    written among the dotted quarter's six 16ths (SIX), a bar 2. long (until 0.18.0 a half note)."""
    toks, pos, beat, end = [], 0, 6 if six else 4, written(n16, six)
    for a, b, p, on in notes:
        a, b = written(a, six), written(b, six)
        if a > pos:
            toks += [f"r{DUR[d]}" for d in pieces(pos, a - pos, beat)]
        ds = pieces(a, b - a, beat)
        nm = pitch_name(p, names, up)
        toks += [f"{nm}{DUR[d]}" + ("~" if k < len(ds) - 1 or on else "") for k, d in enumerate(ds)]
        pos = b
    if pos < end:
        toks += [f"r{DUR[d]}" for d in pieces(pos, end - pos, beat)]
    return " ".join(toks)


def heard(parts_folder, part="bass", heard_db=HEARD_DB):
    """Step 6, from the folder of the six parts (their sum is the recording)."""
    import librosa, soundfile as sf
    total, b = None, None
    for name in PARTS:
        y, sr = sf.read(str(Path(parts_folder) / f"{name}.flac"), dtype="float32", always_2d=True)
        y = librosa.resample(y.mean(axis=1), orig_sr=sr, target_sr=SR).astype(np.float64)
        total = y if total is None else total[:len(y)] + y[:len(total)]
        if name == part:
            b = y
    return 10 * np.log10((b ** 2).mean() / (total ** 2).mean() + 1e-30) >= heard_db


def read(flac, side, key=None):
    """Each bar's music of the file beside the chart (`side`, parsed), or None when no bass is heard (step 6)."""
    if not heard(Path(flac).parent):
        return None
    f0, voiced, env, on = hear(flac)
    bars = side["bars"]
    notes = [n for n in notes_of(f0, voiced, on) if bars[0]["start"] - 0.05 <= n[0] < bars[-1]["end"]]
    slots, where = slots_of(bars)
    att = strength_at(env, slots)
    lens = [len(b["beats"]) * 4 for b in bars]
    per_bar = [[] for _ in bars]
    for i, e, p in round_onto(slots, notes):
        k, q = where[i]
        i0 = i
        while e > i and k < len(bars):
            take = min(lens[k] - q, e - i)
            on_ = e - i > take and k + 1 < len(bars) and att[i + take] < TIE * att[i0]
            per_bar[k].append((q, q + take, p, on_))
            i += take
            k, q = k + 1, 0
    names = names_for(key or side.get("key"))
    return [bar_music(per_bar[k], lens[k], names, six=side.get("time") == "6/8") for k in range(len(bars))]


def main():
    if sys.argv[1] == "--notes":
        f0, voiced, env, on = hear(sys.argv[2])
        Path(sys.argv[3]).write_text(json.dumps(notes_of(f0, voiced, on)), encoding="utf-8")
        return
    side = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    music = read(sys.argv[1], side)
    text = json.dumps(music, ensure_ascii=False, indent=0)
    if "--out" in sys.argv:
        Path(sys.argv[sys.argv.index("--out") + 1]).write_text(text, encoding="utf-8")
    else:
        print(text)


if __name__ == "__main__":
    main()
