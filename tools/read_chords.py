#!/usr/bin/env python3
"""The baseline reader: a recording to its tempo, bar lines and chords. Nothing here is learnt.

    .venv/Scripts/python tools/read_chords.py data/synth/seed1 --out scratch/read/seed1

For each .wav it writes <name>.lys (a Lily# chord chart) and <name>.dettato.json (every bar and
chord with its seconds, in the shape of gen_chords.py's truth). The steps:

1. beats: librosa's beat tracker on the onset strength, carried back to the start and on to the end;
2. bars: the count of beats in a bar and the beat a bar starts on are the pair under which the
   harmony changes most ON bar lines and least between them;
3. meter: from that count and how the beat divides (in two or in three);
4. chords: the bar's chroma against a template for each root and quality, the bass weighing in
   for the root; a bar is given two chords where its halves disagree;
5. key: Krumhansl's profiles on the whole recording, for the spelling.

This is the line to beat, written to be measured (tools/score_chords.py), not to be shipped.
"""
import argparse
import json
import sys
from pathlib import Path

import librosa
import numpy as np

SR = 22050
HOP = 512

QUALITIES = {
    "": [0, 4, 7], "m": [0, 3, 7], "7": [0, 4, 7, 10], "maj7": [0, 4, 7, 11],
    "m7": [0, 3, 7, 10], "sus4": [0, 5, 7], "dim": [0, 3, 6], "m7(b5)": [0, 3, 6, 10],
}
# what a quality has to earn before it is read: a bass that also plays the fifth makes the root and
# the fifth equally loud, which is the suspended chord on the fifth (D minor read as Asus4)
QUALITY_COST = {"": 0.0, "m": 0.0, "7": 0.02, "maj7": 0.02, "m7": 0.02, "sus4": 0.08, "dim": 0.04, "m7(b5)": 0.05}
BASS_WEIGHT = 0.3       # how much the bass at the start of a chord says which note is the root
SPLIT_MARGIN = 0.03     # two chords in a bar have to fit this much better than one
SLASH_RATIO = 2.0       # a bass note that is not the root has to be this much louder than it

MAJOR_PROFILE = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88]
MINOR_PROFILE = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17]
MAJOR_KEYS = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"]
MINOR_KEYS = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B"]
SHARP_ROOTS = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
FLAT_ROOTS = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"]
FLAT_MINORS = {"C", "G", "D", "F"}

TEMPLATES = []  # (root, quality, unit vector)
for _quality, _intervals in QUALITIES.items():
    for _root in range(12):
        _t = np.zeros(12)
        _t[[(_root + i) % 12 for i in _intervals]] = 1.0
        TEMPLATES.append((_root, _quality, _t / np.linalg.norm(_t)))


def unit(v):
    n = np.linalg.norm(v, axis=0)
    return v / np.where(n > 0, n, 1.0)


def beat_grid(oenv):
    """Beat positions in frames, from the first frame to the last, and the beat's length."""
    _, beats = librosa.beat.beat_track(onset_envelope=oenv, sr=SR, hop_length=HOP, trim=False)
    beats = np.asarray(beats, dtype=float)
    if len(beats) < 8:
        raise ValueError("too few beats found")
    period = float(np.median(np.diff(beats)))
    before, at = [], beats[0]
    while at - period > -0.5 * period:
        at -= period
        before.append(max(at, 0.0))
    after, at = [], beats[-1]
    while at + period < len(oenv):
        at += period
        after.append(at)
    return np.concatenate([before[::-1], beats, after]), period


def over(feature, a, b):
    a = int(round(a))
    b = max(a + 1, int(round(b)))
    return np.median(feature[:, a:b], axis=1)


TRIADS = np.array([t for _, q, t in TEMPLATES if q in ("", "m")])
BAR_RATIOS = (1, 1.5, 2, 3, 4, 6, 8)   # a bar, in tracked beats: the tracker may follow any level
BAR_SECONDS = (0.9, 4.0)               # bars shorter or longer than this are not tried
SAME_CHORD_COST = 0.2                  # what a bar line costs when the chord does not change at it


def lines_of(beats, ratio, phase):
    """Bar lines `ratio` beats apart from beat `phase` (both may fall between beats)."""
    index = np.arange(phase, len(beats) - 1 + 1e-9, ratio)
    return np.interp(index, np.arange(len(beats)), beats)


def find_bars(beats, period, chroma, bass, rms):
    """The bar lines under which each bar is one chord and the chord changes at the lines.

    A bar too long holds two chords and fits none; a bar too short repeats its chord in the
    next. The lines are tried at every half beat, for a tracker that follows the offbeats.
    """
    sums = np.concatenate([np.zeros((12, 1)), np.cumsum(chroma, axis=1)], axis=1)
    both = np.concatenate([np.zeros((24, 1)), np.cumsum(np.vstack([chroma, bass]), axis=1)], axis=1)
    loud = np.concatenate([[0.0], np.cumsum(rms[0])])
    quiet = 0.1 * float(np.median(rms[0]))
    last = chroma.shape[1]
    half = max(2, int(round(period / 2)))
    best = (-np.inf, None)
    for ratio in BAR_RATIOS:
        if not BAR_SECONDS[0] <= ratio * period * HOP / SR <= BAR_SECONDS[1]:
            continue
        # where the lines fall: on the beats at which the sound changes most. (How well the bars
        # fit a chord cannot say: a chord rings on into the next bar, so late lines always fit.)
        change, phase = -np.inf, 0.0
        for p in np.arange(0, ratio, 0.5):
            at = np.round(lines_of(beats, ratio, p)).astype(int)
            at = at[(at >= half) & (at + half <= last)]
            if len(at) < 4:
                continue
            before = unit(both[:, at] - both[:, at - half])
            after = unit(both[:, at + half] - both[:, at])
            c = float(np.mean(1.0 - np.sum(before * after, axis=0)))
            if c > change:
                change, phase = c, p
        lines = np.round(lines_of(beats, ratio, phase)).astype(int)
        a, b = lines[:-1], np.maximum(lines[1:], lines[:-1] + 1)
        sounding = (loud[b] - loud[a]) / (b - a) > quiet
        if sounding.sum() < 4:
            continue
        fits = TRIADS @ unit(sums[:, b] - sums[:, a])
        fit = fits.max(axis=0)[sounding].mean()
        label = fits.argmax(axis=0)
        pairs = sounding[1:] & sounding[:-1]
        same = (label[1:] == label[:-1])[pairs].mean() if pairs.any() else 0.0
        score = fit - SAME_CHORD_COST * same
        if score > best[0]:
            best = (score, lines_of(beats, ratio, phase))
    if best[1] is None:
        raise ValueError("no bar length fits")
    return best[1]


def find_meter(lines, oenv):
    """(time signature, the tempo's note, beats of that note in a bar) from where a bar's onsets fall.

    The bars are laid over each other in 24 steps. Quarters and eighths of 4/4 fall on steps that
    a bar in three never uses, and the other way round; in three, a beat in the middle of the bar
    is 6/8 and beats on its thirds are 3/4.
    """
    profile = np.zeros(24)
    for a, b in zip(lines[:-1], lines[1:]):
        for k in range(24):
            f = int(round(a + (b - a) * k / 24))
            if f < len(oenv):
                profile[k] += oenv[max(0, f - 1):f + 2].max()
    four = profile[[3, 6, 9, 15, 18, 21]].mean()
    three = profile[[4, 8, 16, 20]].mean()
    if four >= three:
        return "4/4", "4", 4
    if profile[12] > profile[[8, 16]].mean():
        return "6/8", "4.", 2
    return "3/4", "4", 3


def best_chord(chroma, bass):
    v, vb = unit(chroma), unit(bass)
    best = (-np.inf, 0, "")
    for root, quality, template in TEMPLATES:
        score = float(v @ template) + BASS_WEIGHT * float(vb[root])
        score -= QUALITY_COST[quality]
        if score > best[0]:
            best = (score, root, quality)
    score, root, quality = best
    low = int(np.argmax(vb))
    tones = [(root + i) % 12 for i in QUALITIES[quality]]
    slash = low if low != root and low in tones and vb[low] > SLASH_RATIO * vb[root] else None
    return score, root, quality, slash


def find_key(chroma):
    total = chroma.sum(axis=1)
    best = (-np.inf, 0, "major")
    for mode, profile in (("major", MAJOR_PROFILE), ("minor", MINOR_PROFILE)):
        for tonic in range(12):
            r = np.corrcoef(total, np.roll(profile, tonic))[0, 1]
            if r > best[0]:
                best = (r, tonic, mode)
    return best[1], best[2]


def read(path):
    y, _ = librosa.load(path, sr=SR, mono=True)
    harmonic = librosa.effects.harmonic(y, margin=4)
    oenv = librosa.onset.onset_strength(y=y, sr=SR, hop_length=HOP)
    chroma = librosa.feature.chroma_cqt(y=harmonic, sr=SR, hop_length=HOP,
                                        fmin=librosa.note_to_hz("C2"), n_octaves=5)
    bass = librosa.feature.chroma_cqt(y=harmonic, sr=SR, hop_length=HOP,
                                      fmin=librosa.note_to_hz("C1"), n_octaves=2)
    rms = librosa.feature.rms(y=y, hop_length=HOP)
    beats, period = beat_grid(oenv)
    lines = find_bars(beats, period, chroma, bass, rms)
    time, tempo_note, tempo_beats = find_meter(lines, oenv)
    loud = np.array([float(over(rms, a, b)[0]) for a, b in zip(lines[:-1], lines[1:])])
    keep = len(loud)
    while keep > 1 and loud[keep - 1] < 0.1 * np.median(loud):  # the ring-out is not a bar
        keep -= 1
    lines = lines[:keep + 1]

    seconds = lambda frame: round(float(frame) * HOP / SR, 4)
    tonic, mode = find_key(chroma[:, :int(lines[-1])])
    tonic_name = (MAJOR_KEYS if mode == "major" else MINOR_KEYS)[tonic]
    flats = "b" in tonic_name or (tonic_name == "F" if mode == "major" else tonic_name in FLAT_MINORS)
    roots = FLAT_ROOTS if flats else SHARP_ROOTS

    def symbol(root, quality, slash):
        return roots[root] + quality + ("/" + roots[slash] if slash is not None else "")

    bars = []
    for n, (a, b) in enumerate(zip(lines[:-1], lines[1:])):
        whole = best_chord(over(chroma, a, b), over(bass, a, a + (b - a) / 4))
        chords = [(a, b, whole)]
        if time != "3/4":
            mid = (a + b) / 2
            first = best_chord(over(chroma, a, mid), over(bass, a, a + (mid - a) / 2))
            second = best_chord(over(chroma, mid, b), over(bass, mid, mid + (b - mid) / 2))
            differ = first[1:3] != second[1:3]
            if differ and (first[0] + second[0]) / 2 > whole[0] + SPLIT_MARGIN:
                chords = [(a, mid, first), (mid, b, second)]
        bars.append({
            "bar": n + 1, "start": seconds(a), "end": seconds(b),
            "chords": [{"start": seconds(s), "end": seconds(e), "symbol": symbol(*c[1:]),
                        "fit": round(c[0], 3)} for s, e, c in chords],
        })
    bar_seconds = float(np.median(np.diff(lines))) * HOP / SR
    return {
        "audio": Path(path).name,
        "tempo": {"note": tempo_note, "bpm": int(round(60.0 * tempo_beats / bar_seconds))},
        "time": time,
        "key": f"{tonic_name} {mode}",
        "bars": bars,
    }


def lys(result, title):
    tonic, mode = result["key"].split()
    acc = {"": "", "#": "is", "b": "es"}[tonic[1:]]
    rows = [" | ".join(" ".join(c["symbol"] for c in bar["chords"]) for bar in result["bars"][i:i + 4])
            for i in range(0, len(result["bars"]), 4)]
    return "\n".join([
        f"// read from {result['audio']} by tools/read_chords.py",
        f'title "{title}"',
        f"tempo {result['tempo']['note']} = {result['tempo']['bpm']}",
        f"time {result['time']}",
        f"key {tonic[0].lower()}{acc} {mode}",
        "",
        "chords harmony {",
        *[f"  {row} |" for row in rows],
        "}",
        "",
        "score { chords harmony }",
        "",
    ])


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("input", type=Path, help="a .wav, or a folder of them")
    p.add_argument("--out", type=Path, required=True)
    args = p.parse_args()
    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    args.out.mkdir(parents=True, exist_ok=True)
    failed = 0
    for path in files:
        try:
            result = read(path)
        except Exception as e:  # one recording that cannot be read does not stop the rest
            failed += 1
            print(f"{path.name}: {e}", file=sys.stderr)
            continue
        (args.out / (path.stem + ".dettato.json")).write_text(
            json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
        (args.out / (path.stem + ".lys")).write_text(lys(result, path.stem), encoding="utf-8", newline="\n")
        print(f"{path.name}: {result['key']}, {result['time']}, {result['tempo']['bpm']}, "
              f"{len(result['bars'])} bars")
    if failed:
        sys.exit(f"{failed} of {len(files)} recordings were not read")


if __name__ == "__main__":
    main()
