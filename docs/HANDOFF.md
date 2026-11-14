# Dettato 引き継ぎ

**毎セッションの終わりに書き換える**（追記しない。200 行以内）。方向づけは [ROADMAP.md](ROADMAP.md)、
数字と使い方は [README](../README.md)。

最終更新: 2026-11-14（最初のセッション群の終わり。HEAD は `git log -1` で確かめる）。

## 現在地

M0（合成曲でコード譜が読める）まで終わった。実録音は 1 曲も測っていない。

- `tools/gen_chords.py`: seed から曲を作り、Lily# に鳴らさせて正解（`truth.json`）を作る。
  `--held`（2 小節コード）、`--drift`（テンポの揺れ）。
- `tools/read_chords.py`: 読み取り器。`--model` でモデル入り、`--tempo` でテンポを教える。
  `.lys`（疑わしいコードに `@todo`）と `.dettato.json`（小節とコードの時刻）を書く。
- `tools/train_chords.py`: 半拍のクロマ → コードの小さなネットワーク（隠れ層 1 枚）を学習する。
- `tools/score_chords.py`: 読みを正解と突き合わせる。
- `tools/sample_songs.py`: Lily# のサンプル 4 曲を正解つきの曲にする（生成器と無関係な曲）。

モデル入りの数字（各 24 曲。全項目は README）:

| | seed1 | seed7 | seed11 | seed21（2 小節コード） | seed31（テンポ揺れ 2%） |
|---|---|---|---|---|---|
| テンポ | 23 | 23 | 23 | 16（教えると 22） | 24 |
| 小節線 F | 94.7% | 97.4% | 99.1% | 89.2%（教えると 96.5%） | 97.2% |
| ルート | 95.1% | 95.5% | 97.4% | 97.9% | 97.5% |
| ルート＋種類 | 83.1% | 73.8% | 89.9% | 85.0% | 83.4% |

Lily# のサンプル 4 曲: 伴奏のある 2 曲はルート 93%・99%、旋律だけの 2 曲は 9%・39%。

## 次の一手

**ROADMAP の A0（測定の道具を repo に入れる）から。** 続けて A1（正解つきの実録音を集める）。
A1 は Web での調査（データセットのライセンスと中身）が要る。ユーザーが実録音を出せるかも最初に聞く。

## 環境（この PC）

- 作業は `C:\MyProj\Dettato`。シェルは pwsh MCP（`mcp__pwsh__*`）。組み込みの PowerShell は使えない。
- Python は `.venv\Scripts\python.exe`（3.12。librosa 1.0.1、scikit-learn 1.9.1、numpy 2.5.4）。
  標準ライブラリだけの道具（`gen_chords.py`・`score_chords.py`・`sample_songs.py`）は `py -3.12`。
  素の `python` は LilyPond 同梱のものを拾うので使わない。
- lysc: `C:\MyProj\LilySharp\LilySharp.Cli\bin\Release\net10.0\win-x64\lysc.exe`（`DETTATO_LYSC` に入れる）。
- SoundFont: `soundfonts\MuseScore_General.sf2`（学習と測定の基準。`lysc wav` は `.sf2` だけ読む）。
  別の音色の確認用に `C:\MyProj\omr-compare\minor\venvs\zhaovan\Lib\site-packages\pretty_midi\TimGM6mb.sf2`。
- 最初に `$env:NUMBA_CACHE_DIR='C:\MyProj\Dettato\scratch\numba'` を設定する（コンソールに USERNAME がない）。
- 2 分を超える仕事は切り離して走らせ、ログを見に行く:
  `Start-Process cmd -ArgumentList '/d /s /c "<コマンド> < NUL > scratch\x.log 2>&1"' -WindowStyle Hidden`
- `lysc check` の合否は終了コードと `warning` / `: error` で見る（"No errors found" に `error` が含まれる）。
- `C:\MyProj` 全体の再帰検索は固まる。探す場所を絞る。

### commit の外にあるもの（消えたら作り直す）

| もの | 作り方 |
|---|---|
| `data/synth/seed*/…-band.wav` | `gen_chords.py --seed N --count 24 --out data\synth\seedN --soundfont …`（seed21 は `--held 0.5`、seed31 は `--drift 0.02`） |
| `data/synth/seed100`（学習用 200 曲） | `--seed 100 --count 200` |
| `models/chords.joblib` | `train_chords.py data\synth\seed100 --features scratch\feat-h31 --out models\chords.joblib` |
| `scratch/feat-h31/*.npz`（特徴量の置き場） | `scratch\run.py` が初回に作る（1 曲 4 秒ほど） |
| `scratch/alt-tim`（別 SoundFont の seed1・7・11）、`alt-held/seed101`（60 曲、`--held 0.5`）、`alt-still/seed31`、`alt-drift5/seed31`（`--drift 0.05`）、`alt-ood/samples`（`sample_songs.py`） | 同じ生成器で `--out scratch\alt-…\seedN` |

### 測定の実体（まだ `scratch/` にある。A0 で repo に入れる）

- `scratch\run.py seed1,seed7,seed11 [-v] [--model=models\chords.joblib] [--hint] [--write=dir]`:
  読んで採点し、1 セット 1 行で出す。セット名の代わりにフォルダも渡せる。特徴量は `scratch/feat-h31` に置く
  （フォルダ名に `alt` を含む親を持つ曲は、置き場の名前に接頭辞が付く）。
- `scratch\sweep.py <セット> "{'TIGHTNESS':[300,100]}" [モデル]`: 定数を振る。
- `scratch\doubt.py <セット> <モデル> [しきい値の一覧]`: 印が誤りを指しているか。
- `scratch\barlines.py`・`flip.py`・`keys.py`: 小節線の外れ方、拍の追跡のずれ、調の外れ方。

## セットの扱い

| セット | 役割 | 注意 |
|---|---|---|
| seed100 | 学習用。定数を選ぶのにも使う | モデルはこれしか聞いていない |
| `alt-held/seed101` | 定数を選ぶ用（印のしきい値、テンポの事前分布の検討） | |
| seed1 | テスト。初期の定数はここで決めた | 「見ていないセット」ではない |
| seed7・seed11 | テスト | `TIGHTNESS` を 100 に替えるとき数字を見た |
| seed21・seed31 | 2 小節コード、テンポ揺れ | |
| Lily# サンプル 4 曲 | 生成器と無関係な曲 | 「知らない音」のしきい値（半分）はこの 4 曲を見て決めた |

次に厳密な比較が要るときは、新しい seed（例: 41）を作り、決めるまで見ない。

## 捨てた案（試し直さない）

- 小節線を別に探す手法（新奇度の対比、平均クロマの当てはめ）。→ コードと小節線を 1 回の探索で一緒に決める形にした。
- HPSS をやめて生の音を使う、NNLS で倍音を除く、対数圧縮、`CHANGE_GAIN`、ベースの重み 0.6。どれも効かない。
- 規則だけでコードの重みを詰める。ルート 86〜88% で頭打ち。→ 聞き分けを学習に替えた。
- 短調の属和音の重みを下げて調を当てる。読みに対しては悪化。調の外れはほぼ平行調で、正解のコードを与えても 72 曲中 55 曲。
- 小節の長さの事前分布（長い小節に罰）。2 小節コードの曲を救うぶん普通の曲を壊す。→ `--tempo` で人が教える。
- 拍の長さを局所の中央値で見る。普通の曲の小節線が 2〜3 ポイント落ちる。
- 七の和音の迷いまで印にする。4 個に 1 個に付き、重い誤りを拾う効率が落ちる。

## 分かっている弱点

- 実録音を測っていない。モデルは生成器の曲しか聞いていない。
- 伴奏のない旋律だけの曲はコードを読めない（和声づけは別の課題）。
- 知らない音では、ほぼ全部のコードに印が付く（印が絞り込みにならない）。
- 2 小節続くコードの速い曲を半分のテンポに読む（`--tempo` で直る）。調は 3 分の 1 が平行調に外れる。
- 小節ごと 5% のテンポの揺れで小節線が崩れ始める。止まって再開する曲は未対応。
- 曲の頭の無音・弱起を想定していない。繰り返しをまとめない（コード行が全小節ぶん並ぶ）。
- seed1 の小節線が `TIGHTNESS` 100 で 97.7 → 94.7% に落ちた。どの曲かは見ていない。

## 未決（ユーザーの判断待ち）

ROADMAP §5 のとおり: 実録音をもらえるか、配る形（Python か .NET か）、名前の重複の確認、リモートへの push。
