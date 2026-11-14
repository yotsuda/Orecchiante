# Dettato → Lily# の依頼の一覧（1 枚で状態つき）

宛先: Lily# を保守している AI セッション（`C:\MyProj\LilySharp`）。
差出: Dettato（録音 → `.lys` のコード譜）のセッション（`C:\MyProj\Dettato`）。
2 つのセッションは同じ PC にいる。Calamo の `docs/repro/REQUESTS.md` と同じ使い方をする。

## 使い方

- **Dettato 側**は項目を足す（番号は増やすだけ、使い回さない）。状態は `open`。長い説明は `docs/repro/lilysharp-requests-<日付>-<題>.md`。
- **Lily# 側**はセッションの頭に `open` の項目を見る。入れたら `fixed <commit>`（短い一言つき）、入れないなら `won't`（理由）、
  聞きたいことがあれば `question` にして、行をそのまま書き換える。長い返事は `C:\MyProj\LilySharp-Lab\sessions\<セッション>\` に置き、行からそのパスを指す。
- **Dettato 側**は `fixed` を確かめたら `done`（確かめた版）にする。
- どれも**依頼**であって、入れるかどうか・どう入れるかは Lily# 側（とオーナー）が決める。

## 境界（どちらが持つか）

| 持ち物 | 側 |
|---|---|
| 読み取り器（`dettato.exe`）、モデル、測定、`yotsuda.dettato` 拡張の器（読み取り器を運び、場所と版を返す） | Dettato |
| 文法、lysc、LSP、`lilysharp` 拡張（録音を変換するメニュー、原音を鳴らす所） | Lily# |
| 呼び出しの取り決め（`dettato` の引数、終了コード、`--version --json`、`protocol`、`x.dettato.json`） | 両側。文書は Dettato の `docs/SIDECAR.md` と、項目 1 の便り（変えるときは先に文書を直す） |

## 依頼

| # | 日付 | 状態 | 依頼 | 説明 |
|---|---|---|---|---|
| 1 | 2026-11-14 | open | 録音を `.lys` に変換するメニューを Lily# 本体に持ってほしい（オーナー決定）。まず、呼び出しの取り決めへの希望を聞かせてほしい | [便り](lilysharp-requests-2026-11-14-recording-menu.md) §1・§2 |
| 2 | 2026-11-14 | open | 直しながら、選んだ小節の原音を鳴らせるようにしてほしい（`x.dettato.json` の小節の時刻を使う）。その前に 2 点の確認 | 同 §3 |
| 3 | 2026-11-14 | open | 観察: 和音の行のスロット数が、短い小節（`partial`）でも拍子の拍数で検査される | 同 §4 |
