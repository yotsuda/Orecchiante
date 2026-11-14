# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（C2 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

C2（繰り返しを見つける）が終わった。結論は 2 つ。

1. **繰り返しの照合は、誤りを減らさず、印も良くしない。**（当初の狙い。`rwc-dev` で測って捨てた）
2. **繰り返しは `section` / `form` に書くようにした。** 実録音の譜面は 3 分の 1 短くなり、精度は変わらない。

### 1. 照合が効かなかった理由（数字は README の「What is said again」）

- 読んだコード列で繰り返しを対にすると、緩い照合（4 小節で 75% 一致）では食い違いの 4 分の 3 が本当の違いで、
  多数決は読みを悪くする（誤り 15.0% → 19.7%）。厳しい照合（90%）では食い違いが 0.2% しかなく、直すものがない。
- **正解の繰り返しを与えた上限**でも、多数決で誤り 15.0% → 13.5%、モデルの確率を繰り返し同士で平均して読み直して
  長短 81.8% → 83.7%、印は「下から 30% で 77.8% → 78.6%」。
- モデルの確率の近さで対にして平均（重なり 0.9、4 小節）: 長短 82.5%（+0.7）、ルートは同じ。入れていない。
- モデルは同じ箇所を毎回同じように読む（同じように間違える）。繰り返しの間で違うのは、ほとんどが音楽の方。

### 2. section / form（`read_chords.py` の `find_form`・`fold`、定数 `SAID_AGAIN`・`SECTION_BARS`）

- 4 小節以上の並びが、拍ごとのルート＋種類（七の和音まで）で 95% 以上同じなら同じセクション。いちばん小節を
  節約できる並びから順に取り、残りは 1 回きりのセクション。いつも続けて出る 2 つは 1 つにまとめる。
- 同じセクションの各回は同じ読みにそろえる（拍ごとに、確信度の合計が大きい方）。ルートか長短が食い違った拍には
  印を付け、別候補は食い違った方の読み。`.dettato.json` は全小節を時刻つきで残し、`section`・`barOfSection`・`form` を足した。
- 繰り返しが 1 つもない譜面は、今までどおり section なしで書く。両方の読み取り器（自作・`--ears`）で動く。
- `SAID_AGAIN = 0.95` は `rwc-dev` で選んだ。0.90 はルート −0.4。「ルート＋長短だけで同じとみなす」は合成曲の
  七の和音を 2〜4 ポイント落とす（七の和音だけが違うセクションをまとめてしまう）。

| `--ears`（crema） | `rwc-dev` | `rwc-test` | `rwc-p` | seed1・7・11 |
|---|---|---|---|---|
| 書く小節 / 全小節 | 61.9% | 68.1% | 64.9% | 69〜70% |
| 同、正解のコードを同じ手順に通すと | 69.5% | 76.4% | 72.7% | 54〜55% |
| 1 曲のセクション数（うち 1〜2 小節） | 11.5（2.4） | 9.0（1.8） | 10.2（2.1） | 3.4〜3.9（0.7〜0.8） |
| 同、正解のコード | 9.2（1.5） | 7.5（1.5） | 8.3（1.5） | 2.0〜2.1（0） |
| ルート / 長短 / 種類（section の前 → 後） | 86.0 / 81.8 / 67.7 → 同じ | 87.1 / 82.0 / 69.0 → 87.0 / 81.9 / 68.8 | 86.6 / 81.9 / 68.4 → 86.5 / 81.8 / 68.3 | ±0.1 |
| 印の割合 → 拾う誤り | 29.3% → 76.7% | 25.7% → 70.0% | 27.5% → 73.3% | 17.5% → 69.8% |

- 印は section の前より 0.6〜1.0 ポイント増えた（食い違いの印）。拾う誤りの増え方は、確信度で同じ数だけ増やしたのと同程度。
- 自作モデルの読み取り器（合成曲）はルート・長短が最大 −0.2、種類が最大 −0.2（seed11 のベースは 86.6% → 85.5%）。
  印は 22.9% → 24.4%（拾う誤りは 78.9% のまま）。全表は README。`measure.py all` は 1292 枚すべて `lysc check` 通過。
- **残る読みにくさ:** 実録音は 1 曲 10 セクション前後で、うち 2 つは 1〜2 小節（繰り返しの間で 1 小節だけ読みが違うと、
  そこが独立したセクションになる）。正解のコードでも 8 セクション（バンドは同じ節を同じには弾かない）。
  名前は出てきた順の A, B, C…（Intro・Verse などの推測はしていない）。

### コードモデル（B3 の結論。変わっていない）

配るものに入れるのは **crema 0.2.0**（BSD 2-Clause。重みもライセンスの内と作者が issue 42 で明言、6.4 MB、ONNX にできる）。
`measure.py --ears` の既定。値段（`use_ears()`）と印の式（`SURE_OF`・`DOUBT_EARS` 0.84）は BTC で選んだ値のまま。
BTC を 2 つ目の耳にすると `rwc-test` で 88.4% / 83.5%、印も良くなるが、重みの扱いが不明（未決）。
モデル 4 つの比較表は README の「Which model hears the chords」。

### 道具（`tools/`）

- `read_chords.py`: `find_form(keys, alike)`（セクション探し）、`fold(bars, beats, alike)`（読みをそろえて印）、
  `beat_names(bar, beats)`、`lys()` は section / form を書く。**`ALIKE` はコード同士の似かたの行列で、探索が使う。
  同じ名前の定数を足さない**（今回これで一度数字を壊した）。
- `measure.py form <セット> [--ears] [-v]`: 書く小節の割合とセクション数（読み・正解）。`all` に「The sections」の表を足した。
- 前からのもの: `crema_chords.py`（`.venv-crema`）・`chordmini_chords.py`・`btc_chords.py --probs`・`track_beats.py`（`.venv-bt`）、
  `rwc_truth.py`・`gen_chords.py`・`train_chords.py`・`score_chords.py`・`sample_songs.py`。

## 次の一手

**ROADMAP の C1（曲の頭）。** いまは「1 小節目は最初の小節頭から」で、その前の音は譜面に入らない。順に:

1. `rwc-dev` で、最初に読んだ小節線より前・最後より後に、正解のコードがどれだけあるかを数える（失っている時間）。
   Beat This! の最初の拍より前に小節線を延ばせば拾えるかを見る。
2. 生成器に `--pickup`（弱起）と頭の無音を足し、合成曲で測る。Lily# の弱起の書き方（`partial` 相当）は
   `C:\MyProj\LilySharp\docs\GRAMMAR_FOR_LLM.md` で確かめる。
3. `.lys` に頭の休みと弱起を書き、`lysc check` に通す。

その後は D1（`.dettato.json` の仕様。`section`・`barOfSection`・`form` は今回入れた）。C3・C4 は ROADMAP。

やり残し（急がない）:

- section の読みやすさ: 1〜2 小節のセクションを減らす手（1 番かっこ `|: A [1. B] :| [2. C]` に書く、短い違いは
  前後に吸収する）は試していない。セクションに Intro などの名前を付けることもしていない。
- `crema_chords.py` は TensorFlow の入った 3 つ目の venv で動く。配る形（D2）では ONNX にする（書き出せることは確認済み）。
  CQT を librosa なしで作る所が残る。
- crema の `chord_root`・`chord_bass`・`chord_pitch` の出力は未使用。ベース音はいまも自作の特徴量から（bass 63〜64%）。
- 値段と印の式を crema＋BTC の平均用に選び直すことはしていない（BTC を配れるか決まってから）。
- README 冒頭の「Status」は自作の読み取り器の頃の文のまま（実録音でルート 4 分の 3）。`--ears` の数字に合わせて書き直す。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 3 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0）が Beat This!・BTC・ChordMini。`.venv-crema`（tensorflow、tf-keras、
  crema 0.2.0、`setuptools<81`、tf2onnx、onnxruntime）が crema。作り方は README。
  素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、
  `$env:DETTATO_LYSC='C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe'`。
  `.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'` も（設定しなくても動いた）。
  `make` で作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）と
  `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- 動作確認の 1 コマンド（20 秒。数字は上の表と README）:
  `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears`
  → seed1 ルート 95.1%、`rwc-dev` 86.0% / 81.8%、`rwc-test` 87.0% / 81.9%。
  自作の読み取り器の方は `table seed1 seed7 seed11 seed21 seed31 rwc-p`（特徴量があれば数分）。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 7 秒。`sweep` は 1 通り 4〜7 秒。`all` は 15 分ほど
  （自作の読み取り器で `rwc-p` を読む表が 1 枚 2 分）。RWC の特徴量の作り直しは 20 分。
  Beat This! は CPU で 1 曲 5〜8 秒、BTC は 1〜3 秒、crema は 0.8 秒、ChordMini は 0.4 秒。
- 2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
  止めるときは、コマンドラインに `measure.py all`（または `sweep`）を含む python.exe を止める。
- `lysc check` は 1 枚ずつしか受け取らない。合否は終了コードと `warning` / `: error`（`measure.py` の `check`）。
- `C:\MyProj` 全体の再帰検索は固まる。Windows はフォルダ名の大文字小文字を区別しない（`RWC-P` と `rwc-p` は同じ）。
- Zenodo は 1 接続 350 kB/s ほど。`curl -r` で分けて並行に取る。止まる接続があるので `--speed-limit` を付ける。

### commit の外にあるもの（消えたら作り直す）

| もの | 作り方 |
|---|---|
| 合成曲のセット 13 個（`data/synth/seed*` の wav、`scratch/alt-*`） | `measure.py make`（`measure.py sets` が一覧） |
| `models/chords.joblib` | `train_chords.py data\synth\seed100 --features scratch\feat-h31 --out models\chords.joblib` |
| `scratch/feat-h31/*.npz`（特徴量。実録音は `real_RWC_P*.npz`） | `measure.py` が初回に作る |
| `scratch/real/rwc-p/`（wav 100 個と truth）、`rwc-annotations/`、`RWC-P.zip`（3.79 GB） | DATASETS.md の手順 |
| `scratch/real/beat_this_annotations/`、`scratch/btc/`・`scratch/chordmini/`（clone）、`.venv-bt/`、`.venv-crema/` | README |
| `scratch/real/beats-bt-folds/*.beats`、`scratch/beats-bt/seed*/`（合成曲 5 セットを `final0` で） | `track_beats.py`（README） |
| `scratch/real/chords-crema/`、`scratch/chords-crema/seed*/` | `crema_chords.py <セットのフォルダ> --out <ここ>` |
| `scratch/real/chords-btc/`、`scratch/chords-btc/seed*/` | `btc_chords.py scratch\btc <セットのフォルダ> --out <ここ> --probs` |
| `scratch/real/chords-cmbtc/`・`chords-cm2e1d/`、`scratch/chords-cm*/seed*/` | `chordmini_chords.py scratch\chordmini <セット> --model btc\|2e1d --out <ここ>` |
| `scratch/**/chords-crema+btc/`・`chords-crema+cmbtc/`（確率の平均） | `scratch/b3avg.py crema btc`（commit の外。消えたら 20 行で書ける） |

`scratch/c2.py`（繰り返しの対と多数決。`--oracle` で正解の対）・`c2pool.py`（確率を平均して読み直す。`--oracle`・`--soft`）・
`b3two.py`・`b3fit.py`・`b3avg.py` は commit の外。`c2*.py` は `find_form` が入る前の読み取り器で測った
（いま走らせると section でそろえた後の読みを相手にする）。
`scratch/` の古いスクリプトとログ、`scratch/read-all/`・`scratch/c2out/`（書いた譜面）、`scratch/cmtest/` は消してよい。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100、`held/seed101` | 合成曲の学習用・定数を選ぶ用 | 自作モデルはこれしか聞いていない。公開モデルの出力はまだ作っていない |
| seed1・7・11、seed21・31、`still/`・`drift5/`・`tim/`、`samples` | 合成曲のテスト | 定数を選ぶときに数字を見たことがある。`SLOW_TWO` は seed7・11 の曲ごとの行を見て決めた。**`SAID_AGAIN` と「種類まで同じ」は seed1・seed11 の集計も見て決めた** |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | 値段、`SURE_OF`、`DOUBT_EARS`（BTC で）、2 つ目の耳の重み、`SAID_AGAIN` はここで選んだ。C2 の照合の実験もここだけ |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 集計だけを見ている。B3 では 4 モデルの集計を並べて見たうえで crema に決めた（決め手はライセンス）。C2 では決めた後に数えただけ |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している
（`--rwc` で fold のモデルを使う）。BTC は README 上は RWC を使っていない。ChordMini は使っている。crema は不明。

## 捨てた案（試し直さない）

- **繰り返しの照合で誤りを直す・印を付ける**（上の 1）: 読みの多数決、モデルの確率の平均（読みで対にする・確率の近さで
  対にする）、「繰り返しの相手がいない小節に印」（確信度だけで同じ数を付けるより悪い）。正解の対を与えても +2 ポイントまで。
- section をルート＋長短だけで同じとみなす（七の和音を落とす）、`SAID_AGAIN` 0.90 以下（ルートを落とす）、
  完全一致だけ（精度は同じで、`rwc-dev` で 1 曲 12.8 セクション。0.95 なら 11.5）。
- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回るので、やらない。
- **コードモデルの候補で外したもの:** madmom（モデルが CC BY-NC-SA）。Jiang ほかの large-vocabulary モデル・omnizart
  （MIT だが重みの明記なし。フレームごとの確率を取り出す作りではない）。BACHI（楽譜からの認識）。
  ChordMini（BTC の派生で RWC を学習。ライセンスは BTC 以上にはならない）。
- crema 用に値段を選び直す（85.4〜86.3% で平ら）、印の式を合わせ直す。印に 3 つ目の耳を足す（2 つと同じ）。
- `--ears` で値段を自作モデル並みに高くする。`--ears` でベースのクロマを根音の手がかりに足す（誤差の内）。
- 印の確信度の案で効かなかったもの（BTC、`rwc-dev`）: 半拍ごとの最小値、2 番目の候補との差・比、対数の平均、エントロピー、
  N・X の確率、コードの長さ、ベースがルートか。特徴を 27 個に増やす・勾配ブースティング。短調への一律の割増。
- 別候補 `other` を「確率の合計が最大のルート・長短」から選ぶ（41.5% → 41.0%）。
- 小節線を別に探す手法（新奇度の対比、平均クロマの当てはめ）。→ コードと小節線を 1 回の探索で一緒に決める形にした。
- HPSS をやめて生の音を使う、NNLS で倍音を除く、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6（合成曲で効かない）。
- 規則だけでコードの重みを詰める（ルート 86〜88% で頭打ち）。短調の属和音の重みを下げて調を当てる（悪化）。
- 小節の長さの事前分布、拍の長さを局所の中央値で見る（合成曲で普通の曲を壊す）。
- 七の和音の迷いまで印にする（自作モデルでは効率が落ちた。`--ears` では未確認）。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源が配られていない。Winterreise は歌曲。AAM は合成。
  → RWC-P にした。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点・未確認

- section を書いた `.lys` は `lysc check` を通しただけ。Lily# で描いた譜面（セクション名の箱、段の割り方）と、
  form どおりに鳴るかは目と耳で確かめていない。5 拍・7 拍の拍子が通るかも未確認（RWC では出ていない）。
- README の「2 つ目の耳」の印の数字（77.4% など）と「下から 30%」の数字は section を入れる前に測ったもの（README にその旨）。
- crema の重みのライセンスの根拠は、作者の issue コメント（issue 42。自分では開いておらず、取得を任せた要約で読んだ）と、
  パッケージ同梱であること。配るときは BSD 2-Clause の著作権表示（Copyright (c) 2017, Brian McFee）を同梱する。
- crema が RWC を聞いているかは不明。聞いていれば RWC の数字は甘い。聞いていない実録音での確認はしていない
  （GuitarSet か、ユーザーの曲があればできる）。
- crema は合成曲の七の和音に弱い（seed7 で 64.1%。BTC 73.9%、自作モデル 73.7%）。実録音では BTC より上。
- Beat This! が半分のテンポで追う曲（合成曲で 1 セットに 2〜4 曲、`rwc-dev` で P050）は 2 小節が 1 小節になる。C4 と同じ問題。
- RWC の注釈に調がなく、調は実録音で測れていない。調は読んだコードから規則で決めている（`find_key`）。
- RWC のコードは読み取り器の 8 種類に畳んで採点している（DATASETS.md）。「種類まで」「ベースまで」は畳み方に左右される。
- 1 小節目の前は譜面に入らない。曲の頭の無音・弱起（C1）、大きなテンポ変化（C3）は未対応。
  伴奏のない旋律だけの曲は `--ears` で測っていない。
- Chord Tracker そのものとは比べていない（自動では測れない）。

## 未決（ユーザーの判断待ち）

- **M2 の印の目安（誤りの 8 割・印 3 割以下）をどうするか。** 「C2 の後で判断」の C2 は効かなかった。crema で `rwc-test` は
  「印 26% で 70%」「印 30% で 75%」。残る手は (a) 目安を下げる（例: 印 3 割で 75%）、(b) 印を 4 割近くまで増やす、
  (c) BTC を 2 つ目の耳にする（下。30% で 77%）。決まるまで閾値は 0.84 のまま。
- **BTC を 2 つ目の耳として配るか。** 入れると `rwc-test` でルート 87.0% → 88.4%・長短 81.9% → 83.5%、印も良くなる。
  条件は BTC の作者が重みの扱いを明言すること。jayg996/BTC-ISMIR19 に issue で尋ねるのはユーザー（尋ねていない）。
- **section の書き方。** いまは「ほぼ同じ並びは 1 回だけ書き、名前は A, B, C…」。細切れ（1 曲 10 セクション）が
  読みにくければ、まとめる条件を緩める（精度が落ちる）か、section を書かない指定を足す。使ってみての判断がほしい。
- **値段 5 つ・印の式 5 つ・`SAID_AGAIN` は `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習ではない
  つもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P（スタジオ録音のポップス）と違うなら、その種類の曲を数曲。Chord Tracker で読んだ結果があればそれも。
- 配る形（Python か .NET か。crema は ONNX にできる。Beat This! は torch のまま）、名前の重複の確認、リモートへの push（ROADMAP §5）。
