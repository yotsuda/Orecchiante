# Dettato Chord Chart Transcriber

Reads a recording into an editable chord chart in [Lily#](https://yotsuda.github.io/LilySharp/)
text (`.lys`): tempo, meter, key, bar lines and the chord of every bar. The places it is unsure of
are marked in the text with `@todo`. Everything runs on your computer; nothing is sent anywhere.

**Preview, open source** ([Apache-2.0](https://github.com/yotsuda/Dettato/blob/main/LICENSE)).
Source and issues: <https://github.com/yotsuda/Dettato>.

## Use

1. Right-click a `.wav` in the Explorer, or run
   **Dettato: Transcribe Recording to Lily# Chord Chart (.lys)… (Preview)** from the Command
   Palette and pick one.
2. After some seconds (about five for a three-minute song) `x.lys` opens. It is written beside the
   recording, with `x.dettato.json` (when each bar begins in the recording) beside it. A chart
   that is already there is replaced only after you agree.
3. Correct what it misheard. With the
   [Lily#](https://marketplace.visualstudio.com/items?itemName=yotsuda.lilysharp) extension the
   chart is drawn and played as you edit.

## What to expect

On 100 recorded pop songs (the RWC Popular Music Database) the reader gets the root of the chord
right 87% of the time, root and major/minor 82%, and finds 93% of the bar lines. It is a first
draft to correct, not a finished chart. Songs whose tempo changes much, and bars without a chord
at the start of a song, are not handled yet. The numbers and how they were measured are in the
[repository's README](https://github.com/yotsuda/Dettato#readme).

## Requirements

- Windows x64.
- A `.wav` recording. Other formats (mp3, m4a, flac) are to come.

## Licenses

Dettato is under the Apache License 2.0. The models and libraries it carries are under their own
licenses: see `THIRD-PARTY-NOTICES.md`.
