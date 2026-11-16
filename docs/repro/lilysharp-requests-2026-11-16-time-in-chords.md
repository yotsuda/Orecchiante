# Dettato から Lily# へ（2026-11-16）: コードの行の途中で拍子を変えたい（REQUESTS 13）

表は [`REQUESTS.md`](REQUESTS.md)。

## 0. オーナーの報告

糸（Bank Band）を section に分けていて、C3 の M64 は rit ではなく **6 拍の小節**（4 拍＋2 拍）だと分かった。
オーナー:「M65 に `time 2/4` をひとつ差し込んで直すのがよさそう」。

## 1. 今できること・できないこと（`lysc` master で試した）

コードの行の中に `time` も `partial` も書けない（LYS の chords は和音・`.`・`r`・小節線だけ）:

```
section A {
  chords harmony {
    G C | G C#m7(b5) | time 2/4 C#m7(b5) | time 4/4 C Dsus4 | C D break |
  }
}
```

→ `error: 'chords' takes chord symbols ('Am', 'G7'), '.', 'r'/'R' (N.C.) and barlines; 'time' is none of these.`
（`partial 2 C#m7(b5)` も同じ）

書けるのは section の頭の `time` だけ。2 拍の小節だけの section を作り、次の section で `time 4/4` に戻せば通る:

```
section A { chords harmony { G C | G C#m7(b5) | } }
section X { time 2/4  chords harmony { C#m7(b5) | } }
section B { time 4/4  chords harmony { C Dsus4 | C D | } }
form { A X B }
```

## 2. 頼みたいこと

**コードの行の中で、小節の頭に `time 2/4` のような拍子の変更を書けるようにしてほしい**（ノートの行の途中の `time` が
2026-10-06 から楽譜全体のものになったのと同じ扱い）。例: `| G C#m7(b5) | time 2/4 C#m7(b5) | time 4/4 C Dsus4 |`。

- なぜ: 歌ものには 2 拍や 6 拍の小節が 1 つだけ混じることがよくある。section の頭でしか書けないと、意味のない
  1 小節の section で曲の区切りを割ることになる（オーナーは section を「意味で切る」ものにしたい）。
- Dettato は、Beat This! の downbeat の確からしさから、2 拍ずつ組む向きが途中で入れ替わる所を見つけて
  2 拍の小節を書く予定（糸で、G の小節の頭の値が今の組み方で平均 2.3、2 拍ずらすと 3.5）。入るまでは §1 の
  section の形で書き、入ったら行の中の `time` に切り替える。
- `.dettato.json` の `bars` は今のまま（その小節の `beats` が 2 つになるだけ）。Lily# の `scoreBars` が演奏の拍から
  数えるなら、録音と一緒に鳴らす所は何も変わらないはず。

書き方（`time` を `|` の直後に限るか、拍子記号を紙にどう刷るか、`partial` も許すか）は Lily# とオーナーの判断。
