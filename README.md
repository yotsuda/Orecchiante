# Dettato

Audio to score for [Lily#](https://yotsuda.github.io/LilySharp/): listens to a recording and
writes editable Lily# text (`.lys`), with the places it is unsure of marked for a look.

*Dettato* is Italian for dictation, the exercise of writing down music as it is heard.

**Status: a first reader, measured only on songs of its own making.** `tools/read_chords.py`
writes a chord chart from a recording; no real recording has been counted against a known
answer yet.

Where the work is going, and where it stands: [docs/ROADMAP.md](docs/ROADMAP.md) and
[docs/HANDOFF.md](docs/HANDOFF.md) (both in Japanese).

## First aim: a chord chart

From a recording of a song, read three things and write them as a Lily# `chords` row:

- the tempo,
- the bar lines (where each bar begins, and the time signature),
- the chord of each bar.

A chord the reader doubts is marked on its symbol (`@todo`), as
[Calamo](https://github.com/yotsuda/Calamo) marks a note it doubts. Beside the `.lys`, a file holds
where each bar is in the recording, so an editor can play that bar of the original while it is
corrected.

Notes, melody, bass and drums come after this, if at all.

## Songs with known answers

`tools/gen_chords.py` writes songs from a seed and has Lily# play them, so that a reading can be
counted against the truth:

```
py tools/gen_chords.py --seed 1 --count 24 --out data/synth/seed1 --soundfont MuseScore_General.sf2
```

For each song it writes three files:

- `s1-001.lys`: a chord row (the answer) and a small band that plays it: a chord instrument, a
  bass, sometimes drums and a melody. The key, the meter (4/4, 3/4, 6/8), the tempo, the sounds
  and the playing patterns are drawn from the seed.
- `s1-001-band.wav`: the band as `lysc wav` renders it through the sound font. The chord row is
  not played. The music starts at 0 s; two seconds of ring-out follow the last bar.
- `s1-001.truth.json`: the tempo, the meter, the key, and every bar and chord with its start and
  end in seconds.

The `.lys` and the truth are committed; the recordings are not (the same seed and sound font make
them again). `--lysc` or `DETTATO_LYSC` names Lily#'s compiler. `lysc wav` reads SoundFont 2
(`.sf2`), not `.sf3`.

These songs are simpler than recordings: a constant tempo, no pushed chords, no singing, no
mixing. They say whether a reader works at all; how well it works is measured on recordings.

## The line to beat

`tools/read_chords.py` is a reader with nothing learnt in it: librosa's beat tracker and chroma,
chord templates, and one search for the bar lines and the chords together (a chord changes at a
bar line for nothing, in the middle of a bar at a price). `tools/score_chords.py` counts a
reading against the truth:

```
.venv/Scripts/python tools/read_chords.py data/synth/seed1 --out scratch/read/seed1
py tools/score_chords.py data/synth/seed1 scratch/read/seed1
```

### Measuring

Every number below is made by `tools/measure.py`, which reads and scores in one process and
keeps each recording's features between runs (`scratch/feat-h31/`, seconds a song the first time):

```
$env:DETTATO_LYSC = "<LilySharp>/LilySharp.Cli/bin/Release/net10.0/win-x64/lysc.exe"
$env:DETTATO_SOUNDFONT_ALT = "<a second .sf2>"      # the tables of "another sound font"
$env:DETTATO_SAMPLES = "<LilySharp>/samples"        # the table of "songs it was not made on"
.venv/Scripts/python tools/measure.py make          # renders the sets that are missing (13 sets, 580 songs)
.venv/Scripts/python tools/train_chords.py data/synth/seed100 --features scratch/feat-h31 --out models/chords.joblib
.venv/Scripts/python tools/measure.py all           # every table here, and every chart through lysc check
```

`measure.py sets` lists the sets and what each is for. One table, or one question, at a time:
`table seed1 seed7 -v`, `table seed21 --hint`, `table seed1 --rules` (nothing learnt),
`sweep seed100 TIGHTNESS=300,100`, `doubt seed1 seed7 seed11`, `barlines seed1`, `keys seed1`,
`beats seed7 s7-005`. `all` takes about three minutes once the features are kept, and last
passed all 772 charts it wrote through `lysc check` (2026-11-14).

On three sets of 24 songs (2026-11-14). The reader's constants were chosen on `seed1`, and
`seed7` was looked at while choosing; `seed11` was made later and not chosen on:

| Nothing learnt | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Tempo (within 4%), songs | 22 of 24 | 22 of 24 | 22 of 24 |
| Meter, songs | 22 of 24 | 23 of 24 | 23 of 24 |
| Key, songs | 15 of 24 | 15 of 24 | 16 of 24 |
| Key signature, songs | 21 of 24 | 20 of 24 | 24 of 24 |
| Bar lines (within 70 ms), F | 92.9% | 90.0% | 97.4% |
| Chord root, by time | 87.1% | 83.2% | 90.3% |
| Root and family (major, minor, suspended, diminished) | 75.5% | 73.3% | 83.4% |
| Root and quality (sevenths told apart) | 61.4% | 54.0% | 72.4% |
| The bass note too | 58.1% | 48.6% | 69.8% |

The bar lines owe several points to one mend. Where the accompaniment moves to the off-beats,
the beat tracker follows it there, sliding half a beat over a few beats, and every bar line
after that was read half a beat off (16 of the 18 songs that lost bar lines lost them so). The
half beats are now counted through such a slide, so the lines after it stay in step.

Two smaller mends, found on the 200 songs kept for learning (tempo right in 183 of them before,
193 after; meter 185, then 195, counted when they were made. With the constants of today the
reader with nothing learnt has 178 and 185 there, the reader with the model 192 and 193):

- The tracker sometimes takes three notes for two (a fast song at two thirds of its tempo). The
  beat half as fast again and the beat a third slower are tried too, and the one on whose half
  beats most of the sound begins is kept.
- 3/4 and 6/8 are told apart by whether a bar's thirds or its halves are alike, not by how loud
  the middle of the bar is (a chord played off the beat made a 3/4 look like 6/8).

## One thing learnt: what a chord sounds like

`tools/train_chords.py` learns one small thing from songs with known answers: from the chroma of
one half beat to the chord sounding then (a network of one hidden layer, each half beat learnt in
all twelve keys). The bar lines, when a chord may change, the meter and the key stay the rules of
`read_chords.py`, which takes the model with `--model`:

```
py tools/gen_chords.py --seed 100 --count 200 --out data/synth/seed100 --soundfont MuseScore_General.sf2
.venv/Scripts/python tools/train_chords.py data/synth/seed100 --out models/chords.joblib
.venv/Scripts/python tools/read_chords.py data/synth/seed11 --out scratch/read/seed11 --model models/chords.joblib
```

Learnt from the 200 songs of seed 100 (none of them in the three sets; its prices chosen on `seed1`):

| With the model | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Tempo (within 4%), songs | 23 of 24 | 23 of 24 | 23 of 24 |
| Meter, songs | 23 of 24 | 23 of 24 | 23 of 24 |
| Key, songs | 18 of 24 | 19 of 24 | 16 of 24 |
| Key signature, songs | 23 of 24 | 23 of 24 | 23 of 24 |
| Bar lines (within 70 ms), F | 94.7% | 97.4% | 99.1% |
| Chord root, by time | 95.1% | 95.5% | 97.4% |
| Root and family | 90.6% | 92.6% | 96.6% |
| Root and quality | 83.1% | 73.8% | 89.9% |
| The bass note too | 77.8% | 65.5% | 86.6% |

The same 72 songs played through a sound font the model never heard (TimGM6mb, 6 MB, against
MuseScore General's 215 MB) read as well, so it is not the one sound font the model leans on:

| Another sound font | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Chord root, nothing learnt → with the model | 85.9 → 95.5% | 87.7 → 97.1% | 87.7 → 96.3% |
| Root and family | 78.4 → 92.8% | 83.5 → 94.3% | 83.9 → 94.9% |
| Root and quality | 60.4 → 80.7% | 66.6 → 78.8% | 74.7 → 84.8% |
| Bar lines, F | 84.2 → 99.7% | 93.3 → 99.6% | 79.5 → 90.2% |
| Key signature, songs | 24 of 24 | 23 of 24 | 23 of 24 |

The model has still only heard this project's own songs (their patterns, no singing, no mixing),
so these numbers say that learning the sound and keeping the rules works, not how it reads a
recording. The
training songs and the model are not committed (`data/synth/seed100`, `models/`): the commands
above make them again.

Every chart either reader writes passes `lysc check`. What neither does yet:

- They follow a tempo that wanders a little, not one that moves far (see below), and not a
  song that stops and starts.
- They take the bar to be as long as one chord lasts, so a song whose chords last two bars may
  be read at half its tempo (see below).
- The key is read from the chords found, and a key and its relative minor are made of the same
  chords: about a third of the keys are still wrong, nearly all of them the relative key, so the
  key signature is right far more often. The chords are not what holds it back: the same rule
  given the true chords gets 55 of the 72 keys, the reading 53. These songs' keys are also less
  sure than a real song's (a major song here may begin and end on its sixth degree).

### The chords it doubts

With the model, a chord is marked when the model is not sure of its root or of whether it is
major or minor (a seventh heard or not is a smaller doubt, and marking it would mark one chord
in four). The mark is Lily#'s `@todo`, with the chord the model would have said next:

```
chords harmony {
  Dm7/A | G7 | C | F |
  Bm7@todo("or Bm7(b5)?") | E7/B | Am7 | Am |
}
```

How sure is sure was chosen on 60 songs outside the three sets, and counted on the three sets:

| The marks, `seed1`, `seed7` and `seed11` together | this sound font | another |
|---|---|---|
| Chords read | 2617 | 2578 |
| Wrong in root or family | 5.8% | 4.9% |
| Marked | 22.9% | 17.2% |
| Of the chords wrong in root or family, marked | 78.9% | 87.3% |
| Of the marked, wrong in root or family | 20.0% | 24.8% |
| Of the marked, wrong at all (sevenths too) | 36.1% | 37.2% |

So about one chord in five asks for a look, and four in five of the wrong roots and families are
among them. The chord offered in the mark is the true one for under half of the marked chords
that are wrong (27.3%, 38.8%): it is a hint, not a correction. The reader with nothing learnt
marks nothing.

### Chords that last two bars

`gen_chords.py --held 0.5` makes half of a song's sections hold each chord for two bars
(`data/synth/seed21`, 24 songs). Left alone, the reader with the model takes the tempo of 16 of
them right and reads the faster ones at half their tempo, with bars twice as long; the chords are
read as well as ever (root 97.9%). What is heard does not settle it: a fast song whose chords
last two bars and a slow one whose chords last one sound the same bar by bar. A price on long
bars was tried and thrown away (it mends these songs and breaks as many of the others).

So the reader can be told: `--tempo 150` (about right is enough) keeps only the bars of two,
three or four such beats.

| `seed21`, with the model | left alone | told the tempo |
|---|---|---|
| Tempo (within 4%), songs | 16 of 24 | 22 of 24 |
| Bar lines, F | 89.2% | 96.5% |
| Chord root, by time | 97.9% | 97.9% |

Told the true tempo, the three sets above read the same or a little better (`seed1`: 24 tempi
of 24, bar lines 96.7%; `seed7` and `seed11` unchanged).

### Songs it was not made on

`tools/sample_songs.py` takes the four of Lily#'s own sample songs that have a chord row, plays
each without it, and counts the reading against the row. None is written the way
`gen_chords.py` writes: two are a melody alone, one a melody and a bass, one a voice and a
piano. They are short (8 to 16 bars, 48 bars in all).

| | what plays | nothing learnt | with the model |
|---|---|---|---|
| Amazing Grace | a melody | tempo and all 15 bar lines right; root 20% | the same; root 39% |
| Greensleeves | a melody | tempo right, 15 of 16 bar lines; root 12% | the same; root 9% |
| Morning Light | a melody and a bass | tempo and all 8 bar lines right; root 93%, family 62% | the same; root 93%, family 75% |
| Harbour Lights | a voice and a piano | tempo and all 9 bar lines right; root 83%, family 83% | the same; root 99%, family 99% |

- A melody alone has no chords in it to hear: naming the chords a tune implies is another
  task (harmonising), which neither reader attempts. Their tempo and bar lines are still found
  by the reader with nothing learnt.
- With something under the melody the roots are mostly found (80 to 93%), by both readers.
- Left to find the bars itself, the model did worse than the rules here (two songs at twice
  their tempo, Harbour Lights in 6/8 with no bar line right): its opinion changes from half
  beat to half beat on sounds it does not know, and the bar lines followed it.
- But it says so itself: it doubts 19 of 19, 19 of 19, 8 of 8 and 9 of 13 of the chords it reads
  in these four songs (2 of 29 in a song of `seed11`). So when the model doubts more than half
  of the half beats, the bars are the ones the templates find and the model only names the
  chords inside them; that is the column above, and the reader prints "this is not music the
  model knows". Harbour Lights then comes out all but right, each chord still marked:

  ```
  Eb | Cm7@todo("or Eb?") | Abmaj7@todo("or Fm7?") Bb7@todo("or Eb?") | Eb |
  ```

  The songs of its own making read as before, but for one of `seed21`.

### A tempo that moves

`gen_chords.py --drift 0.02` lets the tempo step up to two in a hundred from each bar to the
next, within six in a hundred of where it began (`data/synth/seed31`, 24 songs; the same songs
were also made with a tempo that does not move, and with steps of five in a hundred).

| `seed31`, with the model | still | steps of 2% | steps of 5% |
|---|---|---|---|
| Tempo (within 4%), songs | 24 of 24 | 24 of 24 | 20 of 24 |
| Bar lines, F | 98.5% | 97.2% | 80.7% |
| Chord root, by time | 97.4% | 97.5% | 94.7% |

A tempo that wanders as a band's does costs about a point of bar lines. One that moves by a
seventh is where the reader begins to lose them; the chords hold longer than the bar lines.

Measuring this found a constant that had outlived its reason. The beat tracker was held three
times as tight as librosa holds it, chosen on `seed1` before the half-beat slide was mended.
At librosa's own value the bar lines of nearly every set rose (`seed7` 89.0% to 97.4%, the 200
songs kept for learning 94.0% to 96.7%, the moving tempo of 5% 67.7% to 80.7%); `seed1`
alone fell, 97.7% to 94.7%.

They need `numpy`, `scipy`, `librosa`, `soundfile` and `scikit-learn` (in `.venv`, not committed).
They are written to be measured and replaced, not shipped.

## Siblings

- [Lily#](https://github.com/yotsuda/LilySharp): the notation language, engraver and editor.
- [Calamo](https://github.com/yotsuda/Calamo): reads a page image into `.lys`.
- [Cantoria](https://github.com/yotsuda/Cantoria): sings a part's notes and words.

Dettato is a program of its own, as they are. It writes `.lys` text and calls Lily#'s `lysc` to
check it; Lily# does not depend on it.

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

No recording and no model that is not the project's own is committed here.
