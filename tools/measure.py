#!/usr/bin/env python3
"""Measures the reader on sets of songs with known answers. Every number in the README is made here.

    .venv/Scripts/python tools/measure.py make                      # render the sets that are missing
    .venv/Scripts/python tools/measure.py all                       # every table of the README
    .venv/Scripts/python tools/measure.py table seed1 seed7 -v      # one line a set (-v: a line a song too)
    .venv/Scripts/python tools/measure.py table seed21 --hint       # told each song's true tempo
    .venv/Scripts/python tools/measure.py table seed1 --rules       # the reader with nothing learnt
    .venv/Scripts/python tools/measure.py table seed1 rwc-dev --ears   # with models trained on recordings
    .venv/Scripts/python tools/measure.py sweep seed100 TIGHTNESS=300,100 Q:maj7=0.02,0.05
    .venv/Scripts/python tools/measure.py doubt seed1 seed7 seed11 --at 0.5,0.7
    .venv/Scripts/python tools/measure.py barlines seed1            # how the bar lines are missed
    .venv/Scripts/python tools/measure.py keys seed1 seed7 seed11   # the key rule, given the true chords
    .venv/Scripts/python tools/measure.py beats seed7 s7-005        # the tracked beats against the true ones

A set is one of the names below (`measure.py sets` lists them) or a folder of <id>.truth.json
files with their recordings beside them. Reading and scoring run in one process; the features of
each recording (the slow part, seconds a song) are kept in scratch/feat-h<N>/ and made again when
the recording is newer than they are.

The sets, the model and the features are not committed. `make` renders the sets with Lily#
(DETTATO_LYSC) through the sound fonts named by DETTATO_SOUNDFONT (default
soundfonts/MuseScore_General.sf2) and DETTATO_SOUNDFONT_ALT (the `tim/` sets); the `samples` set
needs DETTATO_SAMPLES, the folder of Lily#'s sample songs.
"""
import argparse
import ast
import json
import os
import subprocess
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import read_chords as rc
import score_chords as sc

ROOT = Path(__file__).resolve().parent.parent
MODEL = ROOT / "models" / "chords.joblib"
SOUNDFONT = os.environ.get("DETTATO_SOUNDFONT", str(ROOT / "soundfonts" / "MuseScore_General.sf2"))
SOUNDFONT_ALT = os.environ.get("DETTATO_SOUNDFONT_ALT")

# name: (folder, gen_chords.py's arguments or None for sample_songs.py, the other sound font?, what it is for)
SETS = {
    "seed1": ("data/synth/seed1", ["--seed", "1"], False, "test; the first constants were chosen here"),
    "seed7": ("data/synth/seed7", ["--seed", "7"], False, "test; looked at when TIGHTNESS changed"),
    "seed11": ("data/synth/seed11", ["--seed", "11"], False, "test; looked at when TIGHTNESS changed"),
    "seed21": ("data/synth/seed21", ["--seed", "21", "--held", "0.5"], False, "chords that last two bars"),
    "seed31": ("data/synth/seed31", ["--seed", "31", "--drift", "0.02"], False, "a tempo that steps 2% a bar"),
    "seed100": ("data/synth/seed100", ["--seed", "100", "--count", "200"], False, "the model learns from these; constants too"),
    "held/seed101": ("scratch/alt-held/seed101", ["--seed", "101", "--count", "60", "--held", "0.5"], False, "for choosing constants"),
    "still/seed31": ("scratch/alt-still/seed31", ["--seed", "31"], False, "seed31 with a tempo that does not move"),
    "drift5/seed31": ("scratch/alt-drift5/seed31", ["--seed", "31", "--drift", "0.05"], False, "seed31 stepping 5% a bar"),
    "tim/seed1": ("scratch/alt-tim/seed1", ["--seed", "1"], True, "seed1 through another sound font"),
    "tim/seed7": ("scratch/alt-tim/seed7", ["--seed", "7"], True, "seed7 through another sound font"),
    "tim/seed11": ("scratch/alt-tim/seed11", ["--seed", "11"], True, "seed11 through another sound font"),
    "samples": ("scratch/alt-ood/samples", None, False, "Lily#'s four sample songs: not the generator's making"),
    "rwc-p": ("scratch/real/rwc-p", "rwc", False, "recordings: RWC Popular Music, 100 songs (tools/rwc_truth.py)"),
}
TEST = ["seed1", "seed7", "seed11"]
TIM = ["tim/seed1", "tim/seed7", "tim/seed11"]

_RULES = {k: getattr(rc, k) for k in ("MODEL", "STAY_COST", "MID_COST", "ELSE_COST", "BASS_WEIGHT", "EMIT_SCALE", "DOUBT")}


def configure(model, ears=False):
    """The reader with the model at `model`, or (None) with nothing learnt; `ears`: with the
    prices that go with a model trained on recordings (--heard)."""
    for k, v in _RULES.items():
        setattr(rc, k, v)
    if ears:
        rc.use_ears()
    elif model:
        rc.use_model(model)


PARTS = {"rwc-dev": "rwc-p@1-50", "rwc-test": "rwc-p@51-100"}   # looked at to choose by; not looked at


def folder(name):
    name = PARTS.get(name, name).split("@")[0]
    return ROOT / SETS[name][0] if name in SETS else Path(name)


def songs(name):
    """(truth, recording) for each song of a set. `set@11-20` is its eleventh to twentieth song."""
    d = folder(name)
    paths = sorted(d.glob("*.truth.json"))
    if not paths:
        sys.exit(f"{name}: no <id>.truth.json in {d}" + (" (measure.py make)" if name in SETS else ""))
    if "@" in PARTS.get(name, name):
        first, last = PARTS.get(name, name).split("@")[1].split("-")
        paths = paths[int(first) - 1:int(last)]
    for path in paths:
        truth = json.loads(path.read_text(encoding="utf-8"))
        yield truth, path.parent / truth["audio"]


def feats(wav):
    """The features of a recording, kept between runs. Sets that share song names (the same seed
    through another sound font) are told apart by the folder above the set's."""
    d = ROOT / "scratch" / f"feat-h{rc.HARMONIC_FRAMES}"
    above = wav.parent.parent.name
    kept = d / (("" if above == "synth" else above + "_") + wav.stem + ".npz")
    if kept.exists() and not (wav.exists() and wav.stat().st_mtime > kept.stat().st_mtime):
        return dict(np.load(kept))
    if not wav.exists():
        raise FileNotFoundError(f"{wav} is not rendered (measure.py make)")
    print(f"  features of {wav.name} ...", file=sys.stderr, flush=True)
    f = rc.features(wav)
    d.mkdir(parents=True, exist_ok=True)
    np.savez(kept, **f)
    return f


def set_constant(key, value):
    if key.startswith("Q:"):        # the price of a chord quality
        rc.QUALITY_COST[key[2:]] = value
        rc.COSTS = np.array([rc.QUALITY_COST[q] for _, q in rc.CHORDS])
    elif not hasattr(rc, key):
        sys.exit(f"read_chords.py has no constant {key}")
    else:
        setattr(rc, key, value)


def totals(rows):
    n = len(rows)
    if not n:
        return "nothing read"
    found, true, read = (sum(r[k] for r in rows) for k in ("barsFound", "barsTrue", "barsRead"))
    p, r_ = found / max(read, 1), found / true
    secs = sum(r["seconds"] for r in rows)
    share = lambda k: sum(r[k] * r["seconds"] for r in rows) / secs
    count = lambda k: sum(bool(r[k]) for r in rows)
    keyed = sum(r["key"] is not None for r in rows)      # a truth may have no key
    return (f"tempo {count('tempo')}/{n} meter {count('meter')}/{n} key {count('key')}/{keyed} sig {count('signature')}/{keyed}"
            f" | bars F {2 * p * r_ / max(p + r_, 1e-9):.1%} | root {share('root'):.1%} family {share('family'):.1%}"
            f" quality {share('quality'):.1%} bass {share('bass'):.1%}")


def bar_lines(truth, wav, source):
    """Bar lines found by other means, as read_chords.read takes them: (seconds, parts of a bar).
    `source` is "truth" (the true lines: what the chords are worth when the bars are right) or a
    folder of <recording>.beats files, a line a beat: its seconds, a tab, its number in the bar."""
    if source == "truth":
        per = {"6/8": 3}.get(truth["time"], int(truth["time"].split("/")[0]))
        return [b["start"] for b in truth["bars"]] + [truth["bars"][-1]["end"]], 2 * per, None
    given, per = rc.lines_of(Path(source) / (wav.stem + ".beats"))
    return given, 2 * per, per


def lab_reading(truth, wav, labs, lines):
    """A reading whose chords are another model's (<recording>.lab: start, end, Harte label), cut
    at bar lines found by other means: what that model is worth, counted as our own is."""
    import rwc_truth
    given, units, _ = bar_lines(truth, wav, lines)
    spans = []
    for line in (Path(labs) / (wav.stem + ".lab")).read_text().splitlines():
        start, end, label = line.split()[:3]
        label = label if ":" in label or label in ("N", "X") else label + ":maj"
        spans.append((float(start), float(end), rwc_truth.symbol(label)))
    bars = []
    for n, (a, b) in enumerate(zip(given, given[1:])):
        inside = []
        for start, stop, name in spans:
            lo, hi = max(a, start), min(b, stop)
            if name is None or hi - lo < 0.02:
                continue
            if inside and inside[-1]["symbol"] == name and abs(inside[-1]["end"] - lo) < 0.02:
                inside[-1]["end"] = round(hi, 3)
            else:
                inside.append({"start": round(lo, 3), "end": round(hi, 3), "symbol": name})
        bars.append({"bar": n + 1, "start": round(a, 3), "end": round(b, 3), "chords": inside})
    per = units // 2
    bar = float(np.median(np.diff(given)))
    return {"audio": wav.name, "tempo": {"note": "4", "bpm": int(round(60.0 * per / bar))}, "time": f"{per}/4",
            "key": "C major", "bars": bars}


def ears_of(name):
    """Where what the models trained on recordings made of a set is kept: (the folder of .beats,
    the folder of .npz), as tools/track_beats.py and tools/btc_chords.py --probs wrote them (README)."""
    base = PARTS.get(name, name).split("@")[0]
    if base == "rwc-p":
        return ROOT / "scratch" / "real" / "beats-bt-folds", ROOT / "scratch" / "real" / "chords-btc"
    return ROOT / "scratch" / "beats-bt" / base, ROOT / "scratch" / "chords-btc" / base


def run(name, hint=False, verbose=False, write=None, lines=None, labs=None, heard=None, ears=False):
    """Reads and scores a set. Returns each song's numbers, and the charts written."""
    rows, written = [], []
    if ears:
        lines, heard = ears_of(name)
        if not lines.is_dir() or not heard.is_dir():
            print(f"{name}: no beats in {lines} or no chords in {heard}")
            return rows, written
    for truth, wav in songs(name):
        try:
            if labs:
                got = lab_reading(truth, wav, labs, lines or "truth")
            elif lines:
                given, units, per = bar_lines(truth, wav, lines)
                got = rc.read(wav, feats(wav), lines=given, units=units, beats=per,
                              heard=np.load(Path(heard) / (wav.stem + ".npz")) if heard else None)
            else:
                got = rc.read(wav, feats(wav), tempo=truth["tempo"]["bpm"] if hint else None)
            r = sc.score(truth, got)
        except Exception as e:  # one song that cannot be read does not stop the rest
            print(f"{truth['id']} failed: {e}")
            continue
        if write and not labs:
            d = Path(write) / name.replace("/", "-").replace("@", "-")
            d.mkdir(parents=True, exist_ok=True)
            (d / (wav.stem + ".dettato.json")).write_text(json.dumps(got, indent=2) + "\n", encoding="utf-8", newline="\n")
            (d / (wav.stem + ".lys")).write_text(rc.lys(got, wav.stem), encoding="utf-8", newline="\n")
            written.append(d / (wav.stem + ".lys"))
        rows.append(r)
        if verbose:
            chords = [c for bar in got["bars"] for c in bar["chords"]]
            doubted = f" doubted {sum(c['sure'] < rc.DOUBT for c in chords)}/{len(chords)}" if chords and "sure" in chords[0] else ""
            styles = truth.get("band", {}).get("styles", "")
            print(f"{r['id']:<14} tempo {'ok' if r['tempo'] else 'x' + str(r['tempoRatio']):>6} meter {'ok' if r['meter'] else '--'}"
                  f" key {'  ' if r['key'] is None else 'ok' if r['key'] else '--'} bars {r['barsFound']:>2}/{r['barsTrue']} ({r['barsRead']}) root {r['root']:.0%}"
                  f" fam {r['family']:.0%} qual {r['quality']:.0%}{doubted}{' strange' if got.get('strange') else ''}"
                  f"  {truth['time']} {truth['tempo']['bpm']} {styles}", flush=True)
    return rows, written


def check(charts):
    """Every chart through `lysc check`. Its "No errors found" has `error` in it: the exit code
    and the words `warning` and `: error` are what say."""
    lysc = os.environ.get("DETTATO_LYSC", "lysc")
    from concurrent.futures import ThreadPoolExecutor
    one = lambda chart: subprocess.run([lysc, "check", str(chart)], capture_output=True, text=True, encoding="utf-8",
                                       stdin=subprocess.DEVNULL)      # lysc check takes one file
    bad = 0
    with ThreadPoolExecutor(max_workers=os.cpu_count() or 4) as pool:
        results = list(pool.map(one, charts))
    for chart, done in zip(charts, results):
        said = done.stdout + done.stderr
        if done.returncode != 0 or "warning" in said or ": error" in said:
            bad += 1
            print(f"lysc check {chart}: {said.strip()[:300]}")
    print(f"lysc check: {len(charts) - bad} of {len(charts)} charts pass")
    return bad


def doubted(names, ears=False):
    """Every chord read in the sets, with what is true where its middle is: dicts of `said` (how
    sure the reader says it is: what the mark goes by), `fam`
    (root and family right), `all` (quality right too), `offered` (the chord offered in its place
    is the true one; `offeredFam`: in root and family), `song`, and the reader's `why`."""
    rows = []
    for name in names:
        lines, heard = ears_of(name) if ears else (None, None)
        for truth, wav in songs(name):
            if ears:
                given, units, per = bar_lines(truth, wav, lines)
                got = rc.read(wav, feats(wav), lines=given, units=units, beats=per,
                              heard=np.load(heard / (wav.stem + ".npz")), why=True)
            else:
                got = rc.read(wav, feats(wav), why=True)
            spans = sc.chords_of(truth)
            for bar in got["bars"]:
                for c in bar["chords"]:
                    mid = (c["start"] + c["end"]) / 2
                    true = next((p for a, b, p in spans if a <= mid < b), None)
                    if true is None:
                        continue
                    root, quality, _ = sc.parse(c["symbol"])
                    oroot, oquality, _ = sc.parse(c["other"])
                    fam = root == true[0] and sc.FAMILY[quality] == sc.FAMILY[true[1]]
                    rows.append({"said": c["sure"], "fam": fam, "all": fam and quality == true[1],
                                 "offered": oroot == true[0] and oquality == true[1],
                                 "offeredFam": oroot == true[0] and sc.FAMILY[oquality] == sc.FAMILY[true[1]],
                                 "song": truth["id"], "symbol": c["symbol"], "true": true, "key": got["key"], "start": c["start"],
                                 **c["why"]})
    return rows


def doubt(names, thresholds, ears=False):
    """Do the marks point at the wrong chords?"""
    a = np.array([(r["said"], r["fam"], r["all"], r["offered"], r["offeredFam"]) for r in doubted(names, ears)], dtype=float)
    wrong_f, wrong_q = a[:, 1] == 0, a[:, 2] == 0
    print(f"{len(a)} chords read: {wrong_f.mean():.1%} wrong in root or family, {wrong_q.mean():.1%} wrong in quality too")
    for th in thresholds:
        m = a[:, 0] < th
        print(f"sure below {th}: {m.mean():.1%} marked | of the wrong in root or family, marked {m[wrong_f].mean():.1%}"
              f" (of the wrong at all {m[wrong_q].mean():.1%}) | of the marked, wrong in root or family {wrong_f[m].mean():.1%},"
              f" wrong at all {wrong_q[m].mean():.1%} | of the unmarked, wrong at all {wrong_q[~m].mean():.1%}"
              f" | marked and wrong, the chord offered is the true one {a[m & wrong_q, 3].mean():.1%}"
              f" | marked and wrong in root or family, the chord offered has the true ones {a[m & wrong_f, 4].mean():.1%}")


def barlines(names, hint):
    """For each song that loses a tenth of its bar lines: the bar's length read against the true
    one, where the read lines fall against the true ones (in true beats), how the tracked beats sit."""
    kinds = Counter()
    for name in names:
        for truth, wav in songs(name):
            f = feats(wav)
            got = rc.read(wav, f, tempo=truth["tempo"]["bpm"] if hint else None)
            s = sc.score(truth, got)
            if s["barsFound"] >= 0.9 * s["barsTrue"]:
                kinds["fine"] += 1
                continue
            beats, period = rc.beat_grid(f["oenv"])
            bt = beats * rc.HOP / rc.SR
            tb = np.array([b["start"] for b in truth["bars"]])
            bar = float(np.median(np.diff(tb)))
            per = 2 if truth["time"] == "6/8" else int(truth["time"].split("/")[0])
            beat = bar / per
            rb = np.array([b["start"] for b in got["bars"]])
            ratio = float(np.median(np.diff(rb))) / bar
            # where a moment falls in true beats, counted inside its own true bar (a recording's tempo moves)
            ends = np.append(tb, truth["bars"][-1]["end"])
            def place(x):
                i = int(np.clip(np.searchsorted(ends, x, side="right") - 1, 0, len(tb) - 1))
                return i * per + (x - ends[i]) / (ends[i + 1] - ends[i]) * per
            inside = bt[(bt >= ends[0]) & (bt < ends[-1])]
            off = np.array([((place(x) + per / 2) % per) - per / 2 for x in rb])
            boff = np.array([((place(x) + 0.5) % 1.0) - 0.5 for x in inside])      # tracked beats against true beats
            tracked = period * rc.HOP / rc.SR / beat
            spread = float(np.std(boff))
            if abs(ratio - 1) > 0.1:
                kind, group = f"bar x{ratio:.2f}", "bar of another length"
            elif spread > 0.12:
                kind = group = "beats wander"
            elif abs(np.median(off)) > 0.3:
                kind, group = f"lines {np.median(off):+.1f} beats off", "lines a part of a bar off"
            else:
                kind = group = "some lines off"
            kinds[group] += 1
            band = truth.get("band", {})
            print(f"{truth['id']:<14} {truth['time']} {truth['tempo']['bpm']:>3} found {s['barsFound']:>2}/{s['barsTrue']} read {s['barsRead']:>2}"
                  f" | {kind:<22} | tracked beat x{tracked:.2f}, beats' spread {spread:.2f}, median beat offset {np.median(boff):+.2f}"
                  f" | line offsets {np.round(off[:10], 1).tolist()} | drums {band.get('drums')} {next(iter(band.get('styles', {}).values()), '')}")
    print(dict(kinds))


def keys(names, verbose):
    """The key rule alone: find_key given the true chords."""
    notes = rc.MAJOR_KEYS
    kinds, right, n = Counter(), 0, 0
    for name in names:
        for t, _ in songs(name):
            tonic, mode = sc.key_of(t["key"])
            chords = [(c["end"] - c["start"],) + sc.parse(c["symbol"])[:2] for b in t["bars"] for c in b["chords"]]
            got = rc.find_key(chords)
            n += 1
            if got == (tonic, mode):
                right += 1
                continue
            kinds[(mode, (got[0] - tonic) % 12, got[1])] += 1
            if verbose:
                print(t["id"], t["key"], "->", notes[got[0]], got[1], "|",
                      " ".join(c["symbol"] for b in t["bars"][:8] for c in b["chords"]), "...", t["bars"][-1]["chords"][-1]["symbol"])
    print(f"from the true chords: {right}/{n}")
    for (mode, up, got), count in kinds.most_common():
        print(f"  a {mode} key read as the {got} key {up} semitones up: {count}")


def beats(name, song):
    """The tracked beats of one song against its true beats."""
    for truth, wav in songs(name):
        if truth["id"] != song:
            continue
        tracked, period = rc.beat_grid(feats(wav)["oenv"])
        bt = tracked * rc.HOP / rc.SR
        tb = [b["start"] for b in truth["bars"]]
        beat = (tb[1] - tb[0]) / (2 if truth["time"] == "6/8" else int(truth["time"].split("/")[0]))
        print("true beat", round(beat, 3), "s, tracked", round(period * rc.HOP / rc.SR, 3), "s", truth.get("form", ""),
              truth.get("band", {}).get("styles", ""))
        print("where each tracked beat falls, in true beats from the first bar line:")
        print(np.round((bt - tb[0]) / beat, 2).tolist())
        return
    sys.exit(f"{name} has no song {song}")


def make(names):
    """Renders the sets that are not there (a set whose recordings are all there is left alone)."""
    for name in names:
        where, gen, alt, _ = SETS[name]
        d = ROOT / where
        if gen == "rwc":
            print(f"{name}: recordings are not made here: see tools/rwc_truth.py ({len(list(d.glob('*.wav')))} there)")
            continue
        truths = [json.loads(p.read_text(encoding="utf-8")) for p in d.glob("*.truth.json")]
        want = int(gen[gen.index("--count") + 1]) if gen and "--count" in gen else 24 if gen else 4
        if len(truths) >= want and all((d / t["audio"]).exists() for t in truths):
            print(f"{name}: there ({len(truths)} songs)")
            continue
        font = SOUNDFONT_ALT if alt else SOUNDFONT
        if not font or not Path(font).exists():
            print(f"{name}: skipped, no sound font ({'DETTATO_SOUNDFONT_ALT' if alt else 'DETTATO_SOUNDFONT'})")
            continue
        if gen:
            cmd = [sys.executable, str(ROOT / "tools" / "gen_chords.py"), *gen, "--out", str(d), "--soundfont", font]
        else:
            samples = os.environ.get("DETTATO_SAMPLES")
            if not samples:
                print(f"{name}: skipped, DETTATO_SAMPLES does not name Lily#'s samples folder")
                continue
            cmd = [sys.executable, str(ROOT / "tools" / "sample_songs.py"), samples, os.environ.get("DETTATO_LYSC", "lysc"), font, str(d)]
        print(f"{name}: making ...", flush=True)
        done = subprocess.run(cmd, stdin=subprocess.DEVNULL)
        if done.returncode != 0:
            print(f"{name}: failed ({done.returncode})")


def everything(model, write):
    """Every table of the README, in its order, and every chart written through `lysc check`."""
    plan = [
        ("Nothing learnt", TEST, None, False, False),
        ("With the model", TEST + ["seed21", "seed31"], model, False, False),
        ("With the model, told the tempo", TEST + ["seed21"], model, True, False),
        ("Another sound font, nothing learnt", TIM, None, False, False),
        ("Another sound font, with the model", TIM, model, False, False),
        ("A tempo that moves (still, 2%, 5%), with the model", ["still/seed31", "seed31", "drift5/seed31"], model, False, False),
        ("The songs constants are chosen on, with the model", ["seed100", "held/seed101"], model, False, False),
        ("Songs it was not made on, nothing learnt", ["samples"], None, False, True),
        ("Songs it was not made on, with the model", ["samples"], model, False, True),
        ("Recordings, nothing learnt", ["rwc-p"], None, False, False),
        ("Recordings, with the model", ["rwc-p"], model, False, False),
        ("Recordings, with the model, told the tempo", ["rwc-p"], model, True, False),
    ]
    charts = []
    for i, (title, names, m, hint, verbose) in enumerate(plan):
        print(f"\n## {title}")
        configure(m)
        for name in names:
            if not any(folder(name).glob("*.truth.json")):
                print(f"{name}: not made (measure.py make)")
                continue
            rows, written = run(name, hint=hint, verbose=verbose, write=Path(write) / f"{i + 1}" if write else None)
            charts += written
            print(f"{name}: {totals(rows)}", flush=True)
    print("\n## With ears trained on recordings (Beat This! and BTC)")
    configure(None, ears=True)
    for name in TEST + ["seed21", "seed31", "rwc-dev", "rwc-test", "rwc-p"]:
        rows, written = run(name, ears=True, write=Path(write) / "ears" if write and name != "rwc-p" else None)
        charts += written
        if rows:
            print(f"{name}: {totals(rows)}", flush=True)
    print("\n## The marks, with ears trained on recordings")
    for names in (["rwc-dev"], ["rwc-test"], ["rwc-p"], TEST, ["seed21", "seed31"]):
        print(" ".join(names) + ": ", end="")
        doubt(names, [rc.DOUBT], ears=True)
    configure(model)
    for title, names in (("The marks, this sound font", TEST), ("The marks, another sound font", TIM)):
        print(f"\n## {title}")
        doubt(names, [rc.DOUBT])
    print(f"\n## The key rule, given the true chords")
    keys(TEST, False)
    print()
    return check(charts) if charts else 0


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0], epilog="See the top of tools/measure.py for examples.")
    sub = p.add_subparsers(dest="what", required=True)

    def reader(s, sets="+"):
        if sets:
            s.add_argument("sets", nargs=sets, help="names (measure.py sets) or folders")
        s.add_argument("--model", type=Path, default=MODEL, help="what train_chords.py learnt (default: models/chords.joblib)")
        s.add_argument("--rules", action="store_true", help="the reader with nothing learnt")
        s.add_argument("--set", action="append", default=[], metavar="NAME=VALUE", help="a constant of read_chords.py, for this run")
        return s

    s = reader(sub.add_parser("table", help="read and score: one line a set"))
    s.add_argument("--hint", action="store_true", help="tell the reader each song's true tempo")
    s.add_argument("-v", "--verbose", action="store_true", help="a line a song")
    s.add_argument("--write", type=Path, help="write the charts under this folder")
    s.add_argument("--check", action="store_true", help="with --write: every chart through lysc check")
    s.add_argument("--chords", help="count another model's chords: a folder of <recording>.lab files (the bar lines: --lines, else the truth's)")
    s.add_argument("--lines", help='bar lines found by other means: "truth", or a folder of <recording>.beats files')
    s.add_argument("--heard", help="with --lines: chords put together from what a model trained on recordings hears, "
                                   "a folder of <recording>.npz files (tools/btc_chords.py --probs)")
    s.add_argument("--ears", action="store_true",
                   help="--lines and --heard from where the README keeps them for each set (scratch/beats-bt, scratch/chords-btc)")
    s = reader(sub.add_parser("all", help="every table of the README"), sets=None)
    s.add_argument("--write", type=Path, default=ROOT / "scratch" / "read-all", help="where the charts go (default: scratch/read-all)")
    s = reader(sub.add_parser("sweep", help="the table over a grid of constants"), sets=None)
    s.add_argument("sets", help="names or folders, with commas between")
    s.add_argument("grid", nargs="+", metavar="NAME=V1,V2", help="a constant and the values to try (Q:maj7 for a quality's price)")
    s.add_argument("--hint", action="store_true")
    s.add_argument("--lines", help="as for table")
    s.add_argument("--heard", help="as for table")
    s.add_argument("--ears", action="store_true", help="as for table")
    s = reader(sub.add_parser("doubt", help="do the marks point at the wrong chords?"))
    s.add_argument("--at", default=None, help="thresholds with commas between (default: the reader's DOUBT)")
    s.add_argument("--ears", action="store_true", help="as for table")
    s = reader(sub.add_parser("barlines", help="how the bar lines are missed, song by song"))
    s.add_argument("--hint", action="store_true")
    s = sub.add_parser("keys", help="the key rule, given the true chords")
    s.add_argument("sets", nargs="+")
    s.add_argument("-v", "--verbose", action="store_true")
    s = reader(sub.add_parser("beats", help="one song's tracked beats against the true ones"), sets=None)
    s.add_argument("name", metavar="set")
    s.add_argument("song", help="its id, e.g. s7-005")
    s = sub.add_parser("make", help="render the sets that are missing")
    s.add_argument("sets", nargs="*", help="default: all of them")
    sub.add_parser("sets", help="list the sets")
    args = p.parse_args()

    if args.what == "sets":
        for name, (where, gen, alt, why) in SETS.items():
            made = len(list((ROOT / where).glob("*.wav")))
            print(f"{name:<14} {where:<26} {made:>3} recordings  {why}")
        for name, part in PARTS.items():
            print(f"{name:<14} {part}")
        return 0
    if args.what == "make":
        return make(args.sets or list(SETS))
    if args.what == "keys":
        return keys(args.sets, args.verbose)

    model = None if args.rules else args.model
    if model and not Path(model).exists():
        sys.exit(f"{model} is not there: train it (tools/train_chords.py), or read with --rules")
    heard, ears = getattr(args, "heard", None), getattr(args, "ears", False)
    if heard and not getattr(args, "lines", None):
        sys.exit("--heard needs --lines: the model trained on recordings names chords, not bars")
    configure(model, ears=bool(heard) or ears)
    for item in args.set:
        key, value = item.split("=", 1)
        set_constant(key, ast.literal_eval(value))

    if args.what == "table":
        charts = []
        for name in args.sets:
            rows, written = run(name, hint=args.hint, verbose=args.verbose, write=args.write, lines=args.lines, labs=args.chords,
                                heard=heard, ears=ears)
            charts += written
            print(f"{name}: {totals(rows)}", flush=True)
        return check(charts) if args.check and charts else 0
    if args.what == "all":
        return everything(model, args.write)
    if args.what == "sweep":
        import itertools
        grid = {k: ast.literal_eval(v + ",") for k, v in (g.split("=", 1) for g in args.grid)}
        for combo in itertools.product(*grid.values()):
            for k, v in zip(grid, combo):
                set_constant(k, v)
            for name in args.sets.split(","):
                rows, _ = run(name, hint=args.hint, lines=args.lines, heard=heard, ears=ears)
                print(dict(zip(grid, combo)), f"{name}: {totals(rows)}", flush=True)
        return 0
    if args.what == "doubt":
        if rc.MODEL is None and not ears:
            sys.exit("the reader with nothing learnt marks nothing")
        return doubt(args.sets, ast.literal_eval(args.at + ",") if args.at else [rc.DOUBT], ears)
    if args.what == "barlines":
        return barlines(args.sets, args.hint)
    if args.what == "beats":
        return beats(args.name, args.song)


if __name__ == "__main__":
    sys.exit(main())
