# Orecchiante → Lily#: 歌詞を一覧から選ぶ・自分の言葉で探す（依頼 28）

2026-11-27、Orecchiante のセッションから。オーナー「見つからないとき、ユーザーが正しい曲名を入力しないと見つからないのは厳しい。
近いものをいくつか出して選んでもらうとか、ユーザーが好きなキーワードで検索できるとか、なにか工夫がほしい」。
入れるかどうか、どう入れるかは Lily# とオーナーの判断。

## 1. Orecchiante 側（0.18.0 に入れた、未公開）

- `orecchiante lyrics <録音> --list [--title <曲名>] [--artist <アーティスト>] [--query <言葉>]`
  - 何も書かない。stdout に JSON 1 つ:
    ```
    {"title": "wab", "artist": null, "query": null, "seconds": 209,
     "songs": [
      {"id": 17618460, "title": "We're An American Band", "artist": "Grand Funk",
       "album": "We're An American Band PT-SHM", "seconds": 208.1, "timed": true, "words": true},
      ...]}
    ```
    `title`・`artist` は探した名前（タグか、ファイル名か、`--title`・`--artist`）、`seconds` は録音の長さ、`songs` は最大 20。
    `timed`＝行ごとの時刻がある、`words`＝歌詞がある（LRCLIB が「歌のない曲」と言うものは false）。
  - `--query` がなければ、曲名とアーティストで LRCLIB を探せるだけ探す（両方で・両方を語として・曲名の語だけで）。
    `--query` があれば、その言葉だけで探す（曲名・アーティスト・アルバムの語が対象。**歌詞の本文は探せない**＝LRCLIB の検索の仕様）。
  - **長さで切らない**（±2 秒の外も出す）。並びは ±2 秒以内が先、歌詞があるもの、時刻つき、長さが近い順。
  - 終了コード: 0（0 件でも 0）／2 引数／3 LRCLIB に届かない。stderr は今までどおり 1 行目が「<曲名>[ by <アーティスト>], m:ss」。
- `orecchiante lyrics <録音> --id <LRCLIB の番号> [-o <x.lrc>] [--force]`
  - 選ばれた 1 つを `<名前>.lrc` に書く（長さに関わらず）。stdout・stderr・終了コードは今の `lyrics` と同じ
    （0 書けた／1 その番号がない・歌のない曲／2 引数／3 届かない）。
- `orecchiante --version --json` の `commands` に `"lyrics-choose"` が入る（これで対応しているかを見分けられる）。
- 試した: タグのない `wab.mp3` はファイル名「wab」で 20 件出るが全部別の曲。`--query "american band grand funk"` で
  原曲が 1〜3 番目（208 s、録音は 209 s）。`--id 17618460` で `.lrc` が書けた。

## 2. 頼みたいこと

「Convert Recording to Lily# (.lys)…」の歌詞の流れで、曲名を正しく打たなくても選べるようにしてほしい。案（形は任せる）:

1. **見つからなかったとき（終了コード 1）**: 今の「Correct Title and Artist…」の代わりに（または並べて）、
   `--list` の結果を QuickPick で出す。各行は「曲名 — アーティスト」、説明に「アルバム · 3:28（録音より 1 秒短い）· timed」のように。
   長さが ±2 秒より離れたもの（ラジオ版・ライブ・カバー）もそのまま出し、差を見せれば利用者が判断できる。`words: false` は出さないか、灰色に。
2. **一覧の最後に「Search with Other Words…」**: 入力欄（初期値は探した曲名とアーティスト）→ `--list --query <言葉>` → 一覧を出し直す。
   何度でも。0 件なら「見つからない」と言って欄に戻る。
3. **選んだら** `orecchiante lyrics <録音> --id <id>` → 今の「found」と同じ扱い（見つけた版を知らせて変換へ）。
4. （あると良い）**見つかったとき（終了コード 0）も、違う版だったら選び直せる**: 今の知らせ（「words from LRCLIB: … (LRCLIB 17618460)」）に
   「Choose Another…」を付け、押したら 1. の一覧へ（`--id` と `--force` で書き直す）。カバー版に当たったとき（今宵の月のように → 10-FEET）に効く。
5. 一覧を出す前・検索する前にも、送るもの（曲名・アーティスト・長さ／入力した言葉）と送り先は最初の箱で了承済み、という扱いでよいと思う
   （同じ変換の中の続き）。

`commands` に `lyrics-choose` がなければ今の流れのまま（読み取り器が古いときのためのコードは要らない、というオーナーの決定に合わせて、
一覧の道だけにしてもよい）。

## 3. Orecchiante 側が確かめること

入ったら、`scratch/e2e/lyrics25/go.ps1`（実物の VS Code、入れた Lily# と Orecchiante）で、タグのない `wab.mp3` →
見つからない → 一覧 → 自分の言葉で探す → 選ぶ → 譜面に歌詞、を確かめて `done` にする。
