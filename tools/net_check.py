"""What dettato.exe (src/Dettato) hears, against what the tools in Python hear from the same recordings.

    scratch/d2/venv-onnx/Scripts/python tools/net_check.py data/synth/seed1 scratch/net/seed1 --heard scratch/chords-crema/seed1

The second folder is what `dettato ears <recordings> --out <folder> --cqt` wrote. Compared: the input of the
chord model (<name>.cqt.npy) with librosa's constant-Q transform as tools/d2/ears_onnx.py makes it, and, with
--heard, the chords' probabilities (<name>.npz) with that folder's; with --beats, the beats with that folder's.
"""
import argparse
import sys
from pathlib import Path

import numpy as np
import soundfile as sf

sys.path.insert(0, str(Path(__file__).resolve().parent / "d2"))
from ears_onnx import hcqt  # noqa: E402


def beats_of(path):
    return [(float(a), int(b)) for a, b in (line.split() for line in path.read_text().splitlines() if line.strip())]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("recordings", type=Path)
    p.add_argument("net", type=Path, help="what dettato ears wrote")
    p.add_argument("--heard", type=Path, help="a folder of <name>.npz from tools/crema_chords.py or tools/d2/ears_onnx.py")
    p.add_argument("--beats", type=Path, help="a folder of <name>.beats from tools/track_beats.py")
    p.add_argument("--songs", type=int)
    p.add_argument("--no-cqt", action="store_true")
    p.add_argument("-v", "--verbose", action="store_true")
    args = p.parse_args()

    files = sorted(args.recordings.glob("*.wav"))[: args.songs]
    loud_gaps, all_gaps, worst_loud, prob_gaps, same, frames = [], [], 0.0, [], 0, 0
    beat_count, beat_moved, beat_songs, line_songs = 0, 0, 0, 0
    for path in files:
        line = path.stem
        if not args.no_cqt:
            ours = np.load(args.net / (path.stem + ".cqt.npy"))
            signal, rate = sf.read(str(path), dtype="float64")
            theirs = hcqt(signal, rate)[0]
            if ours.shape != theirs.shape:
                sys.exit(f"{path.stem}: {ours.shape} here, {theirs.shape} from librosa")
            gap = np.abs(ours - theirs)
            loud = theirs > -60.0                       # where the floor at -80 dB and the rounding of float32 are far away
            loud_gaps.append(float(gap[loud].mean()))
            all_gaps.append(float(gap.mean()))
            worst_loud = max(worst_loud, float(gap[loud].max()))
            line += f": dB apart {gap[loud].mean():.4f} above -60 dB (worst {gap[loud].max():.2f}), {gap.mean():.4f} everywhere"
        if args.heard:
            ours = np.exp(np.load(args.net / (path.stem + ".npz"))["logp"].astype(np.float64))
            theirs = np.exp(np.load(args.heard / (path.stem + ".npz"))["logp"].astype(np.float64))
            if ours.shape != theirs.shape:
                sys.exit(f"{path.stem}: {ours.shape} probabilities here, {theirs.shape} there")
            gap = np.abs(ours - theirs).max(axis=1)
            prob_gaps.append(float(gap.mean()))
            agree = int((ours.argmax(1) == theirs.argmax(1)).sum())
            same += agree
            frames += len(ours)
            line += f"; probabilities apart {gap.mean():.4f} (worst {gap.max():.3f}), the likeliest chord the same in {agree}/{len(ours)}"
        if args.beats and (args.beats / (path.stem + ".beats")).exists():
            ours, theirs = beats_of(args.net / (path.stem + ".beats")), beats_of(args.beats / (path.stem + ".beats"))
            if len(ours) != len(theirs):
                beat_songs += 1
                line += f"; {len(ours)} beats here, {len(theirs)} there"
            else:
                moved = sum(1 for (a, _), (b, _) in zip(ours, theirs) if abs(a - b) > 1e-9)
                beat_count += len(ours)
                beat_moved += moved
                lines_differ = [n for (_, n) in ours] != [n for (_, n) in theirs]
                line_songs += lines_differ
                line += f"; {moved}/{len(ours)} beats moved" + (", bar lines differ" if lines_differ else "")
        if args.verbose:
            print(line)
    n = len(files)
    print(f"{n} recordings")
    if loud_gaps:
        print(f"the chord model's input: {np.mean(loud_gaps):.1e} dB apart above -60 dB (worst {worst_loud:.2f}), {np.mean(all_gaps):.1e} dB everywhere")
    if prob_gaps:
        print(f"the chords' probabilities: {np.mean(prob_gaps):.1e} apart; the likeliest chord the same in {same}/{frames} frames ({100 * same / frames:.2f}%)")
    if args.beats:
        print(f"the beats: {beat_songs} songs with another number of beats; in the rest {beat_moved}/{beat_count} moved, the bar lines differ in {line_songs}")


if __name__ == "__main__":
    main()
