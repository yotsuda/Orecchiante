# Orecchiante → Lily#: macOS で録音を変換し、鳴らす（依頼 31）

2026-11-27、Orecchiante のセッションから。入れるかどうか、どう入れるかは Lily# とオーナーの判断。

## 1. Orecchiante がしたこと

オーナー「Orecchiante は macOS では動かせないの？」→「進めて」（2026-11-27）。Orecchiante 0.18.0 は macOS でも動く:

- 録音を読むのは macOS の AudioToolbox（`src/Orecchiante/Apple.cs`、ExtAudioFile）。読める種類は wav・mp3・m4a・mp4・aac・flac
  （**wma は読めない**。`orecchiante --version --json` の `inputs` が OS ごとに変わる）。曲名・アーティストのタグも AudioToolbox から。
- パートの FLAC は自前の書き手（`Flac.cs`、Windows でも同じ）。
- 拡張は 3 つのパッケージ: `win32-x64`・`darwin-arm64`・`darwin-x64`。`activate()` が返すものは今までと同じ（`path` は macOS では
  `bin/orecchiante`、拡張子なし）。
- 録音の頭の位置（エンコーダの無音）は CI の macOS で ffmpeg の解読と比べて確かめる（`tests/Orecchiante.Tests/AudioTests.cs`）。
  まだ実物の Mac では誰も触っていない。

Lily# の `package.json` の when（`lilysharp.orecchianteAvailable || isWindows`）は、Orecchiante の `activate()` が値を返せば
macOS でもメニューを出す＝**変換は Lily# に手を入れなくても macOS で動くはず**。

## 2. 頼みたいこと

1. **macOS で m4a・mp4・aac を鳴らす。** 今の Lily# は `recordingCore.ts` の `WINDOWS_AUDIO`（m4a・mp4・aac・wma）を Windows
   でだけ LSP で解読し、macOS では「a .m4a recording is read on Windows only」と断る。Orecchiante は macOS で m4a を変換
   できるので、**変換はできるのに録音を横で鳴らせない**。iPhone のボイスメモや Apple Music の書き出しは m4a なので、Mac の利用者には
   いちばん多い種類のはず。案: LSP の解読を macOS では AudioToolbox で（Orecchiante の `Apple.cs` の `Read` が ExtAudioFile を
   P/Invoke する最小の形。コピーは自由に）。wma は macOS に解読器がないので今のまま断ってよい。
2. **変換メニューに出す種類を Orecchiante の `inputs` に従わせる**（もう従っているなら不要）。`package.json` の when の拡張子の
   正規表現は wma を含むので、macOS で `.wma` を右クリックするとメニューが出て、Orecchiante が「not a kind of recording this
   reads」で終わる。
3. **（確かめ）macOS で、Orecchiante の `path`（拡張子なし）を実行できること。** Orecchiante の `extension.js` は実行の権利がなければ
   `chmod 755` してから返す。

## 3. Orecchiante 側が確かめること

オーナーか誰かが Mac で、Lily# と Orecchiante の macOS 版を入れて mp3 と m4a を変換し、譜面とパートが鳴ることを確かめたら `done`。
CI（`.github/workflows/ci.yml` の macos-latest）で通っているのは、合成した曲の通しの読み取りと、録音の頭の位置まで。
