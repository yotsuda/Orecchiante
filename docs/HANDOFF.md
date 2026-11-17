# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-17 夜（**0.8.6: 迷った和音を `F@or(Bm)` で書く（Lily# 依頼 17、`@todo` は出さない）、孤立した 3 拍の小節に `time 3/4`（オーナー決定）** `THREE_FOUR`・`short_meters`／`ShortMeters`。数字は動かない。`lysc check` は Lily# 依頼 18 の偽の警告で 1387/1388。オーナーは 0.8.5 の糸の 2/4 を聴いて「自然に正しく鳴っている」、終わりの rit のずれは許容。RWC-P に「途中で止まる曲」はない（`scratch/c3/stops.py`）。Open Arms（Journey、オーナーの 3 拍子の曲）は曲全体を 3/4・102 で読む（`scratch/c3/arms/`、オーナーの耳での確かめ待ち）。**0.8.5:** `time 2/4`。**0.8.4:** 2 拍の小節が続く所を downbeat の値で組む。0.8.3: section のテンポ。0.8.2: 格子の起点。0.8.1: 歌の中の境目。0.8.0: 6 パート。0.7.0: パート。0.6.0: 歌。0.5.0: 意味で切る section。経緯は `git log`）。

### D4 の形（2026-11-15、オーナーと話して決めた。依頼 4・[便り](repro/lilysharp-requests-2026-11-15-listening.md)）
**Lily# の設計メモ `C:\MyProj\LilySharp\docs\recording-design.md`（セッションの頭に読む）:** 紐づけは隣のファイル（形式 2）だけ、m4a は Lily# の LSP が復号。
**目標は Transcribe! と同じ体験を Lily# で**（耳コピの道具。**特定の楽器に寄せない**＝オーナー 2026-11-17「ベースは私が趣味で弾いているだけ。ユーザーはいろんな楽器を使う」。オーナーの曲をベースで試すのは一例）。**人が打った小節線が正**（形式 2）。Dettato は `tuning`・`beats` を入れた（読み直しの入口は不要）。

## 現在地

**`dettato <録音> [-o <x.lys>] [--force]` が、mp3・m4a・mp4・aac・flac・wma・wav から `.lys` と `.dettato.json` を録音の隣に書く**
（Python なし。RWC_P001 の mp3 で 6.3 秒）。配る形は .NET＋ONNX の実行ファイルを VS Code 拡張に入れる（Calamo と同じ形）。
**拡張 `yotsuda.dettato` 0.8.7 をこの PC の VS Code に入れた**（0.8.7: 譜面の頭のコメントが `@or` の上での Shift+Ctrl+Up/Down も言う。0.8.6: `@or`・`time 3/4`。0.8.5: `time 2/4`。入った版で糸を通しで読み、scratch の exe と `.lys` が一字一句同じ（`scratch/c3/ito/`）。0.8.4: 2 拍の小節の組み方。0.8.3: section のテンポ。0.6.0: 歌を聞いて section を切る。0.7.0: パートを書く。0.8.0: 6 パート。0.8.1: 歌の中の境目。0.8.2: 格子の起点。入った版の通しで糸・My Life の section は前と同じ）（`extension/`、`tools/deploy-extension.ps1`。.vsix 167 MB、Lily# とは別の拡張）。**コマンドはない**: メニューは Lily# の
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
- **長い録音: 58 分（48 kHz、バンド練習の mp3）は読める**（パートつきで 694 秒、README）。残る大きいメモリ: `Features.Of` の +1.2 GB、Beat This! の段の +1.2 GB（未調査）。
- seed1 を mp3・m4a にすると精度は wav と同じ、譜面は小節の数が違う曲が 3・5 曲。測る道具: `tools/heard_check.py`（頭のずれ）、`tools/kinds_check.py`。ffmpeg は PATH にある。

### .NET の規則（`Reader.cs` 726 行・`Form.cs` 209＝`Turns`・`Phrases`・`Writer.cs` 251。README の「In .NET: the rules」。全体で 2776 行）

- 移したのは `--ears` の道だけ。自作の拍追跡・`MODEL`・`--tempo`・`why` は Python に残す。
- **`.dettato.json` は一字一句、`.lys` は 1 行目以外、268 曲すべてで Python と同じ**（`tools/read_check.py`。比べ方とコマンドは README）。
  同じ拍と確率（`--lines`・`--heard`）と同じ特徴量（`--net scratch/net`）を与えて比べる。`measure.py … --read` は .NET の譜面を数える。
- 一致させるために真似たこと（`Np.cs`）: numpy の和の順、単精度のままの平均とノルム、Python の `//` と `round`、`repr`、同点の扱い。
- **規則を変えるときの手順:** Python で試して測る → .NET に写す → `read_check.py` で全セット一致を確かめる（Python が正）。

### .NET の耳と特徴量（`dettato ears`・`dettato features`。README の「In .NET」の最初の 2 節と「Without torch and TensorFlow」）

- 耳: wav → `.beats`（Beat This!）と `.npz`（crema と BTC の平均、0.4.0 から）、特徴量: wav → `.feat.npz`。突き合わせは `tools/net_check.py`、`measure.py --net scratch/net` で表は全列同じ。
- **`--ears` では HPSS を外した**（`use_ears()` が `HARMONIC_FRAMES = 0`。特徴量は `scratch/feat-h0/`）。1 曲 3 分半で `dettato read` 5.5 秒（Beat This! 3.7）。onnxruntime は 8 スレッド（24 だと遅い）。

### 数字（`measure.py all`、2026-11-17 の 0.8.6 の後＝0.8.5 と一字一句同じ。lysc check 1387/1388（依頼 18）、SIDECAR 1388/1388。ログは `scratch/all.log`、前は `all085.log`）

| `--ears`（crema＋BTC） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 87.4% | 88.9% | 88.2% | 95.5 / 94.4 / 96.7% |
| ルート＋長短 | 82.9% | 83.9% | 83.4% | 92.4 / 90.9 / 94.3% |
| ベース音まで | 65.1% | 66.6% | 65.9% | 76.9 / 65.1 / 80.0% |
| 小節線 F | 94.1% | 95.1% | 94.6% | 97.1 / 91.0 / 94.1% |
| テンポ・拍子 | 47・49 | 47・49 | 94・98 | |
| 印 → 拾う誤り | 34.9% → 85.9% | 28.1% → 72.0% | 31.5% → 78.9% | 14.9% → 74.0% |
| section（1 曲あたり、AIST の F は声なし／歌つき） | 14.3、50.7／66.3% | 13.8、49.7／61.9% | 14.0 | 4.0 / 4.1 / 3.0 |

### 持ち越す知見（README にないもの）
- 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。RWC の正解は両端を語らない。ONNX を書き直すとき: 旧式のトレース直後の torch のモデルは同じプロセスで答えが変わる（比べる前に読み直す）。dynamo は `PYTHONUTF8=1` が要る。

## 次の一手

**section（意味で切る。0.5.0 で既定、0.6.0 で歌も .NET に。オーナー: 調や曲調が変わる所・歌が入る所・ソロで切りたい）**
- オーナーが手で分けた 2 曲: 糸（`scratch/ito/ito-one.lys`: 1・9・17・21・28・32・40・44・51・59）、My Life（`scratch/sec/phrase/mylife-owner.lys`: 1・9・17・31・39・53・69・83・91・105・121・129・135・151）。
  **0.6.0（.NET の歌）で 2 曲とも全部、余分なしで出す**（糸は終わり 67 が増えるだけ。入った拡張の通しの読みでも同じ）。声なしだと糸は 9 つ中 5 つを落とす。
  確かめは `scratch/sec/try.py [定数=値 …]`（`VOICEDIR=scratch/net/voice/sec` で .NET の歌、`NOVOICE=1` で声なし）と `tools/aist_sections.py rwc-dev`（既定で .NET の歌）。**規則を触ったら必ず両方を見る**。
- 仕組み（README「Sections by meaning」「The voice in .NET」）: 歌の出入り・調号 2 つ以上・6 dB で塊に切り、丸ごと別の所で鳴る塊の頭と終わり、長い塊の中は 4 小節の句（`PHRASE_BARS`）の格子で
  「同じ句 2 回の終わり」「V→I」「別の所でも始まる句（`BEGIN_SHARE`）」「8 小節ごとの新しい組（`PAIR_HEARD`、0.8.1）」で切る。名前は再演なら A2。fold はしない。.NET は `Form.Turns`・`Form.Phrases`・`Form.VoiceTurns`。
- **歌の中の境目（0.8.1 `PAIR_HEARD`）と格子の起点（0.8.2 `GRID_ANCHOR`）:** README「The grid and the turns」。dev 64.4 → 66.6%、test 60.5 → 61.9%（0.8.2 は test で効いていない）。
  道具: `scratch/sec/vab.py`・`grid_cues.py`・`grid_rules.py`・`eight.py`・`sweep.ps1 '<定数=値 …>'`（dev の F＋オーナーの 2 曲）・`song_phase.py`・`phase_look.py`・`owner_edges.py`。名前の文字に R はない（`R2` は Lily# で全休符）。
- **歌（0.6.0、0.8.0 から 6 分割の歌）:** `Voice.cs` が Demucs（`htdemucs_6s`、MIT）の ONNX（`tools/d2/demucs_onnx.py --model htdemucs_6s`。STFT と逆 STFT は外、行列積だけ int8＝64 MB）を 7.8 秒ずつ、ずらしなし・モノラルを両チャンネルで回す。
  AIST 3 秒以内の F: dev 64.4%・test 60.5%（4 分割は 64.3・60.1）。**歌は `scratch/net/voice/`（6 分割。RWC 100・seed1 の 7・sec）、4 分割のものは `voice4/`。** 歌のない曲は `VOICE_FLOOR`（0.05）で歌なし（seed1 の 7 曲で確かめ直した）。**Python と .NET は同じ `.voc.npy` で RWC 100 曲が一字一句同じ**（`dettato read … --voice <folder>`、合成曲は `--voice none`）。
- **時間とメモリ（0.6.1）:** 通しで RWC_P001 33〜45 秒（Calamo の負荷しだい）、ピーク 8.6 GB。並行は 3（`Voice.Parallel`）。結果を変えずに速くする手は見つからなかった（README）。
  重なりなしは捨てた（オーナー決定）。section の頭の比べ方は `scratch/sec/ov_cmp.py <A の譜面> <B の譜面>`（最寄りの AIST つき）。
- **パート（0.7.0、オーナー「レコーディングのオンオフをパートごとに。耳コピにとても便利」。README「The parts」）:** 変換のたびに `<名前>.parts/*.flac`（モノラル 16 bit 44100 Hz、録音の秒の上）と `x.dettato.json` の `parts`（SIDECAR.md）。
  確かめは `scratch/sec/parts_check.py <録音> <x.parts>`（和・ずれ）。**Lily# 依頼 16 は fixed**（パートの行は `parts` の数と順で出る。全部オンなら元の録音、1 本でもオフなら残りの和）。ステレオのパートは未。
- **6 パート（0.8.0、オーナー決定 2026-11-17。README「Six parts」）:** drums・bass・other・vocals・guitar・piano（`DETTATO_VOICE_MODEL=htdemucs-mm8.onnx` で 4 に戻せる）。6 本が落とす音は `other` に足して和を録音に戻した。
  アコギとピアノは割れる（オーナー「若干ギターとピアノがうまく分離できていない」）。int8 のせいでも振り分けでもない。5 パートと聴き比べて **6 のまま（オーナー決定）**。合成曲の割合は参考にしない（GM の音、オーナー）。
  残る手（今はやらない）: 別のモデル（BS-RoFormer の 6 パート版 `bs_6stem_fixed`＝music-to-midi が使うもの。**重みに許諾なし**（配布元にライセンスの記載がない、同プロジェクトの THIRD_PARTY_NOTICES）＝配れない）、MoisesDB で実録音を測る（登録が要る）。道具は `scratch/six/`（README）。

- **次:**
  (1) **歌の中の境目は一区切り**（0.8.1・0.8.2 で dev 64.4 → 66.6%、test 60.5 → 61.9%）。2026-11-17 に turn の判定（`scratch/sec/turn_kinds.py`）、ドラムのフィル・クラッシュ・歌の音域（`drum_cues.py`・`drum_rules.py`・`owner_fills.py`）を試して、どれも効かなかった（捨てた案）。
  RWC のパートは `scratch/parts/rwc/<曲>.parts/`、オーナーの 2 曲は `scratch/parts/sec/`（新しい手がかりを試すときの材料）。調の偽物は 4〜5 小節の行って戻る調域に偏る（P023・P019）。
  (2) **C3（テンポの大きな変化）は 0.8.3 で最初の手を入れた**（README「A section's own tempo」、SIDECAR の小節の `tempo`）: 曲順に section を歩き、
  section の小節の平均が今のテンポから 6% を超えて離れ、6 小節以上、4 分の 3 が中央値の 1 割以内、比が 1.3 未満なら、section の頭に `tempo 4 = x`（Lily# は section の頭のテンポを毎回鳴らし、次まで保つ。紙では曲頭の値は A のもので置き換わって刷られる＝png で確かめた）。頭のテンポに戻る section は 2 小節でよい。
  測り方: `scratch/c3/tempo_try.py <譜面> <truth> --written [-v]`（正解の小節が、そこで立っているテンポの小節と 4% 以内か）。dev 91.2 → 91.4%、test 95.2 → 95.3%（**test の曲ごとの行を見た**）、`drift5/seed31` 66.9 → 70.2%、ほかの合成曲は印 0。
  条件なしだと印はすべて小節の読み違い（半分・倍・3/2）だった。P038 の最後の 140.2 は誤り（真は 111、小節を短く読んでいる）。**バンド練習のような録音は考えない（オーナー決定 2026-11-17「練習時の録音を譜面に落とすことはほとんどない」）**＝曲間の話し声・無音（Beat This! が拍を打たず 91〜112 秒の 1 小節になる）は扱わない。`scratch/c3/long/` は参考に残す。
  `still/`・`drift5/` の耳（`scratch/beats-bt/<still|drift5>/seed31`・`chords-crema+btc/…`、歌は `scratch/net/<still|drift5>-seed31`）を作った。Beat This! は 5% 揺れでも小節線 92.8%（自作の追跡は 80.9%）＝小節線はもう問題でない。
  残り: rit・フェルマータ（曲の終わりの数小節。section より細かい。**曲の終わりは 2026-11-17 に測った**: `scratch/c3/tail.py <譜面のフォルダ> [-v]`＝最後の小節の頭が正解の最後の小節の頭か、その終わり（最後の一撃）に 0.3 秒以内か。dev 25/50・test 17/50。曲ごとは `ends.py`、手がかりは `endfeat.py`。規則は捨てた）、途中で止まる曲（**RWC-P には測る材料がない（2026-11-17、`scratch/c3/stops.py <譜面> 1 100 [-v]`）**: 正解の中に N.C. も小節の隙間もなく、前後の 1.35 倍を超える小節は 100 曲で P034 の 1 つだけ。拍子の違う小節は P006・P022・P037・P038・P043（3/4・5/4・6/4）にある。P038 の小節線 67/138 は拍子ではなく、線が正解より 0.1〜0.26 秒早い区間が多いため＝許容 0.07 秒の外）、範囲のテンポ・人の拍マーカー（Lily# の依頼 4）。(3) 速くする手が要るなら: 重なりを 4 分の 1 より小さく（8 分の 1 など、同じ道具で測る。未測定）、空いた PC で測り直す、蒸留した小さなモデル（学習の音源はオーナーが決める）。
- **糸の M64（6 拍＝4＋2）は 0.8.4 で入れた**（README「A bar of two beats」）: `.beats` の 3 列目＝拍の ±2 フレームの downbeat の値の最大（`track_beats.py`・`Beats.cs`、.NET と torch の差 0.04 以内）。
  `pair_runs`／`PairRuns` が `MEND_BARS` の前に、2 拍の小節が続く所を「残す線の値 − 落とす線の値」が最大になるよう組む。1 つ残す 2 拍の小節は値段 12、拍の最初・最後の線では無料。
  糸は 253.96 秒から正しい（`G | C#m7(b5) | C Dsus4 | C D …`）。2 拍の小節はオーナーの位置より 1 小節前（値が 249.96 秒で 3.2、251.94 秒で 2.2）、最後の section の名前が D3 → G（再演と見なされなくなった）。
  **0.8.5 から `time 2/4` を書く（オーナー決定 2026-11-17、前の「書かない」を改めた。README「A bar of 2/4」）**: 前後 4 小節の中央値の 0.4〜0.6 倍で、両隣が 15% 以内、4/4 だけ、弱起・最初・最後でない、section を演奏するたびに同じ小節。次の小節に `time 4/4`（section の最後なら不要＝Lily# は section の終わりで戻る、確かめた）。RWC で 10 曲 14 小節（両端が正解に乗るのは dev 6/10・test 0/8、RWC に本物の 2 拍はほぼない）、合成曲 4 小節。外れても読みの格子では 2 拍の長さ＝録音と同じ速さで鳴り、直す所の印になる。測る道具 `scratch/c3/short_bars.py <譜面の根> <セット> [-iso] [-v]`。**0.8.6 から 3 拍も `time 3/4`（オーナー決定 A、2026-11-17。README「A bar of 3/4」）**: 0.65〜0.85 倍、最後の 2 小節は除く（曲の終わりの rit を短く読んだ小節が 6/7）。RWC に曲の途中の本物の 3/4 はほぼない（P006 だけ読めている）。RWC で 6 曲 7 小節、合成曲 2 曲。道具 `scratch/c3/three_look.py`。依頼 11 は fixed（オーナーの版で確かめたら `done`）、15 は closed。

**オーナーが 0.8.5 の糸を聴いた（2026-11-17、`scratch/c3/ito/ito-ext.lys`）: `time 2/4` の小節（64 小節目、248.04 秒）は「自然に正しく鳴っている」。** 70 小節目から先は録音が大きく rit していて全然合わないが、**「許容しても良い」（オーナー）**＝曲の終わりの rit は急がない。読みは 70 小節目が 3.00 秒（前は 3.8〜4.0）、71・72 が 3.98 秒、録音は 285.02 秒まで＝rit で長くなる小節を短く・少なく読んでいる（捨てた案の C3「曲の終わり」と同じ所）。次にオーナーに聴いてもらうのは、オーナーが譜面にしたい別の曲（練習の録音は対象外＝オーナー決定）。
- **D4 で Dettato 側に作るものは今はない**（読み直しは単に上書き＝オーナー決定、設計メモ §6）。配り直すときは版を 2 か所（`Program.cs`・`extension/package.json`）と CHANGELOG で上げて `tools/deploy-extension.ps1`。
- **Lily# の依頼（`docs/repro/REQUESTS.md`。セッションの頭に見る）:** **17（`@or`）は Lily# `f89b01dcd` で fixed、Dettato は 0.8.6 で `@or(候補)` だけを書く＝変換メニューから実物で確かめたら `done`。18（`time 3/4` の小節の `.` の検査が 4/4 で数える）は `open`。それまで `lysc check` は 1387/1388（P012）。lysc は Lily# の HEAD を `git archive` して `scratch/lysc-f89/` にビルドしたもの（`DETTATO_LYSC` をそこに。古い lysc は `@or` を読めない）。** 1 は `done`。4 は Lily# が作り中（S1〜S3 済み、次は S4）。**16（パートのオン・オフ）は `fixed`＝オーナーの版で確かめてから `done`。** **5（m4a の `iTunSMPB`）は `fixed`＝実物の m4a で確かめてから `done`。** 7（小数のテンポ。Lily# が 6 と書いたのを振り直した）は 0.3.1 で入れた。
  **6（保存先のダイアログ・`audio` の道）・8（`warpFit` を中央値に）・9（`lysc shape`・`LILYSHARP_LYSC_PATH`）は Lily# master で `fixed`、Dettato は 9 を 0.3.2 で使った。Lily# の次の版が入ったら、変換メニューから 3 つとも実物で確かめて `done`**（3・5 も同じく確かめてから）。**この PC の Lily# 拡張（名は 0.16.0、11-17 17:12 に入った master のビルド）には 6・9・11・16 の文字列がある**（`Locate Recording`・`LILYSHARP_LYSC_PATH`・`The score plays`・`"parts"`）＝残るのは変換メニューからの実物の確かめ（VS Code の操作が要る）。

やり残し（急がない）:
- **MuScriptor**（github.com/muscriptor/muscriptor、Kyutai×Mirelo、2026-11-17 にオーナーから）: 多楽器の採譜（MIDI・MusicXML、拍・小節線も。コード名は出さない）、torch の 103M〜1.4B。コードは MIT、**重みは CC BY-NC 4.0（HF でゲート）**。精度・学習データは README になし（arXiv 2607.08168 未読）。M5 の候補、配るならオーナー判断。
  **music-to-midi**（github.com/mason369/music-to-midi、MIT、オーナーから）: 既存モデルの寄せ集め（YourMT3+・MuScriptor・TransKun・Beat This! final0・ChordMini BTC・BS-RoFormer）。重みのライセンス表が役に立つ: **YourMT3+ は Apache-2.0**（M5 の第一候補）、TransKun（ピアノ）MIT、ByteDance のピアノ CC BY 4.0、Aria-AMT は CC BY-NC-SA。GPU 前提。
- 和音のないイントロの小節（`rwc-dev` の P027）は譜面に入れていない。`fold`・`beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# は `r` で N.C.）。最後の和音の終わりは `.dettato.json` にない。`check` は `lysc check --batch` で速くなる。crema の `chord_root`・`chord_bass`・`chord_pitch` は未使用。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は 3 つ＋試し 1 つ。`.venv`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）が読み取りと測定。
  `.venv-bt`（torch 2.15 CPU、beat-this 1.1.0、onnx・onnxruntime・onnxscript）が Beat This!・BTC・ChordMini。`.venv-crema`（tensorflow、tf-keras、crema 0.2.0、`setuptools<81`、tf2onnx、onnxruntime）が crema。
  `scratch/d2/venv-onnx`（numpy、librosa、soxr、soundfile、onnxruntime）が ONNX だけの耳。作り方は README。素の `python` は LilyPond 同梱のものを拾うので使わない。標準ライブラリだけの道具は `py -3.12` でもよい。
- 新しいコンソールごとに最初に設定する（コンソールは 11 分使わないと閉じ、環境変数も消える）:
  `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'`、`$env:DETTATO_LYSC='C:\MyProj\Dettato\scratch\lysc-f89\lysc.exe'`（`@or` を読む lysc。Lily# の次の版が入ったら LilySharp の `LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe` に戻してよい）。`.venv-bt` を動かすときは `$env:TORCH_HOME='C:\MyProj\Dettato\scratch\torch'`、`$env:TORCHINDUCTOR_CACHE_DIR='C:\MyProj\Dettato\scratch\torchinductor'`、`$env:PYTHONUTF8='1'` も。
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
  塊ごとの格子の位相を「格子点から始まる句がほかでも同じ頭で始まる」数で選ぶ（dev 62.1〜63.7%）。歌の端を、1 小節前から始まる句がほかでも同じ頭で始まるなら 1 小節前に（66.0%）。曲全体で 4 小節の位相を揃える（AIST 自身が 1 つの位相に乗るのは中央値 52%）。
  歌の分離を重なりなしで（3 分の 1 速いが 15/100 曲の section が変わり、合っていた線を失う。オーナー決定）。荒く読んで出入りの周りだけ詰める（区切り 88%＝12% 減るだけで 5/100 曲が変わる。荒い読みが出入りを丸ごと見落とす。`scratch/sec/coarse_fine.py`、近似）。
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
