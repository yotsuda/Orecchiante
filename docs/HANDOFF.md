# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（D1 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

D1（横に置くファイルの仕様）が終わった。仕様は [SIDECAR.md](SIDECAR.md)（形式 1、英語）。読み取りの中身は変えていないので、
精度の数字は C1 の終わりと同じ。

### D1 で決めたこと・確かめたこと

- **`.lys` の小節と録音の対応は「順番」で取る。** `bars[]` の n 番目（0 から。弱起も 1 小節）＝ Lily# が form を通して鳴らす
  n 番目の小節。section で 1 回しか書いていない小節も、鳴る回数だけ `bars[]` にある。`lysc layout` の小節数と `bars` の長さが
  `measure.py all` の書いた全譜面で一致した（下の数字）。
- **足した項目は 3 つ:** `dettato`（形式の番号 1）、`duration`（録音の長さ。0.03 秒以内）、`doubt`（`sure` がこれ未満なら疑う。
  `sure` があるときだけ）。ほかの項目は名前も意味も変えていない。
- **`tools/sidecar.py`**（標準ライブラリだけ）: `faults(doc)` が仕様との食い違いを文で返す（小節番号の連続、小節が時間で
  つながる、和音が小節を埋める、form と `section`・`barOfSection` の一致、`sure` と `other`・`doubt` の対）。`lysc_bars()` は
  `lysc layout --batch` を数プロセスに分けて小節数を取る。`measure.py` の `check`（`table --write --check` と `all`）が両方を掛ける。
  `score_chords.py` は知らない形式の番号を断る。
- **Lily# 側には「原音を鳴らす」設計がまだない**（`C:\MyProj\LilySharp\docs` に記述なし）。使えるものはある: `lilysharp/playback` の
  答えが、和音の音ごとに元の位置 `S` と譜面上の秒 `T` を form の回数ぶん持ち、`Beats` の `A` が小節の頭。カーソルの小節 → n の
  求め方は SIDECAR.md の「Finding the seconds of the bar the caret is in」。
- **直した後のずれは扱わない。** 和音を直すだけなら対応は保たれる。小節を足す・削る・form を変えると、それ以降がずれる。
  エディタは「Lily# の鳴らす小節数 ≠ `bars` の長さ」で気づける。合わせ直す仕組みは作っていない。

### 数字（`measure.py all`。C1 の終わりと同じ値）

| `--ears`（crema） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 86.4% | 87.2% | 86.8% | 95.1 / 93.1 / 94.5% |
| ルート＋長短 | 82.2% | 82.1% | 82.2% | 91.6 / 88.4 / 92.4% |
| 小節線 F | 93.0% | 93.9% | 93.4% | 96.6 / 90.7 / 94.0% |
| テンポ・拍子 | 46・49 | 47・49 | 93・98 | |
| 印 → 拾う誤り | 29.3% → 76.6% | 25.7% → 70.1% | 27.5% → 73.3% | 17.4% → 69.9% |

- 自作の読み取り器（モデルあり）: seed1 / 7 / 11 のルート 95.6 / 96.0 / 97.2%、小節線 95.0 / 97.6 / 99.1%。
- 頭と終わり（`measure.py head`）: `seed41` で最初の小節線 22/24（自作）・20/24（`--ears`）、弱起 11/12・7/12。
  `rwc-dev`・`rwc-test` の `--ears` で最初の小節線 39/50・45/50、最後 24/50・13/50。表は README の「How a chart begins and ends」。

### C1・C2 から持ち越す知見（README にないもの）

- 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめ、最後の和音はその後も伸びる）。
- crema の「コードなし」は終わりに強く頭に弱い。曲の途中の N は 9% しか当たらないので、使うのは両端だけ。
  無音の直前 0.5 秒ほどに次の和音をもう聞いているので、デジタル無音は音量でも見る。
- RWC の正解は両端を語らない（弱起なし、最後の小節の後にも注釈の和音が 1 曲 1.6 秒）。小節線 F はそのぶん「余分」に数える。
- 弱起は `"bar": 0, "pickup": 拍数`、section は `Pickup`。Lily# は曲の最初の section にしか `partial` を許さない。

## 次の一手

**D2（配る形）の判断材料を測る。** D2 そのものはユーザーの判断（Python のままか、ONNX にして .NET のコマンドか）。
判断の前に 1 セッションで数字を出す:

1. crema を ONNX にして onnxruntime で動かし、TensorFlow 版と同じ確率が出るか・1 曲何秒かを測る（書き出せることは確認済み）。
2. Beat This! を ONNX にできるか（torch.onnx）。できれば同じく一致と速さ。できなければ、何が障害かを書く。
3. 特徴量（crema の入力の CQT、Beat This! の mel）を .NET で作る手間を見積もる。ここが .NET 版の本体になる。
4. Python のまま配る場合の依存の重さ（venv の大きさ、起動時間）を数える。
5. 2 案を表にして、ユーザーに決めてもらう。

D2 を待たずにできる別の一手: C3（テンポの大きな変化）、D4 の最初の一歩（下の「未確認」の 2 点を Lily# 側で確かめる）。

やり残し（急がない）:

- **和音のないイントロの小節**（ドラムだけの 8 小節など。`rwc-dev` の P027）は譜面に入れていない。入れるには `fold`・
  `beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# の和音の行は `r` で N.C.、小節の頭の `.` で無音）。
  曲の途中の N.C. も同じ仕組みで書ける。
- 最後の和音がどこまで伸びるか（フェルマータ）は `.dettato.json` にない。最後の小節の `end` は 1 小節ぶん。
  小節の中の拍の時刻もない（小節の中は直線で補う前提）。足すなら形式の番号は上げずに項目を足す（SIDECAR.md）。
- `measure.py` の `check` を `lysc check --batch` にすれば速くなる（いまは 1 枚 1 プロセス）。
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
- `lysc check <1 枚>` の合否は終了コードと `warning` / `: error`（`measure.py` の `check` は 1 枚ずつ並行に呼ぶ）。
  `lysc <コマンド> --batch <一覧>` なら 1 プロセスで多数を扱える（`check`・`layout` で確認。`sidecar.py` が使う）。
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

- **D4 の最初に Lily# 側で確かめる 2 点:** `lilysharp/playback` の `Beats` が `partial` の小節にも小節の頭（`A`）を立てるか、
  `lilysharp/placeAt` が弱起に返す小節番号。`lysc layout` は弱起を 1 小節に数える（`bars` の長さと一致）ことだけ確認した。
- **弱起つき・section つきの `.lys` は `lysc check` と `lysc layout` を通しただけ。** Lily# で描いた譜面と、form どおりに鳴るかは目と耳で
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
