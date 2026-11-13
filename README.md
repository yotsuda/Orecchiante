# Dettato

Audio to score for [Lily#](https://yotsuda.github.io/LilySharp/): listens to a recording and
writes editable Lily# text (`.lys`), with the places it is unsure of marked for a look.

*Dettato* is Italian for dictation, the exercise of writing down music as it is heard.

**Status: a first reader, measured only on songs of its own making.** `tools/read_chords.py`
writes a chord chart from a recording; no real recording has been counted against a known
answer yet.

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

On three sets of 24 songs (2026-11-14). The reader's constants were chosen on `seed1`, and
`seed7` was looked at while choosing; `seed11` was made later and not chosen on:

| Nothing learnt | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Tempo (within 4%), songs | 22 of 24 | 21 of 24 | 22 of 24 |
| Meter, songs | 22 of 24 | 23 of 24 | 23 of 24 |
| Key, songs | 15 of 24 | 16 of 24 | 16 of 24 |
| Key signature, songs | 21 of 24 | 20 of 24 | 23 of 24 |
| Bar lines (within 70 ms), F | 95.8% | 82.3% | 90.5% |
| Chord root, by time | 87.5% | 82.8% | 90.1% |
| Root and family (major, minor, suspended, diminished) | 75.6% | 72.9% | 83.2% |
| Root and quality (sevenths told apart) | 61.6% | 53.6% | 72.0% |
| The bass note too | 58.3% | 47.9% | 68.7% |

The bar lines owe several points to one mend. Where the accompaniment moves to the off-beats,
the beat tracker follows it there, sliding half a beat over a few beats, and every bar line
after that was read half a beat off (16 of the 18 songs that lost bar lines lost them so). The
half beats are now counted through such a slide, so the lines after it stay in step.

Two smaller mends, found on the 200 songs kept for learning (tempo right in 183 of them before,
193 after; meter 185, then 195):

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
| Tempo (within 4%), songs | 23 of 24 | 22 of 24 | 23 of 24 |
| Meter, songs | 23 of 24 | 22 of 24 | 23 of 24 |
| Key, songs | 18 of 24 | 17 of 24 | 16 of 24 |
| Key signature, songs | 23 of 24 | 22 of 24 | 23 of 24 |
| Bar lines (within 70 ms), F | 97.7% | 89.5% | 96.8% |
| Chord root, by time | 95.2% | 94.5% | 96.8% |
| Root and family | 90.3% | 91.3% | 96.0% |
| Root and quality | 81.8% | 74.5% | 89.0% |
| The bass note too | 76.6% | 65.6% | 85.8% |

The same 72 songs played through a sound font the model never heard (TimGM6mb, 6 MB, against
MuseScore General's 215 MB) read as well, so it is not the one sound font the model leans on:

| Another sound font | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Chord root, nothing learnt → with the model | 85.9 → 95.4% | 87.3 → 95.7% | 87.8 → 96.2% |
| Root and family | 78.4 → 92.6% | 83.0 → 93.1% | 83.9 → 94.6% |
| Root and quality | 60.3 → 82.1% | 66.3 → 77.7% | 74.7 → 85.2% |
| Bar lines, F | 84.2 → 99.7% | 84.8 → 91.7% | 79.5 → 90.2% |
| Key signature, songs | 24 of 24 | 23 of 24 | 23 of 24 |

The model has still only heard this project's own songs (their patterns, no singing, no mixing),
so these numbers say that learning the sound and keeping the rules works, not how it reads a
recording. The
training songs and the model are not committed (`data/synth/seed100`, `models/`): the commands
above make them again.

Every chart either reader writes passes `lysc check`. What neither does yet:

- They keep a steady tempo (the beat tracker is held tight, which these songs reward and a
  recording that slows down will not).
- They take the bar to be as long as one chord lasts, so a song whose chords last two bars may
  be read at half its tempo (see below).
- The key is read from the chords found, and a key and its relative minor are made of the same
  chords: about a third of the keys are still wrong, nearly all of them the relative key, so the
  key signature is right far more often. The chords are not what holds it back: the same rule
  given the true chords gets 55 of the 72 keys, the reading 51. These songs' keys are also less
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
| Chords read | 2633 | 2575 |
| Wrong in root or family | 6.0% | 5.1% |
| Marked | 23.4% | 17.7% |
| Of the chords wrong in root or family, marked | 82.4% | 93.9% |
| Of the marked, wrong in root or family | 21.3% | 27.0% |
| Of the marked, wrong at all (sevenths too) | 38.2% | 41.3% |

So about one chord in five asks for a look, and five in six of the wrong roots and families are
among them. The chord offered in the mark is the true one for under half of the marked chords
that are wrong (28.5%, 44.1%): it is a hint, not a correction. The reader with nothing learnt
marks nothing.

### Chords that last two bars

`gen_chords.py --held 0.5` makes half of a song's sections hold each chord for two bars
(`data/synth/seed21`, 24 songs). Left alone, the reader with the model takes the tempo of 17 of
them right and reads the faster ones at half their tempo, with bars twice as long; the chords are
read as well as ever (root 98.1%). What is heard does not settle it: a fast song whose chords
last two bars and a slow one whose chords last one sound the same bar by bar. A price on long
bars was tried and thrown away (it mends these songs and breaks as many of the others).

So the reader can be told: `--tempo 150` (about right is enough) keeps only the bars of two,
three or four such beats.

| `seed21`, with the model | left alone | told the tempo |
|---|---|---|
| Tempo (within 4%), songs | 17 of 24 | 22 of 24 |
| Bar lines, F | 88.3% | 90.9% |
| Chord root, by time | 98.1% | 98.2% |

Told the true tempo, the three sets above read the same or a little better (`seed1`: 24 tempi
of 24, bar lines 99.7%; `seed7` and `seed11` unchanged).

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
