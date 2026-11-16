# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-16 夜（半分の小節を組む＝拡張 0.4.1。その前の同日: テンポを小数 1 桁に、lysc のシェイプ、2 番カッコ、短い section の名前を隠す、0.08 秒の小節、2 つ目の耳 BTC。HEAD は `git log -1` で確かめる）。

### 2 つ目の耳 BTC（2026-11-16、オーナー決定。0.4.0。README「Which model hears the chords」の末尾・ROADMAP §5）
- crema と BTC の確率を平均（`Btc.cs`・`tools/two_ears.py`。ONNX は `tools/d2/btc_onnx.py` → `scratch/d2/btc-large-voca.onnx`、13 MB、拡張に同梱）。`rwc-test` ルート 87.3 → 88.8%・長短 82.2 → 83.9%。`measure.py --ears` の既定は `crema+btc`。

### 0.08 秒の小節と半分の小節（2026-11-16、Lily# 依頼 10・オーナーの報告・糸。README「A bar of 0.08 s」）
- **入れた（0.3.5）:** 拍追跡の小節線で、中央値の 1/4 未満の小節は短い方の隣と 1 つに（`TINY_BAR`／`TinyBar`）。小節線 F が全セットで上がった。
- **入れた（0.4.1、オーナー了承。README「Half bars」）: 半分の小節**（Beat This! が途中で倍の速さの拍を取る。オーナー: 糸は市販譜で 60〜70）。`HALF_BARS`（中央値の 0.35〜0.7）・`pair_halves`／`PairHalves`。
  半分に挟まれた 1 小節は拍で 2 つに切る（糸の 9 小節目＝半小節ずれの原因だった）→ 続く半分を 2 つずつ。偶数個は頭から、奇数個は余りの置き場（頭か終わり）を調で選ぶ（`key_heard`＝耳のフレームごとの最尤和音で `find_key`。
  線の後の 1 拍の主和音 ×（1＋線の前の 1 拍の属和音））。偶数個まで調に任せると糸の 58・59 が `C | G D/F#` に割れたのでやめた。糸は 89 → 71 小節、全部 3.6〜5.0 秒（`scratch/ito/ito041.lys`）。
  RWC で 15 曲・合成曲で 5 曲の小節線が変わる。**半分のテンポで読んでいた RWC の 4 曲（P050・P095・P034・P038）では本当の小節をまとめて悪くなる**（長さだけでは見分けられない）。

### 同日のほかの変更（詳しくは README と git の履歴）
- テンポは小数 1 桁（`TENTH_TEMPO`。「拍の総数 ÷ 全長」は捨てた）。`.lys` に font と diagram の注記、最初のコードに `lysc shape` のシェイプ（`LILYSHARP_LYSC_PATH`）。
- 今の既定の section（`find_form`・`fold`）: 6 小節以下は続けてまとめ（`SHORT_SECTION`）、一度きりは 32 小節まで（`LONG_SECTION`）、繰り返し記号は form だけ、2 番カッコ（`lead_off`）、
  3 小節未満は名前を隠す（`~B_1`）。改行は 4 小節ごと（`line_sizes`）、0.4.2 から `| break`。**意味で切る `--phrase` に置き換える見込み**（下の「作業中」）。

### D4 の形（2026-11-15、オーナーと話して決めた。依頼 4・[便り](repro/lilysharp-requests-2026-11-15-listening.md)）
**Lily# の設計メモ `C:\MyProj\LilySharp\docs\recording-design.md`（セッションの頭に読む）:** 紐づけは隣のファイル（形式 2）だけ、m4a は Lily# の LSP が復号。
**目標は Transcribe! と同じ体験を Lily# で**（用途の中心はエレキベースの耳コピ）。**人が打った小節線が正**（形式 2）。Dettato は `tuning`・`beats` を入れた（読み直しの入口は不要）。

## 現在地

**`dettato <録音> [-o <x.lys>] [--force]` が、mp3・m4a・mp4・aac・flac・wma・wav から `.lys` と `.dettato.json` を録音の隣に書く**
（Python なし。RWC_P001 の mp3 で 6.3 秒）。配る形は .NET＋ONNX の実行ファイルを VS Code 拡張に入れる（Calamo と同じ形）。
**拡張 `yotsuda.dettato` 0.4.2 をこの PC の VS Code に入れた**（0.4.2: 行の終わりを `| break` に＝オーナー。改行の位置は同じ）（`extension/`、`tools/deploy-extension.ps1`。113 MB、Lily# とは別の拡張）。**コマンドはない**: メニューは Lily# の
「Convert Recording to Lily# (.lys)… (Preview)」で、Lily# が `ext.activate()` で `{path, version, protocol, inputs}` を受け取る。**VS Code の中で動くことはユーザーが確かめた（0.2.0。0.3.x は Lily# のメニューからはまだ）。**

### コマンドと入力（D3。`Program.cs`・`Audio.cs`。README の「A recording as it comes」、Lily# 向けは便りの §2a）

- stdout は書いた `.lys` のパス 1 行、stderr は段階ごとに 1 行で失敗時は最後の行が `dettato: <理由>`。終了コード 0 書けた／1 録音が読めない／
  2 引数（`.lys` が既にある、を含む）／3 読み取り器自身。2 つのファイルは `.part` に書いてから置き換える。5 秒未満は読まない。
- `dettato --version --json` → 版・`protocol` 1・`commands`・`inputs`（拡張が `bin\dettato.json` から返す）。版は `Program.cs` と `extension/package.json` の 2 か所。
- **wav 以外は Windows の Media Foundation（NAudio 2.2.1、MIT）で読む。** 復号器は Windows のもので、配るものに入れない。素の wav は `Wav.cs` のまま。
- **Media Foundation はエンコーダの足す無音を頭に残す**（ffmpeg やブラウザは取り除く）。`Audio.Lead` がファイルの記録（Xing/Info＋LAME の delay、MP4 の編集リスト）
  から取り除く。素の `.aac` は残る。**「Info」（CBR）のフレームは無音で出るが「Xing」（VBR）は Media Foundation が飛ばす**。
- **ユーザーの音楽フォルダ（`F:\Music`）で測った（2026-11-16、README の「A recording as it comes」の末尾）:** mp3 はほぼ Xing/Info なし（iTunes 製）、m4a は編集リストなしで `iTunSMPB`。
  **合わせる相手は再生する側**（Lily# の mp3 は Chrome の `decodeAudioData`＝ffmpeg と同じ、m4a は Lily# の LSP の Media Foundation）。`Mp3Lead` は Xing/Info なしで −528、
  `Mp4Lead` は編集リストなしで `Priming()`。7 種すべて ffmpeg と 0 標本（`scratch/d3/user/<種類>/`、`heard_check.py <フォルダ> ref.wav`）。Chrome は `tools/chrome_lag.py`＋`tools/cdp.mjs`。
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

- 耳: wav → `.beats`（Beat This!）と `.npz`（crema と BTC の平均、0.4.0 から）、特徴量: wav → `.feat.npz`。突き合わせは `tools/net_check.py`、`measure.py --net scratch/net` で表は全列同じ。
- **`--ears` では HPSS を外した**（`use_ears()` が `HARMONIC_FRAMES = 0`。特徴量は `scratch/feat-h0/`）。1 曲 3 分半で `dettato read` 5.5 秒（Beat This! 3.7）。onnxruntime は 8 スレッド（24 だと遅い）。

### 数字（`measure.py all`、2026-11-16 の半分の小節（0.4.1）の後。lysc check 1388/1388、sidecar 1388/1388＝演奏の小節数で）

| `--ears`（crema＋BTC） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 87.3% | 88.8% | 88.1% | 95.5 / 94.4 / 96.7% |
| ルート＋長短 | 82.9% | 83.9% | 83.4% | 92.4 / 90.9 / 94.3% |
| ベース音まで | 65.0% | 66.6% | 65.8% | 76.9 / 65.1 / 80.1% |
| 小節線 F | 93.8% | 95.3% | 94.5% | 97.1 / 91.0 / 94.1% |
| テンポ・拍子 | 47・49 | 47・49 | 94・98 | |
| 印 → 拾う誤り | 35.4% → 86.4% | 28.7% → 72.5% | 32.1% → 79.4% | 14.9% → 73.8% |

- 自作（モデルあり）seed1 / 7 / 11: ルート 95.6 / 96.0 / 97.2%、小節線 95.0 / 97.6 / 99.1%。`measure.py head`（`--ears`）: 最初の小節線 `rwc-dev` 39/50・`rwc-test` 45/50、最後 23/50・13/50。

### 持ち越す知見（README にないもの）

- 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。RWC の正解は両端を語らない。
- ONNX を書き直すとき: 旧式のトレース直後の torch のモデルは同じプロセスで答えが変わる（比べる前に読み直す）。dynamo は `PYTHONUTF8=1` が要る。

## 次の一手

**作業中（2026-11-16 夜。Python の試作は commit 済みで既定オフ＝`PHRASE_SECTIONS = False`、オンにして試す）: 意味で切る section**（オーナー: 調や曲調が変わる所・歌が入る所・ソロで切りたい）。
- オーナーが糸を手で分けた（`scratch/ito/ito-one.lys`: Intro 1–8・A 9–16・B 17–20・C 21–27・Inter 28–31・A2 32–39・B2 40–43・C2 44–50・Solo 51–58・C3 59–）。**繰り返し記号は要らないかも**（オーナー）。
  My Life も（`scratch/sec/phrase/mylife-owner.lys`: 1・9 リフ・17 歌・31 リフ・39 歌・53 ブリッジ・69 サビ（歌と同じコード）・83 リフ・91 歌・105 ブリッジ・121 楽器の歌・129・135 リフ×2・151）。
  **`--phrase` で 2 曲ともオーナーの境目を全部、余分なしで出す**（糸は手で分けていない終わり 67 が増えるだけ。`scratch/sec/try.py`）。
- 試作 `PHRASE_SECTIONS`（`phrase_sections`・`turns_of`・`voice_turns`）: 歌の出入り（Demucs、`--voice <folder>` の `<name>.voc.npy`）・調号の変化（2 つ以上）・6 dB の音量で塊に切り、塊の中は句（約 15 秒＝糸 4 小節・My Life 8 小節）の格子で
  「同じ句 2 回の終わり」か「V→I が小節線をまたぐ」所で切る（句とその繰り返しの間では切らない）。名前は前の section の再演なら A2・A3。fold（繰り返しのコードをそろえる）はしない。
  加えて（My Life）: 線で囲まれた塊が別の所で丸ごと鳴れば、そこの頭と終わりも境目（より長い塊のかけらは手本にしない・別の塊の途中では切らない・続けて鳴れば 1 つ）。
  調の線は、曲の別の所でも始まる並びが 1 小節前から始まるならそこへ（リフは D の小節から）。歌の線の近くの調の線は、そういう頭なら残す（135 のリフ、歌は 138 まで）。
  **糸はオーナーの分け方と一致**（+67 から終わり）。My Life はまだ粗い（39–54・55–83・91–120: 句の格子が 2 小節ずれる）。`rwc-dev` ルート 87.3 → 87.4%（fold をやめた分）。.NET は未移植。材料は `scratch/sec/`（`turns.py`・`dbg3.py`・`starts.py`）。
- 歌: Demucs（`scratch/sep/venv`、htdemucs）で `rwc-dev` 50 曲、小節の歌あり／なし 96.3%・歌の入りと終わり 94.0% を 1 小節以内（`scratch/sep/count.py`。線は dev で選んだ）。`rwc-test` は裏で分離中（`scratch/sep/test.log`）。
  3 分半で 43 秒（8 スレッド）。配る形（(a) Demucs をそのまま・(b) 蒸留した小さなモデル。(b) は学習の音源をオーナーが決める）は未決。
- **糸の M64 は rit ではなく 6 拍の小節**（オーナー。4＋2 拍、M65 に `time 2/4`）。手がかりは Beat This! の downbeat の値: G の小節の頭が今の組み方で平均 2.3、2 拍ずらすと 3.5（`scratch/sec/downs.py`、`.venv-bt`）。
  案: 2 拍ずつ組む所で向きを downbeat の値で選び、入れ替わる所に 2 拍の小節（値段つき）。要るもの: `.beats` に 3 列目（downbeat の値）＝`track_beats.py` と `Beats.cs`、全セットの拍の作り直し。未着手。
  書き方は **Lily# 依頼 13（コードの行の中の `time 2/4`）＝Lily# master `91187cf45` で fixed**（小節の頭だけ。次の `time` か section の終わりまで効く。次の section は見出しの拍子に戻る）。
  **オーナーは Lily# を手元に入れた＝行の中の `time 2/4` で書いてよい**（手元の lysc で通ることを確かめた。2026-11-16）。
- Lily# 依頼 11（小節数が違っても少ない方まで鳴らす）は Lily# master `184efc304` で fixed。オーナーの版で確かめたら `done`。

**その前の一手（0.4.1 の確認）**: 糸（`scratch/ito/ito041.lys`）の小節の長さ。form の繰り返し記号・2 番カッコは、上の section の作り直しで要らなくなる見込み。
**それから、ユーザーの録音で譜面の出来を聴いてもらう**（耳で聴く所。機械で見られる所は 2026-11-16 に見た）。My Life（`scratch/d3/mylife/`）は 0.2.1 で
「さほど違和感はない」（ブリッジの C#dim・F#7/C# は未確認、3 拍の小節が 1 つ）。次の候補: ユーザーがベースで弾く曲・コードの分かっている曲、
バンド練習（`F:\Music\ギャラン☆Do!\`。58 分の譜面は `scratch/d3/user/long-only/`。テンポが曲ごとに変わる＝C3）。

- **D4 で Dettato 側に作るものは今はない**（読み直しは単に上書き＝オーナー決定、設計メモ §6）。配り直すときは版を 2 か所（`Program.cs`・`extension/package.json`）と CHANGELOG で上げて `tools/deploy-extension.ps1`。
- **Lily# の依頼（`docs/repro/REQUESTS.md`。セッションの頭に見る）:** 1 は `done`。4 は Lily# が作り中（S1〜S3 済み、次は S4）。
  **5（m4a の `iTunSMPB`）は `fixed`＝実物の m4a で確かめてから `done`。** 7（小数のテンポ。Lily# が 6 と書いたのを振り直した）は 0.3.1 で入れた。
  **6（保存先のダイアログ・`audio` の道）・8（`warpFit` を中央値に）・9（`lysc shape`・`LILYSHARP_LYSC_PATH`）は Lily# master で `fixed`、Dettato は 9 を 0.3.2 で使った。
  Lily# の次の版が入ったら、変換メニューから 3 つとも実物で確かめて `done`**（3・5 も同じく確かめてから）。
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
  `make` の `tim/` は `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）、`samples` は `DETTATO_SAMPLES` も。
- 動作確認の 1 コマンド（20 秒）:
  `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears` → seed1 ルート 95.5%・小節線 97.1%・ベース 76.9%、`rwc-dev` 87.3% / 82.9% / 93.8%、`rwc-test` 88.8% / 83.9% / 95.3%（`--ears` は crema＋BTC）。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 7 秒。`all` は 20〜25 分。RWC の特徴量の作り直しは HPSS ありで 20 分、なしで 3 分。2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Dettato`
  止めるときは `measure.py all` を含む python.exe を止める。速さは裏の仕事が終わってから測る（24 コアでも ONNX と torch は取り合う）。
- `lysc check` の合否は終了コードと `warning` / `: error`、`--batch <一覧>` で多数。`C:\MyProj` 全体の再帰検索は固まる。pwsh で書き換えたファイルは Edit の前に読み直す。
  **プレビューで開いている Markdown は Edit を 1 回ずつ出す**。dettato の通しは `--models scratch\d2`（`scratch/net/out` にモデルはない）。

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
| `scratch/**/chords-btc/`（`--probs`）、`chords-crema+btc/`（`--ears` の既定）、`chords-cmbtc/`・`chords-cm2e1d/`、`scratch/d2/btc-large-voca.onnx` | `btc_chords.py --probs`、`two_ears.py`、`chordmini_chords.py`、`tools/d2/btc_onnx.py`（README） |
| `scratch/d2/`（`crema-chord.onnx`、`beat-this-final0-*.onnx`、`crema-classes.json`、`venv-onnx/`、`spike/out/`、`out/`、`mel/`） | README の「Without torch and TensorFlow」の末尾 |
| `scratch/net/`（`out/dettato.exe`、`seed1`・`seed7`・`seed11`・`rwc-p` の `.beats`・`.npz`・`.cqt.npy`・`.feat.npz`、ほかの合成曲 4 セットの `.feat.npz`） | README の「In .NET」の各節の末尾（.NET SDK 10.0.401。RWC は耳 9 分、特徴量 33 秒） |
| `scratch/net/read/<セット>/`（.NET の譜面。`head/seed141` は `seed141`）、`scratch/pyread/<セット>/`（比べる相手の Python の譜面） | README の「In .NET: the rules」の末尾（RWC 100 曲で 27 秒） |

`scratch/c1*.py`・`c2*.py`・`b3*.py` は残す。ほかの古いスクリプトとログ、`scratch/d3/` は消してよい。

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
- **半分の小節:** 頭から 2 つずつ組む（糸で半小節ずれ）。偶数個の並びも調で組み始めを選ぶ（糸の 58・59 が割れた）。
- **D4:** 小節の `beats` に Beat This! の拍を使う（RWC の拍の注釈に等分より近くない。テンポの揺れる録音では未確認）。
- **C1:** 鳴り終わりを「前と同じ和音で、かつ静か」で見分ける。`--ears` の無音を自作と同じ「中央値の 1/10」で見る。
  crema の N だけで無音を見る。弱起の中の和音を拍ごとに書く（Lily# が警告する）。
- **繰り返しの照合で誤りを直す・印を付ける**（C2）: 読みの多数決、確率の平均、「相手がいない小節に印」。正解の対でも +2 ポイントまで。
- 小節を戻すのを 4 拍以上の曲だけにする（合成曲で悪化、seed7 90.4%）。section をルート＋長短だけで同じとみなす、`SAID_AGAIN` 0.90 以下、完全一致だけ。
- **自作の拍追跡と合成曲だけのコードモデルを実録音向けに磨くこと。** 公開モデルが素の出力で上回る。
- **コードモデルの候補で外したもの:** madmom（CC BY-NC-SA）、Jiang ほか・omnizart（重みの明記なし）、BACHI、ChordMini（RWC を学習）。
- crema 用に値段を選び直す、印の式を合わせ直す。印に 3 つ目の耳を足す。`--ears` で値段を高くする、ベースのクロマを足す。
- 印の確信度の案（BTC、`rwc-dev`）: 半拍ごとの最小値、2 番目との差・比、エントロピー、特徴 27 個の勾配ブースティングなど（README）。
- 小節線を別に探す手法（新奇度、平均クロマ）。自作の読み取り器で HPSS をやめる、NNLS、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6。規則だけでコードの重みを詰める。
  短調の属和音の重みを下げて調を当てる。小節の長さの事前分布、拍の長さの局所中央値。七の和音の迷いまで印にする（自作モデルで効率が落ちた。`--ears` では未確認）。
- 実録音のデータ: Isophonics・McGill Billboard・JAAH は音源なし。Winterreise は歌曲。AAM は合成。GuitarSet（CC BY 4.0）は次の候補。

## 分かっている弱点・未確認

- **セントの `tuning`:** librosa との一致は `seed1` の 24 曲だけ。伸びたテープ、±50 セントを超えるずれ、曲の途中で音高が動く録音は見ていない（RWC は −9〜+4）。
- **.NET:** `RWC_P059` の調律が 0.01 ずれる理由。**入力:** 編集リストと `iTunSMPB` が両方ある m4a（いまは編集リストが先）、動画つき mp4、VS Code の Chromium の mp3 の頭、
  メモリの少ない PC で 1 時間、実録音の mp3 の精度（1 曲だけ）、Windows 10・N エディション、RWC を wav から通し、5/4・7/4、別の CPU の速さ、`small0`。通しの規則一致は seed1 だけ。
- 弱起つき・section つき・繰り返し記号つきの `.lys` は `lysc check` と小節数だけ（png は RWC_P010 を 1 枚）。鳴り方は耳で確かめていない。
- 旋律だけの弱起に、自作の読み取り器は和音を書く。半拍より短い弱起は消える。弱起は 1・2・3・4・6 拍だけ（`PARTIAL`）。
- 原因を見ていないもの: `rwc-dev` で最初の小節線が正解と違う 11 曲、`seed41` で外した曲、自作の読み取り器に正解の小節線を与えると実録音のルートが 77.4 → 77.1% に下がること。
- crema の重みのライセンスの根拠は作者の issue コメント（issue 42）とパッケージ同梱。配るときは BSD 2-Clause の表示（Copyright (c) 2017, Brian McFee）を同梱。Beat This! は MIT。
- crema は合成曲の七の和音に弱い（seed7 で 64.1%）。Beat This! が半分のテンポで追う曲は 2 小節が 1 小節になる（C4）。
- RWC の注釈に調がなく、調は実録音で測れていない。大きなテンポ変化（C3）は未対応。Chord Tracker とは比べていない。

## 未決（ユーザーの判断待ち）

- **`x.dettato.json` を人が直してよい形式 2 にすること**（オーナーは「人の小節線が正」に同意。形式の中身と、Lily# と Dettato の書き分けは依頼 4 の返事待ち）。
- **和音のないイントロ・弱起を譜面に書くか。** いまは書かない。書くなら N.C. の小節として。`rwc-dev` で落としている和音なしの小節は 14。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）。** 2 つの耳（0.4.0）で `rwc-test` は「印 28.7% で 72.8%」、`rwc-dev` は 35.2% で 86.4%。印の式（`SURE_OF`）は crema 1 つで選んだまま。選び直すかは未決。閾値は 0.84。
- **section の書き方。** 6 小節以下が続かないようにした（上）。さらに減らすか（6 を 8 に、単独の短い section も隣へ）はオーナーが見てから。
- **値段・印の式・`SAID_AGAIN`・C1 の定数・HPSS なしは `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習では
  ないつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P と違うなら、その種類の曲を数曲（Chord Tracker の結果があればそれも）。名前の重複の確認、push（ROADMAP §5）。
