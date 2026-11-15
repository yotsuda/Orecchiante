"""How many samples Chromium's decodeAudioData (what Lily#'s preview plays an mp3 with) gives before ffmpeg's first one.

    .venv/Scripts/python tools/chrome_lag.py <recording> <ffmpeg's wav of it>

Puts the first 120 KB of the recording (from its first mp3 frame) in headless Chrome through the DevTools protocol
(tools/cdp.mjs; node 22 or later), decodes it there at the wav's rate, reads the first 3 seconds of the first channel
back, and finds the lag against ffmpeg's wav by cross-correlation. (`chrome --dump-dom` does not wait for the decoding.)
"""
import base64, subprocess, sys, tempfile, re
from pathlib import Path
import numpy as np
import soundfile as sf

rec, ref = Path(sys.argv[1]), Path(sys.argv[2])
x, rate = sf.read(ref, dtype="float32", always_2d=True)
x = x[:, 0]
raw = rec.read_bytes()
BR1 = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320]
BR2 = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160]


def flen(k):
    if raw[k] != 0xFF or (raw[k + 1] & 0xE0) != 0xE0 or (raw[k + 1] >> 1 & 3) != 1:
        return 0
    v, bi, si = raw[k + 1] >> 3 & 3, raw[k + 2] >> 4, raw[k + 2] >> 2 & 3
    if v == 1 or bi in (0, 15) or si == 3:
        return 0
    sr = [44100, 48000, 32000][si] >> (0 if v == 3 else 1 if v == 2 else 2)
    return (144000 * BR1[bi] // sr if v == 3 else 72000 * BR2[bi] // sr) + (raw[k + 2] >> 1 & 1)


start = 0
if rec.suffix.lower() == ".mp3":
    for k in range(len(raw) - 4):
        at, chain = k, 0
        while chain < 12 and at + 4 < len(raw) and (n := flen(at)):
            at, chain = at + n, chain + 1
        if chain == 12:
            start = k
            break
data = base64.b64encode(raw[start:start + 120_000]).decode()
expr = f"""(async () => {{
const b = Uint8Array.from(atob("{data}"), c => c.charCodeAt(0)).buffer;
const a = await new OfflineAudioContext(1, 1, {rate}).decodeAudioData(b);
const c = a.getChannelData(0).slice(0, {3 * rate});
const u = new Uint8Array(c.buffer); let s = ""; for (let i = 0; i < u.length; i += 8192) s += String.fromCharCode(...u.subarray(i, i + 8192));
return "OK " + a.sampleRate + " " + btoa(s);
}})()"""
d = Path(tempfile.mkdtemp())
(d / "e.js").write_text(expr, encoding="ascii")
out = subprocess.run(["node", str(Path(__file__).with_name("cdp.mjs")), str(d / "e.js")], capture_output=True, text=True, timeout=90).stdout
m = re.search(r"OK (\d+) ([A-Za-z0-9+/=]+)", out)
if not m:
    print(rec.name, "no decode, start", start, re.search(r"<pre id=.o.>(.{0,200})", out).group(1))
    sys.exit(1)
y = np.frombuffer(base64.b64decode(m.group(2)), dtype="float32")
n, most = min(len(y), 2 * rate), 4096
a, bb = x[:n], y[:n]
c = np.correlate(np.concatenate([np.zeros(most), bb, np.zeros(most)]), a, "valid")
lag = int(np.argmax(np.abs(c))) - most
print(f"{rec.name}: Chrome gives {lag:+d} samples ({1000 * lag / rate:+.1f} ms) against ffmpeg, rate {m.group(1)}")
