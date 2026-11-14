# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（A0〜A2 と B0 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

M1（実録音を測れる）まで終わり、**方針を変えた**: 耳（拍・小節頭・コード）は実録音で学習された公開モデルを土台にし、
Dettato は直せる譜面への組み立てを作る（ユーザー了承、2026-11-14。きっかけは Yamaha Chord Tracker が実曲で
うまく動いているという指摘）。公開モデルを組み込むこと自体も了承済み（ライセンスが配布を許すものに限る）。

RWC-P 100 曲（モデル入り。全項目は README の「Recordings」）:

| | 自作の読み取り器 | 自作のコード＋正しい小節線 | 自作のコード＋Beat This! の小節線 | BTC のコード＋Beat This! の小節線 |
|---|---|---|---|---|
| テンポ | 60/100 | | 91/100 | 91/100 |
| 小節線 F | 66.1% | | 93.8% | 93.8% |
| ルート | 74.7% | 77.3% | 76.6% | 82.6% |
| ルート＋長短 | 63.4% | 66.2% | 65.3% | 77.4% |

- 小節線の誤り（半小節読み 29 曲、裏拍・頭ずれ 18 曲、別テンポ 11 曲）は、拍追跡を Beat This! に替えるだけでほぼ消える。
- 自作のコードモデル（合成曲 200 曲で学習、クロマ 24 個）は、小節線が正しくても 3 ポイントしか上がらない。耳が限界。
- BTC のコードは素の出力（フレームごとの最大、規則なし）。これに規則の組み立てを足すのが次。
- 合成曲（seed1・7・11）での自作の読み取り器は前のまま: 小節線 94.7〜99.1%、ルート 95.1〜97.4%。
  Beat This! の小節線は合成曲では自作に少し負ける（5 セットで F 86〜96% 対 89〜99%）。6/8 を 2 拍子と出す。

道具（すべて `tools/`）:

- `measure.py`: 測定の実体。`sets`・`make`・`all`・`table`・`sweep`・`doubt`・`barlines`・`keys`・`beats`。
  `table` に `--lines truth|<.beats のフォルダ>`（小節線を外から与える）と `--chords <.lab のフォルダ>`（他のモデルのコードを採点）。
  `rwc-dev`（P001〜050）・`rwc-test`（P051〜100）という部分セット名が使える。
- `read_chords.py`: `read(..., lines=, units=)` で小節線を外から受け取れるようにした（そのときはコードだけ読む）。
- `track_beats.py`（Beat This! → `.beats`）、`btc_chords.py`（BTC → `.lab`）: `.venv-bt` で動かす。
- `rwc_truth.py`（RWC の注釈 → `truth.json`）、`gen_chords.py`・`train_chords.py`・`score_chords.py`・`sample_songs.py`。

## 次の一手

**ROADMAP の B1（読み取り器の耳を差し替える）。** 順に:

1. BTC からフレームごとの確率を取り出す（`btc_chords.py` は最大のラベルしか書いていない。`output_layer` の出力から
   170 クラスの対数確率を `.npz` で保存する形を足す。フレームは 1 秒に約 10.8 個）。
2. `read_chords.read` の `lines=` の経路で、`emit` を BTC の確率（半拍ごとに平均し、170 → 自前の 8 種類 × 12 に畳む）に
   差し替え、`decode(..., first=0)` で組み立てる。`rwc-dev` で素の BTC（ルート 81.9%・長短 76.3%）を超えるかを見る。
   `STAY_COST`・`MID_COST`・`EMIT_SCALE` は `rwc-dev` で選び直す。
3. 拍子: Beat This! の 1 小節の拍の数と、拍の中の刻み（`find_meter`）から 4/4・3/4・6/8 を決める。
   いまは 2 拍子 → "2/4" になる（`measure.py` の `bar_lines` と `read` の末尾）。合成曲の 6/8 で確かめる。
4. `read_chords.py` のコマンドに、拍とコードのモデルを使う経路を足す（`.venv-bt` を呼ぶか、1 つの環境にまとめるかはここで決める）。

終わり: `rwc-dev` で小節線 93.3%・ルート 81.9%・長短 76.3% 以上、書いた `.lys` が全部 `lysc check` を通る。
`rwc-test` は数えるだけ。その後 B2（印の較正）、B3（コードモデルの決定）。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 2 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0、librosa ほか）が Beat This! と BTC。作り方は README。
  素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、
  `$env:DETTATO_LYSC='C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe'`。
  `.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'` も（設定しなくても動いた。
  Beat This! の重みは既定の場所に落ちている）。
  `make` で作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）と
  `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- 動作確認の 1 コマンド: `.venv\Scripts\python.exe tools\measure.py table seed1 seed7 seed11 seed21 seed31 rwc-p`
  （特徴量があれば 1 分半。数字は README）。
- 時間の目安: `table` は合成 24 曲で 4 秒、RWC 100 曲で 1 分弱。`all` は 7〜8 分（1072 枚を `lysc check`）。
  RWC の特徴量の作り直しは 20 分。Beat This! は CPU で 1 曲 5〜8 秒、BTC は 1 曲 1〜3 秒。
- 2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
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
| `scratch/real/beats-bt-folds/*.beats`（RWC を fold のモデルで）、`scratch/beats-bt/seed*/`（合成曲を `final0` で）、`scratch/real/chords-btc/*.lab` | `track_beats.py`・`btc_chords.py`（README） |

`scratch/` の古いスクリプト（`run.py` ほか）とログは使わない。消してよい。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100、`held/seed101` | 合成曲の学習用・定数を選ぶ用 | 自作モデルはこれしか聞いていない |
| seed1・7・11、seed21・31、`still/`・`drift5/`・`tim/`、`samples` | 合成曲のテスト | 定数を選ぶときに数字を見たことがある |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 最初の測定と B0 では集計を見た。曲ごとには見ない |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している
（`final0` を RWC で測ると不当に良く出る）ので、`--rwc` で fold のモデルを使う。BTC は README 上は RWC を使っていない。

## 捨てた案（試し直さない）

- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと**（旧 B0〜B2: ドラムの型で小節を決める、生成器を
  実物に寄せる、入力を広げる）。公開モデルが素の出力で上回るので、やらない。
- 小節線を別に探す手法（新奇度の対比、平均クロマの当てはめ）。→ コードと小節線を 1 回の探索で一緒に決める形にした。
- HPSS をやめて生の音を使う、NNLS で倍音を除く、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6（合成曲で効かない）。
- 規則だけでコードの重みを詰める（ルート 86〜88% で頭打ち）。短調の属和音の重みを下げて調を当てる（悪化）。
- 小節の長さの事前分布、拍の長さを局所の中央値で見る（合成曲で普通の曲を壊す）。
- 七の和音の迷いまで印にする（4 個に 1 個に付き、効率が落ちる）。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源が配られていない。Winterreise は歌曲で用途が遠い。
  AAM は合成。→ RWC-P にした。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点・未確認

- BTC の重みのライセンスは、MIT のリポジトリに同梱されているというだけで、重みについての明記はない。学習データは
  市販曲。配るものに入れる前に B3 で決める。Beat This! はコードも重みも MIT と明記（学習データには条件つきのものがある、との注記あり）。
- Beat This!＋BTC の数字は素の出力。`.lys` はまだ書いていない（`--chords` の経路は採点だけ）。印もない。
- Beat This! は合成曲の 6/8・3/4 で拍子とテンポを落とす（17〜20/24）。番号の読み替えを作っていないため。
- RWC の注釈に調がなく、調は実録音で測れていない。5/4 など 3・4・6 拍以外の拍子は書けない。
- RWC のコードは読み取り器の 8 種類に畳んで採点している（DATASETS.md）。「種類まで」「ベースまで」は畳み方に左右される。
  BTC はベース（転回形）を出さないので、「ベースまで」は BTC に不利。
- Chord Tracker そのものとは比べていない（自動では測れない）。同じ数曲の結果をユーザーに見せてもらえば比べられる。
- 伴奏のない旋律だけの曲、曲の頭の無音・弱起、繰り返しのまとめ、大きなテンポ変化は未対応（ROADMAP の C）。

## 未決（ユーザーの判断待ち）

- **コードモデルを自前で学習する場合のデータ**（RWC は CC BY-NC 4.0）。B3 までは不要。
- 使いたい曲の種類が RWC-P（スタジオ録音のポップス）と違うなら、その種類の曲を数曲。Chord Tracker で読んだ結果があればそれも。
- 配る形（Python か .NET か。torch が入るので ONNX 化の可否が関わる）、名前の重複の確認、リモートへの push（ROADMAP §5）。
