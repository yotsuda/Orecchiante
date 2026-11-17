# The file beside a chart: `<name>.dettato.json`

Dettato writes two files for a recording `<name>.wav`: the chart `<name>.lys`, which a person
corrects, and `<name>.dettato.json`, which says **where in the recording every bar of that chart
is**. An editor reads it to play the recording's own bar while the bar is being corrected; the
measuring tools read it to count a reading against the truth.

This is format **1** (2026-11-14; `tuning` and the bars' `beats` added 2026-11-16, for listening
to the recording beside the chart in Lily#; `parts` added 2026-11-17, to hear one part of it at a time). `dettato` and `tools/read_chords.py` write it, `tools/sidecar.py` checks a
file against this page and against the chart beside it, and `tools/measure.py` runs that check on
every chart it writes.

## An example

```json
{
  "dettato": 1,
  "audio": "song.wav",
  "duration": 53.52,
  "tempo": { "note": "4", "bpm": 117 },
  "time": "4/4",
  "key": "F# major",
  "tuning": -12,
  "doubt": 0.84,
  "form": ["Pickup", "A", "B", "A", "C"],
  "bars": [
    { "bar": 0, "pickup": 3, "start": 0.355, "end": 1.9,
      "beats": [0.355, 0.87, 1.38],
      "section": "Pickup", "barOfSection": 1,
      "chords": [
        { "start": 0.355, "end": 1.9, "symbol": "C#7", "fit": 0.955, "sure": 0.973, "other": "F#" }
      ] },
    { "bar": 1, "start": 1.9, "end": 3.96,
      "beats": [1.9, 2.42, 2.94, 3.44],
      "section": "A", "barOfSection": 1,
      "chords": [
        { "start": 1.9, "end": 3.96, "symbol": "F#/A#", "fit": 0.802, "sure": 0.981, "other": "C#" }
      ] }
  ]
}
```

## The items

Every time is in **seconds from the start of the recording**, rounded to 0.0001. For an mp3 or an
AAC file the start is where a player that heeds the file's own note of its encoder's silence starts
(a browser, ffmpeg), not that silence's start: the reader takes it out (README, "A recording as it
comes"; an mp3 with no such note and a bare `.aac` keep theirs, 13 and 23 ms as ffmpeg writes them).

### Of the file

| Item | | What it is |
|---|---|---|
| `dettato` | always | The format's number: 1. |
| `audio` | always | The recording, as found from the folder of this file: its file name when it is beside this file (as `dettato <recording>` writes it); when the chart was written elsewhere (`-o`), the way there from this file's folder, folders parted by `/` (`../Music/x.mp3`), or the whole path when the two are on different drives (`F:/Music/x.mp3`). (Before 2026-11-16, 0.3.0: always the file name alone. The owner chose the way to it over copying the recording beside the chart, 2026-11-16.) |
| `duration` | always | The recording's length in seconds, right to within 0.03 s. |
| `tempo` | always | `note` is the beat, `"4"` or (in 6/8) `"4."`; `bpm` is how many of them a minute, to a tenth (`131.8`; a whole number is written bare, `117`), from the bars about as long as the median one. The chart's `tempo` line, the same number (Lily# sounds the tenth and prints "♩ ≈ 132"). (Before 2026-11-16, 0.3.1: always a whole number. Not the written bars' beats over their span: that drifts with a bar read wrong and with the rests of a long recording; Lily# is asked to set the recording's pace from the bars one by one, REQUESTS 8.) |
| `time` | always | The meter: `"4/4"`, `"3/4"`, `"2/4"`, `"6/8"`, or another number of quarters. One for the whole recording. |
| `key` | always | `"<tonic> major"` or `"<tonic> minor"`, the tonic a letter with `#` or `b`. It is what the chords are spelled by. |
| `tuning` | when measured | How far the recording's pitches are from A = 440 Hz, in whole cents, -50 to 49: -12 is 12 cents flat. The commonest distance of its louder spectral peaks from a semitone (librosa's `estimate_tuning`, 12 bins to the octave). Half a semitone is as far as it can tell: 70 cents sharp reads as -30. For an old tape run fast or slow, the editor turns the pitch by this to bring it back. `dettato` always writes it (2026-11-16 on); `read_chords.py` from features made before then does not. |
| `parts` | when written | The recording separated into parts, for a player to turn each on or off (to hear the bass alone, or all but the singing): `{"drums": …, "bass": …, "other": …, "vocals": …, "guitar": …, "piano": …}`, in that order (0.8.0; 0.7.0 wrote the first four, guitar and piano then in `other`: a player takes the parts as they are listed), each the way to a FLAC file as `audio` is found (`dettato <recording>` writes them into a folder beside this file named like it, `<name>.parts/bass.flac`). Each part is one channel, 16 bits at 44100 Hz, as long as the recording and **on its seconds** (the first sample is the recording's first: FLAC puts nothing before it), and they add up to the recording (in mono): `other` is whatever the others are not (Demucs `htdemucs_6s`, what none of its parts takes put in `other`; README, "The parts", "Six parts"). `dettato` writes them on every reading (2026-11-17, 0.7.0, the owner); `dettato read` and `read_chords.py` do not. A recording read again with `--force` has its parts written over. |
| `strange` | when true | The reader's own model did not know the sound, and the bar lines are the rules' (the reader without `--ears` only). |
| `doubt` | with `sure` | A chord whose `sure` is below this is doubted. |
| `form` | always | The names of the sections in the order they are played. `"Pickup"` is first when the first bar is a pickup. A section of three bars or more is a letter, `"A"`, `"B"` … in the order they come (`"S27"` … past Z); a shorter one is named after the section played before it, `"B_1"`, `"B_2"` … (after the next one, `"A_1"`, when none is before it), and the chart hides its name (`~B_1` in its `form`; 2026-11-16, 0.3.4, the owner). |
| `bars` | always | **Every bar played, in the order it is played**: a bar of a section played three times is here three times, each with its own seconds. |

### Of a bar

| Item | | What it is |
|---|---|---|
| `bar` | always | Its number: 1, 2, 3 … in the order played; a pickup is 0. |
| `pickup` | a pickup | How many beats (of `tempo.note`) the short bar has. Only the first bar may have it. |
| `start`, `end` | always | Its first bar line and the next. A bar begins where the bar before it ends. |
| `beats` | always (2026-11-16 on) | Where each of its beats (of `tempo.note`) begins: as many as the meter has (two in 6/8), or as `pickup` says; the first is `start`, all before `end`. The bar cut evenly: the beat tracker's own beats inside a bar are no nearer the beats annotated in RWC (2026-11-16: within 70 ms of one, 96.2% of both on `rwc-dev`, 94.3% of both on `rwc-test`). Here so that an editor has one place to read beats from, and a person's beats (format 2) one place to go. |
| `section` | always | The section it is a bar of. |
| `barOfSection` | always | Which bar of that section, from 1. |
| `chords` | always | Its chords, one after the other; they fill the bar. Never empty. |

- The **last bar's `end`** may be later than `duration`: a recording that stops inside a bar has
  that bar written whole, as long as the bar before it. How long the last chord rings is not said.
- A pickup's `start` is where its chord begins, not a bar line.

### Of a chord

| Item | | What it is |
|---|---|---|
| `start`, `end` | always | Where it sounds, cut at the bar's lines: a chord held over a bar line is one chord in each bar. |
| `symbol` | always | A root (`C`, `F#`, `Bb`), a quality, and `/` and a bass note when the bass is not the root. The qualities: none (major), `m`, `7`, `maj7`, `m7`, `sus4`, `dim`, `m7(b5)`. |
| `fit` | always | How well the sound fits the chord to the reader's own hearing, 1 at most. For looking into a reading; the marks do not go by it. |
| `sure` | a model heard it | How likely the root and the family (major, minor, suspended, diminished) are right, 0 to 1. Not there for the reader with nothing learnt. |
| `other` | with `sure` | The chord offered in its place: a root and a quality, no bass. |

After the sections are found, every playing of a section says the same chords on every beat (the
chord the playings are surest of together), so the `symbol`s of a bar are those of the chart; the
seconds, `fit` and `sure` are each playing's own.

## The chart and the file

- **The n-th bar Lily# plays is `bars[n]`** (from 0; a pickup is a bar). Lily# plays a chart
  through its `form` and the form's repeat signs, so a bar written once in a section played three
  times is a bar of `bars` each time it is played. `tools/sidecar.py`
  counts the bars Lily# plays of each chart (from the end of `lysc midi`'s last note: the pickup's
  beats, then whole bars) against the length of `bars`.
- The chart is written in sections when a section is played twice or there is a pickup; otherwise
  as one row of bars with no `section` and no `form`. The file names sections either way.
- **Repeat signs are in the chart's `form` only (2026-11-16, the owner's wish: the form alone
  shows how the song goes)**: sections played again running are `|: A :|`, `|: A B :|`,
  `|: A :|*3`, and a section followed by a different section each time is `|: A [1. B] :| [2. C]`
  (one section in each ending; the body may be several sections, `|: B C B [1. D] :| [2. E]`). The bars inside a section are written out. The file's `form`
  stays as played (`["A", "A", "B"]` for `|: A :| B`).
- **Sections of six bars or fewer that come one after the other are one section** (2026-11-16, the
  owner's wish: four and four make eight, also the same four twice; four beside eight stay). So
  `section` and `barOfSection` name the joined sections. **Sections played once that come one after
  the other are one section up to 32 bars**; a longer run, and a section played more than once
  that is longer, is cut into as few sections of 32 bars or fewer as can be, at the sections found
  there (where the music turns) or every eight bars, never evenly for its own sake.
- **Each line of the chart's page is four bars of one section**: `break` after every four bars
  and at the end of each section (not in the pickup, which goes on with the first line). A last
  line of one bar joins the line before (five), and four and two are three and three (`line_sizes`:
  9 bars are 4 5, 10 are 4 3 3, 6 are 3 3).
- A chord of the chart is marked `@todo("or X?")` when the mean `sure` of that chord in that bar,
  over the playings of the bar, is below `doubt`; `X` is the `other` of the least sure of them.
- In a pickup of several chords that Lily# cannot write a chord a beat (three beats in 4/4), the
  chart has the last chord only; the file has them all.

### Finding the seconds of the bar the caret is in

What an editor needs is: this bar of the `.lys` → which of `bars`. Lily#'s language server already
answers it for its own playback (`lilysharp/playback`, `docs/playback-design.md` in Lily#): each
chord's notes come with their source offset (`S`) and their time in the score (`T`), once for
every time the form plays them, and `Beats` marks the first beat of each bar (`A`). So:

1. the notes whose `S` lies in the caret's bar give one `T` for each playing of that bar;
2. the bars begun at or before a `T` (the accented beats) say which bar played it is: `n`;
3. `bars[n].start` to `bars[n].end` is that bar in the recording. Within a bar the recording and
   the score go together in a straight line; from one bar to the next they do not (the tempo of
   a recording moves, the chart's does not).

Lily# measured the two points this leans on (its reply to request 2, 2026-11-14): `Beats` gives
a `partial` bar no accented beat of its own (the first `A` is the line at the pickup's end), so
`n` is the number of `A` at or before `T` when there is a pickup, and that number less one when
there is none; `lilysharp/placeAt` gives a pickup bar 0, as `"bar": 0` here.

### After the chart is corrected

The file is the reading as it was written; nothing rewrites it.

- Changing a chord, a mark, the key or the title leaves the bars where they are: the file holds.
- **Adding or taking out a bar, or changing the form, moves every bar after it.** An editor can
  tell: Lily# then plays a different number of bars than `bars` has. It should say so, and still
  play the recording with the chart as far as the shorter of the two goes, the n-th bar played with
  the n-th of `bars` (the owner, 2026-11-16: a chart being cut into sections or mended bar by bar is
  still to be heard to its end; before 2026-11-16 this said not to play at all). The bars after the
  one added or taken out are then a bar off, which the person is in the middle of putting right.
  Lining the two up again is not built.
- `symbol`, `sure` and `other` are the reader's, not the corrected chart's.

## What is not in it

- Where the last chord stops ringing.
- Bars with no chord: an intro of drums alone, a pickup of melody alone, silence, a count-in. The
  chart begins at the first bar that sounds a chord and ends at the last.
- Anything that tells one recording from another of the same name and length.

## Changing the format

A reader of the file takes the items it knows and leaves the rest: an item may be added without
a new number. The number is raised when an item changes what it means or goes away, and a reader
refuses a number it does not know (`tools/score_chords.py`, `tools/sidecar.py`).

Files written before 2026-11-14 have no `dettato`, `duration` or `doubt`, and are otherwise this.

## The truth files

`tools/gen_chords.py` writes `<id>.truth.json` in the same shape (`audio`, `tempo`, `time`, `key`,
`form`, and `bars` with `bar`, `pickup`, `start`, `end`, `section`, `barOfSection` and `chords` of
`start`, `end`, `symbol`), with items of its own (`id`, `musicSeconds`, `chordSeconds`, `silence`,
`band`, a bar's `bpm` where the tempo steps) and without `dettato`, `duration`, `doubt`, `fit` or
`sure`. A true pickup of melody alone has no chords. The truths made from annotations
(`tools/rwc_truth.py`) have no sections. `tools/sidecar.py` does not check truth files.
