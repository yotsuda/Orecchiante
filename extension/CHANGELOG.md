# Changelog

Notable changes to Dettato are recorded here, newest first.

## 0.5.0

- Sections by meaning: a section begins where the music turns (the key signature moves, the music
  grows far louder or quieter), where a passage played again whole elsewhere begins or ends, and,
  inside a long passage, on a grid of four-bar phrases where a phrase said twice has ended, where the
  dominant goes home across the bar line, or where a phrase played elsewhere begins as it does there.
  A section that plays an earlier one again is named after it (A, A2, A3). No repeat signs, and the
  playings of a section no longer have their chords made the same.
- Where singing comes in or stops is not heard yet (it needs a model for the vocals), so a sung song
  may have longer sections than it should.

## 0.4.2

- A line of the chart ends with its bar line, then the break (`… | G C | break`), which reads more
  easily than `… G C break |`. The lines fall where they did.

## 0.4.1

- Bars half as long as the rest, one after the other (the beat model counting twice as fast for a
  while), are paired into whole bars. An even run is paired from its first; an odd one leaves a half
  over at its first or its last, where the key says (the home chord on the bar line, after its
  dominant). A whole bar between two halves is cut into two first. 糸: 89 → 71 bars, all of 3.6 to 5 s.

## 0.4.0

- Two ears for the chords: BTC (MIT) now listens beside crema, and their probabilities are
  averaged. On the RWC songs counted only, roots 87.3% → 88.8% and root and family 82.2% → 83.9%;
  sevenths read much better on the songs made here. The extension is 13 MB larger.

## 0.3.5

- A bar far shorter than the rest (a quarter of the middle one or less: the beat model marking one
  bar's start twice, 0.08 s apart) is no longer a bar: it goes with the shorter bar beside it.

## 0.3.4

- A section shorter than three bars shows no name: it is named after the section before it
  (`B_1`, `B_2`) and hidden in the form (`|: B [1. ~B_1] :| [2. ~B_2] C`), so the letters run on
  with none missing.

## 0.3.3

- The second ending: where a section is followed once by a short section and another time by a
  longer one that opens the same way, its opening bars become a section of their own
  (`|: B [1. C] :| [2. D] E`), so that the next section begins where its phrase does.
- A section of one bar goes on the line before it.

## 0.3.2

- The first chord of the chart is written with its guitar shape (`D(xx0232)`), so that its
  diagram shows: the shape Lily#'s editor offers first, asked of Lily#'s own `lysc shape` when
  Lily# says where lysc is (`LILYSHARP_LYSC_PATH`, set by its Convert Recording). Without it, the
  chord name alone, as before.

## 0.3.1

- The tempo is written to a tenth (`tempo 4 = 131.8`; a whole number stays bare), in the chart
  and in the file beside it, so that Lily# plays the recording at the pace the chart says.
- The chart opens in the Petaluma music font (`fonts { music "Petaluma" }`, under the key), with a
  note that Shift+Ctrl+Up/Down on the font's name switches it.
- A note above the first chords: Shift+Ctrl+Up/Down on a chord name shows its guitar chord diagram.

## 0.3.0

- The command "Transcribe Recording to Lily# Chord Chart (.lys)…" is gone: the Lily# extension
  has the menu now ("Convert Recording to Lily# (.lys)…"), and this extension carries the reader
  it runs.
- Fewer, longer sections: sections of six bars or fewer that come one after the other are one
  (four and four make eight), and so are sections played once that come one after the other, up
  to 32 bars; a longer one is cut at a turn of the music or every eight bars.
- Four bars a line (a last bar alone joins the line before, four and two are three and three), and every section begins a line.
- The form writes repeat signs where sections come again right away (`|: A :|`,
  `|: A [1. B] :| [2. C]`, `|: B C B [1. D] :| [2. E]`), so that it shows how the song goes. Lily# plays the chart bar for bar
  as before.
- A chart written in another folder than the recording's (`-o`) names the recording by the way to
  it (`../Music/x.mp3`, or the whole path on another drive), not by its file name alone.

## 0.2.2

- The file beside the chart also says the recording's tuning (cents from A = 440 Hz) and the time
  of each beat in each bar.
- An mp3 with no Xing/Info frame (as iTunes writes them) and an m4a with iTunes' gapless note but no
  edit list (iTunes, the iPhone's Voice Memos) are now timed as a browser and ffmpeg play them: the
  bars were 12 ms early on such an mp3 (33 ms at 16 kHz) and 48 ms late on such an m4a.
- A recording of an hour reads in less memory (9.6 GB at its peak before, 7.1 GB now, for 58
  minutes at 48 kHz).

## 0.2.1

- Where the beat model counts a first beat in the middle of a bar, the two half bars are one bar
  again (they threw every later bar off by one against the chart as played). On 100 recorded songs
  the bar lines found rise from 93.4% to 94.2%.
- The tempo is the mean of the regular bars, not the middle bar on the model's 20 ms grid: 131, not
  130, for a song at 130.9.

## 0.2.0

- Reads mp3, m4a, mp4, aac, flac and wma as well as `.wav` (through Windows' own decoders), at any
  sample rate. The silence an mp3 or AAC encoder puts before the music is taken out where the file
  says how long it is, so a bar's time is the time a player shows.
- The reader is a command of its own: `dettato <recording> [-o <x.lys>] [--force]` writes the chart
  beside the recording, says on stderr what it is doing and why it failed, and never writes over a
  chart without `--force`. `dettato --version --json` gives the version, the protocol and the kinds
  of file read; the extension hands them to Lily# with the reader's path.
- A recording that cannot be read is said so in words (shorter than 5 seconds, no bar lines found,
  not a recording) instead of an exit code.

## 0.1.0

- The first package: the reader (`dettato.exe`, with its two models) and one command,
  **Dettato: Transcribe Recording to Lily# Chord Chart (.lys)…**, which reads a `.wav` and opens
  the chart written beside it.
