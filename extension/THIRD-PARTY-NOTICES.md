# Third-party notices

Orecchiante contains the following third-party components. Each is used under its own
license, reproduced or referenced below.

## Beat This!

`bin/beat-this-final0-dynamo.onnx` — finds the beats and the downbeats. It is the checkpoint
`final0` of Beat This!, written out as ONNX, unchanged otherwise.
<https://github.com/CPJKU/beat_this>

MIT License

Copyright (c) 2024 Institute of Computational Perception, JKU Linz, Austria

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## crema

`bin/crema-chord.onnx` — says which chord is sounding. It is the chord model of crema 0.2.0,
written out as ONNX, unchanged otherwise. <https://github.com/bmcfee/crema>

BSD 2-Clause License

Copyright (c) 2017, Brian McFee
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

## BTC

`bin/btc-large-voca.onnx` — says which chord is sounding, with crema (their probabilities are averaged). It is
the large-vocabulary model of BTC (`test/btc_model_large_voca.pt`), written out as ONNX with the mean and the
deviation saved beside its weights, unchanged otherwise. <https://github.com/jayg996/BTC-ISMIR19>
The weights are in the repository, which is under the license below; its authors say nothing else about them.

MIT License

Copyright (c) 2019 Jonggwon Park

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Demucs

`bin/htdemucs_6s-mm8.onnx` — separates the recording into six parts (drums, bass, other, vocals, guitar, piano)
to hear one at a time, and hears where singing comes in and stops (for the sections). It is the network of
Demucs's `htdemucs_6s` (demucs 4.1.0), written out as ONNX without its Fourier transforms, which the reader does
itself, and with its matrix products held in 8-bit integers; otherwise unchanged.
<https://github.com/facebookresearch/demucs> The weights are those the package downloads; the package is under
the license below.

MIT License

Copyright (c) Meta Platforms, Inc. and affiliates.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## ReazonSpeech wav2vec 2.0 (Japanese)

`bin/reazon-kana-mm8.onnx` — hears which kana is being sung, to put a song's words to its notes. It is
reazon-research/japanese-wav2vec2-base-rs35kh, written out as ONNX with only its outputs for the kana kept
(after the softmax over all of them), and with its matrix products held in 8-bit integers; otherwise unchanged.
<https://huggingface.co/reazon-research/japanese-wav2vec2-base-rs35kh>

Published by reazon-research under the Apache License, Version 2.0 — the license of Orecchiante itself, whose
text is in `LICENSE`. The model was changed as said above.

## NMeCab

`bin/LibNMeCab.dll` — cuts Japanese words and reads them in kana (the words of a song, for the model above).
<https://github.com/komutan/NMeCab> Copyright Tsuyoshi Komuta, after MeCab by Taku Kudo.

It is used under the GNU Lesser General Public License, version 2.1 or later (its authors offer it under that
or the GPL): the text is in `licenses/LGPL-2.1.txt`. It is the unchanged library as its package
(LibNMeCab 0.10.2) has it, a file of its own that the reader loads; its source is at the address above, and it
may be replaced by another build of it.

## IPAdic

`bin/IpaDic/` — the dictionary NMeCab reads with, compiled as the package LibNMeCab.IpaDicBin 0.10.0 has it.
Copyright 2000, 2001, 2002, 2003 Nara Institute of Science and Technology, under the terms in
`bin/IpaDic/COPYING` (use, reproduction and distribution permitted with the notice).

## ONNX Runtime

`bin/onnxruntime.dll`, `bin/Microsoft.ML.OnnxRuntime.dll` — runs the models.
<https://github.com/microsoft/onnxruntime>

Copyright (c) Microsoft Corporation, under the MIT License. Its license and its own third-party
notices are in `bin/licenses/onnxruntime-LICENSE.txt` and `bin/licenses/onnxruntime-ThirdPartyNotices.txt`.

## NAudio

`bin/NAudio.Core.dll`, `bin/NAudio.Wasapi.dll` — the way to Windows' Media Foundation, which decodes the
recordings that are not plain `.wav` (mp3, m4a, flac and others). The decoders themselves are Windows' own
and are not in the package. <https://github.com/naudio/NAudio>

MIT License

Copyright 2020 Mark Heath

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## .NET runtime

The reader is published self-contained: the .NET runtime and libraries are in `bin/`.
Copyright (c) .NET Foundation and Contributors, under the MIT License. Their license and
third-party notices are in `bin/licenses/`.

## What the reader's arithmetic follows

The reader's constant-Q transform, onset strength, tuning and chroma are written in C# to give the
numbers librosa gives, following its arithmetic. <https://github.com/librosa/librosa>

ISC License

Copyright (c) 2013--2023, librosa development team.

Permission to use, copy, modify, and/or distribute this software for any purpose with or without
fee is hereby granted, provided that the above copyright notice and this permission notice appear
in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS
SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT,
NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE
OF THIS SOFTWARE.

## The icon

The extension's icon is the F clef of Emmentaler, LilyPond's music font.
Copyright (C) 1997-2023 Han-Wen Nienhuys, Jan Nieuwenhuizen, Werner Lemberg, and the LilyPond
project contributors. The font is dual-licensed; the icon uses it under the SIL Open Font License.
<https://gitlab.com/lilypond/lilypond>
