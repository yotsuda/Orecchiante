# Lily# から Dettato へ（2026-11-16）: `audio` に最後の section 名が入る（REQUESTS 12）

表は [`REQUESTS.md`](REQUESTS.md)。オーナーの報告から見つかったもの。

## 0. オーナーの報告

> `C:\MyProj\Dettato\scratch\sec\phrase\ito.lys` を VS Code のプレビューで再生したけど、音源が鳴らず lys だけしか鳴らない。

## 1. 何が起きているか

`scratch/sec/phrase/` の 2 つの `.dettato.json` で、`audio` が録音の名前ではなく **1 文字** になっている
（2026-11-16 18:07 に書かれたもの）。

| ファイル | `audio` | `form` の最後 | 録音（あるはずの場所） |
|---|---|---|---|
| `ito.dettato.json` | `"G"` | `G` | `scratch/sec/ito.wav` |
| `mylife.dettato.json` | `"F"` | `F` | `scratch/sec/mylife.wav` |

`.lys` の 1 行目も同じ値を書いている: `// read from G by tools/read_chords.py`（mylife は `read from F`）。

Lily# は `audio` を `.dettato.json` のフォルダから読む（REQUESTS 6 の形）ので、`scratch/sec/phrase/G` を探し、
見つからないので録音を鳴らさず、楽譜だけを鳴らす。録音パネルには「G cannot be found (…\phrase\G)」と出て、
「Locate Recording…」で選び直せる。Lily# の側の振る舞いは取り決めどおり。

## 2. 原因（`tools/read_chords.py`、作業ツリーの未コミットの変更）

`read()` の中で、録音の名前を入れた変数 `name` が、section を各小節に書くループのループ変数として使われ、
最後の section 名で上書きされている。

```python
942:  name = Path(path).name                         # 録音のファイル名（ito.wav）
...
1203:     for bar, (name, k) in zip(whole, places):  # ここで name が section 名になる
1204:         bar["section"], bar["barOfSection"] = name, k
...
1234:     "audio": name,                             # 最後の section 名（G・F）が入る
...
1320:     f"// read from {result['audio']} by tools/read_chords.py",
```

1203〜1204 行は `git diff -- tools/read_chords.py` に `+` で出る行（HEAD の `read_chords.py` には無い）。
`form` の最後の section 名と `audio` の値が、2 曲とも一致することが裏づけ。

## 3. 頼みたいこと

1203 行のループ変数を別の名前にしてほしい（例 `for bar, (sec, k) in zip(whole, places):`）。あわせて、
`scratch/sec/phrase/` の 2 つの `.dettato.json` を書き直すか、`audio` を `"../ito.wav"`・`"../mylife.wav"`
にすれば、そのまま録音と一緒に鳴る（録音は 1 つ上のフォルダにある）。

`dettato.exe`（配る読み取り器）に同じ形が入る前に直っていれば、利用者のファイルには出ない。入った後なら、
`audio` が存在しないファイルを指す `.dettato.json` が配られることになる（Lily# は「見つからない」と言い、
選び直させるだけで、壊れはしない）。

Lily# の側は何も変えない。
