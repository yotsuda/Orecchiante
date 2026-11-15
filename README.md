# Dettato

Audio to score for [Lily#](https://yotsuda.github.io/LilySharp/): listens to a recording and
writes editable Lily# text (`.lys`), with the places it is unsure of marked for a look.

*Dettato* is Italian for dictation, the exercise of writing down music as it is heard.

**Status: a chord chart of a recording, to be mended by hand; not yet a tool.**
`tools/read_chords.py --ears` puts a chart together from what two published models hear (the
beats and bar lines of Beat This!, the chords of crema). On 100 studio recordings of pop songs
it has the tempo of 93, 93% of the bar lines, the chord's root right 87% of the time and its
root and family 82%, and it marks 28% of the chords for a look, among them three in four of
the wrong ones (see [Recordings](#recordings)). A song of three and a half minutes takes 18
seconds in three Python environments. The same reader is one program in .NET, `dettato read`
(`src/Dettato`), which writes the same charts letter for letter and takes 5.5 seconds
([In .NET: the rules](#in-net-the-rules)); it reads a `.wav` and is not yet in an editor.
The reader with nothing but its own hearing, which the work began with, is kept for comparison.

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
corrected: `<name>.dettato.json`, every bar played in the order Lily# plays it, with its seconds,
its beats and its chords, and how many cents the recording is off A = 440 Hz (an old tape run
fast or slow). [docs/SIDECAR.md](docs/SIDECAR.md) is its specification, and
`py tools/sidecar.py <folder>` checks files against it and against the charts beside them (Lily#
must play as many bars as the file has).

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

`--pickup 0.5 --silence 3` makes songs that do not begin at 0 s on a bar line: every recording
begins with up to three seconds of silence, and half the songs with a pickup, one to all but one
of a bar's beats before the first bar line. The band plays the dominant there or, where there is
a melody, the melody plays alone as often (nothing to name). In the truth a pickup is a bar
numbered 0 with `"pickup": <beats>`; in the `.lys` it is a section of its own with `partial`.
(`--held` and `--drift` are told of below.)

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
keeps each recording's features between runs (`scratch/feat-h31/`, and `scratch/feat-h0/` for
`--ears`; seconds a song the first time):

```
$env:DETTATO_LYSC = "<LilySharp>/LilySharp.Cli/bin/Release/net10.0/win-x64/lysc.exe"
$env:DETTATO_SOUNDFONT_ALT = "<a second .sf2>"      # the tables of "another sound font"
$env:DETTATO_SAMPLES = "<LilySharp>/samples"        # the table of "songs it was not made on"
.venv/Scripts/python tools/measure.py make          # renders the sets that are missing (15 sets, 628 songs)
.venv/Scripts/python tools/train_chords.py data/synth/seed100 --features scratch/feat-h31 --out models/chords.joblib
.venv/Scripts/python tools/measure.py all           # every table here, and every chart through lysc check
```

`measure.py sets` lists the sets and what each is for. One table, or one question, at a time:
`table seed1 seed7 -v`, `table seed21 --hint`, `table seed1 --rules` (nothing learnt),
`sweep seed100 TIGHTNESS=300,100`, `doubt seed1 seed7 seed11`, `barlines seed1`, `keys seed1`,
`beats seed7 s7-005`, `table seed1 rwc-dev --ears`, `doubt rwc-dev --ears`, `form rwc-dev --ears` and
`head seed41 rwc-dev --ears` (with models trained on recordings, below).
`all` takes about fifteen minutes once the features are kept (the recordings below among them),
and last passed all 1388 charts it wrote through `lysc check` (2026-11-14); the `.dettato.json`
beside each of the 1388 was as [docs/SIDECAR.md](docs/SIDECAR.md) says, with as many bars as
Lily# plays of the chart.

## Recordings

The 100 songs of the RWC Popular Music Database (studio recordings with singing, a band and a
mix; CC BY-NC 4.0), with the beats, bar lines and chords annotated for them.
`tools/rwc_truth.py` writes the annotations as truths; where the recordings and the annotations
come from, and what is done to the chord labels, is in [docs/DATASETS.md](docs/DATASETS.md).
Neither is committed. The annotations name no key, so none is counted.

Nothing in either reader was chosen on these songs, and the model has heard none of them
(2026-11-14, the first time they were read; counted again the same day with the sections the
readers now write, whose one constant was chosen on the first fifty of these songs: no number
moved by more than a tenth of a point):

| `rwc-p`, 100 songs | nothing learnt | with the model | with the model, told the tempo |
|---|---|---|---|
| Tempo (within 4%), songs | 62 | 60 | 64 |
| Meter, songs | 94 | 94 | 94 |
| Bar lines (within 70 ms), F | 64.9% | 66.1% | 67.6% |
| Chord root, by time | 70.6% | 74.9% | 75.0% |
| Root and family | 49.4% | 63.5% | 63.5% |
| Root and quality | 31.3% | 44.5% | 44.3% |
| The bass note too | 30.1% | 41.3% | 41.3% |

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
| Bar lines (within 70 ms), F | 66.1% | | 93.4% | 93.8% | |
| Chord root, by time | 74.9% | 77.1% | 76.8% | 82.6% | 83.1% |
| Root and family | 63.5% | 66.0% | 65.4% | 77.4% | 77.8% |
| Root and quality | 44.5% | 46.2% | 45.6% | 64.3% | 64.6% |
| The bass note too | 41.3% | 42.6% | 42.1% | 58.7% | 59.0% |

So the bar lines were the beat tracker's fault and are mended by a model that has heard
recordings (66% to 94%), and right bar lines do little for this reader's chords (three points): what
it hears is the limit, and a model that has heard recordings hears eight points more of the
roots and fourteen of the families. BTC's chords here are as it gives them, frame by frame,
with none of the reader's rules (a chord changes at a bar line, a doubt is marked); in the
third column the meter is read from Beat This!'s beats as told below. On
`rwc-test` alone the two together have bar lines 94.4%, root 83.3%, root and family 78.3%.

On the songs made here Beat This!'s bar lines are a little behind the reader's own (F 86 to 97%
against 90 to 99% on the five sets).

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

### Which model hears the chords

BTC's repository is MIT and has the weights in it, but nowhere do its authors say what the
weights may be used for, and they learnt from records bought in shops. So four models were set
side by side (2026-11-14), each read through the same rules (the next section; the prices are the
ones chosen for BTC, and for crema other prices are within 0.3 of a point on `rwc-dev`):

| | [crema](https://github.com/bmcfee/crema) 0.2.0 | [BTC](https://github.com/jayg996/BTC-ISMIR19) | [ChordMini](https://github.com/ptnghia-j/ChordMini), BTC | ChordMini, 2E1D | crema and BTC, averaged |
|---|---|---|---|---|---|
| Licence of the code | BSD 2-Clause | MIT | MIT | MIT | |
| The weights | in the package; their author counts them as part of the software ([issue 42](https://github.com/bmcfee/crema/issues/42)) | in the repository; nothing said | in the repository; nothing said | the same | |
| Learnt from | not written down | Isophonics, Robbie Williams, USPop2002 (its README) | BTC's labels of 1,300 hours, then 420 songs of Isophonics, Billboard, RWC Pop and USPop (their paper) | the same | |
| Parameters; the weights' file | 0.53 M; 6.4 MB (ONNX: 2.2 MB) | 3.0 M; 12 MB | 3.0 M; 36 MB | 2.3 M; 28 MB | |
| A song on this CPU: the spectrum, the model | 0.5 s, 0.25 s | 1 to 3 s together | 0.3 s, 0.2 s | 0.3 s, 0.07 s | |
| `rwc-dev`: root, root and family | 86.4%, 82.3% | 85.1%, 79.4% | 86.5%, 82.6% | 84.5%, 80.2% | 87.3%, 82.9% |
| `rwc-test`: root, root and family | 87.3%, 82.1% | 86.7%, 81.6% | 87.5%, 83.0% | 86.0%, 81.7% | 88.7%, 83.8% |
| `rwc-test`: root and quality | 69.1% | 67.9% | 70.7% | 68.9% | 71.2% |
| The five sets made here: root | 93.1 to 96.0% | 94.1 to 97.6% | 93.9 to 97.5% | 93.5 to 97.4% | 94.6 to 97.1% |
| The same: root and family | 88.4 to 93.1% | 91.0 to 94.8% | 90.7 to 95.4% | 88.9 to 93.3% | 91.2 to 95.1% |
| The same: root and quality | 64.1 to 74.6% | 73.9 to 82.4% | 75.5 to 83.5% | 70.8 to 81.8% | 71.2 to 80.6% |

**crema is the one the reader uses.** It is the only one whose author has said the weights are
under the licence of the code; it is the smallest; and on recordings it reads the roots and the
families as well as any of the others. What it gives up is the sevenths on the songs made here
(three to ten points behind BTC), and up to two and a half points of their roots. The numbers on RWC are not all
alike in worth: ChordMini's models learnt from RWC Pop songs (which ones is not published), and
what crema learnt from is not written down, so RWC may be among it; only BTC is known not to
have heard these songs, and only the songs made here are new to all four.

Two ears are better than one, and that is kept for the day BTC's authors say what its weights
may be used for (they have not been asked). Averaging crema's probabilities with BTC's reads
`rwc-test` at 88.4% and 83.5%. And a second ear tells where the first is wrong: weighing in the
second model's probability of the chord read, the least sure 30% of `rwc-test` hold 77.4% of the
wrong chords instead of 75.2% (crema first, BTC second; 79.0% instead of 74.2% the other way
round; weights fitted on `rwc-dev`; counted before the reader wrote sections, below), and a
third ear adds nothing to two.

Not measured: the [large-vocabulary model](https://github.com/music-x-lab/ISMIR2019-Large-Vocabulary-Chord-Recognition)
of Jiang et al. (MIT; nothing said of the weights) and omnizart's (MIT; the same), which give
labels and not the probabilities the reader needs; madmom's, whose models are CC BY-NC-SA.

```
py -3.12 -m venv .venv-crema
.venv-crema/Scripts/python -m pip install crema tf-keras "setuptools<81"
.venv-crema/Scripts/python tools/crema_chords.py scratch/real/rwc-p --out scratch/real/chords-crema
git clone --depth 1 https://github.com/ptnghia-j/ChordMini scratch/chordmini
.venv-bt/Scripts/python tools/chordmini_chords.py scratch/chordmini scratch/real/rwc-p --model btc --out scratch/real/chords-cmbtc
.venv/Scripts/python tools/measure.py table rwc-dev rwc-test --ears          # crema
.venv/Scripts/python tools/measure.py table rwc-dev rwc-test --ears cmbtc    # scratch/real/chords-cmbtc
```

crema 0.2.0 was written for Keras 2 and `pkg_resources`: `crema_chords.py` runs it on `tf-keras`
and takes only the spectrum from its own pipeline. The sets made here go the same way
(`data/synth/seed1 --out scratch/chords-crema/seed1`).

### Their ears, this reader's rules

`read_chords.py --ears` puts a chart together from what two such models hear (Beat This! and
crema; the same words hold for BTC, on which the prices and the marks were chosen), and writes the
`.lys` and the `.dettato.json` as before:

- the bars are Beat This!'s, and so is the meter: the beats it counts in a bar, where two beats
  are the two dotted quarters of 6/8 when the sound between them falls in threes, and the halves
  of a bar of four when they are slower than 80 a minute;
- each bar is cut into half beats, and the chord model's probabilities of its 170 chords
  (`crema_chords.py`, or `btc_chords.py --probs`: about eleven frames a second) are folded into
  the reader's 96 and averaged over each;
- the reader's search then names the chords: a change costs a little at a bar line or its half
  and more elsewhere. Such a model has already smoothed what it says, so the prices are a quarter
  of what the reader's own model needs; they were chosen on `rwc-dev`, with BTC.

```
.venv-bt/Scripts/python tools/track_beats.py song.wav --out ears
.venv-crema/Scripts/python tools/crema_chords.py song.wav --out ears
.venv/Scripts/python tools/read_chords.py song.wav --out charts --ears ears
```

Counted on 2026-11-14 (`measure.py table rwc-dev rwc-test rwc-p --ears`; the beats of RWC are
the fold models', as above; the first three columns again after "Two half bars are one bar" below):

| | `rwc-dev` (chosen on) | `rwc-test` (counted only) | `rwc-p`, all 100 | crema as it is, `rwc-p` | with BTC's chords, `rwc-p` |
|---|---|---|---|---|---|
| Tempo (within 4%), songs | 46 of 50 | 47 of 50 | 93 | 91 | 93 |
| Meter, songs | 49 of 50 | 49 of 50 | 98 | 96 | 98 |
| Bar lines (within 70 ms), F | 93.6% | 94.9% | 94.2% | 93.8% | 93.4% |
| Chord root, by time | 86.4% | 87.3% | 86.9% | 84.4% | 85.9% |
| Root and family | 82.3% | 82.2% | 82.2% | 79.8% | 80.6% |
| Root and quality | 68.0% | 69.2% | 68.6% | 66.5% | 67.1% |
| The bass note too | 64.4% | 64.9% | 64.7% | 61.4% | 62.8% |

Two points of root and of family come from the rules (a chord lasts whole half beats and
changes where bars say it may; with BTC's chords, three). The bass note is still this reader's
own hearing, and with `--ears` it hears the recording as it is: taking the harmonic part out
first, which its own chords need, was nine tenths of the reader's time and made the bass notes a
point worse (63.3%, 64.3% and 63.8% in the first three columns; the roots and families within a
tenth; "Without torch and TensorFlow" below). (`rwc-dev` is where the prices were chosen, with
BTC; nothing was chosen again for crema. "crema as it is" was not counted again: its bass notes
are the reader's of before.)

**The marks.** Such a model is sure of itself more often than it is right, so how likely a
chord's root and family are right is weighed from four things (`SURE_OF` in `read_chords.py`,
five numbers chosen on `rwc-dev`, with BTC): the model's mean probability of the chord over its
half beats; the same in its last half beat, which weighs the other way (what the model says there
is already the next chord's); whether this reader's own templates name the same root and family;
and the first of these over the whole song. A chord below 0.84 is marked. With crema
(`measure.py doubt rwc-dev --ears`, counted 2026-11-14, by chords; the five numbers and the line
are BTC's still: fitted again on crema they catch a point more of `rwc-dev` and no more of
`rwc-test`):

| The marks, `--ears` | `rwc-dev` | `rwc-test` (counted only) | `rwc-p`, all 100 | with BTC's chords: `rwc-dev` (chosen on), `rwc-test` |
|---|---|---|---|---|
| Chords read | 6933 | 6718 | 13651 | 7274, 7042 |
| Wrong in root or family | 14.8% | 15.9% | 15.3% | 19.0%, 18.0% |
| Marked | 30.2% | 25.7% | 28.0% | 30.8%, 24.1% |
| Of the chords wrong in root or family, marked | 77.8% | 70.3% | 73.9% | 79.5%, 63.1% |
| Of the marked, wrong in root or family | 38.1% | 43.5% | 40.5% | 49.0%, 47.2% |
| Marked and wrong so: the chord offered has the true root and family | 58.2% | 52.9% | 55.6% | 42.1%, 45.2% |

(The marks of this table are up to a point more than the line alone gives: a chord two playings
of a section disagree on is marked too, see "What is said again". And they are up to a point more
than they were while the reader's own templates, one of the four things, heard the harmonic part
alone: 29.3% of `rwc-dev` marked then and 76.6% of its wrong chords caught, 25.7% and 70.1% of
`rwc-test`. Nothing was chosen again for it.)

The aim was eight wrong chords in ten among three marked in ten, and it is not met. Marking the
least sure 30% of each set, whatever line that takes, catches 77.8% and 75.2% with crema (79.7%
and 74.0% with BTC; by BTC's own probability alone 75.2% and 71.6%; to catch 80% of `rwc-test`
with BTC, 38% have to be marked; all counted before the reader wrote sections). More measures do not help (tried with BTC): twenty-seven of
them weighed together (the bass, the chords before and after, how often the song has the chord,
whether the key has it) catch 79.4% of `rwc-dev` at 30%, songs held out in tens. The wrong
chords BTC is sure of are mostly ones no probability points at: a major chord read minor on the
same root (one in four of them), and a suspended chord read major (one in ten). What does help
is a second model (the section above). On the songs made here the same line marks 17% of the
chords with crema and catches 70% (`seed1`, `seed7`, `seed11`) and 75% (`seed21`, `seed31`) of
the wrong ones (with BTC: 7 to 9% marked, 44% and 61% caught).

On the songs made here (the beats are those of Beat This!'s model for use, `final0`):

| `--ears` | `seed1` | `seed7` | `seed11` | `seed21` | `seed31` |
|---|---|---|---|---|---|
| Tempo, songs of 24 | 22 | 20 | 21 | 18 | 23 |
| Meter, songs of 24 | 22 | 22 | 22 | 21 | 23 |
| Bar lines, F | 96.6% | 90.7% | 94.0% | 86.1% | 92.6% |
| Chord root, by time | 95.1% | 93.1% | 94.5% | 94.7% | 96.0% |
| Root and family | 91.6% | 88.4% | 92.4% | 92.9% | 93.1% |
| Root and quality | 74.5% | 64.1% | 74.6% | 72.3% | 74.1% |

Beside the reader with its own model (below), the roots are behind by up to three
points and the sevenths (root and quality) by far more (with BTC's chords: the roots and
families within a point, the sevenths behind by up to eight): the reader's model was made on
this very sound. The bar lines are behind on four of the five sets (90.7 to 94.0% against 97.6 to
99.1% on `seed7`, `seed11` and `seed31`), and that is the beat tracker: where it follows a
song at half its tempo it counts four slow beats to two true bars, and some waltzes it counts
in four or in two. The rule for two slow beats was chosen looking at `seed7` and `seed11`.

### What is said again

A song says most of its bars more than once, and a chart that writes them once is shorter and
is mended once. Both readers now write a run of bars that comes again as a Lily# `section`, and
the order the sections are played in as the `form`:

```
section A {
  chords harmony {
    Abmaj7 | Gm7 | Ab | Bbm7 Eb |
  }
}
...
form { A B C D A C E }
```

- Two runs of four bars or more are one section when 95 beats in 100 of them have the same root
  and quality. The run whose saying again saves the most bars is taken first, then the same
  among the bars left; what lies between is a section said once; two sections that only ever
  come one after the other are one.
- Every playing of a section then says the same: on each beat, the chord the playings are surest
  of together. Where they differed in root or family the chord is marked, and the chord offered
  is the other reading.
- In the `.dettato.json` every bar played is still there with its own seconds, and has its
  `section` and `barOfSection`; `form` is the order. A chart in which nothing comes twice is
  written as before, with no sections.

Counted on 2026-11-14 with `--ears` (`measure.py form rwc-dev rwc-test seed1 --ears`; "the truth's
chords" are the true chords put through the same finder, what a reading with no chord wrong
would give):

| The sections, `--ears` | `rwc-dev` (chosen on) | `rwc-test` (counted only) | `seed1`, `seed7`, `seed11` |
|---|---|---|---|
| Bars written, of the bars played | 61.9% | 68.3% | 69.0%, 68.6%, 69.5% |
| The same, of the truth's chords | 69.5% | 76.4% | 54.6%, 54.5%, 53.7% |
| Sections a song (of them a bar or two long) | 11.5 (2.3) | 9.0 (1.8) | 3.8 (0.7), 3.4 (0.8), 3.3 (0.6) |
| The same, of the truth's chords | 9.2 (1.5) | 7.5 (1.5) | 2.1 (0.0), 2.0 (0.0), 2.0 (0.0) |

So a chart of a recording is a third shorter. The 95 in 100 was chosen on `rwc-dev`, where the
chords read as well with the sections as without (as counted then, before the two ends of a chart were read as told below: root 86.0%, root and family 81.8%, root and
quality 67.7%, both ways); on `rwc-test` they cost a tenth of a point of the roots and the
families and two tenths of the qualities, and on the songs made here the reader with its own
model loses up to two tenths of its roots and families (the tables above and below are counted
with the sections). Looser, more is lost: at 90 in 100 `rwc-dev` loses 0.4 of a point of its
roots, and with runs held alike when their roots and families are (a seventh heard or not)
the reader with its own model loses two to four points of the sevenths of the songs made here,
for two of their sections may differ in nothing else. A recording still comes out in about ten
sections, two of them a bar or two long: a bar read otherwise between two sections that come
again is a section of its own. The annotated chords do the same (eight sections a song), for a
band does not play a verse twice alike. Of the songs made here the truth is written in fewer
bars than the reading (a chord misread makes a section of its own); of recordings in more (the
annotations tell passing chords apart that the reading does not).

The marks grow by up to a point for it (the chords two playings disagree on: `rwc-dev` 28.7% to
29.3%, `rwc-test` 24.7% to 25.7%), and catch about as much more of the wrong chords as marking
that many more by sureness would (76.4% to 76.7%, 68.7% to 70.0%).

**The bars said again do not tell where the reading is wrong.** That was the hope (a second
playing as a second ear), and it was measured on `rwc-dev` before the sections were written
(2026-11-14; chords alike in root and family, half beat by half beat):

- In the annotated chords, 74% of the bars lie in a run of four that comes again exactly, and
  56% in a run of eight.
- Runs paired by what was read, loosely (three parts in four alike over four bars): where two
  such bars are read differently they truly are different three times in four, and taking what
  most playings say makes the reading worse (15.0% of the chords wrong, then 19.7%). Paired
  strictly (nine parts in ten), they differ in 2 parts in 1000 and there is nothing to take.
- Paired by the truth itself, which no reader has: taking what most playings say leaves 13.5%
  wrong instead of 15.0%; averaging the model's probabilities over the playings and reading
  again has the roots and families right 83.7% of the time instead of 81.8%; and the least
  sure 30% of the chords hold 78.6% of the wrong ones instead of 77.8%.
- Paired by how alike the model's probabilities are (overlapping nine parts in ten over four
  bars) and averaged: 82.5%, with the roots where they were.

The model reads a passage the same way each time it comes, wrong alike; what differs between
two playings is mostly the music.

### How a chart begins and ends

A recording does not begin on its first bar line and end on its last. Both readers now settle
the two ends of a chart by what sounds there (2026-11-14):

- **The bars reach both ends of the recording.** With `--ears` the bars were only those between
  the first and the last bar line Beat This! found; now they are carried on, as long as the
  first and the last, to 0 s and to the end. On `rwc-dev` 1.2% of the annotated chords' seconds
  lay outside the bars read (4 s before the first line, 129 s after the last: the tracker stops
  where the drums do, and the last chords are held); now 0.5% do (3 s and 50 s).
- **A bar is in the chart when a chord sounds in it**, from the first bar a quarter of which
  sounds a chord to the last such: silence before the music, a count-in and the ring-out are
  left out. To this reader's own hearing a half beat a tenth as loud as the song's middle one is
  silence; with `--ears` a hundredth (a fade-out is quiet and still music), or where crema's "no
  chord" is likelier than not (it names 62% of the seconds before the first annotated chord of
  `rwc-dev`, 93% of those after the last, 0.3% of the chords' own). Past the last bar Beat This!
  found, a bar a fifth as loud as the one before it is the last chord ringing out, and no bar.
- **A pickup is a short bar.** Whole beats of chord before the first bar line (a first bar that
  begins with silence is such a one) are written as a section of their own, which is how Lily#
  lets a piece open short; in the `.dettato.json` it is a bar numbered 0 with `"pickup": <beats>`:

  ```
  section Pickup {
    partial 2
    chords harmony {
      G |
    }
  }
  ...
  form { Pickup A A }
  ```

- **The bar the recording ends in is written whole**, and so is a first bar whose first half
  beat the beat grid began after (this reader's own grid may begin a little late: such a first
  bar used to be lost, and its chord with it).

Bars with sound and no chord before the first chord (a drum intro) are not in the chart yet.

Counted on 2026-11-14 (`measure.py head seed41 rwc-dev rwc-test --ears`, `measure.py head seed41`).
`seed41` is 24 songs made with `--pickup 0.5 --silence 3` and counted only; the constants were
chosen on `head/seed141` (24 more such songs), `seed1` and `rwc-dev`:

| | `seed41`, with the model | `seed41`, `--ears` | `rwc-dev`, `--ears` | `rwc-test`, `--ears` |
|---|---|---|---|---|
| The first bar line read is the true one, songs | 22 of 24 | 20 of 24 | 39 of 50 | 45 of 50 |
| The last, songs | 23 of 24 | 20 of 24 | 24 of 50 | 13 of 50 |
| Pickups with a chord, read as pickups of as many beats | 11 of 12 | 7 of 12 | | |
| Pickups read where there is none with a chord, songs | 2 | 2 | 11 | 11 |
| Chord read before the first true chord, seconds a song | 0.12 | 0.02 | 0.73 | 0.52 |
| Chord read after the last true chord, seconds a song | 0.07 | 0.10 | 1.47 | 2.25 |
| Tempo, meter, songs | 23, 24 | 20, 20 | 46, 49 | 47, 49 |
| Bar lines, F | 97.7% | 91.1% | 93.0% | 93.9% |
| Chord root; root and family | 96.6%; 92.1% | 94.4%; 90.5% | 86.4%; 82.3% | 87.3%; 82.1% |

- On the songs made here both readers begin a chart where the music does, the reader with its
  own model more surely (with `--ears` the bar lines are Beat This!'s). A pickup of a melody
  alone, which the generator makes too, has no chord to name; the reader with its own model has
  no word for "no chord" and names one there (on `head/seed141` both pickups it read "where
  there is none" were such).
- The recordings' truth says less about the ends than the songs' does: its bars begin at the
  first annotated bar line and end a bar after the last, it has no pickups, and its chords are
  cut to its bars. 1.6 s a song of the annotated chords of `rwc-dev` lie after the truth's last
  bar, and a bar read there is right and is counted as a bar line too many; and the bars before
  the first chord that the chart now leaves out are bar lines of the truth (20 of them on
  `rwc-dev`, 14 with no chord). So the bar lines of the recordings fell by half a point (`rwc-dev` 93.3% to 93.0%, `rwc-test` 94.4% to
  93.9%) while their chords rose (root 86.0% to 86.4% and 87.0% to 87.2%; root and family 81.8%
  to 82.2% and 81.9% to 82.1%).
- Not looked into: why the first bar line of eleven songs of `rwc-dev` is not the annotated one
  (in some Beat This! counts a pickup where the annotation has a bar), and the songs of `seed41`
  that either reader gets wrong.
- This reader's own hearing, given the true bar lines of the recordings, lost three tenths of a
  point of its roots for it (77.4% to 77.1%, the table of "What models trained on recordings
  do"). Why was not looked into (a tenth as loud may not be silence in a recording that fades).
  It is the reader kept for comparison, and was left so.

One thing was found in Lily# on the way: a short bar's chords are fitted to the meter's beats,
not to the bar's own, so three chords in a pickup of three beats of 4/4 are warned of (`lysc
check`: "3 chord slot(s) in a 4/4 bar fit no beat"). The reader writes the last chord of such a
pickup alone.

### Without torch and TensorFlow

A chart now takes three Python environments (3.7 GB: torch for Beat This!, TensorFlow for crema,
librosa for the reader). Measured on 2026-11-14, for the choice of what to ship (a Python
command, or a .NET one as Lily#'s and Calamo's are); nothing is chosen yet. The scripts are in
`tools/d2/`, what they write in `scratch/d2/`.

**Both models run in onnxruntime and say the same.**

| | crema's chords | Beat This! (`final0`) |
|---|---|---|
| Written by | tf2onnx, opset 17 | `torch.onnx.export` (both the new exporter and the old trace) |
| The file | 2.2 MB | 82.9 MB |
| Against the model it came from, 10 songs of RWC | probabilities within 3.3e-6; the likeliest chord the same in all 24411 frames | logits within 8.4e-5; all 4103 beats and their bar lines the same |
| Read through the whole reader | `rwc-dev`, `rwc-test`: every number of the table the same | `seed1`: the 24 `.beats` files the same byte for byte |
| A song of three and a half minutes on this CPU (24 cores) | 0.35 to 0.43 s (TensorFlow 0.35 s) | 3.2 to 4.3 s on 8 threads, 10 s on all 24 (torch 2.0 to 2.4 s) |

Beat This!'s mel spectrogram, which torchaudio made, is forty lines of numpy and within 1.2e-4
of it (`ears_onnx.py`). The models then need neither torch nor TensorFlow: one environment of
numpy, librosa, soxr, soundfile and onnxruntime (450 MB, of which numba, llvmlite and scipy are
270) reads
`seed1` and RWC to the same numbers.

**The reader's own time was the harmonic part.** Of the 10 s `read_chords.py --ears` took for
that song, 8 to 9 were the median filter that takes the drums out (`librosa.effects.harmonic`),
there for the reader's own chords. With `--ears` the chords are crema's, and the reader's chroma
is left for the bass note, for how much the sound changes, and for one of the four things the
marks weigh. From the recording as it is (`measure.py table ... --ears --set HARMONIC_FRAMES=31`
is how it was):

| `--ears` | `seed1` | `seed7` | `seed11` | `rwc-dev` | `rwc-test` (counted only) |
|---|---|---|---|---|---|
| Root; root and family, harmonic part | 95.1%; 91.6% | 93.1%; 88.4% | 94.5%; 92.4% | 86.4%; 82.2% | 87.2%; 82.1% |
| The same, the recording as it is | 95.1%; 91.6% | 93.1%; 88.4% | 94.5%; 92.4% | 86.4%; 82.3% | 87.3%; 82.1% |
| The bass note too, harmonic part | 70.4% | 56.6% | 72.2% | 63.3% | 64.3% |
| The same, the recording as it is | 73.4% | 58.5% | 73.6% | 64.3% | 65.0% |

So `use_ears()` now leaves it out (the reader with its own hearing keeps it: there the drums
cost chords). The marks moved with it, by under a point (the table of the marks above).

**One song (3 min 27 s, `RWC_P001`), each step a process of its own, started cold:**

| | Beats and bar lines | Chords | The chart | In all |
|---|---|---|---|---|
| As it was: three environments | 5.4 s (torch) | 9 s (TensorFlow; 8 of them its start) | 10.6 s | 25 s |
| Now: the harmonic part left out | 5.4 s | 9 s | 3.6 s | 18 s |
| One environment, ONNX (`ears_onnx.py`, then `read_chords.py`) | 7.3 s for both: 2 s to start, 0.3 s mel, 3.9 s Beat This!, 0.4 s CQT, 0.35 s crema | | 3.6 s | 11 s |
| .NET, ONNX (`tools/d2/spike`: the beats only) | 3.6 s: 0.2 s to start, 0.1 s resampling and mel, 3.2 s Beat This! | not written | not written | |

In one Python process the third row would be about 9 s (one start, the recording loaded once);
that was not run. Beat This! is most of what is left, in time and in size.

**The .NET trial** (`tools/d2/spike`, 260 lines, with Microsoft.ML.OnnxRuntime as Calamo has it;
12 MB beside the models) reads a `.wav`, halves its rate, makes the mel spectrogram, runs the
model and picks the peaks. On `seed1` four of the 24 songs have another number of beats than
torch finds, and in the other twenty 94 beats are elsewhere (of 2970 in all); bar lines F 96.6%
to 96.4%, the chords the same. That is the resampler, not the port: in Python, scipy's
`resample_poly` in place of soxr does as much (four songs, 77 beats; bar lines 96.5%). What a .NET command still needs, none of it written:

- crema's input, librosa's constant-Q transform of two harmonics (216 bins each). librosa's with
  another resampler inside (`res_type="polyphase"`) reads `rwc-dev` and `rwc-test` to within a
  tenth of a point in every column, so a port need not match it sample for sample;
- the reader's own features without the harmonic part: two chromas from the same transform (with
  librosa's estimate of the tuning), the onset strength, the loudness;
- the reader itself (`read_chords.py`, 920 lines of numpy: the search, the meter, the key, the
  sections, the two files), checked by `measure.py` reading what it writes;
- other rates than 44100 and 22050 Hz, and mp3.

Not measured: Beat This!'s smaller model (`small0`, 8 MB by its README; the server it is fetched
from answered 500 on the day), the models at half precision, another CPU, and any of this on a
recording that is not a `.wav`.

```
.venv-crema/Scripts/python -m pip install tf2onnx onnxruntime
.venv-bt/Scripts/python -m pip install onnx onnxruntime onnxscript
.venv-crema/Scripts/python tools/d2/crema_onnx.py scratch/real/rwc-p --songs 10    # writes scratch/d2/crema-chord.onnx, compares
.venv-crema/Scripts/python tools/d2/crema_classes.py                               # the order of its 170 chords
$env:PYTHONUTF8 = "1"
.venv-bt/Scripts/python tools/d2/bt_onnx.py scratch/real/rwc-p --songs 10 --how dynamo   # scratch/d2/beat-this-final0-dynamo.onnx
.venv-bt/Scripts/python tools/d2/bt_mel.py data/synth/seed1 --out scratch/d2/mel/seed1   # torch's mel, to compare with
py -3.12 -m venv scratch/d2/venv-onnx
scratch/d2/venv-onnx/Scripts/python -m pip install numpy librosa soxr soundfile onnxruntime
scratch/d2/venv-onnx/Scripts/python tools/d2/ears_onnx.py data/synth/seed1 --out scratch/d2/out/seed1 --mel-against scratch/d2/mel/seed1
.venv/Scripts/python tools/measure.py table seed1 --lines scratch/d2/out/seed1 --heard scratch/d2/out/seed1
scratch/d2/venv-onnx/Scripts/python tools/d2/ears_onnx.py scratch/real/rwc-p --out scratch/d2/out/rwc-polyphase --no-beats --cqt-res polyphase
.venv/Scripts/python tools/measure.py table rwc-dev rwc-test --lines scratch/real/beats-bt-folds --heard scratch/d2/out/rwc-polyphase
dotnet build tools/d2/spike -c Release -o scratch/d2/spike/out
scratch/d2/spike/out/Spike.exe data/synth/seed1 scratch/d2/beat-this-final0-dynamo.onnx scratch/d2/out/seed1-net
.venv/Scripts/python tools/d2/beats_diff.py scratch/beats-bt/seed1 scratch/d2/out/seed1-net
.venv/Scripts/python tools/d2/reader_time.py song.wav ears    # where the reader's time goes (ears: the folder of song.beats and song.npz)
```

### In .NET: the ears

What ships is to be a program a VS Code extension carries, as Calamo's is (2026-11-14), so the
reader is being moved to .NET, a part at a time, each checked against the Python it replaces.
The first part is both ears (`src/Dettato`, 636 lines, `dettato ears`): a `.wav` to the
`.beats` and `.npz` that `track_beats.py` and `crema_chords.py` write. It has librosa's
constant-Q transform (`Cqt.cs`: the filters of an octave over the spectra of frames, the rate
halved, the octave below; the early halving, the sparse filters and the scaling as librosa 1.0
has them), a resampler with soxr HQ's pass band but not its samples, pumpp's dB, and the mel
spectrogram and the peaks of the trial above.

| Against Python (`tools/net_check.py`) | `seed1`, `seed7`, `seed11` | RWC, 100 songs |
|---|---|---|
| The chord model's input, above -60 dB | 1.3e-5, 1.4e-5, 1.3e-5 dB from librosa's (worst 0.04) | 8.5e-6 dB (worst 0.16) |
| The chords' probabilities | 2.5e-6, 3.6e-6, 2.3e-6 from crema's | 2.9e-6 |
| The likeliest chord the same | 16547/16548, 17922/17922, 14862/14862 frames | 260677/260678 frames |
| The beats, against torch's | 1 of 2970 moved; one song of `seed7` with another number, the rest of its set the same; 1 of 2419 moved | 5 songs against `ears_onnx.py`: all 1989 the same |
| Every number of `measure.py table` | the same as `--ears` | the same as `--ears` (`rwc-dev`, `rwc-test`; the bar lines from the fold models) |

The trial's 94 moved beats were its resampler, as said there: with the pass band where soxr has
it they are 2. One song (3 min 27 s, `RWC_P001`), cold: 4.3 s (0.2 s to start and open the
models, 0.1 s to load and resample, 3.0 s Beat This!, 0.5 s the two transforms, 0.4 s crema), on
8 threads; 11.5 MB beside the models. Not looked at: memory (2.4 GB at its most over 100 songs
in one process), rates other than 44100 Hz (the resampler takes any; no recording here has
another), and the bar lines of RWC, which need the fold models as ONNX to count.

```
dotnet build src/Dettato -c Release -o scratch/net/out
scratch/net/out/dettato.exe ears data/synth/seed1 --out scratch/net/seed1 --models scratch/d2 --cqt
scratch/d2/venv-onnx/Scripts/python tools/net_check.py data/synth/seed1 scratch/net/seed1 --heard scratch/chords-crema/seed1 --beats scratch/beats-bt/seed1
.venv/Scripts/python tools/measure.py table seed1 --lines scratch/net/seed1 --heard scratch/net/seed1
scratch/net/out/dettato.exe ears scratch/real/rwc-p --out scratch/net/rwc-p --models scratch/d2 --cqt     # 9 minutes
scratch/d2/venv-onnx/Scripts/python tools/net_check.py scratch/real/rwc-p scratch/net/rwc-p --heard scratch/real/chords-crema
.venv/Scripts/python tools/measure.py table rwc-dev rwc-test --lines scratch/real/beats-bt-folds --heard scratch/net/rwc-p
```

### In .NET: what the rules read

The second part (2026-11-14) is what the rules read beside the two models: `read_chords.py`'s
`features()` as `--ears` has it, the recording as it is. `dettato features` writes
`<name>.feat.npz` with the same four arrays (`Features.cs`, 179 lines; 808 in all): one
short-time spectrum gives librosa's onset strength (128 mel bands in dB, the rise from frame to
frame) and its tuning (`piptrack`'s peaks, the commonest distance from a 36th of an octave, in
single precision where librosa is); the loudness is of the same frames; the two chromas are the
constant-Q transform already here, moved by the tuning, folded and set to 1 at the loudest.
The same peaks placed among the semitones (12 to the octave) are the `cents` the file beside a
chart says as `tuning` (2026-11-16): a 36th of an octave is a third of a semitone, so that tuning
cannot tell 30 cents sharp from 3 flat. `RWC_P001` turned by ffmpeg 30, −20 and 45 cents reads
29, −21 and 44 where it reads −1 as it is. `measure.py --net scratch/net` reads from these in
place of librosa's.

| Against librosa (`net_check.py --feats`) | `seed1`, `seed7`, `seed11` | RWC, 100 songs |
|---|---|---|
| The tuning (hundredths of a 36th of an octave) | the same in 72/72 | the same in 99/100 (`RWC_P059`: +0.01 for 0.00) |
| The tuning in cents | the same in 24/24 on `seed1` | not compared |
| Onset strength (its values 1.3 on average) | 1.6e-4, 1.4e-4, 1.5e-4 apart (worst 3.9e-3) | 1.4e-4 (worst 5.0e-3) |
| Chroma, of 1 | 1.3e-7 apart on `seed1` (worst 2.8e-4) | 6.5e-6 (worst 0.04, in `RWC_P059`) |
| Bass chroma, of 1 | 4.2e-7 apart on `seed1` (worst 2.0e-3) | 2.4e-5 (worst 0.31 in a quiet frame of `RWC_P059`; 17 of 1017156 frames louder than a tenth of the song's median are over 0.01 apart, all in that song) |
| Loudness | 8.3e-8 apart of 0.039 | 2.3e-6 of 0.2 (worst 4.4e-4) |
| Every number of `measure.py table --ears`, and of `doubt` | the same | the same (`rwc-dev`, `rwc-test`) |

The chromas are counted where the recording is not digital silence: there a frame is rounding
noise over its largest value, in librosa as here, and nothing like it (up to 0.94 apart). The
loudness and the onset strength are as far apart as the resampler leaves them (not soxr's
samples). librosa's bass chroma has a frame more than the rest in some songs (14 of the files
kept here): the recording is brought down to a 32nd of its rate first, the length rounded up.
This writes the same. One song (`RWC_P001`), cold: 0.43 s in all, where librosa takes 1.2 s
once warm and 3.0 s the first time.

```
scratch/net/out/dettato.exe features data/synth/seed1 --out scratch/net/seed1
.venv/Scripts/python tools/net_check.py data/synth/seed1 scratch/net/seed1 --no-cqt --feats scratch/feat-h0
scratch/net/out/dettato.exe features scratch/real/rwc-p --out scratch/net/rwc-p        # 33 s
.venv/Scripts/python tools/measure.py table seed1 rwc-dev rwc-test --ears --net scratch/net
```

### In .NET: the rules

The third part (2026-11-14) is the reader itself: `dettato read` takes a `.wav` and writes the
`.lys` and the `.dettato.json`, with nothing of Python in between. It is `read_chords.py` as it
runs with `--ears`, step for step (`Reader.cs`, 532 lines: the bars carried to both ends, the
search, the two ends and the pickup, the bass note, the marks, the key; `Form.cs`, 213: the
sections; `Writer.cs`, 145: the two files; 1894 lines in all). The reader with nothing learnt
and the one with its own model are not moved: they are what the songs made here are measured
against, and stay in Python. Where numpy's arithmetic is not the plain one it is done numpy's
way (`Np.cs`: the order of its sums, means in single precision where the features are, Python's
floor division and its rounding to decimals), so the two can be compared letter for letter.

| Against `read_chords.py` (`tools/read_check.py`) | The songs made here | RWC, 100 songs |
|---|---|---|
| `.dettato.json`, letter for letter | the same in 168/168 (`seed1`, `seed7`, `seed11`, `seed21`, `seed31`, `seed41`, `head/seed141`) | the same in 100/100 |
| `.lys`, but the line that names the program | the same in 168/168 | the same in 100/100 |
| `measure.py table`, `doubt`, `form` and `head`, counting dettato's charts (`--read`) | the same (`table` on `seed1`, `seed7`, `seed11`; `form` on `seed1`; `head` on `seed41`) | the same (`table` on `rwc-dev` and `rwc-test`; `doubt` and `form` on `rwc-dev`; `head` on `rwc-test`) |
| `lysc check`, and the files against SIDECAR.md | 168/168, 168/168 | 100/100, 100/100 |

Both read the same beats and probabilities there (`--lines`, `--heard`: for RWC the fold
models' beats, which only Python has) and the same features (.NET's: Python with `--net`).
With the two models run inside (`dettato read` alone), `seed1` is the same in 24/24 as from the
files `dettato ears` writes. One song (3 min 27 s, `RWC_P001`), cold, from the `.wav` to the
chart: 5.5 s (0.3 s to start and open the models, 0.1 s to load and resample, 0.3 s the
features, 3.7 s Beat This!, 1.0 s the transforms and crema, 0.1 s the rules), on 8 threads.
Not done: `--tempo`, the reasons of a mark (`why`, which `measure.py doubt` shows when Python
reads). Other kinds of file than `.wav`: the next section. Not looked at: RWC from the `.wav` alone (the model here has
heard those songs, so its bar lines are not to be counted).

```
scratch/net/out/dettato.exe read song.wav --out charts --models scratch/d2      # song.lys and song.dettato.json
.venv/Scripts/python tools/measure.py table seed1 rwc-dev rwc-test --ears --net scratch/net --write scratch/pyread
scratch/net/out/dettato.exe read data/synth/seed1 --out scratch/net/read/seed1 --lines scratch/beats-bt/seed1 --heard scratch/chords-crema/seed1
scratch/net/out/dettato.exe read scratch/real/rwc-p --out scratch/net/read/rwc-p --lines scratch/real/beats-bt-folds --heard scratch/real/chords-crema   # 27 s
py -3.12 tools/read_check.py scratch/pyread/seed1 scratch/net/read/seed1
py -3.12 tools/read_check.py scratch/pyread/rwc-dev scratch/net/read/rwc-p
.venv/Scripts/python tools/measure.py table seed1 rwc-dev rwc-test --ears --read scratch/net/read
.venv/Scripts/python tools/measure.py check scratch/net/read/seed1 scratch/net/read/rwc-p
```

### A recording as it comes

The command a user calls (2026-11-14, D3), and Lily# when it has the menu:

```
dettato song.mp3                    # song.lys and song.dettato.json beside it
dettato song.mp3 -o charts/x.lys    # there, with x.dettato.json; --force writes over a chart
dettato --version --json            # {"version":"0.2.0","protocol":1,"commands":["read"],"inputs":["wav","mp3","m4a","mp4","aac","flac","wma"]}
```

stderr says each step as it begins (listening, the beats, the chords) and what was read; on
failure its last line says why (`dettato: x.wav was not read: fewer than two bar lines found`).
stdout has the chart's path alone. Exit codes: 0 written; 1 the recording was not read (not a
recording, shorter than 5 seconds, no bar lines, no chord); 2 the arguments (no such file, a kind
not read, a chart already there without `--force`); 3 the reader itself (a model missing, a fault).
The two files are written at the end, under other names first: a reading stopped half-way leaves
nothing. The models are beside the program, or where `--models` says.

A plain `.wav` (16 or 24 bit, float) is read by the program itself, as before; every other kind
by Windows' Media Foundation, through NAudio (MIT), so no decoder is carried. Media Foundation
leaves in front the silence an encoder adds, which a browser or ffmpeg takes out; so does the
reader then, where the file says how much (`Audio.cs`: an mp3's Xing/Info frame and LAME's delay,
an MPEG-4 sound track's edit list). One song made here, written by ffmpeg in each kind and read
back (`dettato audio`, `tools/heard_check.py`: where the cross-correlation with the `.wav` peaks):

| Kind (ffmpeg's encoder) | As Windows gives it | As the reader takes it |
|---|---|---|
| mp3, constant bit rate (libmp3lame; 44100, 48000, 22050 Hz, mono) | 1729 samples late (39 ms; 1153 at 22050 Hz) | 0 |
| mp3, varying bit rate | 577 late (13 ms) | 0 |
| mp3 with no Xing/Info frame | 577 late | 1105 late, as ffmpeg and a browser give it (Windows gives 528 fewer in front) |
| m4a, mp4 (aac; 44100, 48000 Hz) | 1024 late (23 ms) | 0 |
| aac (bare ADTS) | 1024 late | 1024 late: nothing says how much |
| flac, wma, 8-bit wav | 0 | 0 |

Files from elsewhere (the user's library, 2026-11-16): 149 of 150 mp3 picked at random have no
Xing/Info frame (123 carry iTunes' `iTunSMPB` note, which says 528 in front; neither ffmpeg nor
Chrome goes by it), and the m4a of iTunes 11 and of the iPhone's Voice Memos have no edit list but
an `iTunSMPB` note (2112 in front), which ffmpeg goes by. Against ffmpeg's decoding of the same
file, the reader was 528 samples early on those mp3 (12 ms at 44100 Hz, 33 ms at 16000) and 2112
late on those m4a (48 ms); now it is 0 on all seven kinds tried (`scratch/d3/user/`, ffmpeg's wav
beside each as `ref.wav`). Chrome's `decodeAudioData` (what Lily#'s preview plays an mp3 with)
gave the same first sample as ffmpeg on the four mp3 kinds (`tools/chrome_lag.py <file> <ffmpeg's
wav>`, headless Chrome through `tools/cdp.mjs`).

The 24 songs of `seed1` as mp3 (192 kbit/s) and m4a (128 kbit/s), read from the file alone with
the models inside, against the same from the `.wav` (`tools/kinds_check.py`, and `measure.py
table seed1 --ears --read`):

| `seed1`, 24 songs | `.wav` | mp3 | m4a |
|---|---|---|---|
| Bar lines F | 96.6% | 96.4% | 95.6% |
| Root / root and family | 95.1% / 91.6% | 95.1% / 91.6% | 95.2% / 91.5% |
| Tempo, meter | 22, 22 | 22, 22 | 22, 22 |
| Songs with as many bars as from the `.wav` | | 21 | 19 |
| In those, a bar's start against the `.wav`'s: the middle one, the range | | 0 ms, −60 to +20 ms | 0 ms, −1340 to +60 ms |
| In those, chords written the same | | 96.9% | 93.4% |

So a chart from an mp3 is as right as one from the `.wav`, and its bars are at the same times,
but it is not the same chart: the beat model puts a bar line elsewhere in 3 and 5 songs of 24,
and some chords at the edge of a decision fall the other way. Eight of the songs at 48000 Hz
give the `.wav`'s charts to the letter; at 22050 Hz the chords are the same and 2 of 8 have a bar
more or fewer. A recording of RWC as an mp3 with a Japanese name (3 min 27 s) is read in 6.3 s
and passes `lysc check`. Not looked at: files not written by ffmpeg (iTunes' AAC, whose silence
is 2112 samples; mp3 from other encoders), a file with pictures in it, anything over some
minutes (the whole recording is held in memory), the accuracy on recordings as mp3.

```
scratch/net/out/dettato.exe audio kinds --out heard                 # kinds/: one song as a.wav, a.mp3, a.m4a ...
.venv/Scripts/python tools/heard_check.py heard a.wav
scratch/net/out/dettato.exe read mp3/seed1 --out read-mp3/seed1 --models scratch/d2
py -3.12 tools/kinds_check.py read-wav/seed1 read-mp3/seed1
.venv/Scripts/python tools/measure.py table seed1 --ears --read read-mp3
```

### Two half bars are one bar

A song of the owner's (2026-11-14: Billy Joel's "My Life" as an mp3, the first recording read
that is not RWC's) showed two things. The beat model counted a first beat in the middle of one
bar, and the chart had two bars where one is played: every bar after it was one late against the
chart as Lily# plays it. And the tempo was written 130 where the song is at 130.9: the model's
beats are on a grid of 20 ms, the middle bar was 1.84 s, the bars together 1.833 s each.

So (`lines_of`, `MEND_BARS`): two bars in a row that are shorter than the song's bar and have its
beats together are one bar. And (`EVEN_TEMPO`) the tempo is from the mean of the bars within a
tenth of the middle one. Chosen on `rwc-dev` and the three sets below; `rwc-test` was counted
after. With `--ears`, before and after:

| | `rwc-dev` | `rwc-test` (counted only) | `rwc-p` | `seed1` | `seed7` | `seed11` |
|---|---|---|---|---|---|---|
| Bar lines, F | 93.0 → 93.6% | 93.9 → 94.9% | 93.4 → 94.2% | 96.6 → 96.7% | 90.7 → 90.9% | 94.0 → 93.6% |
| Root | 86.4 → 86.4% | 87.3 → 87.3% | 86.8 → 86.9% | 95.1 → 95.1% | 93.1 → 93.1% | 94.5 → 94.5% |
| Tempo, songs | 46 → 46 | 47 → 47 | 93 → 93 | 22 → 22 | 20 → 21 | 21 → 21 |
| Marked → of the wrong in root or family | 30.2% → 77.8% | 25.7% → 70.3% | 28.0% → 73.9% | | | |

Of the 11503 bars the fold models find in RWC, 373 pairs were such halves. The other sets with
ears: `seed21` 86.1 → 86.1%, `seed31` 92.6 → 93.5%, `seed41` 91.1 → 90.8%. The bars written of the
bars played (the sections) are 61.5% and 67.4% on the two halves of RWC (61.9% and 68.3%); the
first and last bar lines are as they were within one song. Mending only where the song's bar has
four beats or more was tried and is worse on the songs made here (`seed7` 90.4%). A lone short
bar stays a bar (it may be the music's own bar of two beats), and is written as long as the rest.
"My Life" is 152 bars at 131 now, the two times its verse leads into the bridge written alike.
In .NET the same (`Reader.cs`), letter for letter on the 268 charts.

### On the songs made here, nothing learnt


On three sets of 24 songs (2026-11-14). The reader's constants were chosen on `seed1`, and
`seed7` was looked at while choosing; `seed11` was made later and not chosen on:

| Nothing learnt | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Tempo (within 4%), songs | 22 of 24 | 22 of 24 | 22 of 24 |
| Meter, songs | 22 of 24 | 23 of 24 | 23 of 24 |
| Key, songs | 15 of 24 | 15 of 24 | 16 of 24 |
| Key signature, songs | 21 of 24 | 19 of 24 | 24 of 24 |
| Bar lines (within 70 ms), F | 93.1% | 90.2% | 97.4% |
| Chord root, by time | 87.5% | 83.6% | 90.3% |
| Root and family (major, minor, suspended, diminished) | 75.9% | 73.7% | 83.4% |
| Root and quality (sevenths told apart) | 61.6% | 54.4% | 72.4% |
| The bass note too | 58.1% | 49.2% | 69.7% |

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
| Bar lines (within 70 ms), F | 95.0% | 97.6% | 99.1% |
| Chord root, by time | 95.6% | 96.0% | 97.2% |
| Root and family | 91.0% | 93.0% | 96.4% |
| Root and quality | 83.3% | 74.2% | 89.7% |
| The bass note too | 77.8% | 66.0% | 85.5% |

The same 72 songs played through a sound font the model never heard (TimGM6mb, 6 MB, against
MuseScore General's 215 MB) read as well, so it is not the one sound font the model leans on:

| Another sound font | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Chord root, nothing learnt → with the model | 86.2 → 95.8% | 88.0 → 97.5% | 88.1 → 96.4% |
| Root and family | 78.7 → 93.1% | 83.7 → 94.7% | 84.2 → 95.1% |
| Root and quality | 60.8 → 81.1% | 66.8 → 79.0% | 74.9 → 84.9% |
| Bar lines, F | 84.1 → 99.7% | 93.4 → 99.7% | 79.3 → 89.9% |
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
| Chords read | 2627 | 2588 |
| Wrong in root or family | 5.9% | 4.9% |
| Marked | 24.4% | 19.3% |
| Of the chords wrong in root or family, marked | 79.2% | 87.5% |
| Of the marked, wrong in root or family | 19.0% | 22.4% |
| Of the marked, wrong at all (sevenths too) | 34.9% | 33.5% |

So about one chord in five asks for a look, and four in five of the wrong roots and families are
among them. (Two marks in a hundred chords are not the model's doubt: they are chords two
playings of a section disagree on, marked since the sections were written; without them 22.9%
and 17.2% were marked, and as many of the wrong ones caught.) The chord offered in the mark is
the true one for under half of the marked chords that are wrong (26.8%, 38.9%): it is a hint,
not a correction. The reader with nothing learnt
marks nothing.

### Chords that last two bars

`gen_chords.py --held 0.5` makes half of a song's sections hold each chord for two bars
(`data/synth/seed21`, 24 songs). Left alone, the reader with the model takes the tempo of 16 of
them right and reads the faster ones at half their tempo, with bars twice as long; the chords are
read as well as ever (root 98.8%). What is heard does not settle it: a fast song whose chords
last two bars and a slow one whose chords last one sound the same bar by bar. A price on long
bars was tried and thrown away (it mends these songs and breaks as many of the others).

So the reader can be told: `--tempo 150` (about right is enough) keeps only the bars of two,
three or four such beats.

| `seed21`, with the model | left alone | told the tempo |
|---|---|---|
| Tempo (within 4%), songs | 16 of 24 | 22 of 24 |
| Bar lines, F | 89.6% | 96.8% |
| Chord root, by time | 98.8% | 98.6% |

Told the true tempo, the three sets above read the same or a little better (`seed1`: 24 tempi
of 24, bar lines 96.9%; `seed7` and `seed11` unchanged).

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
- But it says so itself: it doubts 20 of 20, 19 of 19, 9 of 9 and 9 of 13 of the chords it reads
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
| Bar lines, F | 98.9% | 97.6% | 80.9% |
| Chord root, by time | 98.2% | 98.4% | 95.9% |

A tempo that wanders as a band's does costs about a point of bar lines. One that moves by a
seventh is where the reader begins to lose them; the chords hold longer than the bar lines.

Measuring this found a constant that had outlived its reason. The beat tracker was held three
times as tight as librosa holds it, chosen on `seed1` before the half-beat slide was mended.
At librosa's own value the bar lines of nearly every set rose (`seed7` 89.0% to 97.4%, the 200
songs kept for learning 94.0% to 96.7%, the moving tempo of 5% 67.7% to 80.7%); `seed1`
alone fell, 97.7% to 94.7%.

They need `numpy`, `scipy`, `librosa`, `soundfile` and `scikit-learn` (in `.venv`, not committed).
They are written to be measured and replaced, not shipped.

## The VS Code extension

`extension/` is the reader as a VS Code extension (`yotsuda.dettato`, Windows x64): `dettato.exe`
published self-contained, the two models beside it, and one command, **Dettato: Transcribe
Recording to Lily# Chord Chart (.lys)…**, which reads a recording (`dettato <recording> -o
<chart> --force`, after asking when a chart is there) and opens the chart written beside it.
What it gives another extension is the reader's path and its `--version --json` (`bin/dettato.json`,
written when the package is made). The package is 113 MB (the beat model is 83 MB of it).

```
tools/pack-extension.ps1       # out/extension/dettato-win32-x64-<version>.vsix (the models from scratch/d2)
tools/deploy-extension.ps1     # packs, and installs it into this PC's VS Code; publishes nothing
```

## Siblings

- [Lily#](https://github.com/yotsuda/LilySharp): the notation language, engraver and editor.
- [Calamo](https://github.com/yotsuda/Calamo): reads a page image into `.lys`.
- [Cantoria](https://github.com/yotsuda/Cantoria): sings a part's notes and words.

Dettato is a program of its own, as they are. It writes `.lys` text and calls Lily#'s `lysc` to
check it; Lily# does not depend on it.

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

No recording and no model that is not the project's own is committed here.
