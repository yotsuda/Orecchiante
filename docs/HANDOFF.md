# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（B3 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

B3（コードモデルを決める）が終わった。**配るものに入れるコードモデルは crema 0.2.0**
（McFee & Bello。BSD 2-Clause）。`measure.py --ears` の既定も crema にした（`--ears btc` で BTC）。
`read_chords.py` の値段（`use_ears()`）と印の式（`SURE_OF`・`DOUBT_EARS`）は BTC で選んだ値のまま変えていない
（crema で選び直しても値段は 0.3 ポイント以内、印は `rwc-dev` で +1 ポイント・`rwc-test` で変わらず）。

### crema にした理由

- **重みのライセンスを作者が明言しているのは crema だけ。** bmcfee/crema の issue 42（2026-11-12、open）で
  作者が「重みはソフトウェアの一部で、ライセンスの内。クレジットを書くこと」と答えている
  （LICENSE の文面の変更ではなく issue のコメント）。重みは PyPI のパッケージに同梱（`crema/models/chord/model.h5`）。
- BTC: リポジトリは MIT で重みも同梱だが、README・LICENSE・issue のどこにも重みの扱いの記述がない（license の
  issue は 0 件）。第三者の変換（HF の cstr/btc-chords-GGUF）は「学習データの条件から CC BY-NC-SA」と書いているが、
  根拠の条件は確かめられなかった（Isophonics のページにコード注釈のライセンスの記載はない）。
- ChordMini（Phan ほか、DAFx 2026。MIT のリポジトリに重み同梱、重みの明記なし）: BTC を教師にした疑似ラベル＋
  Isophonics・Billboard・**RWC Pop**・USPop の 420 曲で学習。BTC の派生物なので BTC より条件が良くなることはない。
  RWC を学習しているので RWC の数字は割り引く。
- 小さい: 52.8 万パラメータ、h5 で 6.4 MB。**ONNX に書き出せた**（tf2onnx、opset 17、2.2 MB、出力は元と 3e-6 以内で一致、
  onnxruntime で 1 曲 0.9 秒）。入力は CQT（44.1 kHz、hop 4096、216 ビン × 2）で、CQT は ONNX の外。
- 精度は実録音で BTC 以上（下の表）。弱いのは合成曲の七の和音（BTC より 3〜10 ポイント下）と根音（最大 2.4 下）。

### 数字（`--ears <名前>`。値段は全部 BTC 用のまま。全項目は README の「Which model hears the chords」）

| | crema（既定） | `btc` | `cmbtc`（ChordMini の BTC） | `cm2e1d` | `crema+btc`（確率の平均） |
|---|---|---|---|---|---|
| `rwc-dev` ルート / 長短 | 86.0 / 81.8 | 84.8 / 79.1 | 86.1 / 82.2 | 84.1 / 79.8 | 87.0 / 82.6 |
| `rwc-test` ルート / 長短 | 87.1 / 82.0 | 86.4 / 81.3 | 87.2 / 82.6 | 85.6 / 81.3 | 88.5 / 83.6 |
| `rwc-p` ルート / 長短 | 86.6 / 81.9 | 85.6 / 80.2 | 86.7 / 82.4 | 84.9 / 80.6 | 87.8 / 83.1 |
| 合成曲 5 セットのルート | 93.1〜95.9 | 94.3〜97.5 | 93.9〜97.4 | 93.4〜97.2 | 94.6〜97.0 |
| 合成曲の七の和音まで | 64.1〜74.5 | 73.9〜82.3 | 75.5〜83.4 | 70.8〜81.6 | 71.0〜80.6 |

- テンポ・拍子・小節線は Beat This! のものなので全列同じ（`rwc-test` 47/50・49/50・94.4%）。
- **RWC の数字の重み:** RWC を聞いていないと分かっているのは BTC だけ。ChordMini は聞いている。crema の学習データは
  どこにも書かれていない（Humphrey & Bello の 1217 曲で、RWC Pop を含むと記憶しているが未確認）。4 つとも聞いていないのは合成曲だけ。
- 素の crema（規則なし、`--chords`）は `rwc-p` で 84.4 / 79.8。規則で +2 ポイント。

### 印（crema。`measure.py doubt <セット> --ears`。式と閾値 0.84 は BTC で選んだまま）

| | `rwc-dev` | `rwc-test` | `rwc-p` | BTC のとき（dev / test） |
|---|---|---|---|---|
| 印の付く割合 | 28.7% | 24.7% | 26.7% | 29.9% / 23.2% |
| ルートか長短の誤りのうち、印の付いた割合 | 76.4% | 68.7% | 72.5% | 79.7% / 64.2% |
| 印のうち誤りの割合 | 40.1% | 44.5% | 42.1% | 51.4% / 50.3% |
| 印の付いた誤りで、別候補のルート・長短が正解 | 57.9% | 52.1% | 55.1% | 41.5% / 44.1% |

- 各セットで下から 30% に印: crema 77.8% / 75.2%（BTC 79.7% / 74.0%）。M2 の目安（8 割・印 3 割以下）には届いていない。
- **2 つ目の耳は印に効く**（`scratch/b3two.py`。重みは `rwc-dev` で logistic、`rwc-test` は数えただけ）:
  crema＋BTC で `rwc-test` の 30% が 75.2% → 77.4%、閾値を dev で固定して 70.0% → 72.4%。BTC＋crema なら 74.2% → 79.0%、
  64.6% → 74.5%。足すのは「2 つ目の耳が、読んだコードと同じルート・長短に与える確率の平均」1 個で足りる。3 つ目の耳は足しても変わらない。
- 合成曲（crema）: 印 17%、誤りの 70%（seed1・7・11）と 74%（seed21・31）。

### 道具（`tools/`）

- `crema_chords.py`（`.venv-crema`）: crema → `.lab` と `.npz`（BTC と同じ 170 クラスの順に並べ替えた対数確率）。
  crema 0.2.0 は Keras 2 と `pkg_resources` 前提なので、`tf-keras` を `keras` として差し込み、CQT だけを pump から取る
  （pump の残りは今の scikit-learn で落ちる）。`chord_tag` の出力だけを使う（root・bass・pitch の出力は使っていない）。
- `chordmini_chords.py`（`.venv-bt`）: ChordMini の 2 モデル → 同じ形。`--model 2e1d|btc`。
- `measure.py`: `--ears [名前]` が `scratch/real/chords-<名前>`・`scratch/chords-<名前>/seedN` を読む（既定 `crema`）。
  `doubted()` の行に `end` を足した。`all` の「With ears」「The marks, with ears」は crema。
- `read_chords.py` は変えていない。`--ears <フォルダ>` は `.beats` と `.npz` があればどのモデルのものでも読む。
- 前からのもの: `track_beats.py`・`btc_chords.py --probs`・`rwc_truth.py`・`gen_chords.py`・`train_chords.py`・`score_chords.py`・`sample_songs.py`。

## 次の一手

**ROADMAP の C2（繰り返しを見つける）を先に。** 理由: M2 で残っているのは印だけで、確率からは指せない誤りに届く
見込みがあるのは「同じはずの小節同士の照合」（B2 の結論）。C2 は譜面を短くする M3 の項目でもある。順に:

1. `rwc-dev` で、読んだコード列の中の繰り返し（同じ長さ 4・8 小節の並びが 2 回以上）を見つける。まず正解のコード列で
   「繰り返しはどれだけあるか」を数え、次に読んだ列で見つかる割合を見る。
2. 繰り返しの中で読みが食い違う小節に印を足したとき、印の割合と拾える誤りがどう動くか（`doubted()` の行に列を足す）。
   多数決で読みを直す案も同じ行で測れる。
3. 効くなら `read_chords.py` に入れ、`section` / `form` の書き出しは別の一手にする。

C1（曲の頭）と D1（`.dettato.json` の仕様）はその後。D2（配る形）は下の「未決」が決まると進めやすい。

やり残し（急がない）:

- `crema_chords.py` は TensorFlow を入れた 3 つ目の venv で動く。配る形（D2）では ONNX（書き出せることは確認済み）にして
  `.venv` だけ、または .NET から呼ぶ。CQT を librosa なしで作る所が残る。
- crema の `chord_root`・`chord_bass`・`chord_pitch` の出力は未使用。ベース音（分数コード）はいまも自作の特徴量から
  （bass 63〜64%）。crema の `chord_bass` を使えば上がるかもしれない（未確認）。
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
  `.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'` も（設定しなくても動いた）。
  `make` で作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）と
  `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- 動作確認の 1 コマンド（15 秒。数字は上の表と README）:
  `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears`
  → seed1 ルート 95.1%、`rwc-dev` 86.0% / 81.8%、`rwc-test` 87.1% / 82.0%。
  自作の読み取り器の方は `table seed1 seed7 seed11 seed21 seed31 rwc-p`（特徴量があれば 2 分半）。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 5〜7 秒。`sweep` は 1 通り 4〜7 秒。`all` は 12 分ほど。
  RWC の特徴量の作り直しは 20 分。Beat This! は CPU で 1 曲 5〜8 秒、BTC は 1〜3 秒、crema は 0.8 秒、ChordMini は 0.4 秒。
- 2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
  掃引を途中で止めるときは、コマンドラインに `measure.py sweep` を含む python.exe を止める。
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

`scratch/b3two.py`（2 つ目の耳と印）・`b3fit.py`（印の式の合わせ直し）・`b3avg.py` は commit の外。
`scratch/` の古いスクリプトとログ、`scratch/read-all/`（書いた譜面）、`scratch/cmtest/` は消してよい。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100、`held/seed101` | 合成曲の学習用・定数を選ぶ用 | 自作モデルはこれしか聞いていない。公開モデルの出力はまだ作っていない |
| seed1・7・11、seed21・31、`still/`・`drift5/`・`tim/`、`samples` | 合成曲のテスト | 定数を選ぶときに数字を見たことがある。`SLOW_TWO` は seed7・11 の曲ごとの行を見て決めた |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | 値段、`SURE_OF`、`DOUBT_EARS` はここで選んだ（BTC で）。2 つ目の耳の重みもここ |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 集計だけを見ている。**B3 では 4 モデルの `rwc-test` の集計を並べて見たうえで crema に決めた**（決め手はライセンス） |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している
（`--rwc` で fold のモデルを使う）。BTC は README 上は RWC を使っていない。ChordMini は使っている。crema は不明。

## 捨てた案（試し直さない）

- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回るので、やらない。
- **コードモデルの候補で外したもの:** madmom（モデルが CC BY-NC-SA）。Jiang ほかの large-vocabulary モデル・omnizart
  （MIT だが重みの明記なし。出すのはラベルで、フレームごとの確率を取り出す作りではない）。BACHI（楽譜からの認識で、音ではない）。
  ChordMini（BTC の派生で RWC を学習。2E1D は BTC 並み、BTC 再学習版は +1 ポイントだが、ライセンスは BTC 以上にはならない）。
- crema 用に値段を選び直す（`EMIT_SCALE` 0.1〜0.4 × `ELSE_COST` 0.15〜0.4 で 85.4〜86.3%。平ら）、印の式を合わせ直す（上）。
- 印に 3 つ目の耳を足す（2 つと同じ）。
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

- crema の重みのライセンスの根拠は、作者の issue コメント（issue 42。自分では開いておらず、取得を任せた要約で読んだ）と、
  パッケージ同梱であること。学習データは不明で、市販曲の録音で学習しているのは他のモデルと同じ。
  配るときは BSD 2-Clause の著作権表示（Copyright (c) 2017, Brian McFee）を同梱する。
- crema が RWC を聞いているかは不明。聞いていれば RWC の数字は甘い。聞いていない実録音での確認はしていない
  （GuitarSet か、ユーザーの曲があればできる）。
- crema は合成曲の七の和音に弱い（seed7 で 64.1%。BTC 73.9%、自作モデル 73.8%）。実録音では BTC より上（`rwc-test` 69.0% 対 67.6%）。
- Beat This! が半分のテンポで追う曲（合成曲で 1 セットに 2〜4 曲、`rwc-dev` で P050）は 2 小節が 1 小節になる。C4 と同じ問題。
- RWC の注釈に調がなく、調は実録音で測れていない。調は読んだコードから規則で決めている（`find_key`）。
- RWC のコードは読み取り器の 8 種類に畳んで採点している（DATASETS.md）。「種類まで」「ベースまで」は畳み方に左右される。
- 1 小節目の前は譜面に入らない。曲の頭の無音・弱起、繰り返しのまとめ、大きなテンポ変化は未対応（ROADMAP の C）。
  伴奏のない旋律だけの曲は `--ears` で測っていない。5 拍・7 拍の拍子が `lysc check` を通るかは未確認（RWC では出ていない）。
- Chord Tracker そのものとは比べていない（自動では測れない）。

## 未決（ユーザーの判断待ち）

- **BTC を 2 つ目の耳として配るか。** 入れると `rwc-test` でルート 87.1% → 88.5%・長短 82.0% → 83.6%、印も良くなる（上）。
  条件は BTC の作者が重みの扱いを明言すること。jayg996/BTC-ISMIR19 に issue で尋ねるのはユーザー（文面の案は
  2026-11-14 の報告にある。尋ねていない）。決まるまで crema 1 つで進める。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）をどうするか。** crema で `rwc-test` は「印 25% で 69%」「印 30% で 75%」。
  (a) 目安を下げる、(b) 印を増やす、(c) C2（繰り返しの照合）や 2 つ目の耳の後で判断する。決まるまで閾値は 0.84 のまま。
- **値段 5 つと印の式 5 つは `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習ではないつもりだが、
  「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P（スタジオ録音のポップス）と違うなら、その種類の曲を数曲。Chord Tracker で読んだ結果があればそれも。
- 配る形（Python か .NET か。crema は ONNX にできる。Beat This! は torch のまま）、名前の重複の確認、リモートへの push（ROADMAP §5）。
