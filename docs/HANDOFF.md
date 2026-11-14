# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（D2 の判断材料を測ったセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

D2（配る形）の判断材料を測り終えた。**D2 そのものはユーザーの判断待ち**（下の「未決」の表）。
読み取りの中身は 1 か所だけ変えた: `--ears` のとき HPSS（和音成分の取り出し）をやめた。

### D2 で確かめたこと（README の「Without torch and TensorFlow」。スクリプトは `tools/d2/`、出力は `scratch/d2/`）

- **2 つのモデルは ONNX で同じ答えを出す。** crema（tf2onnx、2.2 MB）: 確率の差 3.3e-6、RWC 100 曲を読み取り器に通して
  表が全桁同じ。Beat This! `final0`（torch.onnx、82.9 MB）: ロジットの差 8.4e-5、10 曲の拍 4103 個と小節線が同じ、
  seed1 の `.beats` 24 個がバイト単位で同じ。Beat This! の mel は numpy 40 行で torch と 1.2e-4 以内。
- **torch も TensorFlow も要らない。** numpy・librosa・soxr・soundfile・onnxruntime の venv 1 つ（450 MB）で同じ数字。
- **組み立ての時間の 9 割は HPSS だった**（1 曲 10 秒のうち 8〜9 秒）。`--ears` では和音は crema のものなので外した
  （`use_ears()` が `HARMONIC_FRAMES = 0` にする。特徴量は `scratch/feat-h0/`）。ルート・長短は ±0.1、ベース音は全セットで上がる
  （`rwc-dev` 63.3 → 64.3%、`rwc-test` 64.3 → 65.0%、seed1 70.4 → 73.4%）。自作の読み取り器は 31 のまま（ドラムで和音が落ちる）。
- **1 曲（3 分 27 秒、RWC_P001）、各段を別プロセスで冷たい状態から:** これまで 25 秒 → HPSS なしで 18 秒
  （Beat This! 5.4、crema 9（うち 8 は TensorFlow の起動）、組み立て 3.6）。venv 1 つ＋ONNX で 11 秒（耳 7.3、組み立て 3.6）。
  1 プロセスにまとめれば 9 秒ほどの見込み（走らせていない）。残る時間の大半は Beat This!（3.2〜4.3 秒）。
- **onnxruntime のスレッド数は 8 に絞る。** 既定（24 全部）だと Beat This! が 1 曲 10 秒に落ちる。torch は 2.0〜2.4 秒で、ONNX より速い。
- **.NET の試作（`tools/d2/spike`、260 行、拍の経路だけ）:** wav → 半分の標本化 → mel → ONNX → 拍。プロセス全体で 3.6 秒、
  モデル以外に 12 MB（Microsoft.ML.OnnxRuntime 1.20.1。Calamo と同じ）。seed1 で小節線 F 96.6 → 96.4%、コードは同じ。
  拍が torch と違う（24 曲中 4 曲で拍の数、残りで 94 個）のは再標本化の違いで、Python でも soxr を scipy に替えると同じだけ動く。
- **crema の CQT は標本単位で合わせなくてよい。** librosa の CQT の中の再標本化を替えても（`polyphase`）、RWC の表は全列 ±0.1。

### 数字（`measure.py all`、2026-11-14 の最後。lysc check 1388/1388、sidecar 1388/1388）

| `--ears`（crema） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 86.4% | 87.3% | 86.8% | 95.1 / 93.1 / 94.5% |
| ルート＋長短 | 82.3% | 82.1% | 82.2% | 91.6 / 88.4 / 92.4% |
| ベース音まで | 64.3% | 65.0% | 64.6% | 73.4 / 58.5 / 73.6% |
| 小節線 F | 93.0% | 93.9% | 93.4% | 96.6 / 90.7 / 94.0% |
| テンポ・拍子 | 46・49 | 47・49 | 93・98 | |
| 印 → 拾う誤り | 30.4% → 77.8% | 25.9% → 71.0% | 28.2% → 74.4% | 17.3% → 69.9% |

- 印が前回（29.3% → 76.6%、25.7% → 70.1%）から動いたのは、印の 4 つの材料の 1 つ（自作のテンプレートが同じ和音を言うか）が
  HPSS なしのクロマを聞くようになったため。`SURE_OF` と閾値は選び直していない。
- 自作の読み取り器（モデルあり）は変えていない: seed1 / 7 / 11 のルート 95.6 / 96.0 / 97.2%、小節線 95.0 / 97.6 / 99.1%。
- 頭と終わり（`measure.py head`）は前回と同じ: `rwc-dev`・`rwc-test` の `--ears` で最初の小節線 39/50・45/50、最後 24/50・13/50。

### 持ち越す知見（README にないもの）

- `.lys` の小節と録音の対応は「順番」（`bars[]` の n 番目＝Lily# が form を通して鳴らす n 番目。SIDECAR.md）。
  Lily# 側に「原音を鳴らす」設計はまだない。使えるのは `lilysharp/playback` の `S`・`T` と `Beats` の `A`。
- 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。
- RWC の正解は両端を語らない（弱起なし）。弱起は `"bar": 0, "pickup": 拍数`、section は `Pickup`。
- 旧式のトレースで ONNX を書いた直後の torch のモデルは、同じプロセスの中で答えが変わる（拍 460 → 465、6 倍遅い）。
  書いたファイルは正しい。比べる前にモデルを読み直す（`bt_onnx.py` はそうしている）。
- torch の新しい書き出し（dynamo）は進み具合に絵文字を出すので、`PYTHONUTF8=1` がないと cp932 で落ちる。

## 次の一手

**ユーザーが D2 を決める**（「未決」の表）。決まった形で次へ:

- **(B) Python＋ONNX に決まったら:** `tools/d2/ears_onnx.py` と `read_chords.py` を 1 つのコマンドにまとめる
  （`dettato song.wav` → `.lys` と `.dettato.json`）。耳の出力をファイルに書かず渡す、録音を 1 回だけ読む、スレッドは 8。
  終わり: 1 コマンド・1 プロセスで `measure.py all` と同じ数字、1 曲 10 秒以内。そのまま D3（mp3、エラーの言い方）へ。
- **(C) .NET に決まったら:** 順に (1) librosa の CQT を移す（crema の入力と 2 つのクロマの共通の土台。Python の値と比べる検査つき）、
  (2) onset strength・rms・クロマ（調律の推定を含む）、(3) `read_chords.py` の規則（920 行）、(4) `measure.py` が .NET の
  書いたものを読んで全表が一致。1 項目 1 セッションの見込みで 3〜5 セッション。試作（`tools/d2/spike`）が出発点。

D2 を待たずにできる一手: C3（テンポの大きな変化）、D4 の最初の一歩（下の「未確認」の 2 点を Lily# 側で確かめる）。

やり残し（急がない）:

- **Beat This! の小さい版（`small0`、README によれば 8 MB）は測れていない。** 配布元（cloud.cp.jku.at）が 500 を返した。
  取れたら `track_beats.py --model small0` で合成曲、RWC は `final0` と並べて（どちらも RWC を学習済みなので相対比較だけ）。
  時間と大きさの大半が Beat This! なので、D2 のどちらの形でも効く。半精度（fp16）も測っていない。
- 和音のないイントロの小節（`rwc-dev` の P027）は譜面に入れていない。`fold`・`beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# は `r` で N.C.）。
- 最後の和音がどこまで伸びるか、小節の中の拍の時刻は `.dettato.json` にない（足すなら形式の番号は上げない）。
- `measure.py` の `check` を `lysc check --batch` にすれば速くなる。1〜2 小節の section を減らす手は試していない。
- crema の `chord_root`・`chord_bass`・`chord_pitch` は未使用（ベース音は自作のクロマ）。crema＋BTC 用の値段と印の選び直しは BTC が決まってから。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 3 つ＋試し 1 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0、onnx・onnxruntime・onnxscript）が Beat This!・BTC・ChordMini。
  `.venv-crema`（tensorflow、tf-keras、crema 0.2.0、`setuptools<81`、tf2onnx、onnxruntime）が crema。
  `scratch/d2/venv-onnx`（numpy、librosa、soxr、soundfile、onnxruntime）が ONNX だけの耳。作り方は README。
  素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、
  `$env:DETTATO_LYSC='C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe'`。
  `.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'`、
  `$env:TORCHINDUCTOR_CACHE_DIR='C:\MyProj\Dettato\scratch\torchinductor'`、`$env:PYTHONUTF8='1'` も。
  `make` で `tim/` を作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）、
  `samples` は `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- 動作確認の 1 コマンド（20 秒）:
  `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears`
  → seed1 ルート 95.1%・小節線 96.6%・ベース 73.4%、`rwc-dev` 86.4% / 82.3% / 93.0%、`rwc-test` 87.3% / 82.1% / 93.9%。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 7 秒。`all` は 20〜25 分（自作の読み取り器で `rwc-p` を読む表が 1 枚 2 分）。
  RWC の特徴量の作り直しは HPSS ありで 20 分、なしで 3 分。合成曲 24 曲のセットを作るのは 30 秒。
- 2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
  止めるときは、コマンドラインに `measure.py all`（または `sweep`）を含む python.exe を止める。
- 速さを測るときは、裏で走っている仕事が終わってから測る（24 コアでも ONNX と torch は取り合う）。
- `lysc check <1 枚>` の合否は終了コードと `warning` / `: error`。`lysc <コマンド> --batch <一覧>` なら 1 プロセスで多数を扱える。
- `C:\MyProj` 全体の再帰検索は固まる。Windows はフォルダ名の大文字小文字を区別しない。
- pwsh MCP の `Update-MatchInFile` で書き換えたファイルは、Edit ツールの前に読み直しが要る。
- **README.md などユーザーが Markdown のプレビューで開いているファイルは、Edit を 1 回ずつ出す。** 並べて出すと、
  プレビューが書き込み中のファイルを読んで「being used by another process」のダイアログを編集の数だけ出す（2026-11-14）。

### commit の外にあるもの（消えたら作り直す）

| もの | 作り方 |
|---|---|
| 合成曲のセット 15 個（`data/synth/seed*` の wav、`scratch/alt-*`） | `measure.py make`（`measure.py sets` が一覧） |
| `models/chords.joblib` | `train_chords.py data\synth\seed100 --features scratch\feat-h31 --out models\chords.joblib` |
| `scratch/feat-h31/*.npz`（自作の読み取り器用）、`scratch/feat-h0/*.npz`（`--ears` 用。実録音は `real_RWC_P*.npz`） | `measure.py` が初回に作る |
| `scratch/real/rwc-p/`（wav 100 個と truth）、`rwc-annotations/`、`RWC-P.zip`（3.79 GB） | DATASETS.md の手順 |
| `scratch/real/beat_this_annotations/`、`scratch/btc/`・`scratch/chordmini/`（clone）、`.venv-bt/`、`.venv-crema/` | README |
| `scratch/real/beats-bt-folds/*.beats`、`scratch/beats-bt/<セット>/`（合成曲 7 セットを `final0` で） | `track_beats.py`（README） |
| `scratch/real/chords-crema/`、`scratch/chords-crema/<セット>/` | `crema_chords.py <セットのフォルダ> --out <ここ>` |
| `scratch/**/chords-btc/`・`chords-cmbtc/`・`chords-cm2e1d/`・`chords-crema+btc/` | `btc_chords.py --probs`・`chordmini_chords.py`・`scratch/b3avg.py` |
| `scratch/d2/`（`crema-chord.onnx`、`beat-this-final0-*.onnx`、`crema-classes.json`、`venv-onnx/`、`spike/out/`、`out/`、`mel/`） | README の「Without torch and TensorFlow」の末尾 |

`scratch/c1*.py`・`c2*.py`・`b3*.py` は commit の外。`scratch/` の古いスクリプトとログ、`read-all/`・`c1out/`・`c1lys/`・`c2out/`・`cmtest/`、
`scratch/d2/` の `*.log`・`one*/`・`beats-small/`・`beats-final0/` は消してよい。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100、`held/seed101`、`head/seed141` | 合成曲の学習用・定数を選ぶ用 | 自作モデルは seed100 しか聞いていない。`head/seed141` は C1 の定数を選んだ |
| seed1・7・11、seed21・31、`still/`・`drift5/`・`tim/`、`samples` | 合成曲のテスト | 定数を選ぶときに数字を見たことがある。C1 では seed1 の曲ごとの行を見た。**HPSS を外す判断は seed1・7・11 の集計と `rwc-dev` で行った** |
| `seed41` | 合成曲のテスト（頭の無音と弱起） | 集計だけを見た。曲ごとには見ていない |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | 値段、`SURE_OF`、`DOUBT_EARS`、`SAID_AGAIN`、C1 の定数、HPSS なしはここで決めた |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 集計だけを見ている。HPSS なしの集計は、決める前に `rwc-dev` と同時に出た（どちらも同じ向き） |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している
（`--rwc` で fold のモデルを使う）。BTC は README 上は RWC を使っていない。ChordMini は使っている。crema は不明。
速さと一致を見るだけの測定（D2）は `final0` で行った。精度の数字には使っていない。

## 捨てた案（試し直さない）

- **D2:** onnxruntime を既定のスレッド数で走らせる（24 コアで Beat This! が 2.5 倍遅い）。`--ears` で HPSS を残す。
- **C1:** 鳴り終わりを「前と同じ和音で、かつ静か」で見分ける。`--ears` の無音を自作と同じ「中央値の 1/10」で見る。
  crema の N だけで無音を見る。弱起の中の和音を拍ごとに書く（Lily# が警告する）。
- **繰り返しの照合で誤りを直す・印を付ける**（C2）: 読みの多数決、確率の平均、「相手がいない小節に印」。正解の対でも +2 ポイントまで。
- section をルート＋長短だけで同じとみなす、`SAID_AGAIN` 0.90 以下、完全一致だけ。
- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回る。
- **コードモデルの候補で外したもの:** madmom（CC BY-NC-SA）。Jiang ほか・omnizart（重みの明記なし、確率を出さない）。
  BACHI（楽譜からの認識）。ChordMini（BTC の派生で RWC を学習）。
- crema 用に値段を選び直す、印の式を合わせ直す。印に 3 つ目の耳を足す。`--ears` で値段を高くする、ベースのクロマを足す。
- 印の確信度の案（BTC、`rwc-dev`）: 半拍ごとの最小値、2 番目との差・比、対数の平均、エントロピー、N・X の確率、コードの長さ、
  ベースがルートか。特徴 27 個・勾配ブースティング。短調への一律の割増。別候補を確率の合計から選ぶ。
- 小節線を別に探す手法（新奇度、平均クロマ）。自作の読み取り器で HPSS をやめる、NNLS、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6。
- 規則だけでコードの重みを詰める。短調の属和音の重みを下げて調を当てる。小節の長さの事前分布、拍の長さの局所中央値。
- 七の和音の迷いまで印にする（自作モデルで効率が落ちた。`--ears` では未確認）。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源なし。Winterreise は歌曲。AAM は合成。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点・未確認

- **D2 で確かめていないこと:** .NET で書いたのは拍の経路だけ（CQT・クロマ・規則は見積もり）。1 プロセスにまとめた Python の時間。
  wav 以外の入力、44100・22050 Hz 以外の標本化。別の CPU での速さ（この PC は 24 コア）。`small0` と半精度。
- **D4 の最初に Lily# 側で確かめる 2 点:** `lilysharp/playback` の `Beats` が `partial` の小節にも小節の頭（`A`）を立てるか、
  `lilysharp/placeAt` が弱起に返す小節番号。`lysc layout` は弱起を 1 小節に数えることだけ確認した。
- 弱起つき・section つきの `.lys` は `lysc check` と `lysc layout` を通しただけ。描いた譜面と鳴り方は目と耳で確かめていない。
- **Lily# の気づき（報告済み、未対応）:** 和音の行のスロット数は、短い小節（`partial`）でも拍子の拍数で検査される。
- 旋律だけの弱起に、自作の読み取り器は和音を書く。半拍より短い弱起は消える。弱起は 1・2・3・4・6 拍だけ（`PARTIAL`）。
- 原因を見ていないもの: `rwc-dev` で最初の小節線が正解と違う 11 曲、`seed41` で外した曲、自作の読み取り器に正解の小節線を与えると実録音のルートが 77.4 → 77.1% に下がること。
- README の「2 つ目の耳」の印の数字と「下から 30%」は section を入れる前、「What is said again」の本文の数字は C1 の前、
  「crema as it is」の列は HPSS を外す前に測ったもの（README にその旨）。
- crema の重みのライセンスの根拠は作者の issue コメント（issue 42。要約で読んだ）とパッケージ同梱。配るときは
  BSD 2-Clause の著作権表示（Copyright (c) 2017, Brian McFee）を同梱する。Beat This! は MIT（コードも重みも）。crema が RWC を聞いているかは不明。
- crema は合成曲の七の和音に弱い（seed7 で 64.1%）。Beat This! が半分のテンポで追う曲は 2 小節が 1 小節になる（C4）。
- RWC の注釈に調がなく、調は実録音で測れていない。大きなテンポ変化（C3）は未対応。Chord Tracker とは比べていない。

## 未決（ユーザーの判断待ち）

- **配る形（D2）。** 測った数字での比較（1 曲 3 分半、この PC）:

  | | (A) いまのまま | (B) Python＋ONNX | (C) .NET＋ONNX |
  |---|---|---|---|
  | 使う人に要るもの | Python と venv 3 つ（3.7 GB、torch と TensorFlow） | Python と venv 1 つ（450 MB）＋モデル 85 MB | 実行ファイル＋onnxruntime 12 MB＋モデル 85 MB（.NET は lysc と同じ扱い） |
  | 1 曲の時間 | 18 秒（3 プロセス） | 11 秒（2 プロセス）。1 つにまとめて 9 秒の見込み | 拍の経路 3.6 秒（実測）。全体で 5 秒前後の見込み（残りは未実装） |
  | 精度 | README の表 | 同じ（実測で全桁一致） | 拍は再標本化の違いで小節線 F −0.2 ほど、コードは ±0.1 の見込み（CQT の感度から） |
  | 残りの作業 | なし | 1 セッション（1 コマンドにまとめる） | 3〜5 セッション（CQT、特徴量、規則 920 行の移植と突き合わせ） |
  | Lily#・Calamo との揃い | 揃わない | 揃わない（VS Code 拡張から Python を呼ぶ） | 揃う（Calamo と同じ OnnxRuntime、net10.0） |
  | 研究を続けやすいか | 続けやすい | 続けやすい（測定の道具と同じ言語） | 規則を変えるたびに 2 か所。Python は測定用に残る |

  私の勧め: **まず (B)**（1 セッションで「1 コマンド・10 秒」になり、M2〜M3 の規則の変更をそのまま続けられる）。
  規則が落ち着いた時点（M3 の後）で (C) に移す。.NET の試作と CQT の感度の数字は、そのときそのまま使える。
- **和音のないイントロ・弱起を譜面に書くか。** いまは書かない。書くなら N.C. の小節として。`rwc-dev` で落としている和音なしの小節は 14。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）をどうするか。** crema で `rwc-test` は「印 26% で 71%」。
  (a) 目安を下げる、(b) 印を 4 割近くまで増やす、(c) BTC を 2 つ目の耳にする。決まるまで閾値は 0.84。
- **BTC を 2 つ目の耳として配るか。** 入れると `rwc-test` でルート 87.3% → 88.7%・長短 82.1% → 83.8%。
  条件は BTC の作者が重みの扱いを明言すること。jayg996/BTC-ISMIR19 に issue で尋ねるのはユーザー（尋ねていない）。
- **section の書き方。** 細切れ（1 曲 10 セクション）が読みにくければ、条件を緩めるか、section を書かない指定を足す。
- **値段・印の式・`SAID_AGAIN`・C1 の定数・HPSS なしは `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習では
  ないつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P と違うなら、その種類の曲を数曲。Chord Tracker で読んだ結果があればそれも。
- 名前の重複の確認、リモートへの push（ROADMAP §5）。
