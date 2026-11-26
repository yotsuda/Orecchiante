# Changelog

Notable changes to Orecchiante (Dettato until 0.10.2) are recorded here, newest first.

## 0.15.0

- A voice staff: when the recording has singing, the chart has a `vocal` part above the bass, the
  melody read from the separated vocal part (pYIN, every note on the chart's eighths, a note held over
  a bar line tied). On the sung notes of 99 RWC songs, about 73% of the notes start on the right 16th;
  the pitch is a guide. An instrumental's lead is often read as the melody; delete the part if not
  wanted.
- Each note is sung "ら" (`singer on vocal sings "ら"`), so that whatever reads the singing (Lily#'s
  timeline, Shosa's dances) has a syllable on every note until the words are written in.
- The file beside the chart keeps each bar's melody as it was read (`vocal`).

## 0.14.0

- A bass staff: when the recording has a bass, the chart has a `bass` part under the chords, read
  from the separated bass part (pYIN, rounded onto the chart's 16ths; a note held over a bar line is
  tied unless the bass strikes it again). On the owner's ear-copied bass parts of 85 songs, 79% of
  the notes start on the right 16th and 68% have the right pitch too. A section played more than
  once is written as it is first played.
- A drum staff: when the recording has drums, a `kit` part read from the separated drum part by how
  much each band grows louder (hi-hat, snare, bass drum). A first try, with no truth yet to measure
  it on. Written in grooves: the song's commonest bars are found and written once as phrases (one
  line a drum, `phrase hatsA { hh8 8 8 8 8 8 8 8 | }`), each bar as its groove's name, `%` for a bar
  the same as the one before; a bar near no groove is written out.
- The file beside the chart keeps each bar's bass and drums as they were read (`bass`, `drums`), so
  that a bar put right in Lily# can be reported against them.
- No bass heard, no bass staff (the part is more than 21 dB quieter than the recording); no drums, no drum staff (30 dB).

## 0.13.2

- Sections are cut a little more readily. On the owner's Transcribe! sections, 70.7 → 72.5% of
  the heads are found where the owner put them.

## 0.13.1

- A section after a riff that repeats every two bars is no longer moved a bar late to line up with
  that riff (今宵の月のように's last section). Repeats are lined up only when their chords match at
  a single offset.

## 0.13.0

- Sections line up with the tune: a section that begins as another one begins (the same chords
  from its head) is cut at the same place in the tune, and the heads of a tune played several
  times are put in line together. Singing that comes in halfway through a bar begins the section
  where the chords change, and a section of 8 or 16 bars with one bar more before the music rises
  is no longer cut short. On the owner's Transcribe! sections, 69.8 → 71.0% of the heads are found
  where the owner put them.

## 0.12.0

- The bass note under a chord (`F#/A#`) is heard in the separated bass part, not in the whole
  recording's low notes: on RWC's songs the bass notes are right a point more often (65.1 → 66.2%
  and 66.6 → 67.5%). The chords' roots are as before.

## 0.11.0

- Dettato is now Orecchiante (Italian for one who plays by ear): another project had published a
  transcriber named Dettato. The extension is `yotsuda.orecchiante`, the reader `orecchiante.exe`, the
  file beside a chart `<name>.orecchiante.json` (its format number under `"orecchiante"`), and the
  settings `ORECCHIANTE_*`. Files named `.dettato.json` are no longer read: convert the recording again.

## 0.10.2

- The chart's chord row is named `prog` (it was `harmony`), the name Lily# gives it in a new chart.
  Charts written before keep their name.

## 0.10.1

- Converting a recording again failed in a OneDrive folder ("Access to the path '….parts' is
  denied"): OneDrive marks its folders read-only, and Windows does not remove a read-only folder.
  The mark is now taken off first, and the parts there before are kept until the new ones are in
  their place (that failure had emptied them).

## 0.10.0

- The sections also listen to the drums: how loud they are, and how their pattern in the bar changes
  across a bar line (a fill, a new groove), from the drums already separated for the parts. On 137
  of the owner's songs not used to choose, 70% of the section starts fall on the owner's (66% in
  0.9.0).
- Once in a while Windows refused to write one of the parts' .flac files when the six were written
  at once; such a part is now written again on its own.

## 0.9.0

- Sections are chosen together: every bar line gets a score from its cues (where singing comes in or
  stops, phrases said again, the chords across it, how the sound changes) and those of the bars
  around it, weighed as the owner marks sections in Transcribe!, and the sections take the lengths
  the owner's do (mostly eight and sixteen bars). Fewer, longer sections: on 137 of the owner's
  songs not used to choose, 66% of the section starts fall on the owner's (38% before).



## 0.8.12

- Finer progress while a recording is read: how much of the parts is separated, by 5% (most of the
  time), with the step the reader is on, and a line while the models open.


## 0.8.11

- A `break` only where a section ends, no longer after every four bars: a section begins a line, and
  Lily# fills the lines between (the text still has four bars a line).


## 0.8.10

- Fewer sections cut in the middle of a verse: where singing begins, the eight bars after it are not
  cut where the singing only pauses, when the same eight bars are played elsewhere in the song.


## 0.8.9

- Fewer sections cut in the wrong place: a section no longer begins on a chord held over from the
  bar before, but where the home chord arrives up to two bars later (a verse of eight bars and two
  more), and a section of one bar joins the one before.


## 0.8.8

- A note above the first section: Ctrl+. on a section's name offers Join Sections (Lily#), to
  join it with the next one, or all the sections selected.


## 0.8.7

- The note above the first chords also says what Shift+Ctrl+Up/Down does on a chord with `@or`:
  it swaps in the other chord and plays it.


## 0.8.6

- A chord the reader doubts is written with the one it would have said next, `F@or(Bm)` (was
  `F@todo("or Bm?")`): in Lily#, Ctrl+Shift+Up/Down swaps the two and plays the one brought forward,
  and a quick fix keeps one.
- A bar of three beats between bars of four is written `time 3/4`, as 0.8.5 writes a bar of two.


## 0.8.5

- A bar of two beats between bars of four is written `time 2/4` at its head, and the bar after it
  `time 4/4` (the owner: "Dettato should write it in"). In 糸 the bar of two near the end now reads
  `time 2/4 G | time 4/4 C#m7(b5)`, so Lily# plays it at the pace of the recording. In
  `x.dettato.json` the bar has two `beats` and `"time": "2/4"`.


## 0.8.4

- Where the beat tracker counts bars of four beats as bars of two for a while, they are put back
  together two by two where the tracker is surest a bar begins, not simply from the first; one bar of
  two beats may be left between them. In 糸 the bars after the six-beat bar near the end (4 + 2) now
  fall where the music's bars do, not two beats off. The bar of two beats is written as a bar of the
  chart's meter (no `time 2/4`: of such bars in RWC's songs, few are true). Against RWC's annotated
  bar lines: 93.8 → 94.1% in the songs used to choose, 95.3 → 95.1% in the others.

## 0.8.3

- A section whose tempo is its own has it written at its head (`tempo 4 = 100.2`): a slower intro,
  or songs at different tempos in one recording. Only where the section's bars agree with each other
  and the step is not one of two, three halves or four thirds (those are bars read wrong). Of RWC's
  100 songs two get one; songs at a steady tempo get none. The chords, the bars and the file's tempo
  are the same; the file has the tempo on the section's first bar (`tempo`).

## 0.8.2

- Sections inside a long stretch count their phrases of four bars from where singing last began or
  stopped, not from where the key or the loudness turned (those turns fall in the middle of a section
  more often than at its start). Against the structure annotated for RWC's songs, a few more section
  starts are found in the songs used to choose (66.1 → 66.6%), as many in the others (62.0 → 61.9%);
  the chords and the bars are the same.

## 0.8.1

- More section starts inside singing: a long stretch is also cut after every eight bars where a new
  phrase begins that comes back elsewhere (a verse's first half, then its second), but not where the
  second half begins as the first did. Against the structure annotated for RWC's songs, more of the
  turns inside singing are found (a verse's A to B 50 → 60% of the songs used to choose, 47 → 55% of
  the others); the chords and the bars are the same.
- No section is named R any more: its second playing, `R2`, read in Lily# as a rest, and the chart
  did not check (songs with eighteen sections or more).

## 0.8.0

- Six parts in place of four: guitar and piano are separated out of `other` too (`guitar.flac`,
  `piano.flac`, after the four in `parts`). Demucs's `htdemucs_6s` (MIT; `bin/htdemucs_6s-mm8.onnx`, 64 MB,
  in place of `htdemucs-mm8.onnx`). Electric guitars come out well; acoustic guitars and pianos are
  often split between the two. `other` keeps what none of the six takes, so that all parts but one are
  the recording without that one. The reading takes as long; the files are about a quarter larger.
- Where singing comes in and stops is heard from these vocals: the chords and the bars are the same, and
  a third of songs have some section starts elsewhere (as many right as before).

## 0.7.0

- The recording's four parts are written beside the chart, to hear one at a time while transcribing:
  `<name>.parts/drums.flac`, `bass.flac`, `other.flac` and `vocals.flac` (mono FLAC, on the recording's
  seconds), named in `<name>.dettato.json` as `parts`. They are what the singing was already separated
  from, so the reading takes no longer; a song of five minutes gives about 30 MB. The charts are the same.

## 0.6.1

- Half the memory while the singing is separated (a song of three and a half minutes peaked at 8.6 GB,
  16.2 GB before), in the same time: the beats and the chords are read meanwhile. The charts are the same.
- On a computer with less memory, fewer pieces of the recording are separated at once (slower, the same chart).

## 0.6.0

- Where singing comes in and stops is heard, and a section begins there: the vocals are separated by
  Demucs (htdemucs, MIT; `bin/htdemucs-mm8.onnx`, 85 MB) and how loud they are tells which bars sing.
  A recording with no singing (the vocals far quieter than the rest) has its sections as before.
- Reading takes longer: the separation is most of it (about half a minute for a song of three and a
  half minutes, more on fewer cores).

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
