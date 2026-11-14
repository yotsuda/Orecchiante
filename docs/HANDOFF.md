# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（C1 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

C1（曲の頭）が終わった。両方の読み取り器（自作・`--ears`）が、譜面の頭と終わりを「鳴っているもの」で決める。
数字と説明は README の「How a chart begins and ends」。

### 分かったこと

- **実録音で失っていたのは頭ではなく終わり。** `rwc-dev` で、読んだ小節の外にあった正解の和音は頭 4 秒・終わり 129 秒
  （全体の 1.2%）。Beat This! はドラムが止まる所で拍をやめ、最後の和音はその後も伸びる。いまは 3 秒・50 秒（0.5%）。
- **crema の「コードなし」は終わりに強く、頭に弱い。** 確率 0.5 超で、最後の和音の後の 93%・最初の和音の前の 62% を
  言い当て、和音のある所の 0.3% を誤って消す。曲の途中の N（68 秒）は 9% しか当たらないので、使うのは両端だけ。
- **crema は無音の直前 0.5 秒ほどに、次の和音をもう聞いている。** デジタル無音は音量でも見ないと弱起を誤って作る。
- **RWC の正解は両端を語らない。** 正解の小節は最初の小節線から最後の 1 小節後まで、弱起はなく、和音は小節で切ってある。
  正解の最後の小節の後にも注釈の和音が 1 曲 1.6 秒あり、そこを正しく読むと小節線は「余分」に数えられる。

### 入れたもの（`read_chords.py` の `read()`、定数 `QUIET`・`HUSH`・`NO_CHORD`・`CHORDED`・`RING_OUT`・`PARTIAL`）

1. `--ears`: 小節の格子を、最初・最後の数小節の長さのまま 0 秒と録音の終わりまで延ばす。最初の小節線の間隔が短ければ
   （中央値の 0.8 未満）、その線は弱起の始まりとして扱う。最後の間隔が短ければ（0.75 未満）拍が止まった所として 1 小節に直す。
2. 半拍ごとに「和音が鳴りうるか」（`sounds`）: 自作は音量が曲の中央値の 1/10 以上、`--ears` は 1/100 以上かつ crema の N が 0.5 以下。
   音量は半拍の中央のフレームで見る（拍の端は `BEAT_LAG` だけ遅れていて、音の出だしが前の半拍に入るため）。
3. 譜面は「1/4 以上が鳴っている最初の小節」から「最後のそういう小節」まで。その外（無音、カウント、鳴り終わり）は書かない。
   最後に見つかった小節より先で、前の小節の 1/5 より静かな小節は鳴り終わりとして落とす（和音が同じかは見ない）。
4. 弱起: 最初の小節の直前に続く「鳴っている拍」を、短い小節（`"bar": 0, "pickup": 拍数`、section は `Pickup`）にする。
   頭が無音で始まる最初の小節も弱起として扱う。1 小節に半拍だけ足りない場合は 1 小節目そのもの（格子が遅れて始まった）。
   `.lys` は `section Pickup { partial 2  chords harmony { G | } }` を form の先頭に（Lily# は曲の最初の section にしか
   `partial` を許さない）。弱起のある譜面は、繰り返しがなくても section で書く。
5. 録音が小節の途中で終わる最後の小節は、1 小節として書く（`end` は前の小節の長さで外挿）。
6. 生成器 `gen_chords.py --pickup 0.5 --silence 3`。弱起は属和音をバンドが弾くか、旋律だけ（和音なし）。正解は
   `"pickup": 拍数` の 0 番の小節と `silence`・`chordSeconds`。採点（`score_chords.py`）は弱起の頭を小節線に数えない。
7. `measure.py head <セット> [--ears] [-v]`: 最初・最後の小節線、弱起、無音に読んだ和音の秒数。`all` にも入れた。

### 数字（前回 → 今回。`measure.py all`、1388 枚すべて `lysc check` 通過）

| `--ears`（crema） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 86.0 → 86.4% | 87.0 → 87.2% | 86.5 → 86.8% | 95.1 / 93.2 → 93.1 / 94.4 → 94.5% |
| ルート＋長短 | 81.8 → 82.2% | 81.9 → 82.1% | 81.8 → 82.2% | 91.5 → 91.6 / 88.4 / 92.3 → 92.4% |
| 小節線 F | 93.3 → 93.0% | 94.4 → 93.9% | 93.8 → 93.4% | 96.4 → 96.6 / 90.6 → 90.7 / 93.7 → 94.0% |
| テンポ・拍子 | 46・49（同じ） | 47・49（同じ） | 93・98（同じ） | 同じ |
| 印 → 拾う誤り | 29.3% → 76.6% | 25.7% → 70.1% | 27.5% → 73.3% | 17.4% → 69.9% |

- 実録音の小節線が 0.3〜0.5 下がったのは、(a) 正解の最後の小節より後に読んだ小節（`rwc-dev` で 28。和音の注釈はある）、
  (b) 最初の和音より前の、譜面に入れなくなった小節（20。うち 14 は和音なし）。和音の精度は上がっている。
- 自作の読み取り器（モデルあり）: seed1 / 7 / 11 のルート 95.1 → 95.6 / 95.4 → 96.0 / 97.2%、小節線 94.7 → 95.0 / 97.4 → 97.6 / 99.1%。
  seed21・seed31 のルートは 97.9 → 98.8%・97.5 → 98.4%（格子が遅れて始まる曲の 1 小節目を失わなくなった）。
- 頭と終わり（`measure.py head`）。`seed41` はテスト用（数えただけ）、定数は `head/seed141`・seed1・`rwc-dev` で選んだ:

| | `seed41` 自作 | `seed41` `--ears` | `rwc-dev` `--ears` | `rwc-test` `--ears` |
|---|---|---|---|---|
| 最初の小節線が正解 | 22/24 | 20/24 | 39/50 | 45/50 |
| 最後の小節線が正解 | 23/24 | 20/24 | 24/50 | 13/50 |
| 和音つきの弱起を同じ拍数で読んだ | 11/12 | 7/12 | （正解に弱起なし） | |
| ない所に読んだ弱起 | 2 | 2 | 11 | 11 |
| 最初の正解の和音より前に読んだ和音 | 0.12 秒/曲 | 0.02 | 0.73 | 0.52 |

- `seed41` の全体: 自作でルート 96.6%・小節線 97.7%、`--ears` で 94.4%・91.1%（小節線は Beat This! の分）。

## 次の一手

**ROADMAP の D1（`.dettato.json` の仕様を決める）。** いま入っている項目: `audio`・`tempo`・`time`・`key`・`strange`・`form`・
`bars[]`（`bar`・`start`・`end`・`section`・`barOfSection`・`pickup`・`chords[]`（`start`・`end`・`symbol`・`fit`・`sure`・`other`））。順に:

1. 項目の意味と単位を `docs/` に書く（弱起は 0 番の小節、最後の小節の `end` は外挿、`sure` は印の元）。
2. Lily# 側の「小節を選ぶと原音を鳴らす」が要るもの（`.lys` の小節と `bars[]` の対応。section で畳んだ小節は各回の時刻が
   複数ある）を `C:\MyProj\LilySharp\docs` で確かめ、足りない項目を足す。
3. `score_chords.py`・`measure.py` をその仕様に合わせる。

その後は D2（配る形。ユーザーの判断）か C3（テンポの大きな変化）。

やり残し（急がない）:

- **和音のないイントロの小節**（ドラムだけの 8 小節など。`rwc-dev` の P027）は譜面に入れていない。入れるには `fold`・
  `beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# の和音の行は `r` で N.C.、小節の頭の `.` で無音）。
  曲の途中の N.C. も同じ仕組みで書ける。
- 最後の和音がどこまで伸びるか（フェルマータ）は `.dettato.json` にない。最後の小節の `end` は 1 小節ぶん。
- section の読みやすさ: 1〜2 小節のセクションを減らす手（1 番かっこ、短い違いを前後に吸収）は試していない。
- `crema_chords.py` は TensorFlow の入った 3 つ目の venv で動く。配る形（D2）では ONNX にする（書き出せることは確認済み）。
- crema の `chord_root`・`chord_bass`・`chord_pitch` の出力は未使用。ベース音はいまも自作の特徴量から。
- 値段と印の式を crema＋BTC の平均用に選び直すことはしていない（BTC を配れるか決まってから）。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 3 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0）が Beat This!・BTC・ChordMini。`.venv-crema`（tensorflow、tf-keras、
  crema 0.2.0、`setuptools<81`、tf2onnx、onnxruntime）が crema。作り方は README。
  素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、
  `$env:DETTATO_LYSC='C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe'`。
  `.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'` と
  `$env:TORCHINDUCTOR_CACHE_DIR='C:\MyProj\Dettato\scratch\torchinductor'` も。
  `make` で `tim/` を作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）、
  `samples` は `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- 動作確認の 1 コマンド（20 秒）:
  `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears`
  → seed1 ルート 95.1%・小節線 96.6%、`rwc-dev` 86.4% / 82.2% / 93.0%、`rwc-test` 87.2% / 82.1% / 93.9%。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 7 秒。`all` は 15〜20 分（自作の読み取り器で `rwc-p` を読む表が 1 枚 2 分）。
  RWC の特徴量の作り直しは 20 分。Beat This! は CPU で 1 曲 5〜8 秒、crema は 0.8 秒。合成曲 24 曲のセットを作るのは 30 秒。
- 2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
  止めるときは、コマンドラインに `measure.py all`（または `sweep`）を含む python.exe を止める。
- `lysc check` は 1 枚ずつしか受け取らない。合否は終了コードと `warning` / `: error`（`measure.py` の `check`）。
- `C:\MyProj` 全体の再帰検索は固まる。Windows はフォルダ名の大文字小文字を区別しない。
- pwsh MCP の `Update-MatchInFile` で書き換えたファイルは、Edit ツールの前に読み直しが要る。

### commit の外にあるもの（消えたら作り直す）

| もの | 作り方 |
|---|---|
| 合成曲のセット 15 個（`data/synth/seed*` の wav、`scratch/alt-*`） | `measure.py make`（`measure.py sets` が一覧） |
| `models/chords.joblib` | `train_chords.py data\synth\seed100 --features scratch\feat-h31 --out models\chords.joblib` |
| `scratch/feat-h31/*.npz`（特徴量。実録音は `real_RWC_P*.npz`） | `measure.py` が初回に作る |
| `scratch/real/rwc-p/`（wav 100 個と truth）、`rwc-annotations/`、`RWC-P.zip`（3.79 GB） | DATASETS.md の手順 |
| `scratch/real/beat_this_annotations/`、`scratch/btc/`・`scratch/chordmini/`（clone）、`.venv-bt/`、`.venv-crema/` | README |
| `scratch/real/beats-bt-folds/*.beats`、`scratch/beats-bt/<セット>/`（合成曲 7 セットを `final0` で。`seed41`・`head/seed141` も） | `track_beats.py`（README） |
| `scratch/real/chords-crema/`、`scratch/chords-crema/<セット>/` | `crema_chords.py <セットのフォルダ> --out <ここ>` |
| `scratch/**/chords-btc/`・`chords-cmbtc/`・`chords-cm2e1d/`・`chords-crema+btc/`（`seed41`・`head/seed141` の分はない） | `btc_chords.py --probs`・`chordmini_chords.py`・`scratch/b3avg.py` |

`scratch/c1*.py`（C1 の調べもの: `c1.py` 譜面の外の和音、`c1n.py` crema の N、`c1bars.py`・`c1tail.py` 両端の小節、`c1sweep.py`）と
`c2*.py`・`b3*.py` は commit の外。`scratch/` の古いスクリプトとログ、`read-all/`・`c1out/`・`c1lys/`・`c2out/`・`cmtest/` は消してよい。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100、`held/seed101`、`head/seed141` | 合成曲の学習用・定数を選ぶ用 | 自作モデルは seed100 しか聞いていない。`head/seed141` は C1 の定数を選んだ |
| seed1・7・11、seed21・31、`still/`・`drift5/`・`tim/`、`samples` | 合成曲のテスト | 定数を選ぶときに数字を見たことがある。**C1 では seed1 の曲ごとの行（終わりの小節、最初の小節）を見て `RING_OUT` と「半拍足りない 1 小節目」を決めた。** seed7 は集計だけ |
| `seed41` | 合成曲のテスト（頭の無音と弱起） | 集計だけを見た。曲ごとには見ていない |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | 値段、`SURE_OF`、`DOUBT_EARS`、`SAID_AGAIN`、C1 の `NO_CHORD`・`RING_OUT`・`HUSH` はここで選んだ |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 集計だけを見ている。C1 では決めた後に数えただけ |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している
（`--rwc` で fold のモデルを使う）。BTC は README 上は RWC を使っていない。ChordMini は使っている。crema は不明。

## 捨てた案（試し直さない）

- **C1:** 鳴り終わりを「前と同じ和音で、かつ静か」で見分ける（鳴り終わりは別の和音に聞こえることが多く、seed1 の 24 曲に
  余分な小節が 11 残る。音量だけなら 4）。`--ears` の無音を自作と同じ「中央値の 1/10」で見る（フェードアウトの小節を落とし、
  `rwc-dev` の終わりで和音のある小節を 35 失う）。crema の N だけで無音を見る（無音の直前に弱起を作る）。
  弱起の中の和音を拍ごとに書く（3 拍の弱起に 3 つ書くと Lily# が警告する。最後の和音だけ書く）。
- **繰り返しの照合で誤りを直す・印を付ける**（C2）: 読みの多数決、モデルの確率の平均、「相手がいない小節に印」。
  正解の対を与えても +2 ポイントまで。
- section をルート＋長短だけで同じとみなす、`SAID_AGAIN` 0.90 以下、完全一致だけ。
- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回るので、やらない。
- **コードモデルの候補で外したもの:** madmom（CC BY-NC-SA）。Jiang ほか・omnizart（重みの明記なし、確率を出さない）。
  BACHI（楽譜からの認識）。ChordMini（BTC の派生で RWC を学習）。
- crema 用に値段を選び直す、印の式を合わせ直す。印に 3 つ目の耳を足す。`--ears` で値段を高くする、ベースのクロマを足す。
- 印の確信度の案（BTC、`rwc-dev`）: 半拍ごとの最小値、2 番目との差・比、対数の平均、エントロピー、N・X の確率、コードの長さ、
  ベースがルートか。特徴 27 個・勾配ブースティング。短調への一律の割増。別候補を確率の合計から選ぶ。
- 小節線を別に探す手法（新奇度、平均クロマ）。HPSS をやめる、NNLS、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6。
- 規則だけでコードの重みを詰める。短調の属和音の重みを下げて調を当てる。小節の長さの事前分布、拍の長さの局所中央値。
- 七の和音の迷いまで印にする（自作モデルで効率が落ちた。`--ears` では未確認）。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源なし。Winterreise は歌曲。AAM は合成。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点・未確認

- **弱起つき・section つきの `.lys` は `lysc check` を通しただけ。** Lily# で描いた譜面と、form どおりに鳴るかは目と耳で
  確かめていない。5 拍・7 拍の拍子も未確認。
- **Lily# の気づき（報告済み、未対応）:** 和音の行のスロット数は、短い小節（`partial`）でも拍子の拍数で検査される。
  4/4 の 3 拍の弱起に和音 3 つで「3 chord slot(s) in a 4/4 bar fit no beat」。Dettato 側は最後の和音だけ書いて避けている。
- 旋律だけの弱起に、自作の読み取り器は和音を書く（「和音なし」を言えない）。`--ears` は書かない。
- 半拍より短い弱起（8 分音符 1 つ）は拍に丸めるので消える。弱起は 4 分の拍で 1・2・3・4・6 拍だけ（`PARTIAL`）。
- `rwc-dev` で最初の小節線が正解と違う 11 曲、`seed41` で外した曲の原因は見ていない。実録音の「ない所に読んだ弱起 11」は
  正解に弱起がないだけで、誤りかどうかは分からない（注釈では 10 曲に最初の小節線より前の拍がある）。
- 自作の読み取り器は、正解の小節線を与えると実録音のルートが 77.4 → 77.1% に下がった（原因は見ていない。フェードアウトを
  無音とみなす疑い）。比較用なのでそのまま。
- README の「2 つ目の耳」の印の数字（77.4% など）と「下から 30%」は section を入れる前、「What is said again」の本文の
  数字は C1 の前に測ったもの（README にその旨）。
- crema の重みのライセンスの根拠は作者の issue コメント（issue 42。要約で読んだ）とパッケージ同梱。配るときは
  BSD 2-Clause の著作権表示（Copyright (c) 2017, Brian McFee）を同梱する。crema が RWC を聞いているかは不明。
- crema は合成曲の七の和音に弱い（seed7 で 64.1%）。Beat This! が半分のテンポで追う曲は 2 小節が 1 小節になる（C4）。
- RWC の注釈に調がなく、調は実録音で測れていない。RWC のコードは 8 種類に畳んで採点している（DATASETS.md）。
- 大きなテンポ変化（C3）は未対応。伴奏のない旋律だけの曲は `--ears` で測っていない。Chord Tracker とは比べていない。

## 未決（ユーザーの判断待ち）

- **和音のないイントロ・弱起を譜面に書くか。** いまは書かない（最初の和音の小節から始める）。書くなら N.C. の小節として。
  旋律を採る段階（M5）では要る。`rwc-dev` で落としている和音なしの小節は 14（小節線 F への効きは 0.1 ほどの見込みで、測っていない）。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）をどうするか。** crema で `rwc-test` は「印 26% で 70%」。
  (a) 目安を下げる、(b) 印を 4 割近くまで増やす、(c) BTC を 2 つ目の耳にする。決まるまで閾値は 0.84。
- **BTC を 2 つ目の耳として配るか。** 入れると `rwc-test` でルート 87.2% → 88.7%・長短 82.1% → 83.8%。
  条件は BTC の作者が重みの扱いを明言すること。jayg996/BTC-ISMIR19 に issue で尋ねるのはユーザー（尋ねていない）。
- **section の書き方。** 細切れ（1 曲 10 セクション）が読みにくければ、条件を緩めるか、section を書かない指定を足す。
- **値段・印の式・`SAID_AGAIN`・C1 の 3 つの定数は `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習では
  ないつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P と違うなら、その種類の曲を数曲。Chord Tracker で読んだ結果があればそれも。
- 配る形（Python か .NET か）、名前の重複の確認、リモートへの push（ROADMAP §5）。
