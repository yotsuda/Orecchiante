# 依頼 20: Dettato は Orecchiante になった（Lily# の側の名前を揃えてほしい）

差出: Orecchiante（旧 Dettato）のセッション、2026-11-20。オーナー決定。

## なぜ

同じ「録音から採譜する」分野に、同名の kobimusic/dettato（録音 → MIDI、Apache-2.0、2026-11-16 に GitHub で公開、
Hugging Face に重み）が出ていた。こちらは未公開だったので、公開の前に名前を変えた。新しい名前は **Orecchiante**
（イタリア語で「楽譜を読まず耳で弾く人」）。GitHub・PyPI・VS Code Marketplace とも同名なし（2026-11-20）。

## Orecchiante 0.11.0 で変わったもの

| もの | 前 | 今 |
|---|---|---|
| 拡張の ID | `yotsuda.dettato` | `yotsuda.orecchiante` |
| 表示名 | Dettato Chord Chart Transcriber | Orecchiante Chord Chart Transcriber |
| 読み取り器 | `bin/dettato.exe`、`bin/dettato.json` | `bin/orecchiante.exe`、`bin/orecchiante.json` |
| 譜面の隣のファイル | `<stem>.dettato.json` | `<stem>.orecchiante.json` |
| その中の形式の番号 | `"dettato": 1` | `"orecchiante": 1`（無ければ 1、は前と同じ） |
| 環境変数 | `DETTATO_*` | `ORECCHIANTE_*` |

`activate()` が返すもの（読み取り器の場所と `--version --json`）の形、コマンドと引数（`read`・`--parts` など）、
形式の中身（SIDECAR.md）は変えていない。版は 0.11.0。

**古い `.dettato.json` は読まなくてよい**（オーナー: そのファイルはオーナーの PC にしかない。変換し直す）。
Orecchiante も書かない。

## Lily# でお願いしたいこと

1. `DETTATO_EXTENSION_ID` を `yotsuda.orecchiante` に。`DETTATO_MIN_VERSION` を `0.11.0` に。
2. 譜面の隣のファイルを `<stem>.orecchiante.json` に（`recordingCore.ts` の名前の組み立てと正規表現、`recording.ts` の
   `findFiles`・`endsWith`、`extension.ts` の `createFileSystemWatcher`、`dettatoCore.ts` の空き名の確かめ）。
   形式の番号は `o.orecchiante`（無ければ 1）。録音を付けるとき Lily# が書くファイルも同じ名前・同じ鍵で。
3. 変換した譜面の名前 `song (Dettato).lys` を `song (Orecchiante).lys` に（同じく `(Orecchiante 2)` …）。
4. 画面の文字（メニュー、知らせ、Practice の表示）と docs（`recording-design.md` など）の Dettato を Orecchiante に。
5. 内部の名前（`dettato.ts`・`dettatoCore.ts`、`lilysharp.dettatoAvailable`、`LILYSHARP_DETTATO_PATH`、`DETTATO_*` の定数）は
   Lily# 側の判断で。揃えるなら `LILYSHARP_ORECCHIANTE_PATH` など。

どれも依頼で、どう入れるかは Lily# 側（とオーナー）が決める。

## 順番

Orecchiante 0.11.0 はビルドと確かめを済ませてコミットしたが、まだ VS Code には入れていない（入れると、今の Lily# は
読み取り器を見つけられない）。Lily# の新しい版が入ったら、`yotsuda.dettato` を外して `yotsuda.orecchiante` を入れる。
行を `fixed` にしてもらえれば、Orecchiante 側で入れて確かめ、`done` にする。

リポジトリのフォルダはこの後 `C:\MyProj\Orecchiante` に変える（`REQUESTS.md` もそこへ移る）。
