# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-19 午後（**物差しを直した＋ドラムの型が効く（まだ dettato に入れていない）。** xsc の dev 14 曲は曲ではなくベースのパート（`-bass-<調>-<bpm>bpm-<調律>hz.mp3`＝**Moises で抜いたベース**、オーナー。xsc にこの名前があればパート。**パートを dettato に入れることはない**＝ベースのパート譜を耳コピするための音源で、コード譜は必ず元の音源から（オーナー決定）。物差しはミックスで測り、パートの入力は考えない）だった→ 13 曲を元のミックスに差し替え（`swapmix.py`）、今の重みのまま dev exact 62.1 → 64.9%。ドラムの手がかり `dp.py PARTS=d` で 5 分割 60.8 → 65.0%、**test（1 回だけ数えた）66.2 → 69.8%**。パートの FLAC を 6 本同時に書くと Windows が時々拒む（2/190 曲、終了コード 3）のを直した（`Program.cs`、未配布）。README「The measure corrected」「The drums' pattern」。以下は午前の記録）。
前回: 2026-11-19（**0.9.0: section をまとめて選ぶ（`SECTION_DP`、Python と .NET の既定。オーナー「(c) かな。固有の曲の悪化はあまり気にしない。平均をより向上させる方法を探して」→「進めて」）。** オーナーの Transcribe! の .xsc の test（数えるだけ）exact F 38.4 → 66.4%、AIST rwc-test 3 秒 62.2 → 72.3%・0.5 秒 37.7 → 52.7%。`read_check.py` は 368/368 一致。入った exe で Open Arms を通しで読んで Python と同じ頭。物差しは `F:\Transcribe\弾いてみた\xsc`（278 曲が使える。`scratch/xsc/`: `index.ps1`→`prep.py`→`dur.py`→`run.ps1`→`eval.py [dev|test]`。dev＝x001〜x145 141 曲で選ぶ、test＝137 曲は数えるだけ）。それより前（0.8.12 進捗、0.8.11 `break`、0.8.10 `SUNG_WHOLE`、0.8.9 `HELD_SHIFT`・`MERGE_SHORT`、0.8.6 `@or`・`time 3/4`、0.8.5 `time 2/4`、0.8.4 2 拍の小節、0.8.3 section のテンポ、0.8.0 6 パート、0.6.0 歌、0.5.0 意味で切る section）は README と `git log`）。

### D4 の形（2026-11-15、オーナーと話して決めた。依頼 4・[便り](repro/lilysharp-requests-2026-11-15-listening.md)）
**Lily# の設計メモ `C:\MyProj\LilySharp\docs\recording-design.md`（セッションの頭に読む）:** 紐づけは隣のファイル（形式 2）だけ、m4a は Lily# の LSP が復号。
**目標は Transcribe! と同じ体験を Lily# で**（耳コピの道具。**特定の楽器に寄せない**＝オーナー 2026-11-17「ベースは私が趣味で弾いているだけ。ユーザーはいろんな楽器を使う」。オーナーの曲をベースで試すのは一例）。**人が打った小節線が正**（形式 2）。Dettato は `tuning`・`beats` を入れた（読み直しの入口は不要）。

## 現在地

**`dettato <録音> [-o <x.lys>] [--force]` が、mp3・m4a・mp4・aac・flac・wma・wav から `.lys` と `.dettato.json` を録音の隣に書く**
（Python なし。RWC_P001 の mp3 で 6.3 秒）。配る形は .NET＋ONNX の実行ファイルを VS Code 拡張に入れる（Calamo と同じ形）。
**拡張 `yotsuda.dettato` 0.9.0 をこの PC の VS Code に入れた**（0.9.0: section をまとめて選ぶ、入った exe で Open Arms を通しで読んで Python と同じ頭。0.8.12: 進捗を細かく。0.8.11: `break` は section の終わりだけ。0.8.10: 歌の止みで 8 小節の verse を割らない `SUNG_WHOLE`、入った exe で Open Arms・糸・My Life を通しで読んで scratch と同じ頭。0.8.9: section の held chord と 1 小節の section、入った exe で Open Arms を通しで読んで scratch と同じ頭。0.8.8: 最初の section の上に Join Sections（Ctrl+.）のコメント。0.8.7: 譜面の頭のコメントが `@or` の上での Shift+Ctrl+Up/Down も言う。0.8.6: `@or`・`time 3/4`。0.8.5: `time 2/4`。入った版で糸を通しで読み、scratch の exe と `.lys` が一字一句同じ（`scratch/c3/ito/`）。0.8.4: 2 拍の小節の組み方。0.8.3: section のテンポ。0.6.0: 歌を聞いて section を切る。0.7.0: パートを書く。0.8.0: 6 パート。0.8.1: 歌の中の境目。0.8.2: 格子の起点。入った版の通しで糸・My Life の section は前と同じ）（`extension/`、`tools/deploy-extension.ps1`。.vsix 167 MB、Lily# とは別の拡張）。**コマンドはない**: メニューは Lily# の
「Convert Recording to Lily# (.lys)… (Preview)」で、Lily# が `ext.activate()` で `{path, version, protocol, inputs}` を受け取る。**VS Code の中で動くことはユーザーが確かめた（0.2.0。0.3.x は Lily# のメニューからはまだ）。**

### コマンドと入力（D3。`Program.cs`・`Audio.cs`。README の「A recording as it comes」、Lily# 向けは便りの §2a）

- stdout は書いた `.lys` のパス 1 行、stderr は段階ごとに 1 行（0.8.12 からパートの分離の 5% ごとも。`Voice.Loudness` の `separated`）で失敗時は最後の行が `dettato: <理由>`。終了コード 0 書けた／1 録音が読めない／
  2 引数（`.lys` が既にある、を含む）／3 読み取り器自身。2 つのファイルは `.part` に書いてから置き換える。5 秒未満は読まない。
- `dettato --version --json` → 版・`protocol` 1・`commands`・`inputs`（拡張が `bin\dettato.json` から返す）。版は `Program.cs` と `extension/package.json` の 2 か所。
- **wav 以外は Windows の Media Foundation（NAudio 2.2.1、MIT）で読み、エンコーダの足す頭の無音を `Audio.Lead` が取り除く**（合わせる相手は再生する側。`F:\Music` の 7 種すべて ffmpeg と 0 標本。細部は README の「A recording as it comes」）。
  58 分の録音も読める（残る大きいメモリ: `Features.Of` と Beat This! の段で各 +1.2 GB）。道具: `tools/heard_check.py`・`kinds_check.py`・`chrome_lag.py`。ffmpeg は PATH にある。

### .NET の規則（`Reader.cs` 726 行・`Form.cs` 209＝`Turns`・`Phrases`・`Writer.cs` 251。README の「In .NET: the rules」。全体で 2776 行）

- 移したのは `--ears` の道だけ。自作の拍追跡・`MODEL`・`--tempo`・`why` は Python に残す。
- **`.dettato.json` は一字一句、`.lys` は 1 行目以外、268 曲すべてで Python と同じ**（`tools/read_check.py`。比べ方とコマンドは README）。
  同じ拍と確率（`--lines`・`--heard`）と同じ特徴量（`--net scratch/net`）を与えて比べる。`measure.py … --read` は .NET の譜面を数える。
- 一致させるために真似たこと（`Np.cs`）: numpy の和の順、単精度のままの平均とノルム、Python の `//` と `round`、`repr`、同点の扱い。
- **規則を変えるときの手順:** Python で試して測る → .NET に写す → `read_check.py` で全セット一致を確かめる（Python が正）。

### .NET の耳と特徴量（`dettato ears`・`dettato features`。README の「In .NET」の最初の 2 節と「Without torch and TensorFlow」）

- 耳: wav → `.beats`（Beat This!）と `.npz`（crema と BTC の平均、0.4.0 から）、特徴量: wav → `.feat.npz`。突き合わせは `tools/net_check.py`、`measure.py --net scratch/net` で表は全列同じ。
- **`--ears` では HPSS を外した**（`use_ears()` が `HARMONIC_FRAMES = 0`。特徴量は `scratch/feat-h0/`）。1 曲 3 分半で `dettato read` 5.5 秒（Beat This! 3.7）。onnxruntime は 8 スレッド（24 だと遅い）。

### 数字（`measure.py all`、2026-11-19 の 0.9.0＝section の行だけ動いた（`all` は歌なしで読む）。lysc check 1388/1388（依頼 18 の直った lysc `scratch/lysc-0986`）、SIDECAR 1388/1388。ログは `scratch/all.log`、前は `all0812.log`。section が 1 つで `time` の小節がある譜面は section の形で書くようにした＝seed7 の s7-009 が落ちたため）

| `--ears`（crema＋BTC） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 87.4% | 88.9% | 88.2% | 95.5 / 94.4 / 96.7% |
| ルート＋長短 | 82.9% | 83.9% | 83.4% | 92.4 / 90.9 / 94.3% |
| ベース音まで | 65.1% | 66.6% | 65.9% | 76.9 / 65.1 / 80.0% |
| 小節線 F | 94.1% | 95.1% | 94.6% | 97.1 / 91.0 / 94.1% |
| テンポ・拍子 | 47・49 | 47・49 | 94・98 | |
| 印 → 拾う誤り | 34.9% → 85.9% | 28.1% → 72.0% | 31.5% → 78.9% | 14.9% → 74.0% |
| section（1 曲あたり、AIST 3 秒の F は声なし／歌つき） | 11.4、69.8／76.2% | 10.1、63.0／72.3% | 10.7 | 3.6 / 3.5 / 3.1 |

- **持ち越す知見（README にないもの）:** 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。RWC の正解は両端を語らない。ONNX を書き直すとき: 旧式のトレース直後の torch のモデルは同じプロセスで答えが変わる（比べる前に読み直す）。dynamo は `PYTHONUTF8=1` が要る。

## 次の一手

**ドラムの手がかりを dettato に入れる（0.10.0）。** dev で選び test で確かめた（上の数字）。手順:
1. **.NET が先に特徴を作る**（Python と一致させるため、`--voice` と同じく .NET の出力を Python が読む）: `Voice.Loudness` で drums も足し合わせ（今は vocals だけ。`Segmented(all)` で全パートが出る）、10 ms ごとの drums の rms と 3 帯域（<200 Hz・〜4 kHz・>4 kHz）の正のスペクトル流束（2048 点ハン窓、hop 441、`log1p(100|X|)`）を `<name>.drum.npy` に。`dettato ears` も書く。xsc 278 曲は Demucs を回し直さずに済むよう、既にある `scratch/xsc/parts/<id>.parts/drums.flac` から作る小さな入口があると速い（Demucs からだと 2 時間半）。
2. **Python `read_chords`**: `read(..., drums=)` で小節ごとに drums の dB と 16 刻み×3 帯域の型（刻みの中の最大、単位長）→ `dp.py` の `parts()`（`PARTS=d`）と同じ 6 列を `section_rows` に足す（前後 3 小節も並べる）。`capture.py` が drum を取り、`dp.py` が `.drum.npy` から読むように（今は `partcues.py` の Python の FFT＝配るものと別物なので、**.NET の特徴で学び直してから** `--json`）。
3. **.NET `Sections.cs`** に同じ列、`read_check.py`（`--drums <folder>`）で 368/368。合成曲は drums が無い／GM の音＝`--voice none` と同じく無しの扱いを決める（無ければ 0 列で学んでいないので、既定の値を入れる）。
4. AIST（RWC、`scratch/parts/rwc/<曲>.parts/drums.flac` がある）でも測る。オーナーの 3 曲（糸・My Life・Open Arms）を見る。
- 試して効かなかった（同日）: 6 パート全部（64.5%、ドラムだけより少し下）、その小節だけの列（前後を並べない、62.2%）、ドラムの音量だけ（60.6%）。歌のない曲で旋律楽器を声の代わりにする `lead.py` は、歌のない dev 曲がほぼベースのパートだったので試す意味がなくなった（test の本物の器楽 6 曲で、配る前に見るなら）。
- 物差しの直し（`scratch/xsc/`）: `mixes.py`（ミックスを探してずれを測る）→ `mixprep.ps1`（wav・耳・パート）→ `swapmix.py [--undo]`（`truth-stems.json` が元、`stems/` にパートの側）→ `capture.py`（`ONLY=`・`VOICEDIR=`）。x093（Stayin' Alive）はミックスが別編集でパートのまま。**dev の数字はこれから差し替え後で比べる**（今の重みで 64.9 / 69.9%、5 分割 60.6〜61.0%）。test の「今の重み」は dp.py の数え方で 66.2%（README の 66.4% は eval.py で読んだ値。差は未確認）。
- パート: `scratch/xsc/parts/<id>.parts/*.flac`（289 曲、`parts.ps1`／`parts2.ps1`、2 本並行で 2 時間半）、手がかり `scratch/xsc/pc/<id>.npz`（`partcues.py`、10 秒）。
- **FLAC の直し**（`Program.cs`、`scratch/net/out-fix` でビルドしただけ、x031 で通った）: wav の入力では Media Foundation が始まっておらず 6 スレッドが同時に `Startup` していた（NAudio は守っていない）のが原因と見て、先に 1 回始め、拒まれたパートは 1 本ずつ作り直す。原因の確証はない（RWC の wav 100 曲では 0 回）。次に配る版に入れる。

**section をまとめて選ぶ `SECTION_DP`（2026-11-19、0.9.0 で Python と .NET の既定。`SECTION_DP = 0`／`Form.SectionDp = false` で前の規則の切れ目）。.NET は `Sections.cs`、`Form.Phrases` が `WHY` を記録して呼ぶ。`read_check.py` 368/368（RWC 歌つき 100・歌なし 100、合成曲 7 セット 168）。**
- 仕組み: 小節線ごとに手がかり 35 個＝`section_cues`（今の規則がそこで切った理由 `WHY`、歌の出入り、4・8 小節の句の繰り返しと再演、主和音・属和音・保った和音、歌の割合）26 個＋`audio_cues`（前後 2・4 小節の音量・鳴る音・和音の違い）9 個、を
  **前後 3 小節ぶんも並べて（245 個）**ロジスティック回帰（C=0.1）で点数にし、`choose_cuts` が動的計画法で選ぶ: 切れ目の点数 − off ＋ λ×log（オーナーの section の長さの分布）＋ μ×（その区間が同じ長さで別の所でも鳴る、`played_again`）。
  重み・長さ・λ 0.5・off −1.5・μ 0.5 は `tools/sections_dp.json`（`scratch/xsc/dp.py EXTRA=1 NB=3 PH=0 C=0.1 AUDIO=1 MUS=0,0.5 --json`、xsc の dev だけで学んだ）。.NET は `Sections.cs` が同じ JSON を exe に埋め込んで読む（`Dettato.csproj` の EmbeddedResource）。点数は小数 9 桁に丸め、対数は `math.log`＝足す順が違っても同じ切れ目。
- 数字（規則 → `SECTION_DP=1`）: **xsc dev exact F 34.1 → 62.1%**（学んだ曲。5 分割の交差検証で 57.2〜57.9%）・near 51.8 → 67.7%。**xsc test（数えるだけ）exact 38.4 → 66.4%・near 56.8 → 71.8%**（読む 1908 → 1395、オーナー 1531）。
  **AIST（歌つき）rwc-dev 3 秒 66.8 → 76.2%・0.5 秒 42.5 → 58.3%、rwc-test 3 秒 62.2 → 72.3%・0.5 秒 37.7 → 52.7%。** 手で切った 3 曲: 糸は 21・44 を落とす、My Life は 105・151 を落として 8 つ増える、Open Arms は 101 を拾い 43・93 が増える。
- 交差検証で効いたもの（5 分割×2 通り、差し替え前）: 26 個だけ 44.0% → 前後 3 小節 55.7% → 音 56.8% → 区間の繰り返し 57.6%。効かない: 位相、2・12・16 小節の句、勾配ブースティング、構造化パーセプトロン（55.4%）。道具: `capture.py`（`cap/<id>.pkl`、80 秒）→ `dp.py [--cv] [K=5 SEED=1] [--test] [--json] …`、`eval.py [dev|test] [--why]`、`look.py <id>`、`barshift.py`、`errs.py [--each]`（種類ごとの F）。手を変えたら: 取り直す → `dp.py --cv K=5`（2 SEED）→ `--json` → test を 1 回 → `Sections.cs` に写して `read_check.py`。配る重みは dev だけで学ぶ（test を物差しに残す、私の判断）。
- **どこで落とすか（`errs.py`、差し替え後の dev）:** 小節が同じ長さ・線も 9 割合う・歌ありの 116 曲が 68.2%（誤りの 77%、読む 1385／オーナー 1226＝1 割多い）、小節を半分で読む 11 曲 34.4%（11%、秒では見分けられない＝`barsec.py`、小節の読みの問題）、歌が聞こえない 3 曲 16.7%。規則の定数（単独で小さい）: `LEAD_IN` ±0、`VOICE_LATE` 0.1 +0.5、`KEY_TURN` 3 +0.9、`MERGE_SHORT` 4 +1.0（差し替え前）。

**section（意味で切る。0.5.0 で既定、0.6.0 で歌も .NET に。オーナー: 調や曲調が変わる所・歌が入る所・ソロで切りたい）**
- オーナーが手で分けた 2 曲: 糸（`scratch/ito/ito-one.lys`: 1・9・17・21・28・32・40・44・51・59）、My Life（`scratch/sec/phrase/mylife-owner.lys`: 1・9・17・31・39・53・69・83・91・105・121・129・135・151）。
  **0.6.0（.NET の歌）で 2 曲とも全部、余分なしで出す**（糸は終わり 67 が増えるだけ。入った拡張の通しの読みでも同じ）。声なしだと糸は 9 つ中 5 つを落とす。
  確かめは `scratch/sec/try.py [定数=値 …]`（`VOICEDIR=scratch/net/voice/sec` で .NET の歌、`NOVOICE=1` で声なし）と `tools/aist_sections.py rwc-dev`（既定で .NET の歌）。**規則を触ったら必ず両方を見る**。
- **Open Arms（Journey、3/4・102、2026-11-17 オーナー）: 「35 小節目が section の頭なのに 33 で切れている」「細切れすぎる」。** 材料は `scratch/sec/arms-in/arms.wav`（ffmpeg）、耳と歌 `scratch/net/voice/sec/arms.*`、見る道具 `scratch/sec/arms.py [定数=値]`（歌の割合・turn・小節ごとのコード）。
  **オーナーの分け方（`scratch/sec/arms-owner.lys`）: 1・9・17・25・35・51・59・67・75・85・101**＝8・8・8・10・16・8・8・8・10・16・10 小節（歌の 8＋延長 2 の 10 小節、サビ 16 小節を 8 で割らない。**67 は 2026-11-18 にオーナーが足した**＝59〜74 は同じ 8 小節 2 回）。`scratch/sec/try.py` の 3 曲目（材料は `scratch/sec/arms.*`、`VOICEDIR=scratch/net/voice/sec`）。小節ごとの表は `scratch/sec/arms_bars.py`（オーナーの頭に *、歌の割合、コード）。
  0.8.9（`HELD_SHIFT`・`MERGE_SHORT`）で 35・85 を拾った。**オーナーの 0.8.9 への評（2026-11-18）:「前回ほど細切れではないが、まだ細かい」。15＝切れ目ではない（2 小節しかない）、62・65＝切れ目ではない・67 で切るべき、79＝本来は切るほどではないが許容できる（音楽が切れた感が少しある・6 小節）、100＝101 なら素晴らしい・1 小節のずれは手で直せるかも。**
  **0.8.10 で `SUNG_WHOLE` 2 を入れた（README「A verse the singing only pauses in」）**: 歌が始まる所から、その 8 小節がほかでも同じに鳴るなら、中で歌が止む・始まるのでは切らず、その止みから `TURN_NEAR` 以内の調・音量の turn も捨てる。15・62・65 が消え 67 を拾った。**残り: 落とす 101、余分 79（許容）・100・109。** 入った exe の通しの読み `scratch/sec/arms-ext/arms.lys`（0.8.9 のは `arms-089.lys`）、糸・My Life は `scratch/sec/ext0810/`（前と同じ）。
  **100→101 はオーナーの判断待ち（2026-11-18、私は「入れない」を勧めた）**: `HELD_SINGS` 1＋`HELD_SUNG` 0.2（Python だけ、既定 0＝切）＝歌が止む所で主和音以外が小節線をまたいで保たれ、その小節の歌が 0.2 以上なら、`HELD_SHIFT` と同じく主和音の来る所へ（近くの turn が代わりに立つ場合もその turn を動かす）。Open Arms は 101 を拾い余分は 79・109 だけ、糸・My Life 同じ、AIST dev 3 秒 66.8 のまま・0.5 秒 42.5 → 42.4%、test 62.2 → 62.1%・37.7 → 37.9%。条件の歌なし（全部の止み）は dev の一致 368 → 364・test −0.3、`HELD_SUNG` 0.15 も悪く 0.3 は 101 を落とす＝閾値は 1 小節（0.24）に合わせた狭い幅。入れるなら .NET に写して `read_check`。残り: 109＝最後の 2 小節（曲の終わりの器楽の尾を前に入れる。`MERGE_SHORT` 3 は My Life の最後の section と 59 を壊した＝最後の塊だけの扱いが要る）。79 はオーナーが許容。
- 仕組み（README「Sections by meaning」「The voice in .NET」）: 歌の出入り・調号 2 つ以上・6 dB で塊に切り、丸ごと別の所で鳴る塊の頭と終わり、長い塊の中は 4 小節の句（`PHRASE_BARS`）の格子で「同じ句 2 回の終わり」「V→I」「別の所でも始まる句（`BEGIN_SHARE`）」「8 小節ごとの新しい組（`PAIR_HEARD`、0.8.1）」で切る。名前は再演なら A2。fold はしない。.NET は `Form.Turns`・`Form.Phrases`・`Form.VoiceTurns`。
- **歌の中の境目（0.8.1 `PAIR_HEARD`）と格子の起点（0.8.2 `GRID_ANCHOR`）:** README「The grid and the turns」。dev 64.4 → 66.6%、test 60.5 → 61.9%（0.8.2 は test で効いていない）。道具: `scratch/sec/vab.py`・`grid_cues.py`・`grid_rules.py`・`eight.py`・`sweep.ps1 '<定数=値 …>'`（dev の F＋オーナーの 2 曲）・`song_phase.py`・`phase_look.py`・`owner_edges.py`。名前の文字に R はない（`R2` は Lily# で全休符）。
- **歌（0.6.0、0.8.0 から 6 分割の歌）:** `Voice.cs` が Demucs（`htdemucs_6s`、MIT）の ONNX（`tools/d2/demucs_onnx.py --model htdemucs_6s`。STFT と逆 STFT は外、行列積だけ int8＝64 MB）を 7.8 秒ずつ、ずらしなし・モノラルを両チャンネルで回す。AIST 3 秒以内の F: dev 64.4%・test 60.5%（4 分割は 64.3・60.1）。**歌は `scratch/net/voice/`（6 分割。RWC 100・seed1 の 7・sec）、4 分割のものは `voice4/`。** 歌のない曲は `VOICE_FLOOR`（0.05）で歌なし（seed1 の 7 曲で確かめ直した）。**Python と .NET は同じ `.voc.npy` で RWC 100 曲が一字一句同じ**（`dettato read … --voice <folder>`、合成曲は `--voice none`）。
- **時間とメモリ（0.6.1）:** 通しで RWC_P001 33〜45 秒（Calamo の負荷しだい）、ピーク 8.6 GB。並行は 3（`Voice.Parallel`）。結果を変えずに速くする手は見つからなかった（README）。重なりなしは捨てた（オーナー決定）。section の頭の比べ方は `scratch/sec/ov_cmp.py <A の譜面> <B の譜面>`（最寄りの AIST つき）。
- **パート（0.7.0、オーナー「レコーディングのオンオフをパートごとに。耳コピにとても便利」。README「The parts」）:** 変換のたびに `<名前>.parts/*.flac`（モノラル 16 bit 44100 Hz、録音の秒の上）と `x.dettato.json` の `parts`（SIDECAR.md）。確かめは `scratch/sec/parts_check.py <録音> <x.parts>`（和・ずれ）。**Lily# 依頼 16 は fixed**（パートの行は `parts` の数と順で出る。全部オンなら元の録音、1 本でもオフなら残りの和）。ステレオのパートは未。
- **6 パート（0.8.0、オーナー決定 2026-11-17。README「Six parts」）:** drums・bass・other・vocals・guitar・piano（`DETTATO_VOICE_MODEL=htdemucs-mm8.onnx` で 4 に戻せる）。6 本が落とす音は `other` に足して和を録音に戻した。アコギとピアノは割れる（オーナー「若干ギターとピアノがうまく分離できていない」）。int8 のせいでも振り分けでもない。5 パートと聴き比べて **6 のまま（オーナー決定）**。合成曲の割合は参考にしない（GM の音、オーナー）。残る手（今はやらない）: 別のモデル（BS-RoFormer の 6 パート版 `bs_6stem_fixed`＝music-to-midi が使うもの。**重みに許諾なし**（配布元にライセンスの記載がない、同プロジェクトの THIRD_PARTY_NOTICES）＝配れない）、MoisesDB で実録音を測る（登録が要る）。道具は `scratch/six/`（README）。

- **次:**
  (1) **歌の中の境目は一区切り**（0.8.1・0.8.2 で dev 64.4 → 66.6%、test 60.5 → 61.9%）。2026-11-17 に turn の判定（`scratch/sec/turn_kinds.py`）、ドラムのフィル・クラッシュ・歌の音域（`drum_cues.py`・`drum_rules.py`・`owner_fills.py`）を試して、どれも効かなかった（捨てた案）。 RWC のパートは `scratch/parts/rwc/<曲>.parts/`、オーナーの 2 曲は `scratch/parts/sec/`（新しい手がかりを試すときの材料）。調の偽物は 4〜5 小節の行って戻る調域に偏る（P023・P019）。
  (2) **C3（テンポの大きな変化）は 0.8.3 で最初の手を入れた**（README「A section's own tempo」、SIDECAR の小節の `tempo`）: 曲順に section を歩き、 section の小節の平均が今のテンポから 6% を超えて離れ、6 小節以上、4 分の 3 が中央値の 1 割以内、比が 1.3 未満なら、section の頭に `tempo 4 = x`（Lily# は section の頭のテンポを毎回鳴らし、次まで保つ。紙では曲頭の値は A のもので置き換わって刷られる＝png で確かめた）。頭のテンポに戻る section は 2 小節でよい。 測り方: `scratch/c3/tempo_try.py <譜面> <truth> --written [-v]`（正解の小節が、そこで立っているテンポの小節と 4% 以内か）。dev 91.2 → 91.4%、test 95.2 → 95.3%（**test の曲ごとの行を見た**）、`drift5/seed31` 66.9 → 70.2%、ほかの合成曲は印 0。 条件なしだと印はすべて小節の読み違い（半分・倍・3/2）だった。P038 の最後の 140.2 は誤り（真は 111、小節を短く読んでいる）。**バンド練習のような録音は考えない（オーナー決定 2026-11-17「練習時の録音を譜面に落とすことはほとんどない」）**＝曲間の話し声・無音（Beat This! が拍を打たず 91〜112 秒の 1 小節になる）は扱わない。`scratch/c3/long/` は参考に残す。 `still/`・`drift5/` の耳（`scratch/beats-bt/<still|drift5>/seed31`・`chords-crema+btc/…`、歌は `scratch/net/<still|drift5>-seed31`）を作った。Beat This! は 5% 揺れでも小節線 92.8%（自作の追跡は 80.9%）＝小節線はもう問題でない。 残り: rit・フェルマータ（曲の終わりの数小節。section より細かい。**曲の終わりは 2026-11-17 に測った**: `scratch/c3/tail.py <譜面のフォルダ> [-v]`＝最後の小節の頭が正解の最後の小節の頭か、その終わり（最後の一撃）に 0.3 秒以内か。dev 25/50・test 17/50。曲ごとは `ends.py`、手がかりは `endfeat.py`。規則は捨てた）、途中で止まる曲（**RWC-P には測る材料がない（2026-11-17、`scratch/c3/stops.py <譜面> 1 100 [-v]`）**: 正解の中に N.C. も小節の隙間もなく、前後の 1.35 倍を超える小節は 100 曲で P034 の 1 つだけ。拍子の違う小節は P006・P022・P037・P038・P043（3/4・5/4・6/4）にある。P038 の小節線 67/138 は拍子ではなく、線が正解より 0.1〜0.26 秒早い区間が多いため＝許容 0.07 秒の外）、範囲のテンポ・人の拍マーカー（Lily# の依頼 4）。(3) 速くする手が要るなら: 重なりを 4 分の 1 より小さく（8 分の 1 など、同じ道具で測る。未測定）、空いた PC で測り直す、蒸留した小さなモデル（学習の音源はオーナーが決める）。
- **糸の M64（6 拍＝4＋2）は 0.8.4 で入れた**（README「A bar of two beats」）: `.beats` の 3 列目＝拍の ±2 フレームの downbeat の値の最大（`track_beats.py`・`Beats.cs`、.NET と torch の差 0.04 以内）。 `pair_runs`／`PairRuns` が `MEND_BARS` の前に、2 拍の小節が続く所を「残す線の値 − 落とす線の値」が最大になるよう組む。1 つ残す 2 拍の小節は値段 12、拍の最初・最後の線では無料。 糸は 253.96 秒から正しい（`G | C#m7(b5) | C Dsus4 | C D …`）。2 拍の小節はオーナーの位置より 1 小節前（値が 249.96 秒で 3.2、251.94 秒で 2.2）、最後の section の名前が D3 → G（再演と見なされなくなった）。
  **0.8.5 から `time 2/4` を書く（オーナー決定 2026-11-17、前の「書かない」を改めた。README「A bar of 2/4」）**: 前後 4 小節の中央値の 0.4〜0.6 倍で、両隣が 15% 以内、4/4 だけ、弱起・最初・最後でない、section を演奏するたびに同じ小節。次の小節に `time 4/4`（section の最後なら不要＝Lily# は section の終わりで戻る、確かめた）。RWC で 10 曲 14 小節（両端が正解に乗るのは dev 6/10・test 0/8、RWC に本物の 2 拍はほぼない）、合成曲 4 小節。外れても読みの格子では 2 拍の長さ＝録音と同じ速さで鳴り、直す所の印になる。測る道具 `scratch/c3/short_bars.py <譜面の根> <セット> [-iso] [-v]`。**0.8.6 から 3 拍も `time 3/4`（オーナー決定 A、2026-11-17。README「A bar of 3/4」）**: 0.65〜0.85 倍、最後の 2 小節は除く（曲の終わりの rit を短く読んだ小節が 6/7）。RWC に曲の途中の本物の 3/4 はほぼない（P006 だけ読めている）。RWC で 6 曲 7 小節、合成曲 2 曲。道具 `scratch/c3/three_look.py`。依頼 11 は fixed（オーナーの版で確かめたら `done`）、15 は closed。**オーナーは 0.8.5 の糸（`scratch/c3/ito/ito-ext.lys`）の 2/4（64 小節目）を「自然に正しく鳴っている」、70 小節目からの終わりの rit のずれは「許容しても良い」**（rit で長くなる小節を短く・少なく読む＝捨てた案の C3「曲の終わり」と同じ所）。
- **D4 で Dettato 側に作るものは今はない**（読み直しは単に上書き＝オーナー決定、設計メモ §6）。配り直すときは版を 2 か所（`Program.cs`・`extension/package.json`）と CHANGELOG で上げて `tools/deploy-extension.ps1`。
- **Lily# の依頼（`docs/repro/REQUESTS.md`。セッションの頭に見る）:** **17（`@or`）は Lily# `f89b01dcd` で fixed、Dettato は 0.8.6 で `@or(候補)` だけを書く＝変換メニューから実物で確かめたら `done`。18（`time 3/4` の小節の `.` の検査）は Lily# `f6256c026` で fixed、19（A-B の旗）も `2882ef7e9` で fixed。lysc は Lily# の HEAD（`098675fb6`）を `git archive` して `dotnet build scratch\lysc-src\LilySharp.Cli -c Release -o scratch\lysc-0986` したもの＝`scratch/read-all/` の 1492 個がすべて通る（`DETTATO_LYSC` をそこに。古い lysc は `@or` を読めず、`lysc-f89` は P012 で偽の警告）。** 1 は `done`。4 は Lily# が作り中（S1〜S3 済み、次は S4）。**16（パートのオン・オフ）は `fixed`＝オーナーの版で確かめてから `done`。** **5（m4a の `iTunSMPB`）は `fixed`＝実物の m4a で確かめてから `done`。** 7（小数のテンポ。Lily# が 6 と書いたのを振り直した）は 0.3.1 で入れた。**6（保存先のダイアログ・`audio` の道）・8（`warpFit` を中央値に）・9（`lysc shape`・`LILYSHARP_LYSC_PATH`）は Lily# master で `fixed`、Dettato は 9 を 0.3.2 で使った。Lily# の次の版が入ったら、変換メニューから 3 つとも実物で確かめて `done`**（3・5 も同じく確かめてから）。**この PC の Lily# 拡張（名は 0.16.0、11-17 17:12 に入った master のビルド）には 6・9・11・16 の文字列がある**（`Locate Recording`・`LILYSHARP_LYSC_PATH`・`The score plays`・`"parts"`）＝残るのは変換メニューからの実物の確かめ（VS Code の操作が要る）。
やり残し（急がない）:
- **MuScriptor**（github.com/muscriptor/muscriptor、Kyutai×Mirelo、2026-11-17 にオーナーから）: 多楽器の採譜（MIDI・MusicXML、拍・小節線も。コード名は出さない）、torch の 103M〜1.4B。コードは MIT、**重みは CC BY-NC 4.0（HF でゲート）**。精度・学習データは README になし（arXiv 2607.08168 未読）。M5 の候補、配るならオーナー判断。 **music-to-midi**（github.com/mason369/music-to-midi、MIT、オーナーから）: 既存モデルの寄せ集め（YourMT3+・MuScriptor・TransKun・Beat This! final0・ChordMini BTC・BS-RoFormer）。重みのライセンス表が役に立つ: **YourMT3+ は Apache-2.0**（M5 の第一候補）、TransKun（ピアノ）MIT、ByteDance のピアノ CC BY 4.0、Aria-AMT は CC BY-NC-SA。GPU 前提。 ／和音のないイントロの小節（`rwc-dev` の P027）は譜面に入れていない。`fold`・`beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# は `r` で N.C.）。最後の和音の終わりは `.dettato.json` にない。`check` は `lysc check --batch` で速くなる。crema の `chord_root`・`chord_bass`・`chord_pitch` は未使用。
## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 3 つ＋試し 1 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0、onnx・onnxruntime・onnxscript）が Beat This!・BTC・ChordMini。`.venv-crema`（tensorflow、tf-keras、crema 0.2.0、`setuptools<81`、tf2onnx、onnxruntime）が crema。
  `scratch/d2/venv-onnx`（numpy、librosa、soxr、soundfile、onnxruntime）が ONNX だけの耳。作り方は README。素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、`$env:DETTATO_LYSC='C:\MyProj\Dettato\scratch\lysc-0986\lysc.exe'`（`@or` と依頼 18 の直しを持つ lysc。Lily# の次の版が入ったら LilySharp の `LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe` に戻してよい）。`.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'`、`$env:TORCHINDUCTOR_CACHE_DIR='C:\MyProj\Dettato\scratch\torchinductor'`、`$env:PYTHONUTF8='1'` も。
  `make` の `tim/` は `DETTATO_SOUNDFONT_ALT`（`C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`）、`samples` は `DETTATO_SAMPLES` も。
- 動作確認の 1 コマンド（20 秒）: `.venv\Scripts\python.exe tools\measure.py table seed1 rwc-dev rwc-test --ears` → seed1 ルート 95.5%・小節線 97.1%・ベース 76.9%、`rwc-dev` 87.4% / 82.9% / 94.1%、`rwc-test` 88.9% / 83.9% / 95.1%（`--ears` は crema＋BTC）。
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
| `scratch/real/rwc-p/`（wav 100 個と truth）、`rwc-annotations/`、`rwc-annotations-archive/`（AIST の構造）、`RWC-P.zip`（3.79 GB） | DATASETS.md の手順 |
| `scratch/sep/venv`（demucs 4.1.0・onnx・onnxruntime）、`scratch/sep/out/htdemucs/*.voc.npy`（torch・ステレオ・ずらしあり、比べる相手） | `scratch/sep/song.py` |
| `scratch/d2/htdemucs-core.onnx`・`htdemucs-mm8.onnx`、`scratch/net/voice/<セット>/*.voc.npy`（.NET の歌、RWC 100 曲・seed1 の 7 曲・`sec`＝オーナーの 2 曲） | `tools/d2/demucs_onnx.py`、`dettato ears`（RWC は Calamo と取り合って 92 分） |
| `scratch/d2/htdemucs_6s-core.onnx`・`-mm8.onnx`、`scratch/net/voice/`（6 分割の歌、0.8.0 から）・`voice4/`（4 分割）、`scratch/six/`（`stems/seed1` 正解のパート、`sep/` 分けたもの、`listen/` オーナー用） | `demucs_onnx.py --model htdemucs_6s`、`dettato ears`（環境変数つき）、`scratch/six/stems.py`・`sep_seed1.ps1` |
| `scratch/parts/rwc/`（RWC 100 曲の 6 パート・譜面、`<曲>.parts/*.flac`）、`scratch/parts/sec/`（オーナーの 2 曲） | `scratch/parts_rwc.ps1`（`dettato <wav> -o … --models scratch\d2`、1 曲 40〜60 秒） |
| `scratch/real/beat_this_annotations/`、`scratch/btc/`・`scratch/chordmini/`（clone）、`.venv-bt/`、`.venv-crema/` | README |
| `scratch/real/beats-bt-folds/*.beats`、`scratch/beats-bt/<セット>/`（合成曲 9 フォルダを `final0` で）、`scratch/sec/*.beats`。0.8.4 から 3 列目つき（無ければ `pair_runs` は働かない） | `track_beats.py`（README。全部で 15 分、`scratch/bt3/regen.ps1`） |
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
- **6 パート:** ギターとピアノの混ざりを int8 をやめて減らす（fp32 で同じ）、guitar・piano（・other）を振幅の比で振り分け直す（指数 1・2・4 で変わらない）。
- **意味で切る section:** 「別の所で前の小節が違って始まる」所を 1 か所でもあれば切る（AIST 65.8% だが My Life の歌を 4 小節ごとに割る）。歌の終わりで、最後の小節の後半が歌っていなければ線を 1 小節前に（効かない）。
  句を秒で決める（10・11・15 秒。糸と RWC を両立する幅がない）。8 小節ごとに無条件で切る（dev 66.9% だが My Life の verse を割る）。
  歌の割合が次の句で下がる所でも切る（`SUNG_DROP` 0.15: dev +0.3、test 62.0 → 61.4%。test を見てから捨てた）。
  turn の判定（2026-11-17）: 調の turn をやめる（dev 67.0% だが My Life の 6 つを落とす）、`KEY_REGION` 6（dev 67.2%、声なし 50.7 → 49.8%）、同じ調に挟まれた 6 小節未満の調域を数えない（`KEY_DETOUR` 6: dev 66.6 → 67.1%・声なし 50.9%、test 61.9 → 61.8%。7 で声なしの糸、8 で My Life が崩れる）、`LOUD_TURN` 4.5・5・8・なし（dev 66.5・66.6・66.3・66.5%）。ドラムと旋律（2026-11-17、パートから）: 格子点で直前の小節のドラムが前の 4 小節と違う（フィル、16 刻み×3 帯域の余弦）所でも切る＝dev 50 曲で 0.7・0.8・0.9 が 66.2・66.7・66.5%（8 小節前と同じ句で始まる所は除いても My Life の 61・73 を割る。半分の曲では +0.4 に見えた）。格子の外の小節頭のクラッシュ（4 kHz 以上）・グルーヴの変化も、既存の線から 2 小節より遠い所で落とした境目に当たるのは 33〜38%（手がかりなしで約 23%）＝F の半分と同じで足しても動かない。歌の音域（yin の中央値の 4 小節の差）は格子点で境目とそれ以外が同じ分布。`VOICE_SHARE`・`VOICE_GAP` を動かす（オーナーの 2 曲が崩れる）。
  held chord の切れ目の移し方（2026-11-18）: 3・4 小節まで（dev 66.1・65.4%）、主和音でなく「和音が変わる所」へ・主和音を保つ所も（65.6%）、格子を数え直さない（66.3%）。短い section を前に入れるのを 3 小節未満・4 小節未満に（66.5%・65.8%、My Life の 151 と Open Arms の 59 を落とす）。
  歌が始まる所から 8 小節の中の turn を全部捨てる（2026-11-18、My Life に 26・48・100・138 が増える＝136 の riff の turn）、歌が止む所からも同じ扱い（糸の 32 を落とす）。
  塊ごとの格子の位相を「格子点から始まる句がほかでも同じ頭で始まる」数で選ぶ（dev 62.1〜63.7%）。歌の端を、1 小節前から始まる句がほかでも同じ頭で始まるなら 1 小節前に（66.0%）。曲全体で 4 小節の位相を揃える（AIST 自身が 1 つの位相に乗るのは中央値 52%）。
  歌の分離を重なりなしで（3 分の 1 速いが 15/100 曲の section が変わり、合っていた線を失う。オーナー決定）。荒く読んで出入りの周りだけ詰める（区切り 88%＝12% 減るだけで 5/100 曲が変わる。荒い読みが出入りを丸ごと見落とす。`scratch/sec/coarse_fine.py`、近似）。
  xsc の dev で効かない規則の定数（2026-11-19、exact F 34.1% に対して）: `PHRASE_BARS` 8（31.8%）、`BEGIN_SHARE` 0、`PAIR_HEARD` 0、`WHOLE_BLOCK` 4、`LOUD_TURN` 9、`VOICE_GAP` 5、`VOICE_RUN` 4、`TURN_NEAR` 3、`SUNG_WHOLE` 4、`MERGE_SHORT` 6（32.7%）。DP で歌の出入りを固定する（42.4%）・規則の最後の切れ目を手がかりに足す（44.6%）は My Life を戻さない。
  DP に足して効かない（5 分割×2、57.6% に対して）: 小節の 4 分の 1 ごとの歌の割合と弱起・尾の手がかり（`VQ=1`・`2`: 57.5・57.2%）、半分読みの曲で長さの分布を 2 倍に（正解の向きを教えても 57.8%、DP の目的関数で選ぶ `TAUS` 57.4%＝長さの分布ではなく手がかりが弱い）。
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

- **`x.dettato.json` を人が直してよい形式 2 にすること**（オーナーは「人の小節線が正」に同意。形式の中身と、Lily# と Dettato の書き分けは依頼 4 の返事待ち）。
- **和音のないイントロ・弱起を譜面に書くか。** いまは書かない（2026-11-17 に尋ねて、オーナーは「今は書かない」側を選んだ）。書くなら N.C. の小節として。`rwc-dev` で落としている和音なしの小節は 14。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）。** 0.5.0（fold なし）で `rwc-test` は「印 28.1% で 72.0%」、`rwc-dev` は 34.9% で 86.0%。印の式（`SURE_OF`）は crema 1 つで選んだまま。選び直すかは未決。閾値は 0.84。
- **値段・印の式・`SAID_AGAIN`・C1 の定数・HPSS なしは `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習では
  ないつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P と違うなら、その種類の曲を数曲（Chord Tracker の結果があればそれも）。名前の重複の確認、push（ROADMAP §5）。
