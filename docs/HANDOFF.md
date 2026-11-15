# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-15（コードは変えていない。D4 の形をオーナーと決め直し、Lily# に依頼 4 を出した。HEAD は `git log -1` で確かめる）。

### D4 の形（2026-11-15、オーナーと話して決めた。依頼 4・[便り](repro/lilysharp-requests-2026-11-15-listening.md)）

- **目標は Transcribe!（Seventh String）と同じ体験を Lily# で。** オーナーは長年の利用者で、用途の中心はエレキベースの耳コピ。
  小節を押すとそこから原音、ループ、再生中に M（小節）・B（拍）・S（セクション）でマーカーを打って後から動かす、範囲のマーカーからテンポ、
  速度（音高は保つ）、セント単位の音高とオクターブ、**音高と速度の連動（オン・オフ）**、EQ。セント単位は、古いテープの録音が伸びで速度と音高ごとずれるのを戻すため。
- **人が打った・動かした小節線が正。** 機械の読みを別に残す必要はない（オーナーの問いで確かめた。読み直しは人の線を入力に取ればよく、測定は作業用フォルダでする）。
  残すのは線ごとの「人が確かめた」印だけ（読み直しで動かさない、確かめていない線を薄く描く）。→ `x.dettato.json` を**人が直してよい形式 2** にする案
  （形式 1 は「誰も書き換えない」。SIDECAR.md は Lily# の返事を見てから直す）。
- 連動オンは再標本化だけで済む（伸縮のエンジン不要）。連動オフと音高を保った速度変更にはエンジンが要る（候補と未確認の点は便り §2）。
- **Dettato が用意するもの:** 復号した音（webview の Chromium は AAC を復号できないはず。未確認）、拍の時刻、調律のずれのセント（`Features` の
  `tuning` が既に測っている。半音の ±1/2 まで）、人の小節線を受けてコードを読み直す入口。拍とセントは項目を足すだけなので形式 1 のまま足せる。

## 現在地

**`dettato <録音> [-o <x.lys>] [--force]` が、mp3・m4a・mp4・aac・flac・wma・wav から `.lys` と `.dettato.json` を録音の隣に書く**
（Python なし。RWC_P001 の mp3 で 6.3 秒）。配る形は .NET＋ONNX の実行ファイルを VS Code 拡張に入れる（Calamo と同じ形。ユーザーの答えは
「VS code extension」の一言で、.NET と読んだのは Claude）。Python の読み取り器と .NET の規則は変えていない（wav の道は 124 曲で前と同じ譜面）。
**拡張 `yotsuda.dettato` 0.2.1 をこの PC の VS Code に入れた**（`extension/`、`tools/deploy-extension.ps1`。113 MB、Lily# とは別の拡張）。
コマンドは 1 つ（録音を選ぶか右クリック → 隣に `.lys` と `.dettato.json` → 開く）。**VS Code の中で動くことはユーザーが確かめた（0.2.0、My Life の mp3）。**
**その 1 曲から直したこと（README の「Two half bars are one bar」）:** 拍のモデルが小節の途中に 1 拍目を数えた所（半端な小節 2 つで 1 小節分の拍）を
1 小節に戻す（`MEND_BARS`、`lines_of`）。テンポは中央の小節の 1 割以内の小節の平均から（`EVEN_TEMPO`。拍は 20 ms 刻みで、中央値だと 130.9 が 130 になった）。

### コマンドと入力（D3。`Program.cs`・`Audio.cs`。README の「A recording as it comes」、Lily# 向けは便りの §2a）

- stdout は書いた `.lys` のパス 1 行、stderr は段階ごとに 1 行で失敗時は最後の行が `dettato: <理由>`。終了コード 0 書けた／1 録音が読めない／
  2 引数（`.lys` が既にある、を含む）／3 読み取り器自身。2 つのファイルは `.part` に書いてから置き換える。5 秒未満は読まない。
- `dettato --version --json` → 版・`protocol` 1・`commands`・`inputs`（拡張が `bin\dettato.json` から返す）。版は `Program.cs` と `extension/package.json` の 2 か所。
- **wav 以外は Windows の Media Foundation（NAudio 2.2.1、MIT）で読む。** 復号器は Windows のもので、配るものに入れない。素の wav は `Wav.cs` のまま。
- **Media Foundation はエンコーダの足す無音を頭に残す**（mp3 CBR 39 ms、VBR 13 ms、AAC 23 ms。ffmpeg やブラウザは取り除く）。`Audio.Lead` が
  ファイルの記録（mp3 の Xing/Info＋LAME の delay、MP4 の編集リスト）から取り除き、wav とのずれは 0 サンプル。Xing のない mp3 と素の `.aac` は残る。**「Info」（CBR）のフレームは無音で出るが「Xing」（VBR）は Media Foundation が飛ばす**。
- seed1 を mp3・m4a にして読むと精度は wav と同じ、譜面は同じではない（小節の数が違う曲が 3・5 曲。数字は README）。48000 Hz の wav は同じ譜面。
- 測る道具: `dettato audio` → `tools/heard_check.py`（頭のずれ）、`tools/kinds_check.py`（譜面どうし）。試した録音は `scratch/d3/`（消してよい）。ffmpeg は PATH にある。

### .NET の規則（`Reader.cs` 532 行・`Form.cs` 213・`Writer.cs` 145・`Np.cs` 105。README の「In .NET: the rules」。全体で 1894 行）

- 移したのは `--ears` の道だけ。自作の拍追跡・`MODEL`・`--tempo`・`why` は Python に残す。
- **`.dettato.json` は一字一句、`.lys` は 1 行目以外、268 曲すべてで Python と同じ**（`tools/read_check.py`。比べ方とコマンドは README）。
  同じ拍と確率（`--lines`・`--heard`）と同じ特徴量（`--net scratch/net`）を与えて比べる。`measure.py … --read` は .NET の譜面を数える。
- 一致させるために真似たこと（`Np.cs`）: numpy の和の順、単精度のままの平均とノルム、Python の `//` と `round`、`repr`、同点の扱い。
- **規則を変えるときの手順:** Python で試して測る → .NET に写す → `read_check.py` で全セット一致を確かめる（Python が正）。

### .NET の耳と特徴量（`dettato ears`・`dettato features`。README の「In .NET」の最初の 2 節と「Without torch and TensorFlow」）

- 耳: wav → `.beats`（Beat This!）と `.npz`（crema）、特徴量: wav → `.feat.npz`（`oenv`・`chroma`・`bass`・`rms`・`tuning`）。Python との一致の
  度合いは README、突き合わせは `tools/net_check.py`。`measure.py --net scratch/net` が librosa の代わりに読み、表は全列同じ。
- librosa のベース用クロマは曲によって 1 フレーム長い（.NET も同じ長さ、規則は配列ごとに切る）。デジタル無音のフレームのクロマは比べるとき除く。
- **`--ears` では HPSS を外した**（`use_ears()` が `HARMONIC_FRAMES = 0`。特徴量は `scratch/feat-h0/`）。自作の読み取り器は 31 のまま。
- **1 曲（RWC_P001、3 分 27 秒）の時間:** Python の venv 3 つで 18 秒、venv 1 つ＋ONNX で 11 秒、`dettato read` で 5.5 秒
  （Beat This! 3.7、CQT と crema 1.0、特徴量 0.3、起動 0.3、規則 0.1）。モデルは crema 2.2 MB、Beat This! `final0` 82.9 MB、ほかに 11.5 MB。
- **onnxruntime のスレッド数は 8 に絞る。** 既定（24 全部）だと Beat This! が 1 曲 10 秒に落ちる。torch は 2.0〜2.4 秒で、ONNX より速い。

### 数字（`measure.py all`、2026-11-14 の最後。lysc check 1388/1388、sidecar 1388/1388）

| `--ears`（crema） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 86.4% | 87.3% | 86.9% | 95.1 / 93.1 / 94.5% |
| ルート＋長短 | 82.3% | 82.2% | 82.2% | 91.6 / 88.4 / 92.4% |
| ベース音まで | 64.4% | 64.9% | 64.7% | 73.4 / 58.5 / 73.5% |
| 小節線 F | 93.6% | 94.9% | 94.2% | 96.7 / 90.9 / 93.6% |
| テンポ・拍子 | 46・49 | 47・49 | 93・98 | |
| 印 → 拾う誤り | 30.2% → 77.8% | 25.7% → 70.3% | 28.0% → 73.9% | 17.5% → 70.2% |

- 自作（モデルあり）seed1 / 7 / 11: ルート 95.6 / 96.0 / 97.2%、小節線 95.0 / 97.6 / 99.1%。`measure.py head`（`--ears`）: 最初の小節線 `rwc-dev` 39/50・`rwc-test` 45/50、最後 23/50・13/50。

### 持ち越す知見（README にないもの）

- 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。
  RWC の正解は両端を語らない（弱起なし）。弱起は `"bar": 0, "pickup": 拍数`、section は `Pickup`。
- ONNX を書き直すとき: 旧式のトレースで書いた直後の torch のモデルは同じプロセスの中で答えが変わる（比べる前に読み直す。
  `bt_onnx.py` はそうしている）。dynamo の書き出しは絵文字を出すので `PYTHONUTF8=1` が要る。

## 次の一手

**ユーザーの録音で試すのを続ける。** 1 曲目は `F:\Music\Billy Joel\Greatest Hits Volume I & Volume II [Disc\1-01 My Life.mp3`（読み直しは
`scratch/d3/mylife/`）。ユーザーの所見は「最初の方のコードは違和感ない、L54 付近からおかしい」で、小節のずれは直した。**直した後（0.2.1）はユーザーが聴いて
「さほど違和感はない」**（ブリッジの C#dim・F#7/C# を 1 つずつ確かめたわけではない）。残る 3 拍の小節が 1 つある。見る所: 読めるか、時間、iTunes などの m4a・ほかのエンコーダの mp3 の頭のずれ（ffmpeg で wav にしたものと
`heard_check.py` で比べる）、長い録音のメモリ、譜面の出来（コードの分かっている曲なら数字も）。

- **並行して D4 の Dettato 側（上の「D4 の形」）:** (1) `x.dettato.json` に拍の時刻と調律のセントを足す（形式 1 のまま。`Writer.cs` と
  `read_chords.py` の両方、`read_check.py` で一致、`sidecar.py` も）、(2) 復号した音を Lily# に渡す口（渡し方は依頼 4 の返事を見て。`dettato audio` が土台）、
  (3) 人の小節線から読み直す入口（`--lines` の道が Python にある）。形式 2 は Lily# の返事の後。
- **Lily# の依頼（`docs/repro/REQUESTS.md`。セッションの頭に見る）:** 1 はオーナー決定「メニューは作る。着手は D3 で呼び出しの形が固まってから、固まったら行に書く」
  → D3 の形（便り §2a）を固まったとして行に書くかをオーナーに確かめてから書く。2 は「今は作らない」のまま、それを広げた 4 が `open`。3 は Lily# で直った（`done` にするのは確かめてから）。
  Lily# がメニューを出す鍵は `lilysharp.dettatoAvailable`、開発用の環境変数は `LILYSHARP_DETTATO_PATH`（拡張が `onStartupFinished` で立てる。まだ）。
  メニューが Lily# に入ると同時に拡張の `dettato.transcribe` を外す。
- 返事を待つ間にできる一手: C3（テンポの大きな変化。範囲のテンポ・人の拍マーカーとつながる）。規則を触るなら「.NET の規則」の手順で。

やり残し（急がない）:
- **Beat This! の小さい版（`small0`、8 MB）は測れていない**（配布元 cloud.cp.jku.at が 500）。取れたら `track_beats.py --model small0` で
  合成曲、RWC は `final0` と相対比較。時間と大きさの大半が Beat This! なので効く。半精度（fp16）も測っていない。
- 和音のないイントロの小節（`rwc-dev` の P027）は譜面に入れていない。`fold`・`beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# は `r` で N.C.）。
- 最後の和音の終わりは `.dettato.json` にない（拍は D4 で足す）。`check` は `lysc check --batch` で速くなる。
- crema の `chord_root`・`chord_bass`・`chord_pitch` は未使用（ベース音は自作のクロマ）。crema＋BTC 用の値段と印の選び直しは BTC が決まってから。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 3 つ＋試し 1 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0、onnx・onnxruntime・onnxscript）が Beat This!・BTC・ChordMini。
  `.venv-crema`（tensorflow、tf-keras、crema 0.2.0、`setuptools<81`、tf2onnx、onnxruntime）が crema。
  `scratch/d2/venv-onnx`（numpy、librosa、soxr、soundfile、onnxruntime）が ONNX だけの耳。作り方は README。
  素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、`$env:DETTATO_LYSC='C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe'`。
  `.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'`、`$env:TORCHINDUCTOR_CACHE_DIR='C:\MyProj\Dettato\scratch\torchinductor'`、`$env:PYTHONUTF8='1'` も。
  `make` で `tim/` を作り直すときは `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）、
  `samples` は `DETTATO_SAMPLES`（`C:\MyProj\LilySharp\samples`。場所は未確認）も。
- 動作確認の 1 コマンド（20 秒）:
  `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears` → seed1 ルート 95.1%・小節線 96.7%・ベース 73.4%、`rwc-dev` 86.4% / 82.3% / 93.6%、`rwc-test` 87.3% / 82.2% / 94.9%。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 7 秒。`all` は 20〜25 分。RWC の特徴量の作り直しは HPSS ありで 20 分、なしで 3 分。2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
  止めるときは `measure.py all` を含む python.exe を止める。速さは裏の仕事が終わってから測る（24 コアでも ONNX と torch は取り合う）。
- `lysc check <1 枚>` の合否は終了コードと `warning` / `: error`。`lysc <コマンド> --batch <一覧>` なら 1 プロセスで多数を扱える。`C:\MyProj` 全体の再帰検索は固まる。pwsh MCP の `Update-MatchInFile` で書き換えたファイルは、Edit ツールの前に読み直しが要る。
- **README.md などユーザーがプレビューで開いている Markdown は、Edit を 1 回ずつ出す**（並べるとエラーのダイアログが編集の数だけ出る）。

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
| `scratch/net/`（`out/dettato.exe`、`seed1`・`seed7`・`seed11`・`rwc-p` の `.beats`・`.npz`・`.cqt.npy`・`.feat.npz`、ほかの合成曲 4 セットの `.feat.npz`） | README の「In .NET」の各節の末尾（.NET SDK 10.0.401。RWC は耳 9 分、特徴量 33 秒） |
| `scratch/net/read/<セット>/`（.NET の譜面。`head/seed141` は `seed141`）、`scratch/pyread/<セット>/`（比べる相手の Python の譜面） | README の「In .NET: the rules」の末尾（RWC 100 曲で 27 秒） |

`scratch/c1*.py`・`c2*.py`・`b3*.py` は commit の外（残す）。消してよい: `scratch/` のほかの古いスクリプトとログ、`read-all/`・`c1out/`・`c1lys/`・`c2out/`・`cmtest/`・`net/one/`、`scratch/d2/` の `*.log`・`one*/`・`beats-*/`。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100、`held/seed101`、`head/seed141` | 合成曲の学習用・定数を選ぶ用 | 自作モデルは seed100 しか聞いていない。`head/seed141` は C1 の定数を選んだ |
| seed1・7・11、seed21・31、`still/`・`drift5/`・`tim/`、`samples` | 合成曲のテスト | 定数を選ぶときに数字を見たことがある。C1 では seed1 の曲ごとの行を見た。**HPSS を外す判断は seed1・7・11 の集計と `rwc-dev` で行った** |
| `seed41` | 合成曲のテスト（頭の無音と弱起） | 集計だけを見た。曲ごとには見ていない |
| `rwc-dev`（P001〜P050） | 実録音で見て決める側 | 値段、`SURE_OF`、`DOUBT_EARS`、`SAID_AGAIN`、C1 の定数、HPSS なしはここで決めた |
| `rwc-test`（P051〜P100） | 実録音で数えるだけの側 | 集計だけを見ている。HPSS なしの集計は、決める前に `rwc-dev` と同時に出た（どちらも同じ向き） |

公開モデルで測るときは、そのモデルが測る曲を学習していないかを先に確かめる。Beat This! は RWC を学習している（`--rwc` で fold のモデル）。BTC は README 上は使っていない。ChordMini は使っている。crema は不明。速さと一致だけの測定（D2・D3）は `final0`。精度の数字には使っていない。

## 捨てた案（試し直さない）

- **D2:** onnxruntime を既定のスレッド数で走らせる（24 コアで Beat This! が 2.5 倍遅い）。`--ears` で HPSS を残す。
- **C1:** 鳴り終わりを「前と同じ和音で、かつ静か」で見分ける。`--ears` の無音を自作と同じ「中央値の 1/10」で見る。
  crema の N だけで無音を見る。弱起の中の和音を拍ごとに書く（Lily# が警告する）。
- **繰り返しの照合で誤りを直す・印を付ける**（C2）: 読みの多数決、確率の平均、「相手がいない小節に印」。正解の対でも +2 ポイントまで。
- 小節を戻すのを 4 拍以上の曲だけにする（合成曲で悪化、seed7 90.4%）。section をルート＋長短だけで同じとみなす、`SAID_AGAIN` 0.90 以下、完全一致だけ。
- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回る。
- **コードモデルの候補で外したもの:** madmom（CC BY-NC-SA）。Jiang ほか・omnizart（重みの明記なし、確率を出さない）。
  BACHI（楽譜からの認識）。ChordMini（BTC の派生で RWC を学習）。
- crema 用に値段を選び直す、印の式を合わせ直す。印に 3 つ目の耳を足す。`--ears` で値段を高くする、ベースのクロマを足す。
- 印の確信度の案（BTC、`rwc-dev`）: 半拍ごとの最小値、2 番目との差・比、対数の平均、エントロピー、N・X の確率、コードの長さ、
  ベースがルートか。特徴 27 個・勾配ブースティング。短調への一律の割増。別候補を確率の合計から選ぶ。
- 小節線を別に探す手法（新奇度、平均クロマ）。自作の読み取り器で HPSS をやめる、NNLS、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6。規則だけでコードの重みを詰める。
  短調の属和音の重みを下げて調を当てる。小節の長さの事前分布、拍の長さの局所中央値。七の和音の迷いまで印にする（自作モデルで効率が落ちた。`--ears` では未確認）。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源なし。Winterreise は歌曲。AAM は合成。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点・未確認

- **.NET で確かめていないこと:** `RWC_P059` の調律が 0.01 ずれる理由（再標本化か単精度の FFT か、切り分けていない。表には出ない）。
  **入力（D3）:** ffmpeg 以外が書いたファイル（iTunes の AAC は無音 2112 標本。Media Foundation が編集リストを無視するのは ffmpeg の
  m4a で見ただけ）、絵の入った mp3、動画つきの mp4、数分を超える録音、実録音を mp3 にしたときの精度（1 曲読めただけ）、
  Windows 10 や N エディション（Media Foundation の復号器の有無）。RWC を wav から通しで読んだ数字（`final0` は RWC を学習済みなので数えない。
  fold のモデルは ONNX にしていない）。規則の一致は「同じ拍・確率・特徴量を与えたとき」で、通しの経路は seed1 の 24 曲だけ。
  拍子が 5/4・7/4 などの曲、`dettato read` が失敗する曲（268 曲に 1 つもなかった。例外の種類を絞って握っている）。
  メモリ（100 曲を 1 プロセスで読むと最大 2.4 GB）。別の CPU での速さ（この PC は 24 コア）。`small0` と半精度。
- **D4 の 2 点は Lily# が答えた（依頼 2 の行）:** `Beats` は弱起に `A` を立てない（n は弱起があれば T 以前の `A` の数、なければ − 1）、
  `placeAt` は弱起に 0。SIDECAR.md の「Not checked yet」はまだ直していない（形式 2 と一緒に）。
- 弱起つき・section つきの `.lys` は `lysc check` と `lysc layout` を通しただけ。描いた譜面と鳴り方は目と耳で確かめていない。
- 旋律だけの弱起に、自作の読み取り器は和音を書く。半拍より短い弱起は消える。弱起は 1・2・3・4・6 拍だけ（`PARTIAL`）。
- 原因を見ていないもの: `rwc-dev` で最初の小節線が正解と違う 11 曲、`seed41` で外した曲、自作の読み取り器に正解の小節線を与えると実録音のルートが 77.4 → 77.1% に下がること。
- crema の重みのライセンスの根拠は作者の issue コメント（issue 42。要約で読んだ）とパッケージ同梱。配るときは
  BSD 2-Clause の著作権表示（Copyright (c) 2017, Brian McFee）を同梱する。Beat This! は MIT（コードも重みも）。crema が RWC を聞いているかは不明。
- crema は合成曲の七の和音に弱い（seed7 で 64.1%）。Beat This! が半分のテンポで追う曲は 2 小節が 1 小節になる（C4）。
- RWC の注釈に調がなく、調は実録音で測れていない。大きなテンポ変化（C3）は未対応。Chord Tracker とは比べていない。

## 未決（ユーザーの判断待ち）

- **`x.dettato.json` を人が直してよい形式 2 にすること**（オーナーは「人の小節線が正」に同意。形式の中身と、Lily# と Dettato の書き分けは依頼 4 の返事待ち）。
- **和音のないイントロ・弱起を譜面に書くか。** いまは書かない。書くなら N.C. の小節として。`rwc-dev` で落としている和音なしの小節は 14。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）をどうするか。** crema で `rwc-test` は「印 26% で 71%」。
  (a) 目安を下げる、(b) 印を 4 割近くまで増やす、(c) BTC を 2 つ目の耳にする。決まるまで閾値は 0.84。
- **BTC を 2 つ目の耳として配るか。** 入れると `rwc-test` でルート 87.3% → 88.7%・長短 82.1% → 83.8%。
  条件は BTC の作者が重みの扱いを明言すること。jayg996/BTC-ISMIR19 に issue で尋ねるのはユーザー（尋ねていない）。
- **section の書き方。** 細切れ（1 曲 10 セクション）が読みにくければ、条件を緩めるか、section を書かない指定を足す。
- **値段・印の式・`SAID_AGAIN`・C1 の定数・HPSS なしは `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習では
  ないつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P と違うなら、その種類の曲を数曲（Chord Tracker の結果があればそれも）。名前の重複の確認、push（ROADMAP §5）。
