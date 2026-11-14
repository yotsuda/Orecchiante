# Dettato

Audio to score for [Lily#](https://yotsuda.github.io/LilySharp/): listens to a recording and
writes editable Lily# text (`.lys`), with the places it is unsure of marked for a look.

*Dettato* is Italian for dictation, the exercise of writing down music as it is heard.

**Status: a first reader, good on songs of its own making and not yet on recordings.**
`tools/read_chords.py` writes a chord chart from a recording. On 100 studio recordings of pop
songs it finds three chord roots in four and two bar lines in three, and marks most of what it
reads as doubtful (see [Recordings](#recordings)).

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
`beats seed7 s7-005`, `table seed1 rwc-dev --ears` (with models trained on recordings, below).
`all` takes about twelve minutes once the features are kept (the recordings below among them),
and last passed all 1292 charts it wrote through `lysc check` (2026-11-14).

## Recordings

The 100 songs of the RWC Popular Music Database (studio recordings with singing, a band and a
mix; CC BY-NC 4.0), with the beats, bar lines and chords annotated for them.
`tools/rwc_truth.py` writes the annotations as truths; where the recordings and the annotations
come from, and what is done to the chord labels, is in [docs/DATASETS.md](docs/DATASETS.md).
Neither is committed. The annotations name no key, so none is counted.

Nothing in either reader was chosen on these songs, and the model has heard none of them
(2026-11-14, the first time they were read):

| `rwc-p`, 100 songs | nothing learnt | with the model | with the model, told the tempo |
|---|---|---|---|
| Tempo (within 4%), songs | 62 | 60 | 64 |
| Meter, songs | 94 | 94 | 94 |
| Bar lines (within 70 ms), F | 65.0% | 66.1% | 67.6% |
| Chord root, by time | 70.5% | 74.7% | 74.8% |
| Root and family | 49.3% | 63.4% | 63.3% |
| Root and quality | 31.2% | 44.4% | 44.2% |
| The bass note too | 30.0% | 41.2% | 41.1% |

Against the songs made here (root 95 to 97%, bar lines 95 to 99%), that is the distance between
a sound font and a record. Where it goes wrong, with the model:

- **Half bars (29 songs).** The tempo is read twice too fast and every bar is cut in two: 88% of
  the true bar lines are found, but only 44% of the lines read are true ones. These are the slow
  songs (62 to 107 beats a minute, all but two), whose chords often change twice a bar: the
  reader takes the bar to be as long as a chord lasts. Telling it the tempo does not mend it,
  for `--tempo` allows bars of two beats.
- **Another tempo (11 songs).** Five are read at half the tempo, four at three quarters, two at
  about half as fast again.
- **The right tempo, the lines in the wrong place (18 songs lose a tenth of their lines or more,
  9 of them nearly all).** In seven the tracked beats sit on the off-beats, so every line is
  half a beat off; in five the beats are right and the bar begins on the wrong one, one or two
  beats off. The other six lose them here and there (one is in 5/4, which the reader cannot
  write). `measure.py barlines rwc-p` lists them.
- **Sounds the model does not know (66 songs).** It doubts more than half of the half beats, so
  the bars are left to the templates, and three chords in four are marked: the marks point at
  nothing. In the 34 songs it does not find strange, the root is right 81% of the time and the
  family 74%; in the 66, 71% and 57%.

  Of the 14537 chords read, 35.4% are wrong in root or family and 72.9% are marked; 92.9% of
  the wrong ones are among the marked, and 45.1% of the marked are wrong.

With the tempo right (60 songs), 77% of the bar lines are found and 76% of those read are true.

From here on the first fifty songs (`rwc-dev`) are the ones looked at to choose by, and the
last fifty (`rwc-test`) are counted and not looked into. As first read, with the model:
`rwc-dev` tempo 35 of 50, bar lines 68.4%, root 73.5%, root and family 61.7%; `rwc-test` tempo
25 of 50, bar lines 63.8%, root 75.9%, root and family 64.9%.

### What models trained on recordings do

Two published models, both small enough to run here in seconds a song on a CPU, counted the
same way (2026-11-14):

- [Beat This!](https://github.com/CPJKU/beat_this) (CPJKU, 2024; code and weights MIT) for the
  beats and the bar lines. RWC is among the recordings it learnt from, so each song is read by
  the one of its eight cross-validation models that never heard it (`tools/track_beats.py --rwc`).
- [BTC](https://github.com/jayg996/BTC-ISMIR19) (Park et al., 2019; MIT) for the chords, its
  170 labels folded into the reader's eight as the truth's are (`tools/btc_chords.py`). Its
  README names Isophonics, Robbie Williams and USPop2002 as what it learnt from, not RWC.

| `rwc-p`, 100 songs | this reader, with its model | its chords, true bar lines | its chords, Beat This!'s bar lines | BTC's chords, Beat This!'s bar lines | BTC's chords, true bar lines |
|---|---|---|---|---|---|
| Tempo (within 4%), songs | 60 | | 93 | 91 | |
| Meter, songs | 94 | | 98 | 96 | |
| Bar lines (within 70 ms), F | 66.1% | | 93.8% | 93.8% | |
| Chord root, by time | 74.7% | 77.3% | 76.7% | 82.6% | 83.1% |
| Root and family | 63.4% | 66.2% | 65.3% | 77.4% | 77.8% |
| Root and quality | 44.4% | 46.3% | 45.5% | 64.3% | 64.6% |
| The bass note too | 41.2% | 42.6% | 41.9% | 58.7% | 59.0% |

So the bar lines were the beat tracker's fault and are mended by a model that has heard
recordings (66% to 94%), and right bar lines do little for this reader's chords (three points): what
it hears is the limit, and a model that has heard recordings hears eight points more of the
roots and fourteen of the families. BTC's chords here are as it gives them, frame by frame,
with none of the reader's rules (a chord changes at a bar line, a doubt is marked); in the
third column the meter is read from Beat This!'s beats as told below. On
`rwc-test` alone the two together have bar lines 94.4%, root 83.3%, root and family 78.3%.

On the songs made here Beat This!'s bar lines are a little behind the reader's own (F 86 to 96%
against 89 to 99% on the five sets).

```
py -3.12 -m venv .venv-bt
.venv-bt/Scripts/python -m pip install torch torchaudio --index-url https://download.pytorch.org/whl/cpu
.venv-bt/Scripts/python -m pip install tqdm einops soxr rotary-embedding-torch soundfile beat-this librosa pandas pyyaml mir_eval pretty_midi pyrubberband
git clone --depth 1 https://github.com/CPJKU/beat_this_annotations scratch/real/beat_this_annotations
git clone --depth 1 https://github.com/jayg996/BTC-ISMIR19 scratch/btc
.venv-bt/Scripts/python tools/track_beats.py scratch/real/rwc-p --out scratch/real/beats-bt-folds --rwc scratch/real
.venv-bt/Scripts/python tools/btc_chords.py scratch/btc scratch/real/rwc-p --out scratch/real/chords-btc
.venv/Scripts/python tools/measure.py table rwc-p --lines truth
.venv/Scripts/python tools/measure.py table rwc-p --lines scratch/real/beats-bt-folds
.venv/Scripts/python tools/measure.py table rwc-p --chords scratch/real/chords-btc --lines scratch/real/beats-bt-folds
```

### Their ears, this reader's rules

`read_chords.py --ears` puts a chart together from what those two models hear, and writes the
`.lys` and the `.dettato.json` as before:

- the bars are Beat This!'s, and so is the meter: the beats it counts in a bar, where two beats
  are the two dotted quarters of 6/8 when the sound between them falls in threes, and the halves
  of a bar of four when they are slower than 80 a minute;
- each bar is cut into half beats, and BTC's probabilities of its 170 chords (`btc_chords.py
  --probs`: about eleven frames a second) are folded into the reader's 96 and averaged over each;
- the reader's search then names the chords: a change costs a little at a bar line or its half
  and more elsewhere. BTC has already smoothed what it says, so the prices are a quarter of what
  the reader's own model needs; they were chosen on `rwc-dev`.

```
.venv-bt/Scripts/python tools/track_beats.py song.wav --out ears
.venv-bt/Scripts/python tools/btc_chords.py scratch/btc song.wav --out ears --probs
.venv/Scripts/python tools/read_chords.py song.wav --out charts --ears ears
```

Counted on 2026-11-14 (`measure.py table rwc-dev rwc-test rwc-p --ears`; the beats of RWC are
the fold models', as above):

| | `rwc-dev` (chosen on) | `rwc-test` (counted only) | `rwc-p`, all 100 | BTC as it is, `rwc-p` |
|---|---|---|---|---|
| Tempo (within 4%), songs | 46 of 50 | 47 of 50 | 93 | 91 |
| Meter, songs | 49 of 50 | 49 of 50 | 98 | 96 |
| Bar lines (within 70 ms), F | 93.3% | 94.4% | 93.8% | 93.8% |
| Chord root, by time | 84.8% | 86.4% | 85.6% | 82.6% |
| Root and family | 79.1% | 81.3% | 80.2% | 77.4% |
| Root and quality | 65.9% | 67.6% | 66.8% | 64.3% |
| The bass note too | 61.6% | 62.6% | 62.1% | 58.7% |

Three points of root and of family come from the rules (a chord lasts whole half beats and
changes where bars say it may). The bass note is still this reader's own hearing, as is the
doubt it marks, which is not yet calibrated for this model: about one chord in four is marked.

On the songs made here (the beats are those of Beat This!'s model for use, `final0`):

| `--ears` | `seed1` | `seed7` | `seed11` | `seed21` | `seed31` |
|---|---|---|---|---|---|
| Tempo, songs of 24 | 22 | 20 | 21 | 18 | 23 |
| Meter, songs of 24 | 22 | 22 | 22 | 21 | 23 |
| Bar lines, F | 96.4% | 90.6% | 93.7% | 86.2% | 92.4% |
| Chord root, by time | 94.5% | 94.3% | 96.8% | 97.2% | 97.5% |
| Root and family | 91.2% | 91.2% | 94.7% | 94.6% | 93.4% |
| Root and quality | 77.1% | 73.9% | 82.3% | 79.9% | 79.7% |

Beside the reader with its own model (below), the roots and families are within a point and
the sevenths (root and quality) are behind by up to eight: the reader's model was made on this
very sound. The bar lines are behind on four of the five sets (90.6 to 93.7% against 97.2 to
99.1% on `seed7`, `seed11` and `seed31`), and that is the beat tracker: where it follows a
song at half its tempo it counts four slow beats to two true bars, and some waltzes it counts
in four or in two. The rule for two slow beats was chosen looking at `seed7` and `seed11`.


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
