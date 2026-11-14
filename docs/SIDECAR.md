# The file beside a chart: `<name>.dettato.json`

Dettato writes two files for a recording `<name>.wav`: the chart `<name>.lys`, which a person
corrects, and `<name>.dettato.json`, which says **where in the recording every bar of that chart
is**. An editor reads it to play the recording's own bar while the bar is being corrected; the
measuring tools read it to count a reading against the truth.

This is format **1** (2026-11-14). `tools/read_chords.py` writes it, `tools/sidecar.py` checks a
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
  "doubt": 0.84,
  "form": ["Pickup", "A", "B", "A", "C"],
  "bars": [
    { "bar": 0, "pickup": 3, "start": 0.355, "end": 1.9,
      "section": "Pickup", "barOfSection": 1,
      "chords": [
        { "start": 0.355, "end": 1.9, "symbol": "C#7", "fit": 0.955, "sure": 0.973, "other": "F#" }
      ] },
    { "bar": 1, "start": 1.9, "end": 3.96,
      "section": "A", "barOfSection": 1,
      "chords": [
        { "start": 1.9, "end": 3.96, "symbol": "F#/A#", "fit": 0.802, "sure": 0.981, "other": "C#" }
      ] }
  ]
}
```

## The items

Every time is in **seconds from the start of the recording**, rounded to 0.0001.

### Of the file

| Item | | What it is |
|---|---|---|
| `dettato` | always | The format's number: 1. |
| `audio` | always | The recording's file name. It is in the folder of this file; no path. |
| `duration` | always | The recording's length in seconds, right to within 0.03 s. |
| `tempo` | always | `note` is the beat, `"4"` or (in 6/8) `"4."`; `bpm` is how many of them a minute, a whole number, from the median bar. The chart's `tempo` line. |
| `time` | always | The meter: `"4/4"`, `"3/4"`, `"2/4"`, `"6/8"`, or another number of quarters. One for the whole recording. |
| `key` | always | `"<tonic> major"` or `"<tonic> minor"`, the tonic a letter with `#` or `b`. It is what the chords are spelled by. |
| `strange` | when true | The reader's own model did not know the sound, and the bar lines are the rules' (the reader without `--ears` only). |
| `doubt` | with `sure` | A chord whose `sure` is below this is doubted. |
| `form` | always | The names of the sections in the order they are played. `"Pickup"` is first when the first bar is a pickup. |
| `bars` | always | **Every bar played, in the order it is played**: a bar of a section played three times is here three times, each with its own seconds. |

### Of a bar

| Item | | What it is |
|---|---|---|
| `bar` | always | Its number: 1, 2, 3 … in the order played; a pickup is 0. |
| `pickup` | a pickup | How many beats (of `tempo.note`) the short bar has. Only the first bar may have it. |
| `start`, `end` | always | Its first bar line and the next. A bar begins where the bar before it ends. |
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
  through its `form`, so a bar written once in a section played three times is three bars of
  `bars`. `tools/sidecar.py` counts the bars Lily# lays out of each chart (`lysc layout`) against
  the length of `bars`: the same for all 1388 charts `measure.py all` wrote on 2026-11-14, charts
  in sections and charts with a pickup among them.
- The chart is written in sections when a section is played twice or there is a pickup; otherwise
  as one row of bars with no `section` and no `form`. The file names sections either way.
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

Not checked yet (it is D4's first step): that `Beats` gives a `partial` bar an accented beat of
its own, and what bar number `lilysharp/placeAt` gives a pickup.

### After the chart is corrected

The file is the reading as it was written; nothing rewrites it.

- Changing a chord, a mark, the key or the title leaves the bars where they are: the file holds.
- **Adding or taking out a bar, or changing the form, moves every bar after it.** An editor can
  tell: Lily# then plays a different number of bars than `bars` has. It should say so and not play
  a bar that may be the wrong one. Lining the two up again is not built.
- `symbol`, `sure` and `other` are the reader's, not the corrected chart's.

## What is not in it

- The beats inside a bar (only its two lines).
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
