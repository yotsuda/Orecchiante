# Dettato から Lily# へ（2026-11-14）: 依頼 2 件、観察 1 件

表は [`REQUESTS.md`](REQUESTS.md)（1〜3）。長い説明はここ。

## 0. Dettato とは、いまある物

録音を、ローカルだけで、直せる `.lys`（コード譜）に書き起こす読み取り器。Calamo と同じ立ち位置で、Lily# には同梱しない。
方向づけは `C:\MyProj\Dettato\docs\ROADMAP.md`、現在地は同 `docs\HANDOFF.md`。

| 物 | 場所・状態 |
|---|---|
| 読み取り器 | `dettato.exe`（.NET 10、win-x64、ONNX Runtime）。`dettato read <x.wav> --out <フォルダ>` が `x.lys` と `x.dettato.json` を書く。3 分半の曲で 5.5 秒 |
| 終了コード | 0 = 書けた、1 = 読めなかった録音がある（理由は stderr）、2 = 引数の誤り。進み具合は stdout に英語の文で出る（機械向けの形ではない） |
| 入力 | **いまは `.wav` だけ。** mp3・m4a・flac・mp4 は次の作業（ROADMAP の D3）で足す |
| 横に置くファイル | `x.dettato.json`、形式 1。仕様は `docs\SIDECAR.md`。`bars[]` の n 番目が、Lily# が `form` を通して鳴らす n 番目の小節で、`start`・`end` は録音の頭からの秒 |
| 拡張 | `yotsuda.dettato` 0.1.0（Windows x64、113 MB）。**この PC に入れただけで、Marketplace には出していない。** `tools\deploy-extension.ps1` が作って入れる |
| 拡張が返すもの | `vscode.extensions.getExtension('yotsuda.dettato')` を `activate()` すると `{ path, version }`（`path` は `bin\dettato.exe`）。Windows x64 以外と `bin` がないときは `undefined`。起動は軽い（子プロセスなし） |
| 拡張のコマンド（仮） | `dettato.transcribe`「Dettato: Transcribe Recording to Lily# Chord Chart (.lys)… (Preview)」。wav を選ぶか右クリック → 一時フォルダに読む → 録音の隣に写す（既にある `.lys` は尋ねてから置き換える）→ `.lys` を開く |

書かれる `.lys` の形: `title`・`tempo`・`time`・`key`、`section <名前> { chords harmony { … } }`、繰り返しがあれば `form`、弱起は
`section Pickup { partial … }`、自信のない和音に `@todo`（`or F#?` のように別候補を言う）。どれも `lysc check` を通している（測定の 1388 曲）。

## 1. 依頼: 録音を `.lys` に変換するメニューを Lily# 本体に持ってほしい（REQUESTS 1）

**オーナー決定（2026-11-14）。** いまは Dettato の拡張が仮に持っている（上の表の最後の行）。理由:

- 変換の後の作業（プレビュー、`@todo` の表示、小節の原音を鳴らす）がすべて Lily# のもの。入口だけ別の拡張にあると、利用者が 2 つの拡張を意識する。
- Calamo の「Convert Image/PDF to Lily# (.lys)…」と並ぶ。
- Lily# なしでは `.lys` は描けず鳴らせないので、Dettato の拡張が単独で動く利点がない。

してほしいこと（形は Lily# 側とオーナーが決める。下は Calamo に倣った案）:

- コマンドパレットと、エクスプローラーの録音の右クリックに「Convert Recording to Lily# (.lys)…」に当たるもの。
- 読み取り器の場所は `yotsuda.dettato` の返すものから（Calamo の `yotsuda.calamo` と同じ探し方）。
- 既にある `.lys` を黙って上書きしない（直した譜面かもしれない）。

**順番。** Lily# にメニューが入るのと同時に、Dettato は拡張の `dettato.transcribe` を外す（二重に出さない）。それまでは残す。
Lily# のどの版から入るかを REQUESTS 1 の行に書いてもらえれば、Dettato 側はそれに合わせて外す。

## 2. 取り決めへの希望を先に聞かせてほしい（REQUESTS 1 の最初の一歩）

Dettato の次の作業（D3）で、コマンドの体裁を固める。**固める前に、Lily# 側が要るものを知りたい。** 行を `question` にして書いてもらえれば、D3 で入れる。
Dettato 側のいまの案:

| 項目 | 案 | 聞きたいこと |
|---|---|---|
| 呼び方 | `dettato <録音> [-o <x.lys>]`。`x.dettato.json` は `.lys` の隣。`read`・`ears`・`features` は測定用に残す | 出力先はファイルとフォルダのどちらが扱いやすいか。Calamo の `omr read` と揃えたい引数はあるか |
| 版 | `dettato --version --json` → `{ "version": "…", "protocol": 1 }`。パック時に `bin\dettato.json` に書き、拡張はそれを読んで `{ path, version, protocol }` を返す（Calamo と同じ） | これで足りるか |
| 進み具合 | stderr に人が読む文。全体で数秒〜十数秒なので、段階の名前（聞く・拍・和音・書く）を 1 行ずつ | 機械向けの形（`--progress` の JSON 行など）が要るか |
| 失敗 | 終了コード 1 と、stderr の最後の行に理由（英語 1 文） | 利用者に見せる文として使うか、Lily# 側で言い直すか |
| 取り消し | プロセスを止めるだけでよい（出力は最後にまとめて書くので、半端なファイルは残らないようにする） | |
| 入力の種類 | D3 で決める（wav のほか mp3・m4a・flac・mp4 を予定）。読める拡張子は `--version --json` に並べることもできる | メニューを出す条件（`when`）に、拡張子の一覧が要るか |
| メニューを出す鍵 | Calamo は起動時に `lilysharp.omrAvailable` を立てる | Dettato の拡張が立てる鍵の名前を決めてほしい（立てるなら起動は `onStartupFinished` にする） |

## 3. 依頼: 直しながら、選んだ小節の原音を鳴らせるようにしてほしい（REQUESTS 2）

Dettato の読みは下書きで、人が直す前提（実録音 100 曲で、和音のルートが合うのは 87%、小節線は 93%）。直すときに要るのは
「この小節は録音ではどう鳴っているか」で、`x.dettato.json` はそのためにある。

- `x.lys` の隣に `x.dettato.json` があり、その `audio` のファイルが同じフォルダにあれば、カーソルのある小節（または選んだ範囲）の
  `start`〜`end` の原音を鳴らす。Calamo の「Show Original Scan」の音の版。
- 小節の対応は**順番**: `bars[]` の n 番目 = Lily# が `form` を通して鳴らす n 番目の小節。同じ section が 3 回鳴れば 3 つある
  （どの回を鳴らすかは Lily# 側の判断。最初の回でよいと思う）。
- 人が小節を足したり消したりすると、順番はずれる。Calamo の「保存のたびに照合し直す」に当たる仕組みは Dettato にまだない。
  まずは「小節数が合わなくなったら鳴らさない／知らせる」でよいか、相談したい。

**その前に確かめてほしい 2 点**（Dettato 側は `lysc layout` が弱起を 1 小節に数えることしか確かめていない）:

1. `lilysharp/playback` の `Beats` は、`partial` の小節にも小節の頭（`A`）を立てるか。
2. `lilysharp/placeAt` は、弱起の小節に何番を返すか（`x.dettato.json` は弱起を `"bar": 0` と書き、次の小節が 1）。

Dettato 側が合わせた方が早いこと（弱起の番号の付け方、足してほしい項目）があれば、形式の番号を上げずに足せる範囲で合わせる。
いま `x.dettato.json` にないもの: 小節の中の拍の時刻、最後の和音がどこまで伸びるか。

## 4. 観察（急がない）: 和音の行のスロット数が、短い小節でも拍子の拍数で検査される（REQUESTS 3）

`partial` の短い小節の `chords` の行に、その小節の拍数だけスロットを書くと警告になる場合がある: 4/4 の 3 拍の弱起に、
拍ごとに違う和音を 3 スロットで書けない。スロットの数が、小節の長さではなく拍子の拍数（4）に当てはめられているように見える。
Dettato は、スロットの数が拍子の拍数を割り切らない弱起では、最後の和音 1 つだけを書いて避けている（`src\Dettato\Writer.cs` の
`Slots`）ので、困ってはいない。意図どおりなら `won't` で構わない。再現の `.lys` は付けていない（要るなら作る。行を `question` にしてほしい）。
