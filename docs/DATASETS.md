# 正解つきの録音（出典とライセンス）

Dettato を測るのに使う、コードと拍の注釈がある録音の一覧。**音源も、注釈から作った `truth.json` も
commit しない**（どれも他者の著作物、またはその派生物）。commit するのは変換の道具とこの一覧だけ。

調べた日: 2026-11-14。ライセンスは配布元のページで確かめたもの。使う前に配布元をもう一度見ること。

## 使っているもの

### RWC 研究用音楽データベース: ポピュラー音楽（RWC-MDB-P-2001）

| | |
|---|---|
| 中身 | スタジオで録音されたポップス 100 曲（日本語 80、英語 20）。歌・バンド・ミックスあり。合計 6 時間 43 分 |
| 音源 | <https://zenodo.org/records/18656623> の `RWC-P.zip`（3.79 GB、WAV、`RWC_P001.wav`〜）。2026-02 に公開された再配布版 |
| 音源のライセンス | CC BY-NC 4.0（Zenodo の表示: `cc-by-nc-4.0`） |
| 注釈 | <https://github.com/rwc-music/rwc-annotations>（CC BY-NC 4.0）。`01_annotations_preprocessed/beats/RWC-P/*.csv`（拍の時刻と小節内の番号。1 が小節の頭）、`chords/RWC-P/*.csv`（開始・終了・Harte 記法のコード。Cho と Bello の注釈が元） |
| ないもの | 調の注釈（`truth.json` の `key` は null。調は数えない） |
| 出典 | Goto ほか "RWC Music Database" (ISMIR 2002)、Balke ほか "RWC Revisited" (TISMIR 9(1), 2026, doi:10.5334/tismir.326) |

非営利の条件つき。**測るためだけに使い、音源・注釈・それらから作ったものを配らない。**
学習に使うかどうかは別に決める（学習したモデルを配るなら NC の条件を確かめる必要がある。未確認）。

作り方:

```
git clone --depth 1 https://github.com/rwc-music/rwc-annotations scratch/real/rwc-annotations
# RWC-P.zip を取り、中の RWC_P*.wav を scratch/real/rwc-p/ に置く
py tools/rwc_truth.py scratch/real/rwc-annotations scratch/real/rwc-p
.venv/Scripts/python tools/measure.py table rwc-p -v
```

Zenodo は 1 接続あたり 350 kB/s ほどに絞られる。`curl -r` で範囲を分けて並行に取ると速い。

変換の決まり（`tools/rwc_truth.py`）:

- 小節は「1 の拍」から次の「1 の拍」まで。最初の 1 より前の音は小節に入れず、数えない。
- 拍子は 1 小節の拍の数の最頻値（100 曲中 99 曲が 4/4、P034 だけ 5/4。読み取り器は 5/4 を書けない）。
  テンポは拍の間隔の中央値。
- コードは読み取り器の 8 種類に畳む: maj6・add9 → 長三和音、min9 → m7、9・13 → 7、sus2 → sus4、
  dim7 → dim、hdim7 → m7(b5)。`N`（コードなし）、増三和音、三度のない和音（パワーコード）は外し、
  その時間も分母から外す（`chordSeconds`）。外れるのは全体の 4% ほど。

### AIST Annotation の曲の構造（RWC-P の section の答え。2026-11-16 から）

| | |
|---|---|
| 中身 | `AIST_RWC-MDB-P-2001_CHORUS/RM-P*.CHORUS.TXT`: 開始・終了（10 ms 単位）・ラベル（`intro`・`verse A`・`chorus A`・`bridge A`・`pre-chorus`・`ending` …）。`AIST_RWC-MDB-P-2001_VOCA_INST` は歌と楽器の区間（未使用） |
| 置き場 | <https://github.com/rwc-music/rwc-annotations-archive>（未整理の注釈の保管庫。AIST の配布元 staff.aist.go.jp は 2026-11-16 に応答なし） |
| 条件 | 同梱の README: 研究以外に使わない、許可なく配らない、使ったことを明記する（Copyright (C) 2006 AIST） |
| 出典 | Goto "AIST Annotation for the RWC Music Database" (ISMIR 2006) |

**測るためだけに使い、注釈もその派生物も配らない。** section の境目の数え方は README の「Sections by meaning」、道具は `tools/aist_sections.py`。

```
git clone --depth 1 https://github.com/rwc-music/rwc-annotations-archive scratch/real/rwc-annotations-archive
.venv/Scripts/python tools/aist_sections.py rwc-dev      # rwc-test は数えるだけ
```

## 候補（まだ使っていない）

| データ | 中身 | 音源 | ライセンス | 見立て |
|---|---|---|---|---|
| GuitarSet v1.1.0 <https://zenodo.org/records/3371780> | ギター 1 本の 30 秒ほどの演奏 360 本。コード・拍・小節頭・調（JAMS） | あり（`audio_mono-mic.zip` 657 MB） | CC BY 4.0 | 伴奏だけ・独奏だけの録音。短い。条件が緩いので学習にも使える見込み |
| Schubert Winterreise Dataset v2.1 <https://zenodo.org/records/10839767> | 歌曲 24 曲 × 演奏。コード・小節・調 | 9 演奏のうち 2 つだけ同梱（517 MB） | CC BY 3.0（1 演奏は改変しての再配布不可） | 歌とピアノのクラシック。コード譜の用途からは遠い |
| AAM <https://zenodo.org/records/5794629> | アルゴリズム作曲の 3000 曲。拍・コード・調ほか | あり（ミックスだけで 44 GB） | CC BY 4.0 | 実録音ではない（実楽器のサンプルによる合成）。学習データを増やす候補 |
| JAAH <https://github.com/MTG/JAAH> | ジャズ 113 曲のコード・拍 | **なし**（注釈だけ） | 未確認 | 音源を別に手に入れる必要がある |
| Isophonics（Beatles ほか）、McGill Billboard | コード・拍の注釈 | **なし**（市販の音源が要る） | 注釈は研究用に公開 | ユーザーが CD を持っていれば使える |

注釈の形式・細かい条件で未確認のもの: GuitarSet の曲数とジャンル（配布ページに記載なし。論文では 360 本）、
AAM の注釈の形式、JAAH のライセンス。
