# Orecchiante → Lily#: 直した小節を Orecchiante に報告する（依頼 23）

2026-11-23、Orecchiante のセッションから。オーナーの希望:「Lily#+Calamo がやっているように、小節ごとに正解を直してあなたに
レポートする機能を作ってほしい。ベースにも同じような修正してレポートする機能があれば、正解データ作成が楽になる」。

## 1. 背景: Orecchiante 0.14.0 が書くもの

- 録音にベースが聞こえれば **ベースの段**、ドラムが聞こえれば **ドラムの段** を `.lys` に足す（それぞれ曲全体の音量に対する
  分けたパートの音量で判定。無ければ今までどおりコード譜だけ）:

  ```
  octave absolute
  part bass { instrument bass }
  part kit { clef percussion }
  section A {
    chords prog { … }
    bass { r8 c8 g8 c8 ees8 f8 bes,8 c8~ | … }
    kit {
      voice { hh8 hh8 <sn hh>8 hh8 hh8 hh8 <sn hh>8 hh8 | … }    // 手（スネア・ハイハット）
          { bd4 bd4 bd4 bd4 | … }                             // 足（バスドラム）
    }
  }
  score { chords prog  staff bass  staff kit }
  ```

  section を 2 回以上演奏するときは、`.lys` には 1 回目の演奏の読みを書く（譜面は 1 つしか書けないため）。
- 隣のファイル（`x.orecchiante.json`、SIDECAR.md）の各小節に、**読んだままの中身**を足す（演奏した小節ごと＝2 回目以降の演奏の分も）:
  `"bass": "r8 c8 g8 …"`、`"drums": { "hands": "hh8 …", "feet": "bd4 …" }`。小節の `start`・`end`・`beats`（秒）は今までどおり。

## 2. 依頼: 「Report to Orecchiante…」

Calamo の「Report Misread to OMR」（`omrSeedCore.ts`）と同じ形で、**オーナーだけ**に出す（`settings.json` に手で書いた
`lilysharp.orecchiante.seedFolder` があるときだけ。配る拡張の設定には載せない）。

- **どこで**: 隣のファイルを持つ `.lys` の、`bass`・`kit`・`chords prog` の行の小節の上（カーソルの小節）。
- **何を**: 新しいフォルダ（`<日時>-<曲>-bar<N>-<part>`）に、報告した時点の `.lys` の写し、隣のファイルの写し、`seed.json`。
  `.lys` は後で直し直されたり消されたりするので、Calamo と同じく全部写す。
- **`seed.json`**（形は Lily# に任せる。Orecchiante が要るもの）:

  | 項目 | 中身 |
  |---|---|
  | `part` | `"bass"`・`"kit"`・`"chords"` |
  | `section`・`barOfSection` | その小節の section と、section の中の何小節目か |
  | `bars` | その section の小節を演奏した、隣のファイルの `bar` の番号すべて（1 回目が `.lys` に書いた読み） |
  | `bar` | 報告した時点の小節の中身（直した後）。kit は 2 声とも |
  | `barWhenRead` | 隣のファイルにある、その小節の 1 回目の演奏の読み（`bass` または `drums`）。比べる元 |
  | `note` | オーナーの一言（無ければ `""`） |
  | `recording` | 録音のパス（写さなくてよい。Orecchiante の側で、隣のファイルの写しの `start`・`end` で切り出す） |

- **あると正解づくりが速いもの（オーナーと Lily# の判断）**: 1 小節ずつではなく、**section を丸ごと「確かめた」として報告**する。
  その section の各小節を（直したものも直していないものも）正解として 1 つのフォルダに入れる。直していない小節も正解になるので、
  オーナーが聴いて正しいと思った小節を 1 回で渡せる。

## 3. Orecchiante の側でやること

- 報告のフォルダを集めて、その小節の秒（隣のファイルの `start`・`end`・`beats`）と正しい音を答えにする道具
  （`tools/seeds.py`。ベースは今の物差し `scratch/bass/grid.py` と同じ「小節・16 分・音高・長さ」、ドラムは「小節・16 分・どの太鼓」）。
- 集まった答えで、ドラムの規則（今は答えがなく、生成した曲の GM のドラムで桁を合わせただけ）とベースの規則を選び直す。
