# Orecchiante → Lily#: 録音の変換で歌詞も取る（依頼 25）

2026-11-26、Orecchiante のセッションから。Shosa の依頼 1（録音の歌のメロディと歌詞を `.lys` に、オーナーの依頼）の
② を Orecchiante 0.17.0（`a502e88`、未公開＝オーナーのタグ待ち）で入れたので、Lily# のメニューからも使えるようにしたい。
入れるかどうか、どう入れるかは Lily# とオーナーの判断。

## 1. いまの形（Orecchiante 側、もう入っている）

- `orecchiante lyrics <録音> [-o <x.lrc>] [--title <曲名>] [--artist <アーティスト>] [--force]`（0.16.0）が、
  LRCLIB（lrclib.net、キー不要の歌詞データベース）から歌詞を取り、録音の隣に `<名前>.lrc` を書く。
  曲名・アーティスト・長さは録音のタグから取る（タグに曲名がなければファイル名）。長さが ±2 秒以内の曲だけを取る。
  - stdout: 書いた `.lrc` のパス 1 行。stderr: 段ごとに 1 行、失敗したら最後の行が `orecchiante: <理由>`。
  - 終了コード: 0 書けた／1 見つからない（または LRCLIB が「歌のない曲」と言う）／2 引数（`.lrc` が既にある、を含む）／3 LRCLIB に届かない。
  - 通信はこのコマンドを走らせたときだけ（オーナー決定、2026-11-26）。歌詞サイトは読まない。
  - `orecchiante --version --json` の `commands` に `"lyrics"` が入っている（0.16.0 から）＝これで対応しているかを見分けられる。
- `orecchiante <録音>`（変換、0.17.0）は、録音の隣に `<名前>.lrc` があり、歌詞が日本語なら、それを分けた歌に合わせる。
  1 モーラ＝1 音符にし、歌の段の下に音節（かな）を書く。英語の歌詞はまだ合わせず、今までどおり "la" で歌う。
  `.lrc` がなければ 0.15.0 と同じ（pYIN の音符に "ら"／"la"）。引数・終了コード・`protocol` は変えていない。

## 2. 頼みたいこと

**「Convert Recording to Lily# (.lys)…」で、変換の前に歌詞を取れるようにしてほしい。** いまは利用者が先にターミナルで
`orecchiante lyrics` を走らせ、`.lrc` を録音の隣に置いておく必要がある。PC に詳しくない人にはこれができない
（歌詞を取る段をコマンドにしたのも、歌詞サイトから貼るのが難しいから）。

案（形は任せる）:

1. 録音の隣に `<名前>.lrc` があれば、それを使う（何も聞かない。変換が自分で読む）。
2. なければ、変換の前に「LRCLIB から歌詞を取るか」を聞く。**送るもの（曲名・アーティスト・長さ）と送り先（lrclib.net）を示し、
   利用者が選んだときだけ** `orecchiante lyrics <録音>` を走らせる。黙って通信しない（Orecchiante の拡張の説明は「no cloud」と書いていて、
   その扱いもオーナーの未決）。「今後も聞かない」の選択肢があると良いかもしれない。
3. 終了コードごとに:
   - 0 → そのまま変換へ（stderr の 2 行目に「<曲名> by <アーティスト> (<アルバム>), <長さ>、lines with their times (LRCLIB <id>)」が出る。見せると、カバー版などの取り違えに気づける）。
   - 1 → 「見つからなかった」と言い、曲名・アーティストを直して取り直す（`--title`・`--artist`）か、歌詞なしで変換するかを選ばせる。
     タグにアーティストがないと長さだけで選ぶので、カバー版に当たることがある（オーナーの「今宵の月のように」は 10-FEET のカバーに当たった）。
   - 3 → 「LRCLIB に届かない」と言い、歌詞なしで変換する。
   - 2 → 引数の誤り（`.lrc` が既にある場合は 1. の扱いなので、ここには来ないはず）。
4. （あると良い）手元の `.lrc` や `.txt`（1 行 1 行の歌詞）を選べる道。選んだファイルを録音の隣に `<名前>.lrc` として写せば、変換が読む。
   LRCLIB 以外の `.lrc` には、譜面の頭の著作権表示が「Text from the .lrc beside the recording」になる。

## 3. 書き出す譜面（見てほしい所）

0.17.0 は歌詞があると次の形で書く（`C:\MyProj\Orecchiante\scratch\e2e\koyoi\今宵の月のように.lys`、`lysc check` は通る、
`lysc timeline` の `sung` に 407 音節）:

```
// read from 今宵の月のように.mp3 by orecchiante
// Lyrics: "今宵の月のように" as performed by エレファントカシマシ (明日に向かって走れ-月夜の歌-)
// The words are copyright of their writers and publishers, who reserve all rights.
// Text from LRCLIB (https://lrclib.net), entry 10699791, for personal use only: do not share or publish the words with this chart.
...
part vocal { instrument tenor  lyrics words }
singer on vocal
...
section A2 {
  chords prog { ... }
  vocal {
    c'8 b8 a8 g8 b4. d'8~ | d'8 b8 ais8 g8 b4. r8 | ...
  }
  lyrics words {
    つ ま で も つ | づ く の か | は き す て て ね こ | ろ ん だ お |
  }
  ...
}
form { A A2 B C C2 D A3 B2 C3 C4 D2 A4 A5 E }
score { chords prog  staff vocal  lyrics words  staff bass  staff kit }
```

- 歌のある section は、演奏ごとに別の section にした（A A2 A3 …）。1 番と 2 番で歌詞も節回しも違うため。verse を使うと 2 番の歌詞が
  1 番の音符に載る。
- 歌詞はかな。漢字の語は複数の音符にまたがり、`漢[かん]` は 1 つの音符に付く書き方なので使っていない。っは前の音節に（かっ）、
  ーは前の母音（と お）、同じ 16 分に落ちた 2 モーラは `~`（elision）。音節のない小節は `|` だけ（`| | き |`）。
- 「この形の方が Lily# で扱いやすい」があれば教えてほしい（漢字で刷りたい場合の書き方など）。紙と再生（歌声）は Orecchiante 側では
  `lysc check` と `lysc timeline` しか見ていない。

## 4. Orecchiante 側が確かめること

入ったら、Lily# のメニューから録音を変換し、`.lrc` が取れて譜面に歌詞が入ることを確かめて `done` にする（オーナーの実物での確かめも頼む）。
