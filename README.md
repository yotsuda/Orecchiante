# Dettato

Audio to score for [Lily#](https://yotsuda.github.io/LilySharp/): listens to a recording and
writes editable Lily# text (`.lys`), with the places it is unsure of marked for a look.

*Dettato* is Italian for dictation, the exercise of writing down music as it is heard.

**Status: not started.** Nothing here reads a recording yet.

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
`seed7` was looked at while choosing; `seed11` was made after the last change and read once:

| | `seed1` | `seed7` | `seed11` |
|---|---|---|---|
| Tempo (within 4%), songs | 21 of 24 | 20 of 24 | 21 of 24 |
| Meter, songs | 21 of 24 | 21 of 24 | 22 of 24 |
| Key, songs | 14 of 24 | 13 of 24 | 15 of 24 |
| Bar lines (within 70 ms), F | 89.3% | 77.0% | 87.7% |
| Chord root, by time | 86.4% | 81.5% | 87.6% |
| Root and family (major, minor, suspended, diminished) | 75.0% | 71.8% | 81.1% |
| Root and quality (sevenths told apart) | 57.9% | 52.3% | 68.7% |
| The bass note too | 54.4% | 46.8% | 66.4% |

Every chart it writes passes `lysc check`. What it does not do yet:

- It keeps a steady tempo (the beat tracker is held tight, which these songs reward and a
  recording that slows down will not).
- It takes the bar to be as long as one chord lasts, so a song whose chords last two bars would be
  read at half its tempo. Nothing rhythmic (the drums' pattern) weighs in yet.
- The key is a guess from the notes heard, and often the relative or the dominant.

It needs `numpy`, `scipy`, `librosa` and `soundfile` (in `.venv`, not committed). It is written to
be measured and replaced, not shipped.

## Siblings

- [Lily#](https://github.com/yotsuda/LilySharp): the notation language, engraver and editor.
- [Calamo](https://github.com/yotsuda/Calamo): reads a page image into `.lys`.
- [Cantoria](https://github.com/yotsuda/Cantoria): sings a part's notes and words.

Dettato is a program of its own, as they are. It writes `.lys` text and calls Lily#'s `lysc` to
check it; Lily# does not depend on it.

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

No recording and no model that is not the project's own is committed here.
