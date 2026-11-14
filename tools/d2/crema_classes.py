"""D2: where each of crema's 170 chord_tag classes stands in BTC's order (what crema_chords.py works out), as JSON."""
import json
import os
import sys
import warnings

os.environ.setdefault("TF_USE_LEGACY_KERAS", "1")
os.environ.setdefault("TF_CPP_MIN_LOG_LEVEL", "2")
warnings.filterwarnings("ignore")
import tf_keras
sys.modules["keras"] = tf_keras
for part in ("models", "layers", "backend", "utils"):
    sys.modules["keras." + part] = getattr(tf_keras, part)
from crema.models.chord import ChordModel

sys.path.insert(0, "tools")
from crema_chords import FLATS, QUALITIES, ROOTS

crema = ChordModel()
tags = [str(c) for c in crema.pump["chord_tag"].encoder.classes_]
names = [f"{r}:{q}" for r in ROOTS for q in QUALITIES] + ["X", "N"]
where = []
for tag in tags:
    root, _, quality = tag.partition(":")
    where.append(names.index(tag if tag in ("X", "N") else f"{FLATS.get(root, root)}:{quality or 'maj'}"))
assert sorted(where) == list(range(170))
json.dump({"tags": tags, "where": where}, open("scratch/d2/crema-classes.json", "w"))
print(len(tags), tags[:16])
