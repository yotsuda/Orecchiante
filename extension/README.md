# Orecchiante Music Transcriber

Listens to a recording and writes it down as an editable score in [Lily#](https://yotsuda.github.io/LilySharp/)
text (`.lys`): tempo, meter, key, bar lines, sections and the chord of every bar, and, when the recording
has them, a bass staff, a drum staff and a voice staff with the song's words. The places it is unsure of
are marked in the text with `@todo`. It is a first draft to correct by ear, the way you would copy a song
by hand, only faster.

*Orecchiante* is Italian for one who plays by ear.

**Preview, open source** ([Apache-2.0](https://github.com/yotsuda/Orecchiante/blob/main/LICENSE)).
Source and issues: <https://github.com/yotsuda/Orecchiante>.

## Use

This extension carries the reader; the menu that runs it is the
[Lily#](https://marketplace.visualstudio.com/items?itemName=yotsuda.lilysharp) extension's.
Install both.

1. Right-click a recording (`.mp3`, `.m4a`, `.flac`, `.wav`, …) in the Explorer and choose
   **Convert Recording to Lily# (.lys)… (Preview)**, or run it from the Command Palette.
2. Lily# asks whether to fetch the song's words. If you say yes, it looks the song up in
   [LRCLIB](https://lrclib.net) (a free, open lyrics database) and lets you choose among the songs it
   finds, or search with other words.
3. After about 45 seconds for a three-and-a-half-minute song (on a 24-core PC), `x.lys` opens. It is written beside the recording,
   with `x.orecchiante.json` (when each bar begins in the recording, and what was heard in it) and
   `x.parts\` (the recording separated into drums, bass, vocals, guitar, piano and the rest).
4. Correct what it misheard. Lily# draws and plays the score as you edit, plays the recording beside
   it, and lets you turn each separated part on and off while you listen.

## What it writes

| | | How well (on recorded pop songs) |
|---|---|---|
| Chords | the chord of every bar, sections where the music turns | root right 87% of the time, root and major/minor 82%, 94% of the bar lines found |
| Bass | the bass line from the separated bass | 79% of the notes start on the right 16th, 68% with the right pitch too |
| Drums | hi-hat, snare and bass drum as step charts, grooves written once | a first try, not yet measured |
| Voice | the melody from the separated vocals | 73% of the notes start on the right 16th |
| Words | the lyrics put to the melody, a syllable a note (Japanese and English) | 83% (Japanese) and 72% (English) of the notes start on the right 16th |

Songs whose tempo changes much are not handled well yet. The numbers and how they were measured are
in the [repository's README](https://github.com/yotsuda/Orecchiante#readme).

## Privacy

The reading runs on your computer. The only thing sent over the network is the song's title, artist
and length to LRCLIB, and only when you choose to fetch the words.

## Requirements

- Windows x64.
- A recording Windows can decode: mp3, m4a, mp4, aac, flac, wma or wav, of 5 seconds or more.
- A Lily# version that reads step charts (the one after 0.17.1).

## Licenses

Orecchiante is under the Apache License 2.0. The models and libraries it carries are under their own
licenses: see `THIRD-PARTY-NOTICES.md`.
