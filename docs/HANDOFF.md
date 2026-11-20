# Orecchiante 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-21（**0.13.2 をタグで公開した**（2026-11-20、オーナーが `v0.13.2` を push → `.github/workflows/release.yml` が pack → GitHub Release に .vsix → Marketplace、run 44751831158 成功＝159 MB の .vsix が通った。Lily# も同じ日に変換メニュー入りの v0.17.0・v0.17.1 を公開）。**次のリリースの手順:** 版を 2 か所（`Program.cs`・`extension/package.json`）と `extension/CHANGELOG.md` の一番上で上げて commit → オーナーが `git -C C:\MyProj\Orecchiante tag vX.Y.Z` と push（タグと package.json が違えば止まる）。**Lily# のフォルダで打たない**（2026-11-20 に一度 Lily# に `v0.13.2` が打たれ、Lily# のリリースを test の段で取り消してタグを消した）。手で回すと pack だけ、`publish_targets` に `win32-x64` で Release の .vsix を Marketplace へ出し直す。secret `VSCE_PAT` は 2027-10-16 まで（Lily#・Calamo と同じ）。**モデル 4 つはリポジトリの `onnx/`**（GitHub の 1 ファイル 100 MB の上限＝大きいモデルを足すときは Release か LFS。作り直したら scratch/d2 から手で）。**0.13.2: section の λ・off を選び直した（`scratch/xsc/dp_BAW.json`、オーナー「平均を重視したい」）**: xsc test 70.7 → 72.5%（near 77.3）・dev 71.6、AIST 歌つき 3 秒 dev 78.7・test 75.1。オーナーの曲では今宵に 40、糸に 36・51・67 が余分。0.13.1: `align_heads` は ±4 小節の中で 1 通りだけ似るときに結ぶ（JSON の `align_wide` 4、2 回目のモデルだけ）＝今宵の 106 → 105。0.13.0: section を曲の繰り返しに揃える（`onset_cues`・`align_heads`・`plus1`・`head_cues`、2 つのモデルと 2 回の読み。今宵の項）。read_check 368/368（`scratch/sec/check13.ps1`）。各版の数字は README。）
前回より前（0.12.0 ベース音をベースのパートで、0.11.0 改名、0.10.0 ドラム、0.9.0 `SECTION_DP`、0.8.x と 0.5.0〜0.7.0）は README・CHANGELOG・`git log`。

### D4 の形（2026-11-15、オーナーと話して決めた。依頼 4・[便り](repro/lilysharp-requests-2026-11-15-listening.md)）
**Lily# の設計メモ `C:\MyProj\LilySharp\docs\recording-design.md`（セッションの頭に読む）:** 紐づけは隣のファイル（形式 2）だけ、m4a は Lily# の LSP が復号。
**目標は Transcribe! と同じ体験を Lily# で**（耳コピの道具。**特定の楽器に寄せない**＝オーナー 2026-11-17「ベースは私が趣味で弾いているだけ。ユーザーはいろんな楽器を使う」。オーナーの曲をベースで試すのは一例）。**人が打った小節線が正**（形式 2）。Orecchiante は `tuning`・`beats` を入れた（読み直しの入口は不要）。

## 現在地

**`orecchiante <録音> [-o <x.lys>] [--force]` が、mp3・m4a・mp4・aac・flac・wma・wav から `.lys` と `.orecchiante.json` を録音の隣に書く**
（Python なし。RWC_P001 の mp3 で 6.3 秒）。配る形は .NET＋ONNX の実行ファイルを VS Code 拡張に入れる（Calamo と同じ形）。
**拡張 `yotsuda.orecchiante` 0.13.2 は Marketplace に出ている**（2026-11-20、タグで。この PC にも入れた。各版の中身は `extension/CHANGELOG.md`、`extension/`・`tools/deploy-extension.ps1`。.vsix 159 MB、Lily# とは別の拡張）。**コマンドはない**: メニューは Lily# の
「Convert Recording to Lily# (.lys)… (Preview)」で、Lily# が `ext.activate()` で `{path, version, protocol, inputs}` を受け取る。**Lily# のメニューからの変換はオーナーが確かめた（2026-11-20「すべて OK」）。** Lily# の公開版は v0.17.0 から変換メニューを持つ。

### コマンドと入力（D3。`Program.cs`・`Audio.cs`。README の「A recording as it comes」、Lily# 向けは便りの §2a）

- stdout は書いた `.lys` のパス 1 行、stderr は段階ごとに 1 行（0.8.12 からパートの分離の 5% ごとも。`Voice.Loudness` の `separated`）で失敗時は最後の行が `orecchiante: <理由>`。終了コード 0 書けた／1 録音が読めない／
  2 引数（`.lys` が既にある、を含む）／3 読み取り器自身。2 つのファイルは `.part` に書いてから置き換える。5 秒未満は読まない。
- `orecchiante --version --json` → 版・`protocol` 1・`commands`・`inputs`（拡張が `bin\orecchiante.json` から返す）。版は `Program.cs` と `extension/package.json` の 2 か所。
- **wav 以外は Windows の Media Foundation（NAudio 2.2.1、MIT）で読み、エンコーダの足す頭の無音を `Audio.Lead` が取り除く**（合わせる相手は再生する側。`F:\Music` の 7 種すべて ffmpeg と 0 標本。細部は README の「A recording as it comes」）。
  58 分の録音も読める（残る大きいメモリ: `Features.Of` と Beat This! の段で各 +1.2 GB）。道具: `tools/heard_check.py`・`kinds_check.py`・`chrome_lag.py`。ffmpeg は PATH にある。

### .NET の規則（`Reader.cs` 726 行・`Form.cs` 209＝`Turns`・`Phrases`・`Writer.cs` 251。README の「In .NET: the rules」。全体で 2776 行）

- 移したのは `--ears` の道だけ。自作の拍追跡・`MODEL`・`--tempo`・`why` は Python に残す。
- **`.orecchiante.json` は一字一句、`.lys` は 1 行目以外、268 曲すべてで Python と同じ**（`tools/read_check.py`。比べ方とコマンドは README）。
  同じ拍と確率（`--lines`・`--heard`）と同じ特徴量（`--net scratch/net`）を与えて比べる。`measure.py … --read` は .NET の譜面を数える。
- 一致させるために真似たこと（`Np.cs`）: numpy の和の順、単精度のままの平均とノルム、Python の `//` と `round`、`repr`、同点の扱い。
- **規則を変えるときの手順:** Python で試して測る → .NET に写す → `read_check.py` で全セット一致を確かめる（Python が正）。

### .NET の耳と特徴量（`orecchiante ears`・`orecchiante features`。README の「In .NET」の最初の 2 節と「Without torch and TensorFlow」）

- 耳: wav → `.beats`（Beat This!）と `.npz`（crema と BTC の平均、0.4.0 から）、特徴量: wav → `.feat.npz`。突き合わせは `tools/net_check.py`、`measure.py --net scratch/net` で表は全列同じ。
- **`--ears` では HPSS を外した**（`use_ears()` が `HARMONIC_FRAMES = 0`。特徴量は `scratch/feat-h0/`）。1 曲 3 分半で `orecchiante read` 5.5 秒（Beat This! 3.7）。onnxruntime は 8 スレッド（24 だと遅い）。

### 数字（`measure.py all`、2026-11-19 の 0.10.0＝section の数だけ動いた（`all` は歌もドラムもなしで読む）。lysc check 1388/1388（依頼 18 の直った lysc `scratch/lysc-0986`）、SIDECAR 1388/1388。ログは `scratch/all.log`、前は `all090.log`）

| `--ears`（crema＋BTC） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 87.4% | 88.9% | 88.2% | 95.5 / 94.4 / 96.7% |
| ルート＋長短 | 82.9% | 83.9% | 83.4% | 92.4 / 90.9 / 94.3% |
| ベース音まで | 65.1% | 66.6% | 65.9% | 76.9 / 65.1 / 80.0% |
| 小節線 F | 94.1% | 95.1% | 94.6% | 97.1 / 91.0 / 94.1% |
| テンポ・拍子 | 47・49 | 47・49 | 94・98 | |
| 印 → 拾う誤り | 34.9% → 85.9% | 28.1% → 72.0% | 31.5% → 78.9% | 14.9% → 74.0% |
| section（1 曲あたり、AIST 全境界 3 秒の F は声なし／歌＋ドラム） | 10.7、67.3／77.2% | 9.2、57.8／74.3% | 10.0 | 3.5 / 3.1 / 2.9 |

- **持ち越す知見（README にないもの）:** 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。RWC の正解は両端を語らない。ONNX を書き直すとき: 旧式のトレース直後の torch のモデルは同じプロセスで答えが変わる（比べる前に読み直す）。dynamo は `PYTHONUTF8=1` が要る。

## 次の一手

**改名（2026-11-20、オーナー決定）: Dettato → Orecchiante 0.11.0、済み**（同名の kobimusic/dettato が 11-16 に公開されていた。ROADMAP「名前」）。exe・拡張 ID `yotsuda.orecchiante`・`<stem>.orecchiante.json`（鍵 `"orecchiante"`）・`ORECCHIANTE_*`。古い `.dettato.json` は読まない。Lily# は依頼 20（2ce9d2381、内部名まで）。`yotsuda.dettato` は外し、入っている exe で変換を確かめた。フォルダは `C:\MyProj\Orecchiante`（scratch の .ps1・.cmd・.bat のパスも直した。venv の pip.exe などの起動器は旧パスを持つ＝python.exe で直に呼ぶ）。**GitHub: `https://github.com/yotsuda/Orecchiante`（2026-11-20 に push 済み、オーナーの指示で）。** 拡張の発行者の表示名は、Marketplace に無いので `deploy-extension.ps1` が `extensions.json` に `Yoshifumi Tsuda` を書く。**Lily# の画面からの変換はオーナーが確かめた（2026-11-20「すべて OK」）。** docs/repro と CHANGELOG の過去の版は Dettato のまま。

候補（私の勧め順。0.10.2 で譜面のコードの行の名前は `prog`＝Lily# のひな形と同じ、合成曲の正解と `gen_chords.py` は `harmony` のまま）:
1. **0.13.2 の section の誤りを種類ごとに数え直し、一番大きい所から**（`scratch/xsc/errs.py`・`extras.py`、dev だけ。0.9.0 の時点では「同じ長さの小節・歌ありの 116 曲」が誤りの 77%、半分読みの 11 曲が 11%＝DP では直らない、捨てた案）。オーナーの曲の残り（今宵 40、糸 36・51・67）は 1 曲ごとの直しになる＝平均が上がる手のときだけ。
2. 声もドラムもないときの扱い（測るときだけの問題: AIST 声なし test 63.0 → 57.8%。直すなら手がかり 6 個を 0 でなく dev の平均で埋める、を JSON に持たせる。orecchiante 本体には影響しない）。
3. 「人が 1 曲を仕上げるまでの時間」の物差し（ROADMAP §5）。**2026-11-20 にオーナーと話した:** xsc の M はオーナーが再生しながら手で打った（多くは等速、一部 1.5 倍）＝小節線は何もなくても曲の長さ（の 1/1.5）で済む。**小節線のタイミングは今の動作で問題ない**（気になるのは変拍子だけ、まれ）、**見てほしいのは xsc の section の位置**。コードの正解はない。
   `scratch/xsc/edits.py [TOL=0.1]`（dev）: section の頭は 1 曲あたり、オーナー 10.2 に対して直す数 7.1（残す 997・1 小節ずらす 77・足す 367・消す 559、141 曲）＝**物差しは section の直しの数（今の exact F と同じ向き）**。小節線は許すずれ 0.05／0.1／0.2 秒で直す数が 54／22／14%（オーナーの打鍵の揺れ）、半分読みの 11 曲は線を 1 本おきに消す（507 本）。
   **ベースの譜で和音を確かめた（2026-11-20、`F:\Transcribe\Xanadu\Score\Ly`＝オーナーが Xanadu で弾く .ly）:** 音符があるのは Uptown Girl・How Deep Is Your Love（Xanadu アレンジ）・Bohemian Rhapsody（一部）だけ。道具 `scratch/bass/ly2beats.py <x.ly> <out>`（LilyPond 2.26 で MIDI→4 分音符ごとのピッチクラス）・`compare.py <x.orecchiante.json> <beats.json> [-v]`（小節のずれを ±12 で探す）。Uptown Girl（`F:\Music\Billy Joel\An Innocent Man\06 …`）1 拍目 98.0%（本当の誤りは「A A B B」の後半の B を落とす 7 小節）、How Deep（`F:\Music\Bee Gees\The Ultimate Bee Gees [Disc 2]\2-01 …`）1 拍目 96.2%（Bb を Ab と読む 2 小節）。2 曲だけ＝規則を選ぶには少ない（確かめ用）。
   小節の途中の和音の変わり目（RWC dev、`scratch/bass/midbar.py`）: 正解の 698 か所の 77.8% を拾い、私たちの変わり目の 88.5% は正解も変わる。`MID_COST` を下げても動かない（耳の確率で決まる）。

**ドラムの手がかり（0.10.0、README「The drums (0.10.0)」）**
- 流れ: `Voice.Heard`（`Loudness` は歌だけを返す薄い包み）が vocals と drums を足し合わせ（パートを書かないときは guitar・piano の逆 STFT をやめ drums をする）、drums を 1 標本ずつ `Drums.Add` に流す（1 時間の録音も持たない）→ `Frames`＝[10 ms, 4]（mean square、3 帯域の正のスペクトル流束: 2048 点 numpy の hanning、`log(1+100|X|)`、<200 Hz・<4 kHz・それ以上）。`Reader.DrumBars`／`drum_bars`→`Sections.Drummed`／`drum_cues`。JSON の `"drums": true` のときだけ列が足される（false なら前と同じ 245 列）。ドラムが無い（合成曲、`--voice none`）なら 6 列とも 0。 入口: `orecchiante ears` が `.drum.npy` も書く、`orecchiante drums <<name>.parts のフォルダ>|<録音> --out` が分けたパートから作る（1 曲 1 秒）、`orecchiante read … --drums <folder>`、Python は `read_chords --drums`・`measure.py --drums scratch/drum`・`aist_sections.py`（歌つきの mode で既定 `scratch/drum/rwc-p`）・`capture.py`（`DRUMDIR`、既定 `scratch/drum/xsc`）・`eval.py`・`scratch/sec/try.py`（`scratch/drum/sec`、`NODRUMS=1`）。Python の `partcues.py` と float の精度で同じ。
- 学び方: `capture.py` → `dp.py EXTRA=1 NB=3 PH=0 C=0.1 AUDIO=1 MUS=0,0.5 DRUMS=1 [--cv K=5 SEED=1|2] [--json] [--test]`。5 分割×2 で 60.8 → 65.0%（ドラムなし 61.0／60.6、あり 64.3／65.6）。test は dp.py の数え方で 66.2 → 69.8%（1 回だけ）。Python と .NET の確かめは `scratch/drum/check.ps1`（ログ `check.log`）。
- 試して効かなかった（同日）: 6 パート全部（64.5%）、その小節だけの列（前後を並べない、62.2%）、ドラムの音量だけ（60.6%）。歌のない曲で旋律楽器を声の代わりにする `lead.py` は試す意味がなくなった（歌のない dev 曲はほぼベースのパートだった。test の本物の器楽 6 曲で見るなら）。 物差しの直し（`scratch/xsc/`）: `mixes.py` → `mixprep.ps1`（wav・耳・パート）→ `swapmix.py [--undo]`（`truth-stems.json` が元）→ `capture.py`。x093（Stayin' Alive）はパートのまま。パートは `scratch/xsc/parts/<id>.parts/*.flac`（289 曲）、ドラムの特徴は `scratch/drum/{xsc,rwc-p,sec}/`（`sec` の arms は `orecchiante ears` の、ito・mylife はパートの flac から）。
- **FLAC の直し**（0.10.0 に入れた）: 先に Media Foundation を 1 回始め、拒まれたパートは 1 本ずつ作り直す。原因の確証はない。**0.10.1: OneDrive の中で変換し直すと「….parts is denied」**（オーナー、糸）＝OneDrive はフォルダに ReadOnly を付け、.NET は読み取り専用のフォルダを消せない。`DeleteFolder` が印を外して消し、古いパートは `.parts.old` に退けて新しいのが入ってから消す（失敗したら戻す）。scratch で ReadOnly を付けて再現→直ったのを確かめた。オーナーの `Transcribe!\source\糸 (Bank Band).parts` は失敗で空になっている（変換し直せば戻る）。

**今宵の月のように（**正しい頭: 1・9・16・24・32・49・56・64・72・80・89・97・105**。9・80・49 は後からオーナーが「正しい」と（40 ではなく 49）。2026-11-20 オーナー、0.12.0 の変換: `C:\Users\ytsud\OneDrive\Documents\Transcribe!\source\今宵の月のように.mp3`）: 「E・E2 の頭は 64・72（私たちは 65・73＝1 小節遅い）。ほかは上手に切れている。人なら誰も間違えない。この曲固有の修正は入れても仕方ない」。E・E2 は 3 拍目から歌が入る（歌の 4 分の 1 ごとの割合 64: 0, 0.08, 0.88, 0.79、72: 0.16, 0.25, 1, 1）。** Python で同じ form を再現（`scratch/sec/koyoi/look.py <wav> <ears> [FROM= TO=] [PAIRS=65-64] [PKL=…]`、耳 `scratch/sec/koyoi/ears`、`dp.py … APPLY=scratch/sec/koyoi/koyoi.pkl SHOW=62-75 FORCEAT=64+72`）。点数 64 −1.19・65 −1.00、72 −1.88・73 −1.45（65・73 は歌の割合が一番上がる小節）。もう 1 つの理由: 72 で切ると E2 が 17 小節（A2 の 89 は正しい）＝長さの事前分布が嫌う（強制すると全体の値 4.25 対 5.10）。**オーナーの xsc では、歌が小節の後半から入る（前が無音）とき、オーナーの頭はその小節 10・次の小節 38**（`scratch/xsc/midin.py`）＝多くは弱起として次の小節で切っている。その小節で切る 10 はコードがその小節で変わる 0.80・次で 0.30（次で切る 38 は 0.53・0.61）。手がかり `dp.py ONSET=3`（後半から入る＋その小節でコードが変わり次では変わらない、など）: 5 分割 SEED 1・2・3 で 64.3／65.6／64.7 → 65.1／65.8／64.8（平均 +0.4）、今宵は 64 −0.70 対 65 −0.66 まで詰まるがまだ 65・73（17 小節の長さで負ける）。まだ入れていない（オーナーと相談中）。 **2026-11-20 続き（オーナー「64 からのコードは C・C2 と同じ並び G G G E Am Am Am D」「8 小節の繰り返しの最後に、ある section だけ 1 小節おまけが付いて盛り上がりへ、という形も拾いたい」）:** `dp.py HEADMATCH=1`＝その小節からの 8・4 小節が、1 回目の読みの頭（ほかの所）からの 8・4 小節と同じ数（と 1 小節前・後のもの）。1 回目の頭は交差で作る（`PASS1OUT=scratch/xsc/pass1.pkl`＝5 分割で他の分割から学んだ頭、test は dev 全部の）→ `PASS1FILE=`。**5 分割 SEED 1・2・3: 64.3／65.6／64.7 → 67.7／67.3／67.3（平均 +2.6）。** capture の read_heads（dev 全部で学んだ頭）を 1 回目に使うと 67.1／67.5／68.0＝漏れはわずか。**今宵では 64・72 を拾うが、C・C2 を 25・33 にずらし 9・49・80 を足す**（1 回目の頭 65・73 に 25・33 が合う）。2 回目の頭で 3 回目を読むと 16・24・32・40 は戻るが E・E2 は 65・73 に戻る＝ずれた読みどうし・正しい読みどうしがそれぞれ辻褄が合い、行き来する。**入れていない。** 次: 繰り返しの揃え方を曲全体で一度に決める（どの組の頭を揃えるかを、歌が後半から入る・コードがその小節で変わる、で決める）。`PLUS1=1|2 [PLUS1_COST=]`（8m＋1 小節で、8m 小節かその前の 8 小節がほかで鳴るなら長さの事前分布を 8m のものに）は SEED 1 で 63.4〜64.7（基準 64.3）、HEADMATCH と合わせて 67.6（HEADMATCH だけ 67.7）＝効かない（オーナーの 9 小節は 78、8 小節 649＝長さの分布がもう持っている）。 **2026-11-20 さらに（オーナー「その方針で」）:** (1) `ONSET=4`＝歌が後半から入る小節の頭はコードが変わる所（dev で後半から入る所: その小節で変わり次で変わらない→オーナーの頭はその小節 11・次 5、次で変わりその小節で変わらない→その小節 0・次 27、`midin.py` の変形）。(2) `ALIGN=2`＝8 小節が同じ（そのまま／1 小節ずらしてのどちらか一つだけで同じ）頭どうしを組にし、組のずれを揃え、組全体を動かす量（どれか 1 つは元の所に残る）を点数の和で選ぶ（`DEBUG=1` で組を表示）。(3) `PLUS1=1 PLUS1_COST=1`。今宵: A＝(1)+(2)+(3) で 1・16・24・32・40・56・64・72・89・97・106（違うのは 106＝正しくは 105 だけ）。**5 分割 SEED 1・2・3（基準 64.3／65.6／64.7、平均 64.9）: A 64.3／66.4／64.4（65.0）、HEADMATCH（1 回目は基準の交差 `pass1.pkl`）＋A 67.5／67.4／68.3（67.7）、HEADMATCH（1 回目は A の交差 `PASS1OUT=scratch/xsc/pass1A.pkl`）＋A＝BA 68.3／67.9／68.3（68.2、+3.3）。** 今宵は BA で C・C2・E・E2 は正しいが 9・80 が増え 40 が 49 に（A のほうが今宵は良い）。test は未見。.NET には未移植（ONSET は小節の 4 分の 1 ごとの歌が要る、ALIGN・PLUS1 は Sections.cs の DP、HEADMATCH は 2 つのモデルと 2 回の読み）。

**section をまとめて選ぶ `SECTION_DP`（2026-11-19、0.9.0 で Python と .NET の既定。`SECTION_DP = 0`／`Form.SectionDp = false` で前の規則の切れ目）。.NET は `Sections.cs`、`Form.Phrases` が `WHY` を記録して呼ぶ。`read_check.py` 368/368（RWC 歌つき 100・歌なし 100、合成曲 7 セット 168）。**
- 仕組み: 小節線ごとに手がかり 35 個＝`section_cues`（今の規則がそこで切った理由 `WHY`、歌の出入り、4・8 小節の句の繰り返しと再演、主和音・属和音・保った和音、歌の割合）26 個＋`audio_cues`（前後 2・4 小節の音量・鳴る音・和音の違い）9 個、を
  **前後 3 小節ぶんも並べて（245 個）**ロジスティック回帰（C=0.1）で点数にし、`choose_cuts` が動的計画法で選ぶ: 切れ目の点数 − off ＋ λ×log（オーナーの section の長さの分布）＋ μ×（その区間が同じ長さで別の所でも鳴る、`played_again`）。
  重み・長さ・λ・off・μ は `tools/sections_dp.json`（0.10.0 から `… DRUMS=1 --json`＝ドラムの 6 列も、λ 0.5・off −2.0・μ 0。0.9.0 は off −1.5・μ 0.5。xsc の dev だけで学んだ）。.NET は `Sections.cs` が同じ JSON を exe に埋め込んで読む（`Orecchiante.csproj` の EmbeddedResource）。点数は小数 9 桁に丸め、対数は `math.log`＝足す順が違っても同じ切れ目。
- 数字（規則 → `SECTION_DP=1`）: **xsc dev exact F 34.1 → 62.1%**（学んだ曲。5 分割の交差検証で 57.2〜57.9%）・near 51.8 → 67.7%。**xsc test（数えるだけ）exact 38.4 → 66.4%・near 56.8 → 71.8%**（読む 1908 → 1395、オーナー 1531）。
  **AIST（歌つき）rwc-dev 3 秒 66.8 → 76.2%・0.5 秒 42.5 → 58.3%、rwc-test 3 秒 62.2 → 72.3%・0.5 秒 37.7 → 52.7%。** 手で切った 3 曲: 糸は 21・44 を落とす、My Life は 105・151 を落として 8 つ増える、Open Arms は 101 を拾い 43・93 が増える。
- 交差検証で効いたもの（5 分割×2 通り、差し替え前）: 26 個だけ 44.0% → 前後 3 小節 55.7% → 音 56.8% → 区間の繰り返し 57.6%。効かない: 位相、2・12・16 小節の句、勾配ブースティング、構造化パーセプトロン（55.4%）。道具: `capture.py`（`cap/<id>.pkl`、80 秒）→ `dp.py [--cv] [K=5 SEED=1] [--test] [--json] …`、`eval.py [dev|test] [--why]`、`look.py <id>`、`barshift.py`、`errs.py [--each]`（種類ごとの F）。手を変えたら: 取り直す → `dp.py --cv K=5`（2 SEED）→ `--json` → test を 1 回 → `Sections.cs` に写して `read_check.py`。配る重みは dev だけで学ぶ（test を物差しに残す、私の判断）。
- **どこで落とすか（`errs.py`、差し替え後の dev）:** 小節が同じ長さ・線も 9 割合う・歌ありの 116 曲が 68.2%（誤りの 77%、読む 1385／オーナー 1226＝1 割多い）、小節を半分で読む 11 曲 34.4%（11%、秒では見分けられない＝`barsec.py`、小節の読みの問題）、歌が聞こえない 3 曲 16.7%。規則の定数（単独で小さい）: `LEAD_IN` ±0、`VOICE_LATE` 0.1 +0.5、`KEY_TURN` 3 +0.9、`MERGE_SHORT` 4 +1.0（差し替え前）。

**section（意味で切る。0.5.0 で既定、0.6.0 で歌も .NET に。オーナー: 調や曲調が変わる所・歌が入る所・ソロで切りたい）**
- オーナーが手で分けた 2 曲: 糸（`scratch/ito/ito-one.lys`: 1・9・17・21・28・32・40・44・51・59。**2026-11-20 オーナー: 17・40・51 は切らない方が良い**＝正しいのは 1・9・21・28・32・44・59）、My Life（`scratch/sec/phrase/mylife-owner.lys`: 1・9・17・31・39・53・69・83・91・105・121・129・135・151）。 確かめは `scratch/sec/try.py [定数=値 …]`（`VOICEDIR=scratch/net/voice/sec` で .NET の歌、`NOVOICE=1` で声なし）と `tools/aist_sections.py rwc-dev`（既定で .NET の歌）。**規則を触ったら必ず両方を見る**。
- **Open Arms（Journey、3/4・102）: オーナーの分け方 1・9・17・25・35・51・59・67・75・85・101**（`scratch/sec/arms-owner.lys`。歌の 8＋延長 2 の 10 小節、サビ 16 小節を 8 で割らない）。`scratch/sec/try.py` の 3 曲目（材料 `scratch/sec/arms.*`、耳と歌 `scratch/net/voice/sec/arms.*`）、見る道具 `scratch/sec/arms.py`・`arms_bars.py`。規則の時代（0.8.9〜0.8.10）のオーナーの評: 15・62・65 は切れ目ではない、79 は許容、100 は 101 なら素晴らしい。
  `HELD_SINGS` 1＋`HELD_SUNG` 0.2（Python だけ、既定 0。歌が止む所で主和音以外が保たれ、その小節の歌が 0.2 以上なら主和音の来る所へ）は 101 を拾うが閾値の幅が狭い＝入れていない（規則の切れ目は `SECTION_DP` の手がかり `WHY` に入るだけ）。
- 仕組み（README「Sections by meaning」「The voice in .NET」）: 歌の出入り・調号 2 つ以上・6 dB で塊に切り、丸ごと別の所で鳴る塊の頭と終わり、長い塊の中は 4 小節の句（`PHRASE_BARS`）の格子で「同じ句 2 回の終わり」「V→I」「別の所でも始まる句（`BEGIN_SHARE`）」「8 小節ごとの新しい組（`PAIR_HEARD`、0.8.1）」で切る。名前は再演なら A2。fold はしない。.NET は `Form.Turns`・`Form.Phrases`・`Form.VoiceTurns`。
- **歌（0.6.0、0.8.0 から 6 分割の歌）:** `Voice.cs` が Demucs（`htdemucs_6s`、MIT）の ONNX（`tools/d2/demucs_onnx.py --model htdemucs_6s`。STFT と逆 STFT は外、行列積だけ int8＝64 MB）を 7.8 秒ずつ、ずらしなし・モノラルを両チャンネルで回す。AIST 3 秒以内の F: dev 64.4%・test 60.5%（4 分割は 64.3・60.1）。**歌は `scratch/net/voice/`（6 分割。RWC 100・seed1 の 7・sec）、4 分割のものは `voice4/`。** 歌のない曲は `VOICE_FLOOR`（0.05）で歌なし（seed1 の 7 曲で確かめ直した）。**Python と .NET は同じ `.voc.npy` で RWC 100 曲が一字一句同じ**（`orecchiante read … --voice <folder>`、合成曲は `--voice none`）。
- **時間とメモリ（0.6.1）:** 通しで RWC_P001 33〜45 秒（Calamo の負荷しだい）、ピーク 8.6 GB。並行は 3（`Voice.Parallel`）。結果を変えずに速くする手は見つからなかった（README）。重なりなしは捨てた（オーナー決定）。section の頭の比べ方は `scratch/sec/ov_cmp.py <A の譜面> <B の譜面>`（最寄りの AIST つき）。
- **パート（0.7.0、オーナー「レコーディングのオンオフをパートごとに。耳コピにとても便利」。README「The parts」）:** 変換のたびに `<名前>.parts/*.flac`（モノラル 16 bit 44100 Hz、録音の秒の上）と `x.orecchiante.json` の `parts`（SIDECAR.md）。確かめは `scratch/sec/parts_check.py <録音> <x.parts>`（和・ずれ）。**Lily# 依頼 16 は fixed**（パートの行は `parts` の数と順で出る。全部オンなら元の録音、1 本でもオフなら残りの和）。ステレオのパートは未。
- **6 パート（0.8.0、オーナー決定 2026-11-17。README「Six parts」）:** drums・bass・other・vocals・guitar・piano（`ORECCHIANTE_VOICE_MODEL=htdemucs-mm8.onnx` で 4 に戻せる）。6 本が落とす音は `other` に足して和を録音に戻した。アコギとピアノは割れる（オーナー「若干ギターとピアノがうまく分離できていない」）。int8 のせいでも振り分けでもない。5 パートと聴き比べて **6 のまま（オーナー決定）**。合成曲の割合は参考にしない（GM の音、オーナー）。残る手（今はやらない）: 別のモデル（BS-RoFormer の 6 パート版 `bs_6stem_fixed`＝music-to-midi が使うもの。**重みに許諾なし**（配布元にライセンスの記載がない、同プロジェクトの THIRD_PARTY_NOTICES）＝配れない）、MoisesDB で実録音を測る（登録が要る）。道具は `scratch/six/`（README）。
- **次:**
  (2) **C3（テンポの大きな変化）は 0.8.3 で最初の手を入れた**（README「A section's own tempo」、SIDECAR の小節の `tempo`）: 曲順に section を歩き、 section の小節の平均が今のテンポから 6% を超えて離れ、6 小節以上、4 分の 3 が中央値の 1 割以内、比が 1.3 未満なら、section の頭に `tempo 4 = x`（Lily# は section の頭のテンポを毎回鳴らし、次まで保つ。紙では曲頭の値は A のもので置き換わって刷られる＝png で確かめた）。頭のテンポに戻る section は 2 小節でよい。 測り方: `scratch/c3/tempo_try.py <譜面> <truth> --written [-v]`（正解の小節が、そこで立っているテンポの小節と 4% 以内か）。dev 91.2 → 91.4%、test 95.2 → 95.3%（**test の曲ごとの行を見た**）、`drift5/seed31` 66.9 → 70.2%、ほかの合成曲は印 0。 条件なしだと印はすべて小節の読み違い（半分・倍・3/2）だった。P038 の最後の 140.2 は誤り（真は 111、小節を短く読んでいる）。**バンド練習のような録音は考えない（オーナー決定 2026-11-17「練習時の録音を譜面に落とすことはほとんどない」）**＝曲間の話し声・無音（Beat This! が拍を打たず 91〜112 秒の 1 小節になる）は扱わない。`scratch/c3/long/` は参考に残す。 `still/`・`drift5/` の耳（`scratch/beats-bt/<still|drift5>/seed31`・`chords-crema+btc/…`、歌は `scratch/net/<still|drift5>-seed31`）を作った。Beat This! は 5% 揺れでも小節線 92.8%（自作の追跡は 80.9%）＝小節線はもう問題でない。 残り: rit・フェルマータ（曲の終わりの数小節。section より細かい。**曲の終わりは 2026-11-17 に測った**: `scratch/c3/tail.py <譜面のフォルダ> [-v]`＝最後の小節の頭が正解の最後の小節の頭か、その終わり（最後の一撃）に 0.3 秒以内か。dev 25/50・test 17/50。曲ごとは `ends.py`、手がかりは `endfeat.py`。規則は捨てた）、途中で止まる曲（**RWC-P には測る材料がない（2026-11-17、`scratch/c3/stops.py <譜面> 1 100 [-v]`）**: 正解の中に N.C. も小節の隙間もなく、前後の 1.35 倍を超える小節は 100 曲で P034 の 1 つだけ。拍子の違う小節は P006・P022・P037・P038・P043（3/4・5/4・6/4）にある。P038 の小節線 67/138 は拍子ではなく、線が正解より 0.1〜0.26 秒早い区間が多いため＝許容 0.07 秒の外）、範囲のテンポ・人の拍マーカー（Lily# の依頼 4）。(3) 速くする手が要るなら: 重なりを 4 分の 1 より小さく（8 分の 1 など、同じ道具で測る。未測定）、空いた PC で測り直す、蒸留した小さなモデル（学習の音源はオーナーが決める）。
- **糸の M64（6 拍＝4＋2）は 0.8.4 で入れた**（README「A bar of two beats」）: `.beats` の 3 列目＝拍の ±2 フレームの downbeat の値の最大（`track_beats.py`・`Beats.cs`、.NET と torch の差 0.04 以内）。 `pair_runs`／`PairRuns` が `MEND_BARS` の前に、2 拍の小節が続く所を「残す線の値 − 落とす線の値」が最大になるよう組む。1 つ残す 2 拍の小節は値段 12、拍の最初・最後の線では無料。 糸は 253.96 秒から正しい（`G | C#m7(b5) | C Dsus4 | C D …`）。2 拍の小節はオーナーの位置より 1 小節前（値が 249.96 秒で 3.2、251.94 秒で 2.2）、最後の section の名前が D3 → G（再演と見なされなくなった）。
  **0.8.5 から `time 2/4` を書く（オーナー決定 2026-11-17、前の「書かない」を改めた。README「A bar of 2/4」）**: 前後 4 小節の中央値の 0.4〜0.6 倍で、両隣が 15% 以内、4/4 だけ、弱起・最初・最後でない、section を演奏するたびに同じ小節。次の小節に `time 4/4`（section の最後なら不要＝Lily# は section の終わりで戻る、確かめた）。RWC で 10 曲 14 小節（両端が正解に乗るのは dev 6/10・test 0/8、RWC に本物の 2 拍はほぼない）、合成曲 4 小節。外れても読みの格子では 2 拍の長さ＝録音と同じ速さで鳴り、直す所の印になる。測る道具 `scratch/c3/short_bars.py <譜面の根> <セット> [-iso] [-v]`。**0.8.6 から 3 拍も `time 3/4`（オーナー決定 A、2026-11-17。README「A bar of 3/4」）**: 0.65〜0.85 倍、最後の 2 小節は除く（曲の終わりの rit を短く読んだ小節が 6/7）。RWC に曲の途中の本物の 3/4 はほぼない（P006 だけ読めている）。RWC で 6 曲 7 小節、合成曲 2 曲。道具 `scratch/c3/three_look.py`。依頼 11 は fixed（オーナーの版で確かめたら `done`）、15 は closed。**オーナーは 0.8.5 の糸（`scratch/c3/ito/ito-ext.lys`）の 2/4（64 小節目）を「自然に正しく鳴っている」、70 小節目からの終わりの rit のずれは「許容しても良い」**（rit で長くなる小節を短く・少なく読む＝捨てた案の C3「曲の終わり」と同じ所）。
- **D4 で Orecchiante 側に作るものは今はない**（読み直しは単に上書き＝オーナー決定、設計メモ §6）。配り直すときは版を 2 か所（`Program.cs`・`extension/package.json`）と CHANGELOG で上げ、この PC には `tools/deploy-extension.ps1`、公開はオーナーのタグ（頭の行）。
- **Lily# の依頼（`docs/repro/REQUESTS.md`。セッションの頭に見る）:** 2026-11-20 オーナーが変換メニューから確かめ「すべて OK」＝1・5・6・8・9・16・17 は `done`、11 は `done`・15 は closed、3・18（`time 3/4` の小節の `.` の検査）・19（A-B の旗）・20（改名）は fixed。4（人の拍マーカー）は Lily# が作り中（S1〜S3 済み、次は S4）。7（小数のテンポ）は 0.3.1 で入れた。**lysc は `scratch/lysc-0986`**（Lily# `098675fb6` を `git archive` して `dotnet build scratch\lysc-src\LilySharp.Cli -c Release -o scratch\lysc-0986`＝`scratch/read-all/` の 1492 個がすべて通る。Lily# の公開版 v0.17 以降の lysc に戻してよい）。
やり残し（急がない）:
- **MuScriptor**（github.com/muscriptor/muscriptor、Kyutai×Mirelo、2026-11-17 にオーナーから）: 多楽器の採譜（MIDI・MusicXML、拍・小節線も。コード名は出さない）、torch の 103M〜1.4B。コードは MIT、**重みは CC BY-NC 4.0（HF でゲート）**。精度・学習データは README になし（arXiv 2607.08168 未読）。M5 の候補、配るならオーナー判断。 **music-to-midi**（github.com/mason369/music-to-midi、MIT、オーナーから）: 既存モデルの寄せ集め（YourMT3+・MuScriptor・TransKun・Beat This! final0・ChordMini BTC・BS-RoFormer）。重みのライセンス表が役に立つ: **YourMT3+ は Apache-2.0**（M5 の第一候補）、TransKun（ピアノ）MIT、ByteDance のピアノ CC BY 4.0、Aria-AMT は CC BY-NC-SA。GPU 前提。 ／和音のないイントロの小節（`rwc-dev` の P027）は譜面に入れていない。`fold`・`beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# は `r` で N.C.）。最後の和音の終わりは `.orecchiante.json` にない。`check` は `lysc check --batch` で速くなる。crema の `chord_root`・`chord_bass`・`chord_pitch` は未使用。
## 環境（この PC）

- 作業は `C:\MyProj\Orecchiante`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 3 つ＋試し 1 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0、onnx・onnxruntime・onnxscript）が Beat This!・BTC・ChordMini。`.venv-crema`（tensorflow、tf-keras、crema 0.2.0、`setuptools<81`、tf2onnx、onnxruntime）が crema。
  `scratch/d2/venv-onnx`（numpy、librosa、soxr、soundfile、onnxruntime）が ONNX だけの耳。作り方は README。素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Orecchiante\scratch\numba'`、`$env:ORECCHIANTE_LYSC='C:\MyProj\Orecchiante\scratch\lysc-0986\lysc.exe'`（`@or` と依頼 18 の直しを持つ lysc。Lily# の次の版が入ったら LilySharp の `LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe` に戻してよい）。`.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Orecchiante\scratch\torch'`、`$env:TORCHINDUCTOR_CACHE_DIR='C:\MyProj\Orecchiante\scratch\torchinductor'`、`$env:PYTHONUTF8='1'` も。
  `make` の `tim/` は `ORECCHIANTE_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）、`samples` は `ORECCHIANTE_SAMPLES` も。
- 動作確認の 1 コマンド（20 秒）: `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears` → seed1 ルート 95.5%・小節線 97.1%・ベース 76.9%、`rwc-dev` 87.4% / 82.9% / 94.1%、`rwc-test` 88.9% / 83.9% / 95.1%（`--ears` は crema＋BTC）。
- 時間の目安: `--ears` の `table` は RWC 50 曲で 7 秒。`all` は 20〜25 分。RWC の特徴量の作り直しは HPSS ありで 20 分、なしで 3 分。2 分を超える仕事は切り離して走らせ、ログを見に行く。pwsh MCP の 1 回の呼び出しは 115 秒以内に収める:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden -WorkingDirectory C:\MyProj\Orecchiante`止めるときは `measure.py all` を含む python.exe を止める。速さは裏の仕事が終わってから測る（24 コアでも ONNX と torch は取り合う）。
- `lysc check` の合否は終了コードと `warning` / `: error`、`--batch <一覧>` で多数。`C:\MyProj` 全体の再帰検索は固まる。pwsh で書き換えたファイルは Edit の前に読み直す。
  **プレビューで開いている Markdown は Edit を 1 回ずつ出す**。orecchiante の通しは `--models scratch\d2`（`scratch/net/out` にモデルはない）。

### commit の外にあるもの（消えたら作り直す）

| もの | 作り方 |
|---|---|
| 合成曲のセット 15 個（`data/synth/seed*` の wav、`scratch/alt-*`） | `measure.py make`（`measure.py sets` が一覧） |
| `models/chords.joblib` | `train_chords.py data\synth\seed100 --features scratch\feat-h31 --out models\chords.joblib` |
| `scratch/feat-h31/*.npz`（自作の読み取り器用）、`scratch/feat-h0/*.npz`（`--ears` 用。実録音は `real_RWC_P*.npz`） | `measure.py` が初回に作る |
| `scratch/real/rwc-p/`（wav 100 個と truth）、`rwc-annotations/`、`rwc-annotations-archive/`（AIST の構造）、`RWC-P.zip`（3.79 GB） | DATASETS.md の手順 |
| `scratch/sep/venv`（demucs 4.1.0・onnx・onnxruntime）、`scratch/sep/out/htdemucs/*.voc.npy`（torch・ステレオ・ずらしあり、比べる相手） | `scratch/sep/song.py` |
| `scratch/d2/htdemucs-core.onnx`・`htdemucs-mm8.onnx`、`scratch/net/voice/<セット>/*.voc.npy`（.NET の歌、RWC 100 曲・seed1 の 7 曲・`sec`＝オーナーの 2 曲） | `tools/d2/demucs_onnx.py`、`orecchiante ears`（RWC は Calamo と取り合って 92 分） |
| `scratch/d2/htdemucs_6s-core.onnx`・`-mm8.onnx`、`scratch/net/voice/`（6 分割の歌、0.8.0 から）・`voice4/`（4 分割）、`scratch/six/`（`stems/seed1` 正解のパート、`sep/` 分けたもの、`listen/` オーナー用） | `demucs_onnx.py --model htdemucs_6s`、`orecchiante ears`（環境変数つき）、`scratch/six/stems.py`・`sep_seed1.ps1` |
| `scratch/parts/rwc/`（RWC 100 曲の 6 パート・譜面、`<曲>.parts/*.flac`）、`scratch/parts/sec/`（オーナーの 2 曲） | `scratch/parts_rwc.ps1`（`orecchiante <wav> -o … --models scratch\d2`、1 曲 40〜60 秒） |
| `scratch/real/beat_this_annotations/`、`scratch/btc/`・`scratch/chordmini/`（clone）、`.venv-bt/`、`.venv-crema/` | README |
| `scratch/real/beats-bt-folds/*.beats`、`scratch/beats-bt/<セット>/`（合成曲 9 フォルダを `final0` で）、`scratch/sec/*.beats`。0.8.4 から 3 列目つき（無ければ `pair_runs` は働かない） | `track_beats.py`（README。全部で 15 分、`scratch/bt3/regen.ps1`） |
| `scratch/real/chords-crema/`、`scratch/chords-crema/<セット>/` | `crema_chords.py <セットのフォルダ> --out <ここ>` |
| `scratch/**/chords-btc/`（`--probs`）、`chords-crema+btc/`（`--ears` の既定）、`chords-cmbtc/`・`chords-cm2e1d/`、`scratch/d2/btc-large-voca.onnx` | `btc_chords.py --probs`、`two_ears.py`、`chordmini_chords.py`、`tools/d2/btc_onnx.py`（README） |
| `scratch/d2/`（`crema-chord.onnx`、`beat-this-final0-*.onnx`、`crema-classes.json`、`venv-onnx/`、`spike/out/`、`out/`、`mel/`） | README の「Without torch and TensorFlow」の末尾 |
| `scratch/net/`（`out/orecchiante.exe`、`seed1`・`seed7`・`seed11`・`rwc-p` の `.beats`・`.npz`・`.cqt.npy`・`.feat.npz`、ほかの合成曲 4 セットの `.feat.npz`） | README の「In .NET」の各節の末尾（.NET SDK 10.0.401。RWC は耳 9 分、特徴量 33 秒） |
| `scratch/drum/{xsc,rwc-p,sec}/*.drum.npy`（0.10.0 のドラムの特徴） | `orecchiante drums scratch\xsc\parts --out scratch\drum\xsc`（4 分）、`scratch\parts\rwc` → `rwc-p`（1.5 分）、`sec` は `scratch\parts\sec` と arms は `orecchiante ears` |
| `scratch/net/read/<セット>/`（.NET の譜面。`head/seed141` は `seed141`）、`scratch/pyread/<セット>/`（比べる相手の Python の譜面） | README の「In .NET: the rules」の末尾（RWC 100 曲で 27 秒） |

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
- **6 パート:** ギターとピアノの混ざりを int8 をやめて減らす（fp32 で同じ）、guitar・piano（・other）を振幅の比で振り分け直す（指数 1・2・4 で変わらない）。
- **意味で切る section:** 「別の所で前の小節が違って始まる」所を 1 か所でもあれば切る（AIST 65.8% だが My Life の歌を 4 小節ごとに割る）。歌の終わりで、最後の小節の後半が歌っていなければ線を 1 小節前に（効かない）。
  句を秒で決める（10・11・15 秒。糸と RWC を両立する幅がない）。8 小節ごとに無条件で切る（dev 66.9% だが My Life の verse を割る）。歌の割合が次の句で下がる所でも切る（`SUNG_DROP` 0.15: dev +0.3、test 62.0 → 61.4%。test を見てから捨てた）。
  turn の判定（2026-11-17）: 調の turn をやめる（dev 67.0% だが My Life の 6 つを落とす）、`KEY_REGION` 6（dev 67.2%、声なし 50.7 → 49.8%）、同じ調に挟まれた 6 小節未満の調域を数えない（`KEY_DETOUR` 6: dev 66.6 → 67.1%・声なし 50.9%、test 61.9 → 61.8%。7 で声なしの糸、8 で My Life が崩れる）、`LOUD_TURN` 4.5・5・8・なし（dev 66.5・66.6・66.3・66.5%）。ドラムと旋律（2026-11-17、パートから）: 格子点で直前の小節のドラムが前の 4 小節と違う（フィル、16 刻み×3 帯域の余弦）所でも切る＝dev 50 曲で 0.7・0.8・0.9 が 66.2・66.7・66.5%（8 小節前と同じ句で始まる所は除いても My Life の 61・73 を割る。半分の曲では +0.4 に見えた）。格子の外の小節頭のクラッシュ（4 kHz 以上）・グルーヴの変化も、既存の線から 2 小節より遠い所で落とした境目に当たるのは 33〜38%（手がかりなしで約 23%）＝F の半分と同じで足しても動かない。歌の音域（yin の中央値の 4 小節の差）は格子点で境目とそれ以外が同じ分布。`VOICE_SHARE`・`VOICE_GAP` を動かす（オーナーの 2 曲が崩れる）。
  held chord の切れ目の移し方（2026-11-18）: 3・4 小節まで（dev 66.1・65.4%）、主和音でなく「和音が変わる所」へ・主和音を保つ所も（65.6%）、格子を数え直さない（66.3%）。短い section を前に入れるのを 3 小節未満・4 小節未満に（66.5%・65.8%、My Life の 151 と Open Arms の 59 を落とす）。
  歌が始まる所から 8 小節の中の turn を全部捨てる（2026-11-18、My Life に 26・48・100・138 が増える＝136 の riff の turn）、歌が止む所からも同じ扱い（糸の 32 を落とす）。
  塊ごとの格子の位相を「格子点から始まる句がほかでも同じ頭で始まる」数で選ぶ（dev 62.1〜63.7%）。歌の端を、1 小節前から始まる句がほかでも同じ頭で始まるなら 1 小節前に（66.0%）。曲全体で 4 小節の位相を揃える（AIST 自身が 1 つの位相に乗るのは中央値 52%）。
  歌の分離を重なりなしで（3 分の 1 速いが 15/100 曲の section が変わり、合っていた線を失う。オーナー決定）。荒く読んで出入りの周りだけ詰める（区切り 88%＝12% 減るだけで 5/100 曲が変わる。荒い読みが出入りを丸ごと見落とす。`scratch/sec/coarse_fine.py`、近似）。
  xsc の dev で効かない規則の定数（2026-11-19、exact F 34.1% に対して）: `PHRASE_BARS` 8（31.8%）、`BEGIN_SHARE` 0、`PAIR_HEARD` 0、`WHOLE_BLOCK` 4、`LOUD_TURN` 9、`VOICE_GAP` 5、`VOICE_RUN` 4、`TURN_NEAR` 3、`SUNG_WHOLE` 4、`MERGE_SHORT` 6（32.7%）。DP で歌の出入りを固定する（42.4%）・規則の最後の切れ目を手がかりに足す（44.6%）は My Life を戻さない。
  DP に足して効かない（5 分割×2、57.6% に対して）: 小節の 4 分の 1 ごとの歌の割合と弱起・尾の手がかり（`VQ=1`・`2`: 57.5・57.2%）、半分読みの曲で長さの分布を 2 倍に（正解の向きを教えても 57.8%、DP の目的関数で選ぶ `TAUS` 57.4%＝長さの分布ではなく手がかりが弱い）。
  **半分読みの曲（2026-11-20、0.10.0 の手がかりで）:** 正解で半分読みの曲を教え、長さの分布を 2 倍にし、オーナーの線の間では切らせない（`dp.py … ORACLE=2`）でも 5 分割 SEED=1 は 64.3 → 64.2%（`ORACLE=1` 63.5%）、学んだ曲の上でも 69.0 → 69.4%＝DP の側で直せる上限がない。見分ける手もない: コードの変わり目の偶奇の偏り（`scratch/xsc/halfcue.py`）は半分読みの曲のほうが小さい（オーナーの 1 小節に 2 和音＝私たちの小節ごとに変わる。偏るのは 2 小節ごとに変わる「同じ長さ」の曲）。秒も駄目（`barsec.py`）。上げるなら小節の読みそのもの（C4）から。`scratch/xsc/half.py` が曲ごとの頭の位置（オーナーの頭は私たちの 16 小節おき、私たちは 8 おきに切りがち）。
  **16 を 8＋8 に割る誤り（2026-11-20）:** 余分の最多は「オーナーの 16 小節の section の真ん中」（`extras.py`: 余分 約 560 のうち 114、私たちの 8 小節 913／オーナー 649、16 小節 113／190）。オーナーの 16 は前半と後半のコードが似る（`halves.py`: 平均 0.65 対 8＋8 の 0.42）が、私たちが真ん中で切った所に限ると分けられない（`mid16.py`: 0.75 以上の割合 オーナーも切った 0.22・余分 0.36）。試して効かない（5 分割、SEED 1・2・3 の平均 64.9%）: 8 小節幅の手がかり `W8=1`（コードの前後の一致 4・8、音量・鳴る音・新しい和音・歌の 8 小節の差。64.8%）、DP に前半と後半の一致の項 `NU`（0.5〜2: 63.8〜62.2%、`HVL=16` で 16 小節だけ: 64.4〜63.3%、SEED 1）。
- **C3（2026-11-17）:** section のテンポを条件なしで書く（4〜15% のどの閾値でも、印は半分・倍・3/2 の小節の読み違いばかりで `still/` に偽の印）。
  曲間の話し声・無音を N.C. の小節にする（オーナー決定で対象外）。`SHORT_BAR` を 2〜40 で動かす（dev 94.0〜94.1% で平ら）。曲の終わり: 拍の取れている最後の小節を、前の小節の 0.2 倍の音量があれば「コードなし」でも残す（dev 25 → 27/50、test 17 のまま）、拍が切れた後に延ばす小節を 1 つまでにする（前者と合わせて dev 32・test 22/50、小節線 F +0.1・+0.4 だがルート −0.3・−0.1。余分な小節 16 → 1 と引き換えに足りない小節 24 → 39）。RWC の注釈は最後の和音の響きを小節として書く曲と書かない曲がある＝規則では揃わない。
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
- **歌の値は走るたびに揺れる**（区間 3 つを並行に回すとき。RWC_P001 で最大 0.01／0.34、1 つずつなら同じ）。onnxruntime の版でもセッションの共有でもない（README「Six parts」の末尾）。原因は未特定。
- **.NET:** `RWC_P059` の調律が 0.01 ずれる理由。**入力:** 編集リストと `iTunSMPB` が両方ある m4a（いまは編集リストが先）、動画つき mp4、VS Code の Chromium の mp3 の頭、
  メモリの少ない PC で 1 時間、実録音の mp3 の精度（1 曲だけ）、Windows 10・N エディション、RWC を wav から通し、5/4・7/4、別の CPU の速さ、Beat This! の `small0`（8 MB）と半精度（配布元が 500）。通しの規則一致は seed1 だけ。
- 弱起つき・section つき・繰り返し記号つきの `.lys` は `lysc check` と小節数だけ（png は RWC_P010 を 1 枚）。鳴り方は耳で確かめていない。
- 旋律だけの弱起に、自作の読み取り器は和音を書く。半拍より短い弱起は消える。弱起は 1・2・3・4・6 拍だけ（`PARTIAL`）。
- 原因を見ていないもの: `rwc-dev` で最初の小節線が正解と違う 11 曲、`seed41` で外した曲、自作の読み取り器に正解の小節線を与えると実録音のルートが 77.4 → 77.1% に下がること。
- crema の重みのライセンスの根拠は作者の issue コメント（issue 42）とパッケージ同梱。配るときは BSD 2-Clause の表示（Copyright (c) 2017, Brian McFee）を同梱。Beat This! は MIT。
- crema は合成曲の七の和音に弱い（seed7 で 64.1%）。Beat This! が半分のテンポで追う曲は 2 小節が 1 小節になる（C4）。
- RWC の注釈に調がなく、調は実録音で測れていない。大きなテンポ変化（C3）は未対応。Chord Tracker とは比べていない。

## 未決（ユーザーの判断待ち）

- **`x.orecchiante.json` を人が直してよい形式 2 にすること**（オーナーは「人の小節線が正」に同意。形式の中身と、Lily# と Orecchiante の書き分けは依頼 4 の返事待ち）。
- **和音のないイントロ・弱起を譜面に書くか。** いまは書かない（2026-11-17 に尋ねて、オーナーは「今は書かない」側を選んだ）。書くなら N.C. の小節として。`rwc-dev` で落としている和音なしの小節は 14。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）。** 0.5.0（fold なし）で `rwc-test` は「印 28.1% で 72.0%」、`rwc-dev` は 34.9% で 86.0%。印の式（`SURE_OF`）は crema 1 つで選んだまま。選び直すかは未決。閾値は 0.84。
- **値段・印の式・`SAID_AGAIN`・C1 の定数・HPSS なしは `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習ではないつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- **Lily# の和音付け（`ChordHarmonizer.cs`）が作る行の名前 `harmony` を `prog` にそろえるか**（オーナー「あとで別途検討」2026-11-20。そろえるなら既に `prog` がある譜面での重なりを見る）。
- 使いたい曲の種類が RWC-P と違うなら、その種類の曲を数曲（Chord Tracker の結果があればそれも）。
