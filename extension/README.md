# Orecchiante Chord Chart Transcriber

Reads a recording into an editable chord chart in [Lily#](https://yotsuda.github.io/LilySharp/)
text (`.lys`): tempo, meter, key, bar lines and the chord of every bar. The places it is unsure of
are marked in the text with `@todo`. Everything runs on your computer; nothing is sent anywhere.

**Preview, open source** ([Apache-2.0](https://github.com/yotsuda/Orecchiante/blob/main/LICENSE)).
Source and issues: <https://github.com/yotsuda/Orecchiante>.

## Use

This extension carries the reader; the menu that runs it is the
[Lily#](https://marketplace.visualstudio.com/items?itemName=yotsuda.lilysharp) extension's.
Install both.

1. Right-click a recording (`.mp3`, `.m4a`, `.flac`, `.wav`, …) in the Explorer and choose
   **Convert Recording to Lily# (.lys)… (Preview)**, or run it from the Command Palette.
2. After half a minute or so for a three-minute song (most of it spent finding where singing comes
   in and stops) `x.lys` opens. It is written beside the recording, with `x.orecchiante.json` (when each
   bar begins in the recording) beside it.
3. Correct what it misheard. Lily# draws and plays the chart as you edit, and can play the
   recording beside it.

## What to expect

On 100 recorded pop songs (the RWC Popular Music Database) the reader gets the root of the chord
right 87% of the time, root and major/minor 82%, and finds 94% of the bar lines. Sections begin
where the music turns: where singing comes in or stops, where the key or the loudness changes. It is a first
draft to correct, not a finished chart. Songs whose tempo changes much, and bars without a chord
at the start of a song, are not handled yet. The numbers and how they were measured are in the
[repository's README](https://github.com/yotsuda/Orecchiante#readme).

## Requirements

- Windows x64.
- A recording Windows can decode: mp3, m4a, mp4, aac, flac, wma or wav, of 5 seconds or more.

## Licenses

Orecchiante is under the Apache License 2.0. The models and libraries it carries are under their own
licenses: see `THIRD-PARTY-NOTICES.md`.
