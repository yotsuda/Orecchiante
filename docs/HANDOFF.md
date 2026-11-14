# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（B2 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

B1（耳の差し替え）と B2（印の質）が終わった。`read_chords.py --ears` が、Beat This! の拍・小節頭と BTC の
フレームごとの確率から `.lys` と `.dettato.json` を組み立てる。B2 は「終わり」の後ろの枝（**目標には届かない。
理由と届く所の数字**）で閉じた。コード・小節線の数字は B1 から動いていない。

RWC-P（Beat This! は fold のモデル。全項目は README の「Their ears, this reader's rules」）:

| | 自作の読み取り器 | 素の BTC＋Beat This!（B0） | `--ears` `rwc-p` | `--ears` `rwc-dev` | `--ears` `rwc-test` |
|---|---|---|---|---|---|
| テンポ | 60/100 | 91/100 | 93/100 | 46/50 | 47/50 |
| 拍子 | 94/100 | 96/100 | 98/100 | 49/50 | 49/50 |
| 小節線 F | 66.1% | 93.8% | 93.8% | 93.3% | 94.4% |
| ルート | 74.7% | 82.6% | 85.6% | 84.8% | 86.4% |
| ルート＋長短 | 63.4% | 77.4% | 80.2% | 79.1% | 81.3% |

- B1 の「終わり」（`rwc-dev` で小節線 93.3%・ルート 81.9%・長短 76.3% 以上）を満たした。
- `rwc-test` は M2 の目安のうち、ルート 80%・長短 75%・小節線 85%・テンポ 85/100 相当をすでに超えている。
  **M2 で残っているのは印**（誤りの 8 割に印、印は 3 割以下）。これは `rwc-test` で届いていない（下）。

### 印（B2 の結果。コードの数で数える。`measure.py doubt <セット> --ears`）

| | `rwc-dev`（選んだ側） | `rwc-test`（数えただけ） | `rwc-p` |
|---|---|---|---|
| 印の付く割合 | 29.9% | 23.2% | 26.6% |
| ルートか長短の誤りのうち、印の付いた割合 | 79.7% | 64.2% | 72.3% |
| 印のうち誤りの割合 | 51.4% | 50.3% | 50.9% |
| 印の付いた誤りで、別候補のルート・長短が正解 | 41.5% | 44.1% | 42.6% |

- 式（`read_chords.py` の `SURE_OF`、`--ears` のときだけ）: 「ルートと長短が合っている確からしさ」=
  logistic(8·sure − 2·last + 0.7·agree + 5.5·songsure − 7.5)。sure は BTC がそのコード（同じルート・同じ長短）に
  与える確率の半拍ごとの平均（B1 までの `sure`）、last は最後の半拍のそれ（逆向きに効く: そこはもう次のコードの音）、
  agree は自作のテンプレートが同じルート・長短を 1 位にするか、songsure は sure の曲全体の平均。
  0.84（`DOUBT_EARS`）未満に印。5 つの数と閾値は `rwc-dev` で選んだ。`.dettato.json` の `sure` はこの値になった。
- 前との比較（`rwc-dev`）: 古い `sure < 0.7` は印 28.3%・誤りの 72.6%。
- **閾値を固定すると `rwc-test` では印が減り（23.2%）、拾うのも 64.2%。** 各セットで下から 30% に印を付けたとすると
  `rwc-dev` 79.7%・`rwc-test` 74.0%（古い sure だけなら 75.2%・71.6%）。`rwc-test` で 8 割を拾うには 38% に印が要る。
  （`rwc-test` は集計だけを見た。閾値は動かしていない。）
- **届かない理由:** 特徴を増やしても伸びない。27 個（ベース、前後のコードの sure、その曲でのコードの頻度、調に入るか、
  エントロピーなど）を logistic / 勾配ブースティングにかけても `rwc-dev` の 30% で 79.4%（曲を 10 分割で外して）。
  自信があるのに誤り（sure ≥ 0.8 の誤り 204 個）の中身は、正解が長調・読みが同じルートの短調 24%、
  正解 sus・読みが長調 10% など、BTC が確信して間違える型で、確率からは指せない。
- 合成曲の `--ears` では印 7〜9%・誤りの 44%（seed1・7・11）と 61%（seed21・31）。重みは実録音のもの。
- 別候補（`other`）: 印の付いた誤りの 4 割強で、ルート・長短が正解。
- 合成曲（seed1・7・11・21・31）の `--ears`: ルート 94.3〜97.5%・長短 91.2〜94.7%（自作モデルと 1 ポイント以内）。
  七の和音まで（quality）は最大 8 ポイント負ける（自作モデルはこの音源そのもので学習している）。
  小節線は 5 セット中 4 つで負ける（seed7 90.6% 対 97.4%、seed11 93.7% 対 99.1%）。原因は Beat This!:
  半分のテンポで追って 2 小節を 1 小節と数える曲と、3/4 を 4 拍・2 拍で数える曲。テンポ 18〜23/24、拍子 21〜23/24。

### `--ears` の中身（`read_chords.py`）

- `lines_of(.beats)` → 小節線の秒と 1 小節の拍数。`read(..., lines=, beats=, heard=)`。
- 拍子は拍数から: 3 → 3/4、4 → 4/4、6 → 6/8。2 拍は、小節の中の刻みが 3 つ割り（`find_meter`）なら 6/8、
  1 小節が 1.5 秒より長ければ（拍が毎分 80 より遅い）4/4、それ以外は 2/4。`SLOW_TWO = 1.5` は seed7・seed11
  （テスト用）を見て決めた。5 拍・7 拍は `5/4` などと書く（`lysc check` を通るかは未確認。RWC では出ていない）。
- `said_in`: BTC の 170 クラス（`FOLD` で自前の 96 に畳む。aug・X・N は捨てる）の確率を、半拍ごとに
  重なりの長さで平均して対数を取る。
- `use_ears()` の値段: `STAY_COST 0.1`・`MID_COST 0.1`・`ELSE_COST 0.25`・`EMIT_SCALE 0.2`・`BASS_WEIGHT 0`
  （`rwc-dev` で選んだ。自作モデル用の 0.4・0.5・1.0 だとルート 82.7%。値段は下げるほど良く、84.4〜84.9% の
  平らな所の中ほどを取った。これ以上下げると半拍ごとに変わる譜面になるので下げていない）。
  `LOG_FLOOR`（-4〜-14）と `BASS_WEIGHT`（0・0.1）は効かない（0.2 ポイント以内）。
- ベース音（分数コード）、`fit`、拍子の刻みは今も librosa の特徴量（`features`）から。`--ears` でも 1 曲数秒かかる。

道具（すべて `tools/`）:

- `measure.py`: `table`・`sweep` に `--ears`（セットごとの置き場所から拍と確率を取る。`ears_of`）、
  `--lines <.beats のフォルダ|truth>`、`--heard <.npz のフォルダ>`、`--chords <.lab のフォルダ>`。
  `all` の最後の方に「With ears trained on recordings」と「The marks, with ears trained on recordings」が出る。
  `doubt --ears --at 0.8,0.84` で印の表。`doubted(names, ears)` がコード 1 個ずつの行（`rc.read(..., why=True)` の
  特徴 20 個ほどと正誤）を返すので、確信度の案はこれで比べる（比べたスクリプトは `scratch/b2*.py`。commit の外）。
- `track_beats.py`（Beat This! → `.beats`）、`btc_chords.py --probs`（BTC → `.lab` と `.npz`）: `.venv-bt` で動かす。
- `rwc_truth.py`・`gen_chords.py`・`train_chords.py`・`score_chords.py`・`sample_songs.py` は前のまま。

## 次の一手

**ROADMAP の B3（コードモデルを決める）。** 順に:

1. BTC の重みのライセンスと学習データを確かめる（リポジトリの LICENSE・README・issue、論文。学習曲の一覧が
   あれば RWC を含まないことも）。配れないなら候補を探す（公開のコード認識モデルで、重みの配布条件が明記されたもの）。
2. 候補ごとに `rwc-dev` / `rwc-test` でルート・長短（`table --lines ... --heard ...` の形に確率を合わせる）、
   大きさ、CPU での速さ、ONNX に書き出せるか。
3. 印にも関わる: 2 つ目のモデルがあれば「2 つの耳が食い違う所」を印の材料にできる（自作のテンプレートとの一致
   `agree` は効いた。独立した耳ほど効くはず）。候補を測るついでに `doubted()` の行に足して見る。

終わり: 配るものに入れるモデルが決まり、理由が HANDOFF にある。

印をさらに良くする道（B2 で分かったこと。どれも未着手）:

- 繰り返しの照合（C2）。同じはずの小節同士で読みが違う所に印。確率からは指せない誤りに届く見込みがある唯一の案。
- 2 つ目の耳との食い違い（上の 3）。
- M2 の印の目安そのものを見直すか（未決の欄）。

B1 でやり残したこと（急がない）:

- `--ears` は拍と確率のファイルを先に作らせる 3 コマンド。`.venv` と `.venv-bt` を 1 つにまとめるか、
  1 コマンドにするかは D2・D3 で決める（torch を配るかどうかに関わる）。
- 素の BTC が RWC を本当に聞いていないかは、BTC の README の記述だけが根拠（学習曲の一覧は見ていない）。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 2 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0、librosa ほか）が Beat This! と BTC。作り方は README。
  素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、
  `$env:DETTATO_LYSC='C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe'`。
  `.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'` も（設定しなくても動いた）。
  `make` で作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）と
  `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- 動作確認の 1 コマンド（45 秒。数字は上の表と README）:
  `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears`
  自作の読み取り器の方は `table seed1 seed7 seed11 seed21 seed31 rwc-p`（特徴量があれば 2 分半）。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 5〜7 秒。`sweep` は 1 通り 4〜7 秒。`all` は 12 分ほど。
  RWC の特徴量の作り直しは 20 分。Beat This! は CPU で 1 曲 5〜8 秒、BTC は 1 曲 1〜3 秒（100 曲で 3 分）。
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
| `scratch/real/beat_this_annotations/`、`scratch/btc/`（clone）、`.venv-bt/` | README の「What models trained on recordings do」 |
| `scratch/real/beats-bt-folds/*.beats`（RWC を fold のモデルで）、`scratch/beats-bt/seed*/`（合成曲 5 セットを `final0` で） | `track_beats.py`（README） |
| `scratch/real/chords-btc/*.lab`・`*.npz`、`scratch/chords-btc/seed*/`（合成曲 5 セット） | `btc_chords.py scratch\btc <セットのフォルダ> --out <ここ> --probs` |

`scratch/` の古いスクリプト（`run.py` ほか）とログ、`scratch/read-ears/`・`scratch/read-all/`（書いた譜面）は消してよい。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100、`held/seed101` | 合成曲の学習用・定数を選ぶ用 | 自作モデルはこれしか聞いていない。Beat This!・BTC の出力はまだ作っていない |
| seed1・7・11、seed21・31、`still/`・`drift5/`・`tim/`、`samples` | 合成曲のテスト | 定数を選ぶときに数字を見たことがある。`SLOW_TWO` は seed7・11 の曲ごとの行を見て決めた |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | `use_ears()` の値段、印の式 `SURE_OF` と `DOUBT_EARS` はここで選んだ |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 集計だけを見ている。曲ごとには見ない |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している
（`final0` を RWC で測ると不当に良く出る）ので、`--rwc` で fold のモデルを使う。BTC は README 上は RWC を使っていない。

## 捨てた案（試し直さない）

- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回るので、やらない。
- `--ears` で値段を自作モデル並みに高くする（BTC は自分で均しているので、高い値段は正しい変化を潰す）。
  `--ears` でベースのクロマを根音の手がかりに足す（`BASS_WEIGHT 0.1`: +0.2 ポイントで誤差の内）。
- 印（`--ears`）の確信度の案で、効かなかったもの（`rwc-dev`）: 半拍ごとの最小値、2 番目の候補との差・比、
  対数の平均、エントロピー、N・X の確率、コードの長さ、ベースがルートか。単独ではどれも元の sure（平均）に負ける。
  特徴を 27 個に増やす・勾配ブースティングにする（4 個の式と 1 ポイント以内）。短調への一律の割増（誤差の内）。
- 別候補 `other` を「確率の合計が最大のルート・長短」から選ぶ（41.5% → 41.0%。いまの「1 個のコードで最大」のまま）。
- 小節線を別に探す手法（新奇度の対比、平均クロマの当てはめ）。→ コードと小節線を 1 回の探索で一緒に決める形にした。
- HPSS をやめて生の音を使う、NNLS で倍音を除く、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6（合成曲で効かない）。
- 規則だけでコードの重みを詰める（ルート 86〜88% で頭打ち）。短調の属和音の重みを下げて調を当てる（悪化）。
- 小節の長さの事前分布、拍の長さを局所の中央値で見る（合成曲で普通の曲を壊す）。
- 七の和音の迷いまで印にする（自作モデルでは 4 個に 1 個に付き、効率が落ちた。`--ears` では未確認）。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源が配られていない。Winterreise は歌曲で用途が遠い。
  AAM は合成。→ RWC-P にした。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点・未確認

- BTC の重みのライセンスは、MIT のリポジトリに同梱されているというだけで、重みについての明記はない。学習データは
  市販曲。配るものに入れる前に B3 で決める。Beat This! はコードも重みも MIT と明記（学習データには条件つきのものがある、との注記あり）。
- Beat This! が半分のテンポで追う曲（合成曲で 1 セットに 2〜4 曲、`rwc-dev` で P050）は 2 小節が 1 小節になる。
  2 拍を 4/4 に読み替える規則では直らない（拍数は 4 で合っているため）。C4（2 小節コード）と同じ問題。
- RWC の注釈に調がなく、調は実録音で測れていない。調はいまも読んだコードから規則で決めている（`find_key`）。
- RWC のコードは読み取り器の 8 種類に畳んで採点している（DATASETS.md）。「種類まで」「ベースまで」は畳み方に左右される。
- 1 小節目の前（最初の小節頭より前の音）は譜面に入らない。曲の頭の無音・弱起、繰り返しのまとめ、
  大きなテンポ変化は未対応（ROADMAP の C）。伴奏のない旋律だけの曲は `--ears` で測っていない（`samples` に拍と確率がない）。
- Chord Tracker そのものとは比べていない（自動では測れない）。同じ数曲の結果をユーザーに見せてもらえば比べられる。

## 未決（ユーザーの判断待ち）

- **M2 の印の目安（誤りの 8 割・印 3 割以下）をどうするか。** `rwc-test` で届くのは「印 23% で 64%」「印 30% で 74%」
  「印 38% で 80%」。(a) 目安を実測に合わせて下げる、(b) 閾値を上げて印を増やす、(c) C2（繰り返しの照合）や
  2 つ目の耳で上げてから判断する、のどれか。決まるまで閾値は 0.84 のまま。
- **印の式の 5 つの数は `rwc-dev`（CC BY-NC 4.0）で選んだ。** `use_ears()` の値段 5 つと同じ扱い（少数の定数の選択で、
  モデルの学習ではない）のつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。

- **コードモデルを自前で学習する場合のデータ**（RWC は CC BY-NC 4.0）。B3 までは不要。
- 使いたい曲の種類が RWC-P（スタジオ録音のポップス）と違うなら、その種類の曲を数曲。Chord Tracker で読んだ結果があればそれも。
- 配る形（Python か .NET か。torch が入るので ONNX 化の可否が関わる）、名前の重複の確認、リモートへの push（ROADMAP §5）。
