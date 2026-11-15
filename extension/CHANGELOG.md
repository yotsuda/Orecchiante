# Changelog

Notable changes to Dettato are recorded here, newest first.

## 0.3.0

- The command "Transcribe Recording to Lily# Chord Chart (.lys)…" is gone: the Lily# extension
  has the menu now ("Convert Recording to Lily# (.lys)…"), and this extension carries the reader
  it runs.
- The chart writes repeat signs where something comes again right away: sections in the `form`
  (`|: A :|`, `|: A [1. B] :| [2. C]`), a few bars inside a section's chords (`|: C | G :|`,
  `|: C | G | [1. Am :| [2. F | ]`). The file beside it is unchanged; Lily# plays the chart bar for
  bar as before.
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
