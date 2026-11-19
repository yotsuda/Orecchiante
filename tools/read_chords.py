#!/usr/bin/env python3
"""The baseline reader: a recording to its tempo, bar lines and chords. Nothing here is learnt.

    .venv/Scripts/python tools/read_chords.py data/synth/seed1 --out scratch/read/seed1

For each .wav it writes <name>.lys (a Lily# chord chart) and <name>.dettato.json (every bar and
chord with its seconds, its section and which bar of it it is, in the shape of gen_chords.py's
truth: docs/SIDECAR.md is its specification, tools/sidecar.py checks a file against it). The steps:

1. beats: librosa's beat tracker on the onset strength, carried back to the start and on to the
   end, and halved: the half beat is the unit everything after is counted in;
2. bars and chords, together: the path through (chord, place in the bar) that fits the chroma of
   every unit best, where a chord may change freely at a bar line, at a price in the middle of
   a bar, and at a high price elsewhere; a bar line the chord does not change at costs a little.
   This is done for each length a bar may have, and the best path over all lengths is kept;
3. meter: from where the onsets fall inside the bars (in fours, in threes, or in two threes);
4. the two ends: the chart runs from the first bar that sounds a chord to the last; whole beats
   of chord before the first bar line are a pickup, a short bar numbered 0 (`"pickup": beats`),
   and the bar the recording ends in is written whole;
   sections: runs of bars said again are written once (`section`) and the order they are played
   in after them (`form`);
5. key: the one whose chords the reading is made of, for the spelling.

This is the line to beat, written to be measured (tools/score_chords.py), not to be shipped.
"""
import argparse
import json
import math
import os
import subprocess
import sys
from collections import Counter
from pathlib import Path

import librosa
import numpy as np

SR = 22050
HOP = 512
FORMAT = 1              # the .dettato.json written here (docs/SIDECAR.md): raised when a reader of it must change

QUALITIES = {
    "": [0, 4, 7], "m": [0, 3, 7], "7": [0, 4, 7, 10], "maj7": [0, 4, 7, 11],
    "m7": [0, 3, 7, 10], "sus4": [0, 5, 7], "dim": [0, 3, 6], "m7(b5)": [0, 3, 6, 10],
}
# what a quality has to earn before it is read: a bass that also plays the fifth makes the root and
# the fifth equally loud, which is the suspended chord on the fifth (D minor read as Asus4)
QUALITY_COST = {"": 0.0, "m": 0.0, "7": 0.02, "maj7": 0.05, "m7": 0.0, "sus4": 0.08, "dim": 0.04, "m7(b5)": 0.05}
BASS_WEIGHT = 0.3       # how much the bass at the start of a bar (or its half) says which note is the root
# The harmonic part of a recording is what stays over this many frames (0: the recording as it is).
# Measured 2026-11-14: 31 and 9 read alike, and the recording as it is reads worse (the drums),
# for this reader's own hearing. With ears trained on recordings it is 0 (use_ears).
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
# With a model trained on recordings (--ears), how likely a chord's root and family are right:
# the logistic of these weights on (the model's mean probability of it over its half beats, the
# same in its last half beat, whether this reader's own templates name the same root and family,
# the mean of the first over the whole song), and a constant. The last half beat weighs the other
# way: what the model says there is already the next chord's. Chosen on rwc-dev, 2026-11-14.
SURE_OF = (8.0, -2.0, 0.7, 5.5, -7.5)
DOUBT_EARS = 0.84       # the mark, with those ears: three chords in ten on rwc-dev
BAR_UNITS = (2, 3, 4, 6, 8, 12, 16)   # a bar, in half beats: the tracker may follow any level
BAR_SECONDS = (0.9, 4.0)              # bars shorter or longer than this are not tried
HINT_TOLERANCE = 0.15   # how far from the tempo told a bar's beats may be
QUIET = 0.1             # a unit this much quieter than the song's middle one is silence
HUSH = 0.01             # ears: a unit this much quieter is silence (a fade-out is not: it is quiet and still music)
NO_CHORD = 0.5         # ears: a unit whose "no chord" is likelier than this has no chord to name
                        # (rwc-dev: 62% of the seconds before the first chord, 93% of those after the last,
                        # 0.3% of the chords' own)
CHORDED = 0.25          # a bar before the first chord or after the last is in the chart when this much of it sounds one
RING_OUT = 0.2          # ears: a bar past the last one found and this much quieter than the bar before is the ring-out
                        # (rwc-dev and seed1; asking for the same chord too leaves 11 such bars in seed1's 24 songs, this 4)
PARTIAL = {1: "4", 2: "2", 3: "2.", 4: "1", 6: "1."}   # a pickup of so many quarters, as Lily#'s `partial` takes it
MEND_BARS = True       # ears: two short bars that together have a bar's beats are one bar (the tracker's slip)
SHORT_BAR = 12.0       # ears: bars of half the beats are paired where the downbeat logits say (pair_runs), a bar left
                        # alone costing this much logit. None: paired from the first (before 2026-11-17)
TINY_BAR = 0.25        # ears: a bar shorter than this share of the middle one is not a bar (糸: 0.08 s, Lily# REQUESTS 10)
TWO_FOUR = (0.4, 0.6, 0.15)  # ears: a bar this share of its neighbours' (low, high), between two within this of theirs, is a bar of 2/4
                        # in 4/4 (short_meters; the owner, 2026-11-17: 糸's M65). None: written as 4/4 (before 0.8.5)
THREE_FOUR = (0.65, 0.85, 2)  # ears: a bar this share of its neighbours', not in the last so many bars, is a bar of 3/4 in 4/4
                        # (short_meters; the owner, 2026-11-17). None: written as 4/4 (before 0.8.6)
HALF_BARS = (0.35, 0.7)  # ears: bars this share of the middle one are halves, paired as the key says (pair_halves). None: as found
EVEN_TEMPO = 0.1       # the tempo is from the mean of the bars within this share of the middle one (0: the middle one alone)
DIAGRAM_NOTE = "// Shift+Ctrl+Up/Down on a chord name shows its guitar chord diagram; on X@or(Y), swaps in Y and plays it"   # above the first chords (the owner, 2026-11-16)
JOIN_NOTE = "// Ctrl+. on a section's name offers Join Sections (with the next one, or all the sections selected)"   # above the first section (the owner, 2026-11-17)
TENTH_TEMPO = True    # ears: the tempo to a tenth, a whole number when it is one (Lily# writes `tempo 4 = 131.8`, 2026-11-16)
SECTION_TEMPO = (0.06, 6, 0.75, 1.3)   # ears: a section's own tempo, written at its head (section_tempos, C3, 2026-11-17):
                        # where it is more than the first share away from the tempo standing, the section has this many
                        # bars or more, this share of them within EVEN_TEMPO of their middle one, and the step is less
                        # than the last (2, 3/2 and 4/3 are the bars read wrong, not the tempo). None: one tempo
                        # (chosen on rwc-dev and seed1/7/11/31 with still/ and drift5/; rwc-test seen song by song)
SLOW_TWO = 1.5        # a bar of two beats longer than this (beats slower than 80 a minute) is a bar of four
                        # (chosen looking at seed7 and seed11, test sets, 2026-11-14)

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


# A model trained on recordings (BTC, through tools/btc_chords.py --probs) names 170 chords: 14
# qualities on each root, then "X" and "N". They are folded into the reader's as tools/rwc_truth.py
# folds the annotations; augmented chords, X and N have no chord of the reader's.
EAR_QUALITIES = ["min", "maj", "dim", "aug", "min6", "maj6", "min7", "minmaj7", "maj7", "7", "dim7", "hdim7", "sus2", "sus4"]
EAR_AS = {"min": "m", "maj": "", "dim": "dim", "min6": "m", "maj6": "", "min7": "m7", "minmaj7": "m", "maj7": "maj7",
          "7": "7", "dim7": "dim", "hdim7": "m7(b5)", "sus2": "sus4", "sus4": "sus4"}
FOLD = np.zeros((170, len(CHORDS)))
for _i in range(168):
    _q = EAR_AS.get(EAR_QUALITIES[_i % 14])
    if _q is not None:
        FOLD[_i, CHORDS.index((_i // 14, _q))] = 1.0


def use_ears():
    """The prices that go with a model trained on recordings and bar lines found by a beat tracker
    (chosen on rwc-dev, 2026-11-14). That model has already smoothed what it says over the bars
    around, so a change costs far less than it does for a model that hears one half beat: with
    this reader's own prices (0.4, 0.5, 1.0) roots are 82.7% right there, with these 84.8%."""
    global STAY_COST, MID_COST, ELSE_COST, BASS_WEIGHT, EMIT_SCALE, DOUBT, HARMONIC_FRAMES
    STAY_COST, MID_COST, ELSE_COST, BASS_WEIGHT, EMIT_SCALE = 0.1, 0.1, 0.25, 0.0, 0.2
    DOUBT = DOUBT_EARS
    # The chords are the model's, so this reader's own chroma no longer has to be free of the drums:
    # it is taken from the recording as it is. Taking the harmonic part out is nine tenths of the
    # reader's time (8 s of a 4 minute song), and with it left in the roots read the same and the
    # bass notes worse (rwc-dev 63.3% -> 64.3% without; README, "Without torch and TensorFlow").
    HARMONIC_FRAMES = 0


NONE = np.zeros((170, 1))   # the model's "N": no chord
NONE[169] = 1.0


def said_in(heard, edges, fold=FOLD):
    """What the model trained on recordings says of each unit: the log of its mean probability of
    each of the reader's chords between one edge (in seconds) and the next. A frame counts for as
    much of it as lies inside the unit. (`fold=NONE`: of there being no chord.)"""
    p = np.exp(heard["logp"].astype(np.float64)) @ fold
    step = float(heard["step"])
    run = np.vstack([np.zeros(p.shape[1]), np.cumsum(p, axis=0)])
    x = np.clip(np.asarray(edges, dtype=float) / step, 0.0, len(p))
    i = np.minimum(x.astype(int), len(p) - 1)
    upto = run[i] + (x - i)[:, None] * p[i]
    width = np.maximum(np.diff(x), 1e-6)
    return np.log(np.maximum(np.diff(upto, axis=0) / width[:, None], 1e-12))


def key_heard(heard):
    """The key of what the ears say, before any reading: find_key on the likeliest chord of each frame that names one."""
    p = np.exp(heard["logp"].astype(np.float64)) @ FOLD
    step, best, named = float(heard["step"]), p.argmax(axis=1), p.sum(axis=1) > 0.5
    chords = []
    for i, n in zip(best, named):
        if not n:
            continue
        if chords and chords[-1][1:] == CHORDS[i]:
            chords[-1] = (chords[-1][0] + step,) + CHORDS[i]
        else:
            chords.append((step,) + CHORDS[i])
    return find_key(chords) if chords else None


def pair_halves(times, downs, heard):
    """Bars half as long as the rest, one after the other, are the halves of bars: the tracker took
    the beats twice as fast for a while (糸: 36 bars of 2 s among bars of 3.8 s, Lily# REQUESTS 10).
    Two by two they are one bar. Which half begins a bar is what the key says: the bar lines are
    where the home chord comes, the more so after its dominant (the owner, 2026-11-16). A whole bar
    between two halves is two halves (the tracker slipped there), cut at its beat nearest the middle."""
    lo, hi = HALF_BARS
    middle = float(np.median([times[b] - times[a] for a, b in zip(downs, downs[1:])]))
    span = lambda k: times[downs[k + 1]] - times[downs[k]]
    half = lambda x: lo * middle <= x < hi * middle
    k = 1
    while k < len(downs) - 2:
        a, b = downs[k], downs[k + 1]
        if not half(span(k)) and half(span(k - 1)) and half(span(k + 1)) and b - a >= 2:
            cut = min(range(a + 1, b), key=lambda i: abs(times[i] - (times[a] + times[b]) / 2))
            if half(times[cut] - times[a]) and half(times[b] - times[cut]):
                downs.insert(k + 1, cut)
        k += 1
    key = key_heard(heard)
    if key is None:
        return downs
    tonic, mode = key
    p = np.exp(heard["logp"].astype(np.float64)) @ FOLD
    home = np.array([r == tonic and _KIND[q] == ("maj" if mode == "major" else "min") for r, q in CHORDS])
    fifth = np.array([r == (tonic + 7) % 12 and _KIND[q] in ("maj", "sus") for r, q in CHORDS])

    def worth(t, beat):
        """How much a bar line at `t` seconds sounds like one: the home chord in the beat after it,
        times two after its dominant in the beat before."""
        before, after = np.exp(said_in(heard, [t - beat, t, t + beat]))
        return float(after[home].sum()) * (1.0 + float(before[fifth].sum()))

    gone, k = set(), 0
    while k < len(downs) - 1:
        if not half(span(k)):
            k += 1
            continue
        e = k
        while e + 1 < len(downs) - 1 and half(span(e + 1)):
            e += 1
        n = e - k + 1
        if n >= 2:
            odd = False         # an even run lies between whole bars that hold their lines: paired from its first
            if n % 2:           # an odd one has a half left over, at its first or its last: the key says which
                beat = (times[downs[e + 1]] - times[downs[k]]) / n / 2
                s = [worth(times[downs[k + j]], beat) for j in range(n + 1)]
                odd = float(np.mean(s[1::2])) > float(np.mean(s[0::2]))
            gone |= {downs[k + j] for j in range(1, n) if j % 2 == (0 if odd else 1)}
        k = e + 1
    return [d for d in downs if d not in gone]


def pair_runs(downs, strength, per):
    """Bars of half the beats, one after the other (the tracker counted a bar of four as two of two),
    paired two by two where the model is surest a bar begins (the downbeat logit of the lines kept,
    less that of the lines dropped). A bar of half the beats may be left alone, at SHORT_BAR: 糸's M65,
    two beats between bars of four (the owner, 2026-11-16); for nothing at the first or last line the
    tracker found, which need not be bar lines. Before this the pairs ran from the first."""
    if per % 2:
        return downs
    gone, k = set(), 0
    while k < len(downs) - 1:
        if downs[k + 1] - downs[k] != per // 2:
            k += 1
            continue
        e = k
        while e + 2 < len(downs) and downs[e + 2] - downs[e + 1] == per // 2:
            e += 1
        n = e - k + 1                       # halves; its lines are downs[k .. e + 1], the first and the last kept
        if n >= 2:
            s = [strength[downs[k + j]] for j in range(n + 1)]
            best, came = [0.0] + [-np.inf] * n, [0] * (n + 1)
            for j in range(1, n + 1):
                gain = s[j] if j < n else 0.0
                free = (j == 1 and k == 0) or (j == n and e + 2 == len(downs))   # where the tracker began or stopped
                one = best[j - 1] - (0.0 if free else SHORT_BAR)
                two = best[j - 2] - s[j - 1] if j >= 2 else -np.inf
                best[j], came[j] = (two + gain, 2) if two >= one else (one + gain, 1)
            j = n
            while j > 0:
                if came[j] == 2:
                    gone.add(downs[k + j - 1])
                j -= came[j]
        k = e + 1
    return [d for d in downs if d not in gone]


def lines_of(path, heard=None):
    """Bar lines found by a beat tracker (tools/track_beats.py: a line a beat, its seconds, a tab,
    its number in the bar), as `read` takes them: the lines in seconds, and the beats in a bar.
    With `heard` (the ears' chords), bars half as long as the rest are paired (pair_halves)."""
    rows = [line.split() for line in Path(path).read_text().splitlines() if line.strip()]
    times, numbers = [float(r[0]) for r in rows], [int(float(r[1])) for r in rows]
    strength = [float(r[2]) for r in rows] if rows and all(len(r) > 2 for r in rows) else None
    downs = [i for i, n in enumerate(numbers) if n == 1]
    if len(downs) < 2:
        raise ValueError("fewer than two bar lines found")
    per = Counter(b - a for a, b in zip(downs, downs[1:])).most_common(1)[0][0]
    if MEND_BARS and SHORT_BAR is not None and strength is not None:
        downs = pair_runs(downs, strength, per)
    if MEND_BARS:       # a first beat counted in the middle of a bar: two short bars that make one whole are one
        kept, i = [downs[0]], 1
        while i < len(downs):
            if i + 1 < len(downs) and downs[i] - kept[-1] < per and downs[i + 1] - kept[-1] == per:
                i += 1
            kept.append(downs[i])
            i += 1
        downs = kept
    if TINY_BAR:                        # a bar far shorter than the rest is two lines for one (Lily# REQUESTS 10, 2026-11-16, 糸):
        lengths = lambda: [times[b] - times[a] for a, b in zip(downs, downs[1:])]     # one goes, the shorter neighbour's
        middle = float(np.median(lengths()))
        while len(downs) > 2:
            s = lengths()
            k = next((k for k, x in enumerate(s) if x < TINY_BAR * middle), None)
            if k is None:
                break
            del downs[1 if k == 0 else k if k == len(s) - 1 or s[k - 1] <= s[k + 1] else k + 1]
    if HALF_BARS and heard is not None and len(downs) > 3:
        downs = pair_halves(times, downs, heard)
    beat = float(np.median(np.diff(times)))
    last = downs[-1] + per
    end = times[last] if last < len(times) else min(times[-1] + beat, times[downs[-1]] + per * beat)
    return [times[i] for i in downs] + ([end] if end > times[downs[-1]] + 0.5 * per * beat else []), per


def tenth(bpm):
    """A tempo as the chart writes it: to a tenth, bare when whole."""
    bpm = round(bpm, 1)
    return int(bpm) if bpm == int(bpm) else bpm


def section_tempos(bars, beats, bpm):
    """{section: its tempo} for the sections whose tempo is their own (SECTION_TEMPO): a recording
    whose tempo moves (a slower intro, songs at 128 to 154 in a band's practice). Walked in the order
    played, from the head's `bpm`; a section once given a tempo keeps it each time it is played."""
    if not SECTION_TEMPO:
        return {}
    away, fewest, agree, cap = SECTION_TEMPO
    of = {}
    for bar in bars:
        if not bar.get("pickup"):
            of.setdefault(bar["section"], []).append(bar["end"] - bar["start"])
    own = {}
    for name, s in of.items():
        s = np.array(s)
        middle = float(np.median(s))
        near = s[np.abs(s - middle) <= EVEN_TEMPO * middle]
        if len(s) >= 2 and len(near) >= agree * len(s):
            own[name] = (60.0 * beats / float(np.mean(near)), len(s))
    given, now, last = {}, float(bpm), None
    for bar in bars:
        name = bar["section"]
        if bar.get("pickup") or name == last:
            continue
        last = name
        if name in given:
            now = given[name]
        elif name in own and abs(own[name][0] / now - 1) > away:
            t, n = own[name]
            if abs(t / bpm - 1) <= away:        # back to the head's tempo: a short section says it too
                given[name] = now = float(bpm)
            elif n >= fewest and 1 / cap < t / now < cap:
                given[name] = now = float(tenth(t))
    return {name: tenth(t) for name, t in given.items()}


def short_meters(bars, time):
    """The bars of 2/4 and 3/4 in a chart of 4/4 (TWO_FOUR, THREE_FOUR), as {index into `bars`: meter}:
    about a half or three quarters as long as the bars around them (the median of four each side),
    between two bars about as long as theirs, and the same in each time its section is played; not a
    pickup, not the first or the last bar (a bar of 3/4 not the last THREE_FOUR[2] bars either: there
    the bars read short are the ritardando's, read wrong). Of 2/4 the tracker counted two beats there
    and the downbeats said a bar begins after them (pair_runs): 糸's M65, two beats between bars of four
    (the owner, 2026-11-17: "Dettato should write it"). Right or not, the bar is as long as the
    recording's: written in its meter it plays at the recording's pace, and marks where to look.
    3/4 the owner chose on 2026-11-17 though RWC-P has next to none in the middle of a song (P006)."""
    if not TWO_FOUR or time != "4/4":
        return {}
    whole = TWO_FOUR[2]
    at = [i for i, bar in enumerate(bars) if not bar.get("pickup")]
    s = np.array([bars[i]["end"] - bars[i]["start"] for i in at])
    if len(s) < 3:
        return {}
    ratio = [float(s[k] / np.median(np.r_[s[max(0, k - 4):k], s[k + 1:k + 5]])) for k in range(len(s))]
    found = {}
    for k in range(1, len(s) - 1):
        if abs(ratio[k - 1] - 1.0) > whole or abs(ratio[k + 1] - 1.0) > whole:
            continue
        if TWO_FOUR[0] <= ratio[k] <= TWO_FOUR[1]:
            found[at[k]] = "2/4"
        elif THREE_FOUR and THREE_FOUR[0] <= ratio[k] <= THREE_FOUR[1] and k < len(s) - THREE_FOUR[2]:
            found[at[k]] = "3/4"
    played = {}                         # a bar of a section is short when it is so each time the section is played
    for i in at:
        played.setdefault((bars[i].get("section"), bars[i].get("barOfSection")), []).append(i)
    return {i: m for i, m in found.items()
            if all(found.get(j) == m for j in played[(bars[i].get("section"), bars[i].get("barOfSection"))])}



def beats_of(start, end, n):
    """Where the `n` beats of a bar begin: the bar cut evenly, the first at its line. (The beat
    tracker's own beats inside the bar are no nearer RWC's annotated beats: within 70 ms 96.2% of
    them on rwc-dev, the same as these; it places them on a grid of 20 ms.)"""
    each = (end - start) / n
    return [start] + [round(start + i * each, 4) for i in range(1, n)]


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


SECTION_BARS = 4        # the shortest run of bars that is a section for being said again
SAID_AGAIN = 0.95       # runs of bars are one section when this share of their beats have the same root and quality
SHORT_SECTION = 6       # two sections running of at most this many bars are one: a chart in sections of four bars
                        # or two is hard to read (the owner, 2026-11-16: four and eight may stand side by side, four
                        # and four may not). 0: as found.
HIDE_SHORT = 3          # a section shorter than this many bars is named after the one before (B_1) and its name hidden (~B_1)
LEAD_OFF = True        # the second ending: a section played once opens with what a short section is elsewhere (lead_off)
LONG_SECTION = 32      # sections played once, one after the other, are one section up to this many bars, and a
                        # longer run is cut into even sections no longer (the owner, 2026-11-16). 0: as found.


KEY_TURN = 2           # ears: a key region this many accidentals from the one before is a turn of the music (a section
                       # may not run across it: the owner, 2026-11-16, sections where the key or the mood changes)
KEY_PRICE = 1.5        # what a change of key signature costs, against a bar's evidence (4 times the log of its share in the scale)
KEY_REGION = 4         # bars: a shorter run in one key is not a region of its own
LOUD_TURN = 6.0        # dB: the two bars after a bar line this much louder or quieter than the two before turn the music
TURN_SLACK = 1         # bars: a section may run across a turn this close to its first or its last bar
TONES = np.zeros((len(CHORDS), 12))
for _k, (_r, _q) in enumerate(CHORDS):
    TONES[_k, [(_r + _i) % 12 for _i in QUALITIES[_q]]] = 1.0
SCALES = np.array([[1.0 if (pc - t) % 12 in (0, 2, 4, 5, 7, 9, 11) else 0.0 for pc in range(12)] for t in range(12)])
SIGNATURE = [((t * 7 + 6) % 12) - 6 for t in range(12)]   # sharps (+) or flats (-) of the major scale on t


def turns_of(notes, loud):
    """Where the music turns (the owner, 2026-11-16): the bars a section begins at rather than runs
    across. `notes`: each bar's notes heard (bars, 12: the chords' tones, weighed by how likely and how
    long); `loud`: each bar's loudness in dB. A turn is where the key signature moves by KEY_TURN
    accidentals or more (the scale of the notes, bar by bar, changing at a price; runs shorter than
    KEY_REGION bars not counted), or where the two bars after are LOUD_TURN dB louder or quieter than the
    two before, the most of the bars around."""
    n = len(loud)
    turns = set()
    if n < 2 * KEY_REGION:
        return turns
    fits = 4.0 * np.log(np.maximum(notes @ SCALES.T / np.maximum(notes.sum(axis=1), 1e-9)[:, None], 1e-3))   # (bars, 12)
    score, back = fits[0].copy(), []
    for i in range(1, n):
        best = float(score.max())
        stay = score >= best - KEY_PRICE
        back.append(np.where(stay, np.arange(12), int(score.argmax())))
        score = np.where(stay, score, best - KEY_PRICE) + fits[i]
    path = [int(score.argmax())]
    for came in back[::-1]:
        path.append(int(came[path[-1]]))
    path = path[::-1]
    regions, start = [], 0                       # (first bar, signature) of each region long enough
    for i in range(1, n + 1):
        if i == n or path[i] != path[start]:
            if i - start >= KEY_REGION:
                regions.append((start, SIGNATURE[path[start]]))
            start = i
    for (_, a), (first, b) in zip(regions, regions[1:]):
        if min(abs(a - b), 12 - abs(a - b)) >= KEY_TURN:
            turns.add(first)
    jump = np.zeros(n)
    for i in range(2, n - 1):
        jump[i] = np.mean(loud[i:i + 2]) - np.mean(loud[i - 2:i])
    for i in range(2, n - 1):
        if abs(jump[i]) >= LOUD_TURN and abs(jump[i]) >= np.abs(jump[max(i - 2, 0):i + 3]).max():
            turns.add(i)
    return turns


PHRASE_SECTIONS = True  # sections as the owner cut 糸 and My Life (2026-11-16): blocks between turns, played again elsewhere, cut on a grid of phrases, named
                       # after the section they play again (A, A2); no repeat signs (the default since 2026-11-16, the owner). False: find_form and fold
PHRASE_SECONDS = 15.0  # a phrase lasts about this long: as many bars as come nearest, a power of two (糸 4 of 3.8 s, My Life 8 of 1.8 s)
PHRASE_BARS = 4        # a phrase is this many bars whatever the tempo (AIST, rwc-dev: sections of 8 bars and 4; 糸 and My Life still the owner's); 0: from PHRASE_SECONDS
BEGIN_SHARE = 0.67     # on the grid, also cut where the phrase is played elsewhere and, at this share of those places or more, the bar before differs (a chorus, a verse B); 0: never
WHOLE_BLOCK = 2        # phrases: a block no longer than this is one section (an intro, an interlude, a solo)
PHRASE_ALIKE = 0.75    # two phrases, or two sections, are the same when this share of their beats have the same root and family
VOICE_LOUD = 0.25      # a moment sings when the vocals are this share of the song's loud end (their 95th percentile)
VOICE_FLOOR = 0.05     # a song sings at all when the vocals' loud end is this share of the recording's (no singing:
                       # 0.001 to 0.005 in seed1; singing: 0.19 to 0.72 in RWC)
VOICE_SHARE = 0.45     # a bar sings when this share of it sings (糸: a verse 0.41 to 0.84, the tail of a phrase 0.39)
VOICE_RUN = 2          # bars: singing, or its silence, has to last this long to turn the music
VOICE_GAP = 3          # bars: a silence shorter than this between singing is a breath, or an instrument's answer (My Life)
TURN_NEAR = 2          # bars: a turn of key or loudness this near where singing begins or stops is that one
LEAD_IN = 0            # (trying) where singing begins or stops on a bar wholly of a dominant resolving across the bar line, the section begins
                       # after it; 2: a turn of key or loudness too; 0: never
VOICE_LATE = 0.0      # (trying) singing begins in the next bar when the first half of the bar it begins in sings less than this share (a pickup); 0: never


def voice_turns(sung, begins=False):
    """Where singing begins or stops (`sung`: the share of each bar that sings) and goes on: the bars a
    section begins at. A silence shorter than VOICE_GAP bars between singing is a breath; then singing
    shorter than VOICE_RUN bars is none."""
    on = [s >= VOICE_SHARE for s in sung]

    def runs():
        out, i = [], 0
        while i < len(on):
            j = i
            while j < len(on) and on[j] == on[i]:
                j += 1
            out.append((i, j, on[i]))
            i = j
        return out

    found = runs()
    for k, (i, j, v) in enumerate(found):
        if not v and j - i < VOICE_GAP and 0 < k < len(found) - 1:
            on[i:j] = [True] * (j - i)
    for i, j, v in runs():
        if v and j - i < VOICE_RUN:
            on[i:j] = [False] * (j - i)
    return {i for i, _, v in runs()[1:] if v or not begins}


PAIR_HEARD = 2         # on the grid, also cut after every pair of phrases from the block's start (AIST, rwc-dev: a verse A of 8 bars,
                       # then B) where the phrase after is played this many times elsewhere and does not begin the pair as the
                       # one before did (a b a c is one period: My Life's verses of 14 and 16 bars, the owner's); 0: never


HELD_SHIFT = 2         # a grid cut where a chord other than the home chord is held across the bar line moves up to this many bars on, to
                       # where the home chord arrives, and the grid goes on from there (Open Arms, the owner: G | G A | D, the verse's 8 bars and 2
                       # more; AIST rwc-dev 66.3 -> 66.7%, without the voice 50.7 -> 52.4%; 3: 66.1%, 4: 65.4%; to any change of chord: 65.6%); 0: never
HELD_SHIFT_LOOSE = 0   # (tried, kept off) ... to where any chord changes, and also when the held chord is the home chord
HELD_REANCHOR = 1      # the grid goes on from the moved cut (0: rwc-dev 66.3%)
MERGE_SHORT = 2        # a section of fewer bars than this joins the one before (the first: the one after); Open Arms' bar 50, the chorus's
                       # last bar (AIST rwc-dev the same 66.7%, 790 -> 776 starts; 3: 66.5% and My Life's last section lost; 4: 65.8%); 0: never
SUNG_WHOLE = 2         # phrases: from where singing begins, these phrases, when played the same elsewhere, are not cut inside by singing that
                       # stops and begins again, nor by a turn of key or loudness TURN_NEAR from it (Open Arms, the owner 2026-11-18: 15, 62, 65 are
                       # not cuts, 67 is; AIST rwc-dev 66.7 -> 66.8%, rwc-test 62.4 -> 62.2%; 糸 and My Life the same); 0: never
HELD_SINGS = 0         # (off, the owner to decide) where singing stops on a chord other than the home chord held across the bar line, the cut
                       # (or the turn standing for it) moves as HELD_SHIFT moves a grid cut (Open Arms, the owner: 100 for 101); 1: where it stops,
                       # 2: where it begins too; 0: never. Not in .NET. With HELD_SUNG 0.2: Open Arms 101, 糸 and My Life the same, AIST rwc-dev
                       # 3 s 66.8% the same, 0.5 s 42.5 -> 42.4%, rwc-test 62.2 -> 62.1%; without HELD_SUNG rwc-dev on the bar 368 -> 364
HELD_SUNG = 0.2        # ... only where the bar singing stops in is still sung this much (the singing trails into it; 0.15 worse, 0.3 misses 101)
GRID_ANCHOR = 1        # count a block's grid from where singing last began or stopped, or a block played again whole begins or
                       # ends, not from a turn of key or loudness (AIST, rwc-dev: 66.1 -> 66.6%, without the voice 48.7 -> 50.9%); 0: from the block's start


WHY = {}                 # phrase_sections: the rule each cut came from, by bar (for measuring only)
CUE_WHYS = ["sing", "turn", "played", "ended", "cadence", "pair", "begins"]
SECTION_DP = 1           # choose the cuts whole (section_rows weighed, choose_cuts; the owner 2026-11-19: the average over the songs the owner cut
                         # in Transcribe!, not a song of his own): what it weighs with is in DP_FILE. 0: the rules' cuts (MERGE_SHORT)
DP_FILE = Path(__file__).with_name("sections_dp.json")   # scratch/xsc/dp.py --json: logistic regression on the owner's dev songs
_DP = {}


def dp_model():
    """The weights, lengths and constants of SECTION_DP (DP_FILE), read once."""
    if not _DP:
        _DP.update(json.loads(DP_FILE.read_text(encoding="utf-8")))
        _DP["lengths"] = {int(k): v for k, v in _DP["lengths"].items()}
    return _DP


def section_cues(fam, tonic, home, sung, why, sings, begins, turns):
    """For each bar line (the bar it begins), what might make it a section's start: the rule that made a cut there,
    where singing begins or stops, a turn, phrases of 4 and 8 said twice or played elsewhere, the chords across it,
    and how the singing changes. One row of floats a bar."""
    n = len(fam)
    sung = [0.0 if s != s else s for s in sung] if sung else [0.0] * n   # a bar past the vocals' end: none

    def alike(a, b, length):
        if a < 0 or b < 0 or a + length > n or b + length > n:
            return False
        beats = [x == y for i in range(length) for x, y in zip(fam[a + i], fam[b + i])]
        return bool(beats) and sum(beats) >= PHRASE_ALIKE * len(beats)

    rows = []
    for i in range(n):
        f = [float(why.get(i) == w) for w in CUE_WHYS]
        f += [float(i in sings), float(i in begins), float(i in turns)]
        for p in (4, 8):
            f.append(float(alike(i - 2 * p, i - p, p) and not alike(i - p, i, p)))   # a phrase said twice ended
            f.append(float(alike(i - p, i, p)))                                         # in the middle of a phrase said twice
            hs = [h for h in range(1, n - p + 1) if abs(h - i) >= p and alike(i, h, p)] if i + p <= n else []
            f.append(min(len(hs), 4) / 4)                                               # the phrase from here is played elsewhere
            f.append(sum(not alike(i - 1, h - 1, 1) for h in hs) / len(hs) if hs else 0.0)   # ... and begins there as here
        last, first = (fam[i - 1][-1], fam[i][0]) if 0 < i < n else (None, None)
        f.append(float(bool(last) and last[0] == (tonic + 7) % 12 and last[1] in ("maj", "sus") and first == (tonic, home)))
        f.append(float(bool(last) and last == first))                                   # a chord held across the bar line
        f.append(float(bool(first) and first == (tonic, home)))
        f.append(float(bool(last) and len(set(fam[i - 1])) == 1 and last[1] in ("maj", "sus") and last[0] == (first[0] + 7) % 12))
        s0, s1 = (sung[i - 1] if i > 0 else 0.0), sung[i]
        f += [s1 - s0, abs(s1 - s0), s1, s0]
        rows.append(f)
    return np.array(rows)


def audio_cues(db, notes, sets):
    """For each bar line, how the bars after it differ from those before, over 2 and 4 bars: loudness (dB, the song's own
    scale), the notes heard (cosine distance), and the share of the chord families after (`sets`, a set a bar) not heard before."""
    n = len(db)
    db = (db - np.median(db)) / (np.std(db) + 1e-9)
    rows = []
    for i in range(n):
        f = [db[i]]
        for w in (2, 4):
            if i - w < 0 or i + w > n:
                f += [0.0, 0.0, 0.0, 0.0]
                continue
            dd = db[i:i + w].mean() - db[i - w:i].mean()
            u, v = notes[i - w:i].sum(0), notes[i:i + w].sum(0)
            before, after = set().union(*sets[i - w:i]), set().union(*sets[i:i + w])
            f += [dd, abs(dd), 1 - float(u @ v / (np.linalg.norm(u) * np.linalg.norm(v) + 1e-9)), len(after - before) / max(len(after), 1)]
        rows.append(f)
    return np.array(rows)


def drum_bars(drums, bars):
    """The drums in each bar (`drums`: what `dettato` writes as <name>.drum.npy, every 10 ms the mean square and the
    onset strength in 3 bands; src/Dettato/Drums.cs): how loud (dB of the mean square), and their pattern: the onset
    strength at its strongest in each of 16 steps of the bar, band by band, as a unit vector."""
    db, pattern = np.zeros(len(bars)), np.zeros((len(bars), 48))
    for i, bar in enumerate(bars):
        a, b = bar["start"], bar["end"]
        seg = drums[int(a * 100):max(int(b * 100), int(a * 100) + 1), 0]
        db[i] = 10 * np.log10((float(np.cumsum(seg, dtype=np.float64)[-1]) / len(seg) if len(seg) else 0.0) + 1e-10)   # summed in order: as .NET
        v = np.zeros((3, 16))
        for s in range(16):
            g0, g1 = a + (b - a) * s / 16, a + (b - a) * (s + 1) / 16
            lo = int(g0 * 100)
            seg = drums[lo:max(int(g1 * 100), lo + 1), 1:]
            if len(seg):
                v[:, s] = seg.max(0)
        v = v.ravel()
        pattern[i] = v / (math.sqrt(float(v @ v)) + 1e-9)
    return db, pattern


def drum_cues(drums, n):
    """For each bar line, how the drums change across it (`drums`: drum_bars, or None: none heard, all 0): the bar's
    loudness against the song's, over 2 and 4 bars how much louder after than before and how far the mean pattern after
    is from the one before (cosine distance), and the bar's pattern against the bar before's. Only when DP_FILE was
    fitted with them (its "drums")."""
    if not dp_model().get("drums"):
        return np.zeros((n, 0))
    return drum_rows(drums, n)


def drum_rows(drums, n):
    """drum_cues, whatever DP_FILE was fitted with (scratch/xsc/dp.py DRUMS=1)."""
    if drums is None:
        return np.zeros((n, 6))
    db, pattern = drums
    z = (db - np.median(db)) / (np.std(db) + 1e-9)
    rows = []
    for i in range(n):
        f = [z[i]]
        for w in (2, 4):
            if i - w < 0 or i + w > n:
                f += [0.0, 0.0]
                continue
            u, v = pattern[i - w:i].mean(0), pattern[i:i + w].mean(0)
            f += [z[i:i + w].mean() - z[i - w:i].mean(), 1 - float(u @ v / (math.sqrt(float(u @ u)) * math.sqrt(float(v @ v)) + 1e-9))]
        f.append(1 - float(pattern[i] @ pattern[i - 1]) if i > 0 else 0.0)
        rows.append(f)
    return np.array(rows)


def section_rows(fam, tonic, home, sung, why, sings, begins, turns, db, notes, drums=None):
    """What SECTION_DP weighs for each bar line: section_cues, audio_cues and drum_cues of the bar, and those of the
    bars around it (dp_model's neighbours each side)."""
    n = len(fam)
    base = section_cues(fam, tonic, home, sung, why, sings, begins, turns)
    if db is None or notes is None:
        db, notes = np.zeros(n), np.zeros((n, 12))
    au = audio_cues(np.asarray(db, float), np.asarray(notes, float), [set(f) for f in fam])
    x = np.hstack([base, au, drum_cues(drums, n)])
    nb = dp_model()["neighbours"]
    z = np.zeros((nb, x.shape[1]))
    pad = np.vstack([z, x, z])
    return np.hstack([x] + [pad[nb + k: nb + k + n] for k in range(-nb, nb + 1) if k])


def played_again(fam, longest=63):
    """again[a, L]: bars a .. a+L-1 are played again elsewhere, as long, not overlapping (PHRASE_ALIKE of the beats)."""
    n = len(fam)
    same = np.array([[sum(x == y for x, y in zip(fam[i], fam[j])) / max(len(fam[i]), 1) for j in range(n)] for i in range(n)])
    diag = np.zeros((n + 1, n + 1))              # diag[i+1, j+1] = same[i, j] + diag[i, j]
    for i in range(n):
        diag[i + 1, 1:] = same[i] + diag[i, :-1]
    again = np.zeros((n + 1, longest + 1))
    for length in range(2, longest + 1):
        h = np.arange(0, n - length + 1)
        for a in range(0, n - length + 1):
            ok = h[(h + length <= a) | (h >= a + length)]
            if len(ok):
                again[a, length] = float(((diag[a + length, ok + length] - diag[a, ok]) / length).max() >= PHRASE_ALIKE)
    return again


def choose_cuts(score, lengths=None, lam=None, off=None, again=None, mu=None):
    """The cuts (bars 1 .. n-1) that make the most of: each cut's score less `off`; `lam` times the log share of each
    section's length among the owner's; `mu` for each section played again elsewhere (`again`, played_again)."""
    model = dp_model()
    lengths = model["lengths"] if lengths is None else lengths
    lam = model["lam"] if lam is None else lam
    off = model["off"] if off is None else off
    mu = (model["mu"] if again is not None else 0.0) if mu is None else mu
    total = sum(lengths.values()) + 0.2 * 63
    logp = np.array([-1e9] + [math.log((lengths.get(k, 0) + 0.2) / total) for k in range(1, 64)])   # math.log: as .NET's
    n = len(score)
    best = np.full(n + 1, -1e18)
    best[0] = 0.0
    back = np.zeros(n + 1, int)
    for j in range(1, n + 1):
        lo = max(0, j - 63)
        a = np.arange(lo, j)
        cand = best[lo:j] + lam * logp[j - a] + (mu * again[a, j - a] if mu else 0.0)
        k = int(np.argmax(cand))
        best[j] = cand[k] + (score[j] - off if j < n else 0.0)
        back[j] = lo + k
    cuts, j = [], n
    while j > 0:
        j = back[j]
        if j > 0:
            cuts.append(j)
    return sorted(cuts)


LETTERS = "ABCDEFGHIJKLMNOPQSTUVWXYZ"   # no R: R2, the second playing of R, reads in Lily# as a whole-bar rest of a half


def section_letter(k):
    """The name of the k-th section that plays none before it: A, B, … (no R), then S26, S27, …"""
    return LETTERS[k] if k < len(LETTERS) else f"S{k + 1}"


def phrase_sections(keys, turns, tonic, mode, phrase=4, sings=frozenset(), begins=frozenset(), sung=None, db=None, notes=None, drums=None):
    """The sections, as the owner cut 糸 (2026-11-16): the music is cut into blocks where it turns (singing
    begins or stops, the key changes, it grows far louder or quieter); a block of WHOLE_BLOCK bars or
    phrases or fewer is one section; a longer one is cut on a grid of `phrase` bars from its start, where a phrase
    said twice has ended (A: four bars, the same four again), where the dominant goes to the home
    chord across the bar line, or after a pair of phrases where a new pair begins (PAIR_HEARD), but
    never between a phrase and its saying again. A section that plays
    one before it again (PHRASE_ALIKE of the beats of the shorter, at their starts) is named after it:
    A, A2, A3. `keys`: the chord on each beat of each bar. Returns the form and, for each bar, its
    section and which bar of it it is."""
    n = len(keys)
    fam = [tuple((r, _KIND[q]) for r, q in k) for k in keys]
    home = "maj" if mode == "major" else "min"
    given = (set(sings), set(begins), set(turns))

    def alike(a, b, length):
        beats = [x == y for i in range(length) for x, y in zip(fam[a + i], fam[b + i])]
        return bool(beats) and sum(beats) >= PHRASE_ALIKE * len(beats)

    def cadence(g):
        last, first = fam[g - 1][-1], fam[g][0]
        return last[0] == (tonic + 7) % 12 and last[1] in ("maj", "sus") and first == (tonic, home)

    def begins_again(g):
        """A phrase from bar g is played elsewhere too, and begins there as here (the bar before differs)."""
        if g <= 0 or g + phrase > n:
            return False
        return any(abs(h - g) >= phrase and alike(g, h, phrase) and not (h > 0 and alike(g - 1, h - 1, 1))
                   for h in range(n - phrase + 1))

    def always_begins(g):
        """A phrase from bar g is played elsewhere, and wherever it is, the bar before it is not the bar before it here."""
        if g <= 0 or g + phrase > n:
            return False
        heard = [h for h in range(1, n - phrase + 1) if abs(h - g) >= phrase and alike(g, h, phrase)]
        return bool(heard) and sum(not alike(g - 1, h - 1, 1) for h in heard) >= BEGIN_SHARE * len(heard)

    if LEAD_IN:                              # a bar wholly on a dominant that resolves across the bar line leads in: the turn is after it
        def lead(s):
            return s + 1 if 0 < s < n - 1 and len(set(fam[s])) == 1 and fam[s][0][1] in ("maj", "sus") \
                and fam[s][0][0] == (fam[s + 1][0][0] + 7) % 12 and fam[s + 1][0][1] in ("maj", "min") else s
        sings, begins = {lead(s) for s in sings}, {lead(s) for s in begins}
        if LEAD_IN == 2:
            turns = {lead(t) for t in turns}
    # a turn a bar after where a phrase played elsewhere begins is that phrase's (My Life: the riff begins on
    # the D its key shares with the song's; the key is seen to change a bar into it)
    if SUNG_WHOLE:                           # from where singing begins, phrases played the same elsewhere are not cut inside
        span, kept_s, kept_t, a = SUNG_WHOLE * phrase, set(), set(turns), None
        for s in sorted(sings):              # and a turn of key or loudness that would stand for the singing dropped goes too
            if a is not None and a < s < a + span:
                kept_t -= {t for t in turns if a < t < a + span and abs(t - s) <= TURN_NEAR}
                continue
            kept_s.add(s)
            a = s if s in begins and s + span <= n and any(
                abs(h - s) >= span and alike(s, h, span) for h in range(n - span + 1)) else None
        sings, turns = kept_s, kept_t
    def held_on(g):                          # a cut on a chord other than the home chord held across the bar line: where the home chord comes
        if not (0 < g < n - 1) or fam[g - 1][-1] != fam[g][0] or fam[g][0] == (tonic, home):
            return g
        return g + next((k for k in range(1, HELD_SHIFT + 1) if g + k < n - 1 and fam[g + k - 1][-1] != fam[g + k][0]
                         and fam[g + k][0] == (tonic, home)), 0)

    moved = {t: t - 1 if begins_again(t - 1) and not begins_again(t) else t for t in turns if 0 < t < n}
    # where singing begins or stops is where the section does, and another turn this near it is the same one, a bar
    # off; but not a turn where a phrase played elsewhere begins (My Life: the riff at 135, the voice ending at 138)
    kept, others = set(), set(moved)
    for s in sorted(sings):                  # in order, and the turns near in order (the same in .NET)
        near = sorted(t for t in others if abs(t - s) <= TURN_NEAR)
        strong = [moved[t] for t in near if begins_again(moved[t])]
        if HELD_SINGS and HELD_SHIFT and (HELD_SINGS == 2 or s not in begins) and (sung is None or sung[s] >= HELD_SUNG):
            s, strong = held_on(s), [held_on(t) for t in strong]
        kept.add(strong[0] if strong else s)
        others -= set(near)
        kept |= set(strong)
    edges = sorted({0, n} | {t for t in kept | {moved[t] for t in others} if 0 < t < n})
    cuts = set(edges)
    WHY.clear()                                  # why each cut is there (for measuring only)
    WHY.update({t: "turn" for t in edges})
    WHY.update({t: "sing" for t in kept if t in WHY})
    # a block the turns close in, played again whole elsewhere, is a section there too: its first bar and the bar
    # after its last are cuts (My Life: the verse, 14 bars between where singing begins and the riff, ends where
    # the bridge begins, the second time). Played again right after itself, it is one section (the riff twice).
    # Not a block that is itself a piece of a longer one played elsewhere (My Life: the verse's first half played by
    # the band), and not where it falls inside another block but at that block's start (糸: the interlude's G C,
    # four bars inside the intro's eight).
    blocks = [(a, b) for a, b in zip(edges, edges[1:]) if b - a >= phrase]

    def heard_at(a, b):
        found, h = [], 0
        while h + (b - a) <= n:
            if (h + (b - a) <= a or h >= b) and alike(h, a, b - a):
                found.append(h)
                h += b - a
            else:
                h += 1
        return found

    played = {blk: heard_at(*blk) for blk in blocks}
    played_cuts = set()                          # the ends of blocks played again whole: the grid may count from them
    for a, b in blocks:
        length = b - a
        if any(d - c > length and any(h <= a and a + length <= h + d - c for h in played[(c, d)]) for c, d in blocks):
            continue
        runs = []                                # playings one right after the other are one run
        for h in played[(a, b)]:
            if runs and runs[-1][1] == h:
                runs[-1][1] = h + length
            else:
                runs.append([h, h + length])
        for h, e in runs:
            if h == b:                           # right after the block itself: one run with it
                cuts.add(e)
                WHY.setdefault(e, "played")
                played_cuts.update((a, e))
            elif next(c for c, d in zip(edges, edges[1:]) if c <= h < d) == h:
                cuts.update((h, e))
                WHY.setdefault(h, "played"); WHY.setdefault(e, "played")
                played_cuts.update((a, b, h, e))
    edges = sorted(c for c in cuts if 0 <= c <= n)
    # a turn of key or loudness cuts, but the grid goes on from where singing last began or stopped, or a block played
    # again whole begins or ends (rwc-dev: 50 of 131 turns fall on an AIST boundary, 114 of 141 places singing begins;
    # a turn in the middle of a section of 8 moved the grid by 2 bars)
    only_turns = {moved[t] for t in others} - kept - played_cuts
    for a, b in zip(edges, edges[1:]):
        if b - a <= WHOLE_BLOCK * phrase:
            continue
        p = (max(k for k in edges if k <= a and k not in only_turns) - a) % phrase if GRID_ANCHOR else 0
        g = a + (p or phrase) - phrase
        while g + phrase < b - 1:
            g += phrase
            middle = g - phrase >= a and g + phrase <= b and alike(g - phrase, g, phrase)
            ended = (g - 2 * phrase >= a and alike(g - 2 * phrase, g - phrase, phrase)
                     and not (g + phrase <= b and alike(g - phrase, g, phrase)))
            pair = (PAIR_HEARD and g - a - p >= 2 * phrase and (g - a - p) % (2 * phrase) == 0 and g + phrase <= b
                    and not alike(g - 2 * phrase, g, phrase)
                    and sum(1 for h in range(n - phrase + 1) if abs(h - g) >= phrase and alike(g, h, phrase)) >= PAIR_HEARD)
            if not middle and (ended or cadence(g) or pair or (BEGIN_SHARE and g + phrase <= b and always_begins(g))):
                s = 0
                if HELD_SHIFT and fam[g - 1][-1] == fam[g][0] and (HELD_SHIFT_LOOSE or fam[g][0] != (tonic, home)):
                    s = next((k for k in range(1, HELD_SHIFT + 1) if g + k < b - 1 and fam[g + k - 1][-1] != fam[g + k][0]
                              and (HELD_SHIFT_LOOSE or fam[g + k][0] == (tonic, home))), 0)
                cuts.add(g + s)
                WHY.setdefault(g + s, "ended" if ended else "cadence" if cadence(g) else "pair" if pair else "begins")
                if HELD_REANCHOR:
                    g += s
                    p += s
    cuts = sorted(cuts)
    if SECTION_DP:                               # the cuts chosen whole: cues weighed per bar line, lengths as the owner's
        model = dp_model()
        rows = section_rows(fam, tonic, home, sung, WHY, *given, db, notes, drums)
        score = np.round(rows @ np.array(model["weights"]) + model["bias"], 9)   # rounded: the same in .NET, whatever order it adds in
        cuts = [0] + choose_cuts(score, again=played_again(fam)) + [n]
    elif MERGE_SHORT:                            # a piece of fewer bars than this joins the one before (the first: the one after)
        while True:
            short = [i for i in range(1, len(cuts)) if cuts[i] - cuts[i - 1] < MERGE_SHORT]
            if not short or len(cuts) <= 2:
                break
            i = short[0]
            cuts.pop(i if i == 1 else i - 1)
    pieces = list(zip(cuts, cuts[1:]))
    bases, names, count = [], [], Counter()
    for a, b in pieces:
        best = None
        for k, (c, d) in enumerate(bases):
            length = min(b - a, d - c)
            if length >= min(phrase, b - a) and alike(a, c, length):
                same = sum(x == y for i in range(length) for x, y in zip(fam[a + i], fam[c + i]))
                if best is None or same > best[0]:
                    best = (same, k)
        if best is None:
            bases.append((a, b))
            k = len(bases) - 1
        else:
            k = best[1]
        letter = section_letter(k)
        count[letter] += 1
        names.append(letter if count[letter] == 1 else f"{letter}{count[letter]}")
    return names, [(name, i + 1) for name, (a, b) in zip(names, pieces) for i in range(b - a)]


def fold(bars, beats, alike, turns=frozenset()):
    """Finds the sections (find_form) and makes every playing of a section say the same: on each
    beat, the chord the playings are surest of together. Where they differed in root or family,
    the chord is marked in every playing, and the chord offered in its place is the other reading.
    Chords are (root, quality, bass) here. Returns the form."""
    kind = lambda c: (c["symbol"][0], _KIND[c["symbol"][1]])
    on = [[next((c for c in bar["chords"] if c["start"] <= bar["start"] + (i + 0.01) * (bar["end"] - bar["start"]) / beats < c["end"]),
                bar["chords"][-1]) for i in range(beats)] for bar in bars]
    form, places = find_form([tuple(c["symbol"][:2] for c in row) for row in on], alike, turns)
    said = {}
    for n, place in enumerate(places):
        bars[n]["section"], bars[n]["barOfSection"] = place
        said.setdefault(place, []).append(n)
    for playings in said.values():
        if len(playings) < 2 or all([c["symbol"] for c in on[n]] == [c["symbol"] for c in on[playings[0]]] for n in playings):
            continue
        agreed = []                             # for each beat: (the chord, how sure, the other reading or None)
        for b in range(beats):
            heard = [on[n][b] for n in playings]
            kinds, symbols = Counter(), Counter()
            for c in heard:
                kinds[kind(c)] += c.get("sure", 1.0)
            won = max(kinds, key=kinds.get)
            for c in heard:
                if kind(c) == won:
                    symbols[c["symbol"]] += c.get("sure", 1.0)
            symbol = max(symbols, key=symbols.get)
            sure = float(np.mean([c.get("sure", 1.0) for c in heard if kind(c) == won]))
            others = Counter()
            for c in heard:
                if kind(c) != won:
                    others[c["symbol"][:2]] += c.get("sure", 1.0)
            agreed.append((symbol, sure, max(others, key=others.get) if others else None))
        for n in playings:
            bar = bars[n]
            marked = "sure" in bar["chords"][0]
            if all(kind(c) == (a[0][0], _KIND[a[0][1]]) for c, a in zip(on[n], agreed)):
                for c, a in zip(on[n][::-1], agreed[::-1]):     # the same chords, named as agreed (a seventh, a bass note)
                    c["symbol"] = a[0]
            else:                                               # written again, a chord a beat
                step = (bar["end"] - bar["start"]) / beats
                chords = []
                for b, (symbol, sure, other) in enumerate(agreed):
                    if chords and chords[-1]["symbol"] == symbol:
                        chords[-1]["end"] = round(bar["start"] + (b + 1) * step, 4) if b < beats - 1 else bar["end"]
                        continue
                    chords.append({"start": round(bar["start"] + b * step, 4), "end": round(bar["start"] + (b + 1) * step, 4) if b < beats - 1 else bar["end"],
                                   "symbol": symbol, "fit": on[n][b]["fit"]})
                    if "why" in on[n][b]:
                        chords[-1]["why"] = on[n][b]["why"]
                    if marked:
                        chords[-1]["sure"] = round(sure, 3)
                        chords[-1]["other"] = on[n][b]["other"] if kind(on[n][b]) == (symbol[0], _KIND[symbol[1]]) else on[n][b]["symbol"][:2]
                bar["chords"] = chords
            if marked:
                for b, (symbol, sure, other) in enumerate(agreed):
                    if other is not None:
                        at = bar["start"] + (b + 0.01) * (bar["end"] - bar["start"]) / beats
                        c = next(c for c in bar["chords"] if c["start"] <= at < c["end"])
                        c["sure"], c["other"] = min(c["sure"], round(DOUBT - 0.001, 3)), other
    return form


def lead_off(bars):
    """The second ending (the owner, 2026-11-16, on 糸): where a section X is followed once by a short
    section played once, C of k bars, and another time by a longer section played once, D, whose
    k-th bar ends on the root C ends on, those k bars of D are the phrase X began, as C is: they
    become a section of their own, and D begins where its phrase does (name_sections names it).
    Chords are (root, quality, bass) here."""
    played = []                         # [section, first bar, bars] for each section played
    for i, bar in enumerate(bars):
        if bar["barOfSection"] == 1:
            played.append([bar["section"], i, 0])
        played[-1][2] += 1
    count = Counter(p[0] for p in played)
    for n in range(1, len(played)):
        name, first, length = played[n]
        if name == "Pickup" or count[name] > 1:
            continue
        ending = next((played[m + 1] for m in range(len(played) - 1) if m + 1 != n and played[m][0] == played[n - 1][0]
                       and count[played[m + 1][0]] == 1 and played[m + 1][2] <= SHORT_SECTION), None)
        if ending is None or ending[2] >= length:
            continue
        k = ending[2]
        if bars[ending[1] + k - 1]["chords"][-1]["symbol"][0] != bars[first + k - 1]["chords"][-1]["symbol"][0]:
            continue
        for b in range(first, first + k):
            bars[b]["section"] = (name, "lead")
        for b in range(first + k, first + length):
            bars[b]["barOfSection"] -= k


def name_sections(bars):
    """Names the sections in the order they come: A, B, C … for those of HIDE_SHORT bars or more; a
    shorter one is named after the section played before it, B_1, B_2 …, and the chart hides its
    name (`~B_1` in the form), so that no letter is missing (the owner, 2026-11-16). Returns the form."""
    lengths = {}
    for bar in bars:
        lengths[bar["section"]] = max(lengths.get(bar["section"], 0), bar["barOfSection"])
    names, shown, under, before = {"Pickup": "Pickup"}, 0, Counter(), None
    for bar in bars:
        s = bar["section"]
        if s not in names:
            if lengths[s] >= HIDE_SHORT:
                names[s] = section_letter(shown)
                shown += 1
            else:
                base = before or section_letter(shown)
                under[base] += 1
                names[s] = f"{base}_{under[base]}"
        if s != "Pickup" and "_" not in names[s]:
            before = names[s]
    for bar in bars:
        bar["section"] = names[bar["section"]]
    return [bar["section"] for bar in bars if bar["barOfSection"] == 1]


def cut_long(total, found):
    """Where a run of `total` bars played once is cut into sections: whole when LONG_SECTION bars or
    fewer; else at the sections `found` there (bars from the run's start: where the music turns) or
    every eight bars from the run's start, never evenly for its own sake (the owner, 2026-11-16).
    Of the ways that keep each within LONG_SECTION: the fewest sections, then the fewest cuts at no
    section found, then the least uneven. [(first bar, end)] from the run's start."""
    if total <= LONG_SECTION:
        return [(0, total)]
    places = [0] + sorted(found | set(range(8, total, 8))) + [total]
    best = {0: (0, 0, 0, -1)}           # place: (sections, cuts at no section found, sum of squared lengths, the cut before)
    for b in places[1:]:
        ways = [(best[a][0] + 1, best[a][1] + (0 if b == total or b in found else 1), best[a][2] + (b - a) ** 2, a)
                for a in places if a < b and a in best and b - a <= LONG_SECTION]
        if ways:
            best[b] = min(ways)
    spans, b = [], total
    while b:
        a = best[b][3]
        spans.append((a, b))
        b = a
    return spans[::-1]


def beat_names(bar, beats):
    """The chord on each beat of a bar: what the chart writes of it."""
    length = (bar["end"] - bar["start"]) / beats
    names = []
    for i in range(beats):
        at = bar["start"] + (i + 0.01) * length
        names.append(next((c["symbol"] for c in bar["chords"] if c["start"] <= at < c["end"]),
                          bar["chords"][-1]["symbol"]))
    return tuple(names)


def find_form(keys, alike=1.0, turns=frozenset()):
    """The sections a chart is made of, from the chord on each beat of each bar (`keys`: a tuple a
    bar): (the form, a section's name for each one played; for each bar, its section and which
    bar of it it is).

    Two runs of bars are the same section when `alike` of their beats have the same chord. The
    run whose saying again saves the most bars becomes a section, the longer run if two save as
    many; then the same among the bars left, until nothing is said twice. What is left between
    sections is a section said once. Two sections that only ever come one after the other are one,
    and so are sections of SHORT_SECTION bars or fewer that come one after the other.

    `turns`: bars where the music turns (turns_of). No section runs across one, but within
    TURN_SLACK bars of its ends; what is said once is cut there; nothing is joined there.
    """
    n = len(keys)
    at_turn = lambda b: any(abs(b - t) <= TURN_SLACK for t in turns)
    ids = {}
    grid = np.full((n, max([len(k) for k in keys] + [1])), -1)
    for i, k in enumerate(keys):
        grid[i, :len(k)] = [ids.setdefault(x, len(ids)) for x in k]
    same = ((grid[:, None, :] == grid[None, :, :]) & (grid[:, None, :] >= 0)).mean(axis=2)   # bar against bar
    apart = np.abs(np.arange(n)[:, None] - np.arange(n)[None, :])
    close, run = {}, np.zeros((n, n))           # for each length: do the runs from bar i and from bar j say the same?
    for length in range(1, n // 2 + 1):
        m = n - length + 1
        run = run[:m, :m] + same[length - 1:, length - 1:]
        if length >= SECTION_BARS:
            close[length] = (run >= alike * length - 1e-9) & (apart[:m, :m] >= length)
    owner = [None] * n
    played = []                                 # (first bar, bars, section) for each section played
    while True:
        free = [0] * (n + 1)                    # how many bars from here on belong to no section yet
        for i in range(n - 1, -1, -1):
            free[i] = 0 if owner[i] is not None else free[i + 1] + 1
        free = np.array(free[:n])
        best = (0, 0, None)
        for length, near in close.items():
            can = free[:len(near)] >= length
            near = near & can[None, :] & can[:, None]
            for i in np.nonzero(near.sum(axis=1) * length >= max(best[0], 1))[0]:
                kept = [int(i)]
                for j in np.nonzero(near[i])[0]:
                    if all(abs(int(j) - k) >= length for k in kept):
                        kept.append(int(j))
                saved = (len(kept) - 1) * length
                if (saved, length) > best[:2]:
                    best = (saved, length, kept)
        if not best[0]:
            break
        section = len(played)
        for s in best[2]:
            played.append((s, best[1], section))
            owner[s:s + best[1]] = [section] * best[1]
    i = 0
    while i < n:                                # what is said once
        if owner[i] is None:
            j = i + 1
            while j < n and owner[j] is None and j not in turns:
                j += 1
            played.append((i, j - i, len(played)))
            i = j
        else:
            i += 1
    played.sort()
    if turns:                                   # a section the music turns inside is cut there, each time it is played
        cuts = {}
        for start, length, s in played:
            cuts.setdefault(s, set()).update(t - start for t in turns if TURN_SLACK < t - start < length - TURN_SLACK)
        played = sorted((start + a, b - a, (s, a) if cuts[s] else s) for start, length, s in played
                        for a, b in zip([0] + sorted(cuts[s]), sorted(cuts[s]) + [length]))
    while True:                                 # A B ... A B: one section
        order = [s for _, _, s in played]
        count = Counter(order)
        pairs = Counter(zip(order, order[1:]))
        parted = {(played[k][2], played[k + 1][2]) for k in range(len(played) - 1) if at_turn(played[k + 1][0])}
        pair = next(((a, b) for (a, b), k in pairs.items() if a != b and k == count[a] == count[b] and (a, b) not in parted), None)
        if pair is None:
            break
        merged, i = [], 0
        while i < len(played):
            if played[i][2] == pair[0]:
                merged.append((played[i][0], played[i][1] + played[i + 1][1], pair[0]))
                i += 2
            else:
                merged.append(played[i])
                i += 1
        played = merged
    if SHORT_SECTION:                           # short sections running are one: until it is longer, or the next is not short
        # (a short section played twice running too: four bars said twice are eight written out, the owner, 2026-11-16)
        joined, i = [], 0
        while i < len(played):
            start, bars, group = played[i][0], played[i][1], (played[i][2],)
            while (bars <= SHORT_SECTION and i + 1 < len(played) and played[i + 1][1] <= SHORT_SECTION
                   and not at_turn(played[i + 1][0])):
                i += 1
                bars += played[i][1]
                group += (played[i][2],)
            joined.append((start, bars, group))
            i += 1
        played = joined
    if LONG_SECTION:                            # sections played once, one after the other, are one, cut where it is too long
        count = Counter(s for _, _, s in played)
        joined, i = [], 0
        while i < len(played):
            if count[played[i][2]] > 1:
                joined.append(played[i])
                i += 1
                continue
            j = i + 1
            while j < len(played) and count[played[j][2]] == 1 and not at_turn(played[j][0]):
                j += 1
            start = played[i][0]
            total = sum(bars for _, bars, _ in played[i:j])
            found = {first - start for first, _, _ in played[i + 1:j]}
            for a, b in cut_long(total, found):
                joined.append((start + a, b - a, ("once", start + a)))
            i = j
        # a section played more than once and longer than LONG_SECTION is cut the same way each time
        played = [(start + a, b - a, (s, a)) if count[s] > 1 else (start, bars, s)
                  for start, bars, s in joined for a, b in (cut_long(bars, set()) if count[s] > 1 else [(0, bars)])]
    names = {}
    for _, _, s in played:
        if s not in names:
            k = len(names)
            names[s] = section_letter(k)
    return [names[s] for _, _, s in played], [(names[s], k + 1) for _, bars, s in played for k in range(bars)]


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
        # how far from A = 440 Hz, in cents: the same pitches as the chromas' tuning, placed among the semitones
        "cents": np.float32(round(100 * float(librosa.estimate_tuning(y=y, sr=SR, bins_per_octave=12)))),
    }


def read(path, f=None, tempo=None, lines=None, units=8, beats=None, heard=None, why=False, voice=None, drums=None):
    """The chart of a recording. `tempo`, if told, is about how many beats a minute it has: what
    is heard cannot tell a fast song whose chords last two bars from a slow one, bar by bar.

    `lines`, if given, are the bar lines in seconds (the end of the last bar too), found by other
    means: then only the chords are read, each bar cut into `units` equal parts. `beats`, if a
    beat tracker found the lines, is how many beats it counted in a bar, and says the meter:
    two beats are two quarters or, when the sound between them falls in threes, the two dotted
    quarters of 6/8. `heard`, if given, is what a model trained on recordings says the chords are
    (the logp and step of tools/btc_chords.py --probs), in place of this reader's own hearing."""
    name = Path(path).name
    ears = heard is not None
    f = f or features(path)
    oenv, chroma, bass, rms = f["oenv"], f["chroma"], f["bass"], f["rms"]
    if lines is not None:
        marks = np.asarray(lines, dtype=float) * SR / HOP + BEAT_LAG
        marks = marks[marks < len(oenv)]
        if beats:
            six = beats == 6 or (beats == 2 and find_meter(marks, oenv)[0] != "4/4")
            if beats == 2 and not six and float(np.median(np.diff(marks))) * HOP / SR > SLOW_TWO:
                beats = 4       # the tracker counted the halves of a bar of four
            units = 6 if six or beats == 3 else 2 * beats
        # The music may begin before the first line found and go on after the last: the bars are
        # carried on, as long as the first and the last, to both ends of the recording. What of
        # them is in the chart is settled below, by what sounds there.
        gaps = np.diff(marks)
        if len(gaps) > 4 and gaps[0] < 0.8 * np.median(gaps[1:5]):    # a first line less than a bar before the second
            marks, gaps = marks[1:], gaps[1:]                         # begins a pickup, not a bar: the units below reach it
        before, after = float(np.median(gaps[:4])) / units, float(np.median(gaps[-5:-1] if len(gaps) > 1 else gaps)) / units
        stopped = gaps[-1] < 0.75 * after * units   # the last line is where the beats stopped, not a bar's end
        if stopped:
            marks = np.append(marks[:-1], marks[-2] + after * units) if marks[-2] + after * units < len(oenv) else marks[:-1]
        led = int(max(marks[0] - BEAT_LAG, 0.0) // before)
        edges = np.concatenate([marks[0] - before * np.arange(led, 0, -1)]
                               + [np.linspace(a, b, units, endpoint=False) for a, b in zip(marks[:-1], marks[1:])]
                               + [marks[-1:], marks[-1] + after * np.arange(1, int((len(oenv) - 1 - marks[-1]) // after) + 1)])
        count = len(edges) - 1
        carried = led + (len(gaps) - stopped) * units     # from this unit on, the bars are past the last one found
        loud = np.array([float(over(rms, a, b)[0]) for a, b in zip(edges[:-1], edges[1:])])
    else:
        led = carried = None
        tracked, period = beat_grid(oenv)
        edges = half_beats(tracked, period)
        loud = np.array([float(over(rms, a, b)[0]) for a, b in zip(edges[:-1], edges[1:])])
        count = len(loud)
        while count > 8 and loud[count - 1] < QUIET * np.median(loud):  # the ring-out is not music
            count -= 1
        edges, loud = edges[:count + 1], loud[:count]
    seconds = np.diff(edges) * HOP / SR
    # Where a chord may be. This reader's own hearing has no word for "no chord": what is far
    # quieter than the song is silence. The ears have one, and a fade-out is quiet and still music.
    # (Only what is a hundred times quieter is silence to them: their "no chord" comes half a second late
    # after a silence, and before the music they hear its first chord already.)
    # A unit is as loud as its middle frame here: its edges are late by the lag of the onsets, and
    # the unit before the music has the first frames of it.
    middle = np.array([float(over(rms, a, b, np.median)[0]) for a, b in zip(edges[:-1], edges[1:])])
    sounds = middle >= (QUIET if heard is None else HUSH) * np.median(loud)

    treble = unit(np.array([over(chroma, a, b) for a, b in zip(edges[:-1], edges[1:])]).T)   # (12, units)
    low = unit(np.array([over(bass, a, b) for a, b in zip(edges[:-1], edges[1:])]).T)
    said = None
    if heard is not None:
        said = np.maximum(said_in(heard, (edges - BEAT_LAG) * HOP / SR), LOG_FLOOR)
        sounds &= np.exp(said_in(heard, (edges - BEAT_LAG) * HOP / SR, NONE))[:, 0] <= NO_CHORD
        emit = EMIT_SCALE * said * seconds[:, None]
    elif MODEL is None:
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

    strange = False
    if lines is not None:
        length = units
        _, path = decode(emit, heard, change, length, first=-led % length)
    else:
        path, length = search(emit, heard)
    if MODEL is not None and lines is None:
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
    given = lines is not None
    lines = np.array([edges[t] for t in starts])
    time, tempo_note, tempo_beats = find_meter(lines, oenv)
    if beats:
        time, tempo_note, tempo_beats = ("6/8", "4.", 2) if six else (f"{beats}/4", "4", beats)
    elif given and units != 6 and (time, units) != ("4/4", 8):   # the beats in a bar came with the lines
        time, tempo_note, tempo_beats = f"{units // 2}/4", "4", units // 2
    elif given and units == 6 and time == "4/4":
        time, tempo_note, tempo_beats = "3/4", "4", 3

    # The bars of the chart: from the first that sounds a chord to the last. Silence before the
    # music, a count-in, the ring-out and the bars carried on past them are not in it.
    spans = list(zip(starts[:-1], starts[1:]))
    if 0 < count - starts[-1] < length:             # the recording ends inside a bar
        spans.append((starts[-1], count))
        carried = starts[-1] if carried is None else carried
    chorded = lambda span: float(np.sum(sounds[span[0]:span[1]])) >= CHORDED * length
    while spans and not chorded(spans[0]):
        spans.pop(0)
    while spans and not chorded(spans[-1]):
        spans.pop()
    if not spans:
        raise ValueError("no chord is heard")
    # Past the last bar found, what dies away is the last chord ringing out, not one more bar
    # (a song that fades out loses less from bar to bar; what the ears name there is often another chord).
    while (carried is not None and len(spans) > 1 and spans[-1][0] >= carried
           and loud[spans[-1][0]:spans[-1][1]].mean() < RING_OUT * loud[spans[-2][0]:spans[-2][1]].mean()):
        spans.pop()
    # A pickup: whole beats of chord that lead into the first bar, in a short bar of their own.
    # A first bar that begins with nothing is one, not a bar.
    per = length / tempo_beats                      # units in a beat
    still = next((i for i in range(spans[0][1] - spans[0][0]) if sounds[spans[0][0] + i]), 0)
    if len(spans) > 1 and still:
        spans.pop(0)
    t = one = spans[0][0]
    while t > 0 and sounds[t - 1] and one - t + 1 < length:
        t -= 1
    if one - t == length - 1:                       # all of a bar but its first unit (unheard, or before the grid begins): the first bar
        spans.insert(0, (max(one - length, 0), one))
        t = one = spans[0][0]
    upbeats = min(int((one - t) / per + 1e-6), tempo_beats - 1)
    while upbeats and time != "6/8" and upbeats not in PARTIAL:
        upbeats -= 1
    led_in = max(one - int(np.ceil(upbeats * per - 1e-6)), t)

    stamp = lambda frame: round(max(float(frame) - BEAT_LAG, 0.0) * HOP / SR, 4)
    weighed = []

    def spell(first, after):
        """The chords of the units from `first` to `after`."""
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
            if said is not None:
                # how sure the model is of it, half beat by half beat, and what else it might be
                # (of its root and whether it is major or minor: a seventh heard or not is a
                # smaller doubt, and would mark one chord in four)
                same = SAME[path[t][1]]
                p = np.exp(said[t:end])
                mine = p[:, same].sum(axis=1)
                plain = (TEMPLATES @ treble[:, t:end]).mean(axis=1) - COSTS     # this reader's own hearing
                chord["sure"] = round(float(mine.mean()), 3)
                weighed.append((float(mine.mean()), float(mine[-1]), float(same[int(np.argmax(plain))])))
                mean = np.where(same, -np.inf, said[t:end].mean(axis=0))
                chord["other"] = CHORDS[int(np.argmax(mean))]
                if why:
                    lb = low[:, t:end].mean(axis=1)
                    chord["why"] = {
                        "logsure": float(np.log(np.maximum(mine, 1e-4)).mean()),
                        "entropy": float(-(p * said[t:end]).sum(axis=1).mean()),
                        "agree": float(same[int(np.argmax(plain))]),
                        "plaingap": float(plain[same].max() - plain[~same].max()),
                        "bassroot": float(lb[root]), "basstone": float(lb[tones].sum()), "vbroot": float(vb[root]),
                        "sure": float(mine.mean()), "low": float(mine.min()),
                        "root": float(p[:, ROOTS == root].sum(axis=1).mean()),
                        "next": float(p[:, SAME[int(np.argmax(mean))]].sum(axis=1).mean()),
                        "none": float(1.0 - min(p.sum(axis=1).mean(), 1.0)),
                        "units": end - t, "share": (end - t) / length, "seconds": float((b - a) * HOP / SR),
                        "first": float(mine[0]), "last": float(mine[-1]),
                        "onbar": float(t == first), "fit": fit, "slash": float(slash),
                        "kind": _KIND[quality],
                    }
            chords.append(chord)
            t = end
        return chords

    bars = []
    if upbeats and led_in < one:
        bars.append({"bar": 0, "pickup": upbeats, "start": stamp(edges[led_in]), "end": stamp(edges[one]),
                     "chords": spell(led_in, one)})
    for n, (first, after) in enumerate(spans):
        chords, end = spell(first, after), edges[after]
        if after - first < length and first == 0 and len(spans) > 1:    # the first bar, from where it began
            begun = max(edges[0] - (edges[1] - edges[0]), BEAT_LAG)
            bars.append({"bar": n + 1, "start": stamp(begun), "end": stamp(end), "chords": chords})
            chords[0]["start"] = stamp(begun)
            continue
        if after - first < length:                  # the bar the recording ends in is written whole
            end = edges[first] + (edges[first] - edges[first - length] if first >= length
                                  else length * (end - edges[first]) / (after - first))
            chords[-1]["end"] = stamp(end)
        bars.append({"bar": n + 1, "start": stamp(edges[first]), "end": stamp(end), "chords": chords})

    read_chords = [c for bar in bars for c in bar["chords"]]
    if ears and weighed:
        # A model trained on recordings is sure of itself more often than it is right. How likely
        # the root and the family are right is weighed from four things (SURE_OF).
        a_sure, a_last, a_agree, a_song, a_none = SURE_OF
        song = float(np.mean([w[0] for w in weighed]))
        for c, (mean_sure, last, agree) in zip(read_chords, weighed):
            z = a_sure * mean_sure + a_last * last + a_agree * agree + a_song * song + a_none
            c["sure"] = round(1.0 / (1.0 + float(np.exp(-z))), 3)
    for i, bar in enumerate(bars):          # where its beats begin, said after its lines
        items = list(bar.items())
        at = [k for k, _ in items].index("end") + 1
        bars[i] = dict(items[:at] + [("beats", beats_of(bar["start"], bar["end"], bar.get("pickup") or tempo_beats))]
                       + items[at:])
    whole = [bar for bar in bars if not bar.get("pickup")]
    turns, sings, begins, sung, db, notes = frozenset(), frozenset(), frozenset(), None, None, None
    if ears and PHRASE_SECTIONS:
        # what each bar sounds: the notes of the chords the ears hear in it, and how loud it is
        chord_starts = np.array([stamp(e) for e in edges[:-1]])
        heard_notes = np.exp(said) @ TONES * seconds[:, None]
        notes = np.array([heard_notes[(chord_starts >= bar["start"]) & (chord_starts < bar["end"])].sum(axis=0) for bar in whole])
        db = np.array([20.0 * np.log10(float(over(rms, bar["start"] * SR / HOP + BEAT_LAG, bar["end"] * SR / HOP + BEAT_LAG)[0]) + 1e-9)
                       for bar in whole])
        turns = frozenset(turns_of(notes, db))
        if voice is not None and len(voice) and float(np.percentile(voice, 95)) >= VOICE_FLOOR * float(np.percentile(rms, 95)):
            singing = voice >= VOICE_LOUD * float(np.percentile(voice, 95))
            sung = [float(np.mean(singing[int(bar["start"] * 100):max(int(bar["end"] * 100), int(bar["start"] * 100) + 1)]))
                    for bar in whole]
            sings = frozenset(voice_turns(sung))
            begins = frozenset(voice_turns(sung, begins=True))
            if VOICE_LATE:                  # singing that begins only in the bar's second half is a pickup: the section begins after it
                head = [float(np.mean(singing[int(bar["start"] * 100):max(int((bar["start"] + bar["end"]) * 50), int(bar["start"] * 100) + 1)]))
                        for bar in whole]
                late = {s for s in begins if s + 1 < len(whole) and head[s] < VOICE_LATE}
                sings = frozenset(s + 1 if s in late else s for s in sings)
                begins = frozenset(s + 1 if s in late else s for s in begins)
    beats_in_bar = 2 if time == "6/8" else int(time.split("/")[0])
    read_chords = [c for bar in bars for c in bar["chords"]]
    tonic, mode = find_key([(c["end"] - c["start"], c["symbol"][0], c["symbol"][1]) for c in read_chords])
    if PHRASE_SECTIONS:
        on = [[next((c for c in bar["chords"] if c["start"] <= bar["start"] + (i + 0.01) * (bar["end"] - bar["start"]) / beats_in_bar < c["end"]),
                    bar["chords"][-1]) for i in range(beats_in_bar)] for bar in whole]
        bar_seconds = float(np.median([bar["end"] - bar["start"] for bar in whole]))
        phrase = PHRASE_BARS or int(min(max(2 ** round(np.log2(PHRASE_SECONDS / bar_seconds)), 2), 16))
        form, places = phrase_sections([tuple(c["symbol"][:2] for c in row) for row in on], turns, tonic, mode, phrase, sings, begins, sung, db, notes,
                                       drum_bars(drums, whole) if drums is not None and ears else None)
        for bar, (section, k) in zip(whole, places):
            bar["section"], bar["barOfSection"] = section, k
    else:
        form = fold(whole, beats_in_bar, SAID_AGAIN, turns)
    if bars[0].get("pickup"):                       # a section of its own: Lily# lets only the first one open short
        bars[0]["section"], bars[0]["barOfSection"] = "Pickup", 1
        form = ["Pickup"] + form
    read_chords = [c for bar in bars for c in bar["chords"]]
    if not PHRASE_SECTIONS:
        tonic, mode = find_key([(c["end"] - c["start"], c["symbol"][0], c["symbol"][1]) for c in read_chords])
        if LEAD_OFF:
            lead_off(bars)
        form = name_sections(bars)
    tonic_name = (MAJOR_KEYS if mode == "major" else MINOR_KEYS)[tonic]
    flats = "b" in tonic_name or (tonic_name == "F" if mode == "major" else tonic_name in FLAT_MINORS)
    roots = FLAT_ROOTS if flats else SHARP_ROOTS
    for c in read_chords:
        root, quality, slash = c["symbol"]
        c["symbol"] = roots[root] + quality + ("/" + roots[slash] if slash is not None else "")
        if "other" in c:
            c["other"] = roots[c["other"][0]] + c["other"][1]
    bar_seconds = float(np.median(np.diff(lines))) * HOP / SR
    if EVEN_TEMPO and ears:      # the lines are a beat tracker's, on its grid of 20 ms: the bars about as long as the middle one, together
        gaps = np.diff(lines) * HOP / SR
        bar_seconds = float(np.mean(gaps[np.abs(gaps - bar_seconds) <= EVEN_TEMPO * bar_seconds]))
    bpm = int(round(60.0 * tempo_beats / bar_seconds))
    if TENTH_TEMPO and ears:
        bpm = round(60.0 * tempo_beats / bar_seconds, 1)
        bpm = int(bpm) if bpm == int(bpm) else bpm
    if ears:
        for i, meter in short_meters(bars, time).items():  # a bar of 2/4 or 3/4: its beats, and its meter after them
            items = list(bars[i].items())
            at = [k for k, _ in items].index("beats") + 1
            n = int(meter.split("/")[0])
            bars[i] = dict(items[:at - 1] + [("beats", beats_of(bars[i]["start"], bars[i]["end"], n)), ("time", meter)] + items[at:])
    if ears and PHRASE_SECTIONS:
        tempos, last = section_tempos(bars, tempo_beats, bpm), None
        for bar in bars:
            if bar["section"] != last and bar["section"] in tempos:
                bar["tempo"] = tempos[bar["section"]]
            last = bar["section"]
    return {                                    # docs/SIDECAR.md says what each of these is
        "dettato": FORMAT,
        "audio": name,
        "duration": round((len(oenv) - 1) * HOP / SR, 2),
        "tempo": {"note": tempo_note, "bpm": bpm},
        "time": time,
        "key": f"{tonic_name} {mode}",
        **({"tuning": int(f["cents"])} if "cents" in f else {}),
        **({"strange": True} if strange else {}),
        **({"doubt": DOUBT} if any("sure" in c for c in read_chords) else {}),
        "form": form,
        "bars": bars,
    }


def lysc_shape(name):
    """The guitar shape Lily#'s editor writes first for a chord name (`lysc shape D` -> "D\txx0232"), when Lily#
    says where its lysc is (LILYSHARP_LYSC_PATH, Lily# REQUESTS 9); else None. As Lysc.cs."""
    lysc = os.environ.get("LILYSHARP_LYSC_PATH")
    if not lysc or not os.path.isfile(lysc):
        return None
    try:
        done = subprocess.run([lysc, "shape", name], capture_output=True, text=True, encoding="utf-8", timeout=20)
    except (OSError, subprocess.SubprocessError):
        return None
    if done.returncode != 0:
        return None
    for line in done.stdout.split("\n"):
        parts = line.rstrip("\r").split("\t")
        if len(parts) == 2 and parts[0] == name:
            return parts[1] or None
    return None


def lys(result, title, shape_of=None):
    """The chart. `shape_of`: a chord name's guitar shape (lysc_shape), or None: the first chord is written with it,
    so that its diagram shows (the owner, 2026-11-16)."""
    tonic, mode = result["key"].split()
    acc = {"": "", "#": "is", "b": "es"}[tonic[1:]]
    beats = 2 if result["time"] == "6/8" else int(result["time"].split("/")[0])
    noted = []                          # the note on the diagrams is written yet
    shaped = []                         # the first chord has been given its shape

    def slots(said):
        """A bar as Lily# writes it: a chord on the beat it starts on, '.' where it goes on.
        `said`: the bar, each time it is played. A chord is marked when, over those times, the
        reader is less sure of it than DOUBT; the chord offered is the least sure time's."""
        short = said[0].get("pickup")
        n = int(said[0]["time"].split("/")[0]) if "time" in said[0] else beats   # a bar of 2/4 or 3/4 (short_meters)
        names = beat_names(said[0], short or n)
        if short and len(set(names)) > 1 and beats % short:
            # Lily# fits a bar's slots to the meter's beats, a short bar's too: three beats of 4/4
            # cannot be written a chord a beat. The chord that leads into the first bar stands for them.
            names = names[-1:]
        marks = [n if i == 0 or n != names[i - 1] else "." for i, n in enumerate(names)]
        doubts = {}
        for name in set(names):
            heard = [c for bar in said for c in bar["chords"] if c["symbol"] == name and "sure" in c]
            if heard and sum(c["sure"] for c in heard) / len(heard) < DOUBT:
                doubts[name] = min(heard, key=lambda c: c["sure"])["other"]
        marks = [f'{m}@or({doubts[m]})' if m in doubts else m for m in marks]   # Lily# REQUESTS 17: Ctrl+Shift+Up/Down swaps them
        if shape_of and not shaped:
            shaped.append(True)
            shape = shape_of(names[0])
            if shape:
                marks[0] = f"{names[0]}({shape})" + marks[0][len(names[0]):]
        if all(m == "." for m in marks[1:]):
            return marks[0]
        half = n // 2
        if not short and n % 2 == 0 and all(m == "." for i, m in enumerate(marks) if i not in (0, half)):
            return f"{marks[0]} {marks[half]}"
        return " ".join(marks)

    def rows(bars, indent, breaks=True, last=True):
        """Four bars a line of the text, and a `break` only where the section ends, so that a section begins
        a line of the page and Lily# fills the lines between (the owner, 2026-11-18; from 2026-11-16 to then
        a `break` after every four bars too). `last`: False when the
        section's last line goes on into the next section, a section of one bar (the owner, 2026-11-16)."""
        out, i = [], 0
        if not noted:                   # the first chords of the chart: how to see a chord's diagram
            noted.append(True)
            out.append(f"{indent}{DIAGRAM_NOTE}")
        # A bar of 2/4 or 3/4 says so at its head, and the bar after it goes back (Lily# takes a `time` at a bar's head
        # to the section's end: a section's last bar needs no way back).
        texts = [(f"time {said[0]['time']} " if "time" in said[0] else f"time {result['time']} " if j and "time" in bars[j - 1][0] else "")
                 + slots(said) for j, said in enumerate(bars)]
        sizes = line_sizes(len(bars))
        for k, size in enumerate(sizes):
            end = " | break" if breaks and last and k == len(sizes) - 1 else " |"   # the bar line, then the break: easier to read (the owner, 2026-11-16)
            out.append(f"{indent}{' | '.join(texts[i:i + size])}{end}")
            i += size
        return out

    head = [
        f"// read from {result['audio']} by tools/read_chords.py",
        f'title "{title}"',
        f"tempo {result['tempo']['note']} = {result['tempo']['bpm']}",
        f"time {result['time']}",
        f"key {tonic[0].lower()}{acc} {mode}",
        "",
        "// Shift+Ctrl+Up/Down on the font name switches the font",
        'fonts { music "Petaluma" }',
        "",
    ]
    form = result.get("form") or []
    short = result["bars"][0].get("pickup")
    # one section: the chart is the bars in their order; but not with a bar of its own time (Lily# takes a time in a row
    # out of a section for the whole chart: seed7's s7-009, the first chart of one section with a bar of 2/4, 0.9.0)
    if len(form) == 1 and not short and not any("time" in bar for bar in result["bars"]):
        return "\n".join(head + ["chords prog {", *rows([[bar] for bar in result["bars"]], "  "), "}", "",
                                 "score {", "  chords prog", "}", ""])
    sections = {}                       # name: its bars, each a list of the bar each time the section is played
    for bar in result["bars"]:
        of = sections.setdefault(bar["section"], [])
        if bar["barOfSection"] > len(of):
            of.append([])
        of[bar["barOfSection"] - 1].append(bar)
    body, written = [JOIN_NOTE], list(sections.items())
    for w, (name, bars) in enumerate(written):
        partial = [f"  partial {'4.' if result['time'] == '6/8' else PARTIAL[short]}"] if name == "Pickup" else []
        own = bars[0][0].get("tempo")   # the section's own tempo (section_tempos): Lily# sounds it each time the section is played
        partial += [f"  tempo {result['tempo']['note']} = {own}"] if own is not None else []
        last = w + 1 == len(written) or len(written[w + 1][1]) > 1      # a section of one bar goes on the line before
        body += [f"section {name} {{", *partial,"  chords prog {", *rows(bars, "    ", name != "Pickup", last), "  }", "}", ""]
    # Repeat signs only in the form, so that the form alone shows how the song goes (the owner, 2026-11-16).
    shown = ["~" + s if "_" in s else s for s in form]     # a short section's name is hidden (name_sections)
    return "\n".join(head + body + ["form { " + form_with_repeats(shown) + " }", "", "score {", "  chords prog", "}", ""])


def line_sizes(bars):
    """How many bars each line of a section has: four, but a last line of one bar goes on the line
    before (five), and four and two are three and three (the owner, 2026-11-16)."""
    sizes = [4] * (bars // 4)
    left = bars % 4
    if left == 1 and sizes:
        sizes[-1] = 5
    elif left == 2 and sizes:
        sizes[-1:] = [3, 3]
    elif left:
        sizes.append(left)
    return sizes


def form_with_repeats(form):
    """The form with repeat signs where sections come again running: a run of sections said again
    (|: A B :|, |: A :|*3), or sections followed by a different section each time (|: A [1. B] :|
    [2. C], |: B C B [1. D] :| [2. E]). The pickup is written as it comes. The one that saves the most sections' playings is
    taken first."""
    n, i, out = len(form), 0, []
    while i < n:
        best = None                     # (sections saved, words, sections it takes)
        for length in range(1, (n - i) // 2 + 1):
            unit = form[i:i + length]
            if "Pickup" in unit:
                break
            times = 1
            while form[i + times * length:i + (times + 1) * length] == unit:
                times += 1
            if times > 1 and (best is None or (times - 1) * length > best[0]):
                best = ((times - 1) * length, "|: " + " ".join(unit) + (" :|" if times == 2 else f" :|*{times}"), times * length)
        for length in range(1, (n - i) // 2 + 1):   # a body of `length` sections, a different section after it each time
            body = form[i:i + length]
            if "Pickup" in body:
                break
            j, endings = i, []
            while j + length < n and form[j:j + length] == body and form[j + length] != body[0] \
                    and form[j + length] not in endings:
                endings.append(form[j + length])
                j += length + 1
            if len(endings) > 1 and (best is None or (len(endings) - 1) * length > best[0]):
                words = " ".join(f"[{k + 1}. {e}]" + (" :|" if k < len(endings) - 1 else "") for k, e in enumerate(endings))
                best = ((len(endings) - 1) * length, f"|: {' '.join(body)} {words}", j - i)
        if best is None:
            out.append(form[i])
            i += 1
        else:
            out.append(best[1])
            i += best[2]
    return " ".join(out)



def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("input", type=Path, help="a .wav, or a folder of them")
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--model", type=Path, help="what train_chords.py learnt, in place of the chord templates")
    p.add_argument("--tempo", type=float,
                   help="about how many beats a minute the recording has, if you know: it settles "
                        "whether a chord lasts one bar or two")
    p.add_argument("--ears", type=Path,
                   help="read with models trained on recordings: the folder of <name>.beats (tools/track_beats.py) "
                        "and <name>.npz (tools/btc_chords.py --probs) of the recordings")
    p.add_argument("--voice", type=Path,
                   help="the folder of <name>.voc.npy: the vocals' loudness every 10 ms (scratch/sep/song.py, Demucs), for the sections")
    p.add_argument("--drums", type=Path,
                   help="the folder of <name>.drum.npy (`dettato drums` or `dettato ears`): the drums every 10 ms, for the sections")
    p.add_argument("--old-sections", action="store_true", help="the sections before 2026-11-16 (find_form and fold, repeat signs) instead of by meaning")
    args = p.parse_args()
    global PHRASE_SECTIONS
    PHRASE_SECTIONS = PHRASE_SECTIONS and not args.old_sections
    if args.ears:
        use_ears()
    elif args.model:
        use_model(args.model)
    files = sorted(args.input.glob("*.wav")) if args.input.is_dir() else [args.input]
    args.out.mkdir(parents=True, exist_ok=True)
    failed = 0
    for path in files:
        try:
            if args.ears:
                heard = np.load(args.ears / (path.stem + ".npz"))
                lines, beats = lines_of(args.ears / (path.stem + ".beats"), heard)
                voc = args.voice / (path.stem + ".voc.npy") if args.voice else None
                drum = args.drums / (path.stem + ".drum.npy") if args.drums else None
                result = read(path, lines=lines, beats=beats, heard=heard,
                              voice=np.load(voc) if voc is not None and voc.exists() else None,
                              drums=np.load(drum) if drum is not None and drum.exists() else None)
            else:
                result = read(path, tempo=args.tempo)
        except Exception as e:  # one recording that cannot be read does not stop the rest
            failed += 1
            print(f"{path.name}: {e}", file=sys.stderr)
            continue
        (args.out / (path.stem + ".dettato.json")).write_text(
            json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
        (args.out / (path.stem + ".lys")).write_text(lys(result, path.stem, lysc_shape), encoding="utf-8", newline="\n")
        read_chords = [c for bar in result["bars"] for c in bar["chords"]]
        doubted = sum(c.get("sure", 1.0) < DOUBT for c in read_chords)
        print(f"{path.name}: {result['key']}, {result['time']}, {result['tempo']['bpm']}, "
              f"{len(result['bars'])} bars"
              + (f", {doubted} of {len(read_chords)} chords doubted" if MODEL is not None or args.ears else "")
              + (": this is not music the model knows" if doubted > 0.5 * len(read_chords) else ""))
    if failed:
        sys.exit(f"{failed} of {len(files)} recordings were not read")


if __name__ == "__main__":
    main()
