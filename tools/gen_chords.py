#!/usr/bin/env python3
"""Makes songs whose chords, bar lines and tempo are known, and has Lily# play them.

Each song is a Lily# file (.lys) written from a seed: a chord row, and a small band that plays
it (a chord instrument, a bass, drums, sometimes a melody). Lily#'s `lysc wav` renders the band;
the chord row is not played, it is the answer. Beside each recording a .truth.json says where
every bar and every chord is, in seconds.

    py tools/gen_chords.py --seed 1 --count 24 --out data/synth/seed1 --soundfont x.sf2

The same seed writes the same songs. Only the Python standard library is used.
"""
import argparse
import array
import json
import os
import random
import re
import subprocess
import sys
import wave
from pathlib import Path

LETTERS = "CDEFGAB"
LETTER_PC = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
SHARP_NAMES = ["c", "cis", "d", "dis", "e", "f", "fis", "g", "gis", "a", "ais", "b"]
FLAT_NAMES = ["c", "des", "d", "ees", "e", "f", "ges", "g", "aes", "a", "bes", "b"]
SIMPLE_ROOTS = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"]

MAJOR_KEYS = ["C", "G", "D", "A", "E", "B", "F#", "Db", "Ab", "Eb", "Bb", "F"]
MINOR_KEYS = ["A", "E", "B", "F#", "C#", "G#", "Eb", "Bb", "F", "C", "G", "D"]
FLAT_MINORS = {"C", "G", "D", "F"}

INTERVALS = {
    "": [0, 4, 7], "m": [0, 3, 7], "7": [0, 4, 7, 10], "maj7": [0, 4, 7, 11],
    "m7": [0, 3, 7, 10], "sus4": [0, 5, 7], "dim": [0, 3, 6], "m7(b5)": [0, 3, 6, 10],
}

# degree name -> (letter steps above the tonic, semitones above it, triad, seventh chord)
MAJOR_DEGREES = {
    "I": (0, 0, "", "maj7"), "ii": (1, 2, "m", "m7"), "iii": (2, 4, "m", "m7"),
    "IV": (3, 5, "", "maj7"), "iv": (3, 5, "m", "m7"), "V": (4, 7, "", "7"),
    "vi": (5, 9, "m", "m7"), "bVII": (6, 10, "", ""),
}
MINOR_DEGREES = {
    "i": (0, 0, "m", "m7"), "ii": (1, 2, "dim", "m7(b5)"), "III": (2, 3, "", "maj7"),
    "iv": (3, 5, "m", "m7"), "v": (4, 7, "m", "m7"), "V": (4, 7, "", "7"),
    "VI": (5, 8, "", "maj7"), "VII": (6, 10, "", "7"),
}
MAJOR_ROWS = [
    "I V vi IV", "I vi IV V", "ii V I I", "I IV V IV", "vi IV I V", "I iii IV V",
    "IV V iii vi", "I IV ii V", "I bVII IV I", "I IV iv I", "vi ii V I", "I vi ii V",
    "IV I V vi", "I iii vi IV", "ii V iii vi", "vi V IV V",
]
MINOR_ROWS = [
    "i VI III VII", "i iv V i", "i VII VI V", "i iv VII III", "ii V i i", "i VI iv V",
    "i v VI VII", "iv VII III VI", "i III VII iv",
]
FORMS = ["A A B A", "A B A B", "A B A C A", "A B B A", "A A B", "A B C B"]

# meter -> eighths in a bar, eighths in a beat, how a bar of two chords divides, the tempo's note
METERS = {
    "4/4": (8, 2, [4, 4], "4"),
    "3/4": (6, 2, None, "4"),
    "6/8": (6, 3, [3, 3], "4."),
}
TEMPI = {"4/4": (70, 170), "3/4": (84, 180), "6/8": (48, 100)}
DUR = {1: "8", 2: "4", 3: "4.", 4: "2", 6: "2.", 8: "1"}
FIRST_NOTE = re.compile(r"(?<![\w<])(?:<[^>]+>|[a-g](?:is|es)?[',]*)\d+\.?")

# hats, backbeat, kick: one bar each
DRUMS = {
    "4/4": [
        ("hh8 8 8 8 8 8 8 8", "r4 sn r sn", "bd4 r8 bd bd4 r"),
        ("hh8 8 8 8 8 8 8 8", "r4 sn r sn", "bd4 bd bd bd"),
        ("hh4 hh hh hh", "r2 sn4 r", "bd4 r r8 bd r4"),
    ],
    "3/4": [
        ("hh4 hh hh", "r4 sn sn", "bd4 r r"),
        ("hh8 8 8 8 8 8", "r4 sn r", "bd4 r bd"),
    ],
    "6/8": [
        ("hh8 8 8 8 8 8", "r4. sn", "bd4. r"),
        ("hh4. hh", "r4. sn", "bd4 bd8 r4."),
    ],
}
COMP_SOUNDS = [
    "acoustic grand", "electric piano 1", "electric piano 2", "acoustic guitar (nylon)",
    "acoustic guitar (steel)", "electric guitar (clean)", "overdriven guitar",
    "drawbar organ", "rock organ", "string ensemble 1",
]
BASS_SOUNDS = ["electric bass (finger)", "electric bass (pick)", "acoustic bass", "fretless bass"]
LEAD_SOUNDS = ["flute", "violin", "alto sax", "trumpet", "clarinet", "vibraphone"]
COMP_STYLES = ["sustain", "pulse", "arp", "offbeat"]
BASS_STYLES = ["root", "root5", "eighths"]
MELODY_BARS = {
    "4/4": [[2, 2, 2, 2], [4, 2, 2], [2, 2, 4], [1, 1, 2, 2, 2], [3, 1, 2, 2], [4, 4], [8]],
    "3/4": [[2, 2, 2], [4, 2], [2, 4], [2, 1, 1, 2], [6]],
    "6/8": [[3, 3], [2, 1, 2, 1], [1, 1, 1, 3], [3, 2, 1], [6]],
}


def pc_of(name):
    return (LETTER_PC[name[0]] + name.count("#") - name.count("b")) % 12


def spell(tonic, steps, semis):
    """The note `steps` letters and `semis` semitones above the tonic, as a chord root."""
    letter = LETTERS[(LETTERS.index(tonic[0]) + steps) % 7]
    pc = (pc_of(tonic) + semis) % 12
    acc = (pc - LETTER_PC[letter] + 6) % 12 - 6
    if abs(acc) > 1:  # a chord root takes one sharp or flat
        return SIMPLE_ROOTS[pc]
    return letter + ("#" * acc if acc > 0 else "b" * -acc)


class Chord:
    def __init__(self, steps, semis, quality):
        self.steps, self.semis, self.quality = steps % 7, semis % 12, quality
        self.bass = None  # (steps, semis) of a bass note that is not the root

    def member(self, i):
        letters = [0, 2, 4, 6]
        return ((self.steps + letters[i]) % 7, (self.semis + INTERVALS[self.quality][i]) % 12)

    def symbol(self, tonic):
        text = spell(tonic, self.steps, self.semis) + self.quality
        if self.bass:
            text += "/" + spell(tonic, *self.bass)
        return text

    def pcs(self, tonic):
        root = (pc_of(tonic) + self.semis) % 12
        return [(root + i) % 12 for i in INTERVALS[self.quality]]

    def bass_pc(self, tonic):
        return (pc_of(tonic) + (self.bass[1] if self.bass else self.semis)) % 12

    def fifth_pc(self, tonic):
        if self.bass:  # over another bass note, the second note is the root
            return (pc_of(tonic) + self.semis) % 12
        return self.pcs(tonic)[2]


class Song:
    def __init__(self, song_id, rng):
        self.id = song_id
        self.rng = rng
        self.mode = rng.choice(["major", "major", "minor"])
        self.tonic = rng.choice(MAJOR_KEYS if self.mode == "major" else MINOR_KEYS)
        self.meter = rng.choices(list(METERS), weights=[70, 15, 15])[0]
        self.bar8, self.beat8, self.split, self.tempo_note = METERS[self.meter]
        self.bpm = rng.randint(*TEMPI[self.meter])
        self.flats = "b" in self.tonic or (
            self.tonic == "F" if self.mode == "major" else self.tonic in FLAT_MINORS)
        self.seventh_rate = rng.choice([0.0, 0.3, 0.7])
        self.split_rate = rng.choice([0.0, 0.15, 0.3]) if self.split else 0.0
        self.comp_sound = rng.choice(COMP_SOUNDS)
        self.bass_sound = rng.choice(BASS_SOUNDS)
        self.lead_sound = rng.choice(LEAD_SOUNDS) if rng.random() < 0.5 else None
        self.drums = rng.choice(DRUMS[self.meter] + [None])
        self.form = rng.choice(FORMS).split()
        self.sections = {}
        for name in sorted(set(self.form)):
            self.sections[name] = {
                "bars": self.make_bars(),
                "comp": rng.choice(COMP_STYLES),
                "bass": rng.choice(BASS_STYLES),
            }
        # how loud the melody, the chords and the bass play: at one level the bass and its
        # overtones cover the chords, as no mixed recording lets them
        self.levels = (rng.choice(["mp", "mf"]), rng.choice(["f", "ff", "fff"]), rng.choice(["p", "mp"]))

    # --- the chords ---

    def make_bars(self):
        rng = self.rng
        degrees = MAJOR_DEGREES if self.mode == "major" else MINOR_DEGREES
        rows = MAJOR_ROWS if self.mode == "major" else MINOR_ROWS
        first = rng.choice(rows)
        second = first if rng.random() < 0.5 else rng.choice(rows)
        chords = []
        for name in (first + " " + second).split():
            steps, semis, triad, seventh = degrees[name]
            quality = seventh if seventh and rng.random() < self.seventh_rate else triad
            if name == "V" and quality in ("", "7") and rng.random() < 0.1:
                quality = "sus4"
            chord = Chord(steps, semis, quality)
            if quality in ("", "m", "7", "m7") and rng.random() < 0.06:
                chord.bass = chord.member(rng.choice([1, 2]))
            chords.append(chord)
        bars = []
        for i, chord in enumerate(chords):
            bar = [chord]
            following = chords[(i + 1) % len(chords)]
            if rng.random() < self.split_rate and following.quality != "dim":
                # the second half of the bar leads to the next bar: its dominant seventh
                bar.append(Chord(following.steps + 4, following.semis + 7, "7"))
            bars.append(bar)
        return bars

    def spans(self, bar):
        """(length in eighths, chord) for each chord of a bar."""
        if len(bar) == 1:
            return [(self.bar8, bar[0])]
        return list(zip(self.split, bar))

    # --- the notes ---

    def note(self, midi):
        name = (FLAT_NAMES if self.flats else SHARP_NAMES)[midi % 12]
        octave = midi // 12 - 1 - 3  # a bare letter is octave 3 under `octave absolute`
        return name + ("'" * octave if octave > 0 else "," * -octave)

    def in_range(self, pc, low):
        """The pitch of this pitch class in the octave that starts at `low`."""
        return low + (pc - low) % 12

    def comp_bar(self, bar, style):
        out = []
        for length, chord in self.spans(bar):
            tones = sorted(self.in_range(pc, 55) for pc in chord.pcs(self.tonic))
            stack = "<" + " ".join(self.note(t) for t in tones) + ">"
            beats = length // self.beat8
            if style == "sustain":
                out.append(stack + DUR[length])
            elif style == "pulse":
                out += [stack + DUR[self.beat8]] * beats
            elif style == "arp":
                out += [self.note(tones[i % len(tones)]) + "8" for i in range(length)]
            else:  # offbeat
                out += (["r8"] + [stack + "8"] * (self.beat8 - 1)) * beats
        return " ".join(out)

    def bass_bar(self, bar, style):
        out = []
        for length, chord in self.spans(bar):
            root = self.note(self.in_range(chord.bass_pc(self.tonic), 40))
            fifth = self.note(self.in_range(chord.fifth_pc(self.tonic), 40))
            beats = length // self.beat8
            if style == "root":
                out.append(root + DUR[length])
            elif style == "root5":
                out += [(fifth if i % 2 else root) + DUR[self.beat8] for i in range(beats)]
            else:  # eighths
                out += [root + "8"] * length
        return " ".join(out)

    def lead_bar(self, bar, state):
        rng = self.rng
        if rng.random() < 0.15:
            return "r" + DUR[self.bar8]
        tonic_pc = pc_of(self.tonic)
        steps = [0, 2, 4, 5, 7, 9, 11] if self.mode == "major" else [0, 2, 3, 5, 7, 8, 10]
        scale = [p for p in range(67, 85) if (p - tonic_pc) % 12 in steps]
        starts, at = {}, 0
        for length, chord in self.spans(bar):
            starts[at] = chord
            at += length
        out, at, chord = [], 0, bar[0]
        for length in rng.choice(MELODY_BARS[self.meter]):
            chord = starts.get(at, chord)
            if at in starts or length >= 4:
                tones = [p for p in range(67, 85) if p % 12 in chord.pcs(self.tonic)]
                tones.sort(key=lambda p: abs(p - state["pitch"]))
                pitch = rng.choice(tones[:2])
            else:
                nearest = min(range(len(scale)), key=lambda i: abs(scale[i] - state["pitch"]))
                moved = nearest + rng.choice([-2, -1, -1, 1, 1, 2])
                pitch = scale[max(0, min(len(scale) - 1, moved))]
            state["pitch"] = pitch
            out.append(self.note(pitch) + DUR[length])
            at += length
        return " ".join(out)

    # --- the file ---

    @staticmethod
    def row(bars, level):
        """A part's bars as one line of music, its first note marked with the part's level."""
        text = " | ".join(bars) + " |"
        return FIRST_NOTE.sub(lambda m: m.group(0) + "@" + level, text, count=1)

    def key_line(self):
        acc = {"": "", "#": "is", "b": "es"}[self.tonic[1:]]
        return f"key {self.tonic[0].lower()}{acc} {self.mode}"

    def lys(self):
        lines = [
            f"// {self.id}: written by tools/gen_chords.py. The chord row is the answer;",
            "// the score `band` is what the recording plays (the chord row is not in it).",
            f'title "{self.id}"',
            f"tempo {self.tempo_note} = {self.bpm}",
            f"time {self.meter}",
            self.key_line(),
            "octave absolute",
            "",
        ]
        if self.lead_sound:
            lines.append(f'part lead {{ clef treble  midiInstrument "{self.lead_sound}" }}')
        lines.append(f'part comp {{ clef treble  midiInstrument "{self.comp_sound}" }}')
        lines.append(f'part bass {{ instrument bass  midiInstrument "{self.bass_sound}" }}')
        if self.drums:
            hats, back, kick = self.drums
            lines += [
                "part kit { clef percussion }",
                "",
                f"phrase hats {{ {hats} | }}",
                f"phrase back {{ {back} | }}",
                f"phrase kick {{ {kick} | }}",
                "phrase groove { drums { hats back kick } }",
            ]
        state = {"pitch": 76}
        for name, section in self.sections.items():
            bars = section["bars"]
            row = " | ".join(" ".join(c.symbol(self.tonic) for c in bar) for bar in bars)
            lines += ["", f"section {name} {{", f"  chords harmony {{ {row} | }}"]
            if self.lead_sound:
                lines.append("  lead { " + self.row([self.lead_bar(b, state) for b in bars], self.levels[0]) + " }")
            comp = self.row([self.comp_bar(b, section["comp"]) for b in bars], self.levels[1])
            bass = self.row([self.bass_bar(b, section["bass"]) for b in bars], self.levels[2])
            lines += [f"  comp {{ {comp} }}", f"  bass {{ {bass} }}"]
            if self.drums:
                lines.append("  kit { " + " ".join(["groove"] * len(bars)) + " }")
            lines.append("}")
        staves = (["lead"] if self.lead_sound else []) + ["comp", "bass"] + (["kit"] if self.drums else [])
        lines += [
            "",
            "form { " + " ".join(self.form) + " }",
            "",
            "score { chords harmony }",
            "",
            "score band { " + "  ".join("staff " + s for s in staves) + " }",
            "",
        ]
        return "\n".join(lines)

    def truth(self):
        eighth = 60.0 / self.bpm / self.beat8
        bar_seconds = self.bar8 * eighth
        bars, index = [], 0
        for name in self.form:
            for n, bar in enumerate(self.sections[name]["bars"]):
                start = index * bar_seconds
                chords, at = [], 0
                for length, chord in self.spans(bar):
                    chords.append({
                        "start": round(start + at * eighth, 4),
                        "end": round(start + (at + length) * eighth, 4),
                        "symbol": chord.symbol(self.tonic),
                    })
                    at += length
                bars.append({
                    "bar": index + 1, "section": name, "barOfSection": n + 1,
                    "start": round(start, 4), "end": round(start + bar_seconds, 4),
                    "chords": chords,
                })
                index += 1
        return {
            "id": self.id,
            "source": self.id + ".lys",
            "audio": self.id + "-band.wav",
            "tempo": {"note": self.tempo_note, "bpm": self.bpm},
            "time": self.meter,
            "key": f"{self.tonic} {self.mode}",
            "form": self.form,
            "band": {
                "comp": self.comp_sound, "bass": self.bass_sound, "lead": self.lead_sound,
                "drums": bool(self.drums),
                "levels": {"lead": self.levels[0], "comp": self.levels[1], "bass": self.levels[2]},
                "styles": {n: {"comp": s["comp"], "bass": s["bass"]} for n, s in self.sections.items()},
            },
            "musicSeconds": round(len(bars) * bar_seconds, 4),
            "bars": bars,
        }


def measure(wav_path):
    """Seconds of the file, and the time of its first sample above 1% of full scale."""
    with wave.open(str(wav_path), "rb") as f:
        rate, channels, frames = f.getframerate(), f.getnchannels(), f.getnframes()
        head = array.array("h", f.readframes(min(frames, rate * 5)))
    first = next((i for i, v in enumerate(head) if abs(v) > 327), None)
    return frames / rate, (None if first is None else first // channels / rate)


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--count", type=int, default=24)
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--lysc", default=os.environ.get("DETTATO_LYSC", "lysc"),
                   help="Lily#'s compiler (default: DETTATO_LYSC, else lysc on the PATH)")
    p.add_argument("--soundfont", default=os.environ.get("LILYSHARP_SOUNDFONT"),
                   help="a SoundFont 2 file (default: LILYSHARP_SOUNDFONT)")
    p.add_argument("--no-render", action="store_true", help="write the .lys and the truth only")
    args = p.parse_args()
    if not args.no_render and not args.soundfont:
        p.error("name a SoundFont 2 with --soundfont, or pass --no-render")
    args.out.mkdir(parents=True, exist_ok=True)
    failed = 0
    for i in range(1, args.count + 1):
        song_id = f"s{args.seed}-{i:03d}"
        song = Song(song_id, random.Random(args.seed * 100003 + i))
        lys = args.out / (song_id + ".lys")
        lys.write_text(song.lys(), encoding="utf-8", newline="\n")
        truth = song.truth()
        if not args.no_render:
            run = subprocess.run(
                [args.lysc, "wav", "--soundfont", args.soundfont, "--score", "band",
                 "-d", str(args.out), str(lys)],
                capture_output=True, text=True, encoding="utf-8")
            said = (run.stdout + run.stderr).strip()
            wav = args.out / truth["audio"]
            if run.returncode != 0 or "warning" in said or "error" in said or not wav.exists():
                failed += 1
                print(f"{song_id}: lysc said (exit {run.returncode}):\n{said}", file=sys.stderr)
                continue
            seconds, first = measure(wav)
            truth["audioSeconds"] = round(seconds, 4)
            truth["firstSound"] = None if first is None else round(first, 4)
            truth["soundfont"] = Path(args.soundfont).name
        (args.out / (song_id + ".truth.json")).write_text(
            json.dumps(truth, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print(f"{song_id}: {truth['key']}, {truth['time']}, {truth['tempo']['bpm']}, "
              f"{len(truth['bars'])} bars, {truth['musicSeconds']:.1f} s"
              + (f", sound from {truth['firstSound']} s, file {truth['audioSeconds']:.1f} s"
                 if "audioSeconds" in truth else ""))
    if failed:
        sys.exit(f"{failed} of {args.count} songs were not rendered")


if __name__ == "__main__":
    main()
