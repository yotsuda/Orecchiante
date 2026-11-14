# Changelog

Notable changes to Dettato are recorded here, newest first.

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
