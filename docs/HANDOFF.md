# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（A0・A1・A2 を終えたセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

M1（実録音を測れる）まで終わった。**合成曲では読めるが、実録音ではまだ使えない。**

| モデル入り | 合成曲（seed1・7・11） | 実録音（RWC-P 100 曲） |
|---|---|---|
| テンポ | 23/24 | 60/100 |
| 小節線 F | 94.7〜99.1% | 66.1% |
| ルート | 95.1〜97.4% | 74.7% |
| ルート＋長短 | 90.6〜96.6% | 63.4% |
| 印の付く割合 | 23% | 73% |

規則だけ（モデルなし）の実録音: テンポ 62、小節線 65.0%、ルート 70.5%、長短 49.3%。
テンポを教えても（`--hint`）ほぼ変わらない（小節線 67.6%）。全項目は README の「Recordings」。

道具:

- `tools/measure.py`: 測定の実体（今回 repo に入れた）。`sets`・`make`・`all`・`table`・`sweep`・`doubt`・
  `barlines`・`keys`・`beats`。`all` で README の全表が出て、書いた `.lys` を全部 `lysc check` に通す。
- `tools/rwc_truth.py`: RWC の注釈 → `truth.json`。`tools/score_chords.py` は調のない正解と
  `chordSeconds`（コードを数える時間）に対応した。
- `tools/gen_chords.py`・`read_chords.py`・`train_chords.py`・`sample_songs.py` は前のまま（読み取り器は今回触っていない）。

## 実録音の誤り（A2 の結果。モデル入り、100 曲）

1. **半小節読み: 29 曲。** テンポを 2 倍に読み、小節を半分に切る。真の小節線の 88% は見つけているが、
   読んだ線の 44% しか正しくない。29 曲中 27 曲が 62〜107 bpm の遅い曲（テンポが合った 60 曲は大半が 107 bpm 以上）。
   遅い曲はコードが 1 小節に 2 回変わることが多く、「小節＝コード 1 つの長さ」という読みが外れる。
   `--tempo` は 2 拍の小節も許すので直らない。
   効きそうな手: (a) `--tempo` を教えたときは 4 拍（3 拍）の小節だけにする。すぐできる。
   (b) 小節の長さを選ぶときに、コードの変化だけでなくドラムの周期（1 小節でひと回りする型）を見る。
   (c) 実録音のテンポ分布に合わせた小節の長さの事前分布（合成曲で捨てた案だが、実録音で測り直す価値がある）。
2. **テンポは合っているのに線の場所が違う: 18 曲（うち 9 曲はほぼ全滅）。** 7 曲は拍が裏拍に乗って全部の線が半拍ずれる
   （P019・024・030・035・036・075・096）。5 曲は拍は合っているが小節の頭が 1〜2 拍ずれる（P010・022・073・095・097）。
   残り 6 曲はところどころ（P034 は 5/4 で、読み取り器が書けない）。
   効きそうな手: 裏拍は、低音（バスドラム・ベース）の立ち上がりが乗る方を表とする。小節の頭は、
   コードの変わり目だけでなく低音の強さとスネア（2・4 拍）の位置で決める。
3. **別のテンポ: 11 曲。** 1/2 が 5 曲、3/4 が 4 曲、約 1.5 倍が 2 曲。`TEMPO_RATIOS` の仕組み（2/3・3/2 を試す）が
   実録音では逆に働いている可能性がある。前半 50 曲の該当曲で `beats` を見るところから。
4. **コード: 知らない音。** 100 曲中 66 曲で「知らない音」と判定（半拍の半分以上に自信なし）。その 66 曲はルート 71%・長短 57%、
   残り 34 曲はルート 81%・長短 74%。印は 73% に付き、誤り（35%）の 93% を含むが、印の 45% しか誤りでない。
   効きそうな手: B1（学習データを実物に寄せる: 歌の代わりの旋律、ドラム入りの厚いミックス、音の加工）。
   モデルの確信度を実録音側で較正し直す（B3）。RWC を学習に使うかは未決（下）。

小節線（1〜3）は規則の側の問題で、合わせて 58 曲。コード（4）より先にやる: 小節が合えばコードの採点も
人の直しやすさも一緒に上がり、学習データを作り直すより 1 回の実験が速い。→ ROADMAP に B0 を足した。

## 次の一手

**ROADMAP の B0（小節の長さと頭を実録音で直す）。** 順に:

1. `measure.py table rwc-dev -v` と `measure.py barlines rwc-dev` で前半 50 曲の該当曲を出す。
2. 上の 1(a) を入れる（`--tempo` で 2 拍の小節を許さない）。`--hint` の数字がどこまで上がるかが、
   「小節の長さが分かれば残りは何か」の上限になる。seed21 の `--hint` の数字を落とさないこと。
3. 半小節読みを音から決める手（1(b)、1(c)）を試す。値は `rwc-dev` と seed100 で選び、`rwc-test` は数えるだけ。
4. 裏拍と小節の頭（2）。

終わり: `rwc-dev` で小節線 F 80% 以上、または効かなかった手の記録。合成曲の全セットを落とさない。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は `.venv\Scripts\python.exe`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）。
  標準ライブラリだけの道具（`gen_chords.py`・`score_chords.py`・`sample_songs.py`・`rwc_truth.py`）は `py -3.12` でもよい。
  素の `python` は LilyPond 同梱のものを拾うので使わない。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、
  `$env:DETTATO_LYSC='C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe'`。
  `make` で作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）と
  `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- SoundFont: `soundfonts\MuseScore_General.sf2`（学習と測定の基準。`lysc wav` は `.sf2` だけ読む）。
- 動作確認の 1 コマンド: `.venv\Scripts\python.exe tools\measure.py table seed1 seed7 seed11 seed21 seed31 rwc-p`
  （特徴量があれば 1 分半。数字は上の表と README）。
- 時間の目安: `table` は合成 24 曲で 4 秒、RWC 100 曲で 1 分弱。`all` は RWC 込みで 7〜8 分（1072 枚）
  （`lysc check` は 1 枚ずつしか受け取らないので並行に呼んでいる）。RWC の特徴量の作り直しは 20 分。
- 2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
- `lysc check` の合否は終了コードと `warning` / `: error` で見る（`measure.py` の `check` がそうしている）。
- `C:\MyProj` 全体の再帰検索は固まる。探す場所を絞る。
- Windows はフォルダ名の大文字小文字を区別しない: `RWC-P.zip` を `scratch\real` に展開すると `rwc-p` に入る。

### commit の外にあるもの（消えたら作り直す）

| もの | 作り方 |
|---|---|
| 合成曲のセット 13 個（`data/synth/seed*` の wav、`scratch/alt-*`） | `measure.py make`（`measure.py sets` が一覧） |
| `models/chords.joblib` | `train_chords.py data\synth\seed100 --features scratch\feat-h31 --out models\chords.joblib` |
| `scratch/feat-h31/*.npz`（特徴量。実録音は `real_RWC_P*.npz`） | `measure.py` が初回に作る。録音の方が新しければ作り直す |
| `scratch/real/rwc-p/`（RWC-P の wav 100 個と truth）、`scratch/real/rwc-annotations/`、`scratch/real/RWC-P.zip`（3.79 GB） | DATASETS.md の手順。zip は md5 `960a11a2…` を確かめた |
| `scratch/rwc-*.log`、`scratch/read-real/`、`scratch/read-all/` | 今回の測定のログと書き出した譜面。消してよい |

`scratch/` の古いスクリプト（`run.py`・`sweep.py`・`doubt.py`・`barlines.py`・`keys.py`・`flip.py`）は
`measure.py` に移した。残っているが使わない。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100 | 学習用。定数を選ぶのにも使う | モデルはこれしか聞いていない |
| `held/seed101` | 定数を選ぶ用 | |
| seed1・seed7・seed11 | 合成曲のテスト | 定数を選ぶときに数字を見たことがある |
| seed21・seed31、`still/`・`drift5/`・`tim/` | 2 小節コード、テンポ揺れ、別の SoundFont | |
| `samples` | Lily# のサンプル 4 曲 | 「知らない音」のしきい値はこの 4 曲を見て決めた |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 最初の測定では、集計と小節線を落とした曲の一覧を見た。以後は見ない |

## 捨てた案（試し直さない）

- 小節線を別に探す手法（新奇度の対比、平均クロマの当てはめ）。→ コードと小節線を 1 回の探索で一緒に決める形にした。
- HPSS をやめて生の音を使う、NNLS で倍音を除く、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6。どれも効かない（合成曲で）。
- 規則だけでコードの重みを詰める。ルート 86〜88% で頭打ち。→ 聞き分けを学習に替えた。
- 短調の属和音の重みを下げて調を当てる。読みに対しては悪化。
- 小節の長さの事前分布（長い小節に罰）。合成曲では 2 小節コードの曲を救うぶん普通の曲を壊す。
  **実録音では未検証**（半小節読みは逆向きの誤りなので、実録音の分布で測り直してよい）。
- 拍の長さを局所の中央値で見る。普通の曲の小節線が 2〜3 ポイント落ちる。
- 七の和音の迷いまで印にする。4 個に 1 個に付き、重い誤りを拾う効率が落ちる。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源が配られていない。Winterreise は歌曲で用途が遠い。
  AAM は合成（学習用の候補にはなる）。→ RWC-P にした。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点

- 実録音: 上の 4 つ。加えて調を測れていない（RWC の注釈に調がない）。
- `measure.py barlines` の「beats' spread」は、拍を半分・倍の細かさで追っている曲では大きく出る（目安にしかならない）。
- 伴奏のない旋律だけの曲はコードを読めない。5/4 など 3・4・6 拍以外の拍子は書けない。
- 2 小節続くコードの速い曲を半分のテンポに読む（合成曲。`--tempo` で直る）。調は 3 分の 1 が平行調に外れる。
- 小節ごと 5% のテンポの揺れで小節線が崩れ始める。曲の頭の無音・弱起を想定していない。繰り返しをまとめない。
- RWC のコードは読み取り器の 8 種類に畳んで採点している（maj6 → 長三和音、sus2 → sus4 など。DATASETS.md）。
  「種類まで」「ベースまで」の数字はこの畳み方に左右される。ルートと長短は左右されない。

## 未決（ユーザーの判断待ち）

- **RWC を学習に使うか**（CC BY-NC 4.0。モデルを配るときの条件に関わる）。決まるまで測定だけに使う。
- 使いたい曲の種類が RWC-P（スタジオ録音のポップス）と違うなら、その種類の曲を数曲。
- 配る形（Python か .NET か）、名前の重複の確認、リモートへの push（ROADMAP §5）。
