#!/usr/bin/env python3
"""Checks .dettato.json files against docs/SIDECAR.md, and against the chart beside each.

    py tools/sidecar.py scratch/read/seed1            # every <name>.dettato.json of a folder
    py tools/sidecar.py song.dettato.json --no-lysc   # the file alone

What is checked of a file: every item is there and is what the specification says; the bars are
numbered in the order they are played and follow one another in time; the chords of a bar fill it;
the sections and the form say the same as the bars do.

What is checked against the chart (<name>.lys beside the file, through `lysc midi`; DETTATO_LYSC
names lysc): Lily# plays as many bars as the file has, its repeats played (a chart may write a bar
once between repeat signs that is played twice). The n-th bar Lily# plays is the n-th of
`bars`: that is how an editor finds the seconds of the bar the caret is in.

Only the Python standard library is used.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

FORMAT = 1
SYMBOL = re.compile(r"^[A-G][#b]?(|m|7|maj7|m7|sus4|dim|m7\(b5\))(/[A-G][#b]?)?$")
OTHER = re.compile(r"^[A-G][#b]?(|m|7|maj7|m7|sus4|dim|m7\(b5\))$")
KEY = re.compile(r"^[A-G][#b]? (major|minor)$")
TIME = re.compile(r"^[1-9][0-9]?/(4|8)$")
JOIN = 0.001            # seconds two times that should be one may differ by (they are rounded to 0.0001)


def faults(doc):
    """What is wrong with a .dettato.json, as sentences; none when it is as docs/SIDECAR.md says."""
    said = []
    wrong = said.append
    number = lambda v: isinstance(v, (int, float)) and not isinstance(v, bool)
    if doc.get("dettato") != FORMAT:
        return [f'"dettato" is {doc.get("dettato")!r}: this checks format {FORMAT}']
    if not isinstance(doc.get("audio"), str) or not doc["audio"] or "\\" in doc["audio"] or doc["audio"].endswith("/"):
        wrong('"audio" is not a file name, or a way to one parted by "/"')
    if not number(doc.get("duration")) or doc["duration"] <= 0:
        wrong('"duration" is not a length in seconds')
    tempo = doc.get("tempo")
    if not isinstance(tempo, dict) or tempo.get("note") not in ("4", "4.") or not number(tempo.get("bpm")) or tempo["bpm"] <= 0 \
            or round(tempo["bpm"], 1) != tempo["bpm"]:
        wrong('"tempo" is not {"note": "4" or "4.", "bpm": a number to a tenth}')
    if not isinstance(doc.get("time"), str) or not TIME.match(doc["time"]):
        wrong('"time" is not a meter')
    elif isinstance(tempo, dict) and (tempo.get("note") == "4.") != (doc["time"] == "6/8"):
        wrong("the tempo counts dotted quarters in 6/8 and quarters elsewhere")
    if not isinstance(doc.get("key"), str) or not KEY.match(doc["key"]):
        wrong('"key" is not "<tonic> major" or "<tonic> minor"')
    if "tuning" in doc and not (isinstance(doc["tuning"], int) and -50 <= doc["tuning"] < 50):
        wrong('"tuning" is not a whole number of cents from -50 to 49')
    if "parts" in doc and not (isinstance(doc["parts"], dict)
                               and list(doc["parts"]) in (["drums", "bass", "other", "vocals", "guitar", "piano"], ["drums", "bass", "other", "vocals"])
                               and all(isinstance(v, str) and v for v in doc["parts"].values())):
        wrong('"parts" is not the ways to drums, bass, other, vocals, guitar and piano (or the first four, 0.7.0)')
    if "strange" in doc and doc["strange"] is not True:
        wrong('"strange" is there only when true')
    bars, form = doc.get("bars"), doc.get("form")
    if not isinstance(bars, list) or not bars:
        return said + ['"bars" has no bar']
    if not isinstance(form, list) or not form or not all(isinstance(s, str) and s for s in form):
        return said + ['"form" is not a list of section names']
    beats = 2 if doc.get("time") == "6/8" else int(doc["time"].split("/")[0]) if isinstance(doc.get("time"), str) and TIME.match(doc["time"]) else None

    sure = [("sure" in c) for bar in bars for c in bar.get("chords") or []]
    if any(sure) != ("doubt" in doc):
        wrong('"doubt" is there exactly when chords have "sure"')
    elif "doubt" in doc and not (number(doc["doubt"]) and 0 < doc["doubt"] < 1):
        wrong('"doubt" is not between 0 and 1')
    if any(sure) and not all(sure):
        wrong('some chords have "sure" and some have not')

    first = 0 if bars[0].get("pickup") else 1
    for i, bar in enumerate(bars):
        at = f"bars[{i}]"
        if bar.get("bar") != i + first:
            wrong(f'{at}: "bar" is {bar.get("bar")!r}, not {i + first}')
        if "pickup" in bar:
            if i:
                wrong(f"{at}: only the first bar may be a pickup")
            elif not isinstance(bar["pickup"], int) or not (beats is None or 0 < bar["pickup"] < beats):
                wrong(f'{at}: "pickup" is not a number of beats less than a bar')
        if not (number(bar.get("start")) and number(bar.get("end")) and 0 <= bar["start"] < bar["end"]):
            wrong(f"{at}: it does not begin before it ends")
            continue
        if i and number(bars[i - 1].get("end")) and abs(bar["start"] - bars[i - 1]["end"]) > JOIN:
            wrong(f"{at}: it begins at {bar['start']}, and the bar before ends at {bars[i - 1]['end']}")
        if number(doc.get("duration")) and bar["start"] >= doc["duration"]:
            wrong(f"{at}: it begins after the recording ends")
        if "beats" in bar:      # not in files written before 2026-11-16
            ticks, many = bar["beats"], bar.get("pickup") or beats
            if (not isinstance(ticks, list) or not ticks or not all(number(t) for t in ticks)
                    or (many is not None and len(ticks) != many)):
                wrong(f'{at}: "beats" is not the {many} beats of the bar')
            elif abs(ticks[0] - bar["start"]) > JOIN or any(b <= a for a, b in zip(ticks, ticks[1:])) or ticks[-1] >= bar["end"]:
                wrong(f'{at}: "beats" do not go from its start up to its end, one after the other')
        chords = bar.get("chords")
        if not isinstance(chords, list) or not chords:
            wrong(f"{at}: no chord")
            continue
        for j, c in enumerate(chords):
            where = f"{at}.chords[{j}]"
            if not (number(c.get("start")) and number(c.get("end")) and c["start"] < c["end"]):
                wrong(f"{where}: it does not begin before it ends")
                continue
            before = chords[j - 1].get("end") if j else bar["start"]
            if number(before) and abs(c["start"] - before) > JOIN:
                wrong(f"{where}: it begins at {c['start']}, where {before} is expected")
            if not isinstance(c.get("symbol"), str) or not SYMBOL.match(c["symbol"]):
                wrong(f'{where}: "symbol" {c.get("symbol")!r} is not a chord this format names')
            if not (number(c.get("fit")) and -1 <= c["fit"] <= 1):
                wrong(f'{where}: "fit" is not a number')
            if ("sure" in c) != ("other" in c):
                wrong(f'{where}: "sure" and "other" come together')
            elif "sure" in c:
                if not (number(c["sure"]) and 0 <= c["sure"] <= 1):
                    wrong(f'{where}: "sure" is not between 0 and 1')
                if not isinstance(c["other"], str) or not OTHER.match(c["other"]):
                    wrong(f'{where}: "other" {c["other"]!r} is not a root and a quality')
        if number(chords[-1].get("end")) and abs(chords[-1]["end"] - bar["end"]) > JOIN:
            wrong(f"{at}: its last chord ends at {chords[-1]['end']}, the bar at {bar['end']}")

    # the form, walked through the bars: each name is one playing of that section, bar 1 to its last
    if bool(bars[0].get("pickup")) != (form[0] == "Pickup") or "Pickup" in form[1:]:
        wrong('"Pickup" is the first section of the form exactly when the first bar is a pickup')
    i, long = 0, {}
    for n, name in enumerate(form):
        k = 0
        while i < len(bars) and bars[i].get("section") == name and bars[i].get("barOfSection") == k + 1:
            i, k = i + 1, k + 1
        if not k:
            wrong(f'form[{n}] is {name!r}, and bars[{i}] is not its first bar' if i < len(bars)
                  else f"form[{n}] is {name!r}, and there are no more bars")
            break
        if long.setdefault(name, k) != k:
            wrong(f"section {name!r} is {long[name]} bars long at first and {k} at form[{n}]")
    else:
        if i < len(bars):
            wrong(f"the form ends before bars[{i}]")
    return said


def midi_end(path):
    """(ticks a quarter note, the tick the last note ends at) of a standard MIDI file."""
    data = Path(path).read_bytes()
    if data[:4] != b"MThd":
        raise ValueError("not a MIDI file")
    quarter = int.from_bytes(data[12:14], "big")
    pos, last = 8 + int.from_bytes(data[4:8], "big"), 0

    def number(body, i):                # a variable-length quantity: (its value, where the next byte is)
        value = 0
        while True:
            value, i = (value << 7) | (body[i] & 0x7F), i + 1
            if body[i - 1] < 0x80:
                return value, i

    while pos + 8 <= len(data):
        size = int.from_bytes(data[pos + 4:pos + 8], "big")
        body, kind, pos = data[pos + 8:pos + 8 + size], data[pos:pos + 4], pos + 8 + size
        if kind != b"MTrk":
            continue
        i, tick, status = 0, 0, 0
        while i < len(body):
            delta, i = number(body, i)
            tick += delta
            if body[i] == 0xFF:         # a meta event
                length, i = number(body, i + 2)
                i += length
            elif body[i] in (0xF0, 0xF7):
                length, i = number(body, i + 1)
                i += length
            else:
                if body[i] & 0x80:
                    status, i = body[i], i + 1
                high = status & 0xF0
                event, i = body[i:i + (1 if high in (0xC0, 0xD0) else 2)], i + (1 if high in (0xC0, 0xD0) else 2)
                if high == 0x80 or (high == 0x90 and event[1] == 0):
                    last = max(last, tick)
    return quarter, last


def lysc_bars(charts):
    """How many bars Lily# plays of each chart, its repeats played (a pickup is one of them):
    {chart: bars}. `charts`: {chart: (its time, its pickup's beats or 0)}. Through `lysc midi
    --batch`, a few processes at once: the bars are as many as the chords last, the pickup's beats
    and then whole bars. A chart Lily# cannot play is not in it."""
    lysc = os.environ.get("DETTATO_LYSC", "lysc")
    listed = [(Path(c).resolve(), told) for c, told in charts.items()]
    workers = max(1, min(os.cpu_count() or 4, 8, len(listed) // 20 or 1))
    found = {}
    with tempfile.TemporaryDirectory() as d:
        jobs = []
        for w in range(workers):
            batch = Path(d) / f"{w}.txt"
            batch.write_text("".join(f"{c}\t{d}\\{w}-{k}\n" for k, (c, _) in enumerate(listed[w::workers])), encoding="utf-8")
            jobs.append(subprocess.Popen([lysc, "midi", "--batch", str(batch)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                         stdin=subprocess.DEVNULL))
        for job in jobs:
            job.wait()
        for w in range(workers):
            for k, (chart, (time, pickup)) in enumerate(listed[w::workers]):
                played = Path(d) / f"{w}-{k}" / (chart.stem + ".mid")
                if not played.exists():
                    continue
                quarter, last = midi_end(played)
                top, under = (int(x) for x in time.split("/"))
                bar = quarter * 4 * top / under
                beat = bar / (2 if time == "6/8" else top)
                whole = (last - pickup * beat) / bar
                if abs(whole - round(whole)) < 0.01:
                    found[chart] = round(whole) + (1 if pickup else 0)
    return found


def check(files, lysc=True):
    """Every file against the specification, and its bars against Lily#'s. Prints what is wrong;
    returns how many files are."""
    docs = {Path(f): json.loads(Path(f).read_text(encoding="utf-8")) for f in files}
    wrong = {f: faults(doc) for f, doc in docs.items()}
    charts = {f: f.with_name(f.name[:-len(".dettato.json")] + ".lys") for f in docs}
    told = "the specification"
    if lysc:
        there = {f: c.resolve() for f, c in charts.items() if c.exists()}
        played = lysc_bars({chart: (docs[f].get("time") or "4/4", (docs[f].get("bars") or [{}])[0].get("pickup") or 0)
                            for f, chart in there.items()})
        for f, chart in there.items():
            if chart not in played:
                wrong[f].append(f"lysc midi does not play {chart.name} in whole bars")
            elif played[chart] != len(docs[f].get("bars") or []):
                wrong[f].append(f"Lily# plays {played[chart]} bars of {chart.name}, and the file has {len(docs[f]['bars'])}")
        told += f", and {len(there)} of them the bars Lily# plays"
    bad = 0
    for f, said in wrong.items():
        if said:
            bad += 1
            print(f"{f}: " + "; ".join(said[:4]) + (f"; and {len(said) - 4} more" if len(said) > 4 else ""))
    print(f"sidecar: {len(docs) - bad} of {len(docs)} files are as {told}")
    return bad


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("input", type=Path, nargs="+", help="a .dettato.json, or a folder of them")
    p.add_argument("--no-lysc", action="store_true", help="do not ask Lily# how many bars the chart beside each file plays")
    args = p.parse_args()
    files = [f for path in args.input for f in (sorted(path.glob("*.dettato.json")) if path.is_dir() else [path])]
    if not files:
        sys.exit("no .dettato.json")
    sys.exit(1 if check(files, lysc=not args.no_lysc) else 0)


if __name__ == "__main__":
    main()
