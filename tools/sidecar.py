#!/usr/bin/env python3
"""Checks .dettato.json files against docs/SIDECAR.md, and against the chart beside each.

    py tools/sidecar.py scratch/read/seed1            # every <name>.dettato.json of a folder
    py tools/sidecar.py song.dettato.json --no-lysc   # the file alone

What is checked of a file: every item is there and is what the specification says; the bars are
numbered in the order they are played and follow one another in time; the chords of a bar fill it;
the sections and the form say the same as the bars do.

What is checked against the chart (<name>.lys beside the file, through `lysc layout`; DETTATO_LYSC
names lysc): Lily# plays as many bars as the file has. The n-th bar Lily# plays is the n-th of
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
    if not isinstance(doc.get("audio"), str) or not doc["audio"] or "/" in doc["audio"] or "\\" in doc["audio"]:
        wrong('"audio" is not a file name')
    if not number(doc.get("duration")) or doc["duration"] <= 0:
        wrong('"duration" is not a length in seconds')
    tempo = doc.get("tempo")
    if not isinstance(tempo, dict) or tempo.get("note") not in ("4", "4.") or not isinstance(tempo.get("bpm"), int) or tempo["bpm"] <= 0:
        wrong('"tempo" is not {"note": "4" or "4.", "bpm": a whole number}')
    if not isinstance(doc.get("time"), str) or not TIME.match(doc["time"]):
        wrong('"time" is not a meter')
    elif isinstance(tempo, dict) and (tempo.get("note") == "4.") != (doc["time"] == "6/8"):
        wrong("the tempo counts dotted quarters in 6/8 and quarters elsewhere")
    if not isinstance(doc.get("key"), str) or not KEY.match(doc["key"]):
        wrong('"key" is not "<tonic> major" or "<tonic> minor"')
    if "tuning" in doc and not (isinstance(doc["tuning"], int) and -50 <= doc["tuning"] < 50):
        wrong('"tuning" is not a whole number of cents from -50 to 49')
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


def lysc_bars(charts):
    """How many bars Lily# plays of each chart (a pickup is one of them): {chart: bars}, through
    `lysc layout --batch`, a few processes at once. A chart Lily# cannot lay out is not in it."""
    lysc = os.environ.get("DETTATO_LYSC", "lysc")
    charts = [Path(c).resolve() for c in charts]
    workers = max(1, min(os.cpu_count() or 4, 8, len(charts) // 20 or 1))
    jobs = []
    with tempfile.TemporaryDirectory() as d:
        for w in range(workers):
            listed = Path(d) / f"{w}.txt"
            listed.write_text("".join(f"{c}\n" for c in charts[w::workers]), encoding="utf-8")
            jobs.append(subprocess.Popen([lysc, "layout", "--batch", str(listed)], stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                         stdin=subprocess.DEVNULL, text=True, encoding="utf-8"))
        outputs = [job.communicate()[0] for job in jobs]
    found = {}
    for w, out in enumerate(outputs):
        mine, chart = charts[w::workers], None
        for line in out.splitlines():
            head = re.match(r"^\[(\d+)/\d+\] ", line)
            if head:
                chart = mine[int(head.group(1)) - 1]
                continue
            counted = re.search(r"systems?, (\d+) bars?\b", line)
            if counted and chart is not None:
                found[chart] = found.get(chart, 0) + int(counted.group(1))
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
        played = lysc_bars(list(there.values()))
        for f, chart in there.items():
            if chart not in played:
                wrong[f].append(f"lysc layout says nothing of {chart.name}")
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
