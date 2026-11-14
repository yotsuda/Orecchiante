#!/usr/bin/env python3
"""The baseline reader: a recording to its tempo, bar lines and chords. Nothing here is learnt.

    .venv/Scripts/python tools/read_chords.py data/synth/seed1 --out scratch/read/seed1

For each .wav it writes <name>.lys (a Lily# chord chart) and <name>.dettato.json (every bar and
chord with its seconds, in the shape of gen_chords.py's truth). The steps:

1. beats: librosa's beat tracker on the onset strength, carried back to the start and on to the
   end, and halved: the half beat is the unit everything after is counted in;
2. bars and chords, together: the path through (chord, place in the bar) that fits the chroma of
   every unit best, where a chord may change freely at a bar line, at a price in the middle of
   a bar, and at a high price elsewhere; a bar line the chord does not change at costs a little.
   This is done for each length a bar may have, and the best path over all lengths is kept;
3. meter: from where the onsets fall inside the bars (in fours, in threes, or in two threes);
4. key: the one whose chords the reading is made of, for the spelling.

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
QUALITY_COST = {"": 0.0, "m": 0.0, "7": 0.02, "maj7": 0.05, "m7": 0.0, "sus4": 0.08, "dim": 0.04, "m7(b5)": 0.05}
BASS_WEIGHT = 0.3       # how much the bass at the start of a bar (or its half) says which note is the root
# The harmonic part of a recording is what stays over this many frames (0: the recording as it is).
# Measured 2026-11-14: 31 and 9 read alike, and the recording as it is reads worse (the drums).
HARMONIC_FRAMES = 31
BEAT_LAG = 2.0          # frames by which the onset strength peaks after the sound begins
TIGHTNESS = 100         # how strictly the beat tracker keeps its tempo (librosa's own value is 100)
TEMPO_RATIOS = (2 / 3, 3 / 2)   # the other beats tried: half as fast again, and a third slower
SLIDE = 0.07            # a beat this much longer or shorter than the rest is the tracker changing its mind
SLASH_RATIO = 2.0       # a bass note that is not the root has to be this much louder than it

# The prices of the path, in the unit of the fit: one second of a chord that fits perfectly is 1.
STAY_COST = 0.20        # a bar line the chord does not change at
MID_COST = 0.50         # a change in the middle of a bar
ELSE_COST = 1.0         # a change anywhere else
CHANGE_GAIN = 0.0       # a change earns this times how much the sound changes there (0..1);
                        # measured 2026-11-14: 0.3 and 0.8 read no better than none
# A learnt model of what a chord sounds like (tools/train_chords.py), in place of the templates:
# its log probability of each chord, cut off at LOG_FLOOR and scaled to stand beside the prices.
MODEL = None
EMIT_SCALE = 0.1
LOG_FLOOR = -8.0
STRANGE = 0.5           # with more of its half beats doubted than this, the bars are not the model's to find
RULES = {"STAY_COST": STAY_COST, "BASS_WEIGHT": BASS_WEIGHT}   # the prices of the reader with nothing learnt
DOUBT = 0.7             # a chord the model is less sure of than this is marked for a look
BAR_UNITS = (2, 3, 4, 6, 8, 12, 16)   # a bar, in half beats: the tracker may follow any level
BAR_SECONDS = (0.9, 4.0)              # bars shorter or longer than this are not tried
HINT_TOLERANCE = 0.15   # how far from the tempo told a bar's beats may be

MAJOR_PROFILE = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88]
MINOR_PROFILE = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17]
MAJOR_KEYS = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"]
MINOR_KEYS = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B"]
SHARP_ROOTS = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
FLAT_ROOTS = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"]
FLAT_MINORS = {"C", "G", "D", "F"}

CHORDS = []  # (root, quality)
_rows = []
for _quality, _intervals in QUALITIES.items():
    for _root in range(12):
        _t = np.zeros(12)
        _t[[(_root + i) % 12 for i in _intervals]] = 1.0
        CHORDS.append((_root, _quality))
        _rows.append(_t / np.linalg.norm(_t))
TEMPLATES = np.array(_rows)                                    # (chords, 12)
ALIKE = TEMPLATES @ TEMPLATES.T                                # (chords, chords): 1 for the same chord
ROOTS = np.array([root for root, _ in CHORDS])
COSTS = np.array([QUALITY_COST[quality] for _, quality in CHORDS])
_KIND = {"": "maj", "7": "maj", "maj7": "maj", "m": "min", "m7": "min", "sus4": "sus", "dim": "dim", "m7(b5)": "dim"}
SAME = np.array([[a == b and _KIND[p] == _KIND[q] for b, q in CHORDS] for a, p in CHORDS])   # the same root and family


def use_model(path):
    """Reads with what train_chords.py learnt, and with the prices that go with it.

    The model has heard the bass, so the bass no longer weighs in apart; and it is surer of a
    chord than a template is, so a bar line with no change has to cost more to stand beside it
    (chosen on data/synth/seed1, 2026-11-14).
    """
    global MODEL, STAY_COST, BASS_WEIGHT, EMIT_SCALE
    import joblib
    MODEL = joblib.load(path)
    STAY_COST, BASS_WEIGHT, EMIT_SCALE = 0.4, 0.0, 0.2


def unit(v):
    n = np.linalg.norm(v, axis=0)
    return v / np.where(n > 0, n, 1.0)


def beat_grid(oenv):
    """Beat positions in frames, from the first frame to the last, and the beat's length."""
    def track(bpm=None):
        _, found = librosa.beat.beat_track(onset_envelope=oenv, sr=SR, hop_length=HOP, trim=False,
                                           tightness=TIGHTNESS, bpm=bpm)
        found = np.asarray(found, dtype=float)
        if len(found) < 8:
            return found, 0.0, -np.inf
        length = float(np.median(np.diff(found)))
        # how much of the sound begins on its half beats
        at = np.clip(np.round(half_beats(found, length)).astype(int), 1, len(oenv) - 2)
        return found, length, float(np.mean(np.maximum(np.maximum(oenv[at - 1], oenv[at]), oenv[at + 1])))

    beats, period, caught = track()
    if len(beats) < 8:
        raise ValueError("too few beats found")
    # the tracker may take three notes for two, or two for three: the beat a half faster or a
    # third slower is tried, and kept if more of the sound begins on its half beats
    for ratio in TEMPO_RATIOS:
        other = track(60.0 * SR / HOP / (period * ratio))
        if other[2] > caught:
            beats, period, caught = other
    before, at = [], beats[0]
    while at - period > -0.5 * period:
        at -= period
        before.append(max(at, 0.0))
    after, at = [], beats[-1]
    while at + period < len(oenv):
        at += period
        after.append(at)
    return np.concatenate([before[::-1], beats, after]), period


def half_beats(beats, period):
    """The edges of the half beats, in frames.

    Where the accompaniment moves to the off-beats the tracker follows it, sliding there over a
    few beats that are each a little long or short. Those beats together are taken as the whole
    number of half beats nearest their length (an odd number, after such a slide), so the half
    beats go on in step and a bar line after the slide falls where it did before.
    """
    steady = np.abs(np.diff(beats) / period - 1.0) <= SLIDE
    edges, i = [beats[0]], 0
    while i < len(beats) - 1:
        j, n = i + 1, 2
        if not steady[i]:
            while j < len(beats) - 1 and not steady[j]:
                j += 1
            n = max(1, int(round((beats[j] - beats[i]) / (period / 2))))
        edges += [beats[i] + (beats[j] - beats[i]) * k / n for k in range(1, n + 1)]
        i = j
    return np.array(edges)


def over(feature, a, b, how=np.mean):
    a = int(round(a))
    b = max(a + 1, int(round(b)))
    return how(feature[:, a:b], axis=1)


def decode(emit, bass, change, length, stay=None, first=None):
    """The best path of (chord, place in a bar of `length` units) and what it is worth.

    emit[t, k]: how well chord k fits unit t, times the unit's seconds; bass[t]: the bass chroma
    of unit t, weighted the same way; change[t]: how much the sound changes into unit t.
    """
    count, chords = emit.shape
    half = length // 2 if length % 2 == 0 else None
    every = np.arange(chords)

    def local(t, place):
        heard = bass[max(t - place, 0)][ROOTS]
        if half is not None and place >= half:
            heard = np.maximum(heard, bass[max(t - (place - half), 0)][ROOTS])
        return emit[t] + heard

    stay = STAY_COST if stay is None else stay
    worth = np.stack([local(0, place) for place in range(length)])
    if first is not None:   # the place in the bar the first unit is known to have
        worth[np.arange(length) != first] = -np.inf
    back = np.zeros((count, length, chords), dtype=np.int16)
    for t in range(1, count):
        new = np.empty_like(worth)
        for place in range(length):
            before = worth[(place - 1) % length]
            order = np.argsort(before)
            first, second = order[-1], order[-2]
            other = np.where(every == first, second, first)     # the best chord that is not this one
            moved = before[other] + CHANGE_GAIN * change[t]
            stayed = before
            if place == 0:
                # at a bar line a move costs as much as the two chords are alike: staying costs
                # all of it, and so nearly does a move to the same notes under another name
                # (C to Am7), or every line would be dodged that way
                gain = CHANGE_GAIN * change[t] * (1.0 - np.eye(chords))
                reach = before[:, None] - stay * ALIKE + gain
                source = np.argmax(reach, axis=0)
                new[place] = reach[source, every] + local(t, place)
                back[t, place] = source
                continue
            elif place == half:
                moved = moved - MID_COST
            else:
                moved = moved - ELSE_COST
            take = moved > stayed
            new[place] = np.where(take, moved, stayed) + local(t, place)
            back[t, place] = np.where(take, other, every)
        worth = new
    place, chord = np.unravel_index(int(np.argmax(worth)), worth.shape)
    total = float(worth[place, chord])
    path = []
    for t in range(count - 1, -1, -1):
        path.append((int(place), int(chord)))
        chord = back[t, place, chord]
        place = (place - 1) % length
    return total, path[::-1]


def find_meter(lines, oenv):
    """(time signature, the tempo's note, beats of that note in a bar) from where a bar's onsets fall.

    The bars are laid over each other in 24 steps. Quarters and eighths of 4/4 fall on steps that
    a bar in three never uses, and the other way round; in three, a bar whose halves are alike is
    6/8 and one whose thirds are alike is 3/4 (which a chord played off the beat does not change,
    as it would the loudness of the middle of the bar).
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
    def again(steps):
        return float(np.corrcoef(profile, np.roll(profile, steps))[0, 1])

    if again(12) > again(8):
        return "6/8", "4.", 2
    return "3/4", "4", 3


FAMILY = {"": "maj", "7": "maj", "maj7": "maj", "m": "min", "m7": "min",
          "sus4": "sus", "dim": "dim", "m7(b5)": "dim"}
# the chords a key is made of, as (semitones above the tonic, family)
KEY_CHORDS = {
    "major": {(0, "maj"), (2, "min"), (4, "min"), (5, "maj"), (7, "maj"), (7, "sus"), (9, "min"), (11, "dim")},
    "minor": {(0, "min"), (2, "dim"), (3, "maj"), (5, "min"), (7, "min"), (7, "maj"), (7, "sus"),
              (8, "maj"), (10, "maj")},
}
HOME_WEIGHT = 0.5       # what a second of the key's own chord counts for, beside a second of any of its chords
END_WEIGHT = 0.1        # what ending on the key's own chord counts for, as a share of the whole


def find_key(chords):
    """The key whose chords the reading is made of: (seconds, root, quality) for each chord read.

    The notes heard cannot tell a key from its relative or its dominant (C major, A minor and
    G major share six notes of seven); which chords are there, which of them is stayed on
    longest, and which one ends the piece, can.
    """
    whole = sum(seconds for seconds, _, _ in chords)
    best = (-np.inf, 0, "major")
    for mode, members in KEY_CHORDS.items():
        home = "maj" if mode == "major" else "min"
        for tonic in range(12):
            score = 0.0
            for seconds, root, quality in chords:
                degree = ((root - tonic) % 12, FAMILY[quality])
                if degree in members:
                    score += seconds
                if degree == (0, home):
                    score += HOME_WEIGHT * seconds
            _, last_root, last_quality = chords[-1]
            if ((last_root - tonic) % 12, FAMILY[last_quality]) == (0, home):
                score += END_WEIGHT * whole
            if score > best[0]:
                best = (score, tonic, mode)
    return best[1], best[2]


def features(path):
    y, _ = librosa.load(path, sr=SR, mono=True)
    harmonic = librosa.effects.harmonic(y, kernel_size=(HARMONIC_FRAMES, 31), margin=4) if HARMONIC_FRAMES else y
    return {
        "oenv": librosa.onset.onset_strength(y=y, sr=SR, hop_length=HOP),
        "chroma": librosa.feature.chroma_cqt(y=harmonic, sr=SR, hop_length=HOP,
                                             fmin=librosa.note_to_hz("C2"), n_octaves=5),
        "bass": librosa.feature.chroma_cqt(y=harmonic, sr=SR, hop_length=HOP,
                                           fmin=librosa.note_to_hz("C1"), n_octaves=2),
        "rms": librosa.feature.rms(y=y, hop_length=HOP),
    }


def read(path, f=None, tempo=None):
    """The chart of a recording. `tempo`, if told, is about how many beats a minute it has: what
    is heard cannot tell a fast song whose chords last two bars from a slow one, bar by bar."""
    name = Path(path).name
    f = f or features(path)
    oenv, chroma, bass, rms = f["oenv"], f["chroma"], f["bass"], f["rms"]
    beats, period = beat_grid(oenv)
    edges = half_beats(beats, period)
    loud = np.array([float(over(rms, a, b)[0]) for a, b in zip(edges[:-1], edges[1:])])
    count = len(loud)
    while count > 8 and loud[count - 1] < 0.1 * np.median(loud):  # the ring-out is not music
        count -= 1
    edges = edges[:count + 1]
    seconds = np.diff(edges) * HOP / SR

    treble = unit(np.array([over(chroma, a, b) for a, b in zip(edges[:-1], edges[1:])]).T)   # (12, units)
    low = unit(np.array([over(bass, a, b) for a, b in zip(edges[:-1], edges[1:])]).T)
    if MODEL is None:
        emit = ((TEMPLATES @ treble).T - COSTS) * seconds[:, None]
    else:
        said = np.full((count, len(CHORDS)), LOG_FLOOR)
        said[:, MODEL.classes_] = np.maximum(MODEL.predict_log_proba(np.hstack([treble.T, low.T])), LOG_FLOOR)
        emit = EMIT_SCALE * said * seconds[:, None]
    heard = BASS_WEIGHT * low.T * seconds[:, None]
    both = unit(np.vstack([treble, low]))
    change = np.zeros(count)
    change[1:] = 1.0 - np.sum(both[:, 1:] * both[:, :-1], axis=0)

    unit_seconds = float(np.median(seconds))
    lengths = BAR_UNITS
    if tempo:  # only the bars of two, three or four such beats
        told = [n for n in BAR_UNITS
                if any(abs(n * unit_seconds * tempo / 60.0 / beats - 1.0) <= HINT_TOLERANCE for beats in (2, 3, 4))]
        lengths = told or BAR_UNITS
    def search(emit, heard, stay=None):
        best = (-np.inf, None, 0)
        for length in lengths:
            if not BAR_SECONDS[0] <= length * unit_seconds <= BAR_SECONDS[1] or count < 4 * length:
                continue
            total, path = decode(emit, heard, change, length, stay)
            if total > best[0]:
                best = (total, path, length)
        if best[1] is None:
            raise ValueError("no bar length fits")
        return best[1], best[2]

    path, length = search(emit, heard)
    strange = False
    if MODEL is not None:
        # Sounds the model does not know: its opinion changes from half beat to half beat and
        # the bar lines follow it. Then the bars are the ones the templates find, and the model
        # names the chords inside them.
        mass = np.array([np.exp(said[t][SAME[k]]).sum() for t, (_, k) in enumerate(path)])
        strange = float(np.mean(mass < DOUBT)) > STRANGE
        if strange:
            plain = ((TEMPLATES @ treble).T - COSTS) * seconds[:, None]
            ruled, length = search(plain, RULES["BASS_WEIGHT"] * low.T * seconds[:, None], RULES["STAY_COST"])
            _, path = decode(emit, heard, change, length, first=ruled[0][0])

    starts = [t for t, (place, _) in enumerate(path) if place == 0]
    if count - starts[-1] >= length:      # the last bar is whole
        starts.append(starts[-1] + length)
    lines = np.array([edges[t] for t in starts])
    time, tempo_note, tempo_beats = find_meter(lines, oenv)

    stamp = lambda frame: round(max(float(frame) - BEAT_LAG, 0.0) * HOP / SR, 4)
    bars = []
    for n, (first, after) in enumerate(zip(starts[:-1], starts[1:])):
        chords, t = [], first
        while t < after:
            end = t
            while end < after and path[end][1] == path[t][1]:
                end += 1
            root, quality = CHORDS[path[t][1]]
            a, b = edges[t], edges[end]
            vb = unit(over(bass, a, a + (b - a) / 4, np.median))
            lowest = int(np.argmax(vb))
            tones = [(root + i) % 12 for i in QUALITIES[quality]]
            slash = lowest != root and lowest in tones and vb[lowest] > SLASH_RATIO * vb[root]
            fit = float(np.mean(TEMPLATES[path[t][1]] @ treble[:, t:end]))
            chord = {
                "start": stamp(a), "end": stamp(b),
                "symbol": (root, quality, lowest if slash else None),
                "fit": round(fit, 3),
            }
            if MODEL is not None:
                # how sure the model is of it, half beat by half beat, and what else it might be
                # (of its root and whether it is major or minor: a seventh heard or not is a
                # smaller doubt, and would mark one chord in four)
                same = SAME[path[t][1]]
                chord["sure"] = round(float(np.exp(said[t:end][:, same]).sum(axis=1).mean()), 3)
                mean = np.where(same, -np.inf, said[t:end].mean(axis=0))
                chord["other"] = CHORDS[int(np.argmax(mean))]
            chords.append(chord)
            t = end
        bars.append({"bar": n + 1, "start": stamp(edges[first]), "end": stamp(edges[after]), "chords": chords})

    read_chords = [c for bar in bars for c in bar["chords"]]
    tonic, mode = find_key([(c["end"] - c["start"], c["symbol"][0], c["symbol"][1]) for c in read_chords])
    tonic_name = (MAJOR_KEYS if mode == "major" else MINOR_KEYS)[tonic]
    flats = "b" in tonic_name or (tonic_name == "F" if mode == "major" else tonic_name in FLAT_MINORS)
    roots = FLAT_ROOTS if flats else SHARP_ROOTS
    for c in read_chords:
        root, quality, slash = c["symbol"]
        c["symbol"] = roots[root] + quality + ("/" + roots[slash] if slash is not None else "")
        if "other" in c:
            c["other"] = roots[c["other"][0]] + c["other"][1]
    bar_seconds = float(np.median(np.diff(lines))) * HOP / SR
    return {
        "audio": name,
        "tempo": {"note": tempo_note, "bpm": int(round(60.0 * tempo_beats / bar_seconds))},
        "time": time,
        "key": f"{tonic_name} {mode}",
        **({"strange": True} if strange else {}),
        "bars": bars,
    }


def lys(result, title):
    tonic, mode = result["key"].split()
    acc = {"": "", "#": "is", "b": "es"}[tonic[1:]]
    beats = {"4/4": 4, "3/4": 3, "6/8": 2}[result["time"]]

    def slots(bar):
        """The bar as Lily# writes it: a chord on the beat it starts on, '.' where it goes on."""
        length = (bar["end"] - bar["start"]) / beats
        names = []
        for i in range(beats):
            at = bar["start"] + (i + 0.01) * length
            names.append(next((c["symbol"] for c in bar["chords"] if c["start"] <= at < c["end"]),
                              bar["chords"][-1]["symbol"]))
        marks = [n if i == 0 or n != names[i - 1] else "." for i, n in enumerate(names)]
        doubts = {c["symbol"]: c["other"] for c in bar["chords"] if c.get("sure", 1.0) < DOUBT}
        marks = [f'{m}@todo("or {doubts[m]}?")' if m in doubts else m for m in marks]
        if all(m == "." for m in marks[1:]):
            return marks[0]
        half = beats // 2
        if beats % 2 == 0 and all(m == "." for i, m in enumerate(marks) if i not in (0, half)):
            return f"{marks[0]} {marks[half]}"
        return " ".join(marks)

    rows = [" | ".join(slots(bar) for bar in result["bars"][i:i + 4])
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
    p.add_argument("--model", type=Path, help="what train_chords.py learnt, in place of the chord templates")
    p.add_argument("--tempo", type=float,
                   help="about how many beats a minute the recording has, if you know: it settles "
                        "whether a chord lasts one bar or two")
    args = p.parse_args()
    if args.model:
        use_model(args.model)
    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    args.out.mkdir(parents=True, exist_ok=True)
    failed = 0
    for path in files:
        try:
            result = read(path, tempo=args.tempo)
        except Exception as e:  # one recording that cannot be read does not stop the rest
            failed += 1
            print(f"{path.name}: {e}", file=sys.stderr)
            continue
        (args.out / (path.stem + ".dettato.json")).write_text(
            json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
        (args.out / (path.stem + ".lys")).write_text(lys(result, path.stem), encoding="utf-8", newline="\n")
        read_chords = [c for bar in result["bars"] for c in bar["chords"]]
        doubted = sum(c.get("sure", 1.0) < DOUBT for c in read_chords)
        print(f"{path.name}: {result['key']}, {result['time']}, {result['tempo']['bpm']}, "
              f"{len(result['bars'])} bars"
              + (f", {doubted} of {len(read_chords)} chords doubted" if MODEL is not None else "")
              + (": this is not music the model knows" if doubted > 0.5 * len(read_chords) else ""))
    if failed:
        sys.exit(f"{failed} of {len(files)} recordings were not read")


if __name__ == "__main__":
    main()
