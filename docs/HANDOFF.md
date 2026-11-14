# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)、録音の出典は [DATASETS.md](DATASETS.md)。

最終更新: 2026-11-14（規則を .NET に移したセッションの終わり。HEAD は `git log -1` で確かめる）。

## 現在地

**移植（ROADMAP の D2 の 4 段）は終わった。`dettato read <wav> --out <フォルダ>` が wav から `.lys` と `.dettato.json` を書く**
（Python なし。RWC_P001 で 5.5 秒）。配る形は .NET＋ONNX の実行ファイルを VS Code 拡張に入れる（Calamo と同じ形。ユーザーの答えは
「VS code extension」の一言で、.NET と読んだのは Claude）。Python の読み取り器は変えていない（`measure.py` に `--read` と `check` を足しただけ）。

### .NET の規則（`Reader.cs` 532 行・`Form.cs` 213・`Writer.cs` 145・`Np.cs` 105。README の「In .NET: the rules」。全体で 1894 行）

- 移したのは `--ears` の道だけ（`read()`・`decode`・`fold`・`find_form`・`find_key`・`lys()`）。自作の拍追跡・`MODEL`・`--tempo`・`why` は Python に残す。
- **`.dettato.json` は一字一句、`.lys` は 1 行目（プログラム名）以外、268 曲すべてで Python と同じ**（seed1・7・11・21・31・41、
  `head/seed141`、RWC 100）。`tools/read_check.py <Python のフォルダ> <.NET のフォルダ>` が比べる。`measure.py … --ears --read scratch/net/read`
  は .NET の譜面を数える（`table`・`doubt`・`form`・`head` が同じ）。`measure.py check <フォルダ>` で lysc check 268/268、sidecar 268/268。
- 比べ方: 両方に同じ拍と確率（`--lines`・`--heard`。RWC は fold のモデルの拍）と同じ特徴量（.NET のもの。Python は `--net scratch/net`）。
  モデルを中で走らせた `dettato read`（`--lines` なし）は、`dettato ears` のファイル経由と seed1 で 24/24 同じ。
- **一致させるために真似たこと（`Np.cs`）:** numpy の和の順（8 個未満は順に、128 個までは 8 本並べて、それ以上は半分ずつ）、
  特徴量の平均とノルムは単精度のまま、Python の `//` と `round(x, 桁)`（.NET では `ToString("F桁")` を読み直す）、`repr`。
  同点は numpy の argmax と同じ「最初の最大」。`argsort` の同点（経路探索の上位 2 つ）は「後ろの添字」にしたが、差が出る曲はなかった。
- **規則を変えるときの手順:** Python で試して測る → .NET に写す → `read_check.py` で全セット一致を確かめる（Python が正）。

### .NET の耳と特徴量（`dettato ears`・`dettato features`。README の「In .NET」の最初の 2 節と「Without torch and TensorFlow」）

- 耳: wav → `.beats`（Beat This!）と `.npz`（crema の確率）。CQT は librosa と 1e-5 dB 以内、crema の最有力のコードは RWC 260678 フレーム中
  260677 で同じ、拍は seed1 で 2970 個中 1 個違う（再標本化の通過帯域を soxr HQ に合わせた。標本は同じではない）。突き合わせは `tools/net_check.py`。
- 特徴量: wav → `.feat.npz`（`oenv`・`chroma`・`bass`・`rms`・`tuning`）。`measure.py --net scratch/net` が librosa の代わりに読み、表は全列同じ。
  調律は合成曲 72/72、RWC 99/100 で librosa と同じ（`RWC_P059` だけ +0.01。この曲のクロマが最大 0.04 ずれる）。
- **librosa のベース用クロマは、曲によって他より 1 フレーム長い**（1/32 に間引くときの切り上げ）。.NET も同じ長さを書き、規則は配列ごとの
  長さで切る。デジタル無音（rms が 0）のフレームのクロマは丸め誤差の正規化で、librosa とも一致しない（比べるときは除く）。
- **`--ears` では HPSS を外した**（`use_ears()` が `HARMONIC_FRAMES = 0`。特徴量は `scratch/feat-h0/`）。自作の読み取り器は 31 のまま。
- **1 曲（RWC_P001、3 分 27 秒）の時間:** Python の venv 3 つで 18 秒、venv 1 つ＋ONNX で 11 秒、`dettato read` で 5.5 秒
  （Beat This! 3.7、CQT と crema 1.0、特徴量 0.3、起動 0.3、規則 0.1）。モデルは crema 2.2 MB、Beat This! `final0` 82.9 MB、ほかに 11.5 MB。
- **onnxruntime のスレッド数は 8 に絞る。** 既定（24 全部）だと Beat This! が 1 曲 10 秒に落ちる。torch は 2.0〜2.4 秒で、ONNX より速い。

### 数字（`measure.py all`、2026-11-14 の最後。lysc check 1388/1388、sidecar 1388/1388）

| `--ears`（crema） | `rwc-dev`（選ぶ側） | `rwc-test`（数えるだけ） | `rwc-p` | seed1 / 7 / 11 |
|---|---|---|---|---|
| ルート | 86.4% | 87.3% | 86.8% | 95.1 / 93.1 / 94.5% |
| ルート＋長短 | 82.3% | 82.1% | 82.2% | 91.6 / 88.4 / 92.4% |
| ベース音まで | 64.3% | 65.0% | 64.6% | 73.4 / 58.5 / 73.6% |
| 小節線 F | 93.0% | 93.9% | 93.4% | 96.6 / 90.7 / 94.0% |
| テンポ・拍子 | 46・49 | 47・49 | 93・98 | |
| 印 → 拾う誤り | 30.4% → 77.8% | 25.9% → 71.0% | 28.2% → 74.4% | 17.3% → 69.9% |

- 自作の読み取り器（モデルあり）は変えていない: seed1 / 7 / 11 のルート 95.6 / 96.0 / 97.2%、小節線 95.0 / 97.6 / 99.1%。
- 頭と終わり（`measure.py head`）は前回と同じ: `rwc-dev`・`rwc-test` の `--ears` で最初の小節線 39/50・45/50、最後 24/50・13/50。

### 持ち越す知見（README にないもの）

- `.lys` の小節と録音の対応は「順番」（`bars[]` の n 番目＝Lily# が form を通して鳴らす n 番目。SIDECAR.md）。
  Lily# 側に「原音を鳴らす」設計はまだない。使えるのは `lilysharp/playback` の `S`・`T` と `Beats` の `A`。
- 実録音で失っていたのは頭ではなく終わり（Beat This! はドラムが止まる所で拍をやめる）。crema の「コードなし」は両端だけ使う。
  RWC の正解は両端を語らない（弱起なし）。弱起は `"bar": 0, "pickup": 拍数`、section は `Pickup`。
- ONNX を書き直すとき: 旧式のトレースで書いた直後の torch のモデルは同じプロセスの中で答えが変わる（比べる前に読み直す。
  `bt_onnx.py` はそうしている）。dynamo の書き出しは絵文字を出すので `PYTHONUTF8=1` が要る。

## 次の一手

**D3: `dettato read` を、ユーザーの曲に掛けられるコマンドにする**（mp3 などの入力、コマンドの体裁、エラーの言い方）。
ユーザー決定（2026-11-14）は「実際の曲で試すより先に、移植を完了させる」で、移植は終わった。試すのに足りないのは入力だけ。

- 入力: いまは wav（`Wav.cs`）だけ。mp3・m4a・flac をどう読むかを最初に決める（候補は着手時に調べる。未調査: .NET だけで
  読めるデコーダとそのライセンス、Windows の Media Foundation、ffmpeg を呼ぶ形。Calamo が画像をどう読んでいるかも先例）。
  44100 Hz 以外の録音（48000 など）は `Resample.To` が任意の比を取るが、1 曲も試していない。mp3 は頭に無音が足されることがあるので、
  同じ曲の wav と小節の時刻を比べる。
- 体裁: `dettato <録音> [-o <.lys>]` で `.lys` と `.dettato.json` を録音の隣に書く、が Claude の案（Calamo の引数を先に見る）。
  モデルは実行ファイルの隣（`--models`）。進み具合と失敗は stderr、終了コード。`read`・`ears`・`features` は測定用に残す。
- 終わり: ユーザーの mp3 を 1 つ渡して `.lys` が開ける。合成曲 1 セットを mp3 にして読み、wav の読みとの差を README に書く。
- その次: ユーザーの曲で試す（コードの分かっている曲があれば数字も）→ D4（VS Code から呼ぶ。下の「未確認」の 2 点が最初）。

ほかにできる一手: C3（テンポの大きな変化）。規則を触るなら「.NET の規則」の手順で。

やり残し（急がない）:
- **Beat This! の小さい版（`small0`、8 MB）は測れていない**（配布元 cloud.cp.jku.at が 500）。取れたら `track_beats.py --model small0` で
  合成曲、RWC は `final0` と相対比較。時間と大きさの大半が Beat This! なので効く。半精度（fp16）も測っていない。
- 和音のないイントロの小節（`rwc-dev` の P027）は譜面に入れていない。`fold`・`beat_names`・`lys()` が「和音のない拍」を扱う必要がある（Lily# は `r` で N.C.）。
- 最後の和音がどこまで伸びるか、小節の中の拍の時刻は `.dettato.json` にない（足すなら形式の番号は上げない）。
  `measure.py` の `check` を `lysc check --batch` にすれば速くなる。1〜2 小節の section を減らす手は試していない。
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

- **.NET で確かめていないこと:** `RWC_P059` の調律が 0.01 ずれる理由（再標本化か単精度の FFT か、切り分けていない。表には出ない）。
  44100 Hz 以外の録音（手元の録音は全部 44100）。RWC を wav から通しで読んだ数字（`final0` は RWC を学習済みなので数えない。
  fold のモデルは ONNX にしていない）。規則の一致は「同じ拍・確率・特徴量を与えたとき」で、通しの経路は seed1 の 24 曲だけ。
  拍子が 5/4・7/4 などの曲、`dettato read` が失敗する曲（268 曲に 1 つもなかった。例外の種類を絞って握っている）。
  メモリ（100 曲を 1 プロセスで読むと最大 2.4 GB）。wav 以外の入力。別の CPU での速さ（この PC は 24 コア）。`small0` と半精度。
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

- **配る形（D2）の読みの確認。** 「VS code extension」を「.NET の実行ファイルを拡張に入れる」と読んで進めている。
  違う意味（拡張の中の TypeScript、Python のまま拡張から呼ぶ、D4 を先に、など）なら言ってもらう。.NET の耳と特徴量は Python からも使える。
- **Dettato の拡張を Lily# と別にするか。** Calamo は別の拡張（`yotsuda.calamo`）で、Lily# が場所を尋ねて呼ぶ。モデルが 85 MB あるので
  同じ形が自然だが、決めるのは D4。規則は Python と .NET の 2 か所にある（測るのは Python、配るのは .NET）。
- **和音のないイントロ・弱起を譜面に書くか。** いまは書かない。書くなら N.C. の小節として。`rwc-dev` で落としている和音なしの小節は 14。
- **M2 の印の目安（誤りの 8 割・印 3 割以下）をどうするか。** crema で `rwc-test` は「印 26% で 71%」。
  (a) 目安を下げる、(b) 印を 4 割近くまで増やす、(c) BTC を 2 つ目の耳にする。決まるまで閾値は 0.84。
- **BTC を 2 つ目の耳として配るか。** 入れると `rwc-test` でルート 87.3% → 88.7%・長短 82.1% → 83.8%。
  条件は BTC の作者が重みの扱いを明言すること。jayg996/BTC-ISMIR19 に issue で尋ねるのはユーザー（尋ねていない）。
- **section の書き方。** 細切れ（1 曲 10 セクション）が読みにくければ、条件を緩めるか、section を書かない指定を足す。
- **値段・印の式・`SAID_AGAIN`・C1 の定数・HPSS なしは `rwc-dev`（CC BY-NC 4.0）で選んだ。** 少数の定数の選択で、モデルの学習では
  ないつもりだが、「RWC を学習に使わない」の線のどちら側と見るかはユーザーの判断。
- 使いたい曲の種類が RWC-P と違うなら、その種類の曲を数曲（Chord Tracker の結果があればそれも）。名前の重複の確認、push（ROADMAP §5）。
