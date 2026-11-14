#!/usr/bin/env python3
"""Counts a reading against the truth: tempo, meter, key, bar lines and chords.

    py tools/score_chords.py data/synth/seed1 scratch/read/seed1

The truth is gen_chords.py's <id>.truth.json; the reading is read_chords.py's
<audio name>.dettato.json. What is counted:

- tempo: right within 4%; otherwise its ratio to the truth is shown (2 = read twice as fast);
- meter, key: the same or not; the key signature too, which a key and its relative share;
- bar lines: a line read within 70 ms of a true one finds it (each true line once);
  precision, recall and F;
- chords, by the share of the music's time: the root right; root and family right (major, minor,
  suspended, diminished); root and quality right (sevenths told apart); the bass note right too.

Only the Python standard library is used.
"""
import argparse
import json
import re
import sys
from pathlib import Path

LETTER_PC = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
FAMILY = {"": "maj", "7": "maj", "maj7": "maj", "m": "min", "m7": "min",
          "sus4": "sus", "dim": "dim", "m7(b5)": "dim"}
SYMBOL = re.compile(r"^([A-G][#b]?)(.*?)(?:/([A-G][#b]?))?$")
BAR_TOLERANCE = 0.07


def pc(name):
    return (LETTER_PC[name[0]] + name.count("#") - name.count("b")) % 12


def parse(symbol):
    root, quality, bass = SYMBOL.match(symbol).groups()
    return pc(root), quality, pc(bass) if bass else pc(root)


def key_of(text):
    tonic, mode = text.split()
    return pc(tonic), mode


def signature_of(text):
    """The key signature, as the major key that has it: a minor key's is its relative major's."""
    tonic, mode = key_of(text)
    return (tonic + 3) % 12 if mode == "minor" else tonic


def chords_of(doc):
    return [(c["start"], c["end"], parse(c["symbol"])) for bar in doc["bars"] for c in bar["chords"]]


def score(truth, reading):
    out = {"id": truth["id"]}
    ratio = reading["tempo"]["bpm"] / truth["tempo"]["bpm"]
    if reading["tempo"]["note"] != truth["tempo"]["note"]:  # count both in eighths
        eighths = {"4": 2, "4.": 3}
        ratio = (reading["tempo"]["bpm"] * eighths[reading["tempo"]["note"]]) / (
            truth["tempo"]["bpm"] * eighths[truth["tempo"]["note"]])
    out["tempoRatio"] = round(ratio, 2)
    out["tempo"] = abs(ratio - 1) <= 0.04 and reading["tempo"]["note"] == truth["tempo"]["note"]
    out["meter"] = reading["time"] == truth["time"]
    if truth.get("key"):
        out["key"] = key_of(reading["key"]) == key_of(truth["key"])
        out["signature"] = signature_of(reading["key"]) == signature_of(truth["key"])
    else:                                    # a truth with no key (annotated recordings): not counted
        out["key"] = out["signature"] = None

    true_lines = [b["start"] for b in truth["bars"]]
    read_lines = [b["start"] for b in reading["bars"]]
    found, used = 0, set()
    for t in true_lines:
        near = [(abs(r - t), i) for i, r in enumerate(read_lines) if i not in used and abs(r - t) <= BAR_TOLERANCE]
        if near:
            used.add(min(near)[1])
            found += 1
    out["barsTrue"], out["barsRead"], out["barsFound"] = len(true_lines), len(read_lines), found

    # the time whose chord the truth names: all of the music, unless the truth leaves some out
    total = truth.get("chordSeconds", truth["musicSeconds"])
    shares = {"root": 0.0, "family": 0.0, "quality": 0.0, "bass": 0.0}
    read_chords = chords_of(reading)
    for start, end, (root, quality, bass) in chords_of(truth):
        for r_start, r_end, (r_root, r_quality, r_bass) in read_chords:
            both = min(end, r_end) - max(start, r_start)
            if both <= 0 or r_root != root:
                continue
            shares["root"] += both
            if FAMILY[r_quality] == FAMILY[quality]:
                shares["family"] += both
            if r_quality == quality:
                shares["quality"] += both
                if r_bass == bass:
                    shares["bass"] += both
    out["seconds"] = total
    out.update({k: v / total for k, v in shares.items()})
    return out


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("truth", type=Path)
    p.add_argument("reading", type=Path)
    p.add_argument("--json", type=Path, help="write every song's numbers here")
    args = p.parse_args()
    rows = []
    for path in sorted(args.truth.glob("*.truth.json")):
        truth = json.loads(path.read_text(encoding="utf-8"))
        read_path = args.reading / (Path(truth["audio"]).stem + ".dettato.json")
        if not read_path.exists():
            print(f"{truth['id']}: not read", file=sys.stderr)
            continue
        rows.append(score(truth, json.loads(read_path.read_text(encoding="utf-8"))))
    if not rows:
        sys.exit("nothing to score")

    yes = lambda b: "ok" if b else "--"
    print(f"{'song':<8} {'tempo':>10} {'meter':>5} {'key':>4} {'bars found/true (read)':>24} "
          f"{'root':>6} {'family':>6} {'quality':>7} {'bass':>6}")
    for r in rows:
        tempo = "ok" if r["tempo"] else f"x{r['tempoRatio']}"
        bars = f"{r['barsFound']}/{r['barsTrue']} ({r['barsRead']})"
        print(f"{r['id']:<8} {tempo:>10} {yes(r['meter']):>5} {'' if r['key'] is None else yes(r['key']):>4} {bars:>24} "
              f"{r['root']:>6.0%} {r['family']:>6.0%} {r['quality']:>7.0%} {r['bass']:>6.0%}")

    n = len(rows)
    found = sum(r["barsFound"] for r in rows)
    true, read = sum(r["barsTrue"] for r in rows), sum(r["barsRead"] for r in rows)
    precision, recall = found / max(read, 1), found / true
    f = 2 * precision * recall / max(precision + recall, 1e-9)
    seconds = sum(r["seconds"] for r in rows)
    share = lambda k: sum(r[k] * r["seconds"] for r in rows) / seconds
    print()
    keyed = [r for r in rows if r["key"] is not None]
    print(f"songs {n}: tempo {sum(r['tempo'] for r in rows)}/{n}, meter {sum(r['meter'] for r in rows)}/{n}, "
          f"key {sum(r['key'] for r in keyed)}/{len(keyed)}, key signature {sum(r['signature'] for r in keyed)}/{len(keyed)}")
    print(f"bar lines: precision {precision:.1%}, recall {recall:.1%}, F {f:.1%} ({found} of {true} found, {read} read)")
    print(f"chords, by time: root {share('root'):.1%}, family {share('family'):.1%}, "
          f"quality {share('quality'):.1%}, with the bass {share('bass'):.1%}")
    if args.json:
        args.json.write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
