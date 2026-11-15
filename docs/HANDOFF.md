# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-16 夜（ユーザーの音楽フォルダの録音で、頭のずれとメモリを直した。拡張 0.2.2。HEAD は `git log -1` で確かめる）。

### D4 の形（2026-11-15、オーナーと話して決めた。依頼 4・[便り](repro/lilysharp-requests-2026-11-15-listening.md)）

**Lily# の設計メモ `C:\MyProj\LilySharp\docs\recording-design.md`（セッションの頭に読む）:** 紐づけは隣のファイル（形式 2）だけ、`.lys` には何も書かない。
m4a は Lily# の LSP が Media Foundation で復号する（音を渡す口は要らない）。形式 2 の項目と書き手の取り合いは設計メモ §6 で未決。

- **目標は Transcribe!（Seventh String）と同じ体験を Lily# で**（オーナーは長年の利用者、用途の中心はエレキベースの耳コピ）。
- **人が打った・動かした小節線が正。** 機械の読みは別に残さない。残すのは線ごとの「人が確かめた」印だけ → `x.dettato.json` を**人が直してよい形式 2** に。
- **Dettato が用意するもの:** 拍の時刻と調律のセント（2026-11-16 に入れた。`tuning` は 12 分割で数え直した整数セント、`beats` は小節の等分。SIDECAR.md・README）、
  人の小節線を受けてコードを読み直す入口（まだ）。

## 現在地

**`dettato <録音> [-o <x.lys>] [--force]` が、mp3・m4a・mp4・aac・flac・wma・wav から `.lys` と `.dettato.json` を録音の隣に書く**
（Python なし。RWC_P001 の mp3 で 6.3 秒）。配る形は .NET＋ONNX の実行ファイルを VS Code 拡張に入れる（Calamo と同じ形）。
**拡張 `yotsuda.dettato` 0.2.2 をこの PC の VS Code に入れた**（`extension/`、`tools/deploy-extension.ps1`。113 MB、Lily# とは別の拡張。0.2.2 で `tuning`・`beats` と下の頭のずれ・メモリの直しが入った）。
コマンドは 1 つ（録音を選ぶか右クリック → 隣に `.lys` と `.dettato.json` → 開く）。**VS Code の中で動くことはユーザーが確かめた（0.2.0、My Life の mp3）。**
**その 1 曲から直したこと（README の「Two half bars are one bar」）:** 半端な小節 2 つを 1 小節に戻す（`MEND_BARS`）、テンポは揃った小節の平均（`EVEN_TEMPO`）。

### コマンドと入力（D3。`Program.cs`・`Audio.cs`。README の「A recording as it comes」、Lily# 向けは便りの §2a）

- stdout は書いた `.lys` のパス 1 行、stderr は段階ごとに 1 行で失敗時は最後の行が `dettato: <理由>`。終了コード 0 書けた／1 録音が読めない／
  2 引数（`.lys` が既にある、を含む）／3 読み取り器自身。2 つのファイルは `.part` に書いてから置き換える。5 秒未満は読まない。
- `dettato --version --json` → 版・`protocol` 1・`commands`・`inputs`（拡張が `bin\dettato.json` から返す）。版は `Program.cs` と `extension/package.json` の 2 か所。
- **wav 以外は Windows の Media Foundation（NAudio 2.2.1、MIT）で読む。** 復号器は Windows のもので、配るものに入れない。素の wav は `Wav.cs` のまま。
- **Media Foundation はエンコーダの足す無音を頭に残す**（ffmpeg やブラウザは取り除く）。`Audio.Lead` がファイルの記録（Xing/Info＋LAME の delay、MP4 の編集リスト）
  から取り除く。素の `.aac` は残る。**「Info」（CBR）のフレームは無音で出るが「Xing」（VBR）は Media Foundation が飛ばす**。
- **ユーザーの音楽フォルダ（`F:\Music`、mp3 19597・m4a 350）で測った（2026-11-16）:** mp3 は 150 曲中 149 曲が Xing/Info なし（iTunes 製、123 曲が `iTunSMPB` を持つ）、
  多くに絵（1 MB を超えるものも、ID3 の長さが違うものも）。m4a は iTunes 11・ボイスメモが編集リストなしで `iTunSMPB`（2112）。ffmpeg の wav と比べて Dettato は
  その mp3 で 528 標本早く、その m4a で 2112 遅かった → **合わせる相手は再生する側**（Lily# の mp3 はブラウザ＝Chrome の `decodeAudioData` は ffmpeg と同じ最初の標本、
  m4a は Lily# の LSP が `Audio.cs` を写した Media Foundation）。`Mp3Lead` は Xing/Info なしで −528（頭に無音を足す）、`Mp4Lead` は編集リストなしで `Priming()`。
  7 種すべて ffmpeg と 0 標本（`scratch/d3/user/<種類>/`、`ref.wav` が ffmpeg。測り方は `heard_check.py <フォルダ> ref.wav`）。**Lily# の `Mp4Lead` にも写してもらう（依頼 5）**。
  Chrome で測る道具は `tools/chrome_lag.py`（＋`tools/cdp.mjs`。ヘッドレスの Chrome を CDP で動かす。`--dump-dom` は非同期の復号を待たない）。
- **長い録音: 58 分（48 kHz、バンド練習の mp3）は読める。** 1500 小節、Calamo が CPU を使っていない時で 74〜81 秒（Beat This! が 60 秒）。メモリのピークは 9.6 → 7.1 GB
  （`Audio.Read` を 1 本の配列に、`Program.Read` で使い終えた配列を手放して `GC.Collect`。出力は前と一字一句同じ、seed1 を wav から通しで読んでも同じ）。
  残る大きい所: `Features.Of` の +1.2 GB（全体の STFT を持つらしい）、Beat This! の段の +1.2 GB（未調査）。
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

- 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。RWC の正解は両端を語らない。
- ONNX を書き直すとき: 旧式のトレース直後の torch のモデルは同じプロセスで答えが変わる（比べる前に読み直す）。dynamo は `PYTHONUTF8=1` が要る。

## 次の一手

**ユーザーの録音で、譜面の出来を聴いてもらう。** 読めるか・時間・頭のずれ・長い録音は 2026-11-16 に機械で見た（上の「コマンドと入力」）。
残るのは耳で聴く所で、ユーザーの手が要る。1 曲目の My Life（`scratch/d3/mylife/`）は 0.2.1 で「さほど違和感はない」（ブリッジの C#dim・F#7/C# は
1 つずつ確かめていない。3 拍の小節が 1 つ残る）。次の候補: ユーザーがベースで弾く曲・コードの分かっている曲（あれば数字も）、バンド練習の録音
（`F:\Music\ギャラン☆Do!\`。58 分の 1 本は `scratch/d3/user/long-only/` に譜面がある。曲の切れ目でテンポが変わるので C3 とつながる）。

- **D4 の Dettato 側で残るのは、人の小節線から読み直す入口だけ**（Python は `--lines` の道。.NET の `--lines` は `.beats` を読む。形式 2 の小節の秒を読むのが素直）。
  形式 2 の中身は Lily# の設計メモ §6 が決まってから。配り直すときは版を 2 か所（`Program.cs`・`extension/package.json`）と CHANGELOG で上げて `tools/deploy-extension.ps1`。
- **Lily# の依頼（`docs/repro/REQUESTS.md`。セッションの頭に見る）:** **1 は Lily# が作った**（master `da1d1caac`〜`7dd898bf9`、次の版から。
  「Convert Recording to Lily# (.lys)… (Preview)」、Dettato が無ければ入れるよう勧める、鍵 `lilysharp.dettatoAvailable` は Lily# が自分で立てる＝Dettato の拡張は何も立てなくてよい）。
  Dettato 側で確かめて行に書いた（取り決めどおり、テスト 6/6、気づいた 3 点。Lily# が 3 点に返事した）。**Lily# のその版が出たら拡張の `dettato.transcribe` を外して 0.3.0**。
  4 は Lily# が作り中（S1〜S3 済み、次は S4）、`tuning`・`beats` は受け取られた。**5（m4a の `iTunSMPB` を Lily# の `Mp4Lead` に写す）は 2026-11-16 に出した。**
  入るまで、iTunes の m4a では Dettato の小節が Lily# の鳴らす音より 48 ms 早い（Lily# の m4a 再生は master だけ）。3 は `done` にするのは確かめてから。
- 返事を待つ間にできる一手: C3（テンポの大きな変化。範囲のテンポ・人の拍マーカーとつながる）。規則を触るなら「.NET の規則」の手順で。

やり残し（急がない）:
- **Beat This! の `small0`（8 MB）と半精度は測れていない**（配布元 cloud.cp.jku.at が 500）。取れたら `track_beats.py --model small0` で `final0` と比べる。
- 和音のないイントロの小節（`rwc-dev` の P027）は譜面に入れていない。`fold`・`beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# は `r` で N.C.）。
- 最後の和音の終わりは `.dettato.json` にない。`check` は `lysc check --batch` で速くなる。crema の `chord_root`・`chord_bass`・`chord_pitch` は未使用。

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
- **D4:** 小節の `beats` に Beat This! の拍を使う（小節の中の拍が拍子の数か倍数で、等分から 1/4 拍以内のとき）。RWC の拍の注釈から 70 ms 以内が
  等分と同じ（`rwc-dev` 96.2%・`rwc-test` 94.3%）、35 ms 以内は 0.2 ポイント下。拍が 20 ms の格子にあり、RWC は小節の中で揺れない。テンポの揺れる録音では未確認。
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

- **セントの `tuning`:** librosa との一致は `seed1` の 24 曲だけ。伸びたテープ、±50 セントを超えるずれ、曲の途中で音高が動く録音は見ていない（RWC は −9〜+4）。
- **.NET で確かめていないこと:** `RWC_P059` の調律が 0.01 ずれる理由（再標本化か単精度の FFT か、切り分けていない。表には出ない）。
  **入力（D3）:** ユーザーのフォルダにない種類（編集リストと `iTunSMPB` が両方ある m4a はどちらが勝つか未確認、いまは編集リストが先）、動画つきの mp4、
  `iTunSMPB` の終わりの埋め草（m4a で 416〜960 標本長い。譜面には効かない）、VS Code の Chromium の mp3 の頭（Chrome だけ測った）、メモリの少ない PC で 1 時間の録音、
  実録音を mp3 にしたときの精度（1 曲だけ）、Windows 10 や N エディション。RWC を wav から通しで読んだ数字（`final0` は RWC を学習済み、fold のモデルは ONNX にしていない）。
  規則の一致は「同じ拍・確率・特徴量を与えたとき」で、通しの経路は seed1 の 24 曲だけ。拍子が 5/4・7/4 などの曲、`dettato read` が失敗する曲（268 曲に 1 つもなかった）。
  別の CPU での速さ（この PC は 24 コア）。`small0` と半精度。
- 弱起つき・section つきの `.lys` は `lysc check` と `lysc layout` を通しただけ。描いた譜面と鳴り方は目と耳で確かめていない。
- 旋律だけの弱起に、自作の読み取り器は和音を書く。半拍より短い弱起は消える。弱起は 1・2・3・4・6 拍だけ（`PARTIAL`）。
- 原因を見ていないもの: `rwc-dev` で最初の小節線が正解と違う 11 曲、`seed41` で外した曲、自作の読み取り器に正解の小節線を与えると実録音のルートが 77.4 → 77.1% に下がること。
- crema の重みのライセンスの根拠は作者の issue コメント（issue 42）とパッケージ同梱。配るときは BSD 2-Clause の表示（Copyright (c) 2017, Brian McFee）を同梱。Beat This! は MIT。
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
