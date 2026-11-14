# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（B1 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

B1（読み取り器の耳を差し替える）が終わった。`read_chords.py --ears` が、Beat This! の拍・小節頭と BTC の
フレームごとの確率から `.lys` と `.dettato.json` を組み立てる。書いた 320 枚は全部 `lysc check` を通る。

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
  **M2 で残っているのは印**（誤りの 8 割に印、印は 3 割以下）。
- 印はまだ較正していない（自作モデル用の `DOUBT = 0.7` のまま）。付く割合は `rwc-dev` 28.7%・`rwc-test` 24.1%。
  誤りをどれだけ拾っているかは測っていない。
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
  `all` の最後の方に「With ears trained on recordings」の表が出る。`doubt` はまだ `--ears` に対応していない。
- `track_beats.py`（Beat This! → `.beats`）、`btc_chords.py --probs`（BTC → `.lab` と `.npz`）: `.venv-bt` で動かす。
- `rwc_truth.py`・`gen_chords.py`・`train_chords.py`・`score_chords.py`・`sample_songs.py` は前のまま。

## 次の一手

**ROADMAP の B2（印の質）。** 順に:

1. `measure.py doubt` を `--ears` に対応させる（`doubt()` が `rc.read(wav, feats(wav))` を直に呼んでいる。
   `run` と同じく `lines`・`beats`・`heard` を渡す）。`rwc-dev` で「印の割合・誤りのうち印の付いた割合・
   印のうち誤りの割合」を `--at 0.5,0.6,0.7,0.8,0.9` で見る。
2. 確信度の作り方を比べる。いまの `sure` は「読んだコードと同じルート・同じ長短の確率の、半拍ごとの平均」。
   候補: 2 番目の候補との差、小節の中での最小値、短いコード（半拍 1〜2 個）への割引、N・X の確率が高い所。
   較正は `rwc-dev` だけで行い、`rwc-test` は数えるだけ。
3. `@todo` に出す別候補（`other`）が正解である割合も見る。

終わり: `rwc-test` で「印 3 割以下・ルートか長短の誤りの 8 割を拾う」か、それが無理な理由と、届く所の数字。
その後 B3（コードモデルの決定: BTC の重みのライセンス、ONNX にできるか）。

B1 でやり残したこと（B2 の後でよい）:

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
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | `use_ears()` の値段はここで選んだ |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 集計だけを見ている。曲ごとには見ない |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している
（`final0` を RWC で測ると不当に良く出る）ので、`--rwc` で fold のモデルを使う。BTC は README 上は RWC を使っていない。

## 捨てた案（試し直さない）

- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回るので、やらない。
- `--ears` で値段を自作モデル並みに高くする（BTC は自分で均しているので、高い値段は正しい変化を潰す）。
  `--ears` でベースのクロマを根音の手がかりに足す（`BASS_WEIGHT 0.1`: +0.2 ポイントで誤差の内）。
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

- **コードモデルを自前で学習する場合のデータ**（RWC は CC BY-NC 4.0）。B3 までは不要。
- 使いたい曲の種類が RWC-P（スタジオ録音のポップス）と違うなら、その種類の曲を数曲。Chord Tracker で読んだ結果があればそれも。
- 配る形（Python か .NET か。torch が入るので ONNX 化の可否が関わる）、名前の重複の確認、リモートへの push（ROADMAP §5）。
