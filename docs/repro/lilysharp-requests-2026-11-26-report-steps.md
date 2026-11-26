# Orecchiante → Lily#: 報告（依頼 23）を `steps` の型でも（依頼 26）

2026-11-26、Orecchiante のセッションから。依頼 24 の `steps` が入ったので、Orecchiante 0.18.0（未公開）はドラムの型を
`steps` で書く。そのため、依頼 23 の「Report to Orecchiante…」のうち型を扱う所が、新しい譜面では働かなくなる。
入れるかどうか、どう入れるかは Lily# とオーナーの判断。

## 1. 0.18.0 の kit の段

```
part kit { clef percussion }
phrase beatA { steps { hh "x... x... x... x..." sn ".... x... .... x..." bd "x.x. ..x. x.x. ..x." } }
phrase beatB { steps { hh "x... x... x.x. x.xx" sn ".... x... .... x..." bd "x... x... ..x. ..x." } }
...
section A {
  ...
  kit {
    beatA | % | % | beatB |
    steps { hh "x... x... x... x..x" bd "x.x. ..x. x.x. xxx." } | beatA | % | r1 |
  }
}
```

- 型は 1 行の `phrase beatA { steps { … } }` だけになった（0.17.0 までの `hatsA`・`backA`・`kickA` と
  `phrase beatA { drums { hatsA backA kickA } }` はもう書かない）。
- 型に近くない小節は `steps { … }` をその場に書く。音のない小節は休符（`r1`）。
- 6/8 は `steps 4. { hh "x..x.. x..x.." … }`（1 拍 6 文字）。
- 隣のファイル（`.orecchiante.json`）の各小節の `drums` は `{ "hh": "x... x... x... x...", "sn": …, "bd": … }`
  （`steps` の行の中身）。形（太鼓ごとの文字列）は同じで、文字列の中身だけが変わった。`protocol` は 1 のまま。

## 2. 今の Lily# で起きること（`editors/vscode/src/orecchianteSeedCore.ts` を読んだ限り）

- `grooveOf` は `drums { a b c }` の形だけを型と見る。そのため:
  - `phrase beatA { steps { … } }` の行で Ctrl+. を押しても、**Report Phrase** が出ない（`caretPhrase` が、型から
    使われる phrase だけを返すため）。
  - **Report Bar・Report Section** は出る。ただ、型の小節（`beatA`、その後の `%`）の項目に `groove`・`lines` が付かない。
    `bar` が `"beatA"` だけになり、型の中身が報告に残らない。

## 3. 頼みたいこと

1. `phrase X { steps … { … } }` を型として扱う（kit の小節から名前で呼ばれる phrase）。
   - その行で **Report Phrase X to Orecchiante…** を出す。`phrase` の `body` は `steps { … }` の全文で、
     `grooves` は `[X]` 自身か空でよい（形は任せる）。
   - 型の小節の項目に `groove: "beatA"` と、`lines` として型の中身を付ける。中身は `{ "hh": "x... …", "sn": …, "bd": … }`
     （太鼓ごとの行の文字列）だと、隣のファイルの `drums` と同じ形になって比べやすい。`steps { … }` の全文 1 本でもよい。
2. 0.17.0 までの譜面（`drums { hatsA backA kickA }` の型）は今のまま働くこと（オーナーの手元に、この形で直した譜面がある）。

## 4. Orecchiante 側が確かめること

入ったら、0.18.0 で書いた譜面（`C:\MyProj\Orecchiante\scratch\e2e\steps\wab.lys` と、その隣の
`wab.orecchiante.json`）で、型の行と型の小節から報告を出す。seed.json に型の中身が入ることを確かめて `done` にする。
