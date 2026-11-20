"""Do a chart's beats keep step with the recording to its end? Every four bars, the grid of eighth notes
the .orecchiante.json's beats make is moved up to half an eighth either way, to where the recording's onsets
fit it best (Lily#'s REQUESTS 15: the beats of My Life fell 2.4 ms a second behind the music).

  .venv/Scripts/python tools/beat_phase.py <recording> <x.orecchiante.json> [--scale K]

Prints the best shift (ms; minus: the music is earlier) along the song, and the slope of a line through
them (ms a second). --scale K divides every time of the chart by K first.
"""
import argparse, json, subprocess
import numpy as np, librosa


def main():
    p = argparse.ArgumentParser()
    p.add_argument("recording"); p.add_argument("chart"); p.add_argument("--scale", type=float, default=1.0)
    a = p.parse_args()
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", a.recording, "-ac", "1", "-ar", "22050", "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    y = np.frombuffer(raw, dtype=np.float32)
    hop = 110                                   # 5 ms
    env = librosa.onset.onset_strength(y=y, sr=22050, hop_length=hop)
    t_env = np.arange(len(env)) * hop / 22050
    bars = [b for b in json.loads(open(a.chart, encoding="utf-8").read())["bars"] if not b.get("pickup")]
    rows = []
    for i in range(0, len(bars) - 3, 4):
        grid = []
        for b in bars[i:i + 4]:
            beats = [x / a.scale for x in b["beats"]] + [b["end"] / a.scale]
            for s, e in zip(beats, beats[1:]):
                grid += [s, (s + e) / 2]
        grid = np.array(grid)
        eighth = float(np.median(np.diff(grid)))
        shifts = np.arange(-eighth / 2, eighth / 2, 0.002)
        score = [np.interp(grid + s, t_env, env).sum() for s in shifts]
        rows.append((grid[0], 1000 * shifts[int(np.argmax(score))], eighth))
    # unwrap across the eighth: a run that drifts passes from -half to +half
    out, prev, add = [], None, 0.0
    for t, s, e in rows:
        if prev is not None and abs(s + add - prev) > 1000 * e / 2:
            add += -1000 * e if s + add > prev else 1000 * e
        out.append((t, s + add)); prev = s + add
    for t, s in out:
        print(f"{t:7.1f} s  {s:+7.0f} ms")
    t, s = np.array(out).T
    k = np.polyfit(t, s, 1)
    print(f"slope {k[0]:+.2f} ms a second (scale {a.scale}), spread about the line {np.std(s - np.polyval(k, t)):.0f} ms")


if __name__ == "__main__":
    main()
