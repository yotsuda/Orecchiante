"""The sung melody read into notes and written as a voice staff (2026-11-26, Shosa's request 1: "the melody of the
singing in the .lys"): the Python side that the .NET reader follows (src/Orecchiante/Melody.cs; tools/melody_check.py
compares them). Chosen on RWC-P 1-50 against the aligned MIDI's sung notes (scratch/vocal/read.py; docs/HANDOFF.md).

From the vocal part Orecchiante separated (<name>.parts/vocals.flac, mono, at the recording's seconds):
1. at 22050 Hz: librosa's pYIN (C2 to C6, frames of 2048 every 256 samples); the onset strength of 64 mel bands up to
   8000 Hz (the same frames) and the onsets librosa.onset.onset_detect picks from it (delta DELTA, backtracked); the
   loudness (librosa.feature.rms, frames of 2048), in dB.
2. notes: the pitch rounded to semitones after the voice's tuning (the circular mean of the voiced frames' distance from
   the nearest semitone), then the median of MED frames within each voiced run; a note starts where the voiced pitch
   changes and stays STEADY frames, or where an onset falls while the pitch holds (AGAIN frames or more after its start);
   notes under MIN_NOTE dropped, and notes quieter than the song's sung notes (their mean dB under the 75th percentile
   of all notes' by more than QUIET: backing voices, reverb, the bleed of other parts); all moved by LAG.
3. the grid: each beat of the chart's bars cut into four 16ths; a note to the nearest eighth (16ths off the eighths
   cost more than they gain in a voice), to its rounded end, or to the next note when the gap is a 16th or less; of
   two notes on one eighth the longer stays.
4. a note held over a bar line is tied there (a voice holds its syllable; the notes are already cut where it sings again).
5. writing: as tools/bass.py's, at the pitch that sounds; the part a tenor (treble clef an octave down) or a soprano,
   whichever has fewer notes out of its range; one syllable "ら" a note for whoever reads the singing (Shosa).
6. a voice is heard (else no staff): the vocal part's mean square within HEARD_DB of the recording's. An instrumental's
   lead (strings, synth, the guitar's tune) is often separated as the voice and read as the melody (2026-11-26:
   Viva La Vida -24.5 dB, Top Gun Anthem -18.2, a string quartet -19.4; parts with nothing in them -31.5 and down).

Measured on RWC-P's aligned MIDI (scratch/vocal/read.py, on Beat This!'s beats): dev 1-50 onsets on the grid F 0.741
(recall 0.74, precision 0.74), test 51-100 0.723.
  .venv/Scripts/python tools/melody.py <vocals.flac> <x.orecchiante.json>      (each bar's music)
"""
import json, sys
from pathlib import Path
import numpy as np

SR, HOP, FRAME = 22050, 256, 2048
FMIN, FMAX = 65.40639132514966, 1046.5022612023945      # C2, C6
STEADY, AGAIN, MIN_NOTE, MED = 6, 4, 0.08, 5
DELTA, QUIET, LAG = 0.1, 8.0, -0.02
HEARD_DB = -30.0

sys.path.insert(0, str(Path(__file__).parent))
import bass  # noqa: E402  (the grid, the lengths, the spelling)


def hear(flac):
    """(f0, voiced, onset frames, loudness in dB) of the vocal part, on frames of HOP at SR."""
    import librosa, soundfile as sf
    y, sr = sf.read(str(flac), dtype="float32", always_2d=True)
    y = librosa.resample(y.mean(axis=1), orig_sr=sr, target_sr=SR)
    f0, voiced, _ = librosa.pyin(y, fmin=FMIN, fmax=FMAX, sr=SR, frame_length=FRAME, hop_length=HOP)
    env = librosa.onset.onset_strength(y=y, sr=SR, hop_length=HOP, fmax=8000, n_mels=64)
    on = librosa.onset.onset_detect(onset_envelope=env, sr=SR, hop_length=HOP, backtrack=True, units="frames", delta=DELTA)
    rms = librosa.feature.rms(y=y, frame_length=FRAME, hop_length=HOP)[0]
    return f0, voiced, on, 20 * np.log10(rms + 1e-6)


def median_runs(midi):
    """The median of MED frames within each voiced run (scipy's median_filter, mode nearest)."""
    out, h, i = midi.copy(), MED // 2, 0
    while i < len(midi):
        if midi[i] < 0:
            i += 1
            continue
        j = i
        while j < len(midi) and midi[j] >= 0:
            j += 1
        run = midi[i:j]
        for k in range(len(run)):
            w = [run[min(max(x, 0), len(run) - 1)] for x in range(k - h, k + h + 1)]
            out[i + k] = int(np.median(w))
        i = j
    return out


def notes_of(f0, voiced, on, db):
    """[[on s, off s, midi], ...] (step 2)."""
    m = 69 + 12 * np.log2(np.nan_to_num(f0, nan=1.0) / 440)
    tune = 0.0
    if voiced.any():
        d = m[voiced] - np.round(m[voiced])
        tune = float(np.angle(np.mean(np.exp(2j * np.pi * d))) / (2 * np.pi))
    midi = median_runs(np.where(voiced, np.round(m - tune), -1).astype(int))
    on = set(int(x) for x in on)
    res, cur, start = [], -1, 0
    for i in range(len(midi)):
        p = midi[i]
        steady = p >= 0 and bool(np.all(midi[i:i + STEADY] == p))
        if p != cur and (p < 0 or steady):
            if cur >= 0:
                res.append([start, i, cur])
            cur, start = p, i
        elif p == cur and p >= 0 and i in on and i - start >= AGAIN:
            res.append([start, i, cur])
            start = i
    if cur >= 0:
        res.append([start, len(midi), cur])
    t = HOP / SR
    res = [n for n in res if (n[1] - n[0]) * t >= MIN_NOTE]
    if res:
        loud = np.array([db[a:b].mean() for a, b, _ in res])
        floor = np.percentile(loud, 75) - QUIET
        res = [n for n, x in zip(res, loud) if x >= floor]
    return [[a * t + LAG, b * t + LAG, int(p)] for a, b, p in res]


def instrument(notes):
    """The voice with fewer notes out of its range (Lily#'s: tenor C3-C5, soprano C4-C6), tenor on a tie."""
    out = lambda lo, hi: sum(1 for _, _, p in notes if p < lo or p > hi)
    return "soprano" if out(60, 84) < out(48, 72) else "tenor"


def read(flac, side, key=None):
    """(the instrument, each bar's music) of the file beside the chart, or None when no voice is heard (step 6)."""
    if not bass.heard(Path(flac).parent, "vocals", HEARD_DB):
        return None
    f0, voiced, on, db = hear(flac)
    bars = side["bars"]
    notes = [n for n in notes_of(f0, voiced, on, db) if bars[0]["start"] - 0.05 <= n[0] < bars[-1]["end"]]
    slots, where = bass.slots_of(bars)
    lens = [len(b["beats"]) * 4 for b in bars]
    per_bar = [[] for _ in bars]
    for i, e, p in bass.round_onto(slots, notes, pull=0.0):
        k, q = where[i]
        while e > i and k < len(bars):
            take = min(lens[k] - q, e - i)
            per_bar[k].append((q, q + take, p, e - i > take and k + 1 < len(bars)))
            i += take
            k, q = k + 1, 0
    names = bass.names_for(key or side.get("key"))
    return instrument(notes), [bass.bar_music(per_bar[k], lens[k], names, up=0) for k in range(len(bars))]


if __name__ == "__main__":
    side = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    res = read(sys.argv[1], side)
    print(json.dumps(res, ensure_ascii=False, indent=0))
