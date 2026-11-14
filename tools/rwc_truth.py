#!/usr/bin/env python3
"""The RWC Popular Music recordings as songs with known answers: their beat and chord annotations
written as <id>.truth.json, the shape gen_chords.py's truth has.

    py tools/rwc_truth.py <rwc-annotations> scratch/real/rwc-p

<rwc-annotations> is a clone of https://github.com/rwc-music/rwc-annotations (beats with the
downbeats marked, and Cho and Bello's chords, both CC BY-NC 4.0). The folder written to is where
RWC-P.zip of https://zenodo.org/records/18656623 (CC BY-NC 4.0) is unpacked: RWC_P001.wav and so
on. Neither the recordings nor the truths made from the annotations are committed.

What becomes of the annotations:

- bars: from one beat marked 1 to the next. What sounds before the first is not in a bar and
  is not counted. The meter is the commonest number of beats in a bar ("4/4", "3/4"; six is
  written "6/8"), the tempo the median beat (for 6/8, three of the annotated beats);
- chords: cut at the bar lines. The reader knows eight qualities, so the labels are folded
  into them (maj6 and add9 are major, min9 is m7, 9 and 13 are 7, sus2 is sus4, dim7 is dim,
  hdim7 is m7(b5)). "N", augmented chords and chords with no third (power chords) are left out,
  and with them their time: `chordSeconds` is the time that is counted;
- key: the annotations have none, so `key` is null and the scorer skips it.

Only the Python standard library is used.
"""
import csv
import json
import sys
import wave
from collections import Counter
from pathlib import Path

LETTER_PC = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
SHARPS = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
FLATS = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"]
DEGREE = {"1": 0, "2": 2, "3": 4, "4": 5, "5": 7, "6": 9, "7": 11, "9": 2, "11": 5, "13": 9}
QUALITY = {"maj": "", "maj6": "", "maj7": "maj7", "maj9": "maj7", "7": "7", "9": "7", "13": "7",
           "min": "m", "min6": "m", "min7": "m7", "min9": "m7", "minmaj7": "m",
           "sus4": "sus4", "sus2": "sus4", "dim": "dim", "dim7": "dim", "hdim7": "m7(b5)"}


def pc(name):
    return (LETTER_PC[name[0]] + name.count("#") - name.count("b")) % 12


def symbol(label):
    """A Harte chord label as one of the reader's chords, or None where it has none to offer."""
    if ":" not in label:
        return None                                   # "N", or a bare root
    root, rest = label.split(":", 1)
    rest, _, bass = rest.partition("/")
    base, _, added = rest.partition("(")
    if base not in QUALITY or "*3" in added or "*b3" in added:
        return None
    quality = QUALITY[base]
    if base in ("maj", "min") and "b7" in added.replace("*", "").split(")")[0].split(","):
        quality = "7" if base == "maj" else "m7"
    if base == "min" and added.rstrip(")") == "7":   # min(7): a minor chord with a major seventh
        quality = "m"
    out = root + quality
    if bass and bass not in ("1",):
        semis = DEGREE[bass.lstrip("b#")] - bass.count("b") + bass.count("#")
        flat = "b" in root or root == "F" or (quality.startswith("m") and "#" not in root)
        if semis % 12:
            out += "/" + (FLATS if flat else SHARPS)[(pc(root) + semis) % 12]
    return out


def rows(path):
    with open(path, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f, delimiter=";"))


def truth_of(song, beats, chords, meta, audio_seconds):
    times = [float(b["t"]) for b in beats]
    downs = [i for i, b in enumerate(beats) if b["beat"] == "1"]
    if len(downs) < 2:
        raise ValueError("fewer than two bars")
    per = Counter(b - a for a, b in zip(downs, downs[1:])).most_common(1)[0][0]
    gaps = sorted(b - a for a, b in zip(times, times[1:]))
    beat = gaps[len(gaps) // 2]
    if per == 6:
        time, tempo = "6/8", {"note": "4.", "bpm": round(60 / (3 * beat))}
    else:
        time, tempo = f"{per}/4", {"note": "4", "bpm": round(60 / beat)}
    last = downs[-1] + per                          # the last bar ends a bar's beats after it begins
    end = times[last] if last < len(times) else times[-1] + beat
    lines = [times[i] for i in downs] + [end]
    spans = [(float(c["t_start"]), float(c["t_end"]), symbol(c["chord"])) for c in chords]
    bars, counted = [], 0.0
    for n, (a, b) in enumerate(zip(lines, lines[1:])):
        inside = []
        for start, stop, name in spans:
            lo, hi = max(a, start), min(b, stop)
            if name is None or hi - lo < 0.02:
                continue
            if inside and inside[-1]["symbol"] == name and abs(inside[-1]["end"] - lo) < 0.02:
                inside[-1]["end"] = round(hi, 3)
            else:
                inside.append({"start": round(lo, 3), "end": round(hi, 3), "symbol": name})
        counted += sum(c["end"] - c["start"] for c in inside)
        bars.append({"bar": n + 1, "start": round(a, 3), "end": round(b, 3), "chords": inside})
    return {
        "id": song, "audio": song + ".wav", "source": "RWC-MDB-P-2001, CC BY-NC 4.0",
        "title": meta.get("Title", ""), "artist": meta.get("Artist", ""),
        "tempo": tempo, "time": time, "key": None,
        "band": {"drums": meta.get("DrumInformation", ""), "styles": {"-": meta.get("GenreSub", "")}},
        "musicSeconds": round(end, 3), "chordSeconds": round(counted, 3), "audioSeconds": audio_seconds,
        "bars": bars,
    }


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    repo, out = Path(sys.argv[1]), Path(sys.argv[2])
    ann = repo / "01_annotations_preprocessed"
    meta = {m["RWCID"]: m for m in rows(repo / "metadata.csv")}
    out.mkdir(parents=True, exist_ok=True)
    left_out = Counter()
    made = missing = 0
    for path in sorted((ann / "chords" / "RWC-P").glob("RWC_P*.csv")):
        song = path.stem
        chords = rows(path)
        for c in chords:
            if symbol(c["chord"]) is None:
                left_out[c["chord"].partition(":")[2].partition("/")[0] or c["chord"]] += float(c["t_end"]) - float(c["t_start"])
        wav, seconds = out / (song + ".wav"), None
        if wav.exists():
            with wave.open(str(wav), "rb") as f:
                seconds = round(f.getnframes() / f.getframerate(), 3)
        else:
            missing += 1
        try:
            truth = truth_of(song, rows(ann / "beats" / "RWC-P" / path.name), chords, meta.get(song, {}), seconds)
        except Exception as e:
            print(f"{song}: {e}")
            continue
        (out / f"{song}.truth.json").write_text(json.dumps(truth, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        made += 1
        print(f"{song}: {truth['time']} {truth['tempo']['bpm']:>3}, {len(truth['bars'])} bars, "
              f"chords counted {truth['chordSeconds'] / truth['musicSeconds']:.0%} of {truth['musicSeconds']:.0f} s  {truth['title']}")
    print(f"{made} truths written to {out}" + (f"; {missing} recordings are not there yet" if missing else ""))
    print("left out, seconds:", ", ".join(f"{k} {v:.0f}" for k, v in left_out.most_common(8)))


if __name__ == "__main__":
    main()
