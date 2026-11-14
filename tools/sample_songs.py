#!/usr/bin/env python3
"""Lily#'s own sample songs as songs with known answers, none of them gen_chords.py's making.

    py tools/sample_songs.py <LilySharp>/samples <lysc> <a .sf2> scratch/samples

Each of the four samples that has a chord row is rendered without it (`lysc wav`, a score of its
staves alone), and the chord row becomes the truth, timed at the tempo written. Two are a melody
alone, one a melody and a bass, one a voice and a piano with a ritardando and a fermata at the end
(the truth does not slow down with it). The copies and the recordings are not committed.
"""
import json, re, subprocess, sys, wave
from pathlib import Path

# name: (tempo note, bpm, time, key, pickup in quarters, staves played, the chord row)
SONGS = {
    "amazing-grace": ("4", 84, "3/4", "G major", 1, ["melody"],
                      "G | G | C | G | G | Em | D | D7 | G | G | C | G | Em | D7 | G"),
    "greensleeves": ("4.", None, "6/8", "A minor", 0.5, ["melody"],
                     "Am | G | Am | E | Am | G | Am E | Am | C | G | Am | E | C | G | Am E | Am"),
    "harbour-lights": ("4", 72, "4/4", "Eb major", 0, ["singer", "rh", "lh"],
                       "Eb | Cm7 | Ab Bb7 | Eb | Ab | Fm7 Bb7 | Gm7 C7 | Fm7 Bb7 | Eb"),
    "morning-light": ("4", 96, "4/4", "G major", 0, ["melody", "bass"],
                      "G | C | G | Em | C | D | Am7 D7 | G"),
}
SAMPLES, lysc, soundfont, out = Path(sys.argv[1]), sys.argv[2], sys.argv[3], Path(sys.argv[4])
out.mkdir(parents=True, exist_ok=True)
for name, (note, bpm, time, key, pickup, staves, row) in SONGS.items():
    text = (SAMPLES / f"{name}.lys").read_text(encoding="utf-8")
    text = re.sub(r"(?m)^singer .*$", "", text)          # the voice as an instrument: no singing engine here
    text += "\nscore band { " + "  ".join("staff " + s for s in staves) + " }\n"
    lys = out / f"{name}.lys"
    lys.write_text(text, encoding="utf-8", newline="\n")
    run = subprocess.run([lysc, "wav", "--soundfont", soundfont, "--score", "band", "-d", str(out), str(lys)],
                         capture_output=True, text=True, encoding="utf-8")
    wav = out / f"{name}-band.wav"
    if run.returncode != 0 or not wav.exists():
        print(name, "not rendered:", (run.stdout + run.stderr).strip()[:400]); continue
    with wave.open(str(wav), "rb") as f:
        audio = f.getnframes() / f.getframerate()
    bars = [b.split() for b in row.split(" | ")]
    quarters = {"3/4": 3, "4/4": 4, "6/8": 3}[time]
    if bpm is None:   # a bare `tempo 60` in 6/8: which note it counts is read off the file's length
        total = pickup + quarters * len(bars)
        per_quarter = (audio - 2.0) / total
        bpm = round(60 / per_quarter / 1.5)
        print(name, "a quarter lasts", round(per_quarter, 3), "s: tempo 4. =", bpm)
    quarter = 60 / bpm / (1.5 if note == "4." else 1)
    bar_seconds, start, doc = quarters * quarter, pickup * quarter, []
    for i, bar in enumerate(bars):
        a = start + i * bar_seconds
        step = bar_seconds / len(bar)
        doc.append({"bar": i + 1, "start": round(a, 4), "end": round(a + bar_seconds, 4),
                    "chords": [{"start": round(a + k * step, 4), "end": round(a + (k + 1) * step, 4), "symbol": c}
                               for k, c in enumerate(bar)]})
    truth = {"id": name, "audio": wav.name, "tempo": {"note": note, "bpm": bpm}, "time": time, "key": key,
             "band": {"drums": False, "styles": {"-": " ".join(staves)}},
             "musicSeconds": round(start + len(bars) * bar_seconds, 4), "audioSeconds": round(audio, 4), "bars": doc}
    (out / f"{name}.truth.json").write_text(json.dumps(truth, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{name}: {len(bars)} bars, music {truth['musicSeconds']} s, file {audio:.1f} s")
